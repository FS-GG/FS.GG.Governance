module FS.GG.Governance.CurrencyEnforcement.Tests.DecideCurrencyTests

// T006 — decideCurrency reproduces the F057 per-view outcomes by reusing the REAL F029 FreshnessKey.diff
// comparator verbatim (never mocked): matching ⇒ Current; a drifted source-digest set / generator version ⇒
// WouldRegenerate with the drifted InputCategory list; a sensed Error or a missing recorded provenance ⇒
// StaleUnresolved (never Current — FR-008).

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement
open FS.GG.Governance.CurrencyEnforcement.Tests.Support

[<Tests>]
let tests =
    testList
        "CurrencyEnforcement.decideCurrency"
        [ test "matching recorded vs sensed ⇒ Current" {
              let d = decideCurrency (entry "v") (Some([ art "h1"; art "h2" ], ver "g1")) (Ok([ art "h1"; art "h2" ], ver "g1"))
              Expect.equal d.Status Current "matching inputs are Current"
              Expect.equal d.Drifted [] "no drifted categories"
          }

          test "covered-artifacts are compared as a SET (reorder ⇒ Current)" {
              let d = decideCurrency (entry "v") (Some([ art "h1"; art "h2" ], ver "g1")) (Ok([ art "h2"; art "h1" ], ver "g1"))
              Expect.equal d.Status Current "set comparison ignores order (F029 FR-004)"
          }

          test "a drifted source-digest set ⇒ WouldRegenerate [CoveredArtifactsCat]" {
              let d = decideCurrency (entry "v") (Some([ art "h1" ], ver "g1")) (Ok([ art "h2" ], ver "g1"))
              Expect.equal d.Status (WouldRegenerate [ CoveredArtifactsCat ]) "source-digest drift"
              Expect.equal d.Drifted [ CoveredArtifactsCat ] "drifted carries the category"
          }

          test "a drifted generator version ⇒ WouldRegenerate [GeneratorVersionCat]" {
              let d = decideCurrency (entry "v") (Some([ art "h1" ], ver "g1")) (Ok([ art "h1" ], ver "g2"))
              Expect.equal d.Status (WouldRegenerate [ GeneratorVersionCat ]) "generator-version drift"
          }

          test "both drifting ⇒ both categories in fixed key-encoding order" {
              let d = decideCurrency (entry "v") (Some([ art "h1" ], ver "g1")) (Ok([ art "h2" ], ver "g2"))
              Expect.equal d.Drifted [ CoveredArtifactsCat; GeneratorVersionCat ] "covered-artifacts before generator-version"
          }

          test "a sensed Error ⇒ StaleUnresolved carrying the reason, never Current (FR-008)" {
              let d = decideCurrency (entry "v") (Some([ art "h1" ], ver "g1")) (Error "provenance lock unreadable")
              Expect.equal d.Status (StaleUnresolved "provenance lock unreadable") "carries the reason verbatim"
          }

          test "no recorded provenance ⇒ StaleUnresolved, never silently passed (FR-008)" {
              let d = decideCurrency (entry "v") None (Ok([ art "h1" ], ver "g1"))

              match d.Status with
              | StaleUnresolved _ -> ()
              | other -> failtestf "expected StaleUnresolved, got %A" other
          } ]
