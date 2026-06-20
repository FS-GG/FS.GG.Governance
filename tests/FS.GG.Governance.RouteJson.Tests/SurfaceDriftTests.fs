module FS.GG.Governance.RouteJson.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives
// ONLY in these tests, never in the library.

// RouteJson exports only the module (no public types), and wrapping its function in a delegate would
// capture a closure compiled into THIS test assembly. So touch a member to force the library assembly
// to load, then locate it by name among the loaded assemblies.
let private routeJson =
    RouteJson.schemaVersion |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.RouteJson"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.RouteJson.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public surface
/// changes this text and trips the baseline assertion.
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
        [ test "RouteJson public surface equals the committed baseline" {
              let actual = renderSurface routeJson

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the RouteJson module, nothing private" {
              let typeNames =
                  routeJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the RouteJson module. No token helpers, sub-object writers,
              // or buffer plumbing leak (they are hidden by RouteJson.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the RouteJson module)"
              Expect.isTrue (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.RouteJson.RouteJsonModule")) "RouteJson module is public"
          }

          test "RouteJson references only the Route transitive graph + BCL + FSharp.Core (FR-015 scope guard)" {
              // No kernel/host/adapter/Snapshot/CLI dependency, and no new third-party package — the
              // absence confirms serialization is the shared-framework System.Text.Json and no
              // later-phase capability leaked in. Gates/Routing/Findings/Config arrive transitively
              // via Route.
              // The one-way graph is RouteJson -> Route -> {Gates, Routing, Findings} -> Config: the
              // library project-references only Route, but its IL references the transitive Governance
              // types it manipulates (RouteResult embeds Gate/FreshnessKey/FindingReport/GovernedPath).
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Route"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Routing"
                  || name = "FS.GG.Governance.Findings"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  routeJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "RouteJson must depend on Route/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT the kernel/host/adapters/Snapshot/CLI — no later-phase capability.
              let forbidden =
                  routeJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "RouteJson must not reference kernel/host/adapters/Snapshot/CLI; found: %A" forbidden)
          } ]
