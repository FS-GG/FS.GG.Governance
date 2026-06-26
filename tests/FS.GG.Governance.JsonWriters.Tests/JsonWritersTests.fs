module FS.GG.Governance.JsonWriters.Tests.JsonWritersTests

open Expecto
open FS.GG.Governance.JsonText
open FS.GG.Governance.JsonWriters
open FS.GG.Governance.Gates.Model              // GateId
open FS.GG.Governance.GateRun.Model            // GateOutcome, GateDisposition
open FS.GG.Governance.CommandRecord.Model      // ExitCode
open FS.GG.Governance.EvidenceReuse.Model      // RecomputeCause, InputCategory via FreshnessKey
open FS.GG.Governance.FreshnessKey.Model       // InputCategory
open FS.GG.Governance.CacheEligibility.Model   // CacheEligibilityReport, entry, verdict

// Semantic tests for the 073 sub-object/map writer leaf, exercising the PUBLIC surface over REAL,
// literally-constructed domain values (Principle V — real values, no mocks). The emitted byte-shape is
// exactly what the *Json projection goldens depend on. Writer output is captured through a real
// Utf8JsonWriter via the JsonText leaf.

let private render f = JsonText.writeToString f

[<Tests>]
let tests =
    testList
        "JsonWriters"
        [ test "writeCause emits the tagged noPriorEvidence object (no categories field)" {
              let actual = render (fun w -> JsonWriters.writeCause w NoPriorEvidence)
              Expect.equal actual """{"kind":"noPriorEvidence"}""" "noPriorEvidence"
          }

          test "writeCause emits inputsChanged with categories in order" {
              let actual =
                  render (fun w -> JsonWriters.writeCause w (InputsChanged [ CheckIdentity; DomainIdentity ]))

              Expect.equal actual """{"kind":"inputsChanged","categories":["check","domain"]}""" "inputsChanged"
          }

          test "writeExecution emits disposition/exitCode/passed for an executed gate" {
              let outcome =
                  { GateId = GateId "g1"
                    Disposition = Executed
                    ExitCode = Some(ExitCode 0)
                    Passed = Some true }

              let actual = render (fun w -> JsonWriters.writeExecution w outcome)
              Expect.equal actual """{"disposition":"executed","exitCode":0,"passed":true}""" "executed"
          }

          test "writeExecution omits exitCode/passed for a not-executed gate (camelCase notExecuted)" {
              let outcome =
                  { GateId = GateId "g1"
                    Disposition = NotExecuted
                    ExitCode = None
                    Passed = None }

              let actual = render (fun w -> JsonWriters.writeExecution w outcome)
              Expect.equal actual """{"disposition":"notExecuted"}""" "notExecuted omits fields"
          }

          test "outcomeByGate keys by gate-id string, first-by-list-order-wins" {
              let a =
                  { GateId = GateId "g1"
                    Disposition = Executed
                    ExitCode = Some(ExitCode 0)
                    Passed = Some true }

              let b =
                  { GateId = GateId "g1"
                    Disposition = NotExecuted
                    ExitCode = None
                    Passed = None }

              let m = JsonWriters.outcomeByGate [ (GateId "g1", a); (GateId "g1", b) ]
              Expect.equal (Map.find "g1" m) a "first entry by list order wins"
          }

          test "verdictByGate keys by gate-id string, first-by-report-order-wins" {
              let report =
                  CacheEligibilityReport
                      [ { Gate = GateId "g1"; Verdict = MustRecompute NoPriorEvidence }
                        { Gate = GateId "g1"; Verdict = MustRecompute(InputsChanged []) } ]

              let m = JsonWriters.verdictByGate report
              Expect.equal (Map.find "g1" m) (MustRecompute NoPriorEvidence) "first entry by report order wins"
          } ]
