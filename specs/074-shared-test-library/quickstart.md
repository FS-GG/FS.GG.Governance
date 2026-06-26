# Quickstart — Shared test-support library (validation guide)

Runnable validation that the migration is **behaviour-preserving**: green suite, identical
per-project test counts, byte-identical goldens. Run from the repo root
(`/home/developer/projects/FS.GG.Governance`). Full member/field details:
[contracts/TestsCommon.fsi](./contracts/TestsCommon.fsi); acceptance invariants:
[contracts/migration-acceptance.md](./contracts/migration-acceptance.md).

## Prerequisites
- .NET `net10.0` SDK; `git` on `PATH` (real `git` drives `SnapshotHelpers`).
- A clean working tree at the start of each step (so `git diff` is a true byte-identity check).
- **Build flags**: in this environment the default parallel build SIGABRTs the F# compiler;
  prefix solution builds with `-m:2 -p:UseSharedCompilation=false` (carried from feature 073).
  The bare `dotnet test` lines below assume those flags where a full-solution build is implied;
  `tasks.md`'s acceptance invariant spells them out.

## 0. Capture the pre-migration baseline (once, before any change)
```bash
# Authoritative per-project test-count baseline (C2/A1). Use the repo's standard test runner.
dotnet test FS.GG.Governance.sln --nologo > /tmp/074-baseline-counts.txt
# Goldens/snapshots are pinned by git: any later byte change appears in `git diff`.
```
Expected: suite green; record the per-project counts.

## 1. US1 — Library lands + one project migrated (P1)
After creating `FS.GG.Governance.Tests.Common` (+ its `.Tests`) and migrating one project:
```bash
dotnet build tests/FS.GG.Governance.Tests.Common/FS.GG.Governance.Tests.Common.fsproj
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.Tests.Common.Tests   # bless the surface baseline once
dotnet test tests/FS.GG.Governance.Tests.Common.Tests                   # surface drift + no-src scope guard + smoke
dotnet test tests/<the-one-migrated-project>                            # green, count unchanged vs baseline
```
Expected: the migrated project is green with the **same** count; `git diff` shows the local
copies deleted and a `ProjectReference` added, **no** golden/snapshot change. Editing
`RepositoryHelpers.findRepoRoot` in the library is then reflected in the migrated project with
no edits to that project (Acceptance Scenario US1-2).

## 2. US2 — Three command suites migrated (P2)
```bash
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests \
            tests/FS.GG.Governance.ShipCommand.Tests \
            tests/FS.GG.Governance.RouteCommand.Tests
git diff --stat -- 'tests/**'    # local fixtures/fakes/helpers deleted; NO golden/snapshot bytes changed
```
Expected: all three green with **identical** per-suite counts; every `verify.json`/`audit.json`/
`route.json` golden and every snapshot byte-identical (C3).

## 3. US3 — Full sweep, no test loss (P3)
```bash
dotnet test FS.GG.Governance.sln --nologo > /tmp/074-after-counts.txt
# Per-project counts equal the baseline; the ONLY total delta is the additive Tests.Common.Tests project.
diff <(grep -oE '<project>:[0-9]+' /tmp/074-baseline-counts.txt) \
     <(grep -oE '<project>:[0-9]+' /tmp/074-after-counts.txt)   # adapt to the runner's count format
# Terminal invariant: the duplicated helpers now have ONE definition (in Tests.Common).
grep -rl 'let .*findRepoRoot' tests --include='Support.fs' | wc -l   # → only documented variants remain
```
Expected: full suite green; per-project counts identical to baseline; total count = baseline +
the new `Tests.Common.Tests` only (A1); duplicated-helper definitions collapsed to the shared
library (A4); net test-support LOC down ≥ ~1,000 (A3).

## Failure triage
- **Count drifted (C2)** → a test was lost/duplicated/renamed by the move. Reject; investigate.
- **Golden/snapshot changed (C3)** → the shared fixture diverged from the local one. Keep the
  **local** one local (intentional variant — FR-006/D4); revert that helper's move.
- **`src` references the library (C5 fails)** → FR-008 violation; remove the reference (the
  library is test-only).
- **Project compiles with both a local copy and the reference** → delete the local copy in the
  **same** commit (C4; spec Edge Case "name collisions").
