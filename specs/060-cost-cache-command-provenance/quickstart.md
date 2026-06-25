# Quickstart & Validation: Cost, Cache, Command, and Provenance (F25)

Runnable validation scenarios proving the feature end to end. Each maps to a user story / success criterion.
Run from the repo root. Contracts: [cost-budget-decision](./contracts/cost-budget-decision.md),
[cost-cache-findings](./contracts/cost-cache-findings.md),
[command-kind-provenance](./contracts/command-kind-provenance.md),
[cost-budget-json](./contracts/cost-budget-json.md), [provenance-json](./contracts/provenance-json.md).
Types: [data-model](./data-model.md).

## Prerequisites

- .NET SDK `net10.0`; `dotnet build FS.GG.Governance.sln` clean.
- Test stack: Expecto 10.2.3 + FsCheck 2.16.6 (already pinned in `Directory.Packages.props`).
- Real `dotnet` on PATH for the command-run kind fixtures (real `ExecutionPort`, as F052/F24).

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test  tests/FS.GG.Governance.CostBudget.Tests/FS.GG.Governance.CostBudget.Tests.fsproj
dotnet test  tests/FS.GG.Governance.CommandKind.Tests/FS.GG.Governance.CommandKind.Tests.fsproj
dotnet test  tests/FS.GG.Governance.CostBudgetJson.Tests/FS.GG.Governance.CostBudgetJson.Tests.fsproj
dotnet test  tests/FS.GG.Governance.ProvenanceJson.Tests/FS.GG.Governance.ProvenanceJson.Tests.fsproj
dotnet test  tests/FS.GG.Governance.VerifyCommand.Tests/FS.GG.Governance.VerifyCommand.Tests.fsproj
```

## Scenario 1 — Cost budget bounds expensive work per profile and mode (Story 1 · SC-001, SC-003)

FSI/semantic test over `Budget.decide`:

1. Build candidate gates spanning all four `Cost` tiers, each with a `MustRecompute` verdict.
2. `decide (budgetFor Light Inner) Inner candidates` → ceiling `Cheap`: the `Cheap` gate is `Recompute`; the
   `Medium`/`High`/`Exhaustive` gates are `OverBudget { Class = Skipped }` (inner-loop mode) with a reason
   naming the gate, tier, and exceeded ceiling.
3. `decide (budgetFor Release Release) Release candidates` → ceiling `Exhaustive`: **every** gate is
   `Recompute`; none is `OverBudget`.
4. `decide (budgetFor Strict Verify) Verify candidates` → ceiling `High`: the `Exhaustive` gate is
   `OverBudget { Class = Deferred }` (boundary mode); the rest `Recompute`.

**Expected**: a skipped/deferred gate is never reported as a pass; the same inputs always produce a
byte-identical report. **Determinism**: shuffle `candidates` → identical report (SC-008).

## Scenario 2 — Evidence reused only when its freshness key proves it applies (Story 2 · SC-002)

Cache hit/miss matrix over the F041 verdict folded by `decide`:

1. A candidate whose verdict is `Reusable ref` → decision `Reuse ref`, **charges nothing** (it is not in
   `recomputeGates report`).
2. For each single freshness dimension (rule hash, an artifact digest, command version, generator version,
   base, head, environment class) changed → F041 yields `MustRecompute (InputsChanged [thatCategory])` →
   `decide` gives `Recompute` (within budget) naming that category, and the gate **is** in
   `recomputeGates report` (charged).
3. A `MustRecompute` gate whose cost exceeds the ceiling → `OverBudget` (Scenario 1), not silently recomputed
   and not silently reused.
4. A gate with `NoPriorEvidence` → `Recompute` with the `NoEvidence` cause — never a fabricated reuse.

**Expected**: reuse happens only on a freshness match; every recompute names its cause; cost-tier change alone
(freshness unchanged) still yields `Reuse` (cost is outside the freshness key).

## Scenario 3 — Stale / synthetic-taint / cache-invalidated findings (Story 3 · SC-004)

Over `Findings.cacheFindings`:

1. A gate whose decision derives from `InputsChanged [RuleHashCat]` → a `Stale [RuleHashCat]` finding naming
   the gate and the changed dimension (`"ruleHash"`).
2. A gate whose supplied taint is `Synthetic` → a distinct `SyntheticTaint` finding — **even when its decision
   is `Reuse`** (synthetic is never silently reused as real).
3. A gate with a clean `Reuse` and `Real` taint → **no** finding.
4. `Findings.enforce mode profile finding` → `EnforcementDecision` with `EffectiveSeverity = Advisory` for
   every kind under every (`Profile`, `RunMode`) — findings never block.

**Expected**: findings are deterministic, carry the offending gate, and are distinguishable by kind.

## Scenario 4 — Command runs across every kind → provenance audit snapshot (Story 4 · SC-005, SC-006)

Over `CommandKind`/`Audit` with a real `ExecutionPort`:

1. Record a run of **each** kind (`Build`/`Test`/`Pack`/`TemplateInstantiation`/`GitDiff`/`PackageInspection`/
   `VisualCapture`) via `GateExecution.senseExecution`; wrap as `KindedCommandRun`. Confirm each `runIdentity`
   is the F032 `canonicalId` and carries its kind.
2. Two runs differing **only** in `SensedDuration` → identical `runIdentity` (duration excluded).
3. `auditSnapshot …` over the runs + provenance inputs → `snapshotIdentity` byte-identical for identical
   inputs; re-running with the same inputs (no-op) is stable; changing a reproducible input (a digest) changes
   it; changing only a duration does **not**.
4. A run that failed to start / timed out is recorded with its F051 sentinel exit code (not dropped).

## Scenario 5 — Agent-review cache identity carried, never blocking (Story 5 · SC-007)

1. A candidate marked `AgentReviewed key` whose `AgentReviewKey.matches` holds → its verdict is `Reusable` →
   decision `Reuse` (reused on matching judge/prompt/check-artifact identity).
2. The same candidate with one F036 identity changed → `MustRecompute` → re-review (`Recompute`), naming the
   changed identity.
3. Across the **full** (`Profile`, `RunMode`) enforcement matrix, an agent-reviewed check's finding
   (`BaseSeverity = Advisory`) derives `Advisory` every time — it never changes a blocking verdict regardless
   of its cache decision. `AdvisoryPromotion` is never invoked.

## Scenario 6 — End-to-end through `fsgg verify` (integration · FR-014, FR-015)

On a real checked-out product (standalone, no monorepo):

```bash
dotnet run --project src/FS.GG.Governance.VerifyCommand -- --repo <product> --profile strict --format json
```

**Expected**:
- A `MustRecompute` gate over the (Strict, Verify) budget is **not** executed (absent from the executed gate
  runs) and is recorded `Deferred` in `cost-budget.json`; in-budget `MustRecompute` gates run; `Reusable`
  gates reuse.
- `cost-budget.json` (`fsgg.cost-budget/v1`) and `provenance.json` (`fsgg.provenance/v1`) are written, each
  deterministic and byte-identical on a re-run with unchanged inputs.
- Existing `verify.json` / `route.json` / `audit.json` goldens are **byte-identical** to before (the sidecars
  are new artifacts, not changes to existing schemas).
- The run uses only the product's own recorded evidence/command-runs/provenance — no monorepo path (FR-015).

## Done-when

- All five new/extended test projects green; `dotnet build FS.GG.Governance.sln` clean.
- Four new surface baselines blessed (`BLESS_SURFACE=1`) and committed.
- The 4×6 budget matrix, the single-dimension cache hit/miss matrix, the every-kind command-run fixtures, the
  snapshot byte-identity + stability fixture, the agent-review-never-blocks matrix, and the determinism/reorder
  tests all pass.
