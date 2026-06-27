# Implementation Plan: Verify god-module split (Phase C)

**Branch**: `076-verify-module-split` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/076-verify-module-split/spec.md`

## Summary

Split the two Verify god modules along their feature seams, with **zero observable
behavior change** (every command/projection golden and snapshot byte-identical):

1. **`VerifyCommand/Loop.fs` (1,009 LOC)** — extract its three *optional* feature
   folds (release-readiness preview, surface-check fold, view-currency fold) into
   three new sibling modules in the same project, leaving `Loop` as the base
   pipeline (cache-eligibility → gate execution → cost-budget → provenance) plus
   explicit seams that call the folds.
2. **`VerifyJson/VerifyJson.fs` (582 LOC)** — split into four seam modules (`Core`,
   `SurfaceChecks`, `ReleaseReadiness`, `GeneratedViews`) plus a thin composing
   `VerifyJson` entry module that keeps its four public entry points and
   `schemaVersion` byte-identical.
3. **GateRunHost decision ADR** — record (in `docs/decisions/0003-…`) the
   pursue/defer/drop verdict on the semi-radical `route → ship → verify` host
   unification. **Planned recommendation: DEFER** (see research.md D6); the
   unification is *not* implemented in this feature.

**Chosen split mechanism (clarified with the user):** each extracted seam is a
**new, additively-public** sibling module with its own curated `.fsi` (cleanest
`.fsi`-first story), and the two reflective surface-drift baselines are **re-blessed
additively** to record exactly those new modules — no existing baseline line removed
or changed. This makes the change **Tier 1 / contracted**. The rejected alternatives
(hide seams as `internal` modules; nest them privately in one file) are recorded in
research.md D1.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; no narrower target).

**Primary Dependencies**: FSharp.Core only for the projection; the host keeps its
existing ProjectReference set (Config/Snapshot/Routing/Findings/Gates/Route/
Enforcement/Ship/VerifyJson/CommandHost/… — unchanged). **No new third-party or
NuGet dependency**; `Directory.Packages.props` untouched.

**Storage**: N/A (pure modules; the host writes `verify.json`/`cost-budget.json`/
`provenance.json` through existing ports, unchanged).

**Testing**: Expecto. Acceptance instrumentation already exists: `VerifyJson.Tests`
(GoldenTests, DeterminismTests, SurfaceChecksEmbed, ReleaseReadinessPreview,
SurfaceDrift) and `VerifyCommand.Tests` (EndToEnd, Loop, Reuse, SurfaceRollup,
SurfaceChecksE2E, ReleasePreview, Currency, Determinism, NoMutation, SurfaceDrift,
ScopeGuard). Acceptance = **byte-identical goldens/snapshots + green suite**.

**Target Platform**: Linux/CI build; `dotnet test`.

**Project Type**: Single repo of one-concern microprojects (`src/` + `tests/`).

**Performance Goals**: N/A (refactor; no hot path touched).

**Constraints**: Byte-identical output is the acceptance test. Pure-core/impure-host
split, Elmish/MVU host boundary, `.fsi`-first discipline, and the acyclic dependency
graph all preserved. One seam moved per commit (FR-010).

**Scale/Scope**: 2 source modules split into ~9 modules + 1 ADR; ~1,591 LOC
reorganized (net LOC may rise slightly from new module headers + `.fsi` files —
acceptable; this phase is clarity-dominated, not a reduction target).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after design below.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | ✅ | Each new public seam module is drafted `.fsi`-first (contracts/) before its `.fs`; exercised through the existing golden/determinism tests that already call the public entry points. |
| II. Visibility lives in `.fsi` | ✅ (load-bearing) | The user's chosen mechanism maximizes this: every new module is public *via* a curated `.fsi`; `.fs` bodies carry NO `private`/`internal`/`public` on top-level bindings. Existing `Loop.fsi`/`VerifyJson.fsi` are byte-identical; surface baselines re-blessed additively only. |
| III. Idiomatic simplicity | ✅ | Plain module/file split; no SRTP, reflection (outside tests), custom operators, type providers, or non-trivial CEs introduced. |
| IV. Elmish/MVU is the boundary | ✅ (load-bearing) | `Loop.update` stays the pure transition and keeps owning `Model`/`Msg`/`Effect`; the extracted folds are pure helpers it *calls* — no `update` case, no I/O, no effect interpretation moves out of the host. Interpreter/Program untouched. |
| V. Test evidence | ✅ | The byte-identical goldens/snapshots are the failing-before/passing-after evidence per commit; additive structural tests (scope guard over the new modules; re-blessed drift) added. No synthetic evidence introduced. |
| VI. Observability & safe failure | ✅ | Diagnostics/exit ordering preserved by construction (folds feed the same accumulators/rollup in the same order); golden drift is the failure signal. |

**Change Classification: Tier 1 (contracted)** — adds additive public API surface
(the new seam modules). Requires `.fsi` updates + surface-baseline updates + test
evidence + this plan. No behavior change.

**Gate result: PASS — no violations.** Complexity Tracking table is empty.

## Project Structure

### Documentation (this feature)

```text
specs/076-verify-module-split/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (split mechanism, seam mapping, GateRunHost ADR verdict)
├── data-model.md        # Phase 1 — the seam-module inventory (no domain data model changes)
├── quickstart.md        # Phase 1 — how to validate (build + byte-identical goldens + re-bless once)
├── contracts/
│   ├── verifycommand-seam-modules.md   # the 3 new host fold modules' curated .fsi surfaces
│   └── verifyjson-seam-modules.md      # the 4 new projection seam modules' curated .fsi surfaces
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.VerifyCommand/
├── ReleasePreview.fsi   # NEW — advisory release-readiness preview fold (decomposed off host Model)
├── ReleasePreview.fs    # NEW
├── SurfaceFold.fsi      # NEW — surface-check verdict fold (surfaceBlocks / foldSurfaceVerdict)
├── SurfaceFold.fs       # NEW
├── ViewCurrencyFold.fsi # NEW — stale-generated-view fold (viewCurrencyBlocks / fold / detail)
├── ViewCurrencyFold.fs  # NEW
├── Loop.fsi             # UNCHANGED public surface
├── Loop.fs              # SHRINKS — base pipeline + seam calls (folds extracted)
├── Interpreter.fsi / .fs / Program.fs   # UNCHANGED
└── FS.GG.Governance.VerifyCommand.fsproj  # add the 3 .fsi/.fs pairs BEFORE Loop.fs in <Compile> order

