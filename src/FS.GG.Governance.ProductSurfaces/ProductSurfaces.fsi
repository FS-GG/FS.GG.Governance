// Curated public signature contract for the product-surface classification + cost-tier selection
// operation (F23).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching ProductSurfaces.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any ProductSurfaces.fs
// body exists (Principle I). `classify` is PURE and TOTAL (FR-009): defined for every input, never
// throwing, reading no clock/filesystem/git/environment/network, and byte-for-byte identical for
// identical input regardless of evaluation time, machine, process, or input order. It renders NO JSON and
// adds NO CLI; its sole output is the `ProductSurfaceReport` value. It re-parses no YAML, re-routes
// nothing, and senses no git — the F014 facts and the F015 route report are consumed verbatim
// (contracts/classification.md).

namespace FS.GG.Governance.ProductSurfaces

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.ProductSurfaces.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ProductSurfaces =

    /// Classify each routed path that falls under a declared product surface, selecting its cost tier.
    /// PURE and TOTAL (FR-009).
    ///
    /// Inputs (all already typed upstream — nothing recomputed here):
    ///   • `facts`   — the F014 `TypedFacts`: the declared `Capabilities.Surfaces` (membership +
    ///                 classification), `Capabilities.Checks` (the declared cost tiers to snap to), and
    ///                 `Policy.Profiles` (which the active profile must name to escalate).
    ///   • `report`  — the F015 `RouteReport`: each `Routed` path carries the routed capability domain.
    ///   • `profile` — the active `ProfileId` (the edge passes a declared profile); a release-oriented or
    ///                 strict profile escalates the target tier on a positive match only.
    ///
    /// Per routed path, in normalized-path order (contracts/classification.md):
    ///   1. Membership — find every declared `Surface` whose `Paths` cover the routed path, by the same
    ///      segment-prefix relation routing/findings use, with glob `Paths` matched via `Glob.matches`.
    ///   2. No covering product surface (or only a boundary-only kind: `ProtectedSurface`/`GovernedRoot`/
    ///      `Routine`) ⇒ NO entry (light-by-default, FR-004). A non-`Routed` path produces no entry.
    ///   3. Precedence (FR-008, D6) — on multi-match, the winner is the highest-precedence `SurfaceClass`
    ///      (the documented total order), ties within a kind broken by ordinal-first `SurfaceId`; the
    ///      `Reason` records which rule applied. Order is data — independent of declaration order (SC-005).
    ///   4. Tier (FR-006, D7) — a cheap-by-default baseline per winning kind, raised by a release-oriented
    ///      profile (`ReleaseSurface` → `ReleaseValidation`) or a strict profile (+1 rank) on a positive
    ///      match only, then snapped to the deepest declared tier ≤ the target (else the cheapest declared;
    ///      else the target with `TierIsDeclared = false`, the F24-pending non-error note, FR-016).
    ///   5. Cheaper-local alternative (FR-007) — `CheaperLocalTier t` when a strictly-cheaper,
    ///      locally-runnable declared tier exists for the domain (the cheapest such), else
    ///      `NoCheaperLocalTier`. Always present.
    ///   6. Explanation — a deterministic string naming the matched capability, classification, selected
    ///      tier, and the cheaper-local alternative.
    ///
    /// Determinism (SC-005): `Classifications` is sorted by normalized `Path` (ordinal) then `SurfaceId`
    /// token; re-ordering the authored surfaces/checks or the input paths does not change the report. An
    /// empty report is a valid, successful outcome (FR-004).
    val classify: facts: TypedFacts -> report: RouteReport -> profile: ProfileId -> ProductSurfaceReport
