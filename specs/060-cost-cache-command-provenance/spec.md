# Feature Specification: Cost, Cache, Command, and Provenance — Budgeted Evidence Reuse

**Feature Branch**: `060-cost-cache-command-provenance`

**Created**: 2026-06-25

**Status**: Planned

**Input**: User description: "next item in plan" — roadmap **F25 ·
`025-cost-cache-command-provenance`** (`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`),
the next unimplemented row after **F24 (`059-package-docs-skills-design-checks`)** merged on 2026-06-25.
F24 gave the major generated-product surface domains concrete deterministic checks — some of them expensive
(FSI transcript compile-and-evaluate, design capture, package inspection). This row makes that expense
**governed**: expensive evidence is reused only when its freshness key proves it still applies, and the
expensive work a run may do is bounded by a **cost budget** scoped to the run's profile and mode, with clear,
auditable reasons for any gate that is skipped or deferred. Per the roadmap: "cost control is part of
correctness, not a CLI convenience."

Two scope decisions are confirmed for this feature (the requester advanced from F24 to the next row, F25,
after F24 merged — confirming the cost-governance scope over further surface-check expansion):

1. **This feature governs *when expensive work runs and when its evidence may be reused*; it does not invent a
   new cache identity or re-open the surface checks.** The freshness-key vocabulary that decides whether prior
   evidence applies — rule hash, artifact digests, command version, generator version, base/head, environment
   class — already exists (F029) and is reused **unchanged**; the per-gate reuse verdict (`Reusable` vs
   `MustRecompute`, each naming its cause) already exists (F030/F041) and is reused **unchanged**. This row adds
   the **cost budget** that bounds how much expensive recompute a run may do, the **single cache decision** that
   folds the existing reuse verdict together with that budget into run / reuse / skip / defer, and the
   **findings** that make stale, synthetically-tainted, and cache-invalidated evidence visible. It does **not**
   add a new freshness dimension, a new reuse verdict, or a new enforcement truth table.

2. **The cost budget is a pure, deterministic decision; the command runs it accounts for are sensed only at the
   host edge.** Whether a gate fits the remaining budget, and the reason a skipped or deferred gate gives, are
   computed by a pure, total function over the already-sensed cost tiers, cache verdicts, profile, and mode —
   the F029/F041 leaf precedent. The actual expensive commands (builds, tests, packs, template instantiation,
   git diffs, package inspection, visual captures) are recorded as **command runs** through the existing
   execution port (F050/F051), and their reproducible identity feeds both the cache and the **provenance audit
   snapshot**. Agent-reviewed cache identity is carried so agent evidence reuses correctly, but agent-reviewed
   checks are **never promoted to blockers** by this row.

## Overview

After F24, Governance can run real, sometimes-expensive deterministic checks across a product's surfaces. But
nothing today **bounds** that expense or **governs** when its evidence may be safely reused as a function of
the profile and run mode. The pieces that decide *whether prior evidence still applies* exist and are correct:
the freshness key (F029) fingerprints the closed set of inputs that, if changed, invalidate evidence (rule
hash, artifact digests, command version, generator version, base/head, environment class — cost deliberately
excluded, because cost does not change validity); the reuse decision (F030) and per-gate cache-eligibility
verdict (F041) turn a freshness match into `Reusable` and a mismatch into `MustRecompute`, always naming the
cause; command runs (F032), executions (F050/F051), and provenance (F033) are all modelled. What is missing is
the **cost dimension of the decision and its audit**:

- There is a `Cost` tier (Cheap / Medium / High / Exhaustive, F014) on every gate, but **no `CostBudget`** — no
  notion of "under this profile and this mode, a run may spend at most *this much* expensive work." So a strict
  release run and a light inner-loop run are, today, equally unbounded.
- There is a `Reusable` / `MustRecompute` verdict per gate, but **no single decision** that folds that verdict
  together with a budget: a reusable gate should cost nothing, a must-recompute gate should spend budget, and a
  must-recompute gate that does not fit the remaining budget should be **skipped or deferred with a clear
  reason** rather than silently run or silently dropped.
- Evidence can be stale, synthetically tainted, or cache-invalidated, but those states are **not surfaced as
  findings** a maintainer sees — they live inside the reuse cause and the evidence model, not in the result.
