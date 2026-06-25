# Phase 1 Data Model: Cost, Cache, Command, and Provenance (F25)

All new types are **closed DUs and plain records**. Every reused type is named with its origin and is consumed
**verbatim** — no field is added to or removed from a reused type. Field order in records below is the intended
`.fsi` declaration order.

## Reused vocabulary (verbatim, no change)

| Type | Origin | Used for |
|------|--------|----------|
| `Cost = Cheap \| Medium \| High \| Exhaustive` (ordered) | F014 `Config.Model` | the budget ceiling and each candidate's tier |
| `Maturity = Observe \| Warn \| BlockOnPr \| BlockOnShip \| BlockOnRelease` | F014 `Config.Model` | the fixed `Warn` maturity `enforce` feeds `deriveEffectiveSeverity` |
| `Profile = Light \| Standard \| Strict \| Release` | F023 `Enforcement` | budget scope (strictness lever) |
| `RunMode = Sandbox \| Inner \| Focused \| Verify \| Gate \| Release` | F023 `Enforcement` | budget scope (protectiveness lever); skip-vs-defer class |
| `Severity = Advisory \| Blocking` | F023 `Enforcement` | base severity of cost/cache findings |
| `EnforcementInput`, `EnforcementDecision`, `deriveEffectiveSeverity` | F023 `Enforcement` | enforce findings without re-opening the truth table |
| `GateId` | F018 `Gates.Model` | identifies each candidate gate / decision entry |
| `CacheEligibilityVerdict = Reusable of EvidenceRef \| MustRecompute of RecomputeCause` | F041 `CacheEligibility.Model` | the verdict the budget folds in |
| `RecomputeCause = NoPriorEvidence \| InputsChanged of InputCategory list` | F030 `EvidenceReuse.Model` | the cause carried into `Recompute`/findings |
| `EvidenceRef` | F030 `EvidenceReuse.Model` | the reuse reference carried in `Reuse` |
| `InputCategory` (10 cases) + `categoryToken` | F029 `FreshnessKey.Model` | naming the changed freshness dimension in findings |
| `CacheKey`, `matches`, `compute` | F036 `AgentReviewKey` | agent-review cache identity carried in the decision |
| `CommandRecord`, `canonicalId`, `identityValue`, `SensedDuration`, `ExitCode` | F032 `CommandRecord.Model` | the recorded run wrapped by a kind; identity unchanged |
| `Provenance`, `build`, `canonicalId`, `Revision`, `RuleHash`, `GeneratorVersion`, `ArtifactHash`, `EnvironmentClass`, `BuilderIdentity` | F033 `Provenance` | the provenance inputs + roll-up; identity unchanged |
| `GateOutcome`, `GateDisposition` | F052 `GateRun.Model` | per-gate execution outcome at the host edge |
| `ExecutionPort`, `senseExecution`, sentinel exit codes | F051 `GateExecution` | the host-edge command run |

## New vocabulary — `FS.GG.Governance.CostBudget` (pure)

### `CostBudget`

```
type CostBudget = { Ceiling: Cost }
```

The maximum `Cost` tier a must-recompute gate may recompute at this run. `Cheap` is the floor (zero expensive
budget); `Exhaustive` admits the full matrix. A budget is **not** scoped data — it is *derived from* a
(`Profile`, `RunMode`) by `budgetFor` (below).

- `budgetFor : Profile -> RunMode -> CostBudget` — total, deterministic. Returns
  `{ Ceiling = min (profileCeiling profile) (modeCeiling mode) }` using the D1 monotone projection tables.
  `profileCeiling`/`modeCeiling` are unexported helpers (off-surface).
- `fits : CostBudget -> Cost -> bool` — `cost <= ceiling` over the ordered `Cost` DU (inclusive boundary).

### `CandidateCost` — the per-gate input to `decide`

```
type AgentReviewMark =
    | Deterministic                       // ordinary gate (default)
    | AgentReviewed of CacheKey            // F036 identity carried; stays advisory (never blocks)

type CandidateCost =
    { Gate: GateId
      Cost: Cost                           // the gate's declared cost tier (F014)
      Verdict: CacheEligibilityVerdict      // the F041 verdict, verbatim
      Review: AgentReviewMark }             // Deterministic | AgentReviewed key
```

The `Verdict` is produced upstream (F041 for deterministic gates; an `AgentReviewKey.matches` comparison at the
edge yields the `Reusable`/`MustRecompute` for agent-reviewed gates). `decide` never recomputes a freshness or
agent-review match — it folds the supplied verdict with the budget.

### `CacheDecision` — the single budgeted decision (FR-004)

```
type DeferralClass =
    | Skipped                              // inner-loop mode (Sandbox/Inner/Focused)
    | Deferred                             // boundary mode (Verify/Gate/Release)

type BudgetReason =
    { Gate: GateId
      Cost: Cost                           // the over-budget tier
      Ceiling: Cost                        // the exceeded ceiling
      Class: DeferralClass }               // why skip vs defer (D2)

type CacheDecision =
    | Reuse of EvidenceRef                  // verdict was Reusable; charges nothing
    | Recompute of RecomputeCause            // MustRecompute and cost <= ceiling; charges its cost
    | OverBudget of BudgetReason             // MustRecompute and cost > ceiling; Skipped | Deferred

type CacheDecisionEntry =
    { Gate: GateId
      Cost: Cost
      Review: AgentReviewMark
      Decision: CacheDecision }

type CacheDecisionReport = CacheDecisionReport of CacheDecisionEntry list
```

