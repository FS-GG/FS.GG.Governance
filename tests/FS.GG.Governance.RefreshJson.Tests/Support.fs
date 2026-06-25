module FS.GG.Governance.RefreshJson.Tests.Support

open System
open System.IO
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel

// REAL hand-built `RefreshDecision` values (Principle V — literal typed values, no mock). A mixed fixture
// covering every per-view status, plus an all-current one, drive the shape/determinism/golden tests.

let private entry (id: string) (kind: ViewKind) (output: string) : GenerationEntry =
    { ViewId = id
      Kind = kind
      OutputPath = output
      Sources = [ output + ".src" ]
      Generator = [ "gen"; id ]
      GeneratorBasis = "v1" }

/// A mixed decision in declared order: regenerated, current, stale-unresolved, not-evaluated.
let decisionMixed: RefreshDecision =
    { Outcome = StaleUnresolved'
      DryRun = false
      Views =
        [ { Entry = entry "gate-metadata" GateMetadata "docs/gates.json"
            Status = Regenerated [ CoveredArtifactsCat ]
            Drifted = [ CoveredArtifactsCat ] }
          { Entry = entry "rule-catalog" RuleCatalog "docs/rules.json"
            Status = Current
            Drifted = [] }
          { Entry = entry "api-surface" ApiSurfaceDoc "surface/x.txt"
            Status = StaleUnresolved "src/x: source not found"
            Drifted = [] }
          { Entry = entry "extra" (Other "custom-kind") "out/extra.txt"
            Status = NotEvaluated
            Drifted = [] } ]
      RegeneratedCount = 1
      CurrentCount = 1
      UnresolvedCount = 1
      NotEvaluatedCount = 1 }

/// An all-current decision (the clean shade).
let decisionClean: RefreshDecision =
    { Outcome = NothingToRefresh
      DryRun = false
      Views =
        [ { Entry = entry "only" Baseline "out.txt"
            Status = Current
            Drifted = [] } ]
      RegeneratedCount = 0
      CurrentCount = 1
      UnresolvedCount = 0
      NotEvaluatedCount = 0 }

// ── repo root + golden path ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

let goldenPath =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.RefreshJson.Tests", "golden", "refresh.json")
