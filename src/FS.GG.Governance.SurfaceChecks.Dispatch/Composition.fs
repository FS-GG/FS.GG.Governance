// The pure F24 dispatcher. Visibility lives in Composition.fsi (Constitution Principle II); this file
// carries NO top-level access modifiers. PURE and TOTAL (FR-008): no I/O, no clock; order-independent and
// deterministic (SC-008) — the result is sorted by (surface id, domain ordinal, file, detail, code).

namespace FS.GG.Governance.SurfaceChecks.Dispatch

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.SurfaceChecks

module PackagePack = FS.GG.Governance.PackageChecks.PackageChecks
module DocsPack = FS.GG.Governance.DocsChecks.DocsChecks
module SkillPack = FS.GG.Governance.SkillChecks.SkillChecks
module DesignPack = FS.GG.Governance.DesignChecks.DesignChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Composition =

    type DomainFactBundle =
        { Package: Map<SurfaceId, FS.GG.Governance.PackageChecks.Model.PackageFacts>
          Docs: Map<SurfaceId, FS.GG.Governance.DocsChecks.Model.DocsFacts>
          Skill: Map<SurfaceId, FS.GG.Governance.SkillChecks.Model.SkillFacts>
          Design: Map<SurfaceId, FS.GG.Governance.DesignChecks.Model.DesignFacts> }

    let emptyBundle: DomainFactBundle =
        { Package = Map.empty
          Docs = Map.empty
          Skill = Map.empty
          Design = Map.empty }

    let domainOf (cls: SurfaceClass) : Model.CheckDomain option =
        match cls with
        | PackageSurface -> Some Model.PackageDomain
        | DocsSurface -> Some Model.DocsDomain
        | SkillSurface -> Some Model.SkillDomain
        | DesignSurface -> Some Model.DesignDomain
        | Routine
        | GovernedRoot
        | ProtectedSurface
        | GeneratedView
        | ReleaseSurface
        | SampleAppSurface
        | GeneratedProductRoot -> None

    let requestsOf (facts: TypedFacts) (report: ProductSurfaceReport) : Model.SurfaceCheckRequest list =
        report.Classifications
        |> List.choose (fun c ->
            match domainOf c.Class with
            | None -> None
            | Some domain ->
                let tag =
                    facts.Capabilities.Surfaces
                    |> List.tryFind (fun s -> s.Id = c.Surface)
                    |> Option.bind (fun s -> s.EvidenceTag)

                Some
                    { Domain = domain
                      Surface = c.Surface
                      Class = c.Class
                      Path = c.Path
                      EvidenceTag = tag })

    let run
        (facts: TypedFacts)
        (report: ProductSurfaceReport)
        (bundle: DomainFactBundle)
        : Model.SurfaceFinding list =
        requestsOf facts report
        |> List.collect (fun req ->
            match req.Domain with
            | Model.PackageDomain ->
                match Map.tryFind req.Surface bundle.Package with
                | Some f -> PackagePack.evaluate req f
                | None -> []
            | Model.DocsDomain ->
                match Map.tryFind req.Surface bundle.Docs with
                | Some f -> DocsPack.evaluate req f
                | None -> []
            | Model.SkillDomain ->
                match Map.tryFind req.Surface bundle.Skill with
                | Some f -> SkillPack.evaluate req f
                | None -> []
            | Model.DesignDomain ->
                match Map.tryFind req.Surface bundle.Design with
                | Some f -> DesignPack.evaluate req f
                | None -> [])
        |> List.sortBy (fun (f: Model.SurfaceFinding) ->
            let (SurfaceId sid) = f.Surface
            let (GovernedPath file) = f.Location.File
            sid, Model.checkDomainOrdinal f.Domain, file, f.Location.Detail, f.Code)
