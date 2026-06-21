// Curated public signature contract for the broad-route cost-explanation operations (F031).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching RouteExplain.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any RouteExplain.fs
// body exists (Principle I). `explain` is PURE and TOTAL (FR-003): defined for every input, never throwing,
// reading no clock, filesystem, git, environment, or network, and identical for identical input regardless
// of evaluation time, machine, process, or input order. This row renders NO JSON/artifact, computes NO
// numeric cost weight or budget, performs NO severity/enforcement/freshness/ship verdict, runs NO gate, and
// adds NO CLI: its sole output is the `RouteExplanation` value. It re-routes nothing and re-selects nothing
// — F019's `SelectedGate` is consumed verbatim (research D2).

namespace FS.GG.Governance.RouteExplain

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RouteExplain =

    /// The high-cost cutoff: a selected gate is "high-cost" iff its declared `Cost` is at or above this
    /// (`High` or `Exhaustive`). Fixed at `High` for the MVP; exposed for inspection/tests (research D3). A
    /// later row MAY parameterize it once budgets are declared.
    val highCostThreshold: Cost

    /// Explain a route's high-cost gates against the catalog. PURE and TOTAL (FR-003).
    ///
    /// Inputs (both already typed upstream — nothing recomputed here):
    ///   • `route`    — the F019 `RouteResult` being explained (its `SelectedGates` carry each gate's
    ///                  declared `Cost`/`Domain`/`Id` and the route trace verbatim).
    ///   • `registry` — the F018 `GateRegistry`, the pool of candidate cheaper-local alternatives.
    ///
    /// Result (FR-001/FR-002/FR-004/FR-008, research D2/D5): a `RouteExplanation` whose `Findings` contains
    /// exactly one `HighCostFinding` per `route.SelectedGates` entry whose `Gate.Cost >= highCostThreshold`
    /// (none below), each embedding that F019 `SelectedGate` verbatim and carrying its resolved
    /// `Alternative`. `Findings` is sorted by `Selected.Gate.Id` ordinal, so re-ordering the input selected
    /// gates, the registry gates, or a gate's selecting paths never changes the result. An empty route, or a
    /// route with no high-cost gate, yields `{ Findings = [] }` — a valid success, never an error.
    ///
    /// Alternative resolution per finding (FR-006/FR-007, research D4/D6): among `registry.Gates`, a
    /// candidate `g` has `g.Domain = h.Domain` AND `g.Cost < h.Cost` (strict) AND
    /// `g.FreshnessKey.Environment ∈ { Local; LocalOrCi }`. The chosen alternative is the candidate with the
    /// lowest `Cost`, ties broken by `GateId` ordinal — `CheaperLocalAlternative g`; if none qualifies,
    /// `NoCheaperLocalAlternative`. Always present (the no-hide rule).
    ///
    /// Pure (FR-003/FR-008, SC-006): no I/O, git, clock, severity, enforcement, freshness verdict, ship
    /// verdict, JSON, or CLI. Reads only the supplied values; identical for identical input.
    val explain: route: RouteResult -> registry: GateRegistry -> RouteExplanation
