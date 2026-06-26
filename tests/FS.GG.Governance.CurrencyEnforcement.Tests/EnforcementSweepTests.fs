module FS.GG.Governance.CurrencyEnforcement.Tests.EnforcementSweepTests

// T008 — the truth-table sweep (SC-003): for every Maturity × RunMode × Profile, decisionOf reproduces the
// REAL F023 deriveEffectiveSeverity exactly — proving 0 new truth-table branches in the leaf.
// Also pins the C1 / FR-009 verify-boundary reality: block-on-pr's floor is the gate run mode, so it blocks
// under verify ONLY under a strict (or release) profile that tightens the floor down to verify.

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement

let private maturities = [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]
let private modes = [ Sandbox; Inner; Focused; Verify; Gate; RunMode.Release ]
let private profiles = [ Light; Standard; Strict; Profile.Release ]

let private finding (maturity: Maturity) : CurrencyFinding =
    { ViewId = "v"
      Kind = RouteProjection
      Cause = SourceDrift [ CoveredArtifactsCat ]
      BaseSeverity = Blocking
      Maturity = maturity }

[<Tests>]
let tests =
    testList
        "CurrencyEnforcement.enforcementSweep"
        [ test "decisionOf equals deriveEffectiveSeverity across every Maturity × RunMode × Profile (SC-003)" {
              for m in maturities do
                  for mode in modes do
                      for p in profiles do
                          let viaLeaf = decisionOf (finding m) mode p

                          let viaCore =
                              deriveEffectiveSeverity
                                  { BaseSeverity = Blocking
                                    Maturity = m
                                    Mode = mode
                                    Profile = p }

                          Expect.equal viaLeaf viaCore (sprintf "leaf decision must equal the truth table at (%A, %A, %A)" m mode p)
          }

          test "block-on-pr: WARNING under verify+standard, BLOCKER under verify+strict, BLOCKER at ship (C1/FR-009)" {
              let f = finding BlockOnPr
              Expect.equal (decisionOf f Verify Standard).EffectiveSeverity Advisory "standard profile: a warning under verify"
              Expect.equal (decisionOf f Verify Strict).EffectiveSeverity Blocking "strict profile tightens the floor down to verify"
              Expect.equal (decisionOf f Gate Standard).EffectiveSeverity Blocking "blocks at the gate (fsgg ship)"
          }

          test "observe/warn dials never block at any run mode or profile (US2 acceptance #2 / T033)" {
              for dial in [ Observe; Warn ] do
                  for mode in modes do
                      for p in profiles do
                          Expect.equal
                              (decisionOf (finding dial) mode p).EffectiveSeverity
                              Advisory
                              (sprintf "%A must stay advisory at (%A, %A)" dial mode p)
          } ]
