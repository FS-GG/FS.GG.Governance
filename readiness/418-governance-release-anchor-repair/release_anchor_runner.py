#!/usr/bin/env python3
"""Failure-atomic, non-publishing delivery control for Governance #418.

The live path is intentionally gated by --apply and an exact target confirmation.
Tests import execute with an in-memory operations adapter; they never mutate GitHub.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Protocol

TARGET = "388819d0e060c11f53e5ca2df3277a54a13f9e75"
TAG = "v1.12.1"
REPOSITORY = "FS-GG/FS.GG.Governance"
WORKFLOW_ID = 303994065


class Ops(Protocol):
    def workflow_state(self) -> str: ...
    def disable(self) -> None: ...
    def enable(self) -> None: ...
    def run_ids(self) -> list[int]: ...
    def tag_target(self) -> str | None: ...
    def push_tag(self) -> None: ...
    def commit_exists(self) -> bool: ...
    def sleep(self, seconds: float) -> None: ...


class LiveOps:
    @staticmethod
    def _run(*args: str) -> str:
        return subprocess.run(args, check=True, text=True, capture_output=True).stdout

    def workflow_state(self) -> str:
        value = json.loads(self._run("gh", "api", f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_ID}"))
        if value.get("id") != WORKFLOW_ID:
            raise RuntimeError(f"workflow id changed: {value.get('id')}")
        return value["state"]

    def disable(self) -> None:
        self._run("gh", "workflow", "disable", str(WORKFLOW_ID), "--repo", REPOSITORY)

    def enable(self) -> None:
        self._run("gh", "workflow", "enable", str(WORKFLOW_ID), "--repo", REPOSITORY)

    def run_ids(self) -> list[int]:
        pages = json.loads(self._run(
            "gh", "api", "--paginate", "--slurp",
            f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_ID}/runs?per_page=100",
        ))
        ids = sorted(item["id"] for page in pages for item in page["workflow_runs"])
        first = json.loads(self._run(
            "gh", "api", f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_ID}/runs?per_page=1"
        ))
        if len(ids) != first["total_count"]:
            raise RuntimeError(f"incomplete run census: enumerated={len(ids)} authority={first['total_count']}")
        return ids

    def tag_target(self) -> str | None:
        output = self._run("git", "ls-remote", "--tags", "origin", f"refs/tags/{TAG}").strip()
        return None if not output else output.split("\t", 1)[0]

    def push_tag(self) -> None:
        self._run("git", "push", "origin", f"{TARGET}:refs/tags/{TAG}")

    def commit_exists(self) -> bool:
        return subprocess.run(
            ["git", "cat-file", "-e", f"{TARGET}^{{commit}}"], capture_output=True
        ).returncode == 0

    @staticmethod
    def sleep(seconds: float) -> None:
        time.sleep(seconds)


@dataclass
class Receipt:
    schema: str = "fsgg.governance.release-anchor-delivery/v1"
    target: str = TARGET
    tag: str = TAG
    workflowId: int = WORKFLOW_ID
    baselineRunIds: list[int] = field(default_factory=list)
    steps: list[dict[str, object]] = field(default_factory=list)
    primaryError: str | None = None
    cleanupError: str | None = None
    workflowActiveAtExit: bool = False
    tagTargetAtExit: str | None = None
    runIdsAtExit: list[int] | None = None

    def step(self, name: str, **facts: object) -> None:
        self.steps.append({"name": name, **facts})


class DeliveryFailure(RuntimeError):
    def __init__(self, message: str, receipt: Receipt | None = None):
        super().__init__(message)
        self.receipt = receipt


def error_text(error: BaseException) -> str:
    return str(error) or error.__class__.__name__


def converge_runs(
    ops: Ops,
    baseline: list[int],
    receipt: Receipt,
    phase: str,
    *,
    interval: float,
    timeout: float,
    minimum_wait: float,
    stable_samples: int,
) -> list[int]:
    started = time.monotonic()
    stable = 0
    previous: list[int] | None = None
    while True:
        current = ops.run_ids()
        added_ids = sorted(set(current) - set(baseline))
        removed_ids = sorted(set(baseline) - set(current))
        if added_ids or removed_ids:
            raise DeliveryFailure(
                f"{phase}: publish workflow run set changed: "
                f"added={added_ids}; removed={removed_ids}"
            )
        stable = stable + 1 if current == previous else 1
        elapsed = time.monotonic() - started
        if elapsed >= minimum_wait and stable >= stable_samples:
            receipt.step(f"{phase}-converged", elapsedSeconds=round(elapsed, 3), samples=stable, runIds=current)
            return current
        if elapsed >= timeout:
            raise DeliveryFailure(f"{phase}: run set did not converge within {timeout}s")
        previous = current
        ops.sleep(interval)


def execute(
    ops: Ops,
    baseline: list[int],
    *,
    inject: Callable[[str], None] = lambda _: None,
    interval: float = 10,
    timeout: float = 180,
    minimum_wait: float = 60,
    stable_samples: int = 3,
    enable_attempts: int = 5,
) -> Receipt:
    receipt = Receipt(baselineRunIds=baseline)
    disable_attempted = False
    primary: BaseException | None = None
    cleanup: BaseException | None = None

    try:
        if not ops.commit_exists():
            raise DeliveryFailure(f"target commit is unreadable: {TARGET}")
        if ops.workflow_state() != "active":
            raise DeliveryFailure("publisher was not active at preflight")
        if ops.tag_target() is not None:
            raise DeliveryFailure(f"refs/tags/{TAG} already exists")
        observed = ops.run_ids()
        if observed != baseline:
            raise DeliveryFailure("live preflight run-id set differs from the reviewed baseline")
        receipt.step("preflight", workflowState="active", runIds=observed, tagPresent=False)

        disable_attempted = True
        ops.disable()
        receipt.step("disable-requested")
        inject("after-disable-request")
        state = ops.workflow_state()
        if state != "disabled_manually":
            raise DeliveryFailure(f"publisher did not disable: {state}")
        receipt.step("disabled-verified", workflowState=state)
        inject("after-disabled-verification")

        ops.push_tag()
        receipt.step("tag-pushed", target=TARGET)
        inject("after-tag-push")
        tag_target = ops.tag_target()
        if tag_target != TARGET:
            raise DeliveryFailure(f"tag target mismatch: {tag_target}")
        receipt.step("tag-verified", target=tag_target)
        inject("after-tag-verification")

        converge_runs(
            ops, baseline, receipt, "disabled-run-set", interval=interval, timeout=timeout,
            minimum_wait=minimum_wait, stable_samples=stable_samples,
        )
        inject("after-disabled-convergence")
    except BaseException as error:
        primary = error
    finally:
        if disable_attempted:
            last: BaseException | None = None
            first_cleanup_error: BaseException | None = None
            for attempt in range(1, enable_attempts + 1):
                try:
                    ops.enable()
                    receipt.step("enable-requested", attempt=attempt)
                    inject("after-enable-request")
                    state = ops.workflow_state()
                    if state != "active":
                        raise DeliveryFailure(f"publisher state after enable attempt {attempt}: {state}")
                    receipt.step("active-verified", attempt=attempt, workflowState=state)
                    inject("after-active-verification")
                    last = None
                    break
                except BaseException as error:
                    first_cleanup_error = first_cleanup_error or error
                    last = error
                    receipt.step("enable-attempt-failed", attempt=attempt, error=error_text(error))
                    try:
                        ops.sleep(min(2 ** (attempt - 1), 10))
                    except BaseException as backoff_error:
                        first_cleanup_error = first_cleanup_error or backoff_error
                        last = backoff_error
                        receipt.step(
                            "enable-backoff-failed", attempt=attempt,
                            error=error_text(backoff_error),
                        )
            cleanup = last
            if cleanup is None and first_cleanup_error is not None and primary is None:
                primary = first_cleanup_error

    if primary is None and cleanup is None:
        try:
            receipt.runIdsAtExit = converge_runs(
                ops, baseline, receipt, "enabled-run-set", interval=interval, timeout=timeout,
                minimum_wait=minimum_wait, stable_samples=stable_samples,
            )
            inject("after-enabled-convergence")
        except BaseException as error:
            primary = error

    try:
        receipt.workflowActiveAtExit = ops.workflow_state() == "active"
    except BaseException as error:
        cleanup = cleanup or error
    try:
        receipt.tagTargetAtExit = ops.tag_target()
    except BaseException as error:
        cleanup = cleanup or error
    if receipt.runIdsAtExit is None:
        try:
            receipt.runIdsAtExit = ops.run_ids()
        except BaseException as error:
            cleanup = cleanup or error

    receipt.primaryError = None if primary is None else error_text(primary)
    receipt.cleanupError = None if cleanup is None else error_text(cleanup)
    if primary is not None or cleanup is not None or not receipt.workflowActiveAtExit:
        detail = f"primary={receipt.primaryError or 'none'}; cleanup={receipt.cleanupError or 'none'}; active={receipt.workflowActiveAtExit}"
        raise DeliveryFailure(detail, receipt)
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--confirm-target")
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--receipt", required=True)
    parser.add_argument("--inject-failure", choices=(
        "after-disable-request", "after-disabled-verification", "after-tag-push",
        "after-tag-verification", "after-disabled-convergence", "after-enable-request",
        "after-active-verification", "after-enabled-convergence",
    ))
    args = parser.parse_args()
    if not args.apply or args.confirm_target != TARGET:
        parser.error(f"live delivery requires --apply --confirm-target {TARGET}")
    source = json.loads(Path(args.baseline).read_text())
    baseline = source["workflow"]["runCensus"]["runIds"]
    fired = False

    def inject(point: str) -> None:
        nonlocal fired
        if args.inject_failure == point and not fired:
            fired = True
            raise DeliveryFailure(f"injected failure: {point}")

    receipt: Receipt | None = None
    try:
        receipt = execute(LiveOps(), baseline, inject=inject)
        return 0
    except DeliveryFailure as error:
        receipt = error.receipt or Receipt(baselineRunIds=baseline, primaryError=str(error))
        raise
    finally:
        Path(args.receipt).write_text(json.dumps(receipt.__dict__, indent=2, sort_keys=True) + "\n")


if __name__ == "__main__":
    raise SystemExit(main())
