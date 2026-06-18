// Curated public signature contract for the design-system tiered rule catalog, the
// high-stakes fence, and the assembled adapter (F11).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Catalog.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Catalog.fs body exists (Principle I). The shapes
// mirror docs/governance-design/adapters.md ("Rule catalog and tier mapping" — the table
// of fourteen rules with their tiers and severities, plus the always-blocking
// `evidenceMeasured` evidence-honesty rule: fifteen in all). PURE — Principle IV is N/A.
//
// This module expresses adherence to a design language as a CATALOG of reified
// `CheckRule`s — each declaring WHO decides (`CheckTier`) and WHETHER it blocks (`Severity`),
// each rendering to a sentence and explaining itself through the kernel's interpreters
// (FR-006). The deterministic, contract-bearing token/contrast/surface checks (and the
// always-blocking `evidenceMeasured`) are the few that BLOCK; the visual-judgement rules use
// the `Opaque` hatch (forced out of the `Deterministic` tier, routed to an agent whose prompt
// is the rule's `Question`); adopting a new policy is `HumanOnly`. It assembles the single
// `Adapter` value the kernel governs a design language with. It adds NO evaluation/
// arbitration/evidence/routing code — those are the kernel's, reached through the F09 SPI
// (FR-003, SC-001). Unlike the F10 Spec Kit adapter there is NO `ConstitutionDial`, NO merge
// fence, NO phase machinery — the blocking set is fixed by FR-009 and the only fence is over
// the public token SURFACE (research D8). It references the SPI and the DesignSystem module
// only — NEVER the F10 adapter (FR-005/FR-016). Reuses F09 `Adapter`/DesignSystem vocabulary,
// F07 `Fence`, F05 `Evidence`, F04 `CheckRule`/`JudgeId`, F03 `Check`; zero new dependencies.