- Command runs are modelled, but there is **no taxonomy** tying every expensive command kind a governed run
  performs (build / test / pack / template instantiation / git diff / package inspection / visual capture) to a
  recorded run and into a **provenance audit snapshot** that proves what actually ran.

This feature closes that gap, in priority order:

- **Cost budget enforcement.** A run under a given **profile** and **run mode** carries a **maximum cost** it
  may spend on expensive recompute. Given the candidate gates, their cost tiers, and their cache verdicts,
  Governance spends budget only on gates that must recompute, reuses the rest for free, and **skips or defers**
  any must-recompute gate that would exceed the budget — each skip/defer carrying a clear, named reason. The
  decision is deterministic and auditable. This is the central new value and the P1 slice.
- **Budgeted cache decision.** The existing per-gate reuse verdict (`Reusable` / `MustRecompute` + cause) is
  folded together with the budget into a **single cache decision** per gate: *reuse* (freshness key matches,
  costs nothing), *recompute* (must recompute and fits the budget), or *skip / defer* (must recompute but over
  budget). Reuse happens **only** when the freshness key proves the evidence applies; every recompute is
  charged against the budget; nothing is silently reused across a changed input or silently run over budget.
- **Cost/cache findings.** Stale evidence, synthetic taint, and cache-invalidation become **findings** with a
  named cause and the gate they apply to, so a maintainer sees *why* a gate had to recompute or why its evidence
  was rejected — never a silent recompute and never a silently reused stale result.
- **Command-run recording and provenance audit.** Every expensive command a governed run performs — build,
  test, pack, template instantiation, git diff, package inspection, visual capture — is recorded as a command
  run with reproducible identity through the existing execution port, and those runs roll up into a
  deterministic **provenance audit snapshot** that proves what ran, against what inputs, in what environment.
- **Agent-review cache identity, never blocking.** The cache decision carries the agent-review identity fields
  (judge / prompt / check-artifact, F036) so agent-reviewed evidence reuses correctly, but agent-reviewed
  checks remain **advisory** — this row never promotes them to blockers.

This feature does **not** add a new freshness-key dimension or change how a freshness match is computed (F029,
reused), does **not** change the per-gate reuse verdict or its causes (F030/F041, reused), does **not** re-open
the enforcement truth table for how a deterministic finding blocks (F018/F023, reused), and adds **no** new
dependency. It supplies the cost budget, the budgeted cache decision, the cost/cache findings, the command-run
audit, and the provenance snapshot — so that expensive evidence is reused only when proven current and expensive
work is bounded by the profile and mode it runs under.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cost budget bounds expensive work per profile and mode (Priority: P1)

