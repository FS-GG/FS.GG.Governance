# Feature Specification: Provenance Core

**Feature Branch**: `033-provenance-record`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 11: Cost, Cache, and Provenance** has landed its first four
rows as pure cores: F029 (`FS.GG.Governance.FreshnessKey`) — *"Define freshness keys…"* — F030
(`FS.GG.Governance.EvidenceReuse`) — *"Cache reusable evidence only when all freshness inputs match"* — F031
(`FS.GG.Governance.RouteExplain`) — *"Explain high-cost routes…"* — and F032
(`FS.GG.Governance.CommandRecord`) — *"Record command runs with executable, arguments, …, and duration."* The
**next** unchecked Phase-11 line is *"Include source commit, base/head, rule hash, generator version, artifact
digests, command records, environment class, and builder identity in provenance."* Continuing this repo's
maintainer-confirmed **pure-core-first** rhythm (F015–F032 each landed a pure, total, deterministic core before
any host edge consumed it), this row is sliced to that single projection: the typed **provenance vocabulary**
and the total, deterministic functions that assemble a complete provenance value from already-sensed facts,
hold its **sensed / non-deterministic** metadata apart from its **reproducible** facts, and project a
byte-stable **canonical identity** over the reproducible part. It performs **no command execution** (it spawns
no process and captures no bytes), reads **no clock / filesystem / git / environment / network**, computes **no
digest from raw bytes** (digests and revisions are supplied), persists **no artifact**, renders **no JSON /
audit.json / attestation document**, and adds **no CLI**.

## Overview

When Governance produces protected-boundary evidence — a routed-and-shipped change with its gates run — the
audit trail must be able to *explain where that evidence came from*: which source it was built against, which
rules and tools produced it, which command runs executed, and who (or what) built it. The design states this
directly: provenance must *"include source commit, base/head, rule hash, generator version, artifact digests,
command records, environment class, and builder identity,"* and the phase's exit criterion is that *"audit
records are sufficient to explain builds, tests, packs, template instantiation, git diffs, package inspection,
and visual capture."* Provenance is the value the audit phase references and the later attestation row signs.

Sensing those facts — resolving the git commit and base/head, hashing the artifacts, running the commands,
measuring durations, reading the builder identity from the environment — is impure work that belongs at the Host
effects boundary (Principle IV), exactly like F016's git sensing and the future command-execution edge. What
this row delivers, ahead of that edge, is the **pure value and vocabulary** the edge will populate and every
downstream report will read: a typed, complete, total **provenance record** assembled from already-sensed
facts, with its **sensed / non-deterministic** metadata (the command records' durations, and any wall-clock
timestamp) held apart so a deterministic report can carry it honestly (the honesty boundary Phase-11 row 6
applies across reports), and a **canonical identity** over the provenance's reproducible facts that is stable
across re-runs (so two builds of the same source with the same rules, tools, runs, and builder share an
identity regardless of how long each run took).

This row answers, deterministically: *"Given the already-sensed facts of a build, what is the complete, typed
provenance value of it; which of its facts are reproducible versus sensed; and what is its stable canonical
identity?"*

This row delivers that as a pure core that **reuses the existing typed facts verbatim** where they exist — F029's
`RuleHash`, `GeneratorVersion`, `ArtifactHash`, and `Revision` (for base/head); F014's `EnvironmentClass`; and
F032's whole `CommandRecord` (and its reproducible canonical identity) for the command-records field — and
otherwise introduces only the minimal new provenance vocabulary:

- **Model a build's provenance as a complete, typed value** — one *provenance record* carries every declared
  fact: the source commit, the base and head revisions, the rule hash, the generator version, the artifact
  digests (the set of artifacts the evidence covers), the command records (the runs that produced it), the
  environment class, and the builder identity. No declared fact is dropped, stringly-typed away, or made
  optional-by-omission.
- **Distinguish reproducible facts from sensed metadata** — the command records carry sensed durations (F032),
  and any wall-clock timestamp carried by provenance is sensed too; those are **marked as sensed /
  non-deterministic** and **excluded from the canonical identity**, while every other fact is reproducible. A
  deterministic report may therefore include the full provenance while clearly flagging the sensed parts.
- **Project a deterministic canonical identity** — a byte-stable identity computed **only over the reproducible
  facts** (source commit, base/head, rule hash, generator version, artifact digests, the command runs'
  reproducible identities, environment class, builder identity), excluding the sensed durations, so the same
  build produced the same way yields the same identity on every recording.

