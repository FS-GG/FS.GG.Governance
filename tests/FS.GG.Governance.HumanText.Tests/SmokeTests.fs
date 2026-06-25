module FS.GG.Governance.HumanText.Tests.SmokeTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.Tests.Support

// T011: exercise the loaded PUBLIC surface — RenderMode.selectMode, every HumanText.of*, and every
// ReportView.viewOf* — over real report fixtures (Constitution I: call the surface, never internals).

let private cap isTty noColor plain =
    { RenderMode.IsTty = isTty
      RenderMode.NoColorEnv = noColor
      RenderMode.ExplicitPlain = plain
      RenderMode.Width = None }

[<Tests>]
let tests =
    testList
        "Smoke"
        [ test "selectMode: JSON always wins" {
              Expect.equal (RenderMode.selectMode true (cap true false false)) RenderMode.Json "json wins on tty"
              Expect.equal (RenderMode.selectMode true (cap false true true)) RenderMode.Json "json wins on non-tty"
          }

          test "selectMode: Rich iff interactive, else Plain" {
              Expect.equal (RenderMode.selectMode false (cap true false false)) RenderMode.Rich "tty no-nocolor no-plain ⇒ rich"
              Expect.equal (RenderMode.selectMode false (cap false false false)) RenderMode.Plain "non-tty ⇒ plain"
              Expect.equal (RenderMode.selectMode false (cap true true false)) RenderMode.Plain "NO_COLOR ⇒ plain"
              Expect.equal (RenderMode.selectMode false (cap true false true)) RenderMode.Plain "--plain ⇒ plain"
          }

          test "every HumanText.of* renders a non-empty string over a real report" {
              Expect.isGreaterThan (HumanText.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes).Length 0 "route"
              Expect.isGreaterThan (HumanText.ofRouteExplanation explanation).Length 0 "explain"
              Expect.isGreaterThan (HumanText.ofShipDecision blockedDecision None []).Length 0 "ship"
              Expect.isGreaterThan (HumanText.ofVerifyDecision blockedDecision None []).Length 0 "verify"
              Expect.isGreaterThan (HumanText.ofReleaseReport blockedReleaseReport).Length 0 "release"
              Expect.isGreaterThan (HumanText.ofCacheEligibilityReport evidenceReport).Length 0 "evidence"
          }

          test "every ReportView.viewOf* yields a titled view over a real report" {
              let nonEmpty (v: ReportView.ReportView) = Expect.isGreaterThan v.Title.Length 0 "title"
              nonEmpty (ReportView.viewOfRouteResult cleanRoute None [])
              nonEmpty (ReportView.viewOfRouteExplanation explanation)
              nonEmpty (ReportView.viewOfShipDecision emptyCleanDecision None [])
              nonEmpty (ReportView.viewOfVerifyDecision emptyCleanDecision None [])
              nonEmpty (ReportView.viewOfReleaseReport cleanReleaseReport)
              nonEmpty (ReportView.viewOfCacheEligibilityReport evidenceReport)
          } ]
