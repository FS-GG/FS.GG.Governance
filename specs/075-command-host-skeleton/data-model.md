# Phase 1 Data Model: CommandHost skeleton extraction

This feature is a behavior-preserving extraction, so it introduces **no new domain
data** — it relocates existing host-skeleton types and helpers into one leaf. The
"entities" below are the types the leaf will own, their shape, and the rules that
keep their move byte-identical. All references to source line numbers are the
2026-06-27 working tree.

## Entity: `CommandHost` leaf (the library)

The new pure leaf `FS.GG.Governance.CommandHost`. Owns the shared host skeleton.
- **Purity rule (FR-002):** depends only on already-shared domain-type projects
  (research D7); references no host/filesystem/git/process project. Enforced by the
  scope-guard test.
- **Surface rule (FR-003/FR-004):** public surface declared solely in
  `CommandHost.fsi`; baseline at `surface/FS.GG.Governance.CommandHost.surface.txt`;
  reflective drift test in `CommandHost.Tests`.
- **Placement rule (FR-011):** below the command hosts, above the domain types;
  graph stays acyclic.

## Entity: `ExitDecision` (canonical DU)

The process-level exit classification. Canonical **superset** form moved to the leaf
(research D2).

| Case | Exit code (`exitCode`) | Notes |
|---|---|---|
| `Success` | 0 | |
| `Blocked` | 1 | The merge-blocking verdict; present in Ship/Verify/Release; never *produced* by Route/Refresh/Evidence/CacheEligibility. |
| `UsageError'` | 2 | |
| `InputUnavailable` | 3 | |
| `ToolError` | 4 | |

- **Validation/total-function rule:** `exitCode : ExitDecision -> int` is total over
  the superset (no non-exhaustive match in the leaf).
- **Surface-preservation rule:** each host re-exports the leaf type
  (`type ExitDecision = CommandHost.ExitDecision`) so its own `Loop.fsi` surface is
  unchanged; consuming `match` sites that become non-exhaustive under the superset
  gain behavior-preserving arms (D2).

## Entity: `GateClassification` (superset DU)

How a selected gate is dispatched. Canonical **superset** form moved to the leaf
(research D3).

| Case | Payload | Produced by |
|---|---|---|
| `ToExecute` | `GateCommand` | all hosts |
| `ToReuse` | `ExitCode` | all hosts |
| `Deferred` | `BudgetReason` | Ship/Verify only (F25 cost-budget demotion) |
| `NoCommand` | — | all hosts |

- **Rule:** Route never produces `Deferred` (its `ExecutionPlanParams.BudgetFold` is
  `None`); Route's consuming matches gain an unreachable, behavior-preserving
  `Deferred` arm (D3).

## Entity: `ExecutionPlanParams` (per-command fold record)

The plain record that parameterizes the shared `executionPlan` (research D4,
FR-006). Carries the per-command behavioral differences as data/closures — no
command-identity branching inside the leaf.

| Field | Type | Meaning |
|---|---|---|
| `BudgetFold` | `(Map<string, CacheEligibilityVerdict> -> Map<string, BudgetReason> * CacheDecisionReport) option` | `None` ⇒ no demotion, empty report (Route). `Some f` ⇒ Ship/Verify: compute the F25 over-budget map + the `CacheDecisionReport`. |

- **Field membership rule:** add a field only when a genuine per-command difference
  exists *and* expressing it as a parameter keeps the leaf command-agnostic. The
  budget fold is the only confirmed difference; further fields are added only if the
  byte-identity gate reveals another divergence in the shared prefix (none expected).

## Relationship: `executionPlan` (the shared function)

```
executionPlan : ExecutionPlanParams
             -> sensed: SensedFacts option
             -> // ...remaining decomposed model-view inputs (Store/SelectedGates/
                //    Tooling/Request.Repo) — settled in implementation...
                (Gate * GateClassification) list
              * Map<string, FreshnessInputs>
              * CacheDecisionReport
```

- The leaf takes **no host `Model`**: each host's `Model` is decomposed into the
  discrete view inputs the plan needs (sensed facts, persisted store, selected
  gates, tooling, repo path), passed as ordinary arguments. This keeps the leaf
  pure and host-agnostic (FR-002) — it never references a host's concrete `Model`
  type. The exact argument list mirrors the contract `.fsi`
  ([contracts/command-host.fsi.md](./contracts/command-host.fsi.md)) and is fixed
  by the compiler during implementation.
- Computes the identical non-budget prefix (freshness resolve → cache-eligibility →
  `verdictMap`/`inputsMap` → base `classify`) for every caller.
- Applies `BudgetFold` when present; otherwise returns `CacheDecisionReport []`.
- **Byte-identity rule (FR-009):** Route destructures the 3-tuple and discards the
  (empty) report; its `(Gate * GateClassification) list` is bit-identical to the
  pre-extraction 2-tuple's first element. Ship/Verify receive their exact prior plan.

## Moved helpers (no new shape — relocation only)

Pure, total, behavior-preserving relocations (research audit table):
`under`, `fail`, `revOfCommit`, `baseHeadOf`, `emptySensedFacts`, `describeInvalid`,
`persistedContent`, `awaitingPersist`, `tryExecute`, and the Verify↔Ship
`buildSnapshot`/`kindedRunsOf`/`kindOf`. Each retains its exact signature and body;
the host's local copy is deleted and replaced by a reference to the leaf.

## Deliberately-not-moved (recorded divergence — FR-008)

| Item | Why it stays local |
|---|---|
| Release's `buildSnapshot` | Different input type (`ReleaseDeclaration * PackEvidenceSet`) — a different function sharing a name (D5). |
| `cacheReportOf` | Single defining site (CacheEligibilityCommand) — no duplication to remove (D6). |
| Per-host `ExitCodeBasis -> ExitDecision` mapper | Host-specific decision policy, not skeleton. |
