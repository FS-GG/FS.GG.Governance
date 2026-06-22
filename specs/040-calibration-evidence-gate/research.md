# Phase 0 Research — Judge-vs-Human Calibration Evidence Gate (F040)

All Technical Context items are resolved; there are **no open NEEDS CLARIFICATION**. The spec defers a small set of
shapes to planning (Assumptions, FR-004, FR-009, Edge Cases); each is decided below. Format per decision: **Decision /
Rationale / Alternatives considered**.

---

## D1 — One new minimal pure-core module (the established rhythm)

**Decision**: Deliver a single new packable library `FS.GG.Governance.Calibration`, compiled `Model.fsi/fs →
Calibration.fsi/fs`, rather than extending a merged core. The operations module is `Calibration` with the decision
named **`decide`**.

**Rationale**: F015–F039 each landed one pure, total, deterministic core per implementation-plan row before any host
edge consumed it; the spec calls this row "the analogue of F023 `deriveEffectiveSeverity` / F030 `decide` / F036
`lookup` / F039 `decide`". A new minimal core keeps the addition isolated and additive (SC-007) and keeps the merged
cores' baselines untouched. `decide` mirrors the F030 / F039 operation name exactly (the spec's named precedent).

**Alternatives considered**: *Extend `AdvisoryPromotion` (F039).* Rejected — F039 owns the *per-finding* eligibility
question and is deliberately scoped to it; calibration is the *rule-pack-level* prerequisite that composes *with*
F039 downstream (FR-008), not a second mode of the same decision. Folding it in would conflate two distinct decisions
and rewrite a merged baseline. *Extend `Enforcement` (F023/F024).* Rejected — F023's domain is effective-severity /
enforcement-verdict derivation; calibration is upstream of and distinct from enforcement, and a *calibrated* outcome
is explicitly **not** an enforcement verdict (FR-008, D7). *Extend `VerdictReuse` (F036) / `ReviewRecord` (F038).*
Rejected — F036 owns cache validity and F038 owns the audit record; neither owns the calibration question.

---

## D2 — The decision is a function of the supplied thresholds and supplied evidence; no review run, no clock read

**Decision**: `decide : CalibrationThresholds -> CalibrationEvidence -> CalibrationDecision`, two curried parameters:
the **policy** (a minimum comparison-sample count + a minimum agreement level) and the per-reviewer **evidence** (the
comparison samples, scoped to a judge identity, plus the observed agreement level). The actual review, the actual
human comparison, counting samples over time, and sensing recency are **not** performed here — every fact is a
supplied, already-formed value.

