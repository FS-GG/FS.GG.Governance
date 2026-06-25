module FS.GG.Governance.PackageChecks.Tests.Support

open System.IO
open FS.GG.Governance.Config.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

/// Walk up from the test working directory to the repo root (the directory holding the .sln).
let repoRoot =
    let rec find (dir: string) =
        if File.Exists(Path.Combine(dir, "FS.GG.Governance.sln")) then
            dir
        else
            match Directory.GetParent dir with
            | null -> dir
            | p -> find p.FullName

    find (Directory.GetCurrentDirectory())

/// Build a package-domain request for a surface id + path (+ optional declared evidence tag).
let requestFor (surfaceId: string) (path: string) (tag: string option) : SC.SurfaceCheckRequest =
    { Domain = SC.PackageDomain
      Surface = SurfaceId surfaceId
      Class = PackageSurface
      Path = normalizePath path
      EvidenceTag = tag |> Option.map EvidenceTag }
