// Curated public signature contract for the PURE MVU core of the `fsgg ship` host command (F026).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Loop.fs body
// exists (Principle I). This module is the PURE side of the Constitution's MVU boundary (Principle IV):
// `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock — the whole
// scope -> load -> route -> registry -> findings -> select -> ROLLUP -> PROJECT -> persist-plan ->
// summarize -> EXIT-FROM-BASIS composition is a pure transition over `Model` + `Msg`, emitting `Effect`
// data the edge `Interpreter` executes. Unlike F022 `route` (which always exits 0), this command MAPS the
// F024 `ShipDecision`'s `ExitCodeBasis` to a process exit category, including a distinct `Blocked` code.
// It REUSES F023 `recognizeMode`/`recognizeProfile` for lever parsing, F024 `Ship.rollup` for the
// verdict, and F025 `AuditJson.ofShipDecision` for the persisted bytes — re-deriving, re-sorting,
// re-classifying, and re-serializing nothing those cores fixed.

namespace FS.GG.Governance.ShipCommand

open FS.GG.Governance.Config.Model            // GovernedPath, Validation
open FS.GG.Governance.Snapshot.Model           // RepoSnapshot
open FS.GG.Governance.Enforcement.Enforcement  // RunMode, Profile
open FS.GG.Governance.Ship.Model               // ShipDecision
open FS.GG.Governance.Gates.Model              // Gate (F046 — the selected gates to sense)
open FS.GG.Governance.Adapters.SddHandoff       // F081 — Reader.HandoffRead, Consumer.consume (handoff gates)
open FS.GG.Governance.FreshnessKey.Model        // Revision (F046 — base/head from RepoSnapshot.Range)
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts (F046 — the sensed facts join input)
open FS.GG.Governance.EvidenceReuse.Model       // ReuseStore (F046 — the read-only reuse store join input)
open FS.GG.Governance.CommandRecord.Model        // CommandRecord (F052 — the assembled run record)
open FS.GG.Governance.GateExecution.Model         // GateCommand (F052 — the command-to-run)
open FS.GG.Governance.GateRun.Model               // GateOutcome (F052 — the per-gate execution outcome)
open FS.GG.Governance.HumanText                   // F27 wiring (063): ReportView (the rich/plain view payload)
// F25 host wiring (064): the four consumed cost-cache/provenance cores + F033 Provenance.
open FS.GG.Governance.Config.Model               // Cost, EnvironmentClass (already in Config.Model)
open FS.GG.Governance.CostBudget.Model            // CacheDecisionReport (budget-filter carrier → cost-budget.json)
open FS.GG.Governance.CommandKind.Model           // CommandKind, AuditSnapshot (kinded runs → provenance.json)
open FS.GG.Governance.Provenance.Model            // BuilderIdentity (normalized edge sense)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// The changed-path scope to roll up (research D4 — mirrors F022). `ExplicitPaths` bypasses git diff
    /// entirely and uses exactly the given list (so a base-blocking change can be driven without a real
    /// diff); `Since`/`DefaultRange` resolve through `Snapshot`.
    type ScopeSelector =
        | ExplicitPaths of GovernedPath list
        | Since of rev: string
        | DefaultRange

    /// Summary output format (FR-007). `--json` selects `Json` and suppresses the text form; `Json`
    /// emits the F025 `audit.json` document verbatim (research D8).
    type OutputFormat =
        | Text
        | Json

    /// The normalized invocation (data-model §2). Defaults: `Repo = "."`, `Mode = Gate`,
    /// `Profile = Standard` (research D5), `AuditOut = <repo>/readiness/audit.json` (research D7).
    /// `Mode`/`Profile` are the F023 typed levers threaded into `Ship.rollup`.
    type RunRequest =
        { Repo: string
          Scope: ScopeSelector
          Mode: RunMode
          Profile: Profile
          Format: OutputFormat
          AuditOut: string
          StorePath: string
          /// F048 opt-in: when `true` (`--persist-store`), the loaded store is pruned/bounded and written
          /// back to `StorePath` atomically; default `false` ⇒ no store write, byte-identical artifacts and
          /// an unchanged verdict/exit (FR-004/FR-007).
          PersistStore: bool
          /// F27 wiring (063): the host-parsed `--plain` flag, carried to the capability-sensing edge so a
          /// piped/explicit-plain run renders ANSI-free even on a TTY (FR-004/FR-012). Never affects JSON.
          ExplicitPlain: bool
          /// F25 wiring (064): the deterministic cost-budget sidecar path (`--cost-budget-out`); default
          /// `<repo>/readiness/cost-budget.json`. A NEW contract (`fsgg.cost-budget/v1`) written beside the
          /// existing artifacts — never folded into `audit.json`, which stays byte-identical (FR-005/FR-007).
          CostBudgetOut: string
          /// F25 wiring (064): the deterministic provenance sidecar path (`--provenance-out`); default
          /// `<repo>/readiness/provenance.json`. A NEW contract (`fsgg.provenance/v1`).
          ProvenanceOut: string
          /// 112 (`--dry-run`): when `true`, run the SIMULATED gate — no gate command is executed (every
          /// selected gate is `NotExecuted`), NOTHING is written to `readiness/` and the store is never
          /// persisted, and the printed output is the marked `SimulateProjection` (schema `fsgg.audit.dryrun/v1`)
          /// with a handoff-sufficiency breakdown. A preview: the process exits 0 regardless of the simulated
          /// verdict. `false` ⇒ the ordinary ship path, byte-identical to before (spec FR-005/FR-006).
          DryRun: bool }

    /// Pure-parser rejections — each maps to `UsageError'`/exit 2 (research D9). `UnrecognizedMode`/
    /// `UnrecognizedProfile` carry the offending string from F023 `recognizeMode`/`recognizeProfile`
    /// `Unrecognized` (research D5); recognition happens IN `parse` so a typo writes no artifact.
    type UsageError =
        | UnknownFlag of string
        | UnexpectedArgument of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths
        | UnrecognizedMode of string
        | UnrecognizedProfile of string

    /// The process-level outcome category (research D6). Unlike F022, this DOES carry a `Blocked` case:
    /// a blocked merge verdict is a distinct non-zero exit, kept apart from the tool-failure categories.
    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    /// Which persisted document an effect/result refers to. `AuditArtifact` is the existing (byte-identical)
    /// write; F25 wiring (064) adds the two self-describing sidecar kinds, written through the SAME atomic
    /// `WriteArtifact` port — `cost-budget.json` (`fsgg.cost-budget/v1`) and `provenance.json`
    /// (`fsgg.provenance/v1`).
    type ArtifactKind =
        | AuditArtifact
        | CostBudgetArtifact
        | ProvenanceArtifact

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter`
    /// executes each and feeds the result back as a `Msg`.
    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        /// F048: atomically write the pruned/bounded/serialised store back to `path`. `content` is the
        /// precomputed `EvidenceReuseStore.serialise (retain defaultRetentionBound (prune loaded))` string —
        /// the decision (whether/what to write) lives in `update` (FR-001/FR-010).
        | PersistStore of path: string * content: string
        /// F052: run the selected must-recompute command-gates ONCE each through the injected F051 port (D4).
        | ExecuteGates of (GateId * GateCommand) list
        /// F25 wiring (064): sense the two NEW provenance edge facts — a NORMALIZED `EnvironmentClass` and a
        /// username/host/clock-free `BuilderIdentity` — so `provenance.json` stays byte-deterministic across
        /// machines/re-runs (FR-006, SC-003). Result fed back as `ProvenanceSensed`.
        | SenseProvenance
        /// F27 wiring (063): emit the rendered output. `text` is the Json contract string (human = None)
        /// OR the ANSI-free plain projection used when the sensed mode is `Plain`; `human` carries the
        /// `ReportView` + operational lines for the `Rich` path the edge selects via `selectMode`
        /// (`senseCapability explicitPlain`). The mode decision is at the edge, never here (FR-004).
        | EmitSummary of text: string * human: (ReportView.ReportView * string) option * explicitPlain: bool
        /// F070 (stale-view blocking): sense generated-view currency at the edge for `repo` — parse
        /// `.fsgg/refresh.yml`, read each view's recorded provenance lock, sense source digests + generator
        /// version, decide currency, and apply the manifest's `currency-enforcement` gate. Result fed back as
        /// `ViewCurrencySensed`. `[]` (unconfigured / absent manifest) ⇒ byte-identical ship.json (FR-004).
        | SenseViewCurrency of repo: string
        /// F081: locate + read every `readiness/<id>/governance-handoff.json` under `repo` (the impure
        /// edge). Result fed back as `HandoffsLoaded`. `[]` (no handoff) ⇒ byte-identical ship (FR-001).
        | LoadHandoffs of repo: string

    /// External results the interpreter feeds back into `update`. `FreshnessSensed`/`StoreLoaded` carry the
    /// F046 sense results; an `Error` on either DEGRADES (substitutes a safe default + a non-fatal cache
    /// note) and NEVER perturbs the ship verdict or the exit code (research D2, FR-009/FR-011).
    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        /// F048: the NON-FATAL store-write ack — distinct from `Wrote`. An `Error` appends a cache note and
        /// NEVER changes `Exit` (never becomes `ToolError`/`Blocked`) or the emitted audit doc (FR-006).
        | StorePersisted of Result<unit, string>
        /// F052: the assembled records of the executed gates, in request order, each tagged by GateId (D4).
        /// `update` folds F049 `capture`, builds the per-gate `GateOutcome`s, projects audit.json with the
        /// execution embed, relocates PASSING command-gates (the verdict change), and persists the grown store.
        | GatesExecuted of (GateId * CommandRecord) list
        /// F25 wiring (064): the two normalized provenance senses fed back from `SenseProvenance`.
        | ProvenanceSensed of environment: EnvironmentClass * builder: BuilderIdentity
        /// F070: the deterministic stale-generated-view currency findings sensed at the edge. `update` folds
        /// them into the verdict (via the existing `deriveEffectiveSeverity` — no truth-table change) and
        /// projects them additively into `ship.json`/`audit.json`'s `generatedViews` array. `[]` ⇒ byte-identical.
        | ViewCurrencySensed of findings: FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list
        /// F081: the raw located handoff reads (path + JSON), in stable `<id>` order. `update` parses +
        /// maps them through `Consumer.consume` (PURE) and folds the derived gates into the verdict.
        | HandoffsLoaded of FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list
        | Emitted

    /// A host-edge diagnostic — distinct from the F014 catalog `Diagnostic`. Actionable text carrying
    /// NO clock, machine-absolute path, or environment value (FR-006, SC-005).
    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    /// How far the pipeline has progressed (data-model §3). `Rolled` collapses F022's `Selected`/
    /// `Projected`: select -> rollup -> project are all pure and happen in one `Loaded(Valid)` step.
    type Phase =
        | Parsed
        | Sensed'
        | Loaded'
        | Selected
        | Rolled
        | Persisted
        | Done

    /// The durable state the workflow owns. `Decision` is the F024 `ShipDecision` (carried so `render`
    /// and the terminal exit mapping read it); `AuditDoc` is the F025 projection string, computed BEFORE
    /// the write effect is emitted (research D10). The F046 fields carry the cache-eligibility pipeline
    /// state: `Snapshot` (base/head — D5), `SelectedGates`, `Sensed`/`Store` (join inputs), `CacheNotes`
    /// (non-fatal degrade notes — D7). None of these participate in the verdict or the exit decision.
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
          /// F052: the declared tooling (command specs) carried from the loaded catalog, so the
          /// classify/execute step can derive each gate's command-to-run (`commandFor`).
          Tooling: ToolingFacts option
          /// F052: the per-gate execution outcomes built on `GatesExecuted`; embedded in `audit.json`, fed to
          /// the verdict relocation (`applyExecution`), and surfaced in the summary. Empty until execution.
          Outcomes: (GateId * GateOutcome) list
          CacheNotes: string list
          /// F048: set `true` on `StoreLoaded(Error _)` (malformed on load) — suppresses the store write so a
          /// malformed file is never clobbered (D6).
          StoreDegraded: bool
          /// F048: set `true` once the store-write ack (`StorePersisted`) has arrived (or the write was
          /// suppressed) — gates `EmitSummary` when persistence is enabled (D10).
          PersistAcked: bool
          /// F25 wiring (064): the normalized provenance edge senses, set by `ProvenanceSensed`. `None` until
          /// sensed (then a deterministic default is used when projecting the sidecar).
          Environment: EnvironmentClass option
          Builder: BuilderIdentity option
          /// F25 wiring (064): the budgeted cache-decision report built in `executionPlan` (the budget filter),
          /// carried to the persist phase for the `cost-budget.json` projection.
          CacheDecision: CacheDecisionReport option
          /// F25 wiring (064): the provenance audit snapshot built on `GatesExecuted`, carried to the persist
          /// phase for the `provenance.json` projection.
          Audit: AuditSnapshot option
          /// F070: the stale-generated-view currency findings sensed at the edge (`[]` until `ViewCurrencySensed`;
          /// default `[]`). Folded into the verdict via the existing `deriveEffectiveSeverity` (no truth-table
          /// change — FR-003) and projected additively into `generatedViews` (omitted when `[]` — FR-004).
          ViewCurrencyFindings: FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list
          /// F081: the located handoff reads, set by `HandoffsLoaded` (default `[]`). Consumed at the
          /// `Loaded(Valid)` fold: `Consumer.consume` derives the handoff gates, which are unioned into the
          /// routed selection BEFORE `Ship.rollup`. `[]` ⇒ identity fold ⇒ byte-identical ship (FR-001).
          Handoffs: FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    /// Parse argv into a normalized request. PURE and TOTAL — usage problems are `UsageError` values,
    /// never exceptions (research D9). Tolerates a leading `ship` verb. `--paths` and `--since` together
    /// ⇒ `PathsAndSinceTogether`; an unrecognized `--mode`/`--profile` (via the F023 recognizers) ⇒
    /// `UnrecognizedMode`/`UnrecognizedProfile`. Omitted levers default to `Gate`/`Standard`.
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effect(s) for a valid request (Principle IV `init`).
    /// `ExplicitPaths` emits `LoadCatalog` directly; `Since`/`DefaultRange` emit `SenseScope` first.
    val init: request: RunRequest -> Model * Effect list

    /// The pure transition that IS the whole composition (FR-004): on sensed scope it loads the catalog;
    /// on a valid catalog it runs `Routing.route` -> `Gates.buildRegistry` ->
    /// `Findings.findUnknownGovernedPaths` -> `Route.select` -> `Ship.rollup` (over the request's
    /// `Mode`/`Profile`) -> `AuditJson.ofShipDecision`, then emits the single `WriteArtifact` effect; on
    /// the write ack it emits the summary; then `Done` with `Exit` mapped from the decision's
    /// `ExitCodeBasis` (`Clean -> Success`, `Blocked -> Blocked`). Any sensing/catalog/write failure
    /// short-circuits to `Done` with the mapped tool-failure `ExitDecision` and no further effects, and
    /// never as `Blocked` (FR-009/FR-010/FR-013). TOTAL — never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary (research D8). `Text` states the verdict and exit-code basis, lists the
    /// blockers/warnings/passing items each with identity and base/effective severity, and the
    /// unknown-governed-path findings, plus the written path; `Json` is the F025 `audit.json` document
    /// text VERBATIM (so `--json` stdout equals the persisted file — SC-002). PURE: no clock/abs-path/env,
    /// byte-stable for a fixed `Model`.
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code: `Success` 0, `Blocked` 1, `UsageError'` 2,
    /// `InputUnavailable` 3, `ToolError` 4 (research D6). `Blocked` (1) is reserved for a blocked merge
    /// verdict and is distinct from every tool-failure code (FR-008, FR-009, SC-004).
    val exitCode: decision: ExitDecision -> int

    /// F052 (D3, D7): the ONE verdict change this row introduces. After a VERBATIM `Ship.rollup`, relocate
    /// every PASSING command-gate (its `GateId` in `passedGateIds`) out of `Blockers`/`Warnings` into
    /// `Passing`, then recompute `Verdict` / `ExitCodeBasis` from the remaining blockers — Ship's OWN
    /// one-line rule re-applied (`Fail` iff a blocker remains). A failing or no-command gate is left exactly
    /// where `Ship.rollup` placed it; findings are never moved. It can only CLEAR blockers a passing gate
    /// would otherwise raise — never create one (FR-006, FR-009). It lives here, NOT in `GateRun`, because it
    /// depends on `Ship.Model`/`Enforcement`; it constructs only the already-public `ShipDecision` values and
    /// edits no frozen core (FR-017).
    val applyExecution: passedGateIds: Set<GateId> -> decision: ShipDecision -> ShipDecision

    /// F25 wiring (064): the total, pure kinded-run label for an executed gate (FR-004, FR-008). Maps the
    /// gate's declared command category to exactly one of the seven `CommandKind`s; the kind is DESCRIPTIVE
    /// metadata only — it never participates in the F032 run identity, so two runs differing only in sensed
    /// duration share an identity. Total over the closed taxonomy (an unrecognized token maps to the
    /// documented `Build` default at the use site; never a silent mislabel of a recognized kind).
    val kindOf: gate: Gate -> CommandKind
