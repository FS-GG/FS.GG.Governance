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
contracts_source="${CONFIG_CONTRACTS_SOURCE:-https://nuget.pkg.github.com/FS-GG/index.json}"
public_source="${CONFIG_PUBLIC_SOURCE:-https://api.nuget.org/v3/index.json}"
python3 "$smoke_dir/archive.py" prepare --package "$package" --version "$2" --sha256 "$3" \
  --revision "$4" --lock "$smoke_dir/../../src/FS.GG.Governance.Config/packages.lock.json" \
  --output "$evidence" --contracts-source "$contracts_source" --public-source "$public_source"
cp "$smoke_dir/consumer/"* "$consumer/"
mv "$consumer/ConfigConsumer.fsproj.in" "$consumer/ConfigConsumer.fsproj"
cp "$evidence/NuGet.Config" "$consumer/NuGet.Config"
export NUGET_PACKAGES="$consumer/packages"
export NUGET_HTTP_CACHE_PATH="$consumer/http-cache"
export NuGetPackageSourceCredentials_contracts="Username=${FSGG_PACKAGES_ACTOR:-unused};Password=${FSGG_PACKAGES_READ_TOKEN:-unused};ValidAuthenticationTypes=Basic"
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
dotnet run --project "$consumer/ConfigConsumer.fsproj" -c Release --no-restore "${props[@]}"
[ "$(sha256sum "$package" | cut -d' ' -f1)" = "$3" ] || { echo 'archive changed during smoke' >&2; exit 1; }
cp "$consumer/packages.lock.json" "$consumer/obj/project.assets.json" "$evidence/"
