# Feature Specification: Check — The Reified, Inspectable Rule Algebra

**Feature Branch**: `003-check-algebra`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F03 · `003-check-algebra` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces a new public algebra (`Check` and its supporting types) plus a six-interpreter surface on the governance kernel (new `.fsi` contract additions, surface-area baseline update). Pure values and total folds; no agent, I/O, or domain vocabulary.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the engineers and downstream features that build on the governance kernel: the `CheckTier`/rule bridge (F04), routing (F07), explanation output (F06), and every domain adapter (F09–F11) that supplies probes. They need a rule's check to be **one value** — not an opaque lambda — so that the same check can be *evaluated* into a verdict, *rendered* into a contract sentence, *hashed* into a cache key, and *explained* as a proof tree, all from a single source that cannot drift apart. This is the keystone of the whole design: reifying the check as data is what makes governance explainable rather than oracular, and cacheable rather than re-run from scratch.

This feature builds directly on the three-valued `Verdict` algebra (F02): `Check` evaluation composes sub-results with the same Kleene conjunction, disjunction, and negation, so a clause an agent must still judge (`Uncertain`) never gets silently treated as a pass or a fail.

### User Story 1 - Build a check as data and evaluate it to a three-valued verdict (Priority: P1)

A consumer expresses a rule's logic by composing a check value from small, named sub-checks — "this probe must be met", "all of these must hold", "at least one of these", "this must NOT hold", "if this then that" — and then evaluates that single value against a set of facts to obtain a pass/fail/undecided verdict. The verdict is computed by the Kleene three-valued composition (F02): an undecided sub-result is preserved, never collapsed into a pass or a fail unless a genuinely dominating result is present.

**Why this priority**: Evaluating a reified check into a three-valued verdict is the minimum viable algebra — without it there is no rule to decide. Making the check a composed *value* (rather than a hand-written `facts -> verdict` lambda) is the entire premise of the design; everything else (render, hash, explain) is a second fold over that same value. The three-valued result is what lets an agent-reviewed clause coexist with deterministic clauses in one expression.

**Independent Test**: Construct a check from the supplied building blocks (an atomic probe, conjunction, disjunction, negation, implication, and the opaque escape hatch), evaluate it against facts whose probes return a mix of met / unmet / undecided outcomes, and confirm the verdict matches the documented Kleene semantics: a conjunction fails if any sub-check fails and is undecided if none fails but at least one is undecided; a disjunction passes if any sub-check passes and is undecided if none passes but at least one is undecided; negation flips pass and fail while leaving undecided unchanged; an implication "a implies b" behaves as "either a does not hold, or b holds".

**Acceptance Scenarios**:

1. **Given** an atomic check whose probe reports "met" over the facts, **When** the consumer evaluates it, **Then** the verdict is a pass; **and** a probe reporting "unmet (reason)" yields a fail carrying that reason; **and** a probe reporting "undecided (reason)" yields an undecided verdict carrying that reason.
2. **Given** a conjunction ("all of these must hold") of sub-checks where at least one evaluates to fail, **When** evaluated, **Then** the verdict is a fail; where none fails but at least one is undecided, the verdict is undecided; where all pass, the verdict is a pass.
3. **Given** a disjunction ("at least one must hold") of sub-checks where at least one passes, **When** evaluated, **Then** the verdict is a pass; where none passes but at least one is undecided, the verdict is undecided; where all fail, the verdict is a fail.
4. **Given** a negation of a sub-check, **When** evaluated, **Then** a passing sub-check yields a fail, a failing sub-check yields a pass, and an undecided sub-check stays undecided.
5. **Given** an implication "a implies b", **When** evaluated, **Then** the result equals evaluating "either (not a) or b" — it passes when a fails or b passes, fails when a passes and b fails, and is undecided when neither a definite pass nor a definite fail of the whole can be reached.
6. **Given** an opaque check (the escape hatch for an irreducible judgement) carrying a name and an evaluation function, **When** evaluated, **Then** its function's outcome is mapped to the corresponding verdict (met→pass, unmet→fail, undecided→undecided).

---

### User Story 2 - Inspect a check without running it: render and hash (Priority: P1)

