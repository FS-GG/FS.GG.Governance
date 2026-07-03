# Quickstart / Validation: Tests & CI hygiene (feature 101)

Runnable checks that prove each user story. Run from the repo root on branch `101-tests-ci-hygiene`.

## Prerequisites

- .NET `net10.0` SDK; `dotnet fsi build.fsx` builds the 160+-project solution (bounded parallelism).
- Baseline the pre-change state first: `dotnet fsi build.fsx test` should be **all green** on `main`.

## US1 — surface-drift consolidation

**The consolidation preserves every verdict (SC-003, FR-004)**

```bash
# 1. Full suite green after consolidation (no baseline re-blessed except Tests.Common's own).
dotnet fsi build.fsx test

# 2. No surface baseline silently changed by the refactor: only Tests.Common's surface should differ.
git status --porcelain surface/     # expect: at most surface/FS.GG.Governance.Tests.Common.surface.txt

# 3. The drift check still bites — deliberately mutate one baseline and confirm RED, then restore.
echo "TYPE Bogus.Injected" >> surface/FS.GG.Governance.Adapters.SpecKit.surface.txt
dotnet test tests/FS.GG.Governance.Adapters.SpecKit.Tests   # expect: FAIL naming the SpecKit surface
git checkout -- surface/FS.GG.Governance.Adapters.SpecKit.surface.txt
```

**The duplication is gone (SC-001, SC-002)**

```bash
# Each migrated file is a thin instantiation (no local renderSurface / bless path / findRepoRoot).
grep -rl "let private renderSurface" tests/ | grep -v Tests.Common   # expect: only the 2 local outliers, if any
grep -rlE "let rec (private )?findRepoRoot" tests/*/SurfaceDriftTests.fs tests/*/SurfaceBaselineTests.fs   # expect: empty

# Aggregate line count collapsed by ~an order of magnitude.
git ls-files '*SurfaceDriftTests.fs' '*SurfaceBaselineTests.fs' | xargs wc -l | tail -1
```

**Every baseline asserted exactly once (FR-005)** — spot-check that `HumanRender` and `SurfaceChecks.Dispatch` each resolve to one `SurfaceDrift.surfaceTest` call and the placement note explains why they have no dedicated project.

## US2 — CI bounded + cached

```bash
# Every job declares an explicit timeout (expect one line per job across both files, 0 missing).
grep -c "timeout-minutes:" .github/workflows/gate.yml .github/workflows/publish.yml

# Every setup-dotnet enables lockfile-keyed caching (expect cache:/cache-dependency-path on all 8).
grep -A3 "setup-dotnet@v4" .github/workflows/*.yml | grep -c "cache:"

# The org-synced build config is byte-identical (FR-012 / SC-006).
git status --porcelain Directory.Build.props Directory.Packages.props .config/dotnet-tools.json   # expect: empty
```
Cache hit is observed on CI: a second run with unchanged `packages.lock.json` restores from cache (visible in the setup-dotnet step log).

## US3 — publish hardening

The `resolve-version` logic is a shell block; validate it by simulating the version-resolution decision (the same `strip_v` + semver regex + fail-closed branch) for representative tags. Confirm:

- tag `vNext` (non-semver) → the resolution **errors and exits non-zero**, no push;
- tag `v1.2.3` equal to the fsproj `<Version>` → resolves to `push=true`;
- tag `v9.9.9` unequal → the existing mismatch error;
- `workflow_dispatch` with no input → `push=false` dry run.

```bash
# Fallback user appears exactly once (SC-006, FR-011).
grep -c "Paradigma11" .github/workflows/publish.yml     # expect: 1
grep -c "NUGET_FALLBACK_USER" .github/workflows/publish.yml   # expect: >=1 (env decl + refs)
```

## Full acceptance

```bash
dotnet fsi build.fsx test        # whole suite green
git diff --stat main -- Directory.Build.props Directory.Packages.props .config/dotnet-tools.json   # empty
```
Real evidence: a green full build+test, a demonstrated RED when a baseline is broken (US1 step 3), and the drift-locked config proven byte-identical.
