module FS.GG.Governance.RefreshCommand.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.RefreshCommand

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// Reflection lives in the helper and here. The public surface is exactly the `Declaration` + `Loop` +
// `Interpreter` modules (the three `.fsi` contracts) plus the thin `Program` Exe entry.

let private library = typeof<Loop.RunRequest>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "RefreshCommand" "FS.GG.Governance.RefreshCommand" library

          test "only Declaration/Loop/Interpreter modules (+ Program entry) are public" {
              let typeNames = library.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "RefreshCommand.DeclarationModule"
                          || n.Contains "RefreshCommand.LoopModule"
                          || n.Contains "RefreshCommand.InterpreterModule"
                          || n.Contains "RefreshCommand.Declaration+"
                          || n.Contains "RefreshCommand.Loop+"
                          || n.Contains "RefreshCommand.Interpreter+"
                          || n.Contains "RefreshCommand.Program"))

              Expect.isEmpty unexpected (sprintf "only Declaration/Loop/Interpreter (+ Program) are public; found extra: %A" unexpected)
          } ]
