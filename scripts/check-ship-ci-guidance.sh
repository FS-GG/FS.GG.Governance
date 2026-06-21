#!/usr/bin/env bash
# check-ship-ci-guidance.sh — Principle-V real evidence for the F027 branch-protection
# guidance (a docs+YAML deliverable, not an F# core). It fails before the template and
# guidance are correct and passes after.
#
# Assertions:
#   (a) the workflow template is valid GitHub Actions YAML  ............ T005
#   (b) the template is fail-closed (no exit-code remap, no path filter)  T006
#   (c) every # CHANGE ME substitution the guidance names is in the file  T010
#   (d) the documented exit codes 0..4 equal the live Loop.exitCode map .  T013
#   (e) the audit field names the guidance lists equal the F025 set ....  T016
#   (f) the fenced ```yaml block in the guidance byte-matches the file ..  T020
#
# It cross-checks the docs against the LIVE F026 (Loop.exitCode) and F025 (audit.json)
# contracts, so the guidance cannot silently drift from the command it wires.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
guidance="$repo_root/docs/ci/github-actions-branch-protection.md"
template="$repo_root/docs/ci/templates/fsgg-ship.yml"
loop_fs="$repo_root/src/FS.GG.Governance.ShipCommand/Loop.fs"
f025_contract="$repo_root/specs/025-audit-json-projection/contracts/audit-json-document.md"

fail_count=0
pass()  { printf '  PASS  %s\n' "$1"; }
fail()  { printf '  FAIL  %s\n' "$1"; fail_count=$((fail_count + 1)); }
info()  { printf '  ....  %s\n' "$1"; }

# Every input artifact must exist before any assertion can be meaningful.
for f in "$guidance" "$template" "$loop_fs" "$f025_contract"; do
  if [ ! -f "$f" ]; then
    fail "missing required input: ${f#"$repo_root"/}"
  fi
done
if [ "$fail_count" -ne 0 ]; then
  printf '\nFAIL: %d missing input(s) — cannot validate.\n' "$fail_count"
  exit 1
fi

echo "(a) template is valid GitHub Actions YAML"
if command -v actionlint >/dev/null 2>&1; then
  if actionlint "$template" >/tmp/fsgg-actionlint.out 2>&1; then
    pass "actionlint validated the Actions schema"
  else
    fail "actionlint reported problems:"; sed 's/^/        /' /tmp/fsgg-actionlint.out
  fi
else
  info "actionlint not on PATH — falling back to a YAML parse-only check"
  info "(parse-only does NOT validate the GitHub Actions schema)"
  if command -v dotnet >/dev/null 2>&1; then
    fsx="$(mktemp /tmp/fsgg-yaml-XXXXXX.fsx)"
    # Validation-only consumer of YamlDotNet pinned to the Directory.Packages.props
    # version (16.3.0) — this adds NO PackageReference to the F# solution.
    cat > "$fsx" <<'FSX'
#r "nuget: YamlDotNet, 16.3.0"
open System.IO
open YamlDotNet.RepresentationModel
let path = System.Environment.GetEnvironmentVariable "FSGG_TEMPLATE"
try
    use r = new StreamReader(path)
    let s = YamlStream()
    s.Load r
    if s.Documents.Count < 1 then eprintfn "no YAML document parsed"; exit 1
    printfn "parsed %d YAML document(s)" s.Documents.Count
    exit 0
with ex ->
    eprintfn "YAML parse error: %s" ex.Message
    exit 1
FSX
    if FSGG_TEMPLATE="$template" dotnet fsi "$fsx" >/tmp/fsgg-fsi.out 2>&1; then
      pass "YamlDotNet 16.3.0 parsed the template ($(sed -n '$p' /tmp/fsgg-fsi.out))"
    else
      fail "YamlDotNet failed to parse the template:"; sed 's/^/        /' /tmp/fsgg-fsi.out
    fi
    rm -f "$fsx"
  else
    fail "neither actionlint nor dotnet available — cannot validate YAML"
  fi
fi

echo "(b) template is fail-closed"
# Strip comments (everything from the first '#' on each line) before the remap checks:
# the template's own comments legitimately *name* the forbidden tokens to explain the
# rule. A real remap would be live YAML/shell, never inside a '#' comment.
template_code="$(sed 's/#.*$//' "$template")"
if echo "$template_code" | grep -qE '\|\|[[:space:]]*true'; then
  fail "gate step contains '|| true' (exit-code remap forbidden, FR-003)"
else
  pass "no '|| true' exit-code swallow (outside comments)"
