# Feature Specification: Agent-Review Verdict Cache-Key Core

**Feature Branch**: `035-agent-review-cache-key`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 11: Cost, Cache, and Provenance** is now complete — its sixth and
final row landed as F034 (`FS.GG.Governance.SensedMetadata`), following F029 (`FreshnessKey`), F030
(`EvidenceReuse`), F031 (`RouteExplain`), F032 (`CommandRecord`), and F033 (`Provenance`). The next
Governance-owned phase the maintainer chose to open is **Phase 12: Agent-Reviewed Rule Guardrails**, whose
purpose is *"allow judgement-heavy checks without treating uncalibrated agent output as deterministic proof."*
Its **first** unchecked line is *"Cache agent-reviewed verdicts by model id, model version, reviewer prompt hash,
model configuration, check hash, artifact hashes, and question text."* Continuing this repo's
maintainer-confirmed **pure-core-first** rhythm (F015–F034 each landed a pure, total, deterministic core before
any host edge consumed it), this row is sliced exactly as the F029→F030 pair was: the **cache-key** primitive
comes first — the typed **agent-review cache key** over the seven declared inputs and the total, deterministic
functions that compute it, compare two keys, and explain how two key inputs differ — before any verdict store,
lookup, or invalidation operation consumes it. It reads **no clock**, invokes **no model / agent / network**,
computes **no hash from raw bytes** (the hashes are supplied, already-computed tokens), reads **no filesystem /
git / environment**, persists **no artifact**, carries **no cached verdict** and runs **no cache store / lookup /
invalidation operation**, makes **no advisory-vs-blocking promotion decision**, and adds **no CLI**.

## Overview

Governance's deterministic checks produce reproducible proof: the same inputs always yield the same verdict, so
the verdict can be cached, diffed, and trusted. **Agent-reviewed** checks cannot make that promise. A
judgement-heavy check ("does this doc page actually explain the public API?") is answered by a language model,
and the *same question over the same artifacts can return a different verdict* when the model, its version, the
reviewer's prompt, the model's configuration, the check, or the question text changes. The design is explicit
that agent-reviewed rules *"are not deterministic proof"* and *"remain advisory by default until the review
system has operational guardrails and calibration evidence"* — and the **first** of those guardrails is a cache
key that captures **exactly what a verdict depends on**, so a cached verdict is reused only when *every* identity
input is unchanged, and a *judge or prompt change invalidates prior cached verdicts*.

The design names the seven inputs precisely (`initial-design.md`, *Optional agent-reviewed constraints* →
*Judge identity drift*): *"Cache keys include model id, model version, reviewer prompt hash, relevant model
configuration, check hash, artifact hashes, and question text. A judge or prompt change invalidates prior cached
verdicts for that rule."* This row delivers the typed key over those seven inputs and the byte-stable,
**injective** encoding that makes the key trustworthy — the same primitive role F029's `FreshnessKey` plays for
deterministic-evidence reuse, here specialised to agent-reviewed verdicts.

This is the direct analogue of the Phase-11 cache pair, and it is sliced the same way:

- **F029 `FreshnessKey`** defined the deterministic-evidence key over its closed input set, with `compute`,
  `matches`, and `diff`; **F030 `EvidenceReuse`** then added the store + reuse decision that consumes it.
