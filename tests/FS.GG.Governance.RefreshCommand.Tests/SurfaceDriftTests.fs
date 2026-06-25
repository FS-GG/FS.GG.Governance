module FS.GG.Governance.RefreshCommand.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// Reflective API surface-drift check (Principle II). Reflection lives ONLY in this test. The public surface
// is exactly the `Declaration` + `Loop` + `Interpreter` modules (the three `.fsi` contracts) plus the thin
// `Program` Exe entry. The committed baseline is produced by running this once with BLESS_SURFACE=1.

let private library = typeof<Loop.RunRequest>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.RefreshCommand.surface.txt")

let private renderSurface (asm: Assembly) =
    let memberFlags =
        BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

    asm.GetExportedTypes()
    |> Array.sortBy (fun t -> t.FullName)
    |> Array.map (fun t ->
        let members =
            t.GetMembers(memberFlags)
            |> Array.map (fun m -> sprintf "  [%A] %s" m.MemberType (m.ToString()))
            |> Array.sort

        String.concat "\n" (Array.append [| sprintf "TYPE %s" t.FullName |] members))
    |> String.concat "\n"

let private normalize (s: string) = s.Replace("\r\n", "\n").TrimEnd()

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ test "RefreshCommand public surface equals the committed baseline" {
              let actual = renderSurface library

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

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
