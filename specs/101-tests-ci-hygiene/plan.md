# Implementation Plan: Tests & CI hygiene — consolidate SurfaceDrift, bound and cache CI, harden publish

**Branch**: `101-tests-ci-hygiene` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/101-tests-ci-hygiene/spec.md`

## Summary

Close the tests/CI hygiene cluster from the 2026-07-02 review (M-CI-3 / M-CI-4 + lows, issue #54, epic #44) in three independent slices:

1. **Surface-drift consolidation (US1, P1)** — replace ~80 near-identical surface-drift test files (74 `SurfaceDriftTests.fs` + 6 `SurfaceBaselineTests.fs` + 1 `HumanRenderSurfaceDriftTests.fs`) with one shared `SurfaceDrift` module in `FS.GG.Governance.Tests.Common` (`surfaceTest` + two mechanical guard builders `referencesOnly`/`noInboundReferences`), reducing each file to a thin instantiation and removing the inline `findRepoRoot` copies (the shared `RepositoryHelpers.findRepoRoot` already exists). The renderer is behaviorally identical across all copies, so every committed baseline still matches with no re-bless — except `Tests.Common`'s own surface, which the new module widens and is re-blessed in place. Two genuinely non-uniform files stay local.

2. **CI bounding + caching (US2, P2)** — add `timeout-minutes` to all 9 workflow jobs and lockfile-keyed NuGet caching (`setup-dotnet` `cache: true` + `cache-dependency-path: '**/packages.lock.json'`) to the 8 restoring jobs. Workflow-level only; org-synced build props untouched.

3. **Publish hardening (US3, P3)** — make `resolve-version` fail closed on a `v*` tag that is not semver (it currently falls through to `push=true`), and single-source the `Paradigma11` fallback NuGet user via a workflow-level `env`.

No product API, JSON contract, or behavior change. The set of things asserted, built, and published is identical after the change.

## Technical Context

**Language/Version**: F# on `net10.0` (test-support library + call-sites); GitHub Actions YAML + bash (workflows). Deliverables: a new `SurfaceDrift` module in `Tests.Common` (`.fsi` + `.fs`) with an added `Expecto` `PackageReference`; ~80 shrunk per-project test files; edits to `gate.yml` + `publish.yml`; one re-blessed `Tests.Common` surface baseline.

**Primary Dependencies**: Expecto (already the repo test framework, centrally pinned via CPM); `System.Reflection` (the surface projection, already used by every copy); `actions/setup-dotnet@v4` native caching. No new third-party package on any product project.

**Storage**: N/A.

**Testing**: Real-evidence CI. The consolidation is validated by the existing surface-drift suite staying green (same verdicts) plus a demonstrated RED when a baseline is deliberately broken; the migrated `Tests.Common` `SurfaceBaselineTests` guards the new helper's own surface. CI changes are validated by inspecting the rendered job definitions and observing a cache hit on a second run.

**Target Platform**: GitHub Actions `ubuntu-latest` + local `dotnet fsi build.fsx test`.

**Performance Goals**: Faster CI (warm NuGet cache) and bounded job wall-time; no product perf surface.

**Constraints**:
- **No edits to org-synced build config** — `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json` stay byte-identical (FR-012). Caching is configured in the workflow files; the Expecto reference is added to the repo-owned `Tests.Common.fsproj` (CPM supplies the version — no central-props edit).
- **No product behavior / contract change** — Tier 2 throughout; the only `.fsi` touched is the test-only `Tests.Common` surface (re-blessed in place).
- **Preserve every verdict** — the shared renderer reproduces every baseline byte-for-byte; any per-project deviation is a helper parameter or stays local (FR-004).
- **Fail closed on publish** — an unreconcilable tag never pushes.

**Scale/Scope**: 1 test-support module (+Expecto ref, +re-blessed baseline) · ~80 test files shrunk · 2 files left local · 2 workflow files edited (9 job timeouts, 8 cache blocks, 1 publish decision branch, 1 single-sourced env).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — Tier 2 (internal test/CI/build hygiene).** No product public API and no external contract change. The only surface that moves is the test-only `Tests.Common` library's own `.fsi` (adding the `SurfaceDrift` module); its baseline is re-blessed in place and guarded by its sibling `SurfaceBaselineTests`. This is consolidation, not a new or widened *product* API.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | Holds. The `SurfaceDrift` module is designed as a curated `.fsi` first (Contract A); its callers are the semantic tests, which must stay green. |
| **II. Visibility lives in `.fsi`** | Holds and is *strengthened*. The helper's surface is declared in `Tests.Common.fsi` with no `.fs` access modifiers; the whole feature makes the repo's Principle-II surface-drift check a single guarded definition instead of ~80 divergent copies. |
| **III. Idiomatic simplicity** | Central. One reflection helper + two mechanical guard builders replace pasted boilerplate; genuinely non-uniform files (the hardcoded-list `Cli.Tests` file, the cross-baseline `Sample` guard) stay local rather than being forced through a parameter. No new abstraction beyond the helper. |
| **IV. Elmish/MVU boundary for I/O** | N/A — pure reflection + test assertions + declarative CI YAML; no stateful/I-O workflow introduced. |
| **V. Test evidence is mandatory** | Real evidence: the existing suite stays green (same verdicts), a deliberately broken baseline goes RED, the drift-locked config is proven byte-identical. No synthetic evidence. |
| **VI. Observability & safe failure** | The consolidated drift diagnostic names the offending project; the publish hardening fails loudly with an actionable "retag with v<semver>" message rather than silently pushing. |

**Engineering Constraints**: `net10.0` ✅; no edits to org-synced props (caching is workflow-level; Expecto via CPM on a test-only fsproj) ✅; dependency-minimalism — Expecto added only to a test-only (`IsPackable=false`) support library that already hosts test builders, no product project gains a dependency ✅; genericity — nothing rendering-specific ✅; `FS.GG.Governance.*` identity preserved ✅.

**Result: PASS.** No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/101-tests-ci-hygiene/
├── plan.md              # This file
├── spec.md              # The three prioritized stories + FR/SC
├── research.md          # Phase 0 — decisions D1–D9
├── data-model.md        # Phase 1 — the helper surface, CI invariant, publish decision table
├── contracts/
│   └── tests-ci-hygiene.md   # Contracts A (SurfaceDrift .fsi) / B (CI job) / C (publish)
├── quickstart.md        # Phase 1 — runnable validation per story
└── checklists/
    └── requirements.md  # spec quality checklist (all pass)
```

