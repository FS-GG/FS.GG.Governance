# Feature Specification: Command-Record Core

**Feature Branch**: `032-command-records`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 11: Cost, Cache, and Provenance** has landed its first three
rows as pure cores: F029 (`FS.GG.Governance.FreshnessKey`) — *"Define freshness keys…"* — F030
(`FS.GG.Governance.EvidenceReuse`) — *"Cache reusable evidence only when all freshness inputs match"* — and
F031 (`FS.GG.Governance.RouteExplain`) — *"Explain high-cost routes…"* The **next** unchecked Phase-11 line is
*"Record command runs with executable, arguments, working directory, environment delta, timeout, exit code,
stdout digest, stderr digest, captured output path, and duration."* Continuing this repo's maintainer-confirmed
**pure-core-first** rhythm (F015–F031 each landed a pure, total, deterministic core before any host edge
consumed it), this row is sliced to that single projection: the typed **command-record vocabulary** and the
total, deterministic functions that build a complete record from already-sensed command facts, distinguish its
**deterministic** facts from its **sensed / non-deterministic** metadata, and project a byte-stable **canonical
identity** over the reproducible facts. It performs **no command execution** (it spawns no process and captures
no bytes), reads **no clock / filesystem / git / environment / network**, computes **no digest from raw bytes**
(digests are supplied), persists **no artifact**, builds **no provenance / attestation**, and adds **no CLI**.

## Overview

When Governance runs a gate's command — a build, a test, a pack, a template instantiation, a git diff, a
package inspection, a visual capture — the audit trail must be able to *explain what actually ran*. The design
states this directly: a command record carries the *"executable, arguments, working directory, environment
delta, timeout, exit code, stdout digest, stderr digest, captured output path, and duration,"* and the phase's
exit criterion is that *"audit records are sufficient to explain builds, tests, packs, template instantiation,
git diffs, package inspection, and visual capture."* A command record is also the unit later rows fold into
**provenance** (Phase-11 row 5 lists *"command records"* among provenance inputs) and that the audit phase
references in `audit.json`.

Recording the *run itself* — spawning the process, capturing stdout/stderr, measuring the wall-clock duration,
hashing the captured bytes — is impure sensing that belongs at the Host effects boundary (Principle IV), exactly
like F016's git sensing. What this row delivers, ahead of that edge, is the **pure value and vocabulary** the
edge will populate and every downstream report will read: a typed, complete, total **command record** assembled
from already-sensed facts, with its sensed/non-deterministic metadata (the duration) **marked as such** so a
deterministic report can carry it honestly, and a **canonical identity** over the run's reproducible facts that
is stable across re-runs (so two runs of the same command in the same context share an identity regardless of
how long each took).

This row answers, deterministically: *"Given the facts of a command run, what is the complete, typed record of
it; which of its facts are reproducible versus sensed; and what is its stable canonical identity?"*

This row delivers that as a pure core that reuses the existing typed facts verbatim where they exist (e.g.
F014's `CommandId` / `EnvironmentClass` / digest-bearing newtypes, F029's digest/`Revision`-style opaque
tokens) and otherwise introduces only the minimal new command-record vocabulary:

- **Model a command run as a complete, typed value** — one *command record* carries every declared fact:
  the executable, the ordered arguments, the working directory, the environment delta (the variables added,
  changed, and removed relative to a baseline), the timeout, the exit code, the stdout digest, the stderr
  digest, the captured-output path, and the duration. No declared fact is dropped, stringly-typed away, or made
  optional-by-omission.
- **Distinguish deterministic facts from sensed metadata** — the duration (and any wall-clock timestamp, if
  carried) is **sensed / non-deterministic** and is marked as such; every other fact is reproducible from the
  command and its context. A deterministic report may therefore include the full record while clearly flagging
  the sensed parts (the contract Phase-11 row 6 will apply across all reports).
- **Project a deterministic canonical identity** — a byte-stable identity computed **only over the
  reproducible facts** (executable, arguments, working directory, environment delta, timeout, exit code, stdout
  digest, stderr digest, captured-output path), excluding the sensed duration, so the same command run in the
  same context yields the same identity on every recording.

