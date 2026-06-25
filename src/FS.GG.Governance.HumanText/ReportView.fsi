// Curated public signature contract for the navigable report view-model (F27, §3).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching ReportView.fs carries NO `private`/`internal`/`public` modifiers on top-level
// bindings — visibility is presence/absence here.
//
// `ReportView` is the single, presentation-free structure BOTH the plain-text projection
// (HumanText) and the rich tables / TUI (HumanRender) render, so every human surface stays
// parity-true to the SAME immutable report object (FR-001, FR-009). Each `viewOf*` is PURE,
// TOTAL, deterministic, and mirrors the input tuple of the matching `*Json.of*` — it reads the
// already-ordered report object and re-derives, re-sorts, and re-classifies nothing.

namespace FS.GG.Governance.HumanText

open FS.GG.Governance.Route.Model              // RouteResult
open FS.GG.Governance.RouteExplain.Model        // RouteExplanation
open FS.GG.Governance.Ship.Model                // ShipDecision
open FS.GG.Governance.ReleaseReport.Model         // ReleaseReport
open FS.GG.Governance.CacheEligibility.Model      // CacheEligibilityReport
open FS.GG.Governance.Gates.Model                 // GateId
open FS.GG.Governance.GateRun.Model               // GateOutcome

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReportView =

    /// A node in the navigable projection of a report. `Leaf` carries a label plus optional detail;
    /// `Group` nests children under a title.
    type ReportNode =
        | Leaf of label: string * detail: string option
        | Group of title: string * children: ReportNode list

    /// The whole report as one navigable tree: a titled root (the verdict/header) over grouped
    /// sections (selected gates, blockers, warnings, preconditions, evidence/provenance references).
    type ReportView =
        { Title: string
          ExitStatus: string
          Sections: ReportNode list }

    /// Project an F019 route result (with the optional cache-eligibility report + per-gate execution
    /// outcomes the JSON path carries) into the navigable view. Route never blocks (FR-008).
    val viewOfRouteResult:
        result: RouteResult ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
            ReportView

    /// Project a route explanation (high-cost gates + cheaper-local-alternative outcomes).
    val viewOfRouteExplanation: explanation: RouteExplanation -> ReportView

    /// Project a ship decision (verdict, blockers, warnings, passing, exit-code basis).
    val viewOfShipDecision:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
            ReportView

    /// Project a verify decision. Reuses the ship-decision view (both render a `ShipDecision`);
    /// kept as a named entry for command parity.
    val viewOfVerifyDecision:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
            ReportView

    /// Project a release report (decision verdict, preconditions, blockers, warnings, exit basis).
    val viewOfReleaseReport: report: ReleaseReport -> ReportView

    /// Project a cache-eligibility (evidence) report (per-gate reusable / must-recompute verdicts).
    val viewOfCacheEligibilityReport: report: CacheEligibilityReport -> ReportView
