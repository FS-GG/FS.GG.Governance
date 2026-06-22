// Curated public signature contract for the verdict-store + invalidation-decision vocabulary of the
// agent-reviewed verdict store core (F036).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body exists
// (Principle I). These are the product-neutral, comparable values the `VerdictReuse.lookup`/`record`
// operations decide over. The F035 agent-review vocabulary (`AgentReviewInputs`, `ReviewInput`) is `open`ed
// from `FS.GG.Governance.AgentReviewKey.Model` and reused VERBATIM (research D2, FR-010) — nothing in
// AgentReviewKey/FreshnessKey/Config is redefined. The only genuinely new type is the opaque `VerdictRef`;
// the store/cause/decision/identity-group types are this row's vocabulary. No field carries raw bytes, host
// paths, clock readings, verdict content, or product vocabulary.

namespace FS.GG.Governance.VerdictReuse

open FS.GG.Governance.AgentReviewKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── New opaque newtype (this feature) ──

    /// An opaque handle to an already-cached agent-reviewed verdict (e.g. a content-addressed pointer /
    /// recorded-verdict id). Minted at the edge and supplied as data; carried back on `Valid`. This core
    /// never parses, validates, produces, or dereferences it, and never reads its advisory/blocking content
    /// (FR-001). An empty string is a literal value (FR-012).
    type VerdictRef = VerdictRef of string

    // ── Key entity: one cached entry ──

    /// One cached entry: the F035 seven-input identity a verdict was produced under, paired with its opaque
    /// reference (FR-001).
    type CachedVerdict =
        { Inputs: AgentReviewInputs
          Verdict: VerdictRef }

    // ── Key entity: the in-value store ──

    /// The immutable collection of cached entries — the supplied, in-value "what has been cached so far"
    /// (FR-002). Newest-first by `record` convention (research D4). Not a live cache, connection, or file.
    /// `record` maintains the at-most-one-entry-per-matching-input-class invariant; `lookup` stays total and
    /// deterministic even on a hand-built store that violates it (head-first scan ⇒ most-recent wins).
    type VerdictStore = VerdictStore of CachedVerdict list

    // ── Key entity: the attribution projection ──

    /// The three identity groups a changed input is attributed to, so US2 sees a judge change and a prompt
    /// change EACH AS SUCH (research D6).
    type IdentityGroup =
        | JudgeIdentity
        | PromptIdentity
        | CheckArtifactIdentity

    /// Attribute a differing input to its identity group. TOTAL over all seven `ReviewInput` cases — model
    /// id/version/config ⇒ `JudgeIdentity`; prompt hash/question text ⇒ `PromptIdentity`; check hash/reviewed
    /// artifacts ⇒ `CheckArtifactIdentity` (data-model table). Total even for `CheckHashInput`, which cannot
    /// itself appear inside an `InputsChanged` diff (the work key, equal by construction — research D5).
    val inputGroup: input: ReviewInput -> IdentityGroup

    // ── Key entity: the no-hide explanation ──

    /// Why no cached verdict served (the no-hide explanation, FR-006). `InputsChanged` carries a NON-EMPTY
    /// list that NEVER includes `CheckHashInput` (the work key, equal for the chosen prior entry by
    /// construction); each element is attributable via `inputGroup`. The two cases are crisply distinct:
    /// `NoCachedVerdict` ("never cached a verdict for this rule") vs `InputsChanged` ("cached one, but an
    /// identity input moved").
    type InvalidationCause =
        | NoCachedVerdict
        | InputsChanged of ReviewInput list

    // ── Key entity: the total result of `lookup` ──

    /// The total result of `lookup` (FR-003): `Valid ref` (some entry matched on every input — reuse this
    /// verdict) or `Invalidated cause` (no entry matched — here is why).
    type LookupDecision =
        | Valid of VerdictRef
        | Invalidated of InvalidationCause
