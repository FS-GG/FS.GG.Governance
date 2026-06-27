// The advisory release-readiness preview fold (076 Phase C seam). Visibility lives in ReleasePreview.fsi
// (Principle II) — no top-level access modifiers here. PURE; never participates in the verify verdict or exit
// code. `previewFrom` lifts verbatim from `Loop.fs`; `previewOf'` is the host-`Model`-free decomposition of
// the old `Loop.previewOf` — `Loop` keeps a one-line wrapper projecting `model.ReleaseDecl`/`model.ReleaseSensed`.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.ReleaseDeclaration           // Declaration.ReleaseDeclaration
open FS.GG.Governance.ReleaseFactsSensing.Model    // SensedRelease
open FS.GG.Governance.CommandKind.Model            // AuditSnapshot
open FS.GG.Governance.ReleaseReport.Model          // VerifyReleasePreview

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReleasePreview =

    // 065 (US3): assemble the advisory release-readiness preview from the loaded declaration + sensed F54
    // facts + the run's audit snapshot, with an EMPTY PackEvidenceSet — verify does NOT pack, so there is no
    // attested subject (FR-007). PURE; never participates in the verify verdict or exit code.
    let previewFrom (decl: Declaration.ReleaseDeclaration) (sensed: SensedRelease) (snapshot: AuditSnapshot) : VerifyReleasePreview =
        let decision = FS.GG.Governance.ReleaseRules.Release.evaluateRelease decl.Rules sensed.Facts

        let emptyPack: FS.GG.Governance.PackEvidence.Model.PackEvidenceSet =
            { Verdicts = []
              Runs = []
              NoPackableProjects = true }

        let attestation = FS.GG.Governance.Attestation.Attestation.summarize snapshot emptyPack
        let report = FS.GG.Governance.ReleaseReport.Report.assemble decision sensed emptyPack attestation
        FS.GG.Governance.ReleaseReport.Report.preview report

    // The advisory preview for the gated declaration + sensed facts (None unless a parseable `.fsgg/release.yml`
    // was sensed ⇒ byte-identical verify.json, no `releaseReadiness` block). Decomposed off `Model` so this
    // module stays host-`Model`-free (Phase B `baseHeadOf` precedent).
    let previewOf'
        (decl: Declaration.ReleaseDeclaration option)
        (sensed: SensedRelease option)
        (snapshot: AuditSnapshot)
        : VerifyReleasePreview option =
        match decl, sensed with
        | Some decl, Some sensed -> Some(previewFrom decl sensed snapshot)
        | _ -> None
