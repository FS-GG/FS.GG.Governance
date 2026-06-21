# Contract: `FS.GG.Governance.RouteExplain` public API (F031)

The sole public surface of this feature, declared in two `.fsi` files. Tier 1: this contract is pinned by the
`surface/FS.GG.Governance.RouteExplain.surface.txt` baseline and the reflective `SurfaceDrift` test.

## Module `FS.GG.Governance.RouteExplain.Model`

The types in [data-model.md](../data-model.md): `AlternativeOutcome`, `HighCostFinding`, `RouteExplanation`.
No functions. Reuses F019/F018/F014 types verbatim (opened, not redefined).

## Module `FS.GG.Governance.RouteExplain.RouteExplain`

```fsharp
namespace FS.GG.Governance.RouteExplain

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RouteExplain =

    /// The high-cost cutoff: a selected gate is "high-cost" iff its declared `Cost` is at or above this
    /// (`High` or `Exhaustive`). Fixed for the MVP; exposed for inspection/tests (research D3).
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
    /// verdict, JSON, or CLI. Reads only the supplied values; byte-identical for identical input.
    val explain: route: RouteResult -> registry: GateRegistry -> RouteExplanation
```

## Laws (asserted by the semantic tests)

| # | Law | FR / SC | Test |
|---|---|---|---|
| L1 | `explain` produces exactly one finding per selected gate with `Cost >= High`, none below — over every `Cost` tier | FR-004, SC-001 | HighCostFinding |
| L2 | Each finding's `Selected` is the F019 selected gate verbatim (gate identity, domain, cost, every selecting path) | FR-005, SC-002 | HighCostFinding |
| L3 | Every finding carries a present `Alternative` — `CheaperLocalAlternative` or `NoCheaperLocalAlternative` | FR-006, SC-003 | Alternative |
| L4 | A named alternative satisfies same-`Domain` ∧ strictly-cheaper ∧ local; flipping any one condition ⇒ `NoCheaperLocalAlternative` | FR-006, SC-004 | Alternative |
| L5 | With several qualifying candidates, the named one is the cheapest, ties broken by `GateId` (deterministic) | FR-007 | Alternative |
| L6 | `explain` is deterministic and order/dup-invariant in selected gates, registry gates, and selecting paths | FR-008, SC-005 | Determinism |
| L7 | Empty route / no high-cost gate ⇒ `{ Findings = [] }` (total, never an error) | FR-011, SC-001 | EmptyRoute |
| L8 | Pure: identical inputs yield identical explanations across cwd/time/filesystem changes | FR-003, SC-006 | Purity |
| L9 | The public surface equals the committed baseline; the assembly references only Route/Gates/Routing/Findings/Config/BCL/FSharp.Core | Principle II, SC-007 | SurfaceDrift |
</content>
