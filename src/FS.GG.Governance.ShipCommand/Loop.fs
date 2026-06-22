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
open FS.GG.Governance.Snapshot.Model      // RepoSnapshot, ChangedPath
open FS.GG.Governance.Routing             // Routing.route
open FS.GG.Governance.Findings            // Findings.findUnknownGovernedPaths
open FS.GG.Governance.Findings.Model       // findingIdToken
open FS.GG.Governance.Gates               // Gates.buildRegistry
open FS.GG.Governance.Gates.Model          // gateIdValue
open FS.GG.Governance.Route               // Route.select
open FS.GG.Governance.Enforcement.Enforcement // RunMode, Profile, Severity, Recognized, recognizeMode, recognizeProfile
open FS.GG.Governance.Ship                // Ship.rollup
open FS.GG.Governance.Ship.Model           // ShipDecision, Verdict, ExitCodeBasis, EnforcedItem, EnforcedItemId
open FS.GG.Governance.AuditJson           // AuditJson.ofShipDecision

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
          AuditOut: string }

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
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | EmitSummary of text: string

    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        | Emitted

    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    type Phase =
        | Parsed
        | Sensed'
        | Loaded'
        | Rolled
        | Persisted
        | Done

    type Model =
        { Request: RunRequest
          Phase: Phase
          Candidates: GovernedPath list option
          Decision: ShipDecision option
          AuditDoc: string option
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
          AuditOut: string option }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Mode = None
          Profile = None
          Json = false
          AuditOut = None }

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
                          AuditOut = acc.AuditOut |> Option.defaultValue (under repo "readiness/audit.json") }

    // ── init (Principle IV) — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Candidates = None
              Decision = None
              AuditDoc = None
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
                    Candidates = Some candidates },
                [ LoadCatalog model.Request.Repo ]

            | Sensed(Error reason) -> fail InputUnavailable ("git sensing unavailable: " + reason) model

            | Loaded(Invalid diags) -> fail InputUnavailable (describeInvalid diags) model

            | Loaded(Valid facts) ->
                // The composition (FR-004): re-derive/re-sort/re-classify/re-serialize nothing — carry
                // the cores' values verbatim. The audit document is computed BEFORE the write (research
                // D10). The new-vs-F022 steps are `Ship.rollup` and `AuditJson.ofShipDecision`.
                let candidates = model.Candidates |> Option.defaultValue []
                let report = Routing.route facts candidates
                let registry = Gates.buildRegistry facts
                let findings = Findings.findUnknownGovernedPaths facts report
                let result = Route.select registry report findings
                let decision = Ship.rollup result model.Request.Mode model.Request.Profile
                // F045: `fsgg ship` resolves no freshness inputs yet, so the cache-eligibility report is
                // `None` — the document renders the not-evaluated section (v2) only; behavior preserved.
                let auditDoc = AuditJson.ofShipDecision decision None

                { model with
                    Phase = Rolled
                    Decision = Some decision
                    AuditDoc = Some auditDoc },
                [ WriteArtifact(AuditArtifact, model.Request.AuditOut, auditDoc) ]

            | Wrote(_, Error reason) ->
                // A write failure is ALWAYS a ToolError, NEVER a blocked verdict (FR-009).
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(_, Ok()) -> { model with Phase = Persisted }, [ EmitSummary(render model model.Request.Format) ]

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
