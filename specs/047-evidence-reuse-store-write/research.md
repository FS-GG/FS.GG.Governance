# Research: Persist, Bound, And Prune The Evidence-Reuse Store

Phase 0 decisions. Each resolves a mechanism the spec deliberately left to the plan (Assumptions:
"plan/clarify mechanism details, not requirement gaps"). No NEEDS CLARIFICATION remained after the spec's
Assumptions — these record the concrete choices and why.

## D1 — Library placement: one new sibling, not an edit to the F030 core

**Decision**: Add a new library `FS.GG.Governance.EvidenceReuseStore` holding all three operations
(`serialise`, `retain`, `prune`). Do **not** add them to the F030 `EvidenceReuse` core.

**Rationale**: FR-012 / SC-007 require the change be additive — no edit to any merged F029–F046 core or its
baseline. Adding functions to `EvidenceReuse` would re-bless `surface/FS.GG.Governance.EvidenceReuse.surface.txt`
(a merged-core baseline edit). A sibling keeps F030 frozen. It also matches the constitution's "heavier
capabilities layer on top, not into the core" rule and the repo's established precedent: F042
`CacheEligibilityJson` layered serialisation in a **separate** project beside F041's pure `CacheEligibility`
core rather than tainting the core with `System.Text.Json`. The F030 core's own doc-comment states it
"computes NO persistence, NO eviction/expiry" — persistence/eviction/expiry belong outside it by its own
design.

**Why all three in one library** (not a `*Json` for serialise + core edits for retain/prune): `retain`/`prune`
are pure store transformations that *could* sit in core, but putting them in the sibling keeps the merged F030
surface untouched and groups the cohesive "store write half" together. The library references only the F030
transitive graph (`EvidenceReuse` → `FreshnessKey` → `Config`) plus shared-framework `System.Text.Json`.

**Alternatives rejected**: (a) Edit F030 core — re-blesses a merged baseline, violates additive intent.
(b) Two libraries (`EvidenceReuseStoreJson` + retain/prune in core) — splits one concern and still edits core.
(c) A `*Json`-only library plus deferring retain/prune — the spec scopes all three to this row (US1–US3).

## D2 — Serialisation mechanism: hand-driven compact `Utf8JsonWriter`

**Decision**: `serialise` builds the document with a hand-driven `System.Text.Json.Utf8JsonWriter` over a
`MemoryStream`, default options (non-indented ⇒ compact), UTF-8 → string — the exact `writeToString` shape of
F042 `CacheEligibilityJson` / F025 `AuditJson` / the kernel `Json.fs`.

**Rationale**: Reuses the only serialisation mechanism already in the repo ⇒ no new dependency (FR-011).
Default `Utf8JsonWriter` options give deterministic, compact, byte-stable output with no indentation/locale
surface (FR-003). `Utf8JsonWriter` performs correct JSON string escaping, covering the "evidence string
containing characters needing JSON escaping" edge case (FR-004 — the string is rendered verbatim *as a JSON
string value*, never parsed). The reader is `System.Text.Json` (`JsonDocument`), so writer/reader agree.

**Alternatives rejected**: string concatenation / a JSON DOM (`JsonNode`) — more code, easier to drift from
the reader's accepted shape, no determinism benefit.

## D3 — Round-trip verification drives the REAL reader

**Decision**: Verify the round-trip against the genuine `FreshnessSensing.realStoreReader` (a `path ->
Result<ReuseStore option,string>`), not a re-implemented parser. `parseStore` (string → store) is hidden
(absent from `FreshnessSensing.fsi`); the only public load path is `realStoreReader`, which reads a **file**.
So tests write `serialise store` to a temp file, call `realStoreReader path`, expect `Ok (Some loaded)`, and
assert `loaded = store`.

**Rationale**: Assumptions require the round-trip be "verified against that real reader, not a
re-implementation". The temp-file hop is the minimal way to reach the public reader; it is test-only I/O (the
library itself stays pure). The test project references both `EvidenceReuseStore` and `FreshnessSensing`.

