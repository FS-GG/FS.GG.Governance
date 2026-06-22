# Contract: `audit.json` v2 — cache-eligibility embed (F045)

The wire delta to the F025 `audit.json` document. The function is
`AuditJson.ofShipDecision: ShipDecision -> CacheEligibilityReport option -> string`. The per-gate
`cacheEligibility` verdict object is the **shared vocabulary** fixed in
[route-json-document.md](./route-json-document.md) — `reusable` / `mustRecompute` (`noPriorEvidence` |
`inputsChanged`) / `notEvaluated` — reused here verbatim.

## Top-level shape (v2)

Field order (fixed): `schemaVersion`, `verdict`, `exitCodeBasis`, `blockers`, `warnings`, `passing`,
**`cacheEligibilityEvaluated`**.

| Field | Delta | Value |
|---|---|---|
| `schemaVersion` | **changed** | `"fsgg.audit/v2"` (was `"fsgg.audit/v1"`) |
| `verdict` | **unchanged** | `pass` \| `fail` — never recomputed, never relaxed by a cache verdict (FR-008) |
| `exitCodeBasis` | **unchanged** | `clean` \| `blocked` |
| `blockers` / `warnings` / `passing` | each **gate** item gains a trailing `cacheEligibility` field; finding items unchanged | partition/order unchanged |
| `cacheEligibilityEvaluated` | **new** (appended last) | `false` when `cache = None`; `true` when `cache = Some _` |

`cacheEligibilityEvaluated` is **always present** (FR-012). A clean empty decision (all three sections empty)
still carries it.

## Per-item delta

The audit projection discriminates items by `kind`. The embed touches **only the gate arm**:

| Item `kind` | Delta |
|---|---|
| `"gate"` | keeps `kind`, `id`, `enforcement` verbatim and gains a trailing **`cacheEligibility`** verdict object, matched to the item by `id` (`gateIdValue`, verbatim) |
| `"finding"` | **unchanged** — keeps `kind`, `id`, `path`, `enforcement`; carries **no** `cacheEligibility` field (cache is gate-scoped — FR-004, SC-002) |

## Example (a relaxed-blocker gate in `warnings`, `Some report`)

```json
{
  "schemaVersion": "fsgg.audit/v2",
  "verdict": "pass",
  "exitCodeBasis": "clean",
  "blockers": [],
  "warnings": [
    { "kind": "gate", "id": "build:rel",
      "enforcement": { "baseSeverity": "blocking", "maturity": "blockOnRelease", "mode": "gate",
                       "profile": "light", "effectiveSeverity": "advisory", "reason": "…" },
      "cacheEligibility": { "kind": "reusable", "evidence": "sha256:abc…" } }
  ],
  "passing": [],
  "cacheEligibilityEvaluated": true
}
```

The gate stays in `warnings` with its full six-field enforcement detail; the `reusable` verdict sits beside it
and changes neither the section nor the ship verdict (US3, the `reusable`-on-base-`blocking` edge).

## Example (`cache = None`, today's `fsgg ship`) — the F028 golden-snapshot shape

```json
{
  "schemaVersion": "fsgg.audit/v2",
  "verdict": "pass",
  "exitCodeBasis": "clean",
  "blockers": [],
  "warnings": [
    { "kind": "gate", "id": "build:rel", "enforcement": { "…": "…" },
      "cacheEligibility": { "kind": "notEvaluated" } }
  ],
  "passing": [],
  "cacheEligibilityEvaluated": false
}
```

This is exactly the delta the 7 re-blessed `fixtures/enforcement/audit-snapshots/*.audit.json` show: the version
bumps, the top-level flag is `false`, each gate item gains `cacheEligibility: { kind:"notEvaluated" }`, and every
other byte is unchanged (SC-004).

## Guarantees

- **Additive / no-hide**: `verdict`, `exitCodeBasis`, every item's section, and the six-field `enforcement`
  detail are byte-identical to the F025-only projection of the same `ShipDecision` (SC-004, FR-008); a cache
  verdict never relaxes a blocker or alters the ship outcome (US2.4, US3).
- **Gate-scoped**: 100% of gate items carry a verdict; 0% of finding items do (SC-002, FR-004).
- **Match by GateId**: first-by-report-order on a duplicate `GateId` (FR-007); an orphan report entry adds
  nothing (FR-006).
- **Deterministic / pure / total**: identical inputs ⇒ byte-identical text; cache entries follow the existing
  composite item order; never throws; the evidence reference is verbatim, never dereferenced; no freshness
  key/hash/cache decision computed; no raw freshness input emitted (FR-007, FR-010, FR-011, SC-003, SC-007).
