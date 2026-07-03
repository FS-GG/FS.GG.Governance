module FS.GG.Governance.GatesJson.Tests.TotalityTests

open System.Text.Json
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GatesJson
open FS.GG.Governance.GatesJson.Tests.Support

// US4 — total over any well-typed gate registry: ofGateRegistry returns a document for every
// GateRegistry F018 can produce (empty, single-gate, many-gate, mixed prerequisites and optional
// freshness commands) and never throws; the empty registry is a valid success. The FsCheck totality
// property generates its GateRegistrys by driving the REAL Gates.buildRegistry (research D7) — no
// directly-constructed (synthetic) values.

let private checkPool =
    [ check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
      check "build" "format" None Cheap Local Observe
      check "docs" "lint" None Cheap LocalOrCi Warn
      check "api" "surface" None High Ci BlockOnPr
      check "release" "audit" None Exhaustive Release BlockOnRelease ]

let private commandPool = [ command "dotnet-test" 600 ]

let private genRegistry : Gen<GateRegistry> =
    gen {
        let! picks = Gen.subListOf checkPool
        return registryFor picks commandPool
    }

let private config = { FsCheckConfig.defaultConfig with maxTest = 300; arbitrary = [] }

[<Tests>]
let tests =
    testList
        "Totality (US4)"
        [ test "the empty registry projects to a valid document with an empty gates array, never throwing (AS1, SC-006, FR-009)" {
              let empty = registryFor [] []
              let json = GatesJson.ofGateRegistry empty
              use doc = parse json
              Expect.equal doc.RootElement.ValueKind JsonValueKind.Object "a JSON object"
              Expect.equal (topLevelFieldOrder doc) [ "schemaVersion"; "gates" ] "fixed field order even when empty"
              Expect.isEmpty (gates doc) "gates present and empty"
          }

          test "a registry mixing present/absent prerequisites and Some/None freshness commands renders each gate's own shape (AS2)" {
              // build:tests has a command prereq + Some command; build:format has neither.
              let r = registryFor [ checkPool.[0]; checkPool.[1] ] commandPool
              use doc = parse (GatesJson.ofGateRegistry r)
              let tests = gateById doc "build:tests"
              let format = gateById doc "build:format"
              Expect.isNonEmpty (prerequisites tests) "build:tests carries its prerequisite"
              Expect.isEmpty (prerequisites format) "build:format carries none — no leak from build:tests"
              Expect.equal (tests.GetProperty("freshnessKey").GetProperty "command").ValueKind JsonValueKind.String "build:tests command present"
              Expect.equal (format.GetProperty("freshnessKey").GetProperty "command").ValueKind JsonValueKind.Null "build:format command null"
          }

          testPropertyWithConfig config "total: ofGateRegistry always returns a parseable document and never throws (AS3, SC-006)"
          <| Prop.forAll (Arb.fromGen genRegistry) (fun r ->
              // drive the real assembler, project, and parse — reaching here without an exception
              // proves totality; the parse proves the output is always well-formed JSON.
              let json = GatesJson.ofGateRegistry r
              use doc = JsonDocument.Parse json
              doc.RootElement.ValueKind = JsonValueKind.Object
              && (topLevelFieldOrder doc) = [ "schemaVersion"; "gates" ]) ]
