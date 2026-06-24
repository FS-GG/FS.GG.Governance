module FS.GG.Governance.ReleaseRules.Tests.FailSafeTests

open Expecto
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// US1 (acc. 3, FR-005, SC-005): an absent / unrecoverable governing fact fails SAFE to `Violated`, never
// `Satisfied`; and `Unmet` vs `Unrecoverable` carry DISTINCT reason text ("not met" vs "no recoverable
// evidence") so "no evidence" is never conflated with "satisfied".

[<Tests>]
let tests =
    testList
        "FailSafeTests"
        [ test "an Unrecoverable fact ⇒ Violated with the distinct 'no recoverable evidence' reason" {
              let rule = blocking Provenance "pkg"
              let f = List.exactlyOne (Release.evaluate [ rule ] (factsOf [ Provenance, Unrecoverable ]))
              Expect.equal f.Outcome Violated "an unrecoverable fact ⇒ Violated (never Satisfied)"
              Expect.stringContains f.Reason "no recoverable evidence" "reason names the missing evidence"
          }

          test "a kind absent from facts.States resolves through factFor to Unrecoverable ⇒ Violated" {
              let rule = blocking TrustedPublishing "pkg"
              // facts supply a DIFFERENT kind, so TrustedPublishing is absent.
              let facts = factsOf [ VersionBump, Met ]
              Expect.equal (Release.factFor facts TrustedPublishing) Unrecoverable "absent key ⇒ Unrecoverable"

              let f = List.exactlyOne (Release.evaluate [ rule ] facts)
              Expect.equal f.Outcome Violated "the absent-key fail-safe ⇒ Violated"
          }

          test "Unmet and Unrecoverable produce distinct reason text" {
              let rule = blocking PublishPlan "pkg"
              let unmet = List.exactlyOne (Release.evaluate [ rule ] (factsOf [ PublishPlan, Unmet ]))
              let unrec = List.exactlyOne (Release.evaluate [ rule ] (factsOf [ PublishPlan, Unrecoverable ]))
              Expect.equal unmet.Outcome Violated "Unmet ⇒ Violated"
              Expect.equal unrec.Outcome Violated "Unrecoverable ⇒ Violated"
              Expect.notEqual unmet.Reason unrec.Reason "the two violated reasons are distinct"
          } ]
