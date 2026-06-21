module FS.GG.Governance.RouteExplain.Tests.EmptyRouteTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.RouteExplain.Tests.Support

// US3 — the owner of the empty/degenerate-route coverage (law L7, FR-011): an empty route, or a route with
// no high-cost gate, yields `{ Findings = [] }` — a valid success, never an error; a high-cost gate over an
// empty registry yields one finding with the explicit `NoCheaperLocalAlternative`. `explain` is total on
// every degenerate input.

[<Tests>]
let tests =
    testList
        "EmptyRoute"
        [ test "an empty route yields an empty explanation (L7, FR-011)" {
              let explanation = RouteExplain.explain (routeOf []) (catalog workedExampleGates)
              Expect.equal explanation { Findings = [] } "no selected gates ⇒ nothing to explain"
          }

          test "a route whose selected gates are all below threshold yields an empty explanation (L7, SC-001)" {
              let route =
                  routeOf
                      [ selGate (gate "build" "cheap" Cheap Local) [ sp "src/a.fs" "src/**" ]
                        selGate (gate "build" "medium" Medium LocalOrCi) [ sp "src/b.fs" "src/**" ] ]

              Expect.equal (RouteExplain.explain route (catalog workedExampleGates)) { Findings = [] } "no high-cost gate ⇒ empty"
          }

          test "a high-cost gate over an empty registry yields one finding with NoCheaperLocalAlternative (L3/L7)" {
              let route = routeOf [ selGate (gate "build" "full" Exhaustive Ci) [ sp "src/a.fs" "src/**" ] ]
              let explanation = RouteExplain.explain route (catalog [])

              let finding = explanation.Findings |> List.exactlyOne
              Expect.equal finding.Alternative NoCheaperLocalAlternative "an empty catalog offers no alternative — explicit none, never omitted"
          }

          test "explain is total on the empty route + empty registry (no exception)" {
              Expect.equal (RouteExplain.explain (routeOf []) (catalog [])) { Findings = [] } "both empty ⇒ empty, no throw"
          } ]
