// Curated public signature for the shared `generatedViews` array writer (JSON-4). This is the SINGLE
// home of the F070 stale-generated-view writer body that AuditJson (audit.json / ship.json) and
// VerifyJson (verify.json) previously hand-copied byte-for-byte. This .fsi is the SOLE declaration of
// the module's public surface (Principle II); GeneratedViewsJson.fs carries NO top-level access
// modifiers — the per-entry `writeGeneratedView` sub-writer is hidden by its ABSENCE here.

namespace FS.GG.Governance.GeneratedViewsJson

open System.Text.Json                          // Utf8JsonWriter
open FS.GG.Governance.Enforcement.Enforcement  // EnforcementDecision

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeneratedViewsJson =

    /// Write the F070 additive `generatedViews` array (stale-generated-view findings folded through the
    /// existing F023 truth table), sorted by viewId. Written ONLY when non-empty ⇒ absent ⇒ byte-identical
    /// to the pre-F070 projection (FR-004). PURE: walks a caller-owned `Utf8JsonWriter`, re-sorting nothing
    /// but the by-viewId order the two projections already applied. Output is byte-identical to the copies
    /// AuditJson and VerifyJson used to carry (guarded by both projections' goldens).
    val writeGeneratedViews:
        w: Utf8JsonWriter ->
        views: (FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding * EnforcementDecision) list ->
            unit
