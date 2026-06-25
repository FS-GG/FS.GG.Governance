# Contract: `FS.GG.Governance.Attestation` (pure, P2)

The SLSA/in-toto-**shaped** projection of the F25 `AuditSnapshot` (+ pack subjects). Useful metadata first; it
**never** claims formal compliance (FR-008). Identity is `Provenance.canonicalId` reused verbatim — the
attestation is a projection, not a new identity (research D5). A failed build yields **no** subject (FR-008).

## `Model.fsi` (draft)

```fsharp
namespace FS.GG.Governance.Attestation

open FS.GG.Governance.Config.Model            // EnvironmentClass
open FS.GG.Governance.FreshnessKey.Model       // Revision, RuleHash, GeneratorVersion, ArtifactHash
open FS.GG.Governance.Provenance.Model           // BuilderIdentity
open FS.GG.Governance.CommandKind.Model          // KindedCommandRun

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
```

## `Attestation.fsi` (draft)

```fsharp
namespace FS.GG.Governance.Attestation

open FS.GG.Governance.CommandKind.Model           // AuditSnapshot
open FS.GG.Governance.PackEvidence.Model           // PackEvidenceSet
open FS.GG.Governance.Attestation.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Attestation =

    /// Project the F25 AuditSnapshot + the pack evidence into an AttestationSummary. Subjects come ONLY from
    /// the Packed outcomes (no fabricated subject for a failed build, FR-008); Materials/Invocation from the
    /// snapshot's Provenance/Runs; Identity from Provenance.canonicalId (F033 verbatim). Subjects sorted by
    /// Name. Compliance is ALWAYS CompatibleShapeNotFormalCompliance (never overclaims, FR-008).
    /// PURE, TOTAL: byte-identical for identical inputs; changes only when a reproducible input changes
    /// (SC-005). Never throws.
    val summarize: snapshot: AuditSnapshot -> pack: PackEvidenceSet -> AttestationSummary
```

## Behavioral guarantees (tested)

- Subject / builder / materials / invocation all populated in an in-toto-compatible shape (SC-005 / Story 3.1).
- `summarize` twice on identical inputs ⇒ byte-identical; changes only when a reproducible input (a subject
  digest, a material, an invocation) changes (SC-005 / Story 3.2; no-op-input-change stability).
- `Compliance = CompatibleShapeNotFormalCompliance` always present (SC-005 / Story 3.3 — never overclaims).
- A snapshot whose pack run failed ⇒ `Subjects = []` (no attested subject, FR-008 / failed-build case); the
  failed run still appears in `Invocation.Runs`.
- Empty/absent provenance input is handled at the edge (the release blocks, never a hollow attestation — D6);
  `summarize` itself is total over any well-typed snapshot.
