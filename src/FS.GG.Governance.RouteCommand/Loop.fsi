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
          StorePath: string }

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
        | EmitSummary of text: string

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
          Sensed: SensedFacts option
          Store: ReuseStore option
          CacheNotes: string list
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