A maintainer runs Governance under a profile (Light / Standard / Strict / Release) and a run mode (Sandbox /
Inner / Focused / Verify / Gate / Release). Each (profile, mode) carries a **maximum cost** the run may spend on
expensive recompute. Given the candidate gates and their cost tiers, Governance runs the gates that fit the
budget and **skips or defers** the gates that would exceed it, each with a **clear, named reason** ("deferred:
exhaustive-cost gate exceeds the Light/Inner budget"). A light inner-loop run never spends exhaustive cost; a
release run permits the full matrix. The budget outcome is deterministic and appears in the result, so cost
control is part of the verdict — not a side effect of which machine happened to run the gate.

**Why this priority**: Bounding expensive work per profile and mode is the feature's reason to exist —
"cost control is part of correctness, not a CLI convenience." It is the foundation the budgeted cache decision
builds on, and it delivers value alone: even before cache integration, a maintainer can see which expensive
gates a profile/mode budget defers and why. Without it, every run is unbounded.

**Independent Test**: Define a budget for each (profile, mode) and a set of candidate gates spanning the cost
tiers. Confirm that under a low budget (Light / Inner) the exhaustive/high-cost gates are skipped or deferred
with a named reason while the cheap gates run; under a high budget (Release) all gates run; and that the same
inputs always produce the same budget decision (deterministic). Confirm a skipped/deferred gate is never
reported as a silent pass.

**Acceptance Scenarios**:

1. **Given** a (profile, mode) with a cost budget and a set of candidate gates whose total cost exceeds it,
   **When** the budget decision runs, **Then** the cheaper gates that fit are run and the expensive gates that
   would exceed the budget are **skipped or deferred**, each carrying a clear reason that names the gate and the
   budget it exceeded.
2. **Given** the same candidate gates under a higher-budget (profile, mode) (e.g. Release), **When** the budget
   decision runs, **Then** every gate fits and none is skipped or deferred for cost.
3. **Given** identical inputs (same gates, costs, profile, mode), **When** the budget decision runs twice,
   **Then** it produces byte-identical decisions and reasons (deterministic, order-independent).
4. **Given** a gate skipped or deferred for cost, **When** the result is read, **Then** the gate is clearly
   marked skipped/deferred-for-cost with its reason — never reported as passed and never silently dropped.

---

### User Story 2 - Expensive evidence reused only when its freshness key proves it applies (Priority: P1)

The maintainer re-runs Governance after a change. For each candidate gate, Governance produces a **single cache
decision**: if the gate's freshness key — rule hash, artifact digests, command version, generator version,
base/head, environment class — matches recorded evidence, the evidence is **reused** and costs nothing against
the budget; if any of those dimensions changed, the gate **must recompute** and the decision names the dimension
that changed (rule-hash invalidation, artifact-digest invalidation, command-version mismatch,
environment-class mismatch); and a must-recompute gate that does not fit the remaining budget is **skipped or
deferred** (Story 1). Reuse happens only when the freshness key proves the evidence still applies — never across
a changed input — and only a must-recompute gate spends budget.

**Why this priority**: Correct reuse is the other half of cost control: a budget is only safe if evidence is
never reused when an input it depends on changed. Folding the existing reuse verdict together with the budget
into one decision is what makes a reusable gate free and an invalidated gate spend (or defer). It is P1
alongside the budget and independently testable against the cache hit/miss matrix.

**Independent Test**: Build a recorded-evidence store and a cache hit/miss matrix: a gate whose freshness key
matches reuses; gates that differ by exactly one dimension (rule hash, an artifact digest, command version,
generator version, base, head, environment class) each force recompute, naming that dimension. Confirm a
reusable gate charges nothing against the budget and a must-recompute gate charges its cost; confirm a
must-recompute gate over budget is skipped/deferred (Story 1 integration).

**Acceptance Scenarios**:

1. **Given** a candidate gate whose freshness key matches recorded evidence, **When** the cache decision runs,
   **Then** the decision is **reuse**, the evidence reference is carried, and the gate charges **nothing**
   against the cost budget.
2. **Given** a candidate gate that differs from recorded evidence by exactly one freshness dimension (rule hash,
   an artifact digest, command version, generator version, base, head, or environment class), **When** the cache
   decision runs, **Then** the decision is **recompute**, naming the dimension that changed, and the gate
   charges its cost against the budget.
3. **Given** a must-recompute gate whose cost does not fit the remaining budget, **When** the cache decision
   runs, **Then** it is **skipped or deferred** with a reason (Story 1), not silently recomputed and not
   silently reused.
4. **Given** no prior evidence for a gate, **When** the cache decision runs, **Then** it is recompute with a
   "no prior evidence" cause — never a fabricated reuse.

---

### User Story 3 - Stale, synthetic-taint, and cache-invalidated findings (Priority: P2)

When evidence cannot be reused or must be distrusted, the maintainer sees **why**. Governance emits a finding
when a gate's evidence is **stale** (an input it depends on changed — cache-invalidated, naming the dimension),
when evidence carries **synthetic taint** (it was produced synthetically rather than by a real run), and when a
recorded reuse was **invalidated**. These findings name the gate and the cause, so a recompute or a rejected
reuse is visible and explained, never silent.

**Why this priority**: Surfacing the cache decision as findings turns an internal verdict into something a
maintainer can act on, and it is the natural home for the synthetic-taint signal the constitution already
requires evidence to carry. It depends on the cache decision (Stories 1–2) existing and is independently
testable.

**Independent Test**: For a gate whose freshness key changed, confirm a cache-invalidated/stale finding is
emitted naming the changed dimension; for evidence marked synthetic, confirm a synthetic-taint finding; for a
clean reuse, confirm no such finding. Confirm the findings are deterministic and carry the offending gate.

**Acceptance Scenarios**:

1. **Given** a gate whose recorded evidence is invalidated because a freshness dimension changed, **When** the
   checks run, **Then** a stale/cache-invalidated finding is emitted naming the gate and the changed dimension.
2. **Given** a gate whose evidence is synthetically tainted, **When** the checks run, **Then** a synthetic-taint
   finding is emitted, distinguishable from a stale finding.
3. **Given** a gate whose freshness key matches and whose evidence is real, **When** the checks run, **Then**
   no stale/synthetic/cache-invalidated finding is emitted for that gate.

---

### User Story 4 - Command runs recorded across every expensive kind, into a provenance audit snapshot (Priority: P2)

The maintainer wants a reproducible record of what actually ran. Every expensive command a governed run
performs — **build, test, pack, template instantiation, git diff, package inspection, visual capture** — is
recorded as a **command run** with reproducible identity (executable, arguments, working directory, environment
delta, exit code, output digests) through the existing execution port, with wall-clock duration kept as sensed
metadata that never affects identity. Those runs roll up into a deterministic **provenance audit snapshot** —
source commit, base/head, rule hash, generator version, artifact digests, environment class, and the command
runs — that proves what ran, against what inputs, in what environment, byte-identically for identical inputs.

**Why this priority**: A command-run taxonomy and a provenance snapshot make the cost/cache decisions auditable
and reproducible, but they build on the budget/cache decisions (Stories 1–2) being the thing worth auditing. It
is independently testable against recorded fixtures.

**Independent Test**: Record command runs of each kind through a fake execution port; confirm each is captured
with reproducible identity and a kind, that duration does not change identity, and that the provenance audit
snapshot is byte-identical for identical inputs and changes only when a reproducible input changes.

**Acceptance Scenarios**:

1. **Given** a governed run that performs commands of several kinds (build, test, pack, template instantiation,
   git diff, package inspection, visual capture), **When** the runs are recorded, **Then** each is captured as a
   command run carrying its kind and reproducible identity, with duration as sensed metadata only.
2. **Given** two runs that differ only in wall-clock duration, **When** their command-run identities are
   compared, **Then** the identities are equal (duration excluded from identity).
3. **Given** a set of recorded command runs and provenance inputs, **When** the provenance audit snapshot is
   produced, **Then** it is byte-identical for identical inputs and differs only when a reproducible input
   (commit, base/head, rule hash, generator version, artifact digest, environment, or a command run) changes.

---

### User Story 5 - Agent-review cache identity carried, never promoted to a blocker (Priority: P3)

Some evidence comes from agent-reviewed (judgement) checks. The cache decision carries the **agent-review
identity** fields — judge identity, prompt identity, check-and-artifact identity (F036) — so agent-reviewed
evidence is reused only when those identities match and re-reviewed when they change. But agent-reviewed checks
remain **advisory**: including their cache identity in the decision never turns an agent-reviewed check into a
blocking gate. Cost governance and agent-review caching coexist without judgement leaking into blocking
verdicts.

**Why this priority**: Carrying agent-review identity keeps expensive agent re-reviews from running needlessly,
but it must not change the safety rule that agent-reviewed checks never block. It is a cross-cutting guarantee,
hence P3, and independently testable.

**Independent Test**: Reuse agent-reviewed evidence when judge/prompt/check-artifact identities match; force
re-review when one changes; confirm that across every profile and mode an agent-reviewed check never blocks the
verdict regardless of its cache decision.

**Acceptance Scenarios**:

1. **Given** agent-reviewed evidence whose judge, prompt, and check-artifact identities match, **When** the
   cache decision runs, **Then** the evidence is reusable on those identities.
2. **Given** agent-reviewed evidence one of whose identities changed, **When** the cache decision runs, **Then**
   it must re-review, naming the changed identity.
3. **Given** an agent-reviewed check with any cache decision, **When** the gate verdict is computed under any
   profile or mode, **Then** the agent-reviewed check never blocks the verdict (it remains advisory).

---

### Edge Cases

- **Budget exactly met**: a gate whose cost exactly equals the remaining budget runs (the boundary is
  inclusive); the next gate over the boundary is deferred — the boundary rule is explicit and deterministic.
- **All gates reusable**: every candidate gate's freshness key matches ⇒ nothing is recomputed, the budget is
  untouched, and no gate is deferred for cost (reuse is free).
- **Budget zero / disabled**: a (profile, mode) with a zero or absent expensive budget ⇒ every must-recompute
  expensive gate is deferred with a reason, but reusable gates and cheap gates still proceed (a zero budget
  bounds *expensive recompute*, not *reuse*).
- **Skip vs defer**: the result distinguishes a gate **skipped** (will not run this row) from one **deferred**
  (could run later / in a higher profile), each with its own reason; neither is reported as a pass.
- **Synthetic evidence reused**: evidence marked synthetic is never silently reused as if real — it surfaces a
  synthetic-taint finding (Story 3) even when its freshness key matches.
- **Command run fails to start / times out**: a command run that cannot start or times out is recorded with its
  sentinel exit code (the existing execution-port behaviour), not dropped, so the audit snapshot still proves
  the attempt.
- **Cost does not affect reuse**: a change in a gate's cost tier alone (with every freshness dimension
  unchanged) ⇒ evidence is still reusable (cost is deliberately excluded from the freshness key); only the
  budget accounting reflects the new cost.
