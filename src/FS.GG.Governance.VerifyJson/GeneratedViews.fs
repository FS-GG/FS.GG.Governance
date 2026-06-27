// The verify.json generated-views writer (076 Phase C seam). Visibility lives in GeneratedViews.fsi
// (Principle II) — no top-level access modifiers here; `writeGeneratedView` is hidden by its absence from the
// .fsi. PURE: walks a caller-owned `Utf8JsonWriter`. Lifted verbatim from VerifyJson.fs; the composing
// `VerifyJson` entry calls `GeneratedViews.writeGeneratedViews` under the same non-empty guard.

namespace FS.GG.Governance.VerifyJson

open System.Text.Json
open FS.GG.Governance.FreshnessKey.Model       // categoryToken
open FS.GG.Governance.Enforcement.Enforcement  // EnforcementDecision
open FS.GG.Governance.JsonTokens               // 073: severityToken
open FS.GG.Governance.RefreshJson              // F070: RefreshModel.viewKindToken for the generatedViews kind

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement // F070: the stale-view finding vocabulary

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeneratedViews =

    // F070: the additive `generatedViews` array (stale-generated-view findings folded through the existing
    // F023 truth table). Each entry carries the view id/kind, the stale cause, the drifted categories (or the
    // undeterminable detail), and BOTH base and effective severity + the lever-naming reason (no-hide, FR-006).
    // Sorted by viewId; written ONLY when non-empty ⇒ absent ⇒ byte-identical to the pre-F070 projection (FR-004).
    let writeGeneratedView (w: Utf8JsonWriter) (finding: CE.CurrencyFinding) (decision: EnforcementDecision) =
        w.WriteStartObject()
        w.WriteString("viewId", finding.ViewId)
        w.WriteString("kind", RefreshModel.viewKindToken finding.Kind)
        w.WriteString("cause", CE.staleCauseToken finding.Cause)

        match finding.Cause with
        | CE.SourceDrift drifted ->
            w.WritePropertyName "drifted"
            w.WriteStartArray()

            for category in drifted do
                w.WriteStringValue(categoryToken category)

            w.WriteEndArray()
        | CE.Undeterminable reason -> w.WriteString("detail", reason)

        w.WriteString("baseSeverity", JsonTokens.severityToken finding.BaseSeverity)
        w.WriteString("effectiveSeverity", JsonTokens.severityToken decision.EffectiveSeverity)
        w.WriteString("reason", decision.Reason)
        w.WriteEndObject()

    let writeGeneratedViews (w: Utf8JsonWriter) (views: (CE.CurrencyFinding * EnforcementDecision) list) =
        match views with
        | [] -> ()
        | _ ->
            w.WritePropertyName "generatedViews"
            w.WriteStartArray()

            for finding, decision in views |> List.sortBy (fun (f, _) -> f.ViewId) do
                writeGeneratedView w finding decision

            w.WriteEndArray()
