#!/usr/bin/env python3
"""Failure-injection and convergence controls for release_anchor_runner.py."""

from __future__ import annotations

import argparse
import time
import xml.etree.ElementTree as ET
from pathlib import Path

from release_anchor_runner import DeliveryFailure, TARGET, execute

BASELINE = [11, 22, 33]
POINTS = (
    "after-disable-request", "after-disabled-verification", "after-tag-push",
    "after-tag-verification", "after-disabled-convergence", "after-enable-request",
    "after-active-verification", "after-enabled-convergence",
)


class FakeOps:
    def __init__(self, *, enable_failures: int = 0, new_run_after: int | None = None):
        self.state = "active"
        self.tag: str | None = None
        self.enable_failures = enable_failures
        self.enable_attempts = 0
        self.run_reads = 0
        self.new_run_after = new_run_after

    def workflow_state(self) -> str: return self.state
    def disable(self) -> None: self.state = "disabled_manually"
    def enable(self) -> None:
        self.enable_attempts += 1
        if self.enable_attempts <= self.enable_failures:
            raise RuntimeError(f"transient enable failure {self.enable_attempts}")
        self.state = "active"
    def run_ids(self) -> list[int]:
        self.run_reads += 1
        return BASELINE + ([44] if self.new_run_after is not None and self.run_reads >= self.new_run_after else [])
    def tag_target(self) -> str | None: return self.tag
    def push_tag(self) -> None: self.tag = TARGET
    def commit_exists(self) -> bool: return True
    def sleep(self, _seconds: float) -> None: return None


def fast_execute(ops: FakeOps, inject=lambda _: None):
    return execute(
        ops, BASELINE, inject=inject, interval=0, timeout=1,
        minimum_wait=0, stable_samples=2, enable_attempts=5,
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--junit", required=True)
    args = parser.parse_args()
    cases: list[tuple[str, float, str | None]] = []

    def check(name, operation) -> None:
        started = time.monotonic()
        try:
            operation()
            cases.append((name, time.monotonic() - started, None))
        except Exception as error:
            cases.append((name, time.monotonic() - started, str(error) or error.__class__.__name__))

    for point in POINTS:
        def failure_control(point=point) -> None:
            ops = FakeOps()
            fired = False
            def inject(current: str) -> None:
                nonlocal fired
                if current == point and not fired:
                    fired = True
                    raise RuntimeError(f"injected failure: {point}")
            try:
                fast_execute(ops, inject)
                raise AssertionError(f"{point} unexpectedly passed")
            except DeliveryFailure as error:
                assert f"injected failure: {point}" in str(error)
                assert error.receipt is not None
                assert error.receipt.primaryError == f"injected failure: {point}"
                assert error.receipt.workflowActiveAtExit is True
                assert ops.state == "active"
                assert ops.enable_attempts >= 1
        check(f"failure injection {point} preserves error and restores active", failure_control)

    def enable_retry() -> None:
        ops = FakeOps(enable_failures=2)
        try:
            fast_execute(ops)
            raise AssertionError("recovered enable disturbance unexpectedly reported success")
        except DeliveryFailure as error:
            assert ops.enable_attempts == 3
            assert error.receipt is not None and error.receipt.workflowActiveAtExit is True
            assert error.receipt.primaryError == "transient enable failure 1"
            assert error.receipt.cleanupError is None
    check("transient enable failures are retried, verified, and reported", enable_retry)

    def primary_and_cleanup() -> None:
        ops = FakeOps(enable_failures=99)
        def inject(point: str) -> None:
            if point == "after-tag-push":
                raise RuntimeError("primary-tag-check-failure")
        try:
            fast_execute(ops, inject)
            raise AssertionError("persistent cleanup failure unexpectedly passed")
        except DeliveryFailure as error:
            assert str(error).startswith("primary=primary-tag-check-failure;")
            assert "cleanup=transient enable failure 5" in str(error)
            assert error.receipt is not None and error.receipt.workflowActiveAtExit is False
    check("primary failure is preserved when cleanup also exhausts", primary_and_cleanup)

    def delayed_run() -> None:
        ops = FakeOps(new_run_after=3)
        try:
            fast_execute(ops)
            raise AssertionError("delayed run unexpectedly accepted")
        except DeliveryFailure as error:
            assert "new publish workflow run ids observed: [44]" in str(error)
            assert error.receipt is not None and error.receipt.workflowActiveAtExit is True
    check("asynchronous new run is rejected before re-enable and cleanup restores active", delayed_run)

    def delayed_run_after_enable() -> None:
        ops = FakeOps(new_run_after=5)
        try:
            fast_execute(ops)
            raise AssertionError("post-enable delayed run unexpectedly accepted")
        except DeliveryFailure as error:
            assert "enabled-run-set: new publish workflow run ids observed: [44]" in str(error)
            assert error.receipt is not None and error.receipt.workflowActiveAtExit is True
    check("asynchronous new run is rejected after verified re-enable", delayed_run_after_enable)

    def changed_baseline() -> None:
        ops = FakeOps(new_run_after=1)
        try:
            fast_execute(ops)
            raise AssertionError("changed preflight baseline unexpectedly accepted")
        except DeliveryFailure as error:
            assert "live preflight run-id set differs" in str(error)
            assert error.receipt is not None and error.receipt.workflowActiveAtExit is True
            assert ops.enable_attempts == 0 and ops.tag is None
    check("changed baseline refuses before disable and leaves live state untouched", changed_baseline)

    suite = ET.Element(
        "testsuite", name="Governance418ReleaseAnchorRunnerControls",
        tests=str(len(cases)), failures=str(sum(f is not None for _, _, f in cases)),
        errors="0", skipped="0", time=f"{sum(d for _, d, _ in cases):.3f}",
    )
    for name, duration, failure in cases:
        case = ET.SubElement(suite, "testcase", name=name, time=f"{duration:.3f}")
        if failure is not None:
            ET.SubElement(case, "failure", message=failure).text = failure
    Path(args.junit).parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(suite).write(args.junit, encoding="utf-8", xml_declaration=True)
    return 1 if any(f is not None for _, _, f in cases) else 0


if __name__ == "__main__":
    raise SystemExit(main())
