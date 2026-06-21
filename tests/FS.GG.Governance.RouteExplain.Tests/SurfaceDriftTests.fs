module FS.GG.Governance.RouteExplain.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.RouteExplain
open FS.GG.Governance.RouteExplain.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1). Reflection lives
// ONLY in these tests, never in the library.

// Touch a member of a public module to force the library assembly to load, then locate it by name.
let private routeExplainAsm =
    RouteExplain.highCostThreshold |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.RouteExplain"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.RouteExplain.surface.txt")

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
        [ test "RouteExplain public surface equals the committed baseline" {
              let actual = renderSurface routeExplainAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules (Model + RouteExplain), nothing else" {
              let typeNames =
                  routeExplainAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.RouteExplain.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.RouteExplain.RouteExplainModule"))
                  "RouteExplain operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       n.ToLowerInvariant().Contains "helper" || n.ToLowerInvariant().Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          test "RouteExplain references only Route/Gates/Routing/Findings/Config + BCL + FSharp.Core (plan D1)" {
              // One-way dependency: RouteExplain -> {Route, Gates} -> ... -> Config. Config/Routing/Findings
              // arrive transitively. No git-sensing Snapshot, no freshness/cache sibling cores, no JSON
              // renderers, no host/adapter/CLI edge, and no new third-party package.
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
                  routeExplainAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf
                      "RouteExplain must depend on Route/Gates/Routing/Findings/Config/BCL/FSharp.Core only; found: %A"
                      offending)

              // Specifically NOT the freshness/cache siblings, JSON renderers, Snapshot, or any edge.
              let forbidden =
                  routeExplainAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.FreshnessKey"
                      || n = "FS.GG.Governance.EvidenceReuse"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.RouteCommand"
                      || n = "FS.GG.Governance.ShipCommand"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf
                      "RouteExplain must not reference Snapshot/FreshnessKey/EvidenceReuse/JSON/host/adapters/CLI; found: %A"
                      forbidden)
          } ]