The core is **pure over supplied data**, exactly like F019/F020/F029/F030/F031/F032: every fact — including the
already-computed digests, the resolved revisions, and the embedded command records — is handed in as a value;
nothing is spawned, captured, hashed-from-bytes, clocked, or persisted. The **actual sensing** (resolving the
commit and base/head, hashing artifacts, running the commands, reading the builder identity), the
**persistence** of the provenance, its **rendering** into any artifact (audit.json or a provenance document),
**attestation / signing**, and any **CLI** are a later Phase-11 row or a host edge and remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capture a build's provenance as one complete, typed value (Priority: P1)

A Governance host has just produced evidence for a change and sensed its facts (the source commit, base/head,
the rules and tools that produced it, the artifacts it covers, the command runs, the environment class, the
builder identity). It needs to assemble those facts into a single typed provenance value that carries **all
eight** declared fields, so nothing about *where the evidence came from* is lost or left untyped before the
provenance flows into audit and attestation.

**Why this priority**: This is the core of the design's *"…include … in provenance"* row and the phase exit
criterion (*"audit records are sufficient to explain builds, tests, packs…"*). A provenance value missing any
declared fact cannot explain where evidence came from; assembling the complete typed value is the load-bearing
guarantee and is independently demonstrable.

**Independent Test**: Supply the eight sensed facts of a build and build the provenance value; assert it carries
each fact verbatim (source commit, base revision, head revision, rule hash, generator version, the artifact
digests, the command records, environment class, builder identity), with the artifact digests reported as a set
and the command records carried whole (each retaining its ten facts). No host, no process spawn, no I/O
required.

**Acceptance Scenarios**:

1. **Given** the sensed facts of a build, **When** the provenance is built, **Then** it carries the source
   commit, the base and head revisions, the rule hash, the generator version, the artifact digests, the command
   records, the environment class, and the builder identity — each readable from the value without re-sensing.
2. **Given** a build that ran several command records, **When** the provenance is built, **Then** every command
   record is carried whole (each retaining all ten of its facts, including its sensed duration), none dropped or
   flattened to an identity-only stub.
3. **Given** a build that covers several artifacts, **When** the provenance is built, **Then** the artifact
   digests are all carried, reported as a set (the same artifact supplied twice is not double-counted).
4. **Given** a build with no command records and/or no covered artifacts, **When** the provenance is built,
   **Then** it is a valid, complete value (an empty command-records list and/or empty artifact-digest set are
   ordinary values, not errors).

---

### User Story 2 - Mark the sensed metadata and project a stable canonical identity (Priority: P1)

A provenance value will appear in deterministic reports (audit.json, an attestation) and be used to recognize
"the same build." The command records it embeds carry sensed durations (F032), and any wall-clock timestamp is
sensed too; those vary run-to-run, so they must be **excluded from the provenance's canonical identity** — two
provenances for the same source built the same way (same rules, tools, runs, environment, builder) must share an
identity even though their command durations differ.

**Why this priority**: This is what lets provenance live honestly inside a *deterministic* report (the design's
repeated honesty boundary: deterministic reports must mark sensed/non-deterministic metadata) and lets the later
attestation row sign a byte-stable provenance identity and dedup builds. It is co-P1 with Story 1: a complete
provenance that mixed sensed durations into its identity could not appear deterministically.

**Independent Test**: Build two provenances that share all reproducible facts but whose embedded command records
differ only in their durations; assert they carry the **same** canonical identity, that the durations remain
reachable as explicitly-sensed metadata (via the embedded records), and that changing any reproducible fact
(e.g. a different head revision, a different rule hash, an extra artifact digest, or a command record differing
in a reproducible fact) **does** change the canonical identity.

**Acceptance Scenarios**:

1. **Given** two provenances identical in every reproducible fact whose command records differ **only** in
   duration, **When** their canonical identities are computed, **Then** the identities are equal (sensed
   durations do not affect identity).
2. **Given** two provenances that differ in any reproducible fact (source commit, base or head revision, rule
   hash, generator version, the artifact-digest set, a command record's reproducible facts, environment class,
   or builder identity), **When** their canonical identities are computed, **Then** the identities differ.
3. **Given** any provenance value, **When** it is inspected, **Then** the sensed durations of its command
   records are reachable as sensed / non-deterministic metadata, distinguishable from the reproducible facts —
   never silently folded into the provenance identity.
