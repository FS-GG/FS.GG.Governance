module FS.GG.Governance.Snapshot.Tests.RoutingFeedTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Tests.Support

// SC-001 (the whole point of the feature): the snapshot's changed paths ARE routing's input. The
// `Changed` set is fed straight into `Routing.route` with NO re-normalization step in between — the
// proof that the snapshot's GovernedPath form is byte-identical to what F015 consumes.

/// A minimal valid TypedFacts (governed root "src" + a small overlapping path map).
let private facts: TypedFacts =
    let entries =
        [ { Glob = GovernedPath "src/**"; Capability = DomainId "core" }
          { Glob = GovernedPath "src/Adapters/**"; Capability = DomainId "adapters" } ]

    let domains = entries |> List.map (fun e -> e.Capability) |> List.distinct

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
          PathMap = entries
          Surfaces = []
          Checks = [] }
      Tooling = None }

let private resultFor path (report: RouteReport) =
    (report.Routings |> List.find (fun r -> r.Path = path)).Result

[<Tests>]
let tests =
    testList
        "RoutingFeed"
        [ test "snapshot.Changed feeds Routing.route unchanged; out-of-root paths are carried, not dropped (SC-001, FR-002)" {
              // A committed diff: two in-root paths (one in a more-specific subtree) and one path
              // OUTSIDE the governed root — assembled into the snapshot's GovernedPath form.
              let snap =
                  Snapshot.assemble
                      { baseRaw with
                          DiffRaw =
                              Ok(
                                  znul [ "A"; "src/Kernel/Eval.fs" ]
                                  + znul [ "M"; "src/Adapters/SpecKit.fs" ]
                                  + znul [ "A"; "docs/intro.md" ]
                              ) }

              // FR-002: the out-of-root path is REPRESENTED in the snapshot (not dropped/decided here).
              Expect.contains (snap.Changed |> List.map (fun c -> c.Path)) (GovernedPath "docs/intro.md") "out-of-root path carried"

              // THE feed-through: the changed paths go straight into route — no normalization between.
              let candidatePaths = snap.Changed |> List.map (fun c -> c.Path)
              let report = Routing.route facts candidatePaths

              match resultFor (GovernedPath "src/Kernel/Eval.fs") report with
              | Routed(DomainId d, _, _) -> Expect.equal d "core" "in-root path routes to core"
              | other -> failtestf "expected Routed core, got %A" other

              match resultFor (GovernedPath "src/Adapters/SpecKit.fs") report with
              | Routed(DomainId d, _, _) -> Expect.equal d "adapters" "more-specific subtree wins → adapters"
              | other -> failtestf "expected Routed adapters, got %A" other

              Expect.equal (resultFor (GovernedPath "docs/intro.md") report) OutOfScope "out-of-root ⇒ OutOfScope"
          } ]