- **Determinism under reordering**: presenting the candidate gates in a different order ⇒ the budget and cache
  decisions, the findings, and the audit snapshot are unchanged (order-independent).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Governance MUST define a **cost budget** scoped to a (profile, run mode) pair that bounds the
  total expensive recompute a run may perform, expressed over the existing `Cost` tiers (Cheap / Medium / High /
  Exhaustive) and the existing `Profile` (Light / Standard / Strict / Release) and `RunMode` (Sandbox / Inner /
  Focused / Verify / Gate / Release) vocabularies — no new tier, profile, or mode is introduced.
- **FR-002**: Given a set of candidate gates with cost tiers and a (profile, mode) budget, Governance MUST
  decide, deterministically and order-independently, which gates **run** within budget and which are **skipped
  or deferred** for cost, and MUST attach a clear reason to each skipped/deferred gate naming the gate and the
  budget it exceeded.
- **FR-003**: A gate that is skipped or deferred for cost MUST be reported as such (distinguishably skipped vs
  deferred) — never reported as passed and never silently dropped.
- **FR-004**: Governance MUST produce a **single cache decision** per candidate gate that folds the existing
  per-gate reuse verdict (`Reusable` / `MustRecompute` + cause, F030/F041) together with the budget into one of:
  **reuse** (freshness key matches; charges nothing against the budget), **recompute** (must recompute and fits
  the budget; charges its cost), or **skip/defer** (must recompute but over budget; FR-002).
