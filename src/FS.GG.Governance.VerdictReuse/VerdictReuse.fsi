// Curated public signature contract for the agent-reviewed verdict store + invalidation-decision operations
// (F036).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// VerdictReuse.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any VerdictReuse.fs body
// exists (Principle I). Both decision operations are PURE and TOTAL (FR-003, FR-009): defined for every
// `AgentReviewInputs`/`VerdictRef`/`VerdictStore` value, never throwing, reading no clock, filesystem, git,
// environment, or network, invoking no model/agent, computing no key bytes (F035 owns the key), and
// byte-for-byte identical for identical input regardless of evaluation time, machine, process, or collection
// order. Validity is EXACTLY F035 `matches`; the explanation is EXACTLY F035 `diff`. This row carries NO
// verdict content (the `VerdictRef` is opaque), performs NO persistence/eviction/expiry, and makes NO
// advisory-vs-blocking promotion decision (FR-011).

namespace FS.GG.Governance.VerdictReuse

open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VerdictReuse =

    /// The empty verdict store (`VerdictStore []`). TOTAL.
    val empty: VerdictStore

    /// Record a verdict for the given agent-review inputs, returning a NEW store. PURE and TOTAL: does not
    /// mutate the input store (FR-007). De-duplicating: any existing entry that `AgentReviewKey.matches`
    /// `inputs` is dropped and the new entry becomes the most-recent, so the store holds at most one entry
    /// per matching-input class (FR-008); entries that merely share the work but differ in some input are
    /// kept. Reads no clock/filesystem/git/environment/network (FR-009).
    val record: inputs: AgentReviewInputs -> verdict: VerdictRef -> store: VerdictStore -> VerdictStore

    /// Decide whether a cached verdict is still valid for `request`. PURE and TOTAL (FR-003). Returns
    /// `Valid ref` IFF some cached entry `AgentReviewKey.matches` the request on EVERY one of the seven
    /// inputs (FR-004) — with duplicates, the most-recently-recorded matching entry's reference (FR-005).
    /// Otherwise returns `Invalidated cause` with a located cause (FR-006): `InputsChanged
    /// (AgentReviewKey.diff request e.Inputs)` for the most-recent entry `e` sharing the request's check hash
    /// (`e.Inputs.Check = request.Check`), else `NoCachedVerdict`. Reads no
    /// clock/filesystem/git/environment/network (FR-009).
    val lookup: request: AgentReviewInputs -> store: VerdictStore -> LookupDecision

    /// The cached entries, newest-first (for inspection/tests). TOTAL.
    val entries: store: VerdictStore -> CachedVerdict list

    /// Unwrap a `VerdictRef` to its string (for storage, messages, tests). TOTAL.
    val referenceValue: verdict: VerdictRef -> string
