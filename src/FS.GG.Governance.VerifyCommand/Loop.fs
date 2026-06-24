// The PURE MVU core of the `fsgg verify` host command (F056). Visibility lives in Loop.fsi (Principle II) —
// this file carries NO top-level access modifiers; helper types/functions absent from the signature are
// hidden by it. `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock: the whole
// scope -> load -> route -> registry -> findings -> select -> ROLLUP -> RUN/REUSE -> PROJECT -> persist-plan
// -> summarize -> EXIT-FROM-BASIS composition is a pure transition over Model + Msg emitting Effect data the
// edge Interpreter executes (Principle IV). It is the CLOSEST SIBLING of `fsgg ship` (F026): it threads the
// FIXED `RunMode.Verify` into the VERBATIM `Ship.rollup` (no `--mode` flag — FR-017), projects its own
// `verify.json` via `VerifyJson.ofVerifyDecision`, surfaces a first-class currency section, and reports
// "nothing to verify" for an empty selection. The verdict comes from F024 `Ship.rollup`; the verdict
// relocation from F052 `applyExecution`; the document bytes from F056 `VerifyJson`.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Config.Model       // GovernedPath, Validation, Valid/Invalid, normalizePath, diagnosticIdToken
open FS.GG.Governance.Snapshot.Model      // RepoSnapshot, ChangedPath, DiffRange, CommitId
open FS.GG.Governance.Routing             // Routing.route
open FS.GG.Governance.Findings            // Findings.findUnknownGovernedPaths
open FS.GG.Governance.Findings.Model       // findingIdToken
open FS.GG.Governance.Gates               // Gates.buildRegistry
open FS.GG.Governance.Gates.Model          // Gate, gateIdValue
open FS.GG.Governance.Route               // Route.select
open FS.GG.Governance.Enforcement.Enforcement // RunMode (Verify), Profile, Severity, Recognized, recognizeProfile
open FS.GG.Governance.Ship                // Ship.rollup
open FS.GG.Governance.Ship.Model           // ShipDecision, Verdict, ExitCodeBasis, EnforcedItem, EnforcedItemId
open FS.GG.Governance.VerifyJson           // VerifyJson.ofVerifyDecision
// F046 cache-eligibility pipeline (sense → resolve → evaluate → embed Some report)
open FS.GG.Governance.FreshnessKey.Model   // Revision, categoryToken
open FS.GG.Governance.FreshnessResolution  // resolve, entries, candidate, isResolved, missingFacts, missingFactToken
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts, FreshnessResolutionEntry
open FS.GG.Governance.CacheEligibility      // evaluate, entries
open FS.GG.Governance.CacheEligibility.Model // CandidateGate, CacheEligibilityEntry, CacheEligibilityVerdict, Reusable, MustRecompute
open FS.GG.Governance.EvidenceReuse         // empty, referenceValue
open FS.GG.Governance.EvidenceReuse.Model   // ReuseStore, EvidenceRef, RecomputeCause, NoPriorEvidence, InputsChanged
open FS.GG.Governance.EvidenceReuseStore    // F048: prune, retain, serialise, defaultRetentionBound
// F052 gate-execution wiring (classify → run → capture → relocate verdict → persist-grown-store)
open FS.GG.Governance.CommandRecord.Model    // CommandRecord, ExitCode (FreshnessInputs/ToolingFacts already open)
open FS.GG.Governance.GateExecution.Model     // GateCommand
open FS.GG.Governance.EvidenceCapture        // EvidenceCapture.capture
open FS.GG.Governance.GateRun                 // Plan.commandFor / priorExitOf / passed
open FS.GG.Governance.GateRun.Model           // GateDisposition, GateOutcome

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    type ScopeSelector =
        | ExplicitPaths of GovernedPath list
        | Since of rev: string
        | DefaultRange

    type OutputFormat =
        | Text
        | Json

    type RunRequest =
        { Repo: string
          Scope: ScopeSelector
          Profile: Profile
          Format: OutputFormat
          VerifyOut: string
          StorePath: string
          PersistStore: bool }

    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths
        | UnrecognizedProfile of string

    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    type ArtifactKind =
        | VerifyArtifact

    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | PersistStore of path: string * content: string
        | ExecuteGates of (GateId * GateCommand) list
        | EmitSummary of text: string

    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        | StorePersisted of Result<unit, string>
        | GatesExecuted of (GateId * CommandRecord) list
        | Emitted

    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    type Phase =
        | Parsed
        | Sensed'
        | Loaded'
        | Selected
        | Rolled
        | Persisted
        | Done

    type Model =
        { Request: RunRequest
          Phase: Phase
          Candidates: GovernedPath list option
          Decision: ShipDecision option
          VerifyDoc: string option
          Snapshot: RepoSnapshot option
          SelectedGates: Gate list
          Sensed: SensedFacts option
          Store: ReuseStore option
          Tooling: ToolingFacts option
          Outcomes: (GateId * GateOutcome) list
          CurrencyNotes: string list
          StoreDegraded: bool
          PersistAcked: bool
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    // ── exitCode — total, no wildcard; `Blocked` 1 reserved for an unmet effective-blocking check ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
        | Blocked -> 1
        | UsageError' -> 2
        | InputUnavailable -> 3
        | ToolError -> 4

    // ── parse — a pure, total argv matcher; usage problems are values, never throws ──

    // Hidden accumulator (absent from Loop.fsi). `Paths = Some []` marks an explicit but empty `--paths`
    // (an EmptyPaths usage error); `Paths = None` means no `--paths` flag was given.
    type ParseAcc =
        { Repo: string option
          Paths: string list option
          Since: string option
          Profile: string option
          Json: bool
          VerifyOut: string option
          Store: string option
          Persist: bool }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Profile = None
          Json = false
          VerifyOut = None
          Store = None
          Persist = false }

    // Join a repo dir with a default relative artifact location. A `.` (or empty) repo yields the clean
    // relative form (`readiness/verify.json`); any other repo is prefixed so the artifact lands inside it.
    // Pure string composition — no filesystem, no clock, no absolute-path resolution.
    let under (repo: string) (rel: string) : string =
        if repo = "." || repo = "" then rel else repo.TrimEnd('/') + "/" + rel

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // Tolerate (and drop) a leading `verify` verb — the verb this command implements.
        let tokens =
            match argv with
            | "verify" :: rest -> rest
            | other -> other

        // Greedily consume the non-flag tokens following `--paths`.
        let rec takePaths (acc: string list) (rest: string list) =
            match rest with
            | t :: more when not (t.StartsWith "--") -> takePaths (t :: acc) more
            | _ -> List.rev acc, rest

        let rec go (acc: ParseAcc) (rest: string list) : Result<ParseAcc, UsageError> =
            match rest with
            | [] -> Ok acc
            | "--repo" :: v :: more -> go { acc with Repo = Some v } more
            | "--repo" :: [] -> Error(MissingValue "--repo")
            | "--since" :: v :: more -> go { acc with Since = Some v } more
            | "--since" :: [] -> Error(MissingValue "--since")
            | "--profile" :: v :: more -> go { acc with Profile = Some v } more
            | "--profile" :: [] -> Error(MissingValue "--profile")
            | "--verify-out" :: v :: more -> go { acc with VerifyOut = Some v } more
            | "--verify-out" :: [] -> Error(MissingValue "--verify-out")
            | "--store" :: v :: more -> go { acc with Store = Some v } more
            | "--store" :: [] -> Error(MissingValue "--store")
            | "--json" :: more -> go { acc with Json = true } more
            | "--persist-store" :: more -> go { acc with Persist = true } more
            | "--paths" :: more ->
                let paths, after = takePaths [] more
                go { acc with Paths = Some paths } after
            // NO `--mode` flag (FR-017): it falls through to UnknownFlag, like any other unknown flag.
            | other :: _ -> Error(UnknownFlag other)

        match go emptyAcc tokens with
        | Error e -> Error e
        | Ok acc ->
            match acc.Paths, acc.Since with
            | Some _, Some _ -> Error PathsAndSinceTogether
            | Some [], None -> Error EmptyPaths
            | scopeChoice ->
                // Recognize the profile IN parse: an unrecognized value is a UsageError decided BEFORE any
                // port is built, so a typo writes no artifact.
                let profileResult =
                    match acc.Profile with
                    | None -> Ok Standard
                    | Some raw ->
                        match recognizeProfile raw with
                        | Recognized p -> Ok p
                        | Unrecognized s -> Error(UnrecognizedProfile s)

                match profileResult with
                | Error e -> Error e
                | Ok profile ->
                    let repo = acc.Repo |> Option.defaultValue "."

                    let scope =
                        match scopeChoice with
                        | Some paths, None -> ExplicitPaths(paths |> List.map normalizePath)
                        | None, Some rev -> Since rev
                        | _ -> DefaultRange

                    Ok
                        { Repo = repo
                          Scope = scope
                          Profile = profile
                          Format = (if acc.Json then Json else Text)
                          VerifyOut = acc.VerifyOut |> Option.defaultValue (under repo "readiness/verify.json")
                          StorePath = acc.Store |> Option.defaultValue (under repo "readiness/evidence-reuse.json")
                          PersistStore = acc.Persist }

    // ── init (Principle IV) — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Candidates = None
              Decision = None
              VerifyDoc = None
              Snapshot = None
              SelectedGates = []
              Sensed = None
              Store = None
              Tooling = None
              Outcomes = []
              CurrencyNotes = []
              StoreDegraded = false
              PersistAcked = false
              Diagnostics = []
              Exit = Success }

        match request.Scope with
        // ExplicitPaths bypasses git diff entirely: set candidates here and go straight to the catalog load.
        | ExplicitPaths paths -> { model with Candidates = Some paths }, [ LoadCatalog request.Repo ]
        | Since _
        | DefaultRange -> model, [ SenseScope request.Scope ]

    // ── update — the whole composition; TOTAL, never throws ──

    // Short-circuit to Done with a mapped ExitDecision + an actionable diagnostic (no clock/abs-path/env).
    let fail (category: ExitDecision) (message: string) (model: Model) : Model * Effect list =
        { model with
            Phase = Done
            Exit = category
            Diagnostics = model.Diagnostics @ [ { Category = category; Message = message } ] },
        []

    let describeInvalid (diags: FS.GG.Governance.Config.Model.Diagnostic list) : string =
        let one (d: FS.GG.Governance.Config.Model.Diagnostic) =
            sprintf "%s (%s)" d.Message (diagnosticIdToken d.Id)

        match diags with
        | [] -> "catalog invalid"
        | _ -> "catalog invalid: " + (diags |> List.map one |> String.concat "; ")

    // Map the decision's typed ExitCodeBasis to the process-level ExitDecision.
    let exitFromBasis (basis: ExitCodeBasis) : ExitDecision =
        match basis with
        | Clean -> Success
        | ExitCodeBasis.Blocked -> Blocked

    // ── F046 cache-eligibility helpers (pure; the degrade policy lives here, not in the sensing edge) ──

    /// The all-`None`/empty `SensedFacts` substituted when freshness sensing fails (every gate resolves
    /// unresolved ⇒ recompute-by-default). NEVER fabricates a sensed value.
    let emptySensedFacts: SensedFacts =
        { RuleHash = None
          GeneratorVersion = None
          Base = None
          Head = None
          CoveredArtifacts = Map.empty
          CommandVersions = Map.empty }

    let revOfCommit (CommitId c) = Revision c

    let baseHeadOf (model: Model) : Revision option * Revision option =
        match model.Snapshot |> Option.bind (fun s -> s.Range) with
        | Some r -> Some(revOfCommit r.Base), Some(revOfCommit r.Head)
        | None -> None, None

    // ── F048 persistence (pure; the decision lives here, not at the write edge) ──

    // The persisted document: F047's prune → bound → serialise pipeline over the LOADED store, verbatim. No
    // reuse policy / bound of our own. Decoupled from the current run's verdict: this feeds only the NEXT
    // run's file and never perturbs the verify verdict or exit code.
    let persistedContent (loaded: ReuseStore) : string =
        loaded
        |> EvidenceReuseStore.prune
        |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
        |> EvidenceReuseStore.serialise

    // Whether the summary must wait for a store-write ack: persistence is enabled, the load did NOT degrade,
    // and no ack has arrived yet.
    let awaitingPersist (model: Model) : bool =
        model.Request.PersistStore && not model.StoreDegraded && not model.PersistAcked

    // ── F052 verdict relocation — the ONE verdict change; Ship.rollup is used VERBATIM, never edited ──

    let applyExecution (passedGateIds: Set<GateId>) (decision: ShipDecision) : ShipDecision =
        // A passing command-gate is relocated out of Blockers/Warnings into Passing (findings never move);
        // then Verdict/ExitCodeBasis are recomputed from the remaining blockers — Ship's OWN rule, re-applied.
        let isPassingGate (item: EnforcedItem) =
            match item.Id with
            | GateItem g -> Set.contains g passedGateIds
            | FindingItem _ -> false

        let blockersKept, blockersMoved = decision.Blockers |> List.partition (isPassingGate >> not)
        let warningsKept, warningsMoved = decision.Warnings |> List.partition (isPassingGate >> not)
        let passing' = decision.Passing @ blockersMoved @ warningsMoved
        let verdict' = if List.isEmpty blockersKept then Pass else Fail

        let basis' =
            match verdict' with
            | Pass -> Clean
            | Fail -> ExitCodeBasis.Blocked

        { decision with
            Verdict = verdict'
            Blockers = blockersKept
            Warnings = warningsKept
            Passing = passing'
            ExitCodeBasis = basis' }

    // ── F052 per-gate classification (pure; recomputable from the model) ──

    type GateClassification =
        | ToExecute of GateCommand
        | ToReuse of ExitCode
        | NoCommand

    let executionPlan (model: Model) : (Gate * GateClassification) list * Map<string, FreshnessInputs> =
        match model.Sensed, model.Store with
        | Some sensed, Some store ->
            let resReport = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries resReport |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store

            let verdictMap =
                CacheEligibility.entries cacheReport
                |> List.fold
                    (fun m e ->
                        let k = gateIdValue e.Gate
                        if Map.containsKey k m then m else Map.add k e.Verdict m)
                    Map.empty

            let inputsMap =
                candidates
                |> List.fold
                    (fun m c ->
                        let k = gateIdValue c.Gate
                        if Map.containsKey k m then m else Map.add k c.Inputs m)
                    Map.empty

            let classify (gate: Gate) : GateClassification =
                let cmdOpt =
                    match model.Tooling with
                    | Some tooling -> Plan.commandFor model.Request.Repo tooling gate
                    | None -> None

                match cmdOpt with
                | None -> NoCommand
                | Some cmd ->
                    match Map.tryFind (gateIdValue gate.Id) verdictMap with
                    | Some(Reusable ref) ->
                        match Plan.priorExitOf ref with
                        | Some priorExit -> ToReuse priorExit
                        | None -> ToExecute cmd
                    | _ -> ToExecute cmd

            (model.SelectedGates |> List.map (fun g -> g, classify g)), inputsMap
        | _ -> [], Map.empty

    // Fires once BOTH the sensed facts and the store have arrived: classify the selected gates and request
    // the run of the must-recompute command-gates through the injected F051 port. Reused/no-command gates
    // spawn nothing. Capture, projection, the verdict relocation, and the persist-grown-store effect all wait
    // for `GatesExecuted`.
    let tryExecute (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Decision with
        | Some _, Some _, Some _ ->
            let plan, _ = executionPlan model

            let toExecute =
                plan
                |> List.choose (fun (g, c) ->
                    match c with
                    | ToExecute cmd -> Some(g.Id, cmd)
                    | ToReuse _
                    | NoCommand -> None)

            model, [ ExecuteGates toExecute ]
        | _ -> model, []

    // On `GatesExecuted`: fold F049 `capture` per executed gate (grows the store), build the per-gate
    // `GateOutcome`s, RELOCATE passing command-gates in the verdict (`applyExecution`), project verify.json
    // WITH the execution embed over the RELOCATED decision (cache report over the LOADED pre-run store), emit
    // the write, and persist the GROWN store. The relocated decision's `ExitCodeBasis` governs the exit.
    let projectExecuted (records: (GateId * CommandRecord) list) (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Decision with
        | Some sensed, Some store, Some decision ->
            let plan, inputsMap = executionPlan model

            let recordMap =
                records |> List.fold (fun m (gid, r) -> Map.add (gateIdValue gid) r m) Map.empty

            let grownStore =
                plan
                |> List.fold
                    (fun s (g, c) ->
                        match c with
                        | ToExecute _ ->
                            match Map.tryFind (gateIdValue g.Id) recordMap, Map.tryFind (gateIdValue g.Id) inputsMap with
                            | Some record, Some inputs -> EvidenceCapture.capture inputs record s
                            | _ -> s
                        | ToReuse _
                        | NoCommand -> s)
                    store

            let outcomes =
                plan
                |> List.map (fun (g, c) ->
                    let outcome =
                        match c with
                        | ToExecute _ ->
                            match Map.tryFind (gateIdValue g.Id) recordMap with
                            | Some record ->
                                let code = record.Reproducible.ExitCode

                                { GateId = g.Id
                                  Disposition = Executed
                                  ExitCode = Some code
                                  Passed = Some(Plan.passed code) }
                            | None ->
                                { GateId = g.Id
                                  Disposition = NotExecuted
                                  ExitCode = None
                                  Passed = None }
                        | ToReuse code ->
                            { GateId = g.Id
                              Disposition = Reused
                              ExitCode = Some code
                              Passed = Some(Plan.passed code) }
                        | NoCommand ->
                            { GateId = g.Id
                              Disposition = NotExecuted
                              ExitCode = None
                              Passed = None }

                    g.Id, outcome)

            // The verdict relocation: a PASSING command-gate is moved to `Passing` and the verdict/exit
            // recomputed. A failing, no-command, or uncertain gate is never in this set, so it keeps its
            // rollup treatment (FR-005: an uncertain result is never coerced to pass).
            let passedGateIds =
                outcomes
                |> List.choose (fun (gid, o) -> if o.Passed = Some true then Some gid else None)
                |> Set.ofList

            let relocated = applyExecution passedGateIds decision

            let resReport = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries resReport |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store
            let verifyDoc = VerifyJson.ofVerifyDecision relocated (Some cacheReport) outcomes

            let persistEffects, persistNotes =
                match model.Request.PersistStore, model.StoreDegraded with
                | true, false -> [ PersistStore(model.Request.StorePath, persistedContent grownStore) ], []
                | true, true ->
                    [],
                    [ "currency note: store not persisted: on-disk store failed to parse; left untouched" ]
                | false, _ -> [], []

            { model with
                Phase = Rolled
                Decision = Some relocated
                VerifyDoc = Some verifyDoc
                Outcomes = outcomes
                CurrencyNotes = model.CurrencyNotes @ persistNotes },
            WriteArtifact(VerifyArtifact, model.Request.VerifyOut, verifyDoc) :: persistEffects
        | _ -> model, []

    let rec update (msg: Msg) (model: Model) : Model * Effect list =
        // Once the pipeline has decided (Done), every further reified Msg is inert.
        if model.Phase = Done then
            model, []
        else
            match msg with
            | Begin -> model, []

            | Sensed(Ok snapshot) ->
                let candidates = snapshot.Changed |> List.map (fun c -> c.Path)

                { model with
                    Phase = Sensed'
                    Candidates = Some candidates
                    Snapshot = Some snapshot },
                [ LoadCatalog model.Request.Repo ]

            | Sensed(Error reason) -> fail InputUnavailable ("git sensing unavailable: " + reason) model

            | Loaded(Invalid diags) -> fail InputUnavailable (describeInvalid diags) model

            | Loaded(Valid facts) ->
                // The composition: re-derive/re-sort/re-classify/re-serialize nothing — carry the cores'
                // values verbatim. The verdict is decided here (`Ship.rollup` at `RunMode.Verify`). Select the
                // gates to sense, then request the two cache senses — UNLESS the selection is empty, in which
                // case short-circuit to a passing "nothing to verify" verdict (FR-012) with no freshness/store/
                // execute work: project verify.json now and emit the single write.
                let candidates = model.Candidates |> Option.defaultValue []
                let report = Routing.route facts candidates
                let registry = Gates.buildRegistry facts
                let findings = Findings.findUnknownGovernedPaths facts report
                let result = Route.select registry report findings
                let decision = Ship.rollup result Verify model.Request.Profile
                let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)

                if List.isEmpty selectedGates then
                    let verifyDoc = VerifyJson.ofVerifyDecision decision None []

                    { model with
                        Phase = Rolled
                        Decision = Some decision
                        VerifyDoc = Some verifyDoc
                        SelectedGates = []
                        Tooling = facts.Tooling
                        PersistAcked = true },
                    [ WriteArtifact(VerifyArtifact, model.Request.VerifyOut, verifyDoc) ]
                else
                    { model with
                        Phase = Selected
                        Decision = Some decision
                        SelectedGates = selectedGates
                        Tooling = facts.Tooling },
                    [ SenseFreshness(selectedGates, baseHeadOf model)
                      LoadStore model.Request.StorePath ]

            // F046: a sensed/store result feeds the pure join. An `Error` DEGRADES to a safe default + a
            // non-fatal currency note — it NEVER fails the command, never perturbs the verdict, never changes
            // the exit code.
            | FreshnessSensed(Ok facts) -> tryExecute { model with Sensed = Some facts }

            | FreshnessSensed(Error reason) ->
                tryExecute
                    { model with
                        Sensed = Some emptySensedFacts
                        CurrencyNotes =
                            model.CurrencyNotes
                            @ [ "currency note: freshness facts could not be sensed (" + reason + "); affected gates are recompute-by-default and reported as not-evaluated" ] }

            | StoreLoaded(Ok store) -> tryExecute { model with Store = Some store }

            | StoreLoaded(Error reason) ->
                // F048: mark the load degraded so the persist write is suppressed (don't clobber a malformed
                // file). The F046 degrade-to-empty + note is unchanged.
                tryExecute
                    { model with
                        Store = Some EvidenceReuse.empty
                        StoreDegraded = true
                        CurrencyNotes =
                            model.CurrencyNotes
                            @ [ "currency note: reuse store unreadable (" + reason + "); treated as empty — every gate is recompute-by-default" ] }

            | Wrote(_, Error reason) ->
                // A write failure is ALWAYS a ToolError, NEVER a blocked verdict.
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(_, Ok()) ->
                // F048: when persistence is enabled and not degraded, the summary waits for the store-write
                // ack (`StorePersisted`) instead of emitting on the verify write.
                if awaitingPersist model then
                    { model with Phase = Persisted }, []
                else
                    { model with Phase = Persisted }, [ EmitSummary(render model model.Request.Format) ]

            // F048: the NON-FATAL store-write ack. An `Error` appends a currency note; NEITHER outcome changes
            // `Exit` (it stays governed solely by `ExitCodeBasis` at `Emitted`) nor the already-emitted verify
            // doc. Once the write is done (Phase = Persisted) the summary is emitted; otherwise it waits.
            | StorePersisted result ->
                let notes =
                    match result with
                    | Ok() -> model.CurrencyNotes
                    | Error reason ->
                        model.CurrencyNotes
                        @ [ "currency note: store not persisted (" + reason + "); run unaffected" ]

                let model = { model with PersistAcked = true; CurrencyNotes = notes }

                match model.Phase with
                | Persisted -> model, [ EmitSummary(render model model.Request.Format) ]
                | _ -> model, []

            // F052: the executed gates' records arrive — capture each, build outcomes, RELOCATE passing
            // command-gates in the verdict, project verify.json with the execution embed, and persist the
            // GROWN store. The relocated decision's `ExitCodeBasis` then governs the terminal exit.
            | GatesExecuted records -> projectExecuted records model

            | Emitted ->
                // The verdict is information until the very end: only the terminal exit category differs
                // between a pass and a fail. Map the decision's basis here.
                let exit =
                    model.Decision
                    |> Option.map (fun d -> exitFromBasis d.ExitCodeBasis)
                    |> Option.defaultValue Success

                { model with Phase = Done; Exit = exit }, []

    // ── render — the deterministic summary ──

    and severityToken (s: Severity) : string =
        match s with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    and pathValue (GovernedPath p) = p

    and itemLine (item: EnforcedItem) : string =
        let identity =
            match item.Id with
            | GateItem g -> sprintf "gate %s" (gateIdValue g)
            | FindingItem(fid, path) -> sprintf "finding %s <- %s" (findingIdToken fid) (pathValue path)

        sprintf "  %s   (base %s, effective %s)" identity (severityToken item.Decision.BaseSeverity) (severityToken item.Decision.EffectiveSeverity)

    and section (name: string) (items: EnforcedItem list) : string list =
        match items with
        | [] -> [ sprintf "%s: none" name ]
        | xs -> (sprintf "%s: %d" name (List.length xs)) :: (xs |> List.map itemLine)

    and isFinding (item: EnforcedItem) : bool =
        match item.Id with
        | FindingItem _ -> true
        | GateItem _ -> false

    // ── the gate → enforcement-assigned effective severity map (data-model §6) — a currency finding carries
    //    its owning gate's effective severity, read from the matching `EnforcedItem.Decision` in the decision
    //    partition. Verify builds NO new EnforcedItem and adds NO new severity path. ──

    and gateSeverityMap (model: Model) : Map<string, Severity> =
        match model.Decision with
        | None -> Map.empty
        | Some decision ->
            [ decision.Blockers; decision.Warnings; decision.Passing ]
            |> List.concat
            |> List.fold
                (fun m item ->
                    match item.Id with
                    | GateItem g ->
                        let k = gateIdValue g
                        if Map.containsKey k m then m else Map.add k item.Decision.EffectiveSeverity m
                    | FindingItem _ -> m)
                Map.empty

    and gateSeverityTag (sevMap: Map<string, Severity>) (gate: string) : string =
        match Map.tryFind gate sevMap with
        | Some s -> sprintf " [%s]" (severityToken s)
        | None -> ""

    // ── F046 currency findings (the first-class projection; recomputed purely from sensed/store/gates) ──

    and cacheEntriesOf (model: Model) : CacheEligibilityEntry list =
        match model.Sensed, model.Store with
        | Some sensed, Some store ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            CacheEligibility.evaluate candidates store |> CacheEligibility.entries
        | _ -> []

    and unresolvedEntriesOf (model: Model) : (string * string list) list =
        match model.Sensed with
        | Some sensed ->
            FreshnessResolution.resolve model.SelectedGates sensed
            |> FreshnessResolution.entries
            |> List.filter (fun e -> not (FreshnessResolution.isResolved e.Outcome))
            |> List.map (fun e -> gateIdValue e.Gate, FreshnessResolution.missingFacts e.Outcome |> List.map FreshnessResolution.missingFactToken)
        | None -> []

    and currencyCauseHuman (cause: RecomputeCause) : string =
        match cause with
        | NoPriorEvidence -> "noPriorEvidence"
        | InputsChanged cats -> "inputsChanged: " + (cats |> List.map categoryToken |> String.concat ",")

    and currencyLinesOf (model: Model) : string list =
        let entries = cacheEntriesOf model
        let unresolved = unresolvedEntriesOf model
        let sevMap = gateSeverityMap model

        let reusable =
            entries
            |> List.choose (fun e ->
                match e.Verdict with
                | Reusable ref -> Some(gateIdValue e.Gate, EvidenceReuse.referenceValue ref)
                | MustRecompute _ -> None)

        let recompute =
            entries
            |> List.choose (fun e ->
                match e.Verdict with
                | MustRecompute cause -> Some(gateIdValue e.Gate, currencyCauseHuman cause)
                | Reusable _ -> None)

        let header =
            sprintf "currency: %d fresh/reused, %d stale/recomputed, %d recompute-by-default" reusable.Length recompute.Length unresolved.Length

        let block (title: string) (lines: string list) =
            match lines with
            | [] -> [ title + " none" ]
            | _ -> title :: lines

        let reusableLines =
            reusable |> List.map (fun (g, r) -> sprintf "  %s <- %s%s" g r (gateSeverityTag sevMap g))

        let recomputeLines =
            recompute |> List.map (fun (g, c) -> sprintf "  %s   (%s)%s" g c (gateSeverityTag sevMap g))

        let unresolvedLines =
            unresolved
            |> List.map (fun (g, facts) -> sprintf "  %s   missing: %s%s" g (String.concat "," facts) (gateSeverityTag sevMap g))

        [ header
          yield! block "fresh/reused:" reusableLines
          yield! block "stale/recomputed:" recomputeLines
          yield! block "recompute by default (unresolved):" unresolvedLines
          yield! model.CurrencyNotes ]

    // ── F052 execution summary — which gates were executed / reused / not-executed, pass/fail ──

    and dispositionWord (d: GateDisposition) : string =
        match d with
        | Executed -> "executed"
        | Reused -> "reused"
        | NotExecuted -> "not-executed"

    and executionLinesOf (model: Model) : string list =
        match model.Outcomes with
        | [] -> [ "execution: none" ]
        | outcomes ->
            let line (gid, (o: GateOutcome)) =
                let verdict =
                    match o.Passed with
                    | Some true -> "pass"
                    | Some false ->
                        match o.ExitCode with
                        | Some(ExitCode code) when code = 124 -> "fail (timeout)"
                        | Some(ExitCode code) when code = 127 -> "fail (start-failure)"
                        | Some(ExitCode code) -> sprintf "fail (exit %d)" code
                        | None -> "fail"
                    | None -> "—"

                sprintf "  %s   (%s, %s)" (gateIdValue gid) (dispositionWord o.Disposition) verdict

            (sprintf "execution: %d gate(s)" (List.length outcomes)) :: (outcomes |> List.map line)

    and renderText (model: Model) : string =
        match model.Decision with
        | None ->
            model.Diagnostics
            |> List.map (fun d -> "error: " + d.Message)
            |> String.concat "\n"
        | Some decision ->
            let verdictToken =
                match decision.Verdict with
                | Pass -> "pass"
                | Fail -> "blocked"

            let basisToken =
                match decision.ExitCodeBasis with
                | Clean -> "clean"
                | ExitCodeBasis.Blocked -> "blocked"

            let allFindings =
                [ decision.Blockers; decision.Warnings; decision.Passing ]
                |> List.concat
                |> List.filter isFinding

            // "nothing to verify" (FR-012): an empty selection with no findings — the change touches no
            // governed path / selects no profile-appropriate check.
            if List.isEmpty model.SelectedGates && List.isEmpty allFindings then
                [ sprintf "verify: verdict %s (exit-code basis: %s)" verdictToken basisToken
                  ""
                  "nothing to verify (no profile-appropriate checks for this change)"
                  ""
                  sprintf "wrote %s    (%s)" model.Request.VerifyOut VerifyJson.schemaVersion ]
                |> String.concat "\n"
            else
                let header = sprintf "verify: verdict %s (exit-code basis: %s)" verdictToken basisToken

                let findingLines =
                    match allFindings with
                    | [] -> [ "findings: none" ]
                    | fs -> (sprintf "findings: %d" (List.length fs)) :: (fs |> List.map itemLine)

                [ [ header; "" ]
                  section "blockers" decision.Blockers
                  section "warnings" decision.Warnings
                  section "passing" decision.Passing
                  [ "" ]
                  findingLines
                  [ "" ]
                  currencyLinesOf model
                  [ "" ]
                  executionLinesOf model
                  [ ""; sprintf "wrote %s    (%s)" model.Request.VerifyOut VerifyJson.schemaVersion ] ]
                |> List.concat
                |> String.concat "\n"

    // The Json form IS the F056 `verify.json` document verbatim (FR-007): `--json` stdout equals the
    // persisted file byte-for-byte. The text form is suppressed under `Json`.
    and renderJson (model: Model) : string =
        match model.VerifyDoc with
        | Some doc -> doc
        | None ->
            let errs =
                model.Diagnostics
                |> List.map (fun d -> System.Text.Json.JsonSerializer.Serialize d.Message)
                |> String.concat ","

            sprintf "{\"errors\":[%s]}" errs

    and render (model: Model) (format: OutputFormat) : string =
        match format with
        | Text -> renderText model
        | Json -> renderJson model
