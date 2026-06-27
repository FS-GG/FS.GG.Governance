# Quickstart: validating the CommandHost skeleton extraction

This guide proves the feature end-to-end: the new pure leaf exists with a curated
surface, the command hosts consume it with their local copies gone, and **every
golden/snapshot fixture is byte-identical** to the pre-feature baseline. It is a
run/verification guide — implementation detail lives in
[contracts/command-host.fsi.md](./contracts/command-host.fsi.md),
[data-model.md](./data-model.md), and [research.md](./research.md).

## Prerequisites

- .NET 10 SDK (the repo targets `net10.0` via `Directory.Build.props`).
- A clean working tree on branch `075-command-host-skeleton`.
- Baseline established **before** any change:

  ```bash
  git switch 075-command-host-skeleton
  dotnet build FS.GG.Governance.sln
  dotnet test  FS.GG.Governance.sln    # record the pre-feature pass + per-project test counts
  ```

## Scenario 1 — One source of truth (User Story 1, FR-001/FR-005/FR-007)

After implementation:

```bash
# The leaf exists with a .fsi-first surface and a body.
ls src/FS.GG.Governance.CommandHost/CommandHost.fsi src/FS.GG.Governance.CommandHost/CommandHost.fs

# No moved helper remains duplicated in a command Loop.fs (expect: only the leaf defines them).
grep -rn "let under " src/*Command/Loop.fs        # expect: no matches
grep -rn "let emptySensedFacts" src/*Command/Loop.fs   # expect: no matches
grep -rn "let exitCode" src/*Command/Loop.fs      # expect: no matches (hosts call CommandHost.exitCode)
```

**Expected:** the greps return nothing in the command hosts; the helpers resolve
from `FS.GG.Governance.CommandHost`.

## Scenario 2 — Behavior preserved, byte-for-byte (FR-009, SC-002 — the acceptance test)

```bash
dotnet test FS.GG.Governance.sln
```

**Expected:**
- Every command golden/snapshot suite passes unchanged: `route.json` (RouteCommand),
  `audit.json` (ShipCommand), `verify.json` (VerifyCommand), plus refresh,
  cache-eligibility, release, evidence, and the projection `*Json` suites.
- Per-project test counts equal the pre-feature baseline **except** the additive
  `FS.GG.Governance.CommandHost.Tests` (FR-010, SC-003).
- A fast cross-check that no golden moved:

  ```bash
  git diff --stat -- '**/golden/**' '**/snapshots/**' '**/*.golden.*'
  # expect: no fixture files changed
  ```

## Scenario 3 — Boundaries & discipline preserved (User Story 2, FR-002/FR-003/FR-004/FR-011)

```bash
# Surface baseline exists and the drift test passes against it.
cat surface/FS.GG.Governance.CommandHost.surface.txt
dotnet test tests/FS.GG.Governance.CommandHost.Tests/FS.GG.Governance.CommandHost.Tests.fsproj
```

**Expected:**
- `SurfaceDrift` test passes (public surface == baseline); intentional surface
  changes are re-blessed with `BLESS_SURFACE=1 dotnet test`.
- The **scope-guard** test passes: the leaf references no `Host`/`Cli`/`*Command`
  and no filesystem/git/process project (purity + acyclic split).
- Inspect the leaf's references to confirm purity:

  ```bash
  grep -n "ProjectReference" src/FS.GG.Governance.CommandHost/FS.GG.Governance.CommandHost.fsproj
  # expect: only shared domain-type projects (research D7); no host/Cli/*Command
  ```

## Scenario 4 — Shared `executionPlan` produces identical plans (FR-006)

Covered by Scenario 2 at the golden level (the plan feeds `route.json`/`audit.json`/
`verify.json`). For a focused check, the `CommandHost.Tests` semantic suite asserts
that `executionPlan` with `BudgetFold = None` (Route shape) and with `Some fold`
(Ship/Verify shape) yields the expected `(Gate * GateClassification) list` over
real, literally-constructed values — no `Deferred` ever appears under `None`.

## Scenario 5 — Type-divergent helpers stayed local (User Story 3, FR-008)

```bash
# Release's buildSnapshot (different input type) and cacheReportOf (single site) remain local.
grep -rn "let buildSnapshot" src/FS.GG.Governance.ReleaseCommand/Loop.fs   # expect: present (stays local)
grep -rn "let cacheReportOf" src/FS.GG.Governance.CacheEligibilityCommand/Loop.fs  # expect: present
```

**Expected:** these remain in their hosts; the reasons are recorded in research D5/D6.
The byte-identity gate (Scenario 2) confirms only genuinely-shared members moved.

## Done when

- [ ] `FS.GG.Governance.CommandHost` builds with a curated `.fsi`, a surface baseline,
      and a passing drift + scope-guard test (SC-005).
- [ ] No moved helper is duplicated in any command `Loop.fs` (SC-001).
- [ ] Full suite green; counts match baseline + only the additive leaf test project
      (SC-003).
- [ ] No golden/snapshot fixture changed (SC-002).
- [ ] Net source reduction across the command hosts ≈ 400–500 LOC (SC-004).
