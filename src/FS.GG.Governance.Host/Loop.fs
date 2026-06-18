namespace FS.GG.Governance.Host

open FS.GG.Governance.Kernel

// Implementation of the pure MVU core (F08). Visibility lives in Loop.fsi — NO
// `private`/`internal`/`public` on any top-level binding (Principle II); helpers omitted
// from the .fsi are private by absence. `update` performs NO I/O and never throws
// (FR-002): all I/O is reified as `Effect` for the edge `Interpreter` to run.

type ArtifactContent = { Ref: ArtifactRef; Content: string }

type JudgeVerdict = { Verdict: Verdict; Confidence: float }

type ReviewTask =
    { Key: string
      Instruction: string
      Data: ArtifactContent list }

type ReviewDispatch = { Task: ReviewTask; Samples: int }

type AcceptancePolicy =
    | SingleSample
    | Agreement of count: int
    | Confidence of threshold: float

type Acceptance =
    | Freeze of Verdict
    | StayPending

type Disclosure = { Rule: RuleId; Justification: string }

type Failure =
    | ArtifactUnavailable of artifact: ArtifactRef * reason: string
    | ReviewDispatchFailed of key: string * reason: string
    | ReviewStoreUnavailable of key: string * reason: string

type Output =
    | ExplanationJson of string
    | ContractJson of string
    | RouteText of string

type Effect =
    | ReadArtifact of ArtifactRef
    | LoadReview of key: string
    | DispatchReview of ReviewDispatch
    | RecordVerdict of RecordedReview
    | EmitOutput of Output

type Msg<'fact> =
    | Sensed of artifact: ArtifactRef * result: Result<string, string>
    | Loaded of key: string * result: Result<RecordedReview option, string>
    | Reviewed of key: string * result: Result<JudgeVerdict list, string>
    | Recorded of key: string * result: Result<unit, string>
    | Disclosed of Disclosure

type Phase =
    | Sensing
    | Planning
    | Quiescent

type Model<'fact> =
    { Phase: Phase
      Facts: FactSet<'fact>
      Route: Route
      Pending: Set<string>
      Disclosures: Disclosure list
      Failures: Failure list
      Rounds: int }

