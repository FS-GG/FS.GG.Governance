# Phase 1 Data Model: Broad-Route Cost Explanation Core (F031)

All types live in `FS.GG.Governance.RouteExplain.Model` and are declared **only** in `Model.fsi` (Principle
II). They reuse the F019/F018/F014 vocabulary verbatim (opened from `FS.GG.Governance.Route.Model`,
`FS.GG.Governance.Gates.Model`, `FS.GG.Governance.Config.Model`) and add the minimum new shape. Every type is
a plain F# record or single closed DU with structural equality/ordering.

## Reused upstream types (not redefined)

| Type | Origin | Used for |
|---|---|---|
| `RouteResult` = `{ SelectedGates; Findings; Cost }` | F019 `Route.Model` | the route being explained (input) |
| `SelectedGate` = `{ Gate; SelectingPaths }` | F019 `Route.Model` | embedded whole in each finding (D2) |
| `SelectingPath` = `{ Path; MatchedGlob }` | F019 `Route.Model` | the route trace (changed path + matched rule) |
| `GateRegistry` = `{ Gates }` | F018 `Gates.Model` | the catalog of candidate alternatives (input) |
| `Gate` = `{ Id; Domain; Description; Prerequisites; Cost; Timeout; Owner; Maturity; ProductCheck; FreshnessKey }` | F018 `Gates.Model` | high-cost gate + alternative gate |
| `Cost` = `Cheap \| Medium \| High \| Exhaustive` | F014 `Config.Model` | high-cost threshold + strict-cheaper test (structural order) |
| `EnvironmentClass` = `Local \| Ci \| LocalOrCi \| Release` | F014 `Config.Model` | local-permission test (via `Gate.FreshnessKey.Environment`) |
| `DomainId`, `GateId`, `CheckId` | F014 `Config.Model` / F018 | same-domain test, ordering, identity |

## New types (this feature)

### `AlternativeOutcome` (closed DU)

```fsharp
type AlternativeOutcome =
    | CheaperLocalAlternative of Gate
    | NoCheaperLocalAlternative
```

- The no-hide result for one high-cost finding (FR-006). **Always present** on a finding — there is no
  "absent/unknown" third state.
- `CheaperLocalAlternative g`: `g` is a registry gate that is **same-domain**, **strictly cheaper**, and
  **locally runnable** relative to the finding's gate (D4/D6). `g` is carried **verbatim** (its full F018
  metadata), not a reduced projection — a consumer reads `g.Id`/`g.Cost`/`g.FreshnessKey.Environment` as
  needed.
- `NoCheaperLocalAlternative`: the explicit "none" — emitted when no registry gate qualifies. Never omitted,
  never null.

### `HighCostFinding` (record)

```fsharp
type HighCostFinding =
    { Selected: SelectedGate          // F019 selected gate (Gate + route trace), verbatim (D2)
      Alternative: AlternativeOutcome } // resolved cheaper-local alternative (D4)
```

- One per selected gate whose `Selected.Gate.Cost >= High` (D3). Carries the design's six fields through
  `Selected` (selected gate id, cost, affected capability domain, and the changed-path/matched-rule trace)
  plus the resolved `Alternative`.
- No raw YAML, host path, timestamp, severity, enforcement, freshness verdict, or ship verdict — only the
  embedded F019/F018 values (FR-010).

### `RouteExplanation` (record)

```fsharp
type RouteExplanation =
    { Findings: HighCostFinding list }  // sorted by Selected.Gate.Id ordinal (D5); [] is a valid success
```

- The deterministic explanation of a route's high-cost gates (FR-002). `Findings` is ordered by
  `Selected.Gate.Id` ordinal so it is independent of input order (FR-008). An empty `Findings` is a valid,
  successful "no broad route to explain" (FR-011) — never an error, never a "select everything" fallback.

## Validation / invariants (enforced by construction, asserted by tests)

| Invariant | Source | Test |
|---|---|---|
| Exactly the selected gates with `Cost >= High` produce a finding; none below | FR-004, D3 | HighCostFinding (all `Cost` tiers) |
| Each finding's `Selected` equals the F019 selected gate verbatim (gate + every selecting path) | FR-005, D2 | HighCostFinding |
| Every finding carries a present `Alternative` (named or explicit none) | FR-006, D4 | Alternative |
| A named alternative is same-`Domain` ∧ `Cost <` finding's ∧ `FreshnessKey.Environment ∈ {Local, LocalOrCi}` | FR-006, D4/D6 | Alternative (+ each failing condition ⇒ none) |
| When several candidates qualify, the named one is cheapest then least `GateId` | FR-007, D4 | Alternative (deterministic tie-break) |
| `Findings` ordered by `GateId`; reordering/duplicating inputs never changes the explanation | FR-008, D5 | Determinism |
| Empty route / no high-cost gate ⇒ `{ Findings = [] }` | FR-011 | EmptyRoute |
| Pure: identical inputs ⇒ identical explanation across cwd/time/fs | FR-003/FR-008, SC-006 | Purity |

## Why these types and not others

- **Embed `SelectedGate`, don't flatten it (D2)** — keeps "consume F019 verbatim" literal and the new surface
  minimal; the route trace and gate metadata are already deterministically typed upstream.
- **`AlternativeOutcome` is a 2-case DU, not an `option`** — the explicit `NoCheaperLocalAlternative`
  constructor names the no-hide "none" at the type level (Principle VI), clearer than `Gate option` whose
  `None` reads as "unknown/absent."
- **`RouteExplanation` wraps a list** — a record (rather than a bare `HighCostFinding list`) leaves room for a
  later row to add aggregate fields (e.g. a per-tier high-cost count) without breaking the type, while keeping
  today's surface to exactly one field.
</content>
