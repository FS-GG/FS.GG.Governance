// The verify.json generated-views seam (076 Phase C). JSON-4: the writer BODY now lives ONCE in the shared
// `GeneratedViewsJson` leaf — it was byte-identical to AuditJson's copy. This seam is retained as a thin,
// byte-preserving delegator so the 076 Phase-C module surface (asserted by SeamModuleScopeGuardTests) and
// VerifyJson's call site (`GeneratedViews.writeGeneratedViews`) are unchanged. Visibility lives in
// GeneratedViews.fsi (Principle II) — no top-level access modifiers here.

namespace FS.GG.Governance.VerifyJson

open System.Text.Json
open FS.GG.Governance.Enforcement.Enforcement  // EnforcementDecision

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement // the stale-view finding vocabulary (signature type)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeneratedViews =

    // JSON-4: delegate verbatim to the shared writer — it omits the array when empty, sorts by viewId, and
    // emits the identical bytes this seam and AuditJson used to hand-copy.
    let writeGeneratedViews (w: Utf8JsonWriter) (views: (CE.CurrencyFinding * EnforcementDecision) list) =
        FS.GG.Governance.GeneratedViewsJson.GeneratedViewsJson.writeGeneratedViews w views
