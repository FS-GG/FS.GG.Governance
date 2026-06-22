# Feature Specification: Agent-Review Record — Auditable Review-Record Core

**Feature Branch**: `038-agent-review-record`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 12: Agent-Reviewed Rule Guardrails** is open. Its **first three**
lines are complete: F035 (`FS.GG.Governance.AgentReviewKey` — the agent-review cache key), F036
(`FS.GG.Governance.VerdictReuse` — the verdict store + invalidation decision), and F037
(`FS.GG.Governance.PromptIsolation` — reviewer-prompt isolation). The next unchecked line is the phase's
**fourth**: *"Record review requests, response digests, model identity, prompt identity, artifact digests, and final
verdict."* This is the design's **auditability** guardrail (`docs/initial-design.md`, *Optional agent-reviewed
constraints*): *"Review requests, response digests, model identity, prompt identity, artifact digests, and final
recorded verdict are part of readiness provenance."* Continuing this repo's maintainer-confirmed **pure-core-first**
rhythm (F015–F037 each landed a pure, total, deterministic core before any host edge consumed it), this row
delivers the **review-record primitive**: a typed value that captures one completed agent review as an immutable,
auditable record — the rendered review request, the supplied response digest, the model identity, the prompt
identity, the reviewed-artifact digests, and the final recorded verdict — and derives a deterministic, byte-stable,
injective **record identity** over its reproducible facts. It invokes **no model / agent / network**, reads **no
clock / filesystem / git / environment**, computes **no hash from raw bytes** (every digest is a supplied token),
runs **no actual review**, performs **no cache lookup / verdict invalidation** (F035 / F036), makes **no
advisory-vs-blocking promotion** (the fifth row), defines **no calibration** (the sixth row), performs **no
persistence / JSON projection**, and adds **no CLI**.

## Overview

An agent-reviewed check sends a reviewer a question about a governed artifact and gets back a verdict. For that
verdict to be **auditable** — to belong in readiness provenance alongside the deterministic command records (F032)
and the provenance record (F033) — the system must keep an honest, reproducible record of *what was asked, of
which judge, about which artifacts, and what answer came back*. The design names the six facts that record must
carry, precisely: **review requests**, **response digests**, **model identity**, **prompt identity**, **artifact
digests**, and the **final recorded verdict**.

This row delivers that record as a **pure value with a deterministic identity**, ahead of any persistence, JSON
projection, or CLI. It is the **auditability** half of the phase's exit criterion (*"Agent-reviewed outputs are
auditable and prompt-isolated"*) — F037 delivered *prompt-isolated*; this row delivers *auditable*. Where F035
keyed *which* verdict a review request maps to, F036 decided *whether a cached verdict is still valid*, and F037
shaped the request so the artifact stays data, **this** core captures *what one completed review actually was*, so
it can be replayed, compared, and trusted later:

| Phase-12 row | Core | Question it answers |
|---|---|---|
| 1 — cache key (F035) | `AgentReviewKey` | *Under what identity is a verdict cached?* |
| 2 — invalidation (F036) | `VerdictReuse` | *Is a cached verdict still valid, and if not, why?* |
| 3 — prompt isolation (F037) | `PromptIsolation` | *How is the request shaped so the artifact is data, not an instruction?* |
| **4 — review record (this row)** | **(new pure core)** | ***What was this completed review — request, judge, artifacts, response, verdict — for the audit trail?*** |

This row is the **agent-review analogue of F032 (`CommandRecord`) / F033 (`Provenance`)**: a pure record-building
core that assembles a complete typed record from already-sensed facts and derives a byte-stable canonical identity
over its **reproducible** facts (the F032/F033 honesty boundary — sensed, non-deterministic metadata is held
structurally apart from identity). What **this** row delivers:

- **One immutable record of a completed review.** A review record pairs the **rendered review request** (the F037
  prompt-isolated request — instructions plus bounded-or-digested artifacts), the **response digest** (the supplied
  hash of the reviewer's response, carrying no response bytes), the **model identity** (the F035 model id +
  version), the **prompt identity** (the F035 reviewer-prompt hash), the **reviewed-artifact digests** (the F029
  artifact hashes), and the **final recorded verdict** — assembled by a total build over already-formed values.
- **A deterministic, injective record identity over reproducible facts.** A total render derives a byte-stable
  canonical identity from the record's reproducible facts (request identity, model identity, prompt identity,
  artifact digests, response digest, verdict) using the established F029 / F032 / F035 tagged, length-prefixed,
  injective encoding — so two records with the same reproducible facts share an identity and any differing fact
  yields a different identity. The record carries no clock value in its identity; any sensed wall-clock metadata is
  held apart (the F032 `SensedDuration` / F033 honesty boundary).
