# Data Model: Persist, Bound, And Prune The Evidence-Reuse Store

This row introduces **no new types**. It operates entirely over the existing F029/F030 values and emits the
existing `fsgg.evidence-reuse-store/v1` document the F046 reader already accepts. What follows is the carried
vocabulary, the exact document shape, and the semantics of the three operations.

## Carried entities (reused verbatim — FR-010)

| Entity | Source | Role here |
|--------|--------|-----------|
| `ReuseStore = ReuseStore of RecordedEvidence list` | F030 `EvidenceReuse.Model` | The value serialised / retained / pruned. Newest-first by `record` convention. |
| `RecordedEvidence = { Inputs: FreshnessInputs; Evidence: EvidenceRef }` | F030 `EvidenceReuse.Model` | One store entry. Carried verbatim; never re-derived. |
| `EvidenceRef = EvidenceRef of string` | F030 `EvidenceReuse.Model` | Opaque edge token. Rendered verbatim, never parsed/dereferenced (FR-004). |
| `FreshnessInputs` | F029 `FreshnessKey.Model` | The freshness world: `Check`, `Domain`, `Command?`, `Environment`, `RuleHash`, `CoveredArtifacts`, `CommandVersion?`, `GeneratorVersion`, `Base`, `Head`. |
| `CheckId`/`DomainId`/`CommandId`/`EnvironmentClass` | F014 `Config.Model` | Carried identity newtypes / closed enum. Unwrapped by pattern match for rendering. |
| `RuleHash`/`ArtifactHash`/`CommandVersion`/`GeneratorVersion`/`Revision` | F029 `FreshnessKey.Model` | Opaque single-case string newtypes. Unwrapped for rendering; never re-hashed. |
| `matches : FreshnessInputs -> FreshnessInputs -> bool` | F029 `FreshnessKey` | The full-match (same-world) relation `prune` reuses verbatim. |
| `decide` / `record` / `entries` | F030 `EvidenceReuse` | `decide` defines the verdicts the safety invariant is checked against; `record` is the dedup `prune` generalises; `entries` unwraps the list. |

## The serialised document — `fsgg.evidence-reuse-store/v1`

The exact shape the F046 `FreshnessSensing` deserializer (`parseStore`/`parseEntry`) accepts. `serialise`
emits **only** this shape. Compact (non-indented) UTF-8.

```jsonc
{
  "schemaVersion": "fsgg.evidence-reuse-store/v1",   // fixed literal; never derived (FR-005)
  "recorded": [                                       // entries verbatim, newest-first (D7); [] for the empty store (FR-005)
    {
      "check": "<CheckId string>",                    // required (reader: reqStr)
      "domain": "<DomainId string>",                  // required
      "command": "<CommandId string>",                // OMITTED when Command = None (D4); else string
      "environment": "local|ci|local-or-ci|release",  // required; exact inverse of parseEnv (D6)
      "ruleHash": "<RuleHash string>",                // required
      "coveredArtifacts": ["<ArtifactHash>", ...],    // required array, verbatim list order, [] allowed (D5)
      "commandVersion": "<CommandVersion string>",    // OMITTED when CommandVersion = None (D4); else string
      "generatorVersion": "<GeneratorVersion string>",// required
      "base": "<Revision string>",                    // required
      "head": "<Revision string>",                    // required
      "evidence": "<EvidenceRef string>"              // required; opaque, verbatim, JSON-escaped (FR-004)
    }
    // ...
  ]
}
```

**Field-by-field mapping to the reader** (`FreshnessSensing.parseEntry`):

