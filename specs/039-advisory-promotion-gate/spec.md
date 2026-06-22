# Feature Specification: Advisory-to-Blocking Promotion Gate — the Single-Sample-Noise Guardrail

**Feature Branch**: `039-advisory-promotion-gate`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 12: Agent-Reviewed Rule Guardrails** is open. Its **first four**
lines are complete: F035 (`FS.GG.Governance.AgentReviewKey` — the agent-review cache key), F036
(`FS.GG.Governance.VerdictReuse` — the verdict store + invalidation decision), F037
(`FS.GG.Governance.PromptIsolation` — reviewer-prompt isolation), and F038 (`FS.GG.Governance.ReviewRecord` — the
auditable review record). The next unchecked line is the phase's **fifth**: *"Keep agent-reviewed findings
advisory until deterministic backing evidence, repeated-review confidence thresholds, or explicit human sign-off
exists."* This is the design's **single-sample-noise** guardrail (`docs/initial-design.md`, *Optional
agent-reviewed constraints*): *"Single-sample noise → Blocking promotion requires either deterministic backing
evidence, repeated-review confidence thresholds, or explicit human sign-off."* Continuing this repo's
maintainer-confirmed **pure-core-first** rhythm (F015–F038 each landed a pure, total, deterministic core before any
host edge consumed it), this row delivers the **advisory-promotion decision primitive**: a typed value and a
single total, deterministic function that decides whether an agent-reviewed finding may be promoted from advisory
to block-eligible, where the **only** three permitted promotion bases are deterministic backing evidence, a
repeated-review confidence threshold being met, or explicit human sign-off — and which **defaults to advisory**
whenever none holds. It is a **decision** core (the analogue of F023 `deriveEffectiveSeverity` / F030 `decide` /
F036 `lookup`), not a record core: it derives **no** byte-stable identity. It invokes **no model / agent /
network**, reads **no clock / filesystem / git / environment**, computes **no hash from raw bytes**, runs **no
actual review**, makes **no cache lookup / verdict invalidation** (F035 / F036), builds **no review record** (F038,
consumed as input not produced), defines **no judge-vs-human calibration** (the sixth row), performs **no
persistence / JSON projection**, and adds **no CLI**.

## Overview

An agent-reviewed check produces a verdict by judgement, not by deterministic proof. The design is explicit that
such a verdict must **stay advisory by default** and may only ever contribute to *blocking* a boundary once one of
three out-of-band, non-agent guardrails is satisfied — never on the strength of a single agent judgement alone.
The design names those three permitted bases precisely: **deterministic backing evidence**, a **repeated-review
confidence threshold**, or **explicit human sign-off**. This is the *single-sample noise* response: a lone review,
however confident the model sounds, is noise until corroborated, repeated, or human-approved.

This row delivers that gate as a **pure decision over supplied facts**, ahead of any persistence, enforcement
wiring, or CLI. Where F035 keyed *which* verdict a request maps to, F036 decided *whether a cached verdict is still
valid*, F037 shaped the request so the artifact stays data, and F038 captured *what a completed review was*, **this**
core decides *whether an agent-reviewed finding has earned the right to be considered for blocking at all* — and,
by default, says **no**:

| Phase-12 row | Core | Question it answers |
|---|---|---|
| 1 — cache key (F035) | `AgentReviewKey` | *Under what identity is a verdict cached?* |
| 2 — invalidation (F036) | `VerdictReuse` | *Is a cached verdict still valid, and if not, why?* |
| 3 — prompt isolation (F037) | `PromptIsolation` | *How is the request shaped so the artifact is data, not an instruction?* |
| 4 — review record (F038) | `ReviewRecord` | *What was this completed review, for the audit trail?* |
| **5 — advisory promotion (this row)** | **(new pure decision core)** | ***May this agent-reviewed finding be promoted from advisory to block-eligible — and on which permitted basis?*** |

