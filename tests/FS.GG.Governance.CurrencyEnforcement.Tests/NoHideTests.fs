module FS.GG.Governance.CurrencyEnforcement.Tests.NoHideTests

// T009 — no-hide totality (SC-004): a finding relaxed to effective Advisory is still produced and carries
// BOTH base and effective severity; relaxing never changes the carried Cause/CurrencyStatus. Plus
// staleCauseToken exhaustiveness (source-drift | undeterminable).
// Extended for T035 (US3): a relaxing profile at the active boundary yields a visible warning, cause unchanged.

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
        "CurrencyEnforcement.noHide"
        [ test "a block-on-ship finding under verify is produced and carries both severities (FR-006)" {
              let views = [ decision "v" (WouldRegenerate [ CoveredArtifactsCat ]) [ CoveredArtifactsCat ] ]
              let f = findingsOf (Some BlockOnShip) views |> List.exactlyOne
              let d = decisionOf f Verify Standard
              Expect.equal f.BaseSeverity Blocking "base severity preserved on the finding"
              Expect.equal d.BaseSeverity Blocking "decision echoes the base severity (no-hide)"
              Expect.equal d.EffectiveSeverity Advisory "relaxed to a warning under verify (FR-009)"
              Expect.isNonEmpty d.Reason "decision carries a lever-naming reason"
          }

          test "a relaxing profile yields a visible warning while the carried Cause is unchanged (T035/US3)" {
              let view = decision "v" (StaleUnresolved "lock unreadable") []
              let f = findingsOf (Some BlockOnPr) [ view ] |> List.exactlyOne
              let relaxed = decisionOf f Verify Light
              Expect.equal relaxed.EffectiveSeverity Advisory "light profile under verify relaxes block-on-pr to a warning"
              Expect.equal f.Cause (Undeterminable "lock unreadable") "the carried cause is never altered by relaxation"
          }

          test "staleCauseToken is exhaustive: source-drift | undeterminable" {
              Expect.equal (staleCauseToken (SourceDrift [ CoveredArtifactsCat ])) "source-drift" "drift token"
              Expect.equal (staleCauseToken (Undeterminable "r")) "undeterminable" "undeterminable token"
          } ]
