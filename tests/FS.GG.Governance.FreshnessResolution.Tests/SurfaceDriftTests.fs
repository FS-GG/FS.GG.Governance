module FS.GG.Governance.FreshnessResolution.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, FR-014). Reflection lives ONLY in
// these tests, never in the library.

let private noSensed: SensedFacts =
    { RuleHash = None
      GeneratorVersion = None
      Base = None
      Head = None
      CoveredArtifacts = Map.empty
      CommandVersions = Map.empty }

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private freshnessResolutionAsm =
    FreshnessResolution.entries (FreshnessResolution.resolve [] noSensed) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.FreshnessResolution"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.FreshnessResolution.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public surface changes this
/// text and trips the baseline assertion.
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
        [ test "FreshnessResolution public surface equals the committed baseline" {
              let actual = renderSurface freshnessResolutionAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules (Model + FreshnessResolution), nothing else" {
              let typeNames =
                  freshnessResolutionAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.FreshnessResolution.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.FreshnessResolution.FreshnessResolutionModule"))
                  "FreshnessResolution operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal" || l.Contains "comparator"))
                  "no helper/internal/comparator module leaks into the public surface"
          }

          test "FreshnessResolution references only CacheEligibility (+transitive cores)/BCL/FSharp.Core (scope guard)" {
              // F043 single-sibling shape: FreshnessResolution -> CacheEligibility (F041), whose transitive pure
              // cores (EvidenceReuse, Gates, FreshnessKey, Config, Kernel) arrive. No
              // RouteJson/AuditJson/GatesJson/CacheEligibilityJson/Enforcement/Ship/Snapshot/Routing/Findings, no
              // host/adapter/CLI edge, and no new third-party package.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.CacheEligibility"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Kernel"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  freshnessResolutionAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "FreshnessResolution must depend on CacheEligibility/(transitive cores)/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT any projection/edge/host/CLI/adapter assembly.
              let forbidden =
                  freshnessResolutionAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.CacheEligibilityJson"
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
                  (sprintf "FreshnessResolution must not reference any projection/edge/host/adapter/CLI; found: %A" forbidden)
          } ]
