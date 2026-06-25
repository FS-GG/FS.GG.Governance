// Curated public signature contract for the SLSA/in-toto-shaped attestation types (F26, P2).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO access modifiers. REUSES the upstream vocabulary verbatim (opened, never redefined):
// F014 `EnvironmentClass` (Config), F029 `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash`
// (FreshnessKey), F033 `BuilderIdentity` (Provenance), F25 `KindedCommandRun` (CommandKind). Identity is
// `Provenance.canonicalId` reused verbatim — the attestation is a projection, not a new identity (D5).

namespace FS.GG.Governance.Attestation

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// One attested artifact (the in-toto "subject"). Built ONLY from a Packed PackArtifact — a failed /
    /// no-artifact pack yields no subject (FR-008). Name is the normalized artifact path.
    type AttestationSubject =
        { Name: string
          Digest: ArtifactHash
          Version: string }

    /// The in-toto "materials" — the reproducible build inputs, projected verbatim from the F033 Provenance.
    /// ArtifactDigests is treated as a SET in identity (D7).
    type AttestationMaterials =
        { RuleHash: RuleHash
          GeneratorVersion: GeneratorVersion
          BaseRevision: Revision
          HeadRevision: Revision
          SourceCommit: Revision
          ArtifactDigests: ArtifactHash list
          Environment: EnvironmentClass }

    /// The in-toto "invocation" — the recorded command runs (order-significant, D7). Duration is carried only
    /// inside each embedded CommandRecord and excluded from identity.
    type AttestationInvocation = { Runs: KindedCommandRun list }

    /// The explicit not-a-claim marker (FR-008). Closed, never derived from clock/env/input.
    type ComplianceMarker =
        | CompatibleShapeNotFormalCompliance

    /// The whole summary — the projection of the F25 AuditSnapshot + pack subjects. Identity is
    /// Provenance.canonicalId (F033 verbatim): changes only when a reproducible input changes; duration never
    /// affects it (SC-005).
    type AttestationSummary =
        { Subjects: AttestationSubject list
          Builder: BuilderIdentity
          Materials: AttestationMaterials
          Invocation: AttestationInvocation
          Identity: string
          Compliance: ComplianceMarker }
