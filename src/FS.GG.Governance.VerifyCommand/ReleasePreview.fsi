// Curated public signature for the advisory release-readiness preview fold (076 Phase C seam, extracted
// from Loop.fs). This .fsi is the SOLE declaration of the module's public surface (Principle II);
// ReleasePreview.fs carries NO top-level access modifiers. PURE; never participates in the verify verdict or
// exit code. `previewFrom` lifts verbatim; `previewOf` (which took the host `Model`) is DECOMPOSED here to
// `previewOf'` taking the gated declaration + sensed facts directly (the Phase B `baseHeadOf` precedent), so
// the module stays host-`Model`-free — `Loop` keeps a one-line wrapper projecting `model.ReleaseDecl`/
// `model.ReleaseSensed` into the call. Compiled BEFORE `Loop.fs`.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.ReleaseDeclaration           // Declaration.ReleaseDeclaration
open FS.GG.Governance.ReleaseFactsSensing.Model    // SensedRelease
open FS.GG.Governance.CommandKind.Model            // AuditSnapshot
open FS.GG.Governance.ReleaseReport.Model          // VerifyReleasePreview

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReleasePreview =

    /// Assemble the advisory release-readiness preview from the loaded declaration + sensed F54 facts + the
    /// run's audit snapshot, with an EMPTY PackEvidenceSet — verify does NOT pack (FR-007). Verbatim from
    /// `Loop.previewFrom`.
    val previewFrom:
        decl: Declaration.ReleaseDeclaration -> sensed: SensedRelease -> snapshot: AuditSnapshot -> VerifyReleasePreview

    /// Decomposed off `Model`: takes the gated declaration + sensed facts directly (`None` ⇒ `None` ⇒ no
    /// preview ⇒ byte-identical verify.json, no `releaseReadiness` block). `Loop` wraps:
    /// `previewOf' model.ReleaseDecl model.ReleaseSensed snapshot`.
    val previewOf':
        decl: Declaration.ReleaseDeclaration option ->
        sensed: SensedRelease option ->
        snapshot: AuditSnapshot ->
            VerifyReleasePreview option
