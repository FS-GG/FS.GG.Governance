# Feature Specification: Explanation Output, the Drift-Proof Contract & Evidence Freshness — Making the Kernel's Reasoning Legible

**Feature Branch**: `006-explanation-output`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F06 · `006-explanation-output` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces a new public surface on the governance kernel (a JSON serialization of the `Explanation` proof tree and of evidence state, a `contract` fold that renders a rule catalog into its published selector, and evidence-freshness predicates over supplied timestamps), plus a surface-area baseline update. Pure values and total derivations; no I/O, no agent call, no clock read, no domain vocabulary. Reading artifacts and their real modification times, dispatching reviews, and recording verdicts remain the edge interpreter's job (F08). **This feature completes Milestone M1 — the first useful product.**

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the people and downstream features that need the kernel's reasoning to be **legible and portable as data**: a developer or CI reading *why* a check passed or failed; an agent consuming a structured explanation; a reviewer reading the published rule contract to know what is being enforced; and the freshness check that asks whether a piece of evidence still covers the artifact it was recorded against. F01 gave the kernel facts, rules, and a fixed-point evaluator with provenance; F03 reified each rule's check into one value that can be evaluated, rendered, hashed, and **explained** into a structured proof tree; F05 added the **evidence dimension** — declared evidence states and the synthetic-taint closure. Those folds and states all currently live as in-memory values.

This feature is the **output layer**: it turns those values into stable, human/agent-readable, JSON-friendly form, and adds the two remaining first-product derivations — the **published contract** (a fold of the rules, never a hand-maintained file) and **simple evidence-freshness** predicates. Three things ship together:

1. **JSON explanation.** The `Explanation` proof tree produced by `Check.explain` (F03) and the evidence states produced by F05 are serialized to deterministic, round-trippable JSON, so a verdict can be inspected, diffed, archived, or handed to an agent without re-running any probe.
2. **The drift-proof contract.** A `contract` function folds a catalog of reified rules into its published, human/agent-readable contract — each rule's identity, severity, spec source, and **rendered** check. Because the contract *is* the rendered selector (F03's `Check.render`), it cannot drift from what is actually enforced.
3. **Evidence freshness.** Pure predicates decide whether a recorded piece of evidence is still **fresh** — i.e. recorded at or after the last change to the artifacts it covers — or **stale**, over timestamps supplied to the kernel.

Like every kernel feature, this is **pure and total**: it performs no I/O, reads no real artifacts, and reads no clock. Timestamps and artifact-modification instants are *supplied* to it as values; discovering them from the filesystem/git and recording verdicts are the F08 edge interpreter's responsibility. The serialization uses only what ships with the base runtime — **no new dependency**.

### User Story 1 - Emit a check's explanation as stable, round-trippable JSON (Priority: P1)

A developer, CI job, or agent has evaluated a rule's reified check against a set of facts and holds the resulting `Explanation` proof tree (F03). They serialize it to JSON to inspect *why* the verdict came out as it did — every node mirrors the check's surface shape, records each atomic probe's met/unmet/unknown outcome, and carries the rolled-up verdict, with the root verdict identical to `eval`. The JSON is deterministic (stable key and node ordering) so it can be diffed and archived, and it round-trips: parsing the emitted JSON yields an explanation equal to the original. No probe is executed during serialization — the proof tree is rendered purely as data.

**Why this priority**: This is the headline of "JSON explanation" and the user-visible payoff of the whole M1 product — the kernel's reasoning becomes portable, inspectable data instead of an opaque in-memory verdict. Without it the first useful product cannot emit the explanations the milestone promises, so it is the minimum viable slice and everything else (contract, freshness, evidence rendering) layers on top.

**Independent Test**: Build a small check, evaluate it to an `Explanation`, serialize it to JSON, and confirm the JSON mirrors the proof-tree shape, records each probe's outcome, and carries the root verdict equal to `eval`. Serialize the same explanation twice and confirm byte-for-byte identical output (determinism). Parse the JSON back and confirm the recovered explanation equals the original (round-trip).

**Acceptance Scenarios**:

