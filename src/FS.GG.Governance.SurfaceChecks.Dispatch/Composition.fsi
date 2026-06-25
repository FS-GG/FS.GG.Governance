// The pure F24 dispatcher (Constitution Principle II — visibility lives here). Given the F23
// ProductSurfaceReport + the per-domain sensed facts, it builds one request per applicable classification,
// runs the matching pack's pure `evaluate`, and aggregates ORDER-INDEPENDENTLY and deterministically
// (FR-008, SC-008). It lives in its OWN project to break the core <-> domains reference cycle (data-model
// §1.1). PURE and TOTAL: no I/O, no clock.

namespace FS.GG.Governance.SurfaceChecks.Dispatch

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.SurfaceChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Composition =

    /// The host fills only the domains whose surfaces were declared/routed (FR-015). A surface absent from a
    /// map ⇒ no facts sensed for it ⇒ that domain contributes nothing for that surface.
    type DomainFactBundle =
        { Package: Map<SurfaceId, FS.GG.Governance.PackageChecks.Model.PackageFacts>
          Docs: Map<SurfaceId, FS.GG.Governance.DocsChecks.Model.DocsFacts>
          Skill: Map<SurfaceId, FS.GG.Governance.SkillChecks.Model.SkillFacts>
          Design: Map<SurfaceId, FS.GG.Governance.DesignChecks.Model.DesignFacts> }

    /// An empty bundle (every map empty) — the host's starting point before any sensor runs.
    val emptyBundle: DomainFactBundle

    /// Map an F23 SurfaceClass to the F24 domain pack, when one exists. Non-product / boundary classes
    /// (Routine/GovernedRoot/ProtectedSurface/GeneratedView/ReleaseSurface/SampleAppSurface/
    /// GeneratedProductRoot) ⇒ None (no pack, no finding — FR-013/FR-015). EXHAUSTIVE: a future SurfaceClass
    /// is a compile error here, never a silent remap.
    val domainOf: cls: SurfaceClass -> Model.CheckDomain option

    /// Derive the per-surface requests from the F23 report (one per applicable classification). The request's
    /// `EvidenceTag` is looked up from `facts.Capabilities.Surfaces` by surface id (the declared tag, or None).
    val requestsOf: facts: TypedFacts -> report: ProductSurfaceReport -> Model.SurfaceCheckRequest list

    /// PURE and TOTAL: run every applicable pack, aggregate, sort by (surface id, domain ordinal, file,
    /// detail, code). No I/O, no clock. Empty report or empty bundle ⇒ empty list (valid success). The result
    /// is identical regardless of the order of `report.Classifications` or of the bundle maps (SC-008).
    val run:
        facts: TypedFacts ->
        report: ProductSurfaceReport ->
        bundle: DomainFactBundle ->
            Model.SurfaceFinding list
