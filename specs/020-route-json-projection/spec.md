# Feature Specification: Deterministic route.json Projection

**Feature Branch**: `020-route-json-projection`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved to the next
unstarted Phase-2 row in `docs/initial-implementation-plan.md`: emit a deterministic `route.json` from
the route result. Sliced (maintainer-confirmed) to the **pure projection** alone — the serialized
document value — deferring the `fsgg route`/`fsgg ship` CLI host wiring and the audit.json ship verdict
to later rows, exactly as every prior Phase-2 row landed its pure core first.

## Overview

F019 produced the typed `RouteResult` — the deterministic selected-gate set with its route trace, the
carried F017 unknown-governed-path findings, and the per-tier cost rollup — but it serializes nothing.
This feature is the **route.json projection**: the pure, total function that renders a `RouteResult`
into a deterministic, versioned `route.json` document — the stable, machine-readable contract that the
later `fsgg route` command, CI, agents, generated readiness views, and optional Governance consumers
read. It is the design's `readiness/<id>/route.json` artifact (`docs/initial-design.md`), restricted to
the fields the upstream rows have already typed.

It renders exactly what `RouteResult` carries — selected gates, the matched rules and changed paths
that selected each (the route trace), the unmatched-governed-path findings, and the rolled-up cost —
into a JSON shape with a declared schema version, deterministic field order, ordinal collection order,
and no clock, host path, or raw-YAML content. It **carries** each selected gate's declared freshness
key forward (so the later cache step has its inputs) but evaluates **no** cache-eligibility verdict,
assigns **no** severity or profile-adjusted enforcement, and emits **no** ship verdict, blockers, or
exit-code basis — those are audit.json / Phase 5 / Phase 11, consistent with how F019 carried the
freshness key without evaluating it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Render a route result to a deterministic route.json (Priority: P1)

A tool (or the later `fsgg route` command) holds a computed `RouteResult` and needs a single,
machine-readable `route.json` document that records which gates the change selected, why each was
selected, what governed paths are unclassified, and the route's cost — so CI, agents, and humans read
one stable artifact instead of an in-memory value.

**Why this priority**: This is the feature's reason to exist — turning the F019 route trace into the
durable contract every downstream consumer reads. Without it there is no route.json; with just this
story the route result becomes a shareable, inspectable artifact (the MVP).

**Independent Test**: Project a real upstream-assembled `RouteResult` (built from real F015/F017/F018
outputs over real typed facts) and assert the resulting document contains every selected gate with its
identity and route trace, the carried findings, and the cost rollup — each value tracing back to the
`RouteResult`.

**Acceptance Scenarios**:

1. **Given** a `RouteResult` with one or more selected gates, **When** it is projected, **Then** the
   document lists each selected gate by its declared `GateId` with its domain, declared cost, and the
   rest of its carried gate metadata, and no gate that was not selected appears.
2. **Given** a selected gate reached by several changed paths, **When** it is projected, **Then** that
   gate appears once, carrying every selecting path and the glob (matched rule) each won on.
3. **Given** a `RouteResult` whose selected-gate set is empty (no `Routed` path reached a gate's
   domain), **When** it is projected, **Then** a valid document is produced with an empty selected-gate
   list and the all-zero cost — never an error and never a "select everything" placeholder.

---

### User Story 2 - A stable, versioned schema for CI and agents (Priority: P1)

CI pipelines, agents, scripts, and snapshot tests consume route.json as a contract. They need the same
inputs to always produce a byte-identical document, with a declared schema version they can branch on,
and a deterministic field and collection order that makes diffs meaningful.

**Why this priority**: A non-deterministic or unversioned document is unusable as a CI/agent contract —
snapshot tests would flap and consumers could not detect schema changes. Determinism and a version
stamp are what make the artifact a *contract* rather than a dump; they are inseparable from US1's value.

**Independent Test**: Project the same `RouteResult` twice and assert byte-for-byte equality; project
two inputs that differ only in the order of candidate paths or registry gates and assert identical
output; assert the document carries a schema-version field and that all collections are in a documented
ordinal order.

**Acceptance Scenarios**:

1. **Given** the same `RouteResult`, **When** it is projected twice, **Then** the two documents are
   byte-for-byte identical.
2. **Given** two `RouteResult`s that are equal as values but were produced from differently-ordered
   inputs, **When** each is projected, **Then** the documents are identical (selected gates ordered by
   `GateId`, selecting paths by normalized path, findings in their F017 order).
3. **Given** any projected document, **When** it is inspected, **Then** it carries a declared schema
   version identifying the route.json contract, and object fields appear in a stable documented order.
4. **Given** any projected document, **When** it is inspected, **Then** it contains no wall-clock
   timestamp, host/absolute path, environment value, or raw YAML — only declared identifiers, the
   declared cost vocabulary, and the carried gate metadata.

---

### User Story 3 - Findings and freshness carried forward, enforcement excluded (Priority: P2)

A consumer reading route.json needs to see the unknown-governed-path findings alongside the selected
gates (what runs *and* what is unclassified, on one artifact), and the later cache step needs each
gate's declared freshness inputs present — but this artifact must not pretend to know severity,
profile-adjusted enforcement, cache eligibility, or a ship verdict it has not computed.