4. **Given** the same provenance, **When** its canonical identity is computed twice, **Then** the two identities
   are byte-for-byte equal (the identity is a stable string/value, suitable for an audit field).

---

### User Story 3 - The provenance and its identity are deterministic and pure over supplied data (Priority: P2)

The provenance-building and identity functions are consumed by audit/attestation rows and by auditors, so they
must be pure, deterministic functions of the supplied facts: identical facts always yield an identical
provenance and identical canonical identity, and the canonical identity is invariant to the *order* in which
order-insensitive collections (the artifact digests, and any set-like collection) are supplied.

**Why this priority**: Determinism and order-independence are what let provenance feed byte-stable artifacts and
reproducible audits (the same guarantee F019/F020/F029/F030/F031/F032 hold). It is essential but builds on the
provenance and identity contracts of Stories 1–2, so it is P2.

**Independent Test**: Build the provenance and compute its canonical identity twice from the same facts and
assert equality. Then reorder (and duplicate) the artifact digests supplied, rebuild, recompute the identity,
and assert the canonical identity is unchanged. Confirm that building a provenance and computing its identity
reads no clock, filesystem, git, environment, or network.

**Acceptance Scenarios**:

1. **Given** the same supplied facts, **When** the provenance is built and its canonical identity computed
   twice, **Then** both the provenance and the identity are identical (determinism).
2. **Given** the same build whose artifact digests are supplied in a different order (or with duplicate entries
   collapsed), **When** the canonical identity is recomputed, **Then** it is unchanged (the identity treats the
   artifact digests as a set).
3. **Given** the provenance is built and its identity computed in different working directories, at different
   times, and with unrelated repository/filesystem state changed between computations, **Then** the results are
   identical (purity — no clock, filesystem, git, environment, or network read).

---

### Edge Cases

- **No command records.** A build that recorded no command runs is an ordinary complete provenance with an empty
  command-records list — not an error.
- **No covered artifacts.** An empty artifact-digest set is an ordinary value; the provenance is still complete.
- **Base equals head.** A build whose base and head revisions are identical (e.g. no diff) is recorded with
  equal base/head — a valid, ordinary value, not a special case.
- **A command record that itself failed or timed out.** Carried whole exactly as F032 records it (a non-zero
  exit code / applied timeout is an ordinary recorded fact); provenance never rejects or filters a command
  record by outcome.
- **Two provenances differing only in command durations.** Share the same canonical identity (the durations are
  sensed, excluded from identity).
- **Two provenances differing only in a reproducible fact** (e.g. one extra artifact digest, a different builder
  identity, or a command record with one different argument). Have different canonical identities.
- **Order or duplication of artifact digests.** Never changes the provenance's canonical identity (deterministic,
  set discipline — the established F029 canonical-string treatment of covered artifacts).
- **Empty-string facts.** An empty source commit, builder identity, generator version, etc. are literal values
  (the F029 opaque-token discipline — no validation, no parsing), each encoding to a distinct, unambiguous
  identity segment, never colliding with absence or with another field.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **provenance** value that carries all eight declared facts of a
  build: the source commit, the base and head revisions, the rule hash, the generator version, the artifact
  digests, the command records, the environment class, and the builder identity. No declared fact may be
  dropped or represented only by omission. (The "base and head" fact is two `Revision`s, so the typed value and
  its `build` function expose **nine fields** — the plan/data-model count — for the eight declared facts.)
- **FR-002**: The system MUST carry the **command records** as the F032 `CommandRecord` value(s) verbatim —
  each retaining all ten of its facts (including its sensed duration) — never reduced to an identity-only stub
  or filtered by the command's outcome. An empty command-records collection is an ordinary value.
- **FR-003**: The system MUST carry the **artifact digests** as a set of already-computed opaque digest tokens:
  the same artifact supplied more than once MUST NOT be double-counted, and an empty set is an ordinary value.
- **FR-004**: The system MUST provide a single, pure, total **build** function that assembles a provenance value
  from the supplied sensed facts. It MUST be defined for every well-typed input (no value causes failure or
  exception): a build with no command records, no covered artifacts, equal base/head, or failed/timed-out
  embedded command records all produce ordinary complete provenance values.
- **FR-005**: The system MUST hold the **sensed / non-deterministic metadata** — the embedded command records'
  durations (and any wall-clock timestamp, **should one ever be carried**; this row carries none — see plan D3)
  — distinguishable from the provenance's reproducible facts, so a deterministic report may include the full
  provenance while flagging the sensed parts (the honesty boundary Phase-11 row 6 applies across reports).
