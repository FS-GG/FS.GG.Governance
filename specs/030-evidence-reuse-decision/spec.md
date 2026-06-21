# Feature Specification: Evidence-Reuse Decision Core

**Feature Branch**: `030-evidence-reuse-decision`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "next item in plan" — resolved against `docs/initial-implementation-plan.md`.
**Phase 11: Cost, Cache, and Provenance** was opened by F029 (`FS.GG.Governance.FreshnessKey`), which landed
its **first** checkbox — *"Define freshness keys over rule hash, artifact hash, command version, generator
version, base/head, environment class, and output digest"* — as a pure core computing a deterministic,
byte-stable freshness **key** plus the `matches`/`diff` comparison. The **next** unchecked Phase-11 line is
*"Cache reusable evidence only when all freshness inputs match,"* and F029's plan named its `matches`/`diff`
as *"the literal foundation of the later 'cache reusable evidence only when all freshness inputs match'
row."* Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F029 each landed a pure,
total, deterministic core before any host edge consumed it), this row is sliced to that single decision: the
typed **evidence-reuse vocabulary** and the total, deterministic function that decides — given a candidate
run's freshness inputs and a collection of previously recorded evidence — *whether* recorded evidence may be
reused, and when it may not, *why*. It performs **no persistence** (no filesystem/database read or write),
no eviction/expiry, computes **no hashes** itself, reads **no clock / filesystem / git / network**, persists
**no artifact**, runs **no gate**, and adds **no CLI**.

## Overview

Governance keeps the local authoring loop cheap by reusing expensive evidence (a build, a test run, a pack)
instead of recomputing it — but **only when it is defensible to do so**. F029 answered "do these two runs
fingerprint to the same world?" with the freshness **key** and its `matches` predicate. This feature answers
the operational question that sits directly on top of it: *"Given the evidence I have already recorded, may I
reuse any of it for this run — and if not, exactly which input changed?"*

That decision is the gate the whole cost/cache phase turns on. It must be **deterministic, total, and
auditable**: the same candidate against the same recorded evidence always yields the same reuse-or-recompute
answer, every recorded entry is considered, and a *recompute* answer is never an opaque "no" — it names the
differing freshness inputs (or "no prior evidence for this work") so a reviewer can see why the cache did not
serve a hit.

This row delivers that as a pure core that reuses F029 verbatim:

- **Model recorded evidence and a reuse store as pure values** — a *recorded evidence entry* pairs a F029
  `FreshnessInputs` value with an **opaque reference to the recorded evidence** (an already-recorded handle —
  the core treats it as an opaque token, never inspecting or producing the evidence itself). A *reuse store*
  is the closed collection of such entries, modeled as an immutable value (no I/O, no live store).
- **Decide reuse with a single total function** — given candidate `FreshnessInputs` and a reuse store, return
  a **reuse decision**: *Reuse* (carrying the matching entry's evidence reference) **iff** some recorded
  entry's freshness inputs match the candidate on **every** category (F029 `matches`), else *Recompute*
  carrying the **no-hide explanation** of why no entry served.
- **Record evidence purely** — a total function that returns a **new** reuse store with the candidate's
  inputs and evidence reference recorded, so that a subsequent identical run would reuse it. Re-recording
  under inputs that match an existing entry **refreshes** that entry deterministically rather than
  accumulating duplicates.

The core is **pure over supplied data**, exactly like F029 and kernel `Freshness`: the reuse store is a value
handed in, not a live cache; the evidence references are opaque tokens minted at the edge; the hashes,
versions, and revisions inside `FreshnessInputs` were already sensed at the edge. The **actual persistence**
of the store (reading/writing it to disk or a database), **eviction / size limits / expiry**, the
**output-digest verification** of reused evidence, the **broad-route cost explanation**, command-run records,
and provenance/attestation are **later Phase-11 rows** and remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reuse recorded evidence when, and only when, all freshness inputs match (Priority: P1)

A Governance consumer recorded evidence for an expensive gate, keyed by that run's freshness inputs. On a
later run for the same gate, Governance must decide — deterministically, without re-running the gate —
whether the recorded evidence still applies. It asks the reuse decision for the candidate's freshness inputs
against the recorded evidence.

**Why this priority**: This is the whole point of the feature and the operational core of the cost/cache
phase. Reuse that fires when any input differs is unsafe (stale evidence); reuse that fails to fire when all
inputs match is worthless (never reuses). The "reuse iff all freshness inputs match" rule is the single
guarantee everything else depends on. It is independently demonstrable and delivers the core value alone.

**Independent Test**: Build a reuse store holding one recorded entry. Ask the decision for a candidate whose
inputs are identical in every category and assert *Reuse* carrying that entry's evidence reference. Then, for
each freshness-input category in turn, ask the decision for a candidate that differs in **only that one
category** and assert *Recompute*. No host, no I/O, no other feature required.

**Acceptance Scenarios**:

1. **Given** a reuse store holding one entry, **When** the decision is asked for a candidate whose freshness
   inputs equal that entry's in every category, **Then** the result is *Reuse* carrying exactly that entry's
   recorded evidence reference.
2. **Given** a reuse store holding one entry, **When** the decision is asked for a candidate differing only in
   the rule hash (and likewise, tested separately, the covered-artifact set, command version present/absent,
   generator version, base revision, head revision, environment class, or carried gate identity), **Then** the
   result is *Recompute* (no reuse).
3. **Given** a reuse store holding several entries of which exactly one matches the candidate on every
   category, **When** the decision is asked, **Then** the result is *Reuse* carrying that one matching entry's
   evidence reference, regardless of the other entries.
4. **Given** an empty reuse store, **When** the decision is asked for any candidate, **Then** the result is
   *Recompute* (there is nothing to reuse).

---

### User Story 2 - A recompute decision is always explained, never opaque (Priority: P1)

When the decision is *Recompute*, an auditor must be able to see **why** the cache did not serve a hit — was
there no prior evidence for this work at all, or was there prior evidence whose inputs changed, and which
inputs? The decision must carry that explanation so "why did this run not reuse last run's evidence?" is
answerable without re-deriving anything outside the core.

**Why this priority**: The design's honesty boundary ("profiles never hide underlying verdicts"; generated
views must identify sources and what changed) extends to cache decisions: a recompute that cannot be
explained is a silent failure. Auditability of the *negative* answer is as load-bearing as the reuse itself,
so it is co-P1 with Story 1.

