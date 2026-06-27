// Curated public signature for the verify.json generated-views writer (076 Phase C seam, extracted from
// VerifyJson.fs). This .fsi is the SOLE declaration of the module's public surface (Principle II);
// GeneratedViews.fs carries NO top-level access modifiers — the per-entry `writeGeneratedView` sub-writer is
// hidden by its ABSENCE here. One public entry the composing module calls under its non-empty guard. Compiled
// after `Core`, before `VerifyJson.fs`.

namespace FS.GG.Governance.VerifyJson

open System.Text.Json                          // Utf8JsonWriter
open FS.GG.Governance.Enforcement.Enforcement  // EnforcementDecision

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeneratedViews =

    /// Write the F070 additive `generatedViews` array (stale-generated-view findings folded through the
    /// existing F023 truth table), sorted by viewId. Written ONLY when non-empty ⇒ absent ⇒ byte-identical to
    /// the pre-F070 projection (FR-004). Verbatim from `VerifyJson.writeGeneratedViews`.
    val writeGeneratedViews:
        w: Utf8JsonWriter ->
        views: (FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding * EnforcementDecision) list ->
            unit
