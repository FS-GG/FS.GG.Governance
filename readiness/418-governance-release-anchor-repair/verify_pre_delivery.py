#!/usr/bin/env python3
"""Read-only pre-delivery checks for Governance #418; emits JUnit XML."""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import json
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

TARGET = "388819d0e060c11f53e5ca2df3277a54a13f9e75"
REPOSITORY = "FS-GG/FS.GG.Governance"
WORKFLOW_ID = 303994065
PACKAGES = (
    ("fs.gg.governance.cli", "FS.GG.Governance.Cli.nuspec", 174,
     "9cb856b0db677510cd07e0ff9d6fad8c9b7895fa40d2b1f74e3f4aa2db56b56e"),
    ("fs.gg.governance.fsharpsurfacecommand", "FS.GG.Governance.FSharpSurfaceCommand.nuspec", 38,
     "06db164952a6b07b967c6386a9a44a641f7a64d0a53159966022cfd868d1ea7a"),
)


def run(*args: str) -> str:
    return subprocess.run(args, check=True, text=True, capture_output=True).stdout


def download(url: str, target: Path, token: str | None = None) -> None:
    command = ["curl", "-fsSL"]
    if token:
        command += ["-H", f"Authorization: Bearer {token}"]
    command += [url, "-o", str(target)]
    subprocess.run(command, check=True, text=True, capture_output=True)


