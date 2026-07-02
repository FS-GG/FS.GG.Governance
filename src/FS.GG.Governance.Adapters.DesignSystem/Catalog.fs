namespace FS.GG.Governance.Adapters.DesignSystem

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi

// The tiered rule catalog, the single high-stakes surface fence, and the assembled adapter
// (plan module 2). Adherence to a design language expressed as a CATALOG of reified
// `CheckRule`s — each declaring WHO decides (`CheckTier`) and WHETHER it blocks (`Severity`),
// each rendering to a sentence and explaining itself through the kernel's interpreters
// (FR-006). It adds NO evaluation/arbitration/evidence/routing code — those are the kernel's,
// reached through the F09 SPI (FR-003). PURE; Principle IV N/A. Unlike the F10 Spec Kit
// adapter there is NO ConstitutionDial, NO merge fence, NO phase machinery — the blocking set
// is fixed by FR-009 and the only fence is over the public token SURFACE (research D8). It
// references the SPI and the DesignSystem module only — NEVER the F10 adapter (FR-005/FR-016).
// The .fs carries NO visibility modifiers on top-level bindings; Catalog.fsi is the sole
// declaration (Principle II) — helpers not named there are module-private by the signature.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Catalog =

    // ── Rule construction helper (module-private; not in Catalog.fsi) ──

    let mkRule
        (id: string)
        (tier: CheckTier)
        (spec: SpecSource)
        (check: Check<DesignSystemFact>)
        : CheckRule<DesignSystemFact> =
        match CheckRule.rule (RuleId id) tier spec check with
        | Ok r -> r
        | Error e -> failwithf "catalog rule %s rejected: %A" id e

    /// An `Opaque` judgement check — un-inspectable, so `isReified` is false and the F04
    /// guardrail forces it out of the `Deterministic` tier (FR-008). It never resolves to a
    /// definite verdict: its `eval` is always `Unknown`, so a person/agent must decide (C4).
    let judgement (name: string) : Check<DesignSystemFact> =
        Opaque(name, fun _ -> Unknown "requires judgement")

    // ── Deterministic, BLOCKING (FR-007/FR-009) — reified, contract-bearing, definite
    //    verdicts. The few that block: token currency, contrast, the public token surface,
    //    and evidence honesty. ──

    let tokenDrift: CheckRule<DesignSystemFact> =
        mkRule
            "token-drift"
            Deterministic
            { Document = "design-system"; Section = "token-currency" }
            (DesignSystem.surfaceMatches GeneratedTokenSurface TokenDocument)
        |> CheckRule.blocking

    let contrastPolicy: CheckRule<DesignSystemFact> =
        mkRule
            "contrast-policy"
            Deterministic
            { Document = "design-system"; Section = "contrast" }
            (DesignSystem.contrastMeets "ant-aa" GeneratedTokenSurface)
        |> CheckRule.blocking

    let tokenSurfaceGate: CheckRule<DesignSystemFact> =
        mkRule
            "token-surface-gate"
            Deterministic
            { Document = "design-system"; Section = "token-surface" }
            (DesignSystem.surfaceObserved "token-surface-gate" GeneratedTokenSurface)
        |> CheckRule.blocking

    let evidenceMeasured: CheckRule<DesignSystemFact> =
        mkRule
            "evidence-measured"
            Deterministic
            { Document = "design-system"; Section = "evidence" }
            DesignSystem.evidenceMeasured
        |> CheckRule.blocking

    // ── Deterministic, ADVISORY (FR-007) — reified, definite verdicts, but they only
    //    report; the default posture is advisory (FR-009). ──

    let spacingScale: CheckRule<DesignSystemFact> =
        mkRule
            "spacing-scale"
            Deterministic
            { Document = "design-system"; Section = "spacing" }
            (DesignSystem.surfaceObserved "spacing-scale" GeneratedTokenSurface)

    let controlHeightDefaults: CheckRule<DesignSystemFact> =
        mkRule
            "control-height"
            Deterministic
            { Document = "design-system"; Section = "control-height" }
            (DesignSystem.surfaceObserved "control-height" GeneratedTokenSurface)

    let intentCoverage: CheckRule<DesignSystemFact> =
        mkRule
            "intent-coverage"
            Deterministic
            { Document = "design-system"; Section = "intent" }
            (DesignSystem.surfaceObserved "intent-coverage" GeneratedTokenSurface)

    let visualStateResolution: CheckRule<DesignSystemFact> =
        mkRule
            "visual-state"
            Deterministic
            { Document = "design-system"; Section = "visual-state" }
            (DesignSystem.surfaceObserved "visual-state" InteractionStateSpec)

    // ── AgentReviewed, ADVISORY (FR-007/FR-008) — `Opaque`, so they can NEVER be
    //    `Deterministic`; they REPORT via their `Question` (the agent's prompt) and never
    //    resolve to a deterministic `Pass`/`Fail`. ──
    //
    // M-ADPT-2: each judgement DECLARES the artifacts its judge inspects via `DesignSystem.reviewing`, so a
    // changed reviewed artifact re-opens the review instead of reusing a stale verdict (`Check.reads` feeds
    // the F04 cache key's artifact half). `RenderedCapture` (`rendered-capture.json`) is the common subject —
    // every visual-quality judgement is made against the rendered output — with the relevant spec added for
    // the structural checks (intent → `InteractionStateSpec`, page pattern → `PagePatternSpec`). The design
    // language is a flat surface with no dedicated artifact for motion/colour/elevation, so those review the
    // rendered capture alone (a defensible mapping until per-quality artifacts exist — see review #50).

    let renderedMatchesIntent: CheckRule<DesignSystemFact> =
        mkRule
            "rendered-matches-intent"
            AgentReviewed
            { Document = "design-system"; Section = "rendered-intent" }
            (DesignSystem.reviewing [ RenderedCapture; InteractionStateSpec ] (judgement "rendered-matches-intent"))
        |> CheckRule.asking "Does the rendered control match the spec intent? List divergences."

    let fourValues: CheckRule<DesignSystemFact> =
        mkRule
            "four-values"
            AgentReviewed
            { Document = "design-system"; Section = "four-values" }
            (DesignSystem.reviewing [ RenderedCapture ] (judgement "four-values"))
        |> CheckRule.asking "Are the four values — natural/certain/meaningful/growing — honoured?"

    let pagePatternCorrect: CheckRule<DesignSystemFact> =
        mkRule
            "page-pattern"
            AgentReviewed
            { Document = "design-system"; Section = "page-pattern" }
            (DesignSystem.reviewing [ PagePatternSpec; RenderedCapture ] (judgement "page-pattern"))
        |> CheckRule.asking "Is the page pattern correct for this composition?"

    let colourInformational: CheckRule<DesignSystemFact> =
        mkRule
            "colour-informational"
            AgentReviewed
            { Document = "design-system"; Section = "colour" }
            (DesignSystem.reviewing [ RenderedCapture ] (judgement "colour-informational"))
        |> CheckRule.asking "Does colour carry information here, or is it decoration?"

    let motionRestraint: CheckRule<DesignSystemFact> =
        mkRule
            "motion-restraint"
            AgentReviewed
            { Document = "design-system"; Section = "motion" }
            (DesignSystem.reviewing [ RenderedCapture ] (judgement "motion-restraint"))
        |> CheckRule.asking "Is motion used with restraint?"

    let elevationLayering: CheckRule<DesignSystemFact> =
        mkRule
            "elevation-layering"
            AgentReviewed
            { Document = "design-system"; Section = "elevation" }
            (DesignSystem.reviewing [ RenderedCapture ] (judgement "elevation-layering"))
        |> CheckRule.asking "Is elevation/overlay layering correct?"

    // ── HumanOnly, BLOCKING (FR-007) — a person decides; `toRule` escalates and never
    //    resolves to a deterministic verdict. ──

    let adoptNewPolicy: CheckRule<DesignSystemFact> =
        mkRule
            "adopt-new-policy"
            HumanOnly
            { Document = "design-system"; Section = "policy-adoption" }
            (judgement "adopt-new-policy")
        |> CheckRule.blocking

    let catalog: CheckRule<DesignSystemFact> list =
        [ tokenDrift
          contrastPolicy
          tokenSurfaceGate
          evidenceMeasured
          spacingScale
          controlHeightDefaults
          intentCoverage
          visualStateResolution
          renderedMatchesIntent
          fourValues
          pagePatternCorrect
          colourInformational
          motionRestraint
          elevationLayering
          adoptNewPolicy ]

    let tokenSurfaceFence: Fence<DesignChange> =
        { Name = "token-surface"
          Trips = fun c -> c.Surfaces.Contains GeneratedTokenSurface }

    let fences: Fence<DesignChange> list = [ tokenSurfaceFence ]

    let adapter (judge: JudgeId) : Adapter<DesignSystemFact, DesignArtifactRef, DesignChange> =
        { Identify = DesignSystem.identify
          ToRef = DesignSystem.toRef
          Probes = DesignSystem.probes
          Rules = catalog
          Fences = fences
          Bridge = DesignSystem.bridge judge }
