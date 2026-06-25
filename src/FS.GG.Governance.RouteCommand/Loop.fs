// The PURE MVU core of the `fsgg route` host command (F022). Visibility lives in Loop.fsi
// (Principle II) — this file carries NO top-level access modifiers; helper types/functions absent
// from the signature are hidden by it. `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O,
// NO git, NO clock: the whole scope -> load -> route -> registry -> findings -> select -> project ->
// persist-plan -> summarize -> exit composition is a pure transition over Model + Msg emitting
// Effect data the edge Interpreter executes (Principle IV). It re-derives/re-sorts/re-classifies
// nothing the eight cores fixed, and computes NO ship verdict (FR-008).

namespace FS.GG.Governance.RouteCommand

open FS.GG.Governance.Config.Model       // GovernedPath, Validation, Valid/Invalid, Cost, normalizePath, diagnosticIdToken
open FS.GG.Governance.Snapshot.Model      // RepoSnapshot, ChangedPath, DiffRange, CommitId
open FS.GG.Governance.Routing             // Routing.route
open FS.GG.Governance.Findings            // Findings.findUnknownGovernedPaths
open FS.GG.Governance.Gates               // Gates.buildRegistry, gateIdValue
open FS.GG.Governance.Gates.Model         // Gate, GateId
open FS.GG.Governance.Route               // Route.select
open FS.GG.Governance.Route.Model          // RouteResult, SelectedGate, SelectingPath, CostRollup
open FS.GG.Governance.ProductSurfaces       // ProductSurfaces.classify (F23 — edge-side product-surface classification)
open FS.GG.Governance.ProductSurfaces.Model  // ProductSurfaceReport, ProductClassification, TierAlternative
open FS.GG.Governance.RouteJson           // RouteJson.ofRouteResult, schemaVersion
open FS.GG.Governance.GatesJson           // GatesJson.ofGateRegistry, schemaVersion
open FS.GG.Governance.HumanText           // F27 wiring (063): HumanText.ofRouteResult — the plain projection
// F046 cache-eligibility pipeline (sense → resolve → evaluate → embed Some report)
open FS.GG.Governance.FreshnessKey.Model   // Revision, categoryToken
open FS.GG.Governance.FreshnessResolution  // resolve, entries, candidate, isResolved, missingFacts, missingFactToken
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts, FreshnessResolutionEntry
open FS.GG.Governance.CacheEligibility      // evaluate, entries
open FS.GG.Governance.CacheEligibility.Model // CandidateGate, CacheEligibilityEntry, CacheEligibilityVerdict, Reusable, MustRecompute
open FS.GG.Governance.EvidenceReuse         // empty, referenceValue
open FS.GG.Governance.EvidenceReuse.Model   // ReuseStore, EvidenceRef, RecomputeCause, NoPriorEvidence, InputsChanged
open FS.GG.Governance.EvidenceReuseStore    // F048: prune, retain, serialise, defaultRetentionBound
// F052 gate-execution wiring (classify → run → capture → persist-grown-store; advisory, always exit 0)
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
          Format: OutputFormat
          GatesOut: string
          RouteOut: string
          StorePath: string
          PersistStore: bool
          ExplicitPlain: bool
          Watch: bool }

    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths

    type ExitDecision =
        | Success
        | UsageError'
        | InputUnavailable
        | ToolError

    type ArtifactKind =
        | GatesArtifact
        | RouteArtifact

    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | PersistStore of path: string * content: string
        | ExecuteGates of (GateId * GateCommand) list
        | EmitSummary of text: string * human: (ReportView.ReportView * string) option * explicitPlain: bool

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
        | Projected
        | Persisted
        | Done

    type Model =
        { Request: RunRequest
          Phase: Phase
          Candidates: GovernedPath list option
          Result: RouteResult option
          GatesDoc: string option
          RouteDoc: string option
          Snapshot: RepoSnapshot option
          SelectedGates: Gate list
          Classifications: ProductSurfaceReport
          Sensed: SensedFacts option
          Store: ReuseStore option
          Tooling: ToolingFacts option
          Outcomes: (GateId * GateOutcome) list
          CacheNotes: string list
          StoreDegraded: bool
          PersistAcked: bool
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    // ── exitCode (research D6) — total, no wildcard, no GovernedBlocking code (FR-008) ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
        | UsageError' -> 2
        | InputUnavailable -> 3
        | ToolError -> 4

    // ── parse (research D8) — a pure, total argv matcher; usage problems are values, never throws ──

    // Hidden accumulator (absent from Loop.fsi). `Paths = Some []` marks an explicit but empty
    // `--paths` (an EmptyPaths usage error); `Paths = None` means no `--paths` flag was given.
    type ParseAcc =
        { Repo: string option
          Paths: string list option
          Since: string option
          Json: bool
          GatesOut: string option
          RouteOut: string option
          Store: string option
          Persist: bool
          Plain: bool
          Watch: bool }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Json = false
          GatesOut = None
          RouteOut = None
          Store = None
          Persist = false
          Plain = false
          Watch = false }

    // Join a repo dir with a default relative artifact location. A `.` (or empty) repo yields the
    // clean relative form (`.fsgg/gates.json`); any other repo is prefixed so the artifact lands
    // inside it. Pure string composition — no filesystem, no clock, no absolute-path resolution.
    let under (repo: string) (rel: string) : string =
        if repo = "." || repo = "" then rel else repo.TrimEnd('/') + "/" + rel

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // Tolerate (and drop) a leading `route` verb — the only subcommand this tool ships.
        let tokens =
            match argv with
            | "route" :: rest -> rest
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
            | "--gates-out" :: v :: more -> go { acc with GatesOut = Some v } more
            | "--gates-out" :: [] -> Error(MissingValue "--gates-out")
            | "--route-out" :: v :: more -> go { acc with RouteOut = Some v } more
            | "--route-out" :: [] -> Error(MissingValue "--route-out")
            | "--store" :: v :: more -> go { acc with Store = Some v } more
            | "--store" :: [] -> Error(MissingValue "--store")
            | "--json" :: more -> go { acc with Json = true } more
            | "--plain" :: more -> go { acc with Plain = true } more
            | "--watch" :: more -> go { acc with Watch = true } more
            | "--persist-store" :: more -> go { acc with Persist = true } more
            | "--paths" :: more ->
                let paths, after = takePaths [] more
                go { acc with Paths = Some paths } after
            | other :: _ -> Error(UnknownFlag other)

        match go emptyAcc tokens with
        | Error e -> Error e
        | Ok acc ->
            match acc.Paths, acc.Since with
            | Some _, Some _ -> Error PathsAndSinceTogether
            | Some [], None -> Error EmptyPaths
            | scopeChoice ->
                let repo = acc.Repo |> Option.defaultValue "."

                let scope =
                    match scopeChoice with
                    | Some paths, None -> ExplicitPaths(paths |> List.map normalizePath)
                    | None, Some rev -> Since rev
                    | _ -> DefaultRange

                Ok
                    { Repo = repo
                      Scope = scope
                      Format = (if acc.Json then Json else Text)
                      GatesOut = acc.GatesOut |> Option.defaultValue (under repo ".fsgg/gates.json")
                      RouteOut = acc.RouteOut |> Option.defaultValue (under repo "readiness/route.json")
                      StorePath = acc.Store |> Option.defaultValue (under repo "readiness/evidence-reuse.json")
                      PersistStore = acc.Persist
                      ExplicitPlain = acc.Plain
                      Watch = acc.Watch }

    // ── init (Principle IV) — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Candidates = None
              Result = None
              GatesDoc = None
              RouteDoc = None
              Snapshot = None
              SelectedGates = []
              Classifications = { Classifications = [] }
              Sensed = None
              Store = None
              Tooling = None
              Outcomes = []
              CacheNotes = []
              StoreDegraded = false
              PersistAcked = false
              Diagnostics = []
              Exit = Success }

        match request.Scope with
        // ExplicitPaths bypasses git diff entirely (research D4): set candidates here and go straight
        // to the catalog load — the faked git Ports is never consulted for a diff (US2 AS1).
        | ExplicitPaths paths -> { model with Candidates = Some paths }, [ LoadCatalog request.Repo ]
        | Since _
        | DefaultRange -> model, [ SenseScope request.Scope ]

    // ── update — the whole composition; TOTAL, never throws (FR-004/FR-013) ──

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

    // ── F046 cache-eligibility helpers (pure; the degrade policy lives here, not in the sensing edge — D2) ──

    /// The all-`None`/empty `SensedFacts` substituted when freshness sensing fails (every gate resolves
    /// unresolved ⇒ `notEvaluated`). NEVER fabricates a sensed value (D2/L3).
    let emptySensedFacts: SensedFacts =
        { RuleHash = None
          GeneratorVersion = None
          Base = None
          Head = None
          CoveredArtifacts = Map.empty
          CommandVersions = Map.empty }

    // Base/head taken FROM the snapshot range (D5) — never re-sensed, never fabricated. `Range = None`
    // (e.g. ExplicitPaths) ⇒ both `None` ⇒ every gate unresolved on base/head (L2).
    let revOfCommit (CommitId c) = Revision c

    let baseHeadOf (model: Model) : Revision option * Revision option =
        match model.Snapshot |> Option.bind (fun s -> s.Range) with
        | Some r -> Some(revOfCommit r.Base), Some(revOfCommit r.Head)
        | None -> None, None

    // ── F048 persistence (pure; the decision lives here, not at the write edge — FR-010/D2) ──

    // The persisted document: F047's prune → bound → serialise pipeline over the LOADED store, verbatim
    // (data-model §2). No reuse policy / bound of our own — `defaultRetentionBound` and the three pure
    // operations are F047's exactly. Decoupled from the current run's verdicts (FR-005): this feeds only
    // the NEXT run's file.
    let persistedContent (loaded: ReuseStore) : string =
        loaded
        |> EvidenceReuseStore.prune
        |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
        |> EvidenceReuseStore.serialise

    // Whether the summary must wait for a store-write ack: persistence is enabled, the load did NOT degrade
    // (a degraded load emits no `PersistStore`, so nothing acks), and no ack has arrived yet (D10).
    let awaitingPersist (model: Model) : bool =
        model.Request.PersistStore && not model.StoreDegraded && not model.PersistAcked

    // ── F052 per-gate classification (pure; recomputable from the model — data-model §classification) ──

    // How one selected gate is handled this run (hidden — absent from Loop.fsi). `ToExecute` carries the
    // command-to-run (spawned once); `ToReuse` carries the recovered prior exit (NOT spawned); `NoCommand`
    // declares no command (NotExecuted).
    type GateClassification =
        | ToExecute of GateCommand
        | ToReuse of ExitCode
        | NoCommand

    // Classify every selected gate and surface the per-gate freshness inputs (the F049 capture key). A gate
    // with no declared command ⇒ `NoCommand`; with a command and an `isReusable` verdict whose prior exit is
    // recoverable ⇒ `ToReuse` (the cache payoff — FR-003); otherwise (must-recompute, OR reusable-but-
    // unrecoverable — D2/FR-004) ⇒ `ToExecute`. PURE: no process, derived entirely from the model.
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

    // Fires once BOTH the sensed facts and the store have arrived (the existing join point): classify the
    // selected gates and request the run of the must-recompute command-gates through the injected F051 port
    // (D5). Reused/no-command gates spawn nothing. Capture, projection, and the persist-grown-store effect
    // all wait for `GatesExecuted`.
    let tryExecute (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Result, model.GatesDoc with
        | Some _, Some _, Some _, Some _ ->
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

    // On `GatesExecuted`: fold F049 `capture` per executed gate into the store (grows it), build the per-gate
    // `GateOutcome`s, project `route.json` WITH the execution embed (cache report over the LOADED pre-run
    // store, unchanged), emit the two writes, and persist the GROWN store (F047/F048 verbatim — FR-010).
    let projectExecuted (records: (GateId * CommandRecord) list) (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Result, model.GatesDoc with
        | Some sensed, Some store, Some result, Some gatesDoc ->
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

            let resReport = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries resReport |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store
            // F23: the additive productSurfaces section (empty ⇒ byte-identical to the F052-era route.json).
            let routeDoc = RouteJson.ofRouteResultWithProductSurfaces result (Some cacheReport) outcomes model.Classifications

            let writes =
                [ WriteArtifact(GatesArtifact, model.Request.GatesOut, gatesDoc)
                  WriteArtifact(RouteArtifact, model.Request.RouteOut, routeDoc) ]

            let persistEffects, persistNotes =
                match model.Request.PersistStore, model.StoreDegraded with
                | true, false -> [ PersistStore(model.Request.StorePath, persistedContent grownStore) ], []
                | true, true ->
                    [],
                    [ "cache note: store not persisted: on-disk store failed to parse; left untouched" ]
                | false, _ -> [], []

            { model with
                Phase = Projected
                RouteDoc = Some routeDoc
                Outcomes = outcomes
                CacheNotes = model.CacheNotes @ persistNotes },
            writes @ persistEffects
        | _ -> model, []

    let rec update (msg: Msg) (model: Model) : Model * Effect list =
        // Once the pipeline has decided (Done), every further reified Msg is inert (FR-013): a
        // batch of write acks after a short-circuit must not resurrect work or re-diagnose.
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
                // The composition (FR-004): re-derive/re-sort/re-classify nothing — carry the cores'
                // values verbatim. The gates document is computed here; the route document waits for the
                // cache-eligibility join (F046). Select the gates to sense, then request the two cache
                // senses (NO write is emitted here anymore — it waits for `tryProject`).
                let candidates = model.Candidates |> Option.defaultValue []
                let report = Routing.route facts candidates
                let registry = Gates.buildRegistry facts
                let findings = Findings.findUnknownGovernedPaths facts report
                let result = Route.select registry report findings
                let gatesDoc = GatesJson.ofGateRegistry registry
                let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)

                // F23: classify the routed paths into product surfaces at the EDGE (not inside a pure
                // `update` body that touches I/O — this is pure). The active profile is the catalog's
                // declared default (or `standard` when no policy is declared).
                let profile = facts.Policy |> Option.map (fun p -> p.DefaultProfile) |> Option.defaultValue (ProfileId "standard")
                let classifications = ProductSurfaces.classify facts report profile

                { model with
                    Phase = Selected
                    Result = Some result
                    GatesDoc = Some gatesDoc
                    SelectedGates = selectedGates
                    Classifications = classifications
                    Tooling = facts.Tooling },
                [ SenseFreshness(selectedGates, baseHeadOf model)
                  LoadStore model.Request.StorePath ]

            // F046: a sensed/store result feeds the pure join. An `Error` DEGRADES to a safe default + a
            // non-fatal cache note (D2) — it NEVER fails the command or changes the exit code (FR-010/FR-011).
            | FreshnessSensed(Ok facts) -> tryExecute { model with Sensed = Some facts }

            | FreshnessSensed(Error reason) ->
                tryExecute
                    { model with
                        Sensed = Some emptySensedFacts
                        CacheNotes =
                            model.CacheNotes
                            @ [ "cache note: freshness facts could not be sensed (" + reason + "); affected gates are recompute-by-default and reported as not-evaluated" ] }

            | StoreLoaded(Ok store) -> tryExecute { model with Store = Some store }

            | StoreLoaded(Error reason) ->
                // F048: mark the load degraded so the persist write is suppressed (don't clobber a
                // malformed file, D6). The F046 degrade-to-empty + note is unchanged.
                tryExecute
                    { model with
                        Store = Some EvidenceReuse.empty
                        StoreDegraded = true
                        CacheNotes =
                            model.CacheNotes
                            @ [ "cache note: reuse store unreadable (" + reason + "); treated as empty — every gate is recompute-by-default" ] }

            | Wrote(_, Error reason) ->
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(_, Ok()) ->
                // Two writes were emitted together; the first ack advances to Persisted, the second
                // (already Persisted) emits the summary. No counter field needed — the Phase carries it.
                // F048: when persistence is enabled and not degraded, the summary waits for the store-write
                // ack (`StorePersisted`) instead of emitting on the second write (D10).
                match model.Phase with
                | Projected -> { model with Phase = Persisted }, []
                | _ ->
                    if awaitingPersist model then
                        model, []
                    else
                        model, [ emitEffect model ]

            // F048: the NON-FATAL store-write ack (FR-006). An `Error` appends a cache note; NEITHER outcome
            // changes `Exit` (set later at `Emitted`) nor the already-emitted route.json/gates.json. Once the
            // writes are done (Phase = Persisted) the summary is emitted; otherwise it waits for them.
            | StorePersisted result ->
                let notes =
                    match result with
                    | Ok() -> model.CacheNotes
                    | Error reason ->
                        model.CacheNotes
                        @ [ "cache note: store not persisted (" + reason + "); run unaffected" ]

                let model = { model with PersistAcked = true; CacheNotes = notes }

                match model.Phase with
                | Persisted -> model, [ emitEffect model ]
                | _ -> model, []

            // F052: the executed gates' records arrive — capture each into the store, build the per-gate
            // outcomes, project route.json WITH the execution embed, and persist the GROWN store. Route stays
            // advisory: `exitCode` is unaffected (always 0 — FR-008).
            | GatesExecuted records -> projectExecuted records model

            | Emitted -> { model with Phase = Done; Exit = Success }, []

    // ── render (research D7) — the deterministic summary, separate from the persisted artifacts ──

    and costToken (c: Cost) : string =
        match c with
        | Cheap -> "cheap"
        | Medium -> "medium"
        | High -> "high"
        | Exhaustive -> "exhaustive"

    and pathValue (GovernedPath p) = p

    and jstr (s: string) = System.Text.Json.JsonSerializer.Serialize s

    // F27 wiring (063): the full CacheEligibilityReport (not just its entries) recomputed purely from the
    // model's sensed facts + loaded store — the same value the route.json embed carries — for the shared
    // HumanText projection. `None` until both senses have arrived.
    and cacheReportOf (model: Model) : CacheEligibilityReport option =
        match model.Sensed, model.Store with
        | Some sensed, Some store ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            Some(CacheEligibility.evaluate candidates store)
        | _ -> None

    // F27 wiring (063): the host operational lines (wrote confirmations) — host output kept around the
    // report projection, never part of the JSON contract (FR-003). Empty when there is no report.
    and operationalLines (model: Model) : string =
        match model.Result with
        | Some result ->
            [ sprintf "wrote %s    (%s)" model.Request.GatesOut GatesJson.schemaVersion
              sprintf "wrote %s    (%s, %d selected)" model.Request.RouteOut RouteJson.schemaVersion result.SelectedGates.Length ]
            |> String.concat "\n"
        | None -> ""

    and renderText (model: Model) : string =
        match model.Result with
        | None ->
            model.Diagnostics
            |> List.map (fun d -> "error: " + d.Message)
            |> String.concat "\n"
        | Some result ->
            // F27 wiring (063): the report facts come from the shared HumanText projection over the SAME
            // RouteResult the *Json path serializes (FR-001); the host keeps only its operational `wrote`
            // lines (never part of the JSON contract — FR-003).
            let projection = HumanText.ofRouteResult result (cacheReportOf model) model.Outcomes
            [ projection; operationalLines model ] |> String.concat "\n"

    // F27 wiring (063): build the emit effect. Json carries the contract string (human = None). Text carries
    // the ANSI-free plain string (used for `Plain`) PLUS the `ReportView` + operational lines (used for the
    // `Rich` path the edge selects); the mode is decided at the edge via `selectMode (senseCapability …)`.
    and emitEffect (model: Model) : Effect =
        match model.Request.Format with
        | Json -> EmitSummary(renderJson model, None, false)
        | Text ->
            match model.Result with
            | Some result ->
                let view = ReportView.viewOfRouteResult result (cacheReportOf model) model.Outcomes
                EmitSummary(renderText model, Some(view, operationalLines model), model.Request.ExplicitPlain)
            | None -> EmitSummary(renderText model, None, model.Request.ExplicitPlain)

    // F27 wiring (063, US3/US4): the report view from a terminal model — the SAME projection the one-shot
    // plain/rich renders use. The read-only watch/tui edges re-render through this. `None` ⇒ no report.
    and humanView (model: Model) : ReportView.ReportView option =
        match model.Result with
        | Some result -> Some(ReportView.viewOfRouteResult result (cacheReportOf model) model.Outcomes)
        | None -> None

    and renderJson (model: Model) : string =
        match model.Result with
        | None ->
            let errs = model.Diagnostics |> List.map (fun d -> jstr d.Message) |> String.concat ","
            sprintf "{\"errors\":[%s]}" errs
        | Some result ->
            let gates =
                result.SelectedGates
                |> List.map (fun sg ->
                    let paths = sg.SelectingPaths |> List.map (fun sp -> jstr (pathValue sp.Path)) |> String.concat ","
                    sprintf "{\"id\":%s,\"cost\":%s,\"paths\":[%s]}" (jstr (gateIdValue sg.Gate.Id)) (jstr (costToken sg.Gate.Cost)) paths)
                |> String.concat ","

            let findings =
                result.Findings.Findings
                |> List.map (fun f -> sprintf "{\"path\":%s,\"message\":%s}" (jstr (pathValue f.Path)) (jstr f.Message))
                |> String.concat ","

            let c = result.Cost

            sprintf
                "{\"selected\":[%s],\"cost\":{\"cheap\":%d,\"medium\":%d,\"high\":%d,\"exhaustive\":%d},\"findings\":[%s],\"wrote\":{\"gates\":%s,\"route\":%s}}"
                gates
                c.Cheap
                c.Medium
                c.High
                c.Exhaustive
                findings
                (jstr model.Request.GatesOut)
                (jstr model.Request.RouteOut)

    and render (model: Model) (format: OutputFormat) : string =
        match format with
        | Text -> renderText model
        | Json -> renderJson model
