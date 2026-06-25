module FS.GG.Governance.GatesJson.Tests.Support

open System
open System.IO
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model

// Fixture builders that assemble REAL inputs of the exact type the F014->F018 chain consumes — a real
// `TypedFacts` and a real `GateRegistry` (from `Gates.buildRegistry`) — never synthetic mocks
// (Principle V, research D7). The `GateRegistry` they produce is the genuine value a downstream
// `fsgg`/CI/agent caller holds; the JSON read helpers inspect the EMITTED BYTES via a read-only
// `JsonDocument` parse, exactly as the kernel's `Json` tests do. No I/O, no YAML, no clock.

// ── repo root (for the surface-drift baseline check, Principle II) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext =
            File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))

        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// ── real upstream-assembly fixture builders (mirroring the F018/F020 test Support) ──

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
      Maturity = maturity
      Tier = None }

/// A real `CommandSpec` from `(commandId, timeoutSeconds)` with inert defaults for the fields the
/// gate projection never reads. The timeout becomes the gate's projected `timeout`.
let command (commandId: string) (timeoutSeconds: int) : CommandSpec =
    { Id = CommandId commandId
      Command = "fixture --run"
      Timeout = TimeoutLimit timeoutSeconds
      Environment = Local }

/// Assemble a real `TypedFacts` with a governed root, a check list (so `Gates.buildRegistry` produces
/// real gates), and a command list. The genuine downstream input — not a fake. The path map / surface
/// list are inert here (gates derive from checks only), but a minimal map keeps domains declared.
let facts (checks: Check list) (commands: CommandSpec list) : TypedFacts =
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

/// Convenience: a real registry directly from a check list + command list.
let registryFor (checks: Check list) (commands: CommandSpec list) : GateRegistry =
    registryOf (facts checks commands)

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

/// The `gates` array elements.
let gates (doc: JsonDocument) : JsonElement list =
    [ for g in doc.RootElement.GetProperty("gates").EnumerateArray() -> g ]

/// The declared `id` of each gate, in emitted order.
let gateIds (doc: JsonDocument) : string list =
    gates doc |> List.map (fun g -> strField g "id")

/// Find the emitted gate object with the given declared id.
let gateById (doc: JsonDocument) (id: string) : JsonElement =
    gates doc |> List.find (fun g -> strField g "id" = id)

/// The `prerequisites` of a gate element as the carried `requiresCommand` strings.
let prerequisites (gate: JsonElement) : string list =
    [ for p in gate.GetProperty("prerequisites").EnumerateArray() -> strField p "requiresCommand" ]

/// Whether an object element has a property of the given name.
let hasField (el: JsonElement) (name: string) : bool =
    match el.TryGetProperty name with
    | true, _ -> true
    | false, _ -> false