The core is **pure over supplied data**, exactly like F019/F020/F029/F030/F031: every fact — including the
already-computed stdout/stderr digests and the measured duration — is handed in as a value; nothing is spawned,
captured, hashed-from-bytes, clocked, or persisted. The **actual command execution and sensing** (running the
process, capturing output bytes, hashing them, timing the run), the **persistence** of the record, its
**rendering** into any artifact (audit.json or a command-records log), **provenance / attestation** assembly,
and any **CLI** are a later Phase-11 row or a host edge and remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capture a command run as one complete, typed record (Priority: P1)

A Governance host has just run a gate's command and sensed its facts (exit code, captured digests, duration,
etc.). It needs to assemble those facts into a single typed record that carries **all ten** declared fields, so
nothing about *what ran* is lost or left untyped before the record flows into audit and provenance.

**Why this priority**: This is the core of the design's *"record command runs…"* row and the phase exit
criterion (*"audit records are sufficient to explain builds, tests, packs…"*). A record missing any declared
fact cannot explain a run; assembling the complete typed value is the load-bearing guarantee and is
independently demonstrable.

**Independent Test**: Supply the ten sensed facts of a command run and build the record; assert the record
carries each fact verbatim (executable, ordered arguments, working directory, environment delta, timeout, exit
code, stdout digest, stderr digest, captured-output path, duration), with the arguments in order and the
environment delta partitioned into added / changed / removed. No host, no process spawn, no I/O required.

**Acceptance Scenarios**:

1. **Given** the sensed facts of a successful command run, **When** the record is built, **Then** it carries
   the executable, the arguments in their given order, the working directory, the environment delta, the
   timeout, the exit code, the stdout digest, the stderr digest, the captured-output path, and the duration —
   each readable from the record without re-sensing.
2. **Given** a command run whose execution changed, added, and removed environment variables relative to its
   baseline, **When** the record is built, **Then** its environment delta reports those three classes
   distinctly (a changed variable is not reported as both an add and a remove).
3. **Given** a command run that exited non-zero, **When** the record is built, **Then** the record is still a
   complete, ordinary value carrying the non-zero exit code (a failed command is recorded, not rejected).
4. **Given** a command run with no arguments and an empty environment delta, **When** the record is built,
   **Then** it is a valid, complete record (an empty argument list and empty delta are ordinary values, not
   errors).

---

### User Story 2 - Mark the sensed metadata and project a stable canonical identity (Priority: P1)

A command record will appear in deterministic reports (audit.json, provenance) and be used to recognize "the
same run." The duration (and any wall-clock timestamp) is sensed and varies run-to-run, so it must be **marked
as sensed / non-deterministic** and must be **excluded from the record's canonical identity** — two runs of the
same command in the same context must share an identity even though their durations differ.