- **Verbatim reuse of the established vocabulary.** The model id / version and reviewer-prompt hash reuse F035, the
  artifact digests reuse F029's `ArtifactHash`, and the review request reuses F037's prompt-isolated request value;
  only the genuinely new vocabulary the row needs (the response digest, the final recorded verdict, and the review
  record itself) is introduced.

The core is **pure and total over supplied data**, exactly like F032 / F033 / F035 / F037: the review request, the
response digest, the model and prompt identities, the artifact digests, and the final verdict are handed in as
already-formed values; nothing is clocked, no model is invoked, no bytes are hashed, nothing is persisted, no
process is spawned. The **actual review** (sending a request to a model and receiving a response), **computing the
response digest from raw bytes**, the **cache lookup / verdict invalidation** (F035 / F036, consumed by neighbours,
not by this core), the **advisory-vs-blocking promotion** (the fifth row), the **judge-vs-human calibration** (the
sixth row), any **persistence / JSON projection** of the record (a later host edge / projection row), and any
**CLI** all remain out of scope. This core makes no verdict and no blocking decision; it only *records* a verdict
already produced, with an honest identity.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capture a completed agent review as one auditable record (Priority: P1)

A Governance component has just completed an agent review: it has the rendered review request it sent, the digest
of the response it received, the identity of the judge (model id + version), the identity of the reviewer prompt,
the digests of the artifacts that were reviewed, and the final verdict it recorded. It must assemble these into a
single, immutable review record so the review is auditable — replayable and comparable later as part of readiness
provenance.

**Why this priority**: This is the heart of the design's auditability constraint — *"Review requests, response
digests, model identity, prompt identity, artifact digests, and final recorded verdict are part of readiness
provenance."* Without a typed record carrying exactly these facts, an agent-reviewed verdict is unauditable: there
is no honest trail of what was asked, of which judge, about which artifacts, or what came back. It is independently
demonstrable from supplied values alone.

**Independent Test**: Supply a rendered review request, a response digest, a model identity, a prompt identity, a
set of artifact digests, and a final verdict as already-formed values; build the review record; and assert the
record carries each of the six facts exactly as supplied, with no fact dropped, altered, or invented. No model
invoked, no I/O.

**Acceptance Scenarios**:

1. **Given** a rendered review request, a response digest, a model identity, a prompt identity, the reviewed
   artifacts' digests, and a final verdict, **When** the review record is built, **Then** the record holds all six
   facts and each is exactly the supplied value.
2. **Given** the same six supplied facts, **When** the record is built twice, **Then** the two records are equal —
   the build is a total, deterministic function of its supplied inputs (no clock, no model, no I/O).
3. **Given** a review over zero artifacts (a request whose data channel is empty), **When** the record is built,
   **Then** it is a valid record with an empty artifact-digest collection and is never treated as malformed.

---

### User Story 2 - Derive a deterministic, injective identity over the record's reproducible facts (Priority: P1)

The recorded review needs a stable identity so two reviews that are reproducibly the same share an identity, and any
review differing in request, judge, prompt, artifacts, response, or verdict gets a different identity. The component
must derive that identity as a pure, deterministic, byte-stable, injective function of the record's **reproducible**
facts — excluding any sensed, non-deterministic metadata (the F032 / F033 honesty boundary).

**Why this priority**: An auditable record without a stable identity cannot be compared, deduplicated, or cited in
provenance. Identity is what makes the record usable downstream — exactly as F032 `canonicalId` and F033
`ProvenanceIdentity` make their records citable. It is co-P1 with Story 1: a record you cannot identify reproducibly
is not yet auditable.

**Independent Test**: Build two records from identical reproducible facts and assert their identities are
byte-equal; change exactly one reproducible fact (request, model id, model version, prompt hash, an artifact digest,
the response digest, or the verdict) and assert the identity changes; supply only sensed/non-deterministic metadata
differing between two otherwise-identical records and assert the identity is unchanged. Confirm the identity is a
single deterministic string over supplied values, with no clock, file, or hash-of-bytes read.

**Acceptance Scenarios**:

1. **Given** two review records with identical reproducible facts, **When** their identities are derived, **Then**
   the identities are byte-for-byte identical.
