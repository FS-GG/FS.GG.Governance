# Feature Specification: The Adapter SPI & Composition Root — A Domain Plugs In By Supplying Only Its Own Vocabulary

**Feature Branch**: `009-adapter-spi`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F09 · `009-adapter-spi` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces a new **pure** component, the adapter **Service Provider Interface (SPI)** and the **composition root**: the contract a domain implements to plug into the already-shipped kernel (it supplies *only* its facts, an artifact mapping, probes, a rule catalog, and fences) and the machinery that lifts each domain's rules into a single closed coproduct and combines several adapters under a deterministic, order-independent cross-domain precedence. It adds a new curated public signature contract and a new surface-area baseline. It is a pure value/fold layer — **Constitution Principle IV is N/A** (no state, no I/O). It **starts Milestone M3 — the adoption bar** ("the kernel is a library, not a platform"), and is consumed by the concrete F10/F11 adapters and wired by the F12 CLI.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are **domain authors** — the people who want their domain (a design system, a Spec Kit lifecycle, a research project, an essay, an engineering design) to be governed by the kernel **without** copying another domain's vocabulary or layout. Below the SPI sits the whole pure kernel (F01–F07): forward-chaining inference with provenance, three-valued verdicts, the reified `Check` algebra and its four interpreters, the `CheckTier` arbitration model, the evidence/taint DAG, the JSON explanation and contract, and routing/run-modes. None of that is domain-specific, and an adapter re-implements **none** of it. This feature defines the thin, total contract that says exactly **what a domain must supply** — and nothing more — and the single **composition root** where independent adapters are lifted into one fact algebra and coupled, when they must be, by an explicit deterministic combinator rather than ad-hoc glue.

The keystone behaviour is that **adoption is cheap and local, and composition is deterministic**. A domain author writes a closed `'fact` union for the domain, maps its artifacts onto the kernel's structural `ArtifactRef`, supplies a handful of probes, authors a rule catalog (each rule a `Check`, a `CheckTier`, and a `Severity`), and names its high-stakes fences — and gets inference, arbitration, evidence, rendering, hashing, explanation, severity, and run-modes for free. Several such adapters then **compose at one composition root** via a closed coproduct fact type, each adapter's rules **lifted** into it by a semantics-preserving fact mapping, so a rule authored over one domain evaluates **identically** before and after lifting. The few rules that must span domains ("a design task must carry a recorded review") are written **once**, in the root, as an `Implies` over the coproduct's facts, and combined under a **fixed, order-independent precedence** (a blocking result always wins; default allow-unless-fenced) — never as positional, first-match-wins glue. Adapters stay **dumb and independent**: no adapter references another, so **removing one leaves the kernel and the remaining adapters intact**. That removal test is the concrete proof of the milestone's thesis — *the kernel is a library you adopt, not a platform you must be shaped by*.

