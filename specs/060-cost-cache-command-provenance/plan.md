# Implementation Plan: Cost, Cache, Command, and Provenance — Budgeted Evidence Reuse (F25)

**Branch**: `060-cost-cache-command-provenance` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/060-cost-cache-command-provenance/spec.md`

## Summary

After F24, Governance runs real, sometimes-expensive deterministic checks across a product's surfaces, but
nothing **bounds** that expense or **governs** when its evidence may be reused. The pieces that decide *whether
prior evidence still applies* already exist and are correct and are reused **verbatim**: the freshness key
(F029 `FreshnessKey.FreshnessInputs` — 10 dimensions, `Cost` deliberately absent), the reuse decision
(F030 `EvidenceReuse.decide` → `Reuse`/`Recompute`+cause), the per-gate cache-eligibility verdict
(F041 `CacheEligibility.evaluate` → `Reusable`/`MustRecompute`+`RecomputeCause`), command runs
(F032 `CommandRecord`), the execution port (F051 `GateExecution.ExecutionPort`, sentinel exit codes),
provenance (F033 `Provenance` + `canonicalId`), the agent-review cache identity (F036 `AgentReviewKey`), and
the enforcement truth table (F018/F023 `Enforcement.deriveEffectiveSeverity`).

What is missing is the **cost dimension of the decision and its audit**. This row supplies exactly four new
things and reuses everything else unchanged:

1. **A `CostBudget` scoped to a (`Profile`, `RunMode`) pair** that bounds the expensive recompute a run may
   perform, expressed over the existing four-value ordered `Cost` DU — **no new tier, profile, or mode**
   (FR-001). Represented as an **ordered cost ceiling with a `Cheap` floor** (research D1).
2. **A single per-gate budgeted cache decision** that folds the *existing* F041 verdict together with the
   budget into one of `Reuse` (free), `Recompute` (charged), or `Skip`/`Defer` (over budget, named reason)
   — **without** adding a new freshness dimension or a new reuse verdict (FR-004, FR-005, FR-006). Skip vs
   defer is decided by run-mode class (research D2).
3. **Cost/cache findings** — `Stale` (the cache-invalidated finding, naming the changed freshness dimension,
   derived from the F041 `RecomputeCause`) and a distinct `SyntheticTaint` — enforced through the existing
   `deriveEffectiveSeverity` (never re-opening the truth table) and surfaced additively (FR-007, FR-013).
4. **A command-run *kind* taxonomy** (`Build`/`Test`/`Pack`/`TemplateInstantiation`/`GitDiff`/
   `PackageInspection`/`VisualCapture`) wrapping the F032 `CommandRecord` **unchanged**, rolled up with the
   F033 `Provenance` inputs into a **deterministic provenance audit snapshot** (FR-008, FR-009).

The cache decision also **carries** the F036 agent-review cache identity so agent-reviewed evidence reuses on
matching judge/prompt/check-artifact identity, while agent-reviewed checks **stay advisory** — F25 never calls
`AdvisoryPromotion` (F039, a later row) and never promotes an agent-reviewed check to a blocker (FR-010,
SC-007).

The work **composes the leaf-plus-sensor precedent** (F029/F041/F051): the budget and cache decisions are
**pure, total functions** over already-sensed inputs; the only I/O — recording command runs, reading the
evidence/provenance sources — lives at the host edge through the existing ports (FR-014). Surfacing follows the
established **deterministic-JSON, byte-identical-when-empty** discipline (F042/F045/F052): two new sidecar
projections (`cost-budget.json`, `provenance.json`) written by the existing `fsgg verify`/`fsgg ship` hosts,
leaving every existing `route.json`/`verify.json`/`audit.json` golden byte-identical.

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **Budget = ordered cost ceiling with a `Cheap` floor (D1).** A `CostBudget` is a single `Cost` ceiling: a
   must-recompute gate recomputes iff its tier `<= ceiling`, else it is over budget. `Cheap` is the floor
   (cheap recompute is never bounded — the "zero/disabled expensive budget" edge), `Exhaustive` admits the
   full matrix (Release). The ceiling is `min(profileCeiling, modeCeiling)` of two monotone projections — both
   the strictness lever and the protectiveness lever can restrict. This reuses the existing **ordered** `Cost`
   DU directly, is per-gate and so trivially **order-independent**, and introduces **no arbitrary numeric
   tier weights** (the rejected per-tier-count alternative, see research D1).
2. **Skip vs defer by run-mode class (D2).** An over-budget must-recompute gate is **`Defer`** in a boundary
   mode (`Verify`/`Gate`/`Release` — it must eventually run at a stricter boundary) and **`Skip`** in an
   inner-loop mode (`Sandbox`/`Inner`/`Focused` — the inner loop deliberately won't run it). Both carry a
   named reason (gate id, cost tier, exceeded ceiling); neither is ever a pass (FR-003).
3. **Two new pure cores + two new sidecar projections; no new host command (D3).**
   `FS.GG.Governance.CostBudget` (budget + budgeted cache decision + cost/cache findings + carried agent-review
   identity) and `FS.GG.Governance.CommandKind` (kind taxonomy + provenance-audit-snapshot roll-up over F033)
   are pure leaves; `FS.GG.Governance.CostBudgetJson` (`fsgg.cost-budget/v1`) and
   `FS.GG.Governance.ProvenanceJson` (`fsgg.provenance/v1`) project them. The existing `fsgg verify`/`fsgg
   ship` hosts gain an edge step that consults the budget to filter `ExecuteGates`, records kinded command
   runs, and writes the two sidecars — additively, every existing golden untouched.
4. **Reuse F029/F030/F041/F032/F033/F036/F018-F023 unchanged (D4).** No new freshness dimension, no new reuse
   verdict, no change to `FreshnessKey`/`EvidenceReuse`/`CacheEligibility`, no change to `CommandRecord` or
   `Provenance` identity, no new enforcement truth table. `Cost` stays excluded from the freshness key. The
   command-run *kind* is descriptive metadata that does **not** alter the F032 `CommandRecord` identity, so the
   reproducible identity is F032's, reused verbatim (research D5).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`, `LangVersion=latest`).

