module FS.GG.Governance.ReleaseCommand.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReleaseCommand

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// Reflection lives in the helper and here, never in the library. The public surface is exactly the
// `Declaration` + `Loop` + `Interpreter` modules (the three `.fsi` contracts) plus the thin `Program`
// Exe entry.

let private library = typeof<Loop.RunRequest>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ReleaseCommand" "FS.GG.Governance.ReleaseCommand" library

          test "only Declaration/Loop/Interpreter modules (+ Program entry) are public" {
              let typeNames = library.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "ReleaseCommand.DeclarationModule"
                          || n.Contains "ReleaseCommand.LoopModule"
                          || n.Contains "ReleaseCommand.InterpreterModule"
                          || n.Contains "ReleaseCommand.Declaration+"
                          || n.Contains "ReleaseCommand.Loop+"
                          || n.Contains "ReleaseCommand.Interpreter+"
                          || n.Contains "ReleaseCommand.Program"))

              Expect.isEmpty
                  unexpected
                  (sprintf "only Declaration/Loop/Interpreter (+ Program entry) are public; found extra: %A" unexpected)
          } ]
