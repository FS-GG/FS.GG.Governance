# Feature Specification: Per-Gate Cache-Eligibility Verdict Core

**Feature Branch**: `041-cache-eligibility-verdict`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan" — resolved against
`docs/initial-implementation-plan.md`. Phases 2, 5, 11, and 12 are complete. The one genuinely-deferred
Governance-owned row is the cache-eligibility line of the route/audit emission row (Phase 2 / Phase 11):
*"Emit deterministic route and audit JSON with selected gates, matched rules, unmatched governed paths,
expected artifacts, cost, **cache eligibility**, profile-adjusted enforcement, and exit-code basis."* The
freshness-key core (F029) and the evidence-reuse decision core (F030) landed the *evaluation* logic, and the
route.json (F020) / audit.json (F025) projections already carry each gate's freshness-key **inputs** — but the
JSON does not yet carry an **evaluated cache-eligibility verdict**. Continuing this repo's maintainer-confirmed
**pure-core-first** rhythm (F015–F040 each landed a pure, total, deterministic core before any host edge or
projection consumed it), this row delivers that missing piece as its **decision value**: a pure core that
evaluates, for each selected gate of a routed change, *whether prior evidence may be reused or the gate must be
recomputed* — and, when it must recompute, *why* — so a later projection row can emit that verdict and a later
host row can resolve and supply its inputs.

## Overview

Governance routes a change to a set of gates (F019), and for any expensive gate it would like to reuse prior
evidence instead of rerunning it — but only when that reuse is defensible. F029 answered *"do two runs
fingerprint to the same world?"* and F030 answered, for a **single** candidate against a store of recorded
evidence, *"may I reuse, and if not, which input changed?"*. What is still missing is the **per-change roll-up**
that the JSON reports need: given **all** the gates a change selected, one evaluated, attributable
cache-eligibility verdict **per gate**, in a stable order, with nothing dropped or merged.

This row delivers exactly that as a pure core that reuses F030 verbatim:

- It consumes a set of **candidate gate evaluations** — each pairing a selected gate's stable identity with the
  freshness inputs that have **already been resolved** for it — together with the recorded **evidence store**.
- For each candidate it produces a **cache-eligibility verdict**: *reusable* (naming the reusable evidence
  reference) or *must-recompute* (naming the cause — no prior evidence, or exactly which freshness inputs
  changed).
- It rolls these up into a **deterministic, gate-attributable report**: one verdict per candidate gate, ordered
  by gate identity, with every gate preserved.

The contract is **recompute-by-default safety, totality, determinism, and gate-attributable no-hide
attribution** — never a falsely-claimed cache hit, never an unexplained miss, never a dropped or merged gate.
The verdict is **necessary-not-sufficient**: a *reusable* verdict authorizes no skip and carries no enforcement
meaning by itself; it is one input a later host wiring step composes with everything else before any gate is
actually skipped.

This core makes **no cache lookup against a real store on disk**, performs **no persistence** (no filesystem,
database, or network read/write), computes **no freshness key or hash itself** (it consumes F029/F030 results),
**resolves none of the freshness inputs** it is given (the host does that — the core fabricates no rule hash,
artifact hash, command version, generator version, or revision), reads **no clock / filesystem / git /
environment**, runs **no gate** and produces **no evidence**, renders **no JSON** (the projection row does that),
maps **no exit code**, and adds **no CLI**. Its sole output is the typed cache-eligibility report value.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Recompute by default when evidence is absent or stale (Priority: P1)

A reviewer routes a change and wants to know, gate by gate, whether each expensive gate can safely reuse prior
evidence. For any gate with no prior recorded evidence — or whose recorded evidence was produced under different
freshness inputs — the answer must default to **must-recompute**, never to a falsely-claimed reuse, and the
reason must be named.

**Why this priority**: This is the safety property the whole row exists to protect. A cache that defaults to
"reuse" when it is unsure would let stale or fabricated evidence pass a gate. Defaulting to recompute is what
makes cache eligibility trustworthy. It is the minimum viable, independently demonstrable slice.

