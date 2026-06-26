module FS.GG.Governance.CurrencyEnforcement.Tests.Support

open System.IO
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel

let repoRoot =
    let rec find (dir: string) =
        if File.Exists(Path.Combine(dir, "FS.GG.Governance.sln")) then
            dir
        else
            match Directory.GetParent dir with
            | null -> dir
            | p -> find p.FullName

    find (Directory.GetCurrentDirectory())

/// A declared generated-view manifest entry (a route projection, by default).
let entry (viewId: string) : GenerationEntry =
    { ViewId = viewId
      Kind = RouteProjection
      OutputPath = "docs/" + viewId + ".generated.json"
      Sources = [ ".fsgg/route.yml" ]
      Generator = [ "fsgg"; "route"; "--json" ]
      GeneratorBasis = "tool-version" }

let art (s: string) : ArtifactHash = ArtifactHash s
let ver (s: string) : GeneratorVersion = GeneratorVersion s

/// One resolved view decision for the findings-gate / no-hide tests.
let decision (viewId: string) (status: CurrencyStatus) (drifted: InputCategory list) : ViewDecision =
    { Entry = entry viewId
      Status = status
      Drifted = drifted }
