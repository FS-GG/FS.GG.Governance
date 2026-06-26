module FS.GG.Governance.EvidenceJson.Tests.ProjectionTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceJson.Tests.Support

// US1 — a well-formed document lists every node once with BOTH declared and effective state, in ascending id
// order, under a versioned `schemaVersion`; the empty document is a valid success (contract C1/C2, INV-1/2/5).

[<Tests>]
let tests =
    testList
        "Projection"
        [ test "schemaVersion is the fixed fsgg.evidence/v1 constant" {
              Expect.equal EvidenceJson.schemaVersion "fsgg.evidence/v1" "schemaVersion constant"

              let root = parse (wellFormed [] [] [])
              Expect.equal (strProp "schemaVersion" root) "fsgg.evidence/v1" "stamped schemaVersion"
          }

          test "well-formed document carries graphFailure: null and a nodes array" {
              let root = parse (wellFormed [ mkNode "a" Real Real NodeFreshness.Fresh "speckit" ] [] [])
              Expect.equal (root.GetProperty("graphFailure").ValueKind) JsonValueKind.Null "graphFailure null"
              Expect.equal (root.GetProperty("nodes").ValueKind) JsonValueKind.Array "nodes array present"
          }

          test "nodes are emitted in ascending id order regardless of input order (INV-4)" {
              let nodes =
                  [ mkNode "speckit:T3" Real Real NodeFreshness.Fresh "speckit"
                    mkNode "speckit:T1" Real Real NodeFreshness.Fresh "speckit"
                    mkNode "speckit:T2" Pending Pending NodeFreshness.Unknown "speckit" ]

              let root = parse (wellFormed nodes [] [])

              let ids =
                  [ for n in root.GetProperty("nodes").EnumerateArray() -> strProp "id" n ]

              Expect.equal ids [ "speckit:T1"; "speckit:T2"; "speckit:T3" ] "ascending by id"
          }

          test "each node shows BOTH declared and effective; taint is the visible delta (FR-002, INV-1)" {
              // A node declared Real that the closure demoted to AutoSynthetic keeps declared=Real.
              let root = parse (wellFormed [ mkNode "a" Real AutoSynthetic NodeFreshness.Fresh "speckit" ] [] [])
              let node = root.GetProperty("nodes").[0]
              Expect.equal (strProp "declared" node) "Real" "declared kept verbatim"
              Expect.equal (strProp "effective" node) "AutoSynthetic" "effective shows the taint"
              Expect.equal (strProp "source" node) "speckit" "source carried"
          }

          test "Skipped renders as a distinct token from Failed and Pending (FR-005, INV-2)" {
              let nodes =
                  [ mkNode "s" Skipped Skipped NodeFreshness.Unknown "speckit"
                    mkNode "f" Failed Failed NodeFreshness.Unknown "speckit"
                    mkNode "p" Pending Pending NodeFreshness.Unknown "speckit" ]

              let root = parse (wellFormed nodes [] [])

              let tok id =
                  root.GetProperty("nodes").EnumerateArray()
                  |> Seq.find (fun n -> strProp "id" n = id)
                  |> strProp "declared"

              Expect.equal (tok "s") "Skipped" "Skipped token"
              Expect.equal (tok "f") "Failed" "Failed token"
              Expect.equal (tok "p") "Pending" "Pending token"
              Expect.isTrue (tok "s" <> tok "f" && tok "s" <> tok "p") "Skipped distinct"
          }

          test "empty well-formed document renders nodes: [] as a success (FR-010, INV-5)" {
              let root = parse (wellFormed [] [] [])
              Expect.equal (root.GetProperty("nodes").GetArrayLength()) 0 "empty nodes array"
              Expect.equal (root.GetProperty("dependencies").GetArrayLength()) 0 "empty dependencies array"
              Expect.equal (root.GetProperty("disclosures").GetArrayLength()) 0 "empty disclosures array"
          }

          test "dependencies and disclosures carry their entries with the documented field names" {
              let root =
                  parse (
                      wellFormed
                          [ mkNode "a" Real Real NodeFreshness.Fresh "speckit" ]
                          [ "a", "b" ]
                          [ "RULE-1", "justified by fixture" ]
                  )

              let dep = root.GetProperty("dependencies").[0]
              Expect.equal (strProp "dependent" dep) "a" "dependent field"
              Expect.equal (strProp "dependency" dep) "b" "dependency field"

              let disc = root.GetProperty("disclosures").[0]
              Expect.equal (strProp "rule" disc) "RULE-1" "rule field"
              Expect.equal (strProp "justification" disc) "justified by fixture" "justification field"
          } ]