**Why this priority**: This is what lets a command record live honestly inside a *deterministic* report (the
design's repeated honesty boundary: deterministic reports must mark sensed/non-deterministic metadata) and lets
later rows fold records into byte-stable provenance and dedup them. It is co-P1 with Story 1: a complete record
that mixed sensed duration into its identity could not appear deterministically.

**Independent Test**: Build two records that share all reproducible facts but differ only in duration; assert
they carry the **same** canonical identity, that the duration is reachable as explicitly-sensed metadata
(distinct from the reproducible facts), and that changing any reproducible fact (e.g. a different argument or a
different stdout digest) **does** change the canonical identity.

**Acceptance Scenarios**:

1. **Given** two records identical in every reproducible fact but differing only in duration, **When** their
   canonical identities are computed, **Then** the identities are equal (duration does not affect identity).
2. **Given** two records that differ in any reproducible fact (executable, an argument, working directory,
   environment delta, timeout, exit code, stdout digest, stderr digest, or captured-output path), **When** their
   canonical identities are computed, **Then** the identities differ.
3. **Given** any command record, **When** it is inspected, **Then** its duration is reachable as
   sensed / non-deterministic metadata, distinguishable from the reproducible facts — never silently folded in
   as if it were reproducible.
4. **Given** the same record, **When** its canonical identity is computed twice, **Then** the two identities are
   byte-for-byte equal (the identity is a stable string/value, suitable for an audit field).

---

### User Story 3 - The record and its identity are deterministic and pure over supplied data (Priority: P2)

The record-building and identity functions are consumed by audit/provenance rows and by auditors, so they must
be pure, deterministic functions of the supplied facts: identical facts always yield an identical record and
identical canonical identity, and the canonical identity is invariant to the *order* in which environment-delta
entries are supplied.

**Why this priority**: Determinism and order-independence are what let the record feed byte-stable artifacts and
reproducible audits (the same guarantee F019/F020/F029/F030/F031 hold). It is essential but builds on the
record and identity contracts of Stories 1–2, so it is P2.

**Independent Test**: Build the record and compute its canonical identity twice from the same facts and assert
equality. Then reorder (and duplicate) the environment-delta entries supplied, rebuild, recompute the identity,
and assert the canonical identity is unchanged. Confirm that building a record and computing its identity reads
no clock, filesystem, git, environment, or network.

**Acceptance Scenarios**:

1. **Given** the same supplied facts, **When** the record is built and its canonical identity computed twice,
   **Then** both the record and the identity are identical (determinism).
2. **Given** the same run whose environment-delta entries are supplied in a different order (or with duplicate
   entries collapsed), **When** the canonical identity is recomputed, **Then** it is unchanged (the identity is
   order-independent over the environment delta).
3. **Given** the record is built and its identity computed in different working directories, at different times,
   and with unrelated repository/filesystem state changed between computations, **Then** the results are
   identical (purity — no clock, filesystem, git, environment, or network read).

---

### Edge Cases

- **Non-zero / failed command.** Recorded as an ordinary complete record carrying the non-zero exit code; a
  failure is auditable, never dropped.
- **Timed-out command.** The timeout is a declared fact of the record; a run that hit its timeout is recorded
  with its exit code/outcome and its timeout value — the record models *that the timeout applied*, it does not
  enforce or measure timeouts.
- **No arguments / empty environment delta.** Ordinary values — a complete record with an empty argument list
  and/or empty delta, not an error.
- **Environment delta with a variable that is changed (not added or removed).** Reported in the *changed* class
  only, carrying enough to distinguish it from an add/remove; a changed variable is never double-counted as a
  removal plus an addition.
- **Two runs differing only in duration.** Share the same canonical identity (duration is sensed, excluded from
  identity).
- **Two runs differing only in a reproducible fact** (e.g. one extra argument, or a different stdout digest).
  Have different canonical identities.
- **Order or duplication of environment-delta entries.** Never changes the record's canonical identity
  (deterministic, order-independent — the established F029 canonical-string discipline).
- **Captured-output path is absent / a command produced no captured-output file.** Modeled as an ordinary
  value (an explicit "no captured-output path"), not a silent empty string that collides with a real path —
  the absence is representable and total.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **command record** that carries all ten declared facts of a single
  command run: the executable, the ordered arguments, the working directory, the environment delta, the timeout,
  the exit code, the stdout digest, the stderr digest, the captured-output path, and the duration. No declared
  fact may be dropped or represented only by omission.
- **FR-002**: The system MUST model the **environment delta** as a typed value distinguishing variables
  **added**, **changed**, and **removed** relative to the run's baseline environment. A changed variable MUST be
  reported once, in the changed class, and never double-counted as a removal-plus-addition.
- **FR-003**: The system MUST provide a single, pure, total **build** function that assembles a command record
  from the supplied sensed facts. It MUST be defined for every well-typed input (no value causes failure or
  exception): a failed, timed-out, argument-less, or empty-delta run all produce ordinary complete records.
- **FR-004**: The system MUST mark the **duration** (and any wall-clock timestamp, if carried) as
  **sensed / non-deterministic metadata**, distinguishable from the record's reproducible facts, so a
  deterministic report may include the full record while flagging the sensed parts (the honesty boundary
  Phase-11 row 6 applies across reports).
- **FR-005**: The system MUST provide a pure, total **canonical identity** of a command record computed **only
  over its reproducible facts** (executable, arguments, working directory, environment delta, timeout, exit
  code, stdout digest, stderr digest, captured-output path) and **excluding** the sensed duration. The identity
  MUST be a byte-stable value suitable for an audit field.
- **FR-006**: Two records sharing all reproducible facts but differing **only** in duration MUST have **equal**
  canonical identities; two records differing in **any** reproducible fact MUST have **different** canonical
  identities (the identity distinguishes runs exactly by their reproducible facts).
- **FR-007**: The canonical identity MUST be **order-independent** over the environment delta: supplying the
  delta's entries in a different order, or with duplicate entries collapsed, MUST NOT change the identity (the
  established F029 canonical-string discipline — entries deduped and ordered deterministically).
