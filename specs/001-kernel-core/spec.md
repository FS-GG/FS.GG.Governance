# Feature Specification: Kernel Core — Facts, Rules, Fixed-Point Derivation, Provenance

**Feature Branch**: `001-kernel-core`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "start the first item in the project." (resolved to F01 · `001-kernel-core` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces the foundational public API surface of the governance kernel (new `.fsi` contract, first surface-area baseline). No agent, I/O, or domain vocabulary; the kernel is a pure reasoner.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the engineers and downstream features that build on the governance kernel: the people writing domain adapters, the routing and evidence layers (F05–F07), and anyone who needs to ask "given these facts and these rules, what else is true, and why?"

### User Story 1 - Derive new facts from asserted facts and rules (Priority: P1)

A kernel consumer supplies a set of asserted (known) facts and a set of monotonic rules. They ask the kernel to evaluate, and receive back the full set of facts — the originals plus everything the rules could derive — having reached a stable state where no further rule can add anything new.

**Why this priority**: This is the core reason the kernel exists. Without forward-chaining derivation to a fixed point, there is no inference engine and nothing for the rest of the system (evidence, routing, checks) to stand on. It is the minimum viable product of the entire repository.

**Independent Test**: Supply a handful of toy facts plus two or three rules that chain (rule A's output is rule B's input), evaluate, and confirm the result contains exactly the asserted facts plus the correct closure of derived facts, with no spurious or missing entries.

**Acceptance Scenarios**:

1. **Given** a set of asserted facts and a set of rules where one rule's conclusion enables another rule, **When** the consumer evaluates, **Then** the result contains the asserted facts and all transitively derivable facts.
2. **Given** asserted facts and rules where no rule's preconditions are met, **When** the consumer evaluates, **Then** the result contains exactly the asserted facts and nothing derived.
3. **Given** any valid set of facts and monotonic rules over a bounded fact space, **When** the consumer evaluates, **Then** evaluation terminates (reaches quiescence) rather than looping indefinitely.

---

### User Story 2 - Understand why each derived fact holds (Priority: P1)

A consumer inspecting the evaluation result can, for any derived fact, see which rule produced it and which input facts that rule consumed. Asserted facts carry no such justification (they were given, not derived).

**Why this priority**: Explainability is the project's central promise — the kernel must be "explainable rather than oracular." A derivation with no recorded justification is indistinguishable from a guess, and every downstream feature (explanation output, evidence taint, route reasons) reads this provenance. It ships with Story 1 because a fact without its reason is only half the product.

**Independent Test**: Evaluate a fact set that requires a multi-step derivation, then for a fact known to be derived, read its justification and confirm it names the responsible rule and the exact input facts that rule relied on; for an asserted fact, confirm its justification is empty.

**Acceptance Scenarios**:

1. **Given** a fact derived by a single rule from two input facts, **When** the consumer inspects that fact's justification, **Then** it records the producing rule's identity and the identities of both input facts.
2. **Given** an asserted (supplied) fact, **When** the consumer inspects its justification, **Then** the justification is empty.
3. **Given** a fact derivable along more than one chain, **When** the consumer inspects its justification, **Then** the recorded justification reflects the derivation that first established it (a deterministic, reproducible choice).

---

### User Story 3 - Get the same answer regardless of rule order (Priority: P1)

A consumer who lists the same rules in a different order, or whose rules happen to fire in a different sequence, receives an identical final fact set with identical justifications. The outcome depends only on the facts and rules, never on incidental ordering.

**Why this priority**: Determinism and order-independence are what make the kernel trustworthy and cacheable. If shuffling rule order changed the answer, no result could be reproduced, no review could be cached, and the engine would be unsound as a basis for governance. This is a defining property of the fixed point, not a nice-to-have.

**Independent Test**: Take a rule set, shuffle its order several ways, evaluate each ordering against the same asserted facts, and confirm every run produces the same final fact set and the same justification for each derived fact.

**Acceptance Scenarios**:

1. **Given** a fixed set of asserted facts and a fixed set of rules, **When** the consumer evaluates two or more different orderings of those rules, **Then** the final fact sets are identical (the least fixed point).
2. **Given** two facts that the kernel's identity function treats as the same fact, **When** both are produced during evaluation, **Then** the result contains a single deduplicated entry, not two.

---

### Edge Cases

- **Empty inputs**: Evaluating with no rules returns exactly the asserted facts; evaluating with no asserted facts and rules that need inputs returns an empty result. Neither errors.
- **Duplicate assertions**: Two asserted facts that map to the same identity collapse to one entry.
- **Re-derivation of an existing fact**: A rule that "produces" a fact already present adds nothing and does not loop — the fact is recognized as already known.
- **Self-referential chains within the monotone fragment**: A rule whose output also satisfies its own precondition still terminates, because re-deriving a known fact adds nothing new.
- **Non-monotonic / stratification boundary**: The kernel assumes rules only *add* facts. Negated, aggregated, or "counted" facts are **supplied** from a lower stratum, never derived inside the same fixed point. This is a documented precondition of the engine, not a behavior it enforces at runtime in this feature; supplying such facts as ordinary asserted facts is the supported path.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST accept a set of asserted facts and a set of rules and produce a result fact set containing the asserted facts plus all facts derivable by the rules.
- **FR-002**: The kernel MUST apply rules by forward chaining until quiescence — repeating until no rule produces a fact not already present.
- **FR-003**: Evaluation MUST terminate for any set of monotonic rules over a bounded fact space.
- **FR-004**: The kernel MUST record, for each derived fact, a justification naming the rule that produced it and the input facts that rule consumed.
- **FR-005**: Asserted (supplied) facts MUST carry an empty justification, distinguishing them from derived facts.
- **FR-006**: The final fact set MUST be independent of the order in which rules are listed or fire (the least fixed point is unique for given facts and rules).
- **FR-007**: The kernel MUST deduplicate facts by a caller-provided identity function so that two facts of the same identity collapse to a single entry, regardless of how many rules or assertions produce them.
- **FR-008**: The kernel MUST report how many evaluation rounds were required to reach quiescence, so consumers can observe convergence (and detect non-convergence in development).
- **FR-009**: The kernel MUST treat facts as opaque values carrying no domain meaning — it MUST NOT inspect, branch on, or assume anything about a fact's contents beyond its caller-supplied identity. A domain's fact vocabulary is supplied entirely by the consumer.
- **FR-010**: The kernel MUST take no dependency on filesystem access, networking, external rule languages, version control, build tooling, or any rendering/domain library. It operates purely on the in-memory facts and rules it is given.
- **FR-011**: The public surface introduced by this feature MUST be declared in a curated signature contract, and an API surface-area baseline MUST be recorded for it (per the repository's surface-drift discipline).
- **FR-012**: The documented precondition MUST be stated to consumers: rules are monotonic (add-only); negated, aggregated, or recursively-negated facts are supplied from a lower stratum and never derived within the same fixed point.

### Key Entities *(include if feature involves data)*

- **Fact**: An opaque, domain-supplied value the kernel reasons over. The kernel knows a fact only through its caller-provided identity and its justification; it never interprets the value itself.
- **Fact identity**: A stable handle for a fact, supplied by a caller-provided identity function, used for deduplication and for naming inputs in justifications.
- **Rule**: A named, described unit of inference that maps the current set of known facts to zero or more newly asserted facts. Rules are add-only (monotonic).
- **Justification (provenance)**: The record attached to a derived fact stating which rule produced it and which input facts it consumed; empty for asserted facts.
- **Evaluation result**: The outcome of running the engine — the complete fact set (asserted plus derived, deduplicated) together with the count of rounds taken to converge.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any supported input, repeated evaluation (and evaluation under any reordering of the rules) yields byte-for-byte identical results — 100% reproducible across runs and orderings.
- **SC-002**: Every derived fact in a result can be traced to a justification naming exactly one producing rule and the specific inputs it used; an auditor can reconstruct each derivation chain back to asserted facts with no gaps.
- **SC-003**: Evaluation over a bounded fact space always terminates; no supported input causes a non-terminating run.
- **SC-004**: A newcomer can supply a few facts and a couple of chained rules and obtain the correct derived closure with provenance, exercising the engine through its public surface alone, in a single short interactive session — no internal helpers required.
- **SC-005**: The feature ships with the kernel carrying zero heavy dependencies — it can be evaluated with nothing beyond the base runtime, confirming the "first useful product, light by default" constraint.

## Assumptions

- The kernel is consumed as a library by other features and adapters in this repository; there is no end-user UI or command-line surface in this feature (those arrive in F08/F12).
- Callers are responsible for supplying a correct identity function for their fact type; the kernel's deduplication and provenance are only as sound as that function (an injective identity is the supported case).
- Rules are written as ordinary typed functions over the fact set; there is no external rule language, parser, or configuration format in scope.
- The monotonicity / stratification precondition (FR-012) is a contract the kernel documents and assumes; runtime enforcement of stratification (rejecting non-monotonic rule sets) is **not** in scope for this feature and is left to the consumer's discipline and to later analysis features.
- Cycle rejection for explicit dependency graphs belongs to the evidence model (F05); within this fixed-point engine, re-deriving an already-known fact simply adds nothing, so ordinary rule chains that "loop" still converge.
- This feature corresponds to F01 in the implementation plan and **locks decision #4 (kernel preconditions)** to the extent of monotonicity and stratification of supplied negated/aggregated facts; the remaining parts of decision #4 (commutative-node hash canonicalization) are locked later, at F03.
