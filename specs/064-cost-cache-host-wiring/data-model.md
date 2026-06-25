# Phase 1 Data Model — Cost-Cache Host Wiring (F25 wiring)

This row introduces **no new pure-core entity**. It adds host-edge glue that builds the four F25 cores' input types
from facts the hosts already hold, and grows each host's `RunRequest`/`Effect`/`ArtifactKind`. All entities below
live at the host edge; none enters a pure core (FR-010).

## Consumed F25 entities (verbatim — not redefined here)

| Entity | Source | Role in the wiring |
|---|---|---|
| `CostBudget { Ceiling: Cost }` | `CostBudget.Model` | the run's cost ceiling, `budgetFor profile mode` |
| `CandidateCost { Gate; Cost; Verdict; Review }` | `CostBudget.Model` | **host builds one per selected gate** |
| `CacheDecision = Reuse \| Recompute \| OverBudget` | `CostBudget.Model` | per-gate decision from `decide` |
| `CacheDecisionReport` | `CostBudget.Model` | the ordered decision set → `cost-budget.json` |
| `CostFinding { Gate; Kind; BaseSeverity; Message }` | `CostBudget.Findings` | advisory finding → `cost-budget.json` |
| `CommandKind` (7 cases) | `CommandKind.Model` | `kindOf gate` target |
| `KindedCommandRun { Kind; Record }` | `CommandKind.Model` | **host builds one per executed gate** |
| `AuditSnapshot { Provenance; Runs }` | `CommandKind.Model` | `auditSnapshot …` → `provenance.json` |

## Host-built input #1 — `CandidateCost` (budget filter)

Built inside `executionPlan` for each selected gate, from facts already in hand:

```
gate.Cost          : Cost                       // from Config (already on the gate)
verdict            : CacheEligibilityVerdict     // from CacheEligibility.evaluate (already computed)
review             : AgentReviewMark             // Deterministic | AgentReviewed (CacheKey …)
                                                 //   from the gate's agent-review identity (D7)
⟹ { Gate = gate.Id; Cost = gate.Cost; Verdict = verdict; Review = review } : CandidateCost
```

`decide (budgetFor profile mode) mode candidates : CacheDecisionReport`. The host then partitions:
`OverBudget` gates are **demoted out of `ToExecute`** (deferred/skipped, never executed, never `Passing`);
`Recompute` gates stay `ToExecute`; `Reuse` gates stay `ToReuse` (charge nothing).

**Validation rules.** Every selected gate appears exactly once as a candidate; the report is `GateId`-ordinal
sorted (core guarantee) so candidate order is irrelevant (FR-006). A demoted gate is structurally excluded from
`applyExecution`'s passed set (SC-002).

## Host-built input #2 — `KindedCommandRun` (kinded recording)

Built on `GatesExecuted records`, where `records : (GateId * CommandRecord) list` already arrives from the edge:

```
kindOf : Gate -> CommandKind                     // total map: gate command category → one of the 7 kinds
⟹ records |> List.map (fun (gid, rec) -> { Kind = kindOf (gateById gid); Record = rec }) : KindedCommandRun list
```

**Validation rules.** `kindOf` is total (no silent mislabel); `runIdentity` is `CommandRecord.canonicalId` verbatim,
so two runs differing only in sensed duration share an identity (FR-004); run order follows the executed order, then
is normalized by the snapshot/projection.

## Host-built input #3 — `AuditSnapshot` (provenance roll-up)

```
auditSnapshot
  sourceCommit:      Revision           // = Head
  baseRevision:      Revision           // from RepoSnapshot
  headRevision:      Revision           // from RepoSnapshot
  ruleHash:          RuleHash           // from SensedFacts (F046)
  generatorVersion:  GeneratorVersion   // from SensedFacts
  artifactDigests:   ArtifactHash list  // from SensedFacts (order-independent set)
  runs:              KindedCommandRun list   // input #2
  environment:       EnvironmentClass   // NEW edge sense — normalized (Local|Ci|LocalOrCi|Release)
  builder:           BuilderIdentity    // NEW edge sense — normalized (no username/host/clock)
  ⟹ AuditSnapshot
```

**Validation rules (determinism, FR-006/SC-003).** `environment` and `builder` MUST be normalized — no username,
hostname, absolute path, or wall-clock — or `provenance.json` would vary by machine. `durationNanos` is emitted as
sensed metadata only and never participates in the snapshot identity.

## Host-built input #4 — the `EvidenceTaint` lookup (findings)

```
taint : GateId -> EvidenceTaint                  // Real | Synthetic, from the gate's recorded evidence provenance
cacheFindings report taint : CostFinding list     // advisory: Stale / SyntheticTaint / NoEvidence
enforce mode profile finding : EnforcementDecision // proves advisory; result NOT applied to the ShipDecision (D6)
```

## Grown host state (per host — declared in `Loop.fsi`, re-blessed in surface baseline)

| Addition | Type | Why |
|---|---|---|
| `ArtifactKind.CostBudgetArtifact` | new case | self-describing sidecar write |
| `ArtifactKind.ProvenanceArtifact` | new case | self-describing sidecar write |
| `RunRequest.CostBudgetOut` | `string` | sidecar path (default `readiness/cost-budget.json`) |
| `RunRequest.ProvenanceOut` | `string` | sidecar path (default `readiness/provenance.json`) |
| `Model` fields for the report/snapshot | `CacheDecisionReport option`, `AuditSnapshot option` | carry the built values to the persist phase |
| `Effect` — reuse existing `WriteArtifact` | (no new case) | two emissions with the new `ArtifactKind`s |
| `kindOf` | `Gate -> CommandKind` | pure kinded-run map |

**Interpreter edge (declared in `Interpreter.fsi`).** Two new edge senses on the `Ports` bundle:
`senseEnvironment : unit -> EnvironmentClass` and `senseBuilder : unit -> BuilderIdentity` (both normalized); the
two new `WriteArtifact` cases are handled by the **existing** atomic writer (no new write port).

## State flow (unchanged backbone, additive branches)

```
Parsed → Sensed' → Loaded' → Selected
   │                            │
   │             executionPlan: + build CandidateCost, decide, demote OverBudget   (D3)
   ▼                            ▼
ExecuteGates (in-budget Recompute only) → GatesExecuted (CommandRecord list)
   │                            │
   │             + kindOf map → KindedCommandRun list → auditSnapshot               (D2, D4)
   ▼                            ▼
Rolled (ShipDecision UNCHANGED) → Persisted
   │
   ├─ WriteArtifact(Verify/AuditArtifact, …)        ← BYTE-IDENTICAL (existing)     (D1, D6)
   ├─ WriteArtifact(CostBudgetArtifact, ofReport report findings)   ← NEW           (D5)
   └─ WriteArtifact(ProvenanceArtifact, ofSnapshot snapshot)        ← NEW           (D5)
```