**Primary Dependencies**: **FSharp.Core 10.1.301 only** for the pure cores; the JSON projections use
**BCL `System.Text.Json`** (`Utf8JsonWriter`, already used by every `*Json` projection) — **no new package**.
The host-edge wiring reuses the existing `FS.GG.Governance.GateExecution.ExecutionPort` (F051) for command
runs and `FS.GG.Governance.FreshnessSensing.StoreReader` (F046) for evidence — **no new dependency anywhere**.
Project references reused verbatim: `Config` (`Cost`), `Enforcement` (`Profile`, `RunMode`, `Severity`,
`deriveEffectiveSeverity`), `CacheEligibility` (`CacheEligibilityReport`, `CacheEligibilityVerdict`),
`EvidenceReuse` (`EvidenceRef`, `RecomputeCause`), `FreshnessKey` (`InputCategory`, `categoryToken`),
`AgentReviewKey` (`CacheKey`, `matches`), `CommandRecord` (`CommandRecord`, `canonicalId`), `Provenance`
(`Provenance`, `build`, `canonicalId`), `Gates`/`GateRun` (`GateId`, `GateOutcome`), `Findings`
(finding-construction precedent).

**Storage**: None new in the cores (pure evaluation over caller-supplied sensed inputs). The only new writes
are the two deterministic **sidecar artifacts** (`cost-budget.json`, `provenance.json`) through the existing
`ArtifactWriter` edge port; the existing evidence-reuse store (F047/F048) is read and written by the existing
machinery, unchanged. No database, no network, no registry.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck / FsCheck 2.16.6 (repo standard). One new test project per new
library. The matrices the spec demands: a **cost-budget enforcement matrix** across the 4×6 (`Profile`,
`RunMode`) grid × cost tiers (SC-001, SC-003); a **cache hit/miss matrix** covering every single-dimension
freshness change driving `Recompute` + the budget charge (SC-002); **stale/synthetic-taint fixtures** (SC-004);
**command-run fixtures across every kind** + a duration-invariance identity test (SC-005); an
**audit-provenance snapshot** byte-identity + no-op-input-change stability fixture (SC-006); the **agent-review
never-blocks** assertion across the full enforcement matrix (SC-007); and a **determinism/reordering** test per
projection (SC-008). FSI semantic tests load the public surface (`decide`, `budgetFor`, `cacheFindings`,
`auditSnapshot`, `ofReport`), never internals (Constitution I).

