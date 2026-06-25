// Curated public signature contract for the PURE MVU core of the `fsgg verify` host command (F056).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised before any Loop.fs body exists (Principle I). This module
// is the PURE side of the Constitution's MVU boundary (Principle IV): `parse`/`init`/`update`/`render`/
// `exitCode` perform NO I/O, NO git, NO clock — the whole scope -> load -> route -> registry -> findings ->
// select -> ROLLUP -> RUN/REUSE -> PROJECT -> persist-plan -> summarize -> EXIT-FROM-BASIS composition is a
// pure transition over `Model` + `Msg`, emitting `Effect` data the edge `Interpreter` executes.
//
// Verify is the CLOSEST SIBLING of `fsgg ship` (F026): the SAME pipeline reused VERBATIM, differing only in
// (a) the FIXED `RunMode.Verify` — there is NO `--mode` flag and NO `Mode` field, so a developer cannot
// escalate verify into the `Gate`-mode merge verdict (FR-017); (b) the first-class currency-findings text
// projection; (c) the `verify.json` schema id (via `VerifyJson.ofVerifyDecision`); and (d) the pre-PR framing
// + the "nothing to verify" empty-selection report. It REUSES F024 `Ship.rollup` for the verdict (threaded
// with `RunMode.Verify`) and F052 `applyExecution` for the verdict relocation — re-deriving, re-sorting,
// re-classifying, and re-serializing nothing those cores fixed.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Config.Model            // GovernedPath, Validation
open FS.GG.Governance.Snapshot.Model           // RepoSnapshot
open FS.GG.Governance.Enforcement.Enforcement  // Profile
open FS.GG.Governance.Ship.Model               // ShipDecision
open FS.GG.Governance.Gates.Model              // Gate (F046 — the selected gates to sense)
open FS.GG.Governance.FreshnessKey.Model        // Revision (F046 — base/head from RepoSnapshot.Range)
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts (F046 — the sensed facts join input)
open FS.GG.Governance.EvidenceReuse.Model       // ReuseStore (F046 — the read-only reuse store join input)
open FS.GG.Governance.CommandRecord.Model        // CommandRecord (F052 — the assembled run record)
open FS.GG.Governance.GateExecution.Model         // GateCommand (F052 — the command-to-run)
open FS.GG.Governance.GateRun.Model               // GateOutcome (F052 — the per-gate execution outcome)
open FS.GG.Governance.HumanText                   // F27 wiring (063): ReportView (the rich/plain view payload)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// The changed-path scope to verify (mirrors F026). `ExplicitPaths` bypasses git diff entirely and uses
    /// exactly the given list; `Since`/`DefaultRange` resolve through `Snapshot`.
    type ScopeSelector =
        | ExplicitPaths of GovernedPath list
        | Since of rev: string
        | DefaultRange

    /// Summary output format (FR-007). `--json` selects `Json` and suppresses the text form; `Json` emits the
    /// F056 `verify.json` document verbatim.
    type OutputFormat =
        | Text
        | Json

    /// The normalized invocation (data-model §1). Defaults: `Repo = "."`, `Profile = Standard`,
    /// `Format = Text`, `Scope = DefaultRange`, `VerifyOut = <repo>/readiness/verify.json`,
    /// `StorePath = <repo>/readiness/evidence-reuse.json`, `PersistStore = false`.
    ///
    /// There is NO `Mode` field — the enforcement mode is FIXED to `RunMode.Verify` (FR-017). The resolved
    /// `Profile` is the only enforcement lever a developer can set (overridable via `--profile`).
    type RunRequest =
        { Repo: string
          Scope: ScopeSelector
          Profile: Profile
          Format: OutputFormat
          VerifyOut: string
          StorePath: string
          /// F048 opt-in: when `true` (`--persist-store`), the loaded store is pruned/bounded and written
          /// back to `StorePath` atomically; default `false` ⇒ no store write, byte-identical artifacts and
          /// an unchanged verdict/exit.
          PersistStore: bool
          /// F27 wiring (063): the host-parsed `--plain` flag, carried to the capability-sensing edge so a
          /// piped/explicit-plain run renders ANSI-free even on a TTY. Layered ON TOP of `--format text|json`:
          /// it never changes the `--json` rejection or the text/json selection — JSON is always ANSI-free.
          ExplicitPlain: bool }

    /// Pure-parser rejections — each maps to `UsageError'`/exit 2. `UnrecognizedProfile` carries the offending
    /// string from F023 `recognizeProfile` `Unrecognized`; recognition happens IN `parse` so a typo writes no
    /// artifact. There is NO `UnrecognizedMode` (verify has no `--mode`): a `--mode` flag is an `UnknownFlag`.
    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths
        | UnrecognizedProfile of string

    /// The process-level outcome category. `Blocked` (1) is a distinct non-zero exit — an unmet
    /// effective-blocking check at `RunMode.Verify` — kept apart from the tool-failure categories.
    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    /// Which persisted document an effect/result refers to. Only `VerifyArtifact` (one write).
    type ArtifactKind =
        | VerifyArtifact

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter` executes
    /// each and feeds the result back as a `Msg`. The EXACT ShipCommand vocabulary.
    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        /// F048: atomically write the pruned/bounded/serialised store back to `path`.
        | PersistStore of path: string * content: string
        /// F052: run the selected must-recompute command-gates ONCE each through the injected F051 port.
        | ExecuteGates of (GateId * GateCommand) list
        /// F27 wiring (063): emit the rendered output. `text` is the verify.json contract string (human = None)
        /// OR the ANSI-free plain projection used when the sensed mode is `Plain`; `human` carries the
        /// `ReportView` + operational `wrote` line for the `Rich` path the edge selects via `selectMode`
        /// (`senseCapability explicitPlain`). The mode decision is at the edge, never here.
        | EmitSummary of text: string * human: (ReportView.ReportView * string) option * explicitPlain: bool

    /// External results the interpreter feeds back into `update`. `FreshnessSensed`/`StoreLoaded` carry the
    /// F046 sense results; an `Error` on either DEGRADES (substitutes a safe default + a non-fatal currency
    /// note) and NEVER perturbs the verify verdict or the exit code.
    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        /// F048: the NON-FATAL store-write ack — distinct from `Wrote`. An `Error` appends a currency note and
        /// NEVER changes `Exit` or the emitted verify doc.
        | StorePersisted of Result<unit, string>
        /// F052: the assembled records of the executed gates, in request order, each tagged by GateId.
        | GatesExecuted of (GateId * CommandRecord) list
        | Emitted

    /// A host-edge diagnostic — distinct from the F014 catalog `Diagnostic`. Actionable text carrying NO
    /// clock, machine-absolute path, or environment value.
    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    /// How far the pipeline has progressed (data-model §8). The ShipCommand `Phase` ladder.
    type Phase =
        | Parsed
        | Sensed'
        | Loaded'
        | Selected
        | Rolled
        | Persisted
        | Done

    /// The durable state the workflow owns. `Decision` is the F024 `ShipDecision` (rolled at `RunMode.Verify`);
    /// `VerifyDoc` is the F056 projection string. The F046 fields carry the cache-eligibility pipeline state.
    /// None of these participate in the verdict or the exit decision beyond the reused `Ship.rollup`/
    /// `applyExecution`.
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
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    /// Parse argv into a normalized request. PURE and TOTAL — usage problems are `UsageError` values, never
    /// exceptions. Tolerates a leading `verify` verb. `--paths` and `--since` together ⇒ `PathsAndSinceTogether`;
    /// an unrecognized `--profile` (via F023 `recognizeProfile`) ⇒ `UnrecognizedProfile`; a `--mode` flag ⇒
    /// `UnknownFlag "--mode"` (FR-017). Omitted profile defaults to `Standard`.
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effect(s) for a valid request (Principle IV `init`).
    /// `ExplicitPaths` emits `LoadCatalog` directly; `Since`/`DefaultRange` emit `SenseScope` first.
    val init: request: RunRequest -> Model * Effect list

    /// The pure transition that IS the whole composition: on sensed scope it loads the catalog; on a valid
    /// catalog it runs `Routing.route` -> `Gates.buildRegistry` -> `Findings.findUnknownGovernedPaths` ->
    /// `Route.select` -> `Ship.rollup` (at `RunMode.Verify`) and either short-circuits an EMPTY selection to a
    /// passing "nothing to verify" verdict or senses freshness/store, runs the stale command-gates, relocates
    /// the verdict (`applyExecution`), projects `verify.json`, emits the single `WriteArtifact`, then the
    /// summary; then `Done` with `Exit` mapped from the decision's `ExitCodeBasis` (`Clean -> Success`,
    /// `Blocked -> Blocked`). Any sensing/catalog/write failure short-circuits to `Done` with the mapped
    /// tool-failure `ExitDecision` and never as `Blocked`. TOTAL — never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary. `Text` states the verdict and exit-code basis, lists the blockers/warnings/
    /// passing items, the findings, the first-class currency section (fresh/reused, stale/recomputed +
    /// categories, recompute-by-default + missing tokens, degrade notes), the per-gate execution, and the
    /// written path — or "nothing to verify" for an empty selection; `Json` is the F056 `verify.json` document
    /// text VERBATIM (so `--json` stdout equals the persisted file). PURE: no clock/abs-path/env.
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code: `Success` 0, `Blocked` 1, `UsageError'` 2,
    /// `InputUnavailable` 3, `ToolError` 4. `Blocked` (1) is reserved for an unmet effective-blocking check
    /// and is distinct from every tool-failure code.
    val exitCode: decision: ExitDecision -> int

    /// F052: the ONE verdict change this row reuses. After a VERBATIM `Ship.rollup`, relocate every PASSING
    /// command-gate (its `GateId` in `passedGateIds`) out of `Blockers`/`Warnings` into `Passing`, then
    /// recompute `Verdict` / `ExitCodeBasis` from the remaining blockers. A failing, no-command, or uncertain
    /// gate is left exactly where `Ship.rollup` placed it; findings never move. It can only CLEAR blockers a
    /// passing gate would otherwise raise — never create one (FR-005: an uncertain result is never coerced to
    /// pass).
    val applyExecution: passedGateIds: Set<GateId> -> decision: ShipDecision -> ShipDecision
