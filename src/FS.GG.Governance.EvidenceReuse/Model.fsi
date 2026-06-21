// Curated public signature contract for the evidence-reuse types (F030).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, comparable values the `EvidenceReuse.decide` /
// `record` operations work over. They REUSE the F029 freshness vocabulary (`FreshnessInputs`,
// `InputCategory`) verbatim — opened from `FS.GG.Governance.FreshnessKey.Model`, never redefined (FR-010).
// The only new type is `EvidenceRef`, an OPAQUE single-case string the edge mints and supplies as data:
// this core never parses, validates, produces, or dereferences it (the F029 `Revision` precedent —
// research D3).

namespace FS.GG.Governance.EvidenceReuse

open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// An opaque handle to already-recorded evidence (e.g. a content-addressed pointer or a recorded-
    /// evidence id). Carried back verbatim on *Reuse*; never parsed, validated, produced, or dereferenced
    /// by this core (FR-001). No validation, no parsing — an empty string is a literal value (FR-012).
    type EvidenceRef = EvidenceRef of string

    /// One stored entry: the world the evidence was recorded against (F029 `FreshnessInputs`) paired with
    /// its opaque reference (FR-001).
    type RecordedEvidence =
        { Inputs: FreshnessInputs
          Evidence: EvidenceRef }

    /// The immutable collection of recorded entries — the supplied, in-value "what has been recorded so
    /// far" (FR-002). Newest-first by `record` convention (research D4). NOT a live cache, connection, or
    /// file: a value handed in and returned.
    type ReuseStore = ReuseStore of RecordedEvidence list

    /// Why no entry served — the no-hide explanation, always present and locatable (FR-006, Principle VI).
    /// `NoPriorEvidence`: no entry shares the candidate's gate identity (Check+Domain). `InputsChanged`:
    /// prior evidence for this gate exists but the world moved — the list is NON-EMPTY and NEVER contains
    /// `CheckIdentity`/`DomainIdentity` (those identify the gate and are equal by construction — research
    /// D5).
    type RecomputeCause =
        | NoPriorEvidence
        | InputsChanged of InputCategory list

    /// The total result of `decide` (FR-003). `Reuse` carries the matching entry's opaque evidence;
    /// `Recompute` carries the located cause.
    type ReuseDecision =
        | Reuse of EvidenceRef
        | Recompute of RecomputeCause
