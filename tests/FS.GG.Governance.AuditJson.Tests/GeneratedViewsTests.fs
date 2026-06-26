module FS.GG.Governance.AuditJson.Tests.GeneratedViewsTests

// T020 (F070) — the additive `generatedViews` wire shape on `ofShipDecisionWithGeneratedViews` (ship.json +
// audit.json): per-entry field content, sorted by viewId, omitted ENTIRELY when empty (byte-identical to
// `ofShipDecision`), and both base + effective severity present (no-hide, FR-006).

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement

let private finding (viewId: string) (cause: CE.StaleCause) : CE.CurrencyFinding =
    { ViewId = viewId
      Kind = RouteProjection
      Cause = cause
      BaseSeverity = Blocking
      Maturity = BlockOnShip }

let private detail (f: CE.CurrencyFinding) = f, CE.decisionOf f Gate Standard

[<Tests>]
let tests =
    testList
        "AuditJson.generatedViews"
        [ test "empty ⇒ omitted entirely ⇒ byte-identical to ofShipDecision (FR-004)" {
              let withEmpty = AuditJson.ofShipDecisionWithGeneratedViews emptyCleanDecision None [] []
              let baseline = AuditJson.ofShipDecision emptyCleanDecision None []
              Expect.equal withEmpty baseline "empty generatedViews ⇒ byte-identical"
              Expect.isFalse (withEmpty.Contains "generatedViews") "no generatedViews key when empty"
          }

          test "source-drift entries carry the full wire shape, sorted by viewId, both severities (gate+block-on-ship ⇒ blocking)" {
              let findings =
                  [ finding "zebra" (CE.SourceDrift [ CoveredArtifactsCat ])
                    finding "alpha" (CE.SourceDrift [ GeneratorVersionCat ]) ]

              let json = AuditJson.ofShipDecisionWithGeneratedViews emptyCleanDecision None [] (findings |> List.map detail)
              Expect.stringContains json "\"generatedViews\"" "array present"
              Expect.isLessThan (json.IndexOf "alpha") (json.IndexOf "zebra") "sorted by viewId"
              Expect.stringContains json "\"kind\":\"route-projection\"" "kind via viewKindToken"
              Expect.stringContains json "\"cause\":\"source-drift\"" "cause token"
              Expect.stringContains json (sprintf "\"%s\"" (categoryToken CoveredArtifactsCat)) "drifted category token"
              Expect.stringContains json "\"baseSeverity\":\"blocking\"" "base severity present"
              Expect.stringContains json "\"effectiveSeverity\":\"blocking\"" "effective severity present (no-hide)"
          }

          test "an undeterminable entry carries cause + detail reason (FR-008), not drifted" {
              let json = AuditJson.ofShipDecisionWithGeneratedViews emptyCleanDecision None [] [ detail (finding "v" (CE.Undeterminable "no recorded provenance")) ]
              Expect.stringContains json "\"cause\":\"undeterminable\"" "undeterminable cause"
              Expect.stringContains json "\"detail\":\"no recorded provenance\"" "the undeterminable reason"
          } ]
