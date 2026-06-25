// Curated public signature contract for the publication-report assembly (F26, P1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Report.fs carries NO access modifiers; the precondition projector + reason helper live ONLY in the .fs.
// `assemble`/`preview` are PURE and TOTAL: no I/O, never throw, byte-identical for identical inputs; they
// never re-derive the verdict (FR-012).

namespace FS.GG.Governance.ReleaseReport

open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.ReleaseReport.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Report =

    /// Assemble the single-source-of-truth report from the four already-computed inputs. Projects the F54
    /// SensedRelease facts into PreconditionEvidence (one per declared family, ordered by
    /// releaseRuleKindOrdinal), carries the Decision/Package/Attestation VERBATIM, and sets
    /// ReleaseExitCodeBasis = decision.ExitCodeBasis. PURE, TOTAL — never re-derives the verdict (FR-012).
    val assemble:
        decision: ReleaseDecision ->
        sensed: SensedRelease ->
        pack: PackEvidenceSet ->
        attestation: AttestationSummary ->
            ReleaseReport

    /// The advisory verify projection (FR-005): drops nothing, sets Advisory = true. The previewed Verdict is
    /// report.Decision.Verdict — advisory only; verify never blocks on it. PURE, TOTAL.
    val preview: report: ReleaseReport -> VerifyReleasePreview
