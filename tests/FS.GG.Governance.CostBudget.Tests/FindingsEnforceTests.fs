module FS.GG.Governance.CostBudget.Tests.FindingsEnforceTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Findings
open FS.GG.Governance.CostBudget.Tests.Support

// US3 (T028): `enforce` calls the REAL `deriveEffectiveSeverity` (never mocked) and derives Advisory for
// EVERY CostFindingKind across the FULL (Profile, RunMode) grid — a base-Advisory finding never blocks
// (FR-010, FR-013, SC-007 family). `kindToken` is the exhaustive stale|syntheticTaint|noEvidence table.

let private allKinds = [ Stale [ RuleHashCat ]; SyntheticTaint; NoEvidence ]

[<Tests>]
let tests =
    testList
        "FindingsEnforce"
        [ test "every finding kind derives Advisory across the full Profile × RunMode grid" {
              for kind in allKinds do
                  let finding =
                      { Gate = gid "build" "tests"
                        Kind = kind
                        BaseSeverity = Advisory
                        Message = "m" }

                  for p in profiles do
                      for m in modes do
                          let decision = Findings.enforce m p finding
                          Expect.equal decision.EffectiveSeverity Advisory (sprintf "%A at %A/%A stays advisory" kind p m)
                          Expect.equal decision.BaseSeverity Advisory "base severity echoed unchanged"
          }

          test "kindToken is the exhaustive stale|syntheticTaint|noEvidence table" {
              Expect.equal (Findings.kindToken (Stale [])) "stale" "stale"
              Expect.equal (Findings.kindToken SyntheticTaint) "syntheticTaint" "syntheticTaint"
              Expect.equal (Findings.kindToken NoEvidence) "noEvidence" "noEvidence"
          } ]
