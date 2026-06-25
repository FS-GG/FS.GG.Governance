module FS.GG.Governance.CostBudget.Tests.BudgetForTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// US1 (T017): `budgetFor` over the full 4×6 (Profile × RunMode) grid is `min` of the two monotone D1
// projections; both levers are monotone and either can restrict (FR-001, SC-001).

/// The expected monotone projection tables (independently re-encoded here so the test pins them).
let private expectedProfileCeiling profile =
    match profile with
    | Light -> Cheap
    | Standard -> Medium
    | Strict -> High
    | Profile.Release -> Exhaustive

let private expectedModeCeiling mode =
    match mode with
    | Sandbox -> Cheap
    | Inner -> Cheap
    | Focused -> Medium
    | Verify -> High
    | Gate -> High
    | RunMode.Release -> Exhaustive

[<Tests>]
let tests =
    testList
        "BudgetFor"
        [ test "budgetFor p m = { Ceiling = min (profileCeiling p) (modeCeiling m) } across the whole 4×6 grid" {
              for p in profiles do
                  for m in modes do
                      let expected = min (expectedProfileCeiling p) (expectedModeCeiling m)
                      Expect.equal (Budget.budgetFor p m).Ceiling expected (sprintf "ceiling for %A/%A" p m)
          }

          test "the anchor points hold" {
              Expect.equal (Budget.budgetFor Light Inner).Ceiling Cheap "Light/Inner floors to Cheap"
              Expect.equal (Budget.budgetFor Profile.Release RunMode.Release).Ceiling Exhaustive "Release/Release admits Exhaustive"
              Expect.equal (Budget.budgetFor Strict Verify).Ceiling High "Strict/Verify is High"
          }

          test "both levers are monotone (a stricter profile / more protective mode never lowers the ceiling)" {
              // profile lever monotone, holding mode fixed
              for m in modes do
                  let ceilings = profiles |> List.map (fun p -> (Budget.budgetFor p m).Ceiling)
                  Expect.equal ceilings (List.sort ceilings) (sprintf "profile lever monotone at mode %A" m)
              // mode lever monotone, holding profile fixed (modes already in protectiveness order)
              for p in profiles do
                  let ceilings = modes |> List.map (fun m -> (Budget.budgetFor p m).Ceiling)
                  Expect.equal ceilings (List.sort ceilings) (sprintf "mode lever monotone at profile %A" p)
          }

          test "fits is the inclusive cost <= ceiling over the ordered Cost DU (edge 'budget exactly met')" {
              let budget = Budget.budgetFor Strict Verify // ceiling High
              Expect.isTrue (Budget.fits budget Cheap) "cheap fits"
              Expect.isTrue (Budget.fits budget High) "exactly-met fits (inclusive)"
              Expect.isFalse (Budget.fits budget Exhaustive) "above ceiling does not fit"
          } ]
