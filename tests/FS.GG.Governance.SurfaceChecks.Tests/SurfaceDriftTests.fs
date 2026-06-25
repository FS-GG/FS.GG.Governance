module FS.GG.Governance.SurfaceChecks.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.SurfaceChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

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

let private check (asm: Assembly) (name: string) =
    let baselinePath = Path.Combine(repoRoot, "surface", sprintf "%s.surface.txt" name)
    let actual = renderSurface asm

    if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
        File.WriteAllText(baselinePath, actual + "\n")

    let baseline = File.ReadAllText baselinePath
    Expect.equal (normalize actual) (normalize baseline) (sprintf "%s surface drifted — BLESS_SURFACE=1 to regenerate" name)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ test "SurfaceChecks public surface equals the committed baseline" {
              check typeof<SC.CheckDomain>.Assembly "FS.GG.Governance.SurfaceChecks"
          }

          test "SurfaceChecks.Dispatch public surface equals the committed baseline" {
              check typeof<FS.GG.Governance.SurfaceChecks.Dispatch.Composition.DomainFactBundle>.Assembly "FS.GG.Governance.SurfaceChecks.Dispatch"
          } ]
