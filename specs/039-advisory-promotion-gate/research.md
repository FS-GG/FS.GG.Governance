# Phase 0 Research — Advisory-to-Blocking Promotion Gate (F039)

All Technical Context items are resolved; there are **no open NEEDS CLARIFICATION**. The spec defers a small set of
shapes to planning (Assumptions); each is decided below. Format per decision: **Decision / Rationale / Alternatives
considered**.

---

## D1 — One new minimal pure-core module (the established rhythm)

**Decision**: Deliver a single new packable library `FS.GG.Governance.AdvisoryPromotion`, compiled `Model.fsi/fs →
AdvisoryPromotion.fsi/fs`, rather than extending a merged core. The operations module is `AdvisoryPromotion` with
the decision named **`decide`**.

**Rationale**: F015–F038 each landed one pure, total, deterministic core per implementation-plan row before any host
edge consumed it; the spec calls this row "the analogue of F023 `deriveEffectiveSeverity` / F030 `decide` / F036
`lookup`". A new minimal core keeps the addition isolated and additive (SC-007) and keeps the merged cores'
baselines untouched. `decide` mirrors F030's operation name exactly (the spec's named precedent).

**Alternatives considered**: *Extend `Enforcement` (F023/F024).* Rejected — F023's domain is effective-severity /
enforcement-verdict derivation; the promotion question is upstream of and distinct from severity (an *eligible to
block* finding is **not** a `Blocking` severity — see D7). Folding it in would couple two unrelated decisions and
rewrite a merged baseline. *Extend `VerdictReuse` (F036).* Rejected — F036 owns cache validity, not promotion; the
spec is explicit this core "makes no cache lookup / verdict invalidation".

---

## D2 — The decision is a function of the supplied bases, not of the finding's identity

**Decision**: `decide : PromotionFacts -> PromotionDecision`, where `PromotionFacts` bundles **only** the three
bases' inputs (backing evidence, confirmation count + threshold, sign-off). The agent-reviewed *finding* itself is
**not** a parameter and gets **no new type**; the caller associates the returned decision with its finding.

**Rationale**: This is the exact shape of the named precedents — F023 `deriveEffectiveSeverity: EnforcementInput ->
EnforcementDecision` takes the *levers*, not a finding id; F030 `decide` takes the candidate inputs + store. The
promotion outcome depends only on which bases are satisfied (FR-003); the finding's identity changes nothing.
FR-003's "given an agent-reviewed finding (**or its identifying facts**)" and FR-009's enumeration of new vocabulary
(which omits any finding type) both license leaving the finding to the caller. This keeps the core minimal
(Principle III) and avoids an unused field or a heavy dependency.

**Alternatives considered**: *Carry an opaque `FindingRef` field in `PromotionFacts`.* Rejected — it would be read
by nothing in the decision (an unused lever), and FR-009 does not list a finding token among the new vocabulary.
*Reuse F038 `RecordedVerdict` / `RecordIdentity` to name the finding.* Rejected — it pulls the whole `ReviewRecord`
dependency chain (→ PromptIsolation, SensedMetadata, AgentReviewKey, FreshnessKey, CommandRecord, Config) to name a
subject the decision never inspects, violating dependency-minimalism for zero logic benefit. FR-007 (verdict is an
opaque fact, never interpreted) is honoured precisely *because* the verdict never enters the core.

---

## D3 — Reuse F030 `EvidenceRef` for the backing-evidence basis; mint the rest

**Decision**: The deterministic-backing-evidence basis is supplied as `EvidenceRef option`
(`FS.GG.Governance.EvidenceReuse.Model.EvidenceRef`, reused verbatim) — `Some _` satisfies the basis, `None` does
not; the core never parses, validates, or dereferences it. All other vocabulary is new and minimal: the closed
three-value `PromotionBasis`, the `ConfirmationCount` / `ConfidenceThreshold` newtypes, the `SignOff` marker, the
`AdvisoryReason`, the `PromotionFacts` bundle, and the `PromotionDecision` outcome.

