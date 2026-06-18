# Feature Specification: Evidence Model & Synthetic Taint — Tracking What's Real and Propagating Doubt

**Feature Branch**: `005-evidence-model`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F05 · `005-evidence-model` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces a new public surface on the governance kernel (the `EvidenceState` value, the `EvidenceGraph` of declared evidence over a dependency DAG, and the `effective` synthetic-taint closure), plus a surface-area baseline update. Pure values and total derivations; no I/O, no agent call, no domain vocabulary. Reading evidence from real artifacts, disclosure logging, and the freshness/explanation output are deferred (F08, F06).

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the engineers and downstream features that need to know **how trustworthy a conclusion's evidence is**: the explanation/freshness output (F06), the routing layer's merge fence (its `evidenceNotSynthetic` blocking rule), and every domain adapter (F09–F11) — notably the self-dogfooding adapter (F10) whose task `TaskDependsOn` graph runs through *this* model rather than a bespoke engine. F01 gave the kernel facts, rules, and a fixed-point evaluator with provenance. This feature adds the orthogonal **evidence dimension**: each piece of work or claim carries a declared *evidence state* (real, synthetic, pending, failed, skipped), and a conclusion that rests — anywhere up its dependency chain — on **synthetic or placeholder evidence** is automatically marked as tainted, so that "this passed, but only on simulated data" can never be silently presented as "this passed."

The keystone behaviour is that **synthetic taint propagates transitively and clears automatically**. The taint (`AutoSynthetic`) is never authored by anyone — it is *computed* by a transitive closure over the dependency graph: any node backed by real evidence that depends, directly or indirectly, on a synthetic node becomes `AutoSynthetic`. When the root-cause synthetic input is later upgraded to real, the taint clears on its own everywhere it had flowed, because the closure is simply recomputed. This is ordinary least-fixed-point dataflow, not a bespoke rules engine, and it generalises past software: a research finding resting on simulated data, or an essay claim resting on an unverified citation, is exactly an `AutoSynthetic` conclusion.