This core makes **no verdict** — F038 already recorded the verdict; this core neither produces, interprets,
compares, nor re-scores it. It decides only the **promotion question**: given a finding that is advisory by default
and the set of promotion bases that are (or are not) present, is the finding **still advisory** or **eligible to
block**, and *why*. It is the gate that makes the phase's exit criterion true — *"protected-branch blocking does
not depend on uncalibrated agent judgement"* — by ensuring an agent's single judgement, on its own, can **never**
turn a finding into a blocker. What **this** row delivers:

- **Advisory by default — the safe-by-construction posture.** With no promotion basis present, the decision is
  *stays advisory*. There is no input, ordering, or default that lets a bare agent-reviewed finding become
  block-eligible. The default is the design's default.
- **Exactly three permitted promotion bases, and no others.** A finding becomes *eligible to block* only when at
  least one of (a) **deterministic backing evidence** corroborates it, (b) a **repeated-review confidence
  threshold** is met (the same verdict reproduced across enough independent reviews to clear a supplied threshold),
  or (c) **explicit human sign-off** is recorded. No fourth basis exists; the model's own confidence is not a
  basis.
- **A total, deterministic decision that names its basis (the no-hide rule).** A single total function over the
  supplied facts returns either *stays advisory* (carrying the reason — no basis present, or a present-but-
  insufficient basis such as too-few confirmations) or *eligible to block* (carrying every permitted basis that
  was satisfied, so the audit trail shows exactly what authorized the promotion). Identical inputs always yield an
  identical decision; nothing is clocked, sensed, or hashed.
- **Eligible-to-block is necessary, not sufficient — calibration is still ahead.** This row decides per-finding
  promotion eligibility only. The sixth row (judge-vs-human calibration) remains a separate prerequisite before any
  agent-reviewed rule may actually block a protected boundary; an *eligible-to-block* decision here never itself
  blocks and never bypasses that calibration gate.

The core is **pure and total over supplied data**, exactly like F023 / F030 / F036: the finding, the presence or
absence of deterministic backing evidence, the confirmation count and its threshold, and the presence or absence of
human sign-off are handed in as already-formed values; nothing is reviewed, clocked, hashed, or persisted. The
**actual agent review**, **producing or interpreting the verdict** (F038 records it; this consumes it as an opaque
fact), the **cache lookup / verdict invalidation** (F035 / F036), the **judge-vs-human calibration** (the sixth
row), the **effective-severity / enforcement derivation** (F023, which this composes *with* downstream but does not
re-implement), any **persistence / JSON projection** of the decision, and any **CLI** all remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An agent-reviewed finding stays advisory by default (Priority: P1)

A Governance component holds an agent-reviewed finding with no corroborating deterministic evidence, no repeated
reviews clearing a confidence threshold, and no human sign-off. It must decide that the finding **stays advisory**
— it may be reported, but it may not block — because a single agent judgement is not deterministic proof.

**Why this priority**: This is the heart of the design's single-sample-noise constraint and the phase's exit
criterion — *"protected-branch blocking does not depend on uncalibrated agent judgement."* If a bare agent-reviewed
finding could become a blocker, the whole guardrail fails. The default must be advisory, by construction, and it is
independently demonstrable from supplied values alone.

**Independent Test**: Supply an agent-reviewed finding together with *no* promotion basis (no backing evidence,
confirmations below threshold or absent, no sign-off) and assert the decision is *stays advisory* with a reason
indicating no permitted basis was satisfied. No model invoked, no I/O.

**Acceptance Scenarios**:

1. **Given** an agent-reviewed finding with no deterministic backing evidence, no repeated-review confirmations,
   and no human sign-off, **When** the promotion decision is made, **Then** the decision is *stays advisory* and
   names the reason as *no permitted promotion basis present*.
2. **Given** the same finding with a confirmation count strictly below the supplied confidence threshold and no
   other basis, **When** the decision is made, **Then** the decision is *stays advisory* — an insufficient count is
   not a basis — and the reason reflects the confidence threshold not being met.
3. **Given** any finding whose only would-be justification is the model's own self-reported confidence (and none of
   the three permitted bases), **When** the decision is made, **Then** the decision is *stays advisory* — model
   self-confidence is never a promotion basis.

