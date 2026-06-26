module FS.GG.Governance.ScaffoldManifestJson.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ScaffoldManifestJson
open FS.GG.Governance.ScaffoldManifestJson.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives ONLY
// in these tests, never in the library.

let private manifestJson =
    // The library exports only the module (no public types), so load its assembly by name.
    System.Reflection.Assembly.Load "FS.GG.Governance.ScaffoldManifestJson"

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ScaffoldManifestJson.surface.txt")

let private renderSurface (asm: Assembly) =
    let memberFlags =
        BindingFlags.Public
        ||| BindingFlags.Instance
        ||| BindingFlags.Static
        ||| BindingFlags.DeclaredOnly

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
        [ test "ScaffoldManifestJson public surface equals the committed baseline" {
              let actual = renderSurface manifestJson

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "ScaffoldManifestJson exports exactly one module, nothing private" {
              let typeNames =
                  manifestJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              Expect.equal typeNames.Length 1 "exactly one exported type (the ScaffoldManifestJson module)"
          }

          test "ScaffoldManifestJson references only Scaffold + BCL + FSharp.Core (leaf scope guard)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Scaffold"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  manifestJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "ScaffoldManifestJson must depend on Scaffold/BCL/FSharp.Core only; found: %A" offending)

              let forbidden =
                  manifestJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "ScaffoldManifestJson must not reference kernel/host/Cli/adapters; found: %A" forbidden)
          } ]