def unsigned_tree(package: Path) -> tuple[int, str, str]:
    with zipfile.ZipFile(package) as archive:
        names = sorted(name for name in archive.namelist() if name != ".signature.p7s")
        manifest = "".join(
            f"{hashlib.sha256(archive.read(name)).hexdigest()}  {name}\n" for name in names
        ).encode()
        nuspec = next(name for name in names if name.endswith(".nuspec"))
        return len(names), hashlib.sha256(manifest).hexdigest(), archive.read(nuspec).decode()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--junit", required=True)
    parser.add_argument("--package-cache", help="Optional cache directory for repeated negative-control runs.")
    parser.add_argument(
        "--mutation",
        choices=(
            "commit", "tags", "tags-positive", "tags-unreadable", "workflow",
            "runs", "runs-positive", "runs-unreadable", "cli-package",
            "surface-package", "package-nonvacuity", "package-unreadable",
        ),
        help="Test-only negative control; the named gate must reject.",
    )
    args = parser.parse_args()
    cases: list[tuple[str, float, str | None]] = []

    def check(name: str, operation) -> None:
        started = time.monotonic()
        try:
            operation()
            cases.append((name, time.monotonic() - started, None))
        except Exception as error:  # the JUnit record must survive a red assertion
            detail = str(error) or error.__class__.__name__
            cases.append((name, time.monotonic() - started, detail))

    commit_target = "0" * 40 if args.mutation == "commit" else TARGET
    check("target commit exists", lambda: run("git", "cat-file", "-e", f"{commit_target}^{{commit}}"))

    def tags() -> None:
        remote = "missing-fsgg-418-remote" if args.mutation == "tags-unreadable" else "origin"
        lines = run("git", "ls-remote", "--tags", remote).splitlines()
        refs = {line.split("\t", 1)[1]: line.split("\t", 1)[0] for line in lines}
        expected_count = 21 if args.mutation == "tags" else 20
        expected_positive = "0" * 40 if args.mutation == "tags-positive" else "05279a3092294b0765b1d1ce82d53b3432520362"
        assert len(refs) == expected_count, f"expected complete {expected_count}-ref baseline, got {len(refs)}"
        assert refs.get("refs/tags/v1.12.0") == expected_positive, "known-present v1.12.0 control did not match"
        assert "refs/tags/v1.12.1" not in refs

    check("remote tag census has positive and negative controls", tags)

    def workflow() -> None:
        value = json.loads(run("gh", "api", f"repos/{REPOSITORY}/actions/workflows/publish.yml"))
        assert value["id"] == WORKFLOW_ID
        expected_state = "disabled_manually" if args.mutation == "workflow" else "active"
        assert value["state"] == expected_state, f"expected workflow state {expected_state}, got {value['state']}"
        historical = run("git", "show", f"{TARGET}:.github/workflows/publish.yml")
        assert "tags: ['v*']" in historical

    check("publisher is active and historical workflow matches v-star tags", workflow)

    def runs() -> None:
        workflow_id = 0 if args.mutation == "runs-unreadable" else WORKFLOW_ID
        pages = json.loads(run(
            "gh", "api", "--paginate", "--slurp",
            f"repos/{REPOSITORY}/actions/workflows/{workflow_id}/runs?per_page=100",
        ))
        values = [item for page in pages for item in page["workflow_runs"]]
        first = json.loads(run(
            "gh", "api", f"repos/{REPOSITORY}/actions/workflows/{workflow_id}/runs?per_page=1"
        ))
        expected_count = 37 if args.mutation == "runs" else 36
        assert len(values) == first["total_count"] == expected_count
        expected_positive = 2 if args.mutation == "runs-positive" else 1
        assert len([item for item in values if item["head_branch"] == "v1.12.0"]) == expected_positive
        assert not [item for item in values if item["head_branch"] == "v1.12.1"]
        at_target = [item for item in values if item["head_sha"] == TARGET]
        assert [(item["id"], item["event"]) for item in at_target] == [(31563788761, "workflow_dispatch")]

    check("complete workflow-run census contains no repair-tag run", runs)

    token = run("gh", "auth", "token").strip()
    package_context = (
        contextlib.nullcontext(Path(args.package_cache))
        if args.package_cache else tempfile.TemporaryDirectory(prefix="fsgg-418-packages-")
    )
    with package_context as directory:
        root = Path(directory)
        root.mkdir(parents=True, exist_ok=True)
        for package_id, nuspec_name, expected_count, expected_digest in PACKAGES:
            def package_check(package_id=package_id, nuspec_name=nuspec_name,
                              expected_count=expected_count, expected_digest=expected_digest) -> None:
                if args.mutation == "cli-package" and package_id == "fs.gg.governance.cli":
                    expected_digest = "0" * 64
                if args.mutation == "surface-package" and package_id == "fs.gg.governance.fsharpsurfacecommand":
                    expected_digest = "0" * 64
                if args.mutation == "package-nonvacuity" and package_id == "fs.gg.governance.cli":
                    expected_count = 0
                request_id = package_id
                if args.mutation == "package-unreadable" and package_id == "fs.gg.governance.cli":
                    request_id = "fs.gg.governance.missing-418"
                github = root / f"github-{request_id}.nupkg"
                public = root / f"public-{request_id}.nupkg"
                if not github.exists():
                    download(
                        f"https://nuget.pkg.github.com/FS-GG/download/{request_id}/1.12.1/{request_id}.1.12.1.nupkg",
                        github, token,
                    )
                if not public.exists():
                    download(
                        f"https://api.nuget.org/v3-flatcontainer/{request_id}/1.12.1/{request_id}.1.12.1.nupkg",
                        public,
                    )
                github_tree = unsigned_tree(github)
                public_tree = unsigned_tree(public)
                assert github_tree[:2] == public_tree[:2] == (expected_count, expected_digest)
                assert f'<repository type="git" url="https://github.com/{REPOSITORY}" commit="{TARGET}" />' in github_tree[2]
                assert f'<repository type="git" url="https://github.com/{REPOSITORY}" commit="{TARGET}" />' in public_tree[2]
                with zipfile.ZipFile(github) as archive:
                    assert ".signature.p7s" not in archive.namelist()
                    assert nuspec_name in archive.namelist()
                with zipfile.ZipFile(public) as archive:
                    assert ".signature.p7s" in archive.namelist()

            check(f"{package_id} feed provenance and unsigned payload match", package_check)

    suite = ET.Element(
        "testsuite",
        name="Governance418PreDelivery",
        tests=str(len(cases)),
        failures=str(sum(failure is not None for _, _, failure in cases)),
        errors="0",
        skipped="0",
        time=f"{sum(duration for _, duration, _ in cases):.3f}",
    )
    properties = ET.SubElement(suite, "properties")
    ET.SubElement(properties, "property", name="targetCommit", value=TARGET)
    for name, duration, failure in cases:
        case = ET.SubElement(suite, "testcase", name=name, time=f"{duration:.3f}")
        if failure is not None:
            ET.SubElement(case, "failure", message=failure).text = failure
    Path(args.junit).parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(suite).write(args.junit, encoding="utf-8", xml_declaration=True)
    return 1 if any(failure is not None for _, _, failure in cases) else 0


if __name__ == "__main__":
    raise SystemExit(main())
