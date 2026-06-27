module FS.GG.Governance.Adapters.SddHandoff.Tests.ReaderTests

open Expecto
open FS.GG.Governance.Adapters.SddHandoff
open FS.GG.Governance.Adapters.SddHandoff.Model

// US2 — safe read + version-check the contract (FR-002/005/011, SC-004). `Reader.parse` is pure,
// total, never throws: an unknown major / malformed / missing-required / declared-autoSynthetic each
// yields a distinct, descriptive diagnostic and NO mapped result.

let private causeOf (r: Result<Handoff, Diagnostic>) =
    match r with
    | Ok _ -> None
    | Error d -> Some d.Cause

let private messageOf (r: Result<Handoff, Diagnostic>) =
    match r with
    | Ok _ -> ""
    | Error d -> d.Message

[<Tests>]
let tests =
    testList
        "Reader"
        [ test "well-formed v1.x handoff parses to Ok with every node state round-tripping" {
              match Reader.parse (Fixtures.read "satisfied") with
              | Error d -> failtestf "expected Ok, got Error %A" d
              | Ok h ->
                  Expect.equal h.ContractVersion "1.0.0" "contract version carried"
                  Expect.equal h.SchemaVersion 1 "schema version carried"
                  let states = h.Evidence.Nodes |> List.map (fun n -> n.State)
                  Expect.contains states Real "real node round-trips"
                  Expect.contains states Skipped "skipped node round-trips"
                  Expect.equal h.Evidence.Dependencies [ ("test:unit", "build:lib") ] "dependency edge round-trips"
                  Expect.isSome h.Readiness "readiness block present"
          }

          test "every declared evidence-state token round-trips through parse (FR-003/004)" {
              // pending/real/synthetic/failed/skipped straight-through; deferred/accepted-deferral map at
              // the Mapping layer, but Reader must accept and carry them as DeclaredState tokens.
              match Reader.parse (Fixtures.read "deferred") with
              | Error d -> failtestf "expected Ok, got Error %A" d
              | Ok h ->
                  let byId id = h.Evidence.Nodes |> List.find (fun n -> n.Id = id)
                  Expect.equal (byId "doc:api").State Deferred "deferred token parsed"
                  Expect.equal (byId "perf:bench").State AcceptedDeferral "accepted-deferral token parsed"
          }

          test "unknown contractVersion major (2.0.0) yields VersionMismatch (FR-002)" {
              let r = Reader.parse (Fixtures.read "v2-major")
              Expect.equal (causeOf r) (Some VersionMismatch) "version-mismatch cause"
          }

          test "malformed JSON yields Malformed and never throws (FR-011)" {
              let r = Reader.parse (Fixtures.read "malformed")
              Expect.equal (causeOf r) (Some Malformed) "malformed cause"
          }

          test "missing required field yields Malformed (FR-011)" {
              let r = Reader.parse (Fixtures.read "missing-required")
              Expect.equal (causeOf r) (Some Malformed) "missing-required → malformed cause"
          }

          test "a node declaring state autoSynthetic yields AutoSyntheticDeclared (FR-005)" {
              let r = Reader.parse (Fixtures.read "autoSynthetic")
              Expect.equal (causeOf r) (Some AutoSyntheticDeclared) "autoSynthetic declared is its own distinct cause"
          }

          test "diagnostic messages are distinct per cause (SC-004)" {
              let vm = messageOf (Reader.parse (Fixtures.read "v2-major"))
              let mal = messageOf (Reader.parse (Fixtures.read "malformed"))
              let auto = messageOf (Reader.parse (Fixtures.read "autoSynthetic"))
              Expect.isFalse (vm = mal) "version-mismatch vs malformed messages differ"
              Expect.isFalse (vm = auto) "version-mismatch vs autoSynthetic messages differ"
              Expect.isFalse (mal = auto) "malformed vs autoSynthetic messages differ"
              Expect.isNotEmpty vm "version-mismatch message is descriptive"
              Expect.isNotEmpty mal "malformed message is descriptive"
              Expect.isNotEmpty auto "autoSynthetic message is descriptive"
          }

          test "parse never throws on garbage input" {
              let r = Reader.parse { Source = "x"; Json = "  not json at all }{" }
              Expect.equal (causeOf r) (Some Malformed) "garbage → Malformed, no throw"
          }

          test "unknown additive (minor) fields are ignored" {
              let withExtra =
                  """{ "contractVersion": "1.4.0", "schemaVersion": 1,
                       "futureField": { "anything": 1 },
                       "evidence": { "nodes": [ { "id": "a", "state": "real", "newNodeField": true } ], "dependencies": [] } }"""
              match Reader.parse { Source = "x"; Json = withExtra } with
              | Error d -> failtestf "expected Ok ignoring unknown fields, got %A" d
              | Ok h -> Expect.equal h.ContractVersion "1.4.0" "minor 1.x accepted, unknown fields ignored"
          } ]
