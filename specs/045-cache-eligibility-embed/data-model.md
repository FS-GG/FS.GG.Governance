# Phase 1 Data Model: Embed Cache-Eligibility Verdicts in route.json and audit.json

This row introduces **no new F# types**. It consumes the F041 `CacheEligibilityReport` verbatim and renders it
through the two existing projections. The "model" here is therefore (a) the reused inputs, (b) the new wire
shapes the render emits, (c) the match/reconciliation rule, and (d) the laws the render must satisfy.

## Reused inputs (all verbatim — none redefined)

| Type / accessor | Source | Role in the embed |
|---|---|---|
| `RouteResult` | F019 `Route.Model` | route.json's existing input — unchanged |
| `ShipDecision` | F024 `Ship.Model` | audit.json's existing input — unchanged |
| `CacheEligibilityReport` | F041 `CacheEligibility.Model` | the **new** optional second input (single-case wrapper of `CacheEligibilityEntry list`) |
| `CacheEligibilityEntry` `{ Gate: GateId; Verdict: CacheEligibilityVerdict }` | F041 | one attributed per-gate verdict |
| `CacheEligibilityVerdict = Reusable of EvidenceRef \| MustRecompute of RecomputeCause` | F041 | the closed two-outcome verdict |
| `RecomputeCause = NoPriorEvidence \| InputsChanged of InputCategory list` | F030 `EvidenceReuse.Model` | the no-hide cause carried by `MustRecompute` |
| `EvidenceRef` | F030 | the opaque evidence reference (echoed, never dereferenced) |
| `CacheEligibility.entries: CacheEligibilityReport -> CacheEligibilityEntry list` | F041 | unwrap the report |
| `EvidenceReuse.referenceValue: EvidenceRef -> string` | F030 | render the opaque reference verbatim |
| `FreshnessKey.categoryToken: InputCategory -> string` | F029 | render each changed category token |
| `Gates.gateIdValue: GateId -> string` | F018 | the match key + the rendered gate id (already used by both projections) |

The supplied `GateId` / `EvidenceRef` / `RecomputeCause` are **opaque facts produced elsewhere**: the projection
never resolves, re-hashes, parses, or dereferences them (FR-011) — it renders only what the report already
carries.

## New optional input

```fsharp
// the new second parameter on each projection
cache: CacheEligibilityReport option
//   None        -> the not-evaluated state (no cache step ran; today's fsgg route / fsgg ship)
//   Some report -> an evaluated report (Some (CacheEligibilityReport []) is evaluated-but-empty, distinct from None)
```

## Internal render structures (private — absent from both `.fsi`)

```fsharp
// First-by-report-order-wins lookup from the report (D4). Built once per projection call.
//   key   = gateIdValue entry.Gate (the rendered GateId string)
//   value = entry.Verdict
// On a duplicate GateId (F041 keeps duplicate candidates), the FIRST entry by the report's
// LIST POSITION wins (the fold keeps the earliest add) — deterministic and total, keyed purely
// on `CacheEligibility.entries` order, NOT re-derived from GateId. F041's `evaluate` already
// emits entries in GateId-ordinal order, so in practice list position coincides with that
// ordering; the rule does not depend on that coincidence.
let verdictByGate (report: CacheEligibilityReport) : Map<string, CacheEligibilityVerdict> =
    CacheEligibility.entries report
    |> List.fold (fun m e ->
        let k = gateIdValue e.Gate
        if Map.containsKey k m then m else Map.add k e.Verdict m) Map.empty
```

The per-gate render then resolves each document gate against this map (or the `None` case):

```text
verdict-for-gate(gateId) =
    match cache with
    | None              -> NotEvaluated
    | Some report ->
        match Map.tryFind (gateIdValue gateId) (verdictByGate report) with
        | Some v -> v            // Reusable _ | MustRecompute _
        | None   -> NotEvaluated // listed in the document, absent from the report (FR-005)
```

## Wire shapes the render emits

### Per-gate `cacheEligibility` verdict object (the reused F042 vocabulary + one new case)

| Verdict | JSON |
|---|---|
| `Reusable ref` | `{ "kind":"reusable", "evidence": "<referenceValue ref>" }` |
| `MustRecompute NoPriorEvidence` | `{ "kind":"mustRecompute", "cause": { "kind":"noPriorEvidence" } }` |
| `MustRecompute (InputsChanged cats)` | `{ "kind":"mustRecompute", "cause": { "kind":"inputsChanged", "categories": ["<categoryToken c>", …] } }` |
| *not evaluated* (no entry / `None`) | `{ "kind":"notEvaluated" }` |

