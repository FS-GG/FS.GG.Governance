# Feature Specification: Judge-vs-Human Calibration Evidence — the Beyond-Advisory-Maturity Gate

**Feature Branch**: `040-calibration-evidence-gate`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 12: Agent-Reviewed Rule Guardrails** is open. Its **first five**
lines are complete: F035 (`FS.GG.Governance.AgentReviewKey` — the agent-review cache key), F036
(`FS.GG.Governance.VerdictReuse` — the verdict store + invalidation decision), F037
(`FS.GG.Governance.PromptIsolation` — reviewer-prompt isolation), F038 (`FS.GG.Governance.ReviewRecord` — the
auditable review record), and F039 (`FS.GG.Governance.AdvisoryPromotion` — the per-finding advisory-to-blocking
promotion gate). The next unchecked line is the phase's **sixth and last**: *"Define judge-vs-human calibration
evidence before any agent-reviewed rule can block protected boundaries."* This is the design's **calibration debt**
guardrail (`docs/initial-design.md`, *Optional agent-reviewed constraints*): *"Agent-reviewed rule packs need
periodic judge-vs-human comparison before they can move beyond advisory maturity."* Continuing this repo's
maintainer-confirmed **pure-core-first** rhythm (F015–F039 each landed a pure, total, deterministic core before any
host edge consumed it), this row delivers the **calibration-evidence decision primitive**: a typed value and a
single total, deterministic function that decides whether an agent-reviewed rule pack has accumulated **enough
judge-vs-human calibration evidence** to move **beyond advisory maturity** — and which **defaults to uncalibrated**
(stays advisory) whenever the evidence is absent or insufficient. It is a **decision** core (the analogue of F023
`deriveEffectiveSeverity` / F030 `decide` / F036 `lookup` / F039 `decide`), not a record core: it derives **no**
byte-stable identity. It invokes **no model / agent / network**, reads **no clock / filesystem / git / environment**,
computes **no hash from raw bytes**, runs **no actual review** and **no actual human comparison**, makes **no cache
lookup / verdict invalidation** (F035 / F036), builds **no review record** (F038, consumed as input not produced),
produces / interprets / re-scores **no verdict** (the judge and human verdicts are opaque facts), derives **no
effective severity / enforcement verdict** (F023 / F024), performs **no persistence / JSON projection**, and adds
**no CLI**.

## Overview

An agent-reviewed check produces a verdict by judgement, not by deterministic proof. F039 settled the *per-finding*
question — may **this** finding be promoted from advisory to block-eligible, on one of three permitted bases — and
it deliberately stopped short: an *eligible-to-block* decision there is **necessary, not sufficient**, because a
separate, **rule-pack-level** prerequisite still stands. That prerequisite is **calibration**. The design is
explicit: agent-reviewed rule packs *"need periodic judge-vs-human comparison before they can move beyond advisory
maturity,"* and *"protected-branch blocking should come from deterministic checks … until calibration exists."* An
agent reviewer that has never been measured against human judgement — or whose measured agreement is poor, or whose
calibration is too thin to be meaningful — must stay **advisory**, no matter how an individual finding scores.

This row delivers that gate as a **pure decision over supplied calibration evidence**, ahead of any persistence,
enforcement wiring, or CLI. Where F035 keyed *which* verdict a request maps to, F036 decided *whether a cached
verdict is still valid*, F037 shaped the request so the artifact stays data, F038 captured *what a completed review
was*, and F039 decided *whether one finding earned per-finding promotion eligibility*, **this** core decides
*whether the agent reviewer behind those findings has earned the right to move beyond advisory maturity at all* —
and, by default, says **no**:

| Phase-12 row | Core | Question it answers |
|---|---|---|
| 1 — cache key (F035) | `AgentReviewKey` | *Under what identity is a verdict cached?* |
| 2 — invalidation (F036) | `VerdictReuse` | *Is a cached verdict still valid, and if not, why?* |
| 3 — prompt isolation (F037) | `PromptIsolation` | *How is the request shaped so the artifact is data, not an instruction?* |
| 4 — review record (F038) | `ReviewRecord` | *What was this completed review, for the audit trail?* |
| 5 — advisory promotion (F039) | `AdvisoryPromotion` | *May **this finding** be promoted from advisory to block-eligible, and on which basis?* |
| **6 — calibration evidence (this row)** | **(new pure decision core)** | ***Has this agent reviewer accumulated enough judge-vs-human calibration evidence to move beyond advisory maturity at all?*** |

