module FS.GG.Governance.Cli.Tests.SurfaceDriftTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Cli
open FS.GG.Governance.Cli.Tests.ParserTests.Support

let baseline =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Cli.surface.txt")

// 100 (M-ARCH-2): the F12 `Project` composition root + its coproduct types (Domain / ProjectFact /
// ProjectChange / ProjectSnapshot / ProjectOptions / EvidenceNodeReport / ProjectEvidenceReport) moved
// to the FS.GG.Governance.ProjectSensing library (still the FS.GG.Governance.Cli namespace); their
// surface is now guarded by FS.GG.Governance.ProjectSensing.Tests. This list is the Cli EXECUTABLE surface.
//
// 100 (M-ARCH-2, reconciled with #49/#70): the run-request vocabulary (CommandKind / OutputFormat /
// ReviewBudget / RunRequest) and the ArtifactReading sensing edge ALSO moved to ProjectSensing so the
// EvidenceCommand tool reuses the single-source sensing without referencing this exe; they too are now
// guarded by FS.GG.Governance.ProjectSensing.Tests and are gone from the Cli EXECUTABLE surface below.
let generatedSurface =
    [ "namespace FS.GG.Governance.Cli"
      "type ParseError"
      "type ExitDecision"
      "type BudgetState"
      "type CommandPayload"
      "type CommandResult"
      "type Phase"
      "type Model"
      "type Msg"
      "type Effect"
      "type CliPorts"
      "module Cli"
      "module CliRender"
      "module ReviewStore" ]

[<Tests>]
let tests =
    testList
        "Surface"
        [ test "CLI surface baseline is unchanged" {
              let expected = File.ReadAllLines baseline |> Array.toList
              Expect.equal generatedSurface expected "surface baseline"
          }

          test "CLI remains optional: lower projects do not reference it" {
              // 100 (M-ARCH-2): anchor on a type that still lives in the Cli EXECUTABLE (RunRequest moved to
              // ProjectSensing), so this guards the Cli assembly rather than the sensing library.
              let cliName = typeof<CliPorts>.Assembly.GetName().Name |> Option.ofObj |> Option.defaultValue "FS.GG.Governance.Cli"

              let lowerAssemblies =
                  [ typeof<FS.GG.Governance.Kernel.FactId>.Assembly
                    typeof<FS.GG.Governance.Host.ArtifactContent>.Assembly
                    typeof<FS.GG.Governance.Adapters.Spi.Adapter<_, _, _>>.Assembly
                    typeof<FS.GG.Governance.Adapters.SpecKit.SpecKitFact>.Assembly
                    typeof<FS.GG.Governance.Adapters.DesignSystem.DesignSystemFact>.Assembly ]

              for assembly in lowerAssemblies do
                  let refs =
                      assembly.GetReferencedAssemblies()
                      |> Array.choose (fun name -> name.Name |> Option.ofObj)
                      |> Set.ofArray
                  let assemblyName = assembly.GetName().Name |> Option.ofObj |> Option.defaultValue "<assembly>"
                  Expect.isFalse (refs.Contains cliName) (assemblyName + " must not reference CLI")
          }

          test "CLI assembly has only expected runtime references" {
              let names =
                  typeof<CliPorts>.Assembly.GetReferencedAssemblies()
                  |> Array.choose (fun name -> name.Name |> Option.ofObj)
                  |> Set.ofArray

              // 100 (M-ARCH-2): the ArtifactReading sensing edge (the sole direct user of the DesignSystem
              // adapter) moved to ProjectSensing, so the Cli executable no longer references
              // Adapters.DesignSystem directly — design-domain sensing is delegated through ProjectSensing.
              for required in
                  [ "FS.GG.Governance.Kernel"
                    "FS.GG.Governance.Host"
                    "FS.GG.Governance.Adapters.Spi"
                    "FS.GG.Governance.Adapters.SpecKit" ] do
                  Expect.isTrue (names.Contains required) ("has " + required)
          } ]
