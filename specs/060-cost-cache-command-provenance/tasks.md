# Tasks: Cost, Cache, Command, and Provenance — Budgeted Evidence Reuse (F25)

**Input**: Design documents from `/specs/060-cost-cache-command-provenance/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md,
contracts/cost-budget-decision.md, contracts/cost-cache-findings.md, contracts/command-kind-provenance.md,
contracts/cost-budget-json.md, contracts/provenance-json.md

**Tier**: **Tier 1 (contracted change)** — full chain owed: `.fsi` for every new module, four new surface
baselines, real test evidence, and two documented new JSON contracts (`fsgg.cost-budget/v1`,
`fsgg.provenance/v1`). New public projects: two pure leaf cores (`CostBudget`, `CommandKind`) and two
deterministic projections (`CostBudgetJson`, `ProvenanceJson`); two extended host projects (`VerifyCommand`,
`ShipCommand`) gain an additive edge step. **No** new dependency, **no** new freshness dimension, **no** new
reuse verdict, **no** change to `FreshnessKey`/`EvidenceReuse`/`CacheEligibility`/`CommandRecord`/`Provenance`
identity, **no** enforcement-truth-table change (FR-006, FR-013, D4/D5). The only observable host change is two
**new** sidecar artifacts; every existing `route.json`/`verify.json`/`audit.json` golden stays byte-identical.
Tests are in scope (Constitution V; plan lists every `.Tests` project).

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]`
may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`/`US4`/`US5`; setup/foundational/integration/polish tasks carry no story tag
- Discipline (Constitution I/II): for every **new** module draft its `.fsi` and a compiling stub before the
  real `.fs` body; semantic tests call the loaded public surface (`Budget.budgetFor`/`fits`/`decide`,
  `Findings.cacheFindings`/`enforce`, `Audit.auditSnapshot`/`runIdentity`, `CostBudgetJson.ofReport`,
  `ProvenanceJson.ofSnapshot`), never internals (Constitution I).

**Design note — compose, don't fork (plan §Structure Decision, D4/D5).** Every freshness/reuse/identity/
enforcement decision already exists and is consumed **verbatim**: F029 `FreshnessKey` (`InputCategory`,
`categoryToken`), F030 `EvidenceReuse` (`EvidenceRef`, `RecomputeCause`), F041 `CacheEligibility`
(`CacheEligibilityVerdict`), F032 `CommandRecord` (`canonicalId`, `identityValue`, `SensedDuration`), F033
`Provenance` (`build`, `canonicalId`), F036 `AgentReviewKey` (`CacheKey`, `matches`), F018/F023 `Enforcement`
(`deriveEffectiveSeverity`). This row supplies only the **cost dimension**: the `CostBudget`, the single
budgeted `CacheDecision`, the cost/cache findings, the command-run *kind* taxonomy + provenance roll-up, and the
two sidecar projections. `Cost` stays excluded from the freshness key. No reused type gains or loses a field.

**Design note — pure cores + edge-only I/O (Constitution IV, FR-014).** `Budget.decide`, `Findings.cacheFindings`,
`Audit.auditSnapshot`, and both `*Json.of…` projections are **pure, total** functions over already-sensed inputs
(the F029/F041/F051 leaf-plus-sensor precedent) — zero filesystem/process/registry dependency. The only I/O —
recording kinded command runs through the existing F051 `GateExecution.ExecutionPort`, reading evidence through
the existing F046 `StoreReader`, and writing the two sidecars through the existing `ArtifactWriter` — lives in
the **existing** `VerifyCommand`/`ShipCommand` MVU boundary: `decide`/`auditSnapshot` are called in `update`;
the command runs and sidecar writes are `Effect`s executed only at the `Interpreter` edge.

**Design note — what is foundational vs. story-owned.** The four `Model.fsi` type vocabularies
(`CostBudget.Model`, `CostBudget.Findings` types, `CommandKind.Model`) plus a compiling stub for every entry
point (`Budget`, `Findings`, `Audit`, both projections), the surface-drift harnesses, and the shared test
support are **foundational** — the whole project graph must compile before any story body lands. Each story then
replaces its stubbed body with the real one, adds its fixtures, and its tests: **budget enforcement** in US1,
**budgeted cache folding + `cost-budget.json`** in US2, **cost/cache findings** in US3, **command-run kinds +
provenance snapshot + `provenance.json`** in US4, the **agent-review-never-blocks** guarantee in US5. The host
edge wiring (budget filters `ExecuteGates`, kinded runs recorded, two sidecars written) lands in the Integration
phase once the cores exist.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the four new `src` projects, the four test projects, and wire the solution. Mirror the
`CacheEligibility` (Model + pure pack in one project) pure-leaf precedent exactly.

- [X] T001 [P] Create `src/FS.GG.Governance.CostBudget/FS.GG.Governance.CostBudget.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.Config`, `FS.GG.Governance.Enforcement`,
  `FS.GG.Governance.Gates`, `FS.GG.Governance.EvidenceReuse`, `FS.GG.Governance.CacheEligibility`,
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.AgentReviewKey`) with compile order `Model.fsi` →
  `Model.fs` → `Budget.fsi` → `Budget.fs` → `Findings.fsi` → `Findings.fs`. Mirror
  `FS.GG.Governance.CacheEligibility.fsproj`.
- [X] T002 [P] Create `src/FS.GG.Governance.CommandKind/FS.GG.Governance.CommandKind.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.CommandRecord`,
  `FS.GG.Governance.Provenance`) with compile order `Model.fsi` → `Model.fs` → `Audit.fsi` → `Audit.fs`.
- [X] T003 [P] Create `src/FS.GG.Governance.CostBudgetJson/FS.GG.Governance.CostBudgetJson.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.CostBudget`,
  `FS.GG.Governance.CacheEligibility`, `FS.GG.Governance.EvidenceReuse`, `FS.GG.Governance.FreshnessKey`,
  `FS.GG.Governance.Enforcement`) with compile order `CostBudgetJson.fsi` → `CostBudgetJson.fs`. Depends on T001
  existing (project reference). Mirror an existing `*Json` projection `.fsproj`.
- [X] T004 [P] Create `src/FS.GG.Governance.ProvenanceJson/FS.GG.Governance.ProvenanceJson.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.CommandKind`,
  `FS.GG.Governance.Provenance`, `FS.GG.Governance.CommandRecord`) with compile order `ProvenanceJson.fsi` →
  `ProvenanceJson.fs`. Depends on T002 existing.
