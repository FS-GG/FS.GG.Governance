module FS.GG.Governance.CacheEligibilityCommand.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CacheEligibilityCommand

// T026/T027 (Principle II, C6) — reflective API surface-drift baseline + dependency/scope-hygiene guard,
// now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in
// the host. The public surface is exactly the `Loop` + `Interpreter` modules (the two `.fsi` contracts);
// the dependency boundary is the F022 selection cores + the cache cores (+ transitive FreshnessKey) — and
// NO RouteJson/GatesJson/AuditJson/RouteCommand (C6).

let private commandAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.CacheEligibilityCommand"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CacheEligibilityCommand" "FS.GG.Governance.CacheEligibilityCommand" commandAsm

          test "the public API surface is exactly the Loop + Interpreter modules (plus the Exe entry)" {
              let typeNames = commandAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.CacheEligibilityCommand.LoopModule"))
                  "Loop module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.CacheEligibilityCommand.InterpreterModule"))
                  "Interpreter module is public"

              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "CacheEligibilityCommand.LoopModule"
                          || n.Contains "CacheEligibilityCommand.InterpreterModule"
                          || n.Contains "CacheEligibilityCommand.Loop+"
                          || n.Contains "CacheEligibilityCommand.Interpreter+"
                          || n.Contains "CacheEligibilityCommand.Program"))

              Expect.isEmpty unexpected (sprintf "only Loop/Interpreter (+ Program entry) are public; found extra: %A" unexpected)
          }

          SurfaceDrift.referencesOnly
              "CacheEligibilityCommand"
              (fun n ->
                  n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Snapshot"
                  || n = "FS.GG.Governance.Routing"
                  || n = "FS.GG.Governance.Findings"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.FreshnessResolution"
                  || n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.CacheEligibilityJson"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.HumanText"
                  || n = "FS.GG.Governance.HumanRender"
                  || n = "FS.GG.Governance.CommandHost")
              commandAsm ]