- **FR-005**: Evidence MUST be **reused only when its freshness key proves it applies** — the existing closed
  freshness-input set (rule hash, artifact digests, command version, generator version, base/head, environment
  class; F029) matching recorded evidence — and any single-dimension change MUST force recompute, naming the
  changed dimension. Reuse MUST NOT occur across a changed input.
- **FR-006**: This feature MUST **reuse** the freshness-key vocabulary and matching (F029), the reuse decision
  and per-gate cache-eligibility verdict and their causes (F030/F041) **unchanged** — it MUST NOT add a new
  freshness dimension, a new reuse verdict, or change how a freshness match is computed. (`Cost` remains
  deliberately excluded from the freshness key — it does not affect reuse validity.)
- **FR-007**: Governance MUST emit a **finding** when a gate's evidence is **stale / cache-invalidated** (a
  freshness dimension changed), naming the gate and the changed dimension; and a distinct **synthetic-taint**
  finding when evidence is synthetically tainted; with no swallowed cause and no silently reused stale or
  synthetic evidence. (A gate with **no prior evidence** at all surfaces a distinct **no-evidence** finding
  from the same machinery — the recompute cause is named, never swallowed.)
- **FR-008**: Governance MUST record every expensive command a governed run performs — **build, test, pack,
  template instantiation, git diff, package inspection, visual capture** — as a **command run** with
  reproducible identity, through the existing execution port (F050/F051) and command-run model (F032), with
  wall-clock duration retained as sensed metadata that does **not** contribute to identity.
- **FR-009**: Governance MUST roll the recorded command runs and the provenance inputs (source commit,
  base/head, rule hash, generator version, artifact digests, environment class; F033) into a deterministic
  **provenance audit snapshot** that is byte-identical for identical inputs and changes only when a reproducible
  input changes.
