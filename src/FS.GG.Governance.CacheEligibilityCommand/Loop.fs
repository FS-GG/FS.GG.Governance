// The PURE MVU core of the `fsgg cache-eligibility` host command (F044). Visibility lives in Loop.fsi
// (Principle II) — this file carries NO top-level access modifiers; helpers absent from the signature are
// hidden by it. `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO hashing, NO clock:
// the whole scope -> load -> select -> resolve -> evaluate -> project -> persist-plan -> summarize -> exit
// composition is a pure transition over Model + Msg emitting Effect data the edge Interpreter executes
// (Principle IV). It reuses the merged cores VERBATIM and computes NO freshness key/hash/cache decision of
// its own (FR-012/FR-013). Cache eligibility is INFORMATION, not a verdict (FR-009/FR-011).

namespace FS.GG.Governance.CacheEligibilityCommand

open FS.GG.Governance.Config.Model // GovernedPath, Validation, Valid/Invalid, normalizePath, Diagnostic, diagnosticIdToken
open FS.GG.Governance.Snapshot.Model // RepoSnapshot, DiffRange, CommitId
open FS.GG.Governance.Routing // Routing.route
open FS.GG.Governance.Findings // Findings.findUnknownGovernedPaths
open FS.GG.Governance.Gates // Gates.buildRegistry, gateIdValue
open FS.GG.Governance.Gates.Model // Gate, GateId
open FS.GG.Governance.Route // Route.select
open FS.GG.Governance.Route.Model // RouteResult, SelectedGate
open FS.GG.Governance.FreshnessKey.Model // Revision, InputCategory, categoryToken
open FS.GG.Governance.FreshnessResolution // resolve, entries, candidate, isResolved, missingFacts, missingFactToken
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts, ResolutionOutcome, FreshnessResolutionReport, FreshnessResolutionEntry
open FS.GG.Governance.CacheEligibility // evaluate, entries
open FS.GG.Governance.CacheEligibility.Model // CandidateGate, CacheEligibilityVerdict, CacheEligibilityEntry, CacheEligibilityReport
open FS.GG.Governance.CacheEligibilityJson // ofReport, schemaVersion
open FS.GG.Governance.EvidenceReuse // referenceValue
open FS.GG.Governance.EvidenceReuse.Model // ReuseStore, EvidenceRef, RecomputeCause
open FS.GG.Governance.HumanText // F27 wiring (063): HumanText.ofCacheEligibilityReport — the plain projection
open FS.GG.Governance.CommandHost // 075: shared host skeleton — `under`, `revOfCommit`, `baseHeadOf`

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    type ScopeSelector =
        | ExplicitPaths of GovernedPath list
        | Since of rev: string
        | DefaultRange

    type OutputFormat =
        | Human
        | Json

    type RunRequest =
        { Repo: string
          Scope: ScopeSelector
          StorePath: string
          CacheOut: string
          UnresolvedOut: string
          Format: OutputFormat
          ExplicitPlain: bool }

    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths
        | BadFormat of value: string

    type ExitDecision =
        | Success
        | UsageError'
        | InputUnavailable
        | ToolError

    type ArtifactKind =
        | CacheArtifact
        | UnresolvedArtifact

    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | EmitSummary of text: string * human: (ReportView.ReportView * string) option * explicitPlain: bool

    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        | Emitted

    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    type Phase =
        | Parsed
        | Sensed'
        | Loaded'
        | Selected
        | Resolved'
        | Evaluated
        | Projected
        | Persisted
        | Done

    type Model =
        { Request: RunRequest
          Phase: Phase
          Snapshot: RepoSnapshot option
          SelectedGates: Gate list
          Sensed: SensedFacts option
          Store: ReuseStore option
          Resolution: FreshnessResolutionReport option
          CacheDoc: string option
          UnresolvedDoc: string option
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    let unresolvedSchemaVersion = "fsgg.cache-eligibility.unresolved/v1"

    // ── exitCode — total, no wildcard, no ship/blocking code (FR-009) ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
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
          Store: string option
          Out: string option
          Format: string option
          Plain: bool }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Store = None
          Out = None
          Format = None
          Plain = false }


    // Derive the sidecar path from the cache-eligibility.json path: same directory, `…unresolved.json`
    // stem (C1). Pure string composition.
    let deriveUnresolved (cacheOut: string) : string =
        let stem =
            if cacheOut.EndsWith ".json" then
                cacheOut.Substring(0, cacheOut.Length - ".json".Length)
            else
                cacheOut

        stem + ".unresolved.json"

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // Tolerate (and drop) a leading `cache-eligibility` verb — the verb this project owns.
        let tokens =
            match argv with
            | "cache-eligibility" :: rest -> rest
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
            | "--store" :: v :: more -> go { acc with Store = Some v } more
            | "--store" :: [] -> Error(MissingValue "--store")
            | "--out" :: v :: more -> go { acc with Out = Some v } more
            | "--out" :: [] -> Error(MissingValue "--out")
            | "--format" :: v :: more -> go { acc with Format = Some v } more
            | "--format" :: [] -> Error(MissingValue "--format")
            | "--plain" :: more -> go { acc with Plain = true } more
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
                let formatChoice =
                    match acc.Format with
                    | None -> Ok Human
                    | Some "human" -> Ok Human
                    | Some "json" -> Ok Json
                    | Some other -> Error(BadFormat other)

                match formatChoice with
                | Error e -> Error e
                | Ok format ->
                    let repo = acc.Repo |> Option.defaultValue "."

                    let scope =
                        match scopeChoice with
                        | Some paths, None -> ExplicitPaths(paths |> List.map normalizePath)
                        | None, Some rev -> Since rev
                        | _ -> DefaultRange

                    let cacheOut = acc.Out |> Option.defaultValue (CommandHost.under repo "readiness/cache-eligibility.json")

                    Ok
                        { Repo = repo
                          Scope = scope
                          StorePath = acc.Store |> Option.defaultValue (CommandHost.under repo "readiness/evidence-reuse.json")
                          CacheOut = cacheOut
                          UnresolvedOut = deriveUnresolved cacheOut
                          Format = format
                          ExplicitPlain = acc.Plain }

    // ── init — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Snapshot = None
              SelectedGates = []
              Sensed = None
              Store = None
              Resolution = None
              CacheDoc = None
              UnresolvedDoc = None
              Diagnostics = []
              Exit = Success }

        match request.Scope with
        // ExplicitPaths bypasses git diff entirely: no snapshot, so base/head resolve to None (L2) and the
        // catalog load runs straight away — the faked git port is never consulted.
        | ExplicitPaths _ -> model, [ LoadCatalog request.Repo ]
        | Since _
        | DefaultRange -> model, [ SenseScope request.Scope ]

    // ── pure helpers (absent from Loop.fsi) ──

    let fail (category: ExitDecision) (message: string) (model: Model) : Model * Effect list =
        { model with
            Phase = Done
            Exit = category
            Diagnostics = model.Diagnostics @ [ { Category = category; Message = message } ] },
        []

    let describeCatalog (diags: FS.GG.Governance.Config.Model.Diagnostic list) : string =
        let one (d: FS.GG.Governance.Config.Model.Diagnostic) = sprintf "%s (%s)" d.Message (diagnosticIdToken d.Id)

        match diags with
        | [] -> "catalog invalid"
        | _ -> "catalog invalid: " + (diags |> List.map one |> String.concat "; ")

    let jstr (s: string) = System.Text.Json.JsonSerializer.Serialize s

    let candidatesFromModel (model: Model) : GovernedPath list =
        match model.Snapshot with
        | Some snap -> snap.Changed |> List.map (fun c -> c.Path)
        | None ->
            match model.Request.Scope with
            | ExplicitPaths paths -> paths
            | Since _
            | DefaultRange -> []

    // The deterministic unresolved sidecar (A2/D7): one entry per UNRESOLVED gate, in the report's existing
    // GateId order, naming EXACTLY and ONLY the missing facts via the public F043 accessors `gateIdValue` +
    // `missingFactToken` (no-hide, FR-005; computes no freshness key/hash, FR-013). Always written, even
    // empty (`"unresolved": []`), so consumers never confuse "file absent" with "no unresolved gates".
    let renderUnresolved (report: FreshnessResolutionReport) : string =
        let body =
            FreshnessResolution.entries report
            |> List.filter (fun e -> not (FreshnessResolution.isResolved e.Outcome))
            |> List.map (fun e ->
                let facts =
                    FreshnessResolution.missingFacts e.Outcome
                    |> List.map (fun f -> jstr (FreshnessResolution.missingFactToken f))
                    |> String.concat ","

                sprintf "{\"gate\":%s,\"missingFacts\":[%s]}" (jstr (gateIdValue e.Gate)) facts)
            |> String.concat ","

        sprintf "{\"schemaVersion\":%s,\"unresolved\":[%s]}" (jstr unresolvedSchemaVersion) body

    // Project the resolved gates: F043 candidate bridge → F041 evaluate → F042 ofReport. The unresolved
    // sidecar is rendered from the SAME report. Both document strings are computed BEFORE either write.
    let tryProject (model: Model) : Model * Effect list =
        match model.Sensed, model.Store with
        | Some sensed, Some store ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store
            let cacheDoc = CacheEligibilityJson.ofReport cacheReport
            let unresolvedDoc = renderUnresolved report

            { model with
                Phase = Projected
                Resolution = Some report
                CacheDoc = Some cacheDoc
                UnresolvedDoc = Some unresolvedDoc },
            [ WriteArtifact(CacheArtifact, model.Request.CacheOut, cacheDoc)
              WriteArtifact(UnresolvedArtifact, model.Request.UnresolvedOut, unresolvedDoc) ]
        | _ -> model, []

    // The F041 verdict entries for the resolved gates (recomputed purely from the report + store for the
    // summary; `evaluate` is pure, so this re-derives nothing the artifact did not already fix).
    let cacheEntriesOf (model: Model) : CacheEligibilityEntry list =
        match model.Resolution, model.Store with
        | Some report, Some store ->
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            CacheEligibility.evaluate candidates store |> CacheEligibility.entries
        | _ -> []

    // F27 wiring (063): the full CacheEligibilityReport (not just its entries) recomputed purely from the
    // model's sensed facts + loaded store — the SAME value the cache-eligibility.json artifact carries — for
    // the shared HumanText projection. `None` until both senses have arrived.
    // STAYS LOCAL (075 research D6, FR-008): a single defining site (this host only) — no duplication to
    // remove, and it reads this host's own `Model`. Moving a single-site helper would add leaf surface for
    // no de-dup gain.
    let cacheReportOf (model: Model) : CacheEligibilityReport option =
        match model.Sensed, model.Store with
        | Some sensed, Some store ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            Some(CacheEligibility.evaluate candidates store)
        | _ -> None

    let unresolvedEntriesOf (model: Model) : (string * string list) list =
        match model.Resolution with
        | Some report ->
            FreshnessResolution.entries report
            |> List.filter (fun e -> not (FreshnessResolution.isResolved e.Outcome))
            |> List.map (fun e -> gateIdValue e.Gate, FreshnessResolution.missingFacts e.Outcome |> List.map FreshnessResolution.missingFactToken)
        | None -> []

    let causeJson (cause: RecomputeCause) : string =
        match cause with
        | NoPriorEvidence -> "{\"kind\":\"noPriorEvidence\"}"
        | InputsChanged cats ->
            sprintf "{\"kind\":\"inputsChanged\",\"categories\":[%s]}" (cats |> List.map (categoryToken >> jstr) |> String.concat ",")

    // ── update — the whole composition; TOTAL, never throws (FR-013) ──

    let rec update (msg: Msg) (model: Model) : Model * Effect list =
        // Once decided (Done), every further reified Msg is inert: a batch of write acks after a
        // short-circuit must not resurrect work or re-diagnose.
        if model.Phase = Done then
            model, []
        else
            match msg with
            | Begin -> model, []

            | Sensed(Ok snapshot) ->
                { model with
                    Phase = Sensed'
                    Snapshot = Some snapshot },
                [ LoadCatalog model.Request.Repo ]

            | Sensed(Error reason) -> fail InputUnavailable ("git sensing unavailable: " + reason) model

            | Loaded(Invalid diags) ->
                // A declared-but-ABSENT catalog (every diagnostic is a missing-file) is missing INPUT
                // (InputUnavailable/3); a present-but-INVALID catalog is a tool-level failure (ToolError/4) —
                // C2, distinguishing missing input from a defect (Constitution VI).
                let category =
                    if not (List.isEmpty diags) && diags |> List.forall (fun d -> d.Id = MissingRequiredFile) then
                        InputUnavailable
                    else
                        ToolError

                fail category (describeCatalog diags) model

            | Loaded(Valid facts) ->
                // The verbatim F022 selection (FR-001): re-derive/re-sort/re-classify nothing.
                let candidates = candidatesFromModel model
                let report = Routing.route facts candidates
                let registry = Gates.buildRegistry facts
                let findings = Findings.findUnknownGovernedPaths facts report
                let result = Route.select registry report findings
                let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
                let baseHead = CommandHost.baseHeadOf (model.Snapshot |> Option.bind (fun s -> s.Range))

                { model with
                    Phase = Selected
                    SelectedGates = selectedGates },
                [ SenseFreshness(selectedGates, baseHead)
                  LoadStore model.Request.StorePath ]

            | FreshnessSensed(Ok sensed) -> tryProject { model with Sensed = Some sensed }

            | FreshnessSensed(Error reason) -> fail ToolError ("freshness sensing failed: " + reason) model

            | StoreLoaded(Ok store) -> tryProject { model with Store = Some store }

            | StoreLoaded(Error reason) -> fail ToolError ("reuse store malformed: " + reason) model

            | Wrote(_, Error reason) -> fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(_, Ok()) ->
                // Two writes were emitted together; the first ack advances to Persisted, the second
                // (already Persisted) emits the summary. The Phase carries the count — no counter field.
                match model.Phase with
                | Projected -> { model with Phase = Persisted }, []
                | _ -> model, [ emitEffect model ]

            | Emitted -> { model with Phase = Done; Exit = Success }, []

    // ── render — the deterministic summary, separate from the persisted artifacts ──

    // F27 wiring (063): the host operational lines — the no-hide unresolved input-signal (the sidecar's
    // recompute-by-default gates, NOT part of the CacheEligibilityReport — FR-003) plus the `wrote`
    // confirmations. Host output kept around the report projection, never part of the JSON contract. Empty
    // when there is no report.
    and operationalLines (model: Model) : string =
        match model.Resolution with
        | None -> ""
        | Some _ ->
            let unresolvedLines =
                match unresolvedEntriesOf model with
                | [] -> []
                | unresolved ->
                    "recompute by default (unresolved):"
                    :: (unresolved |> List.map (fun (g, facts) -> sprintf "  %s   missing: %s" g (String.concat "," facts)))

            let wroteLines =
                [ sprintf "wrote %s    (%s)" model.Request.CacheOut CacheEligibilityJson.schemaVersion
                  sprintf "wrote %s    (%s)" model.Request.UnresolvedOut unresolvedSchemaVersion ]

            [ unresolvedLines; wroteLines ] |> List.concat |> String.concat "\n"

    and renderHuman (model: Model) : string =
        match model.Resolution with
        | None ->
            model.Diagnostics
            |> List.map (fun d -> "error: " + d.Message)
            |> String.concat "\n"
        | Some _ ->
            // F27 wiring (063): the cache-report facts come from the shared HumanText projection over the
            // SAME CacheEligibilityReport the cache-eligibility.json path serializes (FR-001). The host keeps
            // only its operational `wrote`/unresolved lines (NOT part of the JSON contract — FR-003).
            let projection =
                match cacheReportOf model with
                | Some report -> HumanText.ofCacheEligibilityReport report
                | None -> ""

            [ projection; operationalLines model ] |> String.concat "\n"

    // F27 wiring (063): build the emit effect. Json carries the contract string (human = None). Human carries
    // the ANSI-free plain string (used for `Plain`) PLUS the `ReportView` + operational lines (used for the
    // `Rich` path the edge selects); the mode is decided at the edge via `selectMode (senseCapability …)`.
    and emitEffect (model: Model) : Effect =
        match model.Request.Format with
        | Json -> EmitSummary(renderJson model, None, false)
        | Human ->
            match cacheReportOf model with
            | Some report -> EmitSummary(renderHuman model, Some(ReportView.viewOfCacheEligibilityReport report, operationalLines model), model.Request.ExplicitPlain)
            | None -> EmitSummary(renderHuman model, None, model.Request.ExplicitPlain)

    and renderJson (model: Model) : string =
        match model.Resolution with
        | None ->
            let errs = model.Diagnostics |> List.map (fun d -> jstr d.Message) |> String.concat ","
            sprintf "{\"errors\":[%s]}" errs
        | Some _ ->
            let entries = cacheEntriesOf model

            let reusable =
                entries
                |> List.choose (fun e ->
                    match e.Verdict with
                    | Reusable ref -> Some(sprintf "{\"gate\":%s,\"evidence\":%s}" (jstr (gateIdValue e.Gate)) (jstr (EvidenceReuse.referenceValue ref)))
                    | MustRecompute _ -> None)
                |> String.concat ","

            let recompute =
                entries
                |> List.choose (fun e ->
                    match e.Verdict with
                    | MustRecompute cause -> Some(sprintf "{\"gate\":%s,\"cause\":%s}" (jstr (gateIdValue e.Gate)) (causeJson cause))
                    | Reusable _ -> None)
                |> String.concat ","

            let unresolved =
                unresolvedEntriesOf model
                |> List.map (fun (g, facts) ->
                    let fs = facts |> List.map jstr |> String.concat ","
                    sprintf "{\"gate\":%s,\"missingFacts\":[%s]}" (jstr g) fs)
                |> String.concat ","

            sprintf
                "{\"reusable\":[%s],\"mustRecompute\":[%s],\"unresolved\":[%s],\"wrote\":{\"cache\":%s,\"unresolved\":%s}}"
                reusable
                recompute
                unresolved
                (jstr model.Request.CacheOut)
                (jstr model.Request.UnresolvedOut)

    and render (model: Model) (format: OutputFormat) : string =
        match format with
        | Human -> renderHuman model
        | Json -> renderJson model
