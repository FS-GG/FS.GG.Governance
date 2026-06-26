module FS.GG.Governance.ShipCommand.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives ONLY
// in these tests, never in the library. The public surface is exactly the `Loop` + `Interpreter`
// modules (the two `.fsi` contracts); the dependency boundary is the NINE cores + BCL + FSharp.Core,
// and NO edge into the kernel-era Host/Cli (research D1). There is NO external surface generator — this
// test IS the renderer; the committed baseline is produced by running it once (BLESS_SURFACE=1).

let private shipCommand =
    // Touch a member to force the library assembly to load, then locate it by name.
    Loop.exitCode Loop.Success |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ShipCommand"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ShipCommand.surface.txt")

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
        [ test "ShipCommand public surface equals the committed baseline" {
              let actual = renderSurface shipCommand

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public API surface is exactly the Loop + Interpreter modules (plus the Exe entry)" {
              let typeNames = shipCommand.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.ShipCommand.LoopModule"))
                  "Loop module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.ShipCommand.InterpreterModule"))
                  "Interpreter module is public"

              // The ONLY non-Loop/Interpreter exported module is the thin `Program` Exe entry (an
              // [<EntryPoint>] module is always public). No argv-matcher / composition / writer helper
              // leaks — those are hidden by the two `.fsi` contracts (Principle II).
              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "ShipCommand.LoopModule"
                          || n.Contains "ShipCommand.InterpreterModule"
                          || n.Contains "ShipCommand.Loop+" // nested DUs/records of Loop
                          || n.Contains "ShipCommand.Interpreter+" // nested types of Interpreter
                          || n.Contains "ShipCommand.Program"))

              Expect.isEmpty unexpected (sprintf "only Loop/Interpreter (+ Program entry) are public; found extra: %A" unexpected)
          }

          test "ShipCommand references only the nine cores + BCL + FSharp.Core (no kernel/host/cli, D1)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Snapshot"
                  || name = "FS.GG.Governance.Routing"
                  || name = "FS.GG.Governance.Findings"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Route"
                  || name = "FS.GG.Governance.Enforcement"
                  || name = "FS.GG.Governance.Ship"
                  || name = "FS.GG.Governance.AuditJson"
                  // F27 wiring (063): the shared plain human projection over the SAME ShipDecision.
                  || name = "FS.GG.Governance.HumanText"
                  // F27 wiring (063): the rich render + capability sensing at the edge. Spectre is referenced
                  // by HumanRender ONLY — ShipCommand reaches it through RichRender.emitStdout/senseCapability,
                  // never a direct Spectre reference (the forbidden check below still guards that).
                  || name = "FS.GG.Governance.HumanRender"
                  // F045: AuditJson's `ofShipDecision` takes a `CacheEligibilityReport option` ⇒ F041 arrives.
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
                  // F25 wiring (064): the four consumed cost-cache/provenance cores + F033 Provenance, wired at
                  // the MVU edge for the budget filter, kinded-run recording, and the two deterministic sidecars.
                  || name = "FS.GG.Governance.CostBudget"
                  || name = "FS.GG.Governance.CommandKind"
                  || name = "FS.GG.Governance.CostBudgetJson"
                  || name = "FS.GG.Governance.ProvenanceJson"
                  || name = "FS.GG.Governance.Provenance"
                  || name = "FS.GG.Governance.AgentReviewKey"
                  // F070 (stale-view blocking): the pure CurrencyEnforcement leaf (folded into the verdict),
                  // the impure CurrencySensing core (the shared refresh.yml parse + lock read + source digest
                  // at the edge), and RefreshJson (RefreshModel — the generatedViews kind + the manifest types).
                  // All CORES — NOT the RefreshCommand host (the repo forbids command→command references).
                  || name = "FS.GG.Governance.CurrencyEnforcement"
                  || name = "FS.GG.Governance.CurrencySensing"
                  || name = "FS.GG.Governance.RefreshJson"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  shipCommand.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "ShipCommand must depend on the nine cores/BCL/FSharp.Core only; found: %A" offending)

              // It must NOT reference RouteJson/GatesJson (it projects neither) nor the kernel/host/cli.
              let forbidden =
                  shipCommand.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters"
                      // F27 wiring (063), FR-011/SC-007: Spectre stays confined to HumanRender — the ship
                      // host reaches rich rendering through HumanRender's emitStdout, never a direct reference.
                      || n = "Spectre.Console")

              Expect.isEmpty forbidden (sprintf "ShipCommand must not reference RouteJson/GatesJson/kernel/host/cli/adapters; found: %A" forbidden)
          } ]
