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

// #56/B1 — `costRank` makes the cheap→expensive order explicit so a comparison no longer rides the DU
// declaration order. These pin the intended order; a future reorder of the `Cost` cases that changed
// meaning would fail here rather than silently invert "cheaper".
[<Tests>]
let costRankTests =
    testList
        "Config.costRank"
        [ test "ranks the closed order Cheap < Medium < High < Exhaustive" {
              Expect.equal (costRank Cheap) 1 "Cheap"
              Expect.equal (costRank Medium) 2 "Medium"
              Expect.equal (costRank High) 3 "High"
              Expect.equal (costRank Exhaustive) 4 "Exhaustive"
          }

          test "is strictly monotone across the whole ladder" {
              let ordered = [ Cheap; Medium; High; Exhaustive ]
              let ranks = ordered |> List.map costRank
              Expect.equal ranks (List.sort ranks) "ranks are ascending in the intended order"
              Expect.equal (List.distinct ranks |> List.length) 4 "every case has a distinct rank"
          }

          test "sorting by costRank yields cheapest-first regardless of input order" {
              let shuffled = [ Exhaustive; Cheap; High; Medium ]
              Expect.equal
                  (shuffled |> List.sortBy costRank)
                  [ Cheap; Medium; High; Exhaustive ]
                  "sortBy costRank is cheapest-first"
          } ]
