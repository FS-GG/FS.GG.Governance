# Contract: `route.json` v2 — cache-eligibility embed (F045)

The wire delta to the F020 `route.json` document. The function is
`RouteJson.ofRouteResult: RouteResult -> CacheEligibilityReport option -> string`. This contract also fixes the
**shared per-gate verdict vocabulary** reused verbatim by [audit-json-document.md](./audit-json-document.md).

## Top-level shape (v2)

Field order (fixed): `schemaVersion`, `selectedGates`, `findings`, `cost`, **`cacheEligibilityEvaluated`**.

| Field | Delta | Value |
|---|---|---|
| `schemaVersion` | **changed** | `"fsgg.route/v2"` (was `"fsgg.route/v1"`) |
| `selectedGates` | each entry gains a trailing `cacheEligibility` field (below) | unchanged otherwise |
| `findings` | **unchanged** — findings carry no verdict (FR-004) | — |
| `cost` | **unchanged** | — |
| `cacheEligibilityEvaluated` | **new** (appended last) | `false` when `cache = None`; `true` when `cache = Some _` |

`cacheEligibilityEvaluated` is **always present** — it is the cache-eligibility *section* (FR-012). An empty
route (`selectedGates: []`) still carries it (the empty-route edge).

## Per-`selectedGates`-entry delta

Each entry keeps all F020 fields verbatim (`id`, `domain`, `description`, `cost`, `timeout`, `owner`,
`maturity`, `productCheck`, `prerequisites`, `freshnessKey`, `selectingPaths`) and gains a trailing
**`cacheEligibility`** verdict object (the shared vocabulary below), matched to the entry by `id` (`gateIdValue`,
verbatim).

## Shared per-gate `cacheEligibility` verdict vocabulary (also used by audit.json)

```jsonc
// reusable — prior evidence MAY be reused; carries only the opaque reference, never dereferenced (FR-011)
{ "kind": "reusable", "evidence": "<EvidenceReuse.referenceValue ref>" }

// must-recompute, no prior evidence — no "categories" field (distinct from inputsChanged [])
{ "kind": "mustRecompute", "cause": { "kind": "noPriorEvidence" } }

// must-recompute, inputs changed — names exactly the changed categories in the report's order (no-hide, FR-009)
{ "kind": "mustRecompute", "cause": { "kind": "inputsChanged", "categories": ["<FreshnessKey.categoryToken c>", "…"] } }

// not evaluated — gate listed in the document but absent from the report (FR-005), or cache = None (FR-012)
{ "kind": "notEvaluated" }
```

- `kind` is always first. `reusable` ⇒ only `kind` + `evidence`. `mustRecompute` ⇒ always a `cause`, never an
  `evidence` field. `notEvaluated` ⇒ only `kind` — **never** `reusable` without a matching report entry (FR-005,
  the no-hide rule).
- The vocabulary is F042's verbatim (`CacheEligibilityJson`), plus the `notEvaluated` case the embed adds.

## Example (route with two selected gates, `Some report`)

```json
{
  "schemaVersion": "fsgg.route/v2",
  "selectedGates": [
    { "id": "build:rel", "domain": "build", "...": "…", "selectingPaths": [],
      "cacheEligibility": { "kind": "reusable", "evidence": "sha256:abc…" } },
    { "id": "test:unit", "domain": "test", "...": "…", "selectingPaths": [],
      "cacheEligibility": { "kind": "mustRecompute", "cause": { "kind": "inputsChanged", "categories": ["rulePack","coveredArtifacts"] } } }
  ],
  "findings": [],
  "cost": { "cheap": 1, "medium": 1, "high": 0, "exhaustive": 0 },
  "cacheEligibilityEvaluated": true
}
```

## Example (`cache = None`, today's `fsgg route`)

```json
{
  "schemaVersion": "fsgg.route/v2",
  "selectedGates": [
    { "id": "build:rel", "...": "…", "cacheEligibility": { "kind": "notEvaluated" } }
  ],
  "findings": [],
  "cost": { "cheap": 0, "medium": 0, "high": 0, "exhaustive": 0 },
  "cacheEligibilityEvaluated": false
}
```

## Guarantees

- **Additive**: every non-cache byte equals the F020-only projection of the same `RouteResult` (SC-004, FR-008).
- **Match by GateId**: each entry's verdict is the report's verdict for that `id`, first-by-report-order on a
  duplicate `GateId` (FR-007); a report entry matching no listed gate adds nothing (FR-006).
- **Deterministic**: identical inputs ⇒ byte-identical text; cache entries follow the existing `GateId`-ordinal
  order (SC-003). **Pure/total**: never throws; the evidence reference is verbatim, never dereferenced; no
  freshness key/hash/cache decision computed; no raw freshness input emitted (FR-010, FR-011, SC-007).
