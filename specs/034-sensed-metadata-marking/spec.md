# Feature Specification: Sensed-Metadata Marking Core

**Feature Branch**: `034-sensed-metadata-marking`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. **Phase 11: Cost, Cache, and Provenance** has now landed its first five
rows as pure cores: F029 (`FS.GG.Governance.FreshnessKey`) — *"Define freshness keys…"* — F030
(`FS.GG.Governance.EvidenceReuse`) — *"Cache reusable evidence only when all freshness inputs match"* — F031
(`FS.GG.Governance.RouteExplain`) — *"Explain high-cost routes…"* — F032 (`FS.GG.Governance.CommandRecord`) —
*"Record command runs with … and duration"* — and F033 (`FS.GG.Governance.Provenance`) — *"Include source
commit, base/head, …, and builder identity in provenance."* The **next and final** unchecked Phase-11 line is
*"Mark wall-clock timestamps and durations as sensed or non-deterministic metadata when included in
deterministic reports."* Its **structural foundation already exists** — F032 holds a command run's `Duration`
as a distinct `SensedDuration` field, structurally apart from the run's reproducible facts, and F033 keeps those
embedded durations out of the provenance's canonical identity — but the *rendering* layer that actually
**surfaces** a sensed value inside a deterministic report **with an explicit "sensed / non-deterministic"
marker** is not yet built. Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F033
each landed a pure, total, deterministic core before any host edge consumed it), this row is sliced to that
single missing primitive: the typed **sensed-metadata vocabulary** and the total, deterministic functions that
mark a wall-clock timestamp or a duration as sensed metadata and render it in an unambiguously-flagged form that
a deterministic report can carry honestly. It reads **no clock**, performs **no timing**, computes **no digest**,
reads **no filesystem / git / environment / network**, persists **no artifact**, renders **no complete JSON /
audit.json / provenance / attestation document**, and adds **no CLI**.

## Overview

Governance's reports are *deterministic*: the same inputs must produce the same bytes, so the report can be
hashed, diffed, cached, and used as a stable audit field (this is the contract behind `route.json` (F020),
`gates.json` (F021), `audit.json` (F025), the freshness key (F029), and the provenance canonical identity
(F033)). But some of the facts those reports must *explain* are inherently non-deterministic: **how long a
command ran** (a duration) and **when a build happened** (a wall-clock timestamp). They vary on every run.
Dropping them would make the audit trail less honest — the phase's exit criterion is that *"audit records are
sufficient to explain builds, tests, packs, …"*, and how long things took and when they ran are part of that
explanation. Silently mixing them into the deterministic bytes would break reproducibility and let a duration
masquerade as a fact that affects identity.

The design resolves this tension with an explicit honesty rule — the last Phase-11 line: *"Mark wall-clock
timestamps and durations as sensed or non-deterministic metadata when included in deterministic reports."* That
is: a deterministic report **may** include a timestamp or a duration, but only when it is **clearly flagged as
sensed / non-deterministic**, so a reader (and any byte-stable comparison or identity) can tell it apart from the
reproducible facts and exclude it.

