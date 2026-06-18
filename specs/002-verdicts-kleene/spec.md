# Feature Specification: Verdicts — Three-Valued Kleene Composition

**Feature Branch**: `002-verdicts-kleene`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F02 · `002-verdicts-kleene` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces a new public type (`Verdict`) and its combinator surface on the governance kernel (new `.fsi` contract additions, surface-area baseline update). Pure values and total functions; no agent, I/O, or domain vocabulary.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the engineers and downstream features that build on the governance kernel: the reified `Check` algebra and its interpreters (F03), the `CheckTier`/rule bridge (F04), and the routing layer (F07). They need a small, trustworthy verdict algebra so that a rule whose answer is "a competent judge has not yet ruled" composes cleanly with rules that have a definite pass/fail answer — and so that combining sub-results never depends on the order they were written or evaluated.

### User Story 1 - Combine sub-results without losing "still uncertain" (Priority: P1)

A consumer has several sub-verdicts — some definitely pass, some definitely fail, and some that an agent or human must still decide — and combines them with an "all of these must hold" (conjunction) or "at least one of these must hold" (disjunction) operation. The combined result preserves the genuinely undecided state: an unresolved sub-verdict is never silently treated as a pass or as a fail.

**Why this priority**: This is the whole reason verdicts are three-valued. The project's central distinction is that "an agent must still decide this" (`Uncertain`) is *not* the same as "this failed" — routing turns the former into a review request and the latter into a block. If combination collapsed `Uncertain` into pass or fail, the kernel would either approve unreviewed work or reject reviewable work, and the agent-review tier (F04/F08) would be meaningless. It is the minimum viable verdict algebra.

**Independent Test**: Combine a mix of pass, fail, and undecided sub-verdicts under both conjunction and disjunction, and confirm: a conjunction containing a fail yields fail; a conjunction with no fail but at least one undecided yields undecided; a disjunction containing a pass yields pass; a disjunction with no pass but at least one undecided yields undecided. In no case does an undecided input vanish into a pass or fail unless a dominating result (a fail under conjunction, a pass under disjunction) is genuinely present.

**Acceptance Scenarios**:

1. **Given** several sub-verdicts under conjunction where at least one is a definite fail, **When** the consumer combines them, **Then** the result is a fail (a definite fail dominates conjunction, even if other inputs are undecided).
2. **Given** several sub-verdicts under conjunction where none fails but at least one is undecided, **When** the consumer combines them, **Then** the result is undecided (not a pass).
3. **Given** several sub-verdicts under disjunction where at least one is a definite pass, **When** the consumer combines them, **Then** the result is a pass (a definite pass dominates disjunction, even if other inputs are undecided).
4. **Given** several sub-verdicts under disjunction where none passes but at least one is undecided, **When** the consumer combines them, **Then** the result is undecided (not a fail).
5. **Given** a set of sub-verdicts that are all passes, **When** combined under conjunction, **Then** the result is a pass; **and** a set that are all fails, **When** combined under disjunction, **Then** the result is a fail.

---

### User Story 2 - Get the same combined verdict regardless of order (Priority: P1)

A consumer who lists the same sub-verdicts in a different order, or nests the same combinations differently, receives an identical combined verdict — the same pass/fail/undecided outcome **and** the same explanatory reason text. The outcome depends only on the set of sub-verdicts, never on their incidental arrangement.

**Why this priority**: Order-independence is what makes a governance verdict reproducible and cacheable. The kernel's fact derivation is already order-independent (F01); the verdict algebra layered on top must be too, or the same facts could produce two different rendered outcomes depending on how a rule author happened to nest their clauses. This closes a known confluence hazard (a commutative combination reporting an order-dependent reason message) at the algebra level, so every later interpreter inherits the guarantee for free.

**Independent Test**: Take a fixed multiset of sub-verdicts, combine it under conjunction (and separately under disjunction) in several shuffled orders and several re-nestings, and confirm every arrangement yields byte-for-byte the same combined verdict, including the reason string.

**Acceptance Scenarios**:

