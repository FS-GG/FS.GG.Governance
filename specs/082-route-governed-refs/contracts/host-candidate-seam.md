# Contract: the three-host candidate-assembly seam

**Hosts**: `FS.GG.Governance.RouteCommand`, `…ShipCommand`, `…VerifyCommand` (`Loop.fs`).
**Classification**: internal to `update`'s `Loaded(Valid)` arm — **no `Loop.fsi` change**, no
host surface baseline change.

## The edit (identical in all three hosts)

Today each host's `Loaded(Valid facts)` arm opens with:

```fsharp
let candidates = model.Candidates |> Option.defaultValue []
let report = Routing.route facts candidates
```

Replace with:

```fsharp
// F082: promote the handoff's declared governedReferences to first-class routing candidates,
// merged + de-duplicated with the sensed changed paths BEFORE routing (FR-001/FR-002/FR-006).
// Absent / empty / bad handoff ⇒ candidatePaths = [] ⇒ candidates unchanged ⇒ byte-identical
// route/ship/verify output (FR-005, SC-002). The F081 post-select consume-union fold below is
// UNCHANGED — the handoff's own evidence/readiness/integrity gates stay pre-selected (FR-009).
let sensed   = model.Candidates |> Option.defaultValue []
let declared = Consumer.candidatePaths model.Handoffs
let candidates = sensed @ declared |> List.distinct
let report = Routing.route facts candidates
```

Everything after `Routing.route` — `Gates.buildRegistry`, `Findings.findUnknownGovernedPaths`,
`Route.select`, the F081 `Consumer.consume` gate-union fold, `Ship.rollup` (Ship/Verify),
Verify's empty-selection short-circuit, product-surface classification — is **untouched**.

`Consumer` is already opened in all three hosts (`open FS.GG.Governance.Adapters.SddHandoff`,
F081). No new `open`, `Effect`, `Msg`, `Port`, `Phase`, or `Model` field.

## Invariants

| ID | Invariant |
|----|-----------|
| H1 | **No-op identity.** When `model.Handoffs = []` OR no consumable handoff declares `governedReferences`, `declared = []`, so `candidates = sensed @ [] \|> List.distinct ≡ sensed` (already distinct) — byte-identical route/ship/verify output (FR-005, SC-002). |
| H2 | **Additive selection.** Merging can only *add* candidates ⇒ can only *add* `Routed` results ⇒ can only *add* selected gates / selecting paths; it removes none (FR-004). |
| H3 | **Dedup before route.** A path in both `sensed` and `declared` survives once (`List.distinct`), so it is routed once, recorded once per gate, counted once in the cost rollup (FR-006, SC-003). |
| H4 | **Real glob provenance.** A domain gate selected from a declared path carries the real path-map glob (from `Route.select`), never the self-glob `consume` uses on the handoff's own gates (FR-003, SC-006). |
| H5 | **Bad document boundary.** A malformed / version-mismatched handoff contributes zero candidates (via `candidatePaths`) yet its blocking integrity gate still appears (via the unchanged `consume` fold) (FR-008, SC-005). |
| H6 | **No host divergence.** The three hosts apply the identical merge; declared paths contribute identically to candidates in `route`/`ship`/`verify` (spec Edge case). |
| H7 | **Order independence.** Output is independent of handoff-document order and of the `sensed`/`declared` concatenation order, because `Routing.route` sorts routings by normalized path and `Route.select` sorts gates by `GateId` / paths by normalized path (FR-010). |

## Verification step (research D8)

Before editing, confirm the three host surface-drift / scope-guard tests (relaxed in F081 to
permit the `Adapters.SddHandoff` edge) gate at **assembly-reference** granularity. If any
enumerates the exact adapter members a host may call, extend it additively to permit
`candidatePaths` alongside `consume`. Most likely no change is needed (assembly-level guard).
