# Phase 1 Data Model — Deterministic cache-eligibility.json Projection (F042)

This row introduces **no new in-memory vocabulary**. Its "data model" is the **wire shape** of the
`cache-eligibility.json` document and how each already-typed F041 value renders into it. The library is a
pure projection: a `schemaVersion` constant and `ofReport: CacheEligibilityReport -> string`. The full
field order / token / shape contract is [contracts/cache-eligibility-json-document.md](./contracts/cache-eligibility-json-document.md);
the signatures + laws are [contracts/cache-eligibility-json-api.md](./contracts/cache-eligibility-json-api.md).

## Consumed verbatim (the input — not redefined)

The projection's input is the F041 `CacheEligibilityReport` and the types it carries; every accessor used to
render a token is an existing public upstream function. Nothing here is re-modeled.

| Type / accessor | Origin | Use here |
|---|---|---|
| `CacheEligibilityReport = CacheEligibilityReport of CacheEligibilityEntry list` | F041 `CacheEligibility.Model` | The projection input; unwrapped via `CacheEligibility.entries` to its already-ordered entry list. |
| `CacheEligibilityEntry = { Gate: GateId; Verdict: CacheEligibilityVerdict }` | F041 `CacheEligibility.Model` | One rendered document entry — its `gate` + `verdict`. |
| `CacheEligibilityVerdict = Reusable of EvidenceRef \| MustRecompute of RecomputeCause` | F041 `CacheEligibility.Model` | The closed two-outcome verdict rendered as a tagged object (research D4). Matched exhaustively, no wildcard. |
| `RecomputeCause = NoPriorEvidence \| InputsChanged of InputCategory list` | F030 `EvidenceReuse.Model` | The no-hide cause rendered as a tagged object (research D4). Matched exhaustively, no wildcard. |
| `EvidenceRef = EvidenceRef of string` | F030 `EvidenceReuse.Model` | The opaque reusable-evidence reference; rendered verbatim via `referenceValue`, never parsed/dereferenced. |
| `InputCategory` (10 closed cases) | F029 `FreshnessKey.Model` | The changed-input categories; each rendered via `categoryToken`. |
| `GateId = GateId of string` | F018 `Gates.Model` | The entry's gate identity; rendered verbatim via `gateIdValue`, never re-parsed (even across a `:` separator). |
| `Gates.gateIdValue: GateId -> string` | F018 `Gates.Model` | Render the `gate` field. |
| `EvidenceReuse.referenceValue: EvidenceRef -> string` | F030 `EvidenceReuse` | Render the `evidence` field. |
| `FreshnessKey.Model.categoryToken: InputCategory -> string` | F029 `FreshnessKey.Model` | Render each `categories` element. |
| `CacheEligibility.entries: CacheEligibilityReport -> CacheEligibilityEntry list` | F041 `CacheEligibility` | Unwrap the report to its ordered entries. |

## The document (the wire shape this row fixes)

### Top-level object — field order `schemaVersion`, `entries`

```json
{
  "schemaVersion": "fsgg.cache-eligibility/v1",
  "entries": [ <entry> … ]
}
```

- **`schemaVersion`** (string) — `CacheEligibilityJson.schemaVersion`, the fixed constant
  `"fsgg.cache-eligibility/v1"` (FR-013). Never derived from a clock, environment, or input value.
- **`entries`** (array) — `CacheEligibility.entries report`, in the report's existing `GateId`-ordinal
  order (with its structural duplicate tiebreak) preserved verbatim (FR-005). **Always present**; an empty
  report renders as `"entries": []` (FR-009) — never omitted, never a placeholder entry.

### `entry` — field order `gate`, `verdict`

```json
{ "gate": "<gateIdValue Gate>", "verdict": <verdict-object> }
```

- **`gate`** (string) — `gateIdValue entry.Gate`, the declared `GateId` string verbatim (FR-010). Never
  re-parsed to recover a domain or check, even if it contains a `:` separator.
- **`verdict`** (object) — the entry's `Verdict` rendered verbatim (FR-002), the tagged object below.

### `verdict` — a `kind`-tagged object (research D4)

| `CacheEligibilityVerdict` | Rendered |
|---|---|
| `Reusable ref` | `{ "kind": "reusable", "evidence": "<referenceValue ref>" }` |
| `MustRecompute cause` | `{ "kind": "mustRecompute", "cause": <cause-object> }` |

A `reusable` verdict carries **only** `kind` + `evidence` — no skip action, severity, ship verdict, or
exit-code basis (FR-003, necessary-not-sufficient). The `evidence` is the opaque reference string verbatim,
never parsed or dereferenced (FR-003/FR-010). A `mustRecompute` verdict carries **only** `kind` + `cause` —
no `evidence` field.

### `cause` — a `kind`-tagged object (no-hide, FR-004; research D4)

| `RecomputeCause` | Rendered |
|---|---|
| `NoPriorEvidence` | `{ "kind": "noPriorEvidence" }` |
| `InputsChanged cats` | `{ "kind": "inputsChanged", "categories": [ "<categoryToken c>" … ] }` |

Every `mustRecompute` entry carries exactly one cause from this closed vocabulary (FR-004) — never an empty
or opaque cause. `noPriorEvidence` (no `categories` field) is **distinct** from `inputsChanged` with an
empty `categories: []` array (FR-006) — they never collapse to one another. The `categories` array names
exactly the report's changed `InputCategory` list, **in the report's order** (F041 carried F030's `diff`
order verbatim) — none dropped, none added, never truncated (FR-004).

## Closed token vocabularies (FR-011 — branchable, not free text)

| Position | Tokens |
|---|---|
| `verdict.kind` | `reusable`, `mustRecompute` |
| `cause.kind` | `noPriorEvidence`, `inputsChanged` |
| `categories[]` | the `categoryToken` vocabulary: `check`, `domain`, `command`, `environmentClass`, `ruleHash`, `coveredArtifacts`, `commandVersion`, `generatorVersion`, `baseRevision`, `headRevision` (the exact strings are F029 `categoryToken`'s, reused verbatim — never re-spelled here) |

Each `match` rendering a token is exhaustive over the closed DU with **no wildcard**, so a future verdict /
cause / category case is a compile error in this library, never a silently mis-tokened field (research D4).

## Excluded from every document (FR-012, SC-007)

No wall-clock timestamp, host/absolute path, raw freshness input, computed freshness key or hash,
environment-derived value, numeric process exit code, severity, ship verdict, exit-code basis, or
provenance/attestation reference. Only the declared schema version, the declared `gate` id strings, the
closed `verdict`/`cause`/`categories` vocabularies, and the opaque `evidence` reference appear. (The F041
report carries none of the excluded fields, so the projection has nothing to leak — research D7.)

## State & relationships

No state, no transitions, no identity derivation — this is a *projection* of one typed value to a string.
The relationship is `CacheEligibilityReport → string` via `ofReport`, structurally:

```text
ofReport report =
  writeToString (fun w ->
    object:
      "schemaVersion" = schemaVersion
      "entries" = array [ for entry in CacheEligibility.entries report -> writeEntry w entry ])
```

where `writeEntry` writes `gate` + the tagged `verdict` (and, for `mustRecompute`, the tagged `cause` with
its `categories`). The walk is linear and total over any well-typed report (research D8).
