module FS.GG.Governance.GeneratedViewsJson.Tests.GeneratedViewsJsonTests

open Expecto
open FS.GG.Governance.Config.Model                 // Profile (Standard), Maturity (BlockOnShip)
open FS.GG.Governance.Enforcement.Enforcement      // RunMode (Gate), Severity (Blocking)
open FS.GG.Governance.FreshnessKey.Model           // InputCategory, categoryToken
open FS.GG.Governance.RefreshJson.RefreshModel      // ViewKind (RouteProjection)
open FS.GG.Governance.JsonText                      // writeToString (capture)
open FS.GG.Governance.GeneratedViewsJson

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement

// JSON-4: direct tests for the shared `generatedViews` writer leaf — the SINGLE home of the body AuditJson
// (audit.json / ship.json) and VerifyJson (verify.json) used to hand-copy byte-for-byte. Exercises the
// PUBLIC surface over REAL, literally-constructed domain values (Principle V — no mocks; the writer is
// pure). Output is captured through a real Utf8JsonWriter. The end-to-end byte-identity is additionally
// guarded by both projections' own goldens; these pin the shared leaf directly.

let private finding (viewId: string) (cause: CE.StaleCause) : CE.CurrencyFinding =
    { ViewId = viewId
      Kind = RouteProjection
      Cause = cause
      BaseSeverity = Blocking
      Maturity = BlockOnShip }

let private detail (f: CE.CurrencyFinding) = f, CE.decisionOf f Gate Standard

// writeGeneratedViews emits a `generatedViews` PROPERTY, so the caller owns the enclosing object.
let private render views =
    JsonText.writeToString (fun w ->
        w.WriteStartObject()
        GeneratedViewsJson.writeGeneratedViews w views
        w.WriteEndObject())

[<Tests>]
let tests =
    testList
        "GeneratedViewsJson"
        [ test "empty ⇒ the array is omitted entirely (FR-004)" {
              Expect.equal (render []) "{}" "no generatedViews key when empty"
          }

          test "source-drift entries carry the full wire shape, sorted by viewId (both severities, no-hide)" {
              let json =
                  render
                      [ detail (finding "zebra" (CE.SourceDrift [ CoveredArtifactsCat ]))
                        detail (finding "alpha" (CE.SourceDrift [ GeneratorVersionCat ])) ]

              Expect.stringContains json "\"generatedViews\"" "array present"
              Expect.isLessThan (json.IndexOf "alpha") (json.IndexOf "zebra") "sorted by viewId"
              Expect.stringContains json "\"kind\":\"route-projection\"" "kind via viewKindToken"
              Expect.stringContains json "\"cause\":\"source-drift\"" "cause token"
              Expect.stringContains json (sprintf "\"%s\"" (categoryToken CoveredArtifactsCat)) "drifted category token"
              Expect.stringContains json "\"baseSeverity\":\"blocking\"" "base severity present"
              Expect.stringContains json "\"effectiveSeverity\":\"blocking\"" "effective severity present (no-hide)"
          }

          test "an undeterminable entry carries cause + detail reason, not a drifted array" {
              let json = render [ detail (finding "v" (CE.Undeterminable "no recorded provenance")) ]
              Expect.stringContains json "\"cause\":\"undeterminable\"" "undeterminable cause"
              Expect.stringContains json "\"detail\":\"no recorded provenance\"" "the undeterminable reason"
              Expect.isFalse (json.Contains "\"drifted\"") "no drifted array for an undeterminable cause"
          } ]
