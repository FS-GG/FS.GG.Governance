# Contract: the `cache-eligibility.json` document (F042)

The observable wire contract `CacheEligibilityJson.ofReport` produces. This fixes **field order**,
**tokens**, and **shape** — the part of the artifact consumers depend on (the spec's "observable document
contract"). It is the design's per-change cache-eligibility artifact (`docs/initial-design.md`,
`docs/initial-implementation-plan.md` Phase 2 / Phase 11 *"… and cache eligibility …"*), restricted to the
fields F041's `CacheEligibilityReport` has already typed — the host wiring that resolves each gate's
`FreshnessInputs`, the embedding into route.json / audit.json, and any real cache store are deferred (see
[plan](../plan.md) Summary and [research D1](./research.md)).

Output is **compact** (non-indented) UTF-8 from a default `Utf8JsonWriter`; the indented forms below are
for documentation only. Field order is exactly the writer's call order and is part of the contract
(FR-007). The `entries` collection is in the order the `CacheEligibilityReport` already fixed — the
`GateId`-ordinal order with F041's structural duplicate tiebreak — preserved verbatim, re-sorting nothing
(FR-005).

**Relationship to `route.json` (F020) / `gates.json` (F021) / `audit.json` (F025).** Those project the
per-change route, the whole-catalog gate registry, and the whole-change ship verdict. This projects the
per-change **cache-eligibility verdict** — for each selected gate, whether prior evidence may be reused (and
which) or the gate must recompute (and why). It is the sibling of `AuditJson` (it renders one already-typed,
already-ordered core value), applied to F041's `CacheEligibilityReport`.

## Top-level object — field order: `schemaVersion`, `entries`

| Field | JSON type | Source | Notes |
|---|---|---|---|
| `schemaVersion` | string | `CacheEligibilityJson.schemaVersion` | The declared contract version `"fsgg.cache-eligibility/v1"` (FR-013). Fixed constant — never from a clock/environment/input. |
| `entries` | array\<entry\> | `CacheEligibility.entries report` | In the report's `GateId`-ordinal order (with F041's structural duplicate tiebreak), preserved verbatim; **always present**, **empty array** when the report is empty (FR-005, FR-009). |

## entry — field order: `gate`, `verdict`

| Field | JSON type | Source (`CacheEligibilityEntry e`) | Token / rendering |
|---|---|---|---|
| `gate` | string | `e.Gate` | `Gates.gateIdValue` — declared `GateId` string verbatim (FR-010). Never re-parsed to recover a domain/check, even across a `:` separator. |
| `verdict` | object | `e.Verdict` | The tagged verdict object below. Rendered verbatim from the value (FR-002); **never** recomputed. |

## verdict — a **tagged** object keyed by `kind`

Every verdict is one of two shapes, discriminated by `kind`. The `match` is exhaustive over the closed
`CacheEligibilityVerdict` (no wildcard). Field order as listed.

### reusable verdict — field order: `kind`, `evidence`

| Field | JSON type | Source (`Reusable ref`) | Token / rendering |
|---|---|---|---|
| `kind` | string | (discriminator) | Literal `"reusable"`. |
| `evidence` | string | `ref` | `EvidenceReuse.referenceValue` — the opaque evidence reference verbatim (FR-003). Never parsed, dereferenced, validated, or re-derived. |

> A reusable verdict carries **no** skip action, reuse policy, severity, ship verdict, or exit-code basis
> (FR-003, necessary-not-sufficient) — only `kind` + `evidence`.

### mustRecompute verdict — field order: `kind`, `cause`

| Field | JSON type | Source (`MustRecompute cause`) | Token / rendering |
|---|---|---|---|
| `kind` | string | (discriminator) | Literal `"mustRecompute"`. |
| `cause` | object | `cause` | The tagged cause object below — **always present** (the no-hide rule, FR-004). |

> A mustRecompute verdict has **no** `evidence` field (absent, not `null`; the `kind` tag disambiguates).

## cause — a **tagged** object keyed by `kind` (the no-hide rule, FR-004)

