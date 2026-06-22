// Freshness-inputs resolution vocabulary for the per-gate resolution (join) core (F043). The public surface is
// fixed by Model.fsi (Principle II); no top-level binding here carries an access modifier. These are the new
// supplied facts and named outcomes that `FreshnessResolution.resolve` works over; they reuse the F018 `GateId`,
// the F029 `FreshnessInputs` + its newtypes, and the F014 `CommandId` verbatim rather than redefining them
// (FR-012).

namespace FS.GG.Governance.FreshnessResolution

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type SensedFacts =
        { RuleHash: RuleHash option
          GeneratorVersion: GeneratorVersion option
          Base: Revision option
          Head: Revision option
          CoveredArtifacts: Map<GateId, ArtifactHash list>
          CommandVersions: Map<CommandId, CommandVersion> }

    type MissingFact =
        | MissingRuleHash
        | MissingCoveredArtifacts
        | MissingCommandVersion
        | MissingGeneratorVersion
        | MissingBaseRevision
        | MissingHeadRevision

    type ResolutionOutcome =
        | Resolved of FreshnessInputs
        | Unresolved of MissingFact list

    type FreshnessResolutionEntry =
        { Gate: GateId
          Outcome: ResolutionOutcome }

    type FreshnessResolutionReport = FreshnessResolutionReport of FreshnessResolutionEntry list
