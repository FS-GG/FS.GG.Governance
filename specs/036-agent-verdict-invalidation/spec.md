# Feature Specification: Agent-Reviewed Verdict Store & Invalidation Decision Core

**Feature Branch**: `036-agent-verdict-invalidation`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 12: Agent-Reviewed Rule Guardrails** was opened by F035
(`FS.GG.Governance.AgentReviewKey`), which landed the phase's **first** line — *"Cache agent-reviewed verdicts by
model id, model version, reviewer prompt hash, model configuration, check hash, artifact hashes, and question
text"* — as a pure core: the typed `AgentReviewInputs`, the byte-stable injective `CacheKey`, and the
`compute` / `matches` / `diff` functions that key a verdict, compare two keys, and name which of the seven inputs
differ. The **next** unchecked Phase-12 line is *"Invalidate cached verdicts when judge identity or prompt
identity changes."* This row is the direct analogue of the Phase-11 cache pair's second half: F029
(`FreshnessKey`) defined the deterministic-evidence key and F030 (`EvidenceReuse`) added the **store + reuse
decision** that consumed it; here F035 defined the agent-review verdict key and **this row** adds the **verdict
store + lookup / invalidation decision** that consumes F035's `matches`/`diff` verbatim. Continuing this repo's
maintainer-confirmed **pure-core-first** rhythm (F015–F035 each landed a pure, total, deterministic core before
any host edge consumed it), this row is sliced to that single decision: the typed **cached-verdict store**
vocabulary and the total, deterministic functions that decide — given a new review request's inputs and a store
of previously cached verdicts — *whether* a cached verdict is still valid for reuse, and when it is **not**,
*why* it was invalidated (which judge / prompt / check / artifact identity changed). It performs **no
persistence** (no filesystem/database read or write), no eviction/expiry, computes **no key bytes** itself
(F035 owns the key), invokes **no model / agent**, reads **no clock / filesystem / git / network**, carries **no
verdict content**, runs **no actual review**, makes **no advisory-vs-blocking promotion decision**, and adds
**no CLI**.

## Overview

Governance's deterministic checks produce reproducible proof, so their evidence can be cached and reused whenever
the world fingerprints the same (F029/F030). **Agent-reviewed** checks cannot make that promise: the *same
question over the same artifacts can return a different verdict* when the judge (model id, model version, model
configuration) or the prompt (reviewer prompt hash, question text) changes. F035 captured *exactly what a verdict
depends on* in a byte-stable key over seven inputs. This feature answers the operational question that sits
directly on top of that key — the design's *"Judge identity drift"* guarantee: *"A judge or prompt change
invalidates prior cached verdicts for that rule."*

That decision is the second guardrail of Phase 12. It must be **deterministic, total, and auditable**: the same
new request against the same cached verdicts always yields the same valid-or-invalidated answer, every cached
entry is considered, and an *invalidated* answer is never an opaque "no" — it names which of the seven identity
inputs changed (or "no cached verdict for this work"), and in particular surfaces when the change is a **judge
identity** change or a **prompt identity** change, so an auditor can see why a prior verdict was not reused.

This row delivers that as a pure core that reuses F035 verbatim:

