// Curated public signature for the verify.json verdict core (076 Phase C seam, extracted from VerifyJson.fs).
//
// This .fsi is the SOLE declaration of the module's public surface (Principle II); Core.fs carries NO
// `private`/`internal`/`public` modifiers on top-level bindings — visibility is presence/absence here. Owns
// the verdict body the four entry points all begin with. Every token helper and sub-object writer
// (`verdictToken`, `dispositionToken` — the local hyphenated `not-executed` divergence kept here, NOT
// re-unified with `JsonTokens`, FR-009 — `writeCauseValue`/`writeEnforcement`/`writeCache`/`writeExecution`/
// `writeItem`/`writeSection`/`gateItemIds`/`writeCurrency`) is hidden by its ABSENCE here, exactly as the old
// single VerifyJson.fs kept them off its .fsi. Compiled FIRST (before the sibling seams + VerifyJson.fs).

namespace FS.GG.Governance.VerifyJson

open System.Text.Json                         // Utf8JsonWriter
open FS.GG.Governance.Ship.Model              // ShipDecision
open FS.GG.Governance.Gates.Model             // GateId
open FS.GG.Governance.GateRun.Model           // GateOutcome
open FS.GG.Governance.CacheEligibility.Model  // CacheEligibilityReport

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Core =

    /// The declared schema-version token (`"fsgg.verify/v1"`) stamped by `writeCore`; the composing
    /// `VerifyJson` entry re-exports it as its public `schemaVersion`. A fixed, deterministic constant.
    val schemaVersion: string

    /// The shared verdict body the four entry points all begin with — `schemaVersion`, `verdict`,
    /// `exitCodeBasis`, `blockers`/`warnings`/`passing`, `currency` — appended to the caller-owned writer in
    /// the fixed wire order. The optional trailing sections (`surfaceChecks`/`releaseReadiness`/
    /// `generatedViews`) are appended by the entry AFTER this call, so the byte stream is unchanged.
    val writeCore:
        w: Utf8JsonWriter ->
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        execution: (GateId * GateOutcome) list ->
            unit
