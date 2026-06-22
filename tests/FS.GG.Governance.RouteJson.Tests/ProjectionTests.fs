module FS.GG.Governance.RouteJson.Tests.ProjectionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// US1 — render a real route result to a deterministic route.json: each selected gate by its
// declared id with carried F018 metadata + route trace; no non-selected gate; complete cost rollup.
// Every test projects a REAL `RouteResult` from the genuine F015->F017->F018->F019 chain and inspects
// the EMITTED BYTES via JsonDocument (research D7).

// ── shared real fixture: two build gates (each reached by two paths) + one docs gate + one
//    release gate no change reaches; one unclassified in-root path → one finding ──

let private fixtureFacts : TypedFacts =
    facts
        "src"
        [ "src/build/**", "build"
          "src/docs/**", "docs"
          "src/release/**", "release" ]
        [ surface GovernedRoot "root" [ "src" ] ]
        [ check "build" "format" None Cheap Local Observe
          check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
          check "docs" "lint" None Cheap Local Warn
          check "release" "publish" None High Release BlockOnRelease ]
        [ command "dotnet-test" 600 ]

let private fixturePaths =
    [ "src/build/a.fs"; "src/build/b.fs"; "src/docs/Guide.md"; "src/loose/x.fs" ]

let private fixtureResult = resultOf fixtureFacts fixturePaths

/// Find the emitted gate object with the given declared id.
let private gateById (doc: System.Text.Json.JsonDocument) (id: string) =
    selectedGates doc |> List.find (fun g -> strField g "id" = id)

