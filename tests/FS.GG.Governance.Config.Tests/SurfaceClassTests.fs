module FS.GG.Governance.Config.Tests.SurfaceClassTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Tests.Support

// US3 — each MVP surface class is expressible and classifies into its own typed category;
// routine/undeclared files produce no surface or governed-root fact (light-by-default).

let private capsOf name =
    match validateFixture name with
    | Valid f -> f.Capabilities
    | Invalid d -> failtestf "expected Valid for %s, got %A" name d

let private cases =
    [ "surface-routine", Routine
      "surface-governed-root", GovernedRoot
      "surface-protected", ProtectedSurface
      "surface-generated-view", GeneratedView
      "surface-release", ReleaseSurface ]

[<Tests>]
let tests =
    testList
        "SurfaceClass.US3"
        [ testList
              "each MVP surface class classifies into its own category"
              [ for name, expected in cases ->
                    test name {
                        let surfaces = (capsOf name).Surfaces
                        Expect.equal (List.length surfaces) 1 "exactly one declared surface"
                        let s = List.head surfaces
                        Expect.equal s.Class expected "surface kind → SurfaceClass"
                        Expect.equal s.Owner (Owner "platform") "owner preserved (US3 scenario 2)"
                        Expect.equal s.Maturity Observe "maturity preserved (US3 scenario 2)"
                    } ]

          test "surface-undeclared-only → Valid with no surface facts (US3 scenario 3)" {
              let c = capsOf "surface-undeclared-only"
              Expect.isEmpty c.Surfaces "undeclared files produce no surface fact"
          } ]
