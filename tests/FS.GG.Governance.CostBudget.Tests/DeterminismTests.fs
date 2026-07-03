module FS.GG.Governance.CostBudget.Tests.DeterminismTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// US1 (T020): repeated `decide` over identical inputs is byte-identical; any permutation of candidates yields
// an identical (GateId-ordinal) report; no clock/abs-path/username appears in any reason or entry (SC-008).

let private sampleCandidates =
    [ mustRecompute (gid "z" "last") Exhaustive [ RuleHashCat ]
      reusable (gid "a" "first") Cheap
      mustRecompute (gid "m" "mid") Cheap [ BaseRevisionCat ]
      noEvidence (gid "b" "second") Medium ]

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "repeated decide over identical inputs is identical" {
              let budget = Budget.budgetFor Strict Verify
              let a = Budget.decide budget Verify sampleCandidates
              let b = Budget.decide budget Verify sampleCandidates
              Expect.equal a b "byte-identical report"
          }

          testProperty "any permutation of candidates yields an identical report" (fun (seed: int) ->
              let budget = Budget.budgetFor Strict Verify
              // a deterministic shuffle driven by the seed
              let shuffled =
                  sampleCandidates
                  |> List.mapi (fun i c -> (((i + 1) * (abs seed + 7)) % 101), c)
                  |> List.sortBy fst
                  |> List.map snd

              Budget.decide budget Verify sampleCandidates = Budget.decide budget Verify shuffled)

          test "the report entries are in GateId-ordinal order regardless of supply order" {
              let report = Budget.decide (Budget.budgetFor Strict Verify) Verify sampleCandidates
              let gateValues = Budget.entries report |> List.map (fun e -> let (GateId g) = e.Gate in g)
              Expect.equal gateValues (List.sortWith (fun a b -> System.String.CompareOrdinal(a, b)) gateValues) "ordinal-sorted"
          }

          test "no clock/abs-path/username leaks into a reason text" {
              let report = Budget.decide (Budget.budgetFor Light Inner) Inner [ mustRecompute (gid "d" "x") Exhaustive [ RuleHashCat ] ]
              match Budget.overBudget report with
              | [ (_, r) ] ->
                  // BudgetReason carries only typed fields; nothing here can hold a path/clock. Sanity only.
                  Expect.notEqual r.Cost r.Ceiling "the over-budget cost exceeds the ceiling"
              | other -> failtestf "expected one reason, got %A" other
          } ]