---

### User Story 2 - A finding becomes eligible to block on a permitted basis, and the basis is named (Priority: P1)

When at least one of the three permitted bases is present — deterministic backing evidence, a repeated-review
confidence threshold met, or explicit human sign-off — the component must decide that the finding is **eligible to
block**, and the decision must name every permitted basis that was satisfied so the audit trail records exactly what
authorized the promotion.

**Why this priority**: Promotion must be *possible* on the design's own terms, or agent-reviewed rules could never
contribute to blocking even when properly corroborated. Naming the basis is the no-hide rule (the F023 reason / F036
located-cause discipline): an unexplained promotion is as untrustworthy as an unjustified one. It is co-P1 with
Story 1 — together they fix the gate's two outcomes.

**Independent Test**: Supply a finding with exactly one permitted basis satisfied and assert the decision is
*eligible to block* naming that basis; supply a finding with two or three bases satisfied and assert the decision is
*eligible to block* naming all of them; confirm the decision is a deterministic function of the supplied facts.

**Acceptance Scenarios**:

1. **Given** a finding corroborated by deterministic backing evidence and no other basis, **When** the decision is
   made, **Then** it is *eligible to block* and names *deterministic backing evidence* as the satisfied basis.
2. **Given** a finding whose repeated-review confirmation count meets or exceeds the supplied confidence threshold
   and no other basis, **When** the decision is made, **Then** it is *eligible to block* and names *repeated-review
   confidence* as the satisfied basis.
3. **Given** a finding with explicit human sign-off and no other basis, **When** the decision is made, **Then** it
   is *eligible to block* and names *human sign-off* as the satisfied basis.
4. **Given** a finding satisfying two or three permitted bases at once, **When** the decision is made, **Then** it
   is *eligible to block* and names **every** satisfied basis (the no-hide rule), not just the first.

---

### User Story 3 - The decision is total, deterministic, and never blocks on its own (Priority: P2)

The promotion decision must be defined for every combination of supplied facts — present/absent backing evidence,
any confirmation count against any threshold, present/absent sign-off — never throwing and never reading a clock,
file, or model. And an *eligible to block* decision must be understood as *necessary but not sufficient*: it
authorizes consideration for blocking, it does not itself block, and it does not bypass the still-pending
judge-vs-human calibration gate (the sixth row).

**Why this priority**: Totality and determinism are the contract every prior core in this line upholds (F023 / F030
/ F036), and they make the gate trustworthy and testable. Making explicit that eligibility ≠ blocking keeps this
row honestly scoped against the sixth row. It builds on Stories 1–2, so it is P2.

**Independent Test**: Exercise the decision across the full cross-product of basis presence/absence and a range of
confirmation counts straddling the threshold (below, equal, above) and assert it always returns a decision and
never throws; build the decision twice from identical inputs and assert the two decisions are equal; confirm an
*eligible to block* decision carries no blocking action and no calibration claim.

**Acceptance Scenarios**:

1. **Given** any combination of the three bases' presence/absence and any non-negative confirmation count against
   any threshold, **When** the decision is made, **Then** a decision is always returned and the function never
   throws (totality).
2. **Given** the same supplied facts, **When** the decision is made twice, **Then** the two decisions are equal —
   the decision is a total, deterministic function of its inputs (no clock, no model, no I/O).
3. **Given** an *eligible to block* decision, **When** it is inspected, **Then** it represents only promotion
   *eligibility*: it carries no blocking action, asserts no calibration, and does not claim the protected boundary
   may now be blocked (that remains gated by the sixth row).

---

### Edge Cases

- **No basis present at all.** The decision is *stays advisory* with the *no permitted basis* reason — the design's
  default. Never malformed.
- **Confirmation count exactly equal to the threshold.** The confidence basis uses an inclusive *meets-or-exceeds*
  comparison (`>=`), so a count one below the threshold is never satisfied. The inclusive boundary carries one fixed
  exception: a *lone* review never clears the basis (see the next bullet and FR-004), so where a degenerate threshold
  of 0 or 1 would otherwise let a single confirmation satisfy the equal case, it still does **not**. The exact
  comparator is fixed here (`>=`); the threshold's *source* is a supplied value, not parsed by this core.
