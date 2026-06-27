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

// F082 C1–C8 — `Consumer.candidatePaths` projects the declared `governedReferences` of every
// CONSUMABLE document into de-duplicated, deterministically-ordered routing candidates. A document
// `Reader.parse` refuses contributes nothing (FR-008). Pure unit tests over hand-built JSON (no I/O).

let private mk (id: string) (json: string) : Reader.HandoffRead =
    { Source = sprintf "readiness/%s/governance-handoff.json" id; Json = json }

// A consumable v1 document declaring the given `governedReferences` work items.
let private docDeclaring (refsJson: string) : string =
    sprintf
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
             "governedReferences": %s }"""
        refsJson

[<Tests>]
let candidatePathsTests =
    testList
        "Consumer.candidatePaths"
        [ test "C1 — [] ⇒ [] (the no-op path)" {
              Expect.isEmpty (Consumer.candidatePaths []) "no documents ⇒ no candidates"
          }

          test "C2 — a consumable doc with no governedReferences ⇒ []" {
              let doc = mk "wi" (docDeclaring "[]")
              Expect.isEmpty (Consumer.candidatePaths [ doc ]) "no declared references ⇒ no candidates"
          }

          test "C3 — declared src/A/x, tests/A/y ⇒ normalized + sorted" {
              let doc = mk "wi" (docDeclaring """[ { "workItem": "WI-1", "paths": [ "src/A/x", "tests/A/y" ] } ]""")
              Expect.equal
                  (Consumer.candidatePaths [ doc ])
                  [ GovernedPath "src/A/x"; GovernedPath "tests/A/y" ]
                  "the declared paths, normalized and ordinal-sorted"
          }

          test "C4 — two consumable docs with overlapping paths ⇒ union, de-duplicated" {
              let a = mk "aaa" (docDeclaring """[ { "workItem": "WI-1", "paths": [ "src/A/x", "tests/A/y" ] } ]""")
              let b = mk "bbb" (docDeclaring """[ { "workItem": "WI-2", "paths": [ "src/A/x", "src/B/z" ] } ]""")
              Expect.equal
                  (Consumer.candidatePaths [ a; b ])
                  [ GovernedPath "src/A/x"; GovernedPath "src/B/z"; GovernedPath "tests/A/y" ]
                  "the union of declared paths, each once, sorted"
          }

          test "C5 — a consumable + a malformed doc ⇒ only the consumable's paths (FR-008)" {
              let good = mk "good" (docDeclaring """[ { "workItem": "WI-1", "paths": [ "src/A/x" ] } ]""")
              let bad = Fixtures.read "malformed"
              Expect.equal
                  (Consumer.candidatePaths [ good; bad ])
                  [ GovernedPath "src/A/x" ]
                  "the malformed document contributes no candidates"
          }

          test "C6 — a single version-mismatch doc ⇒ [] (FR-008)" {
              Expect.isEmpty (Consumer.candidatePaths [ Fixtures.read "v2-major" ]) "an unsupported major contributes no candidates"
          }

          test "C7 — the same path declared twice across work items ⇒ one entry" {
              let doc =
                  mk
                      "wi"
                      (docDeclaring """[ { "workItem": "WI-1", "paths": [ "src/A/x" ] }, { "workItem": "WI-2", "paths": [ "src/A/x" ] } ]""")
              Expect.equal (Consumer.candidatePaths [ doc ]) [ GovernedPath "src/A/x" ] "a path declared twice survives once"
          }

          test "C8 — a back-slash raw path ⇒ normalized via Reader.parse" {
              let doc = mk "wi" (docDeclaring """[ { "workItem": "WI-1", "paths": [ "src\\A\\x" ] } ]""")
              Expect.equal (Consumer.candidatePaths [ doc ]) [ GovernedPath "src/A/x" ] "back-slashes normalize to forward-slashes"
          } ]
