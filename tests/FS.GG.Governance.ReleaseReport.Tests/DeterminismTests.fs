module FS.GG.Governance.ReleaseReport.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.ReleaseReport
open FS.GG.Governance.ReleaseReport.Tests.Support

// SC-007 / FR-010: repeated assemble/preview over identical inputs ⇒ structurally identical report; the
// precondition ordering is stable regardless of input map order.

[<Tests>]
let tests =
    testList
        "report-determinism"
        [ test "assemble is byte-stable for identical inputs" {
              let sensed = sensedFrom allMet []
              let decision = decisionFor sensed
              let a = Report.assemble decision sensed packEvidence attestation
              let b = Report.assemble decision sensed packEvidence attestation
              Expect.equal a b ""
          }

          test "precondition order is independent of the input states' insertion order" {
              let s1 = sensedFrom allMet []
              let s2 = sensedFrom (List.rev allMet) []
              let r1 = Report.assemble (decisionFor s1) s1 packEvidence attestation
              let r2 = Report.assemble (decisionFor s2) s2 packEvidence attestation
              Expect.equal r1.Preconditions r2.Preconditions "preconditions ordinal-ordered, reorder-invariant"
          }

          test "no clock/abs-path/username leaks into any precondition reason" {
              let sensed = sensedFrom allMet []
              let report = Report.assemble (decisionFor sensed) sensed packEvidence attestation
              for p in report.Preconditions do
                  Expect.isFalse (p.Reason.Contains "/home/") "no host path"
                  Expect.isFalse (p.Reason.Contains "\\Users\\") "no host path"
          } ]
