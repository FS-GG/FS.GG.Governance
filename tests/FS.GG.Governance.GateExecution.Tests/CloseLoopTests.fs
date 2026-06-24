module FS.GG.Governance.GateExecution.Tests.CloseLoopTests

open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.GateExecution
open FS.GG.Governance.GateExecution.Tests.Support

// US1 acceptance 3 (SC-001) — close the chain over the REAL F049/F030 operations from a record
// `senseExecution` assembled: a genuinely executed gate derives a reproducible F049 reference and a reusable
// F030 store entry. Driven once via a fake port (deterministic) and once via the real clean fixture.

[<Tests>]
let tests =
    testList
        "CloseLoop"
        [
          // (1) referenceOf reproducible over a fake-port record ─────────────────────────────────────────
          test "fake port: referenceOf of an assembled record is defined and reproducible" {
              let port = fakePort (System.Text.Encoding.UTF8.GetBytes "hello\n") [||] (ExitCode 0) (SensedDuration 1L)
              let record = Interpreter.senseExecution port baseCommand
              Expect.equal
                  (EvidenceCapture.referenceOf record)
                  (EvidenceCapture.referenceOf record)
                  "referenceOf is byte-stable"
          }

          // (2) capture makes the world reusable for the derived reference (fake port) ────────────────────
          test "fake port: capture into the empty store makes the world reusable for the derived reference" {
              let port = fakePort (System.Text.Encoding.UTF8.GetBytes "hello\n") [||] (ExitCode 0) (SensedDuration 1L)
              let record = Interpreter.senseExecution port baseCommand
              let world = inputs "build:main"
              let grown = EvidenceCapture.capture world record EvidenceReuse.empty
              Expect.equal
                  (EvidenceReuse.decide world grown)
                  (Reuse(EvidenceCapture.referenceOf record))
                  "captured world reusable with the derived reference"
          }

          test "fake port: an unrelated world is still Recompute after capture (recompute-safety)" {
              let port = fakePort (System.Text.Encoding.UTF8.GetBytes "hello\n") [||] (ExitCode 0) (SensedDuration 1L)
              let record = Interpreter.senseExecution port baseCommand
              let world = inputs "build:main"
              let grown = EvidenceCapture.capture world record EvidenceReuse.empty
              Expect.equal
                  (EvidenceReuse.decide differentInputs grown)
                  (Recompute NoPriorEvidence)
                  "capture added no spurious match for a different world"
          }

          // (3) THE REAL EDGE: the loop closes from a GENUINELY EXECUTED gate ─────────────────────────────
          test "real port: a real executed gate's record closes the loop (capture ⇒ reusable)" {
              withTempDir (fun dir ->
                  let record = Interpreter.senseExecution Interpreter.realPort (cleanFixture dir).Command
                  let world = inputs "build:real"
                  let grown = EvidenceCapture.capture world record EvidenceReuse.empty
                  Expect.equal
                      (EvidenceReuse.decide world grown)
                      (Reuse(EvidenceCapture.referenceOf record))
                      "a gate the system actually ran becomes reusable evidence")
          } ]
