module FS.GG.Governance.GateExecution.Tests.SenseTests

open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.ExecutionRecord
open FS.GG.Governance.GateExecution
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateExecution.Tests.Support

// US1 — run a real gate and obtain its assembled CommandRecord (FR-002/FR-004, SC-001/SC-008). Driven BOTH
// through a deterministic fake port (no process) and the REAL port over `/bin/sh` temp-script fixtures. Output
// digests are DERIVED from real captured bytes (`ExecutionRecord.digestOf`), never literals.

[<Tests>]
let tests =
    testList
        "Sense"
        [
          // (1) digests of the captured bytes, in the CORRECT positions, never swapped (fake port) ──────────
          test "fake port: StdoutDigest/StderrDigest are digestOf the captured bytes (never swapped)" {
              let out = System.Text.Encoding.UTF8.GetBytes "the-stdout-bytes"
              let err = System.Text.Encoding.UTF8.GetBytes "different-stderr"
              let port = fakePort out err (ExitCode 0) (SensedDuration 1_000L)
              let record = Interpreter.senseExecution port baseCommand

              Expect.equal record.Reproducible.StdoutDigest (ExecutionRecord.digestOf out) "stdout → StdoutDigest"
              Expect.equal record.Reproducible.StderrDigest (ExecutionRecord.digestOf err) "stderr → StderrDigest"
              // The two streams have distinct content, so the two digests must differ (positions are honored).
              Expect.notEqual record.Reproducible.StdoutDigest record.Reproducible.StderrDigest "digests not swapped"
          }

          // (2) verbatim carriage of EVERY reproducible fact from the command (fake port) ──────────────────
          test "fake port: every reproducible fact is carried verbatim from the command" {
              let port = fakePort [| 1uy |] [| 2uy |] (ExitCode 0) (SensedDuration 5L)
              let record = Interpreter.senseExecution port baseCommand
              let r = record.Reproducible

              Expect.equal r.Executable baseCommand.Executable "executable"
              Expect.equal r.Arguments baseCommand.Arguments "arguments (in supplied order)"
              Expect.equal r.WorkingDirectory baseCommand.WorkingDirectory "working directory"
              Expect.equal r.Environment baseCommand.Environment "env delta (three classes preserved)"
              Expect.equal r.Timeout baseCommand.Timeout "timeout"
              Expect.equal r.CapturedOutput baseCommand.CapturedOutput "captured-output target"
              Expect.equal r.ExitCode (ExitCode 0) "exit code from the outcome"
              Expect.equal record.Duration (SensedDuration 5L) "duration from the outcome"
          }

          test "fake port: a reordered-arguments command assembles to a different record" {
              let port = fakePort [| 1uy |] [| 2uy |] (ExitCode 0) (SensedDuration 5L)
              let reordered = Build.command (arguments = [ Argument "beta"; Argument "alpha" ])
              Expect.notEqual
                  (CommandRecord.canonicalId (Interpreter.senseExecution port baseCommand))
                  (CommandRecord.canonicalId (Interpreter.senseExecution port reordered))
                  "argument order is significant"
          }

          test "fake port: a Changed env entry is never split into Added + Removed" {
              let port = fakePort [| 1uy |] [| 2uy |] (ExitCode 0) (SensedDuration 5L)
              let record = Interpreter.senseExecution port baseCommand
              Expect.equal record.Reproducible.Environment.Changed baseCommand.Environment.Changed "Changed preserved"
              Expect.isNonEmpty record.Reproducible.Environment.Changed "the base command has a Changed entry"
          }

          // (3) canonicalId defined for the fake-port record ─────────────────────────────────────────────
          test "fake port: canonicalId is defined and byte-stable" {
              let port = fakePort [| 1uy |] [| 2uy |] (ExitCode 0) (SensedDuration 5L)
              let record = Interpreter.senseExecution port baseCommand
              Expect.equal (CommandRecord.canonicalId record) (CommandRecord.canonicalId record) "byte-stable"
              Expect.isGreaterThan
                  (CommandRecord.identityValue (CommandRecord.canonicalId record)).Length
                  0
                  "canonicalId renders a non-empty identity"
          }

          // (4) THE REAL EDGE: a clean `/bin/sh` gate → ExitCode 0 + digests of real captured bytes ───────
          test "real port: a clean gate records ExitCode 0 and digests of the real captured bytes" {
              withTempDir (fun dir ->
                  let fx = cleanFixture dir
                  let record = Interpreter.senseExecution Interpreter.realPort fx.Command

                  Expect.equal record.Reproducible.ExitCode (ExitCode 0) "clean exit"
                  Expect.equal
                      record.Reproducible.StdoutDigest
                      (ExecutionRecord.digestOf fx.ExpectedStdout)
                      "real captured stdout → StdoutDigest"
                  Expect.equal
                      record.Reproducible.StderrDigest
                      (ExecutionRecord.digestOf fx.ExpectedStderr)
                      "real captured stderr → StderrDigest"
                  Expect.isGreaterThan
                      (CommandRecord.identityValue (CommandRecord.canonicalId record)).Length
                      0
                      "canonicalId defined for the real record")
          }

          test "real port: swapping the two streams yields a different record (positions are honored)" {
              withTempDir (fun dir ->
                  let normal = Interpreter.senseExecution Interpreter.realPort (cleanFixture dir).Command
                  // fresh dir to avoid script clobber
                  withTempDir (fun dir2 ->
                      let swapped = Interpreter.senseExecution Interpreter.realPort (swappedFixture dir2).Command
                      Expect.notEqual
                          (CommandRecord.canonicalId normal)
                          (CommandRecord.canonicalId swapped)
                          "stdout/stderr swapped ⇒ different identity"))
          }

          // (5) any size / any bytes captured and digested in FULL, no truncation/decoding (SC-008) ───────
          test "real port: empty / binary / large output each digest in full to digestOf the captured bytes" {
              for name, makeFx in
                  [ "empty", emptyFixture; "binary", binaryFixture; "large", largeFixture ] do
                  withTempDir (fun dir ->
                      let fx = makeFx dir
                      let record = Interpreter.senseExecution Interpreter.realPort fx.Command
                      Expect.equal record.Reproducible.ExitCode (ExitCode 0) (sprintf "%s: clean exit" name)
                      Expect.equal
                          record.Reproducible.StdoutDigest
                          (ExecutionRecord.digestOf fx.ExpectedStdout)
                          (sprintf "%s: stdout digested in full (no truncation/decoding)" name))
          } ]
