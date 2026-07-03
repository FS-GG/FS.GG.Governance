# Quickstart / Validation: The gate runs the full test suite on every PR

Feature 102-gate-full-test-suite. Runnable checks that prove each story. Local checks need only the repo + .NET 10 SDK; CI checks observe the PR for this branch.

## Prerequisites

- .NET 10 SDK (`dotnet --version` → 10.x)
- Repo checked out on branch `102-gate-full-test-suite`
- `gh` authenticated for the read-only ruleset check

## US1 — a failing test blocks the merge (P1)

**Local dry-run of exactly what CI runs:**
```bash
dotnet restore FS.GG.Governance.sln --locked-mode
dotnet fsi build.fsx test -c Debug --no-restore
# Expect: all 83 test projects run, "Passed!" per project, overall exit 0.
```

**Prove it goes RED on a hidden regression** (a project that does NOT run in CI today):
```bash
# Pick any assertion in e.g. FS.GG.Governance.EvidenceJson.Tests (not ReferenceGateSet/Cli),
# flip an expected value, then:
dotnet fsi build.fsx test -c Debug --no-restore --no-build
# Expect: that project reports "Failed!", overall exit non-zero. Revert the edit → green again.
```
Acceptance: today this break merges green (build-only gate); after this feature the `full-test-suite`
job fails and the required check blocks the PR. (SC-001, SC-003)

**Confirm total coverage (2 → 83):**
```bash
dotnet fsi build.fsx test -c Debug --no-restore --no-build 2>&1 \
  | grep -c 'Test run for .*\.Tests\.dll'
# Expect: 83 (every *.Tests.fsproj), not 2.
```
(SC-002)

## US2 — bounded and cannot hang the gate (P2)

**Inspect the rendered job:**
```bash
grep -n -A3 'full-test-suite:' .github/workflows/gate.yml
# Expect: name "Full test suite (dotnet fsi build.fsx test)", runs-on ubuntu-latest,
#         timeout-minutes: 30, and the build.fsx test invocation.
```
Acceptance: explicit `timeout-minutes` (not the 360-min default); suite run via the bounded
`build.fsx` entrypoint, not a raw unbounded `dotnet test`. (SC-003)

**Cache warm on rerun (observed on CI):** open the PR, let the job run twice with unchanged
lockfiles; the second run's "Set up .NET" step reports a NuGet cache hit / faster restore. (SC-005)

## US3 — durable required check (P3)

**Confirm the required-check contract already points at this job's name (read-only):**
```bash
gh api repos/FS-GG/FS.GG.Governance/rulesets/18430843 \
  --jq '.rules[] | select(.type=="required_status_checks")
        | .parameters.required_status_checks[].context'
# Expect the list to INCLUDE: Full test suite (dotnet fsi build.fsx test)
```
Acceptance: the new job's `name` equals that context verbatim, so on the PR the required check
transitions from perpetually-pending to reporting; a red job blocks merge. No ruleset edit is
performed by this feature (the context pre-exists). (SC-004)

## Cross-cutting — nothing else moved (SC-006)

```bash
git diff --name-only origin/main...HEAD
# Expect ONLY: .github/workflows/gate.yml  (+ specs/102-* docs)
# NOT present: Directory.Build.props, Directory.Packages.props, .config/dotnet-tools.json,
#              any *.fs / *.fsi / *.fsproj
```
On CI, the `build-config-drift` job stays green (managed files byte-identical). No test is
suppressed via blanket retry or `continue-on-error`.

## Definition of done

- [ ] `full-test-suite` job present in `gate.yml`, name byte-exact, `timeout-minutes: 30`
- [ ] Local `build.fsx test` runs all 83 projects green
- [ ] Deliberately-broken assertion in a non-CI-today project makes the job RED; revert → GREEN
- [ ] PR shows the `Full test suite (dotnet fsi build.fsx test)` required check reporting (not pending)
- [ ] `git diff` touches only `gate.yml` + specs; org build-config files unchanged; `build-config-drift` green
- [ ] Any headless failure surfaced on CI is fixed or narrowly quarantined with a tracking issue (not blanket-suppressed)