**Independent Test**: Against a store holding a prior entry for the same gate identity that differs only in
the head revision, ask the decision for the candidate and assert the *Recompute* result names the head
revision (using F029's `diff` vocabulary) as the differing category. Against an empty store (or a store with
no entry for this gate identity), assert the *Recompute* result reports "no prior evidence" rather than a
spurious category difference.

**Acceptance Scenarios**:

1. **Given** a reuse store with a recorded entry for the candidate's gate identity that differs only in one or
   more freshness-input categories, **When** the decision is *Recompute*, **Then** it identifies at least the
   specific differing category(ies) for that prior entry (the no-hide explanation, expressed in F029's input
   categories).
2. **Given** a reuse store containing no recorded entry for the candidate's work at all, **When** the decision
   is *Recompute*, **Then** it reports the absence of prior evidence (a distinct, locatable cause — not an
   empty/ambiguous "they differ").
3. **Given** any *Recompute* decision, **When** it is inspected, **Then** the cause is always present and
   non-ambiguous (every recompute has a stated reason).

---

### User Story 3 - Recording evidence is pure, deterministic, and de-duplicating (Priority: P2)

After running a gate, Governance records its evidence so the next identical run can reuse it. Recording must
be a pure transform of the reuse store value (returning a new store), must make a just-recorded entry
immediately reusable by a matching candidate, and must not let repeated recording under matching inputs
accumulate duplicate entries.

**Why this priority**: Recording is what makes reuse possible on the *next* run, and it must compose cleanly
with the decision (Stories 1–2). It is essential but builds on the decision contract, so it is P2.

**Independent Test**: Record an entry into an empty store; assert a candidate with matching inputs now decides
*Reuse* with that evidence reference. Record again under inputs that match an existing entry but carry a new
evidence reference; assert the store still resolves a matching candidate to *Reuse* (with the refreshed
reference) and has not grown an additional colliding entry. Record under inputs that match nothing; assert
both entries remain independently reusable.

**Acceptance Scenarios**:

1. **Given** an empty reuse store, **When** an entry is recorded for some freshness inputs and evidence
   reference, **Then** a later decision for a candidate whose inputs match those inputs returns *Reuse*
   carrying that evidence reference.
2. **Given** a reuse store already holding an entry, **When** evidence is recorded again under inputs that
   match that entry but with a different evidence reference, **Then** a matching candidate decides *Reuse* with
   the most recently recorded reference and the store holds no duplicate entry for those inputs.
3. **Given** a reuse store, **When** evidence is recorded under inputs that match no existing entry, **Then**
   the new entry and every prior entry remain independently reusable by their respective matching candidates.
4. **Given** the same starting store and the same sequence of recordings, **When** recording is replayed,
   **Then** the resulting store yields identical reuse decisions for every candidate (recording is
   deterministic).

---

### Edge Cases

- **Empty reuse store.** Deciding against a store with no entries is valid and always yields *Recompute* with
  the "no prior evidence" cause (never an error, never a spurious category diff).
- **Multiple entries match the candidate.** Because matching entries agree with the candidate on every
  category, they agree with each other; the decision is *Reuse* and resolves to a single, deterministically
  chosen evidence reference (re-recording's refresh rule keeps at most one entry per matching-input class, so
  this is the degenerate boundary, handled deterministically).
