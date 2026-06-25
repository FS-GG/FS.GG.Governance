module FS.GG.Governance.RefreshCommand.Tests.DeterminismTests

open System.Collections.Generic
open Expecto
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// US3 — the full `Interpreter.run` produces a byte-deterministic `refresh.json`, and the `--json` stdout is
// the verbatim persisted bytes (one source of truth, FR-007/SC-004).

let private seedSources d = writeFile d "src.txt" "hello\n"

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "two fresh identical repos ⇒ byte-identical refresh.json" {
              let runOnce () =
                  withTempRepo refreshYmlOneView seedSources (fun repo ->
                      runReal repo { requestFor repo with Format = Loop.TextAndJson } |> ignore
                      readFile repo "refresh.json")

              Expect.equal (runOnce ()) (runOnce ()) "identical state + outcome ⇒ identical bytes"
          }

          test "--json stdout equals the persisted refresh.json verbatim" {
              withTempRepo refreshYmlOneView seedSources (fun repo ->
                  let captured = List<string>()

                  let ports =
                      { Interpreter.realPorts repo with Out = fun s -> captured.Add s }

                  Interpreter.run ports { requestFor repo with Format = Loop.Json } |> ignore

                  let persisted = readFile repo "refresh.json"
                  let stdout = String.concat "\n" (List.ofSeq captured)
                  Expect.equal stdout persisted "--json prints exactly what it wrote")
          } ]