**Target Platform**: Cross-platform .NET libraries + the existing `fsgg verify` / `fsgg ship` CLI executables
(Linux/macOS/Windows); standalone (no monorepo) and monorepo usage (FR-015).

**Project Type**: Cost-governance core expansion — two pure leaf cores, two deterministic JSON projections, and
an additive host-edge wiring of existing commands; single-solution F# layout.

**Performance Goals**: Not a hot path. `decide` is a single linear pass over the candidate gates; the budget is
a per-gate tier comparison. The only expense is the *real* command runs the budget exists to bound — those run
through the existing F051 port at their declared tier only.

**Constraints**: Deterministic, **byte-identical** decisions, findings, and snapshots for identical inputs
(no wall-clock / abs-path / username / environment / input-order dependence; stable ordering and path
normalization — FR-011, SC-006, SC-008). The pure cores carry **zero** filesystem/process/registry dependency;
all I/O is at the host edge through existing ports (FR-014). Input-vs-tool-defect diagnostics preserved: a
missing/unreadable evidence store, an absent provenance input, no prior evidence each produce a clear input
signal (a `NoPriorEvidence`-style cause or a named diagnostic), never a fabricated reuse or fabricated pass
(FR-012, Constitution VI). Standalone with no monorepo dependency (FR-015). Freshness key, reuse verdict,
command-record/provenance identity, and the enforcement truth table all untouched (FR-006, FR-013, D4/D5).

**Scale/Scope**: 4 new `src` libraries (`CostBudget`, `CommandKind`, `CostBudgetJson`, `ProvenanceJson`) +
4 new test projects; 1 extended host pipeline (`VerifyCommand`, with the parallel `ShipCommand` edge) writing
2 new sidecars; 4 new committed surface baselines; new fixtures per core; **no** schema change to existing
projections, **no** enforcement-truth-table change, **no** new dependency. P1 = budget + budgeted cache
decision (`CostBudget`); P2 = cost/cache findings + command-run kinds + provenance snapshot
(`CommandKind` + the two projections); P3 = agent-review cache identity carried, never blocking.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Every new public module (`CostBudget.Model`/
  `Budget`/`Findings`, `CommandKind.Model`/`Audit`, `CostBudgetJson`, `ProvenanceJson`) is drafted as `.fsi`
  and exercised through the packed/loaded public surface before any `.fs` body exists (the F041/F052/F053–F057
  precedent). Semantic tests call `budgetFor`, `decide`, `cacheFindings`, `auditSnapshot`, and `ofReport` —
  never internals.
- **II. Visibility Lives in `.fsi`** — PASS. Every new public module ships a curated `.fsi`; `.fs` bodies carry
  no access modifiers. Four new committed surface baselines (`CostBudget`, `CommandKind`, `CostBudgetJson`,
  `ProvenanceJson`); existing projection baselines stay byte-identical (the sidecars are new modules, not new
  overloads on existing ones).
- **III. Idiomatic Simplicity** — PASS. Closed DUs (`CostBudget` ceiling, `CacheDecision`, `CommandKind`,
  cost-finding kinds), plain records, pipelines, exhaustive matches; no SRTP/reflection/type-providers/custom
  CEs/non-trivial active patterns; no new dependency. The budget is the existing **ordered** `Cost` DU — no
  arbitrary numeric weights (research D1). Any local mutation in a JSON writer follows the disclosed
  `AuditJson`/`CacheEligibilityJson` precedent with a one-line reason.