2. **Given** two review records differing in exactly one reproducible fact (the request, the model identity, the
   prompt identity, any one artifact digest, the response digest, or the final verdict), **When** their identities
   are derived, **Then** the identities differ — the identity is injective over its reproducible facts.
3. **Given** two records that are identical in every reproducible fact but differ only in sensed/non-deterministic
   metadata (if any is carried), **When** their identities are derived, **Then** the identities are identical — the
   identity excludes sensed metadata (the F032 / F033 honesty boundary).

---

### User Story 3 - Keep response and artifact content out of the record — digests only, no bytes (Priority: P2)

A response or an artifact can be large, binary, or hostile. The record must never carry raw response bytes or raw
artifact bytes: the response is carried only as its supplied digest, and the artifacts are carried only as their
supplied digests (or, within the embedded request, as F037 bounded excerpts). It must be impossible to attach raw,
unbounded response or artifact content to the record outside the F037-bounded request.

**Why this priority**: This is the bounding half of the auditability constraint — *"captured through bounded
excerpts or digests in the review record."* A record that embedded raw response or artifact bytes would defeat the
F037 prompt-isolation guarantee and bloat the audit trail. It builds on Stories 1–2 (the record and its identity),
so it is P2.

**Independent Test**: Supply a response digest and assert the record carries the digest and no response bytes;
supply artifact digests and assert the record carries the digests and no raw artifact bytes; confirm the only
content the record holds is the F037-bounded request (whose excerpts are already bounded by construction) and that
there is no form that carries raw, unbounded response or artifact content.

**Acceptance Scenarios**:

1. **Given** a reviewer response represented by its supplied digest, **When** the record is built, **Then** the
   record carries the response digest and **no** response bytes.
2. **Given** reviewed artifacts represented by their supplied digests, **When** the record is built, **Then** the
   record carries the artifact digests and **no** raw artifact bytes.
3. **Given** any completed review, **When** the record is built, **Then** the only artifact content it carries is
   inside the F037-bounded review request — there is no form that attaches raw, unbounded response or artifact
   content to the record.

---

### Edge Cases

- **A review over zero artifacts.** A valid record: the artifact-digest collection is empty and the embedded
  request's data channel is empty. It builds and identifies deterministically and is never malformed.
- **Duplicate or identical artifact digests.** Whether the reviewed-artifact digests are modelled as a set
  (order- and duplicate-insensitive, the F029 / F035 identity discipline) or an ordered sequence is a planning
  detail; the fixed contract is that the same supplied digests always yield the same record identity.
- **An empty or unusual response digest.** The response digest is a supplied opaque token (the F029 / F032 / F035
  discipline — no validation, no parsing); an empty or unusual digest is rendered in its own tagged, length-prefixed
  segment and never collides with another field.
- **Two records identical except for the final verdict.** Their identities differ — the verdict is a reproducible
  fact carried in identity (this row records the verdict; promoting or interpreting it is the fifth/sixth rows).
- **Two records whose embedded requests render identically but whose model/prompt identity differs.** Their
  identities differ — model identity and prompt identity are independent reproducible facts (the F035 judge/prompt
  drift discipline carried into the record).
- **Field content that contains the encoding's tag characters, separators, or fence markers.** Read as data by
  length; it cannot terminate a field, forge a boundary, or bleed across fields (the established F029 / F032 / F035
  length-prefixed, injective discipline).
- **Sensed wall-clock metadata, if carried.** Any sensed timestamp/duration the edge attaches is held structurally
  apart from the reproducible facts and excluded from identity (the F032 `SensedDuration` / F033 honesty boundary);
  whether this row carries any sensed metadata at all is a planning decision.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **review-record** value that holds the six audit facts the design
  names: the **review request** (the F037 prompt-isolated request or its rendering), the **response digest** (the
  supplied hash of the reviewer's response), the **model identity** (model id + version), the **prompt identity**
  (the reviewer-prompt hash), the **reviewed-artifact digests** (the artifact content hashes), and the **final
  recorded verdict**. No audit fact may be dropped, and the value MUST carry no raw response bytes and no raw,
  unbounded artifact bytes (artifact content appears only inside the F037-bounded request).
- **FR-002**: The system MUST provide a **total** build function that assembles a review record from those supplied
  facts, total over all supplied values (including a request over zero artifacts and empty / unusual digests).
- **FR-003**: The system MUST provide a **total, deterministic** derivation of a **record identity** — a byte-stable,
  injective canonical value over the record's **reproducible** facts (review request, model identity, prompt
  identity, artifact digests, response digest, final verdict). Records with identical reproducible facts MUST share
  an identity; any differing reproducible fact MUST yield a different identity; the encoding follows the established
  F029 / F032 / F035 tagged, length-prefixed, injective discipline.
