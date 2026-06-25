module FS.GG.Governance.CostBudget.Tests.AgentReviewCacheTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// US5 (T038): an AgentReviewed candidate's verdict is computed from the REAL `AgentReviewKey.matches`
// (never mocked) — matching judge/prompt/check-artifact identity ⇒ Reusable ⇒ Reuse; one identity changed ⇒
// re-review ⇒ Recompute; and `decide`'s budget arithmetic is IDENTICAL to the same Deterministic candidate
// (the review mark never changes the decision) (FR-010, acceptance 5.1/5.2).

/// The verdict the edge supplies for an agent-reviewed gate: Reusable on a key match, else re-review.
let private agentVerdict (recorded: AgentReviewInputs) (current: AgentReviewInputs) : CacheEligibilityVerdict =
    if AgentReviewKey.matches recorded current then
        Reusable refA
    else
        MustRecompute NoPriorEvidence

[<Tests>]
let tests =
    testList
        "AgentReviewCache"
        [ test "matching F036 identity ⇒ Reusable ⇒ Reuse (reused on matching judge/prompt/check-artifact)" {
              Expect.isTrue (AgentReviewKey.matches reviewInputsBase reviewInputsBase) "identical inputs match"
              let verdict = agentVerdict reviewInputsBase reviewInputsBase
              let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ ccReviewed (gid "ai" "review") Cheap verdict reviewKey ]
              match (Budget.entries report |> List.head).Decision with
              | Reuse _ -> ()
              | other -> failtestf "expected Reuse, got %A" other
          }

          test "one F036 identity changed ⇒ no match ⇒ Recompute (re-review)" {
              Expect.isFalse (AgentReviewKey.matches reviewInputsBase reviewInputsChanged) "a changed model version no longer matches"
              let verdict = agentVerdict reviewInputsBase reviewInputsChanged
              let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ ccReviewed (gid "ai" "review") Cheap verdict reviewKeyChanged ]
              match (Budget.entries report |> List.head).Decision with
              | Recompute _ -> ()
              | other -> failtestf "expected Recompute, got %A" other
          }

          test "decide's budget arithmetic for an AgentReviewed candidate is IDENTICAL to the same Deterministic one" {
              for verdict in [ Reusable refA; MustRecompute(InputsChanged [ RuleHashCat ]); MustRecompute NoPriorEvidence ] do
                  for cost in costs do
                      for m in modes do
                          let g = gid "ai" "review"
                          let budget = Budget.budgetFor Strict m
                          let det = Budget.decide budget m [ cc g cost verdict ]
                          let rev = Budget.decide budget m [ ccReviewed g cost verdict reviewKey ]
                          let detDecision = (Budget.entries det |> List.head).Decision
                          let revDecision = (Budget.entries rev |> List.head).Decision
                          Expect.equal revDecision detDecision (sprintf "decision identical for %A cost %A mode %A" verdict cost m)
          } ]
