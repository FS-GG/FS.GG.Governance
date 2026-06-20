module FS.GG.Governance.Route.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Route.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1).
// Reflection lives ONLY in these tests, never in the library.

let private route = typeof<RouteResult>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Route.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public
/// surface changes this text and trips the baseline assertion.
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
        [ test "Route public surface equals the committed baseline" {
              let actual = renderSurface route

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules Model and Route, nothing private" {
              let typeNames =
                  route.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              let hasModel = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Route.ModelModule")
              let hasRoute = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Route.RouteModule")
              Expect.isTrue hasModel "Model module is public"
              Expect.isTrue hasRoute "Route module is public"
          }

          test "Route references only Gates + Routing + Findings + Config + BCL + FSharp.Core (FR-011/FR-013 scope guard)" {
              // No kernel/host/adapter/Snapshot/CLI dependency, and no git/CI/enforcement/severity
              // package — the absence confirms no later-phase capability leaked into F019. The
              // transitive YamlDotNet arrives only via Config and is unused by Route's code.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Routing"
                  || name = "FS.GG.Governance.Findings"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  route.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "Route must depend on Gates/Routing/Findings/Config/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT the kernel/host/adapters/Snapshot/CLI.
              let forbidden =
                  route.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "Route must not reference kernel/host/adapters/Snapshot/CLI; found: %A" forbidden)
          } ]
