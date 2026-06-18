// Curated public signature contract for the Spec Kit domain vocabulary, the phase
// guard, the artifact mapping, and the kernel wiring (F10 · first concrete adapter).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching SpecKit.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any SpecKit.fs body exists (Principle I). The shapes
// mirror docs/governance-design/speckit-in-the-system.md ("The spec-kit adapter",
// "Phase checks as reified rules", "Evidence model"). It is domain #1 of Milestone
// M3 (the adoption bar). It is PURE — values and total folds, no I/O, no Model/Msg/
// Effect, no interpreter (Constitution Principle IV is N/A). SENSING the live
// repository into these facts (reading .specify/feature.json, parsing tasks.md /
// tasks.deps.yml, hashing artifact content) is the F08 effects shell / F12 CLI's job,
// NOT this feature — tests feed SpecKitFacts directly (FR-015).
//
// This module supplies the Spec Kit domain's OWN vocabulary and the kernel wiring: the
// closed `SpecKitFact` union (named by `identify`), the `SpecKitArtifact -> ArtifactRef`
// mapping, the `whenPhase` phase guard, the declared probe set, and the F04 `Bridge`.
// It re-implements NONE of inference, arbitration, evidence, rendering, hashing,
// explanation, severity, or routing — those are all reused from the kernel through the
// F09 SPI (FR-003). Reuses F09 `Adapter`/`Lift`, F05 `EvidenceState`/`Evidence`, F04
// `CheckRule`/`Bridge`/`JudgeId`, F03 `Check`/`Probe`/`ArtifactRef`/`Outcome`, F01
// `FactId`/`FactSet`; zero new dependencies (FR-016).

namespace FS.GG.Governance.Adapters.SpecKit

open FS.GG.Governance.Kernel

/// The Spec Kit lifecycle stages, in workflow order (FR-001, Key Entities `Phase`).
/// Supplied to the stateless kernel as a `PhaseReached` fact so it can govern the
/// STATEFUL lifecycle without holding mutable phase state (US2). `RequireQualifiedAccess`
/// because three case names (`Constitution`, `Tasks`) collide with `SpecKitArtifact`;
/// callers write `Phase.Plan`. ORDERED — `Phase.reached` reads the declaration order as
/// "at or after", which is the whole meaning of the phase guard (FR-005).
[<RequireQualifiedAccess>]
type Phase =
    | Constitution
    | Specify
    | Clarify
    | Plan
    | Tasks
    | Analyze
    | Implement
    | Merge

/// The artifact kinds the Spec Kit workflow produces (FR-002, Key Entities
/// `SpecKitArtifact`), mapped onto the kernel's structural `ArtifactRef` by `SpecKit.toRef`.
/// `RequireQualifiedAccess` for the same reason as `Phase`; callers write
/// `SpecKitArtifact.Spec`.
[<RequireQualifiedAccess>]
type SpecKitArtifact =
    | Constitution
    | Spec
    | Plan
    | Research
    | DataModel
    | Contracts
    | Quickstart
    | Tasks
    | TaskDeps

/// The Spec Kit domain's closed, OWNED fact vocabulary — the facts the kernel folds over
/// to govern a feature's lifecycle (FR-001, Key Entities `SpecKitFact`). Never copied from
/// or shared with another domain (FR-014). `TaskState` carries one of the FIVE AUTHORED
/// `EvidenceState`s (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped`) — `AutoSynthetic` is
/// COMPUTED by `Evidence.effective`, never supplied (F05). `SpecKitGov` is the governance
/// embed case the F04 `Bridge` uses (`Embed`/`Project` of the domain-neutral `RuleOutcome`)
/// — kernel wiring, not domain vocabulary (research D2).
type SpecKitFact =
    /// The supplied current phase (e.g. sensed from `.specify/feature.json`). The bridge
    /// between the stateless kernel and the stateful lifecycle (US2, FR-005).
    | PhaseReached of Phase
    /// An artifact is present in the feature directory.
    | ArtifactPresent of SpecKitArtifact
    /// A `tasks.md` task and its DECLARED evidence state (one authored state per task id).
    | TaskState of taskId: string * state: EvidenceState
    /// A `tasks.deps.yml` dependency edge: `taskId` rests on `dep` (FR-012).
    | TaskDependsOn of taskId: string * dep: string
    /// A task's bound skill id.
    | SkillBound of taskId: string * skillId: string
    /// A constitution area and whether it is filled (non-placeholder) — the dial's
    /// honesty signal verified by the Constitution Check (US4, FR-011).
    | ConstitutionArea of area: string * filled: bool
    /// The F04 governance-outcome embed case (kernel wiring): how a `RuleOutcome` lives in
    /// `SpecKitFact`. Asserted by the bridge, not by the sensing layer (research D2).
    | SpecKitGov of RuleOutcome