This feature builds directly on F01 (the kernel's identity and fact types and its deterministic, monotone evaluation discipline). It performs **no** I/O and reads **no** real artifacts itself — it operates over *declared* evidence states supplied to it; the act of discovering a node's true evidence state from the filesystem/git and the disclosure logging that surrounds a bypass are the edge interpreter's job (F08).

### User Story 1 - Propagate synthetic taint over a dependency graph (Priority: P1)

A consumer assembles a set of evidence nodes — each a piece of work or a claim with a stable identity and a *declared* evidence state — together with the dependencies between them (which node rests on which), forming a dependency graph. They then ask the kernel for the **effective** evidence state of every node. A node declared synthetic stays synthetic; a node declared real that rests — directly or transitively — on any synthetic (or already-tainted) node is reported as `AutoSynthetic`; every other node keeps its declared state. The taint flows down the entire chain, so a conclusion ten steps removed from a single synthetic input is still marked tainted.

**Why this priority**: This is the entire point of the feature — without the transitive taint closure there is no evidence model, only a list of labels. The propagation rule is the minimum viable derivation that turns declared states into an honest, computed verdict about trustworthiness, and everything else (auto-clear, cycle rejection, state preservation) refines behaviour that only exists once taint can flow at all.

**Independent Test**: Build a graph with one synthetic root and a chain of real nodes depending on it; compute the effective states and confirm every real descendant is `AutoSynthetic` and the synthetic root stays synthetic. Build a graph of real nodes with no synthetic anywhere and confirm every effective state equals its declared state (no taint appears).

**Acceptance Scenarios**:

1. **Given** a node declared synthetic and a real node depending on it, **When** effective states are computed, **Then** the synthetic node is reported synthetic and the dependent real node is reported `AutoSynthetic`.
2. **Given** a chain of real nodes rooted at a single synthetic node, **When** effective states are computed, **Then** every real node transitively above the synthetic root is reported `AutoSynthetic` (the taint reaches the full transitive depth).
3. **Given** a graph in which no node is declared synthetic (and none `AutoSynthetic`, which cannot be declared), **When** effective states are computed, **Then** every node's effective state equals its declared state (no taint is introduced).
4. **Given** a real node that depends on two paths, only one of which leads to a synthetic root (a diamond), **When** effective states are computed, **Then** the node is reported `AutoSynthetic` exactly once (taint is idempotent, not double-counted).

---

### User Story 2 - Taint clears automatically when the root cause is upgraded (Priority: P1)

A consumer who has been shown an `AutoSynthetic` conclusion fixes the underlying problem by replacing the synthetic input with real evidence — re-declaring that root node as real. They recompute the effective states and the taint is gone everywhere it had spread, with no manual bookkeeping: every node that no longer rests on any synthetic node is reported real again. Conversely, if a node still rests on a *different* remaining synthetic input, it stays `AutoSynthetic`.

**Why this priority**: Auto-clear is co-equal with propagation because a taint that does not clear is worse than useless — it would force consumers to hand-maintain taint state and would rot. The guarantee that taint is a pure function of the current declared states (so upgrading the root cause is the only action needed to clear it) is what makes the model trustworthy and is the stated intent of the feature ("clears when the root cause is upgraded").

**Independent Test**: Take a graph whose real descendants are `AutoSynthetic` because of one synthetic root; re-declare that root as real; recompute and confirm all those descendants are now reported real. In a graph with two synthetic roots, upgrade one and confirm only the dependents that rested *solely* on the upgraded root clear, while dependents still resting on the second synthetic root remain `AutoSynthetic`.

**Acceptance Scenarios**:

1. **Given** real nodes reported `AutoSynthetic` because of a single synthetic root, **When** that root is re-declared real and effective states are recomputed, **Then** every one of those nodes is reported real (the taint clears with no other change).
2. **Given** a node resting on two synthetic roots, **When** only one root is upgraded to real, **Then** the node remains `AutoSynthetic` (it still rests on a synthetic input).
3. **Given** the same set of declared states, **When** effective states are computed at two different times, **Then** the results are identical (effective taint is a pure function of the current declared states, carrying no hidden history).

---

### User Story 3 - Reject cyclic dependency graphs (Priority: P2)

A consumer who accidentally (or maliciously) declares a dependency cycle — a node that depends on itself directly, or any loop through several nodes — is refused at graph construction. The evidence dependency structure is a **DAG**; a cyclic declaration is not a valid graph and no taint computation is ever attempted over it. This keeps the taint closure a well-defined least-fixed-point and upholds the kernel's monotonic, no-recursive-negation discipline.

**Why this priority**: Cycle rejection is the validity guarantee that makes the taint closure *terminate and be deterministic* — it is the reason the structure is specified as a DAG and it reinforces the kernel's standing precondition (decision #4: DAG only, no cycles). It ranks below propagation and auto-clear because honest, acyclic graphs are fully usable without it, but it is a required safety property of the surface and ships with the constructors.

**Independent Test**: Attempt to construct a graph containing a self-dependency and confirm it is refused; attempt to construct a graph with a multi-node cycle and confirm it is refused. Construct an acyclic graph and confirm it is accepted and computes effective states.

**Acceptance Scenarios**:

1. **Given** a dependency set in which a node depends on itself, **When** a consumer attempts to construct the evidence graph, **Then** the attempt is refused (no graph is produced).
2. **Given** a dependency set containing a cycle through two or more nodes, **When** a consumer attempts to construct the evidence graph, **Then** the attempt is refused.
3. **Given** an acyclic dependency set, **When** a consumer constructs the evidence graph, **Then** it is accepted and its effective states can be computed.

---

### User Story 4 - Honest, domain-neutral evidence states (Priority: P3)

A consumer records evidence states drawn from the same vocabulary whether the work is software, research, or writing, and the non-synthetic states are carried faithfully. Only synthetic evidence taints: a node declared *failed*, *skipped*, or *pending* that happens to depend on a synthetic node is **not** upgraded to `AutoSynthetic` — taint only ever upgrades a node backed by *real* evidence, and every other declared state passes through unchanged. A node that is itself declared synthetic is reported as synthetic (the root cause), never re-labelled `AutoSynthetic`, so the origin of a taint is always distinguishable from its inheritance.

**Why this priority**: These distinctions round out the model into something honest and reusable across domains, but they refine the core taint rule rather than introduce new machinery. They ship here so the full evidence vocabulary is one coherent contract that F06 (freshness) and the adapters (F10) can consume without re-deciding what each state means.

**Independent Test**: Declare a pending/failed/skipped node depending on a synthetic node and confirm its effective state is unchanged (no taint). Declare a node both synthetic and depending on another synthetic node and confirm it is reported synthetic, not `AutoSynthetic`. Use the same states to model a non-software scenario (a finding on simulated data) and confirm the finding is `AutoSynthetic`.

**Acceptance Scenarios**:

1. **Given** a node declared pending, failed, or skipped that depends on a synthetic node, **When** effective states are computed, **Then** the node retains its declared state (it is not tainted to `AutoSynthetic`).
2. **Given** a node declared synthetic that also depends on another synthetic node, **When** effective states are computed, **Then** the node is reported synthetic (the explicit root cause), not `AutoSynthetic`.
3. **Given** a research finding declared real that rests on a node representing simulated data declared synthetic, **When** effective states are computed, **Then** the finding is reported `AutoSynthetic` (the model is domain-neutral).

---

### Edge Cases

- **Empty graph**: An evidence graph with no nodes computes an empty set of effective states — totally, without error.
- **Real node with no dependencies**: A real node depending on nothing is reported real (there is no synthetic ancestor to taint it).
- **`AutoSynthetic` is computed-only**: `AutoSynthetic` is never a valid *declared* input — only the effective-taint computation may produce it. A consumer cannot author a node as `AutoSynthetic`.
- **Synthetic outranks inherited taint**: A node both declared synthetic and resting on synthetic dependencies is reported synthetic, not `AutoSynthetic` — the declared root-cause label wins so the taint's origin stays visible.
- **Non-real states are inert to taint**: Only a *real* node is ever upgraded by a synthetic dependency. Pending, failed, and skipped nodes resting on synthetic evidence keep their declared state — they are not in scope for the "but only on synthetic evidence" caveat.
- **Diamond / shared ancestors**: A node reachable to a synthetic root by more than one path is tainted once; the closure is idempotent and order-independent.
- **Long chains terminate**: Because the structure is a DAG (cycles are rejected at construction), the taint closure always terminates and is a deterministic least-fixed-point.
- **Disclosure and bypass are out of scope here**: That disclosure is mandatory and a bypass flag never changes a verdict (it logs a justification but leaves the result intact) is a routing/edge concern (F07/F08); this feature only computes the honest effective state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST provide an `EvidenceState` value with exactly six cases — *Pending* (not started), *Real* (backed by real evidence), *Synthetic* (done, but only synthetic/placeholder evidence), *Failed*, *Skipped* (with a rationale), and *AutoSynthetic* (computed: rests on (auto-)synthetic evidence) — naming the quality of the evidence behind a node.
- **FR-002**: `AutoSynthetic` MUST be a **computed-only** state — it is never a valid declared/authored input and is produced solely by the effective-taint computation; the five authored states are *Pending*, *Real*, *Synthetic*, *Failed*, and *Skipped*.
- **FR-003**: The kernel MUST provide a means to construct an **evidence graph** from a set of nodes — each carrying a stable identity and a declared `EvidenceState` — and a set of directed dependency edges (node *depends on* node).
- **FR-004**: Constructing an evidence graph MUST **reject** any dependency set that contains a cycle (including a direct self-dependency); the dependency structure MUST be a DAG, so the taint closure is a well-defined, terminating least-fixed-point (reinforces decision #4: DAG only, no cycles).
- **FR-005**: The kernel MUST provide an `effective` taint function that computes, for every node, its effective evidence state by the rule: a node declared *Synthetic* is *Synthetic*; a node declared *Real* that has any (transitive) dependency whose effective state is *Synthetic* or *AutoSynthetic* is *AutoSynthetic*; every other node keeps its declared state.
- **FR-006**: The taint MUST be **transitive** — `AutoSynthetic` flows down the entire dependency chain to every *Real* node that transitively rests on a synthetic node, regardless of chain length.
- **FR-007**: The taint MUST upgrade **only *Real* nodes** — a node declared *Pending*, *Failed*, or *Skipped* that depends on a synthetic node MUST retain its declared state (it is never re-labelled `AutoSynthetic`).
- **FR-008**: A node declared *Synthetic* MUST be reported as *Synthetic* (the root cause), never `AutoSynthetic`, even when it also depends on synthetic nodes — the explicit declared label outranks inherited taint so the origin remains distinguishable.
- **FR-009**: The taint MUST **clear automatically**: recomputing `effective` after a node's declared state changes from *Synthetic* to *Real* MUST report as *Real* every node that no longer transitively rests on any synthetic node, with no other action required.
- **FR-010**: `effective` MUST be a **deterministic least-fixed-point** — for a given graph it MUST produce identical results irrespective of node ordering, edge ordering, or evaluation order (it carries no hidden history; the result is a pure function of the current declared states and dependencies).
- **FR-011**: `effective` MUST be **total** over every valid (acyclic) graph, including the empty graph — it MUST always return a complete set of effective states and MUST NOT throw or return a partial result.
- **FR-012**: The evidence model MUST be **domain-neutral and light** — node identity MUST be generic (parameterised, carrying no domain vocabulary), the surface MUST reuse the in-assembly F01 kernel types, and it MUST add no heavy dependencies beyond the base runtime, preserving the kernel's "light by default" constraint.
- **FR-013**: The model MUST perform **no I/O** — it operates over *declared* evidence states supplied to it and reads no real artifacts, no filesystem, no git, and no network; discovering a node's true declared state and the surrounding disclosure logging are the edge interpreter's responsibility (F08).
- **FR-014**: The public surface introduced by this feature MUST be declared in the curated kernel signature contract, and the kernel's API surface-area baseline MUST be updated to include it (per the repository's surface-drift discipline).

### Key Entities *(include if feature involves data)*

- **EvidenceState**: The quality of the evidence behind a node — *Pending*, *Real*, *Synthetic*, *Failed*, *Skipped*, or the computed-only *AutoSynthetic*. The dimension the kernel tracks orthogonally to a verdict: *whether* a conclusion holds is separate from *how trustworthy the evidence for it is*.
- **Evidence node**: A piece of work or a claim, identified by a stable (generic) handle and carrying one *declared* `EvidenceState`. The unit over which taint is tracked.
- **EvidenceGraph**: The acyclic dependency graph of evidence nodes — who rests on whom. Validated to reject cycles at construction so the taint closure is a well-defined DAG derivation, not a bespoke engine.
- **Effective taint closure**: The function from an evidence graph to each node's *effective* state — the transitive least-fixed-point that upgrades real nodes resting on synthetic evidence to `AutoSynthetic` and leaves everything else as declared.
- **Synthetic taint**: The propagated "this rests on simulated/placeholder evidence" doubt — declared at its root as *Synthetic*, inherited downstream as the computed `AutoSynthetic`, and cleared automatically once the root is upgraded.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any acyclic graph, a *Real* node that transitively depends on at least one *Synthetic* node is reported `AutoSynthetic` 100% of the time, and a *Real* node with no synthetic ancestor is reported *Real* 100% of the time.
- **SC-002**: Taint reaches the full transitive depth — in a chain of N *Real* nodes rooted at one *Synthetic* node, all N descendants are reported `AutoSynthetic`, for arbitrary N.
- **SC-003**: Upgrading the sole *Synthetic* root to *Real* clears `AutoSynthetic` from 100% of the nodes that had been tainted solely because of it, with no other change to the declared states.
- **SC-004**: `effective` is deterministic — it produces identical effective states across 100% of node-ordering and edge-ordering permutations of the same graph.
- **SC-005**: 100% of cyclic dependency declarations (self-loops and multi-node cycles) are rejected at construction; no effective-taint computation is ever performed over a cyclic graph.
- **SC-006**: `AutoSynthetic` never appears as an authored input (it is not an accepted declared state), and a node declared *Synthetic* is reported *Synthetic* (not `AutoSynthetic`) 100% of the time.
- **SC-007**: Non-*Real* nodes (*Pending*, *Failed*, *Skipped*) depending on synthetic nodes retain their declared state in 100% of cases — no spurious taint is introduced.
- **SC-008**: `effective` is total — there exists no acyclic graph (including the empty graph) for which it throws, errors, or returns a partial result; the empty graph yields an empty result.
- **SC-009**: The feature adds zero heavy dependencies to the kernel — the entire surface is exercised with nothing beyond the base runtime and the existing kernel (F01).

## Assumptions

- This feature corresponds to **F05** in the implementation plan and depends on **F01** (the kernel's identity and fact types and its deterministic, monotone evaluation discipline), already merged. It reuses those types rather than re-implementing any of them. It is independent of F02/F03/F04 (it can be built in parallel after F01) and is consumed by F06 (evidence-freshness predicates and explanation output) and the F10 dogfood adapter (whose `TaskDependsOn` graph runs through this model).
- This feature **reinforces decision #4** (GitHub issue #4): the evidence dependency structure is a **DAG only** — cycles are rejected — keeping the taint a monotone, terminating least-fixed-point with no recursive negation or aggregation.
- The taint semantics are exactly the documented closure: `effective(t) = Synthetic` if declared *Synthetic*; `AutoSynthetic` if declared *Real* and any dependency is effectively (*Auto*)*Synthetic*; otherwise the declared state. This feature implements that rule and does not extend it (e.g. it does not propagate *Failed* or *Skipped*).
- **Reading evidence from real artifacts** (discovering a node's true declared state from the filesystem/git/test output), the **disclosure logging** that surrounds a bypass, and the rule that a **bypass flag never changes a verdict** are out of scope — they belong to the F08 effects interpreter and F07 routing at the edge. This feature is a pure derivation over *declared* states.
- The **evidence-freshness predicates**, the JSON serialization of evidence/explanations, and the rendering of effective states for human/agent consumption are out of scope — they are **F06** (explanation output). This feature only computes the effective taint that F06 will render and that routing's merge fence (`evidenceNotSynthetic`) will gate on.
- Governance verdicts (F02) and evidence states are **orthogonal dimensions**: a rule may pass or fail independently of whether its supporting evidence is real or synthetic. This feature defines only the evidence dimension; combining the two (e.g. "passed but on synthetic evidence is a blocker") is a routing concern (F07/F10), not decided here.
- The exact representation of node identity, the precise shape of the graph constructor and its cycle-rejection result, and the exact signature of `effective` are implementation/design details fixed in the plan and the `.fsi`; the spec-level requirements are only the behaviours and invariants stated above (transitive taint, auto-clear, cycle rejection, real-only upgrade, synthetic-outranks-inherited, determinism, totality, domain-neutrality).
