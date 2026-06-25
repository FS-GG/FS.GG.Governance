module FS.GG.Governance.ReleaseReport.Tests.PreconditionEvidenceTests

open Expecto
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseReport
open FS.GG.Governance.ReleaseReport.Tests.Support

// SC-004 / FR-006: the publish-plan / trusted-publishing / template-pin preconditions surface first-class with
// their state + reason, and an unmet one blocks the release (via the existing F53 families — no new family).

let private preconditionFor (states: (ReleaseRuleKind * FactState) list) diagnostics kind =
    let sensed = sensedFrom states diagnostics
    let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
    report, report.Preconditions |> List.find (fun p -> p.Kind = kind)

[<Tests>]
let tests =
    testList
        "precondition-evidence"
        [ test "a resolved publish plan ⇒ PublishPlan precondition Met" {
              let _, p = preconditionFor allMet [] PublishPlan
              Expect.equal p.State Met ""
          }

          test "a missing publish plan ⇒ PublishPlan Unmet and the release blocked naming it" {
              let states =
                  [ VersionBump, Met; PackageMetadata, Met; TemplatePins, Met
                    PublishPlan, Unmet; TrustedPublishing, Met; Provenance, Met ]

              let report, p = preconditionFor states [ PublishPlan, "no publish plan resolved" ] PublishPlan
              Expect.equal p.State Unmet ""
              Expect.equal report.Decision.Verdict Fail "an unmet publish plan blocks the release"
              Expect.stringContains p.Reason "publishPlan" "reason names the family"
          }

          test "an unconfigured trusted-publishing posture ⇒ TrustedPublishing Unmet" {
              let states =
                  [ VersionBump, Met; PackageMetadata, Met; TemplatePins, Met
                    PublishPlan, Met; TrustedPublishing, Unmet; Provenance, Met ]

              let _, p = preconditionFor states [] TrustedPublishing
              Expect.equal p.State Unmet ""
          }

          test "a drifted template pin ⇒ TemplatePins Unrecoverable surfaced" {
              let states =
                  [ VersionBump, Met; PackageMetadata, Met; TemplatePins, Unrecoverable
                    PublishPlan, Met; TrustedPublishing, Met; Provenance, Met ]

              let report, p = preconditionFor states [] TemplatePins
              Expect.equal p.State Unrecoverable ""
              Expect.equal report.Decision.Verdict Fail "an unrecoverable pin blocks the release"
          }

          test "a fully-satisfied set ⇒ a clean decision (never assumed satisfied)" {
              let report, _ = preconditionFor allMet [] PublishPlan
              Expect.equal report.Decision.Verdict Pass ""
              Expect.equal report.ReleaseExitCodeBasis Clean ""
          } ]