/// The Spec Kit domain's OWN change shape (FR-014) — the abstract `'change` an F07 `Fence`
/// classifies. `Phase` is the lifecycle position the merge fence trips on (`= Merge`);
/// `Surfaces` is the set of artifact kinds the change touches, for surface-fencing. A
/// project root narrows its composite change onto this via `Lift.fence` when the Spec Kit
/// adapter is composed with another domain (FR-014).
type SpecKitChange =
    { Phase: Phase
      Surfaces: Set<SpecKitArtifact> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Phase =

    /// The phase's position in the lifecycle (`Constitution = 0 … Merge = 7`) — the total
    /// order `reached` reads. An implementation detail; `reached` is the contract.
    val rank: phase: Phase -> int

    /// `true` iff `current` is AT OR AFTER `required` in the lifecycle (`rank current >=
    /// rank required`) — the predicate `whenPhase` uses to decide whether a guarded rule
    /// contributes (FR-005). Total.
    val reached: current: Phase -> required: Phase -> bool

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SpecKit =

    /// (Component 2) The artifact mapping: every Spec Kit artifact kind onto the kernel's
    /// structural `ArtifactRef` (FR-002) — the SINGLE point where Spec Kit artifacts meet
    /// the otherwise domain-neutral algebra. Total and injective.
    val toRef: artifact: SpecKitArtifact -> ArtifactRef

    /// (Component 1, identity) The sole authority on `SpecKitFact` identity the kernel folds
    /// with (F01 `FixedPoint.evaluate`). Keyed by the ENTITY a fact is about — `TaskState`
    /// by `taskId`, `ConstitutionArea` by `area` — so a later fact for the same entity
    /// supersedes (dedup semantics); value-distinguishing facts (`PhaseReached`,
    /// `ArtifactPresent`, `TaskDependsOn`, `SkillBound`) key by their full value. Injective
    /// on value-bearing facts (theory Hazard 4); at a composition root the project
    /// `Identify` MUST agree with this on injected `SpecKitFact`s so provenance ids survive
    /// the lift (F09 law L3). Total.
    val identify: fact: SpecKitFact -> FactId

    /// The F04 kernel wiring for the Spec Kit domain: `Embed = SpecKitGov`, `Project` the
    /// inverse, `Judge` the supplied judge identity, and `ArtifactHash` a fixed sentinel
    /// (`""`) — the PURE adapter holds no artifact-content facts; F08 supplies real content
    /// hashes at the edge (FR-015). NOT a re-implementation of any cross-cutting facility
    /// (research D2). `judge` is per-run config (F12 supplies it; tests fix one).
    val bridge: judge: JudgeId -> Bridge<SpecKitFact>

    /// (The phase guard, FR-005) Make a check contribute ONLY once the supplied
    /// `PhaseReached` fact is at or after `required`, and be a definite NOT-APPLICABLE
    /// before then. Implemented as `Implies (phaseAtLeast required, check)` over an atomic
    /// "phase >= required" probe (reusing the kernel's `Implies`, NOT new logic): before the
    /// phase the antecedent reports `Unmet`, so the implication is VACUOUSLY SATISFIED — a
    /// definite `Pass`, never a `Fail` or `Uncertain` (the F09 inertness mechanism); at or
    /// after, the antecedent is `Met` and the implication reduces to the check's own verdict
    /// (US2). REIFIED-NESS PRESERVING: a guarded reified check stays reified (so it may be
    /// `Deterministic`); a guarded `Opaque` stays opaque (forced `AgentReviewed`/`HumanOnly`).
    /// The `required` phase appears in the antecedent probe's `Args`, so `render`/`hash`
    /// distinguish `whenPhase Plan` from `whenPhase Tasks`. Total.
    val whenPhase: required: Phase -> check: Check<SpecKitFact> -> Check<SpecKitFact>

    /// (Component 3) The declared atomic predicates the rule catalog composes (F03 `Probe`)
    /// — the Spec Kit probe vocabulary, carried for the contract and for testing; the
    /// `Catalog` rules' checks are authoritative for evaluation (research D2). Total.
    val probes: Probe<SpecKitFact> list
