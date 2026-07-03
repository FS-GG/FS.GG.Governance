module FS.GG.Governance.ScaffoldManifestJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ScaffoldManifestJson

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). The bespoke "exports exactly one module" one-off guard stays inline.

let private manifestJson =
    // The library exports only the module (no public types), so load its assembly by name.
    System.Reflection.Assembly.Load "FS.GG.Governance.ScaffoldManifestJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ScaffoldManifestJson" "FS.GG.Governance.ScaffoldManifestJson" manifestJson

          test "ScaffoldManifestJson exports exactly one module, nothing private" {
              let typeNames =
                  manifestJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              Expect.equal typeNames.Length 1 "exactly one exported type (the ScaffoldManifestJson module)"
          }

          // Leaf scope guard: ScaffoldManifestJson -> Scaffold, plus the 073 JsonText leaf. No kernel/
          // host/Cli/adapter edge.
          SurfaceDrift.referencesOnly
              "ScaffoldManifestJson"
              (fun n -> n = "FS.GG.Governance.Scaffold" || n = "FS.GG.Governance.JsonText")
              manifestJson ]
