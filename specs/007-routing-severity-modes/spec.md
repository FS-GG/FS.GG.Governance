# Feature Specification: Routing, Stakes & Run Modes — Light by Default, Always Explained

**Feature Branch**: `007-routing-severity-modes`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F07 · `007-routing-severity-modes` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces a new public surface on the governance kernel (the abstract `ChangeSet`, the `Stakes` classification with its `Fence` classifiers and `stakesOf` combiner, the `RunMode` lifecycle position, and the `Route` decision with `renderRoute`), plus a surface-area baseline update. Pure values and total derivations; no I/O, no agent call, no domain vocabulary. The effects loop that *acts* on a route (running probes, dispatching agent reviews, logging disclosures) is deferred to F08.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the engineers and downstream features that need to decide **how much proof a given change has to earn** before it is allowed to proceed — and to have that decision explain itself. F04 gave the kernel the `CheckRule` bridge (tiers, severity, the review cache key) and `toRule`; this feature sits *above* the rules and decides, for a concrete proposed change, **which rules apply, whether they merely advise or actually block, and why**. The governing principle is **light by default**: an ordinary change earns the minimum proof — no gates — and only a change that trips a declared *fence* (a named high-stakes boundary, e.g. "touches the merge boundary", "edits security-sensitive surface") is escalated, and even then it only *blocks* at the moment it matters (the merge gate). Crucially, **every routing decision carries a human-readable reason** naming the rule, the fence that raised the stakes, and the rendered check — a change is never silently gated and never silently waved through.

The keystone behaviour is that **routing is light, explainable, and deterministic by precedence — never positional**. Classification combines multiple fences by a fixed precedence rule — *forbid trumps permit* — so that if **any** fence classifies a change as high-stakes the change is `Fenced`, regardless of the order the fences were declared or evaluated in. An unclassified change is `Routine` and is routed with no gates. This makes routing a pure function of (the declared fences, the change, the run mode), reproducible byte-for-byte and immune to fence-ordering, which closes the positional-combination hazard (hazard 5 / decision #4).

This feature builds directly on F04 (the `CheckRule` bridge: `CheckTier`, `Severity`, and `toRule`) and through it on F03's reified `Check` (so a route can render the exact check it gates on) and F02's `Verdict`. It performs **no** I/O and runs **no** probes or agent reviews itself — it operates over *declared* fences and an *abstract* change set, producing a routing decision as a pure value; actually executing that decision (sensing facts, running probes, calling judges, logging disclosures) is the F08 effects interpreter's job.

### User Story 1 - Light by default: an ordinary change earns no gates (Priority: P1)

A consumer presents an ordinary change — one that trips none of the declared fences — and asks the kernel to route it. The change is classified `Routine` and the resulting route carries **no blocking gates**: the applicable rules (if any) are advisory only, and the route's reason states plainly that the change is light because no fence matched. The cost of governing an everyday change is therefore near-zero: nothing blocks, nothing demands review, and the explanation says exactly why.

**Why this priority**: This is the entire thesis of the feature — "a change gets only the proof its risk warrants." Without light-by-default, every change pays the full governance cost and the tool becomes friction that teams route around. The `Routine` ⇒ no-gates path is the minimum viable behaviour that makes governance cheap for the common case, and everything else (fencing, run modes, precedence) only refines what happens to the *uncommon*, high-stakes change.

**Independent Test**: Build a routing input with a non-empty set of fences, none of which match the presented change; route it; confirm the stakes are `Routine`, the blocking set is empty, and the route carries a reason indicating no fence matched. Repeat with an empty fence set and confirm the same light outcome.

**Acceptance Scenarios**:

1. **Given** a change that matches none of the declared fences, **When** it is routed, **Then** its stakes are `Routine` and the route contains no blocking gates.
2. **Given** a `Routine` change, **When** it is routed in any run mode, **Then** the route still contains no blocking gates (a routine change is never escalated by run mode alone).
3. **Given** a `Routine` change, **When** it is routed, **Then** the route carries a non-empty reason explaining that the change is light because no fence matched.

---

### User Story 2 - A fenced change produces a blocking gate that names the rule, the fence, and the check (Priority: P1)

A consumer presents a change that trips a declared fence (for example, it touches the merge boundary) and routes it at the merge gate. The change is classified `Fenced` (carrying the name/reason of the fence it tripped), and the route now contains a **blocking gate**: the relevant rule is enforced, not merely advised. The gate's explanation names the **rule** that applies, the **fence** that raised the stakes, and the **rendered check** the gate is asserting — so a reader sees not just *that* the change is gated but *which* requirement must hold and *why* this change is in scope.

**Why this priority**: A governance tool that can fence but cannot explain its fences is untrustworthy — people disable gates they don't understand. Co-equal with light-by-default is the guarantee that when the tool *does* block, it blocks legibly: rule + fence + rendered check. This is the behaviour that earns the tool the authority to stand at the merge boundary.

**Independent Test**: Declare a fence that matches the presented change; route the change at the gate run mode; confirm the stakes are `Fenced` carrying the fence's name, the blocking set is non-empty, and the rendered route names the applicable rule, the fence reason, and the check text (the same text F03's `Check` render produces) — with no probe or agent ever executed.