**Independent Test**: Evaluate a set of candidate gates against an empty evidence store, and against a store
whose entries differ in one or more freshness inputs; confirm every verdict is *must-recompute*, each naming a
cause — *no prior evidence*, or the exact set of changed freshness-input categories — and that no candidate
yields a reuse.

**Acceptance Scenarios**:

1. **Given** a candidate gate and an empty evidence store, **When** cache eligibility is evaluated, **Then**
   that gate's verdict is *must-recompute* with cause *no prior evidence*.
2. **Given** a candidate gate whose recorded evidence differs in one or more freshness inputs, **When**
   evaluated, **Then** the verdict is *must-recompute* naming exactly the changed freshness-input categories
   (and no others).
3. **Given** any candidate gate for which the evidence store yields no defensible match, **When** evaluated,
   **Then** the verdict is never *reusable*.

---

### User Story 2 - Reusable when prior evidence matches, naming the evidence (Priority: P2)

When a gate's resolved freshness inputs exactly match a recorded evidence entry, the verdict is **reusable** and
it names the reusable evidence reference, so a later step can act on it and an auditor can trace which evidence
would be reused.

**Why this priority**: Reuse is the cost-saving the phase is for, but it only has value once the safe default
(US1) is established. Carrying the evidence reference (not a bare boolean) is what makes the verdict auditable
and actionable downstream.

**Independent Test**: Record evidence for a gate's freshness inputs, then evaluate a candidate with matching
inputs; confirm the verdict is *reusable* and carries the recorded evidence reference.

**Acceptance Scenarios**:

1. **Given** an evidence store containing an entry whose freshness inputs match a candidate gate's resolved
   inputs, **When** evaluated, **Then** that gate's verdict is *reusable* carrying that entry's evidence
   reference.
2. **Given** a store with multiple recorded entries for a gate, **When** evaluated, **Then** the verdict
   reflects the same most-recent-wins reuse decision the underlying evidence-reuse core makes (no new reuse
   policy is introduced here).

---

### User Story 3 - One attributable verdict per gate, deterministic and total (Priority: P3)

A reviewer evaluates all gates a change selected and receives **one** verdict **per gate**, each attributed to
its gate identity, in a stable order, with no gate dropped, merged, or duplicated — and identical inputs always
yield an identical report.

**Why this priority**: The route/audit JSON places each verdict under its gate; the report must therefore be
gate-attributable, complete, deterministically ordered, and reproducible. This is what makes it projectable and
diff-stable, but it depends on the per-gate verdict (US1/US2) existing first.

**Independent Test**: Evaluate a set of candidate gates supplied in arbitrary order; confirm exactly one verdict
per gate, ordered by gate identity, every gate preserved, and that re-evaluating the same inputs under a changed
working directory, clock, or filesystem yields a byte-identical report.

**Acceptance Scenarios**:

1. **Given** N candidate gates, **When** evaluated, **Then** the report contains exactly N verdicts — one per
   gate — each carrying its originating gate identity, with no gate dropped, merged, or duplicated.
2. **Given** the same candidates supplied in two different orders, **When** evaluated, **Then** both reports are
   identical and ordered by gate identity.
3. **Given** identical candidates and store, **When** evaluated under a different working directory / clock /
   filesystem state, **Then** the report is identical (no I/O is performed).

---

### Edge Cases

- **No candidate gates** (a change that selected nothing cacheable): the report is empty — a total, valid
  result, not an error.
- **One candidate gate**: a single-element report; the single-gate path behaves like the many-gate path.
- **Duplicate gate identities among candidates**: two candidates with the same gate identity — the result is
  deterministic and explicitly specified (see Assumptions), never an arbitrary or order-dependent collapse.
- **Empty evidence store**: every candidate resolves to *must-recompute / no prior evidence* (the US1 default).
- **A candidate whose inputs match exactly**: *reusable* with the evidence reference (US2); the inclusive,
  exact-match semantics are inherited from the underlying evidence-reuse core, not re-defined here.
