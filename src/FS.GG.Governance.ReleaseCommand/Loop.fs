// The PURE MVU core of the `fsgg release` host command (F055, grown by 065 F26 host wiring). Visibility
// lives in Loop.fsi (Principle II) — this file carries NO top-level access modifiers. `parse`/`init`/
// `update`/`render`/`exitCode` perform NO I/O, NO git, NO clock. It re-derives, re-classifies, and
// re-serializes nothing the cores fixed: the verdict comes from F053 `Release.evaluateRelease` (verbatim),
// the pack evidence from F26 `Pack.evaluatePack`/`factContributions`, the report from `Report.assemble`,
// and the document bytes from `ReleaseJson.ofReleaseReport` (v2) / `AttestationJson.ofAttestation`.

namespace FS.GG.Governance.ReleaseCommand

open System.Security.Cryptography
open System.Text
open FS.GG.Governance.Config.Model                 // SurfaceId, EnvironmentClass
open FS.GG.Governance.Enforcement.Enforcement       // Severity, Advisory, Blocking, Profile, RunMode
open FS.GG.Governance.Ship.Model                    // Verdict, ExitCodeBasis, Pass, Fail, Clean, Blocked
open FS.GG.Governance.FreshnessKey.Model            // Revision, RuleHash, GeneratorVersion, ArtifactHash
open FS.GG.Governance.Provenance.Model              // BuilderIdentity
open FS.GG.Governance.GateExecution.Model           // GateCommand
open FS.GG.Governance.CommandKind                   // Audit.auditSnapshot
open FS.GG.Governance.CommandKind.Model             // KindedCommandRun, AuditSnapshot
open FS.GG.Governance.PackEvidence                  // Pack.evaluatePack/factContributions
open FS.GG.Governance.PackEvidence.Model            // PackOutcome, PackArtifact, PackEvidenceSet, PackVerdict
open FS.GG.Governance.Attestation                   // Attestation.summarize
open FS.GG.Governance.Attestation.Model             // AttestationSummary
open FS.GG.Governance.ReleaseReport                 // Report.assemble
open FS.GG.Governance.ReleaseReport.Model           // ReleaseReport
open FS.GG.Governance.ValidationMatrix              // Matrix.decideMatrix
open FS.GG.Governance.ValidationMatrix.Model        // MatrixPlan, MatrixBoundary
open FS.GG.Governance.CostBudget                    // Budget.budgetFor
open FS.GG.Governance.ReleaseRules                  // Release.evaluateRelease, Release.releaseRuleKindToken
open FS.GG.Governance.ReleaseRules.Model             // ReleaseDecision, ReleaseFacts, FactState, EnforcedReleaseFinding
open FS.GG.Governance.ReleaseFactsSensing.Model      // SourceLayout, ReleaseExpectations, SensedRelease
open FS.GG.Governance.ReleaseJson                   // ReleaseJson.ofReleaseReport
open FS.GG.Governance.AttestationJson               // AttestationJson.ofAttestation
open FS.GG.Governance.ReleaseDeclaration            // 065: the shared Declaration leaf (was row-local)
open FS.GG.Governance.CommandHost                   // 075: shared host skeleton — `under`

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    type OutputFormat =
        | Text
        | Json
        | TextAndJson

    type RunRequest =
        { Repo: string
          Format: OutputFormat
          ReleaseOut: string
          AttestationOut: string }

    type UsageError = { Message: string }

    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    type ArtifactKind =
        | ReleaseArtifact
        | AttestationArtifact

    type Effect =
        | LoadDeclaration of repo: string
        | SenseRelease of layout: SourceLayout * expectations: ReleaseExpectations
        | PackProjects of (SurfaceId * GateCommand) list
        | SenseProvenance
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | EmitSummary of text: string

    type Msg =
        | Begin
        | DeclarationLoaded of Result<Declaration.ReleaseDeclaration, Declaration.DeclError>
        | Sensed of SensedRelease
        | PacksRun of PackOutcome list
        | ProvenanceSensed of head: Revision * environment: EnvironmentClass * builder: BuilderIdentity
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
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
          Packs: PackOutcome list option
          Head: Revision option
          Environment: EnvironmentClass option
          Builder: BuilderIdentity option
          PackEvidence: PackEvidenceSet option
          Snapshot: AuditSnapshot option
          Attestation: AttestationSummary option
          Report: ReleaseReport option
          Matrix: MatrixPlan option
          Decision: ReleaseDecision option
          ReleaseDoc: string option
          AttestationDoc: string option
          Written: Set<ArtifactKind>
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    // ── exitCode (cli.md exit-code table) — total, no wildcard ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
        | Blocked -> 1
        | UsageError' -> 2
        | InputUnavailable -> 3
        | ToolError -> 4

    // ── parse — a pure, total argv matcher; usage problems are values, never throws ──

    type ParseAcc =
        { Repo: string option
          Format: string option
          Out: string option
          AttestationOut: string option }

    let emptyAcc =
        { Repo = None
          Format = None
          Out = None
          AttestationOut = None }

    let parse (argv: string list) : Result<RunRequest, UsageError> =
        let rec go (acc: ParseAcc) (rest: string list) : Result<ParseAcc, UsageError> =
            match rest with
            | [] -> Ok acc
            | "--repo" :: v :: more -> go { acc with Repo = Some v } more
            | "--repo" :: [] -> Error { Message = "missing value for flag: --repo" }
            | "--format" :: v :: more -> go { acc with Format = Some v } more
            | "--format" :: [] -> Error { Message = "missing value for flag: --format" }
            | "--out" :: v :: more -> go { acc with Out = Some v } more
            | "--out" :: [] -> Error { Message = "missing value for flag: --out" }
            | "--attestation-out" :: v :: more -> go { acc with AttestationOut = Some v } more
            | "--attestation-out" :: [] -> Error { Message = "missing value for flag: --attestation-out" }
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
                          ReleaseOut = acc.Out |> Option.defaultValue (CommandHost.under repo "release.json")
                          AttestationOut = acc.AttestationOut |> Option.defaultValue (CommandHost.under repo "readiness/attestation.json") }

    // ── init (Principle IV) — initial Model + first effects ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Declaration = None
              Sensed = None
              Packs = None
              Head = None
              Environment = None
              Builder = None
              PackEvidence = None
              Snapshot = None
              Attestation = None
              Report = None
              Matrix = None
              Decision = None
              ReleaseDoc = None
              AttestationDoc = None
              Written = Set.empty
              Diagnostics = []
              Exit = Success }

        model, [ LoadDeclaration request.Repo; SenseProvenance ]

    // ── pure host-edge composition helpers (065) — no I/O ──

    /// The pack commands to run, in declared order.
    let packCommandsOf (decl: Declaration.ReleaseDeclaration) : (SurfaceId * GateCommand) list =
        decl.PackableProjects |> List.map (fun p -> p.Surface, p.PackCommand)

    /// The released-version baselines map for `evaluatePack` (a project with no baseline ⇒ first release).
    let baselinesOf (decl: Declaration.ReleaseDeclaration) : Map<SurfaceId, string> =
        decl.PackableProjects
        |> List.choose (fun p -> p.Baseline |> Option.map (fun b -> p.Surface, b))
        |> Map.ofList

    /// Overlay the pack `factContributions` onto the F54 sensed facts: packed evidence wins on the three
    /// pack families (D1). `factContributions` carries ONLY the pack families (empty when no packable
    /// projects), so a plain `Map.fold` overlay never disturbs a non-pack family.
    let mergeFacts (sensed: SensedRelease) (contribs: Map<ReleaseRuleKind, FactState>) : ReleaseFacts =
        { States = (sensed.Facts.States, contribs) ||> Map.fold (fun acc k v -> Map.add k v acc) }

    /// A deterministic, machine/clock-independent rule hash derived from the declared rules (the attestation
    /// materials' rule identity). SHA256 over the canonical, already-sorted rule list — byte-identical for
    /// identical declared rules, different when a rule changes.
    let ruleHashOf (rules: ReleaseRule list) : RuleHash =
        let canon =
            rules
            |> List.map (fun r ->
                let (SurfaceId s) = r.Surface
                sprintf "%s|%s|%A|%A" (Release.releaseRuleKindToken r.Kind) s r.BaseSeverity r.Maturity)
            |> String.concat ";"

        use sha = SHA256.Create()
        let hex = canon |> Encoding.UTF8.GetBytes |> sha.ComputeHash |> System.Convert.ToHexString
        RuleHash(hex.ToLowerInvariant())

    /// The real packed-artifact digests (Packed outcomes only — a failed/no-artifact pack yields no digest,
    /// hence no attested subject, FR-007).
    let digestsOf (pack: PackEvidenceSet) : ArtifactHash list =
        pack.Verdicts
        |> List.choose (fun (v: PackVerdict) ->
            match v.Outcome with
            | Packed(artifact, _) -> Some artifact.Digest
            | PackedNoArtifact _
            | PackFailed _ -> None)

    /// The release attestation snapshot (D2): `base = head = sourceCommit` (a release attests a product
    /// state, not a diff range); digests are the real packed digests; runs are the recorded `Pack` runs;
    /// rule hash from the declared rules; generator/environment/builder normalized (no username/host/clock).
    // STAYS LOCAL (075 research D5, FR-008): a different function that happens to share the name `buildSnapshot`
    // — it takes `ReleaseDeclaration * PackEvidenceSet`, not the Verify↔Ship `KindedCommandRun list`. The
    // shared `CommandHost.buildSnapshot` form does NOT fit, so this is not duplication to remove.
    let buildSnapshot (model: Model) (decl: Declaration.ReleaseDeclaration) (pack: PackEvidenceSet) : AuditSnapshot =
        let head = model.Head |> Option.defaultValue (Revision "")
        let ruleHash = ruleHashOf decl.Rules
        let genVer = GeneratorVersion "fsgg"
        let env = model.Environment |> Option.defaultValue LocalOrCi
        let builder = model.Builder |> Option.defaultValue (BuilderIdentity "fsgg")
        Audit.auditSnapshot head head head ruleHash genVer (digestsOf pack) pack.Runs env builder

    // Map the decision's typed ExitCodeBasis to the process-level ExitDecision (cli.md).
    let exitFromBasis (basis: ExitCodeBasis) : ExitDecision =
        match basis with
        | Clean -> Success
        | ExitCodeBasis.Blocked -> Blocked

    // Short-circuit to Done with a mapped ExitDecision + an actionable diagnostic (no clock/abs-path/env).
    let fail (category: ExitDecision) (message: string) (model: Model) : Model * Effect list =
        { model with
            Phase = Done
            Exit = category
            Diagnostics = model.Diagnostics @ [ { Category = category; Message = message } ] },
        []

    // ── render — the deterministic summary (mutually recursive with update) ──

    let severityToken (s: Severity) : string =
        match s with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    let surfaceValue (SurfaceId s) : string = s

    let ruleLine (e: EnforcedReleaseFinding) : string =
        let f = e.Finding

        sprintf
            "  %s <- %s   (base %s, effective %s)\n    %s"
            (Release.releaseRuleKindToken f.Kind)
            (surfaceValue f.Surface)
            (severityToken f.BaseSeverity)
            (severityToken e.Decision.EffectiveSeverity)
            f.Reason

    let section (name: string) (items: EnforcedReleaseFinding list) : string list =
        match items with
        | [] -> [ sprintf "%s: none" name ]
        | xs -> (sprintf "%s: %d" name (List.length xs)) :: (xs |> List.map ruleLine)

    let renderText (model: Model) : string =
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

    // The Json form IS the F055 `release.json` v2 document verbatim.
    let renderJson (model: Model) : string =
        match model.ReleaseDoc with
        | Some doc -> doc
        | None ->
            model.Diagnostics
            |> List.map (fun d -> System.Text.Json.JsonSerializer.Serialize d.Message)
            |> String.concat ","
            |> sprintf "{\"errors\":[%s]}"

    let render (model: Model) (format: OutputFormat) : string =
        match format with
        | Text -> renderText model
        | TextAndJson -> renderText model
        | Json -> renderJson model

    // ── the three-way join composition (FR-001..FR-007) ──

    /// Fire once the sensed facts, the pack outcomes, AND the provenance senses have all landed (Phase still
    /// `Loaded'`): build the pack evidence, overlay it on the F54 facts, evaluate the release rules VERBATIM,
    /// assemble the snapshot/attestation/report, project both documents, and emit the two atomic writes.
    let tryCompose (model: Model) : Model * Effect list =
        match model.Phase, model.Declaration, model.Sensed, model.Packs, model.Head with
        | Loaded', Some decl, Some sensed, Some outcomes, Some _ ->
            // US4 (FR-014): an UNREADABLE pack OUTPUT is an input-unavailable signal (exit 3) — distinct from
            // a tool defect (exit 4) and from an unmet-rule block (exit 1). Surface a named source, block, and
            // emit NO writes (no hollow attestation, no fabricated pass). An absent head-revision degrades to a
            // deterministic sentinel (D2) and a missing publish plan blocks through the verbatim rule path.
            let unreadable =
                outcomes
                |> List.choose (fun o ->
                    match o with
                    | PackedNoArtifact(SurfaceId s, ArtifactUnreadable reason, _) -> Some(s, reason)
                    | _ -> None)

            match unreadable with
            | (surface, reason) :: _ ->
                fail InputUnavailable (sprintf "pack output unreadable for surface '%s': %s" surface reason) model
            | [] ->

            let pack = Pack.evaluatePack (baselinesOf decl) outcomes
            let merged = mergeFacts sensed (Pack.factContributions pack)
            let decision = Release.evaluateRelease decl.Rules merged
            let snapshot = buildSnapshot model decl pack
            let attestation = Attestation.summarize snapshot pack
            let report = Report.assemble decision sensed pack attestation
            let matrix = Matrix.decideMatrix (Budget.budgetFor Profile.Release RunMode.Release) ScheduledOrRelease decl.Matrix
            let releaseDoc = ReleaseJson.ofReleaseReport report
            let attestationDoc = AttestationJson.ofAttestation report.Attestation

            { model with
                Phase = Sensed'
                PackEvidence = Some pack
                Decision = Some decision
                Snapshot = Some snapshot
                Attestation = Some attestation
                Report = Some report
                Matrix = Some matrix
                ReleaseDoc = Some releaseDoc
                AttestationDoc = Some attestationDoc
                Exit = exitFromBasis decision.ExitCodeBasis },
            [ WriteArtifact(ReleaseArtifact, model.Request.ReleaseOut, releaseDoc)
              WriteArtifact(AttestationArtifact, model.Request.AttestationOut, attestationDoc) ]
        | _ -> model, []

    // ── update — the whole composition; TOTAL, never throws ──

    let update (msg: Msg) (model: Model) : Model * Effect list =
        if model.Phase = Done then
            model, []
        else
            match msg with
            | Begin -> model, []

            | DeclarationLoaded(Error e) ->
                fail InputUnavailable ("release declaration unavailable: " + e.Reason) model

            | DeclarationLoaded(Ok decl) ->
                { model with
                    Phase = Loaded'
                    Declaration = Some decl },
                [ SenseRelease(decl.Layout, decl.Expectations); PackProjects(packCommandsOf decl) ]

            | Sensed sensed -> tryCompose { model with Sensed = Some sensed }

            | PacksRun outcomes -> tryCompose { model with Packs = Some outcomes }

            | ProvenanceSensed(head, environment, builder) ->
                tryCompose
                    { model with
                        Head = Some head
                        Environment = Some environment
                        Builder = Some builder }

            | Wrote(_, Error reason) ->
                // A write failure is ALWAYS a ToolError (exit 4), NEVER a blocked verdict.
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(kind, Ok()) ->
                // Two artifacts are written (release.json v2 + attestation.json), so two `Wrote(Ok)` acks
                // arrive in one batch. Only the FIRST (Phase still Sensed') schedules the summary; the second
                // ack is inert (Phase = Persisted).
                let written = Set.add kind model.Written

                match model.Phase with
                | Persisted
                | Done -> { model with Written = written }, []
                | _ ->
                    { model with
                        Phase = Persisted
                        Written = written },
                    [ EmitSummary(render model model.Request.Format) ]

            | Emitted -> { model with Phase = Done }, []
