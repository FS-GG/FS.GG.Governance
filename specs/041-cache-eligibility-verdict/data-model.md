# Phase 1 Data Model — Per-Gate Cache-Eligibility Verdict Core (F041)

The vocabulary of `FS.GG.Governance.CacheEligibility`. Every type is an immutable value; the reused types
(`GateId` from F018 `Gates.Model`; `FreshnessInputs` / `InputCategory` from F029 `FreshnessKey.Model`;
`ReuseStore` / `ReuseDecision` / `RecomputeCause` / `EvidenceRef` from F030 `EvidenceReuse.Model`) are opened from
their owning modules and **not redefined**. No field carries raw bytes, host paths, clock readings, or product
vocabulary. Compile order: `Model` → `CacheEligibility`.

## Reused verbatim (not redefined)

| Type | Origin | Use here |
|---|---|---|
| `GateId = GateId of string` | F018 `Gates.Model` | The selected gate's stable wire identity (`"<domain>:<checkId>"`) — the attribution key each verdict is placed under (research D2/D3). Opaque to this core. |
| `FreshnessInputs` | F029 `FreshnessKey.Model` | The already-resolved freshness inputs of a candidate gate; passed verbatim to F030 `decide`. Never resolved, fabricated, or re-hashed here (FR-009). |
| `InputCategory` | F029 `FreshnessKey.Model` | The changed-input category vocabulary, carried (via `RecomputeCause`) to name what differed on a *must-recompute* verdict. |
| `ReuseStore` | F030 `EvidenceReuse.Model` | The supplied recorded-evidence collection; consumed verbatim, recorded into never. |
| `ReuseDecision = Reuse of EvidenceRef \| Recompute of RecomputeCause` | F030 `EvidenceReuse.Model` | The single-candidate decision `evaluateGate` composes and relabels (research D4). Not re-exposed directly. |
| `RecomputeCause = NoPriorEvidence \| InputsChanged of InputCategory list` | F030 `EvidenceReuse.Model` | The no-hide cause carried verbatim by `MustRecompute` (FR-002). |
| `EvidenceRef = EvidenceRef of string` | F030 `EvidenceReuse.Model` | The opaque reusable-evidence reference carried verbatim by `Reusable` (FR-002). Never parsed or dereferenced. |

## New vocabulary (this feature — the minimal set FR-012 names)

### `CandidateGate` — the unit of input (FR-009, research D2)

```fsharp
type CandidateGate =
    { Gate: GateId            // supplied attribution key (F018); never derived from Inputs here
      Inputs: FreshnessInputs }   // already-resolved freshness inputs (F029); never resolved/re-hashed here
```

One selected gate's stable identity paired with the freshness inputs **already resolved** for it. Both fields are
supplied facts: the core does not resolve, fabricate, or re-hash the inputs, and does not derive, parse, or
cross-check the `GateId` against `Inputs.Check` / `Inputs.Domain` (research D2).

### `CacheEligibilityVerdict` — the two-outcome per-gate verdict (FR-001/FR-002/FR-010, research D4)

```fsharp
type CacheEligibilityVerdict =
    | Reusable of EvidenceRef          // prior evidence may be reused — carries the reusable reference (F030)
    | MustRecompute of RecomputeCause  // must recompute — carries the named cause (F030), verbatim
```

Exactly one of two outcomes, so a threshold-unmet or opaque yes/no verdict is **unrepresentable** (FR-001). A
`Reusable` verdict carries the evidence reference (FR-002) and is **necessary-not-sufficient**: it holds no skip
action, severity, ship verdict, or exit-code basis (FR-010). A `MustRecompute` verdict carries the no-hide cause —
`NoPriorEvidence`, or `InputsChanged` naming exactly the changed freshness-input categories (FR-002), reused
verbatim from F030. The relabel of F030's `ReuseDecision` introduces **no new reuse policy** (FR-004).

### `CacheEligibilityEntry` — one attributed verdict (FR-005, research D5)

```fsharp
type CacheEligibilityEntry =
    { Gate: GateId
      Verdict: CacheEligibilityVerdict }
```

One candidate gate's verdict attributed to its originating `GateId`, so a later projection can place it under the
correct gate (FR-005).

### `CacheEligibilityReport` — the per-change roll-up (FR-006, research D5)

```fsharp
type CacheEligibilityReport = CacheEligibilityReport of CacheEligibilityEntry list
```

One verdict per candidate gate, every gate preserved (none dropped, merged, or duplicated), in deterministic
`GateId`-ordinal order independent of supply order (FR-006). Single-case wrapper (the `ReuseStore` precedent); the
`entries` accessor unwraps it. `evaluate [] store` yields the empty report — a total, valid result, never an error
(Edge Cases).

## The decision (behaviour summary; full laws in `contracts/cache-eligibility-api.md`)

`evaluateGate candidate store` composes F030 verbatim and relabels:

| `EvidenceReuse.decide candidate.Inputs store` | `evaluateGate candidate store` |
|---|---|
| `Reuse ref` | `Reusable ref` |
| `Recompute cause` | `MustRecompute cause` |

`evaluate candidates store`:

```text
candidates
|> List.map (fun c -> { Gate = c.Gate; Verdict = evaluateGate c store })
|> List.sortWith (fun a b ->
       match String.CompareOrdinal (gateIdValue a.Gate, gateIdValue b.Gate) with
       | 0 -> compare a b      // total, input-order-independent tiebreak (structural; no key computed)
       | n -> n)
|> CacheEligibilityReport
```

Total, deterministic, pure over the supplied facts; identical candidates + store ⇒ byte-identical report. Recompute
is the default: a `Reusable` verdict appears **iff** F030 `decide` returns `Reuse` for that candidate's inputs
against the store (FR-003/FR-004). Ordering computes **no freshness key / hash** (FR-008) — only ordinal string
comparison on `gateIdValue` plus structural comparison as a duplicate-`GateId` tiebreak (research D5).

## State & relationships

No state, no transitions, no identity derivation — this is a *decision / roll-up* core, not a record core. The
relationships are `(CandidateGate, ReuseStore) → CacheEligibilityVerdict` via `evaluateGate` and `(CandidateGate
list, ReuseStore) → CacheEligibilityReport` via `evaluate`. The projections `isReusable` / `reusableEvidence` /
`recomputeCause` map a `CacheEligibilityVerdict` to `bool` / `EvidenceRef option` / `RecomputeCause option`;
`entries` unwraps the report.