| JSON field | Reader call | Built into | Optional? |
|------------|-------------|------------|-----------|
| `check` | `reqStr "check"` → `CheckId` | `Inputs.Check` | required |
| `domain` | `reqStr "domain"` → `DomainId` | `Inputs.Domain` | required |
| `command` | `optStr "command"` → `CommandId option` | `Inputs.Command` | optional (omit on `None`) |
| `environment` | `parseEnv (reqStr "environment")` | `Inputs.Environment` | required |
| `ruleHash` | `reqStr "ruleHash"` → `RuleHash` | `Inputs.RuleHash` | required |
| `coveredArtifacts` | `strArr "coveredArtifacts"` → `ArtifactHash list` | `Inputs.CoveredArtifacts` | required array |
| `commandVersion` | `optStr "commandVersion"` → `CommandVersion option` | `Inputs.CommandVersion` | optional (omit on `None`) |
| `generatorVersion` | `reqStr "generatorVersion"` → `GeneratorVersion` | `Inputs.GeneratorVersion` | required |
| `base` | `reqStr "base"` → `Revision` | `Inputs.Base` | required |
| `head` | `reqStr "head"` → `Revision` | `Inputs.Head` | required |
| `evidence` | `reqStr "evidence"` → `EvidenceRef` | entry `Evidence` | required |

The top level requires `schemaVersion` (= the exact token, else the reader fails `unknown store schema`) and a
`recorded` array (else `missing recorded array`). The empty store ⇒ `"recorded": []` (FR-005), a well-formed
document distinct on disk from an absent file but loading to the same empty store.

## Operations (all pure, total, no I/O — FR-009)

### `serialise : ReuseStore -> string`

A single linear `Utf8JsonWriter` walk (D2). Writes `schemaVersion`, then `recorded` as an array, emitting each
entry in store order (verbatim, newest-first — D7) with the fixed field order (D7), optional fields omitted on
`None` (D4), `coveredArtifacts` verbatim (D5), environment via the exhaustive token map (D6), and every newtype
string + the evidence ref rendered verbatim (FR-004). Total: defined for every store including empty; never
throws (the empty store yields a present, empty `recorded` array — FR-005). Deterministic/byte-stable: identical
store value ⇒ identical bytes (FR-003).

**Guarantees**: FR-001 (inverse of the reader), FR-002 (`serialise` → `realStoreReader` lossless), FR-003
(byte-stable), FR-004 (opaque), FR-005 (empty store well-formed).

### `retain : maxEntries:int -> ReuseStore -> ReuseStore`

Keep the newest `maxEntries` entries: `List.truncate (max 0 maxEntries)` over the newest-first entry list,
re-wrapped as a `ReuseStore` (D8). Plus `defaultRetentionBound : int = 256`.

- **Bounded**: result length ≤ `max 0 maxEntries`.
- **Newest-retained**: keeps the head of the newest-first list (the entries a future run is most likely to
  match), in order.
- **Idempotent at/under bound**: a store of length ≤ bound is returned unchanged (no reorder, no rewrite).
- **Removal-only**: every retained entry is byte-for-byte one of the inputs; nothing is mutated or fabricated.
- **Total**: `maxEntries ≤ 0` ⇒ empty store; defined for every input.

**Guarantees**: FR-006, FR-008 (eviction can only turn a future `Reuse` into `Recompute`), FR-009.

### `prune : ReuseStore -> ReuseStore`

Remove every entry that a strictly-newer entry already `FreshnessKey.matches` (same world), keeping the newest
entry per world-class in newest-first order (D9). One fold over the newest-first list: keep an entry iff no
already-kept (newer) entry `matches` it.

- **Subset, ordered**: survivors are a subset of the input in newest-first order.
- **No-op on clean stores**: a store with no superseded entry (e.g. any `record`-built store) is unchanged.
- **Verdict-preserving**: for any candidate, `decide` against the pruned store equals `decide` against the
  unpruned store (the newest full-match and the cause-locating entry are both retained) — hence
  identical-or-stricter (never more permissive).
- **Total**: defined for every store, including empty and all-distinct.

**Guarantees**: FR-007, FR-008 (no spurious reuse — in fact verdict-identical), FR-009, FR-010 (reuses F029
`matches`).

## The cross-cutting recompute-safety invariant (FR-008, SC-006)

For every operation `op ∈ { serialise∘read, retain n, prune }`, candidate `c`, and store `s`:

> it is **never** the case that `EvidenceReuse.decide c s = Recompute _` and
> `EvidenceReuse.decide c (op s) = Reuse _`.

`serialise` round-trips to an equal store (verdicts identical). `retain`/`prune` only remove entries, which can
lose a potential `Reuse` but never create one. This single property is checked over FsCheck-generated
candidates and stores in `SafetyTests.fs`.
