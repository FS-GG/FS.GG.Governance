module FS.GG.Governance.FSharpSurfaceCommand.Program

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SurfaceChecks.Model
open FS.GG.Governance.DesignChecks.FSharpSurface

let private usage () =
    eprintfn "usage: fsgg-fsharp-surface --root <repo> --project <relative.fsproj> [--test-project] [--requires-baseline] [--baseline-current]"
    2

let private parse arguments =
    let rec loop root project testProject baseline current remaining =
        match remaining with
        | [] ->
            match root, project with
            | Some r, Some p -> Ok(r, p, testProject, baseline, current)
            | _ -> Error()
        | "--root" :: value :: tail -> loop (Some value) project testProject baseline current tail
        | "--project" :: value :: tail -> loop root (Some value) testProject baseline current tail
        | "--test-project" :: tail -> loop root project true baseline current tail
        | "--requires-baseline" :: tail -> loop root project testProject true current tail
        | "--baseline-current" :: tail -> loop root project testProject baseline true tail
        | _ -> Error()
    loop None None false false false arguments

[<EntryPoint>]
let main argv =
    match parse (List.ofArray argv) with
    | Error () -> usage ()
    | Ok(root, project, isTest, requiresBaseline, baselineCurrent) ->
        let request =
            { Domain = DesignDomain
              Surface = SurfaceId "fsharp-public-surface"
              Class = DesignSurface
              Path = normalizePath project
              EvidenceTag = None }
        let result = receipt root project isTest requiresBaseline baselineCurrent request
        printfn "%s" (receiptJson result)
        if result.Malformed.IsSome then 3 else 0