**Acceptance Scenarios**:

1. **Given** a change that trips a declared fence, **When** it is routed at the gate, **Then** its stakes are `Fenced` carrying that fence's name and the route contains at least one blocking gate.
2. **Given** a fenced change routed to a blocking gate, **When** the route is rendered, **Then** the rendering names the applicable rule, the fence that raised the stakes, and the rendered check the gate asserts.
3. **Given** a fenced change, **When** it is routed, **Then** no probe is run and no agent review is dispatched (routing only *decides*; it does not *act*).

---

### User Story 3 - Run mode decides when a fence actually blocks (Priority: P2)

A consumer routes the **same** fenced change at different points in the lifecycle. In `Sandbox` (free experimentation) and in the `Inner` loop (local development), the fence's requirement is surfaced as **advisory** — it informs without blocking, so iteration stays fast. Only at the `Gate` (the merge boundary) does the same fenced requirement become a **blocking** gate. The run mode is the dial that decides *when* a stake is enforced, separately from *whether* a change is high-stakes at all.

**Why this priority**: Separating "is this high-stakes?" (stakes) from "does it block here?" (run mode) is what keeps the inner loop fast while still guaranteeing enforcement at merge. It refines the fenced-change behaviour of Story 2 rather than introducing it, so it ranks below the two P1 stories, but without it the tool would either block too early (friction) or never block (toothless).

**Independent Test**: Take one fenced change and one blocking-severity rule; route it three times — in `Sandbox`, `Inner`, and `Gate`; confirm the blocking set is empty in `Sandbox` and `Inner` and non-empty only in `Gate`, while the stakes are `Fenced` in all three.

**Acceptance Scenarios**:

1. **Given** a fenced change with a blocking-severity requirement, **When** it is routed in `Sandbox` or `Inner`, **Then** the requirement appears as advisory and the blocking set is empty.
2. **Given** the same fenced change and requirement, **When** it is routed in `Gate`, **Then** the requirement appears as a blocking gate.
3. **Given** any change in any run mode, **When** it is routed, **Then** the stakes classification is identical across run modes (run mode changes enforcement, not classification).

---

### User Story 4 - Deterministic precedence: forbid trumps permit, never positional (Priority: P2)

A consumer declares several fences and routes a change that trips one or more of them. The classification combines the fences by a fixed precedence — **forbid trumps permit** — so the change is `Fenced` if **any** fence matches, and the result is **identical regardless of the order** the fences are declared or evaluated in. There is no "last fence wins" or "first fence wins"; a high-stakes verdict from any single fence dominates, and reordering the fence list never changes the outcome.