- [X] T005 [P] Create the four test projects (Expecto + Expecto.FsCheck/FsCheck, each with a `Main.fs` Expecto
  entry — mirror `tests/FS.GG.Governance.CacheEligibility.Tests`): `tests/FS.GG.Governance.CostBudget.Tests`
  (refs `CostBudget`, `Config`, `Enforcement`, `Gates`, `EvidenceReuse`, `CacheEligibility`, `FreshnessKey`,
  `AgentReviewKey`); `tests/FS.GG.Governance.CommandKind.Tests` (refs `CommandKind`, `CommandRecord`,
  `Provenance`, `GateExecution` — the real `ExecutionPort` for the kind fixtures); `tests/FS.GG.Governance.
  CostBudgetJson.Tests` (refs `CostBudgetJson` + `CostBudget` + the F029/F030/F041 cores); `tests/FS.GG.
  Governance.ProvenanceJson.Tests` (refs `ProvenanceJson` + `CommandKind` + `Provenance` + `CommandRecord`).
- [X] T006 Add the four `src` + four `tests` projects to `FS.GG.Governance.sln` (mirror the `CacheEligibility`
  solution-folder entries); confirm `dotnet build FS.GG.Governance.sln` resolves the new graph with empty/stub
  modules and **no reference cycle** (the cores reference only existing leaves; the projections reference their
  core; the two hosts are untouched in this phase). Depends on T001–T005.

**Checkpoint**: Solution restores and builds with empty/stub modules; the reference directions
(`CostBudget`/`CommandKind` → existing leaves; `*Json` → their core) are acyclic.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land every new **type vocabulary** (real) plus a compiling stub for each entry point, the
surface-drift harnesses, and the shared test support. **No story body may begin until the whole graph compiles
and the contracts are exercisable.**

**⚠️ CRITICAL**: Blocks US1–US5.

- [X] T007 [P] Author `src/FS.GG.Governance.CostBudget/Model.fsi` **and** `Model.fs` together (real, compile as a
  pair): `CostBudget = { Ceiling: Cost }`; `AgentReviewMark = Deterministic | AgentReviewed of CacheKey`;
  `CandidateCost = { Gate: GateId; Cost: Cost; Verdict: CacheEligibilityVerdict; Review: AgentReviewMark }`;
  `DeferralClass = Skipped | Deferred`; `BudgetReason = { Gate: GateId; Cost: Cost; Ceiling: Cost; Class:
  DeferralClass }`; `CacheDecision = Reuse of EvidenceRef | Recompute of RecomputeCause | OverBudget of
  BudgetReason`; `CacheDecisionEntry = { Gate: GateId; Cost: Cost; Review: AgentReviewMark; Decision:
  CacheDecision }`; `CacheDecisionReport = CacheDecisionReport of CacheDecisionEntry list`. No access modifiers
  in `.fs` (Constitution II). Field order = the contract's declaration order (data-model §New vocabulary;
  contracts/cost-budget-decision.md `Model.fsi`).
- [X] T008 [P] Author `src/FS.GG.Governance.CostBudget/Findings.fsi` (in module `Findings`, same namespace) +
  the type vocabulary in `Findings.fs` together: `CostFindingKind = Stale of InputCategory list | SyntheticTaint
  | NoEvidence`; `EvidenceTaint = Real | Synthetic`; `CostFinding = { Gate: GateId; Kind: CostFindingKind;
  BaseSeverity: Severity; Message: string }`; and **stubs** for `cacheFindings` (returns `[]`), `kindToken`
  (total token table), `enforce` (calls `deriveEffectiveSeverity` with `BaseSeverity` + fixed maturity). Depends
  on T007 (uses `CacheDecisionReport`). (contracts/cost-cache-findings.md `Findings.fsi`.)
- [X] T009 [P] Author `src/FS.GG.Governance.CommandKind/Model.fsi` + real `Model.fs`: `CommandKind = Build | Test
  | Pack | TemplateInstantiation | GitDiff | PackageInspection | VisualCapture`; `KindedCommandRun = { Kind:
  CommandKind; Record: CommandRecord }`; `AuditSnapshot = { Provenance: Provenance; Runs: KindedCommandRun list }`
  (data-model §`CommandKind`; contracts/command-kind-provenance.md `Model.fsi`).
- [X] T010 Author `src/FS.GG.Governance.CostBudget/Budget.fsi` + a compiling stub `Budget.fs`: signatures
  `budgetFor`, `fits`, `decide`, `recomputeGates`, `reuseGates`, `overBudget`, `entries` per
  contracts/cost-budget-decision.md `Budget.fsi`. Stub `budgetFor` returns `{ Ceiling = Cheap }`, `decide`
  returns `CacheDecisionReport []`, the accessors return `[]`/`false`. Depends on T007.
- [X] T011 Author `src/FS.GG.Governance.CommandKind/Audit.fsi` + a compiling stub `Audit.fs`: signatures
  `kindToken`, `runIdentity`, `auditSnapshot`, `snapshotIdentity` per contracts/command-kind-provenance.md
  `Audit.fsi`. Stub `auditSnapshot` builds a `Provenance.build` over the supplied inputs (so the snapshot type is
  inhabited), `kindToken` is the real total table, `runIdentity`/`snapshotIdentity` return `""`. Depends on T009.
- [X] T012 [P] Author `src/FS.GG.Governance.CostBudgetJson/CostBudgetJson.fsi` + a compiling stub
  `CostBudgetJson.fs`: `schemaVersion = "fsgg.cost-budget/v1"`; `ofReport: CacheDecisionReport -> CostFinding
  list -> string` returns `"{}"` (stub). Depends on T007/T008.
- [X] T013 [P] Author `src/FS.GG.Governance.ProvenanceJson/ProvenanceJson.fsi` + a compiling stub
  `ProvenanceJson.fs`: `schemaVersion = "fsgg.provenance/v1"`; `ofSnapshot: AuditSnapshot -> string` returns
  `"{}"` (stub). Depends on T009.
- [X] T014 Exercise every `.fsi` against its `.fs` and prove the public surface composes before the real bodies
  (Constitution I): `dotnet build FS.GG.Governance.sln` checks each pair, and a smoke semantic test in
  `tests/FS.GG.Governance.CostBudget.Tests/SmokeTests.fs` loads and calls `Budget.decide` (stub),
  `Findings.cacheFindings` (stub), `Findings.enforce`, `Audit.auditSnapshot`/`kindToken` (stub),
  `CostBudgetJson.ofReport` (stub), and `ProvenanceJson.ofSnapshot` (stub). Depends on T007–T013.
- [X] T015 [P] Add a `SurfaceDriftTests.fs` to each of the four test projects — load the project's public
  surface, compare to `surface/FS.GG.Governance.<Project>.surface.txt`, honor `BLESS_SURFACE=1` (mirror the
  existing surface-drift test). Covers `CostBudget`, `CommandKind`, `CostBudgetJson`, `ProvenanceJson`. Baselines
  committed in Phase 8 once `.fs` bodies stabilize. Depends on T014.
