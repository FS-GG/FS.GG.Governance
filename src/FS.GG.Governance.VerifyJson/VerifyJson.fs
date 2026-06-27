namespace FS.GG.Governance.VerifyJson

open System.Text.Json
open FS.GG.Governance.Gates.Model               // GateId
open FS.GG.Governance.Enforcement.Enforcement   // EnforcementDecision
open FS.GG.Governance.Ship.Model                // ShipDecision
open FS.GG.Governance.CacheEligibility.Model     // CacheEligibilityReport
open FS.GG.Governance.GateRun.Model             // GateOutcome
open FS.GG.Governance.ReleaseReport.Model        // F26: VerifyReleasePreview

module SC = FS.GG.Governance.SurfaceChecks.Model // F24: the additive surfaceChecks section
module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement // F070: the stale-view finding vocabulary

// The F056 verify.json projection. Renders the F024 `ShipDecision` (rolled at `RunMode.Verify`) + the F041
// `CacheEligibilityReport` + the F052 per-gate execution outcomes into the deterministic, versioned
// `verify.json` WHOLE-CHANGE pre-PR verification document text via a hand-driven `System.Text.Json`
// `Utf8JsonWriter` walk. PURE and TOTAL (FR-008): no I/O, no git, no clock, never throws. Emit-only.
//
// 076 Phase C: split along its feature seams. The verdict core (`verdictToken`…`writeCore`) moved to
// `FS.GG.Governance.VerifyJson.Core`; the surface-checks / release-readiness / generated-views writers moved
// to their own seam modules (`SurfaceChecks`/`ReleaseReadiness`/`GeneratedViews`). This module is now the
// THIN composing entry: the four public entry points + `schemaVersion` keep IDENTICAL signatures and emit the
// SAME byte stream (each appends the seam writers in the same wire order). VerifyJson.fsi is byte-identical.

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VerifyJson =

    /// The declared schema-version token, owned by `Core` and re-exported here for the public surface and the
    /// host's `wrote … (<schemaVersion>)` line. A fixed, deterministic constant (`"fsgg.verify/v1"`).
    let schemaVersion = Core.schemaVersion

    // ── F24: the additive `surfaceChecks` writer extracted to the `SurfaceChecks` seam module (076 Phase C);
    //    the composing entry below calls `SurfaceChecks.writeSurfaceFinding` under the `findings` guard. ──

    // ── F26: the additive `releaseReadiness` advisory preview block extracted to the `ReleaseReadiness` seam
    //    module (076 Phase C); the composing entry below calls `ReleaseReadiness.writeReleaseReadiness` under
    //    the `Some/None` preview guard. ──

    // ── F070: the additive `generatedViews` writers extracted to the `GeneratedViews` seam module (076 Phase
    //    C); the composing entry below calls `GeneratedViews.writeGeneratedViews` (the identity on []). ──

    // ── the thin composition (hidden) — one top-level object: the Core verdict body, then the three optional
    //    additive sections in the FIXED wire order (surfaceChecks → releaseReadiness → generatedViews). Each
    //    section is the identity on an empty/None input, so every entry below emits the byte stream its
    //    pre-split body produced. ──

    let writeDocument
        (w: Utf8JsonWriter)
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (findings: SC.SurfaceFinding list)
        (preview: VerifyReleasePreview option)
        (generatedViews: (CE.CurrencyFinding * EnforcementDecision) list)
        =
        w.WriteStartObject()
        Core.writeCore w decision cache execution

        // F24: the additive product-surface findings. Written ONLY when non-empty; absent ⇒ byte-identical
        // to the pre-F24 projection (D8). Emitted in the Composition.run order the caller already fixed.
        match findings with
        | [] -> ()
        | _ ->
            w.WritePropertyName "surfaceChecks"
            w.WriteStartArray()
            for f in findings do
                SurfaceChecks.writeSurfaceFinding w f
            w.WriteEndArray()

        match preview with
        | None -> ()
        | Some p -> ReleaseReadiness.writeReleaseReadiness w p

        GeneratedViews.writeGeneratedViews w generatedViews
        w.WriteEndObject()

    // ── the public entry points (signatures byte-identical to VerifyJson.fsi) ──

    let ofVerifyDecisionWithSurfaceChecks
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (findings: SC.SurfaceFinding list)
        : string =
        JsonText.writeToString (fun w -> writeDocument w decision cache execution findings None [])

    /// The F056 contract, unchanged: no surface findings ⇒ NO `surfaceChecks` field, so this is
    /// byte-identical to the pre-F24 projection (existing goldens untouched).
    let ofVerifyDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        : string =
        ofVerifyDecisionWithSurfaceChecks decision cache execution []

    /// F26 (additive, non-breaking): the same projection plus an advisory `releaseReadiness` block carrying
    /// the F26 VerifyReleasePreview. The block is emitted as the document's LAST top-level field ONLY when
    /// `preview` is `Some`; a `None` preview writes NO block, so the output is BYTE-IDENTICAL to
    /// `ofVerifyDecisionWithSurfaceChecks decision cache execution findings`. PURE and TOTAL.
    let ofVerifyDecisionWithPreview
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (findings: SC.SurfaceFinding list)
        (preview: VerifyReleasePreview option)
        : string =
        JsonText.writeToString (fun w -> writeDocument w decision cache execution findings preview [])

    /// F070: the additive verify.json overload carrying the stale-generated-view currency findings + their
    /// F023 `EnforcementDecision`s. Identical to `ofVerifyDecisionWithPreview` plus the trailing
    /// `generatedViews` array (omitted when empty ⇒ byte-identical, FR-004). Existing entry points untouched.
    let ofVerifyDecisionWithGeneratedViews
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (execution: (GateId * GateOutcome) list)
        (findings: SC.SurfaceFinding list)
        (preview: VerifyReleasePreview option)
        (generatedViews: (CE.CurrencyFinding * EnforcementDecision) list)
        : string =
        JsonText.writeToString (fun w -> writeDocument w decision cache execution findings preview generatedViews)
