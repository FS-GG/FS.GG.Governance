# Implementation Plan: Promote `governedReferences` to First-Class Routing Facts

**Branch**: `082-route-governed-refs` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/082-route-governed-refs/spec.md`

## Summary

Today F081 reads an SDD→Governance handoff and turns it into gates, but the handoff's
declared `governedReferences` (`{ workItem, paths }`) only decorate the handoff's own
pre-selected gates with synthetic self-glob provenance; they do **not** influence which
*other* domain gates (build, test, evidence-integrity, …) get selected. This feature
promotes those declared paths to **first-class routing candidates**: they are merged and
de-duplicated with the sensed changed paths and fed through the *same*
`Routing.route → Findings → Route.select` machinery, so the surface a work item *declares*
it governs drives gate selection — not only the files that happen to appear in the git diff.

**Technical approach** (the minimal, surgical seam): add ONE pure, total public function to
the existing consumer adapter — `Consumer.candidatePaths : Reader.HandoffRead list ->
GovernedPath list` — that parses every located document and returns the de-duplicated
declared paths from the **consumable** ones only (a bad/version-mismatched document
contributes nothing, FR-008). Each of the three verdict hosts (`route`/`ship`/`verify`)
merges those declared paths into the candidate list *before* `Routing.route`, leaving the
rest of `Loaded(Valid)` — including F081's post-select gate-union fold — untouched. Declared
paths are already normalized at read time (`Reader.parse`, line 229), so dedup against the
sensed paths is value-equality clean. The no-handoff path stays an identity transform
(`candidatePaths [] = []`), preserving every existing byte-identical golden (FR-005).

No production *core* is touched: `Routing.route` and `Route.select` are unchanged; the only
new surface is the one adapter function (`.fsi` + baseline, additive — Tier 1) and three
internal host edits (no host `.fsi` change). ADR-0002 item #3 moves from "Optional: fold…
or ignore" to **Resolved**, and the handoff tutorial gains a worked example of declared
paths driving selection (FR-012).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard).

**Primary Dependencies**: None new. Reuses existing in-repo libraries only —
`FS.GG.Governance.Adapters.SddHandoff` (`Reader`/`Consumer`/`Model`),
`FS.GG.Governance.Routing` (`Routing.route`), `FS.GG.Governance.Route` (`Route.select`),
`FS.GG.Governance.Config.Model` (`GovernedPath`, `normalizePath`). BCL-only, zero new
package, NO SDD `ProjectReference` — the F081 posture is preserved (Assumption: consumer-side
only).

**Storage**: N/A (pure transform over already-located handoff reads; no new I/O — the
existing `Interpreter.Ports.Handoffs` port already locates `readiness/<id>/governance-handoff.json`).

**Testing**: Expecto + YoloDev runner (repo standard). Real-evidence discipline: drive the
**real** `Config→Gates→Routing→Route` pipeline through the three host `update` functions; no
synthetic routing facts, no mocks of the selection algorithm. Adapter-level unit tests for
`candidatePaths` over real `HandoffRead` JSON fixtures.

**Target Platform**: Linux/CI + dev (host-agnostic, pure cores).

**Project Type**: Single-repo F# library + three MVU command hosts (governance tooling).

**Performance Goals**: Determinism over throughput — byte-identical output for identical
input (FR-010). `candidatePaths` parses each small handoff JSON once per command run
(negligible; the documents are already read into memory by the host port).

**Constraints**: PURE + TOTAL new function (never throws — Constitution VI). Additive-only
selection (FR-004): may add gates, must never remove a gate or drop a selecting path. The
no-handoff / empty / bad-document paths MUST stay byte-identical (FR-005, SC-002).

**Scale/Scope**: 1 new public function (adapter) + its `.fsi`/baseline line; 3 host
`Loop.fs` edits (~3 lines each, no `.fsi` change); adapter + three host test additions; ADR
+ tutorial doc updates. No `src/` *core* (Routing/Route/Gates/Config) change.

## Constitution Check

*GATE: re-evaluated after Phase 1 design — still PASS.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic tests → Impl | ✅ | New `candidatePaths` signature is drafted in `contracts/` (FSI-shaped) before the `.fs` body; semantic tests exercise it and the three hosts through the public pipeline. |
| II. Visibility lives in `.fsi` | ✅ | The new function is declared in `Consumer.fsi`; `Consumer.fs` carries no access modifiers. Surface baseline `surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt` re-blessed additively (BLESS_SURFACE=1). |
| III. Idiomatic simplicity | ✅ | Plain `List.choose`/`List.collect`/`List.distinct` pipeline; no SRTP, reflection, custom operators, or non-trivial CEs. Host edit is a 3-line merge. |
| IV. Elmish/MVU boundary | ✅ | No new I/O; the existing `LoadHandoffs`/`HandoffsLoaded`/`Ports.Handoffs` MVU edge (F081) is reused unchanged. The candidate merge is pure, inside `update`'s `Loaded(Valid)` arm. No new `Effect`/`Msg`/port. |
| V. Test evidence mandatory | ✅ | Real-evidence pipeline tests fail before / pass after; a failing-evidence verdict-flip scenario (SC-004) and the byte-identical no-op guards (SC-002) are added. No synthetic routing facts. |
| VI. Observability / safe failure | ✅ | `candidatePaths` is total — a bad document yields no candidates (and its blocking integrity gate still fires via the unchanged `consume` path, FR-008); no swallowed exceptions, distinct diagnostics preserved. |
| Change Classification | ✅ Tier 1 | Adds public API surface (one adapter function) ⇒ full chain: spec, plan, `.fsi`, baseline, tests, docs. Declared in spec (Assumption: Tier 1). |
| Engineering constraints | ✅ | No new dependency; `net10.0`; adapter stays BCL-only / no SDD reference; generic (no rendering assumptions). |

**Result**: PASS, no violations — Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/082-route-governed-refs/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — design decisions (D1–D9)
├── data-model.md        # Phase 1 — entities & data flow
├── quickstart.md        # Phase 1 — runnable validation scenarios
├── contracts/
│   ├── consumer-candidatePaths.fsi.md   # the new adapter signature contract
│   └── host-candidate-seam.md           # the three-host candidate-merge contract
├── checklists/
│   └── requirements.md  # (pre-existing)
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Adapters.SddHandoff/
├── Consumer.fsi         # + val candidatePaths  (additive — Tier 1)
├── Consumer.fs          # + candidatePaths body (parse → collect → distinct)
├── Reader.fs/.fsi       # UNCHANGED (already normalizes declared paths, line 229)
└── Model.fs/.fsi        # UNCHANGED (GovernedReference shape unchanged)

src/FS.GG.Governance.RouteCommand/Loop.fs    # Loaded(Valid): merge declared candidates before Routing.route
src/FS.GG.Governance.ShipCommand/Loop.fs     #   (same edit; no Loop.fsi change)
src/FS.GG.Governance.VerifyCommand/Loop.fs   #   (same edit; no Loop.fsi change)

surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt   # re-blessed additively (+ candidatePaths)

tests/FS.GG.Governance.Adapters.SddHandoff.Tests/
├── ConsumerTests.fs     # + candidatePaths cases (consumable-only, dedup, bad-doc ⇒ [])
└── SurfaceDriftTests.fs # baseline file re-read (no in-test literal — BLESS_SURFACE=1)

tests/FS.GG.Governance.RouteCommand.Tests/   # + US1/US2/US3 governed-routing scenarios
tests/FS.GG.Governance.ShipCommand.Tests/    # + SC-004 verdict-flip scenario
tests/FS.GG.Governance.VerifyCommand.Tests/  # + SC-005 strict-blocking scenario

docs/decisions/0002-sdd-governance-handoff-contract.md   # item #3 → Resolved (F082)
docs/tutorials/sdd-governance-handoff.md                 # worked example: declared paths drive selection
```

**Structure Decision**: Single-project F# layout (the repo's existing shape). The change is
contained to one leaf adapter (`Adapters.SddHandoff`) plus the candidate-assembly seam in the
three command hosts. No new project, no new dependency edge — the hosts already reference the
adapter (F081); they call one additional already-permitted public member.

## Complexity Tracking

> No Constitution violations — section intentionally empty.
