module FS.GG.Governance.RouteExplain.Tests.HighCostFindingTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.RouteExplain.Tests.Support

// US1 — `explain` emits exactly one `HighCostFinding` per selected gate whose declared `Cost >= High`,
// none below, each embedding the F019 `SelectedGate` verbatim, with `Findings` ordered by `GateId`
// (SC-001/SC-002, plan D3/D5). The threshold/trace/order/count laws (L1, L2); the below-threshold ⇒ empty
// case is owned by EmptyRouteTests.

/// A real `RouteResult` (genuine F015->F017->F018->F019 chain) selecting a single `build:tests` gate of the
/// given cost, reached by one changed path.
let private routeSelectingCost (cost: Cost) : RouteResult =
    let f =
        facts "src" [ "src/**", "build" ] [ check "build" "tests" None cost Local ] []

    selectOf f [ "src/a.fs" ]

let private registryFor (cost: Cost) : GateRegistry =
    registryOf (facts "src" [ "src/**", "build" ] [ check "build" "tests" None cost Local ] [])

[<Tests>]
let tests =
    testList
        "HighCostFinding"
        [ test "highCostThreshold is High" {
              Expect.equal RouteExplain.highCostThreshold High "the fixed MVP cutoff is High (research D3)"
          }

          test "a finding is produced iff the selected gate's Cost >= High, across every tier (L1, SC-001)" {
              for cost in [ Cheap; Medium; High; Exhaustive ] do
                  let route = routeSelectingCost cost
                  let registry = registryFor cost
                  let explanation = RouteExplain.explain route registry
                  let expected = if cost >= High then 1 else 0

                  Expect.equal
                      explanation.Findings.Length
                      expected
                      (sprintf "cost %A should produce %d finding(s)" cost expected)
          }

          test "a high-cost finding embeds the F019 SelectedGate verbatim — gate + every selecting path (L2, SC-002)" {
              // A high-cost gate reached by TWO changed paths: the finding's `Selected` must equal the route
              // entry whole (same Id/Domain/Cost and the same selecting-path set, not a subset).
              let f =
                  facts
                      "src"
                      [ "src/a/**", "build"; "src/b/**", "build" ]
                      [ check "build" "tests" None Exhaustive Local ]
                      []

              let route = selectOf f [ "src/a/x.fs"; "src/b/y.fs" ]
              let registry = registryOf f
              let explanation = RouteExplain.explain route registry

              Expect.equal explanation.Findings.Length 1 "one high-cost gate selected"
              let selected = route.SelectedGates |> List.exactlyOne
              Expect.isTrue (selected.SelectingPaths.Length >= 2) "the gate was reached by several paths"
              Expect.equal
                  explanation.Findings.Head.Selected
                  selected
                  "the finding carries the F019 selected gate verbatim — gate identity/domain/cost and every selecting path"
          }

          test "with several high-cost gates the findings are sorted by GateId ordinal (L1, D5)" {
              // Two checks in the same domain ⇒ one changed path selects both; both are high-cost.
              let f =
                  facts
                      "src"
                      [ "src/**", "build" ]
                      [ check "build" "zeta" None Exhaustive Local
                        check "build" "alpha" None High Local ]
                      []

              let route = selectOf f [ "src/a.fs" ]
              let registry = registryOf f
              let explanation = RouteExplain.explain route registry

              let ids =
                  explanation.Findings |> List.map (fun fnd -> gateIdValue fnd.Selected.Gate.Id)

              Expect.equal ids [ "build:alpha"; "build:zeta" ] "findings are in GateId ordinal order"
          }

          test "the finding count equals the number of selected gates with Cost >= High (no drop, no dup)" {
              // A hand-built route mixing every tier: two of them (High, Exhaustive) are high-cost.
              let route =
                  routeOf
                      [ selGate (gate "build" "cheap" Cheap Local) [ sp "src/a.fs" "src/**" ]
                        selGate (gate "build" "medium" Medium Local) [ sp "src/b.fs" "src/**" ]
                        selGate (gate "build" "high" High Local) [ sp "src/c.fs" "src/**" ]
                        selGate (gate "build" "exhaustive" Exhaustive Local) [ sp "src/d.fs" "src/**" ] ]

              let explanation = RouteExplain.explain route (catalog [])

              let expectedCount =
                  route.SelectedGates |> List.filter (fun sg -> sg.Gate.Cost >= High) |> List.length

              Expect.equal explanation.Findings.Length expectedCount "one finding per high-cost gate, none dropped or duplicated"
              Expect.equal expectedCount 2 "exactly the High and Exhaustive gates"
          } ]
