# Contract: `governance-handoff.json` (the document Governance reads)

**Direction**: SDD-owned, read-only to Governance. Governance imports no SDD code and changes
no field of this shape (FR-013, SC-006). This file documents the shape the consumer reads
*against Governance's own target types* — it is **not** a new contract.

**Provenance**: ADR 0002 (`docs/decisions/0002-sdd-governance-handoff-contract.md`) + the
tutorial (`docs/tutorials/sdd-governance-handoff.md`). Authoritative JSON key spellings are the
sibling `FS.GG.SDD` repo's `017-governance-handoff` `contracts/integration-requirements.md`
(cross-repo; cross-checked at implementation — research D8).

**Location**: `readiness/<id>/governance-handoff.json` within a governed product (optional;
zero or many).

## Shape (illustrative — keys per ADR 0002 / tutorial)

```jsonc
{
  "contractVersion": "1.0.0",        // consumer pins MAJOR 1; unknown major ⇒ version-mismatch (FR-002)
  "schemaVersion": 1,
  "evidence": {
    "nodes": [
      { "id": "test:unit",   "state": "failed", "rationale": "…" },
      { "id": "build:lib",   "state": "real" },
      { "id": "doc:api",     "state": "deferred", "rationale": "tracked in #123" }, // → skipped (FR-004)
      { "id": "perf:bench",  "state": "real", "stale": true }                       // state + staleEvidence (FR-006)
      // a node declaring "state": "autoSynthetic" ⇒ REJECTED (FR-005)
    ],
    "dependencies": [ ["test:unit", "build:lib"] ]   // "a rests on b" → Evidence.build edges
  },
  "readiness": {
    "shipDisposition": "blocked",                // non-shippable ⇒ blocking readiness gate (FR-009)
    "verificationReadiness": "incomplete",
    "blockingDiagnosticIds": ["VIEW_STALE"],     // non-empty ⇒ blocking
    "counts": { "blocking": 1, "advisory": 0 },
    "perViewState": { "ledger": "stale" }
  },
  "governedReferences": [                          // OPTIONAL routing enrichment only (FR-010)
    { "workItem": "WI-1", "paths": ["src/Ledger.fs"] }
  ]
}
```

## Consumption rules (each is a tested ADR-0002 row — SC-002)

| Input | Outcome | Req |
|---|---|---|
| `evidence.nodes[].state` ∈ `{pending,real,synthetic,failed,skipped}` | straight-through `EvidenceState` | FR-003 |
| `deferred` / `accepted-deferral` | `skipped` (not `pending`) | FR-004 |
| `autoSynthetic` declared | reject + diagnostic; no mapped result | FR-005 |
| `stale` node | underlying state **+** `staleEvidence` diagnostic | FR-006 |
| mapped evidence | `Evidence.build` + `Evidence.effective` taint closure | FR-007 |
| `readiness.*` | typed gate-registry entry (selection/severity/roll-up) | FR-009 |
| `governedReferences[*]` | optional enrichment; correctness independent of it | FR-010 |
| unknown `contractVersion` major | version-mismatch diagnostic; no enforce | FR-002 |
| malformed / missing required field | descriptive diagnostic; no crash, no partial enforce | FR-011 |
| absent (no file) | silent no-op; behaviour unchanged | FR-001 |

## Versioning posture (ADR 0002)

Pin **major `1.x`**; ignore unknown additive (minor) fields. A meaning-changing shape change is
a **major** bump + `schemaVersion` change + migration note in both repos; on major mismatch the
consumer reports a version-mismatch diagnostic, never a silent misread.