- [X] T016 [P] Add `tests/FS.GG.Governance.CostBudget.Tests/Support.fs` — small in-memory builders for
  `CandidateCost` (a gate id + cost tier + a chosen `CacheEligibilityVerdict` built from **real**
  `CacheEligibility`/`EvidenceReuse` values, never mocked) and `CacheDecisionReport`, plus the `(GateId ->
  EvidenceTaint)` taint lookups. Reuse real `Config.Cost`/`Enforcement.Profile`/`RunMode` values. Depends on
  T014.

**Checkpoint**: Every new type is real and compiles; `Budget`/`Findings`/`Audit`/both projections stubs compile;
the smoke test exercises the public surface; the surface-drift harnesses and shared test support are in place —
story work can begin.

---

## Phase 3: User Story 1 — Cost budget bounds expensive work per profile and mode (Priority: P1) 🎯 MVP

**Goal**: `Budget.budgetFor profile mode` derives the run's cost ceiling as `min` of the two monotone D1
projections; `Budget.decide budget mode candidates` folds each candidate's F041 verdict with the budget into one
`CacheDecision` — a `MustRecompute` gate that fits the ceiling is `Recompute`, one that exceeds it is
`OverBudget` with a `DeferralClass` chosen by the run-mode class (boundary → `Deferred`, inner-loop → `Skipped`),
each carrying a named reason (gate, cost tier, exceeded ceiling). The report is sorted by `GateId` ordinal so it
is byte-identical regardless of candidate order. A skipped/deferred gate is never a pass.