src/FS.GG.Governance.VerifyJson/
├── Core.fsi / Core.fs                   # NEW — verdict writers (verdictToken…writeCore)
├── SurfaceChecks.fsi / SurfaceChecks.fs # NEW — writeSurfaceFinding
├── ReleaseReadiness.fsi / .fs           # NEW — rr* + pack/version/attestation/releaseReadiness writers
├── GeneratedViews.fsi / .fs             # NEW — writeGeneratedView(s)
├── VerifyJson.fsi       # UNCHANGED public surface (schemaVersion + 4 entry points)
├── VerifyJson.fs        # SHRINKS — thin composing entry calling the four seams
└── FS.GG.Governance.VerifyJson.fsproj   # add the 4 .fsi/.fs pairs BEFORE VerifyJson.fs in <Compile> order

surface/
├── FS.GG.Governance.VerifyCommand.surface.txt  # RE-BLESSED additively (+ 3 module types)
└── FS.GG.Governance.VerifyJson.surface.txt      # RE-BLESSED additively (+ 4 module types)

docs/decisions/
└── 0003-gaterunhost-unification.md      # NEW — the pursue/defer/drop ADR (planned: DEFER)
```

**Structure Decision**: Single-project-per-concern is preserved — the seam modules
live *inside* the two existing projects (FR-001/FR-002), not in new projects, because
they are command-specific (the host folds depend on the host's domain wiring; the
projection seams depend on VerifyJson's exact reference set). The genuinely-shared
skeleton already moved to the `CommandHost` leaf in Phase B; Phase C is intra-project
legibility, not new cross-project leaves.

## Complexity Tracking

> No Constitution violations — table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
