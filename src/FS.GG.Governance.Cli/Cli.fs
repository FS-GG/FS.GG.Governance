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
    | RoutePayload of route: Route * handoffGates: FS.GG.Governance.Gates.Model.Gate list
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

    // F081 wiring: reach the SDD→Governance handoff consumer + gate vocabulary via aliases so the
    // `Maturity`/`Gate` names never collide with the Kernel/Host/SpecKit opens above.
    open FS.GG.Governance.Adapters.SddHandoff
    module GatesModel = FS.GG.Governance.Gates.Model
    module ConfigModel = FS.GG.Governance.Config.Model
    // 090: the canonical Phase-5 enforcement core — the SAME derivation `Ship.rollup` uses for every
    // other gate. The handoff-gate blocking decision flows through it (no handoff-specific branch).
    module Enforcement = FS.GG.Governance.Enforcement.Enforcement

    // 100 (M-ARCH-2): `defaultJudge` moved to the Project module (ProjectSensing library); referenced
    // as `Project.defaultJudge` below. Kept out of the Cli exe so EvidenceCommand can consume it without
    // referencing this executable.

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
              JudgeModel = Project.defaultJudge.ModelId
              JudgeVersion = Project.defaultJudge.Version
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

    // F081 wiring (the handoff verdict): consume every located `governance-handoff.json` into typed
    // gates via the proven `Consumer.consume` (PURE & TOTAL — a bad document becomes a blocking
    // integrity gate, never a throw). This is the SAME consumer ShipCommand/RouteCommand fold; the
    // `route` command now folds it too so a produced handoff drives the verdict.
    let handoffGatesOf (handoffs: Reader.HandoffRead list) : GatesModel.Gate list =
        (Consumer.consume handoffs).Gates

    // 090 (T007): map the route run mode to the enforcement run mode for the handoff-gate call. The
    // strict-merge boundary `Gate` maps to the enforcement `Verify` ordinal (research D1): with a
    // failing handoff at the `BlockOnShip` floor (ordinal 4), strict (tighten 1) pulls the floor to 3
    // so a Verify-mode run blocks, while light (tighten 0) leaves it at 4 so the same run is advisory.
    // `Sandbox`/`Inner` map below any blocking floor ⇒ a failing handoff stays advisory under every
    // profile, preserving the 089 light-mode behavior (Invariant 3). Total over the 3 route cases.
    let toEnforcementMode (mode: RunMode) : Enforcement.RunMode =
        match mode with
        | Sandbox -> Enforcement.Sandbox
        | Inner -> Enforcement.Inner
        | Gate -> Enforcement.Verify

    // 090 (T008): resolve the active enforcement profile from the product's declared `defaultProfile`.
    // Absent / missing / unrecognized → `Strict` — the ONE-WAY fail-safe (FR-004): the gate-blocking
    // decision never relaxes by omission. This is deliberately STRICTER than the `Standard` default the
    // cost-tier path (`ProductSurfaces`) uses; the two serve opposite fail-safe directions (research D2).
    let resolveProfile (declared: ConfigModel.ProfileId option) : Enforcement.Profile =
        match declared with
        | Some(ConfigModel.ProfileId raw) ->
            match Enforcement.recognizeProfile raw with
            | Enforcement.Recognized profile -> profile
            | Enforcement.Unrecognized _ -> Enforcement.Strict
        | None -> Enforcement.Strict

    // 090 (T009): build one consumed handoff gate's enforcement input EXACTLY as `Ship.gateToInput`
    // does (research D3) — base `Blocking` iff the maturity is a block level, maturity/mode/profile
    // carried verbatim. No handoff-specific branch: the gate flows through the generic core like every
    // other gate. Total over the closed `Maturity` union (a future maturity is a compile error).
    let handoffGateToInput (mode: Enforcement.RunMode) (profile: Enforcement.Profile) (gate: GatesModel.Gate) : Enforcement.EnforcementInput =
        let baseSeverity =
            match gate.Maturity with
            | ConfigModel.Observe
            | ConfigModel.Warn -> Enforcement.Advisory
            | ConfigModel.BlockOnPr
            | ConfigModel.BlockOnShip
            | ConfigModel.BlockOnRelease -> Enforcement.Blocking

        { BaseSeverity = baseSeverity
          Maturity = gate.Maturity
          Mode = mode
          Profile = profile }

    // 090 (T009/T010): derive each consumed handoff gate's effective severity through the canonical
    // Phase-5 core parameterized by the active profile. The decisions carry the core's self-explaining
    // `Reason`, so a `GovernedBlocking` exit stays attributable to the failing handoff (Invariant 6);
    // the gates themselves are carried on the route payload for rendering.
    let handoffDecisions (mode: RunMode) (profile: Enforcement.Profile) (gates: GatesModel.Gate list) : Enforcement.EnforcementDecision list =
        let mapped = toEnforcementMode mode
        gates |> List.map (fun gate -> Enforcement.deriveEffectiveSeverity (handoffGateToInput mapped profile gate))

    // The run is blocked by the handoff channel iff ANY consumed gate derives `Blocking` under the
    // active profile (Invariant 4: relaxing one gate never masks another that still blocks). A
    // satisfied handoff (`Warn`) is withheld by the core under every profile, so it always passes.
    let handoffBlocking (mode: RunMode) (profile: Enforcement.Profile) (gates: GatesModel.Gate list) : bool =
        handoffDecisions mode profile gates
        |> List.exists (fun decision -> decision.EffectiveSeverity = Enforcement.Blocking)

    let payloadFor (request: RunRequest) (host: FS.GG.Governance.Host.Model<ProjectFact>) (handoffGates: GatesModel.Gate list) : CommandPayload =
        match request.Command with
        | RouteCommand -> RoutePayload(host.Route, handoffGates)
        | ExplainCommand -> ExplainPayload(explanationsFor request host)
        | ContractCommand -> ContractPayload(Contract.ofRules (commandCatalog request).Catalog)
        | EvidenceCommand -> EvidencePayload(Project.evidenceReport host)
        // F27 wiring (063): the read-only watch/tui surfaces are dispatched at the Program edge and never
        // reach this one-shot payload path. If the pure MVU is driven with them directly (tests), fall back
        // to the route payload — a benign one-shot view, never an interactive loop.
        | WatchCommand
        | TuiCommand -> RoutePayload(host.Route, handoffGates)

    // The route exit is `GovernedBlocking` when the F07 route has a blocking failure OR a consumed SDD
    // handoff blocks at gate mode (FR-002/FR-003): a produced failing handoff is no longer ignored.
    let exitFor (host: FS.GG.Governance.Host.Model<ProjectFact>) (handoffBlocks: bool) =
        if hasBlockingFailure host.Route host.Facts || handoffBlocks then
            GovernedBlocking
        else
            Success

    let resultForHost
        (request: RunRequest)
        (host: FS.GG.Governance.Host.Model<ProjectFact>)
        (budget: BudgetState)
        (handoffs: Reader.HandoffRead list)
        (declaredProfile: ConfigModel.ProfileId option)
        : CommandResult =
        // The handoff verdict participates in the `route` exit only (the command whose contract is the
        // produced-handoff gate; explain/contract/evidence do not consume it). The gates are still carried
        // on the route payload for attribution even in light modes (where they do not block).
        let handoffGates =
            match request.Command with
            | RouteCommand
            | WatchCommand
            | TuiCommand -> handoffGatesOf handoffs
            | ExplainCommand
            | ContractCommand
            | EvidenceCommand -> []

        // 090: the handoff gate honors the product's active policy profile (absent/unrecognized →
        // Strict, the fail-safe) through the canonical enforcement core, instead of the old
        // profile-blind `mode = Gate && gateBlocks` shortcut.
        let profile = resolveProfile declaredProfile
        let blocks = handoffBlocking request.Mode profile handoffGates
        let payload = payloadFor request host handoffGates
        let failures = host.Failures

        { Request = Some request
          Payload = Some payload
          Budget = budget
          Failures = failures
          Exit = exitFor host blocks }

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
            // The handoff documents were located at snapshot time (the I/O edge); fold them into the
            // route verdict here (pure). No snapshot ⇒ no handoffs (the host never ran without one).
            let handoffs =
                model.Snapshot |> Option.map (fun s -> s.Handoffs) |> Option.defaultValue []

            // 090: the declared profile was read at the same snapshot (Config-load) edge; carry it
            // into the pure route verdict. No snapshot ⇒ no profile ⇒ the fail-safe Strict default.
            let declaredProfile = model.Snapshot |> Option.bind (fun s -> s.DefaultProfile)

            let result = resultForHost request host budget handoffs declaredProfile

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