- **IV. Elmish/MVU Is the Boundary** — PASS. The budget/cache/findings/snapshot decisions are pure, total
  leaves — no MVU ceremony (the F041/F046 precedent). The behavioral change (filtering `ExecuteGates` to the
  `Recompute` gates, recording kinded runs, building the snapshot) happens inside the **existing**
  `VerifyCommand`/`ShipCommand` MVU boundary: the pure `decide`/`auditSnapshot` are called in `update`; the
  command runs and sidecar writes are `Effect`s executed only at the `Interpreter` edge through the existing
  `ExecutionPort`/`ArtifactWriter` ports. No new I/O seam is introduced in a pure core.
- **V. Test Evidence Is Mandatory** — PASS. Tests fail-before/pass-after against real cores
  (`CacheEligibility`/`Enforcement`/`Provenance`/`AgentReviewKey` never mocked) and a real `ExecutionPort` for
  the command-run kind fixtures (real `dotnet` invocations, as F052/F24 did). The synthetic-taint signal is a
  *supplied sensed input* exercised with explicit `Synthetic`-marked taint fixtures; any synthetic test
  evidence is disclosed at the use site, carries `Synthetic` in the test name, and is listed in the PR.
- **VI. Observability and Safe Failure** — PASS. The cache decision distinguishes a missing/malformed **input**
  (`NoPriorEvidence`, an unreadable store surfaced by the existing `StoreReader` `Error`, an absent provenance
  input) from a **tool defect**, naming the offending source (FR-012); no swallowed errors; no fabricated reuse
  and no fabricated pass. A skipped/deferred gate is reported as such, never as a pass (FR-003).

**Change Classification: Tier 1 (contracted change)** — adds new public API surface (four new libraries) and
new observable host output (two sidecar JSON artifacts). Requires the full chain: spec, plan, `.fsi` for every
new module, four new surface-area baselines, test evidence, and documentation of the two new JSON contracts. It
adds **no** dependency, **no** change to any existing projection schema, and **no** enforcement-truth-table
change, so the migration surface is limited to the two **new** sidecar artifacts (documented in
`contracts/cost-budget-json.md` and `contracts/provenance-json.md`).

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/060-cost-cache-command-provenance/
├── plan.md                                # This file (/speckit-plan output)
├── research.md                            # Phase 0 — D1..D6 decisions
├── data-model.md                          # Phase 1 — budget, cache decision, kinds, snapshot, findings
├── quickstart.md                          # Phase 1 — per-story validation scenarios
├── contracts/                             # Phase 1
│   ├── cost-budget-decision.md            #   CostBudget + budgetFor + CacheDecision + decide (pure)
│   ├── cost-cache-findings.md             #   stale / synthetic-taint / cache-invalidated findings (pure)
│   ├── command-kind-provenance.md         #   CommandKind taxonomy + KindedCommandRun + provenance audit snapshot
│   ├── cost-budget-json.md                #   fsgg.cost-budget/v1 sidecar (byte-identical, order-independent)
│   └── provenance-json.md                 #   fsgg.provenance/v1 sidecar (byte-identical to provenance identity)
├── checklists/
│   └── requirements.md                    # (already present — spec quality checklist)
└── tasks.md                               # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.CostBudget/                  # NEW (P1/P2/P3) — pure: budget + budgeted cache decision + findings
│   ├── Model.fsi / Model.fs                      #   CostBudget(ceiling), CacheDecision, CacheDecisionEntry,
│   │                                             #     CacheDecisionReport, BudgetReason, CandidateCost,
│   │                                             #     EvidenceTaint, AgentReviewMark, cost-finding kinds
│   ├── Budget.fsi / Budget.fs                     #   budgetFor : Profile -> RunMode -> CostBudget;
│   │                                             #     decide : CostBudget -> RunMode -> CandidateCost list -> CacheDecisionReport
│   ├── Findings.fsi / Findings.fs                 #   cacheFindings : CacheDecisionReport -> taint -> CostFinding list
│   └── FS.GG.Governance.CostBudget.fsproj         #   refs: Config, Enforcement, CacheEligibility,
│                                                  #     EvidenceReuse, FreshnessKey, AgentReviewKey
├── FS.GG.Governance.CommandKind/                 # NEW (P2) — pure: kind taxonomy + provenance audit snapshot
│   ├── Model.fsi / Model.fs                      #   CommandKind, KindedCommandRun, AuditSnapshot
│   ├── Audit.fsi / Audit.fs                       #   auditSnapshot : provenance inputs -> KindedCommandRun list -> AuditSnapshot
│   └── FS.GG.Governance.CommandKind.fsproj        #   refs: CommandRecord, Provenance
├── FS.GG.Governance.CostBudgetJson/              # NEW (P1) — projection
│   ├── CostBudgetJson.fsi / CostBudgetJson.fs    #   ofReport : CacheDecisionReport -> CostFinding list -> string
│   └── FS.GG.Governance.CostBudgetJson.fsproj     #   refs: CostBudget, CacheEligibility, FreshnessKey, Enforcement
├── FS.GG.Governance.ProvenanceJson/              # NEW (P2) — projection
│   ├── ProvenanceJson.fsi / ProvenanceJson.fs    #   ofSnapshot : AuditSnapshot -> string  (fsgg.provenance/v1)
│   └── FS.GG.Governance.ProvenanceJson.fsproj     #   refs: CommandKind, Provenance, CommandRecord
├── FS.GG.Governance.VerifyCommand/               # EXTEND — budget filters ExecuteGates; record kinds; write sidecars
│   ├── Loop.fs                                    #   call CostBudget.decide in update; build only Recompute ExecuteGates;
│   │                                             #     thread CacheDecisionReport + KindedCommandRuns + snapshot
│   └── Interpreter.fs                             #   tag each ExecuteGates run with its CommandKind; write the two sidecars
└── FS.GG.Governance.ShipCommand/                 # EXTEND (parallel edge) — same budget step at RunMode.Gate

