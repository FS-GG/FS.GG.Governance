# Implementation Plan: SDD→Governance Handoff Consumer (enforce, not just produce)

**Branch**: `081-sdd-handoff-consumer` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/081-sdd-handoff-consumer/spec.md`

## Summary

Governance documents and acknowledges the SDD→Governance handoff contract
(`readiness/<id>/governance-handoff.json`, contract `v1.0.0`, ADR 0002) but ships **no code
that consumes it** — the handoff is an inert file. This feature ships the **consumer**: a pure
reader/parser (version-pinned to contract major `1.x`), a pure ADR-0002 mapping that feeds the
declared evidence into the existing `Kernel.Evidence` model and runs its taint closure, and
**host wiring** so the handoff's evidence **and** SDD merge-boundary readiness drive
`route`/`ship`/`verify` verdicts end-to-end.

**Technical approach (from Phase 0 research)**: the route/ship/verify hosts form their verdict
through the **Config → Gates → Routing → Route.select → Ship.rollup** pipeline — *not* the
kernel `Adapter` rule catalogs (F009–F011), which no verdict host references (research D3). So
the consumer maps the handoff into typed **`Gate` registry entries + `SelectedGate` entries**
that each host folds into the registry and `RouteResult.SelectedGates` *before* roll-up.
Evidence and readiness are then enforced by the **same** machinery as every other gate
(selection → `deriveEffectiveSeverity` → roll-up). Blocking-ness is encoded in each handoff
gate's `Maturity`: a `Failed`/`AutoSynthetic` effective evidence state, or a non-shippable /
diagnostic-bearing readiness block, yields a `block-on-*` maturity; satisfied states yield an
advisory `warn`. I/O (locating + reading handoff files) crosses the existing MVU boundary as a
new port/effect/msg in each host; the parse and map are pure. Absence of a handoff is a true
no-op (FR-001, SC-003).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (Constitution Engineering Constraints).

**Primary Dependencies**: BCL only for the new code — `System.Text.Json` for parsing (research
D2). **No new package**; `Directory.Packages.props` unchanged. Reuses `Kernel.Evidence` (F005),
`Config.Model`/`Gates.Model` (F014/F018), `Route.Model` (F019), `Enforcement` (F023),
`Ship` (F024), and the three host command loops (F022/F026/F056).

**Storage**: Read-only consumption of on-disk `readiness/<id>/governance-handoff.json` JSON
documents in a governed product. No new persisted artifact (the handoff gates appear in the
existing `gates.json`/`route.json`/`audit.json`/`verify.json` projections).

**Testing**: Expecto + YoloDev.Expecto.TestSdk (the repo standard; xUnit is absent from central
package management). Real-evidence: load on-disk fixture handoffs through the existing
Config→Gates→Routing→Route→Enforcement→Ship pipeline and assert verdict deltas — no mocks of
the pipeline (Constitution V). A new `FS.GG.Governance.Adapters.SddHandoff.Tests` plus additive
scope-guard/wiring tests in the three host command test projects.

**Target Platform**: Cross-platform .NET CLI/library (Linux/macOS/Windows), matching the repo.

**Project Type**: Single repo, multi-project F# solution (library + command hosts). New leaf
library project in the `Adapters.*` family + additive edits to three host command projects.

**Performance Goals**: Deterministic, byte-stable output; absence path is a strict no-op
(byte-identical to today, SC-003). No hot path; parse cost is bounded by handoff document size.

**Constraints**: Pure `update`/parse/map; I/O only at the interpreter edge (Constitution IV).
Imports no SDD source; changes no SDD-owned contract (FR-013, SC-006). Mapping matches ADR-0002
row-for-row; ADR + tutorial move with the code (FR-014, research D9).

**Scale/Scope**: One new library (~4–6 modules behind one `.fsi` each), additive surface on
three host commands, one new test project + additive host-test wiring, ADR + tutorial edits,
`.sln` + surface-baseline registration.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | ✅ | `.fsi` drafted first (contracts/); tested through the packed pipeline before `.fs` bodies. |
| II. Visibility in `.fsi` | ✅ | Every new public module gets a curated `.fsi`; no `private`/`internal`/`public` on `.fs` top-level bindings; new surface baseline + additive re-bless of three host baselines. |
| III. Idiomatic Simplicity | ✅ | Plain records/unions, pipelines; `System.Text.Json` reader; no SRTP/reflection/type-providers/custom CEs. Reuses `Evidence`/`Gates`/`Ship` verbatim. |
| IV. Elmish/MVU boundary for I/O | ✅ | Handoff file location + read is a new port/effect/msg in each host; `update` and the parse/map stay pure; interpretation at the edge (research D6). |
| V. Test Evidence Mandatory | ✅ | Real-evidence tests over on-disk fixtures through the live pipeline; failing-before/passing-after; no synthetic substitution of the pipeline. |
| VI. Observability & Safe Failure | ✅ | Malformed / version-mismatch / `autoSynthetic` ⇒ distinct descriptive diagnostics, never a crash or partial enforce (FR-002/011, research D5); absent input distinguished from defect. |

**Change Classification**: **Tier 1** (new public API surface: a new library + additive
`Loop.fsi`/`Interpreter.fsi` on three hosts; new dependency on the consumer from three hosts).
Requires the full chain: spec, plan, `.fsi`, surface baselines, tests, docs (ADR + tutorial).

**Gate result**: PASS — no unjustified violations. One *recorded deviation* (not a violation):
the spec's assumption that the consumer "conforms to the established [kernel] adapter SPI" is
superseded by research D3 — the consumer integrates via the gate/route/ship pipeline because the
kernel `Adapter` rule catalogs are not on the verdict path (FR-008 could not otherwise hold).
The consumer remains in the `Adapters.*` *project family*. See Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/081-sdd-handoff-consumer/
├── plan.md              # This file
├── research.md          # Phase 0 decisions (D1–D9)
├── data-model.md        # Phase 1 entities
├── quickstart.md        # Phase 1 validation guide
├── contracts/           # Phase 1 interface contracts
│   ├── handoff-document.md      # the SDD-owned JSON shape Governance reads (read-only)
│   ├── consumer-surface.md      # the new library's .fsi surface (parse/map/gates)
│   └── host-wiring.md           # the additive Loop/Interpreter surface on 3 hosts
├── checklists/
│   └── requirements.md  # (existing) spec quality checklist
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.Adapters.SddHandoff/          # NEW leaf library (research D1)
│   ├── FS.GG.Governance.Adapters.SddHandoff.fsproj
│   ├── Model.fsi / Model.fs           # typed in-memory handoff shape + contract version pin
│   ├── Reader.fsi / Reader.fs         # System.Text.Json parse → Result<Handoff, Diagnostic> (D2, D5)
│   ├── Mapping.fsi / Mapping.fs       # ADR-0002 evidence map → Evidence.build/effective (D4)
│   ├── Readiness.fsi / Readiness.fs   # readiness.* → typed Gate (D3, FR-009)
│   └── Consumer.fsi / Consumer.fs     # parse+map+readiness → (Gate list, SelectedGate list, Diagnostic list)
│
├── FS.GG.Governance.RouteCommand/      # EDIT (additive): Loop.fsi/.fs + Interpreter.fsi/.fs
├── FS.GG.Governance.ShipCommand/       # EDIT (additive): Loop.fsi/.fs + Interpreter.fsi/.fs
└── FS.GG.Governance.VerifyCommand/     # EDIT (additive): Loop.fsi/.fs + Interpreter.fsi/.fs
        # each gains: LoadHandoffs effect, HandoffsLoaded msg, Ports.Handoffs field,
        # and a pure post-Route.select fold unioning handoff gates before rollup (D6)

surface/
└── FS.GG.Governance.Adapters.SddHandoff.surface.txt   # NEW baseline
        # + ADDITIVE re-bless of RouteCommand/ShipCommand/VerifyCommand baselines

tests/
├── FS.GG.Governance.Adapters.SddHandoff.Tests/        # NEW (Expecto+YoloDev)
│   ├── fixtures/                       # committed example handoffs (satisfied / failing /
│   │                                   #   v2-major / malformed / autoSynthetic / stale / deferred)
│   ├── ReaderTests.fs                  # parse + version-check (US2; FR-002/011, SC-004)
│   ├── MappingTests.fs                 # ADR-0002 rows, each traceable to its row (SC-002)
│   ├── ReadinessGateTests.fs          # readiness → gate (US3; SC-005)
│   ├── ConsumerTests.fs               # end-to-end parse→gates over fixtures
│   └── SurfaceDriftTests.fs           # surface baseline + dependency hygiene
├── FS.GG.Governance.ShipCommand.Tests/    # EDIT (additive): real-pipeline verdict-delta test (SC-001)
├── FS.GG.Governance.VerifyCommand.Tests/  # EDIT (additive): readiness-gate-in-verdict + no-op (SC-003/005)
└── FS.GG.Governance.RouteCommand.Tests/   # EDIT (additive): handoff gates in gates.json/route.json

docs/
├── decisions/0002-sdd-governance-handoff-contract.md   # EDIT: close queue item #4; readiness→gate (FR-015, D9)
└── tutorials/sdd-governance-handoff.md                 # EDIT: readiness mapping row → gate (FR-014, D9)

FS.GG.Governance.sln                # EDIT: register new src + test projects
```

