module FS.GG.Governance.JsonText.Tests.SurfaceBaselineTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.JsonText

// Reflective API surface-drift + dependency/scope-hygiene checks for the 073 JsonText leaf (Principle II),
// now via the shared SurfaceDrift helper (101/M-CI-3).

let private jsonTextAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.JsonText"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "JsonText" "FS.GG.Governance.JsonText" jsonTextAsm

          // Scope guard: the leaf has NO governance ProjectReference — it references only FSharp.Core/BCL,
          // so it cannot introduce a cycle and any projection may reference it without pulling in the
          // kernel/host capability the pure projections exclude.
          SurfaceDrift.referencesOnly "JsonText" (fun _ -> false) jsonTextAsm ]
