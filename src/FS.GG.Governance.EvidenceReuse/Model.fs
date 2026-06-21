// Evidence-reuse types for the evidence-reuse decision core (F030). The public surface is fixed by
// Model.fsi (Principle II); no top-level binding here carries an access modifier. These are product-neutral,
// comparable values that `EvidenceReuse.decide` / `record` work over; they reuse the F029 freshness
// vocabulary (`FreshnessInputs`, `InputCategory`) verbatim rather than redefining it (FR-010). `EvidenceRef`
// is an opaque, edge-supplied token (the F029 `Revision` precedent — research D3).

namespace FS.GG.Governance.EvidenceReuse

open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type EvidenceRef = EvidenceRef of string

    type RecordedEvidence =
        { Inputs: FreshnessInputs
          Evidence: EvidenceRef }

    type ReuseStore = ReuseStore of RecordedEvidence list

    type RecomputeCause =
        | NoPriorEvidence
        | InputsChanged of InputCategory list

    type ReuseDecision =
        | Reuse of EvidenceRef
        | Recompute of RecomputeCause