- **A confidence threshold that a single review would satisfy.** A single agent review must never satisfy the
  confidence basis on its own — that would re-admit single-sample noise. Whether the core enforces a minimum
  threshold (e.g. confirmations must represent more than one independent review) or treats the threshold as a
  supplied policy value is a planning decision; the fixed contract is that the *repeated-review* basis models
  *repeated* reviews, and a lone review never clears it.
- **Confirmation count of zero or absent.** Equivalent to the confidence basis being absent — not satisfied; if no
  other basis holds, the finding stays advisory.
- **Multiple bases satisfied simultaneously.** The decision is *eligible to block* and names all satisfied bases
  (the no-hide rule); it is never ambiguous about which basis or bases applied.
- **Deterministic backing evidence and/or human sign-off represented by supplied tokens.** Their presence is a
  supplied fact (the F029 / F032 / F035 opaque-token discipline — no validation, no dereferencing); whether they
  are modelled as opaque reference tokens (the F030 `EvidenceRef` precedent for backing evidence) or simple
  presence markers is a planning decision. Their mere presence, as supplied, satisfies the corresponding basis.
- **An eligible-to-block finding under an uncalibrated rule pack.** Eligibility here does not imply blocking: the
  sixth-row calibration gate still applies before any agent-reviewed rule may block a protected boundary. This row
  produces eligibility, never a block.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **promotion-decision** value with exactly two outcomes — *stays
  advisory* and *eligible to block* — where *stays advisory* carries the **reason** it was not promoted (no
  permitted basis present, or a present-but-insufficient basis such as a confidence count below threshold) and
  *eligible to block* carries the **set of permitted bases that were satisfied** (the no-hide rule). The value MUST
  make it impossible to be *eligible to block* with an empty set of satisfied bases.
- **FR-002**: The system MUST model exactly **three** permitted promotion bases and no others: **deterministic
  backing evidence**, a **repeated-review confidence threshold met**, and **explicit human sign-off**. The
  model's own self-reported confidence MUST NOT be a basis.
- **FR-003**: The system MUST provide a single **total** decision function that, given an agent-reviewed finding (or
  its identifying facts) and the supplied promotion facts (presence/absence of deterministic backing evidence; a
  repeated-review confirmation count and a confidence threshold; presence/absence of human sign-off), returns the
  promotion-decision value. It MUST be **advisory by default**: when none of the three bases is satisfied, the
  result MUST be *stays advisory*. It MUST be *eligible to block* if and only if at least one basis is satisfied.
- **FR-004**: The **repeated-review confidence** basis MUST be satisfied only when the supplied confirmation count
  **meets or exceeds** the supplied confidence threshold (an inclusive `>=` comparison), modelling *repeated*
  reviews; a count below the threshold MUST NOT satisfy it, and a single review alone MUST NOT satisfy it. *(Whether
  the core enforces a minimum threshold or treats the threshold as a supplied policy value is a planning decision;
  the no-single-sample contract is fixed.)*
- **FR-005**: The decision MUST **name every satisfied basis** in an *eligible to block* outcome and **name the
  reason** in a *stays advisory* outcome — the no-hide rule (the F023 reason / F036 located-cause discipline). No
  promotion may be unexplained and no advisory hold may be unattributed.
- **FR-006**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no filesystem,
  no git, no environment, and no network; it MUST invoke no model / agent, run no review, compute no hash from raw
  bytes, perform no cache-key / verdict-store / lookup / invalidation operation (F035 / F036 own those), build no
  review record (F038, consumed not produced), measure no elapsed time, spawn no process, and persist nothing.
  Identical supplied inputs MUST always yield an identical decision.