- **FR-010**: The cache decision MUST carry the **agent-review cache identity** (judge / prompt /
  check-and-artifact identities; F036) so agent-reviewed evidence is reused only on matching identity and
  re-reviewed on a change — but agent-reviewed checks MUST remain **advisory**; this row MUST NOT promote any
  agent-reviewed check to a blocker under any profile or mode.
- **FR-011**: The budget decision, the cache decision, the cost/cache findings, and the provenance audit
  snapshot MUST all be **deterministic** — stable ordering, normalized paths, no wall-clock / username /
  environment dependence — so identical inputs yield byte-identical output.
- **FR-012**: Cost/cache evaluation MUST distinguish a missing/malformed **input** (no recorded evidence, an
  unreadable store, an absent provenance input) from a **tool defect**, naming the offending source, with no
  swallowed errors and no fabricated reuse or fabricated pass (safe-failure, as F014–F059).
- **FR-013**: This feature MUST **reuse** the existing enforcement machinery (F018/F023) for how a deterministic
  finding maps to a blocking verdict — it MUST NOT re-open the enforcement truth table; it supplies new
  findings (stale / synthetic / cache-invalidated), a budget decision, and an audit snapshot only.
- **FR-014**: The budget and cache decision MUST be computed by a pure, total function over already-sensed
  inputs (cost tiers, cache verdicts, profile, mode, recorded evidence); the only I/O — recording command runs
  and reading the evidence/provenance sources — MUST live at the host edge through the existing ports, with no
  filesystem / process / registry dependency in the pure core (the F029/F041/F051 leaf-plus-sensor precedent).
- **FR-015**: A generated product MUST be able to run cost-budgeted governance **standalone** — without monorepo
  access — using only its own recorded evidence, command runs, and provenance, consistent with the
  standalone-governance guarantee (F23/F24); no budget or cache decision may require a monorepo-only path.

### Key Entities *(include if data involved)*

- **Cost budget**: the maximum expensive recompute a run may perform, scoped to a (profile, run mode) pair and
  expressed over the existing `Cost` tiers — the bound the run/skip/defer decision is made against.
- **Cache decision**: the single per-gate decision that folds the existing reuse verdict together with the
  budget — reuse (free), recompute (charged), or skip/defer (over budget) — each carrying its cause/reason.
- **Freshness key / freshness inputs** (reused, F029): the closed, comparable set of inputs (rule hash, artifact
  digests, command version, generator version, base/head, environment class) whose match permits reuse and whose
  change forbids it; cost deliberately excluded.
- **Cache-eligibility verdict** (reused, F030/F041): `Reusable` / `MustRecompute` + named cause per gate, folded
  into the cache decision.
- **Stale / synthetic-taint / cache-invalidated finding**: the findings that surface why a gate had to recompute
  or why its evidence was rejected, each naming the gate and cause.
- **Command run** (reused/extended, F032/F050/F051): a recorded expensive command of a named **kind** (build /
  test / pack / template instantiation / git diff / package inspection / visual capture) with reproducible
  identity and sensed-only duration.
- **Provenance audit snapshot** (reused/extended, F033): the deterministic roll-up of provenance inputs and
  command runs that proves what ran, against what inputs, in what environment.
- **Agent-review cache identity** (reused, F036): the judge / prompt / check-artifact identities that gate
  reuse of agent-reviewed evidence — carried but never promoted to a blocker.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Under a low-budget (profile, mode), every expensive (High/Exhaustive) gate that exceeds the budget
  is skipped or deferred with a named reason while every cheap gate that fits runs; under a high-budget
  (Release) (profile, mode) every gate runs — verified by a cost-budget enforcement matrix across the profiles
  and modes.
- **SC-002**: A gate whose freshness key matches recorded evidence is reused and charges **nothing** against the
  budget, and a gate differing by exactly one freshness dimension (rule hash, an artifact digest, command
  version, generator version, base, head, or environment class) recomputes and names that dimension — verified
  by a cache hit/miss matrix covering every single-dimension change (rule-hash, artifact-digest,
  environment-class, command-version invalidation).
- **SC-003**: A must-recompute gate that does not fit the remaining budget is skipped or deferred and **never**
  silently recomputed or silently reused; a skipped/deferred gate is never reported as passed — verified by the
  budget-integration fixtures.
