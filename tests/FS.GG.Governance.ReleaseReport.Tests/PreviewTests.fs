module FS.GG.Governance.ReleaseReport.Tests.PreviewTests

open Expecto
open FS.GG.Governance.ReleaseReport
open FS.GG.Governance.ReleaseReport.Tests.Support

// SC-003 / FR-005: preview is the advisory subset — same evidence, Advisory = true, never a blocking gate.

[<Tests>]
let tests =
    testList
        "preview"
        [ test "preview carries the report's evidence with Advisory = true" {
              let sensed = sensedFrom allMet []
              let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
              let p = Report.preview report
              Expect.isTrue p.Advisory "always advisory"
              Expect.equal p.Verdict report.Decision.Verdict "previewed verdict = the decision's verdict"
              Expect.equal p.Package report.Package "same package evidence"
              Expect.equal p.Preconditions report.Preconditions "same preconditions"
              Expect.equal p.Attestation report.Attestation "same attestation"
          }

          test "preview drops no preconditions" {
              let sensed = sensedFrom allMet []
              let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
              let p = Report.preview report
              Expect.equal (List.length p.Preconditions) (List.length report.Preconditions) ""
          } ]
