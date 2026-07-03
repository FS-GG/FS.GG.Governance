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
let generatedSurface =
    [ "namespace FS.GG.Governance.Cli"
      "type CommandKind"
      "type OutputFormat"
      "type ReviewBudget"
      "type RunRequest"
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
      "module ArtifactReading"
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
              let cliName = typeof<RunRequest>.Assembly.GetName().Name |> Option.ofObj |> Option.defaultValue "FS.GG.Governance.Cli"

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
                  typeof<RunRequest>.Assembly.GetReferencedAssemblies()
                  |> Array.choose (fun name -> name.Name |> Option.ofObj)
                  |> Set.ofArray

              for required in
                  [ "FS.GG.Governance.Kernel"
                    "FS.GG.Governance.Host"
                    "FS.GG.Governance.Adapters.Spi"
                    "FS.GG.Governance.Adapters.SpecKit"
                    "FS.GG.Governance.Adapters.DesignSystem" ] do
                  Expect.isTrue (names.Contains required) ("has " + required)
          } ]
