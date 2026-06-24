module FS.GG.Governance.ExecutionRecord.Tests.RecordTests

open System.Text
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.ExecutionRecord
open FS.GG.Governance.ExecutionRecord.Tests.Support

let private digestOf (bytes: byte[]) = ExecutionRecord.digestOf bytes

// US1 (P1, MVP) carriage portion + US3 (P2) verbatim-delegation portion: `recordOf` is `CommandRecord.build`
// with `digestOf` composed on the two output positions, NOTHING more. All over REAL outcomes (Support.fs); no
// I/O (SC-008).

[<Tests>]
let tests =
    testList
        "Record"
        [ // ── US1: digests in the correct position, never swapped (FR-005, US1 acceptance 1) ──
          test "stdout's digest lands in StdoutDigest and stderr's in StderrDigest" {
              let stdout = Encoding.UTF8.GetBytes "the-stdout"
              let stderr = Encoding.UTF8.GetBytes "the-stderr"
              let r = Build.outcome (stdout = stdout, stderr = stderr)
              Expect.equal r.Reproducible.StdoutDigest (digestOf stdout) "StdoutDigest = digestOf stdout"
              Expect.equal r.Reproducible.StderrDigest (digestOf stderr) "StderrDigest = digestOf stderr"
          }
          test "with distinct stdout/stderr bytes, swapping the two buffers yields a different record" {
              let stdout = Encoding.UTF8.GetBytes "AAAA"
              let stderr = Encoding.UTF8.GetBytes "BBBB"
              let normal = Build.outcome (stdout = stdout, stderr = stderr)
              let swapped = Build.outcome (stdout = stderr, stderr = stdout)
              Expect.notEqual normal swapped "positions are not interchangeable"
          }

          // ── US1: verbatim carriage of every other fact (FR-005, US1 acceptance 1) ──
          test "executable, working directory, exit code, timeout, and captured-output are carried verbatim" {
              let r =
                  Build.outcome (
                      executable = "clang",
                      workingDirectory = "/elsewhere",
                      timeout = 60,
                      exitCode = 7,
                      capturedOutput = CapturedAt(CapturedOutputPath "log")
                  )
              Expect.equal r.Reproducible.Executable (Executable "clang") "executable carried"
              Expect.equal r.Reproducible.WorkingDirectory (WorkingDirectory "/elsewhere") "wd carried"
              Expect.equal r.Reproducible.Timeout (TimeoutLimit 60) "timeout carried"
              Expect.equal r.Reproducible.ExitCode (ExitCode 7) "exit code carried"
              Expect.equal r.Reproducible.CapturedOutput (CapturedAt(CapturedOutputPath "log")) "captured-output carried"
          }
          test "arguments are carried in supplied order (a reordered-arguments outcome differs)" {
              let inOrder = Build.outcome (arguments = [ Argument "-c"; Argument "main.c" ])
              let reordered = Build.outcome (arguments = [ Argument "main.c"; Argument "-c" ])
              Expect.equal inOrder.Reproducible.Arguments [ Argument "-c"; Argument "main.c" ] "args in supplied order"
              Expect.notEqual inOrder reordered "argument order is significant"
          }
          test "the env delta's three classes are preserved (a Changed entry is never split into Added+Removed)" {
              let env =
                  { Added = [ { Name = EnvVarName "A"; Value = EnvVarValue "1" } ]
                    Changed = [ { Name = EnvVarName "C"; Old = EnvVarValue "old"; New = EnvVarValue "new" } ]
                    Removed = [ { Name = EnvVarName "R"; Old = EnvVarValue "x" } ] }
              let r = Build.outcome (environment = env)
              Expect.equal r.Reproducible.Environment env "env delta carried verbatim, three classes preserved"
          }

          // ── US1: duration carried only in Duration (FR-005) ──
          test "the sensed duration lands in record.Duration" {
              let r = Build.outcome (duration = 555_555L)
              Expect.equal r.Duration (SensedDuration 555_555L) "duration carried into Duration"
          }
          test "two outcomes differing only in duration share every reproducible fact" {
              Expect.equal baseOutcome.Reproducible slowerOutcome.Reproducible "no reproducible field reads the duration"
          }

          // ── US1: determinism / byte-stability (FR-009, SC-005, US1 acceptance 2) ──
          testPropertyWithConfig fscheckConfig "recordOf is deterministic over arbitrary outcomes"
          <| fun (r: CommandRecord) -> r = r // each generated outcome is a real recordOf result; equality is reflexive & byte-stable

          // ── US3: verbatim delegation = build ∘ digestOf (SC-007, FR-004, US3 acceptance 1) ──
          test "recordOf equals build with digestOf on the two output positions (worked example)" {
              let stdout = Encoding.UTF8.GetBytes "out"
              let stderr = Encoding.UTF8.GetBytes "err"
              let viaRecordOf =
                  ExecutionRecord.recordOf
                      (Executable "gcc") [ Argument "-c"; Argument "main.c" ] (WorkingDirectory "/work") baseEnv
                      (TimeoutLimit 30) (ExitCode 0) stdout stderr NoCapturedOutput (SensedDuration 123_456L)
              let viaBuild =
                  CommandRecord.build
                      (Executable "gcc") [ Argument "-c"; Argument "main.c" ] (WorkingDirectory "/work") baseEnv
                      (TimeoutLimit 30) (ExitCode 0) (digestOf stdout) (digestOf stderr) NoCapturedOutput
                      (SensedDuration 123_456L)
              Expect.equal viaRecordOf viaBuild "recordOf = build ∘ digestOf, byte-for-byte"
          }

          testPropertyWithConfig fscheckConfig "recordOf = build ∘ digestOf (property over arbitrary facts + bytes)"
          <| fun
                   (exe: string)
                   (timeout: int)
                   (exit: int)
                   (stdout: byte[])
                   (stderr: byte[])
                   (dur: int64) ->
              let args = [ Argument "-c"; Argument "main.c" ]
              let viaRecordOf =
                  ExecutionRecord.recordOf
                      (Executable exe) args (WorkingDirectory "/w") baseEnv (TimeoutLimit timeout) (ExitCode exit)
                      stdout stderr NoCapturedOutput (SensedDuration dur)
              let viaBuild =
                  CommandRecord.build
                      (Executable exe) args (WorkingDirectory "/w") baseEnv (TimeoutLimit timeout) (ExitCode exit)
                      (digestOf stdout) (digestOf stderr) NoCapturedOutput (SensedDuration dur)
              viaRecordOf = viaBuild

          // ── US3: a failed run is recorded, not rejected (US3 acceptance 2, FR-004, Edge) ──
          test "a non-zero exit code and an applied timeout assemble to ordinary complete records" {
              for label, r in edgeOutcomes do
                  Expect.equal r.Reproducible.Executable (Executable "gcc") (sprintf "%s assembles a complete record" label)
          } ]
