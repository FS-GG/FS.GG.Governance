# Contract: the `audit.json` document (F025)

The observable wire contract `AuditJson.ofShipDecision` produces. This fixes **field order**, **tokens**,
and **shape** — the part of the artifact consumers depend on (the spec's "observable document
contract"). It is the design's `readiness/<id>/audit.json` artifact (`docs/initial-design.md`,
`docs/initial-implementation-plan.md:197`), restricted to the fields F024's `ShipDecision` has already
typed — provenance/attestation references, the numeric process exit code, and any cache-eligibility
verdict are deferred (see [plan](../plan.md) Summary and [research D4/D5](./research.md)).

Output is **compact** (non-indented) UTF-8 from a default `Utf8JsonWriter`; the indented form below is
for documentation only. Field order is exactly the writer's call order and is part of the contract
(FR-007). Collections are in the order the `ShipDecision` already fixed — each section by the F024
composite key (gates before findings, gates by `GateId`, findings by `(path, finding-id token)`),
preserved verbatim (FR-007).

**Relationship to `route.json` (F020) and `gates.json` (F021).** Those project the per-change route and
the whole-catalog gate registry; this projects the **whole-change ship verdict**. Where they carry gate
*metadata*, this carries each enforced item's *verdict outcome* — its identity plus the six-field F023
enforcement detail — under the three-way blockers/warnings/passing partition.

## Top-level object — field order: `schemaVersion`, `verdict`, `exitCodeBasis`, `blockers`, `warnings`, `passing`

| Field | JSON type | Source | Notes |
|---|---|---|---|
| `schemaVersion` | string | `AuditJson.schemaVersion` | The declared contract version `"fsgg.audit/v1"` (FR-013). Fixed constant. |
| `verdict` | string | `decision.Verdict` | `pass` \| `fail`. Rendered verbatim from the value; **never** recomputed from the item sections (FR-002). |
| `exitCodeBasis` | string | `decision.ExitCodeBasis` | `clean` \| `blocked`. Rendered verbatim; **no** numeric process exit code derived (FR-003); **no** basis invented. |
| `blockers` | array\<item\> | `decision.Blockers` | In F024 composite order; **always present**, **empty array** when none (FR-005, FR-009). |
| `warnings` | array\<item\> | `decision.Warnings` | In F024 composite order; **always present**, empty array when none (FR-005, FR-009). |
| `passing` | array\<item\> | `decision.Passing` | In F024 composite order; **always present**, empty array when none (FR-005, FR-009). |

## item entry — a **tagged** object keyed by `kind`

Every item in every section is one of two shapes, discriminated by `kind`. No item appears in more than
one section (FR-005). Field order as listed.

### gate item — field order: `kind`, `id`, `enforcement`

| Field | JSON type | Source (`EnforcedItem item` with `Id = GateItem g`) | Token / rendering |
|---|---|---|---|
| `kind` | string | (discriminator) | Literal `"gate"`. |
| `id` | string | `g` | `Gates.gateIdValue` — declared `GateId` string verbatim (FR-004, FR-010). Never re-parsed to recover a domain, even if it contains a `:` separator. |
| `enforcement` | object | `item.Decision` | The six F023 fields. Shape below. |

### finding item — field order: `kind`, `id`, `path`, `enforcement`

| Field | JSON type | Source (`EnforcedItem item` with `Id = FindingItem (fid, GovernedPath path)`) | Token / rendering |
|---|---|---|---|
| `kind` | string | (discriminator) | Literal `"finding"`. |
| `id` | string | `fid` | `Findings.findingIdToken` — `unknownGovernedPath` \| `unknownProtectedBoundaryPath`. Declared token verbatim (FR-004, FR-010). |
| `path` | string | `path` | `GovernedPath` unwrapped verbatim (FR-010) — never re-normalized. The same `id` on different paths yields distinct entries (FR-004). |
| `enforcement` | object | `item.Decision` | The six F023 fields. Shape below. |