The **structural half** of this rule is already enforced by construction: F032 carries a command run's measured
duration in a distinct `SensedDuration` field held apart from the nine reproducible facts, and its `canonicalId`
never reads it; F033 carries those whole command records inside provenance and its `canonicalId` folds only each
record's reproducible identity, so the durations are *structurally* excluded from the provenance identity. What
is **missing** is the **presentation half**: when a report actually *surfaces* one of these values to a reader,
there is no shared, deterministic way to render it **with its sensed flag** — to say "this is `duration =
1.83s`, and it is sensed / non-deterministic; do not treat it as reproducible." Every later rendering row (a
provenance document, an `audit.json` that embeds provenance, a route report that reports per-gate timing) needs
that primitive, and it should be **one** shared, consistent marking — not re-invented per report.

Sensing these values — reading the wall clock, measuring elapsed time — is impure work that belongs at the Host
effects boundary (Principle IV), exactly like F016's git sensing and the future command-execution edge; F032
already models the measured duration as a *supplied* `SensedDuration`, not something it clocks. What **this** row
delivers, ahead of any report-rendering edge, is the **pure value and vocabulary** that marks an
already-measured timestamp or duration as sensed metadata and renders it in an unambiguously-flagged,
deterministic form:

- **Model sensed metadata as a typed, explicitly-flagged value** — a *sensed metadatum* names its **kind** (the
  two the design calls out: a **wall-clock timestamp** and a **duration**), the **label** of the field it
  annotates (e.g. the field name a report shows it under), and the already-measured value it carries. The type
  *is* the flag: every value of it is sensed by construction — there is no way to carry a timestamp or duration
  through this vocabulary *without* it being marked sensed.
- **Render it in an unambiguously-flagged, deterministic form** — a total function produces the marked rendering
  of a sensed metadatum: its label, its value, and an explicit, unmistakable "sensed / non-deterministic" marker,
  byte-stable for identical inputs, and distinguishable from how a reproducible field renders. A report can
  collect all of its sensed metadata into one clearly-marked section.
- **Guarantee the marking is purely partitioning** — a sensed metadatum contributes **nothing** to any
  reproducible/byte-stable identity. The marking does not *make* a value sensed (the structural exclusion of
  F032/F033 already does that); it makes the sensed-ness **visible and consistent** at the point a report
  surfaces it, so a report's reproducible bytes and its sensed bytes are cleanly separable.

The core is **pure over supplied data**, exactly like F019/F020/F029/F030/F031/F032/F033: the timestamp and the
duration are handed in as already-measured values; nothing is clocked, timed, spawned, hashed, or persisted. The
**actual sensing** (reading the clock, measuring elapsed time), the **complete report documents** that will
embed these flagged values (a provenance document, an `audit.json` that carries provenance, a route report with
timing — each a later row or host edge), **persistence**, **attestation / signing**, and any **CLI** remain out
of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mark a timestamp or duration as explicitly-flagged sensed metadata (Priority: P1)

A Governance component has an already-measured non-deterministic value it needs to put in front of a reader — a
command run's elapsed **duration**, or the **wall-clock timestamp** a build happened at — and is about to include
it in a deterministic report. It needs to turn that bare value into a typed *sensed metadatum* that names what
kind it is, what field it annotates, and that it is sensed — so that the value can never travel through a report
as if it were a reproducible fact.

**Why this priority**: This is the core of the design's *"Mark wall-clock timestamps and durations as sensed or
non-deterministic metadata"* row — the typed, by-construction flag is the load-bearing guarantee that the honesty
rule cannot be forgotten. Without it, each report would re-decide (and could omit) the sensed marking. It is
independently demonstrable from supplied values alone.

**Independent Test**: Take a measured `SensedDuration` and a supplied wall-clock timestamp, mark each as sensed
metadata with a label, and assert each resulting value reports its kind (duration / timestamp), its label, and
its carried value, and is — by its very type — sensed (there is no representation of a marked timestamp or
duration that is *not* sensed). No clock read, no I/O.

**Acceptance Scenarios**:

1. **Given** an already-measured command duration (an F032 `SensedDuration`), **When** it is marked as sensed
   metadata with a label, **Then** the result is a sensed metadatum of kind *duration* carrying that label and
   that duration, flagged sensed.
2. **Given** an already-measured wall-clock timestamp, **When** it is marked as sensed metadata with a label,
   **Then** the result is a sensed metadatum of kind *timestamp* carrying that label and that timestamp, flagged
   sensed.
3. **Given** any sensed metadatum, **When** it is inspected, **Then** its kind, label, and value are all
   readable, and it is sensed by construction — there is no variant of this value that is reproducible.

---

### User Story 2 - Render sensed metadata into a deterministic report with an explicit marker (Priority: P1)

A report renderer is producing a deterministic document and must surface a sensed metadatum inside it. It needs a
single, shared, deterministic way to render that metadatum **with an explicit "sensed / non-deterministic"
marker**, so a reader can tell the value apart from the reproducible fields and a byte-stable
comparison/identity can exclude it — and so every report flags sensed metadata the *same* way.

**Why this priority**: This is the **missing presentation half** the row names ("…when included in deterministic
reports") and the whole reason the row is not yet done — the structural exclusion already exists (F032/F033), but
the *flagged rendering* does not. It is co-P1 with Story 1: a marked value with no consistent flagged rendering
cannot actually appear in a deterministic report honestly.

**Independent Test**: Render a sensed duration metadatum and a sensed timestamp metadatum; assert each rendering
carries the label, the value, and an unambiguous sensed/non-deterministic marker, that the marker is visibly
distinct from how a plain reproducible field would render, and that rendering the same metadatum twice yields
byte-for-byte identical output. Collect several sensed metadata and assert they form one clearly-marked sensed
section, separable from reproducible content.

**Acceptance Scenarios**:

1. **Given** a sensed metadatum (duration or timestamp), **When** it is rendered, **Then** the output carries its
   label, its value, and an explicit, unmistakable sensed / non-deterministic marker.
2. **Given** a sensed metadatum and a reproducible field carrying a similar-looking value, **When** both are
   rendered, **Then** the sensed rendering is distinguishable from the reproducible one — a reader can always tell
   which is sensed.
3. **Given** a set of sensed metadata for one report, **When** they are rendered together, **Then** they form one
   clearly-marked sensed-metadata section that is cleanly separable from the report's reproducible bytes.
4. **Given** the same sensed metadatum, **When** it is rendered twice, **Then** the two renderings are
   byte-for-byte equal (deterministic, stable output).

---

### User Story 3 - The marking and rendering are deterministic, pure, and identity-neutral (Priority: P2)

The marking and rendering functions are consumed by report-rendering rows and read by auditors, so they must be
pure, deterministic functions of the supplied values, and a sensed metadatum must contribute **nothing** to any
reproducible/byte-stable identity — marking a value sensed must never change a report's reproducible bytes or any
canonical identity computed over them.

**Why this priority**: Determinism, purity, and identity-neutrality are what let this primitive live inside the
*deterministic* reports it serves without corrupting them (the same guarantee F020/F029/F032/F033 hold). It is
essential but builds on the marking and rendering contracts of Stories 1–2, so it is P2.

**Independent Test**: Mark and render the same value twice and assert byte-equality. Confirm that marking and
rendering read no clock, filesystem, git, environment, or network. Confirm that a sensed metadatum is
identity-neutral — that the reproducible identity / bytes a report computes over its reproducible facts are the
same whether or not the sensed metadatum is present (the sensed section is excluded from identity).

**Acceptance Scenarios**:

1. **Given** the same supplied value, **When** it is marked and rendered twice, **Then** both the marked value
   and its rendering are identical (determinism).
2. **Given** marking and rendering are performed in different working directories, at different times, and with
   unrelated repository/filesystem state changed between calls, **Then** the results are identical (purity — no
   clock, filesystem, git, environment, or network read; no timing measured here).
3. **Given** a report's reproducible identity computed over its reproducible facts, **When** a sensed metadatum is
   added to (or removed from) the report's sensed section, **Then** that reproducible identity is unchanged (the
   sensed metadata are identity-neutral / excluded — the F032/F033 honesty boundary, now at the rendering layer).

---

### Edge Cases

- **An already-measured zero-length duration.** A duration of zero is an ordinary measured value: marked sensed
  and rendered with its flag like any other — never treated as "absent."
- **A duration with a long fractional part / large magnitude, or a negative magnitude.** Carried and rendered
  verbatim from the supplied measured value (the `SensedDuration` is an opaque `int64` nanoseconds, so a negative
  value is an ordinary literal — marking and rendering are total over all of `int64`); this core neither rounds,
  re-scales, nor re-formats the magnitude beyond a deterministic, byte-stable rendering of what it was given.
- **An empty / minimal label.** An empty label is a literal value (the F029 opaque-token discipline — no
  validation, no parsing); it renders to a distinct, unambiguous form and never collides with a missing label or
  with the sensed marker itself.
- **Two sensed metadata with the same label but different kinds** (e.g. both labelled `at` — one a timestamp, one
  a duration). Each renders with its own kind and value, distinguishable; the marking neither merges nor rejects
  them.
- **No sensed metadata in a report.** An empty sensed section is an ordinary value — a deterministic report with
  nothing sensed renders with an empty (or absent) sensed section, not an error.
- **A value whose textual form contains the sensed marker's characters.** The label and value render so that no
  supplied content can masquerade as the sensed marker or bleed across a field boundary (the established F029/F032
  tagged / unambiguous-encoding discipline) — the flag is never spoofable by the data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **sensed-metadata** value that explicitly marks an already-measured
  non-deterministic value as **sensed / non-deterministic**. The type MUST carry the value's **kind** (the two
  the design names: a **wall-clock timestamp** and a **duration**), a **label** for the field it annotates, and
  the carried value. The flag MUST be intrinsic to the type — every value of it is sensed by construction, with
  **no** representation of a marked timestamp or duration that is reproducible.
- **FR-002**: The system MUST provide total constructors to **mark** (a) an already-measured **duration** and
  (b) an already-measured **wall-clock timestamp** as a sensed metadatum, each with its label. Marking MUST NOT
  read a clock or measure elapsed time — the timestamp and duration are supplied, already-measured values.
- **FR-003**: The system MUST provide a pure, total **render** function that produces the marked rendering of a
  sensed metadatum: its label, its value, and an explicit, unmistakable **sensed / non-deterministic marker**.
  The rendering MUST be byte-stable for identical inputs and **distinguishable** from how a reproducible field
  renders (a reader can always tell a sensed value from a reproducible one).
- **FR-004**: The render of a sensed metadatum MUST be **unspoofable by its data**: no supplied label or value —
  including one whose text contains the marker's characters or a field separator — may masquerade as the sensed
  marker, as another field, or as the absence of a value (the established F029/F032 tagged / unambiguous-encoding
  discipline). An empty label or a zero/empty value is an ordinary literal that renders to a distinct, unambiguous
  form.
- **FR-005**: The system MUST allow several sensed metadata to be rendered as **one clearly-marked sensed-metadata
  section** that is cleanly separable from a report's reproducible bytes, so a deterministic report can include
  the full set of sensed values while keeping them partitioned from its reproducible content.
- **FR-006**: A sensed metadatum MUST be **identity-neutral**: it contributes **nothing** to any
  reproducible/byte-stable identity. Adding or removing a sensed metadatum from a report's sensed section MUST NOT
  change any canonical identity computed over the report's reproducible facts (the F032/F033 honesty boundary,
  now at the rendering layer). This core neither computes nor alters any reproducible identity.
- **FR-007**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no
  filesystem, no git, no environment, and no network; it MUST measure no elapsed time, spawn no process, and
  capture no bytes. Identical supplied values always yield an identical marked value and an identical rendering.
- **FR-008**: The core MUST **reuse the existing typed facts verbatim** where one maps to a sensed value —
  concretely **F032's `SensedDuration`** for the duration kind — without modifying F032 or any other merged core.
  It MUST introduce only the minimal new vocabulary the row needs (a sensed wall-clock **timestamp** token, for
  which no type exists yet, and the sensed-metadatum value and its kind). This feature is additive.
- **FR-009**: The core MUST compute **no digest from raw bytes**, perform **no sensing / timing / persistence**,
  render **no complete report document** (no `audit.json`, no provenance document, no route report — only the
  marked rendering of an individual sensed metadatum and the section that groups them), perform **no attestation /
  signing**, and add **no CLI** surface. Its sole outputs are the typed sensed-metadata value(s) and their
  flagged rendering.
- **FR-010**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1**
  change (see Assumptions). [The concrete module home and name are a planning decision deferred to
  `/speckit-plan`.]
- **FR-011**: The core MUST NOT add a new third-party package dependency; the marking and rendering MUST use only
  facilities already available to the merged cores (the shared framework / BCL) plus the reused F032 vocabulary.

### Key Entities *(include if feature involves data)*

- **Sensed metadatum**: The typed, explicitly-flagged value of one already-measured non-deterministic fact —
  carrying its kind (timestamp or duration), the label of the field it annotates, and the carried value. Sensed
  by construction; identity-neutral; the unit a report surfaces with its sensed flag.
- **Sensed kind**: The closed set of non-deterministic kinds the design names — a **wall-clock timestamp** and a
  **duration**. (Whether one extensible value or two constructors is a planning detail.)
- **Sensed duration (reused verbatim from F032)**: An already-measured elapsed time, reused as F032's
  `SensedDuration` — the same opaque measured-duration token F032 holds apart from a command record's reproducible
  facts; never measured here.
- **Sensed timestamp**: An already-measured wall-clock instant — a minimal new opaque comparable token (the F029
  opaque-token discipline), supplied by the edge, never read from a clock here. (No timestamp type exists yet —
  F032 carries a duration but no timestamp, and F033 carries no timestamp at all.)
- **Sensed-metadata section / rendering**: The deterministic, byte-stable, unambiguously-flagged rendering of a
  sensed metadatum (and of a group of them as one marked section), suitable for inclusion in a deterministic
  report, distinguishable from reproducible content and excluded from any reproducible identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any already-measured duration or wall-clock timestamp, the marked sensed metadatum carries its
  kind, label, and value readably, and is sensed by construction, in 100% of cases — there is no representation of
  a marked timestamp or duration that is reproducible.
- **SC-002**: Every rendered sensed metadatum carries an explicit, unmistakable sensed / non-deterministic marker
  and is distinguishable from a reproducible field's rendering, in 100% of cases — including zero-length
  durations, empty labels, and values whose text contains the marker's characters (the marker is never spoofable
  by the data).
- **SC-003**: Adding or removing a sensed metadatum from a report's sensed section never changes any reproducible
  identity computed over the report's reproducible facts (identity-neutrality), in 100% of cases.
- **SC-004**: For the same supplied value, marking it and rendering it twice yields byte-for-byte identical
  results in 100% of cases (determinism); several sensed metadata render as one clearly-marked, separable
  sensed-metadata section.
- **SC-005**: The core reads no clock, filesystem, git, environment, or network, measures no elapsed time, and
  spawns no process — demonstrable by marked values and renderings being identical when produced in different
  working directories, at different times, and with unrelated repository/filesystem state changed between calls.
- **SC-006**: The merged cores (including F032) and their `surface/*.surface.txt` baselines, and `dotnet build` /
  `dotnet test` over the existing projects, are **unchanged** by this feature except for the additive new surface
  — no existing baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the pure sensed-metadata marking + rendering primitive, over already-measured values.** Reading the
  wall clock and measuring elapsed time are impure sensing for a later host edge (Principle IV; the F016
  git-sensing and future command-execution precedents — F032 already models the duration as a *supplied*
  `SensedDuration`); the **complete report documents** that will embed these flagged values (a provenance
  document, an `audit.json` carrying provenance, a route report with timing), their **persistence**, **attestation
  / signing**, and any **CLI** are **later rows or host edges** and are out of scope here. This row produces only
  the typed sensed-metadata value, its kind/label/value, and its unambiguously-flagged, deterministic rendering.
- **The structural half is already done; this row delivers the presentation half.** F032 holds a command run's
  measured `Duration` in a distinct `SensedDuration` field apart from its reproducible facts, and F033 excludes
  those durations from the provenance canonical identity by construction. What is missing — and what this row adds
  — is the shared, deterministic way to **surface** a timestamp or duration inside a deterministic report **with
  an explicit sensed marker**, so every later rendering row flags sensed metadata the same way.
- **The two sensed kinds are exactly those the design names: a wall-clock timestamp and a duration.** No other
  non-deterministic kind is in scope. Whether the kind is modeled as one extensible value or two constructors is a
  planning detail deferred to `/speckit-plan`.
- **Reuse existing typed facts verbatim; introduce only the minimal new vocabulary.** F032's `SensedDuration` is
  reused verbatim for the duration kind; a minimal opaque **sensed timestamp** token is introduced because none
  exists (F032 carries a duration but no timestamp; F033 carries no timestamp). Whether this core references F032
  directly or carries the duration via a thin local alias is a planning decision deferred to `/speckit-plan`; the
  established rhythm suggests a direct reference. This core redefines none of the merged vocabulary and modifies
  no merged core.
- **The marked rendering's exact format is a planning decision; only its observable contract is fixed here.** The
  spec fixes that the rendering is deterministic, byte-stable, carries label + value + an unambiguous sensed
  marker, is distinguishable from reproducible fields, is unspoofable by its data, and is identity-neutral. The
  concrete marker text, separator scheme, and whether the rendering is a string or a richer value are deferred to
  `/speckit-plan` — consistent with the F029/F032/F033 tagged/unambiguous-encoding discipline this core should
  follow.
- **This core does not compute or own any reproducible identity.** It guarantees only that sensed metadata are
  identity-neutral (excluded from whatever reproducible identity a report computes). The report's reproducible
  bytes and identity remain the concern of that report's own rendering row (F020/F021/F025 today, and the future
  provenance/audit rendering); this core supplies only the *sensed* partition.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. Whether it lands as a new pure-core module (the established rhythm) or extends an existing core is
  the only home decision left to `/speckit-plan`; the established rhythm suggests a new minimal core.
- **Determinism is the contract, not performance.** A report holds a modest number of sensed metadata; there is
  no latency or throughput target. Byte-stability of the marked value and its rendering, totality of marking and
  rendering, and identity-neutrality are the guarantees.
- **This row closes Phase 11.** It is the sixth and final line of *Phase 11: Cost, Cache, and Provenance*; with
  it merged, the phase's exit criteria (expensive evidence reused only when freshness is defensible; routes
  explain cost and cheaper alternatives; audit records sufficient to explain builds — including *how long* and
  *when*, honestly flagged) are met.
