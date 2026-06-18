# Feature Specification: CheckTier & Rule Bridge — Who Decides, and Reproducible Agent Reviews

**Feature Branch**: `004-checktier-rule-bridge`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F04 · `004-checktier-rule-bridge` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces a new public surface on the governance kernel (the `CheckTier` arbitration model, `Severity`, `SpecSource`, the authored `Rule` record, its smart constructors, the cache-key function, and the `toRule` bridge to the kernel's executable `Rule<'fact>`), plus a surface-area baseline update. Pure values and total folds; no agent call, no I/O, no domain vocabulary. The actual agent invocation is deferred to F08.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the engineers and downstream features that assemble governance rule sets on the kernel: routing and run modes (F07), the effects interpreter that performs the agent call (F08), and every domain adapter (F09–F11) that supplies probes and a fact vocabulary. F03 gave them a rule's *check* as one reified value (evaluable, renderable, hashable, explainable). This feature gives that check a **home**: it pairs each check with a declaration of **who is competent to decide it** (the `CheckTier`), how badly a failure matters (the `Severity`), and which authoritative requirement it enforces (the `SpecSource`), and then **bridges** that authored rule into the kernel's executable `Rule<'fact>` so it participates in fixed-point evaluation.

The keystone behaviour is that an **agent review is reproducible**. A stochastic judge is only ever consulted when the inputs actually change, because every review is keyed by a content hash that folds together the check's structure, the artifacts it reads, and the **identity of the judge itself** (its model id, its version, and the reviewer prompt). The same rule over the same artifacts with the same judge yields the same key — a cache hit, no agent call — while any change to the check, the artifacts, or the judge forces a fresh review. This is what lets a non-deterministic reviewer sit inside an otherwise reproducible pipeline.

This feature builds directly on F03 (`Check.eval` / `Check.hash` / `Check.reads` / `Check.isReified`) and F01 (the kernel `Rule<'fact>` / `FactSet<'fact>` / `RuleId` and the fixed-point evaluator). It performs **no** agent call and records **no** verdict itself — it emits a typed review *request* on a cache miss and reads a previously *recorded* verdict on a cache hit; the dispatch and the recording are the F08 interpreter's job at the edge.

### User Story 1 - Bridge a tiered rule into the executable kernel (Priority: P1)

A consumer authors a rule by pairing a reified check with a declaration of who decides it (deterministic machine, AI agent, or human), the authoritative requirement it enforces, and a severity, then bridges that authored rule into a kernel `Rule<'fact>` that the fixed-point evaluator can run. When the rule fires, its behaviour follows its tier: a **Deterministic** rule evaluates the check against the facts and asserts the resulting verdict as a fact; an **AgentReviewed** rule consults the review cache (see User Story 2); a **HumanOnly** rule never decides and instead escalates with a blocker. The bridged rule's human-/agent-readable description **is** the rendered check, so the description can never drift from what is actually enforced.

**Why this priority**: Without the bridge there is no way for a reified check to participate in kernel evaluation at all — this is the minimum viable connection between the F03 algebra and the F01 engine, and the three-tier dispatch is the whole point of the arbitration model. Everything else (caching, guardrails, severity) refines behaviour that only exists once a rule can be bridged and run.

**Independent Test**: Author a Deterministic rule over a reified check, bridge it, run it through the kernel's fixed-point evaluator against facts, and confirm the asserted verdict matches evaluating the check directly; confirm the bridged rule's description equals the rendered check. Author a HumanOnly rule and confirm bridging it produces a rule that escalates (asserts a blocker) and never asserts a decided verdict.

**Acceptance Scenarios**:

1. **Given** an authored Deterministic rule over a reified check, **When** it is bridged and applied to a fact set, **Then** it asserts a fact carrying the verdict that evaluating the check against those facts produces (pass, fail, or undecided), justified by the rule's identity.
2. **Given** any authored rule, **When** it is bridged, **Then** the resulting kernel rule's description equals the rendered text of its check (the contract text is the rendered selector and cannot drift).
3. **Given** an authored HumanOnly rule, **When** it is bridged and applied, **Then** it asserts an escalation/blocker fact and never asserts a machine- or agent-decided verdict.
4. **Given** an authored AgentReviewed rule, **When** it is bridged and applied, **Then** its behaviour is the cache lookup of User Story 2 (a recorded verdict on a hit, a review request on a miss) — never a direct agent call within the bridge itself.

---

### User Story 2 - Reproducible, cacheable agent reviews (Priority: P1)

A consumer relies on an AgentReviewed rule being consulted **only when its inputs actually change**. The bridge computes a content-hash cache key for the review from four ingredients: the check's structural hash, a hash of the artifacts the check declares it reads, and the **identity of the judge** — its model id, its version, and the reviewer prompt. When a recorded verdict for that exact key is already present among the facts, the rule reuses it without consulting the agent (a cache hit). When no recorded verdict matches, the rule emits a typed review request carrying that key, to be dispatched by the edge interpreter (a cache miss). The same rule, artifacts, and judge always produce the same key; changing the check, the artifacts, the judge model, the judge version, or the reviewer prompt always produces a different key and therefore forces a fresh review.

**Why this priority**: Reproducibility of a stochastic judge is the headline guarantee of this feature and the reason the cache key is defined here rather than improvised per-adapter. Folding the **judge's identity** into the key is what makes the re-review policy correct: a verdict frozen by one model/version/prompt must not be silently reused after the judge changes. This is co-equal priority with the bridge itself because an AgentReviewed rule is not meaningfully usable without it.

**Independent Test**: Compute the cache key for an AgentReviewed rule over fixed artifacts and a fixed judge twice and confirm the keys are identical; then vary, one at a time, the check structure, a read artifact's content, the judge model id, the judge version, and the reviewer prompt, and confirm each variation changes the key. Apply the rule with a recorded verdict for the matching key present and confirm no review request is emitted (the recorded verdict is reused); apply it with no matching recorded verdict and confirm exactly one review request carrying the key is emitted.

**Acceptance Scenarios**:

1. **Given** an AgentReviewed rule, fixed read artifacts, and a fixed judge identity (model id, version, reviewer prompt), **When** the cache key is computed twice, **Then** the two keys are identical.
2. **Given** the same rule and judge, **When** any one of {the check's structure, a read artifact's content, the judge model id, the judge version, the reviewer prompt} changes, **Then** the cache key changes.
3. **Given** an AgentReviewed rule whose computed key already has a recorded verdict among the facts, **When** the rule is applied, **Then** it reuses that recorded verdict and emits no review request (no agent consultation).
4. **Given** an AgentReviewed rule with no recorded verdict matching its computed key, **When** the rule is applied, **Then** it emits exactly one review request carrying the cache key (and the rule's question/prompt) for the edge interpreter to dispatch.
5. **Given** a verdict previously recorded under an old judge identity, **When** the judge model, version, or prompt changes so the key changes, **Then** the old verdict is treated as a miss and a fresh review is requested (the re-review-on-judge-change policy).

---

### User Story 3 - The reified-ness guardrail on the Deterministic tier (Priority: P2)

A consumer is prevented from declaring a check machine-decidable when it is not. When authoring a rule, the kernel **refuses** to assign the Deterministic tier to a check that is not fully reified — that is, a check containing an opaque escape-hatch node anywhere in its structure. Such a check must be authored as AgentReviewed or HumanOnly. Opacity therefore becomes a typed, enforced fact rather than a silent leak: an irreducible judgement can never masquerade as a reproducible machine decision.

**Why this priority**: This guardrail is the safety property that makes the tier declaration trustworthy — it is the reason `Check.isReified` exists in F03. It is one priority below the bridge and the cache because the bridge is usable for honestly-tiered rules without it, but it is the headline *correctness* test of this feature and must ship together with the constructors.

**Independent Test**: Attempt to author a Deterministic rule over a check containing an opaque node and confirm the attempt is refused; author the same opaque check as an AgentReviewed rule and confirm it is accepted. Author a Deterministic rule over a fully reified check and confirm it is accepted.

**Acceptance Scenarios**:

1. **Given** a check containing at least one opaque node, **When** a consumer attempts to author it as a Deterministic rule, **Then** the attempt is refused (the rule is not created at the Deterministic tier).
2. **Given** the same opaque check, **When** a consumer authors it as an AgentReviewed or HumanOnly rule, **Then** the rule is created.
3. **Given** a fully reified check (no opaque node), **When** a consumer authors it as a Deterministic rule, **Then** the rule is created.

---

### User Story 4 - Severity and spec provenance, orthogonal to tier (Priority: P3)

A consumer records, independently of who decides a rule, **how badly a failure matters** (a severity of advisory or blocking) and **which authoritative requirement the rule enforces** (a spec source). Severity is orthogonal to tier: any tier can be advisory or blocking, severity defaults to advisory unless declared blocking, and a HumanOnly rule escalates regardless of its severity. The spec source travels with the rule so that a verdict can always be traced back to the governing requirement it came from.

**Why this priority**: Severity and provenance round out the authored rule into something routing (F07) and explanation output (F06) can consume, but they carry no decision logic of their own — they are data the rule records. They ship here so the full `Rule` surface is one coherent contract that F07 can build on, rather than being split across features.

**Independent Test**: Author two rules with the same tier and check but different severities and confirm the severity is recorded and independent of the tier; confirm a rule with no declared severity defaults to advisory and a rule built with the blocking constructor is blocking. Author a rule with a spec source and confirm the source is recorded on the authored rule.

**Acceptance Scenarios**:

1. **Given** an authored rule with no severity declared, **When** it is inspected, **Then** its severity is advisory (the default).
2. **Given** a rule authored with the blocking constructor, **When** it is inspected, **Then** its severity is blocking, with its tier unchanged.
3. **Given** any tier and any severity combination, **When** a rule is authored, **Then** both are recorded independently (severity does not constrain tier and tier does not constrain severity).
4. **Given** an authored rule carrying a spec source, **When** it is bridged and run, **Then** the spec source is recoverable from the authored rule for provenance and contract generation.

---

### Edge Cases

- **Deterministic check that evaluates to undecided**: A Deterministic rule whose reified check evaluates to *undecided* (e.g. a probe legitimately reports undecided) asserts an undecided verdict — the tier does not coerce it to a pass or fail. The undecided state is preserved exactly as F03 evaluation produces it.
- **Empty read set in the cache key**: An AgentReviewed rule whose check declares no read artifacts still produces a stable, deterministic cache key (the artifact half hashes an empty read set to a fixed value); the key still varies with the check structure and the judge identity.
- **HumanOnly is severity-independent**: A HumanOnly rule escalates (asserts a blocker) whether its severity is advisory or blocking — severity governs how routing treats a *failure*, not whether a human-only rule is decided.
- **Re-review on judge change**: A recorded verdict frozen under one judge identity is never reused once the judge model, version, or reviewer prompt changes, because the key changes and the lookup misses. Conversely, an unchanged judge over unchanged inputs always hits.
- **Cache hit short-circuits the agent entirely**: On a hit, the rule asserts the recorded verdict and emits no review request, so the edge interpreter performs no agent call for that rule on that run.
- **Bridging is total**: Bridging any authored rule (any tier, any check including empty conjunctions/disjunctions) produces a kernel rule whose application returns facts for every input and never throws — totality is inherited from the F03 interpreters and the F01 evaluator.
- **Single-sample judge noise is out of scope**: This feature freezes a single recorded verdict per key. Whether to aggregate multiple judge runs or require a confidence threshold before freezing a verdict (open decision #2) is **noted for the F08 interpreter**, not decided here — the cache-key shape defined here does not preclude it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST provide a `CheckTier` value with exactly three cases — *Deterministic* (a machine decides, reproducibly), *AgentReviewed* (an AI agent decides and the verdict is recorded as evidence), and *HumanOnly* (a person decides; the kernel blocks and asks) — declaring who is competent to judge a rule.
- **FR-002**: The kernel MUST provide a `Severity` value with exactly two cases — *Advisory* and *Blocking* — that is orthogonal to `CheckTier` (any tier may carry any severity) and defaults to *Advisory* when not declared.
- **FR-003**: The kernel MUST provide a `SpecSource` value — a stable, structural handle naming the authoritative requirement a rule enforces — that travels with the authored rule for provenance and contract generation.
- **FR-004**: The kernel MUST provide an authored governance-rule value that pairs a reified `Check` (F03) with its identity, its `CheckTier`, its `SpecSource`, its `Severity`, and an optional reviewer question/prompt (used by the AgentReviewed tier). *(Implementation note: this type is named `CheckRule<'fact>`, not `Rule`, to avoid clashing with the already-shipped kernel `Rule<'fact>`; see plan research D1.)*
- **FR-005**: The kernel MUST provide readable smart constructors for authoring rules — including a base constructor, a way to mark a rule *Blocking*, and a way to author an *AgentReviewed* rule with its reviewer question — so rule authoring reads declaratively.
- **FR-006**: Authoring a rule MUST **refuse** to assign the Deterministic tier when the check is not fully reified (i.e. it contains an opaque node anywhere, per `Check.isReified`), forcing the rule to AgentReviewed or HumanOnly; an opaque check MUST NOT be expressible as a Deterministic rule.
- **FR-007**: The kernel MUST provide a `toRule` bridge that turns an authored `Rule` into the kernel's executable `Rule<'fact>` (F01), where the executable rule's description equals the rendered text of the check (`Check.render`) so it cannot drift from what is enforced.
- **FR-008**: A bridged Deterministic rule, when applied to a fact set, MUST evaluate its check (`Check.eval`) against those facts and assert the resulting three-valued verdict as a fact, justified by the rule's identity; the verdict MUST NOT be coerced (an undecided result stays undecided).
- **FR-009**: A bridged AgentReviewed rule, when applied to a fact set, MUST compute the review cache key, and: on a **cache hit** (a recorded verdict for that key is present among the facts) reuse the recorded verdict and emit **no** review request and perform **no** agent call; on a **cache miss** (no recorded verdict matches) emit exactly one typed review request carrying the cache key and the rule's question/prompt, for the edge interpreter (F08) to dispatch.
- **FR-010**: A bridged HumanOnly rule, when applied, MUST escalate by asserting a blocker fact and MUST NOT assert a machine- or agent-decided verdict, regardless of its severity.
- **FR-011**: The kernel MUST provide a cache-key function that derives the review key from exactly these ingredients: the check's structural hash (`Check.hash`), a hash over the artifacts the check declares it reads (`Check.reads`), the judge's **model id** and **version**, and the **reviewer-prompt hash** — combined deterministically. *(Implementation note: the model id and version travel as a `JudgeId` per-run config value, while the reviewer prompt travels as the rule's own `Question` — they are distinct key ingredients, not a single bundled "judge identity"; see plan research D2/D4.)*
- **FR-012**: The cache key MUST be reproducible and input-sensitive: identical inputs (check structure, read artifacts, judge model id, judge version, reviewer prompt) MUST produce an identical key, and a change to any one of those ingredients MUST produce a different key.
- **FR-013**: The bridge MUST implement the re-review-on-judge-change policy: a verdict recorded under one judge identity (model id, version, or reviewer prompt) MUST NOT be reused once that identity changes — the changed key misses, forcing a fresh review request.
- **FR-014**: The kernel MUST define the typed *review-request* fact (carrying at least the rule identity, the reviewer question, and the cache key) and the means to recognise a *recorded-verdict* fact for a given cache key among the current facts, so the bridge can short-circuit on a hit and request on a miss.
- **FR-015**: The bridge and all the surface introduced here MUST perform **no** I/O and **no** agent call — they produce facts and review requests as pure values; the actual agent dispatch, the recording of verdicts, and reading artifact contents are the F08 edge interpreter's responsibility. The means by which governance facts (verdicts, blockers, review requests, recorded verdicts) are represented within an adapter's own `'fact` type is supplied by the caller (an injection/projection the bridge is parameterised by), keeping the kernel domain-neutral.
- **FR-016**: The surface MUST carry no domain vocabulary and add no heavy dependencies — it MUST reuse the in-assembly F03 `Check` interpreters and F01 kernel types and depend on nothing beyond the base runtime, preserving the kernel's "light by default" constraint.
- **FR-017**: `toRule` and every bridged rule's application MUST be total — defined for every authored rule and every fact set (all three tiers, every check including empty combinators), never throwing or returning a partial result.
- **FR-018**: The public surface introduced by this feature MUST be declared in the curated kernel signature contract, and the kernel's API surface-area baseline MUST be updated to include it (per the repository's surface-drift discipline).

### Key Entities *(include if feature involves data)*

- **CheckTier**: The arbitration declaration on a rule — *Deterministic*, *AgentReviewed*, or *HumanOnly* — naming who is competent to decide it. The bridge between classical machine evaluation and the agent harness.
- **Severity**: How badly a failure matters — *Advisory* or *Blocking* — orthogonal to tier, defaulting to advisory. Consumed by routing (F07) to decide whether a failure blocks.
- **SpecSource**: A stable, structural handle to the authoritative requirement a rule enforces, carried for provenance and for the single-source contract fold (F06).
- **Rule (authored)**: The authored governance rule — a reified `Check` paired with its identity, tier, spec source, severity, and optional reviewer question. Distinct from the kernel's executable `Rule<'fact>`, into which `toRule` translates it. *(Named `CheckRule<'fact>` in implementation to avoid the clash with the kernel `Rule<'fact>`; plan research D1.)*
- **Review request**: The typed value emitted on a cache miss — carrying the rule identity, the reviewer question, and the cache key — for the edge interpreter (F08) to dispatch to an agent. The bridge emits it but never acts on it.
- **Recorded verdict**: A previously frozen agent verdict, identified by its cache key, that a cache hit reuses without consulting the agent. Recording it is the F08 interpreter's job; recognising it is the bridge's.
- **Cache key**: The content-hash key of an agent review — the check hash, the read-artifact hash, and the judge identity (model id, version, reviewer-prompt hash) combined — that makes a stochastic review reproducible and only re-consulted when inputs change.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of attempts to author a Deterministic rule over a non-reified check (one containing an opaque node) are refused; no opaque check is ever expressible as a Deterministic rule.
- **SC-002**: For an AgentReviewed rule, identical inputs (check structure, read artifacts, judge model id, judge version, reviewer prompt) produce an identical cache key 100% of the time, and changing any one of those five ingredients changes the key 100% of the time.
- **SC-003**: On a cache hit (a recorded verdict for the computed key is present), the bridged rule emits zero review requests and performs zero agent consultations; on a cache miss, it emits exactly one review request carrying the key.
- **SC-004**: When the judge identity (model id, version, or reviewer prompt) changes, a verdict recorded under the prior identity is reused in 0% of cases — a fresh review is always requested.
- **SC-005**: A bridged Deterministic rule's asserted verdict equals evaluating its check directly against the same facts in 100% of cases, with undecided results never coerced to pass or fail.
- **SC-006**: Every bridged kernel rule's description equals the rendered text of its check 100% of the time, so the published contract cannot drift from the enforced selector.
- **SC-007**: `toRule` and every bridged rule's application are total: there exists no authored rule (any tier, any check) and no fact set for which bridging or application throws, errors, or returns a partial result.
- **SC-008**: Severity is independent of tier: for every tier, both advisory and blocking rules can be authored, an undeclared severity defaults to advisory, and a HumanOnly rule escalates regardless of its severity — verified across all tier/severity combinations.
- **SC-009**: The feature adds zero heavy dependencies to the kernel — the entire surface is exercised with nothing beyond the base runtime and the existing kernel (F01 + F02 + F03).

## Assumptions

- This feature corresponds to **F04** in the implementation plan and depends on **F03** (the reified `Check` algebra and its interpreters `eval` / `render` / `hash` / `reads` / `isReified`) and **F01** (the kernel `Rule<'fact>`, `FactSet<'fact>`, `RuleId`, and the fixed-point evaluator), both already merged. It reuses those interpreters and types directly rather than re-implementing any of them.
- This feature **locks decision #1** (GitHub issue #1): the agent-review cache key includes the judge model id, the judge version, and the reviewer-prompt hash in addition to the check hash and the artifact hashes, with the defined re-review-on-judge-change policy (a changed judge identity changes the key and forces a fresh review).
- This feature **notes decision #2** (GitHub issue #2) for the F08 interpreter and does not decide it here: whether to aggregate N judge runs or require a confidence threshold before freezing a verdict is an edge-interpreter concern; the cache-key shape defined here is compatible with either choice.
- The **actual agent call** (dispatching a review request to a model), the **recording** of a returned verdict as evidence, and any **artifact-content reading** are out of scope — they belong to the F08 effects interpreter at the edge. This feature produces only pure values: the authored rule, the bridged kernel rule, the cache key, and the emitted review-request / recorded-verdict / verdict / blocker facts.
- The **contract fold** (`contract : Rule list -> ...`) and the **JSON serialization of explanations** are out of scope — they are F06 (explanation output). This feature only guarantees that a bridged rule's description is the rendered check (so the F06 contract fold has a non-drifting source).
- Governance outcomes (a decided verdict, a blocker, a review request, a recorded verdict) must be representable within the adapter's own `'fact` type for the kernel `Rule<'fact>` to carry them. The bridge is therefore **parameterised by caller-supplied embedding/projection functions** (lift a governance outcome into `'fact`; recognise a recorded verdict for a key among the facts), which keeps the kernel domain-neutral and is consistent with the adapter-SPI lifting approach (F09). The exact shape of these functions is an implementation detail fixed in the plan and `.fsi`, not in this spec.
- The exact encoding of the cache key, the exact field layout of the review-request and recorded-verdict facts, and the exact signatures of the smart constructors are implementation/design details fixed in the plan and the `.fsi`; the spec-level requirements are only the behaviours and invariants stated above (refusal, reproducibility, hit/miss, re-review, totality, no-drift, orthogonality).
- The judge identity (model id, version, reviewer prompt) is supplied to the cache-key function by the caller; this feature defines how it folds into the key, not where the values come from (that is the F08 interpreter's configuration).