- **FR-008**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no
  filesystem, no git, no environment, and no network, and it MUST spawn no process and capture no bytes.
  Identical supplied facts always yield an identical record and identical canonical identity.
- **FR-009**: The core MUST **reuse the existing typed facts verbatim** where one maps to a declared run fact
  — concretely F014's `TimeoutLimit` for the *timeout* (F014's `CommandId` / `EnvironmentClass` map to none of
  the ten facts and are therefore not reused) — and MUST apply the repo's digest/`Revision`-style opaque-token
  discipline (without referencing F029's own digest type) for the supplied stdout/stderr digests, all without
  modifying F014, F029, or any other merged core. It MUST introduce only the minimal new command-record
  vocabulary. This feature is additive.
- **FR-010**: The core MUST compute **no digest from raw bytes** (the stdout/stderr digests are supplied as
  already-computed opaque tokens), perform **no command execution, no timing, no persistence, no rendering into
  audit.json or any artifact, no provenance / attestation assembly, no severity / enforcement / freshness /
  ship verdict**, and add **no CLI** surface. Its sole outputs are the command-record value and its canonical
  identity.
- **FR-011**: The core MUST represent an **absent captured-output path** as an explicit, total value (a
  distinct "no captured-output path"), never an empty string that could collide with a real path; absence MUST
  be locatable and MUST participate in the canonical identity unambiguously.
- **FR-012**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1**
  change (see Assumptions). [The concrete module home and name are a planning decision deferred to
  `/speckit-plan`.]
- **FR-013**: The core MUST NOT add a new third-party package dependency; the projection MUST use only
  facilities already available to the merged cores (the shared framework / BCL) plus the reused F014 / F029
  vocabulary.

### Key Entities *(include if feature involves data)*

- **Command record**: The complete typed value of one command run — executable, ordered arguments, working
  directory, environment delta, timeout, exit code, stdout digest, stderr digest, captured-output path, and
  duration — with the duration marked as sensed / non-deterministic metadata.
- **Environment delta**: A typed value partitioning the run's environment changes relative to a baseline into
  *added*, *changed*, and *removed* variables; a changed variable appears once, in the changed class.
- **Captured-output path outcome**: For a record, either a concrete captured-output path or an explicit "no
  captured-output path" — a total, locatable representation of absence (never an ambiguous empty string).
- **Canonical identity**: A byte-stable value derived **only** from a record's reproducible facts (excluding
  the sensed duration), order-independent over the environment delta, used to recognize "the same run" and as a
  stable audit field.
- **Timeout (reused verbatim from F014)**: The run's timeout fact is the F014 `TimeoutLimit` newtype,
  consumed verbatim and not redefined. (F014's `CommandId` / `EnvironmentClass` were evaluated and do **not**
  map to any of the ten declared facts — the *executable* is the actual program string, the *environment
  delta* is concrete variable changes — so they are not reused; see plan D1 / data-model.)
- **Digest tokens (opaque, supplied)**: The stdout / stderr digests are opaque, already-computed tokens handed
  in. They apply the F029 opaque-token discipline **locally** (a minimal `OutputDigest` newtype) rather than
  referencing F029's own digest type — mirroring how F029 introduced its own `Revision` (plan D3). No bytes
  are hashed here.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For the sensed facts of a command run, the built record carries every one of the ten declared
  facts readably and verbatim, in 100% of cases (including failed, timed-out, argument-less, and empty-delta
  runs) — no declared fact is dropped or rejected.
