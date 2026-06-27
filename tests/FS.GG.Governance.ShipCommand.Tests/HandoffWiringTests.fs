module FS.GG.Governance.ShipCommand.Tests.HandoffWiringTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// F081 US1 (SC-001): a produced handoff drives the ship verdict through the REAL
// Config→Gates→Routing→Route→Enforcement→Ship.rollup pipeline. Two products differing ONLY in their
// declared handoff evidence (all-satisfied vs a failed node) yield Pass vs Fail, with the blocking
// handoff evidence gate in `Blockers`. The handoff is the sole verdict driver (ExplicitPaths [] selects
// no routed gate), so the delta is traceable to the declared evidence — the handoff is no longer inert.

let private satisfiedJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "real" }, { "id": "test:unit", "state": "real" } ], "dependencies": [] } }"""

let private failingJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "real" }, { "id": "test:unit", "state": "failed" } ], "dependencies": [] } }"""

let private handoffRead json : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead =
    { Source = "readiness/wi-1/governance-handoff.json"; Json = json }

let private runWithHandoff json =
    let req = requestFor (Loop.ExplicitPaths []) Loop.Text
    let cap = newCapture ()
    let ports = { fakePorts validCatalog gitSrcChange cap req with Handoffs = fun _ -> [ handoffRead json ] }
    Interpreter.run ports req

let private blockerGateIds (d: ShipDecision) =
    d.Blockers
    |> List.choose (fun i ->
        match i.Id with
        | GateItem g -> Some(gateIdValue g)
        | FindingItem _ -> None)

[<Tests>]
let tests =
    testList
        "HandoffWiring"
        [ test "satisfied handoff ⇒ Pass; failing handoff ⇒ Fail with the blocking handoff evidence gate in Blockers (SC-001)" {
              let satisfied = runWithHandoff satisfiedJson
              let failing = runWithHandoff failingJson

              Expect.equal (Option.get satisfied.Decision).Verdict Pass "all-satisfied declared evidence ⇒ Pass"
              Expect.equal (Option.get failing.Decision).Verdict Fail "a failed declared node ⇒ Fail"

              Expect.exists
                  (blockerGateIds (Option.get failing.Decision))
                  (fun id -> id.Contains "sdd-handoff:evidence")
                  "the blocking handoff evidence gate appears in Blockers"
          }

          test "HandoffsLoaded keeps update pure and LoadHandoffs is the emitted effect (Principle IV)" {
              let req = requestFor (Loop.ExplicitPaths []) Loop.Text
              let m0, eff = Loop.init req
              Expect.contains eff (Loop.LoadHandoffs req.Repo) "init emits LoadHandoffs (the only handoff I/O effect)"

              let reads = [ handoffRead satisfiedJson ]
              let m1, eff1 = Loop.update (Loop.HandoffsLoaded reads) m0
              Expect.equal m1.Handoffs reads "HandoffsLoaded folds the reads into Model (pure)"
              Expect.isEmpty eff1 "HandoffsLoaded requests no further effect — parse/map happen in the pure Loaded fold"
          }

          test "absent handoff is a true no-op — the verdict matches a run with no Handoffs port result (SC-003)" {
              let req = requestFor (Loop.ExplicitPaths []) Loop.Text
              let cap = newCapture ()
              let withNone = Interpreter.run (fakePorts validCatalog gitSrcChange cap req) req
              // fakePorts already defaults Handoffs to (fun _ -> []) — an empty product has no handoff gates.
              Expect.equal (Option.get withNone.Decision).Verdict Pass "no handoff, no routed gate ⇒ Pass (identity fold)"
              Expect.isEmpty (blockerGateIds (Option.get withNone.Decision)) "no blockers without a handoff"
          } ]
