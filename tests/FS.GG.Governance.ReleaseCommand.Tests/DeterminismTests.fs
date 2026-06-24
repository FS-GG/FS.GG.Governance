module FS.GG.Governance.ReleaseCommand.Tests.DeterminismTests

open System.IO
open Expecto
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// A full `Interpreter.run --format both` over a fixture twice ⇒ byte-identical artifact (SC-003); the text
// summary and the JSON report the SAME verdict for the same run (FR-009/US2.4).

let private runBoth repo =
    let written = ResizeArray<string * string>()
    let outs = ResizeArray<string>()
    let ports = portsWith repo (fun p c -> written.Add(p, c); Ok()) (fun s -> outs.Add s)

    let request =
        { Loop.Repo = repo
          Loop.Format = Loop.TextAndJson
          Loop.ReleaseOut = Path.Combine(repo, "release.json") }

    let model = Interpreter.run ports request
    model, snd written.[0], String.concat "\n" (List.ofSeq outs)

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "two runs over identical state ⇒ byte-identical release.json (SC-003)" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let _, json1, _ = runBoth repo
                  let _, json2, _ = runBoth repo
                  Expect.equal json1 json2 "release.json bytes are identical across runs")
          }

          test "the text summary and the JSON agree on the verdict (FR-009)" {
              withTempRepo releaseYmlAllBlocking writeUnbumpedSources (fun repo ->
                  let _, json, text = runBoth repo
                  Expect.isTrue (json.Contains "\"verdict\":\"fail\"") "json says fail"
                  Expect.isTrue (text.Contains "verdict fail") "text says fail")
          } ]
