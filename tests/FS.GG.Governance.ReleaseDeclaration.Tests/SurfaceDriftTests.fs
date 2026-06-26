module FS.GG.Governance.ReleaseDeclaration.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.ReleaseDeclaration.Tests.Support

// Reflective API surface-drift check (Principle II). Reflection lives ONLY in this test, never in the
// library. The public surface is exactly the `Declaration` module (its one `.fsi` contract). The committed
// baseline is produced by running this once with BLESS_SURFACE=1.

let private library = typeof<Declaration.ReleaseDeclaration>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ReleaseDeclaration.surface.txt")

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
        [ test "ReleaseDeclaration public surface equals the committed baseline" {
              let actual = renderSurface library

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          } ]