- `kind` is always the first field. `reusable` carries only `evidence` (the opaque reference verbatim — no
  parse, no dereference, FR-011). `mustRecompute` always carries a `cause` and never an `evidence` field
  (no-hide, FR-009). `inputsChanged` names exactly the report's categories in the report's order — none dropped,
  added, or truncated (FR-009); `noPriorEvidence` has **no** `categories` field (distinct from `inputsChanged`
  with `categories: []`). `notEvaluated` is a distinct third case — **never** rendered as `reusable` (FR-005).
- The `match` over `CacheEligibilityVerdict` and `RecomputeCause` is **exhaustive with no wildcard**, so a
  future F041 verdict/cause case is a compile error here, never a silently mis-tokened field (the F042
  precedent).

### Top-level `cacheEligibilityEvaluated` boolean (the always-present section)

`false` when `cache = None`; `true` when `cache = Some _` (including an evaluated-empty report). Always present,
so the empty route / clean empty decision still carries the cache-eligibility signal (FR-012, the empty-route
edge).

### Enriched `route.json` document (v2)

Top-level field order: `schemaVersion` (`"fsgg.route/v2"`) → `selectedGates` → `findings` → `cost` →
**`cacheEligibilityEvaluated`**.

Each `selectedGates` entry: all existing fields verbatim (`id`, `domain`, `description`, `cost`, `timeout`,
`owner`, `maturity`, `productCheck`, `prerequisites`, `freshnessKey`, `selectingPaths`) → **`cacheEligibility`**
(the verdict object). The `findings` array entries are **unchanged** (no verdict — FR-004).

### Enriched `audit.json` document (v2)

Top-level field order: `schemaVersion` (`"fsgg.audit/v2"`) → `verdict` → `exitCodeBasis` → `blockers` →
`warnings` → `passing` → **`cacheEligibilityEvaluated`**.

Each **`kind:"gate"`** item (in any section): existing fields verbatim (`kind`, `id`, `enforcement`) →
**`cacheEligibility`** (the verdict object). Each **`kind:"finding"`** item is **unchanged** — no
`cacheEligibility` field (FR-004, SC-002).

## Laws (the render must satisfy these — tested in Phase V)

- **L1 (match by GateId, verbatim)**: a gate entry/item carries the verdict the report attributes to its
  `gateIdValue`; no re-parse of the `GateId` (US1.1–US1.3, US2.1, SC-001).
- **L2 (not-evaluated, never reusable)**: a gate listed but absent from the report, or any gate under `None`,
  renders `{ kind:"notEvaluated" }`; no gate is ever `reusable` without a matching report entry (US1.4, US3.2,
  SC-005).
- **L3 (no-hide cause)**: every `mustRecompute` names its full cause — `noPriorEvidence`, or the complete
  changed-category list in report order, never truncated/substituted (US3.1, SC-005).
- **L4 (gate-scoped)**: route.json findings and audit.json `kind:"finding"` items carry no verdict (US2.2,
  SC-002).
- **L5 (orphan dropped)**: a report entry whose `GateId` matches no document gate adds no gate and no verdict
  (Edge: orphan; FR-006).
- **L6 (duplicate reconciliation)**: a duplicate `GateId` in the report resolves to the first entry by the
  report's **list position** (`CacheEligibility.entries` order), deterministically and totally — the rule keys
  on list position, never re-derived from the `GateId` value (Edge: duplicate; FR-007).
- **L7 (additive / no-hide of enforcement)**: every non-cache field is byte-identical to the pre-embed F020/F025
  projection of the same input (modulo the section + version); a `reusable` verdict on a base-`Blocking` gate
  leaves it a blocker with full enforcement detail; the cache verdict changes no verdict, severity, section, or
  ship outcome (US3, US1/US2 acceptance, SC-004).
- **L8 (no derivation / no raw inputs)**: the document carries no raw freshness input, no hash, no computed
  freshness key, and no severity/enforcement/skip field derived from the cache verdict; the evidence reference
  is verbatim and never dereferenced (US3.3, SC-007).
- **L9 (present section)**: `cacheEligibilityEvaluated` is always present; `false` under `None`, `true` under
  `Some _`; the empty route / clean empty decision is a valid success with the section present (Edge: empty;
  SC-006).
- **L10 (determinism / order)**: identical inputs ⇒ byte-identical text; value-equal inputs from differently-
  ordered upstreams ⇒ identical text; cache entries follow the document's existing gate order (US4, SC-003).
- **L11 (totality)**: `None`, `Some (CacheEligibilityReport [])`, empty route, clean empty decision, and
  finding-only route all return a document and never throw (SC-006).
- **L12 (F042/F044 untouched)**: zero edits to `CacheEligibilityJson` / F044 cores or baselines (SC-008,
  FR-015).