This core makes **no verdict** and runs **no comparison** — the judge verdicts and the human verdicts have already
been produced elsewhere; this core consumes their **already-classified agreement** as supplied facts. It neither
produces, interprets, compares, nor re-scores any verdict. It decides only the **calibration question**: given the
supplied judge-vs-human comparison evidence and the supplied calibration thresholds, is the reviewer **uncalibrated**
(stays advisory) or **calibrated** (beyond advisory maturity), and *why*. It is the gate that makes the phase's exit
criterion true — *"protected-branch blocking does not depend on uncalibrated agent judgement"* — by ensuring an
agent reviewer that has not been calibrated against human judgement can **never**, on its own, contribute to blocking
a protected boundary. What **this** row delivers:

- **Uncalibrated by default — the safe-by-construction posture.** With no calibration evidence, or with evidence that
  falls short of the supplied thresholds, the decision is *uncalibrated*. There is no input, ordering, or default
  that lets an unmeasured agent reviewer reach beyond-advisory maturity. The default is the design's default
  (*"remain advisory by default until … calibration evidence"*).
- **Calibration means human comparison, never self-assessment.** A reviewer becomes *calibrated* only on the strength
  of **judge-vs-human comparison** — samples in which the agent's verdict was checked against a human's verdict on the
  same item and classified as agreeing or disagreeing — measured against supplied thresholds. The model's own
  self-reported confidence is not calibration; only comparison with human judgement is.
- **Periodic comparison, not a single sample.** A lone comparison is not calibration any more than a lone review is
  proof (the F039 single-sample-noise discipline, lifted to the calibration level). The calibration basis requires a
  **minimum number** of comparison samples *and* an **agreement level meeting a supplied threshold**; one sample, or
  none, never calibrates.
- **A total, deterministic decision that names its outcome (the no-hide rule).** A single total function over the
  supplied evidence returns either *uncalibrated* (carrying the reason — no evidence, too few comparison samples, or
  agreement below the threshold) or *calibrated* (carrying the satisfied calibration metrics, so the audit trail
  shows exactly what evidence cleared the gate). Identical inputs always yield an identical decision; nothing is
  clocked, sensed, hashed, or persisted.
- **Calibrated is necessary, not sufficient — it never itself blocks.** This row decides only rule-pack-level
  calibration maturity. A *calibrated* decision authorizes the reviewer to move beyond advisory maturity; it carries
  no blocking action, derives no effective severity, and asserts no protected-boundary block. Blocking still composes
  the **two** necessary conditions — F039 per-finding eligibility **and** this row's per-reviewer calibration —
  through the deterministic enforcement machinery (F023 / F024), which this row does not re-implement.

The core is **pure and total over supplied data**, exactly like F023 / F030 / F036 / F039: the judge-vs-human
comparison samples (each already classified as agreeing or disagreeing), the count of samples, the observed agreement
level, and the calibration thresholds are handed in as already-formed values; nothing is reviewed, compared against a
human at runtime, clocked, hashed, or persisted. The **actual agent review** and the **actual human comparison**,
**producing or interpreting any verdict** (the judge and human verdicts are opaque facts), the **cache lookup /
verdict invalidation** (F035 / F036), the **review record** (F038, consumed as input, not produced), the
**effective-severity / enforcement derivation** (F023 / F024, which compose *with* this decision downstream but are
not re-implemented), any **persistence / JSON projection** of the decision, and any **CLI** all remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An uncalibrated agent reviewer stays advisory by default (Priority: P1)

A Governance component holds an agent-reviewed rule pack with no judge-vs-human calibration evidence — or with
evidence too thin or too disagreeing to clear the supplied thresholds. It must decide that the reviewer is
**uncalibrated** — its findings may be reported, but the reviewer may not move beyond advisory maturity — because an
agent reviewer that has not been measured against human judgement is not trustworthy enough to block.