- **No entry shares the candidate's gate identity.** The decision is *Recompute* with "no prior evidence for
  this work," distinct from "prior evidence existed but inputs changed."
- **Re-recording under matching inputs.** Refreshes the existing entry's evidence reference rather than adding
  a second entry; the store does not grow unboundedly under repeated identical-input recordings.
- **Order/duplication of covered artifacts in candidate or recorded inputs.** Inherited from F029: reordered
  or duplicated covered-artifact hashes never change whether two inputs match, so they never change the reuse
  decision.
- **Opaque evidence reference is never interpreted.** The core neither parses, validates, produces, nor
  dereferences the evidence reference; it only carries the matching entry's reference back on *Reuse*. An
  empty or unusual reference string is a literal value, not an error.
- **Future input growth.** Inherited from F029: adding a new freshness-input category necessarily makes older
  recorded entries stop matching new candidates, correctly forcing *Recompute* of evidence recorded before the
  category existed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **recorded-evidence entry** pairing a F029 `FreshnessInputs`
  value with an **opaque evidence reference** (a handle to already-recorded evidence). The reference MUST be
  treated as an opaque, comparable token: the core MUST NOT parse, validate, dereference, or produce the
  underlying evidence.
- **FR-002**: The system MUST define a **reuse store** as an immutable value holding a collection of
  recorded-evidence entries. It MUST NOT model or perform any live store, persistence, connection, or I/O.
- **FR-003**: The system MUST provide a single, pure, total **reuse-decision** function that, given candidate
  `FreshnessInputs` and a reuse store, returns either *Reuse* (carrying an evidence reference) or *Recompute*
  (carrying an explanation). It MUST be defined for every well-typed input (no value causes failure or
  exception).
- **FR-004**: The decision MUST be *Reuse* **iff** at least one recorded entry's freshness inputs match the
  candidate on **every** input category — reusing F029's match rule verbatim (all freshness inputs match). It
  MUST be *Recompute* whenever no recorded entry matches.
- **FR-005**: On *Reuse*, the decision MUST carry the matching entry's evidence reference. When more than one
  entry matches, the carried reference MUST be chosen **deterministically** (same store + same candidate ⇒
  same reference).
- **FR-006**: On *Recompute*, the decision MUST carry a **non-hidden, locatable cause**: either "no prior
  recorded evidence for the candidate's work" or, where prior evidence for the candidate's work exists, the
  specific differing freshness-input category(ies) — expressed using F029's input-category vocabulary
  (`diff`). A *Recompute* MUST never be an opaque, reasonless negative.
- **FR-007**: The system MUST provide a pure, total **record** function that, given freshness inputs, an
  evidence reference, and a reuse store, returns a **new** reuse store in which a subsequent decision for a
  candidate matching those inputs returns *Reuse* with that reference. Recording MUST NOT mutate the input
  store value.
- **FR-008**: Recording under inputs that **match an existing entry** MUST **refresh** that entry (replace its
  evidence reference, most-recent-wins) rather than add a duplicate; the store MUST hold at most one entry per
  matching-input class. Recording under inputs that match no existing entry MUST add a new entry while leaving
  existing entries reusable.
- **FR-009**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no
  filesystem, no git, no environment, and no network. Identical candidate + identical store always yields the
  identical decision; identical starting store + identical recording sequence always yields an equivalent
  store (same reuse decisions for all candidates).
- **FR-010**: The core MUST **consume F029 verbatim** — `FreshnessInputs`, the `matches` rule, and the `diff`
  explanation — without modifying F029 or any other merged core. It MUST NOT redefine the freshness-input
  vocabulary. This feature is additive.
- **FR-011**: The core MUST compute **no persistence, no eviction/expiry, no size limit, and no reuse side
  effect on any external store**; it MUST compute no ship verdict, run no gate, perform no output-digest
  verification, persist no artifact, and add no CLI surface. Its sole outputs are the reuse decision value and
  the new reuse-store value.
- **FR-012**: The core MUST handle the degenerate cases as ordinary, total outcomes (not errors): an empty
  reuse store, no entry sharing the candidate's gate identity, multiple matching entries, re-recording under
  matching inputs, and an empty/unusual evidence-reference string (each as described in Edge Cases).
- **FR-013**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1**
  change (see Assumptions). [The concrete module home and name are a planning decision deferred to
  `/speckit-plan`.]
- **FR-014**: The core MUST NOT add a new third-party package dependency; the decision MUST use only
  facilities already available to the merged cores (the shared framework / BCL) plus F029.

### Key Entities *(include if feature involves data)*

- **Recorded-evidence entry**: A pairing of a F029 `FreshnessInputs` value (the world the evidence was
  recorded against) with an **opaque evidence reference** (a handle to the recorded evidence). The reference
  carries no semantics the core interprets.
