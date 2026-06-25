module FS.GG.Governance.ReleaseReport.Tests.AssembleTests

open Expecto
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseReport
open FS.GG.Governance.ReleaseReport.Tests.Support

// SC-002 / FR-004 / FR-012: assemble carries the F53 Decision VERBATIM, names ReleaseExitCodeBasis at the
// report level, and orders Preconditions by releaseRuleKindOrdinal.

[<Tests>]
let tests =
    testList
        "assemble"
        [ test "carries the F53 Decision verbatim (same verdict, same exit-code basis)" {
              let sensed = sensedFrom allMet []
              let decision = decisionFor sensed
              let report = Report.assemble decision sensed packEvidence attestation
              Expect.equal report.Decision decision "decision carried verbatim, never re-derived"
              Expect.equal report.ReleaseExitCodeBasis decision.ExitCodeBasis "basis named at the report level"
          }

          test "carries the PackEvidenceSet + AttestationSummary unchanged" {
              let sensed = sensedFrom allMet []
              let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
              Expect.equal report.Package packEvidence ""
              Expect.equal report.Attestation attestation ""
          }

          test "a fully-releasable fixture ⇒ Verdict Pass, ReleaseExitCodeBasis Clean" {
              let sensed = sensedFrom allMet []
              let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
              Expect.equal report.Decision.Verdict Pass ""
              Expect.equal report.ReleaseExitCodeBasis Clean ""
          }

          test "a mergeable-but-not-releasable fixture (an unbumped version) ⇒ Fail/Blocked naming the precondition" {
              // ship would pass; release blocks because the VersionBump family is Unmet
              let sensed =
                  sensedFrom
                      [ VersionBump, Unmet
                        PackageMetadata, Met
                        TemplatePins, Met
                        PublishPlan, Met
                        TrustedPublishing, Met
                        Provenance, Met ]
                      [ VersionBump, "version not bumped past baseline" ]

              let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
              Expect.equal report.Decision.Verdict Fail "release is blocked"
              Expect.equal report.ReleaseExitCodeBasis Blocked "basis distinct from a ship pass"

              let versionPre = report.Preconditions |> List.find (fun p -> p.Kind = VersionBump)
              Expect.equal versionPre.State Unmet "the failing precondition is explicit"
          }

          test "Preconditions are ordered by releaseRuleKindOrdinal" {
              let sensed = sensedFrom allMet []
              let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
              let ordinals = report.Preconditions |> List.map (fun p -> Release.releaseRuleKindOrdinal p.Kind)
              Expect.equal ordinals (List.sort ordinals) "preconditions ordinal-ordered"
          } ]
