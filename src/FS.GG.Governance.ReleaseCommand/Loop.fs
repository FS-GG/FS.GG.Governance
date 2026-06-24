// The PURE MVU core of the `fsgg release` host command (F055). Visibility lives in Loop.fsi
// (Principle II) — this file carries NO top-level access modifiers; helpers absent from the signature are
// hidden by it. `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock: the whole
// parse -> load-declaration -> sense -> EVALUATE -> PROJECT -> summarize -> EXIT-FROM-BASIS composition is
// a pure transition over Model + Msg emitting Effect data the edge Interpreter executes (Principle IV). It
// re-derives, re-classifies, and re-serializes nothing the cores fixed: the verdict comes from F053
// `Release.evaluateRelease`, the document bytes from F055 `ReleaseJson.ofRelease`. Like `ship`, it maps the
// `ReleaseDecision.ExitCodeBasis` to a process exit category, including a distinct `Blocked` code.

namespace FS.GG.Governance.ReleaseCommand

open FS.GG.Governance.Config.Model                 // SurfaceId
open FS.GG.Governance.Enforcement.Enforcement       // Severity, Advisory, Blocking
open FS.GG.Governance.Ship.Model                    // Verdict, ExitCodeBasis, Pass, Fail, Clean, Blocked
open FS.GG.Governance.ReleaseRules                  // Release.evaluateRelease, Release.releaseRuleKindToken
open FS.GG.Governance.ReleaseRules.Model             // ReleaseDecision, EnforcedReleaseFinding
open FS.GG.Governance.ReleaseFactsSensing.Model      // SourceLayout, ReleaseExpectations, SensedRelease
open FS.GG.Governance.ReleaseJson                   // ReleaseJson.ofRelease

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    type OutputFormat =
        | Text
        | Json
        | TextAndJson

    type RunRequest =
        { Repo: string
          Format: OutputFormat
          ReleaseOut: string }

    type UsageError = { Message: string }

    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    type Effect =
        | LoadDeclaration of repo: string
        | SenseRelease of layout: SourceLayout * expectations: ReleaseExpectations
        | WriteArtifact of path: string * content: string
        | EmitSummary of text: string

    type Msg =
        | Begin
        | DeclarationLoaded of Result<Declaration.ReleaseDeclaration, Declaration.DeclError>
        | Sensed of SensedRelease
        | Wrote of Result<unit, string>
        | Emitted

    type Diagnostic =
        { Category: ExitDecision
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
          Declaration: Declaration.ReleaseDeclaration option
          Sensed: SensedRelease option
          Decision: ReleaseDecision option
          ReleaseDoc: string option
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    // ── exitCode (cli.md exit-code table) — total, no wildcard; `Blocked` 1 reserved for a blocked verdict ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
        | Blocked -> 1
        | UsageError' -> 2
        | InputUnavailable -> 3
        | ToolError -> 4

    // ── parse — a pure, total argv matcher; usage problems are values, never throws ──

    // Hidden accumulator (absent from Loop.fsi).
    type ParseAcc =
        { Repo: string option
          Format: string option
          Out: string option }

    let emptyAcc = { Repo = None; Format = None; Out = None }

    // Join a repo dir with a default relative artifact location. A `.` (or empty) repo yields the clean
    // relative form (`release.json`); any other repo is prefixed so the artifact lands inside it. Pure
    // string composition — no filesystem, no clock, no absolute-path resolution.
    let under (repo: string) (rel: string) : string =
        if repo = "." || repo = "" then rel else repo.TrimEnd('/') + "/" + rel

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // Flags only — NO leading `release` subcommand token is expected or stripped (cli.md §subcommand
        // mapping). A leading bare `release` (or any unknown leading positional) is an unknown argument.
        let rec go (acc: ParseAcc) (rest: string list) : Result<ParseAcc, UsageError> =
            match rest with
            | [] -> Ok acc
            | "--repo" :: v :: more -> go { acc with Repo = Some v } more
            | "--repo" :: [] -> Error { Message = "missing value for flag: --repo" }
            | "--format" :: v :: more -> go { acc with Format = Some v } more
            | "--format" :: [] -> Error { Message = "missing value for flag: --format" }
            | "--out" :: v :: more -> go { acc with Out = Some v } more
            | "--out" :: [] -> Error { Message = "missing value for flag: --out" }
            | other :: _ -> Error { Message = "unknown argument: " + other }

        match go emptyAcc argv with
        | Error e -> Error e
        | Ok acc ->
            match acc.Repo with
            | None -> Error { Message = "missing required flag: --repo <dir>" }
            | Some repo ->
                let formatResult =
                    match acc.Format with
                    | None -> Ok Text
                    | Some "text" -> Ok Text
                    | Some "json" -> Ok Json
                    | Some "both" -> Ok TextAndJson
                    | Some other -> Error { Message = "unrecognized --format: " + other + " (expected text|json|both)" }

                match formatResult with
                | Error e -> Error e
                | Ok format ->
                    Ok
                        { Repo = repo
                          Format = format
                          ReleaseOut = acc.Out |> Option.defaultValue (under repo "release.json") }

    // ── init (Principle IV) — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Declaration = None
              Sensed = None
              Decision = None
              ReleaseDoc = None
              Diagnostics = []
              Exit = Success }

        model, [ LoadDeclaration request.Repo ]

    // ── update — the whole composition; TOTAL, never throws (FR-013) ──

    // Short-circuit to Done with a mapped ExitDecision + an actionable diagnostic (no clock/abs-path/env).
    let fail (category: ExitDecision) (message: string) (model: Model) : Model * Effect list =
        { model with
            Phase = Done
            Exit = category
            Diagnostics = model.Diagnostics @ [ { Category = category; Message = message } ] },
        []

    // Map the decision's typed ExitCodeBasis to the process-level ExitDecision (cli.md).
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

            | DeclarationLoaded(Error e) ->
                // An absent/invalid declaration is INPUT-unavailable (exit 3) — never a tool defect, never a
                // blocked verdict. No sensing/write is emitted (data-model state transitions).
                fail InputUnavailable ("release declaration unavailable: " + e.Reason) model

            | DeclarationLoaded(Ok decl) ->
                { model with
                    Phase = Loaded'
                    Declaration = Some decl },
                [ SenseRelease(decl.Layout, decl.Expectations) ]

            | Sensed sensed ->
                match model.Declaration with
                | None ->
                    // Unreachable in a well-formed run (sensing follows a loaded declaration); fail safe.
                    fail ToolError "internal: sensed before declaration loaded" model
                | Some decl ->
                    // The composition (FR-003/FR-004): the verdict is decided HERE by F053
                    // `evaluateRelease` (verbatim), the exit category mapped from its `ExitCodeBasis`. When
                    // the format requests JSON, the F055 projection is computed BEFORE the write effect.
                    let decision = Release.evaluateRelease decl.Rules sensed.Facts
                    let exit = exitFromBasis decision.ExitCodeBasis

                    let model' =
                        { model with
                            Phase = Sensed'
                            Sensed = Some sensed
                            Decision = Some decision
                            Exit = exit }

                    match model'.Request.Format with
                    | Text -> model', [ EmitSummary(render model' Text) ]
                    | Json
                    | TextAndJson ->
                        let doc = ReleaseJson.ofRelease decision sensed
                        { model' with ReleaseDoc = Some doc }, [ WriteArtifact(model'.Request.ReleaseOut, doc) ]

            | Wrote(Error reason) ->
                // A write failure is ALWAYS a ToolError (exit 4), NEVER a blocked verdict (FR-011).
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(Ok()) -> { model with Phase = Persisted }, [ EmitSummary(render model model.Request.Format) ]

            | Emitted -> { model with Phase = Done }, []

    // ── render — the deterministic summary ──

    and severityToken (s: Severity) : string =
        match s with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    and surfaceValue (SurfaceId s) : string = s

    and ruleLine (e: EnforcedReleaseFinding) : string =
        let f = e.Finding

        sprintf
            "  %s <- %s   (base %s, effective %s)\n    %s"
            (Release.releaseRuleKindToken f.Kind)
            (surfaceValue f.Surface)
            (severityToken f.BaseSeverity)
            (severityToken e.Decision.EffectiveSeverity)
            f.Reason

    and section (name: string) (items: EnforcedReleaseFinding list) : string list =
        match items with
        | [] -> [ sprintf "%s: none" name ]
        | xs -> (sprintf "%s: %d" name (List.length xs)) :: (xs |> List.map ruleLine)

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

            let header = sprintf "release: verdict %s (exit-code basis: %s)" verdictToken basisToken

            [ [ header; "" ]
              section "blockers" decision.Blockers
              section "warnings" decision.Warnings
              section "passing" decision.Passing ]
            |> List.concat
            |> String.concat "\n"

    // The Json form IS the F055 `release.json` document verbatim (so `--format json` stdout equals the
    // persisted file byte-for-byte). The human text is suppressed under `Json`.
    and renderJson (model: Model) : string =
        match model.ReleaseDoc with
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