namespace FS.GG.Governance.Adapters.DesignSystem

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Catalog =

    // ── Deterministic, BLOCKING (FR-007) — reified, contract-bearing, definite verdicts.
    //    The few that block: token currency, contrast, and the public token surface. ──

    /// Token drift: the generated token surface matches the source token document
    /// (`surfaceMatches GeneratedTokenSurface TokenDocument`). `Deterministic`, `Blocking`
    /// (FR-007). A definite `Fail` when the generated surface drifts; `Unknown` (advisory)
    /// when the fixture is absent (edge case). Renders and hashes (SC-004/SC-005).
    val tokenDrift: CheckRule<DesignSystemFact>

    /// Colour / contrast policy: a WCAG / Ant ratio is met on the generated token surface
    /// (`contrastMeets policy GeneratedTokenSurface`). `Deterministic`, `Blocking` (FR-007).
    /// A missing contrast fixture is `Unknown`, never a silent `Pass` (edge case).
    val contrastPolicy: CheckRule<DesignSystemFact>

    /// Token surface gate: the public token surface is the blessed one
    /// (`surfaceObserved "token-surface-gate" GeneratedTokenSurface`). `Deterministic`,
    /// `Blocking` (FR-007/FR-009) — the high-stakes surface `tokenSurfaceFence` names.
    val tokenSurfaceGate: CheckRule<DesignSystemFact>

    /// Evidence honesty (the F05 taint realization): no deterministic verdict rests on a
    /// synthetic / unmeasured input (`DesignSystem.evidenceMeasured`). Runs the kernel's
    /// `Evidence.build` + `Evidence.effective` over the `MeasurementState`/`VerdictRestsOn`
    /// facts: the `AutoSynthetic` taint propagates down the dependency graph by the kernel's
    /// fixed point (NOT adapter code), and the check fails if any verdict's effective state
    /// is `Synthetic`/`AutoSynthetic`. `Deterministic` and `Blocking` — honesty about
    /// evidence is non-negotiable; no flag flips its verdict (research D7, mirrors F10's
    /// evidence gate). Distinguishes a malformed graph (`Unmet` + `GraphError`) from a real
    /// taint (Principle VI).
    val evidenceMeasured: CheckRule<DesignSystemFact>

    // ── Deterministic, ADVISORY (FR-007) — reified, definite verdicts, but they only
    //    report; the default posture is advisory (FR-009). ──

    /// Spacing scale conforms (`surfaceObserved "spacing-scale" GeneratedTokenSurface`).
    /// `Deterministic`, `Advisory` (FR-007).
    val spacingScale: CheckRule<DesignSystemFact>

    /// Control-height defaults conform (`surfaceObserved "control-height" GeneratedTokenSurface`).
    /// `Deterministic`, `Advisory` (FR-007).
    val controlHeightDefaults: CheckRule<DesignSystemFact>

    /// Intent coverage: a declared `DesignRule` intent is consumed
    /// (`surfaceObserved "intent-coverage" GeneratedTokenSurface`). `Deterministic`,
    /// `Advisory` (FR-007).
    val intentCoverage: CheckRule<DesignSystemFact>

    /// Visual-state resolution: interaction states resolve
    /// (`surfaceObserved "visual-state" InteractionStateSpec`). `Deterministic`, `Advisory`
    /// (FR-007).
    val visualStateResolution: CheckRule<DesignSystemFact>

    // ── AgentReviewed, ADVISORY (FR-007/FR-008) — `Opaque`, so they can NEVER be
    //    `Deterministic`; they REPORT via their `Question` (the agent's prompt) and never
    //    resolve to a deterministic `Pass`/`Fail`. ──

    /// "Does the rendered control match the spec intent?" `AgentReviewed`, `Advisory`, over an
    /// `Opaque` check carrying the question as its prompt (FR-007/FR-008).
    val renderedMatchesIntent: CheckRule<DesignSystemFact>

    /// The "four values" (natural / certain / meaningful / growing). `AgentReviewed`,
    /// `Advisory`, `Opaque` (FR-007).
    val fourValues: CheckRule<DesignSystemFact>

    /// Page-pattern correctness. `AgentReviewed`, `Advisory`, `Opaque` (FR-007).
    val pagePatternCorrect: CheckRule<DesignSystemFact>

    /// "Does colour carry information here, or is it decoration?" `AgentReviewed`, `Advisory`,
    /// `Opaque` — the design doc's `colour-informational` (FR-007).
    val colourInformational: CheckRule<DesignSystemFact>

    /// Motion restraint. `AgentReviewed`, `Advisory`, `Opaque` (FR-007).
    val motionRestraint: CheckRule<DesignSystemFact>

    /// Elevation / overlay layering. `AgentReviewed`, `Advisory`, `Opaque` (FR-007).
    val elevationLayering: CheckRule<DesignSystemFact>

    // ── HumanOnly, BLOCKING (FR-007) — a person decides; `toRule` escalates and never
    //    resolves to a deterministic verdict. ──

    /// Adopting a NEW design policy (e.g. switching to Material). `HumanOnly`, `Blocking` —
    /// routes to a person and never resolves by engine or agent, exactly as the tier demands
    /// (FR-007, edge case). Over an `Opaque` check (a `HumanOnly` decision is not reified).
    val adoptNewPolicy: CheckRule<DesignSystemFact>

    /// (Component 4) The full tiered rule catalog — every rule above, in a stable order. Each
    /// renders to a sentence and explains itself through the kernel's interpreters (FR-006,
    /// SC-004). The authored, default-severity catalog the `adapter` ships verbatim (there is
    /// no dial to re-promote it — research D8).
    val catalog: CheckRule<DesignSystemFact> list

    /// (Component 5) The single high-stakes fence (FR-009): `{ Name = "token-surface"; Trips =
    /// fun c -> c.Surfaces.Contains GeneratedTokenSurface }`. It raises a change that touches
    /// the public token surface to `Fenced`, so that under `Gate` run-mode the `Blocking`
    /// rules (token-drift, contrast, token-surface-gate, evidence) bite. Unlike F10 this is a
    /// SURFACE fence, not a lifecycle/merge fence — the design domain has no phases (research
    /// D3/D8). The blocking SET is the catalog's severities (the kernel's `Fence` carries no
    /// rules — `Route.route` partitions by `Severity & Fenced & Gate`).
    val tokenSurfaceFence: Fence<DesignChange>

    /// (Component 5) The adapter's fence list — `[ tokenSurfaceFence ]`. Kept short by design
    /// (FR-009: a long blocking/fence list would be the smell of over-fencing). Total.
    val fences: Fence<DesignChange> list

    /// (The whole adapter) Assemble the single `Adapter<DesignSystemFact, DesignArtifactRef,
    /// DesignChange>` value the kernel governs a design language with (FR-003): the five SPI
    /// components (`identify`, `toRef`, `probes`, `catalog`, `fences`) plus the F04 `bridge
    /// judge`. Supplies NOTHING ELSE — inference, arbitration, evidence, render/hash/explain,
    /// severity arbitration, and routing are all the kernel's, reached through the SPI
    /// (SC-001). The adapter exposes NO artifact-authoring operation — it observes a design
    /// language, it does not author one (FR-004) — and carries NO phase/lifecycle/`whenPhase`/
    /// merge-fence/dial machinery from F10 (FR-005). Takes ONLY a `JudgeId` (no dial — research
    /// D8). Total; pure; performs no I/O and no agent call.
    val adapter: judge: JudgeId -> Adapter<DesignSystemFact, DesignArtifactRef, DesignChange>