- **SC-002**: The environment delta reports added, changed, and removed variables as distinct classes in 100%
  of cases; a changed variable is reported exactly once and never as a removal-plus-addition.
- **SC-003**: The duration is, in 100% of cases, reachable as sensed / non-deterministic metadata distinct from
  the reproducible facts, and is **excluded** from the canonical identity.
- **SC-004**: Two records differing only in duration have equal canonical identities, and two records differing
  in any reproducible fact have different canonical identities, in 100% of cases.
- **SC-005**: For the same supplied facts, building the record and computing its canonical identity twice yields
  identical results in 100% of cases (determinism); reordering or duplicating the environment-delta entries
  never changes the canonical identity.
- **SC-006**: The core reads no clock, filesystem, git, environment, or network and spawns no process —
  demonstrable by records and identities being identical when built in different working directories, at
  different times, and with unrelated repository/filesystem state changed between computations.
- **SC-007**: The merged cores (including F014 and F029) and their `surface/*.surface.txt` baselines, and
  `dotnet build` / `dotnet test` over the existing projects, are **unchanged** by this feature except for the
  additive new surface (if any) — no existing baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the pure command-record core, over already-sensed facts.** Running the command, capturing
  stdout/stderr, measuring duration, and hashing captured bytes are impure sensing for a later host edge
  (Principle IV, the F016 git-sensing precedent); the persistence of the record, its rendering into audit.json
  or a command-records log, provenance / attestation assembly (Phase-11 row 5), wall-clock-metadata policy
  across all reports (Phase-11 row 6), and any CLI are **later rows or a host edge** and are out of scope here.
  This row produces only the typed command-record value, its sensed/deterministic distinction, and its
  canonical identity.
- **Digests are supplied, not computed here.** The stdout and stderr digests are opaque, already-computed tokens
  handed in (matching F029, which treats `output digest` / hashes as carried inputs). This core never reads or
  hashes raw output bytes; whether the digest token type is reused from F029 or introduced minimally is a small
  planning detail deferred to `/speckit-plan`.
- **The duration is the sensed/non-deterministic field.** It is carried as a fact of the run but flagged as
  sensed and excluded from the canonical identity. A wall-clock start/finish timestamp is **not required** by
  this row (the design lists "duration"); if a timestamp is later added it is sensed metadata too. Whether
  duration is modeled as a typed span or an opaque measure is a planning detail deferred to `/speckit-plan`.
- **The environment delta is a partition, not a full environment snapshot.** Recording the *delta* (added /
  changed / removed relative to a baseline), not the entire process environment, matches the design's wording
  ("environment delta") and avoids capturing unrelated/secret environment state. How a "changed" variable
  records old-vs-new (e.g. just the new value, or both) is a planning detail deferred to `/speckit-plan`; the
  contract is the three distinct classes and single-counting of a change.
- **Reuse existing typed facts verbatim.** `CommandId` and `EnvironmentClass` (F014) and the digest/`Revision`
  opaque-token discipline (F029) are reused where they fit; this core redefines none of them and modifies no
  merged core. Whether the executable/working-directory/captured-output path are modeled as new newtypes or
  plain typed strings is a planning decision deferred to `/speckit-plan`.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. Whether it lands as a new pure-core module (the established rhythm) or extends an existing core
  is the only home decision left to `/speckit-plan`; the established rhythm suggests a new minimal core.
- **Determinism is the contract, not performance.** A command record holds a modest number of arguments and
  environment-delta entries; there is no latency or throughput target. Byte-stability of the record and its
  canonical identity, and totality of the build, are the guarantees.
- **The record, delta, and identity representations are planning decisions.** Whether the captured-output-path
  outcome is an option or a small closed union, whether the canonical identity is a string or a richer value,
  and the exact shape of the environment delta are deferred to `/speckit-plan`; the spec constrains only
  observable behavior (which facts are carried, the sensed/deterministic split, the identity rules,
  determinism), not representation.
