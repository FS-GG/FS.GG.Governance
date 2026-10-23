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
let initialFor candidate paths =
    fst (Loop.initProvider (requestFor (Loop.ExplicitPaths paths) Loop.Text)
            { ResolutionRequest = candidate; CommandContext = context })
let loaded candidate selectedFacts paths =
    Loop.updateProvider (Loop.LegacyMsg(Loop.Loaded(Valid selectedFacts))) (initialFor candidate paths)
let refused code candidate selectedFacts paths =
    let model, effects = loaded candidate selectedFacts paths
    Expect.equal model.Legacy.Exit Loop.InputUnavailable "invalid declarations are input failures"
    Expect.equal model.Legacy.Phase Loop.Done "refusal is terminal"
    Expect.isTrue (model.Diagnostics |> List.exists(fun d -> d.Code = code)) "typed resolver diagnostic retained"
    Expect.isNone model.Plan "no partial plan"
    Expect.isEmpty effects "no freshness/store/execution or empty success effects"
    Expect.isTrue ((Loop.render model.Legacy Loop.Text).Contains "provider planning rejected") "actionable diagnostics"
    model

/// Interpret the pure provider tail: legacy writes succeed, summaries are emitted, and the one execution request is
/// answered by `observe`. Records every execution request so a test can prove none (or exactly one) happened.
let drivePure (observe: Loop.ProviderGateRun list -> Loop.ProviderGateObservation list) (model: Loop.ProviderModel, effects) =
    let requests = ResizeArray<Loop.ProviderGateRun list>()
    let writes = ResizeArray<string * string>()
    let rec go (model: Loop.ProviderModel) effects =
        match effects with
        | [] -> model
        | effect :: rest ->
            let msg =
                match effect with
                | Loop.ExecuteProviderGates runs ->
                    requests.Add runs
                    Loop.ProviderGatesExecuted(observe runs)
                | Loop.LegacyEffect(Loop.WriteArtifact(kind, path, content)) ->
                    writes.Add((path, content))
                    Loop.LegacyMsg(Loop.Wrote(kind, Ok()))
                | Loop.LegacyEffect(Loop.EmitSummary _) -> Loop.LegacyMsg Loop.Emitted
                | Loop.LegacyEffect other -> failtestf "provider mode must not request legacy effect %A" other
            let next, more = Loop.updateProvider msg model
            go next (rest @ more)
    let terminal = go model effects
    terminal, List.ofSeq requests, List.ofSeq writes

/// A launched run that exited by itself with both streams at EOF and direct resources released (synthetic facts).
let observedExit (gate: string) (exit: int option) : FS.GG.Governance.GateExecution.Model.BoundedObservation =
    let stream: FS.GG.Governance.GateExecution.Model.StreamObservation =
        { State = FS.GG.Governance.GateExecution.Model.EndOfFile; ObservedBytes = 0L
          Prefix = System.Collections.Immutable.ImmutableArray<byte>.Empty }
    { Identity = { Operation = "fsgg-verify-provider"; Launch = gate }
      Launch = FS.GG.Governance.GateExecution.Model.Started
      DirectExit = exit |> Option.map ExitCode
      Stdout = stream
      Stderr = stream
      FirstFailure = None
      SecondaryFailures = []
      CancellationRequested = false
      WorkDeadlineReached = false
      OutputLimitReached = false
      Settlement = FS.GG.Governance.GateExecution.Model.Released
      Descendants = FS.GG.Governance.GateExecution.Model.UncontainedUnobserved }

/// The canonical handoff document a provider run writes (SYNTHETIC content).
let handoffJson (contract: string) (nodes: (string * string) list) (sources: (string * string) list) =
    let nodeJson = nodes |> List.map (fun (id, state) -> sprintf "{\"id\":\"%s\",\"state\":\"%s\"}" id state) |> String.concat ","
    let sourceJson = sources |> List.map (fun (path, digest) -> sprintf "{\"path\":\"%s\",\"digest\":\"%s\"}" path digest) |> String.concat ","
    sprintf "{\"contractVersion\":\"%s\",\"schemaVersion\":1,\"evidence\":{\"nodes\":[%s],\"dependencies\":[]},\"governedReferences\":[],\"sources\":[%s]}" contract nodeJson sourceJson

