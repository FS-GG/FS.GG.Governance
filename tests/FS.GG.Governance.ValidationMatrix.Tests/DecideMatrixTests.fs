module FS.GG.Governance.ValidationMatrix.Tests.DecideMatrixTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ValidationMatrix
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ValidationMatrix.Tests.Support

// SC-006: decideMatrix reuses the F25 CostBudget ordered ceiling — deferred in the inner loop, runs at the
// boundary, never invented.

[<Tests>]
let tests =
    testList
        "decideMatrix"
        [ test "declared Exhaustive matrix + inner-loop budget ⇒ Deferred (named, deterministic)" {
              let plan = Matrix.decideMatrix innerLoopBudget InnerLoop (Some exhaustiveMatrix)
              Expect.equal plan (Deferred(DeferredToScheduledBoundary("pack-all-targets", Exhaustive))) ""
          }

          test "same matrix + scheduled/release budget ⇒ RunNow" {
              let plan = Matrix.decideMatrix releaseBudget ScheduledOrRelease (Some exhaustiveMatrix)
              Expect.equal plan (RunNow exhaustiveMatrix) ""
          }

          test "a lower-cost matrix the inner-loop ceiling admits ⇒ RunNow even at InnerLoop (ceiling is the gate)" {
              let plan = Matrix.decideMatrix innerLoopBudget InnerLoop (Some cheapMatrix)
              Expect.equal plan (RunNow cheapMatrix) ""
          }

          test "None declared ⇒ NotDeclared at every boundary — never invented" {
              Expect.equal (Matrix.decideMatrix innerLoopBudget InnerLoop None) NotDeclared ""
              Expect.equal (Matrix.decideMatrix releaseBudget ScheduledOrRelease None) NotDeclared ""
          } ]