**Independent Test**: Candidate gates spanning all four `Cost` tiers, each `MustRecompute`: `decide (budgetFor
Light Inner) Inner …` (ceiling `Cheap`) runs only the `Cheap` gate and `OverBudget { Class = Skipped }`s the
rest; `decide (budgetFor Release Release) Release …` (ceiling `Exhaustive`) runs every gate; `decide (budgetFor
Strict Verify) Verify …` (ceiling `High`) `OverBudget { Class = Deferred }`s the `Exhaustive` gate; shuffling the
candidates yields a byte-identical report (SC-001, SC-003, SC-008).

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T017 [P] [US1] `tests/FS.GG.Governance.CostBudget.Tests/BudgetForTests.fs` — assert `budgetFor` over the
  **full 4×6 (`Profile` × `RunMode`) grid** returns `{ Ceiling = min (profileCeiling p) (modeCeiling m) }`:
  `Light`/inner modes floor to `Cheap`; `Release`/`Release` admits `Exhaustive`; both levers are monotone (a
  stricter profile or a more protective mode never lowers the ceiling below a laxer one) and `min` makes either
  lever restrict. Assert `fits` is the inclusive `cost <= ceiling` over the ordered `Cost` DU (edge "budget
  exactly met") (FR-001, SC-001; contracts/cost-budget-decision.md behaviour table).
- [X] T018 [P] [US1] `tests/FS.GG.Governance.CostBudget.Tests/DecideBudgetMatrixTests.fs` — drive `decide` with
  all-`MustRecompute` candidates spanning the four tiers across the grid: a gate with `cost <= ceiling` ⇒
  `Recompute`; `cost > ceiling` in a **boundary** mode (`Verify`/`Gate`/`Release`) ⇒ `OverBudget { Class =
  Deferred }`; in an **inner-loop** mode (`Sandbox`/`Inner`/`Focused`) ⇒ `OverBudget { Class = Skipped }`; each
  `BudgetReason` names the gate, its cost, and the exceeded ceiling. Assert a `cost == ceiling` gate is
  `Recompute` (inclusive boundary). (FR-002, FR-003, SC-001, SC-003; data-model §`decide`; research D2.)
- [X] T019 [P] [US1] `tests/FS.GG.Governance.CostBudget.Tests/SkipDeferReportTests.fs` — assert an over-budget
  gate is reported `OverBudget` (distinguishably `Skipped` vs `Deferred`), **never** absent and **never** a
  `Reuse`/`Recompute`; `overBudget report` returns each over-budget gate paired with its `BudgetReason`;
  `recomputeGates`/`reuseGates` partition the rest; the edge "budget zero/disabled" (`Cheap` ceiling) ⇒ every
  `Medium+` `MustRecompute` is `OverBudget` while cheap recompute and all reuse proceed (FR-003, edge cases).
- [X] T020 [P] [US1] `tests/FS.GG.Governance.CostBudget.Tests/DeterminismTests.fs` — repeated `decide` over
  identical inputs ⇒ byte-identical report; FsCheck: a permutation of `candidates` yields a report whose entries
  are identical and in identical (`GateId`-ordinal) order (SC-008); assert no abs-path/clock/username appears in
  any `BudgetReason` or entry.

### Implementation for User Story 1

- [X] T021 [US1] `src/FS.GG.Governance.CostBudget/Budget.fs` — replace the stubs with the real bodies:
  `profileCeiling`/`modeCeiling` (unexported, off-surface) as the D1 monotone projection tables; `budgetFor =
  { Ceiling = min (profileCeiling profile) (modeCeiling mode) }`; `fits budget cost = cost <= budget.Ceiling`
  (ordered `Cost`); `decide budget mode candidates` folds each candidate (`Reusable ref` → `Reuse ref`;
  `MustRecompute cause` with `fits` → `Recompute cause`; `MustRecompute _` with not-`fits` → `OverBudget` whose
  `Class` is `Deferred` for `Verify`/`Gate`/`Release`, `Skipped` for `Sandbox`/`Inner`/`Focused`), then sorts
  entries by `GateId` ordinal; `recomputeGates`/`reuseGates`/`overBudget`/`entries` derive from the report. Pure,
  total, exhaustive matches (no wildcard). Makes T017–T020 pass. Depends on T010.

**Checkpoint**: MVP — a profile/mode budget deterministically bounds which `MustRecompute` gates run and which
are skipped/deferred with a named reason; a skipped/deferred gate is never a pass. The cache folding's reuse path,
findings, command kinds, and host wiring are not yet landed.

---

## Phase 4: User Story 2 — Expensive evidence reused only when its freshness key proves it applies (Priority: P1)

**Goal**: The same `Budget.decide` (US1) folds the **`Reusable`** branch: a candidate whose F041 verdict is
`Reusable ref` becomes `Reuse ref` and charges **nothing** (it is absent from `recomputeGates`); a candidate
differing by exactly one freshness dimension arrives as `MustRecompute (InputsChanged [cat])` and becomes
`Recompute` naming that dimension (charged); `NoPriorEvidence` becomes `Recompute` with that cause — never a
fabricated reuse; a cost-tier change alone (freshness unchanged) still arrives `Reusable` (cost is outside the
key). `CostBudgetJson.ofReport` projects the report's decision section to `fsgg.cost-budget/v1` deterministically.

**Independent Test**: A recorded-evidence cache hit/miss matrix folded by `decide`: `Reusable` ⇒ `Reuse`, not in
`recomputeGates`; each single-dimension change (rule hash, an artifact digest, command version, generator
version, base, head, environment class) ⇒ `Recompute` naming that `InputCategory`; `NoPriorEvidence` ⇒
`Recompute` `NoEvidence` cause; cost-tier-only change ⇒ still `Reuse` (SC-002). `cost-budget.json` round-trips
byte-identically and is order-independent (SC-008).

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T022 [P] [US2] `tests/FS.GG.Governance.CostBudget.Tests/CacheHitMissTests.fs` — build candidates from
  **real** `CacheEligibility`/`EvidenceReuse` verdicts: a `Reusable ref` candidate ⇒ `decide` gives `Reuse ref`
  and the gate is **not** in `recomputeGates report` (charges nothing); for **each** single freshness dimension
  changed ⇒ `MustRecompute (InputsChanged [thatCat])` ⇒ `Recompute` naming the category and the gate **is** in
  `recomputeGates` (charged); a `MustRecompute` gate whose cost exceeds the ceiling ⇒ `OverBudget` (US1
  integration), not silently reused and not silently recomputed; a `NoPriorEvidence` candidate ⇒ `Recompute`
  with the `NoPriorEvidence` cause — never a fabricated reuse (FR-004, FR-005, SC-002, SC-003; acceptance 2.1–2.4).
- [X] T023 [P] [US2] `tests/FS.GG.Governance.CostBudget.Tests/CostExcludedFromReuseTests.fs` — a candidate whose
  only change vs recorded evidence is its `Cost` tier (every freshness dimension unchanged) ⇒ verdict is still
  `Reusable` ⇒ decision `Reuse`; only the budget accounting reflects the new cost (cost deliberately excluded
  from the freshness key — FR-006, edge "cost does not affect reuse").
- [X] T024 [P] [US2] `tests/FS.GG.Governance.CostBudgetJson.Tests/OfReportTests.fs` — `ofReport report []` over
  a mixed report (a `Reuse`, a `Recompute (InputsChanged …)`, a `Recompute NoPriorEvidence`, an `OverBudget
  Skipped`, an `OverBudget Deferred`) emits `schemaVersion = "fsgg.cost-budget/v1"`, a `decisions` array in
  `GateId`-ordinal order, each entry `{ gate, cost, review, decision }` with the tagged-union `decision` shape
  (reuse→`evidence`; recompute→`cause` reusing the F042 `{ kind, categories? }` shape via `categoryToken`;
  overBudget→`{ class, ceiling, reason }`), and a `findings` array (here `[]`); fixed field order verified by
  raw-text `IndexOf`; `agentReviewed` review labelled but its `CacheKey` **not** emitted as a blocking signal
  (contracts/cost-budget-json.md document shape + rules).
- [X] T025 [P] [US2] `tests/FS.GG.Governance.CostBudgetJson.Tests/DeterminismTests.fs` — `ofReport` is
  byte-identical for identical input; reordering the candidates fed to `decide` cannot change the text (the
  report is already `GateId`-ordinal); `decisions` and `findings` are **always present** (empty arrays for an
  all-reusable, no-finding run — well-formed); no wall-clock/host-path/env/process-exit-code leaks (FR-011,
  SC-008; contracts/cost-budget-json.md rules).

### Implementation for User Story 2

> `Budget.decide` already covers the `Reusable` fold (landed in T021); US2 adds **no** `Budget.fs` edit — the
> cache-matrix assertions exercise the existing total function. The new production code here is the projection.

- [X] T026 [US2] `src/FS.GG.Governance.CostBudgetJson/CostBudgetJson.fs` — replace the stub `ofReport`: a
  hand-driven `Utf8JsonWriter` walk (the `CacheEligibilityJson`/`RouteJson` precedent) emitting `schemaVersion` <
  `decisions` < `findings`; within a decision `gate` < `cost` < `review` < `decision`; closed-enum token helpers
  (exhaustive, no wildcard) for `cost`, `review`, the decision `kind`, the deferral `class`, the finding `kind`,
  `baseSeverity`; the recompute `cause` object reuses the F042 `{ kind: noPriorEvidence | inputsChanged,
  categories? }` shape with categories via `FreshnessKey.categoryToken`; the `stale` finding's `categories` only;
  `EvidenceRef` rendered verbatim; `decisions`/`findings` always present. Pure/total/no-I/O. Makes T024/T025
  pass. Depends on T012/T021.

**Checkpoint**: US1 + US2 — evidence is reused only on a proven freshness match (cost excluded), every recompute
names its cause, and the budgeted decision projects to a deterministic, order-independent `cost-budget.json`.

---

## Phase 5: User Story 3 — Stale, synthetic-taint, and cache-invalidated findings (Priority: P2)

**Goal**: `Findings.cacheFindings report taint` derives, per gate: a `Stale cats` finding when the decision
(`Recompute` or `OverBudget`) derives from `InputsChanged cats` (naming each changed F029 dimension via
`categoryToken`); a `NoEvidence` finding from `NoPriorEvidence`; a **distinct** `SyntheticTaint` finding for any
gate whose supplied taint is `Synthetic` — **even when its decision is `Reuse`** (synthetic is never silently
reused as real); and **no** finding for a clean `Reuse` with `Real` taint. Findings are sorted `(GateId ordinal,
kind tag)`. `Findings.enforce` runs each through `deriveEffectiveSeverity` verbatim with `BaseSeverity = Advisory`
— it never escalates.

**Independent Test**: A gate from `InputsChanged [RuleHashCat]` ⇒ `Stale [RuleHashCat]` naming `"ruleHash"`; a
`Synthetic`-taint gate ⇒ a distinct `SyntheticTaint` finding even when its decision is `Reuse`; a clean `Reuse` +
`Real` ⇒ no finding; `enforce mode profile finding` ⇒ `EffectiveSeverity = Advisory` for every kind under every
(`Profile`, `RunMode`) (SC-004).

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T027 [P] [US3] `tests/FS.GG.Governance.CostBudget.Tests/CacheFindingsTests.fs` — drive `cacheFindings`
  over a report + a `(GateId -> EvidenceTaint)` lookup: a `Recompute (InputsChanged cats)` (and an `OverBudget`
  whose underlying cause carried changed inputs) ⇒ a `Stale cats` finding naming each dimension via
  `categoryToken`; a `Recompute NoPriorEvidence` ⇒ a `NoEvidence` finding; a `Synthetic`-taint gate ⇒ a distinct
  `SyntheticTaint` finding **even when the decision is `Reuse`**; a clean `Reuse` + `Real` ⇒ no finding; every
  finding carries `BaseSeverity = Advisory` and a `Message` naming the gate + cause with no raw path/clock/env
  (FR-007, SC-004; contracts/cost-cache-findings.md rules; acceptance 3.1–3.3).
- [X] T028 [P] [US3] `tests/FS.GG.Governance.CostBudget.Tests/FindingsEnforceTests.fs` — assert `enforce mode
  profile finding` calls real `Enforcement.deriveEffectiveSeverity` (never mocked) and returns
  `EffectiveSeverity = Advisory` for **every** `CostFindingKind` across the **full** (`Profile`, `RunMode`) grid
  — a base-Advisory finding never blocks (FR-010, FR-013, SC-007 family); `kindToken` is the exhaustive
  `stale | syntheticTaint | noEvidence` table.
- [X] T029 [P] [US3] `tests/FS.GG.Governance.CostBudget.Tests/FindingsDeterminismTests.fs` — repeated
  `cacheFindings` over identical input ⇒ byte-identical list; FsCheck: reordering the report's entries leaves the
  sorted-by-`(GateId ordinal, kind tag)` findings unchanged (FR-011, SC-004, SC-008).

### Implementation for User Story 3

- [X] T030 [US3] `src/FS.GG.Governance.CostBudget/Findings.fs` — replace the stub `cacheFindings`: per entry,
  emit `Stale cats` from an `InputsChanged cats` cause (reached via `Recompute` or the `OverBudget`'s underlying
  cause), `NoEvidence` from `NoPriorEvidence`, a distinct `SyntheticTaint` for any `Synthetic`-taint gate
  (including a `Reuse` gate), and nothing for a clean `Reuse`+`Real`; each finding `BaseSeverity = Advisory`, a
  deterministic `Message`; sort `(GateId ordinal, kind tag)`. Keep the real `kindToken` and `enforce` (the
  `deriveEffectiveSeverity` call with a fixed warn-equivalent maturity). Pure/total/no-I/O. Makes T027–T029 pass.
  Depends on T008/T021.

**Checkpoint**: US1–US3 — stale/cache-invalidated, synthetic-taint, and no-evidence states are surfaced as
deterministic advisory findings naming the gate and cause; a clean reuse is silent; the findings never block.

---

## Phase 6: User Story 4 — Command runs recorded across every kind, into a provenance audit snapshot (Priority: P2)

**Goal**: `CommandKind.Audit` wraps the F032 `CommandRecord` with one of the seven `CommandKind`s (descriptive,
not part of identity), exposes `runIdentity = CommandRecord.identityValue (canonicalId run.Record)` (duration
excluded), and rolls the runs + the F033 provenance inputs into an `AuditSnapshot` via `Provenance.build`;
`snapshotIdentity = Provenance.canonicalId snapshot.Provenance`. `ProvenanceJson.ofSnapshot` projects it to
`fsgg.provenance/v1` with duration as clearly-sensed metadata that never affects identity.

**Independent Test**: Record a run of **each** kind through a **real** `GateExecution.ExecutionPort` (real
`dotnet` invocations, as F052/F24); confirm each `runIdentity` is the F032 `canonicalId` and carries its kind;
two runs differing only in `SensedDuration` share a `runIdentity`; the `AuditSnapshot` is byte-identical for
identical inputs, stable under a no-op re-derive, changes when a reproducible input (a digest) changes, and is
unchanged by a duration-only change; a failed/timed-out run is recorded with its F051 sentinel exit code, not
dropped (SC-005, SC-006).

### Tests for User Story 4 ⚠️ (write first, must FAIL before impl)

- [X] T031 [P] [US4] Add committed fixtures under `tests/FS.GG.Governance.CommandKind.Tests/fixtures/` and a
  helper that runs a trivial real command of each kind through `GateExecution.senseExecution`/the real
  `ExecutionPort` (a `dotnet --version`-class build, a no-op test, a pack, a template instantiation, a git diff,
  a package inspection, a visual capture stand-in) plus a deliberately failing/timed-out command for the sentinel
  case — each wrapped as a `KindedCommandRun`.
- [X] T032 [P] [US4] `tests/FS.GG.Governance.CommandKind.Tests/RunIdentityTests.fs` — for a run of **each** of
  the seven kinds, assert `runIdentity run = CommandRecord.identityValue (CommandRecord.canonicalId run.Record)`
  and the run carries its `Kind`; `kindToken` is the exhaustive `build|test|pack|templateInstantiation|gitDiff|
  packageInspection|visualCapture` table; two `KindedCommandRun`s differing **only** in `SensedDuration` ⇒
  identical `runIdentity` (duration excluded — FR-008, SC-005; acceptance 4.1, 4.2).
- [X] T033 [P] [US4] `tests/FS.GG.Governance.CommandKind.Tests/AuditSnapshotTests.fs` — `auditSnapshot …` over
  the runs + provenance inputs ⇒ `snapshotIdentity` byte-identical for identical inputs; a no-op re-derive is
  stable; changing a reproducible input (a `ArtifactHash`, a `Revision`, the `RuleHash`, the `GeneratorVersion`,
  the `EnvironmentClass`, the `BuilderIdentity`, or a command run) changes it; changing **only** a duration does
  **not**; a failed/timed-out run is present in `Runs` with its F051 sentinel `ExitCode` (never dropped) (FR-009,
  SC-006; acceptance 4.3, edge "command run fails to start / times out").
- [X] T034 [P] [US4] `tests/FS.GG.Governance.ProvenanceJson.Tests/OfSnapshotTests.fs` — `ofSnapshot snapshot`
  emits `schemaVersion = "fsgg.provenance/v1"`; `identity = Provenance.canonicalId snapshot.Provenance` verbatim;
  fixed field order (`schemaVersion` < `identity` < `sourceCommit` < `base` < `head` < `ruleHash` <
  `generatorVersion` < `environment` < `builder` < `artifactDigests` < `commandRuns`) verified by raw-text
  `IndexOf`; `artifactDigests` rendered **sorted** (set semantics); `commandRuns` in carried order, each
  `{ kind, identity, exitCode, durationNanos }` with `kind` the exhaustive token and `identity =
  CommandRecord.canonicalId run.Record`; two snapshots differing only in durations share the top-level + per-run
  `identity`; an empty run list ⇒ `"commandRuns": []`; no clock/host-path/username leak (contracts/provenance-
  json.md document shape + rules).
- [X] T035 [P] [US4] `tests/FS.GG.Governance.ProvenanceJson.Tests/DeterminismTests.fs` — `ofSnapshot` is
  byte-identical for identical input; a duration-only change leaves the text's `identity` fields unchanged (only
  the sensed `durationNanos` differs); no wall-clock/abs-path/env beyond the opaque `EnvironmentClass`/
  `BuilderIdentity` tokens (FR-011, SC-006).