- **Model a cached verdict and a verdict store as pure values** — a *cached-verdict entry* pairs an F035
  `AgentReviewInputs` value with an **opaque reference to the cached verdict** (an already-recorded handle — the
  core treats it as an opaque token, never inspecting, producing, or dereferencing the verdict itself, and never
  reading the verdict's advisory/blocking content). A *verdict store* is the closed collection of such entries,
  modeled as an immutable value (no I/O, no live cache).
- **Decide validity with a single total function** — given a new request's `AgentReviewInputs` and a verdict
  store, return a **lookup decision**: *Valid* (carrying the matching entry's verdict reference) **iff** some
  cached entry's inputs match the request on **every** one of the seven inputs (F035 `matches`), else
  *Invalidated* carrying the **no-hide explanation** of which identity inputs changed — attributable to judge
  identity, prompt identity, or check / artifact identity — or "no cached verdict for this work."
- **Record verdicts purely** — a total function that returns a **new** verdict store with the request's inputs
  and verdict reference cached, so a subsequent identical request reuses it. Re-recording under inputs that
  match an existing entry **refreshes** that entry deterministically rather than accumulating duplicates.

The core is **pure over supplied data**, exactly like F029/F030/F035: the verdict store is a value handed in, not
a live cache; the verdict references are opaque tokens minted at the edge; the seven identity inputs were already
formed at the edge. The **actual review** (sending the prompt to a model, receiving a verdict), the
**persistence** of the store (reading/writing it to disk or a database), **eviction / size limits / expiry**, the
**separation of governed artifact content from reviewer instructions** (Phase 12's third row), the **recording of
review requests and response digests** (fourth row), the **advisory-vs-blocking promotion** (fifth row), and the
**judge-vs-human calibration** (sixth row) are out of scope. This core makes no verdict and no blocking decision;
it only decides reuse-or-invalidate over cached verdicts honestly, so agent-reviewed findings stay **advisory**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reuse a cached agent-reviewed verdict only when the full judge / prompt / check / artifact identity is unchanged (Priority: P1)

A Governance component has a new agent-review request for a judgement-heavy check and a store of verdicts cached
from earlier requests. Before invoking a model, it must decide — deterministically, without re-running the review
— whether a cached verdict still applies. It asks the lookup decision for the new request's `AgentReviewInputs`
against the cached verdicts.

**Why this priority**: This is the whole point of the feature and the operational core of Phase 12's caching.
Reuse that fires when any identity input differs is unsafe (a stale verdict from a different judge or prompt);
reuse that fails to fire when all inputs match is worthless (never reuses). The "reuse iff all seven inputs
match" rule — the dual of "invalidate when judge or prompt identity changes" — is the single guarantee
everything else depends on. It is independently demonstrable and delivers the core value alone.

**Independent Test**: Build a verdict store holding one cached entry. Ask the lookup for a request whose inputs
are identical in all seven and assert *Valid* carrying that entry's verdict reference. Then, for each of the
seven inputs in turn, ask the lookup for a request that differs in **only that one input** and assert
*Invalidated*. No host, no I/O, no model invoked.

**Acceptance Scenarios**:

1. **Given** a verdict store holding one cached entry, **When** the lookup is asked for a request whose seven
   inputs equal that entry's, **Then** the result is *Valid* carrying exactly that entry's cached-verdict
   reference.
2. **Given** a verdict store holding one cached entry, **When** the lookup is asked for a request differing only
   in the model id (and likewise, tested separately, the model version, reviewer prompt hash, model
   configuration, check hash, the reviewed-artifact set, or the question text), **Then** the result is
   *Invalidated* (no reuse).
3. **Given** a verdict store holding several entries of which exactly one matches the request on every input,
   **When** the lookup is asked, **Then** the result is *Valid* carrying that one matching entry's verdict
   reference, regardless of the other entries.
4. **Given** an empty verdict store, **When** the lookup is asked for any request, **Then** the result is
   *Invalidated* (there is nothing to reuse).

---

### User Story 2 - A judge or prompt change visibly invalidates the prior verdict, and the cause is always explained (Priority: P1)

When a cached verdict is not reused, an auditor (or a maintainer debugging a surprise re-review) must see **why** —
was there no cached verdict for this work at all, or did the judge identity (model id, model version, model
configuration), the prompt identity (reviewer prompt hash, question text), or the check / artifact identity (check
hash, reviewed-artifact hashes) change? The decision must carry that explanation so *"why did this request not
reuse the cached verdict?"* is answerable without re-deriving anything outside the core. A judge or prompt change
must visibly invalidate the prior verdict.

**Why this priority**: This is the observable face of the design's *"A judge or prompt change invalidates prior
cached verdicts for that rule"* — the honesty guarantee that the cache never silently reuses a verdict produced
under a different judge or prompt. Auditability of the *negative* answer is as load-bearing as the reuse itself,
so it is co-P1 with Story 1.

**Independent Test**: Against a store holding a prior entry for the same check that differs only in the model
version, ask the lookup for the request and assert the *Invalidated* result names the model version (using F035's
`diff` vocabulary) as the changed input and attributes it to **judge identity**. Repeat with a changed reviewer
prompt hash and assert it attributes the change to **prompt identity**. Against an empty store (or a store with no
entry for this work), assert the *Invalidated* result reports "no cached verdict" rather than a spurious input
difference.

**Acceptance Scenarios**:

1. **Given** a store with a cached entry for the request that differs only in one or more of the seven inputs,
   **When** the lookup is *Invalidated*, **Then** it identifies exactly the differing input(s) (the no-hide
   explanation, expressed in F035's `diff` inputs) — no differing input hidden, no equal input reported.
2. **Given** a cached entry that differs only in a **judge identity** input (model id, model version, or model
   configuration) — or, tested separately, only in a **prompt identity** input (reviewer prompt hash or question
   text) — **When** the lookup is *Invalidated*, **Then** the cause is attributable to that identity group, so a
   judge change and a prompt change are each visible as such.
3. **Given** a store containing no cached entry for the request's work at all, **When** the lookup is
   *Invalidated*, **Then** it reports the absence of a cached verdict (a distinct, locatable cause — not a
   spurious input difference and not an empty/ambiguous "they differ").
4. **Given** any *Invalidated* decision, **When** it is inspected, **Then** the cause is always present and
   non-ambiguous (every invalidation has a stated reason).

---

### User Story 3 - Recording a cached verdict is pure, deterministic, and de-duplicating (Priority: P2)

After obtaining an agent-reviewed verdict, Governance caches it so the next identical request can reuse it.
Recording must be a pure transform of the verdict-store value (returning a new store), must make a just-recorded
verdict immediately reusable by a matching request, and must not let repeated recording under matching inputs
accumulate duplicate entries.

**Why this priority**: Recording is what makes reuse possible on the *next* request, and it must compose cleanly
with the lookup (Stories 1–2). It is essential but builds on the decision contract, so it is P2.

**Independent Test**: Record an entry into an empty store; assert a request with matching inputs now decides
*Valid* with that verdict reference. Record again under inputs that match an existing entry but carry a new
verdict reference; assert the store still resolves a matching request to *Valid* (with the refreshed reference)
and has not grown an additional colliding entry. Record under inputs that match nothing; assert both entries
remain independently reusable.

**Acceptance Scenarios**:

1. **Given** an empty verdict store, **When** an entry is recorded for some `AgentReviewInputs` and verdict
   reference, **Then** a later lookup for a request whose inputs match returns *Valid* carrying that verdict
   reference.
2. **Given** a verdict store already holding an entry, **When** a verdict is recorded again under inputs that
   match that entry but with a different verdict reference, **Then** a matching request decides *Valid* with the
   most recently recorded reference and the store holds no duplicate entry for those inputs.
3. **Given** a verdict store, **When** a verdict is recorded under inputs that match no existing entry, **Then**
   the new entry and every prior entry remain independently reusable by their respective matching requests.
4. **Given** the same starting store and the same sequence of recordings, **When** recording is replayed,
   **Then** the resulting store yields identical lookup decisions for every request (recording is deterministic).

---

### Edge Cases

- **Empty verdict store.** Looking up against a store with no entries is valid and always yields *Invalidated*
  with the "no cached verdict" cause (never an error, never a spurious input diff).
- **Multiple entries match the request.** Because matching entries agree with the request on all seven inputs,
  they agree with each other; the decision is *Valid* and resolves to a single, deterministically chosen verdict
  reference (re-recording's refresh rule keeps at most one entry per matching-input class, so this is the
  degenerate boundary, handled deterministically).
- **No entry shares the request's check / question.** The decision is *Invalidated* with "no cached verdict for
  this work," distinct from "a cached verdict existed but the judge / prompt / artifact identity changed."
- **Re-recording under matching inputs.** Refreshes the existing entry's verdict reference rather than adding a
  second entry; the store does not grow unboundedly under repeated identical-input recordings.
- **Order / duplication of reviewed-artifact hashes in the request or cached inputs.** Inherited from F035:
  reordered or duplicated reviewed-artifact hashes never change whether two inputs match, so they never change the
  lookup decision; the empty artifact set is an ordinary value.
- **Opaque verdict reference is never interpreted.** The core neither parses, validates, produces, nor
  dereferences the verdict reference, and never reads whether the verdict is advisory or blocking; it only carries
  the matching entry's reference back on *Valid*. An empty or unusual reference string is a literal value, not an
  error.
- **A judge or prompt change with everything else equal.** When only a judge identity input (model id / version /
  configuration) or a prompt identity input (reviewer prompt hash / question text) differs, the cached verdict is
  *Invalidated* and the cause attributes the change to judge or prompt identity respectively — the design's
  invalidation guarantee.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **cached-verdict entry** pairing an F035 `AgentReviewInputs` value
  with an **opaque verdict reference** (a handle to an already-cached agent-reviewed verdict). The reference MUST
  be treated as an opaque, comparable token: the core MUST NOT parse, validate, dereference, or produce the
  underlying verdict, and MUST NOT read its advisory/blocking content.
- **FR-002**: The system MUST define a **verdict store** as an immutable value holding a collection of
  cached-verdict entries. It MUST NOT model or perform any live cache, persistence, connection, or I/O.
- **FR-003**: The system MUST provide a single, pure, total **lookup** function that, given a request's
  `AgentReviewInputs` and a verdict store, returns either *Valid* (carrying a verdict reference) or *Invalidated*
  (carrying an explanation). It MUST be defined for every well-typed input (no value causes failure or
  exception).
- **FR-004**: The lookup MUST be *Valid* **iff** at least one cached entry's inputs match the request on **every**
  one of the seven inputs — reusing F035's `matches` rule verbatim. It MUST be *Invalidated* whenever no cached
  entry matches; in particular, a change in any **judge identity** input (model id, model version, model
  configuration) or any **prompt identity** input (reviewer prompt hash, question text) MUST invalidate an
  otherwise-matching prior verdict (the design's *"a judge or prompt change invalidates prior cached verdicts"*).
- **FR-005**: On *Valid*, the decision MUST carry the matching entry's verdict reference. When more than one entry
  matches, the carried reference MUST be chosen **deterministically** (same store + same request ⇒ same
  reference).
- **FR-006**: On *Invalidated*, the decision MUST carry a **non-hidden, locatable cause**: either "no cached
  verdict for the request's work" or, where a cached verdict for the request's work exists, the specific differing
  input(s) — expressed using F035's `diff` input vocabulary and attributable to **judge identity**, **prompt
  identity**, or **check / artifact identity**. An *Invalidated* decision MUST never be an opaque, reasonless
  negative.
- **FR-007**: The system MUST provide a pure, total **record** function that, given `AgentReviewInputs`, a verdict
  reference, and a verdict store, returns a **new** verdict store in which a subsequent lookup for a request
  matching those inputs returns *Valid* with that reference. Recording MUST NOT mutate the input store value.
- **FR-008**: Recording under inputs that **match an existing entry** MUST **refresh** that entry (replace its
  verdict reference, most-recent-wins) rather than add a duplicate; the store MUST hold at most one entry per
  matching-input class. Recording under inputs that match no existing entry MUST add a new entry while leaving
  existing entries reusable.
- **FR-009**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no
  filesystem, no git, no environment, and no network; it MUST invoke no model / agent, compute no hash or key
  bytes from raw bytes, measure no elapsed time, spawn no process, and capture no bytes. Identical request +
  identical store always yields the identical decision; identical starting store + identical recording sequence
  always yields an equivalent store (same lookup decisions for all requests).
- **FR-010**: The core MUST **consume F035 verbatim** — `AgentReviewInputs`, the `matches` rule, and the `diff`
  explanation (and the `CacheKey`/`compute` it owns, if used to index the store) — without modifying F035 or any
  other merged core. It MUST NOT redefine the agent-review input vocabulary. This feature is additive.
- **FR-011**: The core MUST perform **no persistence, no eviction/expiry, no size limit, and no reuse side effect
  on any external store**; it MUST invoke no model, compute no key bytes itself, carry no verdict content, run no
  actual review, perform no output/response-digest verification, persist no artifact, and add no CLI surface. It
  MUST **not** separate governed artifact content from reviewer instructions (Phase 12's third row), **not**
  record review requests / response digests (fourth row), **not** promote any finding from advisory to blocking
  (fifth row), and **not** define any judge-vs-human calibration (sixth row). Its sole outputs are the lookup
  decision value and the new verdict-store value.
- **FR-012**: The core MUST handle the degenerate cases as ordinary, total outcomes (not errors): an empty verdict
  store, no entry sharing the request's check / question, multiple matching entries, re-recording under matching
  inputs, and an empty/unusual verdict-reference string (each as described in Edge Cases).
- **FR-013**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1** change
  (see Assumptions). [The concrete module home and name are a planning decision deferred to `/speckit-plan`.]
- **FR-014**: The core MUST NOT add a new third-party package dependency; the decision MUST use only facilities
  already available to the merged cores (the shared framework / BCL) plus F035.

### Key Entities *(include if feature involves data)*

- **Cached-verdict entry**: A pairing of an F035 `AgentReviewInputs` value (the judge / prompt / check / artifact
  identity the verdict was produced under) with an **opaque verdict reference** (a handle to the cached verdict).
  The reference carries no semantics the core interprets, and the core never reads whether the verdict is advisory
  or blocking.
- **Verdict store**: An immutable collection of cached-verdict entries — the supplied, in-value "what has been
  cached so far." Not a live cache, connection, or file.
- **Lookup decision**: The total result of asking "is a cached verdict still valid for this request?" against a
  store — either *Valid* (with the matching entry's verdict reference) or *Invalidated* (with a non-hidden cause).
- **Invalidation cause** (the no-hide explanation): Why no cached verdict served — either "no cached verdict for
  this work" or the specific differing F035 inputs, attributable to judge identity, prompt identity, or check /
  artifact identity.
- **Opaque verdict reference** (new opaque value): A token standing for an already-cached agent-reviewed verdict;
  carried on *Valid*, never produced, parsed, or dereferenced by this core.
- **Judge identity / prompt identity / check-artifact identity** (groupings, from F035): Judge identity = model
  id, model version, model configuration; prompt identity = reviewer prompt hash, question text; check / artifact
  identity = check hash, reviewed-artifact hashes. A change in judge or prompt identity invalidates prior cached
  verdicts; the lookup attributes invalidation to the changed group.
- **Agent-review inputs / match rule / diff** (reused from F035): The seven-input value, the all-inputs match
  rule, and the differing-input explainer — consumed verbatim, not redefined.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a request whose inputs match a cached entry on every input, the lookup is *Valid* carrying that
  entry's verdict reference in 100% of cases; for a request differing in **exactly one** input, the lookup is
  *Invalidated* — verified for **every** F035 input (model id, model version, reviewer prompt hash, model
  configuration, check hash, reviewed-artifact set, question text), i.e. 100% single-field-change coverage.
- **SC-002**: A change in any judge identity input (model id, model version, model configuration) or any prompt
  identity input (reviewer prompt hash, question text), with everything else equal, invalidates an
  otherwise-matching cached verdict in 100% of cases, and the cause attributes the change to the judge or prompt
  identity group respectively.
- **SC-003**: Every *Invalidated* decision carries a locatable cause in 100% of cases — either "no cached verdict"
  or at least one specific differing input — and never an empty/ambiguous negative; every differing input is named
  and no equal input is reported.
- **SC-004**: For any request and any verdict store, asking the lookup twice yields identical results in 100% of
  cases (determinism); reordering or duplicating the reviewed-artifact hashes in the request or in a cached entry
  never changes the decision.
- **SC-005**: After recording an entry, a request whose inputs match it decides *Valid* with the recorded
  reference in 100% of cases; re-recording under matching inputs leaves the store with no duplicate entry for
  those inputs and resolves a matching request to the most recently recorded reference.
- **SC-006**: The core reads no clock, filesystem, git, environment, or network and invokes no model —
  demonstrable by lookups and recordings being identical when performed in different working directories, at
  different times, and with unrelated repository / filesystem state changed between operations.
- **SC-007**: The merged cores (including F035) and their `surface/*.surface.txt` baselines, and `dotnet build` /
  `dotnet test` over the existing projects, are **unchanged** by this feature except for the additive new surface
  — no existing baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the verdict store + lookup / invalidation decision + record, over a pure store value.** The actual
  review (invoking a model), the persistence of the store (reading/writing it to disk or a database), eviction /
  expiry / size limits, the separation of governed artifact content from reviewer instructions (Phase 12's third
  row), the recording of review requests and response digests (fourth row), advisory-vs-blocking promotion (fifth
  row), and judge-vs-human calibration (sixth row) are out of scope here. This row produces only the deterministic
  lookup decision and the pure record transform.
- **Verdict references are opaque, supplied tokens.** Consistent with F035's "hashes/tokens are supplied, not
  computed" framing and F030's opaque evidence reference, the verdict reference is minted at the edge (the host
  that actually obtains the verdict) and passed in. This core never invokes a model, opens a file, dereferences a
  reference, reads a verdict's content, or reads a clock. Whether the opaque reference is a new newtype or an
  already-available value is a home decision deferred to `/speckit-plan`; the reasonable default is a thin new
  opaque-string newtype (mirroring F030's `EvidenceRef`).
- **The validity rule is exactly F035 `matches`.** "Invalidate cached verdicts when judge identity or prompt
  identity changes" is implemented as "*Valid* iff some cached entry `matches` the request, else *Invalidated*,"
  reusing F035 verbatim — no new notion of partial or fuzzy match. Because `matches` is true iff **all seven**
  inputs are equal, any judge or prompt (or check / artifact) change necessarily invalidates, satisfying the
  design's named guarantee.
- **The invalidation explanation reuses F035 `diff`.** The no-hide cause is expressed in F035's `diff` input
  vocabulary; for the "cached verdict existed but changed" case the differing inputs come from `diff` against the
  relevant prior entry, grouped into judge / prompt / check-artifact identity. The precise selection of *which*
  prior entry's diff to surface when several near-misses exist is a planning detail deferred to `/speckit-plan`;
  the contract here is only that the cause is always present, locatable, non-ambiguous, and attributable to an
  identity group.
- **The verdict store representation is a planning decision.** Whether the store is modeled as a list, a set, or
  a key-indexed map (keyed by F035's `CacheKey`) is deferred to `/speckit-plan`; the spec constrains only its
  observable behavior (the match rule, the refresh/de-dup rule, determinism), not its representation. Indexing by
  F035's `CacheKey` is the natural default, mirroring how F030 could key by F029's `Key`.
- **This core makes no verdict and no blocking decision; agent-reviewed findings stay advisory.** Deciding reuse
  or invalidation over cached verdicts neither produces a verdict nor promotes any finding to blocking. The
  advisory-by-default posture the design requires (*"protected-branch blocking should come from deterministic
  checks … until calibration exists"*) is unchanged by this row.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. It consumes F035 (and transitively F029's `RuleHash`/`ArtifactHash` and the F014 typed facts)
  verbatim and modifies none of them. Whether it lands as a new pure-core module or extends F035 is the only home
  decision left to `/speckit-plan`; the established rhythm suggests a new minimal core depending on F035.
- **Determinism is the contract, not performance.** The store holds a modest number of cached verdicts per check;
  there is no latency or throughput target. Byte-stability of decisions, totality of `lookup` / `record`, and the
  no-hide explanation are the guarantees.
- **This row is Phase 12's second line.** With it merged, the phase has both the cache-key foundation (F035) and
  the store + invalidation decision that consumes it; the remaining rows (prompt isolation, review recording,
  advisory promotion, and calibration) build toward the phase's exit criteria (agent-reviewed outputs auditable
  and prompt-isolated; missing or stale reviews visible; protected-branch blocking never depending on
  uncalibrated agent judgement).
