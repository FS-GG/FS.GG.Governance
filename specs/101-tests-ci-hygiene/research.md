# Phase 0 Research: Tests & CI hygiene (feature 101)

The nine decisions below resolve the design unknowns for FS.GG.Governance#54. Grounded in a full scan of the real tree (74 `SurfaceDriftTests.fs` + 6 `SurfaceBaselineTests.fs` + 1 `HumanRenderSurfaceDriftTests.fs`; the two CI workflows `gate.yml`/`publish.yml`).

## D1 — Where the shared surface-drift helper lives and what it exposes

**Decision**: Add a new `SurfaceDrift` module to the existing test-support library `FS.GG.Governance.Tests.Common` (`TestsCommon.fsi` / `TestsCommon.fs`), exposing:

- `renderSurface: Assembly -> string` — the one canonical reflective projection (`BindingFlags.Public ||| Instance ||| Static ||| DeclaredOnly`, `GetExportedTypes()` sorted by `FullName`, members rendered `"  [%A] %s" m.MemberType (m.ToString())` and sorted, joined by `\n`).
- `surfaceTest: label:string -> baselineName:string -> asm:Assembly -> Test` — the standard baseline-equality test with the `BLESS_SURFACE=1` bless path, comparing `renderSurface asm` against `surface/<baselineName>.surface.txt`.
- `referencesOnly: label:string -> allowed:(string -> bool) -> asm:Assembly -> Test` — the "references only …" scope guard.
- `noInboundReferences: label:string -> upstream:Assembly list -> asm:Assembly -> Test` — the "no upstream assembly references me" direction guard.

**Rationale**: The issue names `Tests.Common` explicitly. `RepositoryHelpers.findRepoRoot`/`repoRoot` already live there (the "074 Phase D" consolidation), so the repo-root dependency is already satisfied. The `check asm name` helper already present in `SurfaceChecks.Tests`/`CurrencyEnforcement.Tests` is effectively a prototype of `surfaceTest` — this decision lifts it into the shared library. The three renderer variants across the 74 copies differ only cosmetically (flags on one line vs four; parameter `a` vs `asm`); the lines that determine the *output* (sort key, member format) are byte-identical in all copies, so one shared `renderSurface` reproduces every committed baseline exactly.

**Alternatives rejected**: A new dedicated helper project (more ceremony than a module; `Tests.Common` is the sanctioned home). A source-generator / T4 approach (over-engineered for a reflection one-liner).

## D2 — Tests.Common gains an Expecto reference

**Decision**: Add `<PackageReference Include="Expecto" />` to `FS.GG.Governance.Tests.Common.fsproj` (version omitted — Central Package Management supplies it from the org-baseline `Directory.Packages.props`, which already pins Expecto for every test project). Re-bless `surface/FS.GG.Governance.Tests.Common.surface.txt` because the new `SurfaceDrift` module widens the library's own public surface (its sibling `SurfaceBaselineTests.fs` guards that baseline).

**Rationale**: `surfaceTest`/`referencesOnly`/`noInboundReferences` return Expecto `Test` values, so the library that hosts them must reference Expecto. `Tests.Common` deliberately carried no Expecto today (it was pure fixtures/helpers); this is a justified, narrow addition to a **test-only** (`IsPackable=false`) support library — it adds no dependency to any product project and edits no org-synced props (adding a `PackageReference` to a repo-owned fsproj is allowed; only `Directory.Packages.props`/`Directory.Build.props`/`dotnet-tools.json` are drift-locked). The regenerated `Tests.Common/packages.lock.json` is repo-owned and expected to change.

**Alternatives rejected**: Keep `Tests.Common` Expecto-free by having the helper return the raw pieces (rendered string + baseline path) and letting each call-site wrap them in `test "…" { }`. Rejected: it defeats the "~5-line instantiation" goal (FR-002) and re-scatters the bless path across ~80 files — the exact duplication we are removing.

## D3 — Which files migrate, which stay local

**Decision**: Migrate to thin instantiations:
- the **74** `SurfaceDriftTests.fs` **except** `Cli.Tests/SurfaceDriftTests.fs`;
- the **6** `SurfaceBaselineTests.fs` (CommandHost, JsonText, JsonTokens, JsonWriters, RuleIdentity, Tests.Common);
- `Cli.Tests/HumanRenderSurfaceDriftTests.fs`.

Leave **fully local** (out of scope for the helper):
- `Cli.Tests/SurfaceDriftTests.fs` — hardcodes the expected surface as a `string list` under a `"Surface"` test list, no `renderSurface`/bless path; genuinely non-uniform.
- `Sample.SddReferenceProvider.Tests` — carries a cross-baseline "SC-006 no-delta" guard and a `|| not (File.Exists baselinePath)` bless deviation; bespoke by design.

**Rationale**: Consolidate the uniform ~80; do not force the two genuinely-different files through a helper that would distort their intent (Principle III — the helper must express deviations as parameters or leave them alone, per FR-004). Non-surface-drift files that inline `findRepoRoot` for other reasons (`Adapters.SddHandoff.Tests/Fixtures.fs`, `Cli.Tests/ParserTests.fs`, `Snapshot.Tests/Support.fs`) are **out of scope** — this feature touches only the surface-drift family.

## D4 — Parameterize the two mechanical bespoke families; leave the rest local