**Why this priority**: This is the heart of the design's calibration-debt constraint and the phase's exit criterion —
*"protected-branch blocking does not depend on uncalibrated agent judgement."* If an unmeasured reviewer could reach
beyond-advisory maturity, the whole guardrail fails. The default must be uncalibrated, by construction, and it is
independently demonstrable from supplied values alone.

**Independent Test**: Supply calibration evidence that is empty (no comparison samples), or below the required sample
count, or with an agreement level below the supplied threshold, and assert the decision is *uncalibrated* with a
reason indicating which calibration requirement was not met. No model invoked, no human consulted, no I/O.

**Acceptance Scenarios**:

1. **Given** an agent reviewer with no judge-vs-human comparison samples at all, **When** the calibration decision is
   made, **Then** the decision is *uncalibrated* and names the reason as *no calibration evidence present*.
2. **Given** a reviewer whose comparison-sample count is strictly below the supplied minimum, **When** the decision is
   made, **Then** the decision is *uncalibrated* — too thin to be meaningful — and the reason reflects the sample
   count falling below the required minimum.
3. **Given** a reviewer with enough comparison samples but whose observed agreement level is strictly below the
   supplied agreement threshold, **When** the decision is made, **Then** the decision is *uncalibrated* and the reason
   reflects the agreement falling below the threshold.
4. **Given** a reviewer whose only would-be justification is the model's own self-reported confidence (and no
   judge-vs-human comparison), **When** the decision is made, **Then** the decision is *uncalibrated* — model
   self-confidence is never calibration evidence.

---

### User Story 2 - A reviewer becomes calibrated on sufficient judge-vs-human evidence, and the evidence is named (Priority: P1)

When the supplied calibration evidence meets the thresholds — enough judge-vs-human comparison samples *and* an
agreement level meeting or exceeding the supplied threshold — the component must decide that the reviewer is
**calibrated** (beyond advisory maturity), and the decision must name the satisfied calibration metrics so the audit
trail records exactly what evidence cleared the gate.

**Why this priority**: Calibration must be *achievable* on the design's own terms, or agent-reviewed rule packs could
never move beyond advisory maturity even when properly measured against humans. Naming the satisfied metrics is the
no-hide rule (the F023 reason / F036 located-cause / F039 named-basis discipline): an unexplained calibration is as
untrustworthy as an unjustified one. It is co-P1 with Story 1 — together they fix the gate's two outcomes.

**Independent Test**: Supply calibration evidence whose sample count meets or exceeds the required minimum **and**
whose agreement level meets or exceeds the supplied threshold, and assert the decision is *calibrated* naming the
satisfied metrics (the observed sample count and agreement level against their requirements); confirm the decision is
a deterministic function of the supplied evidence.

**Acceptance Scenarios**:

1. **Given** calibration evidence whose comparison-sample count meets or exceeds the required minimum and whose
   agreement level meets or exceeds the supplied threshold, **When** the decision is made, **Then** it is *calibrated*
   and names the satisfied metrics (observed sample count and agreement level).
2. **Given** evidence whose agreement level is exactly equal to the supplied threshold (with the sample-count
   requirement met), **When** the decision is made, **Then** it is *calibrated* — the agreement comparison is
   inclusive (*meets or exceeds*).
3. **Given** evidence comfortably above both the sample-count minimum and the agreement threshold, **When** the
   decision is made, **Then** it is *calibrated* and names the satisfied metrics, not merely a bare "calibrated" flag.

---

### User Story 3 - The decision is total, deterministic, and never blocks on its own (Priority: P2)

The calibration decision must be defined for every combination of supplied evidence — any number of comparison
samples (including none and one), any observed agreement level against any threshold — never throwing and never
reading a clock, file, or model. And a *calibrated* decision must be understood as *necessary but not sufficient* for
blocking: it authorizes beyond-advisory maturity, it does not itself block a protected boundary, and it composes with
F039 per-finding eligibility and the deterministic enforcement machinery (F023 / F024) rather than replacing them.

