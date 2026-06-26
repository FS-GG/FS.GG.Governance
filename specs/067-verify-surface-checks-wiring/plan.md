# Implementation Plan: `fsgg verify` Surface-Checks Host Wiring

**Branch**: `067-verify-surface-checks-wiring` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/067-verify-surface-checks-wiring/spec.md`

## Summary

Wire the already-built, already-unit-tested product-surface **check** pipeline into the `fsgg verify` host so a
drifted package/docs/skill/design surface is sensed, run, reported in `verify.json`'s additive `surfaceChecks`
section, and folded into the verify verdict at `RunMode.Verify`. The pure cores
(`ProductSurfaces.classify`, the four domain sensors + packs, `SurfaceChecks.Dispatch.Composition.run`,
`SurfaceChecks.Model.enforcementInputOf`, `VerifyJson.ofVerifyDecisionWithPreview`'s findings parameter) are
finished and tested — **no host runs them today**. The verify projection already carries an empty-findings
placeholder (`ofVerifyDecisionWithPreview … []`), so the work is: (1) classify + sense + run at the MVU
interpreter edge behind a new effect, (2) fold the findings into the existing rollup via `enforcementInputOf` +
`deriveEffectiveSeverity` (no truth-table change), (3) thread the real `SurfaceFinding list` into the two
projection call sites in place of `[]`, and (4) prove it with real-filesystem E2E tests + frozen goldens,
closing the still-open `059` tasks **T045 / T048 / T052**.

This is a **Tier 1** contracted change to the `fsgg verify` host: its observable `verify.json` output gains the
additive `surfaceChecks` section (already specified in `specs/059-…/contracts/verify-json-surfacechecks.md`),
and its exit code can now be driven by a blocking surface finding.

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`), matching the rest of the solution.

**Primary Dependencies** (all existing in-repo projects; **no new external/NuGet dependency**):
- `FS.GG.Governance.ProductSurfaces` — `classify` (F23 edge classification → `ProductSurfaceReport`).
- `FS.GG.Governance.SurfaceChecks` — `Model.SurfaceFinding`, `Model.enforcementInputOf`.
- `FS.GG.Governance.SurfaceChecks.Dispatch` — `Composition.{emptyBundle, domainOf, requestsOf, run, DomainFactBundle}`.
- `FS.GG.Governance.PackageChecks` / `DocsChecks` / `SkillChecks` / `DesignChecks` — each `Interpreter.{senseX, realPort}` + `Model.XFacts`. (Package `realPort` takes an `ExecutionPort`; Design `realPort` takes a `catalogLayout` tuple. At verify the package port is wrapped read-only — `WriteBaseline` no-op, `ListTranscripts` ⇒ `Ok []` — per FR-012.)
- `FS.GG.Governance.GateExecution` — the existing `ExecutionPort` (`ports.Execute`) the package `realPort` consumes (already a verify dependency).
- `FS.GG.Governance.VerifyJson` — `ofVerifyDecisionWithPreview` (already takes `findings: SurfaceFinding list`).
- `FS.GG.Governance.Config.Model` — `TypedFacts` / `Capabilities.Surfaces` (declared surfaces + evidence tags).

**Storage**: filesystem only — the existing deterministic `verify.json` artifact (atomic temp+rename write
already in the host). No new persisted artifact and no new sidecar. **Crucially, surface sensing is read-only**:
the package sensor's default `realPort` regenerates-and-writes an absent baseline and shells FSI for declared
transcripts; at verify it is wired through a **read-only port** that no-ops the baseline write and lists no
transcripts (FR-012), so verify writes nothing to the working tree and spawns no process.

**Testing**: Expecto, matching the solution. Real cores + real `fsgg verify` host via `Interpreter.run`; only the
edge ports (filesystem product root, the four domain `realPort`s) operate over a **real temp tree**. Synthetic
inputs (if any) carry `Synthetic` in the test name with a use-site disclosure (Constitution V).

