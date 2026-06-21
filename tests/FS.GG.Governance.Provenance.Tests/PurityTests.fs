module FS.GG.Governance.Provenance.Tests.PurityTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Tests.Support

// US3 — `build`/`canonicalId` read no clock/cwd/filesystem and spawn no process: a fixed result is identical
// when recomputed after changing the current directory and after creating/deleting an unrelated temp file
// (SC-006, US3 #3).

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "provenance and identity are unchanged across cwd / filesystem changes" {
              let p0 = rebuild baseProvenance
              let id0 = Provenance.canonicalId p0

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.GetTempPath()
              let tempFile = Path.Combine(tempDir, sprintf "f033-purity-%s.tmp" (Guid.NewGuid().ToString("N")))

              try
                  // Change cwd and touch an unrelated file — neither must influence the pure functions.
                  Directory.SetCurrentDirectory tempDir
                  File.WriteAllText(tempFile, "unrelated")

                  let p1 = rebuild baseProvenance
                  let id1 = Provenance.canonicalId p1

                  File.Delete tempFile
                  let p2 = rebuild baseProvenance
                  let id2 = Provenance.canonicalId p2

                  Expect.equal p1 p0 "provenance unaffected by cwd / filesystem state"
                  Expect.equal p2 p0 "provenance unaffected after deleting the temp file"
                  Expect.equal id1 id0 "identity unaffected by cwd / filesystem state"
                  Expect.equal id2 id0 "identity unaffected after deleting the temp file"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if File.Exists tempFile then File.Delete tempFile
          } ]