- **FR-006**: The system MUST provide a pure, total **canonical identity** of a provenance value computed
  **only over its reproducible facts** (source commit, base/head, rule hash, generator version, the artifact
  digests, the command runs' **reproducible** identities, environment class, builder identity) and **excluding**
  the sensed command-record durations (and any wall-clock timestamp). The identity MUST be a byte-stable value
  suitable for an audit field.
- **FR-007**: Two provenances sharing all reproducible facts but whose command records differ **only** in
  duration MUST have **equal** canonical identities; two provenances differing in **any** reproducible fact
  MUST have **different** canonical identities (the identity distinguishes builds exactly by their reproducible
  facts).
- **FR-008**: The canonical identity MUST be **order-independent** over set-like collections — concretely the
  artifact digests: supplying them in a different order, or with duplicate entries collapsed, MUST NOT change
  the identity (the established F029 canonical-string discipline — entries deduped and ordered deterministically).
- **FR-009**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no
  filesystem, no git, no environment, and no network, and it MUST spawn no process and capture no bytes.
  Identical supplied facts always yield an identical provenance and identical canonical identity.
- **FR-010**: The core MUST **reuse the existing typed facts verbatim** where one maps to a declared provenance
  fact — concretely F029's `RuleHash` (rule hash), `GeneratorVersion` (generator version), `ArtifactHash`
  (artifact digests), and `Revision` (base/head, and as a candidate for source commit); F014's
  `EnvironmentClass` (environment class); and F032's `CommandRecord` plus its reproducible canonical identity
  (command records) — all without modifying F014, F029, F032, or any other merged core. It MUST introduce only
  the minimal new provenance vocabulary (e.g. builder identity, and source commit if not reused). This feature
  is additive.
- **FR-011**: The core MUST compute **no digest from raw bytes** (the digests and revisions are supplied as
  already-computed opaque tokens), perform **no sensing, no persistence, no rendering into audit.json or any
  artifact, no attestation / signing, no severity / enforcement / freshness / ship verdict**, and add **no
  CLI** surface. Its sole outputs are the provenance value and its canonical identity.
- **FR-012**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1**
  change (see Assumptions). [The concrete module home and name are a planning decision deferred to
  `/speckit-plan`.]
- **FR-013**: The core MUST NOT add a new third-party package dependency; the projection MUST use only
  facilities already available to the merged cores (the shared framework / BCL) plus the reused F014 / F029 /
  F032 vocabulary.

### Key Entities *(include if feature involves data)*

- **Provenance**: The complete typed value of one build's origin — source commit, base/head, rule hash,
  generator version, artifact digests, command records, environment class, and builder identity — with the
  embedded command records' sensed durations (and any wall-clock timestamp) marked as sensed / non-deterministic
  metadata and excluded from the canonical identity.
- **Source commit**: The resolved revision the evidence was built against. A candidate for reuse of F029's
  `Revision` newtype (the same opaque comparable revision identity as base/head), or a minimal new newtype — a
  planning detail.
- **Base / head revisions**: The two resolved revisions bounding the change — reused verbatim from F029's
  `Revision` (the local revision newtype that keeps the pure core free of the git-sensing Snapshot assembly).
- **Artifact digests**: The set of already-computed opaque digest tokens for the artifacts the evidence covers —
  reused verbatim from F029's `ArtifactHash`; compared as a set in the canonical identity (order and duplication
  ignored).
- **Command records (reused verbatim from F032)**: The runs that produced the evidence — each an F032
  `CommandRecord` carried whole, contributing its **reproducible** canonical identity (not its sensed duration)
  to the provenance identity.
- **Builder identity**: The identity of who or what produced the evidence (a CI runner, an agent, a user) — a
  minimal new opaque comparable token (the F029 opaque-token discipline), supplied by the edge.
- **Rule hash / generator version / environment class (reused verbatim)**: F029's `RuleHash` and
  `GeneratorVersion` and F014's `EnvironmentClass`, consumed verbatim and not redefined.
- **Canonical identity**: A byte-stable value derived **only** from a provenance's reproducible facts (excluding
  the sensed command-record durations and any wall-clock timestamp), order-independent over the artifact-digest
  set, used to recognize "the same build" and as a stable audit field.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For the sensed facts of a build, the built provenance carries every one of the eight declared
  facts readably and verbatim, in 100% of cases (including builds with no command records, no covered artifacts,
  equal base/head, and failed/timed-out embedded command records) — no declared fact is dropped or rejected.
