// The PURE MVU core of the `fsgg refresh` host command (F057). Visibility lives in Loop.fsi (Principle II)
// — this file carries NO top-level access modifiers; helpers absent from the signature are hidden by it.
// `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock: the whole
// parse -> load-manifest -> sense+read-recorded -> DECIDE-CURRENCY -> regenerate -> record -> project ->
// summarize -> EXIT composition is a pure transition over Model + Msg emitting Effect data the edge
// Interpreter executes (Principle IV). It reuses the F029 `FreshnessKey` comparator (`matches`/`diff`)
// VERBATIM to decide per-view currency — building `recorded`/`current` `FreshnessInputs` that differ ONLY in
// the source-digest set and generator version, the revision fields held EQUAL (research D1) — and the F057
// `RefreshJson.ofRefreshDecision` for the persisted bytes.

namespace FS.GG.Governance.RefreshCommand

open FS.GG.Governance.Config.Model                  // CheckId, DomainId, EnvironmentClass
open FS.GG.Governance.FreshnessKey                  // FreshnessKey.matches/diff
open FS.GG.Governance.FreshnessKey.Model             // FreshnessInputs, ArtifactHash, GeneratorVersion, ...
open FS.GG.Governance.RefreshJson                   // RefreshJson.ofRefreshDecision
open FS.GG.Governance.RefreshJson.RefreshModel       // GenerationManifest, CurrencyStatus, RefreshDecision, ...
open FS.GG.Governance.CommandHost                    // 075: shared host skeleton — `under`

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    type OutputFormat =
        | Text
        | Json
        | TextAndJson

    type Scope =
        | AllViews
        | ByKind of ViewKind
        | ByView of string

    type RunRequest =
        { Repo: string
          DryRun: bool
          Scope: Scope
          Format: OutputFormat
          RefreshOut: string option }

    type UsageError = { Message: string }

    type Effect =
        | LoadManifest of repo: string
        | SenseSource of entry: GenerationEntry
        | ReadRecorded of viewId: string
        | RegenerateView of entry: GenerationEntry
        | RecordProvenance of viewId: string * provenance: (ArtifactHash list * GeneratorVersion * ArtifactHash)
        | WriteArtifact of path: string * content: string
        | EmitSummary of text: string

    type Msg =
        | Begin
        | ManifestLoaded of Result<GenerationManifest, DeclError>
        | Sensed of viewId: string * Result<ArtifactHash list * GeneratorVersion, string>
        | RecordedRead of viewId: string * recorded: (ArtifactHash list * GeneratorVersion) option
        | Regenerated' of viewId: string * Result<ArtifactHash, string>
        | ProvenanceWritten of Result<unit, string>
        | Wrote of Result<unit, string>
        | Emitted

    type Diagnostic =
        { Category: RefreshOutcome
          Message: string }

    type Phase =
        | Parsed
        | Loaded'
        | Sensed'
        | Persisted
        | Done

    type Model =
        { Request: RunRequest
          Phase: Phase
          Manifest: GenerationManifest option
          InScope: GenerationEntry list
          Sensed: Map<string, Result<ArtifactHash list * GeneratorVersion, string>>
          Recorded: Map<string, (ArtifactHash list * GeneratorVersion) option>
          ExpectedRegen: Set<string>
          PendingProv: int
          Views: ViewDecision list
          Decision: RefreshDecision option
          RefreshDoc: string option
          Diagnostics: Diagnostic list
          Exit: RefreshOutcome }

    // ── exitCode (cli.md exit-code table) — total, no wildcard ──

    let exitCode (outcome: RefreshOutcome) : int =
        match outcome with
        | NothingToRefresh -> 0
        | StaleUnresolved' -> 1
        | UsageError' -> 2
        | InputUnavailable -> 3
        | ToolError -> 4
        | ViewsRegenerated -> 5

    // ── parse — a pure, total argv matcher; usage problems are values, never throws ──

    type ParseAcc =
        { Repo: string option
          DryRun: bool
          ViewKind: string option
          View: string option
          Format: string option
          RefreshOut: string option }

    let emptyAcc =
        { Repo = None
          DryRun = false
          ViewKind = None
          View = None
          Format = None
          RefreshOut = None }

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // A leading bare `refresh` token is tolerated (no central dispatcher — command precedent).
        let argv =
            match argv with
            | "refresh" :: rest -> rest
            | _ -> argv

        let rec go (acc: ParseAcc) (rest: string list) : Result<ParseAcc, UsageError> =
            match rest with
            | [] -> Ok acc
            | "--dry-run" :: more -> go { acc with DryRun = true } more
            | "--repo" :: v :: more -> go { acc with Repo = Some v } more
            | "--repo" :: [] -> Error { Message = "missing value for flag: --repo" }
            | "--view-kind" :: v :: more -> go { acc with ViewKind = Some v } more
            | "--view-kind" :: [] -> Error { Message = "missing value for flag: --view-kind" }
            | "--view" :: v :: more -> go { acc with View = Some v } more
            | "--view" :: [] -> Error { Message = "missing value for flag: --view" }
            | "--refresh-out" :: v :: more -> go { acc with RefreshOut = Some v } more
            | "--refresh-out" :: [] -> Error { Message = "missing value for flag: --refresh-out" }
            | "--text" :: more -> go { acc with Format = Some "text" } more
            | "--json" :: more -> go { acc with Format = Some "json" } more
            | "--text-and-json" :: more -> go { acc with Format = Some "text-and-json" } more
            | other :: _ -> Error { Message = "unknown argument: " + other }

        match go emptyAcc argv with
        | Error e -> Error e
        | Ok acc ->
            let formatResult =
                match acc.Format with
                | None -> Ok Text
                | Some "text" -> Ok Text
                | Some "json" -> Ok Json
                | Some "text-and-json" -> Ok TextAndJson
                | Some other -> Error { Message = "unrecognized format: " + other }

            let scopeResult =
                match acc.ViewKind, acc.View with
                | Some _, Some _ -> Error { Message = "mutually exclusive selectors: use --view-kind OR --view, not both" }
                | Some k, None -> Ok(ByKind(viewKindOfToken k))
                | None, Some v -> Ok(ByView v)
                | None, None -> Ok AllViews

            match formatResult, scopeResult with
            | Error e, _ -> Error e
            | _, Error e -> Error e
            | Ok format, Ok scope ->
                Ok
                    { Repo = acc.Repo |> Option.defaultValue "."
                      DryRun = acc.DryRun
                      Scope = scope
                      Format = format
                      RefreshOut = acc.RefreshOut }

    // ── currency decision (pure; reuses F029 FreshnessKey, revisions held EQUAL — research D1) ──

    // Build the per-view `FreshnessInputs`. EVERY field except `CoveredArtifacts` (the source-digest set)
    // and `GeneratorVersion` is a FIXED, EQUAL constant between the recorded and current inputs — including
    // the `Base`/`Head` revisions — so currency depends ONLY on sources + generator, never git position
    // (research D1, the crux distinguishing view currency from per-change gate-evidence reuse).
    let freshnessInputsOf (artifacts: ArtifactHash list) (generator: GeneratorVersion) : FreshnessInputs =
        { Check = CheckId "refresh"
          Domain = DomainId "refresh"
          Command = None
          Environment = Local
          RuleHash = RuleHash ""
          CoveredArtifacts = artifacts
          CommandVersion = None
          GeneratorVersion = generator
          Base = Revision ""
          Head = Revision "" }

    // The `recorded` inputs reconstructed from a view's recorded provenance; an ABSENT record (first
    // generation) reconstructs as empty sources + an empty generator version, which never matches a sensed
    // view (⇒ stale), so a never-generated view is correctly stale (FR-002).
    let recordedInputs (recorded: (ArtifactHash list * GeneratorVersion) option) : FreshnessInputs =
        match recorded with
        | Some(digests, generator) -> freshnessInputsOf digests generator
        | None -> freshnessInputsOf [] (GeneratorVersion "")

    // The drifted categories driving staleness; `[]` iff the view is current (`FreshnessKey.matches`).
    let driftOf (recorded: (ArtifactHash list * GeneratorVersion) option) (current: ArtifactHash list * GeneratorVersion) : InputCategory list =
        let curDigests, curGen = current
        FreshnessKey.diff (recordedInputs recorded) (freshnessInputsOf curDigests curGen)

    let isStale (recorded: (ArtifactHash list * GeneratorVersion) option) (current: ArtifactHash list * GeneratorVersion) : bool =
        let curDigests, curGen = current
        not (FreshnessKey.matches (recordedInputs recorded) (freshnessInputsOf curDigests curGen))

    // ── scope ──

    let inScopeMatch (scope: Scope) (entry: GenerationEntry) : bool =
        match scope with
        | AllViews -> true
        | ByKind k -> entry.Kind = k
        | ByView id -> entry.ViewId = id

    // ── init (Principle IV) — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Manifest = None
              InScope = []
              Sensed = Map.empty
              Recorded = Map.empty
              ExpectedRegen = Set.empty
              PendingProv = 0
              Views = []
              Decision = None
              RefreshDoc = None
              Diagnostics = []
              Exit = NothingToRefresh }

        model, [ LoadManifest request.Repo ]

    // ── update (and finalize/decide/render) — the whole composition; TOTAL, never throws ──

    let fail (category: RefreshOutcome) (message: string) (model: Model) : Model * Effect list =
        { model with
            Phase = Done
            Exit = category
            Diagnostics = model.Diagnostics @ [ { Category = category; Message = message } ] },
        []

    let entryOf (model: Model) (viewId: string) : GenerationEntry option =
        model.InScope |> List.tryFind (fun e -> e.ViewId = viewId)

    // Roll the per-view statuses into the run outcome (research D5): any unresolved ⇒ StaleUnresolved';
    // else any regenerated/would-regenerate ⇒ ViewsRegenerated; else NothingToRefresh.
    let rollup (views: ViewDecision list) : RefreshOutcome * int * int * int * int =
        let isRegen v = match v.Status with | Regenerated _ | WouldRegenerate _ -> true | _ -> false
        let isCurrent v = match v.Status with | Current -> true | _ -> false
        let isUnresolved v = match v.Status with | StaleUnresolved _ -> true | _ -> false
        let isNotEval v = match v.Status with | NotEvaluated -> true | _ -> false
        let regen = views |> List.filter isRegen |> List.length
        let current = views |> List.filter isCurrent |> List.length
        let unresolved = views |> List.filter isUnresolved |> List.length
        let notEval = views |> List.filter isNotEval |> List.length

        let outcome =
            if unresolved > 0 then StaleUnresolved'
            elif regen > 0 then ViewsRegenerated
            else NothingToRefresh

        outcome, regen, current, unresolved, notEval

    let rec update (msg: Msg) (model: Model) : Model * Effect list =
        // Once decided (Done), every further reified Msg is inert.
        if model.Phase = Done then
            model, []
        else
            match msg with
            | Begin -> model, []

            | ManifestLoaded(Error e) ->
                // An absent/invalid manifest is INPUT-unavailable (exit 3) — never a tool defect. No
                // sensing/regeneration/write is emitted.
                fail InputUnavailable ("refresh manifest unavailable: " + e.Reason) model

            | ManifestLoaded(Ok manifest) ->
                let inScope = manifest.Entries |> List.filter (inScopeMatch model.Request.Scope)
                let outOfScope = manifest.Entries |> List.filter (inScopeMatch model.Request.Scope >> not)

                let notEvalViews =
                    outOfScope
                    |> List.map (fun e -> { Entry = e; Status = NotEvaluated; Drifted = [] })

                let model' =
                    { model with
                        Phase = Loaded'
                        Manifest = Some manifest
                        InScope = inScope
                        Views = notEvalViews }

                match inScope with
                | [] -> finalize model'
                | _ ->
                    let effects =
                        inScope
                        |> List.collect (fun e -> [ SenseSource e; ReadRecorded e.ViewId ])

                    model', effects

            | Sensed(viewId, result) ->
                maybeDecideSensing { model with Sensed = Map.add viewId result model.Sensed }

            | RecordedRead(viewId, recorded) ->
                maybeDecideSensing { model with Recorded = Map.add viewId recorded model.Recorded }

            | Regenerated'(viewId, Error reason) ->
                // A generator failure is ALWAYS a ToolError (exit 4) — no partial view recorded.
                fail ToolError (sprintf "generator failed for view '%s': %s" viewId reason) model

            | Regenerated'(viewId, Ok outputDigest) ->
                match entryOf model viewId, Map.tryFind viewId model.Sensed with
                | Some entry, Some(Ok(curDigests, curGen)) ->
                    let drifted = driftOf (Map.find viewId model.Recorded) (curDigests, curGen)

                    let view =
                        { Entry = entry
                          Status = Regenerated drifted
                          Drifted = drifted }

                    { model with
                        Views = model.Views @ [ view ]
                        PendingProv = model.PendingProv + 1 },
                    [ RecordProvenance(viewId, (curDigests, curGen, outputDigest)) ]
                | _ ->
                    // Unreachable in a well-formed run (only sensed-Ok in-scope views are regenerated).
                    fail ToolError (sprintf "internal: regenerated unknown/unsensed view '%s'" viewId) model

            | ProvenanceWritten(Error reason) ->
                fail ToolError ("failed to record provenance: " + reason) model

            | ProvenanceWritten(Ok()) -> maybeFinalize { model with PendingProv = model.PendingProv - 1 }

            | Wrote(Error reason) -> fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(Ok()) -> model, [ EmitSummary(render model model.Request.Format) ]

            | Emitted -> { model with Phase = Done }, []

    // Decide currency once BOTH the sensed digests and the recorded provenance have landed for every
    // in-scope view (the sensing barrier).
    and maybeDecideSensing (model: Model) : Model * Effect list =
        let ready =
            model.Phase = Loaded'
            && model.InScope
               |> List.forall (fun e -> Map.containsKey e.ViewId model.Sensed && Map.containsKey e.ViewId model.Recorded)

        if not ready then model, [] else decideSensing model

    and decideSensing (model: Model) : Model * Effect list =
        // Partition each in-scope view: resolved (Current / StaleUnresolved / WouldRegenerate) decisions are
        // recorded now; views that must be regenerated in WRITE mode are dispatched as `RegenerateView`
        // effects (none in `--dry-run`, FR-013) and decided when their result lands.
        let decideEntry (e: GenerationEntry) : Choice<ViewDecision, GenerationEntry> =
            match Map.find e.ViewId model.Sensed with
            | Error reason ->
                // A source whose digest cannot be sensed ⇒ stale-unresolved, NEVER fabricated current (FR-010).
                Choice1Of2 { Entry = e; Status = StaleUnresolved reason; Drifted = [] }
            | Ok(curDigests, curGen) ->
                let recorded = Map.find e.ViewId model.Recorded
                let current = (curDigests, curGen)

                if not (isStale recorded current) then
                    Choice1Of2 { Entry = e; Status = Current; Drifted = [] }
                else
                    let drifted = driftOf recorded current

                    if model.Request.DryRun then
                        Choice1Of2 { Entry = e; Status = WouldRegenerate drifted; Drifted = drifted }
                    else
                        Choice2Of2 e

        let decisions = model.InScope |> List.map decideEntry
        let resolved = decisions |> List.choose (function Choice1Of2 v -> Some v | _ -> None)
        let needsRegen = decisions |> List.choose (function Choice2Of2 e -> Some e | _ -> None)

        let model' =
            { model with
                Phase = Sensed'
                Views = model.Views @ resolved
                ExpectedRegen = needsRegen |> List.map (fun e -> e.ViewId) |> Set.ofList }

        match needsRegen with
        | [] -> finalize model'
        | _ -> model', (needsRegen |> List.map RegenerateView)

    // Finalize once the sensing barrier is passed, every dispatched regeneration has landed, and no
    // provenance write is outstanding.
    and maybeFinalize (model: Model) : Model * Effect list =
        let regenSettled =
            model.ExpectedRegen
            |> Set.forall (fun v ->
                model.Views
                |> List.exists (fun vw -> vw.Entry.ViewId = v && (match vw.Status with Regenerated _ -> true | _ -> false)))

        if model.Phase = Sensed' && regenSettled && model.PendingProv = 0 then
            finalize model
        else
            model, []

    // Assemble the RefreshDecision (views sorted to declared manifest order), project refresh.json when
    // requested, and emit the write/summary effects.
    and finalize (model: Model) : Model * Effect list =
        let orderIndex =
            match model.Manifest with
            | Some m -> m.Entries |> List.mapi (fun i e -> e.ViewId, i) |> Map.ofList
            | None -> Map.empty

        let views =
            model.Views
            |> List.sortBy (fun v -> Map.tryFind v.Entry.ViewId orderIndex |> Option.defaultValue System.Int32.MaxValue)

        let outcome, regen, current, unresolved, notEval = rollup views

        let decision =
            { Outcome = outcome
              DryRun = model.Request.DryRun
              Views = views
              RegeneratedCount = regen
              CurrentCount = current
              UnresolvedCount = unresolved
              NotEvaluatedCount = notEval }

        let needsArtifact =
            (match model.Request.Format with
             | Json
             | TextAndJson -> true
             | Text -> false)
            || Option.isSome model.Request.RefreshOut

        if needsArtifact then
            let doc = RefreshJson.ofRefreshDecision decision
            let path = model.Request.RefreshOut |> Option.defaultValue (CommandHost.under model.Request.Repo "refresh.json")

            { model with
                Phase = Persisted
                Views = views
                Decision = Some decision
                RefreshDoc = Some doc
                Exit = outcome },
            [ WriteArtifact(path, doc) ]
        else
            let model' =
                { model with
                    Phase = Persisted
                    Views = views
                    Decision = Some decision
                    Exit = outcome }

            model', [ EmitSummary(render model' Text) ]

    // ── render — the deterministic summary ──

    and statusToken (status: CurrencyStatus) : string =
        match status with
        | Current -> "current"
        | Regenerated _ -> "regenerated"
        | WouldRegenerate _ -> "would-regenerate"
        | StaleUnresolved _ -> "stale-unresolved"
        | NotEvaluated -> "not-evaluated"

    and outcomeToken (outcome: RefreshOutcome) : string =
        match outcome with
        | NothingToRefresh -> "nothing-to-refresh"
        | ViewsRegenerated -> "views-regenerated"
        | StaleUnresolved' -> "stale-unresolved"
        | UsageError' -> "usage-error"
        | InputUnavailable -> "input-unavailable"
        | ToolError -> "tool-error"

    and viewLine (v: ViewDecision) : string =
        let drifted =
            match v.Drifted with
            | [] -> ""
            | cats -> "   drifted: " + (cats |> List.map categoryToken |> String.concat ", ")

        let reason =
            match v.Status with
            | StaleUnresolved r -> "\n    " + r
            | _ -> ""

        sprintf "  %s [%s] %s%s%s" v.Entry.ViewId (statusToken v.Status) v.Entry.OutputPath drifted reason

    and renderText (model: Model) : string =
        match model.Decision with
        | None ->
            model.Diagnostics
            |> List.map (fun d -> "error: " + d.Message)
            |> String.concat "\n"
        | Some decision ->
            let mode = if decision.DryRun then " (dry-run)" else ""

            let header =
                sprintf
                    "refresh: %s%s (regenerated %d, current %d, unresolved %d, not-evaluated %d)"
                    (outcomeToken decision.Outcome)
                    mode
                    decision.RegeneratedCount
                    decision.CurrentCount
                    decision.UnresolvedCount
                    decision.NotEvaluatedCount

            (header :: (decision.Views |> List.map viewLine)) |> String.concat "\n"

    // The Json form IS the F057 refresh.json document verbatim (so `--json` stdout equals the persisted
    // file byte-for-byte). The human text is suppressed under `Json`.
    and renderJson (model: Model) : string =
        match model.RefreshDoc with
        | Some doc -> doc
        | None ->
            model.Diagnostics
            |> List.map (fun d -> System.Text.Json.JsonSerializer.Serialize d.Message)
            |> String.concat ","
            |> sprintf "{\"errors\":[%s]}"

    and render (model: Model) (format: OutputFormat) : string =
        match format with
        | Text -> renderText model
        | TextAndJson -> renderText model
        | Json -> renderJson model