Every `mustRecompute` verdict carries exactly one cause from this closed vocabulary — never an empty or
opaque cause. The `match` is exhaustive over the closed `RecomputeCause` (no wildcard).

### noPriorEvidence cause — field order: `kind`

| Field | JSON type | Source (`NoPriorEvidence`) | Token / rendering |
|---|---|---|---|
| `kind` | string | (discriminator) | Literal `"noPriorEvidence"`. **No** `categories` field. |

### inputsChanged cause — field order: `kind`, `categories`

| Field | JSON type | Source (`InputsChanged cats`) | Token / rendering |
|---|---|---|---|
| `kind` | string | (discriminator) | Literal `"inputsChanged"`. |
| `categories` | array\<string\> | `cats` | Each via `FreshnessKey.Model.categoryToken`, in the report's order (F041 carried F030's `diff` order). **Always present**; an empty `cats` renders as `[]` (FR-006) — distinct from `noPriorEvidence`. |

> `noPriorEvidence` (no `categories` field) and `inputsChanged` with `categories: []` are **distinct** and
> never collapse to one another (FR-006, SC-005).

## Excluded from every document (FR-012, SC-007)

No wall-clock timestamp, host/absolute path, raw freshness input, computed freshness key or hash,
environment-derived value, numeric process `exitCode`, severity, ship verdict, exit-code basis, or
provenance/attestation reference. Only the declared schema version, the closed
`verdict.kind`/`cause.kind`/`categories` vocabularies, the declared `gate` id string, and the opaque
`evidence` reference appear.

## Token vocabularies (closed — FR-011)

| Position | Tokens |
|---|---|
| `verdict.kind` | `reusable` \| `mustRecompute` |
| `cause.kind` | `noPriorEvidence` \| `inputsChanged` |
| `categories[]` | `check` \| `domain` \| `command` \| `environmentClass` \| `ruleHash` \| `coveredArtifacts` \| `commandVersion` \| `generatorVersion` \| `baseRevision` \| `headRevision` (F029 `categoryToken`, reused verbatim) |

## Worked sample (indented for readability; the emitted form is compact)

A report from the real F041 `CacheEligibility.evaluate` over three candidate gates against a real F030
`ReuseStore` — one gate with prior matching evidence (`reusable`), one with no prior evidence
(`mustRecompute` / `noPriorEvidence`), and one whose `ruleHash` and `headRevision` moved (`mustRecompute` /
`inputsChanged`) — entries in the report's `GateId`-ordinal order:

```json
{
  "schemaVersion": "fsgg.cache-eligibility/v1",
  "entries": [
    {
      "gate": "build:tests",
      "verdict": {
        "kind": "mustRecompute",
        "cause": { "kind": "inputsChanged", "categories": ["ruleHash", "headRevision"] }
      }
    },
    {
      "gate": "docs:lint",
      "verdict": { "kind": "reusable", "evidence": "ev-A" }
    },
    {
      "gate": "security:scan",
      "verdict": {
        "kind": "mustRecompute",
        "cause": { "kind": "noPriorEvidence" }
      }
    }
  ]
}
```

The empty report projects to:

```json
{ "schemaVersion": "fsgg.cache-eligibility/v1", "entries": [] }
```

A duplicate-`GateId` report (two candidates under `build:tests` with different inputs, both unmatched
against an empty store ⇒ both `MustRecompute NoPriorEvidence`) renders **two** distinct entries under
`build:tests`, neither merged nor deduplicated, in the report's order:

```json
{
  "schemaVersion": "fsgg.cache-eligibility/v1",
  "entries": [
    { "gate": "build:tests", "verdict": { "kind": "mustRecompute", "cause": { "kind": "noPriorEvidence" } } },
    { "gate": "build:tests", "verdict": { "kind": "mustRecompute", "cause": { "kind": "noPriorEvidence" } } }
  ]
}
```

> Sample field *values* (gate ids, evidence reference, category tokens) illustrate the shape; the
> authoritative values are whatever the real F041 `evaluate` carries at test time. The contract this file
> fixes is the **field set, order, and tokens**, not the sample's literal strings.
