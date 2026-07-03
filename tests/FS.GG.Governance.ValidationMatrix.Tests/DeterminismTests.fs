module FS.GG.Governance.ValidationMatrix.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.ValidationMatrix
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ValidationMatrix.Tests.Support

// FR-009 / FR-010 / SC-006: decideMatrix is total and byte-identical for identical inputs; the DeferReason
// names the matrix + cost deterministically with no clock/path/env.

[<Tests>]
let tests =
    testList
        "decideMatrix-determinism"
        [ test "byte-identical for identical inputs" {
              let a = Matrix.decideMatrix innerLoopBudget (Some exhaustiveMatrix)
              let b = Matrix.decideMatrix innerLoopBudget (Some exhaustiveMatrix)
              Expect.equal a b ""
          }

          test "the DeferReason names the matrix + its cost" {
              match Matrix.decideMatrix innerLoopBudget (Some exhaustiveMatrix) with
              | Deferred(DeferredToScheduledBoundary(name, cost)) ->
                  Expect.equal name exhaustiveMatrix.Name "names the matrix"
                  Expect.equal cost exhaustiveMatrix.Cost "names the cost"
              | other -> failtestf "expected Deferred, got %A" other
          } ]
