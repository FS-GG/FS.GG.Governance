module FS.GG.Governance.RouteExplain.Tests.Support

open System
open System.IO
open FsCheck
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model

// Shared REAL-input builders + FsCheck generators for the F031 tests (Principle V — no mocks). Two
// families of builder:
//   • the genuine F014->F015->F017->F018->F019 chain (`facts` -> `registryOf` -> `selectOf`), reusing the
//     Route `Support.fs` shape so a `RouteResult`/`GateRegistry` is a real upstream value; and
//   • hand-built literal `Gate`/`SelectedGate`/`RouteResult`/`GateRegistry` builders for the
//     disordered/duplicate inputs the chain will not naturally produce (`gate`/`sp`/`selGate`/`routeOf`/
//     `catalog`).
// No I/O beyond repo-root resolution.

// ── Repo root (for the surface baseline path) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// ── The genuine upstream chain (real F018 registry / F019 route — no mocks) ──

/// A governed path from a raw (already F014-normalized) string.
let gp (s: string) = GovernedPath s

/// A real `Check` — varies domain/checkId/command/cost/environment; inert defaults elsewhere so a test
/// varies only the fields under test. (Unlike the Route fixture, `environment` is a parameter — the
/// alternative rule is a classification over the gate's declared `EnvironmentClass`.)
let check
    (domain: string)
    (checkId: string)
    (command: string option)
    (cost: Cost)
    (environment: EnvironmentClass)
    : Check =
    { Id = CheckId checkId
      Domain = DomainId domain
      Command = command |> Option.map CommandId
      Owner = Owner "fixture"
      Cost = cost
      Environment = environment
      Maturity = Observe }

/// A real `CommandSpec` from `(commandId, timeoutSeconds)` with inert defaults.
let command (commandId: string) (timeoutSeconds: int) : CommandSpec =
    { Id = CommandId commandId
      Command = "fixture --run"
      Timeout = TimeoutLimit timeoutSeconds
      Environment = Local }

/// Assemble a real `TypedFacts` with a governed root, a `glob -> domain` path map, declared checks, and
/// commands — the genuine input to the F015/F017/F018 producers (the Route `Support.fs` shape). No
/// surfaces are needed for this row (it reads no findings region).
let facts
    (root: string)
    (pathMap: (string * string) list)
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
          Surfaces = []
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

/// The real F018 registry for these facts — `Gates.buildRegistry`, the genuine producer.
let registryOf (facts: TypedFacts) : GateRegistry =
    FS.GG.Governance.Gates.Gates.buildRegistry facts

/// The real F015 route report for a raw candidate-path set.
let reportOf (facts: TypedFacts) (rawPaths: string list) : RouteReport =
    rawPaths
    |> List.map normalizePath
    |> FS.GG.Governance.Routing.Routing.route facts

/// The real F017 finding report for facts + a route report.
let findingsOf (facts: TypedFacts) (report: RouteReport) : FindingReport =
    FS.GG.Governance.Findings.Findings.findUnknownGovernedPaths facts report

/// The convenience full chain: facts + raw candidate paths -> the real `RouteResult` from the genuine
/// F015->F017->F018->F019 chain over real upstream values.
let selectOf (facts: TypedFacts) (rawPaths: string list) : RouteResult =
    let registry = registryOf facts
    let report = reportOf facts rawPaths
    let findings = findingsOf facts report
    FS.GG.Governance.Route.Route.select registry report findings

// ── Hand-built literal builders (for disordered/duplicate inputs the chain won't produce) ──

/// A literal `Gate` with the stable `"domain:checkId"` id, the given declared cost, and the given
/// declared environment (carried inside `FreshnessKey`, the only place a gate's environment lives — D6).
let gate (domain: string) (checkId: string) (cost: Cost) (environment: EnvironmentClass) : Gate =
    let gid = domain + ":" + checkId

    { Id = GateId gid
      Domain = DomainId domain
      Description = sprintf "fixture gate %s" gid
      Prerequisites = []
      Cost = cost
      Timeout = TimeoutLimit 60
      Owner = Owner "fixture"
      Maturity = Observe
      ProductCheck = (environment = Release)
      FreshnessKey =
        { Check = CheckId checkId
          Domain = DomainId domain
          Cost = cost
          Environment = environment
          Command = None } }

