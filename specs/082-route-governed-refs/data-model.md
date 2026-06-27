# Phase 1 Data Model: Promote `governedReferences` to First-Class Routing Facts

**Feature**: `082-route-governed-refs` | **Date**: 2026-06-27

This feature introduces **no new types**. It reuses existing types and adds one function that
projects from them. The "data model" here is the *flow* of existing entities through the
candidate-assembly seam.

## Entities (all pre-existing — no shape change)

| Entity | Type (existing) | Role in this feature |
|--------|-----------------|----------------------|
| **Governed reference** | `Model.GovernedReference = { WorkItem: string; Paths: GovernedPath list }` | The declared `{ workItem, paths }` carried in a handoff. **Unchanged.** Source of the new candidates. |
| **Governed path** | `Config.Model.GovernedPath of string` | A normalized path. Both the sensed and declared candidate sources produce these. **Unchanged.** Normalized at read time by `Reader.parse`. |
| **Handoff read** | `Reader.HandoffRead = { Source: string; Json: string }` | One located `readiness/<id>/governance-handoff.json` (raw). Input to `candidatePaths`. **Unchanged.** |
| **Routing candidate** | `GovernedPath` (list element) | A path submitted to `Routing.route`. **This feature adds declared paths as a second source**, merged + de-duplicated with the sensed change set. |
| **Route report** | `Routing.Model.RouteReport` | Per-path `RoutingResult` (`Routed`/`UnmatchedInRoot`/`OutOfScope`) + diagnostics. Now also covers the declared paths. **Unchanged type.** |
| **Selecting path** | `Route.Model.SelectingPath = { Path: GovernedPath; MatchedGlob: GovernedPath }` | Provenance on a selected gate. Declared-path-driven selection records the **real** matched glob (via `Route.select`). **Unchanged shape** (no new `source` discriminator — spec Assumption). |
| **Selected gate** | `Route.Model.SelectedGate = { Gate; SelectingPaths }` | A chosen gate + its provenance. May now include domain gates reached only by declared paths. **Unchanged type.** |
| **Consume result** | `Consumer.ConsumeResult = { Gates; Selected; Diagnostics }` | F081's handoff-own gate output. **Unchanged** — this feature does NOT add a field to it (see research D1). |

## New function (the only added surface)

```fsharp
// FS.GG.Governance.Adapters.SddHandoff.Consumer
val candidatePaths: reads: Reader.HandoffRead list -> GovernedPath list
```

- **Pure, total** — never throws (Constitution VI).
- **Input**: the located handoff reads (the same list the host already holds in `model.Handoffs`).
- **Output**: de-duplicated, deterministically-ordered declared `GovernedPath`s drawn ONLY
  from documents that `Reader.parse` accepts (`Ok`). A bad/version-mismatched document
  contributes nothing (FR-008).
- **No-op**: `candidatePaths [] = []`, and `candidatePaths reads = []` whenever no consumable
  document declares any `governedReferences` (FR-005, SC-002).

## Validation rules (enforced by reuse, not new code)

| Rule | Where enforced |
|------|----------------|
| Declared paths are normalized | `Reader.parse` (existing, line 229) |
| Bad document ⇒ no candidates | `candidatePaths` keeps only `Ok` parses (D4) |
| Same path from both sources ⇒ once | `List.distinct` on the merged candidate list, before `Routing.route` (D2, FR-006) |
| Declared path out of root ⇒ nothing | `Routing.route` ⇒ `OutOfScope` selects no gate (existing, FR-007) |
| Declared path in-root unmatched ⇒ finding | `Findings.findUnknownGovernedPaths` over the enriched report (existing, FR-007) |
| Selected gates ordered by `GateId`; paths by normalized path | `Route.select` (existing, FR-010, SC-006) |

## Data flow (the candidate-assembly seam)

```text
model.Handoffs : Reader.HandoffRead list
        │
        ├─ Consumer.candidatePaths ─────► declared : GovernedPath list   (F082, NEW call)
        │                                     │
sensed (model.Candidates) ── @ ──────────────┘
        │
        ▼   |> List.distinct                          (FR-006 dedup)
   candidates : GovernedPath list
        │
        ▼  Routing.route facts candidates
   RouteReport ──► Findings.findUnknownGovernedPaths ──► Route.select ──► RouteResult
        │                                                                     │
        └──────────────────── (F081, UNCHANGED) ──────────────────────────────┤
                       Consumer.consume model.Handoffs                         │
                          └─ union handoff-own gates into registry/selection ──┘
                                                                               │
                                                          Ship.rollup / projection / verdict
```

The **only** new arrows are `candidatePaths` and the `@ … |> List.distinct` merge. Everything
downstream is the existing pipeline. When `declared = []` the merge is `sensed @ [] |> List.distinct`
≡ `sensed` (already distinct) — a byte-identical identity transform (FR-005).

## State transitions

None new. The existing MVU lifecycle is unchanged: `LoadHandoffs` (init) → `HandoffsLoaded`
(stores `model.Handoffs`) → `Loaded(Valid)` (the candidate merge + routing + selection happen
here). No new `Effect`, `Msg`, `Phase`, or `Model` field.
