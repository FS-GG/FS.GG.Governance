module FS.GG.Governance.VerifyCommand.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T028 (US3) — the full `Interpreter.run` writing `verify.json` over a fixture twice yields a byte-identical
// artifact (SC-004); and the `--json` stdout string equals the persisted file verbatim (FR-007, one source of
// truth).

let private srcScope = Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]

let private runJson () =
    let cap = newCapture ()
    let req = { requestForProfile srcScope Loop.Json Strict with VerifyOut = "readiness/verify.json" }
    Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortFail cap) req |> ignore
    cap

[<Tests>]
let tests =
    testList
        "Determinism (US3)"
        [ test "two runs over identical state + outcomes write byte-identical verify.json" {
              let a = runJson ()
              let b = runJson ()

              match writtenVerify a, writtenVerify b with
              | Some(_, ca), Some(_, cb) -> Expect.equal ca cb "verify.json byte-identical across runs"
              | _ -> failtest "expected a verify.json write in both runs"
          }

          test "--json stdout equals the persisted file verbatim (one source of truth)" {
              let cap = runJson ()

              match writtenVerify cap with
              | Some(_, content) ->
                  let stdout = String.concat "\n" cap.Emits
                  Expect.equal stdout content "--json stdout == the persisted verify.json"
              | None -> failtest "expected a verify.json write" } ]
