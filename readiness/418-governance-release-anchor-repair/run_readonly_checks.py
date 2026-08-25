#!/usr/bin/env python3
"""Runs and combines every read-only #418 verification/control suite."""

from __future__ import annotations

import argparse
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--junit", required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    commands = (
        ("live", root / "verify_pre_delivery.py"),
        ("verifier-control", root / "test_verifier_controls.py"),
        ("runner-control", root / "test_release_anchor_runner.py"),
    )
    cases: list[ET.Element] = []
    started = time.monotonic()
    with tempfile.TemporaryDirectory(prefix="fsgg-418-all-checks-") as directory:
        for prefix, command in commands:
            report = Path(directory) / f"{prefix}.xml"
            result = subprocess.run(
                ["python3", str(command), "--junit", str(report)],
                text=True, capture_output=True,
            )
            if report.exists():
                source = ET.parse(report).getroot()
                for original in source.findall("testcase"):
                    original.set("name", f"{prefix}: {original.attrib['name']}")
                    cases.append(original)
            if result.returncode != 0 and not report.exists():
                case = ET.Element("testcase", name=f"{prefix}: runner produced no JUnit")
                detail = result.stderr or result.stdout or f"exit {result.returncode}"
                ET.SubElement(case, "failure", message=detail).text = detail
                cases.append(case)

    failures = sum(case.find("failure") is not None for case in cases)
    suite = ET.Element(
        "testsuite", name="Governance418ReadOnlyChecks", tests=str(len(cases)),
        failures=str(failures), errors="0", skipped="0",
        time=f"{time.monotonic() - started:.3f}",
    )
    properties = ET.SubElement(suite, "properties")
    ET.SubElement(properties, "property", name="targetCommit", value="388819d0e060c11f53e5ca2df3277a54a13f9e75")
    for case in cases:
        suite.append(case)
    Path(args.junit).parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(suite).write(args.junit, encoding="utf-8", xml_declaration=True)
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
