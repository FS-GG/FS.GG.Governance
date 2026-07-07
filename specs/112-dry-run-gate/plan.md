# Implementation Plan: Dry-run / simulated governance gate

**Branch**: `112-dry-run-gate` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/112-dry-run-gate/spec.md` (governance issue #101)

## Summary

Add `fsgg ship --dry-run` — a **simulated gate** that runs the existing ship pipeline
(`route → Ship.rollup → projections`) with **no real gate-command execution** and **no artifact
writes**, printing a clearly-marked *simulated* verdict plus a **handoff-sufficiency breakdown**.
It converts the *optional-but-untestable* SDD↔Governance boundary (all-`notEvaluated` in the
FS.GG.Audio feedback) into a previewable one: a reviewer or a consumer team can see "what would
Governance say, and is my handoff even sufficient?" without installing the gate tooling.

The whole feature reuses the pure cores unchanged — `Ship.rollup`, `AuditJson.ofShipDecision`,
`HumanText.ofShipDecision`, and the `SddHandoff` consumer. The only surface move is one additive
`RunRequest.DryRun: bool` field (Tier 1); the dry-run's machine-readable output uses a **distinct
schema id** (`fsgg.audit.dryrun/v1`) so the real `audit.json` contract stays byte-identical and a
simulated document can never be consumed as a genuine gate outcome.

## Technical Context

**Language/Version**: F# on .NET `net10.0`.

**Primary Dependencies**: existing in-repo projects only — `FS.GG.Governance.ShipCommand` (host),
`FS.GG.Governance.Ship` (`rollup`), `FS.GG.Governance.AuditJson` / `FS.GG.Governance.HumanText`
(projections), `FS.GG.Governance.Adapters.SddHandoff` (`Reader`/`Consumer` for handoff ingestion),
`FS.GG.Governance.GateRun` / `GateExecution` (outcome/port vocabulary). No new NuGet dependency,
no new project.

**Storage**: N/A. A dry-run **writes nothing** (no `readiness/` artifact, no store persistence);
real ship output is untouched and byte-identical.

**Testing**: **Expecto** (the command suites use `testList`/`test`, not xUnit). Parse behaviour in
`tests/FS.GG.Governance.ShipCommand.Tests/ParseTests.fs`; end-to-end + byte-identical projection
assertions via faked `Interpreter.Ports` in `Support.fs` / `EndToEndTests.fs`; the reference gate
set is driven through `Loader.loadAndValidate` in `tests/FS.GG.Governance.ReferenceGateSet.Tests`.
Surface moves are pinned by `SurfaceDriftTests.fs`.

**Target Platform**: local `dotnet` + GitHub Actions `ubuntu-latest` (gate.yml build/test +
api-compat/surface-drift). publish.yml unaffected — no published-version change.

**Performance Goals**: N/A. Dry-run does strictly *less* work than a real ship (no process
spawns, no writes). No hot-path change.

**Project Type**: Multi-project F# library + CLI (governance tooling). Single repo.

**Constraints**:
- **Real `audit.json` stays byte-identical.** The simulated document is a *separate* projection
  with schema id `fsgg.audit.dryrun/v1`; `AuditJson.ofShipDecision`'s output is unchanged.
- **A dry-run mutates nothing.** The `update` branch withholds every `WriteArtifact` /
  `PersistStore` effect; verified by an unchanged working tree (SC-003).
- **Simulated output is unmistakable.** Both the human banner and the JSON `simulated: true` +
  distinct schema id carry the marker (FR-006 / SC-004).
- **Deterministic.** Identical inputs ⇒ byte-identical output (repo convention; SC-005).
- **Absence ≠ pass.** The sufficiency breakdown makes the all-`notEvaluated` state visible rather
  than rendering an empty blocker list as a clean Pass (FR-011 / SC-002).

**Scale/Scope**: 1 command host touched · 1 additive `.fsi` field · 2 new pure modules
(`Simulate` + its projection) with `.fsi` · ~3 surface baselines updated · ~10 new Expecto tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — Tier 1.** Adds public CLI surface (`RunRequest.DryRun`) and a new
(clearly-marked, additive) simulated-output projection; `.fsi` and surface baselines move in
lockstep.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | Honoured. The `RunRequest.DryRun` field and the new `Simulate`/`SimulateProjection` `.fsi` are designed first; parse + sufficiency get RED→GREEN Expecto tests; the byte-identical guarantee for real `audit.json` is a pinned assertion. |
| **II. Visibility lives in `.fsi`** | Central. `RunRequest` gains one field in `Loop.fsi`; the two new pure modules ship curated `.fsi` with their own surface baselines. No `.fs` gains an access modifier. |
| **III. Idiomatic simplicity** | The feature *reuses* the pure cores; the dry-run is a pure `update` decision (withhold effects) plus one pure classifier. No SRTP/reflection/CE tricks. |
| **IV. Elmish/MVU boundary for I/O** | Reinforced. "Don't execute, don't write" is expressed as pure `update` choosing not to emit `ExecuteGates`/`WriteArtifact`/`PersistStore` effects — the interpreter/ports are untouched. No new I/O. |
| **V. Test evidence is mandatory** | RED→GREEN for parse + sufficiency classification; byte-identical assertions that real `audit.json` is unchanged and that simulated output is stable across re-runs; a no-writes assertion (Capture has zero writes). Real fixtures (the reference gate set, real handoff docs) — no synthetic evidence. |
| **VI. Observability & safe failure** | Improved. Dry-run surfaces *absence* explicitly (FR-011), marks simulated output loudly (FR-006), and fails safe on malformed/empty input with a named diagnostic rather than a silent Pass (FR-008). |

**Engineering Constraints**: `net10.0` ✅; every touched/new public module keeps a curated `.fsi`
with baselines moved in lockstep ✅; no new dependency ✅; generic governance internals only —
nothing rendering-specific ✅; `FS.GG.Governance.*` identity preserved ✅; no new project edge ✅.

**Result: PASS.** No violations; Complexity Tracking intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/112-dry-run-gate/
├── plan.md              # This file
├── research.md          # Phase 0 — R1 seam (flag-on-ship vs new exe), R2 no-execution model,
│                        #           R3 sufficiency semantics, R4 simulated-marker/schema-id, R5 default-policy reality
├── data-model.md        # Phase 1 — SimulatedResult / Sufficiency types + the reused ShipDecision
├── contracts/
│   └── cli-dry-run.md    # Phase 1 — the `--dry-run` CLI contract + the fsgg.audit.dryrun/v1 document shape
├── quickstart.md        # Phase 1 — per-story validation (parse, no-writes, sufficiency, marker, determinism)
└── checklists/
    └── requirements.md  # spec quality checklist (all pass)
```

