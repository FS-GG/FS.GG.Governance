module FS.GG.Governance.VerifyCommand.Tests.SurfaceDriftTests

open System
open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.VerifyCommand

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library. The
// public surface is exactly the `Loop` + `Interpreter` modules (the two `.fsi` contracts).

let private verifyCommand =
    Loop.exitCode Loop.Success |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.VerifyCommand"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "VerifyCommand" "FS.GG.Governance.VerifyCommand" verifyCommand

          test "the public API surface is exactly Loop + Interpreter + the three 076 fold seams (plus the Exe entry)" {
              let typeNames = verifyCommand.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.VerifyCommand.LoopModule"))
                  "Loop module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.VerifyCommand.InterpreterModule"))
                  "Interpreter module is public"

              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "VerifyCommand.LoopModule"
                          || n.Contains "VerifyCommand.InterpreterModule"
                          || n.Contains "VerifyCommand.Loop+"
                          || n.Contains "VerifyCommand.Interpreter+"
                          || n.Contains "VerifyCommand.Program"
                          // 076 Phase C: the three additive, .fsi-curated host fold seam modules (FR-004).
                          || n.Contains "VerifyCommand.SurfaceFoldModule"
                          || n.Contains "VerifyCommand.ViewCurrencyFoldModule"
                          || n.Contains "VerifyCommand.ReleasePreviewModule"))

              Expect.isEmpty
                  unexpected
                  (sprintf "only Loop/Interpreter/Program + the three 076 fold seams are public; found extra: %A" unexpected)
          }

          test "VerifyCommand references VerifyJson (not AuditJson) and no kernel/host/cli" {
              let referenced =
                  verifyCommand.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              Expect.isTrue (referenced |> Array.contains "FS.GG.Governance.VerifyJson") "references VerifyJson"

              let forbidden =
                  referenced
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      // F081 (research D6): the SDD-handoff consumer is the ONE permitted Adapters.* edge each
                      // verdict host gains (so a produced handoff drives the verdict); other adapters stay forbidden.
                      || (n.StartsWith "FS.GG.Governance.Adapters" && n <> "FS.GG.Governance.Adapters.SddHandoff")
                      // F27 wiring (063), FR-011/SC-007: Spectre stays confined to HumanRender — the verify host
                      // reaches rich rendering through HumanRender's emitStdout, never a direct reference.
                      || n = "Spectre.Console")

              Expect.isEmpty forbidden (sprintf "verify must not reference AuditJson/RouteJson/GatesJson/kernel/host/cli/adapters/Spectre; found: %A" forbidden)
          } ]
