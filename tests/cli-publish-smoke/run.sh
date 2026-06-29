#!/usr/bin/env bash
# Enforcement smoke (089, FR-008 — the green-by-omission guard). REAL evidence: pack the actual CLI
# tool, install it into a throwaway tool-path, and run the INSTALLED `fsgg-governance` against committed
# handoff fixtures. Asserts the cli-enforcement.md contract:
#   • failing handoff + `--mode gate`  → exit 2 (GovernedBlocking) — the handoff drives the verdict
#   • passing handoff + `--mode gate`  → exit 0 (Success)
#   • failing handoff + light (`inner`) → exit 0 (no block; gates enforce only at gate)
#   • structural backstop: the consumer assembly is bundled in the packed tool (tools/**)
# A consumer-less build (the predecessor failure mode) returns 0 on the failing fixture and FAILS here,
# so it can never reach the push job. Non-zero exit on ANY failed assertion.
#
# Usage: tests/cli-publish-smoke/run.sh
# Honors $PACK_CONFIGURATION (default Release). Requires the .NET SDK and (for pack/restore) org-feed
# read creds in the environment (FSGG_PACKAGES_ACTOR / FSGG_PACKAGES_READ_TOKEN, or CI's GITHUB_TOKEN).
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CONFIG="${PACK_CONFIGURATION:-Release}"
CLI_PROJ="$REPO_ROOT/src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj"
TOOL_CMD="fsgg-governance"
PKG_ID="FS.GG.Governance.Cli"
FAIL_FIXTURE="$SCRIPT_DIR/fixtures/failing-handoff"
PASS_FIXTURE="$SCRIPT_DIR/fixtures/passing-handoff"
# 090: same failing handoff as FAIL_FIXTURE, but the product declares `.fsgg/policy.yml
# defaultProfile: light` — the in-repo mirror of the FS.GG.Templates#25 red cell.
LIGHT_FAIL_FIXTURE="$SCRIPT_DIR/fixtures/light-failing-handoff"

fail() { echo "SMOKE FAIL: $*" >&2; exit 1; }
note() { echo "[smoke] $*"; }

# Resolve the version from the fsproj (no hardcoded pin — must match the publish workflow's source).
VERSION="$(dotnet msbuild "$CLI_PROJ" -getProperty:Version 2>/dev/null | tr -d '[:space:]')"
[ -n "$VERSION" ] || fail "could not read <Version> from $CLI_PROJ"
note "CLI version (from fsproj) = $VERSION"

PKG_DIR="$(mktemp -d)"
TOOL_DIR="$(mktemp -d)"
cleanup() { rm -rf "$PKG_DIR" "$TOOL_DIR"; }
trap cleanup EXIT

note "packing $PKG_ID $VERSION ($CONFIG) …"
dotnet pack "$CLI_PROJ" -c "$CONFIG" -o "$PKG_DIR" >/dev/null \
  || fail "dotnet pack failed"

NUPKG="$PKG_DIR/$PKG_ID.$VERSION.nupkg"
[ -f "$NUPKG" ] || fail "expected package not produced: $NUPKG"
note "packed: $NUPKG"

# Structural backstop (cheap): the consumer assembly must be bundled in the tool payload (tools/**).
# Capture the listing first — `grep -q` would close the pipe early and SIGPIPE `unzip`, which `pipefail`
# would then surface as a spurious failure.
if command -v unzip >/dev/null 2>&1; then
  ENTRIES="$(unzip -l "$NUPKG")" || fail "could not read the package archive: $NUPKG"
  printf '%s\n' "$ENTRIES" | grep -Eq 'tools/.*/FS\.GG\.Governance\.Adapters\.SddHandoff\.dll' \
    || fail "consumer assembly FS.GG.Governance.Adapters.SddHandoff.dll is NOT in the packed tool (tools/**) — green-by-omission package"
  note "backstop OK: SddHandoff.dll present in tools/**"
else
  note "backstop SKIPPED: unzip unavailable (behavioral assertions below are authoritative)"
fi

# Install from the local pack output. The repo nuget.config declares packageSourceMapping, which
# `--add-source` cannot combine with (NU1507); supply a minimal isolated config instead — the local
# folder for the bundled tool package, public nuget.org for any declared dep (e.g. FSharp.Core). The
# private org feed is NOT needed: PackAsTool bundles the full runtime closure into tools/**.
SMOKE_CONFIG="$PKG_DIR/nuget.smoke.config"
cat > "$SMOKE_CONFIG" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-pack" value="$PKG_DIR" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
XML

note "installing the tool into $TOOL_DIR …"
dotnet tool install --tool-path "$TOOL_DIR" --configfile "$SMOKE_CONFIG" "$PKG_ID" --version "$VERSION" >/dev/null \
  || fail "dotnet tool install failed"
TOOL="$TOOL_DIR/$TOOL_CMD"
[ -x "$TOOL" ] || fail "installed tool not runnable: $TOOL"

# ── Behavioral assertions (the authoritative real-evidence guard) ──
assert_exit() {
  local want="$1"; shift
  local label="$1"; shift
  "$@" >/dev/null 2>&1
  local got=$?
  if [ "$got" -ne "$want" ]; then
    fail "$label: expected exit $want, got $got  (cmd: $*)"
  fi
  note "OK ($label): exit $got"
}

assert_exit 2 "failing handoff + gate BLOCKS"        "$TOOL" route --root "$FAIL_FIXTURE" --mode gate
assert_exit 0 "passing handoff + gate PASSES"        "$TOOL" route --root "$PASS_FIXTURE" --mode gate
assert_exit 0 "failing handoff + light NO-BLOCK"     "$TOOL" route --root "$FAIL_FIXTURE" --mode inner
# 090 (FR-006/SC-003): the profile-aware behavior. A failing handoff under `defaultProfile: light` is
# advisory at `--mode gate` ⇒ exit 0. The profile-less FAIL_FIXTURE above still blocks (strict default),
# so this assertion proves the profile — not a relaxed gate — flips the verdict.
assert_exit 0 "light profile + failing + gate NO-BLOCK" "$TOOL" route --root "$LIGHT_FAIL_FIXTURE" --mode gate

echo "SMOKE PASS: consumer-bearing $PKG_ID $VERSION enforces the handoff profile-aware (strict/absent gate blocks failing, passes passing, light & non-gate modes do not block)."