- `decide : CostBudget -> RunMode -> CandidateCost list -> CacheDecisionReport` — pure, total,
  order-independent. Per candidate:
  - `Reusable ref` → `Reuse ref` (budget untouched).
  - `MustRecompute cause` with `fits budget cost` → `Recompute cause`.
  - `MustRecompute _` with `not (fits budget cost)` → `OverBudget { Class = (Skipped | Deferred) }`, the class
    chosen by the `RunMode` (boundary → `Deferred`, inner-loop → `Skipped`).
  - Entries sorted by `GateId` ordinal (structural tiebreak), so the report is byte-identical regardless of
    input order (SC-008).
- `recomputeGates : CacheDecisionReport -> GateId list` — the gates whose decision is `Recompute` (what the
  host edge feeds to `ExecuteGates`). `reuseGates : CacheDecisionReport -> GateId list` likewise; `overBudget :
  CacheDecisionReport -> (GateId * BudgetReason) list` pairs each over-budget gate with its reason.

**Validation / invariants.** `decide` is defined for every combination of budget, mode, and candidate list
(including empty → empty report). A `Reusable` verdict can never become `OverBudget` (reuse is free). An
`OverBudget` decision always carries a non-empty reason (the record's fields are total). An `AgentReviewed`
candidate's decision is computed identically to a deterministic one — the review mark affects only enforcement
(advisory), never the budget arithmetic.

## New vocabulary — `FS.GG.Governance.CostBudget.Findings` (pure)

```
type CostFindingKind =
    | Stale of InputCategory list           // a freshness dimension changed (from InputsChanged); cache-invalidated
    | SyntheticTaint                         // evidence was produced synthetically, not by a real run
    | NoEvidence                             // no prior evidence existed (from NoPriorEvidence)

type EvidenceTaint =
    | Real
    | Synthetic                              // supplied sensed input (D5); NOT a field on F030 RecordedEvidence

type CostFinding =
    { Gate: GateId
      Kind: CostFindingKind
      BaseSeverity: Severity                  // Advisory for all three kinds (never escalates)
      Message: string }                       // names the gate and cause; no raw paths/clock/env
```

- `cacheFindings : CacheDecisionReport -> (GateId -> EvidenceTaint) -> CostFinding list` — pure, total. For
  each entry:
  - `Recompute (InputsChanged cats)` (or an `OverBudget` whose underlying cause carried changed inputs) →
    `Stale cats` finding naming each dimension via `categoryToken`.
  - `Recompute NoPriorEvidence` → `NoEvidence` finding.
  - any gate whose supplied taint is `Synthetic` → a **distinct** `SyntheticTaint` finding, **even when the
    decision is `Reuse`** (spec edge "synthetic evidence reused").
  - a clean `Reuse` with `Real` taint → **no** finding (SC-004).
  - findings sorted by `(GateId ordinal, kind tag)`; byte-identical for identical input.
- `enforce : RunMode -> Profile -> CostFinding -> EnforcementDecision` — thin call into
  `Enforcement.deriveEffectiveSeverity` with `BaseSeverity`, the fixed `Maturity = Warn` (F014 `Config.Model`),
  the mode and profile. Reuses the truth table; never escalates an advisory finding.

## New vocabulary — `FS.GG.Governance.CommandKind` (pure)

```
type CommandKind =
    | Build
    | Test
    | Pack
    | TemplateInstantiation
    | GitDiff
    | PackageInspection
    | VisualCapture

type KindedCommandRun =
    { Kind: CommandKind
      Record: CommandRecord }                 // F032 wrapped, NOT extended

type AuditSnapshot =
    { Provenance: Provenance                  // F033 roll-up of the provenance inputs + the runs' .Records
      Runs: KindedCommandRun list }            // carried alongside for the kind labels
```

- `kindToken : CommandKind -> string` — stable wire token (`build`/`test`/`pack`/`templateInstantiation`/
  `gitDiff`/`packageInspection`/`visualCapture`); exhaustive match, no wildcard.
- `runIdentity : KindedCommandRun -> string` — exactly `CommandRecord.identityValue (CommandRecord.canonicalId
  run.Record)` (kind does **not** participate — D5). Two runs differing only in `SensedDuration` share it.
- `auditSnapshot : Revision -> Revision -> Revision -> RuleHash -> GeneratorVersion -> ArtifactHash list ->
  KindedCommandRun list -> EnvironmentClass -> BuilderIdentity -> AuditSnapshot` — builds the F033 `Provenance`
  via `Provenance.build` from the supplied inputs and the runs' `.Record`s, and carries the `Runs` for the
  projection. The snapshot's identity is `Provenance.canonicalId snapshot.Provenance` (reused verbatim).

**Invariants.** The snapshot is byte-identical for identical inputs and changes only when a *reproducible*
input changes (commit, base/head, rule hash, generator version, an artifact digest, the environment, or a
command run) — inherited from `Provenance.canonicalId` (SC-006). Duration never affects identity. A run that
failed to start / timed out is recorded with its F051 sentinel exit code (never dropped — spec edge).

## Recorded micro-decisions

- **Why `OverBudget` carries the cause indirectly.** `OverBudget` records the budget reason (gate/cost/ceiling/
  class) but the underlying `RecomputeCause` (which freshness dimension changed) is still available to
  `cacheFindings` because the entry retains enough to emit the `Stale` finding; the budget *outcome* and the
  *staleness explanation* are separate concerns (one in `cost-budget.json`'s decision section, one in its
  findings section).
- **Why `AgentReviewMark` lives on the candidate, not a separate map.** Keeping the review mark per-candidate
  makes `decide` and the projection total without a side lookup, and makes the never-blocks guarantee
  testable per entry (SC-007).
- **Why two projection libraries, not one.** `cost-budget.json` (the per-gate decision + findings) and
  `provenance.json` (the reproducibility snapshot) are distinct artifacts with distinct identities and distinct
  consumers; splitting them mirrors the one-projection-per-document precedent and lets each schema evolve
  independently (research D3).
