namespace FS.GG.Governance.Adapters.SpecKit

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi

// The reified rule catalog, the constitution dial, the merge fence, and the assembled
// adapter (plan module 2). It turns the previous design's monolithic `analyze` pass into
// a CATALOG of reified `CheckRule`s — each declaring its `CheckTier` and `Severity`, each
// rendering to a sentence and explaining itself through the kernel's interpreters. It adds
// NO evaluation/arbitration/evidence/routing code — those are the kernel's, reached through
// the F09 SPI (FR-003). PURE; Principle IV N/A. The .fs carries NO visibility modifiers on
// top-level bindings; Catalog.fsi is the sole declaration (Principle II) — helpers not
// named there are module-private by the signature.
//
// ── Canonical RuleId strings (T014a) — the single source of truth tying each binding to
//    its kebab-case RuleId. The dial (a Set<RuleId>) matches rules by these exact strings,
//    so `defaultDial` cannot silently promote nothing (finding I1). Every rule construction
//    below and `defaultDial` MUST use exactly these:
//      tasksGraphWellFormed   -> RuleId "tasks-graph"
//      constitutionComplete   -> RuleId "constitution-complete"
//      contractsCurrent       -> RuleId "contracts-current"
//      evidenceNotSynthetic   -> RuleId "evidence-not-synthetic"
//      fencedSurfacesVerified -> RuleId "fenced-surfaces-verified"
//      planSatisfiesSpec      -> RuleId "plan-satisfies-spec"
//      tasksCompleteOrdered   -> RuleId "tasks-complete-ordered"
//      featureInScope         -> RuleId "feature-in-scope"