**Rationale**: FR-009 explicitly names "the F030 `EvidenceRef` precedent for backing evidence" and enumerates the
new vocabulary as *the promotion bases, the human-sign-off marker, the confidence inputs, and the promotion-decision
value* — pointedly **omitting** a backing-evidence token, signalling reuse. `EvidenceRef` *is* an opaque handle to
deterministic evidence (F030's own doc: "an opaque handle to already-recorded evidence … never parsed, validated,
produced, or dereferenced"), so it maps cleanly. One sibling reference (`AdvisoryPromotion → EvidenceReuse`) mirrors
the F036 single-sibling shape; the transitive `FreshnessKey` / `Config` cores are pure and unused here.

**Alternatives considered**: *Mint a local `BackingEvidenceRef of string`, zero project references (BCL +
FSharp.Core only).* Tempting for the cleanest dependency story, but rejected — it introduces a fourth token FR-009
declines to list as new and duplicates a token the spec explicitly tells us to reuse; the 3-deep pure-core chain is
already an established pattern (F036's `VerdictReuse → AgentReviewKey → FreshnessKey → Config`). *Model the sign-off
as a plain `bool`.* Rejected — an opaque `SignOff of string` marker keeps the no-hide audit trail meaningful (which
sign-off) and matches the repo's strong-typing-over-primitives idiom, while still satisfying the basis by mere
presence.

---

## D4 — Confidence inputs: two newtypes; presence-by-`option` for the other two bases

**Decision**: The confidence basis is supplied as `Confirmations: ConfirmationCount` and `ConfidenceThreshold:
ConfidenceThreshold` (both `… of int`). Backing evidence and sign-off are supplied as `EvidenceRef option` and
`SignOff option`; **presence** (`Some`) is what satisfies those two bases.

**Rationale**: Counts and thresholds are genuinely integers, so newtypes (not a bare `int`) preserve the repo's
type-distinctness discipline and make a swapped argument a compile error. Presence-by-`option` is the simplest total
model of "a marker is / isn't supplied" and needs no sentinel. A small flat record (`PromotionFacts`) bundles the
four supplied facts, exactly as F023's `EnforcementInput` bundles its levers.

**Alternatives considered**: *Bare `int` count/threshold.* Rejected — loses type distinctness and invites
argument-order bugs. *A `ConfidenceInputs` sub-record.* Rejected as needless nesting for two fields.

---

## D5 — The advisory reason: two flavours, the insufficient one carries its numbers

**Decision**: `AdvisoryReason = NoPermittedBasis | ConfidenceBelowThreshold of ConfirmationCount *
ConfidenceThreshold`. When no basis is satisfied, `decide` returns `StaysAdvisory (ConfidenceBelowThreshold (c, t))`
iff a review was attempted but fell short (confirmation count `≥ 1` yet the confidence basis is unmet); otherwise
`StaysAdvisory NoPermittedBasis`.

**Rationale**: FR-005 / US1 require the *stays advisory* outcome to **name its reason**, and US1 scenario 2 wants
the reason to "reflect the confidence threshold not being met". Two crisp flavours cover the spec's two cited cases
("no permitted basis present" vs "a present-but-insufficient basis such as a confidence count below threshold").
Carrying `(count, threshold)` in `ConfidenceBelowThreshold` makes the attribution concrete and testable. A count of
zero/absent means "no review attempted" ⇒ `NoPermittedBasis` (Edge Cases: "Confirmation count of zero or absent …
equivalent to the confidence basis being absent").

**Alternatives considered**: *A single opaque string reason.* Rejected — uninspectable, defeats the no-hide
property. *Enumerate every unmet basis.* Rejected as over-modelled — the spec names exactly the two reason flavours;
the satisfied-basis set already carries the positive attribution.

---

## D6 — `EligibleToBlock` is non-empty by construction; bases are named in a fixed order

**Decision**: `PromotionDecision = StaysAdvisory of AdvisoryReason | EligibleToBlock of PromotionBasis *
PromotionBasis list`. The `EligibleToBlock` payload is a **head + tail** (`PromotionBasis * PromotionBasis list`),
so it is impossible to construct with zero satisfied bases. `decide` builds the satisfied-basis list in the fixed
order **DeterministicBackingEvidence, RepeatedReviewConfidence, HumanSignOff** and, when non-empty, splits it into
head + tail.

**Rationale**: FR-001 requires that *eligible to block* with an empty basis set be **unrepresentable**; F# DUs can't
express "non-empty list" directly, and the head+tail encoding is the idiomatic, total way to guarantee `≥ 1`
(`satisfiedBases` reconstitutes `head :: tail`). A fixed deterministic order makes the all-named property (US2
scenario 4 / SC-002) byte-stable and testable. `satisfiedBases : PromotionDecision -> PromotionBasis list` returns
`[]` for advisory and the full list for eligible, giving callers/tests one uniform accessor.

**Alternatives considered**: *`EligibleToBlock of PromotionBasis list` with a smart constructor guarding
non-emptiness.* Rejected — a plain `list` case is constructible empty by any consumer (e.g. via the generated
`NewEligibleToBlock`), so the type would not *make it impossible*; head+tail enforces it in the type. *A `Set` of
bases.* Rejected — `Set` reorders/loses the fixed naming order and adds comparison machinery for a max-three-element
collection; an ordered list is simpler and the elements are already distinct by construction.

---

## D7 — The confidence comparator: inclusive `>=` **and** a no-single-sample floor; eligibility ≠ blocking

**Decision (comparator)**: The repeated-review confidence basis is satisfied **iff `count >= threshold && count >=
2`**. The `count >= threshold` is the inclusive comparison FR-004 fixes; the additional `count >= 2` is the floor
that guarantees a lone review never clears the basis regardless of a misconfigured threshold of 0 or 1.

**Decision (eligibility ≠ blocking)**: `PromotionDecision` carries **no** blocking action, severity, or calibration
field. An `EligibleToBlock` value asserts only promotion *eligibility* — necessary, not sufficient. The core
references neither `Enforcement` (F023/F024 severity) nor any calibration vocabulary (the sixth row).

**Rationale (comparator)**: FR-004 fixes the inclusive `>=` and the no-single-sample contract but defers *whether
the core enforces a minimum threshold or treats the threshold as a supplied policy value*. Enforcing the floor on
the **count** (`≥ 2` = genuinely repeated reviews) directly encodes "a single review alone MUST NOT satisfy it"
without constraining the supplied threshold's source. For any sensible policy (`threshold ≥ 2`) the floor is
redundant and behaviour reduces to exactly `count >= threshold` (so SC-003's below/equal/above checks hold); for a
degenerate `threshold ≤ 1` the floor still blocks a single sample. The comparator is total over all non-negative
counts and thresholds.

**Rationale (eligibility ≠ blocking)**: FR-008 / SC-006 / US3 require that an *eligible to block* decision never
itself blocks and never bypasses the sixth-row judge-vs-human calibration gate. Modelling the outcome as a bare
two-case decision value — with no severity, action, or calibration payload — makes "necessary but not sufficient"
true *by construction*: there is nothing in the value that could be mistaken for an authorization to block. This is
also why D1 keeps the core out of `Enforcement`: reusing `Severity = Advisory | Blocking` for the outcome would
erase the eligibility/blocking distinction the spec is built to preserve.

**Alternatives considered (comparator)**: *Floor the threshold (`max(threshold, 2)`).* Rejected — it silently
rewrites the supplied policy value rather than reading it; flooring the count leaves the threshold untouched and
still guarantees the contract. *Treat the threshold as pure policy with no floor (`count >= threshold` only).*
Rejected — a supplied `threshold = 1` would then let a single review promote, re-admitting single-sample noise the
row exists to stop.
