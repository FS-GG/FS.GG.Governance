// Curated public signature contract for the PURE MVU core of the `fsgg refresh` host command (F057).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — the argv
// accumulator, the per-view currency helper, the roll-up, and the render helpers live ONLY in the .fs.
//
// This module is the PURE side of the Constitution's MVU boundary (Principle IV): `parse`/`init`/`update`/
// `render`/`exitCode` perform NO I/O, NO git, NO clock — the whole
// parse -> load-manifest -> sense+read-recorded -> DECIDE-CURRENCY -> regenerate -> record -> project ->
// summarize -> EXIT composition is a pure transition over `Model` + `Msg`, emitting `Effect` data the edge
// `Interpreter` executes. It REUSES the F029 `FreshnessKey` comparator (`matches`/`diff`) VERBATIM to decide
// per-view currency — building `recorded`/`current` `FreshnessInputs` that differ ONLY in the source-digest
// set and generator version, with the revision fields held EQUAL (research D1: currency depends on sources +
// generator, never git position) — and the F057 `RefreshJson.ofRefreshDecision` for the persisted bytes.

namespace FS.GG.Governance.RefreshCommand

open FS.GG.Governance.FreshnessKey.Model            // ArtifactHash, GeneratorVersion
open FS.GG.Governance.RefreshJson.RefreshModel       // GenerationManifest, GenerationEntry, ViewKind, ...

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// Summary output format (FR-006). `Text` = human summary on stdout; `Json` = write `refresh.json` (and
    /// echo its bytes to stdout); `TextAndJson` = the human summary on stdout AND the `refresh.json` file.
    type OutputFormat =
        | Text
        | Json
        | TextAndJson

    /// The optional view-selection scope (FR-015, research D6). `AllViews` is the documented default (no
    /// selector); `ByKind`/`ByView` narrow the evaluated set. Out-of-scope views are reported
    /// `NotEvaluated`, never assumed current. `--view-kind` and `--view` together is a `UsageError`.
    type Scope =
        | AllViews
        | ByKind of ViewKind
        | ByView of string

    /// The normalized invocation (data-model §`RunRequest`). Defaults: `Repo = "."`, `DryRun = false`,
    /// `Scope = AllViews`, `Format = Text`, `RefreshOut = None`.
    type RunRequest =
        { Repo: string
          DryRun: bool
          Scope: Scope
          Format: OutputFormat
          RefreshOut: string option }

    /// Pure-parser rejection — a single carried actionable message. Maps to `UsageError'`/exit 2: a usage
    /// problem is decided BEFORE any port is built, so a typo writes nothing.
    type UsageError = { Message: string }

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter` executes
    /// each and feeds the result back as a `Msg`. `RegenerateView`/`RecordProvenance` are NOT emitted in
    /// `--dry-run` (FR-013) — only the optional `refresh.json` `WriteArtifact` is.
    type Effect =
        /// Read `.fsgg/refresh.yml` through `Files` and parse it to a `GenerationManifest`.
        | LoadManifest of repo: string
        /// Digest the entry's declared source(s) + sense the generator version (research D2).
        | SenseSource of entry: GenerationEntry
        /// Read the view's recorded provenance from the generated lock (research D4).
        | ReadRecorded of viewId: string
        /// Run the view's declared generator (research D3) — write mode only; never in `--dry-run`.
        | RegenerateView of entry: GenerationEntry
        /// Record the refreshed provenance triple for a regenerated view — write mode only.
        | RecordProvenance of viewId: string * provenance: (ArtifactHash list * GeneratorVersion * ArtifactHash)
        /// Atomic write of the `refresh.json` projection.
        | WriteArtifact of path: string * content: string
        /// Human / JSON summary to stdout.
        | EmitSummary of text: string

    /// External results the interpreter feeds back into `update`. `ManifestLoaded(Error)` ⇒
    /// `InputUnavailable`; a sense failure for a stale view ⇒ `StaleUnresolved`; a generator/provenance/
    /// artifact write failure ⇒ `ToolError`.
    type Msg =
        | Begin
        | ManifestLoaded of Result<GenerationManifest, DeclError>
        | Sensed of viewId: string * Result<ArtifactHash list * GeneratorVersion, string>
        | RecordedRead of viewId: string * recorded: (ArtifactHash list * GeneratorVersion) option
        | Regenerated' of viewId: string * Result<ArtifactHash, string>
        | ProvenanceWritten of Result<unit, string>
        | Wrote of Result<unit, string>
        | Emitted

    /// A host-edge diagnostic — actionable text carrying NO clock, machine-absolute path, or environment
    /// value, tagged with the `RefreshOutcome` category so a missing/malformed INPUT is distinguishable from
    /// a TOOL defect on stderr (Constitution VI).
    type Diagnostic =
        { Category: RefreshOutcome
          Message: string }

    /// How far the pipeline has progressed (data-model §state transitions).
    type Phase =
        | Parsed
        | Loaded'
        | Sensed'
        | Persisted
        | Done

    /// The durable state the workflow owns. `Sensed`/`Recorded` accumulate per-view edge results until the
    /// sensing barrier; `ExpectedRegen` is the set of views dispatched for regeneration (write mode);
    /// `PendingProv` counts outstanding provenance writes; `Views` accumulates per-view decisions in declared
    /// order; `Decision`/`RefreshDoc` are set at finalize. `update` is pure; only the interpreter performs I/O.
    type Model =
        { Request: RunRequest
          Phase: Phase
          Manifest: GenerationManifest option
          InScope: GenerationEntry list
          Sensed: Map<string, Result<ArtifactHash list * GeneratorVersion, string>>
          Recorded: Map<string, (ArtifactHash list * GeneratorVersion) option>
          ExpectedRegen: Set<string>
          PendingProv: int
          Views: ViewDecision list
          Decision: RefreshDecision option
          RefreshDoc: string option
          Diagnostics: Diagnostic list
          Exit: RefreshOutcome }

    /// Parse argv into a normalized request. PURE and TOTAL — usage problems are `UsageError` values, never
    /// exceptions. A leading bare `refresh` token is TOLERATED (no central dispatcher — command precedent).
    /// `--dry-run`, `--view-kind <kind>`, `--view <id>`, `--text`/`--json`/`--text-and-json`,
    /// `--refresh-out <path>`, and an optional `--repo <dir>` (default `.`). `--view-kind` and `--view`
    /// together, an unknown flag, or a missing flag value ⇒ `Error` (exit 2).
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effect (Principle IV `init`): always `LoadManifest`.
    val init: request: RunRequest -> Model * Effect list

    /// The pure transition that IS the whole composition. On a loaded manifest it computes the in-scope set
    /// and requests per-view sensing + recorded-provenance reads; once both land for every in-scope view it
    /// decides currency by reusing `FreshnessKey.matches`/`diff` (revisions held equal — research D1),
    /// requesting `RegenerateView` for each stale view in write mode (none in `--dry-run`); on a successful
    /// regeneration it records refreshed provenance; once all writes settle it assembles the
    /// `RefreshDecision`, projects `refresh.json` when requested, and emits the write/summary effects. A
    /// manifest failure short-circuits to `InputUnavailable`; a sense failure for a stale view is
    /// `StaleUnresolved` (never `Current`); a generator/write failure to `ToolError`. TOTAL — never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary. `Text`/`TextAndJson` render the human report (outcome + counts, then one
    /// line per view with id, status, output, drifted categories / reason); `Json` renders the
    /// `refresh.json` document text VERBATIM (so `--json` stdout equals the persisted file). PURE: no
    /// clock/abs-path/env, byte-stable for a fixed `Model`.
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code (cli.md exit-code table, research D5):
    /// `NothingToRefresh` 0, `StaleUnresolved'` 1, `UsageError'` 2, `InputUnavailable` 3, `ToolError` 4,
    /// `ViewsRegenerated` 5. `0` and `5` are both success; `1` is the genuinely failing outcome.
    val exitCode: outcome: RefreshOutcome -> int
