namespace FS.GG.Governance.HumanText

open System.Text
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseReport.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.HumanText.ReportView

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HumanText =

    // Render one node tree at an indent depth. Local `sb` mutation is the standard string-builder
    // idiom (the *Json precedent); the function is pure and deterministic from the outside.
    let private appendNode (sb: StringBuilder) (depth: int) (node: ReportNode) =
        let rec go depth node =
            let indent = String.replicate depth "  "

            match node with
            | Leaf(label, detail) ->
                match detail with
                | Some d -> sb.Append(indent).Append("- ").Append(label).Append(": ").Append(d).Append('\n') |> ignore
                | None -> sb.Append(indent).Append("- ").Append(label).Append('\n') |> ignore
            | Group(title, children) ->
                sb.Append(indent).Append(title).Append('\n') |> ignore
                children |> List.iter (go (depth + 1))

        go depth node

    let render (view: ReportView) : string =
        let sb = StringBuilder()
        sb.Append(view.Title).Append('\n') |> ignore
        view.Sections |> List.iter (appendNode sb 1)
        sb.Append("exit status: ").Append(view.ExitStatus).Append('\n') |> ignore
        sb.ToString()

    let ofRouteResult
        (result: RouteResult)
        (cache: CacheEligibilityReport option)
        (outcomes: (GateId * GateOutcome) list)
        : string =
        render (viewOfRouteResult result cache outcomes)

    let ofRouteExplanation (explanation: RouteExplanation) : string =
        render (viewOfRouteExplanation explanation)

    let ofShipDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (outcomes: (GateId * GateOutcome) list)
        : string =
        render (viewOfShipDecision decision cache outcomes)

    let ofVerifyDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (outcomes: (GateId * GateOutcome) list)
        : string =
        render (viewOfVerifyDecision decision cache outcomes)

    let ofReleaseReport (report: ReleaseReport) : string =
        render (viewOfReleaseReport report)

    let ofCacheEligibilityReport (report: CacheEligibilityReport) : string =
        render (viewOfCacheEligibilityReport report)