let digestA = "sha256:" + System.String('a', 64)
let handoffFormat: CB.EvidenceFormat = { Id = "fsgg.governance-handoff"; Version = "2.0.0" }
let handoffEvidence: CB.EvidenceBinding = { Id = "log"; Format = handoffFormat; OutputPath = gp "artifacts/log.json" }
let handoffResolution =
    { resolution with SupportedEvidenceFormats = [ handoffFormat ]; Evidence = [ handoffEvidence ] }
let goodHandoff = handoffJson "2.0.0" [ "build:build", "real" ] [ "src/a.fs", digestA ]
let requiredHandoffResolution = { handoffResolution with Bindings = [ { binding with Required = true } ] }
let notPassing (model: Loop.ProviderModel) (gate: string) =
    let decision = model.Legacy.Decision |> Option.get
    let id = FS.GG.Governance.Ship.Model.GateItem(FS.GG.Governance.Gates.Model.GateId gate)
    Expect.isFalse (decision.Passing |> List.exists (fun i -> i.Id = id)) "never relocated to passing"
    Expect.isTrue ((decision.Blockers @ decision.Warnings) |> List.exists (fun i -> i.Id = id)) "kept where enforcement placed it"
let accepted (run: Loop.ProviderGateRun) : Loop.ProviderGateObservation =
    { Gate = run.Gate
      Launch = Loop.Launched(observedExit (FS.GG.Governance.Gates.Model.gateIdValue run.Gate) (Some 0))
      Evidence =
        run.Evidence
        |> List.map (fun e -> { Evidence = e; State = Loop.Produced(goodHandoff, [ { Path = gp "src/a.fs"; Digest = digestA } ]) }) }

