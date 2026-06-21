// Curated public signature contract for the agent-review input vocabulary of the agent-review verdict
// cache-key core (F035).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, comparable values the `AgentReviewKey.compute`
// projection fingerprints: the closed set of SEVEN inputs an agent-reviewed verdict depends on. The CHECK
// hash REUSES F029's `RuleHash` and the reviewed-artifact hashes REUSE F029's `ArtifactHash` VERBATIM —
// brought in by the `open` below rather than redefined (research D2, FR-008). No field carries raw bytes,
// host paths, clock readings, or product vocabulary; the model id/version/prompt-hash/config/question are
// opaque single-case strings supplied by the edge (FR-001).

namespace FS.GG.Governance.AgentReviewKey

open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── New opaque newtypes (this feature, research D2) ──

    /// Which model answered (its id). Opaque and comparable; the actual id is formed at the edge and
    /// passed in as data (FR-001). No validation, no parsing — an empty string is a literal value (FR-003).
    type ModelId = ModelId of string

    /// The model's version. Opaque, comparable, edge-supplied.
    type ModelVersion = ModelVersion of string

    /// A supplied digest of the reviewer prompt. Opaque, comparable, edge-supplied.
    type ReviewerPromptHash = ReviewerPromptHash of string

    /// The relevant model configuration (e.g. a canonical settings token). Opaque, comparable,
    /// edge-supplied.
    type ModelConfig = ModelConfig of string

    /// The question text the reviewer was asked. Opaque, comparable, edge-supplied.
    type QuestionText = QuestionText of string

    // ── Key entity: the closed seven-input set (FR-001) ──

    /// The closed, typed set of the SEVEN inputs an agent-reviewed verdict depends on (FR-001). Every input
    /// is named and type-checked. `ReviewedArtifacts` is a list compared as a SET (order and duplication
    /// ignored — FR-006). All seven are REQUIRED — there is no `option` field; an empty token (e.g.
    /// `QuestionText ""`) is a valid literal value, not an absence. The CHECK hash reuses F029's `RuleHash`
    /// and the reviewed-artifact hashes reuse F029's `ArtifactHash` verbatim (research D2, FR-008).
    ///
    /// Record field order is GROUPING-FOR-READABILITY only — it is NOT the encoding/`diff` order. The
    /// canonical key encoding and the `diff` result use the FIXED encoding order (model id, model version,
    /// prompt hash, model config, check hash, reviewed artifacts, question text) defined in
    /// contracts/agent-review-key-format.md and the `ReviewInput` DU below.
    type AgentReviewInputs =
        { // ── judge identity ──
          Model: ModelId
          ModelVersion: ModelVersion
          Config: ModelConfig
          // ── prompt / question identity ──
          PromptHash: ReviewerPromptHash
          Question: QuestionText
          // ── check / reviewed-artifact identity (reused F029 vocabulary) ──
          Check: RuleHash
          ReviewedArtifacts: ArtifactHash list }

    // ── Key entity: the computed cache key ──

    /// The deterministic, byte-stable, comparable cache key produced from an `AgentReviewInputs` value by
    /// `AgentReviewKey.compute`. Equal `CacheKey`s mean "same judge / prompt / check / artifact identity,
    /// the cached verdict is reusable"; different `CacheKey`s mean "some identity input changed, the verdict
    /// is not reusable". The wrapped string is the canonical tagged, length-prefixed rendering
    /// (contracts/agent-review-key-format.md), so equality is exact byte equality and the value is portable
    /// across runs and machines (what the later verdict store keys on).
    ///
    /// NAMING NOTE (avoid confusion): this computed-key type is `CacheKey` — NOT `Key`. F029's
    /// `FreshnessKey.Model` already exports a `Key` type, and this core `open`s that module (for
    /// `RuleHash`/`ArtifactHash`). Naming this core's key `CacheKey` keeps the two unambiguous and leaves
    /// F029's `Key` untouched (research D3).
    type CacheKey = CacheKey of string

    // ── Key entity: the comparable inputs (the no-hide explainer's vocabulary) ──

    /// The closed enumeration of comparable inputs, returned by `AgentReviewKey.diff` to name exactly what
    /// changed between two input sets (FR-005, the no-hide requirement). One case per input, in the fixed
    /// key-encoding order.
    type ReviewInput =
        | ModelIdInput
        | ModelVersionInput
        | PromptHashInput
        | ModelConfigInput
        | CheckHashInput
        | ReviewedArtifactsInput
        | QuestionTextInput

    /// Stable, human-readable wire token for a `ReviewInput` (for `diff` output and messages).
    /// Deterministic, total, and INJECTIVE over the 7 cases. This readable vocabulary
    /// (`modelId`/`checkHash`/`reviewedArtifacts`/…) is DELIBERATELY DISTINCT from the terse encoding tags
    /// inside the key string (`mid`/`chk`/`art`/…, contracts/agent-review-key-format.md) — see the table in
    /// contracts/agent-review-key-api.md.
    val inputToken: input: ReviewInput -> string
