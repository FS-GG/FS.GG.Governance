// Curated public signature contract for the attestation-summary projection (F26, P2).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Attestation.fs carries NO access modifiers. `summarize` is PURE and TOTAL: no file/process/clock/git/env
// access, never throws, byte-identical for identical inputs; it changes only when a reproducible input
// changes. Identity is `Provenance.canonicalId` reused verbatim (F033).

namespace FS.GG.Governance.Attestation

open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.PackEvidence.Model
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
