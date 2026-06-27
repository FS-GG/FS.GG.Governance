namespace FS.GG.Governance.Cli

open System
open System.Text.Json
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Adapters.SpecKit

type CommandKind =
    | RouteCommand
    | ExplainCommand
    | ContractCommand
    | EvidenceCommand
    | WatchCommand
    | TuiCommand

type OutputFormat =
    | Text
    | Json

type ReviewBudget =
    | CacheOnly
    | FreshReviews of count: int

type RunRequest =
    { Root: string
      Command: CommandKind
      Mode: RunMode
      Format: OutputFormat
      Scope: string list
      Domains: Set<Domain>
      ReviewBudget: ReviewBudget
      ReviewStore: string option
      OutputPath: string option
      Judge: JudgeId
      ExplicitPlain: bool }

type ParseError =
    | MissingCommand
    | UnknownCommand of string
    | UnknownOption of string
    | MissingOptionValue of string
    | InvalidMode of string
    | InvalidFormat of string
    | InvalidReviewBudget of string
    | InvalidRoot of string

type ExitDecision =
    | Success
    | GovernedBlocking
    | UsageError of ParseError list
    | InputUnavailable of reason: string
    | ToolError of reason: string

type BudgetState =
    { Requested: string list
      CacheHits: string list
      CacheMisses: string list
      FreshDispatches: string list
      Pending: string list
      BudgetExhausted: string list }

type CommandPayload =
    | RoutePayload of Route
    | ExplainPayload of Explanation list
    | ContractPayload of ContractEntry list
    | EvidencePayload of ProjectEvidenceReport

type CommandResult =
    { Request: RunRequest option
      Payload: CommandPayload option
      Budget: BudgetState
      Failures: Failure list
      Exit: ExitDecision }

type Phase =
    | Starting
    | LoadingSnapshot
    | RunningHost
    | RenderingOutput
    | Done

type Model =
    { Phase: Phase
      RawArgv: string list
      Request: RunRequest option
      Snapshot: ProjectSnapshot option
      HostModel: FS.GG.Governance.Host.Model<ProjectFact> option
      Budget: BudgetState
      Result: CommandResult option }

type Msg =
    | Parsed of Result<RunRequest, ParseError list>
    | SnapshotLoaded of Result<ProjectSnapshot, string>
    | HostCompleted of host: FS.GG.Governance.Host.Model<ProjectFact> * budget: BudgetState
    | OutputWritten of Result<unit, string>

type Effect =
    | LoadSnapshot of RunRequest
    | RunHost of RunRequest * ProjectSnapshot
    | WriteOutput of RunRequest * CommandResult
    | Finish of ExitDecision

