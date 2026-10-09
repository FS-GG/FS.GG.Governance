#!/usr/bin/env python3
"""Execute the real shell credential branch with synthetic credentials, no SDK/network."""
from pathlib import Path
import subprocess

script = (Path(__file__).resolve().parents[1] / "run.sh").read_text()
start = script.index('if [[ "$contracts_source"')
end = script.index("\nreadarray -t versions", start)
branch = script[start:end]
assert branch.count("NuGetPackageSourceCredentials_contracts=") == 1
org = "https://nuget.pkg.github.com/FS-GG/index.json"
cases = [org, "https://api.nuget.org/v3/index.json", "/tmp/synthetic-local-feed",
         "https://nuget.pkg.github.com/other/index.json", org + "?redirect=other",
         "https://nuget.pkg.github.com/FS-GG/index.json.evil"]
for source in cases:
    environment = {"PATH": "/usr/bin:/bin", "FSGG_PACKAGES_ACTOR": "synthetic-actor",
                   "FSGG_PACKAGES_READ_TOKEN": "synthetic-token",
                   "NuGetPackageSourceCredentials_contracts": "synthetic-inherited-association",
                   "EXPECT_ORG": "1" if source == org else "0"}
    assertion = """import os
value = os.environ.get('NuGetPackageSourceCredentials_contracts')
expected = 'Username=synthetic-actor;Password=synthetic-token;ValidAuthenticationTypes=Basic'
assert (value == expected) if os.environ['EXPECT_ORG'] == '1' else (value is None)
"""
    command = 'set -euo pipefail\ncontracts_source="$1"\n' + branch
    command += "\n/usr/bin/python3 -c \"" + assertion + "\""
    subprocess.run(["/bin/bash", "-c", command, "credential-control", source],
                   env=environment, check=True, timeout=5, stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL)
print("six actual credential-association controls passed (synthetic credentials only)")
