module FS.GG.Governance.FreshnessSensing.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.FreshnessSensing.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, FR-014). Reflection lives ONLY
// in these tests, never in the library. The new Tier-1 baseline for the shared sensing edge's public surface.

// Touch a member to force the library assembly to load, then locate it by name.
let private freshnessSensingAsm =
    FreshnessSensing.realStoreReader |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.FreshnessSensing"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.FreshnessSensing.surface.txt")

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
        [ test "FreshnessSensing public surface equals the committed baseline" {
              let actual = renderSurface freshnessSensingAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "FreshnessSensing references only the cores/BCL/FSharp.Core (no projection/host/CLI/adapter)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.FreshnessResolution"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.CacheEligibility"
                  || name = "FS.GG.Governance.Kernel"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  freshnessSensingAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "FreshnessSensing must depend on the cores/BCL/FSharp.Core only; found: %A" offending)

              let forbidden =
                  freshnessSensingAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.CacheEligibilityJson"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.Route"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "FreshnessSensing must not reference any projection/host/adapter/CLI; found: %A" forbidden)
          } ]
