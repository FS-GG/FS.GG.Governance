module FS.GG.Governance.CommandRecord.Tests.RecordTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandRecord.Tests.Support

// US1 — `build` assembles the ten supplied facts into one complete `CommandRecord` from which each fact
// reads back verbatim (arguments in order; env delta in three distinct classes, a change counted once),
// total over failed / timed-out / argument-less / empty-delta runs (SC-001, SC-002).

[<Tests>]
let tests =
    testList
        "Record"
        [ // (a) Verbatim carriage of all ten facts (SC-001, US1 #1).
          test "build carries all ten facts back verbatim" {
              let r = baseRecord
              Expect.equal r.Reproducible.Executable baseExecutable "executable"
              Expect.equal r.Reproducible.Arguments baseArguments "arguments (same elements, same order)"
              Expect.equal r.Reproducible.WorkingDirectory baseWorkingDirectory "working directory"
              Expect.equal r.Reproducible.Environment baseEnvironment "environment delta"
              Expect.equal r.Reproducible.Timeout baseTimeout "timeout"
              Expect.equal r.Reproducible.ExitCode baseExitCode "exit code"
              Expect.equal r.Reproducible.StdoutDigest baseStdoutDigest "stdout digest"
              Expect.equal r.Reproducible.StderrDigest baseStderrDigest "stderr digest"
              Expect.equal r.Reproducible.CapturedOutput baseCapturedOutput "captured output"
              Expect.equal r.Duration baseDuration "duration"
          }

          test "build preserves argument order exactly" {
              let r =
                  CommandRecord.build
                      baseExecutable
                      [ Argument "z"; Argument "a"; Argument "m" ]
                      baseWorkingDirectory
                      baseEnvironment
                      baseTimeout
                      baseExitCode
                      baseStdoutDigest
                      baseStderrDigest
                      baseCapturedOutput
                      baseDuration

              Expect.equal r.Reproducible.Arguments [ Argument "z"; Argument "a"; Argument "m" ] "order kept verbatim, not sorted"
          }

          // (b) Env-delta three-class partition; a changed var counted once (SC-002, US1 #2, Edge).
          test "env delta is a three-class partition; a changed var appears once in Changed only" {
              let env =
                  { Added = [ { Name = EnvVarName "NEW"; Value = EnvVarValue "v" } ]
                    Changed = [ { Name = EnvVarName "PATH"; Old = EnvVarValue "/a"; New = EnvVarValue "/b" } ]
                    Removed = [ { Name = EnvVarName "OLD"; Old = EnvVarValue "g" } ] }

              let r =
                  CommandRecord.build
                      baseExecutable baseArguments baseWorkingDirectory env baseTimeout baseExitCode
                      baseStdoutDigest baseStderrDigest baseCapturedOutput baseDuration

              Expect.equal r.Reproducible.Environment.Added [ { Name = EnvVarName "NEW"; Value = EnvVarValue "v" } ] "Added carries exactly the added var"
              Expect.equal r.Reproducible.Environment.Changed [ { Name = EnvVarName "PATH"; Old = EnvVarValue "/a"; New = EnvVarValue "/b" } ] "Changed carries the changed var once, with Old+New"
              Expect.equal r.Reproducible.Environment.Removed [ { Name = EnvVarName "OLD"; Old = EnvVarValue "g" } ] "Removed carries exactly the removed var"

              // The changed var's name is NOT present in Added or Removed (never split into an add+remove pair).
              let changedName = EnvVarName "PATH"
              Expect.isFalse (r.Reproducible.Environment.Added |> List.exists (fun a -> a.Name = changedName)) "changed var not in Added"
              Expect.isFalse (r.Reproducible.Environment.Removed |> List.exists (fun a -> a.Name = changedName)) "changed var not in Removed"
          }

          // (c) Totality edge cases — each yields an ordinary complete record; build never throws.
          test "non-zero exit code is an ordinary complete record" {
              let r =
                  CommandRecord.build
                      baseExecutable baseArguments baseWorkingDirectory baseEnvironment baseTimeout
                      (ExitCode 137) baseStdoutDigest baseStderrDigest baseCapturedOutput baseDuration
              Expect.equal r.Reproducible.ExitCode (ExitCode 137) "failed run recorded, not rejected"
          }

          test "argument-less run is an ordinary complete record" {
              let r =
                  CommandRecord.build
                      baseExecutable [] baseWorkingDirectory baseEnvironment baseTimeout baseExitCode
                      baseStdoutDigest baseStderrDigest baseCapturedOutput baseDuration
              Expect.equal r.Reproducible.Arguments [] "empty argument list is ordinary"
          }

          test "entirely empty environment delta is an ordinary complete record" {
              let emptyEnv = { Added = []; Changed = []; Removed = [] }
              let r =
                  CommandRecord.build
                      baseExecutable baseArguments baseWorkingDirectory emptyEnv baseTimeout baseExitCode
                      baseStdoutDigest baseStderrDigest baseCapturedOutput baseDuration
              Expect.equal r.Reproducible.Environment emptyEnv "empty delta is ordinary, not an error"
          }

          test "NoCapturedOutput run is an ordinary complete record" {
              let r =
                  CommandRecord.build
                      baseExecutable baseArguments baseWorkingDirectory baseEnvironment baseTimeout baseExitCode
                      baseStdoutDigest baseStderrDigest NoCapturedOutput baseDuration
              Expect.equal r.Reproducible.CapturedOutput NoCapturedOutput "explicit absence recorded"
          }

          // (d) FsCheck totality: over generated facts, build always returns and round-trips every fact.
          testPropertyWithConfig fscheckConfig "build is total and round-trips every fact" <| fun (facts: ReproducibleFacts) (duration: SensedDuration) ->
              let r = rebuild facts duration
              r.Reproducible = facts && r.Duration = duration ]
