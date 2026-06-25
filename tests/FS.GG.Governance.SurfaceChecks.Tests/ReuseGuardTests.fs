module FS.GG.Governance.SurfaceChecks.Tests.ReuseGuardTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SurfaceChecks.Dispatch

module SC = FS.GG.Governance.SurfaceChecks.Model

// T057 — no-new-vocabulary guard (FR-013, FR-014): domainOf maps ONLY the existing F23 product classes;
// every other existing class ⇒ None. A future SurfaceClass would be a compile error in domainOf's exhaustive
// match, never a silent remap. (This row adds no new SurfaceClass, schema field, or DiagnosticId.)

let private allClasses =
    [ Routine
      GovernedRoot
      ProtectedSurface
      GeneratedView
      ReleaseSurface
      PackageSurface
      DocsSurface
      SkillSurface
      DesignSurface
      SampleAppSurface
      GeneratedProductRoot ]

[<Tests>]
let tests =
    testList
        "SurfaceChecks.reuseGuard"
        [ test "exactly the four product classes map to a domain; all others map to None" {
              let mapped =
                  allClasses
                  |> List.choose (fun cls -> Composition.domainOf cls |> Option.map (fun d -> cls, d))

              Expect.equal
                  mapped
                  [ PackageSurface, SC.PackageDomain
                    DocsSurface, SC.DocsDomain
                    SkillSurface, SC.SkillDomain
                    DesignSurface, SC.DesignDomain ]
                  "only the four product classes route to a pack"

              let toNone = allClasses |> List.filter (fun c -> Composition.domainOf c = None) |> List.length
              Expect.equal toNone 7 "the seven boundary/non-product classes map to None (FR-015)"
          } ]