**Target Platform**: Linux/macOS/Windows CLI (`fsgg verify`), same as the rest of the host suite.

**Project Type**: CLI command host over a pure MVU core (single project family; `src/` + `tests/`).

**Performance Goals**: Surface sensing is bounded by the declared/routed surface count (typically a handful);
the dispatch `run` is pure and `O(findings log findings)` for the deterministic sort. The read-only package
port spawns **no process** at verify (transcripts not executed — FR-012), so verify stays an inner-loop-cheap
filesystem-read pass. No perf regression target beyond "no observable slowdown on the common no-surface path"
(empty bundle ⇒ empty list, short-circuits).

**Constraints**:
- `verify.json` MUST stay byte-identical when there are no findings (additive section omitted; schema version
  unchanged) — FR-004, SC-002.
- The enforcement truth table MUST NOT be re-opened; the verdict is computed by the existing
  `deriveEffectiveSeverity` over inputs built by `enforcementInputOf` — FR-007.
- No other host's output may change — FR-009.
- All sensing happens at the interpreter edge, never inside a pure `update` (Constitution IV).
- Surface sensing is **read-only and non-executing** at verify: no working-tree write, no spawned process; the
  package domain is wired through a read-only port (FR-012). This is a host-edge port choice, not a core change.

**Scale/Scope**: One host (`VerifyCommand`), one new effect + message + a few `Model` fields, two projection
call-site edits, one `.fsproj` reference block, and the F24 sensing/run edge. Net: a bounded host-wiring slice
with the cores untouched.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: PASS. Spec written; the public surface change is the
  grown `VerifyCommand` `Loop.fsi`/`Interpreter.fsi` (new effect/message/model fields) + the (already-present)
  `VerifyJson` findings parameter. Semantic tests exercise the host through `Interpreter.run` and assert the
  `verify.json` contract. FSI updated before/with implementation.
- **II. Visibility Lives in `.fsi`**: PASS. The new `Effect`/`Msg` cases and `Model` fields are declared in
  `VerifyCommand/Loop.fsi`; the new sense entry point in `VerifyCommand/Interpreter.fsi`. No access modifiers in
  `.fs`. The surface-drift baseline test for `VerifyCommand` is re-blessed for the additive surface growth.
- **III. Idiomatic Simplicity**: PASS. Reuses the existing classify→sense→run→fold→project pattern; adds no new
  abstraction. The four domain sensors and the dispatcher are consumed verbatim. No new dependency.
- **IV. Elmish/MVU Is the Boundary**: PASS — central to this plan. Surface sensing + `Composition.run` execute
  in a new `SenseSurfaces` **effect** at the interpreter edge; the pure `update` only emits the effect and folds
  the returned `SurfaceFinding list` (via `SurfacesSensed`) into the model and rollup. Both sides are tested:
  pure update transitions (emit effect; fold findings) and the real interpreter sensing over a temp tree.
- **V. Test Evidence Is Mandatory**: PASS. Fail-before/pass-after over real cores + real host: a drifted-surface
  E2E that fails (exit 0, no `surfaceChecks`) before wiring and passes after; an advisory-only E2E; a
  no-surface byte-identity E2E against a frozen pre-wiring golden. Synthetic inputs disclosed per V.
- **VI. Observability and Safe Failure**: PASS. A surface-fact sensing failure surfaces as a disclosed
  diagnostic (the domain sensors already flag path-escape / unreadable inputs rather than reading or fabricating),
  distinct from a tool defect — FR-010. No silent pass. Surface sensing is also **side-effect-free** at verify
  (read-only package port — no working-tree write, no spawned process — FR-012), so verify causes no surprising
  mutation; a test asserts the working tree is unchanged after a verify run.

**Change Classification**: **Tier 1** (host observable-output + exit-code behavior change, contracted by the
F24 `verify.json` `surfaceChecks` section). No public core API changes (cores reused verbatim).

