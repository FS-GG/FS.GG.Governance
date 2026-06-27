module FS.GG.Governance.Adapters.SddHandoff.Tests.ConsumerTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Adapters.SddHandoff
open FS.GG.Governance.Adapters.SddHandoff.Model

// US1 — parse→gates over fixtures (FR-008/010/011/012). The Consumer is the bridge to the verdict:
// a failing/blocking declaration ⇒ a blocking gate; a satisfied one ⇒ advisory; a bad document ⇒ a
// blocking integrity gate + diagnostic and NO mapped gate (no partial enforce).

let private gateIds (r: Consumer.ConsumeResult) =
    r.Gates |> List.map (fun g -> gateIdValue g.Id)

let private maturityOf (r: Consumer.ConsumeResult) (substr: string) =
    r.Gates
    |> List.tryFind (fun g -> (gateIdValue g.Id).Contains substr)
    |> Option.map (fun g -> g.Maturity)

[<Tests>]
let tests =
    testList
        "Consumer"
        [ test "satisfied handoff ⇒ an advisory (warn) evidence gate" {
              let r = Consumer.consume [ Fixtures.read "satisfied" ]
              Expect.equal (maturityOf r "evidence") (Some Warn) "all-satisfied evidence ⇒ advisory warn"
          }

          test "failing handoff ⇒ a blocking (block-on-ship) evidence gate (FR-008)" {
              let r = Consumer.consume [ Fixtures.read "failing" ]
              Expect.equal (maturityOf r "evidence") (Some BlockOnShip) "a failed node ⇒ blocking evidence gate"
          }

          test "a bad document ⇒ a blocking integrity gate + diagnostic and NO mapped evidence gate (FR-011)" {
              let r = Consumer.consume [ Fixtures.read "malformed" ]
              let ids = gateIds r
              Expect.exists ids (fun id -> id.Contains "integrity") "a blocking integrity gate is present"
              Expect.isFalse (ids |> List.exists (fun id -> id.Contains "evidence")) "no mapped evidence gate for a bad document"
              Expect.equal (maturityOf r "integrity") (Some BlockOnShip) "the integrity gate is blocking"
              Expect.isNonEmpty r.Diagnostics "a diagnostic is surfaced"
          }

          test "zero handoffs ⇒ empty ConsumeResult (the no-op path, SC-003)" {
              let r = Consumer.consume []
              Expect.isEmpty r.Gates "no gates"
              Expect.isEmpty r.Selected "no selected gates"
              Expect.isEmpty r.Diagnostics "no diagnostics"
          }

          test "every gate is pre-selected — Gates and Selected align (research D3)" {
              let r = Consumer.consume [ Fixtures.read "failing" ]
              let gateOrder = r.Gates |> List.map (fun g -> gateIdValue g.Id)
              let selOrder = r.Selected |> List.map (fun sg -> gateIdValue sg.Gate.Id)
              Expect.equal selOrder gateOrder "Selected mirrors Gates (pre-selected), in GateId order"
          }

          test "multiple handoffs load in <id> order with gates sorted by GateId (FR-012, research D7)" {
              // Two documents under distinct ids; order of input must not change the deterministic output.
              let a = { Fixtures.read "satisfied" with Source = "readiness/aaa/governance-handoff.json" }
              let b = { Fixtures.read "failing" with Source = "readiness/bbb/governance-handoff.json" }
              let forward = Consumer.consume [ a; b ] |> gateIds
              let reversed = Consumer.consume [ b; a ] |> gateIds
              Expect.equal forward reversed "gate set is independent of input order"
              Expect.equal forward (List.sort forward) "gates are sorted by GateId"
          }

          test "governedReferences absent vs present does not change correctness (FR-010)" {
              // satisfied.json carries governedReferences; strip them and confirm the evidence maturity is identical.
              let withRefs = Consumer.consume [ Fixtures.read "satisfied" ]

              let strippedJson =
                  """{ "contractVersion": "1.0.0", "schemaVersion": 1,
                       "evidence": { "nodes": [ { "id": "build:lib", "state": "real" }, { "id": "test:unit", "state": "real" }, { "id": "doc:api", "state": "skipped" } ], "dependencies": [ ["test:unit","build:lib"] ] } }"""

              let withoutRefs =
                  Consumer.consume [ { Source = "readiness/satisfied/governance-handoff.json"; Json = strippedJson } ]

              Expect.equal
                  (maturityOf withoutRefs "evidence")
                  (maturityOf withRefs "evidence")
                  "evidence-gate correctness is independent of governedReferences"
          }

          test "a synthetic-tainted handoff ⇒ a blocking evidence gate (taint closure, research D4)" {
              let json =
                  """{ "contractVersion": "1.0.0", "schemaVersion": 1,
                       "evidence": { "nodes": [ { "id": "build:lib", "state": "synthetic" }, { "id": "test:unit", "state": "real" } ],
                                     "dependencies": [ ["test:unit","build:lib"] ] } }"""

              let r = Consumer.consume [ { Source = "readiness/taint/governance-handoff.json"; Json = json } ]
              Expect.equal (maturityOf r "evidence") (Some BlockOnShip) "a Real node resting on Synthetic taints AutoSynthetic ⇒ blocking"
          } ]
