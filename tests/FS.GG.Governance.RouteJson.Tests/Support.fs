module FS.GG.Governance.RouteJson.Tests.Support

open System
open System.IO
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model

// Fixture builders that assemble REAL inputs of the exact types the F015->F017->F018->F019 chain
// consumes — a real `TypedFacts`, a real `GateRegistry` (from `Gates.buildRegistry`), a real
// `RouteReport` (from `Routing.route`), and a real `FindingReport` (from
// `Findings.findUnknownGovernedPaths`) — never synthetic mocks (Principle V, research D7). The
// `RouteResult` they produce is the genuine value a downstream `fsgg route`/CI/agent caller holds;
// the JSON read helpers inspect the EMITTED BYTES via a read-only `JsonDocument` parse, exactly as
// the kernel's `Json` tests do. No I/O, no YAML, no clock.

// ── (a) repo root (for the surface-drift baseline check, Principle II) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext =
            File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))

        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// ── (a) real upstream-assembly fixture builders (mirroring the F019 test Support) ──

/// A governed path from a raw (already F014-normalized) string.
let gp (s: string) = GovernedPath s

/// A real `Check` from its distinguishing fields, with inert defaults so a test varies only the
/// fields under test. `command` is the optional `tooling.yml` reference; `cost`/`environment`/
/// `maturity` carry plain declared values (so the gate's projected `cost`/`maturity`/`freshnessKey`
/// inputs are real, not fabricated).
let check
    (domain: string)
    (checkId: string)
    (command: string option)
    (cost: Cost)
    (environment: EnvironmentClass)
    (maturity: Maturity)
    : Check =
    { Id = CheckId checkId
      Domain = DomainId domain
      Command = command |> Option.map CommandId
      Owner = Owner ("owner-" + domain)
      Cost = cost
      Environment = environment
      Maturity = maturity }

/// A real `CommandSpec` from `(commandId, timeoutSeconds)` with inert defaults for the fields the
/// downstream join never reads. The timeout becomes the gate's projected `timeout`.
let command (commandId: string) (timeoutSeconds: int) : CommandSpec =
    { Id = CommandId commandId
      Command = "fixture --run"
      Timeout = TimeoutLimit timeoutSeconds
      Environment = Local }

/// Build a `Surface` from `(class, id, paths)` with fixed inert defaults for the fields the route
/// join never reads (`Owner`/`Maturity`).
let surface (cls: SurfaceClass) (id: string) (paths: string list) : Surface =
    { Id = SurfaceId id
      Class = cls
      Paths = paths |> List.map GovernedPath
      Owner = Owner "fixture"
      Maturity = Observe }

/// Assemble a real `TypedFacts` with a governed root, a `glob -> domain` path map (so
/// `Routing.route` yields genuine `Routed`/`UnmatchedInRoot`/`OutOfScope` outcomes), a declared
/// surface list (so F017 sees real `Routine`/`ProtectedSurface` regions), a check list (so
/// `Gates.buildRegistry` produces real gates), and a command list. The genuine downstream input —
/// not a fake.
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

/// (c) The real F015 route report for a raw candidate-path set — normalized exactly as a downstream
/// caller would (`Config.Model.normalizePath`), then `Routing.route`.
let reportOf (facts: TypedFacts) (rawPaths: string list) : RouteReport =
    rawPaths
    |> List.map normalizePath
    |> FS.GG.Governance.Routing.Routing.route facts

/// (d) The real F017 finding report for facts + a route report — `findUnknownGovernedPaths`.
let findingsOf (facts: TypedFacts) (report: RouteReport) : FindingReport =
    FS.GG.Governance.Findings.Findings.findUnknownGovernedPaths facts report

/// (e) The convenience full chain: facts + raw candidate paths -> the real `RouteResult` from the
/// genuine F015->F017->F018->F019 chain over real upstream values (mirrors the F019 `selectOf`).
let resultOf (facts: TypedFacts) (rawPaths: string list) : RouteResult =
    let registry = registryOf facts
    let report = reportOf facts rawPaths
    let findings = findingsOf facts report
    FS.GG.Governance.Route.Route.select registry report findings

// ── JsonDocument read helpers (read-only inspection of the emitted bytes) ──

/// Parse the emitted document text into a JsonDocument (the caller disposes via `use`).
let parse (json: string) : JsonDocument = JsonDocument.Parse json

/// Fail-fast read of a required JSON string (the projection never emits null where these probe).
let private reqStr (el: JsonElement) : string =
    match el.GetString() with
    | null -> failwith "expected a JSON string but found null"
    | s -> s

/// Fail-fast read of a named string property on an object element.
let strField (el: JsonElement) (name: string) : string = reqStr (el.GetProperty name)

/// The top-level field names in their emitted order.
let topLevelFieldOrder (doc: JsonDocument) : string list =
    [ for p in doc.RootElement.EnumerateObject() -> p.Name ]

/// The field names of an object element in their emitted order.
let fieldOrder (el: JsonElement) : string list =
    [ for p in el.EnumerateObject() -> p.Name ]

/// The `selectedGates` array elements.
let selectedGates (doc: JsonDocument) : JsonElement list =
    [ for g in doc.RootElement.GetProperty("selectedGates").EnumerateArray() -> g ]

/// The declared `id` of each selected gate, in emitted order.
let selectedGateIds (doc: JsonDocument) : string list =
    selectedGates doc |> List.map (fun g -> strField g "id")

/// The `findings` array elements.
let findings (doc: JsonDocument) : JsonElement list =
    [ for f in doc.RootElement.GetProperty("findings").EnumerateArray() -> f ]

/// The `id` token of each finding, in emitted order.
let findingIds (doc: JsonDocument) : string list =
    findings doc |> List.map (fun f -> strField f "id")

/// A per-tier integer count from the top-level `cost` object.
let costTier (doc: JsonDocument) (tier: string) : int =
    doc.RootElement.GetProperty("cost").GetProperty(tier).GetInt32()

/// Whether an object element has a property of the given name.
let hasField (el: JsonElement) (name: string) : bool =
    match el.TryGetProperty name with
    | true, _ -> true
    | false, _ -> false

/// The `selectingPaths` of a gate element as `(path, matchedGlob)` pairs.
let selectingPaths (gate: JsonElement) : (string * string) list =
    [ for p in gate.GetProperty("selectingPaths").EnumerateArray() ->
          strField p "path", strField p "matchedGlob" ]

/// Every `path` and `matchedGlob` string emitted anywhere in the document (across all
/// selectingPaths and all findings) — the positive path-allowlist probe.
let allEmittedPaths (doc: JsonDocument) : string list =
    let fromGates =
        selectedGates doc
        |> List.collect (fun g -> selectingPaths g |> List.collect (fun (p, m) -> [ p; m ]))

    let fromFindings =
        findings doc |> List.map (fun f -> strField f "path")

    fromGates @ fromFindings
