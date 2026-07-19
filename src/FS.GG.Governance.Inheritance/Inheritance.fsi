// Curated public signature contract for profile-bound gate inheritance (ADR-0049, WI-5).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Inheritance.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// the embedded reference-floor table and the `TypedFacts` skeleton helper live ONLY in the .fs and
// are hidden by their ABSENCE here (the Enforcement.fs hidden-helper precedent).
//
// Design-first artifact: drafted and exercised in FSI before any .fs body exists (Principle I). This
// is the module that RETIRES the "policy is purely local" invariant: `TemplateProfile` — recorded on
// a product's `Surface` but until now provenance-only — becomes a LOOKUP KEY into an embedded,
// org-owned reference floor. A product carrying a bound template-profile INHERITS that floor's gates,
// unioned with its own local gates, as a NON-LOWERABLE floor: a product may raise an inherited gate
// but its local `.fsgg/` can never remove or downgrade one.
//
// It is PURE and TOTAL: no I/O, no git, no clock; deterministic (byte-identical for identical input);
// never throws. It computes no verdict — it produces the EFFECTIVE gate set, which the existing
// `Ship.rollup` enforces through the existing `deriveEffectiveSeverity`, both used VERBATIM. Inherited
// gates are single-sourced through the same `Gates.buildRegistry` projection that produces local
// gates, so an inherited gate is indistinguishable in shape from a locally-declared one.
//
// publish-before-flip: WI-5 landed this contract with the `game` profile's gameplay gate at a
// NON-blocking maturity (`warn`), changing no product's ship verdict. WI-8
// (FS-GG/FS.GG.Governance#276) then flipped it to `block-on-ship` once WI-7's reference-game proof
// was green — so a `game` product now inherits the gameplay gate as a NON-LOWERABLE block-on-ship
// floor it cannot delete or downgrade from its own `.fsgg/`.

namespace FS.GG.Governance.Inheritance

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Inheritance =

    /// The org-owned reference floor for one template-profile: the gates every product carrying that
    /// profile inherits. A deterministic, embedded mapping — the single in-code source of profile-bound
    /// gates (ADR-0049 retires the "no embedded/default policy loader" invariant). An unbound or unknown
    /// profile yields the EMPTY list — never an error, never a fabricated gate. The returned gates are
    /// produced through the same `Gates.buildRegistry` `Check -> Gate` projection as local gates, so they
    /// carry identical `GateId`/`Description`/`Timeout` shape. Sorted by `GateId` ordinal.
    val referenceGatesFor: profile: TemplateProfile -> Gate list

    /// The distinct template-profiles a product declares, read off `Capabilities.Surfaces`
    /// (`Surface.TemplateProfile`). Deterministic: deduplicated and sorted by the profile string. A
    /// product that declares no `templateProfile` on any surface yields the EMPTY list.
    val productTemplateProfiles: facts: TypedFacts -> TemplateProfile list

    /// The full set of gates a product inherits: the union of `referenceGatesFor` over every
    /// template-profile the product declares, deduplicated by `GateId` and sorted by `GateId` ordinal.
    /// EMPTY when the product declares no bound profile — the identity input to `composeEffectiveGates`.
    val inheritedGatesFor: facts: TypedFacts -> Gate list

    /// Compose a product's local gate set with an inherited floor, applying the NON-LOWERABLE rule:
    ///   • a gate id present in BOTH  -> the local gate at the HIGHER-ranked maturity (`maturityRank`):
    ///     a local gate may RAISE an inherited floor but never lower it;
    ///   • a gate id only INHERITED   -> added verbatim;
    ///   • a gate id only LOCAL       -> kept unchanged.
    /// The result is deduplicated by `GateId` and sorted by `GateId` ordinal (deterministic). When
    /// `inherited` is EMPTY the result is `local` unchanged (the identity case).
    val composeEffectiveGates: inherited: Gate list -> local: Gate list -> Gate list

    /// Fold a product's inherited floor into an already-routed `RouteResult`, immediately before the
    /// ship rollup. Replaces `SelectedGates` with the composed effective set: each existing selection
    /// trace (`SelectingPaths`) is PRESERVED (with any raised maturity applied to its `Gate`), and an
    /// inherited-only gate is added with an EMPTY trace — it is present because inherited, not because a
    /// changed path selected it. When the product declares no bound template-profile this is the
    /// IDENTITY (the route is returned unchanged, byte-for-byte), mirroring the existing pre-rollup
    /// consume-union fold. Pure and total; the composed set is enforced by the unchanged `Ship.rollup`.
    val applyInheritance: facts: TypedFacts -> route: RouteResult -> RouteResult
