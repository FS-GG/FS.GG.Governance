// The verdict-bridge composer (F081, FR-008/012). Visibility lives in Consumer.fsi (Principle II).
// PURE and TOTAL. Parses + maps + readiness-projects every located document into typed `Gate` +
// pre-selected `SelectedGate` entries the three verdict hosts fold into `RouteResult.SelectedGates`
// before `Ship.rollup` (research D3). A bad document ⇒ a blocking integrity gate + diagnostic and NO
// mapped gate (FR-011). Empty input ⇒ empty result (SC-003).

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Kernel
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Consumer =

    type ConsumeResult =
        { Gates: Gate list
          Selected: SelectedGate list
          Diagnostics: Diagnostic list }

    // The `<id>` segment of `readiness/<id>/governance-handoff.json` (falls back to the whole source).
    // Local copy — the `Readiness` equivalent is hidden behind its .fsi (only `toGate` is public).
    let private idOf (source: string) : string =
        let segs = source.Replace("\\", "/").Split('/') |> Array.toList

        let rec after =
            function
            | "readiness" :: id :: _ -> Some id
            | _ :: rest -> after rest
            | [] -> None

        match after segs with
        | Some id when id <> "" -> id
        | _ -> source

    // A handoff `Gate` carrying the declared maturity verbatim — no command, default timeout, advisory
    // owner. Mirrors `Readiness.buildGate` (kept local — the helper is hidden behind each .fsi).
    let private buildGate (checkId: string) (maturity: Maturity) (description: string) : Gate =
        { Id = GateId(sprintf "sdd-handoff:%s" checkId)
          Domain = DomainId "sdd-handoff"
          Description = description
          Prerequisites = []
          Cost = Cheap
          Timeout = Gates.defaultTimeout
          Owner = Owner "sdd-handoff"
          Maturity = maturity
          ProductCheck = false
          FreshnessKey =
            { Check = CheckId checkId
              Domain = DomainId "sdd-handoff"
              Cost = Cheap
              Environment = LocalOrCi
              Command = None } }

    // The `SelectingPath` provenance for a document's gates: the declared `governedReferences` paths
    // (each path won "on itself" — synthetic glob), or empty when absent (correctness independent of it
    // — FR-010). Handoff gates are PRE-SELECTED regardless (relevance = the declared work item).
    let private selectingPaths (handoff: Handoff) : SelectingPath list =
        handoff.GovernedReferences
        |> List.collect (fun r -> r.Paths)
        |> List.map (fun p -> { Path = p; MatchedGlob = p })
        |> List.distinct

    // An effective evidence state of `Failed` or `AutoSynthetic` makes the evidence gate blocking-capable.
    let private evidenceBlocking (states: Map<string, EvidenceState>) : bool =
        states
        |> Map.exists (fun _ s ->
            match s with
            | EvidenceState.Failed
            | EvidenceState.AutoSynthetic -> true
            | _ -> false)

    // Process ONE document → its gates (each paired with the doc's selecting paths) + diagnostics.
    let private consumeOne (read: Reader.HandoffRead) : (Gate * SelectingPath list) list * Diagnostic list =
        let id = idOf read.Source

        match Reader.parse read with
        | Error diag ->
            // A bad document ⇒ a blocking integrity gate + the diagnostic, NO mapped gate (FR-011).
            let gate =
                buildGate
                    (sprintf "integrity:%s" id)
                    BlockOnShip
                    (sprintf "SDD handoff '%s' could not be consumed: %s" id diag.Message)

            [ gate, [] ], [ diag ]

        | Ok handoff ->
            let paths = selectingPaths handoff
            let mappedResult, staleDiags = Mapping.mapEvidence read.Source handoff.Evidence

            match mappedResult with
            | Error diag ->
                let gate =
                    buildGate
                        (sprintf "integrity:%s" id)
                        BlockOnShip
                        (sprintf "SDD handoff '%s' evidence could not be mapped: %s" id diag.Message)

                [ gate, paths ], (diag :: staleDiags)

            | Ok(nodes, deps) ->
                match Mapping.effectiveStates nodes deps with
                | Error buildErr ->
                    // Defence in depth: the kernel refused the graph (declared AutoSynthetic / cycle /
                    // dangling). Surface as a blocking integrity gate with the document's source.
                    let diag = { buildErr with Source = read.Source }

                    let gate =
                        buildGate
                            (sprintf "integrity:%s" id)
                            BlockOnShip
                            (sprintf "SDD handoff '%s' evidence graph is invalid: %s" id buildErr.Message)

                    [ gate, paths ], (diag :: staleDiags)

                | Ok effective ->
                    let blocking = evidenceBlocking effective
                    let maturity = if blocking then BlockOnShip else Warn

                    let states =
                        effective
                        |> Map.toList
                        |> List.map (fun (k, v) -> sprintf "%s=%s" k (Json.ofEvidenceState v))
                        |> String.concat ", "

                    let evidenceGate =
                        buildGate
                            (sprintf "evidence:%s" id)
                            maturity
                            (sprintf
                                "SDD declared evidence for '%s' (effective: %s)%s"
                                id
                                states
                                (if blocking then " — blocking (failed/auto-synthetic effective state)" else " — advisory (all satisfied)"))

                    let readinessGates =
                        match handoff.Readiness with
                        | Some block -> [ Readiness.toGate read.Source block ]
                        | None -> []

                    let gates = evidenceGate :: readinessGates |> List.map (fun g -> g, paths)
                    gates, staleDiags

    let consume (reads: Reader.HandoffRead list) : ConsumeResult =
        // Documents loaded in stable `<id>` order; each consumed independently; gates unioned and sorted
        // by `GateId` (the gate pipeline's existing stable key — FR-012, research D7).
        let ordered = reads |> List.sortBy (fun r -> idOf r.Source)
        let perDoc = ordered |> List.map consumeOne
        let pairs = perDoc |> List.collect fst |> List.sortBy (fun (g, _) -> gateIdValue g.Id)
        let diagnostics = perDoc |> List.collect snd

        { Gates = pairs |> List.map fst
          Selected = pairs |> List.map (fun (g, sp) -> { Gate = g; SelectingPaths = sp })
          Diagnostics = diagnostics }

    // F082: the declared `governedReferences` paths of every CONSUMABLE document, projected as
    // first-class routing candidates. A document `Reader.parse` refuses contributes nothing —
    // consistent with `consume`'s bad-document rule; its blocking integrity gate is produced by
    // `consume`, not here (FR-008). Pure + total. Deterministic ordinal order; route re-sorts anyway.
    let candidatePaths (reads: Reader.HandoffRead list) : GovernedPath list =
        reads
        |> List.choose (fun r ->
            match Reader.parse r with
            | Ok handoff -> Some handoff
            | Error _ -> None) // bad document ⇒ no candidates (FR-008)
        |> List.collect (fun h -> h.GovernedReferences |> List.collect (fun g -> g.Paths))
        |> List.distinct // dedup across work items / docs (FR-006)
        |> List.sortBy (fun (GovernedPath p) -> p)