- **This row (F035)** defines the **agent-review verdict key** over the seven judge / prompt / check / artifact
  inputs, with the same `compute` / `matches` / `diff` shape; a later row (Phase 12's second line, *"Invalidate
  cached verdicts when judge identity or prompt identity changes"*) adds the verdict store + invalidation
  decision that consumes it.

What **this** row delivers, ahead of any verdict store or review-recording edge, is the **pure value and
vocabulary** that keys an agent-reviewed verdict:

- **Model the cache key over the seven declared inputs as a typed value** — model id, model version, reviewer
  prompt hash, model configuration, check hash, the set of reviewed-artifact hashes, and the question text. Each
  is an already-formed opaque token supplied by the edge (the F029/F032 opaque-token discipline): this core
  neither invokes a model nor computes any hash; it *keys* over what it is given.
- **Compute a byte-stable, injective cache key** — a total function turns the seven inputs into one canonical,
  byte-stable key in the established F029/F032/F033 tagged, length-prefixed, injective discipline. Two input
  sets that differ in **any** of the seven inputs produce **different** keys; identical input sets produce
  **byte-identical** keys, so a cached verdict is reusable exactly when its key matches.
- **Explain a cache miss** — when two keys differ, a total `diff` names exactly which of the seven inputs
  changed (the no-hide explainer F029 established), so an auditor can see *why* a prior verdict was not reused
  ("the model version changed", "the reviewer prompt hash changed", "a reviewed artifact changed") — the
  observable face of *"a judge or prompt change invalidates prior cached verdicts."*

The core is **pure over supplied data**, exactly like F029/F030/F032/F033: the model identity, prompt hash,
configuration, check hash, artifact hashes, and question text are handed in as already-formed tokens; nothing is
clocked, no model is invoked, no bytes are hashed, nothing is persisted. The **actual review** (sending the
prompt to a model, receiving a verdict), the **verdict store / lookup / invalidation operation** (Phase 12's
second row), the **separation of governed artifact content from reviewer instructions** (third row), the
**recording of review requests and response digests** (fourth row), the **advisory-vs-blocking promotion**
(fifth row), the **judge-vs-human calibration** (sixth row), **persistence**, and any **CLI** remain out of
scope. This core changes nothing about agent-reviewed findings staying **advisory** — it makes no verdict and no
blocking decision; it only keys verdicts so a later store can reuse them honestly.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Key an agent-reviewed verdict by its full judge / prompt / check / artifact identity (Priority: P1)

A Governance component has just obtained (or is about to look up) an agent-reviewed verdict for a judgement-heavy
check, and needs a single stable key that captures everything the verdict depends on — which model answered (id
and version), under which reviewer prompt and model configuration, for which check and question, over which
reviewed artifacts. It needs to turn those seven already-formed inputs into one byte-stable cache key, so the
verdict can be cached under it and found again only when *all* of that identity is unchanged.

**Why this priority**: This is the core of the design's *"Cache agent-reviewed verdicts by [the seven inputs]"*
row and the load-bearing first guardrail of Phase 12 — without a key that captures the full judge/prompt/check
identity, a cache cannot tell a still-valid verdict from a stale one. It is independently demonstrable from
supplied tokens alone.

**Independent Test**: Take the seven inputs (model id, model version, reviewer prompt hash, model configuration,
check hash, a set of artifact hashes, question text) as supplied tokens, compute the key, and assert the same
inputs always produce the byte-identical key while any single differing input produces a different key. No model
invoked, no hash computed, no I/O.

**Acceptance Scenarios**:

1. **Given** the seven supplied inputs for one agent-reviewed verdict, **When** the cache key is computed,
   **Then** the result is a single byte-stable key that incorporates all seven inputs.
2. **Given** two sets of the seven inputs that are equal in every input, **When** their keys are computed,
   **Then** the two keys are byte-for-byte identical (so a cached verdict is reusable).
3. **Given** two sets of inputs that differ in exactly one input (e.g. a newer model version, a changed reviewer
   prompt hash, a different question, or one changed reviewed-artifact hash), **When** their keys are computed,
   **Then** the two keys are different (so a stale verdict is not reused).

---

### User Story 2 - Detect and explain judge / prompt drift so stale verdicts are not reused (Priority: P1)

A cache holds a verdict computed earlier, and a new review request arrives. The component must decide whether the
new request keys to the same verdict — and when it does **not**, it must be able to say *why*, so an auditor (or a
maintainer debugging a surprise re-review) can see that the model version, the reviewer prompt, the configuration,
the check, the question, or a reviewed artifact changed. A judge or prompt change must visibly invalidate the
prior key.

**Why this priority**: This is the observable face of *"A judge or prompt change invalidates prior cached
verdicts for that rule"* — the honesty guarantee that the cache never silently reuses a verdict produced under a
different judge or prompt. It is co-P1 with Story 1: a key that cannot explain a miss leaves drift invisible.

**Independent Test**: Compute keys for two input sets, compare them with `matches`, and assert it is true exactly
when all seven inputs are equal. For two differing sets, call `diff` and assert it names exactly the inputs that
differ (and nothing else) — including the case where only the artifact set differs, and the case where several
inputs differ at once.

**Acceptance Scenarios**:

1. **Given** two sets of inputs, **When** their keys are compared, **Then** they match if and only if all seven
   inputs are equal.
2. **Given** two sets of inputs that differ, **When** the difference is explained, **Then** the explanation names
   exactly which of the seven inputs differ — no differing input is hidden and no equal input is reported.
3. **Given** a prior key and a new request whose model id, model version, reviewer prompt hash, or model
   configuration changed, **When** the keys are compared, **Then** they do not match (judge / prompt drift
   invalidates the prior key), and the difference identifies the changed identity input.

---

### User Story 3 - The keying is deterministic, pure, and order-insensitive over the reviewed-artifact set (Priority: P2)

The keying functions are consumed by a verdict cache and read by auditors, so they must be pure, deterministic
functions of the supplied tokens. The reviewed artifacts are a **set** — the same review over the same artifacts
must key identically regardless of the order or duplication in which the artifact hashes are supplied — while the
other six inputs are distinct scalar identities.

**Why this priority**: Determinism, purity, and set-correct artifact handling are what make the key trustworthy
as a cache key (the same guarantees F029 holds). It is essential but builds on the compute / matches / diff
contracts of Stories 1–2, so it is P2.

**Independent Test**: Compute the key for the same inputs twice and assert byte-equality. Reorder and duplicate
the artifact-hash set and assert the key is unchanged. Confirm the key reads no clock, filesystem, git,
environment, or network, invokes no model, and computes no hash — demonstrable by identical keys produced in
different working directories, at different times, with unrelated repository/filesystem state changed between
calls.

**Acceptance Scenarios**:

1. **Given** the same seven inputs, **When** the key is computed twice, **Then** the two keys are byte-for-byte
   identical (determinism).
2. **Given** the same set of reviewed-artifact hashes supplied in a different order, or with duplicate hashes,
   **When** the key is computed, **Then** the key is unchanged (artifact hashes are a set — order- and
   duplicate-insensitive).
3. **Given** the key is computed in different working directories, at different times, and with unrelated
   repository / filesystem / environment state changed between calls, **Then** the results are identical (purity —
   no clock, filesystem, git, environment, or network read; no model invoked; no bytes hashed).

---

### Edge Cases

- **A review over no artifacts (empty artifact-hash set).** An empty set is an ordinary value: it keys
  deterministically to a distinct, unambiguous form, never treated as "absent" and never colliding with a
  one-artifact set.
- **Duplicate artifact hashes in the supplied set.** Deduplicated by set semantics — a set containing a hash
  twice keys identically to the set containing it once.
- **Artifact hashes supplied in different orders.** Keyed identically — the artifact set is order-insensitive.
- **An empty / minimal scalar token** (e.g. empty question text, empty model configuration). An empty token is a
  literal value (the F029 opaque-token discipline — no validation, no parsing); it encodes to a distinct,
  unambiguous segment and never collides with a missing input or with another input's segment.
- **Two inputs that share the same text in different roles** (e.g. the model id and the question text happen to be
  the same string). Each is keyed in its own tagged, length-prefixed segment, so they never collide and the key
  is unchanged only when each input *in its own role* is equal.
- **A token whose text contains the encoding's separator or tag characters.** Every input is length-prefixed, so
  content containing a separator, tag, or marker character is read by length and cannot masquerade as another
  input or bleed across a field boundary (the established F029/F032 injective-encoding discipline) — the key is
  never spoofable by the data.
- **Only the artifact set differs** between two requests (all six scalar inputs equal). The keys differ, and the
  difference names the reviewed-artifact set as the changed input — a changed artifact invalidates the prior
  verdict.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **agent-review cache-key input** value that carries the seven inputs
  the design names: **model id**, **model version**, **reviewer prompt hash**, **model configuration**, **check
  hash**, the **set of reviewed-artifact hashes**, and the **question text**. Each input is an already-formed
  token supplied by the caller; this core neither invokes a model nor computes any of these hashes.
- **FR-002**: The system MUST provide a total **compute** function that turns the seven inputs into a single
  **byte-stable** cache key. Identical inputs MUST always yield a byte-identical key; inputs that differ in **any**
  of the seven MUST yield a different key (so a cached verdict is reused only when its full judge / prompt / check
  / artifact identity is unchanged).
- **FR-003**: The cache key's canonical encoding MUST be **injective and unspoofable by its data**: no supplied
  token — including one whose text contains a separator, tag, or marker character, or one that equals another
  input's text — may masquerade as a different input, as a field boundary, or as the absence of a value (the
  established F029/F032 tagged, length-prefixed encoding discipline). Distinct input sets MUST NOT collide on the
  same key.