- **SC-004**: Stale/cache-invalidated and synthetic-taint findings are emitted with the offending gate and cause
  for invalidated/synthetic evidence, and a clean reuse emits none — verified by stale-evidence and
  synthetic-taint fixtures.
- **SC-005**: Command runs of every named kind (build / test / pack / template instantiation / git diff /
  package inspection / visual capture) are recorded with reproducible identity, and two runs differing only in
  duration share an identity — verified by command-run fixtures across the kinds.
- **SC-006**: The provenance audit snapshot is **byte-identical** for identical inputs and changes only when a
  reproducible input changes — verified by an audit-provenance snapshot fixture (including a no-op-input-change
  stability check).
- **SC-007**: An agent-reviewed check **never** changes a blocking verdict under any profile or mode regardless
  of its cache decision, and agent-reviewed evidence reuses only on matching judge/prompt/check-artifact
  identity — verified across the enforcement matrix.
- **SC-008**: The budget decision, cache decision, findings, and audit snapshot are **deterministic and
  order-independent**: identical inputs yield byte-identical output and reordering the candidate gates does not
  change any decision — verified by a determinism/reordering test.

## Assumptions

- **Next-item resolution**: "next item in plan" is roadmap **F25 ·
  `025-cost-cache-command-provenance`**, the next unimplemented row after F24 (`059-package-docs-skills-design-checks`)
  merged on 2026-06-25. F25's roadmap dependencies (F18 enforcement registry, F21 JSON projections, F24 surface
  checks) are all implemented.
- **Reuse the freshness key and reuse verdict unchanged**: the closed freshness-input vocabulary (rule hash,
  artifact digests, command version, generator version, base/head, environment class) and its matching are F029,
  reused unchanged; the per-gate `Reusable` / `MustRecompute` verdict and its named causes are F030/F041, reused
  unchanged. `Cost` is and remains excluded from the freshness key — cost does not affect reuse validity
  (F029 research D5). This row adds the cost budget and the budgeted cache decision around them, not a new cache
  identity (FR-005, FR-006).
- **What is genuinely new**: the `CostBudget` scoped to (profile, mode); the single run/reuse/skip/defer cache
  decision that integrates the budget with the existing reuse verdict; the stale / synthetic-taint /
  cache-invalidated findings; the command-run *kind* taxonomy and its roll-up into a provenance audit snapshot;
  and the inclusion of agent-review cache identity in the decision while keeping agent-reviewed checks advisory.
  The exact budget representation (e.g. per-tier counts vs an ordered cost ceiling) and the precise skip-vs-defer
  semantics are planning decisions deferred to `/speckit-plan`.
- **Pure decision + edge sensing split**: the budget and cache decisions are pure, total functions over
  already-sensed inputs (the F029/F041 leaf precedent); recording command runs and reading the
  evidence/provenance sources happen only at the host edge through the existing execution/store ports
  (F050/F051/F047/F048), with no filesystem/process/registry dependency in the pure core (FR-014).
- **Enforcement and schema reuse**: how a deterministic finding maps to a blocking verdict is the existing
  F018/F023 enforcement truth table, reused unchanged (FR-013); the cost/cache/provenance JSON projections
  follow the existing deterministic-JSON, `schemaVersion`-headed precedent (F021/F025/F042), additive and
  byte-identical when empty. Whether the budget/audit surfaces through an existing host command (e.g.
  `fsgg route` / `fsgg ship` / `fsgg verify`) or a dedicated projection is a planning decision.
- **Agent-review stays advisory**: including agent-review cache identity (F036) in the cache decision keeps
  agent re-reviews from running needlessly but never promotes an agent-reviewed check to a blocker — the
  advisory guarantee F24 established is preserved (FR-010, SC-007).
- **Determinism is mandatory**: every decision, finding, and snapshot normalizes ordering and paths and avoids
  wall-clock/username/environment dependence so identical input yields byte-identical output (FR-011, SC-006,
  SC-008), matching the byte-identical discipline F042–F059 hold.
- **Standalone preserved**: cost-budgeted governance runs against a product checked out standalone using only
  its own recorded evidence, command runs, and provenance (FR-015), consistent with the F23/F24 standalone
  guarantee; no budget or cache decision requires monorepo-only access or a network/registry call from the core.