**Why this priority**: Positional combination is a latent correctness bug — if the routing outcome depended on fence order, the same change could be gated on one run and waved through on another, destroying reproducibility and trust. Pinning combination to deterministic forbid-trumps-permit precedence (decision #4 / hazard 5) is a required safety property of the surface; it ranks at P2 because honest single-fence use is already correct, but multi-fence determinism must ship with the classifier.

**Independent Test**: Declare a set of fences in which more than one matches the change; compute the stakes; permute the fence order and recompute; confirm the stakes (and the route) are identical across all permutations, and that the change is `Fenced` whenever at least one fence matches.

**Acceptance Scenarios**:

1. **Given** a change that trips at least one of several declared fences, **When** its stakes are computed, **Then** the change is `Fenced` (a single matching fence is sufficient).
2. **Given** the same change and fence set in two different orderings, **When** stakes are computed for each ordering, **Then** the two results are identical (combination is order-independent).
3. **Given** a change that trips no fence, **When** its stakes are computed against any ordering of the fence set, **Then** the change is `Routine` (permit holds only when no fence forbids).

---

### User Story 5 - Every route is short, filterable, and self-explaining (Priority: P3)

A consumer who has routed a change wants to act on the result: see only the blocking gates (to know what must pass before merge), or render the whole route as text for a log or a PR comment. The route exposes its blocking subset as a **filterable, short** list (proportional to the rules that actually apply to this change, not the whole catalog), and `renderRoute` produces a deterministic, human-readable explanation. Every route — routine or fenced — carries a **reason**, so there is never a decision without an explanation attached.

**Why this priority**: This rounds the routing decision into something usable by humans and by F08/F12 (the effects loop and CLI) without re-deriving anything, but it refines presentation over the core decision rather than introducing new routing behaviour, so it ships last. It guarantees the "always explains itself" half of the feature intent is concretely consumable.

**Independent Test**: Route a change with a mix of advisory and blocking requirements; filter to the blocking subset and confirm it contains exactly the blocking gates and is short (bounded by the applicable rules, not the catalog); call `renderRoute` and confirm it produces a deterministic, non-empty text naming the stakes and each applicable requirement; confirm a `Routine` route also renders a non-empty reason.

**Acceptance Scenarios**:

1. **Given** a route with both advisory and blocking requirements, **When** the blocking subset is requested, **Then** it contains exactly the blocking gates and nothing advisory.
2. **Given** any route, **When** `renderRoute` is called, **Then** it produces a deterministic, non-empty explanation that names the stakes and the applicable requirements.
3. **Given** the same routing input, **When** it is routed twice, **Then** the two routes (and their renderings) are byte-for-byte identical.

---

### Edge Cases

- **No fences declared**: With an empty fence set every change is `Routine` and routes with no gates — totally, without error.
- **Empty applicable-rule set**: A `Routine` change (or a fenced change to which no rule applies) routes to an empty blocking set; the route is still well-formed and carries a reason.
- **Multiple fences match**: The change is `Fenced` once; matching is a disjunction (any fence ⇒ fenced), idempotent and order-independent — not double-counted, not order-sensitive.
- **Fenced but not at the gate**: A fenced change in `Sandbox`/`Inner` produces advisory requirements and an empty blocking set — the stake is recorded but not enforced until `Gate`.
- **Routine at the gate**: A routine change at `Gate` still produces no blocking gates — run mode escalates *enforcement of existing stakes*, it does not manufacture stakes.
- **Reason is mandatory**: Every `Route` — routine or fenced, blocking or advisory — carries a non-empty reason; there is no path that yields a decision without an explanation.
- **Render is execution-free**: `renderRoute` (like F03's check render) names the rule, fence, and rendered check **without** running any probe or agent — rendering a route never triggers an effect.
- **Combination is precedence, not position**: Reordering the fence list, or evaluating fences in any order, never changes the stakes or the route (forbid-trumps-permit, decision #4 / hazard 5).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST provide an **abstract `ChangeSet`** value representing a proposed change to be governed, carrying no domain vocabulary — routing MUST operate over it without assuming any particular repository, file, or artifact shape.
- **FR-002**: The kernel MUST provide a `Stakes` classification with exactly two cases — `Routine` (the light default) and `Fenced of string` (high-stakes, carrying the name/reason of the boundary that was tripped).
- **FR-003**: The kernel MUST provide a `Fence` — a named, declared classifier that decides whether a given `ChangeSet` trips that boundary — so that stakes are raised by *declared* high-stakes boundaries rather than hard-coded.
- **FR-004**: The kernel MUST provide a `stakesOf` combiner that classifies a `ChangeSet` against a set of fences, returning `Fenced` (carrying a tripped fence's name) if **any** fence matches and `Routine` otherwise.
- **FR-005**: `stakesOf` MUST combine fences by **deterministic precedence — forbid trumps permit** — so that a single matching fence yields `Fenced` and the result is **independent of the order** in which the fences are declared or evaluated (closes hazard 5 / decision #4: combination is never positional).
- **FR-006**: An **unclassified change MUST default to `Routine`** — with no fences declared, or with no declared fence matching, the change is `Routine` and is routed with **no blocking gates** (light by default).
- **FR-007**: The kernel MUST provide a `RunMode` with exactly three cases — `Sandbox` (free experimentation), `Inner` (local development loop), and `Gate` (the merge boundary) — naming the lifecycle position at which a change is being evaluated.
- **FR-008**: A blocking-severity requirement on a `Fenced` change MUST surface as **advisory** in `Sandbox` and `Inner`, and as a **blocking gate** only in `Gate` — the run mode decides *when* a stake is enforced, separately from *whether* the change is high-stakes.
- **FR-009**: The `Stakes` classification of a change MUST be **identical across run modes** — run mode changes enforcement (advisory vs blocking), not classification.
- **FR-010**: The kernel MUST provide a `Route` value — the routing decision for a (change, fences, run mode) input — that records the change's stakes, the applicable requirements partitioned into advisory and blocking, and a reason.
- **FR-011**: Every `Route` MUST carry a **non-empty reason** — routine or fenced, blocking or advisory — so that no routing decision is ever produced without an explanation.
- **FR-012**: A blocking gate in a `Route` MUST be explainable by **naming the applicable rule, the fence that raised the stakes, and the rendered check** the gate asserts — reusing F03's check rendering so the gated requirement is shown as the exact check, with no drift.
- **FR-013**: The `Route` MUST expose its **blocking subset as a filterable list** that is **short** — bounded by the rules that actually apply to the change, not the full catalog.
- **FR-014**: The kernel MUST provide `renderRoute`, producing a **deterministic, human-readable** explanation of a route that names the stakes and the applicable requirements; `renderRoute` MUST be **execution-free** (it runs no probe and dispatches no agent review).
- **FR-015**: Routing MUST be a **pure, total, deterministic** function of its inputs (change, fences, run mode) — for given inputs it MUST always produce a route (including for the empty fence set and the no-applicable-rule case), MUST NOT throw or return a partial result, and MUST produce byte-for-byte identical routes (and renderings) across repeated and reordered evaluations.
- **FR-016**: Routing MUST perform **no I/O and run no effects** — it runs no probe, calls no agent/judge, touches no filesystem/git/network, and only *decides* a route over declared fences and an abstract change; actually executing the route (sensing facts, running probes, dispatching reviews, logging disclosures) is the F08 effects interpreter's responsibility.
- **FR-017**: The routing surface MUST be **domain-neutral and light** — it MUST reuse the in-assembly F04/F03/F02 kernel types (`CheckRule`, `Severity`, `Check`, `Verdict`), MUST carry no domain vocabulary, and MUST add no heavy dependencies beyond the base runtime, preserving the kernel's "light by default" constraint.
- **FR-018**: The public surface introduced by this feature MUST be declared in the curated kernel signature contract, and the kernel's API surface-area baseline MUST be updated to include it (per the repository's surface-drift discipline).

### Key Entities *(include if feature involves data)*

- **ChangeSet**: An abstract, domain-neutral handle on a proposed change to be governed. The thing routing reasons *about*; its internal shape is opaque to the kernel and supplied by an adapter.
- **Stakes**: The risk classification of a change — `Routine` (light, the default) or `Fenced of string` (high-stakes, naming the boundary tripped). The dimension that decides how much proof a change must earn.
- **Fence**: A named, declared classifier that decides whether a `ChangeSet` trips a high-stakes boundary (e.g. the merge boundary, a security-sensitive surface). Stakes are raised by declared fences, combined by forbid-trumps-permit precedence.
- **RunMode**: The lifecycle position at which a change is evaluated — `Sandbox`, `Inner`, or `Gate`. The dial that decides *when* a stake is enforced (advisory vs blocking), orthogonal to *whether* the change is high-stakes.
- **Route**: The routing decision for a (change, fences, run mode) input — the stakes, the applicable requirements partitioned into advisory and blocking, and a mandatory reason. Always self-explaining via `renderRoute`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A change that trips no declared fence is classified `Routine` and routed with an empty blocking set 100% of the time (light by default), for any run mode.
- **SC-002**: A change that trips at least one declared fence is classified `Fenced` (carrying a fence name) 100% of the time, regardless of how many fences match or in what order they are declared.
- **SC-003**: `stakesOf` is order-independent — it produces identical stakes across 100% of permutations of the fence set for the same change (forbid trumps permit, never positional).
- **SC-004**: The same blocking-severity requirement on a fenced change appears as advisory in `Sandbox` and `Inner` and as a blocking gate in `Gate` 100% of the time, while the stakes classification is identical across all three run modes.
- **SC-005**: Every route — routine or fenced — carries a non-empty reason; there exists no routing input that yields a route without an explanation.
- **SC-006**: Every blocking gate names its rule, the fence that raised the stakes, and the rendered check; the rendered check is byte-for-byte the text F03's check render produces for that rule (no drift).
- **SC-007**: The blocking subset of a route is bounded by the rules that apply to the change (never the full catalog), and the filtered subset contains exactly the blocking gates.
- **SC-008**: Routing is deterministic — it produces byte-for-byte identical routes and `renderRoute` output across 100% of repeated and reordered evaluations of the same input.
- **SC-009**: Routing is total — there exists no input (including the empty fence set and the no-applicable-rule case) for which it throws, errors, or returns a partial result.
- **SC-010**: Routing executes zero effects — across the full test suite no probe is run and no agent review is dispatched by any routing or rendering call.
- **SC-011**: The feature adds zero heavy dependencies to the kernel — the entire surface is exercised with nothing beyond the base runtime and the existing kernel (F02/F03/F04).

## Assumptions

- This feature corresponds to **F07** in the implementation plan and **starts Milestone M2** (light routing + the effects edge). It depends on **F04** (`004-checktier-rule-bridge`: `CheckTier`, `Severity`, the review cache key, and `toRule`), and through F04 on **F03** (the reified `Check` and its render, reused so a gate shows the exact check) and **F02** (`Verdict`) — all already merged. It reuses those in-assembly kernel types rather than re-implementing any of them.
- This feature **reinforces decision #4** (GitHub issue #4) and closes **hazard 5**: stakes combination over multiple fences is **deterministic by precedence (forbid trumps permit), never positional** — reordering or re-evaluating fences never changes the outcome.
- Routing is **pure**: it *decides* a route over an abstract change and declared fences but **runs nothing**. Sensing the facts that probes need, executing probes, dispatching agent/judge reviews, applying the review cache, and logging disclosures all belong to the **F08 effects interpreter** (the MVU/Elmish boundary), which consumes the `Route` this feature produces. Principle IV (Elmish/MVU) is therefore **not applicable** to this pure feature.
- The **abstract `ChangeSet`** carries no domain vocabulary; concrete change shapes (Spec Kit phases/tasks, design-system tokens, file diffs) are supplied by the adapters (F09–F11). Likewise concrete fences (e.g. the Spec Kit `mergeFence`, a security-surface fence) are declared by adapters and by the host, not hard-coded here.
- `Severity` (Advisory/Blocking) comes from **F04** and is orthogonal to `Stakes`: severity is a property of a *rule*, stakes is a property of a *change*. A blocking-severity rule only produces an actual blocking gate when the change is `Fenced` **and** the run mode is `Gate`; how exactly severity and run mode compose into the advisory/blocking partition is an implementation detail fixed in the plan and the `.fsi`, constrained only by FR-008.
- The **disclosure discipline** (that a bypass logs a justification but never silently changes a verdict) and the **evidence/freshness gating** at the merge fence (e.g. an `evidenceNotSynthetic` blocking rule consuming the F05/F06 effective taint) are surfaced here only as *routing inputs/outputs*; their enforcement and logging are the F08 edge's job, not decided in this pure feature.
- The exact representation of the abstract `ChangeSet`, the precise shape of `Fence` and the `stakesOf` signature, the internal partition of a `Route` into advisory/blocking lists, and the exact text of `renderRoute` are implementation/design details fixed in the plan and the `.fsi`; the spec-level requirements are only the behaviours and invariants stated above (light by default, fenced-gate explainability, run-mode enforcement, deterministic forbid-trumps-permit precedence, mandatory reason, purity/totality/determinism, domain-neutral lightness).