- **SC-002**: Every embedded command record is carried whole (all ten of its facts retained) in 100% of cases,
  and the artifact digests are reported as a set (a duplicate artifact is never double-counted).
- **SC-003**: The embedded command records' sensed durations (and any wall-clock timestamp) are, in 100% of
  cases, reachable as sensed / non-deterministic metadata distinct from the reproducible facts, and are
  **excluded** from the canonical identity.
- **SC-004**: Two provenances differing only in their command-record durations have equal canonical identities,
  and two provenances differing in any reproducible fact have different canonical identities, in 100% of cases.
- **SC-005**: For the same supplied facts, building the provenance and computing its canonical identity twice
  yields identical results in 100% of cases (determinism); reordering or duplicating the artifact digests never
  changes the canonical identity.
- **SC-006**: The core reads no clock, filesystem, git, environment, or network and spawns no process —
  demonstrable by provenances and identities being identical when built in different working directories, at
  different times, and with unrelated repository/filesystem state changed between computations.
- **SC-007**: The merged cores (including F014, F029, and F032) and their `surface/*.surface.txt` baselines, and
  `dotnet build` / `dotnet test` over the existing projects, are **unchanged** by this feature except for the
  additive new surface — no existing baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the pure provenance core, over already-sensed facts.** Resolving the commit and base/head, hashing
  artifacts, running the commands, and reading the builder identity are impure sensing for a later host edge
  (Principle IV, the F016 git-sensing and the future command-execution precedents); the persistence of the
  provenance, its rendering into audit.json or a provenance document, attestation / signing, and any CLI are
  **later rows or a host edge** and are out of scope here. This row produces only the typed provenance value,
  its sensed/reproducible distinction, and its canonical identity.
- **Digests, revisions, versions, and the builder identity are supplied, not computed here.** They are opaque,
  already-computed comparable tokens handed in (matching F029, which treats hashes/versions/revisions as carried
  inputs, and F032, which treats output digests as supplied). This core never reads or hashes raw bytes and
  never resolves a revision; whether the source-commit token reuses F029's `Revision` or is a minimal new
  newtype is a small planning detail deferred to `/speckit-plan`.
- **The command records are the sensed-metadata carriers.** Provenance embeds whole F032 `CommandRecord` values;
  their durations are the sensed/non-deterministic facts and are excluded from the provenance identity exactly as
  F032 excludes them from a command record's identity. A wall-clock build timestamp is **not required** by this
  row (the design lists the eight facts above and no timestamp); if a timestamp is later added it is sensed
  metadata too.
- **The artifact digests are a set; the command records are a sequence carried whole.** The artifact digests are
  treated as a set (order and duplication ignored — matching F029's covered-artifacts discipline). Whether the
  command records are also identity-deduped/ordered as a set or carried as an order-preserving sequence whose
  identity contribution is canonicalized is a planning detail deferred to `/speckit-plan`; the contract is that
  each record is carried whole and contributes its **reproducible** identity to the provenance identity.
- **Reuse existing typed facts verbatim.** F029's `RuleHash` / `GeneratorVersion` / `ArtifactHash` / `Revision`,
  F014's `EnvironmentClass`, and F032's `CommandRecord` (and its `canonicalId` / `identityValue`) are reused
  where they fit; this core redefines none of them and modifies no merged core. The only genuinely new
  vocabulary is the builder identity (and source commit if not reused). Whether this core references F029 and
  F032 directly or via a thin shared dependency is a planning decision deferred to `/speckit-plan`; the
  established rhythm suggests direct references.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. Whether it lands as a new pure-core module (the established rhythm) or extends an existing core
  is the only home decision left to `/speckit-plan`; the established rhythm suggests a new minimal core.
- **Determinism is the contract, not performance.** A provenance value holds a modest number of artifact digests
  and command records; there is no latency or throughput target. Byte-stability of the provenance and its
  canonical identity, and totality of the build, are the guarantees.
- **The provenance and identity representations are planning decisions.** Whether the canonical identity is a
  string or a richer value, the exact field shapes, and whether command records are a set or an ordered sequence
  are deferred to `/speckit-plan`; the spec constrains only observable behavior (which facts are carried, the
  sensed/reproducible split, the identity rules, determinism), not representation.
