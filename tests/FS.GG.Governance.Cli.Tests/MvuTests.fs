module FS.GG.Governance.Cli.Tests.MvuTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Cli

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
      Artifacts = [] }

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
