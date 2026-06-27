// Curated public signature contract for the pure command-host skeleton leaf (feature 075, Phase B).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// CommandHost.fs carries NO access modifiers. Drafted .fsi-first (Principle I).
//
// Pure, total host-skeleton helpers shared by the MVU command Loop.fs hosts. No I/O; the leaf sits BELOW the
// hosts and ABOVE the domain-type owners whose values it walks. Output is byte-identical to today's per-host
// copies (feature 075 acceptance gate: every command/projection golden unchanged).
//
// SCOPE NOTE (FR-008): only genuinely-shared, type-honest members live here. `fail`/`tryExecute`/
// `awaitingPersist` are parameterized by a per-host `Model`/`Effect` record and stay LOCAL (a shared form
// would require generics over each host's Model — Constitution III); Refresh's `fail`/`exitCode` (over
// `RefreshOutcome`), Release's `buildSnapshot` (different input type), and `cacheReportOf` (single site)
// likewise stay LOCAL (research D5/D6). Model-reading movable helpers are DECOMPOSED into the fields they
// read so the leaf never depends on a host Model.

namespace FS.GG.Governance.CommandHost

open FS.GG.Governance.Config.Model            // Diagnostic, ToolingFacts, EnvironmentClass
open FS.GG.Governance.Snapshot.Model          // CommitId, DiffRange
open FS.GG.Governance.FreshnessKey.Model      // Revision, FreshnessInputs
open FS.GG.Governance.Gates.Model             // Gate, GateId
open FS.GG.Governance.GateExecution.Model     // GateCommand
open FS.GG.Governance.CommandRecord.Model     // CommandRecord, ExitCode
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts
open FS.GG.Governance.CacheEligibility.Model  // CacheEligibilityVerdict
open FS.GG.Governance.EvidenceReuse.Model     // ReuseStore
open FS.GG.Governance.CostBudget.Model        // BudgetReason, CacheDecisionReport
open FS.GG.Governance.CommandKind.Model       // CommandKind, KindedCommandRun, AuditSnapshot
open FS.GG.Governance.Provenance.Model        // BuilderIdentity

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CommandHost =

    // ---- exit classification (research D2, FR-005) ----

    /// Canonical process-level exit classification (superset, with the optional blocked path).
    type ExitDecision =
        | Success
        | Blocked
        | UsageError'
        | InputUnavailable
        | ToolError

    /// Total exit-code mapping: Success->0, Blocked->1, UsageError'->2, InputUnavailable->3, ToolError->4.
    val exitCode: decision: ExitDecision -> int

    // ---- gate classification (research D3, FR-005) ----

    /// How a selected gate is dispatched. `Deferred` is produced only when a budget fold runs (Ship/Verify).
    type GateClassification =
        | ToExecute of GateCommand
        | ToReuse of ExitCode
        | Deferred of BudgetReason
        | NoCommand

    // ---- parameterized execution plan (research D4, FR-006) ----

    /// Per-command optional folds that distinguish one host's gate-execution plan from another's.
    type ExecutionPlanParams =
        { BudgetFold:
            (Map<string, CacheEligibilityVerdict> -> Map<string, BudgetReason> * CacheDecisionReport) option }

    // ---- micro-helpers (verbatim relocations — research audit) ----

    /// Repo-relative path joiner. `under "." rel = rel`; otherwise `repo.TrimEnd('/') + "/" + rel`.
    val under: repo: string -> rel: string -> string

    /// CommitId -> Revision (never re-sensed or fabricated).
    val revOfCommit: commit: CommitId -> Revision

    /// Base/head revisions from the snapshot diff-range, or (None, None) when absent.
    val baseHeadOf: range: DiffRange option -> Revision option * Revision option

    /// Fallback SensedFacts when freshness sensing is unavailable (all-empty).
    val emptySensedFacts: SensedFacts

    /// Human-readable catalog-invalid summary over Config diagnostics.
    val describeInvalid: diags: Diagnostic list -> string

    /// The persisted reuse-store document: prune -> retain(default bound) -> serialise over the loaded store.
    val persistedContent: loaded: ReuseStore -> string

    /// The CommandKind of a gate, inferred from its freshness-key command token (Build is the default).
    val kindOf: gate: Gate -> CommandKind

    /// Pair executed records with their gate kind, keyed via the selected gates (Verify<->Ship common form).
    val kindedRunsOf: selectedGates: Gate list -> records: (GateId * CommandRecord) list -> KindedCommandRun list

    /// Assemble the audit snapshot from sensed facts, snapshot range, environment/identity, and runs
    /// (Verify<->Ship common form; Release's same-named builder takes a different input and stays local).
    val buildSnapshot:
        sensed: SensedFacts option ->
        range: DiffRange option ->
        environment: EnvironmentClass option ->
        builder: BuilderIdentity option ->
        runs: KindedCommandRun list ->
            AuditSnapshot

    /// Shared gate-execution plan. Computes the identical non-budget prefix for every host; applies the
    /// budget fold when present (Ship/Verify) else returns an empty report (Route). Plans are byte-identical
    /// to each host's pre-extraction plan. Decomposed model-view inputs so the leaf depends on no host Model.
    val executionPlan:
        parms: ExecutionPlanParams ->
        sensed: SensedFacts option ->
        store: ReuseStore option ->
        selectedGates: Gate list ->
        tooling: ToolingFacts option ->
        repo: string ->
            (Gate * GateClassification) list * Map<string, FreshnessInputs> * CacheDecisionReport
