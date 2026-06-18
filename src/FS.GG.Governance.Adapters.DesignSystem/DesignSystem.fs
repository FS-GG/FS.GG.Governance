namespace FS.GG.Governance.Adapters.DesignSystem

open FS.GG.Governance.Kernel

// The design-system domain vocabulary, the artifact map, the probes, and the kernel
// wiring (plan module 1). PURE — values and total folds, no I/O, no Model/Msg/Effect, no
// interpreter (Constitution Principle IV is N/A). It re-implements NONE of
// inference/arbitration/evidence/render/hash/explain/severity/routing — those are all the
// kernel's, reached through the F09 SPI (FR-003). This domain adopts the kernel by
// DIFFERENCE: there is deliberately NO Phase, NO whenPhase phase guard, NO merge fence, NO
// dial — a design language is a FLAT surface with no lifecycle (FR-005, research D3). The
// .fs carries NO visibility modifiers on top-level bindings; DesignSystem.fsi is the sole
// declaration (Principle II) — helpers not named there are private to the module by the
// signature.

type DesignArtifactRef =
    | TokenDocument
    | GeneratedTokenSurface
    | RenderedCapture
    | InteractionStateSpec
    | PagePatternSpec

type DesignSystemFact =
    | PolicySelected of policy: string
    | DesignRule of ruleId: string
    | SurfaceObservation of probe: string * subject: DesignArtifactRef * met: bool
    | MeasurementState of measurementId: string * state: EvidenceState
    | VerdictRestsOn of verdictId: string * measurementId: string
    | ArtifactPresent of DesignArtifactRef
    | DesignGov of RuleOutcome

type DesignChange =
    { Surfaces: Set<DesignArtifactRef> }

// ── Internal helpers (NOT in DesignSystem.fsi → private to the module by the signature) ──