### Source Code (repository root) — touched surfaces, grouped by user story

```text
# US1 (P1, Tier 1) — simulated verdict, no execution, no writes, marked simulated
src/FS.GG.Governance.ShipCommand/Loop.fs + Loop.fsi     # RunRequest.DryRun bool; ParseAcc field; one flag arm;
                                                        # update branch: withhold WriteArtifact/PersistStore,
                                                        # simulate gate outcomes (NotExecuted), emit simulated summary
src/FS.GG.Governance.ShipCommand/Interpreter.fs         # no Ports change; simulated path avoids ExecutionPort spawns

# US2 (P2, Tier 2 within the feature) — handoff sufficiency breakdown
src/FS.GG.Governance.ShipCommand/Simulate.fs + Simulate.fsi   # NEW pure: classify selected gates + consumed handoff
                                                              # into required-satisfied / required-absent / not-required;
                                                              # assemble SimulatedResult { Decision; Sufficiency }

# US3 (P3, Tier 1 additive) — machine-readable simulated document
src/FS.GG.Governance.ShipCommand/SimulateProjection.fs + .fsi # NEW pure: JSON (schema fsgg.audit.dryrun/v1, simulated:true)
                                                              # + human text/banner projections of SimulatedResult

# Tests
tests/FS.GG.Governance.ShipCommand.Tests/ParseTests.fs         # --dry-run parse arms
tests/FS.GG.Governance.ShipCommand.Tests/*                     # no-writes, simulated-marker, determinism, byte-identical real audit
tests/FS.GG.Governance.ShipCommand.Tests/SurfaceDriftTests.fs  # baseline update for RunRequest + new modules
tests/FS.GG.Governance.ReferenceGateSet.Tests/*                # simulated gate vs the bundled reference set (sufficiency)

# Untouched: AuditJson.ofShipDecision output, real audit.json path, published-version sources, other command exes.
```

**Structure Decision**: Keep the existing multi-project layout; introduce **no new project**. The
dry-run is a **flag on the existing `ship` command** (the lightest seam — one `RunRequest` field, one
flag arm, one pure `update` branch), because ship already owns the `rollup → projection` pipeline
this previews. The two genuinely-new pieces — the sufficiency classifier and the simulated-document
projection — are pure modules *inside* `ShipCommand` (with curated `.fsi` + baselines), not a new
project, because they are ship-specific and add no cross-project edge. A dedicated new Exe or a
`Cli` subcommand was rejected (heavier, and `Cli` has no ship path / a different exit vocabulary).

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