**Why this priority**: Faithful scope is what keeps the walking skeleton honest. Carrying findings and
freshness keys makes the artifact complete for what F019 produced; refusing to emit enforcement/verdict
fields avoids inventing Phase-5/Phase-11 semantics. It builds on US1 but is separable — US1's document
is viable before these carry/exclusion guarantees are pinned.

**Independent Test**: Project a `RouteResult` whose carried `FindingReport` is non-empty and assert
every finding appears unchanged; assert each selected gate's declared freshness key is present in the
document; assert no field expresses severity, profile, mode, maturity-as-enforcement, cache-eligibility
verdict, ship verdict, blockers, warnings, or an exit code.

**Acceptance Scenarios**:

1. **Given** a `RouteResult` carrying non-empty F017 findings, **When** it is projected, **Then** every
   unmatched-governed-path finding appears in the document unchanged, in its F017 order.
2. **Given** a `RouteResult` with empty findings, **When** it is projected, **Then** the findings
   section is present and empty (never omitted, never substituted with a default).
3. **Given** any selected gate, **When** the document is inspected, **Then** the gate's declared
   freshness key inputs are present (carried), but no cache-eligibility verdict is computed or emitted.
4. **Given** any projected document, **When** it is inspected, **Then** it contains no severity,
   profile, mode, enforcement, cache-eligibility verdict, ship verdict, blocker, warning, or exit-code
   field.

---

### User Story 4 - Total over any well-typed route result (Priority: P2)

The projection must succeed for every `RouteResult` the upstream rows can produce — empty, single-gate,
many-gates, findings-only — with no failure mode of its own, because its input is an already-validated
typed value.

**Why this priority**: Totality is what lets later rows call the projection unconditionally without
error handling. It is a property over US1's behavior, valuable once the document shape exists.

**Independent Test**: Property-based projection over generated well-typed `RouteResult`s asserting the
function always returns a document and never throws, including the empty route, a findings-only route
(no selected gates but non-empty findings), and a large many-gate route.

**Acceptance Scenarios**:

1. **Given** an empty `RouteResult` (no selected gates, empty findings, all-zero cost), **When** it is
   projected, **Then** a valid document with empty sections and the all-zero cost is produced — a
   success, not an error.
2. **Given** a `RouteResult` with no selected gates but non-empty findings, **When** it is projected,
   **Then** the document has an empty selected-gate list and the populated findings — both coexist.
3. **Given** any well-typed `RouteResult`, **When** it is projected, **Then** the function returns a
   document and never throws.

---

### Edge Cases

- **Empty route** — no selected gates, empty findings, all-zero cost: a valid document with empty
  sections and the zero-cost rollup, never an error or a "select everything" placeholder (FR-009).
- **Findings-only route** — unmatched governed paths exist but no gate was selected: the document
  carries the findings and an empty selected-gate list (FR-005, FR-006).
- **One gate, many selecting paths** — the gate appears once with all its selecting paths in normalized
  order, not duplicated per path (FR-004, inherited from the F019 trace).
- **Domain identifier containing the gate-id separator** (e.g. a colon in a `DomainId`): the document
  renders the declared `GateId` string verbatim; it neither re-parses nor re-derives the separator
  (FR-008, FR-010).
- **All gates in one cost tier / gates spread across tiers**: the cost rollup renders the per-tier
  counts faithfully, including zero counts for absent tiers (FR-006).
- **Cost vocabulary completeness**: every declared `Cost` tier appears in the rollup with its count
  (including zero) so the shape is stable regardless of which tiers are present (FR-006).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single projection from the F019 `RouteResult` to a `route.json`
  document, rendering the result's selected gates, route trace, carried findings, and cost rollup.
- **FR-002**: The document MUST list each selected gate by its declared `GateId` together with the
  gate's carried metadata (domain, declared cost, timeout, owner, maturity, product-check flag,
  prerequisites, description) verbatim from the route result; it MUST re-derive none of it.
- **FR-003**: The document MUST NOT include any gate that the route result did not select, and MUST NOT
  invent gates, costs, paths, or findings absent from the route result.
- **FR-004**: For each selected gate the document MUST record its route trace — every selecting changed
  path and the matched glob (rule) each won on — with the gate appearing exactly once however many
  paths reached it.
- **FR-005**: The document MUST carry the route result's F017 unknown-governed-path findings unchanged
  and in their existing order; it MUST NOT re-derive, re-sort, re-classify, or drop them. An empty
  finding set MUST render as a present, empty section.
- **FR-006**: The document MUST render the route's cost as the per-tier rollup carried by the route
  result, with every declared `Cost` tier present (including zero counts); it MUST NOT sum tiers into a
  scalar or invent weights.
- **FR-007**: The projection MUST be deterministic: identical route-result inputs MUST produce a
  byte-for-byte identical document, with selected gates in `GateId` ordinal order, each gate's selecting
  paths in normalized-path ordinal order, findings in their F017 order, and object fields in a stable
  documented order.