/// Short, stable names for the design vocabulary — used in `identify`, `toRef`, and the
/// probes' declared `Reads`/`Args` so render/hash distinguish artifacts. Not declared in
/// DesignSystem.fsi, so the signature keeps it module-private (Principle II).
module Naming =

    let artifactName (a: DesignArtifactRef) : string =
        match a with
        | TokenDocument -> "token-document"
        | GeneratedTokenSurface -> "generated-token-surface"
        | RenderedCapture -> "rendered-capture"
        | InteractionStateSpec -> "interaction-state-spec"
        | PagePatternSpec -> "page-pattern-spec"

    /// A stable key for an embedded governance outcome (kernel wiring) — keeps `identify`
    /// injective over the `DesignGov` case.
    let govKey (o: RuleOutcome) : string =
        match o with
        | Decided (RuleId r, _) -> "decided:" + r
        | NeedsReview req -> "needs:" + req.Key
        | Reviewed rr -> "reviewed:" + rr.Key
        | Escalated (RuleId r) -> "escalated:" + r

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DesignSystem =

    let toRef (artifact: DesignArtifactRef) : ArtifactRef =
        { Kind = "design"; Key = Naming.artifactName artifact }

    let identify (fact: DesignSystemFact) : FactId =
        match fact with
        // value-distinguishing facts key by full value …
        | DesignRule r -> FactId("rule:" + r)
        | VerdictRestsOn (v, m) -> FactId("rests:" + v + "->" + m)
        | ArtifactPresent a -> FactId("artifact:" + Naming.artifactName a)
        // … entity-keyed facts key by the entity, so a later fact supersedes (dedup) …
        | PolicySelected _ -> FactId "policy"
        | MeasurementState (m, _) -> FactId("measurement:" + m)
        | SurfaceObservation (probe, subject, _) ->
            FactId("observation:" + probe + ":" + Naming.artifactName subject)
        // … and the governance embed keys by its outcome.
        | DesignGov o -> FactId("gov:" + Naming.govKey o)

    let bridge (judge: JudgeId) : Bridge<DesignSystemFact> =
        { Judge = judge
          ArtifactHash = fun _ _ -> ""
          Embed = DesignGov
          Project =
            function
            | DesignGov o -> Some o
            | _ -> None }

    // ── The atomic three-valued observation the deterministic probes share. It READS the
    //    `SurfaceObservation (name, subject, met)` facts as data and reports `Met` (a present
    //    `true` observation), `Unmet` (a present `false` observation — a definite failure),
    //    or `Unknown` (NO observation for the key — a missing fixture is undecided, NEVER a
    //    silent `Met`, Principle VI, laws Pr1–Pr3). It never renders, captures, or authors. ──

    let observe (name: string) (subject: DesignArtifactRef) : FactSet<DesignSystemFact> -> Outcome =
        fun fs ->
            let observed =
                fs
                |> List.choose (fun f ->
                    match f.Value with
                    | SurfaceObservation (n, s, met) when n = name && s = subject -> Some met
                    | _ -> None)

            match observed with
            | [] -> Unknown(sprintf "no '%s' observation on %s" name (Naming.artifactName subject))
            | mets ->
                if List.contains false mets then
                    Unmet(sprintf "'%s' violated on %s" name (Naming.artifactName subject))
                else
                    Met

    /// The token-drift probe shape — reads `SurfaceObservation ("surface-matches",
    /// generated, _)`; both artifacts ride in `Reads`/`Args` so `surfaceMatches g c` and
    /// `surfaceMatches c g` render and hash differently (law Pr4).
    let surfaceMatchesProbe (generated: DesignArtifactRef) (source: DesignArtifactRef) : Probe<DesignSystemFact> =
        { Name = "surface-matches"
          Reads = [ toRef generated; toRef source ]
          Args = [ ArtifactArg(toRef generated); ArtifactArg(toRef source) ]
          Eval = observe "surface-matches" generated }

    /// The colour/contrast probe shape — `policy` rides as a `LiteralArg`, so
    /// `contrastMeets "AntAA" s` and `contrastMeets "WCAGAAA" s` render and hash differently.
    let contrastMeetsProbe (policy: string) (surface: DesignArtifactRef) : Probe<DesignSystemFact> =
        { Name = "contrast-meets"
          Reads = [ toRef surface ]
          Args = [ LiteralArg policy; ArtifactArg(toRef surface) ]
          Eval = observe "contrast-meets" surface }

    /// The shared deterministic surface probe shape over `SurfaceObservation (name, subject,
    /// _)` — the `name` is the probe's `Name` (so spacing-scale ≠ control-height) and the
    /// `subject` rides in `Reads`/`Args`.
    let surfaceObservedProbe (name: string) (subject: DesignArtifactRef) : Probe<DesignSystemFact> =
        { Name = name
          Reads = [ toRef subject ]
          Args = [ ArtifactArg(toRef subject) ]
          Eval = observe name subject }

    /// The evidence-honesty probe shape (the F05 taint realization): builds the kernel
    /// `EvidenceGraph` from the `MeasurementState` nodes and `VerdictRestsOn` edges, runs
    /// `Evidence.effective`, and reports `Met` iff NO node's effective state is
    /// `Synthetic`/`AutoSynthetic`; `Unmet` (with the offending id) on a real taint; `Unmet`
    /// (with the `GraphError`) on a malformed graph (malformed ≠ tainted, Principle VI). The
    /// `AutoSynthetic` taint propagates down the `VerdictRestsOn` chain by the KERNEL's least
    /// fixed point — this adapter ships NO graph engine (research D7, laws E1–E3).
    let evidenceMeasuredProbe: Probe<DesignSystemFact> =
        { Name = "evidence-measured"
          Reads = []
          Args = []
          Eval =
            fun fs ->
                let nodes =
                    fs
                    |> List.choose (fun f ->
                        match f.Value with
                        | MeasurementState (m, s) -> Some(m, s)
                        | _ -> None)

                let edges =
                    fs
                    |> List.choose (fun f ->
                        match f.Value with
                        | VerdictRestsOn (v, m) -> Some(v, m)
                        | _ -> None)

                match Evidence.build nodes edges with
                | Error e -> Unmet(sprintf "malformed evidence graph: %A" e)
                | Ok graph ->
                    let effective = Evidence.effective graph

                    match
                        effective
                        |> Map.tryFindKey (fun _ s -> s = Synthetic || s = AutoSynthetic)
                    with
                    | Some id -> Unmet(sprintf "verdict '%s' rests on synthetic / unmeasured evidence" id)
                    | None -> Met }

    let surfaceMatches (generated: DesignArtifactRef) (source: DesignArtifactRef) : Check<DesignSystemFact> =
        Atom(surfaceMatchesProbe generated source)

    let contrastMeets (policy: string) (surface: DesignArtifactRef) : Check<DesignSystemFact> =
        Atom(contrastMeetsProbe policy surface)

    let surfaceObserved (name: string) (subject: DesignArtifactRef) : Check<DesignSystemFact> =
        Atom(surfaceObservedProbe name subject)

    let evidenceMeasured: Check<DesignSystemFact> = Atom evidenceMeasuredProbe

    let probes: Probe<DesignSystemFact> list =
        [ surfaceMatchesProbe GeneratedTokenSurface TokenDocument
          contrastMeetsProbe "ant-aa" GeneratedTokenSurface
          surfaceObservedProbe "token-surface-gate" GeneratedTokenSurface
          surfaceObservedProbe "spacing-scale" GeneratedTokenSurface
          surfaceObservedProbe "control-height" GeneratedTokenSurface
          surfaceObservedProbe "intent-coverage" GeneratedTokenSurface
          surfaceObservedProbe "visual-state" InteractionStateSpec
          evidenceMeasuredProbe ]
