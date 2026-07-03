// Broad-route cost-explanation operations for the explanation core (F031). The public surface is fixed by
// RouteExplain.fsi (Principle II); no top-level binding here carries an access modifier. `explain` is pure,
// total, and deterministic (FR-003): no clock, filesystem, git, environment, or network; identical inputs
// always yield the identical explanation. The decision tables are fixed by
// contracts/explanation-semantics.md. F019's `SelectedGate` is consumed verbatim — nothing is re-routed or
// re-selected (research D2). The embedded trace is canonicalized to F019's OWN ordinal order
// (distinct + path-ordinal sort) so the explanation is invariant under reordered/duplicated selecting paths
// (FR-008); this is a no-op on a real F019 route, whose selecting paths are already distinct and sorted, so
// the carried trace stays byte-identical to the route's (FR-005).

namespace FS.GG.Governance.RouteExplain

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RouteExplain =

    let highCostThreshold: Cost = High

    /// Raw string of a `GovernedPath` — for the ordinal selecting-path sort (mirrors F019 `Route.select`).
    let private pathValue (GovernedPath p) = p

    /// Order two `SelectingPath`s by normalized `Path` ordinal, then `MatchedGlob` — F019's own order, so
    /// canonicalizing a real route's trace is a no-op (FR-005/FR-008).
    let private bySelectingPath (a: SelectingPath) (b: SelectingPath) =
        match String.CompareOrdinal(pathValue a.Path, pathValue b.Path) with
        | 0 -> String.CompareOrdinal(pathValue a.MatchedGlob, pathValue b.MatchedGlob)
        | c -> c

    /// A gate "permits local execution" iff its declared environment is `Local` or `LocalOrCi` (D6).
    let private permitsLocal (environment: EnvironmentClass) =
        match environment with
        | Local
        | LocalOrCi -> true
        | Ci
        | Release -> false

    /// Resolve the cheaper-local alternative for a high-cost finding gate `h` against the catalog
    /// (contracts/explanation-semantics.md §2, research D4/D6): a same-domain, strictly-cheaper, locally-
    /// runnable registry gate — the cheapest, ties broken by `GateId` ordinal — else the explicit none.
    /// Strict `<` excludes the gate itself and equal-cost peers; the sort gives cheapest-then-`GateId`, so
    /// reordering or duplicating the registry gates never changes the head (FR-007/FR-008).
    let private resolveAlternative (registry: GateRegistry) (h: Gate) : AlternativeOutcome =
        registry.Gates
        |> List.filter (fun g ->
            g.Domain = h.Domain
            && costRank g.Cost < costRank h.Cost
            && permitsLocal g.FreshnessKey.Environment)
        |> List.sortBy (fun g -> costRank g.Cost, gateIdValue g.Id)
        |> List.tryHead
        |> function
            | Some g -> CheaperLocalAlternative g
            | None -> NoCheaperLocalAlternative

    let explain (route: RouteResult) (registry: GateRegistry) : RouteExplanation =
        let findings =
            route.SelectedGates
            // One finding per selected gate at/above the threshold; none below (FR-004, §1).
            |> List.filter (fun sg -> costRank sg.Gate.Cost >= costRank highCostThreshold)
            // Duplicate selected-gate entries (same gate) collapse — dup-invariance (FR-008).
            |> List.distinctBy (fun sg -> sg.Gate.Id)
            |> List.map (fun sg ->
                let canonicalTrace =
                    sg.SelectingPaths |> List.distinct |> List.sortWith bySelectingPath

                { Selected = { sg with SelectingPaths = canonicalTrace }
                  Alternative = resolveAlternative registry sg.Gate })
            // Findings sorted by `GateId` ordinal — order-independent of the inputs (FR-008, D5).
            |> List.sortWith (fun a b ->
                String.CompareOrdinal(gateIdValue a.Selected.Gate.Id, gateIdValue b.Selected.Gate.Id))

        { Findings = findings }