- **FR-004**: The system MUST provide a total **matches** comparison over two keys (or two input sets) that is
  true **if and only if** all seven inputs are equal — the predicate a verdict cache uses to decide a key hit.
- **FR-005**: The system MUST provide a total **diff** that, for two differing input sets, names **exactly** which
  of the seven inputs differ — no differing input hidden, no equal input reported (the F029 no-hide explainer).
  This is the observable face of *"a judge or prompt change invalidates prior cached verdicts."*
- **FR-006**: The **reviewed-artifact hashes MUST be treated as a set** — order-insensitive and
  duplicate-insensitive: the same artifacts supplied in any order or with duplicates MUST yield the same key. The
  other six inputs are distinct scalar identities, each keyed in its own role.
- **FR-007**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no
  filesystem, no git, no environment, and no network; it MUST invoke no model / agent, compute no hash from raw
  bytes, measure no elapsed time, spawn no process, and capture no bytes. Identical supplied inputs always yield
  an identical key, comparison, and difference.
- **FR-008**: The core MUST **reuse the existing typed facts verbatim** where one maps to an input — concretely
  the established **artifact-hash** vocabulary for the reviewed-artifact hashes (and the established **rule/check
  hash** vocabulary for the check hash, if it maps) — without modifying any merged core. It MUST introduce only
  the minimal new vocabulary the row needs (the model id, model version, reviewer prompt hash, model
  configuration, and question text tokens, for which no type exists yet, and the cache-key value itself). This
  feature is additive. *(Exactly which existing types map, and which are introduced new, is a planning decision
  deferred to `/speckit-plan`.)*
