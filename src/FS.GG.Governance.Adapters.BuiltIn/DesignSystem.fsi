// Curated public signature contract for the design-system domain vocabulary, the
// artifact mapping, the probes, and the kernel wiring (F11 · second concrete adapter).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching DesignSystem.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any DesignSystem.fs body exists (Principle I). The shapes
// mirror docs/governance-design/adapters.md ("Design-system adapter", "Rule catalog and
// tier mapping"). It is domain #2 of Milestone M3 (the adoption bar) and adopts the kernel
// by DIFFERENCE — it shares NONE of the F10 Spec Kit adapter's shape: there is NO Phase, NO
// whenPhase phase guard, NO merge fence, NO dial (FR-005). It is PURE — values and total
// folds, no I/O, no Model/Msg/Effect, no interpreter (Constitution Principle IV is N/A).
// SENSING a live design system into these facts (reading the token tree, capturing rendered
// output, computing contrast ratios, hashing artifact content) is the F08 effects shell /
// F12 CLI's job, NOT this feature — tests feed facts drawn from a fixture token tree
// directly (FR-015).
//
// This module supplies the design-system domain's OWN vocabulary and the kernel wiring: the
// closed `DesignSystemFact` union (named by `identify`), the `DesignArtifactRef ->
// ArtifactRef` mapping, the declared probe set, and the F04 `Bridge`. It re-implements NONE
// of inference, arbitration, evidence, rendering, hashing, explanation, severity, or
// routing — those are all reused from the kernel through the F09 SPI (FR-003). NO rendering
// vocabulary (token types, colour models, layout concepts) appears in the generic kernel or
// the SPI — all design vocabulary is confined to the closed types BELOW (FR-011). Reuses F09
// `Adapter`/`Lift`, F05 `EvidenceState`/`Evidence`, F04 `CheckRule`/`Bridge`/`JudgeId`, F03
// `Check`/`Probe`/`ArtifactRef`/`Outcome`, F01 `FactId`/`FactSet`; zero new dependencies,
// no rendering library (FR-016).

namespace FS.GG.Governance.Adapters.DesignSystem

open FS.GG.Governance.Kernel

/// The artifact kinds the design-system domain inspects (FR-002, Key Entities
/// `DesignArtifactRef`), mapped onto the kernel's structural `ArtifactRef` by
/// `DesignSystem.toRef`. Plain (NO `RequireQualifiedAccess`) — unlike F10's unions, no case
/// names collide, so callers write `TokenDocument` directly (research D4). This is the
/// single place the design domain's artifact vocabulary lives; it never leaks into the
/// kernel or SPI (FR-011).
type DesignArtifactRef =
    /// The source-of-truth token document (the design language's tokens).
    | TokenDocument
    /// The generated token surface (the public, consumed token API) — the high-stakes
    /// surface the token-surface gate and `tokenSurfaceFence` name.
    | GeneratedTokenSurface
    /// A recorded rendered capture of a control/page (read as data, never produced here).
    | RenderedCapture
    /// An interaction-state specification (hover/focus/active/disabled resolution).
    | InteractionStateSpec
    /// A page-pattern specification (layout/composition intent).
    | PagePatternSpec