fi
if echo "$template_code" | grep -qE 'continue-on-error'; then
  fail "template contains 'continue-on-error' (forbidden, FR-003)"
else
  pass "no 'continue-on-error' (outside comments)"
fi
# A `paths:`/`paths-ignore:` filter under the trigger would let a governed change skip
# the required check (FR-005). The template must declare none.
if grep -qE '^[[:space:]]*paths(-ignore)?:' "$template"; then
  fail "template declares a paths/paths-ignore filter (FR-005: gate must not be skippable)"
else
  pass "no paths/paths-ignore filter on the gate"
fi
if grep -qE 'fetch-depth:[[:space:]]*0' "$template"; then
  pass "checkout uses fetch-depth: 0 (FR-009)"
else
  fail "checkout missing fetch-depth: 0 (FR-009: base/head sensing needs the base ref)"
fi

echo "(c) every documented substitution marker exists in the template"
for marker in "CHANGE ME: your protected branch" \
              "CHANGE ME (later): remove once" \
              "CHANGE ME (later): the from-source line"; do
  if grep -qF "$marker" "$template"; then
    pass "template has marker: $marker"
  else
    fail "guidance names a substitution the template lacks: $marker"
  fi
  if ! grep -qF "$marker" "$guidance"; then
    fail "guidance Substitutions section does not name marker: $marker"
  fi
done

echo "(d) documented exit codes equal the live Loop.exitCode mapping (F026)"
# Parse `| Ctor -> N` pairs from the exitCode function in Loop.fs.
while IFS='|' read -r ctor code; do
  ctor="$(echo "$ctor" | tr -d '[:space:]')"
  code="$(echo "$code" | tr -d '[:space:]')"
  [ -z "$ctor" ] && continue
  # The guidance table row for this code must name the same F026 constructor.
  row="$(grep -E "^\| \`$code\` \|" "$guidance" || true)"
  if [ -z "$row" ]; then
    fail "guidance has no exit-code row for \`$code\`"
  elif echo "$row" | grep -qF "$ctor"; then
    pass "code $code → $ctor matches the guidance"
  else
    fail "code $code: Loop.exitCode says '$ctor' but the guidance row does not name it"
  fi
done < <(grep -oE '\| (Success|Blocked|UsageError'\''|InputUnavailable|ToolError) -> [0-9]' "$loop_fs" \
         | sed -E "s/\| ([A-Za-z']+) -> ([0-9])/\1|\2/")

echo "(e) documented audit fields equal the F025 contract set"
# The documented field set (data-model.md Entity 4). Each must appear in BOTH the
# guidance and the F025 contract — so the docs can neither invent nor drop a field.
audit_fields="schemaVersion verdict exitCodeBasis blockers warnings passing \
              kind id path enforcement \
              baseSeverity maturity mode profile effectiveSeverity reason"
for field in $audit_fields; do
  in_guidance=0; in_f025=0
  grep -qF "\`$field\`" "$guidance" && in_guidance=1
  grep -qF "\`$field\`" "$f025_contract" && in_f025=1
  if [ "$in_guidance" -eq 1 ] && [ "$in_f025" -eq 1 ]; then
    pass "audit field '$field' documented and in F025 contract"
  elif [ "$in_guidance" -eq 0 ]; then
    fail "audit field '$field' is in the F025 contract but the guidance omits it"
  else
    fail "guidance names audit field '$field' not found in the F025 contract"
  fi
done

echo "(f) the fenced yaml block in the guidance byte-matches the template file"
# Extract the first ```yaml ... ``` block from the guidance and diff against the file.
extracted="$(mktemp /tmp/fsgg-fenced-XXXXXX.yml)"
awk '
  /^```yaml$/ { grab=1; next }
  grab && /^```$/ { grab=0; exit }
  grab { print }
' "$guidance" > "$extracted"
if [ ! -s "$extracted" ]; then
  fail "no \`\`\`yaml fenced block found in the guidance"
elif diff -u "$template" "$extracted" >/tmp/fsgg-fence.diff 2>&1; then
  pass "fenced block is byte-identical to docs/ci/templates/fsgg-ship.yml"
else
  fail "fenced block has drifted from the template file:"; sed 's/^/        /' /tmp/fsgg-fence.diff
fi
rm -f "$extracted"

echo
if [ "$fail_count" -eq 0 ]; then
  echo "PASS: F027 branch-protection guidance is valid and matches the live F026/F025 contracts."
  exit 0
else
  echo "FAIL: $fail_count assertion(s) failed."
  exit 1
fi