/// A literal `SelectingPath` (changed path + the glob it won on).
let sp (path: string) (glob: string) : SelectingPath =
    { Path = GovernedPath path
      MatchedGlob = GovernedPath glob }

/// A literal `SelectedGate` — a gate plus its route trace.
let selGate (g: Gate) (paths: SelectingPath list) : SelectedGate = { Gate = g; SelectingPaths = paths }

/// A literal `GateRegistry` from a gate list (the catalog of candidate alternatives).
let catalog (gates: Gate list) : GateRegistry = { Gates = gates }

/// A literal `RouteResult` from a selected-gate list. `explain` reads only `SelectedGates`; the carried
/// `Findings`/`Cost` are inert here (an empty report + the all-zero rollup), present only to type the
/// value.
let routeOf (selected: SelectedGate list) : RouteResult =
    { SelectedGates = selected
      Findings = { Findings = [] }
      Cost =
        { Cheap = 0
          Medium = 0
          High = 0
          Exhaustive = 0 } }

/// The F031 worked-example catalog (contracts/explanation-semantics.md §2): domain `build` spanning every
/// `Cost` tier and the local/non-local `EnvironmentClass`es, plus a cross-domain `docs:links`.
let workedExampleGates: Gate list =
    [ gate "build" "full" Exhaustive Ci // the high-cost finding gate
      gate "build" "unit" Cheap Local // same domain, cheaper, local  -> candidate
      gate "build" "integration" Medium LocalOrCi // same domain, cheaper, local  -> candidate
      gate "build" "smoke-ci" Medium Ci // same domain, cheaper, NOT local
      gate "build" "release-verify" Exhaustive Local // same domain, local, NOT strictly cheaper
      gate "docs" "links" Cheap Local ] // different domain

// ── FsCheck generators (real values, no mocks) ──

let private genCost: Gen<Cost> =
    Gen.elements [ Cheap; Medium; High; Exhaustive ]

let private genEnvironment: Gen<EnvironmentClass> =
    Gen.elements [ Local; Ci; LocalOrCi; Release ]

let private genDomain: Gen<string> = Gen.elements [ "build"; "test"; "docs" ]

let private genCheckId: Gen<string> =
    Gen.elements [ "full"; "unit"; "integration"; "smoke"; "lint"; "links" ]

let private genGate: Gen<Gate> =
    gen {
        let! domain = genDomain
        let! checkId = genCheckId
        let! cost = genCost
        let! env = genEnvironment
        return gate domain checkId cost env
    }

let private genSelectingPath: Gen<SelectingPath> =
    gen {
        let! p = Gen.elements [ "src/a.fs"; "src/b.fs"; "docs/x.md"; "test/c.fs" ]
        let! glob = Gen.elements [ "src/**"; "docs/**"; "test/**" ]
        return sp p glob
    }

let private genSelectedGate: Gen<SelectedGate> =
    gen {
        let! g = genGate
        let! paths = Gen.listOf genSelectingPath
        return selGate g paths
    }

let private genRouteResult: Gen<RouteResult> =
    gen {
        let! selected = Gen.listOf genSelectedGate
        return routeOf selected
    }

let private genGateRegistry: Gen<GateRegistry> =
    gen {
        let! gates = Gen.listOf genGate
        return catalog gates
    }

type Generators =
    static member RouteResult() : Arbitrary<RouteResult> = Arb.fromGen genRouteResult
    static member GateRegistry() : Arbitrary<GateRegistry> = Arb.fromGen genGateRegistry

/// FsCheck config registering the real `RouteResult` / `GateRegistry` generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<Generators> ] }