**Result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/067-verify-surface-checks-wiring/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── verify-json-surfacechecks.md   # the F24 surfaceChecks contract, reaffirmed for the verify host
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.VerifyCommand/
│   ├── FS.GG.Governance.VerifyCommand.fsproj   # EDIT — add the 7 surface ProjectReferences (no external dep)
│   ├── Loop.fsi / Loop.fs                       # EDIT — SenseSurfaces effect, SurfacesSensed msg, Model.SurfaceFindings,
│   │                                            #        rollup fold via enforcementInputOf, [] → findings at projection
│   ├── Interpreter.fsi / Interpreter.fs         # EDIT — add SenseSurfaces port to Ports (wired in realPorts repo);
│   │                                            #        handler: classify → sense 4 domains (read-only package port) → run
│   └── Program.fs                               # (unchanged — Ports built by realPorts)
│
├── FS.GG.Governance.ProductSurfaces/            # REUSE (classify)
├── FS.GG.Governance.SurfaceChecks/              # REUSE (SurfaceFinding, enforcementInputOf)
├── FS.GG.Governance.SurfaceChecks.Dispatch/     # REUSE (Composition.run/requestsOf/domainOf/emptyBundle)
├── FS.GG.Governance.PackageChecks/              # REUSE (Interpreter.sensePackage/realPort)
├── FS.GG.Governance.DocsChecks/                 # REUSE (Interpreter.senseDocs/realPort)
├── FS.GG.Governance.SkillChecks/                # REUSE (Interpreter.senseSkill/realPort)
├── FS.GG.Governance.DesignChecks/               # REUSE (Interpreter.senseDesign/realPort)
└── FS.GG.Governance.VerifyJson/                 # REUSE (ofVerifyDecisionWithPreview findings param — already present)

tests/
├── FS.GG.Governance.VerifyCommand.Tests/
│   ├── SurfaceChecksE2ETests.fs                 # NEW — real-filesystem fsgg verify (059 T045 acceptance 1–3)
│   ├── SurfaceRollupTests.fs                    # NEW — pure update fold: blocking fails / advisory no-escalate
│   ├── goldens/                                 # NEW/REUSE — pre-wiring byte-identity verify.json + non-empty golden
│   ├── SurfaceDriftTests.fs                     # EDIT — re-bless VerifyCommand surface baseline (additive growth)
│   └── Support.fs                               # EDIT — temp multi-surface tree + drift/advisory builders
└── (no other test project changes)

docs/initial-implementation-plan.md             # EDIT — flip the verify surface-checks note to closed
specs/059-package-docs-skills-design-checks/tasks.md  # EDIT — mark T045/T048/T052 complete, citing 067
```

**Structure Decision**: Single-project-family layout (the established repo shape). All new code lives in the
`FS.GG.Governance.VerifyCommand` host and its test project; every surface-check capability is consumed from its
existing project. No new `src/` project is created.

## Phase 0 — Research

See [research.md](./research.md). It resolves: (D1) where the sense+run edge attaches in the verify MVU loop,
(D2) the source of the surfaces-to-classify set, (D3) how findings fold into the rollup without re-opening
the truth table, (D4) the profile passed to `enforcementInputOf`, (D5) the byte-identity / golden strategy,
(D6) the safe-failure behavior for a sensing error, and (D7) the **read-only package port** (no baseline write,
no transcript execution) plus the **surface-sense port** on the verify `Ports` record. No `NEEDS CLARIFICATION`
markers remain.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — the `Model` growth (`SurfaceFindings`, classification), the new `Effect`
  (`SenseSurfaces`) / `Msg` (`SurfacesSensed`), the `DomainFactBundle` assembly, and the fold into the rollup.
- [contracts/verify-json-surfacechecks.md](./contracts/verify-json-surfacechecks.md) — the additive
  `surfaceChecks` section contract (C1 byte-identical-when-empty, C2 element shape + ordering, C3 enforcement
  unchanged), reaffirmed against the `fsgg verify` host as the producing surface.
- [quickstart.md](./quickstart.md) — the runnable validation scenarios mapping to SC-001…SC-005.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
