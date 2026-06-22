module FS.GG.Governance.CacheEligibilityCommand.Tests.FailureTests

open Expecto
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T024 (Edge, FR-010/C2) — failure short-circuits map to distinct non-zero exit codes, write NO partial
// artifact, emit a structured diagnostic naming the input, and `run` never throws (L11). Plus the success
// overwrite path is exercised by InterpreterTests/EndToEndTests (atomic temp+rename).

let private git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

let private run files g sensor store failPaths =
    let req = requestFor Loop.DefaultRange Loop.Human
    let cap = newCapture ()
    let ports = fakePortsFailingWrites files g sensor store cap failPaths req
    req, cap, Interpreter.run ports req

let private hasDiagnostic (model: Loop.Model) (substr: string) =
    model.Diagnostics |> List.exists (fun d -> d.Message.Contains substr)

[<Tests>]
let tests =
    testList
        "Failure"
        [ test "not-a-git-repo ⇒ InputUnavailable (exit 3), no artifact written" {
              let _, cap, model = run validCatalog gitNotRepo fixedSensor (storeReaderOf (Ok None)) Set.empty
              Expect.equal model.Exit Loop.InputUnavailable "not-a-repo ⇒ InputUnavailable"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit 3"
              Expect.isEmpty cap.Writes "no artifact on a sensing failure"
          }

          test "declared catalog absent ⇒ InputUnavailable (exit 3), no artifact (C2: missing input)" {
              let _, cap, model = run Map.empty git fixedSensor (storeReaderOf (Ok None)) Set.empty
              Expect.equal model.Exit Loop.InputUnavailable "absent catalog ⇒ InputUnavailable"
              Expect.isEmpty cap.Writes "no artifact"
          }

          test "invalid catalog ⇒ ToolError (exit 4), no artifact, structured diagnostic" {
              let _, cap, model = run invalidCatalog git fixedSensor (storeReaderOf (Ok None)) Set.empty
              Expect.equal model.Exit Loop.ToolError "invalid catalog ⇒ ToolError"
              Expect.equal (Loop.exitCode model.Exit) 4 "exit 4"
              Expect.isEmpty cap.Writes "no artifact"
              Expect.isTrue (hasDiagnostic model "catalog invalid") "names the malformed input"
          }

          test "malformed present store ⇒ ToolError (exit 4), no artifact" {
              let _, cap, model = run validCatalog git fixedSensor (storeReaderOf (Error "unexpected end of JSON")) Set.empty
              Expect.equal model.Exit Loop.ToolError "malformed store ⇒ ToolError"
              Expect.isEmpty cap.Writes "no artifact written on a malformed store"
              Expect.isTrue (hasDiagnostic model "reuse store malformed") "names the malformed store"
          }

          test "unwritable output ⇒ ToolError (exit 4), NO partial artifact (FR-010)" {
              let req = requestFor Loop.DefaultRange Loop.Human
              // Both output paths fail (the realistic unwritable-output dir) ⇒ nothing is left on disk.
              let _, cap, model = run validCatalog git fixedSensor (storeReaderOf (Ok None)) (Set.ofList [ req.CacheOut; req.UnresolvedOut ])
              Expect.equal model.Exit Loop.ToolError "unwritable output ⇒ ToolError"
              Expect.isEmpty cap.Writes "no partial artifact left on a failed write"
              Expect.isTrue (hasDiagnostic model "failed to write artifact") "names the write failure"
          } ]
