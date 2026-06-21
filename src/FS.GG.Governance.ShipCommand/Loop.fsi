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
          AuditOut: string }

    /// Pure-parser rejections — each maps to `UsageError'`/exit 2 (research D9). `UnrecognizedMode`/
    /// `UnrecognizedProfile` carry the offending string from F023 `recognizeMode`/`recognizeProfile`
    /// `Unrecognized` (research D5); recognition happens IN `parse` so a typo writes no artifact.
    type UsageError =
        | UnknownFlag of string
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

    /// Which persisted document an effect/result refers to. Only `AuditArtifact` (one write — research D3).
    type ArtifactKind =
        | AuditArtifact

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter`
    /// executes each and feeds the result back as a `Msg`.
    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | EmitSummary of text: string

    /// External results the interpreter feeds back into `update`.
    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
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
        | Rolled
        | Persisted
        | Done

    /// The durable state the workflow owns. `Decision` is the F024 `ShipDecision` (carried so `render`
    /// and the terminal exit mapping read it); `AuditDoc` is the F025 projection string, computed BEFORE
    /// the write effect is emitted (research D10).
    type Model =
        { Request: RunRequest
          Phase: Phase
          Candidates: GovernedPath list option
          Decision: ShipDecision option
          AuditDoc: string option
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
