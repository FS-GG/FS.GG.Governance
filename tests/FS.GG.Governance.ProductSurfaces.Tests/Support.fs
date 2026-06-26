module FS.GG.Governance.ProductSurfaces.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.ProductSurfaces
open FS.GG.Governance.ProductSurfaces.Model
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

/// The on-disk `.fsgg` parent directory for a named fixture (shared with the Config test suite).
let fixtureDir (name: string) =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.Config.Tests", "fixtures", name)

/// The validated `TypedFacts` for a named fixture, through the real Loader edge. Fails the test if the
/// fixture is not `Valid` (the classify scenarios all use valid catalogs).
let factsOf (name: string) : TypedFacts =
    match Loader.loadAndValidate (fixtureDir name) with
    | Valid f -> f
    | Invalid d -> failwithf "expected Valid for fixture '%s', got: %A" name d

/// The validation result for a named fixture (for the standalone/escape scenarios).
let validateFixture (name: string) = Loader.loadAndValidate (fixtureDir name)

/// Build a real `RouteReport` by routing the given candidate paths against the facts (never mocked).
let routeOf (facts: TypedFacts) (paths: string list) : RouteReport =
    Routing.route facts (paths |> List.map normalizePath)

/// Classify the given candidate paths under a named profile against a fixture's real facts.
let classifyPaths (name: string) (profile: string) (paths: string list) : ProductSurfaceReport =
    let facts = factsOf name
    let report = routeOf facts paths
    ProductSurfaces.classify facts report (ProfileId profile)

/// The single classification for a path, or None.
let forPath (report: ProductSurfaceReport) (path: string) : ProductClassification option =
    report.Classifications |> List.tryFind (fun c -> c.Path = normalizePath path)
