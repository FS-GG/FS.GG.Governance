module FS.GG.Governance.CostBudget.Tests.CacheHitMissTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// US2 (T022): candidates built from REAL CacheEligibility/EvidenceReuse verdicts — Reusable ⇒ Reuse (charges
// nothing); each single freshness dimension changed ⇒ Recompute naming that category (charged); a cost over
// the ceiling ⇒ OverBudget (US1 integration); NoPriorEvidence ⇒ Recompute with that cause — never a
// fabricated reuse (FR-004, FR-005, SC-002, SC-003).

/// The clean single-field categories (exclude the gate-identity Check/Domain and the entangled Command,
/// whose variant changes two categories at once) — each maps `baseInputs` to a one-dimension change.
let private singleDimCategories =
    nonIdentityCategories |> List.filter (fun (c, _) -> c <> CommandIdentity)

[<Tests>]
let tests =
    testList
        "CacheHitMiss"
        [ test "a Reusable candidate becomes Reuse and is NOT in recomputeGates (charges nothing)" {
              let g = gid "build" "tests"
              let verdict = verdictFor baseInputs baseStore // exact match ⇒ Reusable
              Expect.equal verdict (Reusable refA) "real F041 verdict is Reusable on an exact match"

              let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ cc g Exhaustive verdict ]
              match (Budget.entries report |> List.head).Decision with
              | Reuse r -> Expect.equal r refA "reuse carries the recorded evidence verbatim"
              | other -> failtestf "expected Reuse, got %A" other
              Expect.equal (Budget.recomputeGates report) [] "a reused gate charges nothing"
          }

          test "each single freshness dimension changed ⇒ Recompute naming exactly that category (charged)" {
              let g = gid "build" "tests"
              for (cat, variant) in singleDimCategories do
                  let verdict = verdictFor (variant baseInputs) baseStore
                  match verdict with
                  | MustRecompute(InputsChanged [ c ]) -> Expect.equal c cat (sprintf "real verdict names %A" cat)
                  | other -> failtestf "expected MustRecompute (InputsChanged [%A]), got %A" cat other

                  let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ cc g Exhaustive verdict ]
                  match (Budget.entries report |> List.head).Decision with
                  | Recompute(InputsChanged [ c ]) -> Expect.equal c cat (sprintf "decision names %A" cat)
                  | other -> failtestf "expected Recompute naming %A, got %A" cat other
                  Expect.equal (Budget.recomputeGates report) [ g ] "the recompute gate is charged"
          }

          test "a NoPriorEvidence candidate ⇒ Recompute with the NoPriorEvidence cause — never a fabricated reuse" {
              let g = gid "new" "gate"
              let verdict = verdictFor baseInputs EvidenceReuse.empty // empty store ⇒ NoPriorEvidence
              Expect.equal verdict (MustRecompute NoPriorEvidence) "real F041 verdict is NoPriorEvidence against an empty store"

              let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ cc g Cheap verdict ]
              match (Budget.entries report |> List.head).Decision with
              | Recompute NoPriorEvidence -> ()
              | other -> failtestf "expected Recompute NoPriorEvidence, got %A" other
          }

          test "a MustRecompute gate whose cost exceeds the ceiling ⇒ OverBudget — not silently reused, not silently recomputed" {
              let g = gid "build" "tests"
              let verdict = verdictFor (baseInputs |> fun i -> { i with RuleHash = RuleHash "r2" }) baseStore
              // Inner ⇒ Cheap ceiling; an Exhaustive cost is over budget.
              let report = Budget.decide (Budget.budgetFor Light Inner) Inner [ cc g Exhaustive verdict ]
              match (Budget.entries report |> List.head).Decision with
              | OverBudget _ -> ()
              | other -> failtestf "expected OverBudget, got %A" other
          } ]