**Alternatives rejected**: exposing `parseStore` in the `FreshnessSensing.fsi` — edits a merged-core surface
(FR-012). Re-implementing the parser in tests — forbidden by Assumptions; would not catch reader drift.

## D4 — Optional fields (`command`, `commandVersion`): omit when `None`

**Decision**: When `FreshnessInputs.Command` / `.CommandVersion` is `None`, **omit** the `command` /
`commandVersion` property entirely. When `Some`, emit it as a JSON string.

**Rationale**: The reader's `optStr` maps **both** an absent key and a `null` value to `None`, so either
round-trips. Picking one rendering (omit) and using it unconditionally keeps output deterministic/byte-stable
(FR-003) and the document minimal. Omission is unambiguous here because these are the *only* optional fields;
every other field is required by the reader (`reqStr` / `strArr`) and always present. (Note: this is an
option-presence distinction, **not** the sensed-empty-vs-unsensed list distinction — see D5.)

**Alternatives rejected**: emit `null` — equally correct but larger and no clearer; the choice is arbitrary so
the simpler one wins. Mixing (omit sometimes, null other times) — would break byte-stability.

## D5 — `coveredArtifacts`: always present, emitted verbatim, never sorted

**Decision**: Always emit `coveredArtifacts` as a JSON array (the reader's `strArr` requires the field present
and of kind array). Emit the `ArtifactHash` strings in the entry's **stored list order, verbatim** — do not
sort, de-dup, or reorder. An empty covered set emits `[]`.

**Rationale**: Round-trip identity (FR-002, SC-001) is exact structural equality on `ReuseStore`, and
`FreshnessInputs.CoveredArtifacts` is a **list** whose F# equality is order-sensitive. The reader reads the
array into a list in array order. So to guarantee `loaded = input`, `serialise` must preserve the exact list
order — sorting would break round-trip identity for any store whose covered list is not already sorted. (F029
`matches` compares covered artifacts as a *set*, but that governs reuse decisions, not the value identity the
round-trip must preserve.) The present-but-empty `[]` is the sensed-empty case and is distinct from any
"unsensed" rendering — it round-trips as an empty list, preserving the F029/F043 distinction (Edge Cases).

**Alternatives rejected**: canonical-sort covered artifacts for "stability" — unnecessary (determinism is
"same value → same bytes", already satisfied by verbatim emission) and **incorrect** (breaks round-trip
identity).

## D6 — Environment token: exhaustive inverse of `parseEnv`, no wildcard

**Decision**: Render `EnvironmentClass` with the exact inverse of the reader's `parseEnv`: `Local`→`"local"`,
`Ci`→`"ci"`, `LocalOrCi`→`"local-or-ci"`, `Release`→`"release"`. Match exhaustively with **no** wildcard.

**Rationale**: The serialised token must be one `parseEnv` accepts, or the round-trip fails. An exhaustive
match with no `_` makes a future `EnvironmentClass` case a **compile error** here (the F042 no-wildcard
discipline), never a silently mis-tokened field.

## D7 — Field order: a single fixed, documented order

**Decision**: Top-level object order: `schemaVersion`, then `recorded`. Each entry's field order:
`check`, `domain`, `command`(if present), `environment`, `ruleHash`, `coveredArtifacts`,
`commandVersion`(if present), `generatorVersion`, `base`, `head`, `evidence`. Entries are emitted in the
store's existing **newest-first list order**, re-sorting nothing.

**Rationale**: A fixed order is required for byte-stability (FR-003). This order mirrors the `FreshnessInputs`
record declaration (carried identity, then Phase-11 additions, then base/head) with `evidence` last, matching
the reader's `parseEntry` construction for readability. Entry order is verbatim because `ReuseStore` equality
is list-order-sensitive (same reasoning as D5) and `record` already maintains newest-first.

