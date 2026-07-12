module FS.GG.Governance.RoutePipeline.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.Tests.Common

// 100 (M-ARCH-2): re-homed from RouteCommand.Tests. Reflective API surface-drift + dependency/scope-hygiene
// checks (Principle II) for the FS.GG.Governance.RoutePipeline library, which now owns the route pipeline.
// Reflection lives ONLY in these tests, never in the library. The public surface is exactly the `Loop` +
// `Interpreter` modules (the two `.fsi` contracts, still in the `FS.GG.Governance.RouteCommand` namespace);
// the dependency boundary is the eight cores + BCL + FSharp.Core, and NO edge into the kernel-era Host/Cli.

let private repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

let private routePipeline = SurfaceDrift.assemblyNamed "FS.GG.Governance.RoutePipeline"

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.RoutePipeline.surface.txt")

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
        [ test "RoutePipeline public surface equals the committed baseline" {
              let actual = renderSurface routePipeline

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public API surface is exactly the Loop + Interpreter modules" {
              let typeNames = routePipeline.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.RouteCommand.LoopModule"))
                  "Loop module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.RouteCommand.InterpreterModule"))
                  "Interpreter module is public"

              // ONLY Loop/Interpreter (and their nested DUs/records) are exported — no argv-matcher /
              // composition / writer helper leaks (those are hidden by the two `.fsi` contracts, Principle II).
              // The thin `Program` entry now lives in the RouteCommand executable, not here.
              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "RouteCommand.LoopModule"
                          || n.Contains "RouteCommand.InterpreterModule"
                          || n.Contains "RouteCommand.Loop+" // nested DUs/records of Loop
                          || n.Contains "RouteCommand.Interpreter+")) // nested types of Interpreter

              Expect.isEmpty unexpected (sprintf "only Loop/Interpreter are public; found extra: %A" unexpected)
          }

          test "RoutePipeline references only the eight cores + BCL + FSharp.Core (no kernel/host/cli, D1)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Snapshot"
                  || name = "FS.GG.Governance.Routing"
                  || name = "FS.GG.Governance.Findings"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Route"
                  // F081 (research D6): the SDD→Governance handoff CONSUMER — the ONE permitted Adapters.* edge.
                  || name = "FS.GG.Governance.Adapters.SddHandoff"
                  || name = "FS.GG.Governance.RouteJson"
                  || name = "FS.GG.Governance.GatesJson"
                  || name = "FS.GG.Governance.ProductSurfaces"
                  || name = "FS.GG.Governance.CacheEligibility"
                  || name = "FS.GG.Governance.FreshnessSensing"
                  || name = "FS.GG.Governance.FreshnessResolution"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.EvidenceReuseStore"
                  || name = "FS.GG.Governance.GateRun"
                  || name = "FS.GG.Governance.GateExecution"
                  || name = "FS.GG.Governance.EvidenceCapture"
                  || name = "FS.GG.Governance.CommandHost"
                  || name = "FS.GG.Governance.CostBudget"
                  || name = "FS.GG.Governance.ExecutionRecord"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "FS.GG.Governance.HumanText"
                  || name = "FS.GG.Governance.HumanRender"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  routePipeline.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "RoutePipeline must depend on the eight cores/BCL/FSharp.Core only; found: %A" offending)

              let forbidden =
                  routePipeline.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || (n.StartsWith "FS.GG.Governance.Adapters" && n <> "FS.GG.Governance.Adapters.SddHandoff")
                      // F27 wiring (063), FR-011/SC-007: Spectre stays confined to HumanRender.
                      || n = "Spectre.Console")

              Expect.isEmpty forbidden (sprintf "RoutePipeline must not reference kernel/host/cli/adapters; found: %A" forbidden)
          } ]