### Implementation for User Story 4

- [X] T036 [US4] `src/FS.GG.Governance.CommandKind/Audit.fs` — replace the stubs: `runIdentity` = exactly
  `CommandRecord.identityValue (CommandRecord.canonicalId run.Record)` (kind does not participate);
  `auditSnapshot` builds the F033 `Provenance` via `Provenance.build` from the supplied inputs and the runs'
  `.Record`s (order-significant, as F033) and carries `Runs`; `snapshotIdentity = Provenance.canonicalId
  snapshot.Provenance`; keep the real `kindToken`. Pure/total/no-I/O. Makes T032/T033 pass. Depends on T011.
- [X] T037 [US4] `src/FS.GG.Governance.ProvenanceJson/ProvenanceJson.fs` — replace the stub `ofSnapshot`: a
  hand-driven `Utf8JsonWriter` walk in the fixed field order; `identity`/per-run `identity` reused from F033/F032
  verbatim (no new fingerprint); `artifactDigests` rendered sorted (set); `commandRuns` in carried order with the
  exhaustive `kind` token and the sensed `durationNanos` clearly separate from identity; empty run list ⇒ `[]`;
  no clock/host-path/env leak. Pure/total/no-I/O. Makes T034/T035 pass. Depends on T013/T036.

**Checkpoint**: US1–US4 — every expensive command kind is recorded with a duration-invariant reproducible
identity and rolled into a byte-identical provenance audit snapshot projected to `provenance.json`.

