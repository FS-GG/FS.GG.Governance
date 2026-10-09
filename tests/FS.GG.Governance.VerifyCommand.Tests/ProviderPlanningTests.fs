module FS.GG.Governance.VerifyCommand.Tests.ProviderPlanningTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandHost
open FS.GG.Governance.GateRun
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

module CB = FS.GG.Governance.Config.CapabilityBindings

// SYNTHETIC normalized declarations and real Verify selection; no observed provider/runtime evidence.
let command: Fsgg.Provider.DeclaredCommand =
    { Executable = "literal tool"; Arguments = [ ""; "two words"; "$HOME"; ";" ] }
let limits: CB.CommandLimits =
    { WorkingDirectory = Some(gp "src"); Timeout = Some(TimeoutLimit 60)
      Cost = Some Cheap; Environments = [ Local ] }
let binding: CB.CapabilityBinding =
    { CapabilityId = "build:build"; Required = false; Platforms = [ "linux" ]
      ToolIds = [ "compiler" ]; EvidenceIds = [ "log" ]; Binding = CB.Command command; Limits = Some limits }
let format: CB.EvidenceFormat = { Id = "log"; Version = "1.0.0" }
let resolution: CB.ResolutionRequest =
    { ContractVersion = "1.0.0"; SelectedPlatform = "linux"; RequiredCapabilityIds = [ binding.CapabilityId ]
      SemanticOnlyObligationIds = []; KnownPlatforms = [ "linux" ]; SupportedEnvironments = [ Local ]
      SupportedEvidenceFormats = [ format ]; Tools = [ { Id = "compiler"; ExactVersion = "10.0.0" } ]
      Evidence = [ { Id = "log"; Format = format; OutputPath = gp "artifacts/log.json" } ]; Bindings = [ binding ] }
let context: Plan.ProviderCommandContext =
    { RepoRoot = "/synthetic"; Environment = Local; RemainingTimeout = TimeoutLimit 25
      EnvironmentDelta = { Added = []; Changed = []; Removed = [] } }
let facts =
    let old = factsOf validCatalog
    let check = old.Capabilities.Checks |> List.find(fun c -> c.Id = CheckId "build")
    { old with
        Project = { old.Project with Domains = [ DomainId "build" ] }
        Capabilities =
            { old.Capabilities with
                Domains = [ DomainId "build" ]
                Surfaces = []
                PathMap = [ { Glob = gp "src/**"; Capability = DomainId "build" } ]
                Checks = [ { check with Domain = DomainId "build" } ] } }
let loaded candidate selectedFacts paths =
    let initial, _ = Loop.initProviderPlanning (requestFor (Loop.ExplicitPaths paths) Loop.Text)
                        { ResolutionRequest = candidate; CommandContext = context }
    Loop.updateProviderPlanning (Loop.Loaded(Valid selectedFacts)) initial
let refused code candidate selectedFacts paths =
    let model, effects = loaded candidate selectedFacts paths
    Expect.equal model.Legacy.Exit Loop.InputUnavailable "invalid declarations are input failures"
    Expect.isTrue (model.Diagnostics |> List.exists(fun d -> d.Code = code)) "typed resolver diagnostic retained"
    Expect.isNone model.Plan "no partial plan"
    Expect.isEmpty effects "no freshness/store/execution or empty success effects"
    Expect.isTrue ((Loop.render model.Legacy Loop.Text).Contains "provider planning rejected") "actionable diagnostics"