This feature builds directly on **F04** (it composes `CheckRule<'fact>` catalogs — each rule's `Check` + `CheckTier` + `Severity` + `Bridge` — and reuses `toRule`, the `RuleOutcome`/`NeedsReview`/`RecordedReview` vocabulary, and the cache key) and on **F01** (the `Rule<'fact>`/`FactSet<'fact>` it lifts and the `FixedPoint.evaluate` that evaluates the composed catalog unchanged), and through them on the reified `Check` algebra (**F03**), verdicts (**F02**), and the evidence/taint model (**F05**). It is **pure** — no `Model`/`Msg`/`Effect`, no interpreter — so Principle IV does not apply; the impure effects shell (**F08**, already shipped) and the **F12** CLI are what wire a *composed* catalog into a running loop. It **ships no concrete production adapter** — F10 (Spec Kit) and F11 (design system) deliver those — and proves its generality with small, neutral **example** adapters in the test suite.

### User Story 1 - A domain plugs in by supplying only five things; everything else is reused (Priority: P1)

A domain author adopts the kernel by supplying **exactly five** domain-specific components and nothing more: (1) a closed `'fact` union for the domain, (2) a mapping from its artifacts onto the kernel's structural `ArtifactRef`, (3) a set of probes (the atomic, inspectable predicates its rules compose), (4) a rule catalog — each rule a `Check`, a `CheckTier`, and a `Severity`, and (5) a set of fences naming its high-stakes surfaces for routing. Inference, arbitration, evidence, rendering, hashing, explanation, severity, and run-modes are **all reused from the kernel** — the author writes none of them. The SPI is the typed shape of "what a domain must hand the kernel," and the kernel is the rest.

**Why this priority**: This is the thesis of the adoption-bar milestone and the contract the whole feature exists to make concrete. If a domain has to supply more than its own vocabulary — if it must re-implement inference or arbitration, or be shaped like an existing adapter — then the kernel is a platform, not a library, and the boundary is in the wrong place. The five-part SPI is the minimum viable contract: it is what every other story composes, lifts, and combines, so it ships first.

**Independent Test**: Author a small, neutral example adapter that supplies only the five components; confirm it governs its own artifacts (derives facts, evaluates its rules, renders an explanation) using **only** kernel facilities — and that the adapter contains **no** inference, arbitration, evidence, rendering, hashing, explanation, severity, or routing code of its own.

**Acceptance Scenarios**:

1. **Given** the adapter SPI, **When** a domain author supplies the five components (fact union, artifact mapping, probes, rule catalog, fences), **Then** the domain is fully governable with no further domain code.
2. **Given** an example adapter, **When** its rules are evaluated, **Then** every cross-cutting facility (fixed point, tier arbitration, verdict algebra, evidence/taint, render/hash/explain, routing) comes from the kernel, not the adapter.
3. **Given** an adapter that omits one of the five components, **When** it is assembled, **Then** the gap is a typed/explicit error at the boundary, not a silent partial adoption.

---

### User Story 2 - An adapter's rules lift into the project coproduct and evaluate unchanged (Priority: P1)

A domain author's rules are authored over the **domain's own** `'fact` union, yet a real project runs several domains at once. At the **composition root** the per-domain fact types are combined into a single closed **coproduct** (`ProjectFact = DomainA of … | DomainB of … | …`), and each adapter's rules are **lifted** into it by a semantics-preserving fact mapping. A rule authored over `DomainA`'s facts, once lifted into `ProjectFact`, evaluates to the **identical** outcome (verdict and provenance) it produced over `DomainA` alone — the lift adds no behaviour, it only re-targets the fact channel. The lifting boilerplate is confined to one place per domain (small `inject` helpers and single-case active patterns), so adapter rules read domain-agnostically.

**Why this priority**: Composition is worthless if it changes what a rule means. The faithful, semantics-preserving lift is what lets a domain author reason about their rules in isolation and trust that composition will not silently alter them. It is the mechanism every multi-adapter project depends on, and the property the composition root must guarantee, so it is co-equal P1 with the SPI itself.

**Independent Test**: Author an example adapter and evaluate each of its rules twice — once over the domain fact type directly, once over the lifted rule over the coproduct fact type carrying the same facts; confirm the two outcomes (verdict + provenance) are byte-for-byte identical for every rule.

**Acceptance Scenarios**:

1. **Given** a rule authored over a domain fact type, **When** it is lifted into the coproduct, **Then** evaluating the lifted rule over coproduct-wrapped facts yields the same verdict and provenance as evaluating the original over the domain facts.
2. **Given** the composed catalog of several adapters' lifted rules, **When** the kernel evaluates it, **Then** it runs through the **unchanged** `FixedPoint.evaluate` — the kernel gains no adapter-specific code.
3. **Given** an adapter that uses the judgement (`Opaque`) hatch, **When** its rule is lifted, **Then** the lift preserves its tier (it stays out of `Deterministic` and routes to review), exactly as it would un-lifted.

---

### User Story 3 - Independent adapters compose at one root with explicit, deterministic, order-independent cross-domain coupling (Priority: P1)

A real project runs several adapters at once and occasionally needs a rule that **spans** them — "a design task must carry a recorded review" couples a design fact and a Spec Kit fact. That cross-domain coupling is written **once**, at the composition root, as an `Implies` over the coproduct's facts, and the per-rule verdicts are combined under a **fixed, order-independent precedence**: a blocking result always wins; absent a blocking result the default is allow-unless-fenced. The merged verdict is therefore **independent of the order** in which adapters are composed and rules are evaluated. Cross-domain rules stay a **small, named, reviewed set** listed in the one root, never scattered ad-hoc glue between adapters.

**Why this priority**: Cross-domain coupling is the most powerful and the most dangerous part of composition — the classic "first matching rule wins" firewall failure mode lives exactly here. Making coupling an explicit, deterministic, order-independent combinator concentrated in one reviewable place is what keeps a multi-domain verdict trustworthy. Without it, composition would be non-confluent and the verdict would depend on accidents of rule order. It ships with the composition root because the root is incomplete without a defined combination rule.

**Independent Test**: Author two example adapters and a single cross-domain `Implies` rule at the root; evaluate the composed catalog under every permutation of adapter-composition order and rule order; confirm the merged verdict is identical across all permutations, and that a blocking result from any adapter wins regardless of position.

**Acceptance Scenarios**:

1. **Given** two adapters and a cross-domain `Implies` rule, **When** the composed catalog is evaluated, **Then** the merged verdict follows a fixed precedence (a blocking result wins; default allow-unless-fenced) and is independent of composition/evaluation order.
2. **Given** the same composition, **When** adapter order or rule order is permuted, **Then** the least fixed point and the merged verdict are byte-for-byte identical.
3. **Given** a cross-domain rule whose antecedent domain is **absent** from a composition, **When** the catalog is evaluated, **Then** the rule is inert (no antecedent facts) — not an error and not a silent fail.

---

### User Story 4 - Removing one adapter leaves the kernel and the other adapters intact (Priority: P2)

A maintainer drops one adapter from a multi-adapter project. The kernel and **every remaining adapter** keep working unchanged: nothing in the kernel or in the other adapters referenced the removed one, so the only thing lost is that domain's own facts and rules. No adapter is load-bearing for another; the coupling that exists lives **only** at the composition root and degrades gracefully (a cross-domain rule whose domain is gone simply stops firing). This is the **boundary test** — the concrete demonstration that the kernel is a library you adopt à la carte, not a platform whose shape every domain must share.

**Why this priority**: This is the F09 exit criterion — "the kernel is a library, not a platform, made concrete." A domain author will only trust the adoption bar if adopting (and un-adopting) is genuinely independent. The removal test is the falsifiable proof: if removing adapter B breaks adapter A or the kernel, the boundary has leaked and the milestone is not met. It refines the composition story (P1) into an independence guarantee, so it ranks P2 but must ship with the root.

**Independent Test**: Compose ≥2 example adapters; remove one; confirm the kernel and the remaining adapter(s) still derive facts, evaluate rules, and explain results unchanged — zero references break, and any cross-domain rule that named the removed domain becomes inert rather than throwing.

**Acceptance Scenarios**:

1. **Given** a composition of two or more adapters, **When** one adapter is removed, **Then** the kernel and the remaining adapters evaluate unchanged.
2. **Given** that removal, **When** the remaining catalog is evaluated, **Then** no reference to the removed adapter is required anywhere outside the (now-inert) cross-domain rules at the root.
3. **Given** an adapter authored against the SPI, **When** it is inspected, **Then** it references only the SPI and the kernel — never another adapter.

---

### User Story 5 - A second, unrelated domain adopts the kernel without copying the first's vocabulary or layout (Priority: P2)

A second domain author, working in a domain unrelated to the first (say research vs. a design system), adopts the kernel by supplying their **own** five components — their own facts, artifacts, probes, rules, and fences. They copy **none** of the first domain's vocabulary, layout, or rules. Both domains govern themselves through the same kernel and SPI, and can compose in one project if a real product uses both. The clean mapping across two unrelated domains is the evidence the abstraction sits at the right altitude — if the second domain had to be shaped like the first to adopt, the boundary would be wrong and would have to move.

**Why this priority**: The adoption bar is explicitly *two unrelated domains adopt cheaply* — one adapter proves the SPI compiles, two unrelated adapters prove it is **generic**. This is the milestone's measurable definition of "not a platform." It ranks P2 because the SPI, lifting, and composition (P1) are the machinery; this story is the generality evidence that the machinery is at the right altitude.

**Independent Test**: Author two **unrelated** example adapters (distinct vocabularies, distinct artifact kinds, distinct probes); confirm each governs itself end-to-end through the kernel, neither imports the other's facts/artifacts/probes/rules, and the two compose in one root without either being reshaped to resemble the other.

**Acceptance Scenarios**:

1. **Given** the SPI, **When** a second, unrelated domain adopts it, **Then** that domain defines its own fact vocabulary and governs itself without copying the first domain's facts, artifacts, probes, or rules.
2. **Given** two unrelated example adapters, **When** they are composed at one root, **Then** neither adapter is reshaped to resemble the other, and both evaluate correctly.
3. **Given** a domain whose shape does **not** resemble any existing adapter, **When** it adopts the SPI, **Then** adoption requires only the five components — no inheritance of another domain's layout.

---

### Edge Cases

- **Single adapter, no cross-domain rules**: a one-adapter composition (the trivial coproduct) evaluates exactly as the adapter would standalone — composition adds no behaviour when there is nothing to couple.
- **Two non-interacting adapters**: adapters that share no cross-domain rule compose with no interaction — neither sees the other's facts, and each evaluates as if alone.
- **Cross-domain rule with an absent antecedent domain**: an `Implies` whose antecedent domain is not in the composition is **inert** (no antecedent facts ever hold) — never an error, never a silent fail.
- **Conflicting verdicts across domains**: when one adapter yields a blocking fail and another a pass, the fixed precedence resolves it deterministically (a blocking result wins), independent of order.
- **Duplicate fences from two adapters**: two adapters naming the same high-stakes surface are **deduped** in the composed fence union (route gate sets are unions), not double-counted; the composed tier is a `max`.
- **Lifted judgement (`Opaque`) rule**: an adapter's judgement rule, once lifted, stays out of the `Deterministic` tier and routes to review exactly as un-lifted — the lift preserves tier and the `Opaque` non-inspectability flag.
- **Composition order permuted**: permuting the order adapters are composed or rules are evaluated yields an identical least fixed point and an identical merged verdict (confluence preserved across the coproduct).
- **Minimal adapter**: an adapter that supplies zero cross-domain rules and an empty fence set is still valid — the five-part contract permits empty rule/fence sets where a domain has none.
- **Adding a new domain**: extending the project to a new domain is a **central edit to the coproduct** at the root (the closed-union trade), not an open third-party plug-in — adding an *interpreter* stays trivial, adding a *domain* is one reviewed root change.
- **Negation-across-domains stays stratified**: a cross-domain rule must not negation-check a fact another adapter could *derive* in the same fixed point — facts that are negate-checked are supplied in a lower stratum, preserving confluence (the kernel's stratification discipline carries across the coproduct).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST define an **adapter SPI** — the typed contract a domain supplies to be governed — consisting of **exactly five** domain-specific components and nothing more: (1) a closed domain `'fact` union, (2) a mapping from the domain's artifacts onto the kernel's structural `ArtifactRef`, (3) a set of probes, (4) a rule catalog where each rule carries a `Check`, a `CheckTier`, and a `Severity`, and (5) a set of fences naming the domain's high-stakes surfaces for routing.
- **FR-002**: Everything cross-cutting MUST be **reused from the kernel**, not supplied by an adapter — inference (`FixedPoint.evaluate`), arbitration (`CheckTier`/`toRule`), the verdict algebra, the evidence/taint model, rendering, hashing, explanation, severity, and routing/run-modes. An adapter MUST contain none of these.
- **FR-003**: The feature MUST provide a **composition root** that combines several adapters' domain fact types into a single **closed coproduct** `'fact` type and assembles their rule catalogs into one catalog the kernel evaluates; the root is the **one place** lifting and cross-domain rules live.
- **FR-004**: The feature MUST provide a **semantics-preserving lift** of a rule from a domain `'fact` union into the coproduct (a contravariant fact mapping), such that the lifted rule evaluates to an **identical** outcome (verdict and provenance) to the original over the domain facts — the lift adds no behaviour and re-targets only the fact channel.
- **FR-005**: The composed catalog MUST evaluate through the **unchanged** kernel — `FixedPoint.evaluate` over the lifted `Rule`/`CheckRule` set — adding **no new evaluation logic**; the kernel MUST gain no adapter-specific code (dependency direction: adapters → kernel, never the reverse).
- **FR-006**: Adapters MUST be **independent**: no adapter references or depends on another; each is authored against the SPI and the kernel only. The lifting boilerplate MUST be confined to one place per domain (e.g. an `inject` helper plus a single-case active pattern) so adapter rules read domain-agnostically.
- **FR-007**: Cross-domain coupling MUST be an **explicit, deterministic combinator** — expressed as `Implies` over the coproduct's facts plus a **fixed, order-independent precedence** (a blocking result always wins; default allow-unless-fenced) — and MUST NEVER be ad-hoc glue or a positional, first-match-wins rule.
- **FR-008**: The merged cross-domain verdict and the composed least fixed point MUST be **order-independent** — identical under every permutation of adapter-composition order and rule-evaluation order (confluence preserved across the coproduct).
- **FR-009**: **Removing one adapter** from a multi-adapter composition MUST leave the kernel and every remaining adapter functioning unchanged; a cross-domain rule naming the removed domain MUST become **inert** (its antecedent never holds), not an error — the "library, not platform" boundary.
- **FR-010**: The feature MUST demonstrate **generality** with at least **two unrelated example adapters** (distinct vocabularies, artifacts, and probes) that each govern themselves through the kernel and compose at one root **without** either copying the other's vocabulary, layout, or rules. (These are example/test adapters; F09 ships **no** concrete production adapter.)
- **FR-011**: The composed **fence set** MUST be the **deduped union** of the adapters' fences and the composed **tier** a `max`, so routing over a multi-adapter project is order-independent and free of double-counting.
- **FR-012**: The cross-domain rule set MUST stay **small, named, and listed in one place** (the composition root) so it is the single surface code review guards; the closed-coproduct trade (a new domain is a central root edit, not an open third-party plug-in) MUST be explicit.
- **FR-013**: The SPI and composition root MUST be **pure** — values and total functions, no I/O, no `Model`/`Msg`/`Effect`, no interpreter (Constitution Principle IV is N/A); wiring a composed catalog into a running loop is the job of the F08 effects shell and the F12 CLI, not this feature.
- **FR-014**: An adapter that **omits or malforms** one of the five components MUST surface as a **typed/explicit error at the boundary**, not a silent partial adoption (observability & safe failure — a missing component is malformed input, distinguishable from a tool defect).
- **FR-015**: The feature MUST live in a **new pure component separate from the kernel**, depend on the kernel (F01–F07, in particular F04 + F05) without the kernel depending on it, and keep its dependency footprint **light** (BCL + `FSharp.Core` + kernel only — no heavy dependency).
- **FR-016**: The public surface introduced by this feature MUST be declared in a **curated signature contract** (`.fsi`) and the API **surface-area baseline MUST be added/updated** to include it (per the repository's surface-drift discipline; Tier 1).

### Key Entities *(include if feature involves data)*

- **Adapter (SPI)**: The five-part contract a domain supplies — its closed fact union, artifact mapping, probes, rule catalog, and fences. The whole of what a domain must hand the kernel; everything else is reused.
- **Domain fact type**: A closed `'fact` union owned by one domain — its private vocabulary, never shared with or copied from another domain.
- **Project fact (coproduct)**: The composition root's closed sum of the participating domains' fact types — the single algebra the kernel folds over a multi-domain project.
- **Lift (fact mapping)**: The semantics-preserving embedding of a domain rule into the coproduct (a contravariant mapping on the fact channel) — re-targets a rule without changing its meaning.
- **Rule catalog**: An adapter's rules, each a `Check` + `CheckTier` + `Severity` (an F04 `CheckRule<'fact>`); composed into one catalog the kernel evaluates unchanged.
- **Fence set**: An adapter's named high-stakes surfaces for routing; composed as a deduped union across adapters.
- **Cross-domain rule**: A rule spanning two domains, written once at the root as an `Implies` over the coproduct's facts and combined under deterministic precedence (a blocking result wins; default allow-unless-fenced).
- **Composition root**: The single place where adapters are lifted into the coproduct, cross-domain rules are listed, and the composed catalog/fence set is assembled — the one surface code review guards.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An adapter is **fully specified by exactly five** supplied components — an example adapter authored against the SPI supplies only those and **reuses 100%** of kernel facilities (it contains no inference, arbitration, evidence, rendering, hashing, explanation, severity, or routing code of its own).
- **SC-002**: **Faithful lifting** — for **100% of an example adapter's rules**, evaluating the lifted rule over coproduct-wrapped facts yields a **byte-for-byte identical** outcome (verdict + provenance) to evaluating the original over the domain facts.
- **SC-003**: A cross-domain `Implies` rule produces the **same merged verdict under every permutation** of adapter-composition order and rule-evaluation order (order-independent), and a blocking result from any adapter wins regardless of position.
- **SC-004**: Removing any **one** adapter from a ≥2-adapter composition leaves the kernel and the remaining adapter(s) evaluating **unchanged** — the boundary test passes, zero references break, and any cross-domain rule naming the removed domain becomes inert rather than throwing.
- **SC-005**: **Two unrelated** example domains each govern themselves through the kernel with their **own** vocabulary; neither imports the other's facts, artifacts, probes, or rules (the adoption bar is met with **zero cross-copying**).
- **SC-006**: The composed rule catalog evaluates through the **unchanged** kernel fixed point — the kernel gains **no** adapter-specific code, and the dependency direction (adapters → kernel, never the reverse) holds.
- **SC-007**: Cross-domain precedence is **deterministic** — a blocking result always wins and a fenced surface defaults correctly — verified by property tests over randomized rule/verdict mixes (commutative, order-free combination).
- **SC-008**: The new component adds **no heavy dependency** (references only the kernel, `FSharp.Core`, and the BCL), the kernel does **not** reference it, and the API surface-area baseline is recorded — the SPI is exercised entirely through the built/packed component, not private helpers.

## Assumptions

- This feature corresponds to **F09** (`009-adapter-spi`) in the dated implementation plan and **starts Milestone M3 — the adoption bar** ("the kernel is a library, not a platform"). It depends on **F04** (the `CheckRule<'fact>` catalog: `Check` + `CheckTier` + `Severity` + `Bridge`, plus `toRule`, the `RuleOutcome`/`NeedsReview`/`RecordedReview` vocabulary, and the cache key) and **F05** (the evidence/taint model), and through them on **F01–F03** (the `Rule`/`FactSet`/`FixedPoint`, the reified `Check` algebra, and the verdict algebra) — all already merged. It is consumed by **F10** (the Spec Kit adapter) and **F11** (the design-system adapter) and wired by **F12** (the CLI's project-level composition root). It is reused alongside **F08** (the effects shell, already shipped, which runs a *composed* catalog).
- This is a **pure** feature: values and total folds, no state and no I/O — so **Constitution Principle IV (Elmish/MVU) is N/A**, exactly as it was for F01–F07. Wiring a composed catalog into a running loop (the `Model`/`Msg`/`Effect`/interpreter) is the job of F08 and F12, not this feature.
- F09 ships the **generic** SPI and composition-root machinery **only**; it ships **no concrete production adapter** — F10 (Spec Kit) and F11 (design system) deliver those. Generality is demonstrated with **small, neutral example/toy adapters** authored in the test suite; these are disclosed as synthetic example domains (per Principle V — they carry the synthetic-disclosure discipline since they are illustrative, not real adopters).
- The composition is a **closed-union specialization of *Data Types à la Carte*** (see `docs/governance-design/theory-and-composition.md`): the kernel folds one `ProjectFact` algebra assembled from per-domain pieces. We **deliberately keep the coproduct closed** — a single, reviewable composition root — at the cost of hand-written lifting boilerplate (tamed with small `inject` helpers and single-case active patterns). Adding an *interpreter* stays trivial; adding a *domain* is a central edit to the root. **Open, third-party extensibility / plug-in loading is explicitly out of scope.**
- **Cross-domain coupling** follows the established deterministic model (the Cedar precedence the kernel's route/merge already use): a blocking result always wins; absent one, the default is allow-unless-fenced. This is **commutative and order-free by construction**, so the merged verdict is confluent. Cross-domain coupling is expressed only as `Implies` over the coproduct plus this precedence, **never** as positional, first-match-wins rules. The stratification discipline (no negation over still-being-derived facts) carries across the coproduct.
- The **exact shapes** — the adapter record/interface, the `ProjectFact` coproduct, the `Rule.contramapFacts` (or equivalently named) lifting combinator and its single-case active patterns, the `inject` helpers, and the cross-domain precedence function — are implementation/design details fixed in the **plan** and the curated **`.fsi`**; the spec-level requirements are only the behaviours and invariants stated above (the five-part contract, faithful semantics-preserving lifting, deterministic order-independent cross-domain coupling, adapter independence, the removal/boundary guarantee, the two-unrelated-domains adoption bar, and the pure/light/kernel-only footprint).
- **Out of scope** (deferred to later features): the concrete **Spec Kit adapter** (F10), the concrete **design-system adapter** (F11), the **CLI command surface** and the project-level composition-root *wiring* (F12), the **effects edge** (F08, already shipped), open third-party plug-in extensibility, and the future *rule-set analysis* interpreter (the SMT-style "can this ever pass / is this rule shadowed" fold the inspectable algebra reserves a slot for). This feature provides the generic SPI and composition root those features adopt and wire; it ships no domain adapter and no end-user command surface of its own.
</content>