---

## Phase 7: User Story 5 — Agent-review cache identity carried, never promoted to a blocker (Priority: P3)

**Goal**: An `AgentReviewed key` candidate's `CacheDecision` is computed identically to a `Deterministic` one —
the F036 `CacheKey` is carried so agent-reviewed evidence reuses on matching judge/prompt/check-artifact identity
and re-reviews when one changes — but the review mark affects **only** enforcement: across every (`Profile`,
`RunMode`) an agent-reviewed finding stays `Advisory` and never changes a blocking verdict. `AdvisoryPromotion`
(F039) is never invoked.

**Independent Test**: An `AgentReviewed key` candidate whose `AgentReviewKey.matches` holds ⇒ verdict `Reusable`
⇒ decision `Reuse`; the same candidate with one F036 identity changed ⇒ `MustRecompute` ⇒ `Recompute` naming the
change; across the full enforcement matrix an agent-reviewed check's finding derives `Advisory` every time, never
blocking, regardless of its cache decision (SC-007).

### Tests for User Story 5 ⚠️ (write first, must FAIL before impl)

- [X] T038 [P] [US5] `tests/FS.GG.Governance.CostBudget.Tests/AgentReviewCacheTests.fs` — build an `AgentReviewed
  key` candidate from **real** `AgentReviewKey.matches`/`compute` (never mocked): identities match ⇒ verdict
  `Reusable` ⇒ `decide` gives `Reuse` (reused on matching judge/prompt/check-artifact); one identity changed ⇒
  `MustRecompute` ⇒ `Recompute` naming the change; assert `decide`'s budget arithmetic for an `AgentReviewed`
  candidate is **identical** to the same `Deterministic` candidate (the mark never changes the decision) (FR-010,
  acceptance 5.1, 5.2).
- [X] T039 [P] [US5] `tests/FS.GG.Governance.CostBudget.Tests/AgentReviewNeverBlocksTests.fs` — across the
  **full** (`Profile`, `RunMode`) grid, an agent-reviewed gate's cost/cache finding (any `CacheDecision`) derives
  `EffectiveSeverity = Advisory` via real `deriveEffectiveSeverity` — it never blocks the verdict; assert
  `AdvisoryPromotion` is never referenced/invoked by `CostBudget` (the project carries no F039 reference) (FR-010,
  SC-007, acceptance 5.3).

> No production change in this phase: the `AgentReviewMark` fold (T021) and the advisory enforcement (T030)
> already deliver the guarantee; US5 asserts it across the full matrix.

**Checkpoint**: All five stories — agent-review cache identity is carried so re-reviews are avoided, yet an
agent-reviewed check never blocks under any mode/profile; judgement never leaks into a blocking verdict.

---

## Phase 8: Integration — host edge: budget filters `ExecuteGates`, kinded runs recorded, two sidecars written (FR-014, SC-006, D3)

**Purpose**: Wire the pure cores into the existing `fsgg verify` / `fsgg ship` hosts additively. The budget
consults `decide` to filter which `MustRecompute` gates `ExecuteGates` actually runs; each executed run is tagged
with its `CommandKind`; the budgeted decision + findings project to `cost-budget.json` and the kinded runs +
provenance inputs to `provenance.json` — every existing golden left byte-identical. Depends on all four cores
(US1–US4). MVU discipline: `decide`/`auditSnapshot` in `update`; command runs + sidecar writes are `Effect`s at
the `Interpreter` edge through the existing `ExecutionPort`/`StoreReader`/`ArtifactWriter` (Constitution IV).

> **STATUS — COMPLETE (landed by row `064-cost-cache-host-wiring`, 2026-06-25).** The host-edge slice was
> implemented as its own contracted row, `specs/064-cost-cache-host-wiring/`. Both `fsgg verify` and `fsgg ship`
> now build a `CandidateCost` per selected gate, run `Budget.decide (budgetFor profile mode)` in a pure
> demotion step inside `executionPlan` (an `OverBudget` gate becomes a new `GateClassification.Deferred` — never
> executed, never passed), record kinded runs via a total `kindOf` map, build the `AuditSnapshot`, and write the
> two deterministic sidecars (`cost-budget.json`, `provenance.json`) through the existing atomic `WriteArtifact`
> port. Every existing `verify.json`/`route.json`/`audit.json`/ship golden stays byte-identical (proven against
> the genuine core recomputation); both host surface baselines were re-blessed; the full solution is green. See
> `specs/064-cost-cache-host-wiring/tasks.md` for the per-task detail and honest deviations.

### Tests ⚠️ (write first, must FAIL before impl)

- [X] T040 [P] `tests/FS.GG.Governance.VerifyCommand.Tests/CostBudgetE2ETests.fs` — real-filesystem `fsgg verify`
  (standalone, no monorepo): a `MustRecompute` gate over the (Strict, Verify) budget is **not** executed (absent
  from the executed gate runs) and is recorded `Deferred` in `cost-budget.json`; in-budget `MustRecompute` gates
  run; `Reusable` gates reuse (charge nothing); `cost-budget.json` (`fsgg.cost-budget/v1`) and `provenance.json`
  (`fsgg.provenance/v1`) are written and byte-identical on a re-run with unchanged inputs; the existing
  `verify.json`/`route.json`/`audit.json` goldens are **byte-identical** to before (the sidecars are new
  artifacts) (FR-014, FR-015, SC-006; quickstart Scenario 6).
