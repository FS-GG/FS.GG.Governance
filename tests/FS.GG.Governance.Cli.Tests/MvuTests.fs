module FS.GG.Governance.Cli.Tests.MvuTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Cli
open FS.GG.Governance.Adapters.SddHandoff

// Evidence obligations (Principle IV/V): these tests exercise the public Cli.init/update
// transition and assert emitted effects as values. No filesystem, process, network, clock, or
// fake judge is touched here; real edge evidence lives in Snapshot/Packaging/ReadOnly tests.

let request =
    match Cli.parse [ "route"; "--root"; "." ] with
    | Ok request -> request
    | Error errors -> failwithf "%A" errors

let snapshot =
    { Root = "."
      Supplied = []
      Change = { SpecKit = None; DesignSystem = None; Scope = [] }
      Artifacts = []
      Handoffs = [] }

let hostModel: FS.GG.Governance.Host.Model<ProjectFact> =
    { Phase = FS.GG.Governance.Host.Phase.Quiescent
      Facts = []
      Route = { Stakes = Routine; Advisory = []; Blocking = []; Reason = "light" }
      Pending = Set.empty
      Disclosures = []
      Failures = []
      Rounds = 0 }

let emptyBudget =
    { Requested = []
      CacheHits = []
      CacheMisses = []
      FreshDispatches = []
      Pending = []
      BudgetExhausted = [] }

// F081 wiring (FR-002/FR-003): a produced governance-handoff.json drives the `route` exit. The host
// Route is benign (no blocking failure) here, so the ONLY blocking source is the consumed handoff —
// proving the handoff itself flips the exit, not some unrelated rule.
let private handoffRead (json: string) : Reader.HandoffRead =
    { Source = "readiness/wi-1/governance-handoff.json"; Json = json }

let private failingHandoff =
    handoffRead
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "test:unit", "state": "failed" } ], "dependencies": [] } }"""

let private passingHandoff =
    handoffRead
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "test:unit", "state": "real" } ], "dependencies": [] } }"""

// Drive parse → snapshot(with handoffs) → host-completion and recover the route exit from WriteOutput.
let private routeExit (mode: string) (handoffs: Reader.HandoffRead list) : ExitDecision =
    let argv = [ "route"; "--root"; "."; "--mode"; mode ]
    let req = match Cli.parse argv with | Ok r -> r | Error e -> failwithf "%A" e
    let model, _ = Cli.init argv
    let model, _ = Cli.update (SnapshotLoaded(Ok { snapshot with Handoffs = handoffs })) model
    let _, effects = Cli.update (HostCompleted(hostModel, emptyBudget)) model
    effects
    |> List.tryPick (function | WriteOutput (_, result) -> Some result.Exit | _ -> None)
    |> Option.defaultWith (fun () -> failwithf "no WriteOutput effect (req=%A)" req.Command)

[<Tests>]
let handoffExitTests =
    testList
        "Cli handoff route exit"
        [ test "failing handoff blocks at --mode gate (GovernedBlocking, exit 2)" {
              Expect.equal (routeExit "gate" [ failingHandoff ]) GovernedBlocking "failing handoff must block the gate"
          }

          test "passing handoff passes at --mode gate (Success, exit 0)" {
              Expect.equal (routeExit "gate" [ passingHandoff ]) Success "an all-real handoff is advisory, not blocking"
          }

          test "failing handoff does NOT block in light mode (inner ⇒ Success)" {
              Expect.equal (routeExit "inner" [ failingHandoff ]) Success "light/non-strict mode never blocks on the handoff"
          }

          test "failing handoff does NOT block in light mode (sandbox ⇒ Success)" {
              Expect.equal (routeExit "sandbox" [ failingHandoff ]) Success "sandbox is light; the failing handoff is advisory"
          }

          test "no handoff ⇒ Success at gate (byte-identical, green-by-omission only when truly nothing to enforce)" {
              Expect.equal (routeExit "gate" []) Success "no handoff present ⇒ the route is unaffected"
          }

          test "a malformed handoff document blocks at gate (bad doc ⇒ blocking integrity gate)" {
              Expect.equal (routeExit "gate" [ handoffRead "{ not json" ]) GovernedBlocking "a bad document is a blocking integrity gate, never silently ignored"
          }]

[<Tests>]
let tests =
    testList
        "Cli MVU"
        [ test "init emits LoadSnapshot for a parsed command" {
              let model, effects = Cli.init [ "route"; "--root"; "." ]
              Expect.equal model.Phase LoadingSnapshot "phase"
              Expect.equal effects [ LoadSnapshot request ] "effect"
          }

          test "parse failure finishes with UsageError" {
              let model, effects = Cli.init [ "nope" ]
              Expect.equal model.Phase Done "done"
              Expect.isTrue (effects |> List.exists (function | Finish (UsageError _) -> true | _ -> false)) "finish usage"
          }

          test "snapshot success emits RunHost and host completion emits WriteOutput" {
              let model, _ = Cli.init [ "route"; "--root"; "." ]
              let model, effects = Cli.update (SnapshotLoaded(Ok snapshot)) model
              Expect.equal model.Phase RunningHost "running"
              Expect.equal effects [ RunHost(request, snapshot) ] "run host"

              let model, effects = Cli.update (HostCompleted(hostModel, emptyBudget)) model
              Expect.equal model.Phase RenderingOutput "rendering"
              Expect.isTrue (effects |> List.exists (function | WriteOutput _ -> true | _ -> false)) "write output"
          }

          test "snapshot and output failures become distinct exit decisions" {
              let model, _ = Cli.init [ "route"; "--root"; "." ]
              let model, effects = Cli.update (SnapshotLoaded(Error "missing root")) model
              Expect.equal model.Phase RenderingOutput "input failure still renders"
              Expect.isTrue (effects |> List.exists (function | WriteOutput (_, result) -> result.Exit = InputUnavailable "missing root" | _ -> false)) "input unavailable"

              let model, _ = Cli.update (OutputWritten(Error "disk full")) model
              Expect.equal model.Phase Done "done"
              Expect.isTrue (model.Result |> Option.exists (fun result -> result.Exit = ToolError "disk full")) "tool error"
          } ]
