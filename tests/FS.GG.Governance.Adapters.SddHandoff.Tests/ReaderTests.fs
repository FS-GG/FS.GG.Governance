module FS.GG.Governance.Adapters.SddHandoff.Tests.ReaderTests

open Expecto
open FS.GG.Governance.Adapters.SddHandoff
open FS.GG.Governance.Adapters.SddHandoff.Model
open System.IO

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
        [ test "well-formed v2.x handoff parses to Ok with every node state round-tripping" {
              match Reader.parse (Fixtures.read "satisfied") with
              | Error d -> failtestf "expected Ok, got Error %A" d
              | Ok h ->
                  Expect.equal h.ContractVersion "2.0.0" "contract version carried"
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

          test "unknown contractVersion major (3.0.0) yields VersionMismatch (FR-002)" {
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

          test "a malformed dependency edge is REJECTED as Malformed, not silently dropped (ADPT-2)" {
              // AutoSynthetic taint flows along dependency edges; a dropped edge could leave a
              // downstream verdict resting on a synthetic node un-tainted. Every malformed v2 object
              // shape must fail the whole handoff, mirroring the strict node fold.
              let edge (dep: string) =
                  sprintf
                      """{ "contractVersion": "2.0.0",
                           "evidence": { "nodes": [ { "id": "a", "state": "real" } ], "dependencies": [ %s ] } }"""
                      dep

              let cases =
                  [ """{ "dependent": "a" }""", "missing dependency"
                    """{ "dependency": "b" }""", "missing dependent"
                    """{ "dependent": "a", "dependency": 5 }""", "non-string member"
                    """[ "a", "b" ]""", "legacy tuple"
                    "\"a:b\"", "scalar in place of an object" ]

              for dep, label in cases do
                  let r = Reader.parse { Source = "x"; Json = edge dep }
                  Expect.equal (causeOf r) (Some Malformed) (sprintf "%s → Malformed, not dropped" label)
          }

          test "a present-but-non-array 'dependencies' is Malformed (ADPT-2)" {
              let json =
                  """{ "contractVersion": "2.0.0",
                       "evidence": { "nodes": [ { "id": "a", "state": "real" } ], "dependencies": {} } }"""

              let r = Reader.parse { Source = "x"; Json = json }
              Expect.equal (causeOf r) (Some Malformed) "non-array dependencies → Malformed"
          }

          test "an explicit-null or absent 'dependencies' is accepted as no edges (ADPT-2)" {
              // `dependencies` is optional and carries no edges to drop, so null/absent must NOT be
              // rejected — only a present, malformed *value* fails closed.
              let nullDeps =
                  """{ "contractVersion": "2.0.0",
                       "evidence": { "nodes": [ { "id": "a", "state": "real" } ], "dependencies": null } }"""

              let absentDeps =
                  """{ "contractVersion": "2.0.0",
                       "evidence": { "nodes": [ { "id": "a", "state": "real" } ] } }"""

              for json, label in [ nullDeps, "null"; absentDeps, "absent" ] do
                  match Reader.parse { Source = "x"; Json = json } with
                  | Error d -> failtestf "expected Ok for %s dependencies, got %A" label d
                  | Ok h -> Expect.isEmpty h.Evidence.Dependencies (sprintf "%s dependencies → no edges" label)
          }

          test "a well-formed dependency edge still round-trips (ADPT-2 happy path)" {
              let json =
                  """{ "contractVersion": "2.0.0",
                       "evidence": { "nodes": [ { "id": "a", "state": "real" } ],
                                     "dependencies": [
                                       { "dependent": "a", "dependency": "b" },
                                       { "dependent": "c", "dependency": "d" }
                                     ] } }"""

              match Reader.parse { Source = "x"; Json = json } with
              | Error d -> failtestf "expected Ok, got Error %A" d
              | Ok h -> Expect.equal h.Evidence.Dependencies [ ("a", "b"); ("c", "d") ] "edges carried in source order"
          }

          test "unknown additive (minor) fields are ignored" {
              let withExtra =
                  """{ "contractVersion": "2.4.0", "schemaVersion": 1,
                       "futureField": { "anything": 1 },
                       "evidence": { "nodes": [ { "id": "a", "state": "real", "newNodeField": true } ], "dependencies": [] } }"""
              match Reader.parse { Source = "x"; Json = withExtra } with
              | Error d -> failtestf "expected Ok ignoring unknown fields, got %A" d
              | Ok h -> Expect.equal h.ContractVersion "2.4.0" "minor 2.x accepted, unknown fields ignored"
          }

          test "real v2 projection parses typed performance evidence and flat governed references" {
              match Reader.parse (Fixtures.read "performance-v2") with
              | Error d -> failtestf "expected v2 producer-shaped fixture to parse, got %A" d
              | Ok handoff ->
                  Expect.equal handoff.PerformanceEvidence.Length 1 "typed performance item parsed"
                  Expect.equal handoff.PerformanceEvidence.Head.Intent.Value.Id "PI-001" "typed intent carried"
                  Expect.equal handoff.Evidence.Dependencies [ ("task:T-1", "evidence:EV-PERF") ] "object edge parsed"
                  Expect.equal handoff.GovernedReferences.Length 1 "flat governed reference parsed"
          }

          test "publish-smoke handoffs stay valid v2 fixtures" {
              // The release workflow consumes these separately from the adapter test fixtures. Keep
              // them behind the same strict Reader so a contract-version bump cannot leave a legacy
              // edge shape that turns the nominal passing smoke into a false release block.
              let smokeRoot =
                  Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "cli-publish-smoke", "fixtures"))

              let fixtures =
                  [ "failing-handoff", "wi-089-fail"
                    "light-failing-handoff", "wi-090-light"
                    "passing-handoff", "wi-089-pass" ]

              for fixture, workItem in fixtures do
                  let path =
                      Path.Combine(smokeRoot, fixture, "readiness", workItem, "governance-handoff.json")

                  let input: Reader.HandoffRead = { Source = path; Json = File.ReadAllText path }

                  match Reader.parse input with
                  | Error d -> failtestf "publish-smoke fixture %s must parse as v2: %A" fixture d
                  | Ok handoff ->
                      Expect.equal handoff.ContractVersion "2.0.0" (sprintf "%s uses the v2 contract" fixture)
                      Expect.equal
                          handoff.Evidence.Dependencies
                          [ ("test:unit", "build:lib") ]
                          (sprintf "%s carries the typed dependency edge" fixture)
          } ]
