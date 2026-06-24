module FS.GG.Governance.ReleaseCommand.Tests.PersistenceEdgeTests

open System.IO
open Expecto
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// A failed/interrupted `Write` leaves no partial `release.json` (FR-012/US2.3) using a faked failing
// `ArtifactWriter`: the command maps the failure to `ToolError` and no artifact appears on disk.

[<Tests>]
let tests =
    testList
        "PersistenceEdge"
        [ test "a failing ArtifactWriter ⇒ ToolError and no release.json on disk" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let outPath = Path.Combine(repo, "release.json")
                  let ports = portsWith repo (fun _ _ -> Error "simulated write failure") ignore

                  let request =
                      { Loop.Repo = repo
                        Loop.Format = Loop.TextAndJson
                        Loop.ReleaseOut = outPath }

                  let model = Interpreter.run ports request
                  Expect.equal model.Exit Loop.ToolError "write failure → ToolError (exit 4)"
                  Expect.equal (Loop.exitCode model.Exit) 4 "exit code 4"
                  Expect.isFalse (File.Exists outPath) "no partial artifact left behind")
          }

          test "the real atomic writer leaves no partial file when the destination cannot be created" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  // A regular file stands where the output's parent directory would need to be ⇒ the atomic
                  // temp+rename writer fails cleanly (no partial release.json).
                  let blocker = Path.Combine(repo, "blocker")
                  File.WriteAllText(blocker, "x")
                  let outPath = Path.Combine(blocker, "nested", "release.json")
                  let ports = { Interpreter.realPorts repo with Out = ignore }

                  let request =
                      { Loop.Repo = repo
                        Loop.Format = Loop.Json
                        Loop.ReleaseOut = outPath }

                  let model = Interpreter.run ports request
                  Expect.equal model.Exit Loop.ToolError "unwritable destination → ToolError"
                  Expect.isFalse (File.Exists outPath) "no partial artifact")
          } ]