type LoopConfig<'change, 'fact> =
    { Identify: 'fact -> FactId
      Rules: CheckRule<'fact> list
      Bridge: Bridge<'fact>
      Fences: Fence<'change> list
      Mode: RunMode
      Policy: AcceptancePolicy
      SenseArtifact: ArtifactRef -> string -> 'fact
      ReadContent: FactSet<'fact> -> ArtifactRef -> string option }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    // ── The acceptance policy (decision #2, FR-009) ──

    let defaultPolicy = SingleSample

    let samplesFor policy =
        match policy with
        | SingleSample -> 1
        | Confidence _ -> 1
        | Agreement count -> max 1 count

    let accept policy samples =
        match policy, samples with
        | _, [] -> StayPending
        | SingleSample, s :: _ -> Freeze s.Verdict
        | Agreement n, _ ->
            // Freeze a verdict shared by >= n samples; order-independent (canonicalize ties
            // by the verdict's textual form so the choice never depends on sample order).
            samples
            |> List.countBy (fun s -> s.Verdict)
            |> List.filter (fun (_, c) -> c >= n)
            |> List.sortBy (fun (v, _) -> sprintf "%A" v)
            |> List.tryHead
            |> Option.map (fun (v, _) -> Freeze v)
            |> Option.defaultValue StayPending
        | Confidence t, _ ->
            // Freeze iff all (non-empty) samples agree AND mean confidence >= t.
            match samples |> List.map (fun s -> s.Verdict) |> List.distinct with
            | [ v ] ->
                let mean = (samples |> List.sumBy (fun s -> s.Confidence)) / float (List.length samples)
                if mean >= t then Freeze v else StayPending
            | _ -> StayPending

    // ── Internal helpers (absent from Loop.fsi → private by Principle II) ──

    // Append-only fact upsert, deduplicated by the caller's identity authority (FR-014).
    let assertFact (config: LoopConfig<'change, 'fact>) (value: 'fact) (facts: FactSet<'fact>) =
        let id = config.Identify value
        if facts |> List.exists (fun fa -> fa.Id = id) then
            facts
        else
            facts @ [ { Id = id; Value = value; Provenance = [] } ]

    // Failures/disclosures are deduplicated and kept in a deterministic (sorted) order so
    // the final Model is byte-for-byte identical across completion orders (R-D1/R-D2, SC-007).
    let addFailure (f: Failure) (failures: Failure list) =
        if List.contains f failures then failures else List.sort (f :: failures)

    let addDisclosure (d: Disclosure) (ds: Disclosure list) =
        if List.contains d ds then ds else List.sort (d :: ds)

    // Project the governance outcomes out of a fact set via the caller's bridge.
    let outcomesOf (config: LoopConfig<'change, 'fact>) (facts: FactSet<'fact>) =
        facts |> List.choose (fun fa -> config.Bridge.Project fa.Value)

    // The SUPPLIED facts — sensed artifacts + asserted RecordedReviews (empty provenance). The
    // kernel re-derives everything else, so planning always evaluates over THESE (never over a
    // previous round's derived facts), keeping the final fact set equal to a clean kernel run and
    // free of stale NeedsReview outcomes (SC-002).
    let suppliedFacts (model: Model<'fact>) =
        model.Facts |> List.filter (fun fa -> List.isEmpty fa.Provenance)

    // Run the already-shipped kernel fixed point over the supplied facts (FR-006, no new logic).
    let evaluate (config: LoopConfig<'change, 'fact>) (facts: FactSet<'fact>) =
        let rules = config.Rules |> List.map (CheckRule.toRule config.Bridge)
        FixedPoint.evaluate config.Identify rules facts

    // The F06/F07 edge outputs, emitted once at quiescence (FR-015).
    let emitOutputs (config: LoopConfig<'change, 'fact>) (model: Model<'fact>) =
        let planned = config.Rules |> List.map (fun r -> r.Check) |> Check.allOf
        [ EmitOutput(ExplanationJson(Json.ofExplanation (Check.explain model.Facts planned)))
          EmitOutput(ContractJson(Json.ofContract (Contract.ofRules config.Rules)))
          EmitOutput(RouteText(Route.renderRoute model.Route)) ]

    // The reviewer instruction (rule Question) and the read artifacts as untrusted DATA —
    // SEPARATE channels the loop never merges (FR-010, decision #3). Data content is recovered
    // from the facts via the caller's ReadContent (the SenseArtifact inverse) — no I/O.
    let isolate (config: LoopConfig<'change, 'fact>) (model: Model<'fact>) (req: ReviewRequest) =
        let data =
            match config.Rules |> List.tryFind (fun r -> r.Id = req.Rule) with
            | Some rule ->
                Check.reads rule.Check
                |> List.distinct
                |> List.choose (fun ref ->
                    config.ReadContent model.Facts ref
                    |> Option.map (fun content -> { Ref = ref; Content = content }))
            | None -> []

        { Key = req.Key
          Instruction = defaultArg req.Question ""
          Data = data }

    // Recover the open NeedsReview request for a cache key from the current facts.
    let requestFor (config: LoopConfig<'change, 'fact>) (model: Model<'fact>) (key: string) =
        let result = evaluate config (suppliedFacts model)

        outcomesOf config result.Facts
        |> List.tryPick (function
            | NeedsReview req when req.Key = key -> Some req
            | _ -> None)

    // PLAN + decide effects: run the kernel, emit LoadReview for fresh NeedsReview keys, and
    // at quiescence (no fresh needs, nothing pending) emit the F06/F07 outputs ONCE (FR-006/015).
    let advance (config: LoopConfig<'change, 'fact>) (model: Model<'fact>) : Model<'fact> * Effect list =
        let result = evaluate config (suppliedFacts model)
        let model = { model with Facts = result.Facts; Rounds = max model.Rounds result.Rounds }
        let outcomes = outcomesOf config result.Facts

        let recordedKeys =
            outcomes
            |> List.choose (function
                | RuleOutcome.Reviewed rr -> Some rr.Key
                | _ -> None)
            |> Set.ofList

        let toLoad =
            outcomes
            |> List.choose (function
                | NeedsReview req -> Some req.Key
                | _ -> None)
            |> List.filter (fun k -> not (Set.contains k model.Pending) && not (Set.contains k recordedKeys))
            |> List.distinct

        if not (List.isEmpty toLoad) then
            { model with Phase = Planning }, List.map LoadReview toLoad
        elif Set.isEmpty model.Pending then
            { model with Phase = Quiescent }, emitOutputs config model
        else
            // reviews dispatched or held below-policy are still outstanding; nothing to emit yet
            { model with Phase = Planning }, []

    let continueSensing (config: LoopConfig<'change, 'fact>) (model: Model<'fact>) =
        // While sensing, a read is resolved once its content hash is readable (a sensed fact) or
        // it failed; when every declared read is resolved, PLAN (FR-005).
        let failed =
            model.Failures
            |> List.choose (function
                | ArtifactUnavailable (r, _) -> Some r
                | _ -> None)
            |> Set.ofList

        let resolved ref =
            Set.contains ref failed || config.Bridge.ArtifactHash model.Facts ref <> ""

        let expected =
            config.Rules |> List.collect (fun r -> Check.reads r.Check) |> List.distinct

        if model.Phase = Sensing && List.forall resolved expected then
            advance config model
        else
            model, []

    // ── init + the pure transition (FR-001, FR-002) ──

    let init (config: LoopConfig<'change, 'fact>) (change: 'change) : Model<'fact> * Effect list =
        let route = Route.route config.Fences config.Rules config.Mode change

        let reads =
            config.Rules |> List.collect (fun r -> Check.reads r.Check) |> List.distinct

        let model =
            { Phase = (if List.isEmpty reads then Planning else Sensing)
              Facts = []
              Route = route
              Pending = Set.empty
              Disclosures = []
              Failures = []
              Rounds = 0 }

        if List.isEmpty reads then
            // nothing to sense → PLAN immediately (spec Edge Cases "Nothing to do")
            advance config model
        else
            model, List.map ReadArtifact reads

    let update
        (config: LoopConfig<'change, 'fact>)
        (msg: Msg<'fact>)
        (model: Model<'fact>)
        : Model<'fact> * Effect list =
        match msg with
        | Sensed (ref, Ok content) ->
            let model = { model with Facts = assertFact config (config.SenseArtifact ref content) model.Facts }
            continueSensing config model

        | Sensed (ref, Error e) ->
            let model = { model with Failures = addFailure (ArtifactUnavailable(ref, e)) model.Failures }
            continueSensing config model

        | Loaded (key, Ok (Some rr)) ->
            // cache HIT: assert the recorded verdict, re-plan, emit NO dispatch (FR-008)
            let model =
                { model with
                    Facts = assertFact config (config.Bridge.Embed(RuleOutcome.Reviewed rr)) model.Facts
                    Pending = Set.remove key model.Pending }

            advance config model

        | Loaded (key, Ok None) ->
            // cache MISS: dispatch a review (instruction/data isolated) unless already pending
            if Set.contains key model.Pending then
                model, []
            else
                match requestFor config model key with
                | Some req ->
                    let dispatch = { Task = isolate config model req; Samples = samplesFor config.Policy }
                    { model with Pending = Set.add key model.Pending }, [ DispatchReview dispatch ]
                | None -> model, []

        | Loaded (key, Error e) ->
            { model with Failures = addFailure (ReviewStoreUnavailable(key, e)) model.Failures }, []

        | Reviewed (key, Ok samples) ->
            if not (Set.contains key model.Pending) then
                model, [] // idempotent: already resolved
            else
                match accept config.Policy samples with
                | Freeze v ->
                    match requestFor config model key with
                    | Some req ->
                        let rr = { Rule = req.Rule; Key = key; Verdict = v }

                        let model =
                            { model with
                                Facts = assertFact config (config.Bridge.Embed(RuleOutcome.Reviewed rr)) model.Facts
                                Pending = Set.remove key model.Pending }

                        let model, effects = advance config model
                        model, RecordVerdict rr :: effects
                    | None -> { model with Pending = Set.remove key model.Pending }, []
                | StayPending ->
                    // below policy: record nothing, conclusion stays Uncertain; keep the key in
                    // Pending so this run does not re-dispatch — the NEXT run will (FR-009/SC-004).
                    model, []

        | Reviewed (key, Error e) ->
            { model with Failures = addFailure (ReviewDispatchFailed(key, e)) model.Failures }, []

        | Recorded (_, Ok ()) -> model, [] // no-op; the fact is already asserted (FR-014)

        | Recorded (key, Error e) ->
            { model with Failures = addFailure (ReviewStoreUnavailable(key, e)) model.Failures }, []

        | Disclosed d -> { model with Disclosures = addDisclosure d model.Disclosures }, []
