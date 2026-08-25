#!/usr/bin/env python3
"""Proves every live pre-delivery gate rejects its bounded mutation."""

from __future__ import annotations

import argparse
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path

MUTATIONS = {
    "commit": "target commit exists",
    "tags": "remote tag census has positive and negative controls",
    "tags-positive": "remote tag census has positive and negative controls",
    "tags-unreadable": "remote tag census has positive and negative controls",
    "workflow": "publisher is active and historical workflow matches v-star tags",
    "runs": "complete workflow-run census contains no repair-tag run",
    "runs-positive": "complete workflow-run census contains no repair-tag run",
    "runs-unreadable": "complete workflow-run census contains no repair-tag run",
    "cli-package": "fs.gg.governance.cli feed provenance and unsigned payload match",
    "surface-package": "fs.gg.governance.fsharpsurfacecommand feed provenance and unsigned payload match",
    "package-nonvacuity": "fs.gg.governance.cli feed provenance and unsigned payload match",
    "package-unreadable": "fs.gg.governance.cli feed provenance and unsigned payload match",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--junit", required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    verifier = root / "verify_pre_delivery.py"
    cases: list[tuple[str, float, str | None]] = []
    with tempfile.TemporaryDirectory(prefix="fsgg-418-control-cache-") as cache:
        for mutation, expected_case in MUTATIONS.items():
            started = time.monotonic()
            failure: str | None = None
            report = Path(cache) / f"{mutation}.xml"
            result = subprocess.run(
                ["python3", str(verifier), "--junit", str(report),
                 "--package-cache", cache, "--mutation", mutation],
                text=True, capture_output=True,
            )
            try:
                assert result.returncode == 1, f"expected verifier exit 1, got {result.returncode}: {result.stderr}"
                suite = ET.parse(report).getroot()
                assert suite.attrib["tests"] == "6"
                failures = [case.attrib["name"] for case in suite.findall("testcase") if case.find("failure") is not None]
                assert failures == [expected_case], f"expected only {expected_case!r}, got {failures!r}"
            except Exception as error:
                failure = str(error) or error.__class__.__name__
            cases.append((f"{mutation} mutation is rejected by {expected_case}", time.monotonic() - started, failure))

    suite = ET.Element(
        "testsuite", name="Governance418VerifierControls", tests=str(len(cases)),
        failures=str(sum(f is not None for _, _, f in cases)), errors="0", skipped="0",
        time=f"{sum(d for _, d, _ in cases):.3f}",
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
