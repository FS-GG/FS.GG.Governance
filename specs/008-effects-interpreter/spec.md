# Feature Specification: The Effects Edge — Sense → Plan → Act, with Nondeterminism Reified as Evidence

**Feature Branch**: `008-effects-interpreter`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "create specs for the next item in the project." (resolved to F08 · `008-effects-interpreter` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** — introduces the first **impure** component of the system: a new effects-shell module (the `Model`/`Msg`/`Effect`/`init`/`update` boundary plus an edge interpreter) that gathers facts from real artifacts, runs the already-shipped pure kernel over them, dispatches agent reviews, and records verdicts — with a curated public signature contract and a surface-area baseline update. This is the **Elmish/MVU boundary feature** (Constitution Principle IV applies for the first time): `update` is pure and I/O is reified as data; only the edge interpreter performs effects. It **completes the effects half of Milestone M2** and is consumed by the F12 CLI.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the host application and the F12 CLI that must actually **run governance against a real project**: read the artifacts a change touches, evaluate the kernel's rules over what they find, ask an AI judge the questions only it can answer, freeze those answers as durable evidence, and re-run cheaply without re-asking what is already settled. Everything below the kernel (F01–F07) is a pure value derivation — it *decides* but never *acts*. This feature is the **imperative shell** that closes the loop: it **senses** facts from the world, **plans** by running the pure kernel, and **acts** on the resulting effects, turning each real-world result back into an event the pure core consumes.

The keystone behaviour is that **the hard, untrustworthy part of governance — I/O and a stochastic AI judge — is made observable and safe by reifying it as data**. The state of the loop is a plain `Model`; every external interaction (reading an artifact, dispatching a review, recording a verdict) is an `Effect` *value the pure `update` requests but does not run*; and every result (a file's contents, a judge's verdict, a failure) comes back as a `Msg`. The `update` function is a pure, total transition from `(Model, Msg)` to `(Model, Effect list)` — so the entire decision logic of the loop is exhaustively testable without any I/O, and the nondeterministic edge is confined to one interpreter that can be exercised against a real filesystem and a fake judge. A stochastic verdict, once accepted, is **frozen as evidence** keyed by the F04 content-hash cache key, so a re-run **hits the cache and never re-dispatches** — nondeterminism enters once, is recorded, and is thereafter reproducible.

This feature builds directly on **F07** (it consumes a `Route` and acts on it — enforcing blocking gates at the `Gate` boundary), **F06** (it emits the JSON explanation and the published contract at the edge — alongside the F07 rendered route; evidence-freshness *emission* is deferred to F12), **F04** (it dispatches the `NeedsReview` requests `toRule` produces and records the `RecordedReview` facts that hit the cache on re-run, folding in the judge identity), and through them the whole pure kernel (F01–F03, F05). It performs the I/O those pure features deliberately deferred. It **locks decision #2** (a stochastic verdict is aggregated / meets a confidence threshold before it is frozen) and **decision #3** (a governed artifact is untrusted data: the reviewer instruction is isolated from the artifact content so a malicious artifact cannot rewrite the judge's task), and it **opens decision #5** (a cost/latency budget for reviews plus a judge-vs-human meta-validation loop) as a tracked deferral.

### User Story 1 - A pure sense→plan→act core: I/O is data, `update` never touches the world (Priority: P1)

A consumer drives the governance loop entirely through values: it builds an initial `Model`, feeds the pure `update` a `Msg` (for example, "this artifact's contents arrived" or "the judge answered"), and receives the next `Model` plus a list of `Effect` values describing the I/O the loop now *wants* performed — **without `update` performing any of it**. The consumer can assert the exact next state and the exact requested effects in a unit test, with no filesystem, no network, and no agent. The loop's entire decision logic is a plain, deterministic function of state and events.

**Why this priority**: This is the thesis of the boundary feature and the property the constitution's Principle IV exists to guarantee: state transitions become plain values that can be tested exhaustively, and I/O becomes an explicit contract. Without a pure `update` that only *requests* effects, the impure shell is an untestable tangle — exactly the opacity a governance tool is supposed to eliminate for others. Everything else (real reads, real dispatch, freezing, caching) is an interpretation of the effects this core emits, so this story is the minimum viable boundary.

**Independent Test**: Construct a `Model` and a representative `Msg`; call `update`; assert the returned `Model` and the returned `Effect` list are exactly as expected; confirm by construction that no file was read, no process spawned, and no agent called (the call is a pure function evaluated in a test with no edge present).

**Acceptance Scenarios**:

1. **Given** an initial `Model` and an artifact-read-result `Msg`, **When** `update` is called, **Then** it returns the next `Model` (the sensed facts now in scope) and an `Effect` list, and performs no I/O itself.
2. **Given** a `Model` with no pending work, **When** `update` is driven to quiescence, **Then** it emits an empty `Effect` list (the loop is done) and the final `Model` carries the complete fact set.
3. **Given** identical `(Model, Msg)` inputs, **When** `update` is called repeatedly, **Then** it returns byte-for-byte identical `(Model, Effect list)` results (the pure core is deterministic).

---

### User Story 2 - The edge interpreter executes effects against the real world and feeds results back as messages (Priority: P1)

A consumer runs the loop against a **real project**: the edge interpreter takes each `Effect` value the pure core emitted — *read this artifact*, *dispatch this review*, *record this verdict* — performs the actual I/O (reads the file, calls the injected judge port, writes to the review store), and turns each result back into a `Msg` that re-enters `update`. Sensing, planning, and acting compose into one driven loop: facts gathered from artifacts feed the kernel, the kernel's review requests become dispatches, and the judges' answers become recorded evidence — all while the only impure code is this one interpreter.

**Why this priority**: A pure core that nothing drives is inert. Co-equal with the pure boundary is an interpreter that actually closes the loop against real artifacts and a real (or faithfully faked) judge, so the feature delivers a runnable `sense → plan → act` cycle rather than a value algebra. This is the half that earns the feature its "effects edge" name.

**Independent Test**: Point the interpreter at a **real filesystem fixture** (a small tree of governed artifacts) and an injected fake judge; run the loop from `init` to quiescence; confirm the artifacts were actually read, the expected reviews were dispatched to the judge port, the verdicts were recorded to the store, and the final `Model` reflects the evaluated kernel result — with the pure `update` unchanged from Story 1.

**Acceptance Scenarios**:

1. **Given** a `ReadArtifact` effect and a real fixture file, **When** the interpreter runs it, **Then** the file's contents are read and delivered back as a Msg whose facts enter the next `update`.
2. **Given** a `DispatchReview` effect, **When** the interpreter runs it against the injected judge port, **Then** the judge's verdict is delivered back as a Msg.
3. **Given** the full loop from `init`, **When** it is driven to quiescence against the fixture, **Then** the produced fact set equals what the pure kernel yields over the same sensed facts (the edge adds sensing and dispatch, never new logic).

---

### User Story 3 - A recorded verdict is frozen as evidence and the cache hits on re-run (Priority: P1)

A consumer runs governance over a change that needs an agent review; the judge answers; the verdict is **recorded as durable evidence** keyed by the F04 content-hash cache key (which folds in the judge's identity, the check's structure, the artifact contents, and the prompt). On a **second run over an unchanged change**, the loop finds the recorded verdict, **short-circuits without dispatching a new review**, and reaches the same decision. Nondeterminism entered the system exactly once; thereafter the result is reproducible and free.

**Why this priority**: This is the F08 exit criterion — "sense→plan→act with nondeterminism reified as evidence." It is what makes an AI-judged governance loop affordable and reproducible: without freezing-and-caching, every run re-asks the judge, costs accrue, and the answer drifts. The round-trip-then-cache-hit is the concrete proof that the stochastic edge has been tamed into a deterministic, audited value.

**Independent Test**: Run the loop over a change requiring review against a fake judge that records how many times it was called; confirm exactly one dispatch and one recorded verdict on the first run; re-run over the identical change and confirm **zero** dispatches (a cache hit), the same final decision, and that mutating any cache-key ingredient (judge version, check, artifact contents, or prompt) forces a fresh dispatch.

**Acceptance Scenarios**:

1. **Given** a change requiring an agent review, **When** the loop runs the first time, **Then** exactly one review is dispatched and its verdict is recorded against the F04 cache key.
2. **Given** a recorded verdict and an unchanged change, **When** the loop runs again, **Then** no review is dispatched (cache hit) and the recorded verdict is used.
3. **Given** a recorded verdict and a change in which one cache-key ingredient differs (judge identity, check, artifact content, or prompt), **When** the loop runs again, **Then** a fresh review is dispatched (the stale verdict is not reused).

---

### User Story 4 - A stochastic verdict is aggregated / meets a confidence threshold before it is frozen (Priority: P2)

A consumer governs a high-stakes change whose verdict comes from a stochastic judge. Before that verdict is **frozen** as durable evidence, the loop applies a configurable acceptance policy — aggregate N samples and/or require a confidence threshold — so a single noisy answer cannot be cemented. If the policy is satisfied, the verdict is recorded; if not, the conclusion stays **`Uncertain`** (a review still pending), never silently promoted to a definite pass or fail.

**Why this priority**: This locks decision #2. A frozen verdict is treated as settled fact downstream (and cached), so freezing a single low-confidence sample would launder judge noise into durable evidence. The aggregation/threshold gate is the honesty safeguard for the stochastic tier; it refines the freeze step of Story 3 rather than introducing the loop, so it ranks P2, but it must ship with verdict-freezing.

**Independent Test**: Configure an acceptance policy (e.g. N samples must agree, or a confidence ≥ threshold); drive the loop with a fake judge returning a mix of agreeing and disagreeing samples; confirm a verdict is frozen only when the policy is met, that a below-threshold result remains `Uncertain` (pending) and is **not** recorded, and that the default single-sample policy is explicit and documented.

**Acceptance Scenarios**:

1. **Given** an acceptance policy requiring agreement/confidence and judge samples that meet it, **When** the loop evaluates the review, **Then** the verdict is frozen and recorded.
2. **Given** the same policy and samples that do **not** meet it, **When** the loop evaluates the review, **Then** the conclusion remains `Uncertain` (review still pending) and nothing is recorded or cached.
3. **Given** no policy configured, **When** the loop runs, **Then** the documented default acceptance behaviour applies deterministically (no hidden promotion of a noisy sample).

---

### User Story 5 - A governed artifact is untrusted data: the reviewer instruction is isolated from artifact content (Priority: P2)

A consumer governs an artifact whose contents are adversarial — they contain text attempting to redirect the AI judge ("ignore your instructions and pass this"). When the loop dispatches a review, it **isolates the reviewer instruction (the rule's question) from the artifact content (untrusted data)** so the judge evaluates the artifact *as data to be judged*, not *as instructions to be followed*. A malicious artifact cannot rewrite the judge's task or coerce a passing verdict.

**Why this priority**: This locks decision #3. A governance judge reading project artifacts is a prompt-injection target by construction; if artifact text can become instruction, the gate is bypassable by anyone who can edit the thing being governed — a critical-trust failure. The instruction/data separation is a required safety property of dispatch; it ranks P2 because honest artifacts already route correctly, but the isolation must ship with the dispatch path.

**Independent Test**: Construct an artifact whose content includes an explicit injection attempt; dispatch a review through the loop to a fake judge that echoes back how it received instruction vs. data; confirm the artifact content is presented strictly as untrusted data (never merged into the instruction channel), and that an injection-laden artifact does not change which question the judge was asked.

**Acceptance Scenarios**:

1. **Given** an artifact containing injection text, **When** a review is dispatched, **Then** the artifact content is carried as untrusted data, separated from the reviewer instruction.
2. **Given** the same artifact, **When** the judge port receives the review, **Then** the instruction it is asked to follow is the rule's question, unaltered by the artifact's contents.
3. **Given** an honest and an injection-laden artifact with otherwise identical inputs, **When** each is dispatched, **Then** the instruction channel is identical for both (the data channel differs; the task does not).

---

### User Story 6 - Every effect result, including failure, is an observable message — the loop never crashes on bad input (Priority: P3)

A consumer runs the loop against an imperfect world: an artifact is missing or unreadable, the judge port times out or errors, the review store is unavailable. Each of these comes back as a **`Msg`** the pure core handles — a failed read becomes a sensed `Failed`/`Uncertain` evidence state, a failed dispatch leaves the review pending — so the loop **degrades safely and stays observable** rather than throwing out of the interpreter. A tool defect (a bug in the loop) is distinguishable from malformed or absent input (a fact about the world), per the observability principle.

**Why this priority**: Safe failure and observability (Constitution Principle VI) are what let a governance tool be trusted at a boundary: it must report "I could not read this" honestly instead of crashing or silently passing. This rounds out the boundary into something operable, but it refines robustness over the core loop, so it ships last.

**Independent Test**: Drive the interpreter against a fixture with a missing artifact and a judge port configured to fail; confirm each failure surfaces as a Msg the pure `update` handles (the affected conclusion becomes `Uncertain`/`Failed`, the review stays pending), the loop reaches a well-formed final `Model` without throwing, and the final state distinguishes "input was bad/absent" from any internal defect.

**Acceptance Scenarios**:

1. **Given** a `ReadArtifact` effect for a missing/unreadable artifact, **When** the interpreter runs it, **Then** the failure returns as a Msg and the affected conclusion becomes `Uncertain`/`Failed` (never a silent pass, never a crash).
2. **Given** a `DispatchReview` effect whose judge port fails, **When** the interpreter runs it, **Then** the failure returns as a Msg, the review stays pending, and the loop continues.
3. **Given** any sequence of effect failures, **When** the loop is driven to quiescence, **Then** it terminates with a well-formed `Model` and emits no unhandled exception.

---

### Edge Cases

- **Nothing to do**: `init` over a change that touches no artifact and needs no review reaches quiescence with an empty effect list and a well-formed, empty-derivation `Model`.
- **Cache hit, no dispatch**: a recorded verdict for the current cache key suppresses the `DispatchReview` effect entirely — the loop must not emit a dispatch it will only discard.
- **Stale cache, fresh dispatch**: any change to a cache-key ingredient (judge identity, check structure, artifact content hash, or prompt) yields a different key, so a prior verdict is not reused.
- **Below-confidence verdict**: a stochastic answer that fails the acceptance policy is **not** frozen and **not** cached — the conclusion stays `Uncertain` (pending), so the next run re-dispatches.
- **Injection-laden artifact**: artifact content that attempts to redirect the judge is carried only on the untrusted-data channel; the instruction channel is unaffected.
- **Failed read / failed dispatch / unavailable store**: each is reified as a `Msg` and handled in `update`; none throws out of the interpreter (safe failure).
- **Gate recomputes from base**: when acting on a `Gate`-mode `Route`, enforcement recomputes from the base independently of any local run mode, so a locally-sandboxed state cannot be landed un-gated.
- **Disclosure never changes a verdict**: a bypass/override logs a justification as an observable record but never silently flips a verdict — honesty about evidence is separate from the freedom to iterate.
- **Re-entrant / duplicate messages**: applying the same effect-result `Msg` twice is idempotent — it does not double-record a verdict or double-count a fact (identity is by the kernel's `FactId` and the cache key).
- **Effect ordering**: the final `Model` is independent of the order in which the interpreter happens to complete independent effects (the pure kernel's order-independence is preserved across the edge).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST expose an **Elmish-style boundary** (Constitution Principle IV): a `Model` (the durable loop state — sensed facts, pending reviews, recorded verdicts), a `Msg` (effect results and internal transitions), an `Effect`/`Cmd<Msg>` (the I/O the loop requests but does not perform), an `init` (initial `Model` plus startup effects), a pure `update`, and an edge interpreter that executes effects and turns results back into `Msg`.
- **FR-002**: `update` MUST be **pure and total** — a deterministic function from `(Model, Msg)` to `(Model, Effect list)` that performs **no I/O** (no filesystem, network, process, clock, or agent call) inside itself and never throws; for identical inputs it MUST produce identical outputs.
- **FR-003**: All I/O MUST be **reified as `Effect` values** — at minimum *read an artifact*, *dispatch a review*, and *record a verdict* — that the pure core only *requests*; effects MUST be executed **only** by the edge interpreter, never inside `update`.
- **FR-004**: The interpreter MUST **execute each effect and feed its result back as a `Msg`**, including failures (a failed read, a failed/timed-out dispatch, an unavailable store), so that every real-world outcome re-enters the pure core as an event.
- **FR-005**: The loop MUST **sense** facts by interpreting `ReadArtifact` effects and asserting the resulting artifact-content facts into the `Model`'s fact set, so the pure kernel evaluates over what was actually found in the world.
- **FR-006**: The loop MUST **plan** by running the already-shipped **pure kernel** (F01 fixed-point evaluation over the F04-bridged rules) over the sensed facts — emitting `DispatchReview` effects for the `NeedsReview` requests `toRule` produces and treating `Decided` outcomes as settled — adding **no new decision logic** beyond sensing, dispatch, and recording.
- **FR-007**: The loop MUST **act** by interpreting `DispatchReview` against an **injected judge port** (not a hard-wired transport) and `RecordVerdict` against an injected review store, recording an accepted agent verdict as a `RecordedReview` fact keyed by the **F04 content-hash cache key** (judge identity + check hash + artifact hashes + prompt hash).
- **FR-008**: On a re-run over an unchanged change, a **recorded verdict matching the cache key MUST short-circuit the review** — no `DispatchReview` effect is emitted and the recorded verdict is used (a cache hit); a change to any cache-key ingredient MUST force a fresh dispatch.
- **FR-009**: A **stochastic verdict MUST be frozen only when a configurable acceptance policy is met** (aggregate N samples and/or a confidence threshold — decision #2); a verdict that fails the policy MUST remain `Uncertain` (review still pending) and MUST NOT be recorded or cached. The default policy MUST be explicit and deterministic.
- **FR-010**: When dispatching a review, the loop MUST **isolate the reviewer instruction (the rule's question) from the governed artifact content (untrusted data)** so artifact content can never be interpreted as instruction (decision #3 — prompt-injection safety); the instruction channel MUST be independent of the artifact's contents.
- **FR-011**: The loop MUST **consume an F07 `Route` and act on it** — enforcing blocking gates only when the `Route` is in `Gate` mode, and recomputing enforcement from the base independently of any local run mode, so a locally-sandboxed state cannot be landed past a gate un-checked. *Enforcement within this feature* means the loop **computes and exposes** the `Route` (with its `Blocking` partition populated only at `Gate`) recomputed from the base fences/rules at `init`; the loop performs **no separate halting effect** — refusing to land on a non-empty `Blocking` is the host/F12's action, not an effect the loop emits.
- **FR-012**: The loop MUST **degrade safely and observably** (Constitution Principle VI): a failed read/dispatch/record MUST surface as a `Msg` the pure core handles (the affected conclusion becomes `Uncertain`/`Failed`, a review stays pending), MUST NOT throw out of the interpreter, and MUST keep a **tool defect distinguishable from malformed or absent input**.
- **FR-013**: A **bypass/override MUST be recorded as an observable disclosure** (a logged justification) but MUST NEVER silently change a verdict — disclosure of synthetic/bypassed evidence is mandatory and is separate from the run-mode freedom to iterate. The `Disclosed` message is **supplied by the host/caller** (e.g. an F12 run-mode bypass); F08 only **records** it (appending to the observable disclosure log) and never lets it flip a verdict — it originates no disclosure of its own.
- **FR-014**: Applying the same effect-result `Msg` more than once MUST be **idempotent** — it MUST NOT double-record a verdict or double-count a fact — and the final `Model` MUST be **independent of the completion order** of independent effects (the kernel's order-independence is preserved across the edge).
- **FR-015**: The feature MUST **emit the edge outputs** at the edge — the F06 JSON explanation, the F06 published contract, and the F07 rendered route (the `Output` cases `ExplanationJson` / `ContractJson` / `RouteText`) — these are the persisted/printed artifacts the pure F06/F07 layers produced as values and deferred emitting. Evidence-freshness (`Freshness.decide`) is computed as an F06 value but its **report emission is deferred to F12**; it is **not** an `Output` case in this feature.
- **FR-016**: Semantic tests MUST cover **both sides of the boundary**: pure transition tests (`(Model, Msg) ⇒` next `Model` + effects) asserted with no I/O, and interpreter tests executed against a **real filesystem fixture** and a fake judge port (Constitution Principles IV and V — prefer real evidence).
- **FR-017**: The feature MUST live in a **new effects-shell component separate from the pure kernel**, depend on the kernel (F01–F07) without making the kernel depend on it, and keep its dependency footprint **light** — the judge transport and the store are **injected ports**, so the loop is exercised end-to-end with no real network and no real agent.
- **FR-018**: The public surface introduced by this feature MUST be declared in a **curated signature contract** (`.fsi`) and the API **surface-area baseline MUST be updated** to include it (per the repository's surface-drift discipline).

### Key Entities *(include if feature involves data)*

- **Model**: The durable state the governance loop owns — the facts sensed so far, the reviews pending, and the verdicts recorded. The single value the pure transitions read and rewrite.
- **Msg**: An event the loop accepts — an artifact-read result (success or failure), an agent verdict (or dispatch failure), a record-verdict outcome, or an internal transition. The only way the world (or the loop) advances the `Model`.
- **Effect**: A *request for I/O as data* — read an artifact, dispatch a review, record a verdict — that the pure `update` emits and only the edge interpreter performs. The explicit, auditable contract of everything the loop touches outside itself.
- **Judge port**: The injected boundary to the AI reviewer — given an isolated instruction (the rule's question) and untrusted data (the artifact content), it returns a verdict. Injected so the loop is testable without a real agent.
- **Review store**: The injected boundary where recorded verdicts persist, keyed by the F04 content-hash cache key, so a settled verdict survives to short-circuit a re-run.
- **Acceptance policy**: The configurable rule (aggregate N / confidence threshold) that decides whether a stochastic verdict is trustworthy enough to **freeze** as durable evidence.
- **Recorded verdict (frozen evidence)**: A stochastic answer that met the acceptance policy, recorded against its cache key — nondeterminism captured once as a reproducible value.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of `update` transitions are exercised as pure functions — every transition test asserts the next `Model` and emitted effects with **zero** I/O performed (no file, process, network, clock, or agent), confirming I/O is fully reified as data.
- **SC-002**: The full `sense → plan → act` loop runs end-to-end against a **real filesystem fixture** and an injected fake judge, producing a final fact set equal to what the pure kernel yields over the same sensed facts.
- **SC-003**: An agent verdict round-trips and the cache hits on re-run — a second run over an unchanged change dispatches **zero** new reviews, while changing any single cache-key ingredient forces exactly one fresh dispatch.
- **SC-004**: A stochastic verdict that fails the configured acceptance policy is frozen **0%** of the time (it stays `Uncertain`/pending and is never recorded or cached), and one that meets the policy is frozen deterministically.
- **SC-005**: An injection-laden artifact does not alter the reviewer instruction in 100% of dispatch cases — the instruction channel is byte-for-byte identical to the honest-artifact case; only the untrusted-data channel differs.
- **SC-006**: Every interpreted effect result, **including every failure mode** (missing/unreadable artifact, failed/timed-out dispatch, unavailable store), surfaces as a handled `Msg`; there exists no driven input that makes the loop throw an unhandled exception or reach quiescence in a malformed `Model`.
- **SC-007**: The loop is order- and repetition-robust — the final `Model` is identical across permutations of independent-effect completion order, and re-applying the same effect-result `Msg` records no duplicate verdict and no duplicate fact.
- **SC-008**: Enforcement at a `Gate`-mode `Route` is recomputed from the base independently of local run mode — a state developed in `Sandbox`/`Inner` cannot pass a blocking gate without the gate evaluating it afresh.
- **SC-009**: The effects shell adds no heavy/unnecessary dependency and never reaches a real network or a real agent in the test suite — the judge and store are injected ports, exercised entirely with fakes and a real local filesystem fixture.
- **SC-010**: Both sides of the boundary are covered by semantic tests through the built component (pure transition tests + a real-filesystem interpreter test + an FSI transcript driving `init`/`update`), with the pure `update` carrying no I/O and the interpreter carrying all of it.

## Assumptions

- This feature corresponds to **F08** (`008-effects-interpreter`) in the dated implementation plan and **completes the effects half of Milestone M2** (light routing + the effects edge). It depends on **F07** (`Route`/`renderRoute`, run modes) and **F06** (the JSON explanation and contract it emits at the edge; freshness *emission* deferred to F12), and through them on **F04** (`toRule`, the `NeedsReview`/`RecordedReview` outcomes, the `cacheKey`, the `JudgeId`) and the rest of the pure kernel (F01–F03, F05) — all already merged. It is consumed by **F12** (the CLI, which wires its commands to this interpreter) and reused by **F13** (external validation).
- This is the **Elmish/MVU boundary feature** — Constitution **Principle IV applies for the first time**. Unlike F01–F07 (pure derivations where MVU was N/A), this feature has multi-step state and external I/O, so it MUST expose `Model`/`Msg`/`Effect`/`init`/`update` + an edge interpreter, with `update` pure and effects interpreted only at the edge. A **local MVU/effect algebra** is acceptable (the constitution permits this for libraries/tools) provided the same separation holds; whether the Elmish package is used as the runtime is a plan/`.fsi` decision, constrained only by FR-001–FR-004.
- This feature **locks decision #2** (aggregate N runs / require a confidence threshold before freezing a verdict) and **decision #3** (reviewer prompt-injection: governed artifacts are untrusted data; isolate instruction vs. data in the review prompt), and **opens decision #5** (a cost/latency budget for reviews plus a judge-vs-human meta-validation loop) as a tracked deferral, not implemented here.
- The **AI judge and the review store are injected ports**, not concrete transports/back-ends. The kernel and this loop stay agnostic to *how* a judge is reached or *where* verdicts are stored; the host/CLI (F12) supplies concrete implementations. Tests use a fake judge and a real local-filesystem store fixture so the whole loop runs with **no real network and no real agent** (Principle V — the evidence is real I/O against a real filesystem, with only the stochastic agent faked, since a real agent cannot be a reproducible test oracle).
- The **acceptance policy** for freezing a stochastic verdict (decision #2) is **configurable**; the precise default (e.g. single-sample pass-through vs. an N-agreement default) is an implementation detail fixed in the plan and `.fsi`, constrained only by FR-009 (a below-policy verdict is never frozen, and the default is explicit and deterministic).
- The **exact shapes** of `Model`, `Msg`, and `Effect`, the precise interpreter signature, the judge-port and store-port interfaces, the disclosure/log record format, and the wiring of `Route` enforcement are implementation/design details fixed in the plan and the curated `.fsi`; the spec-level requirements are only the behaviours and invariants stated above (pure `update`, I/O-as-data, sense→plan→act, freeze-then-cache, aggregation-before-freeze, instruction/data isolation, safe-failure observability, gate-recompute-from-base, light injected ports).
- A **structured-logging library** has not yet been selected (deferred TODO in the constitution); this feature surfaces disclosures/observability as values and via the host's logging, and the concrete logging dependency, if any, is recorded in an ADR (`docs/decisions/`) before or during implementation, per the dated plan.
- **Out of scope** (deferred to later features): the **adapter SPI and composition root** (F09 — the coproduct `ProjectFact`, cross-domain precedence), the concrete **Spec Kit / design-system adapters** (F10/F11), the **CLI command surface** (F12), and the **cost/latency budget + judge-vs-human meta-validation** (decision #5, F12+). This feature provides the generic effects edge those features wire into; it ships no domain adapter and no end-user command surface of its own.
</content>
