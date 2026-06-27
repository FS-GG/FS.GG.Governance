// The PURE MVU core of the `fsgg verify` host command (F056). Visibility lives in Loop.fsi (Principle II) —
// this file carries NO top-level access modifiers; helper types/functions absent from the signature are
// hidden by it. `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock: the whole
// scope -> load -> route -> registry -> findings -> select -> ROLLUP -> RUN/REUSE -> PROJECT -> persist-plan
// -> summarize -> EXIT-FROM-BASIS composition is a pure transition over Model + Msg emitting Effect data the
// edge Interpreter executes (Principle IV). It is the CLOSEST SIBLING of `fsgg ship` (F026): it threads the
// FIXED `RunMode.Verify` into the VERBATIM `Ship.rollup` (no `--mode` flag — FR-017), projects its own
// `verify.json` via `VerifyJson.ofVerifyDecision`, surfaces a first-class currency section, and reports
// "nothing to verify" for an empty selection. The verdict comes from F024 `Ship.rollup`; the verdict
// relocation from F052 `applyExecution`; the document bytes from F056 `VerifyJson`.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Config.Model       // GovernedPath, Validation, Valid/Invalid, normalizePath, diagnosticIdToken
open FS.GG.Governance.Snapshot.Model      // RepoSnapshot, ChangedPath, DiffRange, CommitId
open FS.GG.Governance.Routing             // Routing.route
open FS.GG.Governance.Findings            // Findings.findUnknownGovernedPaths
open FS.GG.Governance.Findings.Model       // findingIdToken
open FS.GG.Governance.Gates               // Gates.buildRegistry
open FS.GG.Governance.Gates.Model          // Gate, gateIdValue
open FS.GG.Governance.Route               // Route.select
open FS.GG.Governance.Enforcement.Enforcement // RunMode (Verify), Profile, Severity, Recognized, recognizeProfile
open FS.GG.Governance.Ship                // Ship.rollup
open FS.GG.Governance.Ship.Model           // ShipDecision, Verdict, ExitCodeBasis, EnforcedItem, EnforcedItemId
open FS.GG.Governance.VerifyJson           // VerifyJson.ofVerifyDecision
open FS.GG.Governance.HumanText           // F27 wiring (063): HumanText.ofVerifyDecision — the plain projection
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
open FS.GG.Governance.CommandHost             // 075: shared host skeleton — under/describeInvalid/emptySensedFacts/
                                              //   revOfCommit/baseHeadOf/persistedContent/kindOf/kindedRunsOf/
                                              //   buildSnapshot/GateClassification/executionPlan (ExitDecision/
                                              //   exitCode/fail/tryExecute stay LOCAL — type-divergent on Model/Effect)
