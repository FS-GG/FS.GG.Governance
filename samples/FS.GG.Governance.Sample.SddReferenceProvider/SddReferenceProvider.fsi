// Curated public signature contract for the SDD reference template provider (072).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching SddReferenceProvider.fs carries NO `private`/`internal`/`public` modifiers on top-level
// bindings — visibility is presence/absence here. This is the ONE new public binding the feature adds;
// the generic-core baselines stay byte-identical (SC-006).
//
// Design-first artifact: drafted and exercised in FSI (scripts/sdd-reference.fsx) before this body's
// .fs existed (Principle I). `provider` is a CONCRETE, conforming instance of the 071
// `Model.TemplateProvider` contract — plain data: a record value whose `Emit` is PURE and deterministic
// (no clock/guid/env, never throws) and DESCRIBES a minimal but buildable F#/.NET runtime skeleton whose
// dependency closure is FSharp.Core only (research D2/D6, contract R1). The sample hardcodes its OWN
// provider id, package/manifest, and layout — exactly the provider-specific knowledge the generic seam
// must NOT carry (FR-002).

namespace FS.GG.Governance.Sample.SddReferenceProvider

open FS.GG.Governance.Scaffold

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SddReferenceProvider =

    /// The stable id the seam records but never interprets (071 FR-003) — `ProviderId
    /// "fsgg.sample.sdd-reference"`.
    val providerId: Model.ProviderId

    /// The resolved, selectable reference provider. Declares `ContractVersion { Major = 1; Minor = 0 }`
    /// (the seam's supported range) and a PURE `Emit` that returns the fixed, buildable runtime skeleton
    /// derived from the request target's leaf name (contract R1/R2, data-model §1/§2).
    val provider: Model.TemplateProvider
