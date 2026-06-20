module FS.GG.Governance.RouteJson.Tests.TotalityTests

open System.Text.Json
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// US4 — total over any well-typed route result: ofRouteResult returns a document for every
// RouteResult the upstream rows can produce (empty, single-gate, many-gate, findings-only) and never
// throws; the empty and findings-only routes are valid successes. The FsCheck totality property
// generates its RouteResults by driving the REAL F015->F017->F018->F019 chain (research D7) — no
// directly-constructed (synthetic) values.

let private fixtureFacts =
    facts
        "src"
        [ "src/build/**", "build"
          "src/docs/**", "docs" ]
        [ surface GovernedRoot "root" [ "src" ]
          surface ProtectedSurface "api-surface" [ "src/api" ] ]
        [ check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
          check "build" "format" None Cheap Local Observe
          check "docs" "lint" None Cheap LocalOrCi Warn ]
        [ command "dotnet-test" 600 ]

let private pool =
    [ "src/build/A.fs"
      "src/build/B.fs"
      "src/docs/G.md"
      "src/api/secret.fs"
      "src/loose/x.fs"
      "../outside/y.fs" ]

let private genPaths : Gen<string list> =
    gen {
        let! picks = Gen.listOf (Gen.elements pool)
        return picks
    }

let private config = { FsCheckConfig.defaultConfig with maxTest = 300; arbitrary = [] }

let private allZeroCost (doc: JsonDocument) =
    [ "cheap"; "medium"; "high"; "exhaustive" ] |> List.forall (fun t -> costTier doc t = 0)

[<Tests>]
let tests =
    testList
        "Totality (US4)"
        [ test "the empty route projects to a valid document with empty sections + all-zero cost (AS1, SC-006, FR-009)" {
              let empty = resultOf fixtureFacts []
              let json = RouteJson.ofRouteResult empty
              use doc = parse json
              Expect.equal doc.RootElement.ValueKind JsonValueKind.Object "a JSON object"
              Expect.isEmpty (selectedGates doc) "selectedGates present and empty"
              Expect.isEmpty (findings doc) "findings present and empty"
              Expect.isTrue (allZeroCost doc) "all-zero cost"
          }

          test "a findings-only route projects with both sections coexisting (AS2)" {
              // only an unclassified in-root path → no gates selected, but a finding present
              let r = resultOf fixtureFacts [ "src/loose/x.fs" ]
              Expect.isEmpty r.SelectedGates "no gates selected"
              Expect.isNonEmpty r.Findings.Findings "a finding is present"
              use doc = parse (RouteJson.ofRouteResult r)
              Expect.isEmpty (selectedGates doc) "selectedGates present and empty"
              Expect.isNonEmpty (findings doc) "findings present and non-empty"
          }

          testPropertyWithConfig config "total: ofRouteResult always returns a parseable document and never throws (AS3, SC-006)"
          <| Prop.forAll (Arb.fromGen genPaths) (fun paths ->
              // drive the real chain, project, and parse — reaching here without an exception proves
              // totality; the parse proves the output is always well-formed JSON.
              let r = resultOf fixtureFacts paths
              let json = RouteJson.ofRouteResult r
              use doc = JsonDocument.Parse json
              doc.RootElement.ValueKind = JsonValueKind.Object
              && (topLevelFieldOrder doc) = [ "schemaVersion"; "selectedGates"; "findings"; "cost" ]) ]
