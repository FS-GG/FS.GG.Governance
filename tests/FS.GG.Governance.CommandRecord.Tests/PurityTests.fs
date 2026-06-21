module FS.GG.Governance.CommandRecord.Tests.PurityTests

open System
open System.IO
open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Tests.Support

// US3 — `build`/`canonicalId` read no clock/cwd/filesystem and spawn no process: a fixed result is identical
// when recomputed after changing the current directory and after creating/deleting an unrelated temp file
// (SC-006, US3 #3).

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "record and identity are unchanged across cwd / filesystem changes" {
              let r0 = rebuild baseRecord.Reproducible baseDuration
              let id0 = CommandRecord.canonicalId r0

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.GetTempPath()
              let tempFile = Path.Combine(tempDir, sprintf "f032-purity-%s.tmp" (Guid.NewGuid().ToString("N")))

              try
                  // Change cwd and touch an unrelated file — neither must influence the pure functions.
                  Directory.SetCurrentDirectory tempDir
                  File.WriteAllText(tempFile, "unrelated")

                  let r1 = rebuild baseRecord.Reproducible baseDuration
                  let id1 = CommandRecord.canonicalId r1

                  File.Delete tempFile
                  let r2 = rebuild baseRecord.Reproducible baseDuration
                  let id2 = CommandRecord.canonicalId r2

                  Expect.equal r1 r0 "record unaffected by cwd / filesystem state"
                  Expect.equal r2 r0 "record unaffected after deleting the temp file"
                  Expect.equal id1 id0 "identity unaffected by cwd / filesystem state"
                  Expect.equal id2 id0 "identity unaffected after deleting the temp file"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if File.Exists tempFile then File.Delete tempFile
          } ]
