module FS.GG.Governance.GateExecution.Tests.IdentityTests

open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.GateExecution
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateExecution.Tests.Support

// US3 — the reproducible identity is stable across runs; only the duration varies (FR-009, SC-005/6). No new
// implementation — `senseExecution` delegates to F050 `recordOf`, which excludes the duration (F050 FR-006);
// these assertions pin the property at the EDGE.

let private baseOut: byte[] = [| 10uy; 20uy; 30uy |]
let private baseErr: byte[] = [| 40uy; 50uy |]

/// Assemble a record through the fake port — perturb the command and/or the sensed outcome independently.
let private mk (cmd: GateCommand) (out: byte[]) (err: byte[]) (exit: ExitCode) (dur: int64) =
    Interpreter.senseExecution (fakePort out err exit (SensedDuration dur)) cmd

let private baseRec = mk baseCommand baseOut baseErr (ExitCode 0) 100L

[<Tests>]
let tests =
    testList
        "Identity"
        [
          // (1) stable across runs — a deterministic real gate run twice → byte-identical identity ─────────
          test "real port: two runs of a deterministic gate yield byte-identical canonicalId despite duration" {
              withTempDir (fun dir ->
                  let cmd = (cleanFixture dir).Command
                  let run1 = Interpreter.senseExecution Interpreter.realPort cmd
                  let run2 = Interpreter.senseExecution Interpreter.realPort cmd
                  Expect.equal
                      (CommandRecord.canonicalId run1)
                      (CommandRecord.canonicalId run2)
                      "canonicalId is stable across runs"
                  Expect.equal
                      (EvidenceCapture.referenceOf run1)
                      (EvidenceCapture.referenceOf run2)
                      "the derived F049 reference is stable across runs")
          }

          // (2) sensitivity — perturbing exactly ONE reproducible input changes the identity + reference ───
          test "perturbing any one reproducible input changes both canonicalId and referenceOf" {
              let baseId = CommandRecord.canonicalId baseRec
              let baseRef = EvidenceCapture.referenceOf baseRec

              let variants =
                  [ "stdout byte", mk baseCommand [| 10uy; 20uy; 31uy |] baseErr (ExitCode 0) 100L
                    "stderr byte", mk baseCommand baseOut [| 40uy; 51uy |] (ExitCode 0) 100L
                    "argument value", mk (Build.command (arguments = [ Argument "alpha"; Argument "GAMMA" ])) baseOut baseErr (ExitCode 0) 100L
                    "argument order", mk (Build.command (arguments = [ Argument "beta"; Argument "alpha" ])) baseOut baseErr (ExitCode 0) 100L
                    "working directory", mk (Build.command (workingDirectory = "/elsewhere")) baseOut baseErr (ExitCode 0) 100L
                    "env Added", mk (Build.command (environment = { baseEnv with Added = baseEnv.Added @ [ { Name = EnvVarName "X"; Value = EnvVarValue "2" } ] })) baseOut baseErr (ExitCode 0) 100L
                    "env Changed", mk (Build.command (environment = { baseEnv with Changed = [ { Name = EnvVarName "FSGG_CHANGED"; Old = EnvVarValue "old"; New = EnvVarValue "OTHER" } ] })) baseOut baseErr (ExitCode 0) 100L
                    "env Removed", mk (Build.command (environment = { baseEnv with Removed = [ { Name = EnvVarName "OTHER_REMOVED"; Old = EnvVarValue "x" } ] })) baseOut baseErr (ExitCode 0) 100L
                    "timeout", mk (Build.command (timeout = 60)) baseOut baseErr (ExitCode 0) 100L
                    "captured output", mk (Build.command (capturedOutput = CapturedAt(CapturedOutputPath "x"))) baseOut baseErr (ExitCode 0) 100L
                    "exit code", mk baseCommand baseOut baseErr (ExitCode 1) 100L ]

              for label, variant in variants do
                  Expect.notEqual (CommandRecord.canonicalId variant) baseId (sprintf "%s changes canonicalId" label)
                  Expect.notEqual (EvidenceCapture.referenceOf variant) baseRef (sprintf "%s changes referenceOf" label)
          }

          // (3) duration-invariance — duration-only difference does NOT change identity / reference ────────
          test "two outcomes differing only in SensedDuration share canonicalId and referenceOf" {
              let slower = mk baseCommand baseOut baseErr (ExitCode 0) 999_999L
              Expect.equal (CommandRecord.canonicalId baseRec) (CommandRecord.canonicalId slower) "duration-invariant id"
              Expect.equal (EvidenceCapture.referenceOf baseRec) (EvidenceCapture.referenceOf slower) "duration-invariant ref"
          }

          testPropertyWithConfig fscheckConfig "duration never leaks into the identity (property)"
          <| fun (cmd: GateCommand) (out: byte[]) (err: byte[]) (d1: int64) (d2: int64) ->
              let a = Interpreter.senseExecution (fakePort out err (ExitCode 0) (SensedDuration d1)) cmd
              let b = Interpreter.senseExecution (fakePort out err (ExitCode 0) (SensedDuration d2)) cmd
              CommandRecord.canonicalId a = CommandRecord.canonicalId b
              && EvidenceCapture.referenceOf a = EvidenceCapture.referenceOf b ]
