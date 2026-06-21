module FS.GG.Governance.SensedMetadata.Tests.PurityTests

open System
open System.IO
open Expecto
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Tests.Support

// US3 — `markDuration` / `markTimestamp` / `render` / `renderSection` read no clock/cwd/filesystem and spawn
// no process: a fixed result is identical when recomputed after changing the current directory and after
// creating/deleting an unrelated temp file (SC-005, US3 #2).

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "marking and rendering are unchanged across cwd / filesystem changes" {
              let m0 = markDur "elapsed" 1_830_000_000L
              let r0 = SensedMetadata.render m0
              let s0 = SensedMetadata.renderSection [ workedTimestamp; workedDuration ]

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.GetTempPath()
              let tempFile = Path.Combine(tempDir, sprintf "f034-purity-%s.tmp" (Guid.NewGuid().ToString("N")))

              try
                  // Change cwd and touch an unrelated file — neither must influence the pure functions.
                  Directory.SetCurrentDirectory tempDir
                  File.WriteAllText(tempFile, "unrelated")

                  let m1 = markDur "elapsed" 1_830_000_000L
                  let r1 = SensedMetadata.render m1
                  let s1 = SensedMetadata.renderSection [ workedTimestamp; workedDuration ]

                  File.Delete tempFile
                  let m2 = markDur "elapsed" 1_830_000_000L
                  let r2 = SensedMetadata.render m2
                  let s2 = SensedMetadata.renderSection [ workedTimestamp; workedDuration ]

                  Expect.equal m1 m0 "marking unaffected by cwd / filesystem state"
                  Expect.equal m2 m0 "marking unaffected after deleting the temp file"
                  Expect.equal r1 r0 "render unaffected by cwd / filesystem state"
                  Expect.equal r2 r0 "render unaffected after deleting the temp file"
                  Expect.equal s1 s0 "renderSection unaffected by cwd / filesystem state"
                  Expect.equal s2 s0 "renderSection unaffected after deleting the temp file"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if File.Exists tempFile then File.Delete tempFile
          } ]