> A gate item has **no** `path` field (a gate has no governed path); the `kind` tag disambiguates — the
> field is absent, not `null` ([research D5](./research.md)).

### `enforcement` — field order: `baseSeverity`, `maturity`, `mode`, `profile`, `effectiveSeverity`, `reason`

The F023 `EnforcementDecision` carried verbatim — all six fields, none dropped, none re-derived
(FR-006). On a relaxed base-`Blocking` item rendered in `warnings`, `baseSeverity` and
`effectiveSeverity` differ and are **both** present, so a profile can never hide the underlying verdict
(FR-011, US3).

| Field | JSON type | Source (`EnforcementDecision d`) | Token / rendering |
|---|---|---|---|
| `baseSeverity` | string | `d.BaseSeverity` | `advisory` \| `blocking`. The input base severity, echoed unchanged. |
| `maturity` | string | `d.Maturity` | `observe` \| `warn` \| `blockOnPr` \| `blockOnShip` \| `blockOnRelease`. |
| `mode` | string | `d.Mode` | `sandbox` \| `inner` \| `focused` \| `verify` \| `gate` \| `release`. |
| `profile` | string | `d.Profile` | `light` \| `standard` \| `strict` \| `release`. |
| `effectiveSeverity` | string | `d.EffectiveSeverity` | `advisory` \| `blocking`. The derived effective severity. |
| `reason` | string | `d.Reason` | The carried explainable reason, verbatim. Free text — JSON-escaped by the writer (FR-012); never re-derived (FR-006). |

## Excluded from every document (FR-011, FR-012, SC-007)

No numeric process `exitCode`, provenance/attestation reference, artifact digest, cache-eligibility or
freshness verdict, per-change route trace, gate registry metadata (`cost`, `timeout`, `owner`,
`prerequisites`, `freshnessKey`), raw YAML, host/absolute path, wall-clock timestamp, or
environment-derived value. Only the declared schema version, the closed `verdict`/`exitCodeBasis`/
severity/maturity/mode/profile vocabularies, the declared id strings, the governed path, and the
carried free-text `reason` appear.

## Worked sample (indented for readability; the emitted form is compact)

A failing decision — one blocking gate, one warning that is a base-`Blocking` finding relaxed to
`Advisory` (the no-hide case), and one passing gate — rolled up by the real F024 `Ship.rollup`:

```json
{
  "schemaVersion": "fsgg.audit/v1",
  "verdict": "fail",
  "exitCodeBasis": "blocked",
  "blockers": [
    {
      "kind": "gate",
      "id": "build:tests",
      "enforcement": {
        "baseSeverity": "blocking",
        "maturity": "blockOnShip",
        "mode": "gate",
        "profile": "standard",
        "effectiveSeverity": "blocking",
        "reason": "base blocking at maturity blockOnShip under profile standard in mode gate"
      }
    }
  ],
  "warnings": [
    {
      "kind": "finding",
      "id": "unknownGovernedPath",
      "path": "src/new/Thing.fs",
      "enforcement": {
        "baseSeverity": "blocking",
        "maturity": "warn",
        "mode": "gate",
        "profile": "light",
        "effectiveSeverity": "advisory",
        "reason": "base blocking relaxed to advisory by maturity warn under profile light"
      }
    }
  ],
  "passing": [
    {
      "kind": "gate",
      "id": "docs:lint",
      "enforcement": {
        "baseSeverity": "advisory",
        "maturity": "observe",
        "mode": "gate",
        "profile": "standard",
        "effectiveSeverity": "advisory",
        "reason": "base advisory withheld at maturity observe"
      }
    }
  ]
}
```

The empty/clean decision projects to:

```json
{ "schemaVersion": "fsgg.audit/v1", "verdict": "pass", "exitCodeBasis": "clean", "blockers": [], "warnings": [], "passing": [] }
```

> Sample field *values* (gate/finding ids, paths, reason text) illustrate the shape; the authoritative
> values are whatever the real F024 `rollup` carries at test time. The contract this file fixes is the
> **field set, order, and tokens**, not the sample's literal strings.
</content>