[<Tests>]
let tests =
    testList
        "Projection (US1)"
        [ test "every selected gate is present exactly once by declared id with carried metadata verbatim (SC-001)" {
              use doc = parse (RouteJson.ofRouteResult fixtureResult None)

              // one emitted gate per real SelectedGate, by declared id, in the same (GateId) order
              let emittedIds = selectedGateIds doc
              let expectedIds = fixtureResult.SelectedGates |> List.map (fun sg -> gateIdValue sg.Gate.Id)
              Expect.equal emittedIds expectedIds "emitted gate ids match the result's selected gates, in order"
              Expect.equal (List.length emittedIds) (List.length (List.distinct emittedIds)) "each gate appears exactly once"

              // each carried metadata field matches the embedded F018 Gate verbatim
              for sg in fixtureResult.SelectedGates do
                  let gate = sg.Gate
                  let g = gateById doc (gateIdValue gate.Id)
                  let (DomainId domain) = gate.Domain
                  let (Owner owner) = gate.Owner
                  let (TimeoutLimit secs) = gate.Timeout
                  Expect.equal (strField g "domain") domain "domain verbatim"
                  Expect.equal (strField g "description") gate.Description "description verbatim"
                  Expect.equal (g.GetProperty("timeout").GetInt32()) secs "timeout (int seconds) verbatim"
                  Expect.equal (strField g "owner") owner "owner verbatim"
                  Expect.equal (g.GetProperty("productCheck").GetBoolean()) gate.ProductCheck "productCheck verbatim"
          }

          test "build:tests carries its declared cost/maturity tokens and its command prerequisite" {
              use doc = parse (RouteJson.ofRouteResult fixtureResult None)
              let g = gateById doc "build:tests"
              Expect.equal (strField g "cost") "medium" "declared cost token"
              Expect.equal (strField g "maturity") "blockOnShip" "declared maturity carried verbatim (not enforcement)"
              Expect.equal (g.GetProperty("timeout").GetInt32()) 600 "timeout resolved from the referenced command"

              let prereqs =
                  [ for p in g.GetProperty("prerequisites").EnumerateArray() -> strField p "requiresCommand" ]

              Expect.equal prereqs [ "dotnet-test" ] "the command prerequisite is rendered"

              // a command-less gate renders an empty prerequisites array
              let fmt = gateById doc "build:format"
              Expect.isEmpty [ for p in fmt.GetProperty("prerequisites").EnumerateArray() -> p ] "no command → empty prerequisites"
          }

          test "a gate reached by >=2 paths appears once with all selecting paths in normalized-path order (FR-004)" {
              use doc = parse (RouteJson.ofRouteResult fixtureResult None)
              let g = gateById doc "build:tests"
              let emitted = selectingPaths g

              let expected =
                  fixtureResult.SelectedGates
                  |> List.find (fun sg -> gateIdValue sg.Gate.Id = "build:tests")
                  |> fun sg ->
                      sg.SelectingPaths
                      |> List.map (fun sp ->
                          let (GovernedPath p) = sp.Path
                          let (GovernedPath m) = sp.MatchedGlob
                          p, m)

              Expect.equal emitted expected "all selecting paths carried in the result's normalized-path order"
              Expect.equal (List.length emitted) 2 "the two build paths both reached this gate"
          }

          test "no gate the result did not select appears; nothing is invented (FR-003)" {
              use doc = parse (RouteJson.ofRouteResult fixtureResult None)
              let emitted = selectedGateIds doc |> Set.ofList
              // release:publish exists in the registry but no path routes to it → must be absent
              Expect.isFalse (emitted.Contains "release:publish") "an unreached registry gate is absent"
              let expected = fixtureResult.SelectedGates |> List.map (fun sg -> gateIdValue sg.Gate.Id) |> Set.ofList
              Expect.equal emitted expected "exactly the result's selected gates, no extras"
          }

          test "an empty selected-gate route projects present-and-empty selectedGates + all-zero cost (FR-009)" {
              let empty = resultOf fixtureFacts []
              use doc = parse (RouteJson.ofRouteResult empty None)
              Expect.isEmpty (selectedGates doc) "selectedGates present and empty"
              for tier in [ "cheap"; "medium"; "high"; "exhaustive" ] do
                  Expect.equal (costTier doc tier) 0 (sprintf "%s tier is zero" tier)
          }

          test "cost always carries the four integer tiers incl. zero, never a summed scalar (FR-006, SC-005)" {
              use doc = parse (RouteJson.ofRouteResult fixtureResult None)
              Expect.equal (fieldOrder (doc.RootElement.GetProperty "cost")) [ "cheap"; "medium"; "high"; "exhaustive" ] "every tier present, in order"
              // distinct gates: build:format (cheap), build:tests (medium), docs:lint (cheap)
              Expect.equal (costTier doc "cheap") 2 "two cheap gates"
              Expect.equal (costTier doc "medium") 1 "one medium gate"
              Expect.equal (costTier doc "high") 0 "no high gate"
              Expect.equal (costTier doc "exhaustive") 0 "no exhaustive gate"
          }

          test "a domain containing the gate-id separator renders id and domain verbatim, no re-parse (FR-010)" {
              let f =
                  facts
                      "src"
                      [ "src/bu/**", "build:unit" ]
                      []
                      [ check "build:unit" "tests" None Medium Local Observe ]
                      []

              let r = resultOf f [ "src/bu/x.fs" ]
              use doc = parse (RouteJson.ofRouteResult r None)
              let sg = r.SelectedGates |> List.exactlyOne
              let g = gateById doc (gateIdValue sg.Gate.Id)
              let (DomainId domain) = sg.Gate.Domain
              Expect.equal (strField g "id") (gateIdValue sg.Gate.Id) "id equals gateIdValue verbatim (id is build:unit:tests)"
              Expect.equal (strField g "domain") domain "domain equals declared DomainId, no separator re-derivation"
              Expect.equal domain "build:unit" "the declared domain carried the separator verbatim"
          }

          test "all-in-one-tier and spread-across-tiers routes render per-tier cost counts faithfully (FR-006)" {
              // all gates in one tier: two cheap checks in one domain reached by one path
              let oneTier =
                  facts
                      "src"
                      [ "src/a/**", "a" ]
                      []
                      [ check "a" "c1" None Cheap Local Observe
                        check "a" "c2" None Cheap Local Observe ]
                      []

              use d1 = parse (RouteJson.ofRouteResult (resultOf oneTier [ "src/a/x.fs" ]) None)
              Expect.equal (costTier d1 "cheap") 2 "both gates in the cheap tier"
              Expect.equal (costTier d1 "medium") 0 "no medium"
              Expect.equal (costTier d1 "high") 0 "no high"
              Expect.equal (costTier d1 "exhaustive") 0 "no exhaustive"

              // spread across tiers: one gate per tier, all reached by one path in one domain
              let spread =
                  facts
                      "src"
                      [ "src/a/**", "a" ]
                      []
                      [ check "a" "c1" None Cheap Local Observe
                        check "a" "c2" None Medium Local Observe
                        check "a" "c3" None High Local Observe
                        check "a" "c4" None Exhaustive Local Observe ]
                      []

              use d2 = parse (RouteJson.ofRouteResult (resultOf spread [ "src/a/x.fs" ]) None)
              for tier in [ "cheap"; "medium"; "high"; "exhaustive" ] do
                  Expect.equal (costTier d2 tier) 1 (sprintf "exactly one gate in the %s tier" tier)
          }

          test "JSON-special characters in free text round-trip via the writer, never manual escaping (FR-002/FR-005)" {
              // a checkId carrying a quote, a backslash, and a newline flows into the composed gate
              // description; the value read back from the parsed document must equal it exactly.
              let weird = "te\"st\\x\ny"

              let f =
                  facts
                      "src"
                      [ "src/q/**", "q" ]
                      []
                      [ check "q" weird None Medium Local Observe ]
                      []

              let r = resultOf f [ "src/q/x.fs" ]
              use doc = parse (RouteJson.ofRouteResult r None)
              let sg = r.SelectedGates |> List.exactlyOne
              let g = gateById doc (gateIdValue sg.Gate.Id)
              Expect.equal (strField g "id") (gateIdValue sg.Gate.Id) "id with special chars round-trips exactly"
              Expect.equal (strField g "description") sg.Gate.Description "description with special chars round-trips exactly"
          } ]
