// Curated public signature contract for the agent-review verdict cache-key operations (F035).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching AgentReviewKey.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any AgentReviewKey.fs
// body exists (Principle I). All four operations are PURE and TOTAL (FR-002, FR-007): defined for every
// `AgentReviewInputs` value, never throwing, reading no clock, filesystem, git, environment, or network,
// invoking no model/agent, hashing no bytes, and byte-for-byte identical for identical input regardless of
// evaluation time, machine, process, or collection order. This row carries/stores NO cached verdict and
// runs NO cache store/lookup/invalidation operation (FR-009): its sole outputs are the key value, the
// cache-hit decision, and the no-hide difference.

namespace FS.GG.Governance.AgentReviewKey

open FS.GG.Governance.AgentReviewKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AgentReviewKey =

    /// Render the seven agent-review inputs to their canonical, deterministic, byte-stable `CacheKey`
    /// (contracts/agent-review-key-format.md). PURE and TOTAL: defined for every `AgentReviewInputs` value;
    /// reads no clock, filesystem, git, environment, or network; invokes no model; hashes no bytes (FR-002,
    /// FR-007). Reviewed artifacts are compared as a SET — deduplicated and ordinally sorted — so order and
    /// duplication never affect the result (FR-006). The encoding is INJECTIVE across inputs: the same
    /// opaque string placed in two different inputs yields different keys (FR-003). BCL string building
    /// only; no hashing.
    val compute: inputs: AgentReviewInputs -> CacheKey

    /// The cache-hit predicate: true IFF the two input sets agree on EVERY input — i.e. their keys are
    /// equal. TOTAL. Defined as `compute a = compute b`, so the predicate and the key can never disagree
    /// (FR-004). The literal foundation of the later "invalidate cached verdicts when judge/prompt identity
    /// changes" row.
    val matches: a: AgentReviewInputs -> b: AgentReviewInputs -> bool

    /// The no-hide explainer: the inputs whose values differ between two input sets, in the fixed
    /// key-encoding order (FR-005). Empty IFF `matches a b`. Reviewed artifacts are compared as a SET
    /// (reordered/duplicated artifacts are not reported). TOTAL. The observable face of "a judge or prompt
    /// change invalidates prior cached verdicts."
    val diff: a: AgentReviewInputs -> b: AgentReviewInputs -> ReviewInput list

    /// Unwrap a `CacheKey` to its canonical string (for storage, messages, tests). TOTAL.
    val value: key: CacheKey -> string
