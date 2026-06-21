# Contract: Evidence-Reuse Public API

The public surface of `FS.GG.Governance.EvidenceReuse` — the sole declaration is the two `.fsi` files. This
contract fixes the signatures and the laws each must satisfy; the surface-drift baseline
(`surface/FS.GG.Governance.EvidenceReuse.surface.txt`) is the byte-level guard.

## Module `FS.GG.Governance.EvidenceReuse.Model`

Declares the types in [data-model.md](../data-model.md): the new `EvidenceRef` newtype, the
`RecordedEvidence` record, the `ReuseStore` single-case DU, the `RecomputeCause` DU, and the `ReuseDecision`
DU. It `open`s `FS.GG.Governance.FreshnessKey.Model` for `FreshnessInputs` and `InputCategory` (reused
verbatim). No operations live here (this module is types only) — the `categoryToken` readable vocabulary is
F029's and is reused for messages/tests.

```fsharp
/// An opaque handle to already-recorded evidence. Carried back on Reuse; never parsed, validated,
/// produced, or dereferenced by this core (FR-001). An empty string is a literal value (FR-012).
type EvidenceRef = EvidenceRef of string

/// One stored entry: the world the evidence was recorded against (F029) + its opaque reference.
type RecordedEvidence = { Inputs: FreshnessInputs; Evidence: EvidenceRef }

/// The immutable collection of recorded entries (newest-first by `record` convention). Not a live cache.
type ReuseStore = ReuseStore of RecordedEvidence list

/// Why no entry served (the no-hide explanation, FR-006). InputsChanged carries a NON-EMPTY list that
/// never includes CheckIdentity/DomainIdentity (those identify the gate and are equal by construction).
type RecomputeCause =
    | NoPriorEvidence
    | InputsChanged of InputCategory list

/// The total result of `decide`.
type ReuseDecision =
    | Reuse of EvidenceRef
    | Recompute of RecomputeCause
```

## Module `FS.GG.Governance.EvidenceReuse` (operations)

```fsharp
/// The empty reuse store (`ReuseStore []`). TOTAL.
val empty: ReuseStore

/// Record evidence for the given freshness inputs, returning a NEW store. PURE and TOTAL: does not mutate
/// the input store (FR-007). De-duplicating: any existing entry that `FreshnessKey.matches` `inputs` is
/// dropped and the new entry becomes the most-recent, so the store holds at most one entry per
/// matching-input class (FR-008). Reads no clock/filesystem/git/environment/network (FR-009).
val record: inputs: FreshnessInputs -> evidence: EvidenceRef -> store: ReuseStore -> ReuseStore

/// Decide whether recorded evidence may be reused for `candidate`. PURE and TOTAL (FR-003). Returns
/// `Reuse ref` IFF some recorded entry `FreshnessKey.matches` the candidate on EVERY input category
/// (FR-004) — with duplicates, the most-recently-recorded matching entry's reference (FR-005). Otherwise
/// returns `Recompute cause` with a located cause (FR-006): `InputsChanged (FreshnessKey.diff candidate e)`
/// for the most-recent entry `e` sharing the candidate's GateId (Check+Domain), else `NoPriorEvidence`.
val decide: candidate: FreshnessInputs -> store: ReuseStore -> ReuseDecision

/// The recorded entries, newest-first (for inspection/tests). TOTAL.
val entries: store: ReuseStore -> RecordedEvidence list

/// Unwrap an EvidenceRef to its string (for storage, messages, tests). TOTAL.
val referenceValue: reference: EvidenceRef -> string
```

## Laws (verified by the test project)

| Law | Statement | Tests / SC |
|---|---|---|
| **Reuse iff all match** | `decide c s = Reuse r` ⇔ some entry in `s` `matches` `c` (and `r` is its evidence). | ReuseDecisionTests / SC-001 |
| **Single-field distinction** | If `s` holds one entry and `c` differs from it in exactly one category, `decide c s = Recompute (InputsChanged [thatCategory])`. Holds for every category. | ReuseDecisionTests, ExplanationTests / SC-001, SC-003 |
| **Reflexive reuse** | `decide e.Inputs (record e.Inputs e.Evidence empty) = Reuse e.Evidence`. | RecordTests / SC-005 |
| **Empty store** | `decide c empty = Recompute NoPriorEvidence` for every `c`. | EmptyStoreTests / SC-004 |
| **Cause located** | Every `Recompute` carries `NoPriorEvidence` or a non-empty `InputsChanged`; the latter never contains `CheckIdentity`/`DomainIdentity`. | ExplanationTests / SC-003 |
| **NoPriorEvidence vs InputsChanged** | If no entry shares `c`'s Check+Domain ⇒ `NoPriorEvidence`; if a same-gate entry exists but none fully matches ⇒ `InputsChanged (diff c e)`. | ExplanationTests / SC-003 |
| **Determinism** | `decide c s` is identical every time, anywhere; `record` replayed on the same start store yields equivalent decisions. | DeterminismTests, PurityTests / SC-002, SC-006 |
| **Set semantics** | Reordering/duplicating `CoveredArtifacts` in `c` or in a stored entry changes no decision. | DeterminismTests / SC-002 |
| **Refresh / de-dup** | `record i r2 (record i r1 s)` resolves a matching candidate to `r2`, and `entries` holds no duplicate for `i`. | RecordTests / SC-005 |
| **Independence** | Recording under non-matching inputs leaves every prior entry independently reusable. | RecordTests / SC-005 |
| **No mutation** | `record` does not alter the input `ReuseStore` value. | RecordTests / FR-007 |
| **Totality** | Every `FreshnessInputs`/`EvidenceRef`/`ReuseStore` yields a decision/store with no exception (incl. empty store, empty reference). | EmptyStoreTests, property tests / FR-012 |
| **Purity** | Decisions/records are identical across changed cwd, time, and unrelated filesystem state. | PurityTests / SC-006 |

## Scope guard (negative contract)

- The assembly references **only** `FSharp.Core`, `FS.GG.Governance.FreshnessKey`,
  `FS.GG.Governance.Config` (transitive, via F029), and the BCL (`System.*` / `System.Private.CoreLib` /
  `netstandard` / `mscorlib`). It MUST NOT reference `Gates`, `Snapshot`, `Route`, `Routing`, `Findings`,
  any `Adapters.*`, `Host`, `Cli`, `Ship`, `Enforcement`, `AuditJson`, or any host/edge assembly — verified
  by the `SurfaceDrift` scope-hygiene test (the F029 precedent).
- No new third-party `PackageReference` (FR-014).
- Exactly the two modules above are public; no helper module leaks (hidden by the `.fsi`).