**Decision**: Expose `referencesOnly` and `noInboundReferences` (D1) for the two structural bespoke families that appear across ~57 files (the "references only … + BCL/FSharp.Core" scope guard and the "adapter → Spi → kernel" direction guard). Leave **local** the heterogeneous guards: symbol-leak checks ("no rendering/host/network symbol leaks into the surface", ~6 files, which inspect *rendered surface text* with bespoke predicates) and one-offs (e.g. `ScaffoldManifestJson` "exports exactly one module").

**Rationale**: The two families have byte-identical bodies parameterized only by an allowed-set predicate / an upstream list — ideal for a shared builder. The symbol-leak and one-off guards each carry a distinct predicate and message; forcing them into a parameter would be less clear than leaving them inline next to the `surfaceTest` call (FR-004: preserve every project's actual assertion).

## D5 — Resolve the HumanRender / SurfaceChecks.Dispatch placement

**Decision**: Both surfaces are already asserted (both have committed baselines) — this feature normalizes their *placement*, it does not add coverage. `HumanRender`'s check (currently a hand-rolled file in `Cli.Tests`) becomes a `SurfaceDrift.surfaceTest` instantiation; `SurfaceChecks.Dispatch`'s check stays as a second `surfaceTest` call inside `SurfaceChecks.Tests`. Record the placement decision in the quickstart/README note so the "why isn't there a `HumanRender.Tests`?" question is answered once.

**Rationale**: Adding two new dedicated test projects for two tiny library surfaces is more scaffolding than the coverage warrants; the requirement (FR-005) is that every baseline is asserted by exactly one test and no placement is left unexplained — met by normalizing onto the helper and documenting.

## D6 — NuGet caching via setup-dotnet native support

**Decision**: On every `actions/setup-dotnet@v4` step that precedes a restore (8 steps across the two workflows), add:

```yaml
with:
  dotnet-version: "10.0.x"
  cache: true
  cache-dependency-path: '**/packages.lock.json'
```

**Rationale**: `setup-dotnet` has first-class NuGet caching keyed on a lockfile glob; the repo commits 166 `packages.lock.json` files, so the key is well-defined and a genuine dependency change (lockfile edit) misses the cache and re-restores (FR-008). This is configured entirely in the repo-owned workflow files — no `Directory.Build.props`/`Directory.Packages.props` edit (FR-012). Caching only accelerates the *download*; `dotnet restore --locked-mode` still validates the resolved graph, so the deterministic-gate enforcement is unchanged.

**Alternatives rejected**: Manual `actions/cache` keyed by hand (more moving parts than `setup-dotnet`'s native option). Caching in a shared build prop (would edit a drift-locked file).

## D7 — Explicit per-job timeouts

**Decision**: Add `timeout-minutes` to **all 9 jobs** (gate.yml: `gate`, `build-config-drift`, `reference-gate-set-pack`, `api-compatibility-gate`; publish.yml: `resolve-version`, `cli-tests`, `enforcement-smoke`, `publish`, `publish-reference-gate-set`). Bounds are generous-but-finite (build/test/pack jobs ≈ 20–30 min; the light drift/resolve jobs ≈ 10–15 min), each comfortably above observed run times yet far below the 360-minute default.

**Rationale**: The requirement (FR-007) is that no job relies on the platform default; the exact minute values are a bounded engineering choice made at implementation time against observed durations, biased high enough not to flake but low enough to kill a hang in minutes.

## D8 — Publish workflow fails closed on a non-semver `v*` tag

**Decision**: In `resolve-version`'s release/tag branch (`publish.yml` ~lines 104–114), restructure so that a tag matching the `v*` trigger but **not** the semver shape errors and exits non-zero (fail-closed), instead of falling through to `push="true"`:

- semver tag **equal** to the fsproj `<Version>` → `push=true` (unchanged);
- semver tag **not equal** → existing mismatch error (unchanged);
- `v*` tag that is **not** semver (e.g. `vNext`) → **new** error: "tag '…' is not a semantic version; cannot reconcile against the fsproj `<Version>` (…). Retag with v<major>.<minor>.<patch>."; exit 1;
- `workflow_dispatch` with no version input → `push=false` dry run (unchanged).

**Rationale**: Fail-closed matches the repo's established publish convention (FR-007 fail-safe everywhere; the path already errors on a semver mismatch rather than guessing). The finding offered "fail or dry-run"; failing is the stronger guarantee against a mislabeled push and the smaller change (one branch added). Recorded as the chosen option in the spec's Assumptions.

**Alternatives rejected**: Resolve a bad tag to a no-push dry run (silently succeeds — weaker signal; a maintainer expecting a publish would not notice).

## D9 — Single-source the fallback NuGet user

**Decision**: Declare a workflow-level `env: NUGET_FALLBACK_USER: Paradigma11` in `publish.yml` and change both `NuGet/login` steps to `user: ${{ secrets.NUGET_USER || env.NUGET_FALLBACK_USER }}`.

**Rationale**: One declaration, two references (FR-011). The `env` context is available in a step's `with:` block, so no job restructuring is needed. The value and its `secrets.NUGET_USER` override precedence are unchanged — pure de-duplication.

## Constitution alignment (all decisions)

Tier 2 throughout (internal test/CI/build hygiene; no product API or contract change). The only `.fsi` that changes is `Tests.Common`'s own test-support surface (D2), re-blessed in place — not a product API. Principle II is *strengthened*: one shared, guarded surface-drift definition replaces ~80 divergent copies. Principle III: helper exposes exactly the shared shape; genuine deviations stay local (D3/D4). Dependency-minimalism (Engineering Constraints) is respected — Expecto is added only to a test-only library and only because it already hosts test builders (D2), and no product project gains a dependency.
