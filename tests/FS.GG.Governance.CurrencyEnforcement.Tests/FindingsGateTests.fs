module FS.GG.Governance.CurrencyEnforcement.Tests.FindingsGateTests

// T007 — the opt-in gate (D5): findingsOf None _ = []; Current/NotEvaluated ⇒ no finding; each
// WouldRegenerate/Regenerated/StaleUnresolved ⇒ exactly one finding with BaseSeverity = Blocking,
// Maturity = configured, in declared manifest order. Plus the NotEvaluated-vs-StaleUnresolved boundary
// (out-of-scope ⇒ pass; in-scope-undeterminable ⇒ finding, never a silent pass — FR-008).
// Extended for T033 (US2): observe/warn dials produce a finding that never blocks (asserted in the sweep).

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement
open FS.GG.Governance.CurrencyEnforcement.Tests.Support

[<Tests>]
let tests =
    testList
        "CurrencyEnforcement.findingsOf"
        [ test "None ⇒ [] (unconfigured ⇒ byte-identity)" {
              let views = [ decision "v" (WouldRegenerate [ CoveredArtifactsCat ]) [ CoveredArtifactsCat ] ]
              Expect.equal (findingsOf None views) [] "no config ⇒ no findings"
          }

          test "Current and NotEvaluated ⇒ no finding" {
              let views = [ decision "a" Current []; decision "b" NotEvaluated [] ]
              Expect.equal (findingsOf (Some BlockOnShip) views) [] "fresh + out-of-scope ⇒ none"
          }

          test "each stale/unresolved view ⇒ one finding, Blocking base, configured maturity, declared order" {
              let views =
                  [ decision "a" (WouldRegenerate [ CoveredArtifactsCat ]) [ CoveredArtifactsCat ]
                    decision "b" (Regenerated [ GeneratorVersionCat ]) [ GeneratorVersionCat ]
                    decision "c" (StaleUnresolved "x") [] ]

              let findings = findingsOf (Some BlockOnShip) views
              Expect.equal (findings |> List.map (fun f -> f.ViewId)) [ "a"; "b"; "c" ] "declared manifest order"
              Expect.all findings (fun f -> f.BaseSeverity = Blocking) "base severity is Blocking (D5)"
              Expect.all findings (fun f -> f.Maturity = BlockOnShip) "maturity = the configured dial"
          }

          test "drift ⇒ SourceDrift cause; unresolved ⇒ Undeterminable cause" {
              let views =
                  [ decision "drift" (WouldRegenerate [ CoveredArtifactsCat ]) [ CoveredArtifactsCat ]
                    decision "unres" (StaleUnresolved "no declared sources") [] ]

              let findings = findingsOf (Some BlockOnShip) views
              Expect.equal findings.[0].Cause (SourceDrift [ CoveredArtifactsCat ]) "drift carries the drifted categories"
              Expect.equal findings.[1].Cause (Undeterminable "no declared sources") "unresolved carries the reason"
          }

          test "NotEvaluated (out of scope) ⇒ pass; StaleUnresolved (in scope) ⇒ finding, never a silent pass (FR-008)" {
              let views =
                  [ decision "skip" NotEvaluated []
                    decision "unres" (StaleUnresolved "missing manifest entry") [] ]

              let findings = findingsOf (Some BlockOnShip) views
              Expect.equal (findings |> List.map (fun f -> f.ViewId)) [ "unres" ] "only the in-scope undeterminable view is a finding"
          } ]
