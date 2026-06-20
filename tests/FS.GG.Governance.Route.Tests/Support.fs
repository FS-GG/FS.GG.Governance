module FS.GG.Governance.Route.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model

// Fixture builders that assemble REAL inputs of the exact types `Route.select` consumes — a real
// `GateRegistry` (from `Gates.buildRegistry`), a real `RouteReport` (from `Routing.route`), and a
// real `FindingReport` (from `Findings.findUnknownGovernedPaths`), all from a real in-memory
// `TypedFacts` — never synthetic mocks (Principle V, research D8). Driving the genuine
// F015->F017->F018->F019 chain transitively re-exercises the upstream rows. No I/O, no YAML.

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

/// A governed path from a raw (already F014-normalized) string.
let gp (s: string) = GovernedPath s

/// A real `Check` from its distinguishing fields, with inert defaults so a test varies only the
/// field under test. `command` is the optional `tooling.yml` reference; everything else carries a
/// plain declared value.
let check
    (domain: string)
    (checkId: string)
    (command: string option)
    (cost: Cost)
    : Check =
    { Id = CheckId checkId
      Domain = DomainId domain
      Command = command |> Option.map CommandId
      Owner = Owner "fixture"
      Cost = cost
      Environment = Local
      Maturity = Observe }

/// A real `CommandSpec` from `(commandId, timeoutSeconds)` with inert defaults for the fields the
/// downstream join never reads.
let command (commandId: string) (timeoutSeconds: int) : CommandSpec =
    { Id = CommandId commandId
      Command = "fixture --run"
      Timeout = TimeoutLimit timeoutSeconds
      Environment = Local }

/// Build a `Surface` from `(class, id, paths)` with fixed inert defaults for the fields the
/// route join never reads (`Owner`/`Maturity`).
let surface (cls: SurfaceClass) (id: string) (paths: string list) : Surface =
    { Id = SurfaceId id
      Class = cls
      Paths = paths |> List.map GovernedPath
      Owner = Owner "fixture"
      Maturity = Observe }

/// Assemble a real `Valid TypedFacts` with a governed root, a `glob -> domain` path map (so
/// `Routing.route` yields genuine `Routed`/`UnmatchedInRoot`/`OutOfScope` outcomes), a declared
/// surface list (so F017 sees real `Routine`/`ProtectedSurface` regions), a check list (so
/// `Gates.buildRegistry` produces real gates), and a command list. The declared domains are the
/// union of those referenced by the path map and the checks; optional policy is absent and
/// tooling is present when commands are supplied. The genuine downstream input — not a fake.
let facts
    (root: string)
    (pathMap: (string * string) list)
    (surfaces: Surface list)
    (checks: Check list)
    (commands: CommandSpec list)
    : TypedFacts =
    let entries =
        pathMap |> List.map (fun (g, d) -> { Glob = GovernedPath g; Capability = DomainId d })

    let domains =
        (entries |> List.map (fun e -> e.Capability)) @ (checks |> List.map (fun c -> c.Domain))
        |> List.distinct

    { Project =
        { SchemaVersion = SchemaVersion 1
          Id = ProjectId "fixture"
          Domains = domains
          GovernedRoot = GovernedPath root
          PackageSurfaces = []
          PolicyRef = None
          CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 1
          Domains = domains
          PathMap = entries
          Surfaces = surfaces
          Checks = checks }
      Tooling =
        match commands with
        | [] -> None
        | cs ->
            Some
                { SchemaVersion = SchemaVersion 1
                  Commands = cs
                  EnvironmentClasses = []
                  ExternalTools = [] } }

/// (b) The real F018 registry for these facts — `Gates.buildRegistry`, the genuine producer.
let registryOf (facts: TypedFacts) : GateRegistry =
    FS.GG.Governance.Gates.Gates.buildRegistry facts

/// (c) The real F015 route report for a raw candidate-path set — normalized exactly as a
/// downstream caller would (`Config.Model.normalizePath`), then `Routing.route`.
let reportOf (facts: TypedFacts) (rawPaths: string list) : RouteReport =
    rawPaths
    |> List.map normalizePath
    |> FS.GG.Governance.Routing.Routing.route facts

/// (d) The real F017 finding report for facts + a route report — `findUnknownGovernedPaths`.
let findingsOf (facts: TypedFacts) (report: RouteReport) : FindingReport =
    FS.GG.Governance.Findings.Findings.findUnknownGovernedPaths facts report

/// (e) The convenience full chain: facts + raw candidate paths -> the real `RouteResult` from the
/// genuine F015->F017->F018->F019 chain over real upstream values.
let selectOf (facts: TypedFacts) (rawPaths: string list) : RouteResult =
    let registry = registryOf facts
    let report = reportOf facts rawPaths
    let findings = findingsOf facts report
    FS.GG.Governance.Route.Route.select registry report findings
