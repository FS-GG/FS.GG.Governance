module FS.GG.Governance.Adapters.DesignSystem.Tests.ProjectFact

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.DesignSystem

// ─────────────────────────────────────────────────────────────────────────────
// Evidence-obligations note (T017) — read before adding tests in this suite:
//
// (a) Principle IV is N/A. F11 is a PURE value/fold layer — no Model/Msg/Effect,
//     no interpreter, no multi-step state, no I/O (FR-015). There are therefore NO
//     init/update/interpreter tasks and no MVU-boundary obligations. SENSING a live
//     design system into DesignSystemFacts (reading the token tree, capturing
//     rendered output, computing contrast ratios, hashing artifact content) and
//     WIRING the adapter into a running loop is the already-shipped F08 effects shell
//     and the F12 CLI, not this feature — the tests feed fixture-drawn facts directly.
//
// (b) The design-system adapter is the REAL adopter under test. Its rules are fed
//     REAL DesignSystemFacts drawn from a REAL fixture token tree through the BUILT
//     FS.GG.Governance.Adapters.DesignSystem + Spi + Kernel libraries and assert real
//     verdicts/provenance/render/hash/routes (the deterministic-engine "prefer real
//     evaluation" path; no marker needed). The faithful-lift proof composes it
//     alongside the REAL F10 Spec Kit adapter — there is NO synthetic domain in this
//     feature (a stronger composition proof than F10's, which used a synthetic toy),
//     so NO `Synthetic` marker is owed anywhere.
//
// (c) SENSING is out of scope (FR-015) — tests feed fixture-drawn facts directly.
//
// (d) The shipped library never references F10 — only THIS test project does, solely
//     to prove the faithful lift / adoption bar (SC-008, enforced by the
//     dependency-hygiene test in SurfaceDriftTests).
// ─────────────────────────────────────────────────────────────────────────────

// The catalogs of the two domains both live in a module named `Catalog`; full-path module
// abbreviations keep them unambiguous without opening the F10 namespace (whose `SpecKitFact`
// also has an `ArtifactPresent` case that would clash with the design domain's).
module SpecKitMod = FS.GG.Governance.Adapters.SpecKit.SpecKit
module SkCatalog = FS.GG.Governance.Adapters.SpecKit.Catalog

type SpecKitFact = FS.GG.Governance.Adapters.SpecKit.SpecKitFact
type SpecKitArtifact = FS.GG.Governance.Adapters.SpecKit.SpecKitArtifact
type SpecKitChange = FS.GG.Governance.Adapters.SpecKit.SpecKitChange

let judge: JudgeId = { ModelId = "design-judge"; Version = "1" }

/// The assembled design-system adapter under test — the REAL adopter (domain #2).
let designAdapter: Adapter<DesignSystemFact, DesignArtifactRef, DesignChange> =
    Catalog.adapter judge

/// The assembled REAL F10 Spec Kit adapter — the composition partner (domain #1).
let specKitAdapter: Adapter<SpecKitFact, SpecKitArtifact, SpecKitChange> =
    SkCatalog.adapter judge SkCatalog.defaultDial

/// A stable string key for any governance outcome — reused by every domain's `Identify`
/// so an embedded `RuleOutcome` is identified uniformly.
let govKey (o: RuleOutcome) : string =
    match o with
    | Decided (RuleId r, _) -> "decided:" + r
    | NeedsReview req -> "needs:" + req.Key
    | Reviewed rr -> "reviewed:" + rr.Key
    | Escalated (RuleId r) -> "escalated:" + r

// ═════════════════════════════════════════════════════════════════════════════
// The composition root (consumer-authored): the CLOSED `ProjectFact` coproduct with its
// OWN `Governance of RuleOutcome` case, the single-case active patterns, the `inject`
// constructors, the project change shape, the narrow functions, and the project
// `Identify`/`Bridge`. F09 ships the generic machinery; the consumer writes this short,
// single-case wiring (here for tests; F12 for a real project). The two domains are
// genuinely UNRELATED — neither references the other; only this root knows both.
// ═════════════════════════════════════════════════════════════════════════════

type ProjectFact =
    | Design of DesignSystemFact
    | SpecKit of SpecKitFact
    | Governance of RuleOutcome

let (|DesignP|_|) =
    function
    | Design f -> Some f
    | _ -> None

let (|SpecKitP|_|) =
    function
    | SpecKit f -> Some f
    | _ -> None

let injectDesign: DesignSystemFact -> ProjectFact = Design
let injectSpecKit: SpecKitFact -> ProjectFact = SpecKit

type ProjectChange =
    { DesignChange: DesignChange
      SpecKitChange: SpecKitChange }

let narrowDesign (c: ProjectChange) : DesignChange = c.DesignChange
let narrowSpecKit (c: ProjectChange) : SpecKitChange = c.SpecKitChange

/// The project `Identify` delegates per case, so it AGREES with each domain's `Identify` on
/// injected facts — `projIdentify (Design f) = DesignSystem.identify f` (law L3, the
/// precondition that makes provenance ids survive the lift). Hazard-4 injective.
let projIdentify (f: ProjectFact) : FactId =
    match f with
    | Design d -> DesignSystem.identify d
    | SpecKit s -> SpecKitMod.identify s
    | Governance o -> FactId("proj:gov:" + govKey o)

/// A representative real F10 fact — used to prove the composed project still evaluates the
/// Spec Kit domain through the kernel (constructed fully-qualified so the F10 namespace need
/// not be opened here, which would clash `ArtifactPresent`/`Catalog` with the design domain).
let sampleSpecKitFact: SpecKitFact =
    FS.GG.Governance.Adapters.SpecKit.PhaseReached FS.GG.Governance.Adapters.SpecKit.Phase.Merge

let projBridge: Bridge<ProjectFact> =
    { Judge = judge
      ArtifactHash = fun _ _ -> ""
      Embed = Governance
      Project =
        function
        | Governance o -> Some o
        | Design _
        | SpecKit _ -> None }