type ConstitutionDial =
    { BlockingAtMerge: Set<RuleId>
      EarlyFences: (string * Phase) list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Catalog =

    // ── Fact accessors (module-private; not in Catalog.fsi) ──

    let taskStates (fs: FactSet<SpecKitFact>) : (string * EvidenceState) list =
        fs
        |> List.choose (fun f ->
            match f.Value with
            | TaskState (t, s) -> Some(t, s)
            | _ -> None)

    let taskDeps (fs: FactSet<SpecKitFact>) : (string * string) list =
        fs
        |> List.choose (fun f ->
            match f.Value with
            | TaskDependsOn (t, d) -> Some(t, d)
            | _ -> None)

    let skillBindings (fs: FactSet<SpecKitFact>) : (string * string) list =
        fs
        |> List.choose (fun f ->
            match f.Value with
            | SkillBound (t, s) -> Some(t, s)
            | _ -> None)

    let constitutionAreas (fs: FactSet<SpecKitFact>) : (string * bool) list =
        fs
        |> List.choose (fun f ->
            match f.Value with
            | ConstitutionArea (a, filled) -> Some(a, filled)
            | _ -> None)

    let artifactsPresent (fs: FactSet<SpecKitFact>) : SpecKitArtifact list =
        fs
        |> List.choose (fun f ->
            match f.Value with
            | ArtifactPresent a -> Some a
            | _ -> None)

    let declaredTaskIds (fs: FactSet<SpecKitFact>) : Set<string> =
        taskStates fs |> List.map fst |> Set.ofList

    // ── Atomic probes the catalog composes (module-private) ──

    /// The load-bearing evidence probe — runs the kernel's F05 `Evidence.build` +
    /// `Evidence.effective`: `AutoSynthetic` taint propagates down the `TaskDependsOn`
    /// graph by the kernel's least fixed point (NOT adapter code). `Unmet` iff any node's
    /// effective state is `Synthetic`/`AutoSynthetic`; on a malformed graph `Unmet` with
    /// the `GraphError` (malformed ≠ tainted, distinguishable from `Unknown`).
    let evidenceCleanProbe: Probe<SpecKitFact> =
        { Name = "evidence-not-synthetic"
          Reads = []
          Args = []
          Eval =
            fun fs ->
                match Evidence.build (taskStates fs) (taskDeps fs) with
                | Error e -> Unmet(sprintf "malformed evidence graph: %A" e)
                | Ok graph ->
                    let effective = Evidence.effective graph

                    if effective |> Map.exists (fun _ s -> s = Synthetic || s = AutoSynthetic) then
                        Unmet "synthetic evidence reaches the base"
                    else
                        Met }

    /// Every dependency edge's DEPENDENT task is a declared `TaskState`.
    let everyTaskHasDepsProbe: Probe<SpecKitFact> =
        { Name = "every-task-declared"
          Reads = []
          Args = []
          Eval =
            fun fs ->
                let declared = declaredTaskIds fs

                match taskDeps fs |> List.map fst |> List.filter (declared.Contains >> not) with
                | [] -> Met
                | missing -> Unmet(sprintf "undeclared dependent task(s): %A" missing) }

    /// Every dependency edge's TARGET task is a declared `TaskState` (the dep resolves).
    let depsResolveProbe: Probe<SpecKitFact> =
        { Name = "deps-resolve"
          Reads = []
          Args = []
          Eval =
            fun fs ->
                let declared = declaredTaskIds fs

                match taskDeps fs |> List.map snd |> List.filter (declared.Contains >> not) with
                | [] -> Met
                | missing -> Unmet(sprintf "unresolved dependency target(s): %A" missing) }

    /// The dependency topology is acyclic — reads the SAME `Evidence.build` result (a
    /// derivation, not a bespoke engine); `Unmet` only on a `Cycle`.
    let acyclicProbe: Probe<SpecKitFact> =
        { Name = "acyclic"
          Reads = []
          Args = []
          Eval =
            fun fs ->
                match Evidence.build (taskStates fs) (taskDeps fs) with
                | Error (Cycle cycle) -> Unmet(sprintf "cyclic task graph: %A" cycle)
                | _ -> Met }

    /// Every `SkillBound` names a declared `TaskState`.
    let skillIdsResolveProbe: Probe<SpecKitFact> =
        { Name = "skill-ids-resolve"
          Reads = []
          Args = []
          Eval =
            fun fs ->
                let declared = declaredTaskIds fs

                match skillBindings fs |> List.map fst |> List.filter (declared.Contains >> not) with
                | [] -> Met
                | missing -> Unmet(sprintf "skill bound to undeclared task(s): %A" missing) }

    /// Every supplied `ConstitutionArea` is filled (non-placeholder).
    let constitutionProbe: Probe<SpecKitFact> =
        { Name = "constitution-areas-filled"
          Reads = [ SpecKit.toRef SpecKitArtifact.Constitution ]
          Args = []
          Eval =
            fun fs ->
                match constitutionAreas fs |> List.tryFind (snd >> not) with
                | Some (area, _) -> Unmet(sprintf "constitution area '%s' is a placeholder" area)
                | None -> Met }

    /// The published contract / surface views are present and current.
    let contractsCurrentProbe: Probe<SpecKitFact> =
        { Name = "contracts-current"
          Reads = [ SpecKit.toRef SpecKitArtifact.Contracts ]
          Args = []
          Eval =
            fun fs ->
                if artifactsPresent fs |> List.contains SpecKitArtifact.Contracts then
                    Met
                else
                    Unmet "contracts not present / drifted" }

    /// The touched high-stakes surfaces carry their verification facts.
    let fencedSurfacesProbe: Probe<SpecKitFact> =
        { Name = "fenced-surfaces-verified"
          Reads = [ SpecKit.toRef SpecKitArtifact.TaskDeps ]
          Args = []
          Eval =
            fun fs ->
                if artifactsPresent fs |> List.contains SpecKitArtifact.TaskDeps then
                    Met
                else
                    Unmet "fenced surface lacks its verification" }

    // ── Rule construction helper (module-private) ──

    let mkRule (id: string) (tier: CheckTier) (spec: SpecSource) (check: Check<SpecKitFact>) : CheckRule<SpecKitFact> =
        match CheckRule.rule (RuleId id) tier spec check with
        | Ok r -> r
        | Error e -> failwithf "catalog rule %s rejected: %A" id e

    // ── The deterministic checks — reified, advisory by default (the dial promotes the
    //    merge-blocking ones); `evidence-not-synthetic` is Blocking by default. ──

    let tasksGraphWellFormed: CheckRule<SpecKitFact> =
        mkRule
            "tasks-graph"
            Deterministic
            { Document = "speckit"; Section = "task-graph" }
            (SpecKit.whenPhase
                Phase.Tasks
                (Check.allOf
                    [ Atom everyTaskHasDepsProbe
                      Atom depsResolveProbe
                      Atom acyclicProbe
                      Atom skillIdsResolveProbe ]))

    let constitutionComplete: CheckRule<SpecKitFact> =
        mkRule
            "constitution-complete"
            Deterministic
            { Document = "constitution"; Section = "completeness" }
            (SpecKit.whenPhase Phase.Constitution (Atom constitutionProbe))

    let contractsCurrent: CheckRule<SpecKitFact> =
        mkRule
            "contracts-current"
            Deterministic
            { Document = "speckit"; Section = "contracts" }
            (SpecKit.whenPhase Phase.Plan (Atom contractsCurrentProbe))

    let evidenceNotSynthetic: CheckRule<SpecKitFact> =
        mkRule
            "evidence-not-synthetic"
            Deterministic
            { Document = "constitution"; Section = "evidence" }
            (Atom evidenceCleanProbe)
        |> CheckRule.blocking

    let fencedSurfacesVerified: CheckRule<SpecKitFact> =
        mkRule
            "fenced-surfaces-verified"
            Deterministic
            { Document = "speckit"; Section = "surfaces" }
            (Atom fencedSurfacesProbe)

    // ── The judgement checks — `Opaque`, so never `Deterministic`; advisory, they report
    //    via their `Question` and never block the inner loop. ──

    let planSatisfiesSpec: CheckRule<SpecKitFact> =
        mkRule
            "plan-satisfies-spec"
            AgentReviewed
            { Document = "plan"; Section = "coverage" }
            (SpecKit.whenPhase Phase.Plan (Opaque("plan-satisfies-spec", fun _ -> Unknown "judgement")))
        |> CheckRule.asking "Does plan.md address every requirement in spec.md? List gaps."

    let tasksCompleteOrdered: CheckRule<SpecKitFact> =
        mkRule
            "tasks-complete-ordered"
            AgentReviewed
            { Document = "tasks"; Section = "completeness" }
            (SpecKit.whenPhase Phase.Tasks (Opaque("tasks-complete-ordered", fun _ -> Unknown "judgement")))
        |> CheckRule.asking "Are the tasks complete and ordered for the plan?"

    let featureInScope: CheckRule<SpecKitFact> =
        mkRule
            "feature-in-scope"
            HumanOnly
            { Document = "spec"; Section = "scope" }
            (Opaque("feature-in-scope", fun _ -> Unknown "a person must decide whether this is worth doing"))

    let catalog: CheckRule<SpecKitFact> list =
        [ tasksGraphWellFormed
          constitutionComplete
          contractsCurrent
          evidenceNotSynthetic
          fencedSurfacesVerified
          planSatisfiesSpec
          tasksCompleteOrdered
          featureInScope ]

    let mergeFence: Fence<SpecKitChange> =
        { Name = "feature-merge"
          Trips = fun c -> c.Phase = Phase.Merge }

    let defaultDial: ConstitutionDial =
        { BlockingAtMerge =
            Set.ofList
                [ RuleId "constitution-complete"
                  RuleId "contracts-current"
                  RuleId "fenced-surfaces-verified" ]
          EarlyFences = [] }

    let fences (dial: ConstitutionDial) : Fence<SpecKitChange> list =
        mergeFence
        :: [ for (name, phase) in dial.EarlyFences -> { Name = name; Trips = fun c -> c.Phase = phase } ]

    let adapter (judge: JudgeId) (dial: ConstitutionDial) : Adapter<SpecKitFact, SpecKitArtifact, SpecKitChange> =
        // Promote each rule the dial names; `evidence-not-synthetic` is already Blocking in
        // `catalog`, so it stays blocking regardless of the dial (FR-013). `blocking` never
        // demotes, so the promotion is idempotent.
        let promoted =
            catalog
            |> List.map (fun r -> if dial.BlockingAtMerge.Contains r.Id then CheckRule.blocking r else r)

        { Identify = SpecKit.identify
          ToRef = SpecKit.toRef
          Probes = SpecKit.probes
          Rules = promoted
          Fences = fences dial
          Bridge = SpecKit.bridge judge }
