module FS.GG.Governance.Routing.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

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
