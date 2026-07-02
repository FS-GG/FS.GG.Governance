namespace FS.GG.Governance.Adapters.SpecKit

open FS.GG.Governance.Kernel

// The Spec Kit domain vocabulary, the artifact map, the kernel wiring, and the keystone
// `whenPhase` phase guard (plan module 1). PURE — values and total folds, no I/O, no
// Model/Msg/Effect, no interpreter (Constitution Principle IV is N/A). It re-implements
// NONE of inference/arbitration/evidence/render/hash/explain/severity/routing — those are
// all the kernel's, reached through the F09 SPI (FR-003). The .fs carries NO visibility
// modifiers on top-level bindings; SpecKit.fsi is the sole declaration (Principle II) —
// helpers not named there are private to the module by the signature.

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

type SpecKitFact =
    | PhaseReached of Phase
    | ArtifactPresent of SpecKitArtifact
    | TaskState of taskId: string * state: EvidenceState
    | TaskDependsOn of taskId: string * dep: string
    | SkillBound of taskId: string * skillId: string
    | ConstitutionArea of area: string * filled: bool
    | SpecKitGov of RuleOutcome

type SpecKitChange =
    { Phase: Phase
      Surfaces: Set<SpecKitArtifact> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Phase =

    let rank (phase: Phase) : int =
        match phase with
        | Phase.Constitution -> 0
        | Phase.Specify -> 1
        | Phase.Clarify -> 2
        | Phase.Plan -> 3
        | Phase.Tasks -> 4
        | Phase.Analyze -> 5
        | Phase.Implement -> 6
        | Phase.Merge -> 7

    let reached (current: Phase) (required: Phase) : bool = rank current >= rank required

// ── Internal helpers (NOT in SpecKit.fsi → private to the module by the signature) ──

/// A short, stable name for a phase — used in `identify` and in the phase-guard's
/// declared `LiteralArg` so render/hash distinguish `whenPhase Plan` from `whenPhase Tasks`.
/// Not declared in SpecKit.fsi, so the signature keeps it module-private (Principle II).
module Naming =

    let phaseName (p: Phase) : string =
        match p with
        | Phase.Constitution -> "constitution"
        | Phase.Specify -> "specify"
        | Phase.Clarify -> "clarify"
        | Phase.Plan -> "plan"
        | Phase.Tasks -> "tasks"
        | Phase.Analyze -> "analyze"
        | Phase.Implement -> "implement"
        | Phase.Merge -> "merge"

    let artifactName (a: SpecKitArtifact) : string =
        match a with
        | SpecKitArtifact.Constitution -> "constitution"
        | SpecKitArtifact.Spec -> "spec"
        | SpecKitArtifact.Plan -> "plan"
        | SpecKitArtifact.Research -> "research"
        | SpecKitArtifact.DataModel -> "data-model"
        | SpecKitArtifact.Contracts -> "contracts"
        | SpecKitArtifact.Quickstart -> "quickstart"
        | SpecKitArtifact.Tasks -> "tasks"
        | SpecKitArtifact.TaskDeps -> "task-deps"

    /// A stable key for an embedded governance outcome (kernel wiring) — keeps `identify`
    /// injective over the `SpecKitGov` case.
    let govKey (o: RuleOutcome) : string =
        match o with
        | Decided (RuleId r, _) -> "decided:" + r
        | NeedsReview req -> "needs:" + req.Key
        | Reviewed rr -> "reviewed:" + rr.Key
        | Escalated (RuleId r) -> "escalated:" + r

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SpecKit =

    /// The atomic "supplied phase >= `required`" predicate the phase guard is an `Implies`
    /// over. `required` rides in `Args` as a `LiteralArg`, so render/hash see it (law P4).
    /// Not in SpecKit.fsi → module-private by the signature (Principle II).
    let phaseAtLeast (required: Phase) : Probe<SpecKitFact> =
        { Name = "phase-reached-at-least"
          Reads = []
          Args = [ LiteralArg(Naming.phaseName required) ]
          Eval =
            fun fs ->
                let satisfied =
                    fs
                    |> List.exists (fun f ->
                        match f.Value with
                        | PhaseReached current -> Phase.reached current required
                        | _ -> false)

                if satisfied then
                    Met
                else
                    Unmet(sprintf "phase before %s" (Naming.phaseName required)) }

    let toRef (artifact: SpecKitArtifact) : ArtifactRef =
        { Kind = "speckit"; Key = Naming.artifactName artifact }

    let identify (fact: SpecKitFact) : FactId =
        match fact with
        // value-distinguishing facts key by full value …
        | PhaseReached p -> FactId("phase:" + Naming.phaseName p)
        | ArtifactPresent a -> FactId("artifact:" + Naming.artifactName a)
        | TaskDependsOn (t, d) -> FactId("dep:" + t + "->" + d)
        | SkillBound (t, s) -> FactId("skill:" + t + ":" + s)
        // … entity-keyed facts key by the entity, so a later fact supersedes (dedup) …
        | TaskState (t, _) -> FactId("task:" + t)
        | ConstitutionArea (area, _) -> FactId("area:" + area)
        // … and the governance embed keys by its outcome.
        | SpecKitGov o -> FactId("gov:" + Naming.govKey o)

    // M-ADPT-2: `ArtifactHash` is a no-op HERE because on the real (CLI) path the composition root supplies
    // the content-sensing bridge (`Cli/Project.fs` `Project.bridge`, which hashes the `ArtifactContentFact`
    // the Host loop senses) — this adapter-local bridge is only lifted for its `Rules`/`Embed`/`Project`,
    // never its `ArtifactHash` (see `Composition.lift`). The stale-verdict window (M-ADPT-2) is closed by the
    // rules now DECLARING their reviewed artifacts via `reviewing` (above): that puts `plan.md`/`spec.md` into
    // `Check.reads`, so the loop senses their content and the real bridge folds their hashes into the cache
    // key. A content-bearing `SpecKitFact` case would be needed only to make THIS standalone stub honest too.
    let bridge (judge: JudgeId) : Bridge<SpecKitFact> =
        { Judge = judge
          ArtifactHash = fun _ _ -> ""
          Embed = SpecKitGov
          Project =
            function
            | SpecKitGov o -> Some o
            | _ -> None }

    let whenPhase (required: Phase) (check: Check<SpecKitFact>) : Check<SpecKitFact> =
        Implies(Atom(phaseAtLeast required), check)

    /// (M-ADPT-2) Declare, for an agent-review check, the artifacts the judge actually reviews, so their
    /// SENSED CONTENT enters the F04 agent-review cache key (`Check.reads` drives its artifact half). Without
    /// this a `plan-satisfies-spec` review over an `Opaque` judgement declares NO reads, so a changed
    /// `plan.md`/`spec.md` produces the SAME key and a stale `Reviewed` verdict is reused. The injected atom
    /// always reports `Met` — the neutral element of `All` — so it changes the check's declared `reads`/`hash`
    /// but NEVER its evaluated verdict: the guarded `Opaque` judgement stays the sole driver of the outcome
    /// (and, still carrying an `Opaque`, the check stays non-reified ⇒ `AgentReviewed`, not `Deterministic`).
    let reviewing (artifacts: SpecKitArtifact list) (check: Check<SpecKitFact>) : Check<SpecKitFact> =
        let readsAtom =
            { Name = "reviews-artifacts"
              Reads = artifacts |> List.map toRef
              Args = artifacts |> List.map (Naming.artifactName >> LiteralArg)
              Eval = fun _ -> Met }

        All [ Atom readsAtom; check ]

    let probes: Probe<SpecKitFact> list =
        [ phaseAtLeast Phase.Constitution
          phaseAtLeast Phase.Specify
          phaseAtLeast Phase.Clarify
          phaseAtLeast Phase.Plan
          phaseAtLeast Phase.Tasks
          phaseAtLeast Phase.Analyze
          phaseAtLeast Phase.Implement
          phaseAtLeast Phase.Merge ]
