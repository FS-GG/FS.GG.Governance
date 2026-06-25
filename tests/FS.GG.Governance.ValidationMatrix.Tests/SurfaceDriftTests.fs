module FS.GG.Governance.ValidationMatrix.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ValidationMatrix
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ValidationMatrix.Tests.Support

let private asm =
    Matrix.decideMatrix releaseBudget ScheduledOrRelease None |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ValidationMatrix"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ValidationMatrix.surface.txt")

let private renderSurface (a: Assembly) =
    let memberFlags =
        BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

    a.GetExportedTypes()
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
        [ test "ValidationMatrix public surface equals the committed baseline" {
              let actual = renderSurface asm

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath
              Expect.equal (normalize actual) (normalize baseline) "public surface drifted — bless with BLESS_SURFACE=1"
          } ]
