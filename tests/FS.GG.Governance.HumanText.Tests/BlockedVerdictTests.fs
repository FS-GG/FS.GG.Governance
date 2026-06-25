module FS.GG.Governance.HumanText.Tests.BlockedVerdictTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.Tests.Support

// T016 [US1]: a blocked report renders as blocked with explicit reason(s) + exit status, matching
// the report object's verdict/exit-code basis — never softened. Plus the FR-012 one-shot input
// signal: an unknown-governed-path finding surfaces a clear input signal, distinct from a tool defect.

[<Tests>]
let tests =
    testList
        "BlockedVerdict"
        [ test "blocked ship decision renders FAIL + blocked exit status + the blocking gate" {
              let text = HumanText.ofShipDecision blockedDecision None []
              Expect.stringContains text "FAIL" "verdict rendered as FAIL"
              Expect.stringContains text "exit status: blocked" "exit status named blocked"
              Expect.stringContains text "Blockers" "blockers section present"
              Expect.stringContains text "build:ship" "the BlockOnShip gate is named as a blocker"
          }

          test "verify shares the blocked rendering of the same ShipDecision" {
              Expect.equal
                  (HumanText.ofVerifyDecision blockedDecision None [])
                  (HumanText.ofShipDecision blockedDecision None [])
                  "verify and ship project the same ShipDecision identically"
          }

          test "blocked release renders FAIL + blocked exit status" {
              let text = HumanText.ofReleaseReport blockedReleaseReport
              Expect.stringContains text "FAIL" "release verdict rendered as FAIL"
              Expect.stringContains text "exit status: blocked" "release exit status named blocked"
          }

          test "clean decision renders PASS + clean exit status (never softened to FAIL)" {
              let text = HumanText.ofShipDecision emptyCleanDecision None []
              Expect.stringContains text "PASS" "clean verdict rendered as PASS"
              Expect.stringContains text "exit status: clean" "clean exit status"
          }

          test "FR-012: an unknown-governed-path finding surfaces a clear input signal in a one-shot route render" {
              let text = HumanText.ofRouteResult routeWithFindings None []
              Expect.stringContains text "Findings" "findings section present"
              Expect.stringContains text "src/new/Thing.fs" "the unclassified input path is named"
              Expect.stringContains text "src/boundary/Api.fs" "the protected-boundary input path is named"
          } ]
