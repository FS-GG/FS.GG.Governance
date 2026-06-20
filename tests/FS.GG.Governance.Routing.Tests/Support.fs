module FS.GG.Governance.Routing.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model

// Fixture builders that assemble REAL inputs of the exact types `Routing.route` consumes
// (TypedFacts / GovernedPath / DomainId) — not synthetic mocks (Principle V). No I/O, no YAML.

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

/// A capability domain id.
let dom (s: string) = DomainId s

/// Build a `PathMapEntry list` from `(glob, domain)` pairs.
let pathMap (pairs: (string * string) list) : PathMapEntry list =
    pairs
    |> List.map (fun (g, d) -> { Glob = GovernedPath g; Capability = DomainId d })

/// Assemble a minimal valid `TypedFacts` with the given governed root and path map. The
/// declared domains are exactly those referenced by the path map; surfaces/checks are empty;
/// the optional policy/tooling files are absent.
let facts (root: string) (pairs: (string * string) list) : TypedFacts =
    let entries = pathMap pairs
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
          Surfaces = []
          Checks = [] }
      Tooling = None }