- **Reuse store**: An immutable collection of recorded-evidence entries — the supplied, in-value "what has
  been recorded so far." Not a live cache, connection, or file.
- **Reuse decision**: The total result of asking "may I reuse?" for a candidate against a store — either
  *Reuse* (with the matching entry's evidence reference) or *Recompute* (with a non-hidden cause).
- **Recompute cause** (the no-hide explanation): Why no entry served — either "no prior evidence for this
  work" or the specific differing freshness-input categories (F029's `InputCategory` vocabulary).
- **Opaque evidence reference** (new opaque value): A token standing for already-recorded evidence; carried
  on *Reuse*, never produced, parsed, or dereferenced by this core.
- **Freshness inputs / match rule / diff** (reused from F029): The freshness-input value, the all-categories
  match rule, and the differing-category explainer — consumed verbatim, not redefined.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a candidate whose freshness inputs match a recorded entry on every category, the decision is
  *Reuse* carrying that entry's evidence reference in 100% of cases; for a candidate differing in **exactly
  one** category, the decision is *Recompute* — verified for **every** freshness-input category (rule hash,
  covered-artifact set, command version present/absent, generator version, base revision, head revision,
  environment class, gate identity), i.e. 100% single-field-change coverage.
- **SC-002**: For any candidate and any reuse store, asking the decision twice yields identical results in
  100% of cases (determinism); reordering or duplicating the covered-artifact hashes in the candidate or in a
  recorded entry never changes the decision.
- **SC-003**: Every *Recompute* decision carries a locatable cause in 100% of cases — either "no prior
  evidence" or at least one specific differing category — and never an empty/ambiguous negative.
- **SC-004**: An empty reuse store yields *Recompute* with the "no prior evidence" cause for every candidate
  (100%), and never a spurious category difference or error.
- **SC-005**: After recording an entry, a candidate whose inputs match it decides *Reuse* with the recorded
  reference in 100% of cases; re-recording under matching inputs leaves the store with no duplicate entry for
  those inputs and resolves a matching candidate to the most recently recorded reference.
- **SC-006**: The core reads no clock, filesystem, git, environment, or network — demonstrable by decisions
  and recordings being identical when performed in different working directories, at different times, and with
  unrelated repository/filesystem state changed between operations.
- **SC-007**: The merged cores (including F029) and their `surface/*.surface.txt` baselines, and `dotnet
  build` / `dotnet test` over the existing projects, are **unchanged** by this feature except for the additive
  new surface (if any) — no existing baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the reuse decision + record, over a pure store value.** The actual persistence of the reuse store
  (reading/writing it to disk or a database), eviction / expiry / size limits, the output-digest verification
  of reused evidence, the broad-route cost explanation, command-run records, and provenance/attestation are
  **later Phase-11 rows** and are out of scope here. This row produces only the deterministic reuse decision
  and the pure record transform.
- **Evidence references are opaque, supplied tokens.** Consistent with F029's "hashes/versions are supplied,
  not computed" framing, the evidence reference is minted at the edge (the interpreter that records evidence)
  and passed in. This core never opens a file, runs a gate, dereferences a reference, or reads a clock. Whether
  the opaque reference is a new newtype or an already-available value is a home decision deferred to
  `/speckit-plan`; the reasonable default is a thin new opaque-string newtype (mirroring F029's `Revision`).
- **The reuse rule is exactly F029 `matches`.** "Cache reusable evidence only when all freshness inputs match"
  is implemented as "*Reuse* iff some recorded entry `matches` the candidate," reusing F029 verbatim — no new
  notion of partial or fuzzy match.
- **The recompute explanation reuses F029 `diff`.** The no-hide cause is expressed in F029's `InputCategory`
  vocabulary; for the "prior evidence existed but changed" case the differing categories come from `diff`
  against the relevant prior entry. The precise selection of *which* prior entry's diff to surface when
  several near-misses exist (e.g. entries sharing the candidate's gate identity) is a planning detail deferred
  to `/speckit-plan`; the contract here is only that the cause is always present, locatable, and non-ambiguous.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. It consumes F029 (and transitively the F014 typed facts) verbatim and modifies none of them.
  Whether it lands as a new pure-core module or extends F029 is the only home decision left to
  `/speckit-plan`; the established rhythm suggests a new minimal core depending on F029.
- **Determinism is the contract, not performance.** The store holds a modest number of recorded entries per
  gate; there is no latency or throughput target. Byte-stability of decisions and totality are the guarantees.
- **The reuse store representation is a planning decision.** Whether the store is modeled as a list, a set, or
  a key-indexed map (keyed by the F029 `Key`) is deferred to `/speckit-plan`; the spec constrains only its
  observable behavior (the match rule, the refresh/de-dup rule, determinism), not its representation.