1. **Given** an `Explanation` proof tree for an evaluated check, **When** it is serialized to JSON, **Then** the JSON mirrors the tree's node structure, each atomic node records its probe name and met/unmet/unknown outcome, and every node carries its rolled-up verdict.
2. **Given** an `Explanation`, **When** it is serialized twice, **Then** the two JSON outputs are byte-for-byte identical (deterministic, stable ordering).
3. **Given** the JSON emitted for an `Explanation`, **When** it is parsed back, **Then** the recovered explanation is equal to the original (round-trip with no loss).
4. **Given** a check containing an `Opaque` (un-reified) node, **When** its explanation is serialized, **Then** the opaque node is represented by its declared name and recorded outcome only (no probe is executed and no un-inspectable function is emitted).

---

### User Story 2 - Generate the published rule contract as a drift-proof fold of the rules (Priority: P1)

A reviewer or contributor wants to read the **contract** the governance kernel enforces — the catalog of rules, each with its identity, severity, the spec it traces to, and a human/agent-readable statement of *what* it checks. They call `contract` over the rule catalog and receive that document, where each rule's statement is the **rendered check** (F03's `Check.render`) — derived, not hand-written. Because the contract is produced by folding the same checks that are evaluated, it **cannot drift** from what is actually enforced: there is no separate file to fall out of date. The contract is deterministic and emittable both as readable text and as JSON.

**Why this priority**: A drift-proof contract is co-equal with the JSON explanation as a first-product deliverable — it is the standing answer to "what does this enforce?" and the single-source-of-truth guarantee is the whole point of reifying checks in F03. It is P1 because it is small, high-value, and removes a perennial source of rot (a contract doc that lies about the rules); freshness (P2) and evidence rendering (P3) are refinements that do not block this guarantee.

**Independent Test**: Build a catalog of a few reified rules, fold it with `contract`, and confirm each entry carries the rule's id, severity, spec source, and a statement equal to `Check.render` of that rule's check. Change a rule's check and confirm the contract entry changes accordingly (it tracks the selector). Reorder the catalog and confirm each rule's own entry is unchanged (per-rule rendering is stable).

**Acceptance Scenarios**:

1. **Given** a catalog of reified rules, **When** `contract` is folded over it, **Then** the result contains one entry per rule carrying its id, severity, spec source, and a rendered statement of its check.
2. **Given** a rule whose rendered statement appears in the contract, **When** that rule's check is modified, **Then** the contract entry's statement changes to match the new rendering (the contract tracks the selector and cannot drift).
3. **Given** the same rule catalog, **When** `contract` is folded over it twice, **Then** the two results are identical (deterministic).
4. **Given** a contract, **When** it is emitted as JSON, **Then** the JSON is deterministic and round-trips to an equal contract.

---

### User Story 3 - Decide whether recorded evidence is still fresh (Priority: P2)

A consumer holds a piece of recorded evidence (e.g. a test run, a review) that was captured at a known instant and that covers one or more artifacts whose last-change instants are known. They ask the kernel whether the evidence is **fresh** — still describing the current artifacts — or **stale**. The rule is simple and causal: evidence is fresh exactly when it was recorded **at or after** the most recent change to every artifact it covers; if any covered artifact changed after the evidence was recorded, the evidence is stale (it describes a version that no longer exists). The predicate is pure over the supplied instants — the kernel reads no clock and no filesystem — and is domain-neutral: the same predicate serves a software test, a research measurement, or a citation check.

**Why this priority**: Freshness is the "simple freshness" first-product scope item — it makes the evidence model temporally honest by catching evidence that has silently gone stale. It is P2 rather than P1 because the JSON explanation and the contract are independently useful without it, but it is required for M1 completeness and is the natural complement to F05's evidence states.

**Independent Test**: Record evidence at instant T covering an artifact last changed at T−1 and confirm it is fresh; change the artifact to T+1 (after the evidence) and confirm the same evidence is now stale. With evidence covering several artifacts, confirm it is fresh only when recorded at or after the latest of their change instants, and stale if any one of them changed afterward.

**Acceptance Scenarios**:

1. **Given** evidence recorded at instant T covering an artifact last changed at or before T, **When** freshness is evaluated, **Then** the evidence is reported fresh.
2. **Given** evidence recorded at instant T covering an artifact that changed after T, **When** freshness is evaluated, **Then** the evidence is reported stale.
3. **Given** evidence covering several artifacts, **When** freshness is evaluated, **Then** it is fresh only if recorded at or after the latest covered-artifact change instant, and stale if any covered artifact changed afterward.
4. **Given** the same evidence and artifact instants, **When** freshness is evaluated at two different times, **Then** the results are identical (it is a pure function of the supplied instants, reading no clock).

---

### User Story 4 - Serialize evidence states for the evidence report (Priority: P3)

A consumer wants the F05 evidence dimension to appear in the same JSON output as the explanation — both the declared states and the computed **effective** states (including the computed-only `AutoSynthetic` taint). They render an `EvidenceState`, and a map of node-to-effective-state, to stable JSON. Each of the six states serializes to a distinct, stable token, so a downstream reader can see at a glance that "this passed, but only on synthetic evidence" (an `AutoSynthetic` node) without re-running the closure. The rendering is domain-neutral — it serializes the state vocabulary, with node identity rendered by a supplied projection — so it works for software, research, or writing.

**Why this priority**: This rounds out the JSON output so the milestone's "stores facts … taints synthetic evidence, and emits JSON explanations" is fully covered in one report, but it is a thin serialization layer over F05's already-computed states rather than new reasoning, so it is the lowest priority and ships after the explanation, contract, and freshness slices.

**Independent Test**: Serialize each of the six `EvidenceState` cases and confirm each yields a distinct, stable token that round-trips. Compute `effective` over a small tainted graph (F05), serialize the resulting node-to-state map, and confirm the `AutoSynthetic` nodes are visibly marked and the JSON round-trips to the same map.

**Acceptance Scenarios**:

1. **Given** each of the six `EvidenceState` cases, **When** it is serialized, **Then** it yields a distinct, stable JSON token that round-trips to the same state.
2. **Given** the effective-state map computed over a tainted evidence graph, **When** it is serialized, **Then** every node's effective state (including `AutoSynthetic`) is present and the JSON round-trips to an equal map.

---

### Edge Cases