[<Tests>]
let tests = testList "ProviderPlanningLoop" [
    testCase "malformed optional binding outside selection refuses before legacy cache" (fun _ ->
        let bad = { binding with CapabilityId = "test:test"; Limits = Some { limits with Timeout = Some(TimeoutLimit 0) } }
        refused CB.InvalidExecutionLimit { resolution with Bindings = [ binding; bad ] } facts [ gp "src/a.fs" ] |> ignore)
    testCase "required binding outside selection cannot disappear" (fun _ ->
        refused CB.MissingRequiredBinding { resolution with RequiredCapabilityIds = [ "build:build"; "test:test" ] } facts [ gp "src/a.fs" ] |> ignore)
    testCase "required gate omitted by routing refuses" (fun _ ->
        let required = { binding with CapabilityId = "test:test"; Required = true }
        refused CB.MissingRequiredBinding { resolution with Bindings = [ binding; required ] } facts [ gp "src/a.fs" ] |> ignore)
    testCase "empty selection cannot return nothing to verify, and the refusal names the workspace catalog" (fun _ ->
        let model = refused CB.MissingRequiredBinding resolution facts [ gp "README.md" ]
        Expect.stringContains (Loop.render model.Legacy Loop.Text) ".fsgg" "names the workspace Governance catalog the selection comes from")
    testCase "a malformed declaration refusal does not claim a catalog cause" (fun _ ->
        let bad = { binding with Limits = Some { limits with Timeout = Some(TimeoutLimit 0) } }
        let model = refused CB.InvalidExecutionLimit { resolution with Bindings = [ bad ] } facts [ gp "src/a.fs" ]
        Expect.isFalse ((Loop.render model.Legacy Loop.Text).Contains ".fsgg") "catalog hint only for selection refusals")
    testCase "valid literal plan requests one execution with literal argv, limits and evidence, and nothing legacy" (fun _ ->
        let model, effects = loaded resolution facts [ gp "src/a.fs" ]
        Expect.notEqual model.Legacy.Phase Loop.Done "a declaration is not yet a verdict"
        Expect.isEmpty model.Diagnostics "valid declarations not malformed"
        let plan = model.Plan |> Option.get
        match snd plan.Gates.Head with
        | CommandHost.ProviderExecute(actual, cost, evidence) ->
            Expect.equal actual.Executable (Executable command.Executable) "literal executable"
            Expect.equal actual.Arguments (List.map Argument command.Arguments) "literal ordered argv"
            Expect.equal actual.Timeout (TimeoutLimit 25) "remaining budget"
            Expect.equal cost Medium "effective gate floor"
            Expect.equal evidence resolution.Evidence "declared evidence carried, not accepted"
        | other -> failtestf "unexpected %A" other
        match effects with
        | [ Loop.ExecuteProviderGates [ run ] ] ->
            Expect.equal run.Gate (FS.GG.Governance.Gates.Model.GateId "build:build") "the effective gate"
            Expect.equal run.WorkingDirectory (gp "src") "the binding's governed relative working directory"
            Expect.isFalse run.Required "required flag carried from the binding"
            Expect.equal run.Evidence resolution.Evidence "the declared outputs"
            Expect.equal run.Command.Arguments (List.map Argument command.Arguments) "literal argv reaches the edge unchanged"
        | other -> failtestf "expected exactly one execution request, got %A" other
        let terminal, later = Loop.updateProvider (Loop.LegacyMsg(Loop.StoreLoaded(Ok FS.GG.Governance.EvidenceReuse.EvidenceReuse.empty))) model
        Expect.equal terminal model "later cache result inert"
        Expect.isEmpty later "no reuse")
    testCase "accepted execution relocates the gate, writes verify.json and passes" (fun _ ->
        let terminal, requests, writes = drivePure (List.map accepted) (loaded handoffResolution facts [ gp "src/a.fs" ])
        Expect.equal requests.Length 1 "exactly one execution request"
        Expect.equal terminal.Legacy.Exit Loop.Success "accepted evidence passes"
        Expect.equal (terminal.Results |> List.map (fun (_, _, v) -> v)) [ Loop.ProviderAccepted ] "accepted"
        let path, doc = List.exactlyOne writes
        Expect.equal path "readiness/verify.json" "verify.json through the legacy writer"
        Expect.stringContains doc "build:build" "gate projected"
        match terminal.Legacy.Outcomes with
        | [ _, { Disposition = FS.GG.Governance.GateRun.Model.Executed(ExitCode 0, true) } ] -> ()
        | other -> failtestf "unexpected outcomes %A" other)
    testCase "causal control: the same zero exit without its evidence is not accepted" (fun _ ->
        let noEvidence run = { accepted run with Evidence = run.Evidence |> List.map (fun e -> ({ Evidence = e; State = Loop.AbsentAfterRun }: Loop.ProviderEvidenceObservation)) }
        let terminal, _, _ = drivePure (List.map noEvidence) (loaded requiredHandoffResolution facts [ gp "src/a.fs" ])
        Expect.equal terminal.Legacy.Exit Loop.Blocked "a zero exit cannot certify evidence"
        notPassing terminal "build:build"
        Expect.isTrue (terminal.Legacy.Diagnostics |> List.exists (fun d -> d.Message.Contains "was not produced by the run")) "missing evidence named")
    testCase "a required capability that is not accepted blocks whatever its maturity" (fun _ ->
        let warnFacts =
            { facts with Capabilities = { facts.Capabilities with Checks = facts.Capabilities.Checks |> List.map (fun c -> { c with Maturity = Warn }) } }
        let requiredResolution = requiredHandoffResolution
        let refusedRun (run: Loop.ProviderGateRun) = { accepted run with Launch = Loop.Launched(observedExit "build:build" (Some 1)) }
        let terminal, _, writes = drivePure (List.map refusedRun) (loaded requiredResolution warnFacts [ gp "src/a.fs" ])
        Expect.equal terminal.Legacy.Exit Loop.Blocked "required floor"
        let decision = terminal.Legacy.Decision |> Option.get
        Expect.isTrue (decision.Blockers |> List.exists (fun i -> i.Id = FS.GG.Governance.Ship.Model.GateItem(FS.GG.Governance.Gates.Model.GateId "build:build"))) "moved to blockers"
        Expect.isEmpty decision.Warnings "not left as a warning"
        Expect.stringContains (snd (List.exactlyOne writes)) "required provider capability was not accepted" "reason projected"
        // Control: the same refusal on an optional Warn gate stays advisory.
        let optional, _, _ = drivePure (List.map refusedRun) (loaded handoffResolution warnFacts [ gp "src/a.fs" ])
        Expect.equal optional.Legacy.Exit Loop.Success "optional warn gate keeps its maturity"
        Expect.isTrue (optional.Legacy.Diagnostics |> List.exists (fun d -> d.Message.Contains "advisory under its maturity")) "refusal still reported")
    testCase "higher declared cost remains deferred, unexecuted and nonpassing" (fun _ ->
        let expensive = { binding with Limits = Some { limits with Cost = Some Exhaustive } }
        let model, effects = loaded { resolution with Bindings = [ expensive ] } facts [ gp "src/a.fs" ]
        match snd (Option.get model.Plan).Gates.Head with
        | CommandHost.ProviderDeferred(Exhaustive, _) -> ()
        | other -> failtestf "unexpected %A" other
        let terminal, requests, _ = drivePure (fun _ -> failtest "a deferred gate is never executed") (model, effects)
        Expect.isEmpty requests "no execution"
        notPassing terminal "build:build"
        match terminal.Legacy.Outcomes with
        | [ _, { Disposition = FS.GG.Governance.GateRun.Model.NotExecuted } ] -> ()
        | other -> failtestf "unexpected outcomes %A" other)
    testCase "a required deferred capability is forced blocking" (fun _ ->
        let warnFacts =
            { facts with Capabilities = { facts.Capabilities with Checks = facts.Capabilities.Checks |> List.map (fun c -> { c with Maturity = Warn }) } }
        let expensive = { binding with Required = true; Limits = Some { limits with Cost = Some Exhaustive } }
        let terminal, requests, _ = drivePure (fun _ -> failtest "never executed") (loaded { resolution with Bindings = [ expensive ] } warnFacts [ gp "src/a.fs" ])
        Expect.isEmpty requests "no execution"
        Expect.equal terminal.Legacy.Exit Loop.Blocked "required deferral blocks")
    testCase "semantic obligation preserves commandless floor without passing or executing" (fun _ ->
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
        let terminal, requests, _ = drivePure (fun _ -> failtest "semantic obligations are command-free") (model, effects)
        Expect.isEmpty requests "command-free"
        notPassing terminal "gameplay:fr-covered"
        match terminal.Legacy.Outcomes with
        | [ _, { Disposition = FS.GG.Governance.GateRun.Model.NotExecuted } ] -> ()
        | other -> failtestf "unexpected outcomes %A" other)
    testCase "unknown optional capability remains visibly unsupported" (fun _ ->
        let unknown = { binding with CapabilityId = "future:check" }
        let model, effects = loaded { resolution with Bindings = [ binding; unknown ] } facts [ gp "src/a.fs" ]
        Expect.equal (Option.get model.Plan).UnsupportedCapabilityIds [ unknown.CapabilityId ] "unknown retained"
        match effects with
        | [ Loop.ExecuteProviderGates runs ] ->
            Expect.equal (runs |> List.map (fun r -> r.Gate)) [ FS.GG.Governance.Gates.Model.GateId "build:build" ] "only the admitted executable runs"
        | other -> failtestf "unexpected %A" other)
    testCase "unrequested, repeated or mismatched observations are inert or refused, never accepted" (fun _ ->
        let initial = initialFor requiredHandoffResolution [ gp "src/a.fs" ]
        let early, none = Loop.updateProvider (Loop.ProviderGatesExecuted []) initial
        Expect.equal early initial "an observation before planning is inert"
        Expect.isEmpty none "nothing requested"
        let model, _ = loaded requiredHandoffResolution facts [ gp "src/a.fs" ]
        let wrongGate run = { accepted run with Gate = FS.GG.Governance.Gates.Model.GateId "other:gate" }
        let terminal, _, _ = drivePure (List.map wrongGate) (model, [ Loop.ExecuteProviderGates model.Runs ])
        Expect.equal terminal.Legacy.Exit Loop.Blocked "an observation for another gate is not this run"
        Expect.equal (terminal.Results |> List.map (fun (_, _, v) -> v)) [ Loop.ProviderRefused [ Loop.NotObserved ] ] "not observed"
        let again, more = Loop.updateProvider (Loop.ProviderGatesExecuted(List.map accepted model.Runs)) terminal
        Expect.equal again terminal "a repeated report after the verdict is inert"
        Expect.isEmpty more "nothing more")
]
