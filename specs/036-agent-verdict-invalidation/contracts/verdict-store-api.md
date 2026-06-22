# Contract: Verdict-Store Public API

The public surface of `FS.GG.Governance.VerdictReuse` — the sole declaration is the two `.fsi` files. This
contract fixes the signatures and the laws each must satisfy; the surface-drift baseline
(`surface/FS.GG.Governance.VerdictReuse.surface.txt`) is the byte-level guard.

## Module `FS.GG.Governance.VerdictReuse.Model`

Declares the types in [data-model.md](../data-model.md): the new `VerdictRef` newtype, the `CachedVerdict`
record, the `VerdictStore` single-case DU, the `IdentityGroup` DU + `inputGroup` projection, the
`InvalidationCause` DU, and the `LookupDecision` DU. It `open`s `FS.GG.Governance.AgentReviewKey.Model` for
`AgentReviewInputs` and `ReviewInput` (reused verbatim). The only operation in this module is `inputGroup` (a
total projection); the decision operations live in the `VerdictReuse` module. F035's `inputToken` readable
vocabulary is reused for messages/tests.

```fsharp
/// An opaque handle to an already-cached agent-reviewed verdict. Carried back on Valid; never parsed,
/// validated, produced, or dereferenced by this core, and never read for advisory/blocking content
/// (FR-001). An empty string is a literal value (FR-012).
type VerdictRef = VerdictRef of string

/// One cached entry: the F035 seven-input identity a verdict was produced under + its opaque reference.
type CachedVerdict = { Inputs: AgentReviewInputs; Verdict: VerdictRef }

/// The immutable collection of cached entries (newest-first by `record` convention). Not a live cache.
type VerdictStore = VerdictStore of CachedVerdict list

/// The three identity groups a changed input is attributed to (research D6).
type IdentityGroup =
    | JudgeIdentity          // model id, model version, model configuration
    | PromptIdentity         // reviewer prompt hash, question text
    | CheckArtifactIdentity  // check hash, reviewed-artifact hashes

/// Attribute a differing input to its identity group. TOTAL over all seven ReviewInput cases.
val inputGroup: input: ReviewInput -> IdentityGroup

/// Why no cached verdict served (the no-hide explanation, FR-006). InputsChanged carries a NON-EMPTY list
/// that never includes CheckHashInput (the work key, equal for the chosen prior entry by construction).
type InvalidationCause =
    | NoCachedVerdict
    | InputsChanged of ReviewInput list

/// The total result of `lookup`.
type LookupDecision =
    | Valid of VerdictRef
    | Invalidated of InvalidationCause
```

## Module `FS.GG.Governance.VerdictReuse` (operations)

