module FS.GG.Governance.CommandHost.Tests.ProviderPlanTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateRun
open FS.GG.Governance.CommandHost

// SYNTHETIC: normalized declarations/effective gates/profile inputs, not observed tools,
// workspace provenance or execution evidence. Actual producer/Verify join is GOV423-C2 phase2/SDD928-C2.3.

module CB = FS.GG.Governance.Config.CapabilityBindings
let command : Fsgg.Provider.DeclaredCommand =
    { Executable = "tool with spaces"; Arguments = [ ""; "two words"; "$HOME"; ";"; "last" ] }
let limits : CB.CommandLimits =
    { WorkingDirectory = Some (GovernedPath "src/./cli")
      Timeout = Some (TimeoutLimit 60); Cost = Some Cheap; Environments = [ Local ] }
let binding : CB.CapabilityBinding =
    { CapabilityId = "build:build"; Required = false; Platforms = [ "linux" ]
      ToolIds = [ "compiler" ]; EvidenceIds = [ "build-log" ]; Binding = CB.Command command; Limits = Some limits }
let format : CB.EvidenceFormat = { Id = "build-log"; Version = "1.0.0" }
let request : CB.ResolutionRequest =
    { ContractVersion = "1.0.0"; SelectedPlatform = "linux"; RequiredCapabilityIds = [ "build:build" ]
      SemanticOnlyObligationIds = []; KnownPlatforms = [ "linux" ]; SupportedEnvironments = [ Local; Ci ]
      SupportedEvidenceFormats = [ format ]; Tools = [ { Id = "compiler"; ExactVersion = "10.0.0" } ]
      Evidence = [ { Id = "build-log"; Format = format; OutputPath = GovernedPath "artifacts/build.json" } ]; Bindings = [ binding ] }
let gate : Gate =
    { Id = GateId "build:build"; Domain = DomainId "build"; Description = "Synthetic effective build gate"
      Prerequisites = [ RequiresCommand (CommandId "legacy-build") ]; Cost = Medium; Timeout = TimeoutLimit 40
      Owner = Owner "synthetic-owner"; Maturity = BlockOnShip; ProductCheck = false
      FreshnessKey = { Check = CheckId "build"; Domain = DomainId "build"; Cost = Medium; Environment = LocalOrCi; Command = Some (CommandId "legacy-build") } }
let context : Plan.ProviderCommandContext =
    { RepoRoot = "/synthetic-governed"; Environment = Local; RemainingTimeout = TimeoutLimit 25
      EnvironmentDelta = { Added = [ { Name = EnvVarName "LITERAL"; Value = EnvVarValue "a $HOME; b" } ]; Changed = []; Removed = [] } }
let require condition message = if not condition then failwith message
let rejected code candidate profile gates =
    match CommandHost.providerExecutionPlan candidate profile Exhaustive gates with
    | Ok _ -> failwithf "Expected located refusal %A" code
    | Error ds ->
        require (ds |> List.exists (fun d -> d.Code = code)) (sprintf "Missing expected refusal %A: %A" code ds)
        require (ds = List.sortBy (fun d -> d.CapabilityId, d.Field, d.Code, d.Message) ds) "Diagnostics should be deterministic"
let planned candidate ceiling gates =
    match CommandHost.providerExecutionPlan candidate context ceiling gates with
    | Error ds -> failwithf "Unexpected refusal: %A" ds
    | Ok value -> value