- [X] T041 [P] `tests/FS.GG.Governance.ShipCommand.Tests/CostBudgetE2ETests.fs` — the same budget step at the
  `fsgg ship` host (`RunMode.Gate`): the (profile, Gate) budget filters `ExecuteGates`, kinded runs are recorded,
  both sidecars are written and byte-identical on re-run, and every existing ship golden stays byte-identical.
- [X] T042 [P] `tests/FS.GG.Governance.VerifyCommand.Tests/StandaloneCostTests.fs` — a generated product checked
  out **standalone** (no monorepo): the budget/cache decision and the provenance snapshot use only the product's
  own recorded evidence, command runs, and provenance — no monorepo path; a missing/unreadable evidence store ⇒ a
  clear input diagnostic (`NoPriorEvidence`-style cause surfaced via the F046 `StoreReader` `Error`), never a
  fabricated reuse or fabricated pass (FR-012, FR-015).

### Implementation

- [X] T043 `src/FS.GG.Governance.VerifyCommand/Loop.fs` (+ `Loop.fsi` if its `Model`/`Msg`/`Effect` surface
  changes) — in `update` at `RunMode.Verify`: build `CandidateCost`s from the routed gates' cost tiers + the
  existing F041 verdicts (+ the F036 mark for agent-reviewed gates), call `Budget.decide (budgetFor profile
  Verify) Verify candidates`, and select **only** `recomputeGates` for the `ExecuteGates` effect; thread the
  `CacheDecisionReport`, the per-gate `EvidenceTaint`, the `KindedCommandRun`s, and the `AuditSnapshot` through
  the model to the persist step. Pure `update`; no I/O here. Depends on T021/T030/T036.
- [X] T044 `src/FS.GG.Governance.VerifyCommand/Interpreter.fs` — at the edge: tag each `ExecuteGates` run with
  the `CommandKind` known at the call site (via the existing `GateExecution.ExecutionPort`), build the
  `AuditSnapshot` via `Audit.auditSnapshot`, call `Findings.cacheFindings`, and write the two sidecars
  (`cost-budget.json` via `CostBudgetJson.ofReport`, `provenance.json` via `ProvenanceJson.ofSnapshot`) through
  the existing `ArtifactWriter`; fold the advisory cost/cache findings into the existing rollup via
  `Findings.enforce` (no truth-table change). Empty inputs ⇒ the sidecars are still well-formed (empty arrays);
  existing goldens untouched. Add the four core/projection project references to `VerifyCommand.fsproj`. Makes
  T040/T042 pass. Depends on T043/T026/T037.
- [X] T045 `src/FS.GG.Governance.ShipCommand/Loop.fs` + `Interpreter.fs` (+ `.fsi` as needed) — the parallel edge
  at `RunMode.Gate`: same `decide`/`ExecuteGates` filter, kinded-run recording, `cacheFindings` rollup, and the
  two sidecar writes through the existing ports; add the four project references to `ShipCommand.fsproj`. Makes
  T041 pass. Depends on T043/T044 (shared wiring shape).

**Checkpoint**: `fsgg verify` and `fsgg ship` bound expensive recompute by the (profile, mode) budget, record
kinded runs, and emit `cost-budget.json` + `provenance.json` deterministically — every existing golden
byte-identical, standalone preserved.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Bless the new surface baselines, document the two new JSON contracts, run the determinism/standalone/
reuse guards, update docs, and run the quickstart validation.

- [X] T046 Bless and commit the four new surface baselines (`BLESS_SURFACE=1 dotnet test …`), then re-run drift
  green: `surface/FS.GG.Governance.CostBudget.surface.txt`, `…CommandKind.surface.txt`,
  `…CostBudgetJson.surface.txt`, `…ProvenanceJson.surface.txt`; re-bless `…VerifyCommand.surface.txt` /
  `…ShipCommand.surface.txt` **only if** their `Model`/`Loop` surface changed (T043/T045).
- [X] T047 [P] *(DONE — landed via `064-cost-cache-host-wiring` (T031 quickstart + committed goldens/byte-identity anchors); the projection shapes are pinned byte-for-byte by the
  `CostBudgetJson`/`ProvenanceJson` tests; committed `golden/` files + the host byte-identity anchors landed with
  the host wiring.)* Commit the deterministic goldens: a `cost-budget.json` golden (a mixed report with each decision
  kind + each finding kind) and a `provenance.json` golden (runs of several kinds + a sentinel-exit run) under
  the projection test projects' `golden/` dirs; plus the pre-F25 `verify.json`/`route.json`/`audit.json`
  byte-identity anchors used by T040/T041's untouched-golden assertions (reuse the existing host goldens).
- [X] T048 [P] `tests/FS.GG.Governance.CostBudget.Tests/ReuseGuardTests.fs` — the no-new-vocabulary guard: assert
  this row adds **no** new `InputCategory`, **no** new `CacheEligibilityVerdict`/`RecomputeCause` case, **no**
  change to `CommandRecord`/`Provenance` identity, and **no** enforcement-truth-table constant — `decide` folds
  the F041 verdict verbatim (a future verdict case is a compile error via the exhaustive match) and the findings
  reuse `deriveEffectiveSeverity`; `Cost` is absent from the freshness key (FR-006, FR-013, D4/D5).
- [X] T049 [P] `tests/FS.GG.Governance.CommandKind.Tests/IdentityReuseGuardTests.fs` — assert `runIdentity`/
  `snapshotIdentity` compute **no** new fingerprint (they equal `CommandRecord.canonicalId`/`Provenance.canonicalId`
  verbatim) and the `CommandKind` never participates in either identity (a kind-only change leaves both equal) —
  the descriptive-metadata guarantee (research D5, FR-008, FR-009).
- [X] T050 [P] Update `CLAUDE.md` and the roadmap row: F25 `025-cost-cache-command-provenance` complete — the
  `CostBudget` ordered-ceiling budget + single budgeted `CacheDecision` (reuse/recompute/skip-defer by run-mode
  class), the stale/synthetic-taint/no-evidence advisory findings, the `CommandKind` seven-kind taxonomy + F033
  provenance audit snapshot, and the two new deterministic sidecars (`fsgg.cost-budget/v1`, `fsgg.provenance/v1`)
  wired additively through `fsgg verify`/`fsgg ship`; note the F029/F030/F041/F032/F033/F036/F018-F023 reuse
  (no new freshness dimension, reuse verdict, identity, or truth-table change) and that agent-reviewed checks stay
  advisory (F039 `AdvisoryPromotion` deferred).