tests/
├── FS.GG.Governance.CostBudget.Tests/            # NEW — 4×6 budget matrix, cache hit/miss matrix, skip/defer,
│                                                  #   agent-review never-blocks, findings, determinism/reorder
├── FS.GG.Governance.CommandKind.Tests/           # NEW — every kind recorded, duration-invariant identity,
│   └── fixtures/                                  #   snapshot byte-identity + no-op-input-change stability (real port)
├── FS.GG.Governance.CostBudgetJson.Tests/        # NEW — schemaVersion/field order, byte-for-byte, order-independence
├── FS.GG.Governance.ProvenanceJson.Tests/        # NEW — snapshot projection, identity stability, determinism
├── FS.GG.Governance.VerifyCommand.Tests/         # EXTEND — real-fs end-to-end: deferred gate not executed, sidecars emitted
└── FS.GG.Governance.ShipCommand.Tests/           # EXTEND — same at RunMode.Gate

surface/
├── FS.GG.Governance.CostBudget.surface.txt        # NEW
├── FS.GG.Governance.CommandKind.surface.txt       # NEW
├── FS.GG.Governance.CostBudgetJson.surface.txt    # NEW
└── FS.GG.Governance.ProvenanceJson.surface.txt    # NEW

FS.GG.Governance.sln                               # EDIT — add 4 src + 4 test projects
```

**Structure Decision**: Compose, don't fork. F25 **consumes** the F029/F030/F041 freshness+reuse verdict, the
F032/F033 command-run+provenance identity, the F036 agent-review identity, and the F018/F023 enforcement truth
table — adding the cost dimension *around* them, never inside them. The budget and cache decisions are one pure
leaf (`CostBudget`); the kind taxonomy and provenance roll-up are a second pure leaf (`CommandKind`); each is
projected by a dedicated deterministic-JSON sidecar following the F042/F045 precedent. The behavioral change —
the budget bounding which gates the existing `fsgg verify`/`fsgg ship` host actually executes — is wired at the
already-established F052 `ExecuteGates` edge, additively, with every existing golden left byte-identical.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
