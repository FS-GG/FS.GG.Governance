// Curated public signature contract for the product-surface classification types (F23).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, YAML-free values `ProductSurfaces.classify`
// returns. They REUSE the F014 catalog vocabulary verbatim — opened from `FS.GG.Governance.Config.Model`
// (`SurfaceClass`/`GeneratedProductTier`/`DomainId`/`SurfaceId`/`GovernedPath`) — never redefined
// (FR-014). The only new shapes are the classification: the `TierAlternative` DU (modeled on
// RouteExplain's `AlternativeOutcome` — always present, never option/null), the `ClassificationReason`
// DU explaining a multi-match win, the per-path `ProductClassification` record, and the
// `ProductSurfaceReport` that wraps the deterministically-ordered classifications. Every emitted
// collection is in deterministic order (FR-012); no field carries raw YAML, host paths, timestamps, or
// product vocabulary beyond declared ids.

namespace FS.GG.Governance.ProductSurfaces

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The cheaper-local-alternative outcome for a selected tier (FR-007) — modeled on
    /// `RouteExplain.AlternativeOutcome`: ALWAYS present, never `option`/null and never a third
    /// "absent/unknown" state. `CheaperLocalTier t`: `t` is a strictly-cheaper, locally-runnable declared
    /// tier (a declared tiered check in the winning capability's domain with `Environment ∈ {Local;
    /// LocalOrCi}` and `Tier < SelectedTier`); the cheapest such tier, so it names the quickest check to
    /// run locally first. `NoCheaperLocalTier`: the explicit "none" — emitted when no declared tier
    /// qualifies; never omitted, never null.
    type TierAlternative =
        | CheaperLocalTier of GeneratedProductTier
        | NoCheaperLocalTier

    /// Why this surface won when a path fell under one or more declared surfaces (D6) — deterministic and
    /// order-independent:
    ///   • `OnlySurface`            — exactly one declared surface covered the path.
    ///   • `HighestPrecedenceKind`  — more than one covered it and the winner's `SurfaceClass` was the
    ///                                unique highest-precedence kind (the documented total order).
    ///   • `OrdinalSurfaceTiebreak` — more than one of the highest-precedence kind covered it; the
    ///                                ordinal-first `SurfaceId` won.
    type ClassificationReason =
        | OnlySurface
        | HighestPrecedenceKind
        | OrdinalSurfaceTiebreak

    /// One routed path classified to a declared product surface with its selected cost tier
    /// (FR-002/FR-006/FR-007). `Capability` is the F015 routed domain the path reached; `Surface`/`Class`
    /// are the winning declared surface and its classification; `SelectedTier` is the snapped cost tier;
    /// `TierIsDeclared` is `false` when no tiered check is declared for the domain (the F24-pending
    /// non-error note, FR-016); `Alternative` is the cheaper-local outcome; `Reason` is the precedence
    /// explanation; `Explanation` names the capability, class, selected tier, and (when known) the cheaper
    /// local alternative. No raw YAML, host path, or timestamp — only declared ids.
    type ProductClassification =
        { Path: GovernedPath
          Capability: DomainId
          Surface: SurfaceId
          Class: SurfaceClass
          SelectedTier: GeneratedProductTier
          TierIsDeclared: bool
          Alternative: TierAlternative
          Reason: ClassificationReason
          Explanation: string }

    /// The deterministic aggregate (FR-008): one entry per routed path that fell under a declared product
    /// surface, sorted by normalized `Path` (ordinal) then `SurfaceId` token. A routed path under no
    /// declared product surface (or under only a boundary-only kind) produces NO entry (light-by-default,
    /// FR-004). An EMPTY report is a valid, successful outcome — never an error and never a fabricated
    /// classification.
    type ProductSurfaceReport = { Classifications: ProductClassification list }

    // ── Stable rendering helpers (for the explanation, tests, and the route.json projection) ──

    /// The stable wire token for a `ClassificationReason`
    /// (e.g. `HighestPrecedenceKind` → `"highestPrecedenceKind"`). Total and deterministic.
    val classificationReasonToken: reason: ClassificationReason -> string