**Why this priority**: Totality and determinism are the contract every prior core in this line upholds (F023 / F030 /
F036 / F039), and they make the gate trustworthy and testable. Making explicit that calibration ≠ blocking keeps this
row honestly scoped against the enforcement phase. It builds on Stories 1–2, so it is P2.

**Independent Test**: Exercise the decision across the full cross-product of comparison-sample counts (zero, one, and
many) and observed agreement levels straddling the threshold (below, equal, above) and assert it always returns a
decision and never throws; make the decision twice from identical evidence and assert the two decisions are equal;
confirm a *calibrated* decision carries no blocking action and no enforcement verdict.

**Acceptance Scenarios**:

1. **Given** any number of comparison samples (including zero and one) and any observed agreement level against any
   threshold, **When** the decision is made, **Then** a decision is always returned and the function never throws
   (totality).
2. **Given** the same supplied evidence, **When** the decision is made twice, **Then** the two decisions are equal —
   the decision is a total, deterministic function of its inputs (no clock, no model, no human consulted, no I/O).
3. **Given** a *calibrated* decision, **When** it is inspected, **Then** it represents only beyond-advisory
   *maturity*: it carries no blocking action, derives no effective severity, and does not claim any protected boundary
   may now be blocked (blocking still composes F039 eligibility, this calibration, and the enforcement machinery).

---

### Edge Cases

- **No comparison samples at all.** The decision is *uncalibrated* with the *no calibration evidence* reason — the
  design's default. Never malformed.
- **Exactly one comparison sample.** A lone judge-vs-human comparison is not *periodic* comparison and never
  calibrates — the F039 single-sample-noise discipline lifted to the calibration level. Whether the core enforces a
  fixed minimum-of-more-than-one floor or treats the minimum purely as a supplied threshold is a planning decision;
  the fixed contract is that a single sample never calibrates.
- **Sample count exactly equal to the required minimum.** The sample-count requirement uses an inclusive
  *meets-or-exceeds* comparison (`>=`), so a count one below the minimum is never sufficient; subject to the
  no-single-sample floor above.
- **Agreement level exactly equal to the threshold.** The agreement requirement uses an inclusive *meets-or-exceeds*
  comparison (`>=`), so an agreement one step below the threshold is never satisfied. The exact comparator is fixed
  here (`>=`); the threshold's *source* is a supplied value, not parsed by this core.
- **Enough samples but agreement below threshold.** *Uncalibrated*, with the agreement-below-threshold reason — a
  reviewer that has been measured and disagrees with humans too often is not calibrated.
- **Enough agreement but too few samples.** *Uncalibrated*, with the too-few-samples reason — high agreement over a
  handful of comparisons is not enough evidence to trust.
- **Stale calibration evidence.** The design calls for *periodic* comparison, so calibration evidence may go stale.
  Whether this row models a recency requirement (and, if so, takes the recency as a **supplied** fact compared against
  a supplied freshness window — never reading a clock, the F034 sensed-metadata discipline) or treats staleness as a
  separate downstream concern is a planning decision; if modelled, stale evidence yields *uncalibrated* with a
  stale-calibration reason.
- **Judge or prompt identity change.** Calibration is tied to a specific judge/prompt identity (the F035/F036
  judge-drift discipline); evidence gathered under one model id / model version / reviewer-prompt hash does not
  calibrate a different identity. Whether this core scopes the evidence to an identity or receives evidence already
  filtered to one identity is a planning decision; the fixed contract is that calibration is per judge identity, not
  global.
- **A calibrated reviewer under deterministic-only enforcement policy.** Calibration here does not imply blocking:
  beyond-advisory maturity is necessary, not sufficient. The enforcement machinery (F023 / F024), composing this
  calibration with F039 eligibility, still decides whether anything actually blocks. This row produces calibration
  status, never a block.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **calibration-decision** value with exactly two outcomes —
  *uncalibrated* and *calibrated* — where *uncalibrated* carries the **reason** the reviewer did not clear the gate
  (no calibration evidence, too few comparison samples, or agreement below threshold — and, if a recency requirement
  is modelled, stale calibration) and *calibrated* carries the **satisfied calibration metrics** (the observed sample
  count and agreement level that cleared the thresholds) — the no-hide rule. The value MUST make it impossible to be
  *calibrated* without the supplied thresholds having been met.
