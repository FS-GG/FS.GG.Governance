module FS.GG.Governance.AgentReviewKey.Tests.PurityTests

open System
open System.IO
open Expecto
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Tests.Support

// US3: purity — the key for a fixed input is byte-identical when recomputed after changing the current
// directory and after creating/deleting an unrelated temp file (no clock/cwd/filesystem influence, no model
// invocation) (SC-006, FR-007).

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "the key is identical across changed cwd and unrelated filesystem state" {
              let before = AgentReviewKey.value (AgentReviewKey.compute baseInputs)

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tempDir |> ignore

              try
                  Directory.SetCurrentDirectory tempDir
                  let tempFile = Path.Combine(tempDir, "unrelated.tmp")
                  File.WriteAllText(tempFile, "noise")
                  let during = AgentReviewKey.value (AgentReviewKey.compute baseInputs)
                  File.Delete tempFile
                  let after = AgentReviewKey.value (AgentReviewKey.compute baseInputs)

                  Expect.equal during before "changed cwd + new temp file does not change the key"
                  Expect.equal after before "deleting the temp file does not change the key"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  try
                      Directory.Delete(tempDir, true)
                  with _ ->
                      ()
          }

          test "repeated calls at different wall-clock moments give the identical key" {
              let a = AgentReviewKey.value (AgentReviewKey.compute baseInputs)
              System.Threading.Thread.Sleep 5
              let b = AgentReviewKey.value (AgentReviewKey.compute baseInputs)
              Expect.equal a b "no clock influence on the key"
          } ]
