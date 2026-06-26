module FS.GG.Governance.Gates.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

/// A real `Check` from its distinguishing fields, with inert defaults so a test varies only the
/// field under test. `command` is the optional `tooling.yml` reference; everything else carries a
/// plain declared value.
let check
    (domain: string)
    (checkId: string)
    (command: string option)
    (owner: string)
    (cost: Cost)
    (environment: EnvironmentClass)
    (maturity: Maturity)
    : Check =
    { Id = CheckId checkId
      Domain = DomainId domain
      Command = command |> Option.map CommandId
      Owner = Owner owner
      Cost = cost
      Environment = environment
      Maturity = maturity
      Tier = None }

/// A real `CommandSpec` from `(commandId, timeoutSeconds)` with inert defaults for the fields the
/// registry never reads (`Command`/`Environment`).
let command (commandId: string) (timeoutSeconds: int) : CommandSpec =
    { Id = CommandId commandId
      Command = "fixture --run"
      Timeout = TimeoutLimit timeoutSeconds
      Environment = Local }

/// Assemble a real `Valid TypedFacts` from a check list and a command list. `Project` is an inert
/// default, `Policy = None`, the declared domains are exactly those the checks reference, and the
/// commands are wrapped as `Tooling = Some { ... }` (a present `tooling.yml`). The genuine
/// downstream input — not a fake.
let factsOf (checks: Check list) (commands: CommandSpec list) : TypedFacts =
    let domains = checks |> List.map (fun c -> c.Domain) |> List.distinct

    { Project =
        { SchemaVersion = SchemaVersion 1
          Id = ProjectId "fixture"
          Domains = domains
          GovernedRoot = GovernedPath "src"
          PackageSurfaces = []
          PolicyRef = None
          CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 1
          Domains = domains
          PathMap = []
          Surfaces = []
          Checks = checks }
      Tooling =
        Some
            { SchemaVersion = SchemaVersion 1
              Commands = commands
              EnvironmentClasses = []
              ExternalTools = [] } }

/// The absent-`tooling.yml` variant: `Tooling = None`, so the command-timeout index is empty and a
/// command-referencing check still falls back to `defaultTimeout` (covers C1/I1).
let factsNoTooling (checks: Check list) : TypedFacts =
    let withTooling = factsOf checks []
    { withTooling with Tooling = None }