1. **Given** a fixed multiset of sub-verdicts, **When** the consumer combines them under conjunction in two different orders, **Then** the resulting verdicts are identical, including their reason text.
2. **Given** the same multiset, **When** combined under disjunction in two different orders, **Then** the resulting verdicts are identical, including their reason text.
3. **Given** three or more sub-verdicts, **When** the consumer regroups the same combination (e.g. combining the first two and then the third, versus the last two and then the first), **Then** the result is identical (the combination is associative).
4. **Given** a combination that resolves to a fail (or undecided) drawing on more than one contributing reason, **When** the consumer reads the combined reason, **Then** it reflects the contributing reasons in a deterministic, order-independent form rather than "whichever clause happened to be evaluated first".

---

### User Story 3 - Negate a verdict (Priority: P2)

A consumer can flip a verdict's polarity: a pass becomes a fail and a fail becomes a pass, while an undecided verdict stays undecided (there is nothing definite to flip). This supports rules expressed as "this condition must NOT hold" and the implication form built on it.

**Why this priority**: Negation is required for the `Not` and `Implies` cases of the reified `Check` algebra (F03), but it is a thin, total operation on top of the core conjunction/disjunction story, so it rides one priority below the combination MVP. It is included here so the full Kleene operator set (`and`, `or`, `negate`) ships as one coherent algebra rather than being split across features.

**Independent Test**: Negate each of the three verdict kinds and confirm pass and fail swap while undecided is unchanged; negate twice and confirm the original pass/fail *tag* is recovered (the verdict itself recovers exactly for an undecided verdict and for a reasonless pass/fail; a reasoned fail recovers its tag but not its reason — the reason is dropped at the flip).

**Acceptance Scenarios**:

1. **Given** a passing verdict, **When** the consumer negates it, **Then** the result is a failing verdict.
2. **Given** a failing verdict, **When** the consumer negates it, **Then** the result is a passing verdict.
3. **Given** an undecided verdict, **When** the consumer negates it, **Then** the result is still undecided (an unresolved judgement has no definite polarity to flip).

---

### Edge Cases

- **Empty combination**: Combining zero sub-verdicts under conjunction yields a pass (the identity of "all must hold" — there is nothing to violate); combining zero under disjunction yields a fail (the identity of "at least one must hold" — nothing satisfied it). Neither errors. This keeps combination associative when sub-lists are empty.
- **Single sub-verdict**: Combining exactly one sub-verdict under either conjunction or disjunction returns that sub-verdict unchanged (including its reason).
- **All undecided**: Combining only undecided sub-verdicts (under either operation) yields an undecided result, never a pass or fail.
- **Reason on a non-failing outcome**: A pass carries no reason; an undecided verdict carries the reason explaining what is still pending. When a dominating result is reached (a fail under conjunction, a pass under disjunction), only the contributing reasons of that dominating kind shape the combined reason — passes contribute no text.
- **Double negation of undecided**: Negating an undecided verdict twice leaves it undecided (it never had a polarity); negating pass/fail twice recovers the original pass/fail *tag* (exactly recovering the original verdict for a reasonless pass/fail, since a fail's reason is not carried back through the flip).
- **Reason determinism under duplication**: If two sub-verdicts contribute identical reason text, the combined reason does not depend on how many duplicates appeared or where they sat in the list (deterministic de-duplication / ordering), so the result stays reproducible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST provide a three-valued verdict value with exactly three cases: a pass (success, no reason), a fail (carrying a human-readable reason), and an undecided/"uncertain" verdict (carrying a human-readable reason describing what is still pending).
- **FR-002**: The kernel MUST provide a conjunction ("all must hold") combination over a collection of verdicts with these semantics: if any input is a fail, the result is a fail; otherwise if any input is undecided, the result is undecided; otherwise the result is a pass.
- **FR-003**: The kernel MUST provide a disjunction ("at least one must hold") combination over a collection of verdicts with these semantics: if any input is a pass, the result is a pass; otherwise if any input is undecided, the result is undecided; otherwise the result is a fail.
- **FR-004**: The kernel MUST provide a negation that maps a pass to a fail and a fail to a pass, and leaves an undecided verdict undecided.
- **FR-005**: Conjunction and disjunction MUST be commutative and associative in the resulting verdict — reordering or re-nesting the same sub-verdicts MUST NOT change the combined pass/fail/undecided outcome.
- **FR-006**: The reason text of a combined verdict MUST be order-independent — two arrangements of the same sub-verdicts that produce the same outcome MUST produce the same reason string (the verdict is reproducible in full, not only in its pass/fail/undecided tag).
- **FR-007**: An undecided input MUST NOT be silently coerced to a pass or a fail; it can only be overridden by a genuinely dominating result (a fail under conjunction, a pass under disjunction), never discarded.
- **FR-008**: All verdict operations MUST be total — defined for every combination of inputs, including the empty collection — and MUST NOT raise errors, throw, or return a partial result for any input.
- **FR-009**: Combining zero verdicts MUST return the identity for that operation: a pass for conjunction and a fail for disjunction.
- **FR-010**: The verdict type and its operations MUST carry no domain meaning beyond pass/fail/undecided and a free-text reason — they MUST NOT inspect facts, reference any domain vocabulary, or perform I/O, and they MUST take no dependency on filesystem, networking, version control, build tooling, or any rendering/domain library.
- **FR-011**: The public surface introduced by this feature MUST be declared in the curated kernel signature contract, and the kernel's API surface-area baseline MUST be updated to include it (per the repository's surface-drift discipline).

