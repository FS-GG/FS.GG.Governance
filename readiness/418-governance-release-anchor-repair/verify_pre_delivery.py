#!/usr/bin/env python3
"""Read-only pre-delivery checks for Governance #418; emits JUnit XML."""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import json
import shutil
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


def mutate_package(package: Path, mutation: str) -> None:
    """Apply a production-shaped mutation to a disposable package subject."""
    if mutation == "unreadable":
        package.write_bytes(b"not-a-zip")
        return
    if mutation == "empty":
        with zipfile.ZipFile(package, "w"):
            pass
        return
    with zipfile.ZipFile(package) as archive:
        entries = [(info.filename, archive.read(info.filename)) for info in archive.infolist()]
    if mutation == "payload":
        candidate = next(name for name, _ in entries if not name.endswith(".nuspec") and name != ".signature.p7s")
        entries = [(name, data + b"\0" if name == candidate else data) for name, data in entries]
    elif mutation == "provenance":
        entries = [
            (name, data.replace(TARGET.encode(), ("0" * 40).encode()) if name.endswith(".nuspec") else data)
            for name, data in entries
        ]
    else:
        raise ValueError(f"unknown package mutation: {mutation}")
    with zipfile.ZipFile(package, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, data in entries:
            archive.writestr(name, data)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--junit", required=True)
    parser.add_argument("--package-cache", help="Optional cache directory for repeated negative-control runs.")
    parser.add_argument(
        "--mutation",
        choices=(
            "commit-missing", "commit-unreadable",
            "tags-target-present", "tags-positive-missing", "tags-unreadable", "tags-empty",
            "workflow-disabled", "workflow-trigger-missing", "workflow-unreadable",
            "runs-target-present", "runs-positive-missing", "runs-unreadable", "runs-empty",
            "cli-payload", "cli-provenance", "cli-unreadable", "cli-empty",
            "surface-payload", "surface-provenance", "surface-unreadable", "surface-empty",
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

    def commit() -> None:
        exists = subprocess.run(
            ["git", "cat-file", "-e", f"{TARGET}^{{commit}}"], capture_output=True
        ).returncode == 0
        if args.mutation == "commit-missing":
            exists = False
        if args.mutation == "commit-unreadable":
            raise RuntimeError("injected unreadable commit subject")
        assert exists, f"target commit is absent: {TARGET}"

    check("target commit exists", commit)

    def tags() -> None:
        lines = run("git", "ls-remote", "--tags", "origin").splitlines()
        refs = {line.split("\t", 1)[1]: line.split("\t", 1)[0] for line in lines}
        if args.mutation == "tags-target-present":
            refs.pop(next(ref for ref in refs if ref not in ("refs/tags/v1.12.0", "refs/tags/v1.12.1")))
            refs["refs/tags/v1.12.1"] = TARGET
        elif args.mutation == "tags-positive-missing":
            positive = refs.pop("refs/tags/v1.12.0", None)
            refs["refs/tags/subject-positive-removed"] = positive or TARGET
        elif args.mutation == "tags-unreadable":
            raise RuntimeError("injected unreadable tag subject")
        elif args.mutation == "tags-empty":
            refs.clear()
        assert len(refs) == 20, f"expected complete 20-ref baseline, got {len(refs)}"
        assert refs.get("refs/tags/v1.12.0") == "05279a3092294b0765b1d1ce82d53b3432520362", "known-present v1.12.0 control did not match"
        assert "refs/tags/v1.12.1" not in refs

    check("remote tag census has positive and negative controls", tags)

    def workflow() -> None:
        value = json.loads(run("gh", "api", f"repos/{REPOSITORY}/actions/workflows/publish.yml"))
        historical = run("git", "show", f"{TARGET}:.github/workflows/publish.yml")
        if args.mutation == "workflow-disabled":
            value["state"] = "disabled_manually"
        elif args.mutation == "workflow-trigger-missing":
            historical = historical.replace("tags: ['v*']", "tags: ['never']")
        elif args.mutation == "workflow-unreadable":
            raise RuntimeError("injected unreadable workflow subject")
        assert value["id"] == WORKFLOW_ID
        assert value["state"] == "active", f"expected workflow state active, got {value['state']}"
        assert "tags: ['v*']" in historical

    check("publisher is active and historical workflow matches v-star tags", workflow)

    def runs() -> None:
        pages = json.loads(run(
            "gh", "api", "--paginate", "--slurp",
            f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_ID}/runs?per_page=100",
        ))
        values = [item for page in pages for item in page["workflow_runs"]]
        first = json.loads(run(
            "gh", "api", f"repos/{REPOSITORY}/actions/workflows/{WORKFLOW_ID}/runs?per_page=1"
        ))
        if args.mutation == "runs-target-present":
            index = next(i for i, item in enumerate(values) if item["head_branch"] != "v1.12.0" and item["head_sha"] != TARGET)
            values[index] = {"id": 99999999999, "head_branch": "v1.12.1", "head_sha": TARGET, "event": "push"}
        elif args.mutation == "runs-positive-missing":
            for item in values:
                if item["head_branch"] == "v1.12.0":
                    item["head_branch"] = "subject-positive-removed"
        elif args.mutation == "runs-unreadable":
            raise RuntimeError("injected unreadable run-census subject")
        elif args.mutation == "runs-empty":
            values = []
            first["total_count"] = 0
        assert len(values) == first["total_count"] == 36
        assert len([item for item in values if item["head_branch"] == "v1.12.0"]) == 1
        assert not [item for item in values if item["head_branch"] == "v1.12.1"]
        at_target = [item for item in values if item["head_sha"] == TARGET]
        assert [(item["id"], item["event"]) for item in at_target] == [(31563788761, "workflow_dispatch")]

    check("complete workflow-run census contains no repair-tag run", runs)

    token = run("gh", "auth", "token").strip()
    package_cache_context = (
        contextlib.nullcontext(Path(args.package_cache)) if args.package_cache
        else tempfile.TemporaryDirectory(prefix="fsgg-418-package-cache-")
    )
    with package_cache_context as cache_directory, tempfile.TemporaryDirectory(prefix="fsgg-418-package-subjects-") as subject_directory:
        cache = Path(cache_directory)
        root = Path(subject_directory)
        cache.mkdir(parents=True, exist_ok=True)
        for package_id, nuspec_name, expected_count, expected_digest in PACKAGES:
            def package_check(package_id=package_id, nuspec_name=nuspec_name,
                              expected_count=expected_count, expected_digest=expected_digest) -> None:
                github_source = cache / f"github-{package_id}.nupkg"
                public_source = cache / f"public-{package_id}.nupkg"
                if not github_source.exists():
                    download(
                        f"https://nuget.pkg.github.com/FS-GG/download/{package_id}/1.12.1/{package_id}.1.12.1.nupkg",
                        github_source, token,
                    )
                if not public_source.exists():
                    download(
                        f"https://api.nuget.org/v3-flatcontainer/{package_id}/1.12.1/{package_id}.1.12.1.nupkg",
                        public_source,
                    )
                github = root / f"github-{package_id}.nupkg"
                public = root / f"public-{package_id}.nupkg"
                shutil.copy2(github_source, github)
                shutil.copy2(public_source, public)
                prefix = "cli" if package_id == "fs.gg.governance.cli" else "surface"
                if args.mutation and args.mutation.startswith(prefix + "-"):
                    package_mutation = args.mutation.removeprefix(prefix + "-")
                    mutate_package(github, package_mutation)
                    if package_mutation == "provenance":
                        mutate_package(public, package_mutation)
                github_tree = unsigned_tree(github)
                public_tree = unsigned_tree(public)
                assert github_tree[0] > 0 and public_tree[0] > 0
                assert f'<repository type="git" url="https://github.com/{REPOSITORY}" commit="{TARGET}" />' in github_tree[2]
                assert f'<repository type="git" url="https://github.com/{REPOSITORY}" commit="{TARGET}" />' in public_tree[2]
                with zipfile.ZipFile(github) as archive:
                    assert ".signature.p7s" not in archive.namelist()
                    assert nuspec_name in archive.namelist()
                with zipfile.ZipFile(public) as archive:
                    assert ".signature.p7s" in archive.namelist()
                assert github_tree[:2] == public_tree[:2] == (expected_count, expected_digest)

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
