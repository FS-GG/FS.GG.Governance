module FS.GG.Governance.DocsChecks.Tests.Support

open System.IO
open FS.GG.Governance.Config.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

let repoRoot =
    let rec find (dir: string) =
        if File.Exists(Path.Combine(dir, "FS.GG.Governance.sln")) then
            dir
        else
            match Directory.GetParent dir with
            | null -> dir
            | p -> find p.FullName

    find (Directory.GetCurrentDirectory())

let requestFor (surfaceId: string) (path: string) (tag: string option) : SC.SurfaceCheckRequest =
    { Domain = SC.DocsDomain
      Surface = SurfaceId surfaceId
      Class = DocsSurface
      Path = normalizePath path
      EvidenceTag = tag |> Option.map EvidenceTag }
