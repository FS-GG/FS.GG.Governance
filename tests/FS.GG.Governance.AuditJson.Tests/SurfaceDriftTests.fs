module FS.GG.Governance.AuditJson.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives
// ONLY in these tests, never in the library.

// AuditJson exports only the module (no public types). Touch a member to force the library assembly to
// load, then locate it by name among the loaded assemblies.
let private auditJson =
    AuditJson.schemaVersion |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.AuditJson"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.AuditJson.surface.txt")

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
        [ test "AuditJson public surface equals the committed baseline" {
              let actual = renderSurface auditJson

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the AuditJson module, nothing private" {
              let typeNames =
                  auditJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the AuditJson module. No token helpers, enforcement/item
              // writers, or buffer plumbing leak (they are hidden by AuditJson.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the AuditJson module)"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.AuditJson.AuditJsonModule"))
                  "AuditJson module is public"
          }

          test "AuditJson references only the Ship transitive graph + BCL + FSharp.Core (FR-014 scope guard)" {
              // One-way dependency: AuditJson -> Ship -> Enforcement/Route/Config/Gates/Findings/Kernel.
              // No host/adapter/CLI edge and no new third-party package — the absence confirms
              // serialization is the shared-framework System.Text.Json and no later-phase capability
              // leaked in.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Ship"
                  || name = "FS.GG.Governance.Enforcement"
                  || name = "FS.GG.Governance.Route"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Findings"
                  || name = "FS.GG.Governance.Kernel"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  auditJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "AuditJson must depend on the Ship graph/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT the host/adapters/Snapshot/Routing/RouteJson/GatesJson/RouteCommand/CLI.
              let forbidden =
                  auditJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.RouteCommand"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "AuditJson must not reference host/adapters/Snapshot/Routing/RouteJson/GatesJson/RouteCommand/CLI; found: %A" forbidden)
          } ]
