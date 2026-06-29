namespace FS.GG.Governance.Cli

open System
open System.Text.Json
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Adapters.SpecKit

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CliRender =

    // F081 wiring: gate vocabulary for rendering the consumed handoff gates on the route payload.
    module GatesModel = FS.GG.Governance.Gates.Model
    module ConfigModel = FS.GG.Governance.Config.Model

    let commandName command =
        match command with
        | RouteCommand -> "route"
        | ExplainCommand -> "explain"
        | ContractCommand -> "contract"
        | EvidenceCommand -> "evidence"
        | WatchCommand -> "watch"
        | TuiCommand -> "tui"

    let modeName mode =
        match mode with
        | Sandbox -> "sandbox"
        | Inner -> "inner"
        | Gate -> "gate"

    let formatName format =
        match format with
        | Text -> "text"
        | Json -> "json"

    let ruleIdText (RuleId value) = value

    let evidenceStateText state =
        Json.ofEvidenceState state
        |> fun text -> text.Trim('"')

    let freshnessText freshness =
        match freshness with
        | Fresh -> "fresh"
        | Stale -> "stale"

    let exitCategory decision =
        match decision with
        | Success -> "success"
        | GovernedBlocking -> "governed-blocking"
        | UsageError _ -> "usage-error"
        | InputUnavailable _ -> "input-unavailable"
        | ToolError _ -> "tool-error"

    let quote (value: string) = JsonSerializer.Serialize(value)

    let jsonArray strings =
        strings |> Cli.stableStrings |> List.map quote |> String.concat "," |> sprintf "[%s]"

    let renderParseError error =
        match error with
        | MissingCommand -> "missing command: expected route, explain, contract, or evidence"
        | UnknownCommand value -> "unknown command: " + value
        | UnknownOption value -> "unknown option: " + value
        | MissingOptionValue value -> "missing value for " + value
        | InvalidMode value -> "invalid mode: " + value + " (expected sandbox, inner, or gate)"
        | InvalidFormat value -> "invalid format: " + value + " (expected text or json)"
        | InvalidReviewBudget value -> "invalid review budget: " + value + " (expected non-negative integer)"
        | InvalidRoot value -> "invalid root value: " + value

    let failureText (failure: Failure) =
        match failure with
        | ArtifactUnavailable (artifact, reason) -> artifact.Kind + ":" + artifact.Key + " unavailable: " + reason
        | ReviewDispatchFailed (key, reason) -> "review dispatch failed for " + key + ": " + reason
        | ReviewStoreUnavailable (key, reason) -> "review store unavailable for " + key + ": " + reason

    let budgetLine (budget: BudgetState) =
        sprintf
            "budget: requested=%d cacheHits=%d cacheMisses=%d freshDispatches=%d pending=%d exhausted=%d"
            (List.length budget.Requested)
            (List.length budget.CacheHits)
            (List.length budget.CacheMisses)
            (List.length budget.FreshDispatches)
            (List.length budget.Pending)
            (List.length budget.BudgetExhausted)

    let renderExplanation index explanation =
        sprintf "proof %d: verdict=%A" index (Explanation.verdict explanation)

    let renderEvidenceNode (node: EvidenceNodeReport) =
        let declared = node.Declared |> Option.map evidenceStateText |> Option.defaultValue "none"
        let effective = node.Effective |> Option.map evidenceStateText |> Option.defaultValue "none"
        let freshness = node.Freshness |> Option.map freshnessText |> Option.defaultValue "unknown"
        sprintf "- %s declared=%s effective=%s freshness=%s source=%s" node.Id declared effective freshness node.Source

    // F081 wiring: render the SDD→Governance handoff gates folded onto the route payload.
    let maturityToken (maturity: ConfigModel.Maturity) =
        match maturity with
        | ConfigModel.Observe -> "observe"
        | ConfigModel.Warn -> "warn"
        | ConfigModel.BlockOnPr -> "block-on-pr"
        | ConfigModel.BlockOnShip -> "block-on-ship"
        | ConfigModel.BlockOnRelease -> "block-on-release"

    let maturityBlocks (maturity: ConfigModel.Maturity) =
        match maturity with
        | ConfigModel.BlockOnPr
        | ConfigModel.BlockOnShip
        | ConfigModel.BlockOnRelease -> true
        | ConfigModel.Observe
        | ConfigModel.Warn -> false

    let handoffGateText (gate: GatesModel.Gate) =
        sprintf "  - [%s] %s — %s" (maturityToken gate.Maturity) (GatesModel.gateIdValue gate.Id) gate.Description

    let routeText (route: Route) (handoffGates: GatesModel.Gate list) =
        let baseText = Route.renderRoute route

        if List.isEmpty handoffGates then
            baseText
        else
            [ yield baseText
              yield sprintf "handoff (%d):" (List.length handoffGates)
              yield! handoffGates |> List.map handoffGateText ]
            |> String.concat "\n"

    let renderPayloadText (payload: CommandPayload) =
        match payload with
        | RoutePayload (route, handoffGates) -> routeText route handoffGates
        | ExplainPayload explanations ->
            explanations
            |> List.mapi (fun i explanation -> renderExplanation (i + 1) explanation)
            |> String.concat "\n"
        | ContractPayload contract -> Contract.render contract
        | EvidencePayload report ->
            [ yield "evidence nodes:"
              yield! report.Nodes |> List.map renderEvidenceNode
              yield "dependencies:"
              yield! report.Dependencies |> List.map (fun (a, b) -> "- " + a + " rests on " + b)
              yield "disclosures:"
              yield! report.Disclosures |> List.map (fun d -> "- " + ruleIdText d.Rule + ": " + d.Justification) ]
            |> String.concat "\n"

    let renderText (result: CommandResult) =
        let requestLines =
            match result.Request with
            | Some request ->
                [ "command: " + commandName request.Command
                  "mode: " + modeName request.Mode
                  "root: " + request.Root
                  "scope: " + (if List.isEmpty request.Scope then "." else String.concat "," request.Scope) ]
            | None -> [ "command: <usage>" ]

        let exitLine =
            sprintf "exit: %s (%d)" (exitCategory result.Exit) (Cli.exitCode result.Exit)

        let errors =
            match result.Exit with
            | UsageError errors -> errors |> List.map renderParseError
            | InputUnavailable reason -> [ reason ]
            | ToolError reason -> [ reason ]
            | _ -> []

        [ yield! requestLines
          yield exitLine
          yield budgetLine result.Budget

          if not (List.isEmpty errors) then
              yield "errors:"
              yield! errors |> List.map (fun e -> "- " + e)

          if not (List.isEmpty result.Failures) then
              yield "failures:"
              yield! result.Failures |> List.map (failureText >> fun f -> "- " + f)

          match result.Payload with
          | Some payload ->
              yield "payload:"
              yield renderPayloadText payload
          | None -> () ]
        |> String.concat "\n"

    let contractEntryJson (entry: ContractEntry) =
        let severity =
            match entry.Severity with
            | Advisory -> "advisory"
            | Blocking -> "blocking"

        "{"
        + "\"id\":" + quote (ruleIdText entry.Id)
        + ",\"severity\":" + quote severity
        + ",\"spec\":{\"document\":" + quote entry.Spec.Document + ",\"section\":" + quote entry.Spec.Section + "}"
        + ",\"statement\":" + quote entry.Statement
        + "}"

    let stakesJson stakes =
        match stakes with
        | Routine -> "{\"kind\":\"routine\"}"
        | Fenced name -> "{\"kind\":\"fenced\",\"name\":" + quote name + "}"

    let handoffGateJson (gate: GatesModel.Gate) =
        "{"
        + "\"id\":" + quote (GatesModel.gateIdValue gate.Id)
        + ",\"maturity\":" + quote (maturityToken gate.Maturity)
        + ",\"blocking\":" + (if maturityBlocks gate.Maturity then "true" else "false")
        + ",\"description\":" + quote gate.Description
        + "}"

    let routeJson (route: Route) (handoffGates: GatesModel.Gate list) =
        "{"
        + "\"kind\":\"route\""
        + ",\"stakes\":" + stakesJson route.Stakes
        + ",\"reason\":" + quote route.Reason
        + ",\"blocking\":[" + (route.Blocking |> List.map contractEntryJson |> String.concat ",") + "]"
        + ",\"advisory\":[" + (route.Advisory |> List.map contractEntryJson |> String.concat ",") + "]"
        + ",\"handoff\":[" + (handoffGates |> List.map handoffGateJson |> String.concat ",") + "]"
        + "}"

    let explanationJson (explanations: Explanation list) =
        explanations
        |> List.map Json.ofExplanation
        |> String.concat ","
        |> sprintf "{\"kind\":\"explain\",\"proofs\":[%s]}"

    let contractJson (contract: ContractEntry list) =
        "{\"kind\":\"contract\",\"entries\":" + Json.ofContract contract + "}"

    let optionStateJson (state: EvidenceState option) =
        state |> Option.map Json.ofEvidenceState |> Option.defaultValue "null"

    let optionFreshnessJson (freshness: Freshness option) =
        freshness |> Option.map (freshnessText >> quote) |> Option.defaultValue "null"

    let evidenceNodeJson (node: EvidenceNodeReport) =
        "{"
        + "\"id\":" + quote node.Id
        + ",\"declared\":" + optionStateJson node.Declared
        + ",\"effective\":" + optionStateJson node.Effective
        + ",\"freshness\":" + optionFreshnessJson node.Freshness
        + ",\"source\":" + quote node.Source
        + "}"

    let evidenceJson (report: ProjectEvidenceReport) =
        "{"
        + "\"kind\":\"evidence\""
        + ",\"nodes\":[" + (report.Nodes |> List.sortBy (fun n -> n.Id) |> List.map evidenceNodeJson |> String.concat ",") + "]"
        + ",\"dependencies\":["
        + (report.Dependencies
           |> List.sort
           |> List.map (fun (a, b) -> "{\"dependent\":" + quote a + ",\"dependency\":" + quote b + "}")
           |> String.concat ",")
        + "]"
        + ",\"disclosures\":["
        + (report.Disclosures
           |> List.sort
           |> List.map (fun d -> "{\"rule\":" + quote (ruleIdText d.Rule) + ",\"justification\":" + quote d.Justification + "}")
           |> String.concat ",")
        + "]"
        + "}"

    let payloadJson (payload: CommandPayload) =
        match payload with
        | RoutePayload (route, handoffGates) -> routeJson route handoffGates
        | ExplainPayload explanations -> explanationJson explanations
        | ContractPayload contract -> contractJson contract
        | EvidencePayload report -> evidenceJson report

    let reviewBudgetJson (budget: ReviewBudget) =
        match budget with
        | CacheOnly -> "{\"kind\":\"cache-only\",\"count\":0}"
        | FreshReviews count -> "{\"kind\":\"fresh-reviews\",\"count\":" + string count + "}"

    let requestJson (request: RunRequest) =
        "{"
        + "\"command\":" + quote (commandName request.Command)
        + ",\"mode\":" + quote (modeName request.Mode)
        + ",\"root\":" + quote request.Root
        + ",\"scope\":" + jsonArray request.Scope
        + ",\"format\":" + quote (formatName request.Format)
        + ",\"reviewBudget\":" + reviewBudgetJson request.ReviewBudget
        + ",\"judge\":{\"modelId\":" + quote request.Judge.ModelId + ",\"version\":" + quote request.Judge.Version + "}"
        + "}"

    let exitJson (decision: ExitDecision) =
        "{"
        + "\"category\":" + quote (exitCategory decision)
        + ",\"code\":" + string (Cli.exitCode decision)
        + "}"

    let budgetJson (budget: BudgetState) =
        "{"
        + "\"requested\":" + jsonArray budget.Requested
        + ",\"cacheHits\":" + jsonArray budget.CacheHits
        + ",\"cacheMisses\":" + jsonArray budget.CacheMisses
        + ",\"freshDispatches\":" + jsonArray budget.FreshDispatches
        + ",\"pending\":" + jsonArray budget.Pending
        + ",\"budgetExhausted\":" + jsonArray budget.BudgetExhausted
        + "}"

    let failuresJson (failures: Failure list) =
        failures
        |> List.map failureText
        |> jsonArray

    let errorsJson (decision: ExitDecision) =
        match decision with
        | UsageError errors -> errors |> List.map renderParseError |> jsonArray
        | InputUnavailable reason -> jsonArray [ reason ]
        | ToolError reason -> jsonArray [ reason ]
        | _ -> "[]"

    let renderJson (result: CommandResult) =
        "{"
        + "\"schema\":\"fsgg-governance.cli.v1\""
        + ",\"request\":" + (result.Request |> Option.map requestJson |> Option.defaultValue "null")
        + ",\"exit\":" + exitJson result.Exit
        + ",\"budget\":" + budgetJson result.Budget
        + ",\"failures\":" + failuresJson result.Failures
        + ",\"errors\":" + errorsJson result.Exit
        + ",\"payload\":" + (result.Payload |> Option.map payloadJson |> Option.defaultValue "null")
        + "}"

    let render (result: CommandResult) =
        match result.Request with
        | Some request when request.Format = Json -> renderJson result
        | _ -> renderText result