type CliPorts =
    { LoadSnapshot: RunRequest -> Result<ProjectSnapshot, string>
      RunHost: RunRequest -> ProjectSnapshot -> FS.GG.Governance.Host.Model<ProjectFact> * BudgetState
      WriteOutput: RunRequest -> CommandResult -> Result<unit, string> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Cli =

    let defaultJudge =
        { ModelId = "fsgg-governance-default"
          Version = "2026-06" }

    let emptyBudget =
        { Requested = []
          CacheHits = []
          CacheMisses = []
          FreshDispatches = []
          Pending = []
          BudgetExhausted = [] }

    let allDomains = Set.ofList [ SpecKitDomain; DesignSystemDomain ]

    let stableStrings values =
        values |> List.distinct |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a, b))

    let parseCommand (text: string) =
        match text with
        | "route" -> Ok RouteCommand
        | "explain" -> Ok ExplainCommand
        | "contract" -> Ok ContractCommand
        | "evidence" -> Ok EvidenceCommand
        | "watch" -> Ok WatchCommand
        | "tui" -> Ok TuiCommand
        | other -> Error(UnknownCommand other)

    let parseMode (text: string) =
        match text.ToLowerInvariant() with
        | "sandbox" -> Ok Sandbox
        | "inner" -> Ok Inner
        | "gate" -> Ok Gate
        | other -> Error(InvalidMode other)

    let parseFormat (text: string) =
        match text.ToLowerInvariant() with
        | "text" -> Ok Text
        | "json" -> Ok Json
        | other -> Error(InvalidFormat other)

    let parseBudget (text: string) =
        match Int32.TryParse(text) with
        | true, value when value >= 0 ->
            if value = 0 then Ok CacheOnly else Ok(FreshReviews value)
        | _ -> Error(InvalidReviewBudget text)

    let parseDomain (text: string) =
        match text.ToLowerInvariant() with
        | "all" -> Ok allDomains
        | "speckit" | "spec-kit" -> Ok(Set.singleton SpecKitDomain)
        | "design" | "design-system" -> Ok(Set.singleton DesignSystemDomain)
        | other -> Error(UnknownOption("--domain " + other))

    let splitScope (value: string) =
        value.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
        |> Array.toList

    type ParseAcc =
        { Root: string
          Mode: RunMode
          Format: OutputFormat
          Scope: string list
          Domains: Set<Domain>
          ReviewBudget: ReviewBudget
          ReviewStore: string option
          OutputPath: string option
          JudgeModel: string
          JudgeVersion: string
          ExplicitPlain: bool
          Errors: ParseError list }

    let addError (error: ParseError) (acc: ParseAcc) = { acc with Errors = acc.Errors @ [ error ] }

    let requireValue option rest onValue (acc: ParseAcc) =
        match rest with
        | [] -> addError (MissingOptionValue option) acc, []
        | value :: tail -> onValue value acc, tail

    let isInvalidRoot (value: string) =
        String.IsNullOrWhiteSpace value || value.IndexOf('\u0000') >= 0

    let parseOptions (command: CommandKind) (args: string list) =
        let initial: ParseAcc =
            { Root = "."
              Mode = Inner
              Format = Text
              Scope = []
              Domains = allDomains
              ReviewBudget = CacheOnly
              ReviewStore = None
              OutputPath = None
              JudgeModel = defaultJudge.ModelId
              JudgeVersion = defaultJudge.Version
              ExplicitPlain = false
              Errors = [] }

        let rec loop (acc: ParseAcc) remaining =
            match remaining with
            | [] -> acc
            | "--json" :: tail -> loop { acc with Format = Json } tail
            // F27 wiring (063): additive explicit-plain signal. It composes with --json/--format without
            // changing their meaning (the Json branch still wins); it only forces ANSI-free output on a TTY.
            | "--plain" :: tail
            | "--no-color" :: tail -> loop { acc with ExplicitPlain = true } tail
            | "--root" :: tail ->
                let acc, tail =
                    requireValue
                        "--root"
                        tail
                        (fun value acc ->
                            if isInvalidRoot value then addError (InvalidRoot value) acc else { acc with Root = value })
                        acc

                loop acc tail
            | "--mode" :: tail ->
                let acc, tail =
                    requireValue
                        "--mode"
                        tail
                        (fun value acc ->
                            match parseMode value with
                            | Ok mode -> { acc with Mode = mode }
                            | Error e -> addError e acc)
                        acc

                loop acc tail
            | "--format" :: tail ->
                let acc, tail =
                    requireValue
                        "--format"
                        tail
                        (fun value acc ->
                            match parseFormat value with
                            | Ok format -> { acc with Format = format }
                            | Error e -> addError e acc)
                        acc

                loop acc tail
            | "--scope" :: tail ->
                let acc, tail =
                    requireValue
                        "--scope"
                        tail
                        (fun value acc -> { acc with Scope = acc.Scope @ splitScope value })
                        acc

                loop acc tail
            | "--review-budget" :: tail ->
                let acc, tail =
                    requireValue
                        "--review-budget"
                        tail
                        (fun value acc ->
                            match parseBudget value with
                            | Ok budget -> { acc with ReviewBudget = budget }
                            | Error e -> addError e acc)
                        acc

                loop acc tail
            | "--review-store" :: tail ->
                let acc, tail =
                    requireValue "--review-store" tail (fun value acc -> { acc with ReviewStore = Some value }) acc

                loop acc tail
            | "--out" :: tail ->
                let acc, tail =
                    requireValue "--out" tail (fun value acc -> { acc with OutputPath = Some value }) acc

                loop acc tail
            | "--judge-model" :: tail ->
                let acc, tail =
                    requireValue "--judge-model" tail (fun value acc -> { acc with JudgeModel = value }) acc

                loop acc tail
            | "--judge-version" :: tail ->
                let acc, tail =
                    requireValue "--judge-version" tail (fun value acc -> { acc with JudgeVersion = value }) acc

                loop acc tail
            | "--domain" :: tail ->
                let acc, tail =
                    requireValue
                        "--domain"
                        tail
                        (fun value acc ->
                            match parseDomain value with
                            | Ok domains -> { acc with Domains = domains }
                            | Error e -> addError e acc)
                        acc

                loop acc tail
            | option :: tail when option.StartsWith("--", StringComparison.Ordinal) ->
                loop (addError (UnknownOption option) acc) tail
            | extra :: tail -> loop (addError (UnknownOption extra) acc) tail

        let acc: ParseAcc = loop initial args

        if List.isEmpty acc.Errors then
            let request: RunRequest =
                { Root = acc.Root
                  Command = command
                  Mode = acc.Mode
                  Format = acc.Format
                  Scope = acc.Scope |> stableStrings
                  Domains = acc.Domains
                  ReviewBudget = acc.ReviewBudget
                  ReviewStore = acc.ReviewStore
                  OutputPath = acc.OutputPath
                  Judge =
                    { ModelId = acc.JudgeModel
                      Version = acc.JudgeVersion }
                  ExplicitPlain = acc.ExplicitPlain }

            Ok
                request
        else
            Error acc.Errors

    let parse (argv: string list) =
        match argv with
        | [] -> Error [ MissingCommand ]
        | command :: rest ->
            match parseCommand command with
            | Ok command -> parseOptions command rest
            | Error e -> Error [ e ]

    let exitCode decision =
        match decision with
        | Success -> 0
        | GovernedBlocking -> 2
        | UsageError _ -> 64
        | InputUnavailable _ -> 66
        | ToolError _ -> 70

    let outcomeByRule (facts: FactSet<ProjectFact>) =
        facts
        |> List.choose (fun fact ->
            match fact.Value with
            | GovernanceFact (RuleOutcome.Decided (rule, verdict)) -> Some(rule, Choice1Of3 verdict)
            | GovernanceFact (RuleOutcome.NeedsReview request) -> Some(request.Rule, Choice2Of3 request.Key)
            | GovernanceFact (RuleOutcome.Escalated rule) -> Some(rule, Choice3Of3 ())
            | _ -> None)

    let hasBlockingFailure (route: Route) (facts: FactSet<ProjectFact>) =
        let blocking = route.Blocking |> List.map (fun entry -> entry.Id) |> Set.ofList

        outcomeByRule facts
        |> List.exists (fun (rule, outcome) ->
            Set.contains rule blocking
            && match outcome with
               | Choice1Of3 Pass -> false
               | Choice1Of3 (Fail _) -> true
               | Choice1Of3 (Uncertain _) -> true
               | Choice2Of3 _ -> true
               | Choice3Of3 _ -> true)

    let commandCatalog (request: RunRequest) : FS.GG.Governance.Adapters.Spi.Composed<ProjectFact, ProjectChange> =
        let options: ProjectOptions =
            { Domains = request.Domains
              Judge = request.Judge
              SpecKitDial = Catalog.defaultDial }

        Project.compose
            options

    let explanationsFor (request: RunRequest) (host: FS.GG.Governance.Host.Model<ProjectFact>) =
        let composed = commandCatalog request
        composed.Catalog |> List.map (fun rule -> Check.explain host.Facts rule.Check)

    let payloadFor (request: RunRequest) (host: FS.GG.Governance.Host.Model<ProjectFact>) : CommandPayload =
        match request.Command with
        | RouteCommand -> RoutePayload host.Route
        | ExplainCommand -> ExplainPayload(explanationsFor request host)
        | ContractCommand -> ContractPayload(Contract.ofRules (commandCatalog request).Catalog)
        | EvidenceCommand -> EvidencePayload(Project.evidenceReport host)
        // F27 wiring (063): the read-only watch/tui surfaces are dispatched at the Program edge and never
        // reach this one-shot payload path. If the pure MVU is driven with them directly (tests), fall back
        // to the route payload — a benign one-shot view, never an interactive loop.
        | WatchCommand
        | TuiCommand -> RoutePayload host.Route

    let exitFor (host: FS.GG.Governance.Host.Model<ProjectFact>) =
        if hasBlockingFailure host.Route host.Facts then
            GovernedBlocking
        else
            Success

    let resultForHost (request: RunRequest) (host: FS.GG.Governance.Host.Model<ProjectFact>) (budget: BudgetState) : CommandResult =
        let payload = payloadFor request host
        let failures = host.Failures

        { Request = Some request
          Payload = Some payload
          Budget = budget
          Failures = failures
          Exit = exitFor host }

    let usageResult (errors: ParseError list) : CommandResult =
        { Request = None
          Payload = None
          Budget = emptyBudget
          Failures = []
          Exit = UsageError errors }

    let inputResult (request: RunRequest) (reason: string) : CommandResult =
        { Request = Some request
          Payload = None
          Budget = emptyBudget
          Failures = []
          Exit = InputUnavailable reason }

    let toolResult (request: RunRequest option) (budget: BudgetState) (failures: Failure list) (reason: string) : CommandResult =
        { Request = request
          Payload = None
          Budget = budget
          Failures = failures
          Exit = ToolError reason }

    let init (argv: string list) =
        match parse argv with
        | Ok request ->
            { Phase = LoadingSnapshot
              RawArgv = argv
              Request = Some request
              Snapshot = None
              HostModel = None
              Budget = emptyBudget
              Result = None },
            [ LoadSnapshot request ]
        | Error errors ->
            let result = usageResult errors

            { Phase = Done
              RawArgv = argv
              Request = None
              Snapshot = None
              HostModel = None
              Budget = emptyBudget
              Result = Some result },
            [ Finish result.Exit ]

    let update (msg: Msg) (model: Model) =
        match msg, model.Request with
        | Parsed parsed, _ ->
            match parsed with
            | Ok request ->
                { model with
                    Phase = LoadingSnapshot
                    Request = Some request
                    Result = None },
                [ LoadSnapshot request ]
            | Error errors ->
                let result = usageResult errors
                { model with Phase = Done; Result = Some result }, [ Finish result.Exit ]

        | SnapshotLoaded (Ok snapshot), Some request ->
            { model with
                Phase = RunningHost
                Snapshot = Some snapshot },
            [ RunHost(request, snapshot) ]

        | SnapshotLoaded (Error reason), Some request ->
            let result = inputResult request reason

            { model with
                Phase = RenderingOutput
                Result = Some result },
            [ WriteOutput(request, result) ]

        | HostCompleted (host, budget), Some request ->
            let result = resultForHost request host budget

            { model with
                Phase = RenderingOutput
                HostModel = Some host
                Budget = budget
                Result = Some result },
            [ WriteOutput(request, result) ]

        | OutputWritten (Ok ()), _ ->
            match model.Result with
            | Some result -> { model with Phase = Done }, [ Finish result.Exit ]
            | None ->
                let result = toolResult model.Request model.Budget [] "output completed without a result"
                { model with Phase = Done; Result = Some result }, [ Finish result.Exit ]

        | OutputWritten (Error reason), _ ->
            let failures = model.Result |> Option.map (fun result -> result.Failures) |> Option.defaultValue []
            let result = toolResult model.Request model.Budget failures reason
            { model with Phase = Done; Result = Some result }, [ Finish result.Exit ]

        | _, None ->
            let result = toolResult None model.Budget [] "command state lost its request"
            { model with Phase = Done; Result = Some result }, [ Finish result.Exit ]

    let fallbackResult (model: Model) =
        model.Result
        |> Option.defaultValue (toolResult model.Request model.Budget [] "command ended without a result")

    let run (ports: CliPorts) (argv: string list) =
        let initial, effects = init argv

        let rec drive model pending =
            match pending with
            | [] -> fallbackResult model
            | Finish _ :: _ -> fallbackResult model
            | LoadSnapshot request :: tail ->
                let loaded =
                    try
                        ports.LoadSnapshot request
                    with ex ->
                        Error ex.Message

                let model, effects = update (SnapshotLoaded loaded) model
                drive model (tail @ effects)
            | RunHost (request, snapshot) :: tail ->
                let completed =
                    try
                        Ok(ports.RunHost request snapshot)
                    with ex ->
                        Error ex.Message

                match completed with
                | Ok (host, budget) ->
                    let model, effects = update (HostCompleted(host, budget)) model
                    drive model (tail @ effects)
                | Error reason ->
                    let result = toolResult (Some request) model.Budget [] reason

                    let model =
                        { model with
                            Phase = RenderingOutput
                            Result = Some result }

                    drive model (tail @ [ WriteOutput(request, result) ])
            | WriteOutput (request, result) :: tail ->
                let written =
                    try
                        ports.WriteOutput request result
                    with ex ->
                        Error ex.Message

                let model, effects = update (OutputWritten written) model
                drive model (tail @ effects)

        drive initial effects
