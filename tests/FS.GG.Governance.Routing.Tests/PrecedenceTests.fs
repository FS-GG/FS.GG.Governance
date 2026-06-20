module FS.GG.Governance.Routing.Tests.PrecedenceTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Routing.Tests.Support

// One fixture per FR-005 precedence rung, asserted through the public `Routing.route` (US2).

/// The single routing result for one candidate path under a path map rooted at ".".
let private routeOne (pairs: (string * string) list) (path: string) =
    let report = Routing.route (facts "." pairs) [ gp path ]
    (List.exactlyOne report.Routings).Result

[<Tests>]
let tests =
    testList
        "Precedence"
        [ test "rung 1 — exact-literal beats a wildcard (ExactLiteral)" {
              // src/** and the exact literal both match; the literal wins (US2 AS2).
              let result =
                  routeOne [ "src/**", "core"; "src/Kernel/Eval.fs", "kernel-eval" ] "src/Kernel/Eval.fs"

              Expect.equal result (Routed(dom "kernel-eval", gp "src/Kernel/Eval.fs", ExactLiteral)) "exact literal wins"
          }

          test "rung 2 — more literal segments wins (MoreSpecific)" {
              // src/Adapters/** ({src,Adapters}) beats src/** ({src}) for a deep path (US2 AS1).
              let result =
                  routeOne [ "src/**", "core"; "src/Adapters/**", "adapters" ] "src/Adapters/X.fs"

              Expect.equal result (Routed(dom "adapters", gp "src/Adapters/**", MoreSpecific)) "deeper literal wins"
          }

          test "rung 3 — single-segment * beats cross-segment ** (MoreSpecific)" {
              // src/* (fewer **) beats src/** for src/x (US2 AS3).
              let result = routeOne [ "src/*", "a"; "src/**", "b" ] "src/x"
              Expect.equal result (Routed(dom "a", gp "src/*", MoreSpecific)) "single-segment * is more specific"
          }

          test "rung order is reorder-invariant (US2 AS4, FR-012)" {
              // The winner, glob, and reason are identical regardless of authored order.
              let forward = routeOne [ "src/**", "core"; "src/Adapters/**", "adapters" ] "src/Adapters/X.fs"
              let reversed = routeOne [ "src/Adapters/**", "adapters"; "src/**", "core" ] "src/Adapters/X.fs"
              Expect.equal reversed forward "winner unchanged under path-map re-ordering"
          } ]
