namespace FS.GG.Governance.Attestation

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind.Model

// The F26 SLSA/in-toto-shaped attestation type vocabulary (P2). Pure, product-neutral values. The surface is
// Model.fsi (Principle II) — no access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type AttestationSubject =
        { Name: string
          Digest: ArtifactHash
          Version: string }

    type AttestationMaterials =
        { RuleHash: RuleHash
          GeneratorVersion: GeneratorVersion
          BaseRevision: Revision
          HeadRevision: Revision
          SourceCommit: Revision
          ArtifactDigests: ArtifactHash list
          Environment: EnvironmentClass }

    type AttestationInvocation = { Runs: KindedCommandRun list }

    type ComplianceMarker =
        | CompatibleShapeNotFormalCompliance

    type AttestationSummary =
        { Subjects: AttestationSubject list
          Builder: BuilderIdentity
          Materials: AttestationMaterials
          Invocation: AttestationInvocation
          Identity: string
          Compliance: ComplianceMarker }
