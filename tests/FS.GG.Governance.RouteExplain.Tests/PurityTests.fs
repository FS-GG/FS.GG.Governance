module FS.GG.Governance.RouteExplain.Tests.PurityTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain
open FS.GG.Governance.RouteExplain.Tests.Support

// US3 — `explain` reads no clock/cwd/filesystem (SC-006, law L8): a fixed result is identical when
// recomputed after changing the current directory and after creating/deleting an unrelated temp file, and
// across repeated calls. Demonstrates the function depends only on its supplied values.

let private route =
    routeOf [ selGate (gate "build" "full" Exhaustive Ci) [ sp "src/a.fs" "src/**" ] ]

let private registry = catalog workedExampleGates

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "the explanation is identical across cwd / filesystem changes and repeated calls (L8, SC-006)" {
              let baseline = RouteExplain.explain route registry

              // Repeated calls are identical (no hidden state).
              Expect.equal (RouteExplain.explain route registry) baseline "repeated call identical"

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
              Directory.CreateDirectory tempDir |> ignore
              let tempFile = Path.Combine(tempDir, "unrelated.tmp")

              try
                  // Change the current directory.
                  Directory.SetCurrentDirectory tempDir
                  Expect.equal (RouteExplain.explain route registry) baseline "unchanged after cwd change"

                  // Create an unrelated file.
                  File.WriteAllText(tempFile, "noise")
                  Expect.equal (RouteExplain.explain route registry) baseline "unchanged after creating a file"

                  // Delete it again.
                  File.Delete tempFile
                  Expect.equal (RouteExplain.explain route registry) baseline "unchanged after deleting the file"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if Directory.Exists tempDir then
                      Directory.Delete(tempDir, true)
          } ]
