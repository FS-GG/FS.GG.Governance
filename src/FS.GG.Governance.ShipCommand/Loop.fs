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
// F046 cache-eligibility pipeline (sense → resolve → evaluate → embed Some report)
open FS.GG.Governance.FreshnessKey.Model   // Revision, categoryToken
open FS.GG.Governance.FreshnessResolution  // resolve, entries, candidate, isResolved, missingFacts, missingFactToken
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts, FreshnessResolutionEntry
open FS.GG.Governance.CacheEligibility      // evaluate, entries
open FS.GG.Governance.CacheEligibility.Model // CandidateGate, CacheEligibilityEntry, CacheEligibilityVerdict, Reusable, MustRecompute
open FS.GG.Governance.EvidenceReuse         // empty, referenceValue
open FS.GG.Governance.EvidenceReuse.Model   // ReuseStore, EvidenceRef, RecomputeCause, NoPriorEvidence, InputsChanged
open FS.GG.Governance.EvidenceReuseStore    // F048: prune, retain, serialise, defaultRetentionBound

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
          PersistStore: bool }

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

    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | PersistStore of path: string * content: string
        | EmitSummary of text: string

    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        | StorePersisted of Result<unit, string>
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
          CacheNotes: string list
          StoreDegraded: bool
          PersistAcked: bool
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
          Persist: bool }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Mode = None
          Profile = None
          Json = false
          AuditOut = None
          Store = None
          Persist = false }

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
            | "--store" :: v :: more -> go { acc with Store = Some v } more
            | "--store" :: [] -> Error(MissingValue "--store")
            | "--json" :: more -> go { acc with Json = true } more
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
                          PersistStore = acc.Persist }

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
              CacheNotes = []
              StoreDegraded = false
              PersistAcked = false
              Diagnostics = []
              Exit = Success }

        match request.Scope with
        // ExplicitPaths bypasses git diff entirely (research D4): set candidates here and go straight
        // to the catalog load — the faked git Ports is never consulted for a diff.
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
    let tryProject (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Decision with
        | Some sensed, Some store, Some decision ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store
            let auditDoc = AuditJson.ofShipDecision decision (Some cacheReport)

            let persistEffects, persistNotes =
                match model.Request.PersistStore, model.StoreDegraded with
                | true, false -> [ PersistStore(model.Request.StorePath, persistedContent store) ], []
                | true, true ->
                    [],
                    [ "cache note: store not persisted: on-disk store failed to parse; left untouched" ]
                | false, _ -> [], []

            { model with
                Phase = Rolled
                AuditDoc = Some auditDoc
                CacheNotes = model.CacheNotes @ persistNotes },
            WriteArtifact(AuditArtifact, model.Request.AuditOut, auditDoc) :: persistEffects
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
                    SelectedGates = selectedGates },
                [ SenseFreshness(selectedGates, baseHeadOf model)
                  LoadStore model.Request.StorePath ]

            // F046: a sensed/store result feeds the pure join. An `Error` DEGRADES to a safe default + a
            // non-fatal cache note (D2) — it NEVER fails the command, never perturbs the verdict, and never
            // changes the exit code (FR-009/FR-011).
            | FreshnessSensed(Ok facts) -> tryProject { model with Sensed = Some facts }

            | FreshnessSensed(Error reason) ->
                tryProject
                    { model with
                        Sensed = Some emptySensedFacts
                        CacheNotes =
                            model.CacheNotes
                            @ [ "cache note: freshness facts could not be sensed (" + reason + "); affected gates are recompute-by-default and reported as not-evaluated" ] }

            | StoreLoaded(Ok store) -> tryProject { model with Store = Some store }

            | StoreLoaded(Error reason) ->
                // F048: mark the load degraded so the persist write is suppressed (don't clobber a
                // malformed file, D6). The F046 degrade-to-empty + note is unchanged.
                tryProject
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
                // F048: when persistence is enabled and not degraded, the summary waits for the store-write
                // ack (`StorePersisted`) instead of emitting on the audit write (D10).
                if awaitingPersist model then
                    { model with Phase = Persisted }, []
                else
                    { model with Phase = Persisted }, [ EmitSummary(render model model.Request.Format) ]

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
                | Persisted -> model, [ EmitSummary(render model model.Request.Format) ]
                | _ -> model, []

            | Emitted ->
                // The verdict is information until the very end: only the terminal exit category differs
                // between a pass and a fail (data-model §4). Map the decision's basis here.
                let exit =
                    model.Decision
                    |> Option.map (fun d -> exitFromBasis d.ExitCodeBasis)
                    |> Option.defaultValue Success

                { model with Phase = Done; Exit = exit }, []

    // ── render (research D8) — the deterministic summary ──

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

    // ── F046 cache summary (the F044 pattern; recomputed purely from the model's sensed/store/gates) ──

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

    and cacheCauseHuman (cause: RecomputeCause) : string =
        match cause with
        | NoPriorEvidence -> "noPriorEvidence"
        | InputsChanged cats -> "inputsChanged: " + (cats |> List.map categoryToken |> String.concat ",")

    and cacheLinesOf (model: Model) : string list =
        let entries = cacheEntriesOf model
        let unresolved = unresolvedEntriesOf model

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
                | MustRecompute cause -> Some(gateIdValue e.Gate, cacheCauseHuman cause)
                | Reusable _ -> None)

        let header =
            sprintf "cache-eligibility: %d reusable, %d must-recompute, %d unresolved" reusable.Length recompute.Length unresolved.Length

        let block (title: string) (lines: string list) =
            match lines with
            | [] -> [ title + " none" ]
            | _ -> title :: lines

        let reusableLines = reusable |> List.map (fun (g, r) -> sprintf "  %s <- %s" g r)
        let recomputeLines = recompute |> List.map (fun (g, c) -> sprintf "  %s   (%s)" g c)

        let unresolvedLines =
            unresolved |> List.map (fun (g, facts) -> sprintf "  %s   missing: %s" g (String.concat "," facts))

        [ header
          yield! block "reusable:" reusableLines
          yield! block "must recompute:" recomputeLines
          yield! block "recompute by default (unresolved):" unresolvedLines
          yield! model.CacheNotes ]

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
                | Fail -> "fail"

            let basisToken =
                match decision.ExitCodeBasis with
                | Clean -> "clean"
                | ExitCodeBasis.Blocked -> "blocked"

            let header = sprintf "ship: verdict %s (exit-code basis: %s)" verdictToken basisToken

            // The unknown-governed-path findings surface as `FindingItem`s across the partition; list
            // them explicitly so the no-default-deny reasoning is observable (FR-012).
            let allFindings =
                [ decision.Blockers; decision.Warnings; decision.Passing ]
                |> List.concat
                |> List.filter isFinding

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
              cacheLinesOf model
              [ ""; sprintf "wrote %s    (%s)" model.Request.AuditOut AuditJson.schemaVersion ] ]
            |> List.concat
            |> String.concat "\n"

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
