// Curated public signature contract for the broad-route cost-explanation types (F031).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, YAML-free values the `RouteExplain.explain`
// projection returns. They REUSE the F019/F018/F014 vocabulary verbatim — opened from
// `FS.GG.Governance.Route.Model` (`SelectedGate`/`SelectingPath`/`RouteResult`),
// `FS.GG.Governance.Gates.Model` (`Gate`/`GateRegistry`), and `FS.GG.Governance.Config.Model`
// (`Cost`/`EnvironmentClass`/`DomainId`/`GateId`) — never redefined (FR-009). The only new shape is the
// explanation: a closed `AlternativeOutcome` DU, the `HighCostFinding` record that embeds the F019
// `SelectedGate` whole, and the `RouteExplanation` record that wraps the deterministically-ordered
// findings.

namespace FS.GG.Governance.RouteExplain

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The cheaper-local-alternative result for ONE high-cost finding — the no-hide outcome (FR-006),
    /// ALWAYS present on a finding, never `option`/null and never an "absent/unknown" third state.
    /// `CheaperLocalAlternative g`: `g` is a registry `Gate` that is same-domain, strictly cheaper, and
    /// locally runnable relative to the finding's gate (research D4/D6); `g` is carried VERBATIM (its full
    /// F018 metadata — a consumer reads `g.Id`/`g.Cost`/`g.FreshnessKey.Environment` as needed), not a
    /// reduced projection. `NoCheaperLocalAlternative`: the explicit "none" — emitted when no registry gate
    /// qualifies; never omitted, never null.
    type AlternativeOutcome =
        | CheaperLocalAlternative of Gate
        | NoCheaperLocalAlternative

    /// One high-cost gate on the route, explained. `Selected` is the F019 `SelectedGate` embedded VERBATIM
    /// (research D2) — it carries the design's six fields (the selected gate id, its declared cost, the
    /// affected capability domain, and the full changed-path/matched-rule trace via `SelectingPaths`)
    /// re-deriving none of them. `Alternative` is the resolved cheaper-local outcome (research D4). No raw
    /// YAML, host path, timestamp, severity, enforcement, freshness verdict, or ship verdict — only the
    /// embedded F019/F018 values (FR-010).
    type HighCostFinding =
        { Selected: SelectedGate
          Alternative: AlternativeOutcome }

    /// The deterministic explanation of a route's high-cost gates (FR-002): one `HighCostFinding` per
    /// selected gate whose declared `Cost >= High`. `Findings` is sorted by `Selected.Gate.Id` ordinal so
    /// it is independent of input order (FR-008). An EMPTY `Findings` is a valid, successful "no broad route
    /// to explain" (FR-011) — never an error, never a "select everything" fallback.
    type RouteExplanation = { Findings: HighCostFinding list }
