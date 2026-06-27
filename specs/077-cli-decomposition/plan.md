# Implementation Plan: CLI render / IO decomposition

**Branch**: `077-cli-decomposition` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/077-cli-decomposition/spec.md`

## Summary

Phase E of the architecture/quality/de-duplication roadmap. The optional CLI tool
mixes three concerns inside two large modules: `Cli/Cli.fs` (829 LOC) conflates
argument **parsing/normalization + the MVU** with **all text and JSON rendering**,
and `Cli/Program.fs` (673 LOC) scatters **artifact reading / path resolution / fact
extraction** and **review-store persistence** inline, with a `runHost` that couples
budget accounting + effect interpretation + I/O in one function.

The fix is a **structural decomposition** (not a behavior change): extract three new
`.fsi`-curated modules inside the existing `FS.GG.Governance.Cli` project —
`CliRender` (pure projection of `CommandResult` → text/JSON), `ArtifactReading`
(impure spec-kit/design path resolution + reads + fact extraction → `ProjectSnapshot`),
and `ReviewStore` (impure load/save of `RecordedReview`) — leaving `runHost`/`main`
as thin port orchestration. The acceptance bar is the same byte-identity discipline
that gated Phases A–D (073/075/076/074): every CLI text/JSON transcript, golden, and
snapshot stays byte-identical; the surface baseline grows only additively; one
concern is moved per commit with the full suite green at each.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: FSharp.Core only for the new modules. The CLI project
already references Kernel, Host, Adapters.Spi/SpecKit/DesignSystem, HumanText,
HumanRender, RouteCommand. **No new dependency and no new project** (FR-008).

**Storage**: Filesystem at the process edge only — spec-kit/design artifacts (read)
and the review store (`~/.cache/fs-gg-governance/reviews` or `--review-store`,
read/write). The extraction does not change any path, sanitization, or serialization.

**Testing**: Expecto via `FS.GG.Governance.Cli.Tests` (parser/MVU/snapshot/output/
read-only/surface-drift suites) plus the whole-repo `dotnet test`. Byte-identity is
asserted by the existing goldens/transcripts; the surface-drift test gates the
additive baseline.

**Target Platform**: Linux/macOS/Windows .NET CLI (`dotnet tool` `fsgg-governance`).

**Project Type**: Single optional CLI project within a multi-project F# solution.

**Performance Goals**: N/A — pure relocation; no hot path is added or changed.

**Constraints**: Byte-identical CLI text/JSON output (the binding contract); curated
`.fsi` per new public module with no top-level access modifiers in `.fs`; acyclic
graph; CLI stays optional (no lower project references it).

**Scale/Scope**: ~450 LOC relocated across three new module pairs (render ≈ 200,
artifact-reading ≈ 190, review-store ≈ 65). The design report's headline figure of
"~200 LOC" tracks the dominant render extraction; see research.md D7 / SC-005 note.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Spec → FSI → Semantic Tests → Implementation | ✅ PASS | New surfaces drafted as `.fsi` first (contracts/); behavior locked by existing semantic tests + byte-identical goldens that fail if a body drifts. |
| II. Visibility in `.fsi`, not `.fs` | ✅ PASS | Each new module ships a curated `.fsi`; `.fs` bodies carry no `private`/`internal`/`public` (FR-006). CLI surface baseline updated additively (FR-011). |
| III. Idiomatic Simplicity | ✅ PASS | Plain `let` functions relocated verbatim; no operators, SRTP, reflection, type providers, or non-trivial CEs introduced. |
| IV. Elmish/MVU boundary | ✅ PASS | The CLI MVU (`Model`/`Msg`/`Effect`/`init`/`update`/`run`) is unchanged and stays in `module Cli`. Rendering becomes a pure view; artifact-reading/review-store stay edge effects interpreted by `runHost`/`main`. |
| V. Test Evidence | ✅ PASS | Pre-change goldens/transcripts are the failing-on-drift evidence; additive surface-drift + scope-guard tests cover the new modules. No synthetic evidence introduced. |
| VI. Observability & Safe Failure | ✅ PASS | The `InputUnavailable` / `missing <path>` / `review-store-unavailable` / `review-dispatch-failed` degrade paths are preserved verbatim (edge cases). |
| Change Classification | ✅ Tier 1 | New public modules + curated `.fsi` + additively-grown surface baseline, no behavioral drift — declared Tier 1 in the spec. |

**Result**: PASS, no violations. Complexity Tracking is empty (nothing to justify).

## Project Structure

### Documentation (this feature)

```text
specs/077-cli-decomposition/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output — decomposition seams + compile-order + re-export decisions
├── data-model.md        # Phase 1 output — concerns as entities + per-module surfaces
├── quickstart.md        # Phase 1 output — byte-identity validation guide
├── contracts/           # Phase 1 output — the three new module .fsi sketches
│   ├── CliRender.fsi
│   ├── ArtifactReading.fsi
│   └── ReviewStore.fsi
├── checklists/
│   └── requirements.md  # /speckit-specify output (all items pass)
└── tasks.md             # /speckit-tasks output (NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Cli/
├── FS.GG.Governance.Cli.fsproj   # add CliRender/ArtifactReading/ReviewStore compile entries (no new ProjectReference)
├── Project.fsi / Project.fs      # unchanged
├── Cli.fsi / Cli.fs              # KEEP namespace types + parsing/normalization + MVU + exitCode + shared pure helpers;
│                                 #   REMOVE the render* bodies (relocated to CliRender)
├── CliRender.fsi / CliRender.fs  # NEW — renderParseError/renderText/renderJson/render + all *Json/*Text sub-writers
├── ArtifactReading.fsi / .fs     # NEW — path resolution + file/dir reads + spec-kit/design fact extraction + loadSnapshot
├── ReviewStore.fsi / .fs         # NEW — store-root/safeFileName/verdict (de)serialization + loadReview/saveReview
└── Program.fs                    # THIN — fullPath + budget folds + runHost (effect interpreter) + writeOutput +
                                  #   watch/tui edge (unchanged) + main; delegates reads/persistence to the new modules

surface/
└── FS.GG.Governance.Cli.surface.txt   # additively add: module CliRender, module ArtifactReading, module ReviewStore

tests/FS.GG.Governance.Cli.Tests/
├── SurfaceDriftTests.fs          # mirror the three additive module lines in the in-test `generatedSurface` literal;
│                                 #   add a scope-guard assertion for the new modules (additive)
└── OutputTests.fs                # update 2 call sites Cli.renderJson/renderText → CliRender.renderJson/renderText
```

**Structure Decision**: Single optional CLI project (no new assembly — ~450 LOC of
intra-project relocation does not warrant one, per the design's "add focused units,
do not collapse or add projects" stance and FR-008). All new modules live in the
existing `FS.GG.Governance.Cli` namespace so the namespace-level surface baseline
grows additively. Compile order is `Project → Cli → CliRender → ArtifactReading →
ReviewStore → Program` (research D2): `CliRender` compiles **after** `Cli` so it
freely reuses `Cli`'s public pure vocabulary (`exitCode`, `commandName`, `quote`,
`jsonArray`, …); the impure reading/persistence modules compile after it; `Program`
last, wiring everything.

## Complexity Tracking

> No constitution violations — section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |
