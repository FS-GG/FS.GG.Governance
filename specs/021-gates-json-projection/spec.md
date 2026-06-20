# Feature Specification: Deterministic gates.json Projection

**Feature Branch**: `021-gates-json-projection`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved to the next
unstarted Phase-2 row in `docs/initial-implementation-plan.md`: emit the design's `.fsgg/gates.json`
artifact — a "generated gate registry with ids, prerequisites, cost, timeout, owner, maturity, and
freshness keys". Sliced (continuing the maintainer-confirmed pure-core-first pattern of F014–F020) to
the **pure projection** alone — the serialized document value — deferring the `fsgg route`/`fsgg ship`
CLI host wiring that would persist it to disk to the later CLI rows.

## Overview

F018 produced the typed `GateRegistry` — the deterministic, `GateId`-ordered set of `Gate`s assembled
from the declared capability checks, each carrying its identity, prerequisites, cost, timeout, owner,
maturity, product-check flag, and freshness-key inputs — but it serializes nothing. This feature is the
**gates.json projection**: the pure, total function that renders a `GateRegistry` into a deterministic,
versioned `gates.json` document — the stable, machine-readable gate catalog that the later `fsgg`
commands, CI, agents, generated readiness views, and humans read to learn what gates a repository
declares, independent of any particular change. It is the design's `.fsgg/gates.json` artifact
(`docs/initial-design.md`, the data-artifacts table), restricted to the fields F018 has already typed.

It renders exactly what `GateRegistry` carries — each gate by its declared `GateId`, with its domain,
description, declared prerequisites, cost, timeout, owner, maturity, product-check flag, and
freshness-key inputs — into a JSON shape with a declared schema version, deterministic field order,
ordinal collection order, and no clock, host path, or raw-YAML content. It **carries** each gate's
declared freshness key forward (so the later cache step has its inputs) but evaluates **no**
cache-eligibility verdict, assigns **no** severity or profile-adjusted enforcement, selects **no** gate
for any change, and emits **no** ship verdict — those are route.json / audit.json / Phase 5 / Phase 11,
consistent with how F018 carried the freshness key and product-check flag without evaluating them.

Where F020's route.json is the *per-change* view (which gates a specific change selected, from a
`RouteResult`), this gates.json is the *whole-catalog* view (every gate the repository declares, from
the `GateRegistry`). They are sibling pure projections of two different upstream typed values.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Render a gate registry to a deterministic gates.json (Priority: P1)

A tool (or the later `fsgg` command) holds an assembled `GateRegistry` and needs a single,
machine-readable `gates.json` document that records every declared gate — its identity, what it
requires, its cost and timeout, who owns it, its maturity, and its freshness-key inputs — so CI, agents,
and humans read one stable catalog instead of an in-memory value.

**Why this priority**: This is the feature's reason to exist — turning the F018 registry into the
durable catalog every downstream consumer reads. Without it there is no gates.json; with just this story
the registry becomes a shareable, inspectable artifact (the MVP).

**Independent Test**: Project a real upstream-assembled `GateRegistry` (built by F018 `buildRegistry`
over real typed facts) and assert the resulting document contains every gate with its declared id and
complete carried metadata — each value tracing back to a `Gate` in the registry, none invented.

**Acceptance Scenarios**:

1. **Given** a `GateRegistry` with one or more gates, **When** it is projected, **Then** the document
   lists each gate by its declared `GateId` with its domain, description, prerequisites, declared cost,
   timeout, owner, maturity, and product-check flag, all verbatim from the registry, and no gate absent
   from the registry appears.
2. **Given** a gate carrying one or more declared prerequisites, **When** it is projected, **Then** that
   gate records each prerequisite (the required command) it carries, and a gate with no prerequisites
   records a present, empty prerequisite list.
3. **Given** a `GateRegistry` whose gate set is empty (no declared checks), **When** it is projected,
   **Then** a valid document is produced with an empty gate list — never an error and never a
   placeholder gate.

---

### User Story 2 - A stable, versioned schema for CI and agents (Priority: P1)

CI pipelines, agents, scripts, and snapshot tests consume gates.json as a contract. They need the same
registry to always produce a byte-identical document, with a declared schema version they can branch on,
and a deterministic field and collection order that makes diffs meaningful.

**Why this priority**: A non-deterministic or unversioned catalog is unusable as a CI/agent contract —
snapshot tests would flap and consumers could not detect schema changes. Determinism and a version stamp
are what make the artifact a *contract* rather than a dump; they are inseparable from US1's value.

**Independent Test**: Project the same `GateRegistry` twice and assert byte-for-byte equality; project
two registries that are equal as values but were assembled from differently-ordered declared checks and
assert identical output; assert the document carries a schema-version field and that all collections are
in a documented ordinal order.

**Acceptance Scenarios**:

1. **Given** the same `GateRegistry`, **When** it is projected twice, **Then** the two documents are
   byte-for-byte identical.
2. **Given** two `GateRegistry`s that are equal as values but were produced from differently-ordered
   declared checks, **When** each is projected, **Then** the documents are identical (gates in `GateId`
   ordinal order, each gate's prerequisites in their carried order).
3. **Given** any projected document, **When** it is inspected, **Then** it carries a declared schema
   version identifying the gates.json contract, and object fields appear in a stable documented order.
4. **Given** any projected document, **When** it is inspected, **Then** it contains no wall-clock
   timestamp, host/absolute path, environment-derived value, or raw YAML — only declared identifiers,
   the declared cost/maturity vocabulary, and the carried gate metadata.

---

### User Story 3 - Freshness keys carried forward, enforcement excluded (Priority: P2)

A consumer reading gates.json needs each gate's declared freshness inputs present (so the later cache
step has them) — but this catalog must not pretend to know severity, profile-adjusted enforcement, cache
eligibility, a selected-for-this-change flag, or a ship verdict it has not computed.

**Why this priority**: Faithful scope is what keeps the walking skeleton honest. Carrying the freshness
key and product-check flag makes the artifact complete for what F018 produced; refusing to emit
enforcement/selection/verdict fields avoids inventing Phase-5/Phase-11/route semantics. It builds on US1
but is separable — US1's document is viable before these carry/exclusion guarantees are pinned.

**Independent Test**: Project a `GateRegistry` and assert each gate's declared freshness-key inputs
(check, domain, cost, environment, optional command) are present in the document; assert the
product-check flag is carried verbatim; assert no field expresses severity, profile, mode,
maturity-as-enforcement, cache-eligibility verdict, per-change selection, ship verdict, blockers,
warnings, or an exit code.

**Acceptance Scenarios**:

1. **Given** any gate, **When** the document is inspected, **Then** the gate's declared freshness-key
   inputs are present (carried), but no cache-eligibility verdict is computed or emitted.
2. **Given** a gate whose freshness key carries an optional command, **When** it is projected, **Then**
   the command is recorded when present and rendered as an explicit absent value when not — never
   silently dropped or invented.
3. **Given** any gate, **When** the document is inspected, **Then** the product-check flag is carried
   verbatim from the registry (not re-derived from the environment).
4. **Given** any projected document, **When** it is inspected, **Then** it contains no severity,
   profile, mode, enforcement, cache-eligibility verdict, per-change gate selection, ship verdict,
   blocker, warning, or exit-code field.

---

### User Story 4 - Total over any well-typed gate registry (Priority: P2)

The projection must succeed for every `GateRegistry` F018 can produce — empty, single-gate, many-gates,
gates with and without prerequisites — with no failure mode of its own, because its input is an
already-validated typed value.

**Why this priority**: Totality is what lets later rows call the projection unconditionally without error
handling. It is a property over US1's behavior, valuable once the document shape exists.

**Independent Test**: Property-based projection over generated well-typed `GateRegistry`s asserting the
function always returns a document and never throws, including the empty registry, a single gate, and a
large many-gate registry with mixed prerequisites and freshness keys.

**Acceptance Scenarios**:

1. **Given** an empty `GateRegistry` (no gates), **When** it is projected, **Then** a valid document
   with an empty gate list is produced — a success, not an error.
2. **Given** a `GateRegistry` with gates that mix present and absent prerequisites and optional
   freshness-key commands, **When** it is projected, **Then** every gate renders faithfully with its
   own shape — no gate's optional field leaks onto another.
3. **Given** any well-typed `GateRegistry`, **When** it is projected, **Then** the function returns a
   document and never throws.

---

### Edge Cases

- **Empty registry** — no gates (no declared checks): a valid document with an empty gate list, never an
  error or a placeholder gate (FR-009).
- **Gate with no prerequisites**: the gate renders a present, empty prerequisite list, never an omitted
  field (FR-004).
- **Gate whose freshness key carries no command** (`Check.Command = None`): the command is rendered as
  an explicit absent value, distinguishable from a present command (FR-014).
- **Domain identifier containing the gate-id separator** (e.g. a colon in a `DomainId`): the document
  renders the declared `GateId` string verbatim; it neither re-parses nor re-derives the separator
  (FR-008, FR-010).
- **Gate carrying the default timeout vs. a command-derived timeout**: both render as the declared
  `TimeoutLimit` the registry carries, verbatim; the projection re-derives no timeout (FR-002, FR-006).
- **Free-text description containing JSON-significant characters**: rendered as carried, with faithful
  escaping; the round-tripped value equals the carried description (FR-002, FR-012).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single projection from the F018 `GateRegistry` to a `gates.json`
  document, rendering every gate the registry carries.
- **FR-002**: The document MUST list each gate by its declared `GateId` together with the gate's carried
  metadata (domain, description, prerequisites, declared cost, timeout, owner, maturity, product-check
  flag) verbatim from the registry; it MUST re-derive none of it.
- **FR-003**: The document MUST NOT include any gate absent from the registry, and MUST NOT invent gates,
  prerequisites, costs, timeouts, or freshness keys absent from the registry.
- **FR-004**: For each gate the document MUST record its declared prerequisites (the required commands),
  with the prerequisite list present and empty when the gate has none — never an omitted field.
- **FR-005**: The document MUST render each gate's declared `Maturity` and `Cost` verbatim from the
  registry vocabulary; it MUST NOT translate maturity into enforcement or cost into a weighted scalar.
- **FR-006**: The document MUST render each gate's carried `TimeoutLimit` verbatim; it MUST NOT compute,
  default, or re-derive a timeout (F018 already resolved the command-or-default timeout).
- **FR-007**: The projection MUST be deterministic: identical registry inputs MUST produce a
  byte-for-byte identical document, with gates in `GateId` ordinal order, each gate's prerequisites in
  their carried order, and object fields in a stable documented order.
- **FR-008**: The projection MUST be pure and total: no file, process, clock, network, or git access; it
  MUST NOT throw for any well-typed `GateRegistry`; an empty registry MUST be a valid success.
- **FR-009**: An empty registry (no gates) MUST project to a valid document with an empty gate list —
  never an error and never a placeholder gate.
- **FR-010**: The document MUST render the declared `GateId`, command, and other identifier strings
  verbatim; it MUST NOT re-parse a `GateId` to recover a domain or re-derive any carried id.
- **FR-011**: The document MUST NOT contain severity, profile, mode, maturity-as-enforcement,
  profile-adjusted enforcement, cache-eligibility verdict, per-change gate selection, route trace, ship
  verdict, blockers, warnings, or an exit-code basis. These are out of scope (route.json / audit.json /
  Phase 5 / Phase 11).
- **FR-012**: The document MUST NOT contain raw YAML, host/absolute paths, wall-clock timestamps, or any
  environment-derived value — only declared identifiers, the declared cost/maturity vocabulary, the
  carried gate metadata (including the carried freshness-key inputs), and the carried free-text
  description.
- **FR-013**: The document MUST carry a declared schema version identifying the gates.json contract, so
  consumers can detect contract changes.
- **FR-014**: The document MUST carry each gate's declared freshness-key inputs (check, domain, cost,
  environment, and optional command) without computing or emitting any cache-eligibility verdict; the
  optional command MUST be rendered as an explicit absent value when the gate carries none.
