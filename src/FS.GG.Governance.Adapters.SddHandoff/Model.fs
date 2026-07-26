// The typed SDD→Governance handoff shape + version pin (F081). Visibility lives in Model.fsi
// (Principle II). Pure data — records/unions and one constant; no I/O, no behaviour.

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type DeclaredState =
        | Pending
        | Real
        | Synthetic
        | Failed
        | Skipped
        | Deferred
        | AcceptedDeferral

    type DeclaredNode =
        { Id: string
          State: DeclaredState
          Stale: bool
          Rationale: string option }

    type EvidenceBlock =
        { Nodes: DeclaredNode list
          Dependencies: (string * string) list }

    type ReadinessBlock =
        { ShipDisposition: string
          VerificationReadiness: string
          BlockingDiagnosticIds: string list
          Counts: (string * int) list
          PerViewState: (string * string) list }

    type JourneyProvenanceDisposition =
        | JourneySatisfied
        | JourneyReceiptInvalid
        | JourneyReceiptStale
        | JourneyProvenanceUnsupported

    type JourneyReadiness =
        { ObligationsUnmet: int
          BlockingDiagnosticIds: string list
          RelatedIds: string list
          Disposition: JourneyProvenanceDisposition }

    type GovernedReference =
        { Path: GovernedPath
          Owner: string
          Relationship: string
          Kind: string option
          Operation: string option }

    type HandoffDiagnostic =
        { Id: string
          Message: string
          Correction: string
          RelatedIds: string list }

    type Handoff =
        { ContractVersion: string
          SchemaVersion: int
          GeneratorVersion: string option
          Evidence: EvidenceBlock
          Readiness: ReadinessBlock option
          JourneyReadiness: JourneyReadiness option
          GovernedReferences: GovernedReference list
          PerformanceEvidence: Fsgg.Schemas.GovernanceHandoffPerformanceEvidence list
          Diagnostics: HandoffDiagnostic list }

    type PerformanceGateState =
        | PerformancePassed
        | PerformanceFailed
        | PerformanceEnvironmentLimited
        | PerformanceNotApplicable

    type PerformanceEvaluation =
        { EvidenceId: string
          ArtifactPath: string
          State: PerformanceGateState
          Measurements: Fsgg.Schemas.PerformanceEvidenceMeasurement list
          Failures: string list
          Remediation: string }

    type DiagnosticCause =
        | VersionMismatch
        | Malformed
        | AutoSyntheticDeclared
        | StaleEvidence

    type Diagnostic =
        { Cause: DiagnosticCause
          Source: string
          Message: string }

    let supportedContractMajor = 2