**Rationale**: This is the exact shape of the named precedents — F030 `decide` takes the candidate inputs + store;
F023 `deriveEffectiveSeverity` takes the levers. Two curried parameters keep policy (reusable across reviewers) and
evidence (per-reviewer) cleanly separable, which is how an edge would call it (one threshold set against many
reviewers' evidence). FR-003 fixes the inputs as "the comparison samples, **or** their summarised count and observed
agreement level" and "the supplied calibration thresholds (a minimum comparison-sample count and a minimum agreement
level)"; the Assumptions fix that "every fact is a SUPPLIED value; this core neither runs reviews nor consults a human
nor reads a clock."

**Alternatives considered**: *Bundle thresholds + evidence into one `CalibrationInput` record (the F023
`EnforcementInput` / F039 `PromotionFacts` shape).* A reasonable variant, but rejected as needless nesting here:
thresholds are *policy* and evidence is *per-reviewer data* with different lifecycles, so two parameters read more
honestly than one record fusing them. *A single summarised `{ SampleCount; ObservedAgreement }` input with no sample
list.* Rejected — FR-002 MUST model calibration as "a collection of comparison samples"; dropping the list would lose
the FR-002 vocabulary and make the honest derived sample count (D4) impossible.

---

## D3 — Reuse F035 identity + F038 `RecordedVerdict`; mint the rest (the FR-009 reuse decision)

**Decision**: Reuse **F035** `ModelId` / `ModelVersion` / `ReviewerPromptHash` (from
`FS.GG.Governance.AgentReviewKey.Model`) **verbatim** for the per-judge calibration scope (`JudgeIdentity`), and reuse
**F038** `RecordedVerdict` (from `FS.GG.Governance.ReviewRecord.Model`) **verbatim** for the two opaque verdicts a
comparison sample pairs. All other vocabulary is new and minimal: the `AgreementClassification` union, the
`JudgeIdentity` scope record, the `ComparisonSample` record, the `SampleCount` / `AgreementLevel` newtypes, the
`CalibrationEvidence` / `CalibrationThresholds` / `CalibrationMetrics` records, the `CalibrationReason` union, and the
`CalibrationDecision` outcome. The core references `FS.GG.Governance.AgentReviewKey` and
`FS.GG.Governance.ReviewRecord`; the transitive pure cores (`PromptIsolation`, `SensedMetadata`, `FreshnessKey`,
`Config`) arrive through F038 but are unused here.

**Rationale**: FR-009 **SHOULD** "reuse the established judge/prompt identity vocabulary already modelled in the repo
(F035 `ModelId` / `ModelVersion` / `ReviewerPromptHash`) for the per-judge calibration scope" and **MAY** "reuse the
recorded-verdict vocabulary (F038 `RecordedVerdict`) for the per-sample verdicts". The Key Entities entry for the
comparison sample is explicit: "The judge and human verdicts are opaque facts **(F038 vocabulary)**." Reusing both
honors FR-009's "reuse existing typed facts verbatim … introduce only the minimal new vocabulary" and lets the
`ComparisonSample` faithfully model FR-002's "pairing the agent reviewer's verdict with a human's verdict" *without
minting a redundant verdict type* (which FR-009 declines to list among the new vocabulary). Both reused tokens are
opaque single-case strings that this core never parses, validates, or dereferences (FR-007, the F029/F035 opaque-token
discipline).

**Alternatives considered**: *Identity-only footprint — reference **only** F035 `AgentReviewKey` (the small
`AgentReviewKey → FreshnessKey → Config` chain, matching F039's three-deep depth), carry only the
`AgreementClassification` per sample, and **omit** the opaque verdicts entirely* (since `decide` never inspects them —
the F039 D2 dependency-minimalism precedent: "don't pull a dependency chain to name a subject the decision never
inspects"). This is the leanest option and was seriously weighed. Rejected because FR-002 MUST model the sample as
"pairing the … verdict with a human's verdict," Key Entities designates those verdicts as "**(F038 vocabulary)**," and
FR-009 says reuse-verbatim / mint-minimal — so carrying the paired verdicts via the *existing* `RecordedVerdict` is
the most faithful reading, at the cost of the (pure, unused) F038 transitive chain. *Mint a new local `Verdict of
string`, zero F038 reference.* Rejected — it duplicates a token the spec points to (F038 `RecordedVerdict`) and
violates FR-009's "introduce only the minimal new vocabulary." *Reuse F023 `Severity = Advisory | Blocking` for the
outcome.* Rejected — a *calibrated* decision is **not** a `Blocking` severity (FR-008); reusing `Severity` would erase
the maturity/blocking distinction this row exists to preserve (mirrors F039 D7's reasoning).

---

## D4 — The comparison sample carries the agreement classification (consumed) + the opaque verdicts; the sample count is derived, the agreement level is supplied

**Decision**: `AgreementClassification = Agreeing | Disagreeing` is the per-sample already-classified fact, and the
**only** thing `decide` consumes from a sample. `ComparisonSample = { JudgeVerdict: RecordedVerdict; HumanVerdict:
RecordedVerdict; Agreement: AgreementClassification }`. `CalibrationEvidence = { Scope: JudgeIdentity; Samples:
ComparisonSample list; ObservedAgreement: AgreementLevel }`. The **comparison-sample count is derived** from
`List.length Samples` (`observedSampleCount`), the honest count of evidence actually present; the **observed agreement
level is supplied** as `ObservedAgreement: AgreementLevel`. `SampleCount = SampleCount of int` and `AgreementLevel =
AgreementLevel of int` are single-case newtypes on the edge's own opaque scale.

**Rationale**: FR-007 fixes the verdicts as opaque facts "never produced, interpreted, compared, re-scored, or
thresholded"; the consumed fact is the *already-classified agreement* (FR-002, FR-007, Key Entities). Deriving the
sample count from the actual list makes "no evidence" (`Samples = []`) and "too few samples" (count below the
effective minimum) **honest** — a reviewer cannot over-claim how many samples back it. The agreement *level*, by
contrast, is a *rate-like summary on a scale the edge owns* (percent, permille, basis points — the spec leaves "the
exact representation of the agreement level" a planning decision), so the core consumes it as a supplied integer and
compares with `>=`, exactly as F039 consumes its supplied `ConfirmationCount` / `ConfidenceThreshold` integers. This
keeps the core pure, total, and **float-free** (no division, no float-equality fragility) while honoring FR-003's
"their summarised count and observed agreement level … handed in by the edge." Newtypes (not bare `int`) preserve the
repo's type-distinctness discipline (a swapped count/level is a compile error).

**Alternatives considered**: *Derive the agreement level from the samples by counting `Agreeing` over the total.*
Rejected — "agreement level" is a *level* whose scale (rate? percentage? smoothed?) is an edge policy the spec
declines to fix here; deriving a rate forces either floats (determinism-fragile) or a fraction comparator
(cross-multiplication complexity) for no faithfulness gain, since the edge already summarises agreement. *Supply the
sample count too (not derive it).* Rejected — a supplied count could disagree with the actual `Samples` length, an
avoidable "hide"; deriving it keeps the count and the evidence in lockstep. *Bare `int` count/level.* Rejected — loses
type distinctness and invites argument-order bugs.

---

## D5 — The calibration reason: three flavours, each carrying its numbers; metrics named on the calibrated side

**Decision**: `CalibrationReason = NoCalibrationEvidence | TooFewSamples of SampleCount * SampleCount |
AgreementBelowThreshold of AgreementLevel * AgreementLevel`. `decide` returns, in precedence order:
`Uncalibrated NoCalibrationEvidence` when there are no samples at all; else `Uncalibrated (TooFewSamples (observed,
required))` when the derived count is below the effective minimum; else `Uncalibrated (AgreementBelowThreshold
(observed, required))` when the supplied agreement is below the threshold; else `Calibrated metrics`. The *calibrated*
side carries `CalibrationMetrics = { ObservedSamples; RequiredSamples; ObservedAgreement; RequiredAgreement }` so the
audit trail records exactly what cleared the gate (the no-hide rule). In both `TooFewSamples` and `CalibrationMetrics`,
`required`/`RequiredSamples` is the **effective** minimum applied, `max(MinimumSamples, 2)` (D7), so the named bar is
truthful even under a degenerate supplied minimum.

**Rationale**: FR-005 requires the *uncalibrated* outcome to **name its reason** and the *calibrated* outcome to
**name the satisfied metrics** (the no-hide rule — the F023 reason / F036 located-cause / F039 named-basis
discipline). The three reason flavours map one-to-one to the spec's three cited uncalibrated cases (US1 scenarios 1–3
/ Edge Cases): no evidence, too few samples, agreement below threshold. The precedence order matches the acceptance
scenarios (no-evidence first, then sample-count, then agreement). Carrying the observed + required numbers in each
makes every hold and every clearance concrete and testable.

**Alternatives considered**: *A single opaque string reason.* Rejected — uninspectable, defeats the no-hide property.
*A bare `Calibrated` flag with no metrics.* Rejected — US2 scenario 3 explicitly wants "the satisfied metrics, not
merely a bare 'calibrated' flag." *Report the supplied `MinimumSamples` (not the effective `max(min, 2)`) in the
reason/metrics.* Rejected — under a supplied minimum below 2 it would understate the actual bar the no-single-sample
floor enforces; reporting the effective minimum keeps the attribution honest (D7).

---

## D6 — `CalibrationDecision` is two-outcome; calibrated is unrepresentable without metrics

**Decision**: `CalibrationDecision = Uncalibrated of CalibrationReason | Calibrated of CalibrationMetrics`. The
`Calibrated` case **always** carries a `CalibrationMetrics` payload (the satisfied thresholds), so a *calibrated*
decision without the thresholds having been met is **unrepresentable** in the type.

**Rationale**: FR-001 requires the value to "make it impossible to be *calibrated* without the supplied thresholds
having been met." Encoding the satisfied metrics *into* the `Calibrated` case (rather than a bare flag) is the
idiomatic, total way to make that guarantee structural: the only way to obtain a `Calibrated` value is through
`decide`, which constructs it solely on the success branch after both gates pass, and the value then exposes exactly
which observed/required numbers cleared them. `Uncalibrated` symmetrically always carries its `CalibrationReason`
(D5). This mirrors F039's `StaysAdvisory of AdvisoryReason | EligibleToBlock of …` two-outcome encoding.

**Alternatives considered**: *`Calibrated` as a bare case (no payload) + a separate metrics accessor.* Rejected — a
payload-free `Calibrated` is constructible by any consumer (via the generated `Calibrated` constructor) without
metrics, so the type would not *make it impossible*; embedding the metrics enforces it. *A `bool` result.* Rejected —
loses the no-hide reason/metrics entirely (FR-005).

---

## D7 — The comparator: inclusive `>=` on both gates **and** a no-single-sample floor; calibrated ≠ blocking

**Decision (comparator)**: Let `observed = List.length evidence.Samples`, `min = MinimumSamples`, `effectiveMin =
max(min, 2)`, `obs = ObservedAgreement`, `req = MinimumAgreement`. The calibration basis is satisfied **iff `observed
>= effectiveMin && obs >= req`**. Both comparisons are inclusive `>=` (FR-004); the `max(min, 2)` floor guarantees a
**lone sample never calibrates** regardless of a misconfigured minimum of 0 or 1, and a count one below the effective
minimum is never sufficient.

**Decision (calibrated ≠ blocking)**: `CalibrationDecision` carries **no** blocking action, severity, effective
severity, enforcement verdict, or F039 eligibility field. A `Calibrated` value asserts only beyond-advisory
*maturity* — necessary, not sufficient. The core references neither `Enforcement` (F023/F024) nor `AdvisoryPromotion`
(F039).

**Rationale (comparator)**: FR-004 fixes the inclusive `>=` on both the sample-count and the agreement comparison and
fixes that "a single comparison sample alone MUST NOT satisfy it," while deferring "whether the core enforces a fixed
minimum-sample floor or treats the minimum purely as a supplied threshold." Enforcing the floor on the **derived
count** (`>= 2` = genuinely periodic comparison) directly encodes the no-single-sample contract without constraining
where the supplied minimum comes from. For any sensible policy (`min >= 2`) the floor is redundant and behaviour
reduces to exactly `observed >= min` (so SC-003's below/equal/above checks hold); for a degenerate `min <= 1` the
floor still blocks a single sample. The comparator is total over all counts and levels (including negative/degenerate
supplied integers — it never throws).

**Rationale (calibrated ≠ blocking)**: FR-008 / SC-006 / US3 require that a *calibrated* decision never itself blocks
and that blocking still composes F039 per-finding eligibility, this calibration, and the deterministic enforcement
machinery (F023/F024). Modelling the outcome as a bare two-case decision value — with no severity, action, or
eligibility payload — makes "necessary but not sufficient" true *by construction*: there is nothing in the value that
could be mistaken for an authorization to block. This is also why D1 keeps the core out of `Enforcement` and out of
`AdvisoryPromotion`.

**Alternatives considered (comparator)**: *Floor the supplied minimum (`MinimumSamples := max(MinimumSamples, 2)`
silently and report it as the supplied value).* Rejected for *reporting* — it would hide that the floor moved the bar;
instead the effective minimum is computed and **named** in the reason/metrics (D5). *Treat the minimum as pure policy
with no floor (`observed >= min` only).* Rejected — a supplied `min = 1` would then let a lone comparison calibrate,
re-admitting the single-sample noise the row exists to stop (the F039 single-sample-noise discipline lifted to
calibration). *Exclusive `>` on either gate.* Rejected — FR-004 fixes inclusive `>=` (an agreement or count exactly at
the threshold calibrates; US2 scenario 2).

---

## D8 — Recency / staleness is deliberately **not** modelled in this row

**Decision**: This core models **no** recency requirement: `CalibrationReason` has no `Stale` case,
`CalibrationThresholds` has no freshness window, and `CalibrationEvidence` carries no supplied-recency fact. Staleness
of calibration evidence is treated as a **separate downstream concern**.

**Rationale**: The spec explicitly makes recency a planning decision: "Whether this row models a recency requirement …
or treats staleness as a separate downstream concern is a planning decision." Deferring it keeps this core minimal
(Principle III) and tightly scoped to the **two** thresholds the design's calibration basis names (a minimum
comparison-sample count and a minimum agreement level). Modelling recency would add a `SensedMetadatum`-style
supplied-recency input + a supplied freshness window + a `Stale` reason — vocabulary not required to make the
calibration basis correct, and best owned by the freshness machinery (F029/F034) at the edge where the F034
sensed-metadata discipline already lives. If a later row needs recency, it composes a freshness check *before*
`decide` (filtering stale samples out of the supplied evidence) or *after* it, without changing this core's contract.

**Alternatives considered**: *Model recency now (supplied recency + supplied window + `Stale` reason, never a clock
read — the F034 discipline).* A spec-permitted variant, rejected for this row as premature: it widens the surface for
a concern the calibration basis does not require, and the spec's fixed contracts (no-single-sample, per-identity,
inclusive `>=`) do not include recency. Keeping it out matches F039's restraint (it "deliberately stopped short" of
the next row's concern).
