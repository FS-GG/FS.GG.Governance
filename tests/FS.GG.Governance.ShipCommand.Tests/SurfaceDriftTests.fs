module FS.GG.Governance.ShipCommand.Tests.SurfaceDriftTests

open System
open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ShipCommand

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library. The
// public surface is exactly the `Loop` + `Interpreter` modules (the two `.fsi` contracts); the dependency
// boundary is the NINE cores + BCL + FSharp.Core, and NO edge into the kernel-era Host/Cli (research D1).

let private shipCommand =
    // Touch a member to force the library assembly to load, then locate it by name.
    Loop.exitCode Loop.Success |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ShipCommand"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ShipCommand" "FS.GG.Governance.ShipCommand" shipCommand

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

          SurfaceDrift.referencesOnly
              "ShipCommand"
              (fun n ->
                  n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Snapshot"
                  || n = "FS.GG.Governance.Routing"
                  || n = "FS.GG.Governance.Findings"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Adapters.SddHandoff"
                  || n = "FS.GG.Governance.Enforcement"
                  || n = "FS.GG.Governance.Ship"
                  || n = "FS.GG.Governance.AuditJson"
                  || n = "FS.GG.Governance.HumanText"
                  || n = "FS.GG.Governance.HumanRender"
                  || n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.FreshnessSensing"
                  || n = "FS.GG.Governance.FreshnessResolution"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.EvidenceReuseStore"
                  || n = "FS.GG.Governance.GateRun"
                  || n = "FS.GG.Governance.GateExecution"
                  || n = "FS.GG.Governance.EvidenceCapture"
                  || n = "FS.GG.Governance.CommandHost"
                  || n = "FS.GG.Governance.ExecutionRecord"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.CostBudget"
                  || n = "FS.GG.Governance.CommandKind"
                  || n = "FS.GG.Governance.CostBudgetJson"
                  || n = "FS.GG.Governance.ProvenanceJson"
                  || n = "FS.GG.Governance.Provenance"
                  || n = "FS.GG.Governance.AgentReviewKey"
                  || n = "FS.GG.Governance.CurrencyEnforcement"
                  || n = "FS.GG.Governance.CurrencySensing"
                  || n = "FS.GG.Governance.RefreshJson")
              shipCommand ]
