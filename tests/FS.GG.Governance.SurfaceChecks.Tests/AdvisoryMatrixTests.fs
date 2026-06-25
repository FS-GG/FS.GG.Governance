module FS.GG.Governance.SurfaceChecks.Tests.AdvisoryMatrixTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.SurfaceChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

// T049 (US5) — a base-Advisory finding never escalates to Blocking under any (RunMode, Profile) pair.
// Verified against the REAL Enforcement core (never mocked).

let private modes = [ Sandbox; Inner; Focused; Verify; Gate; RunMode.Release ]
let private profiles = [ Light; Standard; Strict; Profile.Release ]

let private finding severity maturity : SC.SurfaceFinding =
    { Domain = SC.DocsDomain
      Surface = SurfaceId "s"
      Code = "docs.example-freshness"
      Location = { File = normalizePath "x"; Detail = "d" }
      BaseSeverity = severity
      Maturity = maturity
      EvidenceTag = None
      IsInputState = false
      Message = "m" }

[<Tests>]
let tests =
    testList
        "SurfaceChecks.advisoryMatrix"
        [ test "a base-Advisory finding is Advisory across every (RunMode, Profile) pair" {
              let advisory = finding Advisory BlockOnPr

              for mode in modes do
                  for profile in profiles do
                      let decision = deriveEffectiveSeverity (SC.enforcementInputOf advisory mode profile)

                      Expect.equal
                          decision.EffectiveSeverity
                          Advisory
                          (sprintf "advisory must stay advisory under (%A, %A)" mode profile)
          }

          test "a base-Blocking finding can block (it is the deterministic path that changes the verdict)" {
              let blocking = finding Blocking BlockOnPr
              // Under Strict at Verify the deterministic finding blocks (the floor is reached).
              let decision = deriveEffectiveSeverity (SC.enforcementInputOf blocking Verify Strict)
              Expect.equal decision.EffectiveSeverity Blocking "deterministic finding blocks at Verify under Strict"
          } ]