```fsharp
/// The empty verdict store (`VerdictStore []`). TOTAL.
val empty: VerdictStore

/// Record a verdict for the given agent-review inputs, returning a NEW store. PURE and TOTAL: does not mutate
/// the input store (FR-007). De-duplicating: any existing entry that `AgentReviewKey.matches` `inputs` is
/// dropped and the new entry becomes the most-recent, so the store holds at most one entry per matching-input
/// class (FR-008). Reads no clock/filesystem/git/environment/network (FR-009).
val record: inputs: AgentReviewInputs -> verdict: VerdictRef -> store: VerdictStore -> VerdictStore

/// Decide whether a cached verdict is still valid for `request`. PURE and TOTAL (FR-003). Returns
/// `Valid ref` IFF some cached entry `AgentReviewKey.matches` the request on EVERY one of the seven inputs
/// (FR-004) — with duplicates, the most-recently-recorded matching entry's reference (FR-005). Otherwise
/// returns `Invalidated cause` with a located cause (FR-006): `InputsChanged (AgentReviewKey.diff request
/// e.Inputs)` for the most-recent entry `e` sharing the request's check hash (`e.Inputs.Check =
/// request.Check`), else `NoCachedVerdict`. Reads no clock/filesystem/git/environment/network (FR-009).
val lookup: request: AgentReviewInputs -> store: VerdictStore -> LookupDecision

/// The cached entries, newest-first (for inspection/tests). TOTAL.
val entries: store: VerdictStore -> CachedVerdict list

/// Unwrap a VerdictRef to its string (for storage, messages, tests). TOTAL.
val referenceValue: verdict: VerdictRef -> string
```

## Laws (verified by the test project)

| Law | Statement | Tests / SC |
|---|---|---|
| **Valid iff all match** | `lookup r s = Valid ref` ⇔ some entry in `s` `matches` `r` (and `ref` is its verdict). | LookupDecisionTests / SC-001 |
| **Single-field invalidation** | If `s` holds one entry sharing `r`'s check and `r` differs from it in exactly one of the other inputs, `lookup r s = Invalidated (InputsChanged [thatInput])`. Holds for every non-check input. | LookupDecisionTests, ExplanationTests / SC-001, SC-003 |
| **Judge/prompt attribution** | An entry differing only in a judge input (model id/version/config) ⇒ the changed input's `inputGroup` is `JudgeIdentity`; only in a prompt input (prompt hash/question) ⇒ `PromptIdentity`. | ExplanationTests / SC-002 |
| **Reflexive validity** | `lookup i (record i v empty) = Valid v`. | RecordTests / SC-005 |
| **Empty store** | `lookup r empty = Invalidated NoCachedVerdict` for every `r`. | EmptyStoreTests / SC-001, SC-003 |
| **Cause located** | Every `Invalidated` carries `NoCachedVerdict` or a non-empty `InputsChanged`; the latter never contains `CheckHashInput`. | ExplanationTests / SC-003 |
| **NoCachedVerdict vs InputsChanged** | If no entry shares `r`'s check ⇒ `NoCachedVerdict`; if a same-check entry exists but none fully matches ⇒ `InputsChanged (diff r e)`. | ExplanationTests / SC-003 |
| **Determinism** | `lookup r s` is identical every time, anywhere; `record` replayed on the same start store yields equivalent decisions. | DeterminismTests, PurityTests / SC-004, SC-006 |
| **Set semantics** | Reordering/duplicating `ReviewedArtifacts` in `r` or in a stored entry changes no decision. | DeterminismTests / SC-004 |
| **Refresh / de-dup** | `record i v2 (record i v1 s)` resolves a matching request to `v2`, and `entries` holds no duplicate for `i`. | RecordTests / SC-005 |
| **Independence** | Recording under non-matching inputs leaves every prior entry independently reusable. | RecordTests / SC-005 |
| **No mutation** | `record` does not alter the input `VerdictStore` value. | RecordTests / FR-007 |
| **Totality** | Every `AgentReviewInputs`/`VerdictRef`/`VerdictStore` yields a decision/store with no exception (incl. empty store, empty reference). | EmptyStoreTests, property tests / FR-012 |
| **Purity** | Decisions/records are identical across changed cwd, time, and unrelated filesystem state. | PurityTests / SC-006 |

## Scope guard (negative contract)

- The assembly references **only** `FSharp.Core`, `FS.GG.Governance.AgentReviewKey`,
  `FS.GG.Governance.FreshnessKey` (transitive, via F035), `FS.GG.Governance.Config` (transitive), and the BCL
  (`System.*` / `System.Private.CoreLib` / `netstandard` / `mscorlib`). It MUST NOT reference `Gates`,
  `Snapshot`, `Route`, `Routing`, `Findings`, `EvidenceReuse`, any `Adapters.*`, `Host`, `Cli`, `Ship`,
  `Enforcement`, `AuditJson`, or any host/edge assembly — verified by the `SurfaceDrift` scope-hygiene test (the
  F029/F030/F035 precedent).
- No new third-party `PackageReference` (FR-014).
- Exactly the two modules above are public; no helper module leaks (hidden by the `.fsi`).
