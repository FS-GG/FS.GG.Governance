module FS.GG.Governance.RouteCommand.Tests.HandoffRouteWiringTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// F081 US1 (FR-008/012, research D7): handoff gates appear as selected gates in gates.json / route.json,
// and the interpreter-side `Handoffs` port locates handoff files in stable `<id>` order (the impure edge
// the pure `Consumer` determinism assumes). The handoff is the sole verdict driver (ExplicitPaths []).

let private evidenceHandoffJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "real" }, { "id": "test:unit", "state": "failed" } ], "dependencies": [] } }"""

let private handoffRead source json : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead =
    { Source = source; Json = json }

let private withTempDir (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-handoff-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

[<Tests>]
let tests =
    testList
        "HandoffRouteWiring"
        [ test "handoff gates appear in gates.json and route.json's selected gates (FR-008)" {
              let req = requestFor (Loop.ExplicitPaths []) Loop.Text
              let cap = newCapture ()

              let ports =
                  { fakePorts validCatalog gitEmpty cap req with
                      Handoffs = fun _ -> [ handoffRead "readiness/wi-1/governance-handoff.json" evidenceHandoffJson ] }

              let model = Interpreter.run ports req

              Expect.exists
                  (model.SelectedGates |> List.map (fun g -> gateIdValue g.Id))
                  (fun id -> id.Contains "sdd-handoff:evidence")
                  "the handoff evidence gate is selected"

              let gatesDoc = Option.get model.GatesDoc
              let routeDoc = Option.get model.RouteDoc
              Expect.stringContains gatesDoc "sdd-handoff:evidence" "the handoff gate appears in gates.json (the registry)"
              Expect.stringContains routeDoc "sdd-handoff:evidence" "the handoff gate appears in route.json's selected gates"
          }

          test "the real Handoffs port returns documents in stable <id> order; an empty repo ⇒ [] (FR-012, Principle IV edge)" {
              withTempDir (fun dir ->
                  // Two handoff documents under distinct ids, created out of order.
                  for id in [ "bbb"; "aaa" ] do
                      let sub = Path.Combine(dir, "readiness", id)
                      Directory.CreateDirectory sub |> ignore
                      File.WriteAllText(Path.Combine(sub, "governance-handoff.json"), evidenceHandoffJson)

                  let reads = (Interpreter.realPorts dir).Handoffs dir

                  Expect.equal
                      (reads |> List.map (fun r -> r.Source))
                      [ "readiness/aaa/governance-handoff.json"; "readiness/bbb/governance-handoff.json" ]
                      "the port reads in stable ordinal <id> order regardless of filesystem order")

              withTempDir (fun dir ->
                  Expect.isEmpty ((Interpreter.realPorts dir).Handoffs dir) "a repo with no readiness/ handoffs ⇒ [] (the no-op path)")
          } ]
