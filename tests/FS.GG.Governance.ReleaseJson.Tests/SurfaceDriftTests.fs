module FS.GG.Governance.ReleaseJson.Tests.SurfaceDriftTests

open System
open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReleaseJson

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// The public surface is exactly the `ReleaseJson` module (the one `.fsi` contract).

let private library =
    // Touch a member to force the library assembly to load, then locate it by name.
    ReleaseJson.schemaVersion |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ReleaseJson"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ReleaseJson" "FS.GG.Governance.ReleaseJson" library ]