- **FR-004**: The record identity MUST **exclude any sensed, non-deterministic metadata** (e.g. a wall-clock
  timestamp or measured duration), holding it structurally apart from the reproducible facts — the F032
  `SensedDuration` / F033 honesty boundary. Two records identical in every reproducible fact but differing only in
  sensed metadata MUST share an identity. *(Whether this row carries any sensed metadata at all is a planning
  decision; if it does, this boundary applies.)*
- **FR-005**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no filesystem,
  no git, no environment, and no network; it MUST invoke no model / agent, compute no hash from raw bytes (every
  digest is a supplied token), measure no elapsed time, spawn no process, and persist nothing. Identical supplied
  inputs MUST always yield an identical record and an identical identity.
- **FR-006**: The core MUST **reuse existing typed facts verbatim** where one maps to an audit fact — concretely the
  established **model-identity** vocabulary (model id + version) and **prompt-identity** vocabulary (reviewer-prompt
  hash) from F035, the established **artifact-hash** vocabulary from F029 for the artifact digests, and the
  established **prompt-isolated review-request** value from F037 — without modifying any merged core. It MUST
  introduce only the minimal new vocabulary the row needs (the response digest, the final recorded verdict, and the
  review-record value, for which no type exists yet). This feature is additive. *(Exactly which existing types map,
  whether the embedded request is the F037 `ReviewRequest` or its `RenderedPrompt`, and the response-digest /
  verdict shapes are planning decisions deferred to `/speckit-plan`.)*
- **FR-007**: The core MUST carry and store the verdict as an **opaque recorded fact** only: it MUST **not** promote
  any finding from advisory to blocking (the fifth row), MUST **not** interpret, compare, or threshold the verdict,
  and MUST **not** define any judge-vs-human calibration (the sixth row). It MUST run **no cache key / verdict store
  / lookup / invalidation operation** (F035 / F036 own those), invoke **no** model / agent, compute **no** digest
  from raw bytes, perform **no** persistence and **no** JSON projection, and add **no** CLI surface. Its sole outputs
  are the typed review-record value and its deterministic identity.
- **FR-008**: If this feature introduces a public F# module, its surface MUST be governed by the repo's `.fsi`-first
  and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1** change (see
  Assumptions). [The concrete module home and name are a planning decision deferred to `/speckit-plan`.]
- **FR-009**: The core MUST NOT add a new third-party package dependency; the build and identity derivation MUST use
  only facilities already available to the merged cores (the shared framework / BCL) plus the reused existing
  vocabulary (F029 / F035 / F037).

### Key Entities *(include if feature involves data)*

- **Review request**: The F037 prompt-isolated request (reviewer instructions plus bounded-or-digested artifact
  payloads) or its deterministic rendering — *what was asked*. Reused from F037, not re-modelled.
- **Response digest**: The supplied content hash standing in for the reviewer's response — *what came back*, carrying
  identity without response bytes. New minimal vocabulary (or a reused digest token; a planning decision).
- **Model identity**: The judge's identity — model id + version (F035 vocabulary) — *which judge answered*.
- **Prompt identity**: The reviewer-prompt hash (F035 vocabulary) — *under which prompt the judge answered*.
- **Artifact digests**: The reviewed artifacts' content hashes (F029 `ArtifactHash`) — *about which artifacts*.
- **Final recorded verdict**: The verdict produced by the review, carried as an opaque recorded fact — *what answer
  was recorded*. New minimal vocabulary; not interpreted, promoted, or thresholded here.
- **Review record**: The assembled value pairing all six audit facts — the immutable, auditable unit that belongs
  in readiness provenance.
- **Record identity**: The deterministic, injective, byte-stable canonical value over the record's reproducible
  facts — the citable, comparable face of the audit record, excluding sensed metadata.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every built review record carries all six audit facts — review request, response digest, model
  identity, prompt identity, artifact digests, and final verdict — each exactly as supplied, in 100% of cases, with
  no fact dropped, altered, or invented.
- **SC-002**: The record identity is injective over the reproducible facts — records with identical reproducible
  facts share a byte-identical identity, and any single differing reproducible fact (request, model id, model
  version, prompt hash, an artifact digest, the response digest, or the verdict) yields a different identity — in
  100% of cases.
- **SC-003**: The record identity excludes sensed/non-deterministic metadata — two records identical in every
  reproducible fact but differing only in any sensed metadata share an identity — in 100% of cases.
