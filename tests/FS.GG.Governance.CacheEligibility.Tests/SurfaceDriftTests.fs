module FS.GG.Governance.CacheEligibility.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, SC-008). Reflection lives ONLY
// in these tests, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private cacheEligibilityAsm =
    CacheEligibility.entries (CacheEligibility.evaluate [] EvidenceReuse.empty) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CacheEligibility"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.CacheEligibility.surface.txt")

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
        [ test "CacheEligibility public surface equals the committed baseline" {
              let actual = renderSurface cacheEligibilityAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules (Model + CacheEligibility), nothing else" {
              let typeNames =
                  cacheEligibilityAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.CacheEligibility.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.CacheEligibility.CacheEligibilityModule"))
                  "CacheEligibility operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal" || l.Contains "comparator"))
                  "no helper/internal/comparator module leaks into the public surface"
          }

          test "CacheEligibility references only EvidenceReuse/Gates (+transitive cores)/BCL/FSharp.Core (SC-008 scope guard)" {
              // F041 two-sibling shape: CacheEligibility -> EvidenceReuse (F030) and -> Gates (F018), whose
              // transitive pure cores (FreshnessKey, Config) arrive. No
              // RouteJson/AuditJson/Enforcement/Ship/Snapshot/Routing/Findings, no host/adapter/CLI edge, and
              // no new third-party package.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  cacheEligibilityAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "CacheEligibility must depend on EvidenceReuse/Gates/(transitive cores)/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT RouteJson/AuditJson/Enforcement/Ship/Snapshot/Routing/Findings/Adapters/
              // Host/CLI/etc.
              let forbidden =
                  cacheEligibilityAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Route"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "CacheEligibility must not reference RouteJson/AuditJson/Enforcement/Ship/Snapshot/Route/Routing/Findings/host/adapters/CLI; found: %A" forbidden)
          } ]