- **Empty / leaf explanations**: An explanation that is a single atomic node serializes to a single JSON node carrying its outcome and verdict; an empty `All` (vacuously `Pass`) and an empty `Any` serialize faithfully with their inherited verdicts (no special-casing, no error).
- **Empty rule catalog**: `contract` over an empty catalog yields an empty contract — totally, without error.
- **Empty / single-artifact freshness**: Evidence covering **no** artifacts is fresh (there is nothing it can be stale against); evidence covering exactly one artifact reduces to a single instant comparison.
- **Tie at the same instant**: Evidence recorded at the **same** instant as an artifact's last change is **fresh** (the boundary is inclusive — "recorded at or after"); this tie-handling is fixed and documented so freshness is unambiguous.
- **Opaque nodes in the contract / explanation**: An `Opaque` (un-reified) check contributes its declared name only; no un-inspectable function is ever serialized, and rendering/serialization never executes a probe.
- **Determinism over ordering**: JSON object keys and array members are emitted in a fixed, stable order so output is diffable and the contract/explanation cannot vary run-to-run for the same input.
- **Round-trip fidelity**: Parsing emitted JSON reconstructs a value equal to the original for explanations, contracts, and evidence states; serialization loses no information the kernel needs to reason about.
- **No clock, no I/O**: Freshness never reads the system clock and serialization never reads a file — every instant and artifact is supplied as a value; discovering real modification times and persisting JSON are the F08 edge's job.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST provide a JSON serialization of the `Explanation` proof tree (F03) that mirrors the tree's surface shape, records each atomic node's probe name and met/unmet/unknown `Outcome`, and carries each node's rolled-up `Verdict`, with the root node's verdict identical to `Check.eval` over the same check and facts.
- **FR-002**: Serializing an `Explanation` MUST execute **no** probe and MUST emit no un-inspectable function — an `Opaque` node contributes its declared name and recorded outcome only.
- **FR-003**: JSON output MUST be **deterministic** — for a given value the kernel MUST emit byte-for-byte identical JSON every time, with a fixed, stable ordering of object keys and array members, so output is diffable and archivable.
- **FR-004**: The `Explanation` JSON MUST **round-trip** — parsing the emitted JSON MUST yield an `Explanation` equal to the original, with no loss of structure, outcome, or verdict.
- **FR-005**: The kernel MUST provide a `contract` function that folds a catalog of reified rules into a published contract carrying, for each rule, its identity, severity, spec source, and a **rendered** statement of its check produced by `Check.render` (F03).
- **FR-006**: The published contract MUST be **drift-proof** — each rule's statement MUST be the rendered selector itself (not a separately authored string), so the contract cannot diverge from what is enforced; changing a rule's check MUST change its contract statement accordingly.
- **FR-007**: The `contract` fold MUST be **deterministic** and total over any rule catalog, including the empty catalog (which yields an empty contract), and MUST be emittable as both human/agent-readable text and round-trippable JSON.
- **FR-008**: The kernel MUST provide an **evidence-freshness predicate** that reports a recorded piece of evidence as *fresh* when it was recorded at or after the most recent change instant of every artifact it covers, and *stale* when any covered artifact changed after the evidence was recorded.
- **FR-009**: Freshness MUST treat the boundary **inclusively** — evidence recorded at the **same** instant as a covered artifact's last change is *fresh* — and MUST report evidence that covers **no** artifacts as *fresh*.
- **FR-010**: The freshness predicate MUST be a **pure function of the supplied instants** — it MUST read no system clock, no filesystem, no git, and no network, and MUST produce identical results for identical inputs regardless of when it is evaluated.
- **FR-011**: The kernel MUST provide a stable JSON serialization of `EvidenceState` (all six cases, including the computed-only `AutoSynthetic`) in which each case maps to a distinct, stable token that round-trips, and a serialization of an effective-state map (node identity → effective `EvidenceState`) given a supplied projection of node identity.
- **FR-012**: All output in this feature MUST be **domain-neutral and light** — it MUST carry no domain vocabulary, node/rule identity MUST be generic or rendered by a supplied projection, and it MUST add **no dependency beyond the base runtime** (reusing only the JSON facilities that ship with the runtime), preserving the kernel's "light by default" constraint.
- **FR-013**: The feature MUST perform **no I/O** — it operates over values supplied to it (explanations, rule catalogs, evidence records, instants) and reads no real artifacts; discovering artifact modification times, recording verdicts, and persisting JSON are the edge interpreter's responsibility (F08).
- **FR-014**: The public surface introduced by this feature MUST be declared in the curated kernel signature contract, and the kernel's API surface-area baseline MUST be updated to include it (per the repository's surface-drift discipline).

### Key Entities *(include if feature involves data)*

- **JSON explanation**: The serialized form of F03's `Explanation` proof tree — a structured, deterministic, round-trippable document recording each node's kind, the atomic probes' outcomes, and the rolled-up verdict at every node. The portable, inspectable record of *why* a verdict holds.
- **Published contract**: The fold of a reified-rule catalog into one entry per rule — id, severity, spec source, and the **rendered** check. The single-source, drift-proof statement of *what* the kernel enforces, because it is the selector rendered rather than a parallel document.
- **Evidence record (for freshness)**: A recorded piece of evidence carrying the instant it was captured and the set of artifacts it covers (each with a last-change instant). The unit over which freshness is decided.
- **Freshness verdict**: The pure decision — *fresh* or *stale* — that evidence still describes the current artifacts, computed as an inclusive comparison of the recorded instant against the latest covered-artifact change instant.
- **Serialized evidence state**: The stable JSON token for each `EvidenceState` (including the computed-only `AutoSynthetic`) and the serialization of an effective-state map, so F05's declared and computed evidence appears in the same report as the explanation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of `Explanation` proof trees serialize to JSON that mirrors the tree structure, records every atomic node's outcome, and carries a root verdict identical to `Check.eval` — for every check shape (`Atom`, `All`, `Any`, `Not`, `Implies`, `Opaque`).
- **SC-002**: JSON serialization is deterministic — the same value produces byte-for-byte identical JSON across 100% of repeated serializations and across any ordering permutation of the inputs that does not change the value's meaning.
- **SC-003**: Explanations, contracts, and evidence states round-trip — parsing emitted JSON reconstructs a value equal to the original in 100% of cases, with no loss.
- **SC-004**: Serialization and contract folding execute **zero** probes and never read a clock or filesystem — 100% of output is derived purely from supplied values (verified by the absence of any I/O on the surface).
- **SC-005**: The published contract tracks the selector — changing any rule's check changes that rule's contract statement to equal `Check.render` of the new check in 100% of cases, and no contract entry is authored independently of a check (the contract cannot drift).
- **SC-006**: `contract` is total and deterministic — it produces a complete, identical result for any rule catalog including the empty catalog (which yields an empty contract) in 100% of cases.
- **SC-007**: The freshness predicate is correct and inclusive — evidence is reported fresh exactly when recorded at or after the latest covered-artifact change instant (boundary inclusive; no covered artifacts ⇒ fresh) and stale otherwise, in 100% of cases.
- **SC-008**: Freshness is pure — it produces identical results for identical supplied instants regardless of evaluation time and reads no clock, filesystem, git, or network (verified by the absence of any I/O on the surface).
- **SC-009**: The feature adds zero dependencies beyond the base runtime — the entire surface is exercised with nothing beyond the runtime's own JSON facilities and the existing kernel (F01/F03/F05), and the kernel's dependency-hygiene baseline passes unchanged.