- **SC-004**: The record carries no raw response bytes and no raw, unbounded artifact bytes — the only artifact
  content present is inside the F037-bounded request — in 100% of cases.
- **SC-005**: For the same supplied facts, building the record and deriving its identity twice yields byte-for-byte
  identical results in 100% of cases (determinism), with no model invoked, no bytes hashed, and nothing persisted —
  demonstrable by identical records and identities produced in different working directories, at different times,
  and with unrelated repository / filesystem state changed between calls (purity).
- **SC-006**: The merged cores and their `surface/*.surface.txt` baselines, and `dotnet build` / `dotnet test` over
  the existing projects, are **unchanged** by this feature except for the additive new surface — no existing baseline
  is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the pure review-record primitive: capture and identity — over supplied values.** Assembling the six
  audit facts into one immutable record and deriving a deterministic, injective identity over its reproducible facts
  are the whole of this row (the design's *auditability* response). The **actual model review**, **computing the
  response digest from raw bytes**, the **advisory-vs-blocking promotion** (the fifth row), the **judge-vs-human
  calibration** (the sixth row), any **persistence / JSON projection** of the record, and any **CLI** are out of
  scope here. This is the agent-review analogue of F032 / F033's pure record cores.
- **Every fact is a SUPPLIED value; this core neither runs reviews, hashes bytes, nor reads a clock.** The review
  request (F037), the response digest, the model and prompt identities (F035), the artifact digests (F029), and the
  final verdict are already-formed values handed in by the edge (the F029 / F032 / F035 opaque-token discipline — no
  validation, no parsing). Running the review, computing the response digest, and sensing any timestamp belong to a
  later host edge (Principle IV), exactly as F032 / F033 model their digests and durations as supplied.
- **Reuse existing typed facts verbatim; introduce only the minimal new vocabulary.** The model id / version and
  reviewer-prompt hash reuse F035, the artifact digests reuse F029's `ArtifactHash`, and the review request reuses
  F037's prompt-isolated request; the response digest, the final recorded verdict, and the review-record value are
  minimal new types because none exists yet. Whether this core references the owning cores directly (the F033
  three-sibling precedent) or carries thin local aliases, whether the embedded request is F037's `ReviewRequest` or
  its `RenderedPrompt`, and the exact response-digest and verdict shapes are planning decisions deferred to
  `/speckit-plan`; the established rhythm suggests direct references. This core redefines none of the merged
  vocabulary and modifies no merged core.
- **Sensed metadata, if any, is held apart from identity.** If the edge attaches a sensed timestamp or duration to a
  review (e.g. when the review ran), it is held structurally apart from the reproducible facts and excluded from
  identity — the F032 `SensedDuration` / F033 honesty boundary, and exactly the marking F034 (`SensedMetadata`)
  provides. Whether this row carries any sensed metadata at all is a planning decision; the fixed contract is that
  identity is over reproducible facts only.
- **The record identity's exact format is a planning decision; only its observable contract is fixed here.** The
  spec fixes that the identity is deterministic, byte-stable, and injective over the reproducible facts, and that
  field content is length-delimited so it cannot escape. The concrete tag scheme, separators, and whether artifact
  digests are a set or ordered sequence are deferred to `/speckit-plan` — consistent with the F029 / F032 / F035
  tagged, length-prefixed, injective encoding this core should follow.
- **This core makes no verdict and no blocking decision; agent-reviewed findings stay advisory.** Recording a
  verdict that was already produced neither produces a new verdict nor promotes any finding to blocking. The
  advisory-by-default posture the design requires (*"protected-branch blocking should come from deterministic
  checks … until calibration exists"*) is unchanged by this row.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and carries
  the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party dependency**.
  Whether it lands as a new pure-core module (the established rhythm) or extends an existing core is the only home
  decision left to `/speckit-plan`; the established rhythm suggests a new minimal core.
- **Determinism is the contract, not performance.** A review record holds a modest number of supplied facts; there
  is no latency or throughput target. Faithful capture of the six audit facts, totality of build, injectivity of the
  identity over reproducible facts, and exclusion of sensed metadata are the guarantees.
- **This is Phase 12's fourth row.** With it merged, the phase has cache keying (F035), verdict invalidation (F036),
  prompt isolation (F037), and an auditable review record in place, leaving advisory promotion and calibration —
  toward the phase's exit criteria (agent-reviewed outputs **auditable** and prompt-isolated; missing or stale
  reviews visible; protected-branch blocking never depending on uncalibrated agent judgement).
