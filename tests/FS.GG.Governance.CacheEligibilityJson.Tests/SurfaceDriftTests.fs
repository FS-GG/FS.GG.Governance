module FS.GG.Governance.CacheEligibilityJson.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.CacheEligibilityJson.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives ONLY in
// these tests, never in the library.

// CacheEligibilityJson exports only the module (no public types). Touch a member to force the library
// assembly to load, then locate it by name among the loaded assemblies.
let private cacheEligibilityJson =
    CacheEligibilityJson.schemaVersion |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CacheEligibilityJson"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.CacheEligibilityJson.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public surface changes
/// this text and trips the baseline assertion.
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
        [ test "CacheEligibilityJson public surface equals the committed baseline" {
              let actual = renderSurface cacheEligibilityJson

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the CacheEligibilityJson module, nothing private" {
              let typeNames =
                  cacheEligibilityJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the CacheEligibilityJson module. No writer / token helper leak
              // (they are hidden by CacheEligibilityJson.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the CacheEligibilityJson module)"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.CacheEligibilityJson.CacheEligibilityJsonModule"))
                  "CacheEligibilityJson module is public"
          }

          test "CacheEligibilityJson references only the CacheEligibility transitive graph + BCL + FSharp.Core (FR-014 scope guard)" {
              // One-way dependency: CacheEligibilityJson -> CacheEligibility -> EvidenceReuse/Gates/FreshnessKey/
              // Config. No host/adapter/CLI/sibling-projection edge and no new third-party package — the absence
              // confirms serialization is the shared-framework System.Text.Json and no later-phase capability
              // leaked in.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.CacheEligibility"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  // 073: the dependency-free JsonText leaf (the shared deterministic-emit helper writeToString).
                  || name = "FS.GG.Governance.JsonText"
                  // 073: the pure JsonWriters leaf (the shared sub-object/map writers).
                  || name = "FS.GG.Governance.JsonWriters"
                  || name.StartsWith "System."

              let offending =
                  cacheEligibilityJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "CacheEligibilityJson must depend on the CacheEligibility graph/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT the host/adapters/Snapshot/Routing/RouteJson/GatesJson/AuditJson/Ship/CLI.
              let forbidden =
                  cacheEligibilityJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.RouteCommand"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "CacheEligibilityJson must not reference host/adapters/Snapshot/Routing/RouteJson/GatesJson/AuditJson/Enforcement/Ship/Findings/CLI; found: %A" forbidden)
          } ]
