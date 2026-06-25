module FS.GG.Governance.SurfaceChecks.Tests.Support

open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.SurfaceChecks.Dispatch

module SC = FS.GG.Governance.SurfaceChecks.Model
module Pkg = FS.GG.Governance.PackageChecks.Model
module Docs = FS.GG.Governance.DocsChecks.Model
module Skill = FS.GG.Governance.SkillChecks.Model
module Design = FS.GG.Governance.DesignChecks.Model

let repoRoot =
    let rec find (dir: string) =
        if File.Exists(Path.Combine(dir, "FS.GG.Governance.sln")) then
            dir
        else
            match Directory.GetParent dir with
            | null -> dir
            | p -> find p.FullName

    find (Directory.GetCurrentDirectory())

/// A declared surface with an optional evidence tag.
let surface (id: string) (cls: SurfaceClass) (tag: string option) : Surface =
    { Id = SurfaceId id
      Class = cls
      Paths = [ normalizePath (id + "/path") ]
      Owner = Owner "team"
      Maturity = Warn
      EvidenceTag = tag |> Option.map EvidenceTag
      TemplateProfile = None
      Baseline = None }

let typedFacts (surfaces: Surface list) : TypedFacts =
    { Project =
        { SchemaVersion = SchemaVersion 2
          Id = ProjectId "p"
          Domains = []
          GovernedRoot = normalizePath "."
          PackageSurfaces = []
          PolicyRef = None
          CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 2
          Domains = []
          PathMap = []
          Surfaces = surfaces
          Checks = [] }
      Tooling = None }

let requestForDocs (surfaceId: string) (path: string) : SC.SurfaceCheckRequest =
    { Domain = SC.DocsDomain
      Surface = SurfaceId surfaceId
      Class = DocsSurface
      Path = normalizePath path
      EvidenceTag = Some(EvidenceTag (surfaceId + "-tag")) }

let requestForSkill (surfaceId: string) (path: string) : SC.SurfaceCheckRequest =
    { Domain = SC.SkillDomain
      Surface = SurfaceId surfaceId
      Class = SkillSurface
      Path = normalizePath path
      EvidenceTag = None }

let classification (path: string) (surfaceId: string) (cls: SurfaceClass) : ProductClassification =
    { Path = normalizePath path
      Capability = DomainId "cap"
      Surface = SurfaceId surfaceId
      Class = cls
      SelectedTier = StructuralScan
      TierIsDeclared = false
      Alternative = NoCheaperLocalTier
      Reason = OnlySurface
      Explanation = "" }

let report (classifications: ProductClassification list) : ProductSurfaceReport =
    { Classifications = classifications }

// ── Fact builders that produce exactly one finding per domain ──

let packageDriftFacts (path: string) : Pkg.PackageFacts =
    { BaselineSource = normalizePath path
      Baseline = Pkg.BaselineDrift([ "val added" ], [])
      Transcripts = [] }

let docsDanglingFacts (path: string) : Docs.DocsFacts =
    { Sources = [ normalizePath path ]
      Links = [ { Source = normalizePath path; LinkText = "x"; Target = "missing"; Outcome = Docs.LinkDangling "missing" } ]
      References = []
      Examples = []
      Unreadable = [] }

let skillBrokenFacts (id: string) : Skill.SkillFacts =
    { SkillId = id
      PathContract = [ { Claimed = "ghost"; Outcome = Skill.PathUnresolved "ghost" } ]
      TaskList = Skill.TaskListConsistent
      Mirror = Skill.NoMirrorDeclared
      Unreadable = [] }
