// Curated public signature contract for the PURE MVU core of the `fsgg release` host command (F055,
// grown by 065 F26 host wiring). This .fsi is the SOLE declaration of the module's public surface
// (Constitution Principle II). The matching Loop.fs carries NO `private`/`internal`/`public` modifiers on
// top-level bindings — the argv accumulator, the per-section render helpers, the pack/snapshot composition
// helpers, and the exit-from-basis mapper live ONLY in the .fs.
//
// This module is the PURE side of the Constitution's MVU boundary (Principle IV): `parse`/`init`/`update`/
// `render`/`exitCode` perform NO I/O, NO git, NO clock. 065 wires the seven already-built F26 surfaces in
// ADDITIVELY: on a loaded declaration the host requests packing every declared packable project through the
// F51 execution port (`PackProjects`) alongside `SenseRelease`; when the sensed facts, the pack outcomes,
// and the normalized provenance senses have all landed (a three-way join), `update` builds the
// `PackEvidenceSet` (`Pack.evaluatePack`), overlays `Pack.factContributions` onto the F54 sensed facts
// (packed evidence wins on `VersionBump`/`PackageMetadata`/`Provenance`), calls `Release.evaluateRelease`
// **verbatim**, assembles the `AuditSnapshot`/`AttestationSummary`/`ReleaseReport`, projects `release.json`
// v2 (`ReleaseJson.ofReleaseReport`) + `attestation.json` (`AttestationJson.ofAttestation`), and emits the
// two atomic writes. The `ReleaseDecision`/`ExitCodeBasis` are carried into the report WITHOUT
// re-derivation. A failed/unbumped/downgraded pack flows through `factContributions` → `Unmet` →
// `evaluateRelease` blocks it (no host re-derivation).

namespace FS.GG.Governance.ReleaseCommand