## D8 — Retention policy: keep the newest `maxEntries`, bound passed in with a default constant

**Decision**: `retain (maxEntries: int) (store: ReuseStore) : ReuseStore` keeps the first `maxEntries` entries
of the newest-first list (`List.truncate (max 0 maxEntries)`) and drops the rest. Also expose
`defaultRetentionBound : int = 256`. `maxEntries ≤ 0` ⇒ the empty store; a store already at or under the bound
is returned **unchanged** (idempotent, no reorder/rewrite).

**Rationale**: The store is newest-first (`record` convention), and a future run is most likely to match the
**newest** worlds, so global newest-first retention keeps the highest-value evidence (FR-006). It is total
(`max 0` guards negatives), idempotent at/under bound (`List.truncate` returns the list when shorter), and
removes only whole entries — never mutates or fabricates (US2). Recompute-safe by construction: removing
entries can only remove a potential winner, turning a future `Reuse` into `Recompute` — never the reverse
(FR-008). The bound is a **parameter** (testable, lets the later host row choose) with a named default; the
spec explicitly defers per-gate fairness and the exact numeric cap. `256` is a generous flat cap that keeps
disk/read cost bounded while comfortably covering many gates × a few recent worlds each.

**Alternatives rejected**: a hard-coded internal cap (less testable, no host control); per-gate fairness (the
spec defers it to keep the MVP newest-first-global); newest-first re-sort on output (the list is already
newest-first; re-sorting risks reordering equal-position entries and breaking idempotence).

## D9 — Pruning policy: drop entries a strictly-newer entry already full-matches

**Decision**: `prune (store: ReuseStore) : ReuseStore` removes every entry `e` for which some **strictly
newer** entry `e'` (earlier in the newest-first list) satisfies `FreshnessKey.matches e'.Inputs e.Inputs`
(same freshness world). Survivors are the newest entry of each world-equivalence-class, in their original
newest-first order. Implementation: a single fold over the newest-first list keeping an entry iff no
already-kept (newer) entry matches it — i.e. `record`'s full-match dedup applied across the whole store.

**Rationale**: An entry can win a reuse decision only if it is the newest full-match for some candidate
(`decide` returns the first/newest matching entry). If a strictly-newer entry shares its world, that newer
entry always wins for every candidate matching the world, so the older one "can never be served again" — the
spec's dead-entry criterion (US3). Defining the criterion as F029 `matches` reuses F030/F029 semantics
**verbatim** (FR-010) — no new policy. This is provably **verdict-preserving**: for any candidate, the newest
full-match is unchanged (the kept newer entry), and the cause-locating entry (most-recent sharing the gate)
is also unchanged, so `decide` is identical pre/post prune — hence trivially identical-or-stricter (FR-008,
SC-005). Survivors are a subset in newest-first order (US3 acceptance 1); a store with no dead entries (e.g.
any `record`-built store, which already de-dups full-matches) is returned unchanged (US3 acceptance 2).

**Alternatives rejected**: wall-clock/TTL expiry — `RecordedEvidence` carries no timestamp; out of scope
(Assumptions). Pruning by "gate identity only" (drop older entries sharing Check+Domain regardless of world) —
**unsafe**: different worlds of the same gate are legitimate distinct evidence that can each still win; dropping
them could change verdicts and is not "can never be reused again".

## Cross-cutting: the recompute-safety invariant (FR-008, SC-006)

All three operations satisfy: for every candidate `c` and store `s`, comparing `EvidenceReuse.decide c s`
against `decide c (op s)`, the result is **identical-or-stricter** — formally, it is never the case that
`decide c s` is a `Recompute` while `decide c (op s)` is a `Reuse`. `serialise` round-trips to an equal store
(verdicts identical). `retain` and `prune` only remove entries (a removed entry can only lose a potential
`Reuse`, never gain one). This is the single property `SafetyTests.fs` checks over FsCheck-generated
candidates and stores for each operation.
