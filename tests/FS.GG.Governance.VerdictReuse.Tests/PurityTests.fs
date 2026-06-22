module FS.GG.Governance.VerdictReuse.Tests.PurityTests

open System
open System.IO
open Expecto
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model
open FS.GG.Governance.VerdictReuse.Tests.Support

// Cross-cutting (SC-006, FR-009): `lookup` and `record` are identical across changed working directory,
// elapsed wall-clock time, and unrelated filesystem state — no clock/cwd/filesystem/git/environment/network
// read, no model invoked.

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "lookup and record are identical across changed cwd and unrelated filesystem state" {
              let store = handStore [ variantPromptHash baseInputs, refV2; baseInputs, refV1 ]
              let request = variantQuestion baseInputs
              let beforeLookup = VerdictReuse.lookup request store
              let beforeRecord = VerdictReuse.entries (VerdictReuse.record request refV3 store)

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tempDir |> ignore

              try
                  Directory.SetCurrentDirectory tempDir
                  let tempFile = Path.Combine(tempDir, "unrelated.tmp")
                  File.WriteAllText(tempFile, "noise")
                  let duringLookup = VerdictReuse.lookup request store
                  let duringRecord = VerdictReuse.entries (VerdictReuse.record request refV3 store)
                  File.Delete tempFile
                  let afterLookup = VerdictReuse.lookup request store

                  Expect.equal duringLookup beforeLookup "changed cwd + new temp file does not change the lookup"
                  Expect.equal afterLookup beforeLookup "deleting the temp file does not change the lookup"
                  Expect.equal duringRecord beforeRecord "changed cwd + new temp file does not change the record"
              finally
                  Directory.SetCurrentDirectory originalCwd

                  try
                      Directory.Delete(tempDir, true)
                  with _ ->
                      ()
          }

          test "repeated calls at different wall-clock moments give the identical decision" {
              let store = handStore [ baseInputs, refV1 ]
              let a = VerdictReuse.lookup baseInputs store
              System.Threading.Thread.Sleep 5
              let b = VerdictReuse.lookup baseInputs store
              Expect.equal a b "no clock influence on the decision"
          } ]
