module FS.GG.Governance.Findings.Tests.PlaneUniformityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings
open FS.GG.Governance.Findings.Tests.Support

// US5: an unclassified in-root path yields the same finding whichever F016 plane it came from
// (the decision is path+surface keyed, never plane keyed); a path appearing in more than one
// plane collapses to a single finding by the documented dedup; the union across planes drops
// nothing (SC-007, FR-010).

let private facts0 = facts "src" [ "src/Kernel/**", "kernel" ] []

[<Tests>]
let tests =
    testList
        "PlaneUniformity"
        [ test "the same unclassified in-root path gives an identical finding from each F016 plane (US5 AS1, SC-007)" {
              // Three planes modeled as separate single-path RouteReports — the decision never
              // reads a plane, so each must yield byte-identical findings.
              let committed = Findings.findUnknownGovernedPaths facts0 (routeOf facts0 [ "src/New.fs" ])
              let dirty = Findings.findUnknownGovernedPaths facts0 (routeOf facts0 [ "src/New.fs" ])
              let untracked = Findings.findUnknownGovernedPaths facts0 (routeOf facts0 [ "src/New.fs" ])

              Expect.equal committed dirty "committed-plane finding equals dirty-plane finding"
              Expect.equal dirty untracked "dirty-plane finding equals untracked-plane finding"
              Expect.hasLength committed.Findings 1 "exactly one finding"
          }

          test "a path repeated in Routings (same result) collapses to a single finding (US5 AS2, FR-010)" {
              // The realistic case: a caller concatenated several routed planes; routing is a pure
              // function of the path, so the duplicate carries the same RoutingResult.
              let report =
                  routingsWith
                      [ routing "src/New.fs" UnmatchedInRoot
                        routing "src/New.fs" UnmatchedInRoot
                        routing "src/New.fs" UnmatchedInRoot ]

              let result = Findings.findUnknownGovernedPaths facts0 report
              Expect.hasLength result.Findings 1 "the repeated path collapses to exactly one finding"
          }

          test "a multi-path multi-plane union drops no path and is deterministically ordered (US5 AS3)" {
              // Two planes' routings concatenated, with one shared in-root unknown and distinct ones.
              let report =
                  routingsWith
                      [ routing "src/New.fs" UnmatchedInRoot // shared across both planes
                        routing "src/Beta.fs" UnmatchedInRoot
                        routing "src/New.fs" UnmatchedInRoot // shared duplicate
                        routing "src/Alpha.fs" UnmatchedInRoot
                        routing "src/Kernel/k.fs" (Routed(DomainId "kernel", GovernedPath "src/Kernel/**", OnlyMatch)) ]

              let result = Findings.findUnknownGovernedPaths facts0 report
              let paths = result.Findings |> List.map (fun f -> f.Path)

              Expect.equal
                  paths
                  [ normalizePath "src/Alpha.fs"; normalizePath "src/Beta.fs"; normalizePath "src/New.fs" ]
                  "the union of unknowns, deduped and ordinally sorted, dropping nothing"
          } ]
