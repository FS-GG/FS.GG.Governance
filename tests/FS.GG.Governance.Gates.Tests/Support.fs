module FS.GG.Governance.Gates.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model

// Fixture builders that assemble REAL inputs of the exact type `buildRegistry` consumes — a real
// in-memory `TypedFacts` (the shape Config emits, the genuine value a downstream caller passes) —
// not synthetic mocks (Principle V, research D10). No I/O, no YAML.

/// Locate the repo root (the dir holding the solution) by walking up from the test binary —
/// used by the surface-drift baseline check (Principle II).
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext =
            File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))

        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

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
      Maturity = maturity }

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
