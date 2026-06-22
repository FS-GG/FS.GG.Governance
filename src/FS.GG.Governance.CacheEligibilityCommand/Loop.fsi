// Curated public signature contract for the PURE MVU core of the `fsgg cache-eligibility` host command (F044).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is presence/
// absence here; every sensing/codec/render helper stays unexposed by absence from this signature.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx, the F044 section) before any
// Loop.fs body exists (Principle I). This module is the PURE side of the Constitution's MVU boundary
// (Principle IV): `parse`/`init`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO hashing, NO clock —
// the whole scope -> load -> select -> resolve -> evaluate -> project -> persist-plan -> summarize -> exit
// composition is a pure transition over `Model` + `Msg`, emitting `Effect` data the edge `Interpreter`
// executes. It REUSES the merged cores (F018/F019/F029/F030/F041/F042/F043) VERBATIM, computing NO freshness
// key, NO hash, and NO cache decision of its own (FR-012/FR-013). Cache eligibility is INFORMATION, not a
// verdict: it assigns no severity, profile, mode, enforcement, ship verdict, or provenance (FR-009/FR-011).

namespace FS.GG.Governance.CacheEligibilityCommand

open FS.GG.Governance.Config.Model // GovernedPath
open FS.GG.Governance.Config // Validation (Config.Model)
open FS.GG.Governance.Snapshot.Model // RepoSnapshot
open FS.GG.Governance.Gates.Model // Gate
open FS.GG.Governance.FreshnessKey.Model // Revision
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts, FreshnessResolutionReport
open FS.GG.Governance.EvidenceReuse.Model // ReuseStore

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// The changed-path scope to route (mirrors RouteCommand). `ExplicitPaths` bypasses git diff entirely
    /// (and so resolves base/head to `None`, L2); `Since`/`DefaultRange` resolve through `Snapshot`.
    type ScopeSelector =
        | ExplicitPaths of GovernedPath list
        | Since of rev: string
        | DefaultRange

    /// Summary output format (FR-007). `--format json` selects `Json`; default `Human`.
    type OutputFormat =
        | Human
        | Json

    /// The normalized invocation (data-model §RunRequest). Defaults applied in `parse`: `Repo = "."`,
    /// `CacheOut = <repo>/readiness/cache-eligibility.json`, `UnresolvedOut` DERIVED from `CacheOut`
    /// (`…unresolved.json` stem), `StorePath = <repo>/readiness/evidence-reuse.json`, `Format = Human`.
    type RunRequest =
        { Repo: string
          Scope: ScopeSelector
          StorePath: string
          CacheOut: string
          UnresolvedOut: string
          Format: OutputFormat }

    /// Pure-parser rejections (mirrors RouteCommand.Loop.UsageError) — each maps to `UsageError'`/exit 2.
    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | PathsAndSinceTogether
        | EmptyPaths
        | BadFormat of value: string

    /// The process-level outcome category. Deliberately carries NO ship/blocking verdict (FR-009):
    /// a gate that must recompute or is unresolved is INFORMATION (exit 0), never a non-zero exit.
    type ExitDecision =
        | Success
        | UsageError'
        | InputUnavailable
        | ToolError

    /// Which persisted document an effect/result refers to.
    type ArtifactKind =
        | CacheArtifact
        | UnresolvedArtifact

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter`
    /// executes each and feeds the result back as a `Msg`. `SenseFreshness` carries the selected gates
    /// plus the base/head revisions taken from `RepoSnapshot.Range` (D4) — the interpreter senses the
    /// remaining facts behind the `FreshnessSensor` port and assembles the `SensedFacts`.
    type Effect =
        | SenseScope of ScopeSelector
        | LoadCatalog of repo: string
        | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
        | LoadStore of path: string
        | WriteArtifact of kind: ArtifactKind * path: string * content: string
        | EmitSummary of text: string

    /// External results the interpreter feeds back into `update`. An ABSENT store file is `StoreLoaded
    /// (Ok empty)`, never an `Error` (FR-006); a malformed present store is `StoreLoaded (Error _)`.
    type Msg =
        | Begin
        | Sensed of Result<RepoSnapshot, string>
        | Loaded of Validation
        | FreshnessSensed of Result<SensedFacts, string>
        | StoreLoaded of Result<ReuseStore, string>
        | Wrote of kind: ArtifactKind * result: Result<unit, string>
        | Emitted

    /// A host-edge diagnostic — actionable text carrying NO clock, machine-absolute path, or environment
    /// value (FR-008). Distinct from the F014 catalog `Diagnostic`.
    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    /// How far the pipeline has progressed.
    type Phase =
        | Parsed
        | Sensed'
        | Loaded'
        | Selected
        | Resolved'
        | Evaluated
        | Projected
        | Persisted
        | Done

    /// The durable state the workflow owns. `CacheDoc` is the F042 projection string and `UnresolvedDoc`
    /// the sidecar render, BOTH computed before either write effect is emitted (the RouteCommand precedent).
    type Model =
        { Request: RunRequest
          Phase: Phase
          Snapshot: RepoSnapshot option
          SelectedGates: Gate list
          Sensed: SensedFacts option
          Store: ReuseStore option
          Resolution: FreshnessResolutionReport option
          CacheDoc: string option
          UnresolvedDoc: string option
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    /// The schema id of the no-hide unresolved sidecar (`"fsgg.cache-eligibility.unresolved/v1"`). A fixed
    /// deterministic constant; the F042 `cache-eligibility.json` keeps its own `fsgg.cache-eligibility/v1`.
    val unresolvedSchemaVersion: string

    /// Parse argv into a normalized request. PURE and TOTAL — usage problems are `UsageError` values, never
    /// exceptions. Tolerates a leading `cache-eligibility` verb. `--paths` + `--since` ⇒ `PathsAndSinceTogether`;
    /// `--format` other than `human`/`json` ⇒ `BadFormat`.
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effect(s). `ExplicitPaths` emits `LoadCatalog` directly (no git);
    /// `Since`/`DefaultRange` emit `SenseScope` first (the RouteCommand shape).
    val init: request: RunRequest -> Model * Effect list

    /// The pure transition that IS the whole composition. On a valid catalog it runs the verbatim F022
    /// selection, then emits `SenseFreshness` (carrying base/head from the snapshot range) and `LoadStore`;
    /// once BOTH the sensed facts and the store are present it runs F043 `resolve` → F043 `candidate` →
    /// F041 `evaluate` → F042 `ofReport`, computes the unresolved sidecar, and emits the two `WriteArtifact`
    /// effects; on both writes it emits the summary; then `Done`/`Success`. Any sensing/catalog/store/write
    /// failure short-circuits to `Done` with the mapped `ExitDecision` and NO further effects (no partial
    /// artifact). PURE and TOTAL — no I/O, no hash, never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary — separate from the persisted artifacts — partitioning the selected gates
    /// into reusable / must-recompute / recompute-by-default-unresolved with their causes and named missing
    /// facts. PURE: no clock/abs-path/env, byte-stable for a fixed `Model` (FR-008).
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code: `Success` 0, `UsageError'` 2,
    /// `InputUnavailable` 3, `ToolError` 4. No ship/blocking code (FR-009).
    val exitCode: decision: ExitDecision -> int
