module FS.GG.Governance.FreshnessKey.Tests.PurityTests

open System
open System.IO
open Expecto
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Tests.Support

// Purity (SC-006, FR-008): the key for a fixed input is byte-identical regardless of working directory,
// wall-clock time, or unrelated filesystem state. The core reads no clock/cwd/filesystem/git/network.

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "key is identical across a changed current directory and an unrelated temp file (SC-006)" {
              let expected = FreshnessKey.value (FreshnessKey.compute baseInputs)

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tempDir |> ignore
              let tempFile = Path.Combine(tempDir, "unrelated.tmp")

              try
                  // Change cwd, create an unrelated file, recompute.
                  Directory.SetCurrentDirectory tempDir
                  File.WriteAllText(tempFile, "noise")
                  let afterCwdChange = FreshnessKey.value (FreshnessKey.compute baseInputs)

                  // Delete the file, recompute again.
                  File.Delete tempFile
                  let afterDelete = FreshnessKey.value (FreshnessKey.compute baseInputs)

                  Expect.equal afterCwdChange expected "changing cwd / creating a file must not change the key"
                  Expect.equal afterDelete expected "deleting a file must not change the key"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if Directory.Exists tempDir then Directory.Delete(tempDir, true)
          }

          test "repeated computation over time is byte-stable (SC-006)" {
              let runs = [ for _ in 1..50 -> FreshnessKey.value (FreshnessKey.compute baseInputs) ]
              Expect.equal (runs |> List.distinct |> List.length) 1 "every recomputation is byte-identical"
          } ]
