module FS.GG.Governance.RouteCommand.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives ONLY
// in these tests, never in the library. The public surface is exactly the `Loop` + `Interpreter`
// modules (the two `.fsi` contracts); the dependency boundary is the eight cores + BCL + FSharp.Core,
// and NO edge into the kernel-era Host/Cli (research D1).

let private routeCommand =
    // Touch a member to force the library assembly to load, then locate it by name.
    Loop.exitCode Loop.Success |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.RouteCommand"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.RouteCommand.surface.txt")

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
        [ test "RouteCommand public surface equals the committed baseline" {
              let actual = renderSurface routeCommand

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public API surface is exactly the Loop + Interpreter modules (plus the Exe entry)" {
              let typeNames = routeCommand.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.RouteCommand.LoopModule"))
                  "Loop module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.RouteCommand.InterpreterModule"))
                  "Interpreter module is public"

              // The ONLY non-Loop/Interpreter exported module is the thin `Program` Exe entry (an
              // [<EntryPoint>] module is always public). No argv-matcher / composition / writer helper
              // leaks — those are hidden by the two `.fsi` contracts (Principle II).
              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "RouteCommand.LoopModule"
                          || n.Contains "RouteCommand.InterpreterModule"
                          || n.Contains "RouteCommand.Loop+" // nested DUs/records of Loop
                          || n.Contains "RouteCommand.Interpreter+" // nested types of Interpreter
                          || n.Contains "RouteCommand.Program"))

              Expect.isEmpty unexpected (sprintf "only Loop/Interpreter (+ Program entry) are public; found extra: %A" unexpected)
          }

          test "RouteCommand references only the eight cores + BCL + FSharp.Core (no kernel/host/cli, D1)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Snapshot"
                  || name = "FS.GG.Governance.Routing"
                  || name = "FS.GG.Governance.Findings"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Route"
                  || name = "FS.GG.Governance.RouteJson"
                  || name = "FS.GG.Governance.GatesJson"
                  // F23: the edge-side product-surface classification (classify) + its additive route.json embed.
                  || name = "FS.GG.Governance.ProductSurfaces"
                  // F045: RouteJson's `ofRouteResult` takes a `CacheEligibilityReport option` ⇒ F041 arrives.
                  || name = "FS.GG.Governance.CacheEligibility"
                  // F046: the cache-eligibility pipeline — the shared sensing edge + the resolution/store cores.
                  || name = "FS.GG.Governance.FreshnessSensing"
                  || name = "FS.GG.Governance.FreshnessResolution"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.FreshnessKey"
                  // F048: the pure write half of the evidence-reuse store (prune/retain/serialise).
                  || name = "FS.GG.Governance.EvidenceReuseStore"
                  // F052: run the selected gates (GateExecution port), capture their evidence (EvidenceCapture),
                  // and the shared pure helpers + GateOutcome vocabulary (GateRun) + transitive F050/F032.
                  || name = "FS.GG.Governance.GateRun"
                  || name = "FS.GG.Governance.GateExecution"
                  || name = "FS.GG.Governance.EvidenceCapture"
                  || name = "FS.GG.Governance.ExecutionRecord"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  routeCommand.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "RouteCommand must depend on the eight cores/BCL/FSharp.Core only; found: %A" offending)

              let forbidden =
                  routeCommand.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "RouteCommand must not reference kernel/host/cli/adapters; found: %A" forbidden)
          } ]
