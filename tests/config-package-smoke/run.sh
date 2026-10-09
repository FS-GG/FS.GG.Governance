#!/usr/bin/env bash
# Takes an existing exact archive, version, digest and revision; never packs/pushes.
set -euo pipefail
[ "$#" -eq 5 ] || { echo 'usage: run.sh ARCHIVE VERSION SHA256 SOURCE_REVISION EVIDENCE_DIRECTORY' >&2; exit 1; }
smoke_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
package="$(realpath "$1")"
evidence="$(realpath -m "$5")"
consumer="$(mktemp -d)"
trap 'rm -rf "$consumer"' EXIT
mkdir -p "$evidence"
contracts_source="${CONFIG_CONTRACTS_SOURCE:-https://api.nuget.org/v3/index.json}"
public_source="${CONFIG_PUBLIC_SOURCE:-https://api.nuget.org/v3/index.json}"
python3 "$smoke_dir/archive.py" prepare --package "$package" --version "$2" --sha256 "$3" \
  --revision "$4" --lock "$smoke_dir/../../src/FS.GG.Governance.Config/packages.lock.json" \
  --output "$evidence" --contracts-source "$contracts_source" --public-source "$public_source"
cp "$smoke_dir/consumer/"* "$consumer/"
mv "$consumer/ConfigConsumer.fsproj.in" "$consumer/ConfigConsumer.fsproj"
cp "$evidence/NuGet.Config" "$consumer/NuGet.Config"
export DOTNET_CLI_HOME="$consumer/cli-home"
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export MSBUILDDISABLENODEREUSE=1
export NUGET_PACKAGES="$consumer/packages"
export NUGET_HTTP_CACHE_PATH="$consumer/http-cache"
# Associate GitHub credentials only with the exact admitted organization feed.
if [[ "$contracts_source" == 'https://nuget.pkg.github.com/FS-GG/index.json' ]]; then
  export NuGetPackageSourceCredentials_contracts="Username=${FSGG_PACKAGES_ACTOR:-unused};Password=${FSGG_PACKAGES_READ_TOKEN:-unused};ValidAuthenticationTypes=Basic"
else
  unset NuGetPackageSourceCredentials_contracts
fi
readarray -t versions < <(python3 - "$evidence/versions.json" <<'PY'
import json, sys
v = json.load(open(sys.argv[1]))
for name in ['FS.GG.Contracts', 'YamlDotNet', 'FSharp.Core']: print(v[name])
PY
)
props=("-p:ConfigVersion=$2" "-p:ContractsVersion=${versions[0]}" "-p:YamlVersion=${versions[1]}" "-p:CoreVersion=${versions[2]}")
dotnet restore "$consumer/ConfigConsumer.fsproj" "${props[@]}" --configfile "$consumer/NuGet.Config" --no-cache --force-evaluate
python3 "$smoke_dir/archive.py" resolved --manifest "$evidence/manifest.json" \
  --assets "$consumer/obj/project.assets.json" --lock "$consumer/packages.lock.json" --cache "$NUGET_PACKAGES" \
  --source "$(dirname "$package")" --source "$contracts_source" --source "$public_source"
dotnet run --project "$consumer/ConfigConsumer.fsproj" -c Release --no-restore "${props[@]}" \
  -- "$evidence/default-consumer-result.json" "$3" "$4"
# The selected compatibility claim binds the actual loaded assembly and restored
# signed archive, independently of the producer's unchanged dependency metadata.
python3 - "$evidence" "$NUGET_PACKAGES" "${versions[0]}" <<'PYVERIFY'
import hashlib, json, shutil, sys, zipfile
from pathlib import Path
evidence, cache, version = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3]
manifest = json.loads((evidence / 'manifest.json').read_text())
receipt = json.loads((evidence / 'default-consumer-result.json').read_text())
def require(condition, message):
    if not condition: raise ValueError(message)
require(receipt['result'] == 'passed', 'consumer did not pass')
require(receipt['configSha256'] == manifest['sha256'] and
        receipt['sourceRevision'] == manifest['sourceRevision'], 'consumer candidate identity drift')
require(receipt['loadedConfigCount'] == receipt['loadedContractsCount'] == 1,
        'ambiguous loaded Config/Contracts identity')
require(receipt['configAssemblySha256'] == manifest['entries']['lib/net10.0/FS.GG.Governance.Config.dll'],
        'loaded Config differs from the selected candidate')
require(version == '7.6.0', 'selected published Contracts compatibility version requires review')
archives = list((cache / 'fs.gg.contracts' / version).glob('*.nupkg'))
require(len(archives) == 1, 'not one actual restored Contracts archive')
archive = archives[0]
archive_sha = hashlib.sha256(archive.read_bytes()).hexdigest()
require(archive_sha == 'b1df3ebd6251f5b18aaece4dd0c5449a7825dc7056f925cf2516febc35f9dfc5',
        'restored Contracts archive differs from the selected published archive')
with zipfile.ZipFile(archive) as package:
    assembly_sha = hashlib.sha256(package.read('lib/net10.0/FS.GG.Contracts.dll')).hexdigest()
require(assembly_sha == '91f484d28416c5d860a375a91ed70cdda1d3b6d85d504c15ea21e08a9af727ee' and
        receipt['contractsAssemblySha256'] == assembly_sha and
        receipt['contractsAssemblyVersion'] == '7.6.0.0', 'loaded Contracts identity drift')
filename = 'FS.GG.Contracts.7.6.0.nupkg'
shutil.copyfile(archive, evidence / filename)
lock = json.loads((Path(cache).parent / 'packages.lock.json').read_text())['dependencies']['net10.0']
selection = {'version': version, 'archiveFile': filename, 'archiveSha256': archive_sha,
             'assemblySha256': assembly_sha, 'contentHash': lock['FS.GG.Contracts']['contentHash']}
(evidence / 'contracts-selection.json').write_text(json.dumps(selection, indent=2) + '\n')
PYVERIFY
[ "$(sha256sum "$package" | cut -d' ' -f1)" = "$3" ] || { echo 'archive changed during smoke' >&2; exit 1; }
cp "$consumer/packages.lock.json" "$consumer/obj/project.assets.json" "$evidence/"
