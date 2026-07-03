module FS.GG.Governance.VerifyJson.Tests.SurfaceDriftTests

open System
open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.VerifyJson

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// The public surface is exactly the `VerifyJson` module (the one `.fsi` contract).

let private verifyJson =
    VerifyJson.schemaVersion |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.VerifyJson"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "VerifyJson" "FS.GG.Governance.VerifyJson" verifyJson ]