A consumer renders a check into a human- and agent-readable sentence (which becomes the rule's contract text) and hashes it into a stable structural key (which becomes the cache key for agent reviews) — **without evaluating any probe**. The rendering and the hash depend only on the *shape* of the check (the probe names, declared arguments, and declared inputs), never on running it against facts. Crucially, two checks that are the same up to the order of a commutative node (the members of an "all" or an "any") produce the **same** hash, while order that carries meaning (the two sides of an implication, the ordered arguments of a probe) is preserved in the hash.

**Why this priority**: Inspectability-without-execution is the property the entire design rests on (the algebra is deliberately applicative, never monadic, precisely so its structure is fixed in advance and can be folded without being run). The render fold is what makes the published contract a *fold of the rules* that cannot drift from what is actually enforced. The hash fold is what makes a stochastic agent review reproducible and cacheable: the same check over the same artifacts must yield the same key, regardless of the incidental order a rule author wrote the clauses in, or the cache will miss spuriously and re-consult the agent. This closes a known confluence hazard (commutative nodes producing order-dependent cache keys) at the algebra level.

**Independent Test**: Take a check and render it without supplying any facts; confirm a readable string is produced and that no probe's evaluation function is invoked. Hash the same check and a reordering of one of its commutative nodes (e.g. "all of [a; b]" versus "all of [b; a]") and confirm the two hashes are identical; then hash an implication and its reversed form ("a implies b" versus "b implies a"), and a probe with reordered arguments, and confirm those hashes differ. Confirm an opaque check hashes by its name alone (its evaluation function is never part of the key).

**Acceptance Scenarios**:

1. **Given** any check, **When** the consumer renders it, **Then** a deterministic readable string is produced using only the check's structure (probe names, arguments, inputs) and **no probe evaluation function is executed** (no facts are required to render).
2. **Given** any check, **When** the consumer hashes it, **Then** a stable structural key is produced without executing any probe, **and** re-hashing the identical check yields the identical key.
3. **Given** a conjunction (or disjunction) and the same conjunction with its members reordered, **When** both are hashed, **Then** the two hashes are identical (commutative nodes are canonicalized).
4. **Given** an implication "a implies b" and the reversed "b implies a" (with a and b distinct), **When** both are hashed, **Then** the two hashes differ (the two sides of an implication are positional).
5. **Given** an atomic probe and the same probe with its declared arguments listed in a different order, **When** both are hashed, **Then** the two hashes differ (a probe's arguments are an ordered list and meaning depends on their order).
6. **Given** an opaque check, **When** it is hashed, **Then** the key derives from its name only, never from its (un-inspectable) evaluation function.

---

### User Story 3 - Explain a result as a proof tree (Priority: P2)

A consumer evaluates a check and also obtains a structured explanation — a proof tree mirroring the check's shape, recording for each atomic probe whether it was met, unmet, or undecided, and how the sub-results combined to the overall verdict. The explanation's overall verdict always agrees with the verdict that plain evaluation produces; the two interpreters cannot disagree because they fold the same value.

**Why this priority**: The explanation is what feeds the kernel's provenance (F01) and the JSON explanation output (F06), turning a verdict into something a human or an agent can audit ("this failed because contrast was 3.1:1, below the 4.5:1 floor"). It is one priority below evaluation/inspection because a verdict is useful on its own first; the proof tree is the audit layer over it. Its serialization to JSON and the contract fold are deferred to F06 — this feature produces the structured explanation value, not its wire format.

**Independent Test**: Evaluate a multi-level check against facts and request its explanation; confirm the explanation's top-level verdict is byte-for-byte the same verdict that direct evaluation returns, and that each atomic probe's recorded met/unmet/undecided state matches that probe's outcome over the same facts.

**Acceptance Scenarios**:

1. **Given** any check and any fact set, **When** the consumer both evaluates the check and requests its explanation, **Then** the explanation's overall verdict equals the verdict returned by evaluation.
2. **Given** a composed check (conjunction/disjunction/negation/implication over atomic probes), **When** explained, **Then** the explanation's structure mirrors the check's structure and records each atomic probe's met/unmet/undecided outcome.

---

### User Story 4 - Detect opacity to gate machine-decidability, and collect declared inputs (Priority: P3)

A consumer asks two structural questions of a check without evaluating it: (a) is every node inspectable, or does the check contain an opaque escape-hatch node? and (b) which artifacts does the check declare it reads? The first answer (the "is it fully reified" predicate) is what later lets the rule builder (F04) refuse to mark an opaque check as machine-decidable, forcing it to an agent or a human. The second answer (the declared inputs) drives routing and forms the artifact half of the cache key.

**Why this priority**: These are thin structural folds that other features depend on but that carry no logic of their own — they ride below evaluation, rendering, and explanation. They are included here so the full six-interpreter surface ships as one coherent algebra rather than being split across features, and so F04 can build the tier bridge against a stable contract.

**Independent Test**: Build a fully structural check (no opaque node) and confirm the "is it reified" predicate is true; insert an opaque node anywhere in it and confirm the predicate becomes false. Separately, build a check from probes that declare which artifacts they read and confirm the collected reads contain exactly those declared artifacts.

**Acceptance Scenarios**:

1. **Given** a check whose nodes are all structural (atoms and combinators, no opaque node), **When** the consumer asks whether it is fully reified, **Then** the answer is true.
2. **Given** a check containing at least one opaque node anywhere in its structure, **When** the consumer asks whether it is fully reified, **Then** the answer is false.
3. **Given** a check composed from probes that each declare the artifacts they read, **When** the consumer collects the check's declared reads, **Then** the result contains exactly the artifacts those probes declared (an opaque node contributes none, since it declares no inspectable structure).

---

### Edge Cases

- **Empty conjunction / disjunction**: An "all of []" check evaluates to a pass (nothing to violate) and an "any of []" check evaluates to a fail (nothing satisfied it), inheriting the identity behaviour of the underlying verdict algebra (F02). Neither errors. Rendering and hashing an empty combinator are likewise total.
- **Undecided propagation**: A check containing an atomic probe that returns "undecided" (for example an agent-reviewed clause not yet answered) yields an undecided verdict unless a dominating result is genuinely present (a fail under conjunction, a pass under disjunction). The undecided state is never silently coerced.
- **Negation of undecided**: Negating a sub-check that evaluates to undecided leaves it undecided — there is no definite polarity to flip — exactly as the verdict algebra defines.
- **Implication is desugared, not primitive**: "a implies b" behaves identically to "either (not a) or b"; its verdict, and therefore its explanation, follow from that equivalence. Its hash and render, however, keep the two sides positional, because "a implies b" is not the same statement as "b implies a".
- **Render and hash never execute**: Rendering or hashing a check must not invoke any probe's evaluation function and must not require a fact set. A check built from probes that would throw or block if executed must still render and hash cleanly. This is the inspectable-without-execution guarantee.
- **Opaque node**: The opaque escape hatch carries a name and an evaluation function but no inspectable inner structure. It renders by its name, hashes by its name only, contributes no declared reads, makes the "is it reified" predicate false, and evaluates by running its function. Its opacity is always visible in the model, never silent.
- **Duplicate sub-checks in a commutative node**: Because commutative nodes are canonicalized for hashing, an "all" or "any" containing duplicated members hashes deterministically regardless of how many duplicates appear or where they sit, so the cache key stays reproducible.
- **Reason aggregation under commutative nodes**: When a conjunction fails on more than one sub-check, or otherwise combines several reasons, the combined reason is order-independent (inherited from the F02 reason aggregation), so reordering the members never changes the rendered reason.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST provide an artifact reference value — a stable, structural handle naming an artifact by a kind and a key — that an adapter maps its own artifacts onto. This handle is the only domain-specific thing the algebra touches, and it is renderable and hashable.
- **FR-002**: The kernel MUST provide a probe outcome value with exactly three cases: *met*, *unmet* (carrying a reason), and *undecided* (carrying a reason), mapping one-to-one onto the three verdict cases (pass / fail / undecided) from F02.
- **FR-003**: The kernel MUST provide a probe — a named unit carrying its declared read artifacts, its declared arguments, and an evaluation function from facts to an outcome — such that the probe's *declared shape* (name, arguments, reads) is what gets rendered and hashed while its evaluation function is only ever run, never rendered or hashed.
- **FR-004**: The kernel MUST provide a closed check algebra with exactly these combinators: an atomic probe, a conjunction over a list of checks, a disjunction over a list of checks, a negation of a check, an implication between two checks, and an opaque escape hatch carrying a name and an evaluation function but no inspectable inner structure.
- **FR-005**: The kernel MUST provide readable smart constructors / operators for building checks (an atomic-probe constructor, conjunction and disjunction constructors, a negation constructor, an implication operator, and binary "and" / "or" operators), so checks read like the sentences they enforce.
- **FR-006**: The kernel MUST provide an evaluation interpreter that folds a check over a fact set to a three-valued verdict using Kleene composition: an atom maps its probe's outcome to the matching verdict; a conjunction is the verdict conjunction; a disjunction is the verdict disjunction; a negation is the verdict negation; an implication evaluates as "either (not the antecedent) or the consequent"; an opaque node runs its function and maps the outcome. Undecided sub-results MUST be preserved per the verdict algebra.
- **FR-007**: The kernel MUST provide a render interpreter that folds a check into a deterministic, human- and agent-readable string using only the check's structure, **without executing any probe evaluation function and without requiring a fact set**.
- **FR-008**: The kernel MUST provide a structural hash interpreter that folds a check into a stable key **without executing any probe evaluation function**, where: commutative nodes (conjunction and disjunction) are canonicalized so that reordering their members does not change the hash; positional structure (the two sides of an implication, and the ordered arguments and reads of a probe) is preserved so that meaningful reordering does change the hash; and an opaque node contributes only its name.
- **FR-009**: The kernel MUST provide an explanation interpreter that folds a check over a fact set into a structured proof tree recording each atomic probe's met / unmet / undecided outcome, whose overall verdict is identical to the verdict the evaluation interpreter produces for the same check and facts.
- **FR-010**: The kernel MUST provide a reads interpreter that collects the artifact references a check declares it reads (from its atomic probes), for use by routing and the artifact half of the agent-review cache key.
- **FR-011**: The kernel MUST provide a "fully reified" predicate that is true when every node of a check is structural and false when the check contains at least one opaque node anywhere in its structure.
- **FR-012**: The check algebra MUST be applicative, not monadic — there MUST be no data-dependent sequencing (no "bind") inside a check, so that a check's structure is fixed in advance and the render, hash, reads, and reified-ness interpreters can fold it without ever executing it.
- **FR-013**: Every interpreter MUST be total — defined for every check including the empty conjunction (which evaluates to a pass) and the empty disjunction (which evaluates to a fail) — and MUST NOT raise errors, throw, or return a partial result for any input.
- **FR-014**: Evaluation of a check MUST be order-independent in its verdict: reordering the members of a commutative node (conjunction or disjunction) MUST NOT change the resulting verdict, and the combined reason MUST be order-independent (both inherited from the F02 verdict algebra).
- **FR-015**: The check algebra and all its interpreters MUST carry no domain vocabulary and MUST NOT perform I/O — they MUST NOT reference any software, design, or workflow vocabulary, and MUST take no dependency on filesystem, networking, version control, build tooling, or any rendering/domain library. The algebra and interpreters live in the kernel so every adapter reuses them and supplies only its probe set.
- **FR-016**: The public surface introduced by this feature MUST be declared in the curated kernel signature contract, and the kernel's API surface-area baseline MUST be updated to include it (per the repository's surface-drift discipline).

### Key Entities *(include if feature involves data)*

- **Artifact reference**: A stable, structural handle to a governed artifact, naming it by a kind and a key. The single point at which an adapter's own artifact vocabulary meets the otherwise domain-neutral algebra. Renderable and hashable.
- **Outcome**: The three-valued result a probe reports for a single atomic check — *met*, *unmet* (with reason), or *undecided* (with reason) — mapping one-to-one to the pass / fail / undecided verdict cases.
- **Probe argument**: A declared, renderable/hashable argument of a probe — an artifact reference, a literal string, or a number — describing the probe's parameters as data so they appear in the contract and the cache key.
- **Probe**: A named atomic check supplied by an adapter, carrying its declared read artifacts, its declared arguments, and an evaluation function from facts to an outcome. Its declared shape is inspected; its function is only executed.
- **Check**: The closed, reified combinator algebra — atom, conjunction, disjunction, negation, implication, and the opaque escape hatch — that is a single value which can be evaluated, rendered, hashed, explained, read for inputs, and tested for reified-ness.
- **Explanation**: The structured proof tree produced by explaining a check over facts: it mirrors the check's shape, records each atomic probe's outcome, and carries an overall verdict that matches plain evaluation. Its serialization format is out of scope here (deferred to F06).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Rendering or hashing any check produces output without invoking a single probe evaluation function and without a fact set — 100% of checks are inspectable without being executed.
- **SC-002**: For commutative nodes, reordering members never changes the hash (a conjunction or disjunction and any permutation of its members hash identically, 100% of the time); for positional structure, meaningful reordering always changes the hash (the two sides of an implication, and a probe's argument order, are reflected in the key).
- **SC-003**: Across every check and fact set, the evaluation interpreter's verdict matches the Kleene three-valued composition of its sub-results, and an undecided sub-result is preserved whenever no dominating result is present: 0% of cases silently convert an undecided sub-result into a pass or a fail.
- **SC-004**: For every check and fact set, the explanation interpreter's overall verdict equals the evaluation interpreter's verdict — the two folds never disagree, 100% of the time.
- **SC-005**: The "fully reified" predicate is false for a check if and only if it contains at least one opaque node — correct on 100% of checks, with no false positives or false negatives.
- **SC-006**: Every interpreter is total: there exists no check (including empty conjunctions and disjunctions and any mix of the combinators) for which any interpreter errors, throws, or fails to return a result.
- **SC-007**: A newcomer can build a check from the public smart constructors and fold it all six ways (evaluate, render, hash, explain, collect reads, test reified-ness) through the public surface alone, in a single short interactive session — no internal helpers required.
- **SC-008**: The feature adds zero heavy dependencies to the kernel — the entire algebra and its interpreters can be exercised with nothing beyond the base runtime and the existing kernel, preserving the "light by default" constraint.

## Assumptions

- This feature corresponds to **F03** in the implementation plan and depends on **F02** (the three-valued verdict algebra), which is already merged. Evaluation reuses the F02 verdict conjunction, disjunction, and negation directly, so the Kleene semantics and the order-independent reason aggregation are inherited rather than re-implemented.
- The check algebra is consumed as a library by later kernel features (F04 tier/rule bridge, F06 explanation output, F07 routing) and by every adapter (F09–F11); there is no end-user UI or command-line surface in this feature.
- The `CheckTier`, `Severity`, the `Rule` record, the `toRule` bridge to the kernel `Rule`, the agent-review cache machinery, and the contract fold are **out of scope** here — they are F04 (tier/rule bridge) and F06 (explanation output). This feature provides only the `Check` algebra and its six interpreters; F04 builds the tier bridge on top of the reified-ness predicate, hash, and reads, and F06 serializes the explanation and folds the contract.
- The explanation produced here is a structured in-memory proof-tree value; its JSON serialization and any freshness predicates are deferred to F06. The only spec-level guarantee on the explanation is that its overall verdict matches evaluation.
- The exact textual format of the render output and the exact encoding of the hash are implementation/design details fixed in the plan and `.fsi`, not in this spec. The spec-level requirements are only that render is deterministic and execution-free (FR-007), and that the hash canonicalizes commutative nodes while staying positional elsewhere and is execution-free (FR-008).
- Probe evaluation functions and reasons are opaque to the algebra: the kernel runs a probe's function to get an outcome and treats the outcome's reason as free text, exactly as the verdict algebra treats reasons. The algebra's correctness does not depend on probe or reason content.
- The algebra is closed (a fixed set of combinators reviewed in one place) by deliberate design — third parties do not add new combinator cases; they add probes. This is the closed-coproduct trade-off recorded in the governance-design theory notes, accepted here, not re-litigated.
- This feature pins the commutative-node hash canonicalization (hazard 3 from the governance-design confluence analysis) and confirms `Not` over an evaluated sub-verdict is total and order-free (not the negation-as-failure hazard, which is a rule-level concern handled by stratification elsewhere).
