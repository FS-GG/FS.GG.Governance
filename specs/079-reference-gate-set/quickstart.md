# Quickstart: Reference `.fsgg` Gate Set

**Feature**: `079-reference-gate-set`

Validation/run guide proving the populated reference loads, routes, and stays
non-blocking by default. Implementation details live in `tasks.md`; contracts in
`contracts/`; the concrete YAML in `data-model.md` §A.

## Prerequisites

- .NET SDK `net10.0` (repo standard).
- Repo built: `dotnet build FS.GG.Governance.sln`.

## Scenario 1 — The reference loads cleanly (US1, SC-002)

```bash
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests \
  --filter "FullyQualifiedName~Loads"
```

**Expected**: green — `loadAndValidate samples/sdd-reference-gate-set` returns `Valid`
with 0 diagnostics and 0 unknown-config findings.

## Scenario 2 — Gates assemble and route (US1, SC-001, SC-004)

```bash
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests \
  --filter "FullyQualifiedName~Routes"
```

**Expected**: green — registry has the 3 gates `build:build`, `test:test`,
`evidence:evidence`; candidate paths `src/App/Program.fs`, `tests/App.Tests/Tests.fs`,
`build.fsx` each select their gate; 0 orphan checks/commands/unreachable domains.

## Scenario 3 — Non-blocking by default, blockable under strict (US2, SC-003, SC-006)

```bash
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests \
  --filter "FullyQualifiedName~Profile"
```

**Expected**: green — on a failing change at `RunMode.Verify`, every selected gate is
`Advisory` under `Light` (default); ≥1 gate is `Blocking` under `Strict`. `defaultProfile`
is `light`.

## Scenario 4 — Evidence is a first-class, advisory-on-first-touch gate (US3)

Covered by Scenarios 2 and 3: the `evidence:evidence` gate is present, bound to the
declared `build-evidence` command, and `warn` maturity keeps it advisory under every
profile (the "no real evidence yet" first-touch posture).

## Scenario 5 — Whole guard (FR-010, SC-007)

```bash
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests
```

**Expected**: all guard assertions G1–G7 (see `contracts/regression-guard.contract.md`)
green. Mutating the reference to empty its checks, break a command reference, or change
`defaultProfile` away from `light` MUST turn this red.

## Downstream reuse check (FR-009, SC-005)

Copy `samples/sdd-reference-gate-set/.fsgg/` unedited into a fresh directory and load it:
it MUST validate and route with 0 edits — confirming it is reusable as the P4 overlay
source.

## Optional — inspect via the CLI

From a git working tree that contains the copied `.fsgg`, the optional CLI routes a
real change against it (text or `--format json`):

```bash
fsgg route --repo . --paths src/App/Program.fs
```

**Expected**: the `build` (and any domain-shared) gate is selected and reported; under the
default `light` profile the reported posture is advisory.
