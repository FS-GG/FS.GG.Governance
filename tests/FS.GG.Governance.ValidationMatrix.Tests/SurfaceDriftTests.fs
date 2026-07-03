module FS.GG.Governance.ValidationMatrix.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ValidationMatrix
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ValidationMatrix.Tests.Support

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm =
    Matrix.decideMatrix releaseBudget None |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ValidationMatrix"
        | None -> false)

[<Tests>]
let tests =
    testList "SurfaceDrift" [ SurfaceDrift.surfaceTest "ValidationMatrix" "FS.GG.Governance.ValidationMatrix" asm ]
