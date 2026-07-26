// Curated public signature contract for the Spec Kit phase-check rule catalog, the
// constitution dial, the merge fence, and the assembled adapter (F10).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Catalog.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Catalog.fs body exists (Principle I). The shapes
// mirror docs/governance-design/speckit-in-the-system.md ("Phase checks as reified
// rules", "Run-modes mapped to phases", "The constitution is the dial", "The one
// deliberate behavior change"). PURE — Principle IV is N/A.
//
// This module turns the previous design's monolithic, opaque `analyze` pass into a
// CATALOG of reified `CheckRule`s — each declaring WHO decides (`CheckTier`) and WHETHER
// it blocks (`Severity`), each rendering to a sentence and explaining itself through the
// kernel's interpreters (FR-006). It assembles the single `Adapter` value the kernel
// governs this repo with. It adds NO evaluation/arbitration/evidence/routing code — those
// are the kernel's, reached through the F09 SPI (FR-003, SC-001). Reuses F09
// `Adapter`/SpecKit vocabulary, F07 `Fence`, F05 `Evidence`, F04 `CheckRule`/`JudgeId`,
// F03 `Check`; zero new dependencies.

namespace FS.GG.Governance.Adapters.SpecKit

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi

/// The constitution-authored dial (US4, FR-011): the SINGLE reviewable place that
/// configures which rules block at merge and which earlier phases opt into a hard-stop
/// fence. The "complete enforcement <-> light" dial of docs/governance-design/lessons.md,
/// expressed as DATA so the blocking set is what the constitution authored, NOT a fixed
/// list in the adapter (SC-005). `adapter` reads it to promote rules and assemble fences;
/// the kernel is unchanged. (`evidence-not-synthetic` is always blocking regardless of the
/// dial — honesty about evidence is non-negotiable, FR-013.)
type ConstitutionDial =
    { /// Rule ids the constitution promotes to `Blocking` at merge (FR-011). Authored in
      /// constitution.md; varying this set varies which rules bite at the fence (SC-005).
      BlockingAtMerge: Set<RuleId>
      /// Opt-in earlier-phase hard-stops (FR-010): each `(name, phase)` adds a fence that
      /// trips when the change is at that phase — visible in the constitution, no kernel
      /// change. Empty by default (the inner loop informs; only merge enforces, US3).
      EarlyFences: (string * Phase) list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Catalog =

    // ── The deterministic checks (FR-007) — reified, so they MAY be `Deterministic`. Each
    //    is advisory by default; the dial promotes the merge-blocking ones (US3/US4). ──

    /// `tasks.md` <-> dependency-topology consistency, guarded by `whenPhase Phase.Tasks`:
    /// `allOf [ everyTaskHasDeps; depsResolve; acyclic; skillIdsResolve ]` (FR-007). The
    /// `acyclic`/`depsResolve` sub-checks run the kernel's F05 `Evidence.build` over the
    /// `TaskState`/`TaskDependsOn` facts — a DERIVATION, not a bespoke graph engine (US5,
    /// FR-012). `Deterministic`, default `Advisory` (US5 acceptance 2).
    val tasksGraphWellFormed: CheckRule<SpecKitFact>

    /// Constitution-area completeness, guarded by `whenPhase Phase.Constitution`: every
    /// `ConstitutionArea` is filled (non-placeholder). The Constitution Check that verifies
    /// the dial was filled in honestly (US4, FR-011). `Deterministic`, default `Advisory` —
    /// promoted to `Blocking` at merge by the dial (a placeholder area blocks the merge).
    val constitutionComplete: CheckRule<SpecKitFact>

    /// Surface-baseline / contract currency (generated views not drifted), guarded by
    /// `whenPhase Phase.Plan`. `Deterministic`, default `Advisory` — promoted to `Blocking`
    /// at merge by the dial (FR-007/FR-009).
    val contractsCurrent: CheckRule<SpecKitFact>

    /// Evidence-not-synthetic — the load-bearing merge gate (US5, FR-013). Runs the kernel's
    /// F05 `Evidence.build` + `Evidence.effective` over the `TaskState`/`TaskDependsOn`
    /// facts: the `AutoSynthetic` taint propagates down the dependency graph by the kernel's
    /// fixed point (NOT adapter code), and the check fails if any node's effective state is
    /// `Synthetic`/`AutoSynthetic`. `Deterministic` and `Blocking` BY DEFAULT — the one rule
    /// the dial cannot relax; no disclosure flag or override flips its verdict (FR-013,
    /// SC-004). Only bites at merge because the inner loop is advisory (run-mode `Inner`).
    val evidenceNotSynthetic: CheckRule<SpecKitFact>

    /// Fenced surfaces verified — the touched high-stakes surfaces carry their verification
    /// facts. `Deterministic`, default `Advisory` — promoted to `Blocking` at merge by the
    /// dial (named in the merge fence's blocking set, FR-009).
    val fencedSurfacesVerified: CheckRule<SpecKitFact>

    // ── The judgement checks (FR-007) — `Opaque`, so they can NEVER be `Deterministic`;
    //    advisory, they REPORT via their `Question` and never block the inner loop. ──

    /// "Does plan.md address every requirement in spec.md? List gaps." `AgentReviewed`,
    /// `Advisory`, guarded by `whenPhase Phase.Plan` over an `Opaque` check — reports gaps
    /// during planning, never blocks (FR-007, the design doc's `planSatisfiesSpec`).
    val planSatisfiesSpec: CheckRule<SpecKitFact>

    /// "Are the tasks complete and ordered for the plan?" `AgentReviewed`, `Advisory`,
    /// guarded by `whenPhase Phase.Tasks` over an `Opaque` check (FR-007).
    val tasksCompleteOrdered: CheckRule<SpecKitFact>

    /// "Is the feature in scope / worth doing?" `HumanOnly` — routes to a person and never
    /// resolves to a deterministic verdict, exactly as the tier demands (FR-007, edge case).
    val featureInScope: CheckRule<SpecKitFact>

    /// (Component 4) The full phase-check rule catalog — every rule above, in a stable
    /// order. Each renders to a sentence and explains itself through the kernel's
    /// interpreters (FR-006, SC-006), replacing the monolithic `analyze` pass. The
    /// authored, default-severity catalog; `adapter` applies the dial to it.
    val catalog: CheckRule<SpecKitFact> list

    /// (Component 5) The SINGLE merge fence (FR-009): `{ Name = "feature-merge"; Trips = fun
    /// c -> c.Phase = Phase.Merge }`. It raises the change to `Fenced` at `Phase.Merge`, so
    /// that under `Gate` run-mode the `Blocking`-severity rules bite — "merge is the single
    /// fence that flips to Gate". Everything before merge informs; only the boundary enforces
    /// (US3). The blocking SET is the dial's, not this fence's (the kernel's `Fence` carries
    /// no rules — `Route.route` partitions by `Severity & Fenced & Gate`).
    val mergeFence: Fence<SpecKitChange>

    /// The recommended dial: promotes `constitutionComplete`, `contractsCurrent`, and
    /// `fencedSurfacesVerified` to `Blocking` at merge (alongside the always-blocking
    /// `evidenceNotSynthetic`), with no early fences. The "light" posture is an empty
    /// `BlockingAtMerge` (only evidence blocks); both are honoured by `adapter` (SC-005).
    val defaultDial: ConstitutionDial

    /// Assemble the fence list from the dial: `mergeFence` plus one fence per
    /// `dial.EarlyFences` entry (each tripping at its named phase, FR-010). Total.
    val fences: dial: ConstitutionDial -> Fence<SpecKitChange> list

    /// (The whole adapter) Assemble the single `Adapter<SpecKitFact, SpecKitArtifact,
    /// SpecKitChange>` value the kernel governs this repo with (FR-003): the five SPI
    /// components (`identify`, `toRef`, `probes`, the dial-promoted `catalog`, `fences dial`)
    /// plus the F04 `bridge judge`. Promotes each rule named in `dial.BlockingAtMerge` to
    /// `Blocking` (and keeps `evidenceNotSynthetic` blocking regardless, FR-013); everything
    /// else stays `Advisory`. Supplies NOTHING ELSE — inference, arbitration, evidence,
    /// render/hash/explain, severity arbitration, and routing are all the kernel's, reached
    /// through the SPI (SC-001). The adapter exposes NO artifact-authoring operation — it
    /// observes, it does not author (FR-004). Total; pure; performs no I/O and no agent call.
    val adapter: judge: JudgeId -> dial: ConstitutionDial -> Adapter<SpecKitFact, SpecKitArtifact, SpecKitChange>