### Source Code (repository root)

```text
tests/
├── FS.GG.Governance.Tests.Common/
│   ├── TestsCommon.fsi                    # ADD module SurfaceDrift (renderSurface/surfaceTest/referencesOnly/noInboundReferences)
│   ├── TestsCommon.fs                     #   its .fs body (no access modifiers)
│   └── FS.GG.Governance.Tests.Common.fsproj  # ADD <PackageReference Include="Expecto" /> (CPM version)
├── FS.GG.Governance.*.Tests/
│   ├── SurfaceDriftTests.fs               # SHRINK each (73 of 74) to a SurfaceDrift.* instantiation
│   └── SurfaceBaselineTests.fs            # SHRINK the 6 (CommandHost/JsonText/JsonTokens/JsonWriters/RuleIdentity/Tests.Common)
├── FS.GG.Governance.Cli.Tests/
│   ├── HumanRenderSurfaceDriftTests.fs    # SHRINK to a SurfaceDrift.surfaceTest call (normalize placement)
│   └── SurfaceDriftTests.fs               # LEAVE LOCAL (hardcoded-list outlier)
└── FS.GG.Governance.Sample.SddReferenceProvider.Tests/
    └── SurfaceDriftTests.fs               # LEAVE LOCAL (cross-baseline no-delta guard)

surface/
└── FS.GG.Governance.Tests.Common.surface.txt   # RE-BLESS (SurfaceDrift module widens the surface)

.github/workflows/
├── gate.yml       # 4 jobs: timeout-minutes on all; cache on the 3 setup-dotnet jobs
└── publish.yml    # 5 jobs: timeout-minutes on all; cache on all 5 setup-dotnet jobs; resolve-version fail-closed; single-sourced NUGET_FALLBACK_USER
```

**Structure Decision**: The shared logic lands in the existing `Tests.Common` test-support library (where `RepositoryHelpers` already lives), exposed through its curated `.fsi`. Per-project files become thin instantiations; two genuinely non-uniform files stay local. CI changes are confined to the two repo-owned workflow files so the drift-locked org build config is untouched. See [research.md](./research.md) for D1–D9 and [contracts/tests-ci-hygiene.md](./contracts/tests-ci-hygiene.md) for the authoritative shapes.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
