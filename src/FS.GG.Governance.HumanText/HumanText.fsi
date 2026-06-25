// Curated public signature contract for the plain-text human projection (F27, §2).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching HumanText.fs carries NO `private`/`internal`/`public` modifiers on top-level
// bindings — visibility is presence/absence here.
//
// Each `of*` is PURE, TOTAL, deterministic, and ANSI-FREE: it renders the matching `ReportView`
// (so the plain text and the rich/TUI surfaces are one structure projected twice) of the SAME
// immutable report object that produces the command's byte-identical JSON (FR-001). The text is
// human-readable and NON-CONTRACTUAL — held only to smoke-snapshot stability (FR-003, FR-011);
// JSON stays the only contract. No ANSI/CSI escape ever appears; no absolute path, wall-clock,
// username, or environment value leaks; a blocked verdict renders as blocked, never softened
// (FR-002). Each signature mirrors the input tuple of the matching `*Json.of*`.

namespace FS.GG.Governance.HumanText

open FS.GG.Governance.Route.Model              // RouteResult
open FS.GG.Governance.RouteExplain.Model        // RouteExplanation
open FS.GG.Governance.Ship.Model                // ShipDecision
open FS.GG.Governance.ReleaseReport.Model         // ReleaseReport
open FS.GG.Governance.CacheEligibility.Model      // CacheEligibilityReport
open FS.GG.Governance.Gates.Model                 // GateId
open FS.GG.Governance.GateRun.Model               // GateOutcome

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HumanText =

    /// Render an already-projected `ReportView` to ANSI-free plain text. Exposed so callers (and the
    /// HumanRender degrade path) can render a view directly; every `of*` below is `viewOf* >> render`.
    val render: view: ReportView.ReportView -> string

    /// `fsgg route` — mirrors `RouteJson.ofRouteResult`.
    val ofRouteResult:
        result: RouteResult ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
            string

    /// `fsgg explain` — over `RouteExplain.RouteExplanation`.
    val ofRouteExplanation: explanation: RouteExplanation -> string

    /// `fsgg ship` — over `Ship.ShipDecision`.
    val ofShipDecision:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
            string

    /// `fsgg verify` — mirrors `VerifyJson.ofVerifyDecision` (same `ShipDecision`).
    val ofVerifyDecision:
        decision: ShipDecision ->
        cache: CacheEligibilityReport option ->
        outcomes: (GateId * GateOutcome) list ->
            string

    /// `fsgg release` — over `ReleaseReport.ReleaseReport`.
    val ofReleaseReport: report: ReleaseReport -> string

    /// `fsgg evidence` — over `CacheEligibility.CacheEligibilityReport`.
    val ofCacheEligibilityReport: report: CacheEligibilityReport -> string
