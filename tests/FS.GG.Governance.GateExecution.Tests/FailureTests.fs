module FS.GG.Governance.GateExecution.Tests.FailureTests

open System.Diagnostics
open Expecto
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.ExecutionRecord
open FS.GG.Governance.GateExecution
open FS.GG.Governance.GateExecution.Tests.Support

// US2 — a failed, missing, or overrunning gate is RECORDED, never thrown or hung (FR-005/6/7/8,
// SC-002/3/4). All driven through the REAL port over `/bin/sh` fixtures. Principle VI: total & safe.

[<Tests>]
let tests =
    testList
        "Failure"
        [
          // (1) non-zero exit recorded, not rejected ─────────────────────────────────────────────────────
          test "real port: a gate exiting 7 records ExitCode 7 with its output digested (not rejected)" {
              withTempDir (fun dir ->
                  let fx = exit7Fixture dir
                  let record = Interpreter.senseExecution Interpreter.realPort fx.Command
                  Expect.equal record.Reproducible.ExitCode (ExitCode 7) "the real non-zero exit code is recorded"
                  Expect.equal
                      record.Reproducible.StdoutDigest
                      (ExecutionRecord.digestOf fx.ExpectedStdout)
                      "captured output is digested even on failure"
                  Expect.equal
                      record.Reproducible.StderrDigest
                      (ExecutionRecord.digestOf fx.ExpectedStderr)
                      "captured stderr is digested even on failure")
          }

          // (2) missing executable → recorded failure, no throw ──────────────────────────────────────────
          test "real port: a missing executable records startFailureExitCode + a captured diagnostic, no throw" {
              let cmd = missingExecutableCommand ()
              // Must not throw — capture the record (a throw would fail the test here).
              let record = Interpreter.senseExecution Interpreter.realPort cmd
              Expect.equal record.Reproducible.ExitCode Interpreter.startFailureExitCode "reified as startFailureExitCode"
              // The diagnostic (the failure message) is captured in the stderr bytes — a NON-EMPTY buffer,
              // so its digest differs from the empty-bytes digest.
              Expect.notEqual
                  record.Reproducible.StderrDigest
                  (ExecutionRecord.digestOf [||])
                  "a non-empty diagnostic is captured in the stderr bytes"
          }

          // (3) timeout bounded: terminated within a bounded time, never the full overrun, never hangs ────
          test "real port: an overrunning gate is terminated as timeoutExitCode within a bounded time" {
              withTempDir (fun dir ->
                  let fx = timeoutFixture dir // 1s TimeoutLimit; the script sleeps 30s
                  let sw = Stopwatch.StartNew()
                  let record = Interpreter.senseExecution Interpreter.realPort fx.Command
                  sw.Stop()

                  Expect.equal record.Reproducible.ExitCode Interpreter.timeoutExitCode "reified as timeoutExitCode"
                  // Returned in a bounded time — comfortably under the script's 30s sleep (not the full overrun).
                  Expect.isLessThan sw.Elapsed.TotalSeconds 15.0 "returns bounded, never the full overrun / hang")
          }

          // (3b) a very large TimeoutLimit must not overflow into a bogus start failure (#56/B3) ────────────
          test "real port: a huge TimeoutLimit runs the fast command and records its real exit (no overflow start-failure)" {
              // `TimeoutLimit(Int32.MaxValue)` made `seconds * 1000` overflow int32 to a negative wait; the old
              // code threw and reified startFailureExitCode (leaking the started process). The clamp keeps the
              // wait non-negative, so /bin/echo completes and records ExitCode 0.
              let record = Interpreter.senseExecution Interpreter.realPort (hugeTimeoutFastCommand ())
              Expect.equal record.Reproducible.ExitCode (ExitCode 0) "echo exits 0 under a huge timeout"
              Expect.notEqual
                  record.Reproducible.ExitCode
                  Interpreter.startFailureExitCode
                  "a huge timeout is NOT misreported as a start failure"
          }

          // (4) clean exit + a pipe-holding background child does NOT hang the port (M-CORE-2 / H2) ─────────
          test "real port: a clean exit whose backgrounded child keeps the pipes open still returns bounded" {
              withTempDir (fun dir ->
                  let fx = pipeHoldingChildFixture dir // exits 0 at once; a backgrounded `sleep 30` holds fd 1/2
                  let sw = Stopwatch.StartNew()
                  let record = Interpreter.senseExecution Interpreter.realPort fx.Command
                  sw.Stop()

                  // The gate itself exited 0 within its timeout — this is the CLEAN-exit path, not a timeout.
                  Expect.equal record.Reproducible.ExitCode (ExitCode 0) "the gate's clean exit code is recorded"
                  // The never-EOF streams must not stall the drain: bounded (~5s), never the child's 30s sleep.
                  Expect.isLessThan sw.Elapsed.TotalSeconds 15.0 "clean-exit drain is bounded, never hangs on the child")
          } ]