- **FR-002**: Calibration MUST be modelled as **judge-vs-human comparison evidence**: a collection of comparison
  samples, each pairing the agent reviewer's verdict with a human's verdict on the same reviewed item and classified
  as **agreeing or disagreeing**. The model's own self-reported confidence MUST NOT be calibration evidence; only
  comparison with human judgement counts.
- **FR-003**: The system MUST provide a single **total** decision function that, given the supplied judge-vs-human
  calibration evidence (the comparison samples, or their summarised count and observed agreement level) and the
  supplied calibration thresholds (a minimum comparison-sample count and a minimum agreement level), returns the
  calibration-decision value. It MUST be **uncalibrated by default**: when the evidence is absent or falls short of a
  threshold, the result MUST be *uncalibrated*. It MUST be *calibrated* if and only if every supplied threshold is
  met.
- **FR-004**: The **calibration basis** MUST be satisfied only when the supplied comparison-sample count **meets or
  exceeds** the supplied minimum **and** the observed agreement level **meets or exceeds** the supplied agreement
  threshold (inclusive `>=` comparisons), modelling *periodic* judge-vs-human comparison; a sample count below the
  minimum MUST NOT satisfy it, an agreement level below the threshold MUST NOT satisfy it, and a **single** comparison
  sample alone MUST NOT satisfy it. *(Whether the core enforces a fixed minimum-sample floor or treats the minimum
  purely as a supplied policy value, the exact representation of the agreement level, and whether a recency
  requirement is included are planning decisions; the no-single-sample and inclusive-comparator contracts are fixed.)*
- **FR-005**: The decision MUST **name the satisfied calibration metrics** in a *calibrated* outcome and **name the
  reason** in an *uncalibrated* outcome — the no-hide rule (the F023 reason / F036 located-cause / F039 named-basis
  discipline). No calibration may be unexplained and no uncalibrated hold may be unattributed.
- **FR-006**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no filesystem, no
  git, no environment, and no network; it MUST invoke no model / agent, run no review, perform no human comparison at
  runtime, compute no hash from raw bytes, perform no cache-key / verdict-store / lookup / invalidation operation
  (F035 / F036 own those), build no review record (F038, consumed not produced), measure no elapsed time, spawn no
  process, and persist nothing. If a recency requirement is modelled, the recency MUST be a **supplied** fact compared
  against a **supplied** window — never a clock read (the F034 sensed-metadata discipline). Identical supplied inputs
  MUST always yield an identical decision.