let cases : (string * (unit -> unit)) list = [
    "Synthetic literal command preserves whole-request policy, gate floors and bounded inputs", (fun () ->
        let plan = planned request Exhaustive [ gate ]
        require (fst plan.Gates.Head = gate) "Effective gate/maturity/owner must be retained"
        match snd plan.Gates.Head with
        | CommandHost.ProviderExecute (actual, cost, evidence) ->
            require (actual.Executable = Executable command.Executable) "Literal executable"
            require (actual.Arguments = List.map Argument command.Arguments) "Literal ordered argv"
            require (actual.WorkingDirectory = WorkingDirectory "/synthetic-governed/src/cli") "Governed cwd"
            require (actual.Environment = context.EnvironmentDelta) "Admitted delta"
            require (actual.Timeout = TimeoutLimit 25) "Remaining host deadline cap"
            require (cost = Medium) "Existing gate cost floor"
            require (evidence = request.Evidence) "Evidence declaration preserved, not accepted"
        | other -> failwithf "Expected executable: %A" other)
    "Synthetic missing required binding outside selection refuses whole request", (fun () ->
        rejected CB.MissingRequiredBinding { request with RequiredCapabilityIds = [ "build:build"; "test:test" ] } context [ gate ])
    "Synthetic missing selected required gate cannot disappear", (fun () ->
        let test = { binding with CapabilityId = "test:test"; Required = true }
        rejected CB.MissingRequiredBinding { request with Bindings = [ binding; test ] } context [ gate ])
    "Synthetic malformed unselected optional declaration refuses", (fun () ->
        let test = { binding with CapabilityId = "test:test"; Limits = Some { limits with Timeout = Some (TimeoutLimit 0) } }
        rejected CB.InvalidExecutionLimit { request with Bindings = [ binding; test ] } context [ gate ])
    "Synthetic empty selection is non-passing", (fun () -> rejected CB.MissingRequiredBinding request context [])
    "Synthetic duplicate selected identities refuse", (fun () -> rejected CB.DuplicateIdentity request context [ gate; gate ])
    "Synthetic cost deferral retains higher effective floor", (fun () ->
        let plan = planned request Cheap [ gate ]
        require (snd plan.Gates.Head = CommandHost.ProviderDeferred (Medium, Cheap)) "Deferral must not execute/pass")
    "Synthetic declared higher cost cannot lower inherited floor", (fun () ->
        let candidate = { request with Bindings = [ { binding with Limits = Some { limits with Cost = Some High } } ] }
        let plan = planned candidate Medium [ gate ]
        require (snd plan.Gates.Head = CommandHost.ProviderDeferred (High, Medium)) "Higher binding cost preserved")
    "Synthetic recognized semantic floor remains commandless", (fun () ->
        let floor = { binding with CapabilityId = "gameplay:fr-covered"; Required = true; Binding = CB.SemanticOnly; Limits = None; ToolIds = []; EvidenceIds = [] }
        let floorGate = { gate with Id = GateId floor.CapabilityId; Prerequisites = [] }
        let candidate = { request with SemanticOnlyObligationIds = [ floor.CapabilityId ]; Bindings = [ binding; floor ] }
        let plan = planned candidate Exhaustive [ floorGate; gate ]
        require (plan.Gates |> List.exists (fun (g, c) -> g = floorGate && c = CommandHost.ProviderSemanticOnly)) "Semantic floor retained; no pass claim")
    "Synthetic executable floor cannot downgrade to semantic-only", (fun () ->
        let floor = { binding with CapabilityId = "gameplay:fr-covered"; Required = true; Binding = CB.SemanticOnly; Limits = None; ToolIds = []; EvidenceIds = [] }
        let candidate = { request with SemanticOnlyObligationIds = [ floor.CapabilityId ]; Bindings = [ binding; floor ] }
        rejected CB.IllegalSemanticOnly candidate context [ gate; { gate with Id = GateId floor.CapabilityId } ])
    "Synthetic optional unknown capability remains unsupported", (fun () ->
        let unknown = { binding with CapabilityId = "future:check" }
        let candidate = { request with Bindings = [ binding; unknown ] }
        let plan = planned candidate Exhaustive [ gate; { gate with Id = GateId unknown.CapabilityId } ]
        require (plan.UnsupportedCapabilityIds = [ unknown.CapabilityId ]) "Unknown must remain visible"
        require (plan.Gates |> List.exists (fun (_, c) -> c = CommandHost.ProviderUnsupported unknown.CapabilityId)) "No unknown executable")
    "Synthetic extra effective gate cannot vanish without admitted binding", (fun () ->
        rejected CB.MissingRequiredBinding request context [ gate; { gate with Id = GateId "fsharp:public-surface" } ])
    "Synthetic incompatible observed environment refuses", (fun () -> rejected CB.InvalidExecutionLimit request { context with Environment = Ci } [ gate ])
    "Synthetic conflicting environment delta refuses", (fun () ->
        let delta = { context.EnvironmentDelta with Removed = [ { Name = EnvVarName "LITERAL"; Old = EnvVarValue "old" } ] }
        rejected CB.MalformedInput request { context with EnvironmentDelta = delta } [ gate ])
    "Synthetic exhausted remaining deadline refuses", (fun () -> rejected CB.InvalidExecutionLimit request { context with RemainingTimeout = TimeoutLimit 0 } [ gate ])
    "Synthetic effective gate timeout is never weakened", (fun () ->
        let plan = planned request Exhaustive [ { gate with Timeout = TimeoutLimit 5 } ]
        match snd plan.Gates.Head with
        | CommandHost.ProviderExecute (actual, _, _) -> require (actual.Timeout = TimeoutLimit 5) "Stricter effective gate timeout"
        | other -> failwithf "%A" other)
    "Synthetic process-invalid literal argument refuses", (fun () ->
        let bad = { binding with Binding = CB.Command { command with Arguments = [ string (char 0) ] } }
        rejected CB.MalformedInput { request with Bindings = [ bad ] } context [ gate ])
]

[<Tests>]
let tests = testList "ProviderExecutionPlan" (cases |> List.map (fun (name, run) -> testCase name (fun _ -> run ())))
