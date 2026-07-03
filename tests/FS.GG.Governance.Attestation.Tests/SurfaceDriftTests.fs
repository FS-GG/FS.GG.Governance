module FS.GG.Governance.Attestation.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Attestation
open FS.GG.Governance.Attestation.Tests.Support

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm =
    Attestation.summarize (snapshotOf [ packRun ]) twoPacked |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.Attestation"
        | None -> false)

[<Tests>]
let tests =
    testList "SurfaceDrift" [ SurfaceDrift.surfaceTest "Attestation" "FS.GG.Governance.Attestation" asm ]