// 065 wiring (US3): the declaration-gated advisory release-readiness preview (verify does NOT pack).
open FS.GG.Governance.ReleaseFactsSensing.Model   // SourceLayout, ReleaseExpectations, SensedRelease (F54)
open FS.GG.Governance.ValidationMatrix.Model       // MatrixPlan, MatrixBoundary (InnerLoop)
open FS.GG.Governance.ReleaseReport.Model          // VerifyReleasePreview
open FS.GG.Governance.ReleaseDeclaration           // Declaration (the shared leaf)

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
          Profile: Profile
          Format: OutputFormat
          VerifyOut: string
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
        | UnrecognizedProfile of string

    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    type ArtifactKind =
        | VerifyArtifact
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
        | SenseReleasePreview of repo: string
        | EmitSummary of text: string * human: (ReportView.ReportView * string) option * explicitPlain: bool
        // 067: sense + run the product-surface checks at the edge (the report is classified purely in update).
        | SenseSurfaces of report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport
        // F070: sense generated-view currency at the edge for `repo` (parse manifest, read provenance, sense).
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
        | ReleasePreviewSensed of (Declaration.ReleaseDeclaration * SensedRelease) option
        // 067: the deterministic, already-sorted findings from `Composition.run`, folded into the verdict.
        | SurfacesSensed of findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
        // F070: the deterministic stale-generated-view currency findings, folded into the verdict + projected.
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
          VerifyDoc: string option
          Snapshot: RepoSnapshot option
          SelectedGates: Gate list
          Sensed: SensedFacts option
          Store: ReuseStore option
          Tooling: ToolingFacts option
          Outcomes: (GateId * GateOutcome) list
          CurrencyNotes: string list
          StoreDegraded: bool
          PersistAcked: bool
          Environment: EnvironmentClass option
          Builder: BuilderIdentity option
          CacheDecision: CacheDecisionReport option
          Audit: AuditSnapshot option
          ReleaseDecl: Declaration.ReleaseDeclaration option
          ReleaseSensed: SensedRelease option
          ReleasePreview: VerifyReleasePreview option
          ReleaseMatrix: MatrixPlan option
          // 067: the surface-check findings sensed at the edge ([] until SurfacesSensed) + the readiness flag.
          SurfaceFindings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
          SurfacesPending: bool
          // F070: the stale-generated-view currency findings sensed at the edge ([] until ViewCurrencySensed).
          ViewCurrencyFindings: CE.CurrencyFinding list
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    // ── exitCode — total, no wildcard; `Blocked` 1 reserved for an unmet effective-blocking check ──

    let exitCode (decision: ExitDecision) : int =
        match decision with
        | Success -> 0
        | Blocked -> 1
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
          Profile: string option
          Json: bool
          VerifyOut: string option
          Store: string option
          Persist: bool
          Plain: bool
          CostBudgetOut: string option
          ProvenanceOut: string option }

    let emptyAcc =
        { Repo = None
          Paths = None
          Since = None
          Profile = None
          Json = false
          VerifyOut = None
          Store = None
          Persist = false
          Plain = false
          CostBudgetOut = None
          ProvenanceOut = None }


    let parse (argv: string list) : Result<RunRequest, UsageError> =
        // Tolerate (and drop) a leading `verify` verb — the verb this command implements.
        let tokens =
            match argv with
            | "verify" :: rest -> rest
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
            | "--profile" :: v :: more -> go { acc with Profile = Some v } more
            | "--profile" :: [] -> Error(MissingValue "--profile")
            | "--verify-out" :: v :: more -> go { acc with VerifyOut = Some v } more
            | "--verify-out" :: [] -> Error(MissingValue "--verify-out")
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
            // NO `--mode` flag (FR-017): it falls through to UnknownFlag, like any other unknown flag.
            | other :: _ -> Error(UnknownFlag other)

        match go emptyAcc tokens with
        | Error e -> Error e
        | Ok acc ->
            match acc.Paths, acc.Since with
            | Some _, Some _ -> Error PathsAndSinceTogether
            | Some [], None -> Error EmptyPaths
            | scopeChoice ->
                // Recognize the profile IN parse: an unrecognized value is a UsageError decided BEFORE any
                // port is built, so a typo writes no artifact.
                let profileResult =
                    match acc.Profile with
                    | None -> Ok Standard
                    | Some raw ->
                        match recognizeProfile raw with
                        | Recognized p -> Ok p
                        | Unrecognized s -> Error(UnrecognizedProfile s)

                match profileResult with
                | Error e -> Error e
                | Ok profile ->
                    let repo = acc.Repo |> Option.defaultValue "."

                    let scope =
                        match scopeChoice with
                        | Some paths, None -> ExplicitPaths(paths |> List.map normalizePath)
                        | None, Some rev -> Since rev
                        | _ -> DefaultRange

                    Ok
                        { Repo = repo
                          Scope = scope
                          Profile = profile
                          Format = (if acc.Json then Json else Text)
                          VerifyOut = acc.VerifyOut |> Option.defaultValue (CommandHost.under repo "readiness/verify.json")
                          StorePath = acc.Store |> Option.defaultValue (CommandHost.under repo "readiness/evidence-reuse.json")
                          PersistStore = acc.Persist
                          ExplicitPlain = acc.Plain
                          CostBudgetOut = acc.CostBudgetOut |> Option.defaultValue (CommandHost.under repo "readiness/cost-budget.json")
                          ProvenanceOut = acc.ProvenanceOut |> Option.defaultValue (CommandHost.under repo "readiness/provenance.json") }

    // ── init (Principle IV) — initial Model + first effect ──

    let init (request: RunRequest) : Model * Effect list =
        let model =
            { Request = request
              Phase = Parsed
              Candidates = None
              Decision = None
              VerifyDoc = None
              Snapshot = None
              SelectedGates = []
              Sensed = None
              Store = None
              Tooling = None
              Outcomes = []
              CurrencyNotes = []
              StoreDegraded = false
              PersistAcked = false
              Environment = None
              Builder = None
              CacheDecision = None
              Audit = None
              ReleaseDecl = None
              ReleaseSensed = None
              ReleasePreview = None
              ReleaseMatrix = None
              SurfaceFindings = []
              SurfacesPending = false
              ViewCurrencyFindings = []
              Diagnostics = []
              Exit = Success }

        // F25 wiring (064): sense the two normalized provenance facts FIRST, so `Environment`/`Builder` are
        // populated before either the empty-selection short-circuit or the executed-gate persist projects the
        // `provenance.json` sidecar. F070: sense generated-view currency in this first batch too, so
        // `ViewCurrencyFindings` is populated before either projection path runs (breadth-first driver).
        match request.Scope with
        // ExplicitPaths bypasses git diff entirely: set candidates here and go straight to the catalog load.
        // 065 (US3): the declaration load + release-fact sense is serialized BEFORE the catalog load (the
        // preview must be ready when verify.json is projected). `SenseReleasePreview` ⇒ `ReleasePreviewSensed`
        // ⇒ `LoadCatalog`.
        | ExplicitPaths paths ->
            { model with Candidates = Some paths },
            [ SenseViewCurrency request.Repo; SenseProvenance; SenseReleasePreview request.Repo ]
        | Since _
        | DefaultRange -> model, [ SenseViewCurrency request.Repo; SenseProvenance; SenseScope request.Scope ]

    // ── update — the whole composition; TOTAL, never throws ──

    // Short-circuit to Done with a mapped ExitDecision + an actionable diagnostic (no clock/abs-path/env).
    let fail (category: ExitDecision) (message: string) (model: Model) : Model * Effect list =
        { model with
            Phase = Done
            Exit = category
            Diagnostics = model.Diagnostics @ [ { Category = category; Message = message } ] },
        []

    // Map the decision's typed ExitCodeBasis to the process-level ExitDecision.
    // Host-specific policy mapper — STAYS LOCAL (FR-008): it constructs this host's `ExitDecision`.
    let exitFromBasis (basis: ExitCodeBasis) : ExitDecision =
        match basis with
        | Clean -> Success
        | ExitCodeBasis.Blocked -> Blocked

    // ── F048 persistence (pure) — `emptySensedFacts`/`revOfCommit`/`baseHeadOf`/`persistedContent` moved to
    //    the shared CommandHost leaf (075); `baseHeadOf` is decomposed there to take the snapshot diff-range. ──

    // Whether the summary must wait for a store-write ack: persistence is enabled, the load did NOT degrade,
    // and no ack has arrived yet.
    let awaitingPersist (model: Model) : bool =
        model.Request.PersistStore && not model.StoreDegraded && not model.PersistAcked

    // ── F052 verdict relocation — the ONE verdict change; Ship.rollup is used VERBATIM, never edited ──

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

    // ── 067: surface-findings verdict fold (the contract change; the truth table is NOT re-opened) ──

    // True iff any surface finding is effective-Blocking at `RunMode.Verify` under the active profile. The
    // effective severity is derived by the EXISTING `deriveEffectiveSeverity` over the input `enforcementInputOf`
    // builds from the finding — reuse only, no new rule, no new severity (FR-007, FR-008). A base-Advisory
    // finding never escalates; a base-Blocking finding blocks once the verify floor is reached.
    let surfaceBlocks (profile: Profile) (findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list) : bool =
        findings
        |> List.exists (fun f ->
            (deriveEffectiveSeverity (FS.GG.Governance.SurfaceChecks.Model.enforcementInputOf f Verify profile))
                .EffectiveSeverity = Blocking)

    // Fold the surface findings into an ALREADY-rolled (and, on the executed path, ALREADY-relocated) decision:
    // a blocking surface finding fails the run; an advisory one leaves the verdict/exit untouched. Surface
    // findings stay DISTINCT from gate/finding items in the projection (`surfaceChecks` vs `execution`) — this
    // only flips the verdict/exit basis, it never injects a surface item into Blockers/Warnings/Passing. MUST
    // run AFTER `applyExecution` (which recomputes from gate blockers only and would otherwise erase the
    // surface-driven block). With `findings = []` it is the identity ⇒ byte-identical verify.json (FR-004).
    let foldSurfaceVerdict
        (profile: Profile)
        (findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list)
        (decision: ShipDecision)
        : ShipDecision =
        if surfaceBlocks profile findings then
            { decision with
                Verdict = Fail
                ExitCodeBasis = ExitCodeBasis.Blocked }
        else
            decision

    // ── F070: stale-generated-view verdict fold (mirrors foldSurfaceVerdict; truth table NOT re-opened) ──
    // True iff any currency finding is effective-Blocking at `RunMode.Verify` under the active profile, via the
    // EXISTING `deriveEffectiveSeverity` (through the leaf's `decisionOf`). Reuse only — no new rule/severity.
    // A finding configured `block-on-ship`/`block-on-release` is effective-Advisory under verify (a warning,
    // FR-009); a `block-on-pr` finding blocks under verify only under a `strict`/`release` profile (C1).
    let viewCurrencyBlocks (profile: Profile) (findings: CE.CurrencyFinding list) : bool =
        findings
        |> List.exists (fun f -> (CE.decisionOf f Verify profile).EffectiveSeverity = Blocking)

    let foldViewCurrencyVerdict
        (profile: Profile)
        (findings: CE.CurrencyFinding list)
        (decision: ShipDecision)
        : ShipDecision =
        if viewCurrencyBlocks profile findings then
            { decision with
                Verdict = Fail
                ExitCodeBasis = ExitCodeBasis.Blocked }
        else
            decision

    // F070: pair each finding with its EnforcementDecision (Verify run mode + active profile) for the
    // additive `generatedViews` projection — carries both base + effective severity + the lever reason.
    let viewCurrencyDetail (profile: Profile) (findings: CE.CurrencyFinding list) =
        findings |> List.map (fun f -> f, CE.decisionOf f Verify profile)

    // ── F25 wiring (064): pure host-edge helpers (kinded-run label, provenance snapshot build) ──

    // The total kinded-run label (FR-004, FR-008). Descriptive metadata derived from the gate's declared
    // command token (or its id when no command is declared); it never participates in the F032 run identity.
    // Total over the closed taxonomy: a recognized token maps to its kind; an unrecognized token maps to the
    // documented `Build` default below — an explicit catch-all at the use site, never a silent mislabel of a
    // recognized kind. The richer category source (a declared kind on the gate) is a documented later extension.
    // Re-export of the shared CommandHost.kindOf (075) — kept as a public member so this host's Loop.fsi
    // surface is preserved; the body now lives once in the leaf.
    let kindOf (gate: Gate) : CommandKind = CommandHost.kindOf gate

    // The agent-review mark per gate (FR-009/D7). The MVP `.fsgg` schema declares no agent-reviewed checks, so
    // every gate is `Deterministic`; carrying the mark through `decide` keeps the reuse identity correct for the
    // later agent-review extension. Agent-reviewed checks stay advisory regardless of the decision (D6/D7).
    let reviewMarkOf (_gate: Gate) : AgentReviewMark = Deterministic

    // The sensed evidence taint per gate (D5). The host produces real runs and the MVP store records no
    // synthetic-evidence provenance, so taint is `Real` for every gate; a `Stale`/`NoEvidence` finding still
    // arises purely from the budgeted report's recompute causes. Synthetic-evidence taint is a later extension.
    let taintOf (_gid: GateId) : EvidenceTaint = Real

    // `buildSnapshot` (decomposed model-view inputs) moved to the shared CommandHost leaf (075). Verify's
    // `buildSnapshot` was byte-identical to Ship's, so they share the one leaf form.

    // 065 (US3): assemble the advisory release-readiness preview from the loaded declaration + sensed F54
    // facts + the run's audit snapshot, with an EMPTY PackEvidenceSet — verify does NOT pack, so there is no
    // attested subject (FR-007). PURE; never participates in the verify verdict or exit code.
    let previewFrom (decl: Declaration.ReleaseDeclaration) (sensed: SensedRelease) (snapshot: AuditSnapshot) : VerifyReleasePreview =
        let decision = FS.GG.Governance.ReleaseRules.Release.evaluateRelease decl.Rules sensed.Facts

        let emptyPack: FS.GG.Governance.PackEvidence.Model.PackEvidenceSet =
            { Verdicts = []
              Runs = []
              NoPackableProjects = true }

        let attestation = FS.GG.Governance.Attestation.Attestation.summarize snapshot emptyPack
        let report = FS.GG.Governance.ReleaseReport.Report.assemble decision sensed emptyPack attestation
        FS.GG.Governance.ReleaseReport.Report.preview report

    // The advisory preview for the current model (None unless a parseable `.fsgg/release.yml` was sensed ⇒
    // byte-identical verify.json, no `releaseReadiness` block).
    let previewOf (model: Model) (snapshot: AuditSnapshot) : VerifyReleasePreview option =
        match model.ReleaseDecl, model.ReleaseSensed with
        | Some decl, Some sensed -> Some(previewFrom decl sensed snapshot)
        | _ -> None

    // `kindedRunsOf`, `GateClassification`, and the parameterized `executionPlan` moved to the shared
    // CommandHost leaf (075, FR-006). The F25 budget fold below (verify uses the literal `Verify` run-mode,
    // not the request's mode) is supplied to the shared `executionPlan`; it captures this host's request
    // profile + `reviewMarkOf` (host cost policy) so the leaf stays command-agnostic.
    let verifyPlan (model: Model) =
        let budgetFold (verdictMap: Map<string, CacheEligibilityVerdict>) =
            let budget = FS.GG.Governance.CostBudget.Budget.budgetFor model.Request.Profile Verify

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

            let budgetReport = FS.GG.Governance.CostBudget.Budget.decide budget Verify candidateCosts

            let overReasons =
                FS.GG.Governance.CostBudget.Budget.overBudget budgetReport
                |> List.map (fun (gid, reason) -> gateIdValue gid, reason)
                |> Map.ofList

            overReasons, budgetReport

        CommandHost.executionPlan
            { BudgetFold = Some budgetFold }
            model.Sensed
            model.Store
            model.SelectedGates
            model.Tooling
            model.Request.Repo

    // Fires once BOTH the sensed facts and the store have arrived: classify the selected gates and request
    // the run of the must-recompute command-gates through the injected F051 port. Reused/no-command gates
    // spawn nothing. Capture, projection, the verdict relocation, and the persist-grown-store effect all wait
    // for `GatesExecuted`.
    let tryExecute (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Decision with
        | Some _, Some _, Some _ ->
            let plan, _, budgetReport = verifyPlan model

            let toExecute =
                plan
                |> List.choose (fun (g, c) ->
                    match c with
                    | CommandHost.ToExecute cmd -> Some(g.Id, cmd)
                    | CommandHost.ToReuse _
                    | CommandHost.Deferred _
                    | CommandHost.NoCommand -> None)

            { model with CacheDecision = Some budgetReport }, [ ExecuteGates toExecute ]
        | _ -> model, []

    // 067: the empty-selection ("nothing to verify") projection, now deferred until `SurfacesSensed` arrives so
    // a surface finding (e.g. a drifted package baseline on a repo whose changed paths select no gates) is still
    // sensed, folded, and reported. Mirrors the pre-067 inline empty-path body verbatim EXCEPT: (a) the verdict
    // is folded through `foldSurfaceVerdict` (a blocking surface finding fails the run — FR-007), and (b) the
    // real `model.SurfaceFindings` replace the `[]` projection placeholder. With no findings it is byte-identical
    // to the pre-067 output (FR-004). Emits the same three artifacts (verify.json + the two sidecars).
    let projectEmpty (model: Model) : Model * Effect list =
        match model.Decision with
        | Some decision ->
            let emptyReport = CacheDecisionReport []
            let costBudgetDoc = FS.GG.Governance.CostBudgetJson.CostBudgetJson.ofReport emptyReport []
            let snapshot =
                CommandHost.buildSnapshot
                    model.Sensed
                    (model.Snapshot |> Option.bind (fun s -> s.Range))
                    model.Environment
                    model.Builder
                    []
            let provenanceDoc = FS.GG.Governance.ProvenanceJson.ProvenanceJson.ofSnapshot snapshot
            let preview = previewOf model snapshot
            let folded = foldSurfaceVerdict model.Request.Profile model.SurfaceFindings decision
            // F070: also fold the stale-generated-view findings (empty ⇒ identity ⇒ byte-identical, FR-004).
            let folded = foldViewCurrencyVerdict model.Request.Profile model.ViewCurrencyFindings folded

            let verifyDoc =
                VerifyJson.ofVerifyDecisionWithGeneratedViews
                    folded
                    None
                    []
                    model.SurfaceFindings
                    preview
                    (viewCurrencyDetail model.Request.Profile model.ViewCurrencyFindings)

            { model with
                Phase = Rolled
                Decision = Some folded
                VerifyDoc = Some verifyDoc
                SelectedGates = []
                CacheDecision = Some emptyReport
                Audit = Some snapshot
                ReleasePreview = preview
                PersistAcked = true },
            [ WriteArtifact(VerifyArtifact, model.Request.VerifyOut, verifyDoc)
              WriteArtifact(CostBudgetArtifact, model.Request.CostBudgetOut, costBudgetDoc)
              WriteArtifact(ProvenanceArtifact, model.Request.ProvenanceOut, provenanceDoc) ]
        | None -> model, []

    // On `GatesExecuted`: fold F049 `capture` per executed gate (grows the store), build the per-gate
    // `GateOutcome`s, RELOCATE passing command-gates in the verdict (`applyExecution`), project verify.json
    // WITH the execution embed over the RELOCATED decision (cache report over the LOADED pre-run store), emit
    // the write, and persist the GROWN store. The relocated decision's `ExitCodeBasis` governs the exit.
    let projectExecuted (records: (GateId * CommandRecord) list) (model: Model) : Model * Effect list =
        match model.Sensed, model.Store, model.Decision with
        | Some sensed, Some store, Some decision ->
            let plan, inputsMap, budgetReport = verifyPlan model

            let recordMap =
                records |> List.fold (fun m (gid, r) -> Map.add (gateIdValue gid) r m) Map.empty

            let grownStore =
                plan
                |> List.fold
                    (fun s (g, c) ->
                        match c with
                        | CommandHost.ToExecute _ ->
                            match Map.tryFind (gateIdValue g.Id) recordMap, Map.tryFind (gateIdValue g.Id) inputsMap with
                            | Some record, Some inputs -> EvidenceCapture.capture inputs record s
                            | _ -> s
                        | CommandHost.ToReuse _
                        | CommandHost.Deferred _
                        | CommandHost.NoCommand -> s)
                    store

            let outcomes =
                plan
                |> List.map (fun (g, c) ->
                    let outcome =
                        match c with
                        | CommandHost.ToExecute _ ->
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
                        | CommandHost.ToReuse code ->
                            { GateId = g.Id
                              Disposition = Reused
                              ExitCode = Some code
                              Passed = Some(Plan.passed code) }
                        // A deferred (over-budget) gate is NOT executed and NOT reused — recorded NotExecuted so
                        // it is structurally excluded from the passed set (never coerced to pass — SC-002).
                        | CommandHost.Deferred _ ->
                            { GateId = g.Id
                              Disposition = NotExecuted
                              ExitCode = None
                              Passed = None }
                        | CommandHost.NoCommand ->
                            { GateId = g.Id
                              Disposition = NotExecuted
                              ExitCode = None
                              Passed = None }

                    g.Id, outcome)

            // The verdict relocation: a PASSING command-gate is moved to `Passing` and the verdict/exit
            // recomputed. A failing, no-command, or uncertain gate is never in this set, so it keeps its
            // rollup treatment (FR-005: an uncertain result is never coerced to pass).
            let passedGateIds =
                outcomes
                |> List.choose (fun (gid, o) -> if o.Passed = Some true then Some gid else None)
                |> Set.ofList

            let relocated = applyExecution passedGateIds decision
            // 067: fold the surface findings into the RELOCATED verdict (a blocking finding fails the run —
            // FR-007) AFTER `applyExecution`, which recomputes from gate blockers only. With no findings this is
            // the identity, so the executed path stays byte-identical to the pre-067 golden (FR-004).
            let folded = foldSurfaceVerdict model.Request.Profile model.SurfaceFindings relocated
            // F070: also fold the stale-generated-view findings (empty ⇒ identity ⇒ byte-identical, FR-004).
            let folded = foldViewCurrencyVerdict model.Request.Profile model.ViewCurrencyFindings folded

            let resReport = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries resReport |> List.choose FreshnessResolution.candidate
            let cacheReport = CacheEligibility.evaluate candidates store
            // verify.json is projected over the surface-folded decision + cache report + outcomes; the budgeted
            // findings are NOT folded in (D6). 065 (US3): build the snapshot first so the advisory preview can
            // use it. 067: thread the real `model.SurfaceFindings` (was `[]`) so the additive `surfaceChecks`
            // section is emitted when non-empty and omitted (byte-identical) when empty.
            let snapshot =
                CommandHost.buildSnapshot
                    model.Sensed
                    (model.Snapshot |> Option.bind (fun s -> s.Range))
                    model.Environment
                    model.Builder
                    (CommandHost.kindedRunsOf model.SelectedGates records)
            let preview = previewOf model snapshot
            let verifyDoc =
                VerifyJson.ofVerifyDecisionWithGeneratedViews
                    folded
                    (Some cacheReport)
                    outcomes
                    model.SurfaceFindings
                    preview
                    (viewCurrencyDetail model.Request.Profile model.ViewCurrencyFindings)

            // F25 wiring (064): the two NEW deterministic sidecars (D5/D6). cost-budget.json = the budgeted
            // decisions + the advisory cost/cache findings; provenance.json = the kinded-run audit snapshot.
            let findings =
                FS.GG.Governance.CostBudget.Findings.cacheFindings budgetReport taintOf

            let costBudgetDoc =
                FS.GG.Governance.CostBudgetJson.CostBudgetJson.ofReport budgetReport findings

            // `snapshot` already built above (065 — used by the preview); reuse it for provenance.json.
            let provenanceDoc = FS.GG.Governance.ProvenanceJson.ProvenanceJson.ofSnapshot snapshot

            let persistEffects, persistNotes =
                match model.Request.PersistStore, model.StoreDegraded with
                | true, false -> [ PersistStore(model.Request.StorePath, CommandHost.persistedContent grownStore) ], []
                | true, true ->
                    [],
                    [ "currency note: store not persisted: on-disk store failed to parse; left untouched" ]
                | false, _ -> [], []

            { model with
                Phase = Rolled
                Decision = Some folded
                VerifyDoc = Some verifyDoc
                Outcomes = outcomes
                CacheDecision = Some budgetReport
                Audit = Some snapshot
                ReleasePreview = preview
                CurrencyNotes = model.CurrencyNotes @ persistNotes },
            WriteArtifact(VerifyArtifact, model.Request.VerifyOut, verifyDoc)
            :: WriteArtifact(CostBudgetArtifact, model.Request.CostBudgetOut, costBudgetDoc)
            :: WriteArtifact(ProvenanceArtifact, model.Request.ProvenanceOut, provenanceDoc)
            :: persistEffects
        | _ -> model, []

    let rec update (msg: Msg) (model: Model) : Model * Effect list =
        // Once the pipeline has decided (Done), every further reified Msg is inert.
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
                // 065 (US3): sense the release preview before loading the catalog (see init).
                [ SenseReleasePreview model.Request.Repo ]

            | Sensed(Error reason) -> fail InputUnavailable ("git sensing unavailable: " + reason) model

            | Loaded(Invalid diags) -> fail InputUnavailable (CommandHost.describeInvalid diags) model

            | Loaded(Valid facts) ->
                // The composition: re-derive/re-sort/re-classify/re-serialize nothing — carry the cores'
                // values verbatim. The verdict is decided here (`Ship.rollup` at `RunMode.Verify`). Select the
                // gates to sense, then request the two cache senses — UNLESS the selection is empty, in which
                // case short-circuit to a passing "nothing to verify" verdict (FR-012) with no freshness/store/
                // execute work.
                let candidates = model.Candidates |> Option.defaultValue []
                let report = Routing.route facts candidates
                let registry = Gates.buildRegistry facts
                let findings = Findings.findUnknownGovernedPaths facts report
                let result = Route.select registry report findings
                let decision = Ship.rollup result Verify model.Request.Profile
                let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)

                // 067: classify the declared product surfaces ∩ verify scope (PURE — same feed `fsgg route`
                // uses, so classification is identical — FR-001) and request the read-only sense+dispatch at
                // the interpreter edge. BOTH projection paths (empty-selection and executed) now wait for
                // `SurfacesSensed` so the findings are folded into the verdict and projected before verify.json
                // is written. An empty report ⇒ no requests ⇒ `[]` ⇒ byte-identical verify.json (FR-004).
                let profileId =
                    facts.Policy
                    |> Option.map (fun p -> p.DefaultProfile)
                    |> Option.defaultValue (ProfileId "standard")

                let productReport =
                    FS.GG.Governance.ProductSurfaces.ProductSurfaces.classify facts report profileId

                if List.isEmpty selectedGates then
                    // The "nothing to verify" path: store the passing decision and sense surfaces; `projectEmpty`
                    // fires on `SurfacesSensed`. A drifted surface on a no-gate change still blocks here.
                    { model with
                        Phase = Selected
                        Decision = Some decision
                        SelectedGates = []
                        Tooling = facts.Tooling
                        SurfacesPending = true },
                    [ SenseSurfaces productReport ]
                else
                    { model with
                        Phase = Selected
                        Decision = Some decision
                        SelectedGates = selectedGates
                        Tooling = facts.Tooling
                        SurfacesPending = true },
                    // SenseSurfaces FIRST so `SurfacesSensed` is folded before `StoreLoaded` triggers
                    // `tryExecute` ⇒ `ExecuteGates` ⇒ `GatesExecuted` ⇒ `projectExecuted` (which reads the
                    // already-populated `model.SurfaceFindings`).
                    [ SenseSurfaces productReport
                      SenseFreshness(selectedGates, CommandHost.baseHeadOf (model.Snapshot |> Option.bind (fun s -> s.Range)))
                      LoadStore model.Request.StorePath ]

            // F046: a sensed/store result feeds the pure join. An `Error` DEGRADES to a safe default + a
            // non-fatal currency note — it NEVER fails the command, never perturbs the verdict, never changes
            // the exit code.
            | FreshnessSensed(Ok facts) -> tryExecute { model with Sensed = Some facts }

            | FreshnessSensed(Error reason) ->
                tryExecute
                    { model with
                        Sensed = Some CommandHost.emptySensedFacts
                        CurrencyNotes =
                            model.CurrencyNotes
                            @ [ "currency note: freshness facts could not be sensed (" + reason + "); affected gates are recompute-by-default and reported as not-evaluated" ] }

            | StoreLoaded(Ok store) -> tryExecute { model with Store = Some store }

            | StoreLoaded(Error reason) ->
                // F048: mark the load degraded so the persist write is suppressed (don't clobber a malformed
                // file). The F046 degrade-to-empty + note is unchanged.
                tryExecute
                    { model with
                        Store = Some EvidenceReuse.empty
                        StoreDegraded = true
                        CurrencyNotes =
                            model.CurrencyNotes
                            @ [ "currency note: reuse store unreadable (" + reason + "); treated as empty — every gate is recompute-by-default" ] }

            | Wrote(_, Error reason) ->
                // A write failure is ALWAYS a ToolError, NEVER a blocked verdict.
                fail ToolError ("failed to write artifact: " + reason) model

            | Wrote(_, Ok()) ->
                // F25 wiring (064): three artifacts may be written (verify.json + the two sidecars), so multiple
                // `Wrote(Ok)` acks arrive in one batch. Only the FIRST (Phase = Rolled) schedules the summary /
                // persist wait; subsequent sidecar acks are inert (the summary is already in flight).
                match model.Phase with
                | Persisted
                | Done -> model, []
                | _ ->
                    // F048: when persistence is enabled and not degraded, the summary waits for the store-write
                    // ack (`StorePersisted`) instead of emitting on the verify write.
                    if awaitingPersist model then
                        { model with Phase = Persisted }, []
                    else
                        let model = { model with Phase = Persisted }
                        model, [ emitEffect model ]

            // F048: the NON-FATAL store-write ack. An `Error` appends a currency note; NEITHER outcome changes
            // `Exit` (it stays governed solely by `ExitCodeBasis` at `Emitted`) nor the already-emitted verify
            // doc. Once the write is done (Phase = Persisted) the summary is emitted; otherwise it waits.
            | StorePersisted result ->
                let notes =
                    match result with
                    | Ok() -> model.CurrencyNotes
                    | Error reason ->
                        model.CurrencyNotes
                        @ [ "currency note: store not persisted (" + reason + "); run unaffected" ]

                let model = { model with PersistAcked = true; CurrencyNotes = notes }

                match model.Phase with
                | Persisted -> model, [ emitEffect model ]
                | _ -> model, []

            // F052: the executed gates' records arrive — capture each, build outcomes, RELOCATE passing
            // command-gates in the verdict, project verify.json with the execution embed, and persist the
            // GROWN store. The relocated decision's `ExitCodeBasis` then governs the terminal exit.
            | GatesExecuted records -> projectExecuted records model

            // F25 wiring (064): record the two normalized provenance senses; pure state, no phase change.
            | ProvenanceSensed(environment, builder) ->
                { model with
                    Environment = Some environment
                    Builder = Some builder },
                []

            // 065 (US3): the declaration + sensed F54 facts (or None) landed — store them, record the
            // inner-loop matrix decision (a declared matrix is `Deferred`; an undeclared one `NotDeclared`),
            // then proceed to the catalog load. Never changes the verify verdict or exit code (FR-006/FR-009).
            | ReleasePreviewSensed opt ->
                let decl, sensed =
                    match opt with
                    | Some(d, s) -> Some d, Some s
                    | None -> None, None

                let matrix =
                    decl
                    |> Option.map (fun d ->
                        FS.GG.Governance.ValidationMatrix.Matrix.decideMatrix
                            (FS.GG.Governance.CostBudget.Budget.budgetFor model.Request.Profile Verify)
                            InnerLoop
                            d.Matrix)

                { model with
                    ReleaseDecl = decl
                    ReleaseSensed = sensed
                    ReleaseMatrix = matrix },
                [ LoadCatalog model.Request.Repo ]

            // 067: the deterministic surface findings landed. Store them (the verdict fold happens at
            // projection via `foldSurfaceVerdict`). The empty-selection path projects HERE (it was deferred so
            // the findings could be folded); the executed path projects later in `projectExecuted`, which reads
            // the now-populated `model.SurfaceFindings`. `findings = []` ⇒ byte-identical verify.json (FR-004).
            | SurfacesSensed surfaceFindings ->
                let model =
                    { model with
                        SurfaceFindings = surfaceFindings
                        SurfacesPending = false }

                if List.isEmpty model.SelectedGates then
                    projectEmpty model
                else
                    model, []

            // F070: the stale-generated-view currency findings landed (sensed in the first batch, before the
            // catalog/gate chain). Store them; both projection paths read `model.ViewCurrencyFindings`. Pure
            // state, no projection trigger. `[]` (unconfigured) ⇒ byte-identical verify.json (FR-004).
            | ViewCurrencySensed findings -> { model with ViewCurrencyFindings = findings }, []

            | Emitted ->
                // The verdict is information until the very end: only the terminal exit category differs
                // between a pass and a fail. Map the decision's basis here.
                let exit =
                    model.Decision
                    |> Option.map (fun d -> exitFromBasis d.ExitCodeBasis)
                    |> Option.defaultValue Success

                { model with Phase = Done; Exit = exit }, []

    // ── render — the deterministic summary ──

    // F27 wiring (063): the full CacheEligibilityReport (not just its entries) recomputed purely from the
    // model's sensed facts + loaded store — the SAME value the verify.json embed carries — for the shared
    // HumanText projection. `None` until both senses have arrived (mirrors VerifyJson's `Some cacheReport`).
    and cacheReportOf (model: Model) : CacheEligibilityReport option =
        match model.Sensed, model.Store with
        | Some sensed, Some store ->
            let report = FreshnessResolution.resolve model.SelectedGates sensed
            let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
            Some(CacheEligibility.evaluate candidates store)
        | _ -> None

    // F27 wiring (063): the host operational line (the `wrote` confirmation) — host output kept around the
    // report projection, never part of the JSON contract (FR-003). Empty when there is no decision.
    and operationalLines (model: Model) : string =
        match model.Decision with
        | Some _ -> sprintf "wrote %s    (%s)" model.Request.VerifyOut VerifyJson.schemaVersion
        | None -> ""

    and renderText (model: Model) : string =
        match model.Decision with
        | None ->
            model.Diagnostics
            |> List.map (fun d -> "error: " + d.Message)
            |> String.concat "\n"
        | Some decision ->
            // F27 wiring (063) US1: the report facts come from the shared HumanText projection over the SAME
            // ShipDecision the verify.json path serializes (FR-001); the host keeps only its operational `wrote`
            // line (never part of the JSON contract — FR-003).
            let projection = HumanText.ofVerifyDecision decision (cacheReportOf model) model.Outcomes
            [ projection; operationalLines model ] |> String.concat "\n"

    // F27 wiring (063) US2: build the emit effect. Json carries the contract string (human = None). Text carries
    // the ANSI-free plain string (used for `Plain`) PLUS the `ReportView` + operational line (used for the
    // `Rich` path the edge selects); the mode is decided at the edge via `selectMode (senseCapability …)`.
    // `--json` (Format = Json) is ALWAYS the ANSI-free contract — never the rich path.
    and emitEffect (model: Model) : Effect =
        match model.Request.Format with
        | Json -> EmitSummary(renderJson model, None, false)
        | Text ->
            match model.Decision with
            | Some decision ->
                let view = ReportView.viewOfVerifyDecision decision (cacheReportOf model) model.Outcomes
                EmitSummary(renderText model, Some(view, operationalLines model), model.Request.ExplicitPlain)
            | None -> EmitSummary(renderText model, None, model.Request.ExplicitPlain)

    // The Json form IS the F056 `verify.json` document verbatim (FR-007): `--json` stdout equals the
    // persisted file byte-for-byte. The text form is suppressed under `Json`.
    and renderJson (model: Model) : string =
        match model.VerifyDoc with
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
