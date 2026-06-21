module FS.GG.Governance.RouteExplain.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.RouteExplain.Tests.Support

// US3 — `explain` is a pure, deterministic function of the supplied route + catalog: identical inputs ⇒
// identical explanation, and reordering/duplicating selected gates, registry gates, or a gate's selecting
// paths never changes it (SC-005, plan D5; law L6).

// A route with two high-cost gates (one reached by several paths) over a catalog with several candidates —
// rich enough that order and duplication could, if mishandled, change the result.
let private gFull = gate "build" "full" Exhaustive Ci
let private gHigh = gate "build" "harden" High Local

let private paths =
    [ sp "src/b.fs" "src/**"; sp "src/a.fs" "src/**"; sp "lib/z.fs" "lib/**" ]

let private route = routeOf [ selGate gFull paths; selGate gHigh [ sp "src/a.fs" "src/**" ] ]

let private registry =
    catalog
        [ gate "build" "integration" Medium LocalOrCi
          gate "build" "unit" Cheap Local
          gate "build" "smoke-ci" Medium Ci ]

let private canonical = RouteExplain.explain route registry

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig fscheckConfig "explain is referentially transparent — twice yields equal results (L6, SC-005)" (fun
                                                                                                                                       (r: RouteResult)
                                                                                                                                       (reg: GateRegistry) ->
              RouteExplain.explain r reg = RouteExplain.explain r reg)

          test "reordering the selected gates does not change the explanation (L6)" {
              let reordered = routeOf (route.SelectedGates |> List.rev)
              Expect.equal (RouteExplain.explain reordered registry) canonical "findings re-sorted by GateId — order-invariant"
          }

          test "duplicating a selected gate does not change the explanation (L6, dup-invariance)" {
              let duplicated = routeOf (route.SelectedGates @ route.SelectedGates)
              Expect.equal (RouteExplain.explain duplicated registry) canonical "duplicate selected gates collapse"
          }

          test "reordering the registry gates does not change the explanation (L6)" {
              let reordered = catalog (registry.Gates |> List.rev)
              Expect.equal (RouteExplain.explain route reordered) canonical "alternative resolution sorts candidates — order-invariant"
          }

          test "duplicating the registry gates does not change the explanation (L6, dup-invariance)" {
              let duplicated = catalog (registry.Gates @ registry.Gates)
              Expect.equal (RouteExplain.explain route duplicated) canonical "duplicate candidates resolve to the same head"
          }

          test "reordering a gate's selecting paths does not change the explanation (L6)" {
              // Shuffle the high-cost gate's selecting paths; the carried trace canonicalizes identically.
              let shuffled =
                  routeOf [ selGate gFull (paths |> List.rev); selGate gHigh [ sp "src/a.fs" "src/**" ] ]

              Expect.equal (RouteExplain.explain shuffled registry) canonical "selecting-path order-invariant"
          }

          test "duplicating a gate's selecting paths does not change the explanation (L6, dup-invariance)" {
              let withDupPaths =
                  routeOf [ selGate gFull (paths @ paths); selGate gHigh [ sp "src/a.fs" "src/**" ] ]

              Expect.equal (RouteExplain.explain withDupPaths registry) canonical "duplicate selecting paths collapse"
          } ]
