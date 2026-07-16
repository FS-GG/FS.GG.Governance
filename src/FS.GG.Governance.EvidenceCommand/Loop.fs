// The PURE MVU core of the `fsgg evidence` host command (069). Visibility lives in Loop.fsi (Principle II).
// `parse`/`init`/`toDocument`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock — the whole
// sense → build-closure → project → persist → summarize → exit composition is a pure transition over
// `Model` + `Msg`, emitting `Effect` data the edge `Interpreter` executes. It composes `Kernel.Evidence`
// (`build`/`effective`) HERE (pure functions) to recover the `GraphError` `Project.evidenceReport` swallows to
// `Map.empty` (D3/FR-004), without modifying that function. Effective evidence is INFORMATION: the exit code
// is OPERATIONAL ONLY (FR-007).

namespace FS.GG.Governance.EvidenceCommand

open System.IO
open FS.GG.Governance.Kernel
open FS.GG.Governance.Cli
open FS.GG.Governance.EvidenceJson

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    // The model — implementations of the types declared in Loop.fsi (the .fsi is the signature; the
    // definitions live here).
    type OutputFormat =
        | Human
        | Json

    type RunRequest =
        { Repo: string
          Out: string
          Format: OutputFormat
          ExplicitPlain: bool }

    type UsageError =
        | UnknownFlag of string
        | UnexpectedArgument of string
        | MissingValue of flag: string
        | BadFormat of value: string

    type ExitDecision =
        | Success
        | UsageError'
        | InputUnavailable
        | ToolError

    type ReportFault =
        | InputMissing of reason: string
        | ToolFault of reason: string

    type Effect =
        | SenseReport of repo: string
        | WriteArtifact of path: string * content: string
        | EmitSummary of text: string

    type Msg =
        | Begin
        | Reported of Result<ProjectEvidenceReport, ReportFault>
        | Wrote of Result<unit, string>
        | Emitted

    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    type Phase =
        | Parsed
        | Sensed
        | Projected
        | Persisted
        | Done

    type Model =
        { Request: RunRequest
          Phase: Phase
          Report: ProjectEvidenceReport option
          Document: EvidenceDocument option
          Doc: string option
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    // ── parse ──

    /// Mutable-free accumulator threaded through the argv fold.
    type private Acc =
        { Repo: string
          Out: string option
          Format: OutputFormat
          ExplicitPlain: bool }

    let private defaultOut (repo: string) = Path.Combine(repo, "readiness", "evidence.json")

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // Tolerate (and drop) a leading `evidence` verb so both `fsgg evidence …` and a direct `… ` invocation
        // parse identically (the cache-eligibility parser precedent).
        let argv =
            match argv with
            | "evidence" :: rest -> rest
            | _ -> argv

        let rec loop (acc: Acc) (args: string list) : Result<Acc, UsageError> =
            match args with
            | [] -> Ok acc
            // M-CLI-3 (#49): a `--`-prefixed next token is NOT a value — reject as MissingValue rather than
            // silently swallowing the following flag.
            | "--repo" :: value :: rest when not (value.StartsWith "--") -> loop { acc with Repo = value } rest
            | "--out" :: value :: rest when not (value.StartsWith "--") -> loop { acc with Out = Some value } rest
            | "--format" :: value :: rest when not (value.StartsWith "--") ->
                match value with
                // CLI-3: `text` is an ADDITIVE synonym for the canonical `human` token, so the plain
                // spelling accepted by route/ship/verify/release/refresh/dispatcher works here too
                // (`evidence --format text` no longer errors). `human` stays the canonical/default token;
                // this is backward-compatible and does not contradict ADR-0006 (nothing is renamed or
                // removed) — it realizes that ADR's deferred `--format text|json|both`-everywhere direction
                // one non-breaking step early. See docs/decisions/0006-cli-format-flag-vocabularies.md.
                | "human" | "text" -> loop { acc with Format = Human } rest
                | "json" -> loop { acc with Format = Json } rest
                | other -> Error(BadFormat other)
            // M-CLI-7 (#49): `--plain` is an ADDITIVE ANSI-free signal that never overrides `--format` (the
            // `Json` branch still wins). EvidenceCommand emits no ANSI, so it is an accepted, documented no-op
            // here — see the usage text and `render`; `ExplicitPlain` is intentionally not consumed.
            | "--plain" :: rest -> loop { acc with ExplicitPlain = true } rest
            // Missing value: empty rest OR a `--`-prefixed next token (the guarded arms above declined it).
            | ("--repo" | "--out" | "--format") :: _ -> Error(MissingValue(List.head args))
            | flag :: _ when flag.StartsWith "--" -> Error(UnknownFlag flag)
            // CLI-5: a stray non-`--` positional is an UnexpectedArgument, not a mis-labelled "unknown flag".
            | other :: _ -> Error(UnexpectedArgument other)

        loop
            { Repo = "."
              Out = None
              Format = Human
              ExplicitPlain = false }
            argv
        |> Result.map (fun acc ->
            { Repo = acc.Repo
              Out = acc.Out |> Option.defaultValue (defaultOut acc.Repo)
              Format = acc.Format
              ExplicitPlain = acc.ExplicitPlain })

    // ── init ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Report = None
              Document = None
              Doc = None
              Diagnostics = []
              Exit = Success }

        model, [ SenseReport request.Repo ]

    // ── report → document (the pure host mapping; recovers the swallowed GraphError, D3/FR-004) ──

    let private ruleIdText (rule: RuleId) =
        let (RuleId value) = rule
        value

    /// MVP freshness mapping (D4/INV-6): a resolved `Fresh` maps to `Fresh`; a bare `Stale` with no resolved
    /// cause and an unsensed `None` BOTH map to `Unknown` — never a guessed cause, never a guessed `Fresh`.
    let private freshnessOf (freshness: Freshness option) : NodeFreshness =
        match freshness with
        | Some Freshness.Fresh -> NodeFreshness.Fresh
        | Some Freshness.Stale -> NodeFreshness.Unknown
        | None -> NodeFreshness.Unknown

    let toDocument (report: ProjectEvidenceReport) : EvidenceDocument =
        // Declared nodes for the closure: a report node with `Declared = None` is a sensing defect, dropped
        // here (never fabricated). Node ids are strings, so `Evidence.build` yields a `GraphError<string>`
        // directly usable as the `Malformed` content.
        let declared =
            report.Nodes |> List.choose (fun n -> n.Declared |> Option.map (fun d -> n.Id, d))

        let content =
            match Evidence.build declared report.Dependencies with
            | Error failure -> Malformed failure
            | Ok graph ->
                let effective = Evidence.effective graph

                let nodes =
                    report.Nodes
                    |> List.choose (fun n ->
                        match n.Declared with
                        | None -> None
                        | Some declaredState ->
                            Some
                                { Id = n.Id
                                  Declared = declaredState
                                  Effective = Map.tryFind n.Id effective |> Option.defaultValue declaredState
                                  Freshness = freshnessOf n.Freshness
                                  Source = n.Source })

                WellFormed(nodes, report.Dependencies)

        { Content = content
          Disclosures = report.Disclosures |> List.map (fun d -> ruleIdText d.Rule, d.Justification) }

    // ── update ──

    let private diag (category: ExitDecision) (message: string) = { Category = category; Message = message }

    let rec update (msg: Msg) (model: Model) : Model * Effect list =
        match msg with
        // F13 (#49): once the pipeline has decided (Done), every further reified Msg is inert — matches the
        // guard every sibling host (Route/Ship/Verify) documents. Without this, a duplicate Wrote/Reported
        // after Done would re-mutate Phase and re-schedule effects.
        | _ when model.Phase = Done -> model, []
        | Begin -> model, [ SenseReport model.Request.Repo ]

        | Reported(Error(InputMissing reason)) ->
            { model with
                Phase = Done
                Exit = InputUnavailable
                Diagnostics = model.Diagnostics @ [ diag InputUnavailable reason ] },
            []

        | Reported(Error(ToolFault reason)) ->
            { model with
                Phase = Done
                Exit = ToolError
                Diagnostics = model.Diagnostics @ [ diag ToolError reason ] },
            []

        | Reported(Ok report) ->
            let document = toDocument report
            let doc = EvidenceJson.ofReport document

            { model with
                Phase = Projected
                Report = Some report
                Document = Some document
                Doc = Some doc },
            [ WriteArtifact(model.Request.Out, doc) ]

        | Wrote(Error reason) ->
            { model with
                Phase = Done
                Exit = ToolError
                Diagnostics = model.Diagnostics @ [ diag ToolError ("evidence.json write failed: " + reason) ] },
            []

        | Wrote(Ok()) ->
            let next = { model with Phase = Persisted }
            next, [ EmitSummary(render next model.Request.Format) ]

        | Emitted -> { model with Phase = Done; Exit = Success }, []

    // ── render (pure summary; no HumanText/Spectre dependency — keeps cores untouched) ──

    and render (model: Model) (format: OutputFormat) : string =
        match format, model.Doc with
        | Json, Some doc -> doc
        | Json, None -> EvidenceJson.ofReport { Content = WellFormed([], []); Disclosures = [] }
        | Human, _ ->
            match model.Document with
            | None -> "evidence: no document"
            | Some document ->
                match document.Content with
                | Malformed failure ->
                    let kind =
                        match failure with
                        | Cycle cycle -> "cycle [" + String.concat "; " cycle + "]"
                        | UnknownNode node -> "unknownNode " + node
                        | AutoSyntheticDeclared node -> "autoSyntheticDeclared " + node

                    "evidence: graph failure: " + kind + " (no per-node map emitted)"
                | WellFormed(nodes, dependencies) ->
                    let header =
                        sprintf
                            "evidence: %d nodes, %d dependencies, %d disclosures"
                            (List.length nodes)
                            (List.length dependencies)
                            (List.length document.Disclosures)

                    let freshnessText (f: NodeFreshness) =
                        match f with
                        | NodeFreshness.Fresh -> "fresh"
                        | NodeFreshness.Stale _ -> "stale"
                        | NodeFreshness.Unresolved _ -> "unresolved"
                        | NodeFreshness.Unknown -> "unknown"

                    let stateText (s: EvidenceState) =
                        match s with
                        | Pending -> "Pending"
                        | Real -> "Real"
                        | Synthetic -> "Synthetic"
                        | Failed -> "Failed"
                        | Skipped -> "Skipped"
                        | AutoSynthetic -> "AutoSynthetic"

                    let lines =
                        nodes
                        |> List.sortBy (fun n -> n.Id)
                        |> List.map (fun n ->
                            sprintf
                                "  %s: declared=%s effective=%s freshness=%s [%s]"
                                n.Id
                                (stateText n.Declared)
                                (stateText n.Effective)
                                (freshnessText n.Freshness)
                                n.Source)

                    String.concat "\n" (header :: lines)

    // ── exitCode ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
        | UsageError' -> 2
        | InputUnavailable -> 3
        | ToolError -> 4