- **FR-008**: The projection MUST be pure and total: no file, process, clock, network, or git access;
  it MUST NOT throw for any well-typed `RouteResult`; an empty route MUST be a valid success.
- **FR-009**: An empty route (no selected gates, empty findings, all-zero cost) MUST project to a valid
  document with empty sections and the all-zero cost rollup — never an error and never a
  "select everything" fallback.
- **FR-010**: The document MUST render the declared `GateId` and path identifiers verbatim; it MUST NOT
  re-parse a `GateId` to recover a domain or re-normalize a path.
- **FR-011**: The document MUST NOT contain severity, profile, mode, maturity-as-enforcement,
  profile-adjusted enforcement, cache-eligibility verdict, ship verdict, blockers, warnings, or an
  exit-code basis. These are out of scope (audit.json / Phase 5 / Phase 11).
- **FR-012**: The document MUST NOT contain raw YAML, host/absolute paths, wall-clock timestamps, or
  any environment-derived value — only declared identifiers, the declared cost vocabulary, the carried
  gate metadata, and the carried findings.
- **FR-013**: The document MUST carry a declared schema version identifying the route.json contract, so
  consumers can detect contract changes.
- **FR-014**: The document MUST carry each selected gate's declared freshness key inputs (carried for a
  later cache step) without computing or emitting any cache-eligibility verdict.
- **FR-015**: The capability MUST require no installed FS.GG package in any inspected repository and add
  no third-party runtime dependency beyond what the upstream typed values already require.

### Key Entities *(include if feature involves data)*

- **route.json document**: The deterministic, versioned, machine-readable projection of one
  `RouteResult`. Sections: schema version, selected gates (each with identity, carried metadata, route
  trace, and freshness-key inputs), unmatched-governed-path findings, and the per-tier cost rollup.
- **Selected-gate entry**: One selected gate as rendered — its declared `GateId`, carried metadata, the
  list of selecting paths with their matched globs, and its carried freshness-key inputs.
- **Route trace (per gate)**: The selecting changed paths and the matched glob (rule) each won on — the
  "why this gate was selected" link, carried from the F019 `SelectedGate`.
- **Findings section**: The carried F017 `FindingReport` rendered unchanged.
- **Cost rollup section**: The per-tier counts of distinct selected gates, every declared tier present.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Projecting a route result with N selected gates yields a document containing exactly those
  N gates, each with its declared id, carried metadata, and complete route trace — 100% of selected
  gates represented, zero non-selected gates present.
- **SC-002**: Projecting the same route result any number of times yields byte-for-byte identical
  documents (0% variance).
- **SC-003**: Two route results equal as values but assembled from differently-ordered inputs project
  to identical documents (0% order-dependence).
- **SC-004**: 100% of carried findings appear in the document unchanged and in order; an empty finding
  set always renders as a present, empty section.
- **SC-005**: The cost section always presents every declared cost tier with its count (including zero),
  for 100% of inputs, with no summed scalar.
- **SC-006**: The projection returns a document for 100% of well-typed route results, including the
  empty route and a findings-only route, and never throws.
- **SC-007**: Every projected document carries a schema version and contains zero occurrences of
  severity, enforcement, cache-eligibility verdict, ship verdict, raw YAML, host paths, timestamps, or
  environment values.

## Assumptions

- **Scope is the pure projection only.** This feature produces the route.json *document value* from a
  `RouteResult`. It does not read git, compute a route, parse `.fsgg`, or expose a CLI command — those
  are the F016 snapshot, F015/F019 route, and the later `fsgg route`/`fsgg ship` rows. The slice was
  maintainer-confirmed before drafting, continuing the pure-core-first pattern of F014/F015/F017/F018/
  F019.
- **Input is the already-validated F019 `RouteResult`.** The projection trusts the upstream typing: it
  re-derives nothing, re-validates nothing, and has no failure mode of its own (mirrors F019's totality
  rationale).
- **"Expected artifacts," "cache eligibility," and "profile-adjusted enforcement"** named in the
  implementation-plan route.json row are **deferred**: the `Gate`/`RouteResult` carry no expected-
  artifacts field (F018), cache eligibility is a Phase-11 evaluation over the carried freshness key, and
  enforcement is Phase 5. This row emits the fields the upstream values actually carry and stops there.
- **audit.json is a separate, later row.** Ship verdict, blockers, warnings, provenance, and exit-code
  basis belong to audit.json and depend on enforcement (Phase 5); they are not emitted here.
- **The cost rollup is rendered as the F019 per-tier multiset**, not a summed scalar — preserving the
  declared `Cost` vocabulary (the F019/D5 "no invented semantics" choice). A weighted total is a
  Phase-11 concern that would declare weights first.
- **Determinism excludes any clock or environment input**: stable output depends only on the
  `RouteResult` value, so no wall-clock time or host-derived value enters the document.
- **Home and serialization mechanism are plan-time decisions.** Whether the projection lives in a new
  sibling library or alongside F019, and whether the deterministic JSON is produced by a BCL serializer
  or a hand-rolled writer, are settled in `/speckit-plan`; the spec fixes only the observable document
  contract (content, order, stability, version, exclusions).
