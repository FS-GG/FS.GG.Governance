# Implementation Plan: Repair the repository's dependency fences

**Branch**: `100-dependency-fences` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/100-dependency-fences/spec.md`

## Summary

Restore three drifted **dependency fences** to a single true state and guard each one so it cannot drift silently again (2026-07-02 review M-ARCH-1/2/3, issue #53, epic #44):

1. **YAML fence (M-ARCH-1, P1)** — the README claims YamlDotNet is "isolated to Config" (README:117,146), but **five** projects declare a direct `YamlDotNet` package reference (Config, CurrencySensing, RefreshCommand, ReleaseDeclaration, ReleaseCommand). Approach: first strip any *dead* `YamlDotNet` references (projects that carry the package but use no YamlDotNet type), then **document the genuine remaining owner set** in the README with a one-line rationale per owner, and add a guard test asserting the direct-`YamlDotNet` set equals that documented allowlist exactly. Forcing all YAML parsing through one owner is rejected — the five parse *distinct* YAML domains, and a single god-parser would worsen coupling (Principle III).

2. **Exe→exe references (M-ARCH-2, P2)** — exactly two edges break the "every executable is a leaf" property: `Cli → RouteCommand` and `EvidenceCommand → Cli`. Approach: extract the two shared payloads into ordinary internal library projects — a **route-pipeline** library (RouteCommand's `Interpreter`+`Loop`) referenced by both RouteCommand and Cli, and a **project-sensing** library (Cli's `Project` module + `defaultJudge`) referenced by both Cli and EvidenceCommand. The eight executables become thin `main` leaves; no executable references another.

3. **`fsgg` tool-name collision (M-ARCH-3, P2)** — three tools set `ToolCommandName=fsgg` (RouteCommand, EvidenceCommand, CacheEligibilityCommand). Approach: **RouteCommand keeps `fsgg`** (the flagship route tool); EvidenceCommand → `fsgg-evidence`, CacheEligibilityCommand → `fsgg-cache-eligibility`. A guard asserts at most one project claims `fsgg`. Package IDs are untouched (`ToolCommandName` ≠ `PackageId`).

All three guards live in one new self-contained test project modeled on `RenameGuard.Tests` (Tier 2, no `.fsi`, scans `git ls-files '*.fsproj'`). P3 add-ons (edge-tier reference-convention note, centralized `VersionPrefix` in `Directory.Build.local.props`, local ADR index) are included only where cheap.

## Technical Context

**Language/Version**: F# on .NET `net10.0`. Deliverables are: new/edited `.fsproj` + `.fsi`/`.fs` for two extracted libraries; `.fsproj` metadata edits (`ToolCommandName`, `PackageReference` removals); README edits; one new guard-test project; optional `Directory.Build.local.props` metadata.

**Primary Dependencies**: Expecto (existing test framework); the `dotnet`/MSBuild project graph; `git ls-files` (guard tree scan, per the RenameGuard precedent). No new third-party package on any product project.

**Storage**: N/A.

**Testing**: Real-evidence CI. New: one guard-test project (`FS.GG.Governance.DependencyFences.Tests`) asserting the three fences against the real tracked tree, plus red-path unit tests on the pure matchers. Existing semantic tests for the refactored exes (`Cli.Tests`, `RouteCommand.Tests`, `EvidenceCommand.Tests`, their `SurfaceDriftTests`) must stay green; surface baselines that *move* with a relocated module are re-baselined in place (a relocation, not a product-API change).

**Target Platform**: GitHub Actions `ubuntu-latest` + local `dotnet test`.

**Performance Goals**: N/A (build/test-time hygiene).

**Constraints**:
- **No edits to org-synced build config** — `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json` stay byte-identical to the org baseline; repo-owned config (the centralized `VersionPrefix`) goes in `Directory.Build.local.props`.
- **Package IDs permanent** (ADR-0003) — new libraries are internal (`IsPackable=false`); no packable artifact's `PackageId` changes. `ToolCommandName` changes are allowed (not the package identity).
- **No product behavior / JSON contract change** — the route and evidence pipelines behave identically after extraction; JSON contracts untouched.
- **Fences agree with docs** — each guard asserts the documented allowlist; drift fails the build loudly with an actionable diagnostic (RenameGuard template).
- **Multiplexer out of scope** — ADR-0003's single-entry `fsgg` multiplexer is not built here; only the collision is removed until it lands.

**Scale/Scope**: 2 new internal library projects · 2 executables slimmed to leaves (Cli, RouteCommand, EvidenceCommand touched) · 3 `ToolCommandName` values · 1 new guard-test project · README + optional `Directory.Build.local.props` + optional ADR index. ~5 `YamlDotNet` references audited.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — Tier 2 (internal refactor + hygiene + guards).** No new *product* public API and no external contract change. M-ARCH-2 **relocates** existing internal modules from executables into libraries — the moved `.fsi` surface travels with its module and its surface-drift baseline is re-baselined in place; this is a relocation, not a new or widened API. The three guards are Tier-2 regression tests (no `.fsi`, no baseline), exactly like `RenameGuard.Tests`.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | Holds. The guard tests are written to the documented allowlists (the "contract"); the extracted libraries keep their existing `.fsi` and are covered by the existing exe semantic tests, which must stay green. |
| **II. Visibility lives in `.fsi`** | Holds. Each relocated module keeps its `.fsi`; the two new libraries expose exactly the moved surface via `.fsi`. The exes shrink to a `main` with no public surface. |
| **III. Idiomatic simplicity** | Central. The YAML fence is *documented*, not force-coupled through one god-parser; the extraction *removes* coupling (two exe→exe edges). No new abstraction beyond the two thin libraries. |
| **IV. Elmish/MVU boundary for I/O** | Preserved. The route pipeline's impure `realPorts` stays behind the `Interpreter.Ports` boundary; extraction moves the boundary intact into the library. |
| **V. Test evidence is mandatory** | Real evidence only: guards scan the actual tracked tree; acceptance is a green full build+test plus a demonstrated red build when a fence is deliberately broken. |
| **VI. Observability & safe failure** | Each guard fails loudly with a file-level, actionable diagnostic (which project broke which fence), per the RenameGuard template. |

**Engineering Constraints**: `net10.0` ✅; no edits to org-synced props (VersionPrefix → `Directory.Build.local.props`) ✅; dependency-minimalism — new libraries add no new package dependency; guards use BCL + `git` only ✅; genericity — nothing rendering-specific ✅; `FS.GG.Governance.*` identity preserved ✅.

**Result: PASS.** No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/100-dependency-fences/
├── plan.md              # This file
├── research.md          # Phase 0 — the four fence decisions (D1 YAML, D2 extraction, D3 fsgg, D4 guard host) + P3
├── data-model.md        # Phase 1 — the project-graph model the guards read (ProjectNode, FenceRule, Violation)
├── contracts/
│   └── dependency-fences.md   # the three authoritative allowlists the guards assert
├── quickstart.md        # Phase 1 — run the guards, break a fence to see red, full build+test
└── checklists/
    └── requirements.md  # spec quality checklist (all pass)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.RoutePipeline/         # NEW internal lib (IsPackable=false)
│   ├── *.fsi / *.fs                        #   MOVE RouteCommand's Interpreter + Loop here
│   └── FS.GG.Governance.RoutePipeline.fsproj
├── FS.GG.Governance.ProjectSensing/        # NEW internal lib (IsPackable=false)
│   ├── *.fsi / *.fs                        #   MOVE Cli's Project module + defaultJudge here
│   └── FS.GG.Governance.ProjectSensing.fsproj
├── FS.GG.Governance.RouteCommand/          # SLIM to a main leaf; reference RoutePipeline; keep ToolCommandName=fsgg
├── FS.GG.Governance.Cli/                   # reference RoutePipeline + ProjectSensing; DROP ref to RouteCommand
├── FS.GG.Governance.EvidenceCommand/       # reference ProjectSensing; DROP ref to Cli; ToolCommandName=fsgg-evidence
├── FS.GG.Governance.CacheEligibilityCommand/  # ToolCommandName=fsgg-cache-eligibility
├── FS.GG.Governance.Config/                # (YAML owner — keep + document)
├── FS.GG.Governance.CurrencySensing/       # (audit YamlDotNet ref: keep+document or remove if dead)
├── FS.GG.Governance.RefreshCommand/        #   "
├── FS.GG.Governance.ReleaseDeclaration/    #   "
└── FS.GG.Governance.ReleaseCommand/        #   "

tests/
└── FS.GG.Governance.DependencyFences.Tests/   # NEW self-contained guard project (Tier 2, no .fsi)
    ├── DependencyFenceTests.fs             #   YAML-owner set · no exe→exe · single fsgg + red-path matchers
    └── FS.GG.Governance.DependencyFences.Tests.fsproj

README.md                                   # EDIT: correct the YAML-owner claim; note the fsgg names + edge-tier ref convention
Directory.Build.local.props                 # OPTIONAL EDIT: centralize <VersionPrefix> for baseline-only fsprojs (repo-owned)
docs/adr/README.md (or similar)             # OPTIONAL NEW: local index for org ADR references (0007/0012/0013)
```

**Structure Decision**: Two new internal (`IsPackable=false`) libraries hold the extracted, formerly-cross-executable logic, so no package-ID contract is created and the eight executables become leaves. The guards are consolidated into one new `RenameGuard`-style test project that reads the real `.fsproj` graph via `git ls-files`. Repo-owned build metadata (`VersionPrefix`) stays in the drift-exempt `Directory.Build.local.props`; org-synced props are untouched.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
