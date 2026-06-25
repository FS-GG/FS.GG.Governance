namespace FS.GG.Governance.ReleaseReport

open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model

// The F26 publication-report type vocabulary (P1). Immutable, presentation-free values. The surface is
// Model.fsi (Principle II) — no access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type PreconditionEvidence =
        { Kind: ReleaseRuleKind
          State: FactState
          Reason: string }

    type ReleaseReport =
        { Decision: ReleaseDecision
          Sensed: SensedRelease
          Package: PackEvidenceSet
          Preconditions: PreconditionEvidence list
          Attestation: AttestationSummary
          ReleaseExitCodeBasis: ExitCodeBasis }

    type VerifyReleasePreview =
        { Verdict: Verdict
          Package: PackEvidenceSet
          Preconditions: PreconditionEvidence list
          Attestation: AttestationSummary
          Advisory: bool }