- **A candidate whose every freshness input differs**: *must-recompute* naming every changed category — the
  no-hide attribution is complete, never truncated to "first difference".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core MUST decide, for each supplied candidate gate, a single **cache-eligibility verdict** that
  is exactly one of two outcomes — *reusable* or *must-recompute* — so that a threshold-unmet or unexplained
  verdict is unrepresentable.
- **FR-002**: A *reusable* verdict MUST carry the reusable **evidence reference**; a *must-recompute* verdict MUST
  carry a **named cause** — either *no prior evidence* or the explicit set of changed freshness-input categories
  (the no-hide rule). No verdict may be an opaque yes/no.
- **FR-003**: The core MUST default to *must-recompute* whenever prior evidence is absent or does not defensibly
  match the candidate's resolved freshness inputs; it MUST NOT emit *reusable* in the absence of a matching
  recorded entry (recompute-by-default safety).
- **FR-004**: The core MUST derive each candidate's verdict by composing the existing evidence-reuse decision
  over the candidate's resolved freshness inputs and the supplied evidence store; it MUST NOT introduce a new or
  divergent reuse policy, re-implement matching, or re-rank recorded entries.
- **FR-005**: The core MUST attribute every verdict to its originating gate identity, so the verdict can be placed
  under the correct gate in a later projection.
- **FR-006**: The core MUST return exactly one verdict per supplied candidate gate, preserving every candidate —
  no gate dropped, merged into another, or silently duplicated — and MUST emit verdicts in a deterministic order
  by gate identity, independent of the order candidates were supplied.
- **FR-007**: The core MUST be **total**: it returns a well-formed report for every input — including no
  candidates, a single candidate, and a candidate with no matching evidence — and never throws, swallows a
  failure, or silently drops a candidate.
- **FR-008**: The core MUST be **pure and deterministic**: identical candidates and evidence store always yield
  an identical report; it reads no clock, filesystem, git, environment, or network, invokes no gate, computes no
  hash or freshness key itself, and resolves none of the freshness inputs it is given.
- **FR-009**: The core MUST treat the supplied freshness inputs and evidence references as opaque facts produced
  elsewhere — it neither resolves, fabricates, re-hashes, nor interprets a rule hash, artifact hash, command
  version, generator version, or revision, and it produces no evidence of its own.
- **FR-010**: A *reusable* verdict MUST be **necessary-not-sufficient**: it carries no skip action, no enforcement
  severity, no ship verdict, and no exit-code basis — it asserts only "prior evidence may be reused for this
  gate", which a later host step composes with other facts before any gate is actually skipped.
- **FR-011**: The core MUST NOT render JSON, persist anything, perform any cache lookup against a real on-disk or
  networked store, map any process exit code, or add any CLI — those belong to the later projection and host rows.
- **FR-012**: The core MUST reuse the existing freshness-input, evidence-reference, evidence-store, changed-input
  category, and gate-identity vocabulary verbatim rather than redefining them, introducing only the minimal new
  vocabulary this row needs (the candidate pairing, the two-outcome verdict, and the per-gate report).
- **FR-013**: The core MUST add no new third-party dependency; its only couplings are to the sibling pure cores
  that already own the reused vocabulary.
- **FR-014**: The change MUST be purely additive: it modifies no existing merged core, public surface baseline,
  or projection, and leaves existing build and test runs unchanged.

### Key Entities *(include if feature involves data)*

- **Candidate gate evaluation**: one selected gate's stable **gate identity** paired with the **freshness inputs**
  already resolved for it. The unit of input the core evaluates; the inputs are supplied, never resolved here.
- **Evidence store**: the supplied collection of previously recorded evidence (each entry pairing freshness
  inputs with an evidence reference). Consumed verbatim from the existing evidence-reuse vocabulary; the core
  records nothing into it.
- **Cache-eligibility verdict**: the two-outcome per-gate decision — *reusable* (carrying the evidence reference)
  or *must-recompute* (carrying the named cause: *no prior evidence*, or the changed freshness-input categories).
