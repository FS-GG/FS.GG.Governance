module FS.GG.Governance.HumanText.Tests.ReportParityTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.RouteJson
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.ReleaseJson
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.HumanText.Tests.Support

// T018 [US1]: from ONE report value the HumanText text and the *Json JSON convey the same verdict,
// blockers, and exit status — the human view is not a separately-computed summary (FR-001, SC-001).

let private ci (s: string) = s.ToLowerInvariant()

[<Tests>]
let tests =
    testList
        "ReportParity"
        [ test "route: the same gate ids surface in both the text and route.json" {
              let text = HumanText.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes
              let json = RouteJson.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes

              for gate in [ "build:compile"; "docs:lint" ] do
                  Expect.stringContains text gate (sprintf "%s in text" gate)
                  Expect.stringContains json gate (sprintf "%s in json" gate)
          }

          test "verify: the verdict + each blocker gate surface in both the text and verify.json" {
              let text = HumanText.ofVerifyDecision blockedDecision (Some evidenceReport) mixedOutcomes
              let json = VerifyJson.ofVerifyDecision blockedDecision (Some evidenceReport) mixedOutcomes

              // the blocked status surfaces in both (text: "FAIL" + "exit status: blocked"; json: "blocked").
              Expect.stringContains (ci text) "blocked" "blocked status in text"
              Expect.stringContains (ci json) "blocked" "blocked status in json"
              Expect.stringContains text "build:ship" "blocker in text"
              Expect.stringContains json "build:ship" "blocker in json"
          }

          test "release: the verdict surfaces in both the text and release.json" {
              let text = HumanText.ofReleaseReport blockedReleaseReport
              let json = ReleaseJson.ofReleaseReport blockedReleaseReport
              Expect.stringContains (ci text) "fail" "verdict in text"
              Expect.stringContains (ci json) "fail" "verdict in json"
          }

          test "evidence: the same gate ids surface in both the text and evidence json" {
              let text = HumanText.ofCacheEligibilityReport evidenceReport
              let json = CacheEligibilityJson.ofReport evidenceReport

              for gate in [ "build:ship"; "build:rel" ] do
                  Expect.stringContains text gate (sprintf "%s in text" gate)
                  Expect.stringContains json gate (sprintf "%s in json" gate)
          } ]
