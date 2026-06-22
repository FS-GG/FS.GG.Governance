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
open FS.GG.Governance.RouteJson           // RouteJson.ofRouteResult, schemaVersion
open FS.GG.Governance.GatesJson           // GatesJson.ofGateRegistry, schemaVersion
// F046 cache-eligibility pipeline (sense → resolve → evaluate → embed Some report)
open FS.GG.Governance.FreshnessKey.Model   // Revision, categoryToken
open FS.GG.Governance.FreshnessResolution  // resolve, entries, candidate, isResolved, missingFacts, missingFactToken
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts, FreshnessResolutionEntry
open FS.GG.Governance.CacheEligibility      // evaluate, entries
open FS.GG.Governance.CacheEligibility.Model // CandidateGate, CacheEligibilityEntry, CacheEligibilityVerdict, Reusable, MustRecompute
open FS.GG.Governance.EvidenceReuse         // empty, referenceValue
open FS.GG.Governance.EvidenceReuse.Model   // ReuseStore, EvidenceRef, RecomputeCause, NoPriorEvidence, InputsChanged

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
          StorePath: string }

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
        | EmitSummary of text: string

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
          Sensed: SensedFacts option
          Store: ReuseStore option
          CacheNotes: string list
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
          Store: string option }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Json = false
          GatesOut = None
          RouteOut = None
          Store = None }

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
                      StorePath = acc.Store |> Option.defaultValue (under repo "readiness/evidence-reuse.json") }

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
              Sensed = None
              Store = None
              CacheNotes = []
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

    // The pure sense → resolve → evaluate → embed join (data-model §3). Fires only once BOTH the sensed
    // facts and the store have arrived; builds the REAL `CacheEligibilityReport` and passes it as
    // `Some report` to the F045 embed, then emits the two writes (the existing counter dance is preserved).
    let tryProject (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Result, model.GatesDoc with
        | Some sensed, Some store, Some result, Some gatesDoc ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store
            let routeDoc = RouteJson.ofRouteResult result (Some cacheReport)

            { model with
                Phase = Projected
                RouteDoc = Some routeDoc },
            [ WriteArtifact(GatesArtifact, model.Request.GatesOut, gatesDoc)
              WriteArtifact(RouteArtifact, model.Request.RouteOut, routeDoc) ]
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

                { model with
                    Phase = Selected
                    Result = Some result
                    GatesDoc = Some gatesDoc
                    SelectedGates = selectedGates },
                [ SenseFreshness(selectedGates, baseHeadOf model)
                  LoadStore model.Request.StorePath ]

            // F046: a sensed/store result feeds the pure join. An `Error` DEGRADES to a safe default + a
            // non-fatal cache note (D2) — it NEVER fails the command or changes the exit code (FR-010/FR-011).
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
                tryProject
                    { model with
                        Store = Some EvidenceReuse.empty
                        CacheNotes =
                            model.CacheNotes
                            @ [ "cache note: reuse store unreadable (" + reason + "); treated as empty — every gate is recompute-by-default" ] }

            | Wrote(_, Error reason) ->
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(_, Ok()) ->
                // Two writes were emitted together; the first ack advances to Persisted, the second
                // (already Persisted) emits the summary. No counter field needed — the Phase carries it.
                match model.Phase with
                | Projected -> { model with Phase = Persisted }, []
                | _ -> model, [ EmitSummary(render model model.Request.Format) ]

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

    and causeHuman (cause: RecomputeCause) : string =
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
                | MustRecompute cause -> Some(gateIdValue e.Gate, causeHuman cause)
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
        match model.Result with
        | None ->
            model.Diagnostics
            |> List.map (fun d -> "error: " + d.Message)
            |> String.concat "\n"
        | Some result ->
            let selected = result.SelectedGates
            let changed = model.Candidates |> Option.map List.length |> Option.defaultValue 0
            let header = sprintf "route: %d gate(s) selected for %d changed path(s)" selected.Length changed

            let gateLines =
                if List.isEmpty selected then
                    [ "  (no gates selected)" ]
                else
                    selected
                    |> List.collect (fun sg ->
                        let id = gateIdValue sg.Gate.Id
                        let cost = costToken sg.Gate.Cost

                        match sg.SelectingPaths with
                        | [] -> [ sprintf "  %s   (%s)" id cost ]
                        | ps -> ps |> List.map (fun sp -> sprintf "  %s <- %s   (%s)" id (pathValue sp.Path) cost))

            let c = result.Cost
            let costLine = sprintf "cost: cheap=%d medium=%d high=%d exhaustive=%d" c.Cheap c.Medium c.High c.Exhaustive

            let findingLines =
                match result.Findings.Findings with
                | [] -> [ "findings: none" ]
                | fs ->
                    (sprintf "findings: %d" (List.length fs))
                    :: (fs |> List.map (fun f -> sprintf "  %s: %s" (pathValue f.Path) f.Message))

            let wroteLines =
                [ sprintf "wrote %s    (%s)" model.Request.GatesOut GatesJson.schemaVersion
                  sprintf "wrote %s    (%s, %d selected)" model.Request.RouteOut RouteJson.schemaVersion selected.Length ]

            [ [ header; "" ]
              gateLines
              [ ""; costLine ]
              findingLines
              [ "" ]
              cacheLinesOf model
              [ "" ]
              wroteLines ]
            |> List.concat
            |> String.concat "\n"

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