**Structure Decision**: New leaf library in the `Adapters.*` family (research D1), consumed by
the three existing host command projects via the gate/route/ship pipeline (research D3). All
real directories named above. Implementation order: spec → `.fsi` (contracts/) → semantic tests
→ `.fs` bodies (Constitution I), one user story / concern per commit (US2 reader → US1 evidence
+ host wiring → US3 readiness gate → ADR/tutorial lockstep).

## Complexity Tracking

| Deviation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Consumer integrates via the **gate/route/ship pipeline**, not the kernel `Adapter` SPI the spec *assumed* | FR-008 requires the handoff to demonstrably change a route/ship/verify verdict; verified (research D3) that those hosts derive verdicts only from `RouteResult` gates+findings and never read the kernel `Adapter` rule catalogs | Using the kernel `Adapter<'fact,'artifact,'change>` SPI produces `RuleOutcome`s no verdict host consumes — it would leave the handoff inert, failing the headline requirement. Project still lands in the `Adapters.*` family per the spec's placement intent. |
| Handoff diagnostics realized as a **blocking gate + diagnostic text**, not new F017 `FindingId` cases | Keeps a single verdict mechanism (gates) and a frozen, path-scoped `Findings` surface (research D5) | Extending `Findings.Model` widens an unrelated, path-scoped surface for a non-path concern with no behavioural gain. |
| **Three** host command public surfaces change (additive) | The handoff must affect `route`, `ship`, AND `verify` (FR-008 names all three); each owns its own typed `Loop`/`Interpreter` | A shared host shim was considered; the loops keep their own typed `Effect`/`Msg` by design (CommandHost precedent), so the shared work lives in the new pure adapter, called identically from each — no new host surface beyond the additive port/effect/msg. |