- **FR-015**: The capability MUST require no installed FS.GG package in any inspected repository and add
  no third-party runtime dependency beyond what the upstream typed values already require.

### Key Entities *(include if feature involves data)*

- **gates.json document**: The deterministic, versioned, machine-readable projection of one
  `GateRegistry`. Sections: schema version and the gate list (each gate with identity, carried metadata,
  prerequisites, and freshness-key inputs).
- **Gate entry**: One gate as rendered — its declared `GateId`, domain, description, prerequisite list,
  cost, timeout, owner, maturity, product-check flag, and carried freshness-key inputs.
- **Prerequisite (per gate)**: A declared required command the gate carries (F018 `RequiresCommand`),
  rendered as the declared command id.
- **Freshness-key inputs (per gate)**: The carried `{check, domain, cost, environment, optional command}`
  inputs — present for a later cache step, never evaluated here.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Projecting a registry with N gates yields a document containing exactly those N gates, each
  with its declared id and complete carried metadata — 100% of registry gates represented, zero invented
  gates.
- **SC-002**: Projecting the same registry any number of times yields byte-for-byte identical documents
  (0% variance).
- **SC-003**: Two registries equal as values but assembled from differently-ordered declared checks
  project to identical documents (0% order-dependence).
- **SC-004**: 100% of each gate's carried freshness-key inputs appear in the document; a gate with no
  command renders an explicit absent command, distinguishable from a present one.
- **SC-005**: Every gate's prerequisite list, cost, timeout, owner, maturity, and product-check flag
  render verbatim from the registry for 100% of inputs, with no enforcement translation and no weighted
  cost scalar.
- **SC-006**: The projection returns a document for 100% of well-typed registries, including the empty
  registry, and never throws.
- **SC-007**: Every projected document carries a schema version and contains zero occurrences of
  severity, enforcement, cache-eligibility verdict, per-change selection, route trace, ship verdict, raw
  YAML, host paths, timestamps, or environment values.

## Assumptions

- **Scope is the pure projection only.** This feature produces the gates.json *document value* from a
  `GateRegistry`. It does not read git, assemble the registry, parse `.fsgg`, select gates for a change,
  or expose a CLI command — those are the F014 facts, F018 assembly, F019/F020 route, and the later
  `fsgg` rows. The slice continues the maintainer-confirmed pure-core-first pattern of
  F014/F015/F017/F018/F019/F020.
- **Input is the already-validated F018 `GateRegistry`.** The projection trusts the upstream typing: it
  re-derives nothing, re-validates nothing, and has no failure mode of its own (mirrors F018's/F020's
  totality rationale). The registry's `GateId` ordinal order is the document's gate order — no re-sort.
- **"Cache eligibility" and "profile-adjusted enforcement"** are not gates.json concerns: cache
  eligibility is a Phase-11 evaluation over the carried freshness key, and enforcement is Phase 5. This
  row emits the fields F018 actually carries (including the carried freshness-key inputs and the
  product-check flag) and stops there.
- **Per-change selection, route trace, and ship verdict belong to other artifacts.** gates.json is the
  whole-catalog view; which gates a specific change selects is route.json (F020), and the ship
  verdict/blockers/exit-code basis is audit.json. None is emitted here.
- **Maturity and cost are carried as the declared F014 vocabulary**, not translated — a gate's
  `Maturity` is rendered verbatim (not as an enforcement decision, which is Phase 5) and its `Cost` as
  the declared tier (not a weighted scalar, which would declare weights first).
- **Determinism excludes any clock or environment input**: stable output depends only on the
  `GateRegistry` value, so no wall-clock time or host-derived value enters the document.
- **Home and serialization mechanism are plan-time decisions.** Whether the projection lives in a new
  sibling library (the F020 `FS.GG.Governance.RouteJson` precedent) or elsewhere, and whether the
  deterministic JSON is produced by a BCL writer or otherwise, are settled in `/speckit-plan`; the spec
  fixes only the observable document contract (content, order, stability, version, exclusions). The F020
  route.json projection establishes the expected shape: a new sibling `FS.GG.Governance.*` library that
  references only the upstream typed value's project and adds no third-party dependency.
