module FS.GG.Governance.Findings.Tests.PrecedenceTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Findings
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Findings.Tests.Support

// US3: an `UnmatchedInRoot` path on a declared `ProtectedSurface` escalates to a distinct
// `UnknownProtectedBoundaryPath` / `ProtectedBoundaryUnknown sid` finding; `Protected > Routine`
// resolves overlaps to a single escalated finding; multi-protected ties pick the ordinal-first
// `SurfaceId`, independent of authoring order (SC-003, FR-006/FR-007/FR-009).

let private rootFacts surfaces = facts "src" [ "src/Kernel/**", "kernel" ] surfaces

let private only (report: FindingReport) =
    Expect.hasLength report.Findings 1 "exactly one finding"
    List.head report.Findings

[<Tests>]
let tests =
    testList
        "Precedence"
        [ test "an UnmatchedInRoot path within a ProtectedSurface escalates and carries the SurfaceId (US3 AS1, SC-003)" {
              let facts = rootFacts [ surface ProtectedSurface "kernel-core" [ "src/Core" ] ]
              let report = routeOf facts [ "src/Core/Secret.fs" ]
              let f = only (Findings.findUnknownGovernedPaths facts report)

              Expect.equal f.Id UnknownProtectedBoundaryPath "escalated id"
              Expect.equal f.Zone (ProtectedBoundaryUnknown(sid "kernel-core")) "zone carries the escalating SurfaceId"
          }

          test "an in-root unknown in no declared surface stays the ordinary flavor, distinct from protected (US3 AS2)" {
              let facts = rootFacts [ surface ProtectedSurface "kernel-core" [ "src/Core" ] ]
              let report = routeOf facts [ "src/Loose.fs" ]
              let f = only (Findings.findUnknownGovernedPaths facts report)

              Expect.equal f.Id UnknownGovernedPath "ordinary id"
              Expect.equal f.Zone GovernedRootUnknown "ordinary zone — distinct from the protected flavor"
          }

          test "Protected > Routine: a path in both a Routine and a ProtectedSurface → a single escalated finding (US3 AS, FR-007)" {
              let surfaces =
                  [ surface Routine "legacy" [ "src/Kernel" ]
                    surface ProtectedSurface "kernel-core" [ "src/Kernel" ] ]
              // route against a facts with NO path map so src/Kernel/New.fs is UnmatchedInRoot.
              let facts = facts "src" [] surfaces
              let report = routeOf facts [ "src/Kernel/New.fs" ]
              let f = only (Findings.findUnknownGovernedPaths facts report)

              Expect.equal f.Id UnknownProtectedBoundaryPath "escalated, never silenced"
              Expect.equal f.Zone (ProtectedBoundaryUnknown(sid "kernel-core")) "escalating surface identity"
              Expect.stringContains f.Message "legacy" "message names the contradictory routine surface"
              Expect.stringContains f.Message "kernel-core" "message names the protected surface"
          }

          test "multiple matching protected surfaces → ordinal-first SurfaceId, independent of authoring order (FR-009)" {
              let forward =
                  [ surface ProtectedSurface "alpha" [ "src/Z" ]
                    surface ProtectedSurface "beta" [ "src/Z" ] ]

              let factsFwd = facts "src" [] forward
              let factsRev = facts "src" [] (List.rev forward)

              let fwd = Findings.findUnknownGovernedPaths factsFwd (routeOf factsFwd [ "src/Z/x.fs" ])
              let rev = Findings.findUnknownGovernedPaths factsRev (routeOf factsRev [ "src/Z/x.fs" ])

              Expect.equal
                  (List.head fwd.Findings).Zone
                  (ProtectedBoundaryUnknown(sid "alpha"))
                  "ordinal-first SurfaceId 'alpha' wins"
              Expect.equal fwd rev "result unchanged when the two surfaces are authored in reverse order"
          } ]
