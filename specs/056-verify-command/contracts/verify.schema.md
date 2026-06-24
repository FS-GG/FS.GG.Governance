# Contract: `verify.json` deterministic projection

Produced by `FS.GG.Governance.VerifyJson`:

```fsharp
val schemaVersion: string   // "fsgg.verify/v1"
val ofVerifyDecision:
    decision: ShipDecision ->
    cache: CacheEligibilityReport option ->
    execution: (GateId * GateOutcome) list ->
        string
```

Pure, total, emit-only. Re-derives/re-sorts/re-classifies nothing: `decision` fixed the verdict, basis, and
partition order; `cache`/`execution` fixed the per-gate disposition order. No I/O, no clock, no git; never
throws; byte-identical for identical inputs (FR-007/FR-008, SC-004).

## Document grammar (fixed field order)

```json
{
  "schemaVersion": "fsgg.verify/v1",
  "verdict": "pass" | "blocked",
  "exitCodeBasis": "clean" | "blocked",
  "blockers": [ <item> ],
  "warnings": [ <item> ],
  "passing":  [ <item> ],
  "currency": {
    "fresh":      [ { "gate": <string>, "evidence": <string> } ],
    "recomputed": [ { "gate": <string>, "cause": <cause> } ],
    "unresolved": [ { "gate": <string>, "missing": [ <string> ] } ]
  }
}
```

`<item>` (an `EnforcedItem`, fields in this order):

```json
{
  "id": { "kind": "gate", "gate": <string> }
      |  { "kind": "finding", "finding": <string>, "path": <string> },
  "enforcement": {
    "baseSeverity": "advisory" | "blocking",
    "maturity": <string>,
    "mode": "verify",
    "profile": <string>,
    "effectiveSeverity": "advisory" | "blocking",
    "reason": <string>
  },
  "cache":     <cache verdict> | null,
  "execution": { "disposition": "executed" | "reused" | "not-executed",
                 "exitCode": <int> | null, "passed": <bool> | null } | null
}
```

`<cause>`:

```json
"noPriorEvidence"
  |  { "kind": "inputsChanged", "categories": [ <string> ] }
```

`<cache verdict>` (per item, mirroring `AuditJson`):

```json
{ "kind": "reusable", "evidence": <string> }
  |  { "kind": "mustRecompute", "cause": <cause> }
```

## Determinism rules

- Compact `Utf8JsonWriter` (default options — no indentation).
- Fixed field order exactly as written above; never sorted at emit time except where a core already fixed
  order. Arrays (`blockers`/`warnings`/`passing`, `currency.*`) emit in the cores' fixed order.
- Exhaustive closed-enum token helpers (no wildcard) — a new case is a compile error here, never a
  mis-tokened field. `mode` is always `"verify"`.
- Opaque evidence references rendered verbatim, never dereferenced.
- `noPriorEvidence` (no `categories`) is distinct from `inputsChanged` with `"categories": []`.
- **No** timestamp, absolute path, username, environment value, or raw config text anywhere.
- `--json` stdout equals this string byte-for-byte (one source of truth — FR-007).

## Currency derivation (no new sensing)

The `currency` object is computed from `cache` (F041 `CacheEligibilityReport`) and the freshness resolution
behind it (F043) — the same join the command's text render and `ShipCommand` already perform:

- `fresh` ⇐ cache verdict `Reusable ref` → `{ gate, evidence = referenceValue ref }`.
- `recomputed` ⇐ cache verdict `MustRecompute cause` → `{ gate, cause }` (the changed `categories` are the
  "generated view out of date relative to its declared sources" signal).
- `unresolved` ⇐ a selected gate with no resolved freshness facts → `{ gate, missing = missing-fact tokens }`.

Severity is **not** re-decided here: a blocking unmet check already appears under `blockers` via the reused
`Ship.rollup`; `currency` labels the freshness disposition only.
