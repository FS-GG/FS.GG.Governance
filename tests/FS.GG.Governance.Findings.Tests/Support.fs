module FS.GG.Governance.Findings.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model

// Fixture builders that assemble REAL inputs of the exact types `findUnknownGovernedPaths`
// consumes — a real in-memory `TypedFacts` (the shape Config emits) and a real `RouteReport`
// (built by calling `Routing.route`, the genuine downstream producer) — not synthetic mocks
// (Principle V, research D7). No I/O, no YAML.

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

/// A surface id.
let sid (s: string) = SurfaceId s

/// Build a `Surface` from `(class, id, paths)` with fixed inert defaults for the fields the
/// F017 decision never reads (`Owner`/`Maturity`).
let surface (cls: SurfaceClass) (id: string) (paths: string list) : Surface =
    { Id = SurfaceId id
      Class = cls
      Paths = paths |> List.map GovernedPath
      Owner = Owner "fixture"
      Maturity = Observe
      EvidenceTag = None
      TemplateProfile = None
      Baseline = None }

/// Assemble a real `TypedFacts` with the given governed root, a `glob → domain` path map (so
/// `Routing.route` yields genuine `Routed`/`UnmatchedInRoot`/`OutOfScope` outcomes), and a
/// declared surface list (so the classifier sees real `Routine`/`ProtectedSurface`/inert
/// surfaces). The declared domains are exactly those referenced by the path map; optional
/// policy/tooling files are absent.
let facts (root: string) (pairs: (string * string) list) (surfaces: Surface list) : TypedFacts =
    let entries =
        pairs |> List.map (fun (g, d) -> { Glob = GovernedPath g; Capability = DomainId d })

    let domains = entries |> List.map (fun e -> e.Capability) |> List.distinct

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
          Checks = [] }
      Tooling = None }

/// Route a set of raw candidate path strings against the facts — normalizing them exactly as a
/// downstream caller would (via `Config.Model.normalizePath`) and calling the genuine
/// `Routing.route`. Yields a REAL `RouteReport`.
let routeOf (facts: TypedFacts) (rawPaths: string list) : RouteReport =
    rawPaths
    |> List.map normalizePath
    |> FS.GG.Governance.Routing.Routing.route facts

/// Hand-build a `RouteReport` from a `PathRouting list` — for the dedup/plane tests that need a
/// `Routings` list containing a repeated path (the realistic case: a caller concatenated several
/// routed planes, and routing is a pure function of the path so the duplicate carries the same
/// `RoutingResult`).
let routingsWith (routings: PathRouting list) : RouteReport =
    { Routings = routings; Diagnostics = [] }

/// One `PathRouting` from a raw path string and a result.
let routing (rawPath: string) (result: RoutingResult) : PathRouting =
    { Path = normalizePath rawPath; Result = result }