- [X] T051 *(DONE — landed via `064-cost-cache-host-wiring` (T031 quickstart end-to-end + T032 full-solution gate green); the library-level matrices/determinism/surface checks pass, and the
  real-host `fsgg verify` smoke + the "every existing golden byte-identical" anchor landed with the host wiring.)*
  Run the `quickstart.md` validation end to end (all six scenarios + the constitution-gate checks):
  `dotnet build FS.GG.Governance.sln` clean (warnings-as-errors); the four new test projects + the two extended
  host test projects + the whole solution green (no regression); the four new surface baselines match; the 4×6
  budget matrix, the single-dimension cache hit/miss matrix, the every-kind command-run fixtures (real
  `ExecutionPort`), the snapshot byte-identity + stability fixture, the agent-review-never-blocks matrix, and the
  determinism/reorder tests all pass; a real-host `fsgg verify` smoke run shows the two sidecars written
  deterministically with every existing golden byte-identical. Record the evidence on this line.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no deps; T001–T005 parallel (T003 needs T001, T004 needs T002 to reference), T006 after.
- **Foundational (Phase 2)** → after Setup. T007 first (`CostBudget.Model`); T008/T009 after their model deps
  (T008 needs T007); T010 after T007; T011 after T009; T012 after T007/T008; T013 after T009; T014 after
  T007–T013; T015/T016 after T014. **Blocks all stories.**
- **US1 (Phase 3)** → after Foundational. MVP. (`Budget.fs` real body.)
- **US2 (Phase 4)** → after US1 (`decide` must be real for the cache matrix; the projection renders the report).
- **US3 (Phase 5)** → after US1 (`Findings.cacheFindings` reads the report).
- **US4 (Phase 6)** → after Foundational; **independent of US1–US3** (different libraries — `CommandKind` +
  `ProvenanceJson`).
- **US5 (Phase 7)** → after US1 + US3 (the fold + advisory enforcement deliver the guarantee; US5 asserts it).
- **Integration (Phase 8)** → after all four cores (US1–US4); the host runs `decide`, records kinded runs, and
  writes both sidecars.
- **Polish (Phase 9)** → after the desired stories + Integration; T046/T047/T051 need the `.fs` bodies + host
  wiring stable; T048/T049 (reuse guards) need the real `decide`/`Audit` (T021/T036).

### Within each story

- Tests first and FAILING, then implementation (Constitution V).
- For every new module, `.fsi` + compiling stub (Phase 2) before the real `.fs`; `Model` (Phase 2) before
  `Budget`/`Findings`/`Audit`; the cores before the projections before the host wiring.

### Parallel opportunities

- Phase 1: T001–T005 together (T006 after).
- Phase 2: T008/T009 after their models in parallel; T010/T011 in parallel; T012/T013 in parallel; T015/T016 in
  parallel after T014.
- **US4 (Phase 6) is fully independent of US1–US3** (`CommandKind`/`ProvenanceJson` share no file with
  `CostBudget`/`CostBudgetJson`) and can be staffed in parallel from the end of Foundational.
- Each story's `[P]` test tasks run together; Phase 8 tests T040/T041/T042 in parallel; Phase 9 T047/T048/T049/
  T050 are independent `[P]` tasks.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL — every type + stubs compile) → 3. Phase 3 US1 → **STOP &
   VALIDATE** (SC-001/SC-003: the 4×6 budget matrix bounds expensive recompute, a skipped/deferred gate is never
   a pass, the report is deterministic and order-independent). The budget bounds expensive work with no cache
   folding, findings, command kinds, or host wiring yet.

### Incremental delivery

Setup + Foundational → US1 (budget enforcement, MVP) → US2 (budgeted cache folding + `cost-budget.json`) → US3
(cost/cache findings) → US4 (command kinds + provenance snapshot + `provenance.json`, independent) → US5
(agent-review never blocks) → Integration (host edge wiring + two sidecars) → Polish. Each slice adds value
without breaking the prior; the two sidecars are new artifacts, so every existing golden stays byte-identical.

### Parallel team strategy

After Foundational, Developer A takes US1→US2→US3 (the `CostBudget` core + `CostBudgetJson`), Developer B takes
US4 (`CommandKind` + `ProvenanceJson`, independent). They converge on Integration (Phase 8) once both cores land;
US5 + Polish follow.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- **Reuse, don't reinvent** (D4/D5): `decide` folds the F041 verdict verbatim (no new freshness dimension or
  reuse verdict); findings reuse `FreshnessKey.categoryToken` + `deriveEffectiveSeverity` (no truth-table change);
  `runIdentity`/`snapshotIdentity` reuse `CommandRecord.canonicalId`/`Provenance.canonicalId` verbatim (the kind
  is descriptive, never in identity); the host edge reuses the existing `GateExecution.ExecutionPort` (command
  runs), `StoreReader` (evidence), and `ArtifactWriter` (sidecars). Upstream cores are never mocked in semantic
  tests (Constitution V).
- **No new vocabulary / no schema change to existing projections** (FR-006, FR-013): `Cost` stays excluded from
  the freshness key; no new `InputCategory`/`CacheEligibilityVerdict`/`RecomputeCause` case; no
  `CommandRecord`/`Provenance` identity change; no enforcement-truth-table constant; the only new wire contracts
  are the two **new** sidecars (`fsgg.cost-budget/v1`, `fsgg.provenance/v1`).
- **Determinism is mandatory** (FR-011, SC-006, SC-008): `decide` sorts by `GateId` ordinal; `cacheFindings`
  sorts `(GateId, kind)`; both projections emit a fixed field order; no clock/abs-path/username/environment in
  any decision, finding, snapshot, or JSON; duration is sensed metadata only and never affects identity. The
  per-core determinism tests (T020/T025/T029/T035) + the reorder tests enforce it.
- **Elmish/MVU applicability**: `Budget.decide`, `Findings.cacheFindings`, `Audit.auditSnapshot`, and both
  projections are **pure, total leaves** — no MVU ceremony (the F041/F046 precedent). The behavioral change
  (filtering `ExecuteGates` to the `Recompute` gates, recording kinded runs, writing the sidecars) is in the
  **existing** `VerifyCommand`/`ShipCommand` boundary: `decide`/`auditSnapshot` in `update`; the command runs and
  sidecar writes are `Effect`s at the `Interpreter` edge (T043/T044/T045). Pure transitions are exercised
  directly (T017–T020/T022/T023/T027–T029/T032/T033/T038/T039); real-port/real-fs evidence is T031/T040/T041/T042.
- **Safe failure** (FR-012, Constitution VI): a missing/unreadable evidence store, an absent provenance input, or
  no prior evidence each surfaces a clear input signal (a `NoPriorEvidence`-style cause or a named diagnostic),
  never a fabricated reuse or fabricated pass; a skipped/deferred gate is reported as such, never a pass (T042).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document on the
  task line.
