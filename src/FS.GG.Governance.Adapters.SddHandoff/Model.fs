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

    type GovernedReference =
        { WorkItem: string
          Paths: GovernedPath list }

    type Handoff =
        { ContractVersion: string
          SchemaVersion: int
          Evidence: EvidenceBlock
          Readiness: ReadinessBlock option
          GovernedReferences: GovernedReference list }

    type DiagnosticCause =
        | VersionMismatch
        | Malformed
        | AutoSyntheticDeclared
        | StaleEvidence

    type Diagnostic =
        { Cause: DiagnosticCause
          Source: string
          Message: string }

    let supportedContractMajor = 1