open FS.GG.Governance.Config.Model                 // SurfaceId, EnvironmentClass
open FS.GG.Governance.FreshnessKey.Model            // Revision
open FS.GG.Governance.Provenance.Model              // BuilderIdentity
open FS.GG.Governance.GateExecution.Model           // GateCommand
open FS.GG.Governance.CommandKind.Model             // AuditSnapshot
open FS.GG.Governance.PackEvidence.Model            // PackOutcome, PackEvidenceSet
open FS.GG.Governance.Attestation.Model             // AttestationSummary
open FS.GG.Governance.ValidationMatrix.Model        // MatrixPlan
open FS.GG.Governance.ReleaseReport.Model           // ReleaseReport
open FS.GG.Governance.ReleaseRules.Model            // ReleaseDecision
open FS.GG.Governance.ReleaseFactsSensing.Model     // SourceLayout, ReleaseExpectations, SensedRelease
open FS.GG.Governance.ReleaseDeclaration            // 065: the shared Declaration leaf (was row-local)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// Summary output format (FR-007). `Text` = human summary on stdout; `Json` = the `release.json` v2
    /// bytes echoed to stdout; `TextAndJson` = the human summary. The two release artifacts
    /// (`release.json` v2 + `attestation.json`) are ALWAYS written regardless of format — the publication
    /// boundary's durable audit record (FR-004).
    type OutputFormat =
        | Text
        | Json
        | TextAndJson

    /// The normalized invocation. Defaults: `Format = Text`, `ReleaseOut = <repo>/release.json`,
    /// `AttestationOut = <repo>/readiness/attestation.json`. `--repo` is REQUIRED.
    type RunRequest =
        { Repo: string
          Format: OutputFormat
          ReleaseOut: string
          AttestationOut: string }

    /// Pure-parser rejection — a single carried actionable message. Maps to `UsageError'`/exit 2.
    type UsageError = { Message: string }

    /// The process-level outcome category (cli.md exit-code table). Five distinguishable classes (FR-005).
    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    /// Which persisted document a write effect/result refers to (the 064 verify precedent): `release.json`
    /// v2 vs the `attestation.json` sidecar.
    type ArtifactKind =
        | ReleaseArtifact
        | AttestationArtifact

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter` executes
    /// each and feeds the result back as a `Msg`.
    type Effect =
        /// Read `.fsgg/release.yml` through `Files` and parse it to a `ReleaseDeclaration`.
        | LoadDeclaration of repo: string
        /// Build `realPort repo layout`, run `senseRelease` against the declared expectations.
        | SenseRelease of layout: SourceLayout * expectations: ReleaseExpectations
        /// 065: run each declared pack `GateCommand` through the F51 execution port, reading each produced
        /// artifact and feeding back a `PackOutcome` per project (a failed pack is recorded, never dropped).
        | PackProjects of (SurfaceId * GateCommand) list
        /// 065: sense the normalized head revision + environment + builder for the release attestation
        /// (no username/host/clock leakage); result fed back as `ProvenanceSensed`.
        | SenseProvenance
        /// Atomic write of a release projection, distinguished by `ArtifactKind`.
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        /// Human / JSON summary to stdout.
        | EmitSummary of text: string

    /// External results the interpreter feeds back into `update`. `DeclarationLoaded(Error)` ⇒
    /// `InputUnavailable`; `Wrote(Error)` ⇒ `ToolError` (never a blocked verdict).
    type Msg =
        | Begin
        | DeclarationLoaded of Result<Declaration.ReleaseDeclaration, Declaration.DeclError>
        | Sensed of SensedRelease
        /// 065: the recorded pack outcomes (failed packs included), in request order.
        | PacksRun of PackOutcome list
        /// 065: the three normalized provenance senses fed back from `SenseProvenance`.
        | ProvenanceSensed of head: Revision * environment: EnvironmentClass * builder: BuilderIdentity
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        | Emitted

    /// A host-edge diagnostic tagged with the `ExitDecision` category so a missing/malformed INPUT is
    /// distinguishable from a TOOL defect on stderr (Constitution VI).
    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    /// How far the pipeline has progressed. `Sensed'` marks the fired three-way join (the composition);
    /// `Persisted` marks the first write ack (the summary is then scheduled).
    type Phase =
        | Parsed
        | Loaded'
        | Sensed'
        | Persisted
        | Done

    /// The durable state the workflow owns. The 065 additions carry the pack/provenance inputs and the
    /// assembled F26 report objects; `Decision` is the F053 `ReleaseDecision` carried verbatim into `Report`.
    type Model =
        { Request: RunRequest
          Phase: Phase
          Declaration: Declaration.ReleaseDeclaration option
          Sensed: SensedRelease option
          // 065 inputs (set by the interpreter feedback msgs):
          Packs: PackOutcome list option
          Head: Revision option
          Environment: EnvironmentClass option
          Builder: BuilderIdentity option
          // 065 assembled-in-update F26 objects:
          PackEvidence: PackEvidenceSet option
          Snapshot: AuditSnapshot option
          Attestation: AttestationSummary option
          Report: ReleaseReport option
          Matrix: MatrixPlan option
          Decision: ReleaseDecision option
          ReleaseDoc: string option
          AttestationDoc: string option
          /// Which release artifacts have been written (the two-write join → summary).
          Written: Set<ArtifactKind>
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    /// Parse argv into a normalized request. PURE and TOTAL. `--repo` is required; `--format` defaults to
    /// `text`; `--out` defaults to `<repo>/release.json`; `--attestation-out` defaults to
    /// `<repo>/readiness/attestation.json`.
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effects (Principle IV `init`): `LoadDeclaration` +
    /// `SenseProvenance`.
    val init: request: RunRequest -> Model * Effect list

    /// The pure transition that IS the whole composition (FR-001..FR-007). TOTAL — never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary. `Text`/`TextAndJson` render the human verdict; `Json` renders the
    /// `release.json` v2 document text VERBATIM. PURE: no clock/abs-path/env.
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code: `Success` 0, `Blocked` 1, `UsageError'` 2,
    /// `InputUnavailable` 3, `ToolError` 4.
    val exitCode: decision: ExitDecision -> int
