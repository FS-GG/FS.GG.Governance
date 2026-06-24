// Curated public signature contract for the PURE MVU core of the `fsgg release` host command (F055).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here. The argv accumulator, the per-section render helpers, and the exit-from-basis
// mapper live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Loop.fs body
// exists (Principle I). This module is the PURE side of the Constitution's MVU boundary (Principle IV):
// `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock — the whole
// parse -> load-declaration -> sense -> EVALUATE -> PROJECT -> summarize -> EXIT-FROM-BASIS composition is
// a pure transition over `Model` + `Msg`, emitting `Effect` data the edge `Interpreter` executes. It
// REUSES F053 `Release.evaluateRelease` for the verdict and F055 `ReleaseJson.ofRelease` for the
// persisted bytes — re-deriving, re-classifying, and re-serializing nothing those cores fixed. Like
// `ship` (and unlike `route`), it MAPS the `ReleaseDecision.ExitCodeBasis` to a process exit category,
// including a distinct `Blocked` code, among five distinguishable classes.

namespace FS.GG.Governance.ReleaseCommand

open FS.GG.Governance.ReleaseRules.Model            // ReleaseDecision
open FS.GG.Governance.ReleaseFactsSensing.Model     // SourceLayout, ReleaseExpectations, SensedRelease

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// Summary output format (FR-007). `text` = human summary on stdout; `json` = write `release.json`
    /// (and echo its bytes to stdout); `both` = the human summary on stdout AND the `release.json` file.
    type OutputFormat =
        | Text
        | Json
        | TextAndJson

    /// The normalized invocation (data-model §`RunRequest`). Defaults: `Format = Text`, `ReleaseOut =
    /// <repo>/release.json`. `--repo` is REQUIRED (no default working directory).
    type RunRequest =
        { Repo: string
          Format: OutputFormat
          ReleaseOut: string }

    /// Pure-parser rejection — a single carried actionable message. Maps to `UsageError'`/exit 2: a usage
    /// problem is a value decided BEFORE any port is built, so a typo writes no artifact (data-model).
    type UsageError = { Message: string }

    /// The process-level outcome category (cli.md exit-code table). Five distinguishable classes (FR-005).
    /// `Blocked` (1) is a blocked release verdict — distinct from every failure-to-run code.
    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter`
    /// executes each and feeds the result back as a `Msg`.
    type Effect =
        /// Read `.fsgg/release.yml` through `Files` and parse it to a `ReleaseDeclaration`.
        | LoadDeclaration of repo: string
        /// Build `realPort repo layout`, run `senseRelease` against the declared expectations.
        | SenseRelease of layout: SourceLayout * expectations: ReleaseExpectations
        /// Atomic write of the `release.json` projection.
        | WriteArtifact of path: string * content: string
        /// Human / JSON summary to stdout.
        | EmitSummary of text: string

    /// External results the interpreter feeds back into `update`. `DeclarationLoaded(Error)` ⇒
    /// `InputUnavailable`; `Wrote(Error)` ⇒ `ToolError` (never a blocked verdict).
    type Msg =
        | Begin
        | DeclarationLoaded of Result<Declaration.ReleaseDeclaration, Declaration.DeclError>
        | Sensed of SensedRelease
        | Wrote of Result<unit, string>
        | Emitted

    /// A host-edge diagnostic — actionable text carrying NO clock, machine-absolute path, or environment
    /// value, tagged with the `ExitDecision` category so a missing/malformed INPUT is distinguishable from
    /// a TOOL defect on stderr (Constitution VI).
    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    /// How far the pipeline has progressed (data-model §state transitions).
    type Phase =
        | Parsed
        | Loaded'
        | Sensed'
        | Persisted
        | Done

    /// The durable state the workflow owns. `Decision` is the F053 `ReleaseDecision` (carried so `render`
    /// and the terminal exit mapping read it); `ReleaseDoc` is the F055 projection string, computed BEFORE
    /// the write effect is emitted. `update` is pure; `evaluateRelease` is called purely inside `update`
    /// once sensing returns; only the interpreter performs I/O.
    type Model =
        { Request: RunRequest
          Phase: Phase
          Declaration: Declaration.ReleaseDeclaration option
          Sensed: SensedRelease option
          Decision: ReleaseDecision option
          ReleaseDoc: string option
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    /// Parse argv into a normalized request. PURE and TOTAL — usage problems are `UsageError` values,
    /// never exceptions. Consumes the FLAGS ONLY (no leading `release` subcommand token is expected or
    /// stripped — cli.md §subcommand mapping); a leading bare `release` or any unknown leading positional
    /// is an unknown argument ⇒ `Error`. `--repo` is required; `--format` defaults to `text`; `--out`
    /// defaults to `<repo>/release.json`.
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effect (Principle IV `init`): always `LoadDeclaration`.
    val init: request: RunRequest -> Model * Effect list

    /// The pure transition that IS the whole composition (FR-003/FR-004): on a loaded declaration it
    /// requests sensing; on the sensed facts it computes `Release.evaluateRelease decl.Rules sensed.Facts`
    /// purely, resolves the `ExitDecision` from the decision's `ExitCodeBasis` (`Clean -> Success`,
    /// `Blocked -> Blocked`), projects `release.json` when the format requests JSON, and emits the write /
    /// summary effects; a declaration failure short-circuits to `InputUnavailable`, a write failure to
    /// `ToolError` (never `Blocked`). TOTAL — never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary. `Text`/`TextAndJson` render the human verdict (overall verdict + basis,
    /// then blockers / warnings / passing rules each with kind, surface, base/effective severity, and
    /// reason); `Json` renders the `release.json` document text VERBATIM (so `--format json` stdout equals
    /// the persisted file). PURE: no clock/abs-path/env, byte-stable for a fixed `Model`.
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code: `Success` 0, `Blocked` 1, `UsageError'` 2,
    /// `InputUnavailable` 3, `ToolError` 4 (cli.md exit-code table, SC-005). `Blocked` (1) is reserved for
    /// a blocked release verdict and is distinct from every failure-to-run code.
    val exitCode: decision: ExitDecision -> int