- **FR-007**: The core MUST treat each comparison sample's **judge and human verdicts as opaque facts**: it MUST NOT
  produce, interpret, compare, re-score, or threshold the verdicts themselves (F038 records a verdict; this consumes
  the already-classified agreement, not the verdict's meaning). It decides only the **calibration question** over the
  supplied evidence.
- **FR-008**: A *calibrated* decision MUST be **necessary but not sufficient** for blocking: it MUST carry no blocking
  action and MUST NOT assert that any protected boundary may be blocked. The system MUST NOT derive effective
  severity or an enforcement verdict (F023 / F024 own those), MUST NOT re-implement F039 per-finding eligibility (it
  composes *with* it downstream), and MUST NOT emit any **persistence**, **JSON projection**, or **CLI** surface. Its
  sole output is the typed calibration-decision value.
- **FR-009**: The core MUST **reuse existing typed facts verbatim** where one maps cleanly — concretely, it SHOULD
  reuse the established judge/prompt identity vocabulary already modelled in the repo (F035
  `ModelId` / `ModelVersion` / `ReviewerPromptHash`) for the per-judge calibration scope where applicable, and MAY
  reuse the recorded-verdict vocabulary (F038 `RecordedVerdict`) for the per-sample verdicts — without modifying any
  merged core. It MUST introduce only the minimal new vocabulary the row needs (the judge-vs-human comparison sample,
  the calibration evidence, the calibration thresholds, the satisfied-metrics / reason attribution, and the
  calibration-decision value). This feature is additive. *(Exactly which existing types map, and the shapes of the
  comparison-sample / evidence / threshold inputs, are planning decisions deferred to `/speckit-plan`.)*
- **FR-010**: If this feature introduces a public F# module, its surface MUST be governed by the repo's `.fsi`-first
  and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1** change (see
  Assumptions). [The concrete module home and name are a planning decision deferred to `/speckit-plan`.]
- **FR-011**: The core MUST NOT add a new third-party package dependency; the decision MUST use only facilities
  already available to the merged cores (the shared framework / BCL) plus reused existing vocabulary.

### Key Entities *(include if feature involves data)*

- **Judge-vs-human comparison sample**: One measurement in which the agent reviewer's verdict on an item was checked
  against a human's verdict on the same item and classified as **agreeing or disagreeing**. The judge and human
  verdicts are opaque facts (F038 vocabulary); only the agreement classification is consumed. *The unit of calibration
  evidence.*
- **Calibration evidence**: The collection of judge-vs-human comparison samples for an agent reviewer (scoped to a
  judge/prompt identity — F035 `ModelId` / `ModelVersion` / `ReviewerPromptHash`), summarised by a comparison-sample
  count and an observed agreement level. *What might calibrate the reviewer.*
- **Calibration thresholds**: The supplied policy levers — a minimum comparison-sample count and a minimum agreement
  level (and, if modelled, a recency window) — against which the evidence is measured. Supplied values, not parsed by
  this core.
- **Calibration metrics**: The satisfied measurements carried by a *calibrated* outcome — the observed sample count
  and agreement level that cleared the thresholds — the no-hide record of what authorized beyond-advisory maturity.
- **Calibration reason**: The attribution carried by an *uncalibrated* outcome — no calibration evidence, too few
  comparison samples, agreement below threshold (or, if modelled, stale calibration) — the no-hide rule for the
  default.
- **Calibration decision**: The two-outcome value — *uncalibrated* (with reason) or *calibrated* (with satisfied
  metrics) — the gate's verdict on the calibration question.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With no calibration evidence, or with evidence falling short of any supplied threshold, the decision is
  *uncalibrated* in 100% of cases — an unmeasured or under-measured agent reviewer is never *calibrated*, and the
  model's self-reported confidence never calibrates.
- **SC-002**: With the supplied thresholds met (comparison-sample count and agreement level both at or above their
  requirements), the decision is *calibrated* and names the satisfied metrics (the no-hide rule), in 100% of cases.
- **SC-003**: The calibration basis is satisfied exactly when the comparison-sample count meets or exceeds the
  required minimum **and** the observed agreement level meets or exceeds the supplied threshold, and never for a
  single comparison sample alone — verifiable across sample counts and agreement levels below, equal to, and above
  their thresholds in 100% of cases.
- **SC-004**: The decision is total — it returns a decision and never throws for every combination of
  comparison-sample count (including zero and one) and observed agreement level against every threshold — in 100% of
  cases.
- **SC-005**: For the same supplied evidence, making the decision twice yields equal results in 100% of cases
  (determinism), with no model invoked, no human consulted, no review run, no bytes hashed, and nothing persisted —
  demonstrable across different working directories, at different times, and with unrelated repository / filesystem
  state changed between calls (purity).
- **SC-006**: A *calibrated* decision carries no blocking action and no enforcement verdict — it is
  necessary-not-sufficient and composes with F039 eligibility and the enforcement machinery — in 100% of cases.
- **SC-007**: The merged cores and their `surface/*.surface.txt` baselines, and `dotnet build` / `dotnet test` over
  the existing projects, are **unchanged** by this feature except for the additive new surface — no existing baseline
  is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the pure calibration decision — over supplied values.** Deciding whether an agent reviewer is
  *uncalibrated* or *calibrated* (beyond advisory maturity), on the design's judge-vs-human comparison evidence
  measured against supplied thresholds, defaulting to uncalibrated, is the whole of this row (the design's
  *calibration debt* response). The **actual review**, the **actual human comparison**, **producing or interpreting
  any verdict**, the **effective-severity / enforcement derivation** (F023 / F024), F039 **per-finding eligibility**
  (composed with, not re-implemented), any **persistence / JSON projection**, and any **CLI** are out of scope. This
  is a *decision* core (the F023 / F030 / F036 / F039 analogue), not a record core — it derives no byte-stable
  identity.
- **Every fact is a SUPPLIED value; this core neither runs reviews nor consults a human nor reads a clock.** The
  comparison samples (each already classified as agreeing or disagreeing), the sample count, the observed agreement
  level, the calibration thresholds, and any recency facts are already-formed values handed in by the edge (the F029 /
  F032 / F035 opaque-token discipline — no validation, no dereferencing). Running the agent review, performing the
  human comparison, counting samples over time, and sensing recency belong to a later host edge (Principle IV).
- **Uncalibrated is the default, by construction.** There is no input, ordering, or fallback that calibrates a
  reviewer absent sufficient judge-vs-human evidence. *Calibrated* requires the supplied thresholds to be met; the
  type makes a threshold-unmet calibration unrepresentable. This encodes the design's *"remain advisory by default
  until … calibration evidence."*
- **Calibration is necessary, not sufficient — and it never itself blocks.** A *calibrated* outcome authorizes a
  reviewer to move beyond advisory maturity; it does not block and does not bypass F039 per-finding eligibility or the
  deterministic enforcement machinery. Per the design, *"protected-branch blocking should come from deterministic
  checks … until calibration exists"*; this row keeps that true by being the calibration prerequisite without ever
  itself blocking.
- **Calibration is per judge identity, and a single sample is never enough.** Calibration evidence is tied to a
  judge/prompt identity (F035 `ModelId` / `ModelVersion` / `ReviewerPromptHash`); a judge or prompt change means the
  prior evidence does not calibrate the new identity (the F036 judge-drift discipline). A lone comparison never
  calibrates (the F039 single-sample-noise discipline lifted to calibration). The basis is met at *sample count ≥
  minimum* **and** *agreement ≥ threshold* (both inclusive). Whether this core scopes evidence to an identity itself
  or receives identity-filtered evidence, whether it enforces a fixed minimum-sample floor, the exact representation
  of the agreement level, and whether a recency requirement is modelled are planning decisions deferred to
  `/speckit-plan`; the no-single-sample, per-identity, and inclusive-comparator contracts are fixed here.
- **Reuse existing typed facts verbatim; introduce only the minimal new vocabulary.** Judge/prompt identity already
  exists in the repo (F035 `ModelId` / `ModelVersion` / `ReviewerPromptHash`) and the recorded verdict exists (F038
  `RecordedVerdict`); whether this core reuses them, and the exact shapes of the comparison-sample, evidence, and
  threshold inputs, are planning decisions deferred to `/speckit-plan`. The comparison sample, the calibration
  evidence, the thresholds, the satisfied-metrics / reason attribution, and the calibration-decision value are
  minimal new types because none exists yet. This core redefines none of the merged vocabulary and modifies no merged
  core.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and carries
  the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party dependency**. Whether
  it lands as a new pure-core module (the established rhythm) or extends an existing core is the only home decision
  left to `/speckit-plan`; the established rhythm suggests a new minimal core.
- **Determinism is the contract, not performance.** A calibration decision is a small computation over a handful of
  supplied facts; there is no latency or throughput target. Uncalibrated-by-default safety, totality over all inputs,
  correct two-threshold logic, the inclusive comparisons, the no-single-sample floor, and the no-hide attribution are
  the guarantees.
- **This is Phase 12's sixth and final row.** With it merged, the phase has cache keying (F035), verdict invalidation
  (F036), prompt isolation (F037), an auditable review record (F038), an advisory-by-default per-finding promotion
  gate (F039), and a judge-vs-human calibration gate — completing the phase toward its exit criteria (agent-reviewed
  outputs auditable and prompt-isolated; missing or stale reviews visible; protected-branch blocking never depending
  on uncalibrated agent judgement). This **closes Phase 12**.