- **FR-007**: The core MUST treat the agent-reviewed finding's **verdict as an opaque fact**: it MUST NOT produce,
  interpret, compare, re-score, or threshold the verdict itself (F038 records it; the verdict's meaning is the
  reviewer's). It decides only the **promotion question** over the supplied bases.
- **FR-008**: An *eligible to block* decision MUST be **necessary but not sufficient** for blocking: it MUST carry
  no blocking action and MUST NOT assert that any protected boundary may be blocked. The system MUST NOT define or
  perform any **judge-vs-human calibration** (the sixth row), MUST NOT derive effective severity or an enforcement
  verdict (F023 / F024 own those), and MUST NOT emit any **persistence**, **JSON projection**, or **CLI** surface.
  Its sole output is the typed promotion-decision value.
- **FR-009**: The core MUST **reuse existing typed facts verbatim** where one maps cleanly — concretely, it SHOULD
  reuse the established advisory/blocking vocabulary already modelled in the repo where applicable, and MAY reuse an
  existing opaque reference token for deterministic backing evidence (the F030 `EvidenceRef` precedent) — without
  modifying any merged core. It MUST introduce only the minimal new vocabulary the row needs (the promotion bases,
  the human-sign-off marker, the confidence inputs, and the promotion-decision value). This feature is additive.
  *(Exactly which existing types map, and the shapes of the backing-evidence / sign-off / confidence inputs, are
  planning decisions deferred to `/speckit-plan`.)*
- **FR-010**: If this feature introduces a public F# module, its surface MUST be governed by the repo's `.fsi`-first
  and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1** change (see
  Assumptions). [The concrete module home and name are a planning decision deferred to `/speckit-plan`.]
- **FR-011**: The core MUST NOT add a new third-party package dependency; the decision MUST use only facilities
  already available to the merged cores (the shared framework / BCL) plus reused existing vocabulary.

### Key Entities *(include if feature involves data)*

- **Agent-reviewed finding**: The finding under consideration — an agent-reviewed verdict (F038 vocabulary) treated
  as an opaque fact. *What might be promoted.* Reused/identified, not re-scored.
- **Deterministic backing evidence**: A supplied marker (or opaque reference token, the F030 `EvidenceRef`
  precedent) that a deterministic check independently corroborates the finding — *the first permitted basis*.
- **Repeated-review confidence**: A supplied repeated-review confirmation count together with a supplied confidence
  threshold; the basis is met when the count meets or exceeds the threshold — *the second permitted basis*,
  modelling that a lone review is noise.
- **Human sign-off**: A supplied marker that a human explicitly signed off on the finding — *the third permitted
  basis*.
- **Promotion basis**: The closed three-value vocabulary of permitted bases (deterministic backing evidence,
  repeated-review confidence, human sign-off); the set of those satisfied justifies an *eligible to block* outcome.
- **Advisory reason**: The attribution carried by a *stays advisory* outcome — no permitted basis present, or a
  present-but-insufficient basis (e.g. confidence below threshold) — the no-hide rule for the default.
- **Promotion decision**: The two-outcome value — *stays advisory* (with reason) or *eligible to block* (with the
  non-empty set of satisfied bases) — the gate's verdict on the promotion question.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With no permitted promotion basis satisfied, the decision is *stays advisory* in 100% of cases — a
  bare agent-reviewed finding is never *eligible to block*, and the model's self-reported confidence never promotes.
- **SC-002**: With at least one permitted basis satisfied, the decision is *eligible to block* and names every
  satisfied basis (the no-hide rule), in 100% of cases; with two or three bases satisfied, all are named.
- **SC-003**: The repeated-review confidence basis is satisfied exactly when the supplied confirmation count meets
  or exceeds the supplied threshold and never for a single review alone — verifiable across counts below, equal to,
  and above the threshold in 100% of cases.
- **SC-004**: The decision is total — it returns a decision and never throws for every combination of basis
  presence/absence and every non-negative confirmation count against every threshold — in 100% of cases.
- **SC-005**: For the same supplied facts, making the decision twice yields equal results in 100% of cases
  (determinism), with no model invoked, no review run, no bytes hashed, and nothing persisted — demonstrable across
  different working directories, at different times, and with unrelated repository / filesystem state changed
  between calls (purity).
- **SC-006**: An *eligible to block* decision carries no blocking action and no calibration claim — it is
  necessary-not-sufficient — in 100% of cases.
- **SC-007**: The merged cores and their `surface/*.surface.txt` baselines, and `dotnet build` / `dotnet test` over
  the existing projects, are **unchanged** by this feature except for the additive new surface — no existing
  baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the pure advisory-promotion decision — over supplied values.** Deciding whether an agent-reviewed
  finding is *stays advisory* or *eligible to block*, on the design's three permitted bases, defaulting to advisory,
  is the whole of this row (the design's *single-sample noise* response). The **actual review**, **producing or
  interpreting the verdict**, the **judge-vs-human calibration** (the sixth row), the **effective-severity /
  enforcement derivation** (F023 / F024), any **persistence / JSON projection**, and any **CLI** are out of scope.
  This is a *decision* core (the F023 / F030 / F036 analogue), not a record core — it derives no byte-stable
  identity.
- **Every fact is a SUPPLIED value; this core neither runs reviews nor reads a clock.** The finding, the
  presence/absence of deterministic backing evidence, the repeated-review confirmation count and threshold, and the
  presence/absence of human sign-off are already-formed values handed in by the edge (the F029 / F032 / F035
  opaque-token discipline — no validation, no dereferencing). Sensing whether deterministic evidence exists,
  counting independent reviews, and capturing a human sign-off belong to a later host edge (Principle IV).
- **Advisory is the default, by construction.** There is no input, ordering, or fallback that promotes a finding
  absent a permitted basis. *Eligible to block* requires a non-empty set of satisfied bases; the type makes an
  empty-basis promotion unrepresentable. This encodes the design's *"remain advisory by default until … operational
  guardrails and calibration evidence."*
- **Eligibility is necessary, not sufficient — calibration is the next row.** An *eligible to block* outcome
  authorizes consideration for blocking; it does not block and does not bypass the sixth-row judge-vs-human
  calibration gate. Per the design, *"protected-branch blocking should come from deterministic checks … until
  calibration exists"*; this row keeps that true by never itself blocking.
- **The repeated-review basis models repeated reviews; a single sample is never enough.** The confidence basis is
  met at *count ≥ threshold* (inclusive). Whether the core also enforces a minimum threshold so a single review can
  never clear it, or treats the threshold purely as a supplied policy value, is a planning decision deferred to
  `/speckit-plan`; the no-single-sample contract is fixed here.
- **Reuse existing typed facts verbatim; introduce only the minimal new vocabulary.** The advisory/blocking notion
  already exists in the repo (F023 `Severity = Advisory | Blocking`); whether this core reuses it, references the
  F038 verdict / F030 `EvidenceRef`, or carries thin local markers, and the exact shapes of the backing-evidence,
  sign-off, and confidence inputs, are planning decisions deferred to `/speckit-plan`. The promotion bases, the
  promotion-decision value, and the human-sign-off / confidence inputs are minimal new types because none exists
  yet. This core redefines none of the merged vocabulary and modifies no merged core.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and carries
  the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party dependency**.
  Whether it lands as a new pure-core module (the established rhythm) or extends an existing core is the only home
  decision left to `/speckit-plan`; the established rhythm suggests a new minimal core.
- **Determinism is the contract, not performance.** A promotion decision is a small computation over a handful of
  supplied facts; there is no latency or throughput target. Advisory-by-default safety, totality over all inputs,
  correct three-basis logic, the inclusive confidence comparison, and the no-hide attribution are the guarantees.
- **This is Phase 12's fifth row.** With it merged, the phase has cache keying (F035), verdict invalidation (F036),
  prompt isolation (F037), an auditable review record (F038), and an advisory-by-default promotion gate, leaving
  only judge-vs-human calibration (the sixth row) — toward the phase's exit criteria (agent-reviewed outputs
  auditable and prompt-isolated; missing or stale reviews visible; protected-branch blocking never depending on
  uncalibrated agent judgement).
