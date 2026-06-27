// Curated public signature for the verify.json release-readiness writer (076 Phase C seam, extracted from
// VerifyJson.fs). This .fsi is the SOLE declaration of the module's public surface (Principle II);
// ReleaseReadiness.fs carries NO top-level access modifiers — the `rr*` token helpers and the
// `writePackProject`/`writePackageEvidence`/`writeVersionPolicy`/`writeAttestationRef` sub-writers are hidden
// by their ABSENCE here (minimal additive surface, FR-004). One public entry the composing module calls under
// its `match preview with Some p -> … | None -> ()` guard. Compiled after `Core`, before `VerifyJson.fs`.

namespace FS.GG.Governance.VerifyJson

open System.Text.Json                       // Utf8JsonWriter
open FS.GG.Governance.ReleaseReport.Model   // VerifyReleasePreview

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReleaseReadiness =

    /// Write the F26 advisory `releaseReadiness` block — `advisory` (always true), the previewed `verdict`,
    /// and the `packageEvidence`/`versionPolicy`/`attestation` shape mirroring release.json v2. verify's exit
    /// code is decided WITHOUT it (FR-005). Verbatim from `VerifyJson.writeReleaseReadiness`.
    val writeReleaseReadiness: w: Utf8JsonWriter -> preview: VerifyReleasePreview -> unit