## Assumptions

- This feature corresponds to **F06** (`006-explanation-output`) in the implementation plan and depends on **F03** (the reified `Check` algebra — `Explanation`, `Check.render`, `Check.eval`) and **F05** (the evidence model — `EvidenceState`, `effective`), both already merged. It is independent of F07 routing and the F08 edge. It is consumed by F08 (which emits these outputs at the edge) and the CLI (F12, which exposes `explain` / `contract` / evidence-report commands).
- **Freshness is the simple, causal model**: evidence is fresh iff it was recorded at or after the latest change to the artifacts it covers, and stale once any covered artifact changes afterward. An **absolute max-age / TTL** notion of freshness (e.g. "evidence older than N days is stale regardless of artifact changes") is **out of scope** for this "simple freshness" first-product slice; if needed it is a later refinement. Timestamps are supplied as opaque comparable instants — the kernel reads no clock.
- **JSON uses the runtime's built-in facilities** (`System.Text.Json`, which ships with `net10.0`) so the kernel takes **no new package dependency**, consistent with the design's "JSON lives in the kernel because the runtime provides it" rationale. The exact serializer choice, the JSON schema/field names, and the precise `.fsi` signatures are implementation/design details fixed in the plan and the `.fsi`; the spec-level requirements are only the behaviours and invariants stated above (mirror-shape, determinism, round-trip, drift-proof contract, inclusive freshness, purity, domain-neutrality).
- The **contract** is folded from the reified-rule catalog introduced by F03/F04 (the rule carrying a renderable `Check`, an id, a severity, and a spec source). The contract reuses `Check.render` as the single source of each rule's statement so it cannot drift; it does not introduce a new rule type.
- The `Explanation` type is **non-generic** (it records names, outcomes, and verdicts, never `'fact`), so its serialization is straightforward and carries no domain vocabulary; node/rule identity for the contract and the evidence map is generic or rendered through a supplied projection, keeping all output domain-neutral (FR-012).
- **Reading real artifacts and their modification times, dispatching agent reviews, recording verdicts, and persisting/printing the JSON** are out of scope — they belong to the **F08** effects interpreter and the **F12** CLI at the edge. This feature is a pure derivation/serialization over *supplied* values; it never performs I/O (FR-013).
- Governance verdicts (F02) and evidence states (F05) remain **orthogonal dimensions**; this feature serializes each faithfully but does not combine them into a routing decision (whether "passed but on synthetic evidence" blocks is an F07/F10 routing concern, not decided here).
- **Exit / milestone**: completing this feature completes **M1 — the first useful product** (a pure kernel that stores facts, evaluates rules to a fixed point with provenance, taints synthetic evidence, and emits JSON explanations + a drift-proof contract + freshness, with zero heavy dependencies). Packing `FS.GG.Governance.Kernel` to the local NuGet feed is the milestone's exit action; the mechanics of packing are a plan/tasks concern, not a spec requirement.
