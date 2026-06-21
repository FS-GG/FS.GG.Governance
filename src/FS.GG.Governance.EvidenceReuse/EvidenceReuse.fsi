// Curated public signature contract for the evidence-reuse operations (F030).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching EvidenceReuse.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any EvidenceReuse.fs
// body exists (Principle I). Both `decide` and `record` are PURE and TOTAL (FR-003, FR-009): defined for
// every input, never throwing, reading no clock, filesystem, git, environment, or network, and identical
// for identical input regardless of evaluation time, machine, process, or covered-artifact order. This row
// computes NO persistence, NO eviction/expiry, NO output-digest verification, runs NO gate, emits NO ship
// verdict, and adds NO CLI: its sole outputs are the reuse decision and the updated store value. Reuse is
// EXACTLY F029 `matches`; the explanation is EXACTLY F029 `diff` (research D2).

namespace FS.GG.Governance.EvidenceReuse

open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceReuse =

    /// The empty reuse store (`ReuseStore []`). TOTAL.
    val empty: ReuseStore

    /// Record evidence for the given freshness inputs, returning a NEW store. PURE and TOTAL: does not
    /// mutate the input store (FR-007). De-duplicating: any existing entry that `FreshnessKey.matches`
    /// `inputs` is dropped and the new entry becomes the most-recent, so the store holds at most one entry
    /// per matching-input class (FR-008). Entries that merely share the gate but differ in some category
    /// are KEPT (they are evidence for a different world of the same gate). Reads no
    /// clock/filesystem/git/environment/network (FR-009).
    val record: inputs: FreshnessInputs -> evidence: EvidenceRef -> store: ReuseStore -> ReuseStore

    /// Decide whether recorded evidence may be reused for `candidate`. PURE and TOTAL (FR-003). Returns
    /// `Reuse ref` IFF some recorded entry `FreshnessKey.matches` the candidate on EVERY input category
    /// (FR-004) — with duplicates, the most-recently-recorded matching entry's reference (FR-005, head-first
    /// over the newest-first store). Otherwise returns `Recompute cause` with a located cause (FR-006):
    /// `InputsChanged (FreshnessKey.diff candidate e.Inputs)` for the most-recent entry `e` sharing the
    /// candidate's GateId (Check AND Domain) — a non-empty list excluding the identity categories — else
    /// `NoPriorEvidence`. Reads no clock/filesystem/git/environment/network (FR-009).
    val decide: candidate: FreshnessInputs -> store: ReuseStore -> ReuseDecision

    /// The recorded entries, newest-first (for inspection/tests). TOTAL.
    val entries: store: ReuseStore -> RecordedEvidence list

    /// Unwrap an `EvidenceRef` to its string (for storage, messages, tests). TOTAL.
    val referenceValue: reference: EvidenceRef -> string
