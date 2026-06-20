// The route-selection entry point for route gate selection (F019). The public surface is fixed by
// Route.fsi (Principle II); no top-level binding here carries an access modifier. `select` is a PURE,
// TOTAL join of three already-typed upstream outputs — the F018 `GateRegistry`, the F015
// `RouteReport`, and the F017 `FindingReport` — into the deterministic `RouteResult` route trace. It
// re-parses no YAML, re-routes no glob, re-builds no registry, and re-classifies no finding (FR-008,
// FR-012): it only joins, deduplicates, orders, and rolls up. No I/O, git, clock, or randomness; it
// never throws and is byte-identical for identical input (FR-007, SC-005).

namespace FS.GG.Governance.Route

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Route =

    // ── Ordinal sort keys (FR-007): output order depends only on declared ids, never input order ──

    /// The raw string of a `GovernedPath` — for the ordinal selecting-path sort.
    let private pathValue (GovernedPath p) = p

    /// Compare two `SelectedGate`s by their `GateId` ordinal (the documented selected-gate order).
    let private bySelectedGateId (a: SelectedGate) (b: SelectedGate) =
        String.CompareOrdinal(gateIdValue a.Gate.Id, gateIdValue b.Gate.Id)

    /// Compare two `SelectingPath`s by their normalized `Path` ordinal (the documented selecting-path
    /// order), with the matched glob as a stable secondary key.
    let private bySelectingPath (a: SelectingPath) (b: SelectingPath) =
        match String.CompareOrdinal(pathValue a.Path, pathValue b.Path) with
        | 0 -> String.CompareOrdinal(pathValue a.MatchedGlob, pathValue b.MatchedGlob)
        | c -> c

    /// Tally one declared `Cost` tier into the running rollup — the per-tier multiset of FR-006/D5.
    let private addCost (roll: CostRollup) (cost: Cost) =
        match cost with
        | Cheap -> { roll with Cheap = roll.Cheap + 1 }
        | Medium -> { roll with Medium = roll.Medium + 1 }
        | High -> { roll with High = roll.High + 1 }
        | Exhaustive -> { roll with Exhaustive = roll.Exhaustive + 1 }

    let private zeroCost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 }

    let select
        (registry: GateRegistry)
        (report: RouteReport)
        (findings: FindingReport)
        : RouteResult =

        // ── (T011) The domain -> gates index: one O(1) lookup per routed path, keyed on the DECLARED
        //    `Gate.Domain` (FR-010 — the key IS the domain; the `GateId` string is never re-parsed to
        //    recover a domain). Each bucket is in `GateId` ordinal order.
        let indexByDomain : Map<DomainId, Gate list> =
            registry.Gates
            |> List.groupBy (fun g -> g.Domain)
            |> List.map (fun (d, gates) ->
                d, gates |> List.sortWith (fun a b -> String.CompareOrdinal(gateIdValue a.Id, gateIdValue b.Id)))
            |> Map.ofList

        // ── (T012/T014) Per-path union + dedup. Fold the routings (read only `report.Routings`;
        //    `report.Diagnostics` is NOT consumed — research D7). Each `Routed (d, glob, _)` path
        //    selects EVERY gate in its domain's bucket, recording a `{Path; MatchedGlob = glob}`
        //    selecting path against each; the accumulator dedups by `GateId` so a gate reached by
        //    several paths grows its selecting-path list rather than appearing twice (FR-002).
        //    `UnmatchedInRoot`/`OutOfScope` contribute nothing — no "select everything" fallback
        //    (FR-003). The accumulator preserves first-seen gate value; ordering is imposed by the
        //    final sort, not by fold order, so the result is permutation-invariant (FR-007).
        let accumulate (acc: Map<GateId, Gate * SelectingPath list>) (routing: PathRouting) =
            match routing.Result with
            | Routed(d, glob, _) ->
                match Map.tryFind d indexByDomain with
                | None -> acc
                | Some gates ->
                    let selecting = { Path = routing.Path; MatchedGlob = glob }
                    gates
                    |> List.fold
                        (fun (a: Map<GateId, Gate * SelectingPath list>) (g: Gate) ->
                            match Map.tryFind g.Id a with
                            | Some(gate, paths) -> Map.add g.Id (gate, selecting :: paths) a
                            | None -> Map.add g.Id (g, [ selecting ]) a)
                        acc
            | UnmatchedInRoot
            | OutOfScope -> acc

        let selectedMap = report.Routings |> List.fold accumulate Map.empty

        // ── (T012/T016/T024) Build the ordered selected-gate list: each gate's selecting paths
        //    deduplicated and sorted by normalized-path ordinal (FR-007); the gates themselves sorted
        //    by `GateId` ordinal (FR-007, SC-005).
        let selectedGates =
            selectedMap
            |> Map.toList
            |> List.map (fun (_, (gate, paths)) ->
                { Gate = gate
                  SelectingPaths = paths |> List.distinct |> List.sortWith bySelectingPath })
            |> List.sortWith bySelectedGateId

        // ── (T020) Cost rollup: the per-tier multiset over the DISTINCT selected gates (each gate
        //    counted once — the dedup already collapsed multi-path gates). No summed scalar, no
        //    invented tier weights (research D5, FR-006). Empty selection -> the all-zero identity.
        let cost = selectedGates |> List.fold (fun roll sg -> addCost roll sg.Gate.Cost) zeroCost

        // ── (T018) Findings carried through VERBATIM — no re-derive, re-sort, re-classify, or filter
        //    (FR-005). The F017 report is already deterministically ordered.
        { SelectedGates = selectedGates
          Findings = findings
          Cost = cost }
