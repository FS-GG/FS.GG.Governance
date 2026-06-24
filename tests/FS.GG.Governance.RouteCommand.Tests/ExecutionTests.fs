module FS.GG.Governance.RouteCommand.Tests.ExecutionTests

open System.IO
open Expecto
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// F052 US3 — `fsgg route` runs its selected command-gates and REPORTS each gate's execution outcome in
// route.json, but stays ADVISORY: it always exits 0 regardless of any gate's exit code, and never makes a
// merge decision. A reusable gate is reused (not re-run) on a second run. Driven through a deterministic fake
// port (and a REAL writable temp store for the reuse demo); no `Synthetic` outcome.

[<Tests>]
let tests =
    testList
        "Execution"
        [ test "US3: a gate that EXITS non-zero is reported in route.json, but the command still EXITS 0 (FR-008)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Json
              let cap = newCapture ()
              // A failing execution port — route must still exit 0 (advisory).
              let model = Interpreter.run (fakePortsExec validCatalog git fakeSensor absentStoreReader (fakeExecPortExiting 1) cap req) req

              Expect.equal model.Exit Loop.Success "route is advisory — always exit 0 even when a gate fails"
              let route = writtenOf cap Loop.RouteArtifact |> Option.map snd |> Option.defaultValue ""
              Expect.stringContains route "\"disposition\":\"executed\"" "each selected gate's execution outcome is reported"
              Expect.stringContains route "\"passed\":false" "the failing gate's outcome is reported (advisory, not enforced)"
          }

          test "US3: a passing gate is reported executed/passed; route still exits 0" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Json
              let cap = newCapture ()
              let model = Interpreter.run (fakePortsExec validCatalog git fakeSensor absentStoreReader fakeExecPort cap req) req

              Expect.equal model.Exit Loop.Success "advisory exit 0"
              let route = writtenOf cap Loop.RouteArtifact |> Option.map snd |> Option.defaultValue ""
              Expect.stringContains route "\"passed\":true" "the passing gate's outcome is reported"
          }

          test "US3: a reusable gate is NOT spawned a second time; route.json reports it reused (SC-003)" {
              withTempRepo (fun dir ->
                  let counter = { Calls = 0 }
                  let port = countingExecPort counter 0
                  let req =
                      match Loop.parse [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let ports = { Interpreter.realPorts req.Repo with Execute = port }

                  // Run 1 (empty store): executes + captures the selected command-gates.
                  let model1 = Interpreter.run ports req
                  let afterRun1 = counter.Calls
                  Expect.isGreaterThan afterRun1 0 "run 1 executes the selected command-gates"
                  Expect.equal model1.Exit Loop.Success "route exits 0"
                  Expect.stringContains (File.ReadAllText req.RouteOut) "\"disposition\":\"executed\"" "run 1 reports executed"

                  // Run 2 (same world, store from run 1): the gates are reusable ⇒ NOT spawned again.
                  let model2 = Interpreter.run ports req
                  Expect.equal counter.Calls afterRun1 "run 2 spawns NO new process (reused)"
                  Expect.equal model2.Exit Loop.Success "route still exits 0"
                  Expect.stringContains (File.ReadAllText req.RouteOut) "\"disposition\":\"reused\"" "run 2 reports the gates as reused")
          } ]
