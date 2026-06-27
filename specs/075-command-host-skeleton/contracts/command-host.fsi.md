# Contract: `FS.GG.Governance.CommandHost` public surface (`.fsi`-first draft)

The leaf's contract is its curated `.fsi`. Per Constitution Principle I/II this is
drafted **before** the `.fs` body and is the *sole* declaration of the public
surface; the surface-area baseline (`surface/FS.GG.Governance.CommandHost.surface.txt`)
and the reflective drift test in `CommandHost.Tests` pin it. The sketch below is the
target surface; final `open`s and the exact `Model`-view inputs are settled during
implementation (the byte-identity gate validates each move). Anything not listed here
stays **private** (and, where it is a divergent helper, **local** to its host — see
[research.md](../research.md) D5/D6).

```fsharp
// Curated public signature contract for the pure command-host skeleton leaf (feature 075, Phase B).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the
// matching CommandHost.fs carries NO access modifiers. Drafted .fsi-first (Principle I).
//
// Pure, total host-skeleton helpers shared by the MVU command Loop.fs hosts. No I/O; the leaf sits BELOW
// the hosts and ABOVE the domain-type owners whose values it walks. Output is byte-identical to today's
// per-host copies (feature 075 acceptance gate: every command/projection golden unchanged).
//
// SCOPE NOTE: only genuinely-shared members live here. Release's `buildSnapshot` (different input type),
// `cacheReportOf` (single site), and each host's `ExitCodeBasis -> ExitDecision` policy mapper DIVERGE and
// stay LOCAL in their hosts (research D5/D6, FR-008).

namespace FS.GG.Governance.CommandHost

open FS.GG.Governance.Snapshot.Model            // Revision, CommitId, Range
open FS.GG.Governance.Gates.Model               // Gate, GateId, GateCommand
open FS.GG.Governance.GateRun.Model             // GateOutcome / ExitCode
open FS.GG.Governance.FreshnessSensing.Model     // SensedFacts, FreshnessInputs
open FS.GG.Governance.CacheEligibility.Model     // CacheEligibilityVerdict, CacheDecisionReport
open FS.GG.Governance.CostBudget.Model           // BudgetReason
// (final open set fixed by the compiler against the moved bodies — research D7)

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

    /// Shared gate-execution plan. Computes the identical non-budget prefix for every host; applies the
    /// budget fold when present (Ship/Verify) else returns an empty report (Route). Plans are byte-identical
    /// to each host's pre-extraction plan.
    val executionPlan:
        parms: ExecutionPlanParams ->
        // model-view inputs settled in implementation (Sensed/Store/SelectedGates/Tooling/Request.Repo) so
        // the leaf does NOT depend on any host's concrete Model type:
        sensed: SensedFacts option ->
        // ... remaining inputs ...
            (Gate * GateClassification) list * Map<string, FreshnessInputs> * CacheDecisionReport

    // ---- micro-helpers (verbatim relocations — research audit) ----

    /// Repo-relative path joiner. `under "." rel = rel`; otherwise `repo.TrimEnd('/') + "/" + rel`.
    val under: repo: string -> rel: string -> string

    /// CommitId -> Revision (never re-sensed or fabricated).
    val revOfCommit: commit: CommitId -> Revision

    /// Base/head revisions from the snapshot range, or (None, None) when absent.
    // (signature mirrors the host form; takes the snapshot range view, not a host Model)
    val baseHeadOf: range: Range option -> Revision option * Revision option

    /// Fallback SensedFacts when freshness sensing is unavailable (all-empty).
    val emptySensedFacts: SensedFacts

    // fail / describeInvalid / persistedContent / awaitingPersist / tryExecute /
    // buildSnapshot (Verify↔Ship form) / kindedRunsOf / kindOf — declared here once their host text is
    // confirmed byte-identical at move time (research audit + FR-013 one-concern-per-commit).
```

## Contract obligations (what the surface-drift + golden tests enforce)

1. **Exactly the shared helpers, nothing more** (FR-003): the baseline equals the
   rendered reflection; any added/removed member fails `SurfaceDriftTests` until
   re-blessed via `BLESS_SURFACE=1 dotnet test`.
2. **No host/impure edge** (FR-002): the scope-guard test asserts the leaf
   references no `Host`/`Cli`/`*Command` and no filesystem/git/process project.
3. **Byte-identical behavior** (FR-009): every consumer's command/projection golden
   and snapshot is unchanged after the host switches to the leaf helper.
4. **Re-export preserves host surfaces**: each host's `Loop.fsi` keeps its
   `ExitDecision`/`GateClassification` surface via a type alias to the leaf, so the
   per-host surface baselines do not drift unexpectedly (changes, if any, are
   blessed deliberately and noted).