### Key Entities *(include if feature involves data)*

- **Verdict**: A three-valued judgement — *pass* (the check holds, no reason needed), *fail* (the check does not hold, with a reason), or *undecided* (a competent judge has not yet ruled, with a reason describing what is pending). Undecided is distinct from fail and is the value that routing later turns into a review request rather than a block.
- **Reason**: The free-text explanation attached to a fail or an undecided verdict. The kernel treats it as opaque text; it combines reasons deterministically but never interprets their content.
- **Conjunction / disjunction combination**: The two order-independent reductions over a collection of verdicts ("all must hold" / "at least one must hold") that yield a single combined verdict.
- **Negation**: The polarity flip on a single verdict (pass↔fail; undecided unchanged).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any collection of verdicts, combining them under conjunction (or disjunction) in any order or nesting yields byte-for-byte identical results — outcome and reason — 100% of the time (full order-independence).
- **SC-002**: Across every combination of inputs, an undecided verdict is preserved whenever no dominating result is present: 0% of cases silently convert an undecided input into a pass or a fail.
- **SC-003**: Every verdict operation is total: there exists no input (including empty collections and any mix of the three cases) for which an operation errors, throws, or fails to return a verdict.
- **SC-004**: A newcomer can construct the three verdict kinds, combine and negate them, and observe the documented Kleene outcomes through the public surface alone, in a single short interactive session — no internal helpers required.
- **SC-005**: The feature adds zero heavy dependencies to the kernel — the verdict algebra can be exercised with nothing beyond the base runtime, preserving the "light by default" constraint.

## Assumptions

- This feature corresponds to **F02** in the implementation plan and depends only on **F01** (the kernel core). It may be folded into **F03** (the `Check` algebra) if it proves small, but is specified independently so the verdict semantics are pinned down and tested before any interpreter relies on them.
- The verdict algebra is consumed as a library by later kernel features (F03, F04, F07); there is no end-user UI or command-line surface in this feature.
- The three-valued semantics are the standard Kleene "strong" interpretation, with *pass*=true, *fail*=false, *undecided*=unknown. This is a documented, deliberate choice; no alternative truth table is in scope.
- Reason aggregation for a combined verdict is a deterministic, order-independent function of the contributing reasons (for example: collect the reasons of the dominating kind, de-duplicate, order them by a stable rule, and join them). The precise rendering of the joined reason is an implementation/design detail fixed in the plan and `.fsi`, not in this spec; the only spec-level requirement is that it be reproducible (FR-006).
- Reasons are opaque free text supplied by callers; the kernel neither validates nor parses them. A caller that supplies meaningful reasons gets meaningful combined explanations, but the algebra's correctness does not depend on reason content.
- This feature does not introduce facts, rules, evaluation, or evidence; it operates purely on already-determined verdict values. How a verdict is *produced* from facts (probes, outcomes) is the `Check` algebra's responsibility at F03.
- Runtime enforcement of any caller discipline (e.g. requiring non-empty reasons) is out of scope; the operations are total over all inputs by construction.
