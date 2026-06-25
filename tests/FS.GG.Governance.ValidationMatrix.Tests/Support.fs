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

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