- **FR-009**: The core MUST carry and store **no cached verdict** and run **no cache store / lookup / invalidation
  operation** (the verdict store + invalidation is Phase 12's second row); it MUST **not** separate governed
  artifact content from reviewer instructions (third row), **not** record review requests / response digests
  (fourth row), **not** promote any finding from advisory to blocking (fifth row), and **not** define any
  judge-vs-human calibration (sixth row). It MUST invoke no model, compute no digest from raw bytes, perform no
  persistence, and add **no CLI** surface. Its sole outputs are the typed cache key, its canonical value, the
  `matches` comparison, and the `diff` explanation.
- **FR-010**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1** change
  (see Assumptions). [The concrete module home and name are a planning decision deferred to `/speckit-plan`.]
- **FR-011**: The core MUST NOT add a new third-party package dependency; the keying MUST use only facilities
  already available to the merged cores (the shared framework / BCL) plus any reused existing vocabulary.

### Key Entities *(include if feature involves data)*

- **Agent-review cache-key inputs**: The typed set of the seven inputs an agent-reviewed verdict depends on —
  model id, model version, reviewer prompt hash, model configuration, check hash, the set of reviewed-artifact
  hashes, and the question text. All supplied as already-formed tokens; the unit the key is computed from.
- **Agent-review cache key**: The deterministic, byte-stable, injective canonical key computed over the seven
  inputs — the identity under which an agent-reviewed verdict is cached and found again. Two verdicts are
  cache-interchangeable exactly when their keys are equal.
- **Judge identity**: The portion of the inputs that names *which judge answered* — model id, model version, and
  the relevant model configuration. A change here invalidates prior verdicts (read by Phase 12's invalidation
  row; here it is simply part of the key).
- **Prompt / question identity**: The portion that names *what was asked* — reviewer prompt hash and question
  text. A change here likewise invalidates prior verdicts.
- **Check / reviewed-artifact identity**: The check hash and the set of reviewed-artifact hashes — *what was
  reviewed*. The artifact hashes are a set (order- and duplicate-insensitive).
- **Input difference**: The no-hide explanation of which of the seven inputs differ between two input sets — the
  auditable reason a cached verdict was (or was not) a key hit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any seven supplied inputs, the computed cache key is byte-identical for identical inputs and
  different whenever any single input differs, in 100% of cases — so a cached verdict is reused only when its
  full judge / prompt / check / artifact identity is unchanged.
- **SC-002**: The reviewed-artifact hashes are keyed as a set — supplying them in any order, or with duplicates,
  never changes the key, in 100% of cases; the empty artifact set keys to a distinct, unambiguous value.
- **SC-003**: For any two differing input sets, the difference names exactly the inputs that differ and no others,
  in 100% of cases — every cache miss is explainable as a named judge / prompt / check / artifact change.
- **SC-004**: For the same supplied inputs, computing the key, comparison, and difference twice yields
  byte-for-byte identical results in 100% of cases (determinism), with no model invoked, no bytes hashed, and
  nothing persisted.
- **SC-005**: The canonical encoding is injective — no two distinct input sets collide on the same key, and no
  token (including separator-bearing text, empty tokens, or a token equal to another input's text) can spoof a
  field boundary or another input — in 100% of cases.
- **SC-006**: The core reads no clock, filesystem, git, environment, or network and invokes no model —
  demonstrable by keys, comparisons, and differences being identical when produced in different working
  directories, at different times, and with unrelated repository / filesystem state changed between calls.
- **SC-007**: The merged cores and their `surface/*.surface.txt` baselines, and `dotnet build` / `dotnet test`
  over the existing projects, are **unchanged** by this feature except for the additive new surface — no existing
  baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the pure agent-review cache-KEY primitive, over already-formed supplied tokens.** Computing the key,
  comparing two keys, and explaining their difference are the whole of this row — the F029 slice, specialised to
  agent-reviewed verdicts. The **verdict store / lookup / invalidation operation** is Phase 12's *second* row (the
  F030 analogue), and the **separation of artifact content from reviewer instructions** (third row), **recording
  of review requests and response digests** (fourth row), **advisory-vs-blocking promotion** (fifth row), and
  **judge-vs-human calibration** (sixth row) are later rows. Invoking a model, computing any hash, persistence,
  and any CLI are out of scope here.
- **The hashes are SUPPLIED tokens; this core neither hashes bytes nor invokes a model.** The reviewer prompt
  hash, model configuration, check hash, and artifact hashes are already-formed opaque tokens handed in by the
  edge (the F029/F032 opaque-token discipline — no validation, no parsing). The model id, model version, and
  question text are likewise supplied opaque tokens. The actual review and the production of these digests belong
  to a later host edge (Principle IV), exactly as F032 models its `OutputDigest`/`SensedDuration` as supplied.
- **Reuse existing typed facts verbatim; introduce only the minimal new vocabulary.** The established
  artifact-hash vocabulary (F029's `ArtifactHash`) is reused verbatim for the reviewed-artifact hashes, and the
  established rule/check-hash vocabulary (F029's `RuleHash`) may map to the check hash; the model id, model
  version, reviewer prompt hash, model configuration, and question text are minimal new opaque tokens because none
  exists yet. Whether this core references the owning core directly or carries a thin local alias, and exactly
  which existing type maps to the check hash, are planning decisions deferred to `/speckit-plan`; the established
  rhythm suggests a direct reference. This core redefines none of the merged vocabulary and modifies no merged
  core.
- **The seven inputs partition conceptually into judge identity (model id, model version, model configuration),
  prompt / question identity (reviewer prompt hash, question text), and check / artifact identity (check hash,
  artifact hashes).** Phase 12's invalidation row reads those groupings ("a judge or prompt change invalidates");
  this row keys over the flat seven inputs and the `diff` reports per-input differences, from which the groupings
  are derivable. Whether the key models the groupings explicitly or keeps the flat seven is a planning detail.
- **The cache key's exact format is a planning decision; only its observable contract is fixed here.** The spec
  fixes that the key is deterministic, byte-stable, injective, set-correct for the artifact hashes, comparable via
  `matches`, and explainable via `diff`. The concrete tag scheme, separators, and whether the key is a string or a
  richer value are deferred to `/speckit-plan` — consistent with the F029/F032/F033 tagged, length-prefixed,
  injective encoding this core should follow.
- **This core makes no verdict and no blocking decision; agent-reviewed findings stay advisory.** Keying a verdict
  for caching neither produces a verdict nor promotes any finding to blocking. The advisory-by-default posture the
  design requires (*"protected-branch blocking should come from deterministic checks … until calibration
  exists"*) is unchanged by this row.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. Whether it lands as a new pure-core module (the established rhythm) or extends an existing core is
  the only home decision left to `/speckit-plan`; the established rhythm suggests a new minimal core.
- **Determinism is the contract, not performance.** A review cache holds a modest number of keyed verdicts; there
  is no latency or throughput target. Byte-stability of the key, totality of `compute` / `matches` / `diff`, and
  injectivity of the encoding are the guarantees.
- **This row opens Phase 12.** It is the first line of *Phase 12: Agent-Reviewed Rule Guardrails*. With it merged,
  the phase has the cache-key foundation its remaining rows (verdict invalidation, prompt isolation, review
  recording, advisory promotion, and calibration) build on, toward the phase's exit criteria (agent-reviewed
  outputs auditable and prompt-isolated; missing or stale reviews visible; protected-branch blocking never
  depending on uncalibrated agent judgement).