- **Changed-input category**: the existing freshness-input category vocabulary, reused verbatim to name what
  differed when a verdict is *must-recompute*.
- **Cache-eligibility report**: the per-change roll-up — one cache-eligibility verdict per candidate gate, each
  attributed to its gate identity, in deterministic gate-identity order, with every gate preserved.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every candidate evaluated against an empty store, or against a store with no defensibly-matching
  entry, the verdict is *must-recompute* with a named cause — 100% of such cases, with zero falsely-reusable
  verdicts (US1, FR-003).
- **SC-002**: For every candidate whose resolved freshness inputs match a recorded entry, the verdict is
  *reusable* and carries the correct evidence reference — matching the underlying evidence-reuse decision in 100%
  of cases (US2, FR-002/FR-004).
- **SC-003**: Every *must-recompute* verdict driven by changed inputs names exactly the set of changed
  freshness-input categories — no missing category and no spurious category — across all combinations of changed
  inputs (US1, FR-002).
- **SC-004**: The core returns a well-formed report and never throws across the full cross-product of candidate
  counts (zero, one, many) and store states (empty, matching, non-matching) (US3, FR-007).
- **SC-005**: Identical candidates and store yield a byte-identical report under changed working directory,
  clock, and filesystem state, with no I/O performed (US3, FR-008).
- **SC-006**: Every report contains exactly one verdict per supplied candidate gate, attributed to its gate
  identity, in deterministic gate-identity order, with no gate dropped, merged, or duplicated — independent of
  input order (US3, FR-005/FR-006).
- **SC-007**: A *reusable* verdict carries no skip action, enforcement severity, ship verdict, or exit-code basis
  (FR-010) — verified by the verdict type carrying none of those fields.
- **SC-008**: The feature is additive: existing cores, public-surface baselines, and projections are unchanged,
  and existing build/test runs pass unchanged (FR-014).

## Assumptions

- **Freshness inputs are resolved upstream, not here.** Each candidate's freshness inputs arrive fully resolved
  (rule/artifact hashes, command/generator versions, base/head revisions). The host wiring that lifts a routed
  gate's declared identity into fully-resolved freshness inputs is a later row, explicitly out of this scope; this
  core fabricates none of those values.
- **Reuse policy is inherited, not redefined.** "Defensibly matches" means exactly what the existing
  evidence-reuse decision core already means (exact match on all freshness inputs, most-recent entry wins). This
  row composes that decision per gate; it introduces no new matching, ranking, recency, or expiry policy.
- **Duplicate candidate gate identities** are evaluated independently and ordered deterministically by gate
  identity; the row does not assume the caller pre-deduplicates, and it neither merges nor drops duplicates. (If
  planning surfaces a strong reason to forbid duplicates outright, that becomes an input precondition rather than
  a silent collapse.)
- **The verdict is advisory to the host, not an action.** Producing a *reusable* verdict authorizes nothing on
  its own; the actual skip/run, severity, ship roll-up, and exit code are composed downstream (Phase-5/host rows),
  consistent with the necessary-not-sufficient discipline established by F039/F040.
- **Determinism is the contract, not performance.** The decision is a small computation over a handful of
  supplied facts per gate; latency is not a success criterion.

## Out of Scope

- **Rendering the verdict into route.json / audit.json** — the projection that emits the cache-eligibility verdict
  into the JSON documents (extending F020 / F025) is the **next** row, not this one.
- **Host wiring that resolves freshness inputs** — sensing rule/artifact hashes, command/generator versions, and
  base/head revisions for a routed gate, and threading them in, is a later host row.
- **Cache storage, lookup against a real store, eviction, or expiry** — no persistence or on-disk/networked cache
  is touched.
- **Running gates, producing evidence, or computing freshness keys/hashes** — owned by F029/F030 and the eventual
  execution edge.
- **Enforcement, ship verdict, exit-code mapping, and CLI** — owned by Phase-5 cores (F023/F024) and the host
  commands (F022/F026).
