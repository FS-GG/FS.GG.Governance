// The PURE MVU core of the `fsgg ship` host command (F026). Visibility lives in Loop.fsi
// (Principle II) — this file carries NO top-level access modifiers; helper types/functions absent
// from the signature are hidden by it. `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O,
// NO git, NO clock: the whole scope -> load -> route -> registry -> findings -> select -> ROLLUP ->
// PROJECT -> persist-plan -> summarize -> EXIT-FROM-BASIS composition is a pure transition over
// Model + Msg emitting Effect data the edge Interpreter executes (Principle IV). It re-derives,
// re-sorts, re-classifies, and re-serializes nothing the nine cores fixed: the verdict comes from
// F024 `Ship.rollup`, the document bytes from F025 `AuditJson.ofShipDecision`, the levers from F023
// `recognizeMode`/`recognizeProfile`. The ONE new-vs-F022 behavior is the `Blocked` exit category.

namespace FS.GG.Governance.ShipCommand

open FS.GG.Governance.Config.Model       // GovernedPath, Validation, Valid/Invalid, normalizePath, diagnosticIdToken
open FS.GG.Governance.Snapshot.Model      // RepoSnapshot, ChangedPath, DiffRange, CommitId
open FS.GG.Governance.Routing             // Routing.route
open FS.GG.Governance.Findings            // Findings.findUnknownGovernedPaths
open FS.GG.Governance.Findings.Model       // findingIdToken
open FS.GG.Governance.Gates               // Gates.buildRegistry
open FS.GG.Governance.Gates.Model          // Gate, gateIdValue
open FS.GG.Governance.Route               // Route.select
open FS.GG.Governance.Enforcement.Enforcement // RunMode, Profile, Severity, Recognized, recognizeMode, recognizeProfile
open FS.GG.Governance.Ship                // Ship.rollup
open FS.GG.Governance.Ship.Model           // ShipDecision, Verdict, ExitCodeBasis, EnforcedItem, EnforcedItemId
open FS.GG.Governance.AuditJson           // AuditJson.ofShipDecision
open FS.GG.Governance.HumanText           // F27 wiring (063): HumanText.ofShipDecision — the plain projection
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
// F25 host wiring (064): the four consumed cores + F033 Provenance (budget filter, kinded runs, two sidecars).
open FS.GG.Governance.CostBudget.Model        // CostBudget, CandidateCost, AgentReviewMark, CacheDecision, BudgetReason, CacheDecisionReport
open FS.GG.Governance.CostBudget.Findings     // CostFinding, EvidenceTaint (Real/Synthetic), cacheFindings, enforce
open FS.GG.Governance.CommandKind.Model       // CommandKind, KindedCommandRun, AuditSnapshot
open FS.GG.Governance.Provenance.Model        // BuilderIdentity

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement // F070: stale-view finding vocabulary + fold

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
          Mode: RunMode
          Profile: Profile
          Format: OutputFormat
          AuditOut: string
          StorePath: string
          PersistStore: bool
          ExplicitPlain: bool
          CostBudgetOut: string
          ProvenanceOut: string }

    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths
        | UnrecognizedMode of string
        | UnrecognizedProfile of string

    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    type ArtifactKind =
        | AuditArtifact
        | CostBudgetArtifact
        | ProvenanceArtifact

    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | PersistStore of path: string * content: string
        | ExecuteGates of (GateId * GateCommand) list
        | SenseProvenance
        | EmitSummary of text: string * human: (ReportView.ReportView * string) option * explicitPlain: bool
        | SenseViewCurrency of repo: string

    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        | StorePersisted of Result<unit, string>
        | GatesExecuted of (GateId * CommandRecord) list
        | ProvenanceSensed of environment: EnvironmentClass * builder: BuilderIdentity
        | ViewCurrencySensed of findings: CE.CurrencyFinding list
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
          AuditDoc: string option
          Snapshot: RepoSnapshot option
          SelectedGates: Gate list
          Sensed: SensedFacts option
          Store: ReuseStore option
          Tooling: ToolingFacts option
          Outcomes: (GateId * GateOutcome) list
          CacheNotes: string list
          StoreDegraded: bool
          PersistAcked: bool
          Environment: EnvironmentClass option
          Builder: BuilderIdentity option
          CacheDecision: CacheDecisionReport option
          Audit: AuditSnapshot option
          ViewCurrencyFindings: CE.CurrencyFinding list
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    // ── exitCode (research D6) — total, no wildcard; `Blocked` 1 reserved for a blocked merge verdict ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
        | Blocked -> 1
        | UsageError' -> 2
        | InputUnavailable -> 3
        | ToolError -> 4

    // ── parse (research D9) — a pure, total argv matcher; usage problems are values, never throws ──

    // Hidden accumulator (absent from Loop.fsi). `Paths = Some []` marks an explicit but empty
    // `--paths` (an EmptyPaths usage error); `Paths = None` means no `--paths` flag was given.
    type ParseAcc =
        { Repo: string option
          Paths: string list option
          Since: string option
          Mode: string option
          Profile: string option
          Json: bool
          AuditOut: string option
          Store: string option
          Persist: bool
          Plain: bool
          CostBudgetOut: string option
          ProvenanceOut: string option }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Mode = None
          Profile = None
          Json = false
          AuditOut = None
          Store = None
          Persist = false
          Plain = false
          CostBudgetOut = None
          ProvenanceOut = None }

    // Join a repo dir with a default relative artifact location. A `.` (or empty) repo yields the
    // clean relative form (`readiness/audit.json`); any other repo is prefixed so the artifact lands
    // inside it. Pure string composition — no filesystem, no clock, no absolute-path resolution.
    let under (repo: string) (rel: string) : string =
        if repo = "." || repo = "" then rel else repo.TrimEnd('/') + "/" + rel

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // Tolerate (and drop) a leading `ship` verb — the verb this command implements.
        let tokens =
            match argv with
            | "ship" :: rest -> rest
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
            | "--mode" :: v :: more -> go { acc with Mode = Some v } more
            | "--mode" :: [] -> Error(MissingValue "--mode")
            | "--profile" :: v :: more -> go { acc with Profile = Some v } more
            | "--profile" :: [] -> Error(MissingValue "--profile")
            | "--audit-out" :: v :: more -> go { acc with AuditOut = Some v } more
            | "--audit-out" :: [] -> Error(MissingValue "--audit-out")
            | "--cost-budget-out" :: v :: more -> go { acc with CostBudgetOut = Some v } more
            | "--cost-budget-out" :: [] -> Error(MissingValue "--cost-budget-out")
            | "--provenance-out" :: v :: more -> go { acc with ProvenanceOut = Some v } more
            | "--provenance-out" :: [] -> Error(MissingValue "--provenance-out")
            | "--store" :: v :: more -> go { acc with Store = Some v } more
            | "--store" :: [] -> Error(MissingValue "--store")
            | "--json" :: more -> go { acc with Json = true } more
            | "--plain" :: more -> go { acc with Plain = true } more
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
                // Recognize the levers IN parse (research D5): an unrecognized value is a UsageError
                // decided BEFORE any port is built, so a typo writes no artifact.
                let modeResult =
                    match acc.Mode with
                    | None -> Ok Gate
                    | Some raw ->
                        match recognizeMode raw with
                        | Recognized m -> Ok m
                        | Unrecognized s -> Error(UnrecognizedMode s)

                let profileResult =
                    match acc.Profile with
                    | None -> Ok Standard
                    | Some raw ->
                        match recognizeProfile raw with
                        | Recognized p -> Ok p
                        | Unrecognized s -> Error(UnrecognizedProfile s)

                match modeResult, profileResult with
                | Error e, _ -> Error e
                | _, Error e -> Error e
                | Ok mode, Ok profile ->
                    let repo = acc.Repo |> Option.defaultValue "."

                    let scope =
                        match scopeChoice with
                        | Some paths, None -> ExplicitPaths(paths |> List.map normalizePath)
                        | None, Some rev -> Since rev
                        | _ -> DefaultRange

                    Ok
                        { Repo = repo
                          Scope = scope
                          Mode = mode
                          Profile = profile
                          Format = (if acc.Json then Json else Text)
                          AuditOut = acc.AuditOut |> Option.defaultValue (under repo "readiness/audit.json")
                          StorePath = acc.Store |> Option.defaultValue (under repo "readiness/evidence-reuse.json")
                          PersistStore = acc.Persist
                          ExplicitPlain = acc.Plain
                          CostBudgetOut = acc.CostBudgetOut |> Option.defaultValue (under repo "readiness/cost-budget.json")
                          ProvenanceOut = acc.ProvenanceOut |> Option.defaultValue (under repo "readiness/provenance.json") }

    // ── init (Principle IV) — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Candidates = None
              Decision = None
              AuditDoc = None
              Snapshot = None
              SelectedGates = []
              Sensed = None
              Store = None
              Tooling = None
              Outcomes = []
              CacheNotes = []
              StoreDegraded = false
              PersistAcked = false
              Environment = None
              Builder = None
              CacheDecision = None
              Audit = None
              ViewCurrencyFindings = []
              Diagnostics = []
              Exit = Success }

        // F25 wiring (064): sense the two normalized provenance facts FIRST, so `Environment`/`Builder` are
        // populated before the executed-gate persist projects the `provenance.json` sidecar. F070: sense
        // generated-view currency in this first batch too, so `ViewCurrencyFindings` is populated before the
        // gate-execution chain reaches `projectExecuted` (the breadth-first driver processes this batch first).
        match request.Scope with
        // ExplicitPaths bypasses git diff entirely (research D4): set candidates here and go straight
        // to the catalog load — the faked git Ports is never consulted for a diff.
        | ExplicitPaths paths ->
            { model with Candidates = Some paths }, [ SenseViewCurrency request.Repo; SenseProvenance; LoadCatalog request.Repo ]
        | Since _
        | DefaultRange -> model, [ SenseViewCurrency request.Repo; SenseProvenance; SenseScope request.Scope ]

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

    // Map the decision's typed ExitCodeBasis to the process-level ExitDecision (research D6, FR-008).
    let exitFromBasis (basis: ExitCodeBasis) : ExitDecision =
        match basis with
        | Clean -> Success
        | ExitCodeBasis.Blocked -> Blocked

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

    let revOfCommit (CommitId c) = Revision c

    let baseHeadOf (model: Model) : Revision option * Revision option =
        match model.Snapshot |> Option.bind (fun s -> s.Range) with
        | Some r -> Some(revOfCommit r.Base), Some(revOfCommit r.Head)
        | None -> None, None

    // ── F048 persistence (pure; the decision lives here, not at the write edge — FR-010/D2) ──

    // The persisted document: F047's prune → bound → serialise pipeline over the LOADED store, verbatim
    // (data-model §2). No reuse policy / bound of our own. Decoupled from the current run's verdict (FR-005):
    // this feeds only the NEXT run's file and never perturbs the ship verdict or exit code.
    let persistedContent (loaded: ReuseStore) : string =
        loaded
        |> EvidenceReuseStore.prune
        |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
        |> EvidenceReuseStore.serialise

    // Whether the summary must wait for a store-write ack: persistence is enabled, the load did NOT degrade
    // (a degraded load emits no `PersistStore`, so nothing acks), and no ack has arrived yet (D10).
    let awaitingPersist (model: Model) : bool =
        model.Request.PersistStore && not model.StoreDegraded && not model.PersistAcked

    // The pure sense → resolve → evaluate → embed join (data-model §3). Fires only once BOTH the sensed
    // facts and the store have arrived; passes the REAL `CacheEligibilityReport` as `Some report` to the
    // F045 embed. The ship verdict / partition / enforcement / `ExitCodeBasis` are UNCHANGED — the cache
    // section is the only delta (SC-003). Emits the single `WriteArtifact`. F048: when persistence is
    // enabled AND the on-disk store did not degrade on load, it ALSO emits a `PersistStore` effect carrying
    // `persistedContent (loaded store)` (D2/D4); a degraded load instead appends a non-fatal don't-clobber
    // note and emits no write (D6).
    // ── F052 verdict relocation (D3) — the ONE verdict change; Ship.rollup is used VERBATIM, never edited ──

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

    // ── F25 wiring (064): pure host-edge helpers (kinded-run label, provenance snapshot build) ──

    // The total kinded-run label (FR-004, FR-008). Descriptive metadata derived from the gate's declared
    // command token (or its id when no command is declared); it never participates in the F032 run identity.
    // Total over the closed taxonomy: a recognized token maps to its kind; an unrecognized token maps to the
    // documented `Build` default below — an explicit catch-all at the use site, never a silent mislabel of a
    // recognized kind. The richer category source (a declared kind on the gate) is a documented later extension.
    let kindOf (gate: Gate) : CommandKind =
        let token =
            match gate.FreshnessKey.Command with
            | Some(CommandId c) -> c.ToLowerInvariant()
            | None -> (gateIdValue gate.Id).ToLowerInvariant()

        let has (sub: string) = token.Contains sub

        if has "test" then Test
        elif has "pack" then Pack
        elif has "template" || has "scaffold" || has "instantiate" then TemplateInstantiation
        elif has "diff" then GitDiff
        elif has "audit" || has "inspect" || has "restore" || has "list" then PackageInspection
        elif has "capture" || has "visual" || has "screenshot" || has "snapshot" then VisualCapture
        elif has "build" || has "format" || has "lint" || has "compile" then Build
        else Build // documented default for an unrecognized command token (no silent mislabel)

    // The agent-review mark per gate (FR-009/D7). The MVP `.fsgg` schema declares no agent-reviewed checks, so
    // every gate is `Deterministic`; carrying the mark through `decide` keeps the reuse identity correct for the
    // later agent-review extension. Agent-reviewed checks stay advisory regardless of the decision (D6/D7).
    let reviewMarkOf (_gate: Gate) : AgentReviewMark = Deterministic

    // The sensed evidence taint per gate (D5). The host produces real runs and the MVP store records no
    // synthetic-evidence provenance, so taint is `Real` for every gate; a `Stale`/`NoEvidence` finding still
    // arises purely from the budgeted report's recompute causes. Synthetic-evidence taint is a later extension.
    let taintOf (_gid: GateId) : EvidenceTaint = Real

    // Build the provenance audit snapshot (D4) from the already-sensed facts plus the two normalized edge
    // senses, feeding the kinded runs through. `SourceCommit = Head` (D4). Missing optional facts substitute a
    // deterministic empty value (NEVER a clock/username/host), so `provenance.json` is byte-identical across
    // machines and re-runs. The environment/builder default to a normalized constant until sensed.
    let buildSnapshot (model: Model) (runs: KindedCommandRun list) : AuditSnapshot =
        let sensed = model.Sensed |> Option.defaultValue emptySensedFacts
        let baseSnap, headSnap = baseHeadOf model
        let baseRev = sensed.Base |> Option.orElse baseSnap |> Option.defaultValue (Revision "")
        let headRev = sensed.Head |> Option.orElse headSnap |> Option.defaultValue (Revision "")
        let ruleHash = sensed.RuleHash |> Option.defaultValue (RuleHash "")
        let genVer = sensed.GeneratorVersion |> Option.defaultValue (GeneratorVersion "")
        let digests = sensed.CoveredArtifacts |> Map.toList |> List.collect snd
        let env = model.Environment |> Option.defaultValue LocalOrCi
        let builder = model.Builder |> Option.defaultValue (BuilderIdentity "fsgg")
        FS.GG.Governance.CommandKind.Audit.auditSnapshot headRev baseRev headRev ruleHash genVer digests runs env builder

    let kindedRunsOf (model: Model) (records: (GateId * CommandRecord) list) : KindedCommandRun list =
        let gateById =
            model.SelectedGates |> List.map (fun g -> gateIdValue g.Id, g) |> Map.ofList

        records
        |> List.choose (fun (gid, record) ->
            match Map.tryFind (gateIdValue gid) gateById with
            | Some g -> Some { Kind = kindOf g; Record = record }
            | None -> None)

    // ── F052 per-gate classification (pure; recomputable from the model — data-model §classification) ──
    // F25 wiring (064): the budget filter is layered HERE — an `OverBudget` must-recompute gate is demoted from
    // `ToExecute` to `Deferred reason`, so it is never executed and never reaches the passed set (SC-002).

    type GateClassification =
        | ToExecute of GateCommand
        | ToReuse of ExitCode
        | Deferred of BudgetReason
        | NoCommand

    let executionPlan (model: Model) : (Gate * GateClassification) list * Map<string, FreshnessInputs> * CacheDecisionReport =
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

            // The budget filter (D1/D3): one `CandidateCost` per selected gate; fold the F041 verdict with
            // `budgetFor profile mode` (ship threads the request's `Mode`/`Profile` — D3) via `decide`.
            let budget =
                FS.GG.Governance.CostBudget.Budget.budgetFor model.Request.Profile model.Request.Mode

            let candidateCosts =
                model.SelectedGates
                |> List.map (fun g ->
                    let verdict =
                        match Map.tryFind (gateIdValue g.Id) verdictMap with
                        | Some v -> v
                        | None -> MustRecompute NoPriorEvidence

                    { Gate = g.Id
                      Cost = g.Cost
                      Verdict = verdict
                      Review = reviewMarkOf g })

            let budgetReport =
                FS.GG.Governance.CostBudget.Budget.decide budget model.Request.Mode candidateCosts

            let overReasons =
                FS.GG.Governance.CostBudget.Budget.overBudget budgetReport
                |> List.map (fun (gid, reason) -> gateIdValue gid, reason)
                |> Map.ofList

            let classify (gate: Gate) : GateClassification =
                let cmdOpt =
                    match model.Tooling with
                    | Some tooling -> Plan.commandFor model.Request.Repo tooling gate
                    | None -> None

                match cmdOpt with
                | None -> NoCommand
                | Some cmd ->
                    let baseClass =
                        match Map.tryFind (gateIdValue gate.Id) verdictMap with
                        | Some(Reusable ref) ->
                            match Plan.priorExitOf ref with
                            | Some priorExit -> ToReuse priorExit
                            | None -> ToExecute cmd
                        | _ -> ToExecute cmd

                    match baseClass with
                    | ToExecute _ ->
                        match Map.tryFind (gateIdValue gate.Id) overReasons with
                        | Some reason -> Deferred reason
                        | None -> baseClass
                    | other -> other

            (model.SelectedGates |> List.map (fun g -> g, classify g)), inputsMap, budgetReport
        | _ -> [], Map.empty, CacheDecisionReport []

    // Fires once BOTH the sensed facts and the store have arrived: classify the selected gates and request
    // the run of the must-recompute command-gates through the injected F051 port (D5). Reused/no-command
    // gates spawn nothing. Capture, projection, the verdict relocation, and the persist-grown-store effect
    // all wait for `GatesExecuted`.
    let tryExecute (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Decision with
        | Some _, Some _, Some _ ->
            let plan, _, budgetReport = executionPlan model

            let toExecute =
                plan
                |> List.choose (fun (g, c) ->
                    match c with
                    | ToExecute cmd -> Some(g.Id, cmd)
                    | ToReuse _
                    | Deferred _
                    | NoCommand -> None)

            { model with CacheDecision = Some budgetReport }, [ ExecuteGates toExecute ]
        | _ -> model, []

    // On `GatesExecuted`: fold F049 `capture` per executed gate (grows the store), build the per-gate
    // `GateOutcome`s, RELOCATE passing command-gates in the verdict (`applyExecution`), project audit.json
    // WITH the execution embed over the RELOCATED decision (cache report over the LOADED pre-run store), emit
    // the write, and persist the GROWN store. The relocated decision's `ExitCodeBasis` governs the exit (D3).
    // F070: fold the stale-generated-view findings into the verdict (mirroring F067's `foldSurfaceVerdict`).
    // Any finding whose EFFECTIVE severity (the existing F023 `deriveEffectiveSeverity`, via the leaf's
    // `decisionOf`) is `Blocking` fails the run; otherwise identity. An empty list (unconfigured / all-current)
    // ⇒ identity ⇒ byte-identical ship.json (FR-004). NO truth-table logic here — reuse only (FR-003).
    let viewCurrencyBlocks (mode: RunMode) (profile: Profile) (findings: CE.CurrencyFinding list) : bool =
        findings
        |> List.exists (fun f -> (CE.decisionOf f mode profile).EffectiveSeverity = Blocking)

    let foldViewCurrencyVerdict
        (mode: RunMode)
        (profile: Profile)
        (findings: CE.CurrencyFinding list)
        (decision: ShipDecision)
        : ShipDecision =
        if viewCurrencyBlocks mode profile findings then
            { decision with
                Verdict = Fail
                ExitCodeBasis = ExitCodeBasis.Blocked }
        else
            decision

    let projectExecuted (records: (GateId * CommandRecord) list) (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Decision with
        | Some sensed, Some store, Some decision ->
            let plan, inputsMap, budgetReport = executionPlan model

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
                        | Deferred _
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
                        // A deferred (over-budget) gate is NOT executed and NOT reused — recorded NotExecuted so
                        // it is structurally excluded from the passed set (never coerced to pass — SC-002).
                        | Deferred _ ->
                            { GateId = g.Id
                              Disposition = NotExecuted
                              ExitCode = None
                              Passed = None }
                        | NoCommand ->
                            { GateId = g.Id
                              Disposition = NotExecuted
                              ExitCode = None
                              Passed = None }

                    g.Id, outcome)

            // The verdict relocation (D3): a PASSING command-gate is moved to `Passing` and the verdict/exit
            // recomputed. A failing or no-command gate is never in this set, so it keeps its rollup treatment.
            let passedGateIds =
                outcomes
                |> List.choose (fun (gid, o) -> if o.Passed = Some true then Some gid else None)
                |> Set.ofList

            let relocated = applyExecution passedGateIds decision
            // F070: fold the stale-generated-view currency findings into the verdict AFTER `applyExecution`
            // (which recomputes from gate blockers only). An effective-Blocking finding fails the run at the
            // Gate boundary; an empty list (unconfigured) is the identity, so the pre-F070 golden is unchanged.
            let folded =
                foldViewCurrencyVerdict model.Request.Mode model.Request.Profile model.ViewCurrencyFindings relocated

            let currencyDetail =
                model.ViewCurrencyFindings
                |> List.map (fun f -> f, CE.decisionOf f model.Request.Mode model.Request.Profile)

            let resReport = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries resReport |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store
            // audit.json is projected over the currency-folded decision + cache report + outcomes; the additive
            // `generatedViews` array is omitted when there are no currency findings ⇒ byte-identical (FR-004/D6).
            let auditDoc =
                AuditJson.ofShipDecisionWithGeneratedViews folded (Some cacheReport) outcomes currencyDetail

            // F25 wiring (064): the two NEW deterministic sidecars (D5/D6). cost-budget.json = the budgeted
            // decisions + the advisory cost/cache findings; provenance.json = the kinded-run audit snapshot.
            let findings =
                FS.GG.Governance.CostBudget.Findings.cacheFindings budgetReport taintOf

            let costBudgetDoc =
                FS.GG.Governance.CostBudgetJson.CostBudgetJson.ofReport budgetReport findings

            let snapshot = buildSnapshot model (kindedRunsOf model records)
            let provenanceDoc = FS.GG.Governance.ProvenanceJson.ProvenanceJson.ofSnapshot snapshot

            let persistEffects, persistNotes =
                match model.Request.PersistStore, model.StoreDegraded with
                | true, false -> [ PersistStore(model.Request.StorePath, persistedContent grownStore) ], []
                | true, true ->
                    [],
                    [ "cache note: store not persisted: on-disk store failed to parse; left untouched" ]
                | false, _ -> [], []

            { model with
                Phase = Rolled
                Decision = Some folded
                AuditDoc = Some auditDoc
                Outcomes = outcomes
                CacheDecision = Some budgetReport
                Audit = Some snapshot
                CacheNotes = model.CacheNotes @ persistNotes },
            WriteArtifact(AuditArtifact, model.Request.AuditOut, auditDoc)
            :: WriteArtifact(CostBudgetArtifact, model.Request.CostBudgetOut, costBudgetDoc)
            :: WriteArtifact(ProvenanceArtifact, model.Request.ProvenanceOut, provenanceDoc)
            :: persistEffects
        | _ -> model, []

    let rec update (msg: Msg) (model: Model) : Model * Effect list =
        // Once the pipeline has decided (Done), every further reified Msg is inert (FR-013).
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
                // The composition (FR-004): re-derive/re-sort/re-classify/re-serialize nothing — carry
                // the cores' values verbatim. The verdict is decided here (`Ship.rollup`); the audit
                // document waits for the cache-eligibility join (F046). Select the gates to sense, then
                // request the two cache senses (NO write is emitted here anymore).
                let candidates = model.Candidates |> Option.defaultValue []
                let report = Routing.route facts candidates
                let registry = Gates.buildRegistry facts
                let findings = Findings.findUnknownGovernedPaths facts report
                let result = Route.select registry report findings
                let decision = Ship.rollup result model.Request.Mode model.Request.Profile
                let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)

                { model with
                    Phase = Selected
                    Decision = Some decision
                    SelectedGates = selectedGates
                    Tooling = facts.Tooling },
                [ SenseFreshness(selectedGates, baseHeadOf model)
                  LoadStore model.Request.StorePath ]

            // F046: a sensed/store result feeds the pure join. An `Error` DEGRADES to a safe default + a
            // non-fatal cache note (D2) — it NEVER fails the command, never perturbs the verdict, and never
            // changes the exit code (FR-009/FR-011).
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
                // A write failure is ALWAYS a ToolError, NEVER a blocked verdict (FR-009).
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(_, Ok()) ->
                // F25 wiring (064): three artifacts may be written (audit.json + the two sidecars), so multiple
                // `Wrote(Ok)` acks arrive in one batch. Only the FIRST (Phase = Rolled) schedules the summary /
                // persist wait; subsequent sidecar acks are inert (the summary is already in flight).
                match model.Phase with
                | Persisted
                | Done -> model, []
                | _ ->
                    // F048: when persistence is enabled and not degraded, the summary waits for the store-write
                    // ack (`StorePersisted`) instead of emitting on the audit write (D10).
                    if awaitingPersist model then
                        { model with Phase = Persisted }, []
                    else
                        { model with Phase = Persisted }, [ emitEffect model ]

            // F048: the NON-FATAL store-write ack (FR-006). An `Error` appends a cache note; NEITHER outcome
            // changes `Exit` (it stays governed solely by `ExitCodeBasis` at `Emitted` — never `ToolError`/
            // `Blocked`) nor the already-emitted audit.json. Once the write is done (Phase = Persisted) the
            // summary is emitted; otherwise it waits for it.
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

            // F052: the executed gates' records arrive — capture each, build outcomes, RELOCATE passing
            // command-gates in the verdict, project audit.json with the execution embed, and persist the
            // GROWN store. The relocated decision's `ExitCodeBasis` then governs the terminal exit (D3).
            | GatesExecuted records -> projectExecuted records model

            // F25 wiring (064): record the two normalized provenance senses; pure state, no phase change.
            | ProvenanceSensed(environment, builder) ->
                { model with
                    Environment = Some environment
                    Builder = Some builder },
                []

            // F070: the stale-generated-view currency findings landed. Store them; the verdict fold and the
            // additive `generatedViews` projection happen in `projectExecuted`, which reads this field. Pure
            // state, no phase change. `[]` (unconfigured) ⇒ identity fold ⇒ byte-identical ship.json (FR-004).
            | ViewCurrencySensed findings -> { model with ViewCurrencyFindings = findings }, []

            | Emitted ->
                // The verdict is information until the very end: only the terminal exit category differs
                // between a pass and a fail (data-model §4). Map the decision's basis here.
                let exit =
                    model.Decision
                    |> Option.map (fun d -> exitFromBasis d.ExitCodeBasis)
                    |> Option.defaultValue Success

                { model with Phase = Done; Exit = exit }, []

    // ── render (research D8) — the deterministic summary ──

    // F27 wiring (063): the full CacheEligibilityReport (not just its entries) recomputed purely from the
    // model's sensed facts + loaded store — the same value the audit.json embed carries — for the shared
    // HumanText projection. `None` until both senses have arrived.
    and cacheReportOf (model: Model) : CacheEligibilityReport option =
        match model.Sensed, model.Store with
        | Some sensed, Some store ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            Some(CacheEligibility.evaluate candidates store)
        | _ -> None

    // F27 wiring (063): the host operational line (the audit `wrote` confirmation) — host output kept around
    // the report projection, never part of the JSON contract (FR-003). Empty when there is no decision.
    and operationalLines (model: Model) : string =
        match model.Decision with
        | Some _ -> sprintf "wrote %s    (%s)" model.Request.AuditOut AuditJson.schemaVersion
        | None -> ""

    and renderText (model: Model) : string =
        match model.Decision with
        | None ->
            model.Diagnostics
            |> List.map (fun d -> "error: " + d.Message)
            |> String.concat "\n"
        | Some decision ->
            // F27 wiring (063): the report facts come from the shared HumanText projection over the SAME
            // ShipDecision the audit.json path serializes (FR-001); the host keeps only its operational
            // `wrote` line (never part of the JSON contract — FR-003).
            let projection = HumanText.ofShipDecision decision (cacheReportOf model) model.Outcomes
            [ projection; operationalLines model ] |> String.concat "\n"

    // F27 wiring (063): build the emit effect. Json carries the contract string (human = None). Text carries
    // the ANSI-free plain string (used for `Plain`) PLUS the `ReportView` + operational line (used for the
    // `Rich` path the edge selects); the mode is decided at the edge via `selectMode (senseCapability …)`.
    and emitEffect (model: Model) : Effect =
        match model.Request.Format with
        | Json -> EmitSummary(renderJson model, None, false)
        | Text ->
            match model.Decision with
            | Some decision ->
                let view = ReportView.viewOfShipDecision decision (cacheReportOf model) model.Outcomes
                EmitSummary(renderText model, Some(view, operationalLines model), model.Request.ExplicitPlain)
            | None -> EmitSummary(renderText model, None, model.Request.ExplicitPlain)

    // The Json form IS the F025 `audit.json` document verbatim (research D8, FR-007): `--json` stdout
    // equals the persisted file byte-for-byte and inherits F025 byte-stability. The text form is
    // suppressed under `Json`.
    and renderJson (model: Model) : string =
        match model.AuditDoc with
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