/// The design-system domain's closed, OWNED fact vocabulary — the facts the kernel folds
/// over to govern adherence to a design language (FR-001, Key Entities `DesignSystemFact`).
/// Never copied from or shared with the Spec Kit (F10) domain (FR-001). Covers the five
/// FR-001 categories: the selected policy (`PolicySelected`), the domain's design rules
/// (`DesignRule`), deterministic verdicts read from the surface (`SurfaceObservation` +
/// the `MeasurementState`/`VerdictRestsOn` evidence pair), recorded reviews and blockers
/// (`DesignGov`). `MeasurementState` carries one of the FIVE AUTHORED `EvidenceState`s
/// (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped`) — `AutoSynthetic` is COMPUTED by
/// `Evidence.effective`, never supplied (F05). `DesignGov` is the governance embed case the
/// F04 `Bridge` uses (`Embed`/`Project` of the domain-neutral `RuleOutcome`) — kernel
/// wiring, not domain vocabulary (research D2/D5). There is deliberately NO `PhaseReached`
/// fact — a design language is a flat surface with no lifecycle (FR-005, research D3).
type DesignSystemFact =
    /// The selected design policy (e.g. "AntDesign"). The subject of the `HumanOnly`
    /// adopt-a-new-policy rule (FR-001 category 1).
    | PolicySelected of policy: string
    /// A declared design rule / intent of the domain — the unit `intent-coverage` checks is
    /// consumed (FR-001 category 2).
    | DesignRule of ruleId: string
    /// A sensed deterministic observation read from the surface, keyed by `(probe, subject)`:
    /// `met = true` ⇒ the probe is satisfied, `false` ⇒ violated. One parametric case
    /// collapses the boolean deterministic observations (token-match, contrast, spacing,
    /// control-height, intent, visual-state) — research D5 (FR-001 category 3).
    | SurfaceObservation of probe: string * subject: DesignArtifactRef * met: bool
    /// A sensed measurement and its DECLARED `EvidenceState` (one authored state per id) —
    /// the F05 node `evidence-measured` builds its graph from (FR-001 category 3, evidence).
    | MeasurementState of measurementId: string * state: EvidenceState
    /// A deterministic verdict rests on a measurement: `verdictId` rests on `measurementId`
    /// — the F05 dependency edge; the synthetic taint flows down it (research D5/D7).
    | VerdictRestsOn of verdictId: string * measurementId: string
    /// A fixture artifact is present/observed — lets a probe report `Unknown` (absent) as
    /// distinct from `Unmet` (present but failing) (edge cases, research D6).
    | ArtifactPresent of DesignArtifactRef
    /// The F04 governance-outcome embed case (kernel wiring): how a `RuleOutcome` (a recorded
    /// review or a blocker) lives in `DesignSystemFact`. Asserted by the bridge, not by the
    /// sensing layer (research D2). FR-001 categories 4 & 5.
    | DesignGov of RuleOutcome

