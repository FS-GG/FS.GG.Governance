module FS.GG.Governance.HumanText.Tests.AnsiFreeTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.Tests.Support

// T014 [US1]: every HumanText.of* over every fixture contains NO ANSI/CSI escape (assert no ESC[).

let private esc = string (char 0x1B)
let private hasAnsi (s: string) = s.Contains(esc + "[") || s.Contains esc

let private allRenders: (string * string) list =
    [ "route-clean", HumanText.ofRouteResult cleanRoute None []
      "route-findings", HumanText.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes
      "explain", HumanText.ofRouteExplanation explanation
      "ship-clean", HumanText.ofShipDecision emptyCleanDecision None []
      "ship-blocked", HumanText.ofShipDecision blockedDecision (Some evidenceReport) mixedOutcomes
      "verify-blocked", HumanText.ofVerifyDecision blockedDecision None []
      "release-clean", HumanText.ofReleaseReport cleanReleaseReport
      "release-blocked", HumanText.ofReleaseReport blockedReleaseReport
      "evidence", HumanText.ofCacheEligibilityReport evidenceReport ]

[<Tests>]
let tests =
    testList
        "AnsiFree"
        [ for name, text in allRenders ->
              test (sprintf "%s render has no ANSI escape" name) {
                  Expect.isFalse (hasAnsi text) (sprintf "%s must be ANSI-free" name)
              } ]
