module FS.GG.Governance.ValidationMatrix.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.ValidationMatrix.Model

// Shared REAL builders for the F26 ValidationMatrix tests. CostBudget is the F25 ordered-Cost ceiling value.

let exhaustiveMatrix: ExhaustiveMatrix =
    { Name = "pack-all-targets"
      Cost = Exhaustive
      Dimensions = [ "packableProjects"; "targetFrameworks" ] }

let cheapMatrix: ExhaustiveMatrix =
    { Name = "smoke"; Cost = Cheap; Dimensions = [ "one" ] }

/// An inner-loop budget whose ceiling is below Exhaustive.
let innerLoopBudget: CostBudget = { Ceiling = Medium }

/// A scheduled/release budget admitting the full Exhaustive matrix.
let releaseBudget: CostBudget = { Ceiling = Exhaustive }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
