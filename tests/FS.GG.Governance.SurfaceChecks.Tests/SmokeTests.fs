module FS.GG.Governance.SurfaceChecks.Tests.SmokeTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.SurfaceChecks.Dispatch
open FS.GG.Governance.SurfaceChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

// T013 — exercise the public surface composes: Composition.run, enforcementInputOf, the token tables.
[<Tests>]
let tests =
    testList
        "SurfaceChecks.smoke"
        [ test "Composition.run over an empty report ⇒ [] (valid success)" {
              let result = Composition.run (typedFacts []) (report []) Composition.emptyBundle
              Expect.isEmpty result "empty report ⇒ no findings"
          }

          test "enforcementInputOf builds the F023 input verbatim from a finding" {
              let finding: SC.SurfaceFinding =
                  { Domain = SC.PackageDomain
                    Surface = SurfaceId "s"
                    Code = "package.baseline-drift"
                    Location = { File = normalizePath "x"; Detail = "d" }
                    BaseSeverity = Blocking
                    Maturity = BlockOnPr
                    EvidenceTag = None
                    IsInputState = false
                    Message = "m" }

              let input = SC.enforcementInputOf finding Verify Strict
              Expect.equal input.BaseSeverity Blocking "base severity carried"
              Expect.equal input.Maturity BlockOnPr "maturity carried"
              Expect.equal input.Mode Verify "mode carried"
              Expect.equal input.Profile Strict "profile carried"
          }

          test "token tables are total and stable" {
              Expect.equal (SC.checkDomainToken SC.PackageDomain) "package" "package token"
              Expect.equal (SC.checkDomainToken SC.DesignDomain) "design" "design token"
              Expect.equal (SC.severityToken Blocking) "blocking" "blocking token"
              Expect.equal (SC.severityToken Advisory) "advisory" "advisory token"
          } ]
