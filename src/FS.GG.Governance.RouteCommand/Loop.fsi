// Curated public signature contract for the PURE MVU core of the `fsgg route` host command (F022).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Loop.fs body
// exists (Principle I). This module is the PURE side of the Constitution's MVU boundary (Principle IV):
// `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock — the whole
// scope→load→route→registry→findings→select→project→persist-plan→summarize→exit composition is a pure
// transition over `Model` + `Msg`, emitting `Effect` data the edge `Interpreter` executes. It computes
// NO ship verdict (FR-008): no merge decision, severity, profile, mode, enforcement, cache eligibility,
// blockers, or exit-code-from-blockers.

namespace FS.GG.Governance.RouteCommand

open FS.GG.Governance.Config.Model       // GovernedPath
open FS.GG.Governance.Snapshot.Model      // RepoSnapshot
open FS.GG.Governance.Route.Model          // RouteResult
open FS.GG.Governance.Config              // Validation (Config.Model)
open FS.GG.Governance.Gates.Model          // Gate (F046 — the selected gates to sense)
open FS.GG.Governance.FreshnessKey.Model    // Revision (F046 — base/head, passed through from RepoSnapshot.Range)
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts (F046 — the sensed facts join input)
open FS.GG.Governance.EvidenceReuse.Model   // ReuseStore (F046 — the read-only reuse store join input)
open FS.GG.Governance.Config.Model          // ToolingFacts (F052 — declared command specs)
open FS.GG.Governance.CommandRecord.Model    // CommandRecord (F052 — the assembled run record)
open FS.GG.Governance.GateExecution.Model     // GateCommand (F052 — the command-to-run)
open FS.GG.Governance.GateRun.Model           // GateOutcome (F052 — the per-gate execution outcome)
open FS.GG.Governance.ProductSurfaces.Model    // ProductSurfaceReport (F23 — the product-surface classification)
open FS.GG.Governance.HumanText               // F27 wiring (063): ReportView (the rich/plain view payload)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// The changed-path scope to route (research D4). `ExplicitPaths` bypasses git diff entirely and
    /// routes exactly the given list (US2 AS1); `Since`/`DefaultRange` resolve through `Snapshot`.
    type ScopeSelector =
        | ExplicitPaths of GovernedPath list
        | Since of rev: string
        | DefaultRange

    /// Summary output format (FR-007). `--json` selects `Json` and suppresses the text form.
    type OutputFormat =
        | Text
        | Json

    /// The normalized invocation (data-model §2). Defaults: `Repo = "."`,
    /// `GatesOut = <repo>/.fsgg/gates.json`, `RouteOut = <repo>/readiness/route.json` (research D5).
    /// `StorePath` is the read-only evidence-reuse store path (F046): `--store`, default
    /// `<repo>/readiness/evidence-reuse.json` (research D6); absent on disk ⇒ `EvidenceReuse.empty`.
    type RunRequest =
        { Repo: string
          Scope: ScopeSelector
          Format: OutputFormat
          GatesOut: string
          RouteOut: string
          StorePath: string
          /// F048 opt-in: when `true` (`--persist-store`), the loaded store is pruned/bounded and written
          /// back to `StorePath` atomically; default `false` ⇒ no store write, byte-identical artifacts
          /// (FR-004/FR-007).
          PersistStore: bool
          /// F27 wiring (063): the host-parsed `--plain` flag, carried to the capability-sensing edge so a
          /// piped/explicit-plain run renders ANSI-free even on a TTY (FR-004/FR-012). Never affects JSON.
          ExplicitPlain: bool
          /// F27 wiring (063, US3): the host-parsed `--watch` flag. A pure-Loop no-op (the read-only watch
          /// loop is an interpreter-edge concern driven by `HumanRender.Watch.run`); the one-shot evaluation
          /// and the persisted artifacts are unaffected (FR-009). Never selects `Json`.
          Watch: bool }

    /// Pure-parser rejections — each maps to `UsageError`/exit 2 (research D6/D8).
    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths

    /// The process-level outcome category (research D6). Deliberately carries NO `GovernedBlocking`
    /// (FR-008): selecting many gates or producing many findings is `Success`, never a non-zero exit.
    type ExitDecision =
        | Success
        | UsageError'
        | InputUnavailable
        | ToolError

    /// Which persisted document an effect/result refers to.
    type ArtifactKind =
        | GatesArtifact
        | RouteArtifact

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter`
    /// executes each and feeds the result back as a `Msg`. `SenseFreshness`/`LoadStore` are the F046
    /// cache-eligibility senses: the freshness facts of the selected gates (with the change's base/head)
    /// and the read-only reuse store; neither writes anything.
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
        /// `update` requests this after cache eligibility; the interpreter runs `senseExecution` per gate.
        | ExecuteGates of (GateId * GateCommand) list
        /// F27 wiring (063): emit the rendered output. `text` is the Json contract string (human = None)
        /// OR the ANSI-free plain projection used when the sensed mode is `Plain`; `human` carries the
        /// `ReportView` + operational lines for the `Rich` path the edge selects via `selectMode`
        /// (`senseCapability explicitPlain`). The mode decision is at the edge, never here (FR-004).
        | EmitSummary of text: string * human: (ReportView.ReportView * string) option * explicitPlain: bool

    /// External results the interpreter feeds back into `update`. `FreshnessSensed`/`StoreLoaded` carry the
    /// F046 sense results; an `Error` on either DEGRADES (substitutes a safe default + a non-fatal cache
    /// note), never fails the command (research D2).
    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        /// F048: the NON-FATAL store-write ack — distinct from `Wrote`. An `Error` appends a cache note and
        /// NEVER changes `Exit` or the emitted artifacts (FR-006).
        | StorePersisted of Result<unit, string>
        /// F052: the assembled records of the executed gates, in request order, each tagged by GateId (D4).
        /// `update` folds F049 `capture` per record, builds the per-gate `GateOutcome`s, projects the
        /// document with the execution embed, and emits the persist-grown-store effect.
        | GatesExecuted of (GateId * CommandRecord) list
        | Emitted

    /// A host-edge diagnostic — distinct from the F014 catalog `Diagnostic`. Actionable text carrying
    /// NO clock, machine-absolute path, or environment value (FR-006, SC-005).
    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    /// How far the pipeline has progressed (data-model §3).
    type Phase =
        | Parsed
        | Sensed'
        | Loaded'
        | Selected
        | Projected
        | Persisted
        | Done

    /// The durable state the workflow owns. `GatesDoc`/`RouteDoc` are the F021/F020 projection strings,
    /// both computed before any write effect is emitted (research D9). The F046 fields carry the
    /// cache-eligibility pipeline state: `Snapshot` (kept to derive base/head — D5), `SelectedGates` (the
    /// gates to sense, set at `Loaded(Valid)`), `Sensed`/`Store` (the join inputs), and `CacheNotes`
    /// (non-fatal degrade notes surfaced in the summary — D7).
    type Model =
        { Request: RunRequest
          Phase: Phase
          Candidates: GovernedPath list option
          Result: RouteResult option
          GatesDoc: string option
          RouteDoc: string option
          Snapshot: RepoSnapshot option
          SelectedGates: Gate list
          /// F23: the product-surface classification computed at the edge (at `Loaded(Valid)`) from the
          /// loaded facts + the route report under the catalog's default profile; threaded into the
          /// additive `productSurfaces` route.json section and the human summary. Empty until loaded.
          Classifications: ProductSurfaceReport
          Sensed: SensedFacts option
          Store: ReuseStore option
          /// F052: the declared tooling (command specs) carried from the loaded catalog, so the
          /// classify/execute step can derive each gate's command-to-run (`commandFor`).
          Tooling: ToolingFacts option
          /// F052: the per-gate execution outcomes built on `GatesExecuted` (executed/reused/not-executed),
          /// embedded in `route.json` and surfaced in the summary. Empty until execution completes.
          Outcomes: (GateId * GateOutcome) list
          CacheNotes: string list
          /// F048: set `true` on `StoreLoaded(Error _)` (malformed on load) — suppresses the store write so a
          /// malformed file is never clobbered (D6).
          StoreDegraded: bool
          /// F048: set `true` once the store-write ack (`StorePersisted`) has arrived (or the write was
          /// suppressed) — gates `EmitSummary` when persistence is enabled (D10).
          PersistAcked: bool
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    /// Parse argv into a normalized request. PURE and TOTAL — usage problems are `UsageError` values,
    /// never exceptions (research D8). `--paths` and `--since` together ⇒ `PathsAndSinceTogether`.
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effect(s) for a valid request (Principle IV `init`).
    /// `ExplicitPaths` emits `LoadCatalog` directly; `Since`/`DefaultRange` emit `SenseScope` first.
    val init: request: RunRequest -> Model * Effect list

    /// The pure transition that IS the whole composition (FR-004): on sensed scope it loads the catalog;
    /// on a valid catalog it runs `Routing.route` → `Gates.buildRegistry` →
    /// `Findings.findUnknownGovernedPaths` → `Route.select`, projects via `RouteJson`/`GatesJson`, and
    /// emits the two `WriteArtifact` effects; on both writes it emits the summary; then `Done`/`Success`.
    /// Any sensing/catalog/write failure short-circuits to `Done` with the mapped `ExitDecision` and no
    /// further effects (FR-009/FR-010/FR-011/FR-013). TOTAL — never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary (research D7) — separate from the persisted artifacts. `Text` lists each
    /// selected gate by id with its selecting path and per-tier cost, the cost rollup, the
    /// unknown-governed-path findings, and the two written paths; `Json` is the machine-readable form
    /// (FR-007). PURE: no clock/abs-path/env, byte-stable for a fixed `Model` (SC-002).
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code: `Success` 0, `UsageError'` 2,
    /// `InputUnavailable` 3, `ToolError` 4 (research D6). No `GovernedBlocking` code (FR-008).
    val exitCode: decision: ExitDecision -> int

    /// F27 wiring (063): the report view projected from a terminal model — the SAME `ReportView` the plain
    /// and rich one-shot renders use (`viewOfRouteResult` over the resolved `RouteResult` + recomputed cache
    /// + outcomes). `None` when the model carries no report. The watch/tui edges re-render through this.
    val humanView: model: Model -> ReportView.ReportView option
