namespace FS.GG.Governance.CommandHost

// The 075 (Phase B) pure command-host skeleton leaf. Each member is the genuinely-shared body the MVU
// command Loop.fs hosts used to hand-copy. The exit/gate classifications are canonical SUPERSETS (the
// optional `Blocked`/`Deferred` paths folded in); the model-reading helpers are DECOMPOSED into the fields
// they read so the leaf depends on NO host Model/Effect. The pure skeleton stays clock/process/network-free;
// output byte-identical to today's per-host copies. The trailing "host edge I/O leaves" section (#49,
// second-extraction pass) is the ONE exception — genuinely-shared impure host edges (atomic write, readiness
// discovery, env/builder sensing, the snapshot/catalog step-arm realizations) that every host used to
// hand-copy; they live here in the host layer (never the domain core). No visibility modifiers — the surface
// is CommandHost.fsi (Principle II).

open System.IO
open FS.GG.Governance.Config.Model            // Diagnostic, diagnosticIdToken, ToolingFacts, Environment, LocalOrCi
open FS.GG.Governance.Snapshot.Model          // CommitId, DiffRange
open FS.GG.Governance.FreshnessKey.Model      // Revision, RuleHash, GeneratorVersion, FreshnessInputs, CommandId
open FS.GG.Governance.Gates                   // gateIdValue
open FS.GG.Governance.Gates.Model             // Gate, GateId
open FS.GG.Governance.GateExecution.Model     // GateCommand
open FS.GG.Governance.CommandRecord.Model     // CommandRecord, ExitCode
open FS.GG.Governance.FreshnessResolution     // resolve, entries, candidate
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts
open FS.GG.Governance.CacheEligibility        // evaluate, entries
open FS.GG.Governance.CacheEligibility.Model  // CacheEligibilityVerdict, Reusable, MustRecompute
open FS.GG.Governance.EvidenceReuse.Model     // ReuseStore, NoPriorEvidence
open FS.GG.Governance.EvidenceReuseStore      // prune, retain, serialise, defaultRetentionBound
open FS.GG.Governance.GateRun                 // Plan.commandFor / priorExitOf
open FS.GG.Governance.CostBudget.Model        // BudgetReason, CacheDecisionReport
open FS.GG.Governance.CommandKind.Model       // CommandKind, KindedCommandRun, AuditSnapshot
open FS.GG.Governance.Provenance.Model        // BuilderIdentity

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CommandHost =

    // NOTE (#49, F2): the canonical `ExitDecision`/`exitCode` were removed — they were dead (zero product
    // consumers) and each host owns a genuinely-shaped exit DU (some carry `Blocked`, some don't), so a shared
    // superset here fit none of them. Deleted per issue #49 ("adopt OR delete if truly dead").

    // ── gate classification (research D3, FR-005) — canonical SUPERSET DU (with `Deferred`) ──

    type GateClassification =
        | ToExecute of GateCommand
        | ToReuse of ExitCode
        | Deferred of BudgetReason
        | NoCommand

    // ── parameterized execution plan inputs (research D4, FR-006) ──

    // The per-command optional folds that distinguish one host's gate-execution plan from another's. The
    // budget fold carries host behaviour (it captures the host's request profile/mode + per-gate cost), so
    // the leaf stays command-agnostic: `None` ⇒ no demotion, empty report (Route); `Some f` ⇒ Ship/Verify
    // compute the F25 over-budget map + the `CacheDecisionReport`.
    type ExecutionPlanParams =
        { BudgetFold:
            (Map<string, CacheEligibilityVerdict> -> Map<string, BudgetReason> * CacheDecisionReport) option }

    // ── micro-helpers (verbatim relocations — research audit table) ──

    // Join a repo dir with a default relative artifact location. A `.` (or empty) repo yields the clean
    // relative form; any other repo is prefixed so the artifact lands inside it. Pure string composition.
    let under (repo: string) (rel: string) : string =
        if repo = "." || repo = "" then rel else repo.TrimEnd('/') + "/" + rel

    // CommitId -> Revision (never re-sensed or fabricated).
    let revOfCommit (CommitId c) = Revision c

    // Base/head revisions FROM the snapshot diff-range (research D5) — never re-sensed, never fabricated.
    // `None` ⇒ both `None` (decomposed from the host `Model` so the leaf takes no Model dependency).
    let baseHeadOf (range: DiffRange option) : Revision option * Revision option =
        match range with
        | Some r -> Some(revOfCommit r.Base), Some(revOfCommit r.Head)
        | None -> None, None

    // The all-`None`/empty `SensedFacts` substituted when freshness sensing fails. NEVER fabricates a value.
    let emptySensedFacts: SensedFacts =
        { RuleHash = None
          GeneratorVersion = None
          Base = None
          Head = None
          CoveredArtifacts = Map.empty
          CommandVersions = Map.empty }

    // Human-readable catalog-invalid summary over Config diagnostics.
    let describeInvalid (diags: Diagnostic list) : string =
        let one (d: Diagnostic) =
            sprintf "%s (%s)" d.Message (diagnosticIdToken d.Id)

        match diags with
        | [] -> "catalog invalid"
        | _ -> "catalog invalid: " + (diags |> List.map one |> String.concat "; ")

    // The persisted document: F047's prune → bound → serialise pipeline over the LOADED store, verbatim.
    let persistedContent (loaded: ReuseStore) : string =
        loaded
        |> EvidenceReuseStore.prune
        |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
        |> EvidenceReuseStore.serialise

    // The CommandKind of a gate, inferred from its freshness-key command token (or gate-id fallback). The
    // documented default for an unrecognized token is `Build` (no silent mislabel). Verify↔Ship common form.
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

    // Pair executed records with their gate kind, keyed via the selected gates. Verify↔Ship common form
    // (decomposed: takes the selected-gate list rather than the host `Model`).
    let kindedRunsOf (selectedGates: Gate list) (records: (GateId * CommandRecord) list) : KindedCommandRun list =
        let gateById =
            selectedGates |> List.map (fun g -> gateIdValue g.Id, g) |> Map.ofList

        records
        |> List.choose (fun (gid, record) ->
            match Map.tryFind (gateIdValue gid) gateById with
            | Some g -> Some { Kind = kindOf g; Record = record }
            | None -> None)

    // Assemble the audit snapshot from the sensed facts (falling back to snapshot revisions), digests, runs,
    // and the build environment/identity. Verify↔Ship common form (decomposed model-view inputs; Release's
    // same-named builder takes a different input type and stays LOCAL — research D5).
    let buildSnapshot
        (sensed: SensedFacts option)
        (range: DiffRange option)
        (environment: EnvironmentClass option)
        (builder: BuilderIdentity option)
        (runs: KindedCommandRun list)
        : AuditSnapshot =
        let sensed = sensed |> Option.defaultValue emptySensedFacts
        let baseSnap, headSnap = baseHeadOf range
        let baseRev = sensed.Base |> Option.orElse baseSnap |> Option.defaultValue (Revision "")
        let headRev = sensed.Head |> Option.orElse headSnap |> Option.defaultValue (Revision "")
        let ruleHash = sensed.RuleHash |> Option.defaultValue (RuleHash "")
        let genVer = sensed.GeneratorVersion |> Option.defaultValue (GeneratorVersion "")
        let digests = sensed.CoveredArtifacts |> Map.toList |> List.collect snd
        let env = environment |> Option.defaultValue LocalOrCi
        let builder = builder |> Option.defaultValue (BuilderIdentity "fsgg")
        FS.GG.Governance.CommandKind.Audit.auditSnapshot headRev baseRev headRev ruleHash genVer digests runs env builder

    // ── parameterized gate-execution plan (research D4, FR-006) ──

    // The shared per-gate classification + freshness-input plan. Computes the identical non-budget prefix
    // (freshness resolve → cache-eligibility → verdict/inputs maps → base `classify`) for every host, then
    // applies the per-command `BudgetFold` when present (Ship/Verify demote over-budget `ToExecute` gates to
    // `Deferred`) or returns an empty report (Route). Plans are byte-identical to each host's pre-extraction
    // plan. Decomposed model-view inputs so the leaf depends on NO host Model.
    let executionPlan
        (parms: ExecutionPlanParams)
        (sensed: SensedFacts option)
        (store: ReuseStore option)
        (selectedGates: Gate list)
        (tooling: ToolingFacts option)
        (repo: string)
        : (Gate * GateClassification) list * Map<string, FreshnessInputs> * CacheDecisionReport =
        match sensed, store with
        | Some sensed, Some store ->
            let resReport = FreshnessResolution.resolve selectedGates sensed
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

            // Per-command budget fold: Ship/Verify supply the F25 over-budget map + report; Route supplies
            // None (no demotion, empty report). The fold captures the host's request profile/mode + cost.
            let overReasons, budgetReport =
                match parms.BudgetFold with
                | Some fold -> fold verdictMap
                | None -> Map.empty, CacheDecisionReport []

            let classify (gate: Gate) : GateClassification =
                let cmdOpt =
                    match tooling with
                    | Some tooling -> Plan.commandFor repo tooling gate
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

            (selectedGates |> List.map (fun g -> g, classify g)), inputsMap, budgetReport
        | _ -> [], Map.empty, CacheDecisionReport []

    // ── host-loop combinators (F2 second-extraction pass — genuinely-shared pure forms) ──

    // Reify any exception from a Result-returning impure call into `Error e.Message`. A PURE combinator over
    // the thunk (the I/O lives inside `call`); shared verbatim by every command host's persistence/sense edge.
    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    // The generic MVU drive loop: map each effect to a message via `step`, fold `update` over the messages
    // accumulating new effects, and recurse until no effects remain or `isDone model`. Byte-identical to each
    // host's hand-copied `drive`; parameterized over the host's done-predicate / effect→msg step / update so
    // the leaf depends on NO host Model/Effect type. Pure (the impure step lives in the caller-supplied `step`).
    let rec drive
        (isDone: 'model -> bool)
        (step: 'effect -> 'msg)
        (update: 'msg -> 'model -> 'model * 'effect list)
        (model: 'model)
        (effects: 'effect list)
        : 'model =
        if isDone model then
            model
        else
            match effects with
            | [] -> model
            | _ ->
                let model2, newEffects =
                    effects
                    |> List.map step
                    |> List.fold
                        (fun (m, acc) msg ->
                            let m2, e2 = update msg m
                            m2, acc @ e2)
                        (model, [])

                drive isDone step update model2 newEffects

    // ── host edge I/O leaves (#49 second-extraction pass) — the shared impure host edges every command
    // host used to hand-copy. These live in the host layer, above the domain core (which stays FS/git-free).

    // The real persistence port: create parent dirs, write to a unique temp sibling, then atomically rename
    // over the target — a failed write leaves NO partial/truncated file (research D9, FR-010). Reifies any
    // exception to `Error` so the interpreter edge never throws out of itself.
    let writeAtomic (path: string) (content: string) : Result<unit, string> =
        try
            match Path.GetDirectoryName path with
            | null
            | "" -> ()
            | dir -> Directory.CreateDirectory dir |> ignore

            let tmp = path + ".tmp-" + System.Guid.NewGuid().ToString("N")
            File.WriteAllText(tmp, content)
            File.Move(tmp, path, true)
            Ok()
        with e ->
            Error e.Message

    // F081: locate + read every readiness/<id>/governance-handoff.json under `repo` in stable ordinal <id>
    // order. A missing readiness dir (or any IO failure) yields `[]`. The ordinal sort is spelled out so the
    // ordering guarantee is visible and identical to the former per-host copies + the Cli mirror (#49, D3).
    let realHandoffs (repo: string) : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list =
        try
            let readinessDir = Path.Combine(repo, "readiness")

            if not (Directory.Exists readinessDir) then
                []
            else
                Directory.GetDirectories readinessDir
                |> Array.sortWith (fun a b -> System.String.CompareOrdinal(Path.GetFileName a, Path.GetFileName b))
                |> Array.choose (fun dir ->
                    let file = Path.Combine(dir, "governance-handoff.json")

                    if File.Exists file then
                        Some
                            { FS.GG.Governance.Adapters.SddHandoff.Reader.Source =
                                sprintf "readiness/%s/governance-handoff.json" (Path.GetFileName dir)
                              FS.GG.Governance.Adapters.SddHandoff.Reader.Json = File.ReadAllText file }
                    else
                        None)
                |> Array.toList
        with _ ->
            []

    // Sense the runner environment from the `CI` variable: set ⇒ `Ci`, unset/empty ⇒ `Local`. Fully qualified
    // to avoid the `Ci` clash with `Snapshot.Model.CiEnvironment` in scope here (#49, D1).
    let senseEnvironmentReal () : FS.GG.Governance.Config.Model.EnvironmentClass =
        match System.Environment.GetEnvironmentVariable "CI" with
        | null
        | "" -> FS.GG.Governance.Config.Model.Local
        | _ -> FS.GG.Governance.Config.Model.Ci

    let senseBuilderReal () : FS.GG.Governance.Provenance.Model.BuilderIdentity =
        FS.GG.Governance.Provenance.Model.BuilderIdentity "fsgg"

    // Shared realization of the `SenseScope` step arm: sense a snapshot for `options` against `git`, mapping
    // any sensing diagnostic (or thrown exception) to a single `Error` string. senseSnapshot NEVER throws; a
    // failure surfaces as a SensingDiagnostic (not-a-repo, unknown-ref, git-unavailable) ⇒ InputUnavailable.
    // The host wraps this `Result` in its own `Sensed` msg, so `step`'s signature is unchanged.
    let senseSnapshotResult
        (git: FS.GG.Governance.Snapshot.Ports)
        (options: SnapshotOptions)
        : Result<RepoSnapshot, string> =
        try
            let snap = FS.GG.Governance.Snapshot.Interpreter.senseSnapshot git options

            match snap.Diagnostics with
            | [] -> Ok snap
            | ds ->
                ds
                |> List.map (fun d -> sprintf "%s: %s" (sensingDiagnosticIdToken d.Id) d.Message)
                |> String.concat "; "
                |> Error
        with e ->
            Error e.Message

    // Shared realization of the `LoadCatalog` step arm: read the repo's `.fsgg` sources through `files` and
    // apply the pure `Schema.validate` (mirrors Loader.loadAndValidate); a misbehaving reader's exception is
    // reified to an `Invalid` catalog. The host wraps this in its own `Loaded` msg.
    let loadCatalogValidation (files: FS.GG.Governance.Config.Loader.FileReader) : Validation =
        try
            FS.GG.Governance.Config.Loader.readSource (GovernedPath ".") files
            |> FS.GG.Governance.Config.Schema.validate
        with e ->
            Invalid
                [ { Id = MissingRequiredFile
                    File = Project
                    Locator = { Field = None; Id = None; Line = None }
                    Message = "catalog read failed: " + e.Message } ]