/// The design-system domain's OWN change shape (FR-014) — the abstract `'change` an F07
/// `Fence` classifies. `Surfaces` is the set of artifact kinds the change touches, for
/// surface-fencing; `tokenSurfaceFence` trips when it touches the public token surface.
/// There is deliberately NO `Phase` field — unlike F10's `SpecKitChange`, this domain has
/// no lifecycle position (FR-005, research D3). A project root narrows its composite change
/// onto this via `Lift.fence` when the design-system adapter is composed with another domain
/// (FR-014).
type DesignChange =
    { Surfaces: Set<DesignArtifactRef> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DesignSystem =

    /// (Component 2) The artifact mapping: every design artifact kind onto the kernel's
    /// structural `ArtifactRef` (a `Kind` + a stable `Key`) (FR-002) — the SINGLE point
    /// where design artifacts meet the otherwise domain-neutral algebra. Total and injective.
    val toRef: artifact: DesignArtifactRef -> ArtifactRef

    /// (M-ADPT-2) Wrap an agent-review check to DECLARE the artifacts the judge reviews, so their sensed
    /// content enters the F04 cache key (`Check.reads` drives its artifact half) and a changed reviewed
    /// artifact re-opens the review instead of reusing a stale verdict. The injected atom always reports
    /// `Met` (the neutral element of `All`), so it changes the check's `reads`/`hash` but NOT its evaluated
    /// verdict, and — still carrying the guarded `Opaque` — the check stays non-reified. Total.
    val reviewing: artifacts: DesignArtifactRef list -> check: Check<DesignSystemFact> -> Check<DesignSystemFact>

    /// (Component 1, identity) The sole authority on `DesignSystemFact` identity the kernel
    /// folds with (F01 `FixedPoint.evaluate`). Keyed by the ENTITY a fact is about —
    /// `PolicySelected` by a fixed key (one selected policy), `MeasurementState` by
    /// `measurementId`, `SurfaceObservation` by `(probe, subject)` — so a later fact for the
    /// same entity supersedes (dedup semantics); value-distinguishing facts (`DesignRule`,
    /// `VerdictRestsOn`, `ArtifactPresent`) key by their full value. Injective on
    /// value-bearing facts (theory Hazard 4); at a composition root the project `Identify`
    /// MUST agree with this on injected `DesignSystemFact`s so provenance ids survive the
    /// lift (F09 law L3). Total.
    val identify: fact: DesignSystemFact -> FactId

    /// The F04 kernel wiring for the design-system domain: `Embed = DesignGov`, `Project` the
    /// inverse, `Judge` the supplied judge identity, and `ArtifactHash` a fixed sentinel
    /// (`""`) — the PURE adapter holds no artifact-content facts; F08 supplies real content
    /// hashes at the edge (FR-015). NOT a re-implementation of any cross-cutting facility
    /// (research D2). `judge` is per-run config (F12 supplies it; tests fix one).
    val bridge: judge: JudgeId -> Bridge<DesignSystemFact>

    // ── (Component 3) The probes: atomic, inspectable predicates the catalog composes. They
    //    READ the supplied fixture facts as data and report a three-valued `Outcome`
    //    (`Met`/`Unmet`/`Unknown`) — a MISSING fixture artifact is `Unknown` (undecided),
    //    never a silent `Met` (FR-010, edge cases, research D6). They NEVER render, capture,
    //    or author (FR-004). Each names the `DesignArtifactRef`s it reads and carries its
    //    subject/policy as `ProbeArg`s, so it renders and hashes. ──

    /// The token-drift probe: the generated token surface matches the source token document.
    /// Reads the `SurfaceObservation ("surface-matches", generated, _)` fact; `Met` when
    /// `met`, `Unmet` when not, `Unknown` when the generated surface is absent. Total.
    val surfaceMatches:
        generated: DesignArtifactRef -> source: DesignArtifactRef -> Check<DesignSystemFact>

    /// The colour/contrast probe: a WCAG / Ant ratio is met on `surface` under `policy`.
    /// Reads the `SurfaceObservation ("contrast-meets", surface, _)` fact; `policy` is a
    /// `LiteralArg` so the rendered statement and hash distinguish policies. `Unknown` when
    /// the surface fixture is absent (a missing contrast fixture is never a silent `Met`,
    /// edge case). Total.
    val contrastMeets:
        policy: string -> surface: DesignArtifactRef -> Check<DesignSystemFact>

    /// A generic deterministic surface probe over `SurfaceObservation (name, subject, _)` —
    /// the shared shape behind the spacing-scale, control-height, intent-coverage, and
    /// visual-state checks (each a `surfaceObserved "<name>" subject`). `Met`/`Unmet`/
    /// `Unknown` exactly as `surfaceMatches`. Total.
    val surfaceObserved: name: string -> subject: DesignArtifactRef -> Check<DesignSystemFact>

    /// The evidence-honesty probe (the F05 taint realization): builds the kernel
    /// `EvidenceGraph` from the `MeasurementState` nodes and `VerdictRestsOn` edges via
    /// `Evidence.build`, runs `Evidence.effective`, and reports `Met` iff NO node's effective
    /// state is `Synthetic`/`AutoSynthetic`; `Unmet` (with the offending id) when a verdict
    /// rests — directly or transitively — on a synthetic/unmeasured input; `Unmet` (with the
    /// `GraphError`) when `Evidence.build` fails (a malformed graph is distinguishable from a
    /// real taint, Principle VI). The taint flows by the KERNEL's least fixed point — the
    /// adapter ships NO graph code (research D7). Total.
    val evidenceMeasured: Check<DesignSystemFact>

    /// (Component 3) The declared atomic predicates the rule catalog composes (F03 `Probe`)
    /// — the design-system probe vocabulary, carried for the contract and for testing; the
    /// `Catalog` rules' checks are authoritative for evaluation (research D2). Total.
    val probes: Probe<DesignSystemFact> list
