module FS.GG.Governance.CostBudget.Tests.AgentReviewNeverBlocksTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Findings
open FS.GG.Governance.CostBudget.Tests.Support

// US5 (T039): across the FULL (Profile, RunMode) grid, an agent-reviewed gate's cost/cache finding (any kind)
// derives Advisory via the real `deriveEffectiveSeverity` — it never blocks the verdict (FR-010, SC-007,
// acceptance 5.3). A cost/cache finding carries no review mark, so an agent-reviewed gate's finding is
// enforced identically to any other — and always advisory.

let private allKinds = [ Stale [ RuleHashCat ]; SyntheticTaint; NoEvidence ]

[<Tests>]
let tests =
    testList
        "AgentReviewNeverBlocks"
        [ test "an agent-reviewed gate's finding derives Advisory under EVERY mode/profile (never blocks)" {
              for kind in allKinds do
                  let finding =
                      { Gate = gid "ai" "review"
                        Kind = kind
                        BaseSeverity = Advisory
                        Message = "agent-reviewed advisory" }

                  for p in profiles do
                      for m in modes do
                          Expect.equal (Findings.enforce m p finding).EffectiveSeverity Advisory (sprintf "%A at %A/%A advisory" kind p m)
          } ]