[<Tests>]
let tests = testList "ProviderPlanningLoop" [
    testCase "malformed optional binding outside selection refuses before legacy cache" (fun _ ->
        let bad = { binding with CapabilityId = "test:test"; Limits = Some { limits with Timeout = Some(TimeoutLimit 0) } }
        refused CB.InvalidExecutionLimit { resolution with Bindings = [ binding; bad ] } facts [ gp "src/a.fs" ])
    testCase "required binding outside selection cannot disappear" (fun _ ->
        refused CB.MissingRequiredBinding { resolution with RequiredCapabilityIds = [ "build:build"; "test:test" ] } facts [ gp "src/a.fs" ])
    testCase "required gate omitted by routing refuses" (fun _ ->
        let required = { binding with CapabilityId = "test:test"; Required = true }
        refused CB.MissingRequiredBinding { resolution with Bindings = [ binding; required ] } facts [ gp "src/a.fs" ])
    testCase "empty selection cannot return nothing to verify" (fun _ ->
        refused CB.MissingRequiredBinding resolution facts [ gp "README.md" ])
    testCase "valid literal plan retains effective floors and bypasses legacy reuse" (fun _ ->
        let model, effects = loaded resolution facts [ gp "src/a.fs" ]
        Expect.equal model.Legacy.Exit Loop.Blocked "declaration is not pass"
        Expect.isEmpty effects "no legacy store or execution"
        Expect.isEmpty model.Diagnostics "valid declarations not malformed"
        let plan = model.Plan |> Option.get
        match snd plan.Gates.Head with
        | CommandHost.ProviderExecute(actual, cost, evidence) ->
            Expect.equal actual.Executable (Executable command.Executable) "literal executable"
            Expect.equal actual.Arguments (List.map Argument command.Arguments) "literal ordered argv"
            Expect.equal actual.Timeout (TimeoutLimit 25) "remaining budget"
            Expect.equal cost Medium "effective gate floor"
            Expect.equal evidence resolution.Evidence "evidence not accepted"
        | other -> failtestf "unexpected %A" other
        Expect.isTrue ((Loop.render model.Legacy Loop.Json).Contains "not connected") "explicit runtime boundary"
        let terminal, later = Loop.updateProviderPlanning (Loop.StoreLoaded(Ok FS.GG.Governance.EvidenceReuse.EvidenceReuse.empty)) model
        Expect.equal terminal model "later cache result inert"
        Expect.isEmpty later "no reuse")
    testCase "higher declared cost remains deferred and nonpassing" (fun _ ->
        let expensive = { binding with Limits = Some { limits with Cost = Some Exhaustive } }
        let model, effects = loaded { resolution with Bindings = [ expensive ] } facts [ gp "src/a.fs" ]
        match snd (Option.get model.Plan).Gates.Head with
        | CommandHost.ProviderDeferred(Exhaustive, _) -> ()
        | other -> failtestf "unexpected %A" other
        Expect.equal model.Legacy.Exit Loop.Blocked "deferral is not pass"
        Expect.isEmpty effects "no execution")
    testCase "semantic obligation preserves commandless floor without passing" (fun _ ->
        let semantic = { binding with CapabilityId = "gameplay:fr-covered"; Required = true;
                                     Binding = CB.SemanticOnly; Limits = None; ToolIds = []; EvidenceIds = [] }
        let check = facts.Capabilities.Checks.Head
        let semanticCapabilities =
            { facts.Capabilities with
                Checks = [ { check with Domain = DomainId "gameplay"; Id = CheckId "fr-covered"; Command = None } ]
                Domains = [ DomainId "gameplay" ]
                PathMap = [ { Glob = gp "src/**"; Capability = DomainId "gameplay" } ] }
        let semanticFacts = { facts with Capabilities = semanticCapabilities }
        let request = { resolution with RequiredCapabilityIds = []; SemanticOnlyObligationIds = [ semantic.CapabilityId ]; Bindings = [ semantic ] }
        let model, effects = loaded request semanticFacts [ gp "src/a.fs" ]
        Expect.equal (snd (Option.get model.Plan).Gates.Head) CommandHost.ProviderSemanticOnly "semantic floor retained"
        Expect.equal model.Legacy.Exit Loop.Blocked "semantic declaration not evidence"
        Expect.isEmpty effects "no legacy path")
    testCase "unknown optional capability remains visibly unsupported" (fun _ ->
        let unknown = { binding with CapabilityId = "future:check" }
        let model, effects = loaded { resolution with Bindings = [ binding; unknown ] } facts [ gp "src/a.fs" ]
        Expect.equal (Option.get model.Plan).UnsupportedCapabilityIds [ unknown.CapabilityId ] "unknown retained"
        Expect.equal model.Legacy.Exit Loop.Blocked "unsupported is not pass"
        Expect.isEmpty effects "no legacy effects")
]
