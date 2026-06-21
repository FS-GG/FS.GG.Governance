# Feature Specification: Deterministic audit.json Projection

**Feature Branch**: `025-audit-json-projection`

**Created**: 2026-06-21

**Status**: Draft

**Change classification**: Tier 1 — adds new public API surface (a new packable library `FS.GG.Governance.AuditJson` and its `AuditJson` module). Requires the full artifact chain: spec, plan, `.fsi`, surface-area baseline, test evidence, and docs (see plan.md Constitution Check).

**Input**: User description: "start the next item in the implementation plan." — resolved to the next
unstarted Phase-2 row in `docs/initial-implementation-plan.md` (the `readiness/<id>/audit.json` artifact
— line 197: "Ship verdict, blockers, warnings, provenance references, and exit-code basis"): emit a
deterministic `audit.json` from the ship decision. Sliced (continuing the maintainer-confirmed
pure-core-first pattern of F020/F021) to the **pure projection** alone — the serialized document value —
deferring the `fsgg ship` CLI host wiring, the numeric process exit code, and provenance references
(which the `ShipDecision` does not carry) to later rows.

## Overview

F024 produced the typed `ShipDecision` — the whole-change ship verdict (`Pass`/`Fail`), the deterministic
`Blockers`/`Warnings`/`Passing` partition of every enforced gate and finding (each carrying its full
F023 enforcement detail), and the typed exit-code basis (`Clean`/`Blocked`) — but it serializes nothing.
This feature is the **audit.json projection**: the pure, total function that renders a `ShipDecision`
into a deterministic, versioned `audit.json` document — the stable, machine-readable verdict contract
that the later `fsgg ship` command, CI gates, branch-protection checks, agents, and generated readiness
views read. It is the design's `readiness/<id>/audit.json` artifact (`docs/initial-design.md`,
`docs/initial-implementation-plan.md:197`), restricted to the fields the upstream rows have already
typed — exactly as F020's route.json restricted itself to what `RouteResult` carried.

It renders exactly what `ShipDecision` carries — the verdict, the three-way blockers/warnings/passing
partition, each enforced item's identity (gate id, or finding id paired with its path) and its complete
six-field enforcement detail (base severity, maturity, run mode, profile, effective severity, reason),
and the exit-code basis — into a JSON shape with a declared schema version, deterministic field order,
ordinal collection order, and no clock, host path, or raw-YAML content. It honours the design's hard
rule that a profile **must never hide the underlying verdict** (`docs/initial-design.md:575`, `:806`):
every item — including a relaxed blocker that became a warning — carries its base severity, effective
severity, mode, profile, maturity, and reason, so the document is self-explaining. It emits **no**
numeric process exit code (the `fsgg ship` host edge maps the basis to a number), invents **no**
provenance/attestation references (the `ShipDecision` carries none — they are the later Release &
provenance phase), and evaluates **no** cache/freshness verdict (Phase 11) — consistent with how F020
carried freshness keys without evaluating them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Render a ship decision to a deterministic audit.json (Priority: P1)

A tool (or the later `fsgg ship` command) holds a computed `ShipDecision` and needs a single,
machine-readable `audit.json` document that records the whole-change verdict, which items blocked, which
were relaxed to warnings, which passed, and the basis for the exit code — so CI, branch-protection
checks, agents, and humans read one stable artifact instead of an in-memory value.

**Why this priority**: This is the feature's reason to exist — turning the F024 ship decision into the
durable verdict contract every downstream consumer reads. Without it there is no audit.json; with just
this story the ship decision becomes a shareable, inspectable artifact (the MVP).

**Independent Test**: Project a real upstream-assembled `ShipDecision` (built from a real F019
`RouteResult` rolled up by F024 `Ship.rollup` at a real mode/profile) and assert the resulting document
contains the verdict, the exit-code basis, and every blocker/warning/passing item with its identity and
full enforcement detail — each value tracing back to the `ShipDecision`.

**Acceptance Scenarios**:

1. **Given** a `ShipDecision` with one or more blockers, **When** it is projected, **Then** the document
   records `verdict = fail` and `exitCodeBasis = blocked`, and lists every blocker by its identity (gate
   id, or finding id + path) with its complete enforcement detail; and no item that the decision did not
   classify as a blocker appears in the blockers section.
2. **Given** a `ShipDecision` whose blockers list is empty (verdict `Pass`), **When** it is projected,
   **Then** the document records `verdict = pass` and `exitCodeBasis = clean`, with a present, empty
   blockers section — never an error and never an invented blocker.
3. **Given** a `ShipDecision` carrying warnings and passing items alongside (or without) blockers,
   **When** it is projected, **Then** the document lists every warning and every passing item in its
   own section, each with its identity and full enforcement detail, and no item appears in more than one
   section.

---

### User Story 2 - A stable, versioned schema for CI, branch protection, and agents (Priority: P1)

CI pipelines, branch-protection checks, agents, scripts, and snapshot tests consume audit.json as a
contract. They need the same `ShipDecision` to always produce a byte-identical document, with a declared
schema version they can branch on, and a deterministic field and collection order that makes diffs
meaningful and the verdict unambiguous.

**Why this priority**: A non-deterministic or unversioned verdict document is unusable as a CI /
branch-protection contract — snapshot tests would flap and consumers could not detect schema changes.
Determinism and a version stamp are what make the artifact a *contract* rather than a dump; they are
inseparable from US1's value.

**Independent Test**: Project the same `ShipDecision` twice and assert byte-for-byte equality; project
two decisions that are equal as values but were assembled from differently-ordered upstream inputs and
assert identical output; assert the document carries a schema-version field and that all item
collections are in the `ShipDecision`'s documented ordinal order.

**Acceptance Scenarios**:

1. **Given** the same `ShipDecision`, **When** it is projected twice, **Then** the two documents are
   byte-for-byte identical.
2. **Given** two `ShipDecision`s that are equal as values but were produced from differently-ordered
   route inputs, **When** each is projected, **Then** the documents are identical (each section in the
   `ShipDecision`'s stable composite order — gates before findings, gates by id, findings by path then
   id token — preserved verbatim).
3. **Given** any projected document, **When** it is inspected, **Then** it carries a declared schema
   version identifying the audit.json contract, and object fields appear in a stable documented order.
4. **Given** any projected document, **When** it is inspected, **Then** it contains no wall-clock
   timestamp, host/absolute path, environment value, raw YAML, or numeric process exit code — only
   declared identifiers, the closed verdict/basis/severity vocabularies, and the carried enforcement
   detail.

---

### User Story 3 - The no-hide rule is visible on every item (Priority: P2)

A reviewer reading audit.json must be able to see *why* each item landed where it did — especially a
base-blocking item that a relaxed profile or low maturity turned into a warning. Every item, in every
section, must carry its base severity, effective severity, mode, profile, maturity, and reason, so a
relaxed blocker is always legible as a self-explaining warning and the document never hides the
underlying verdict.

**Why this priority**: The no-hide rule is the design's hard governance guarantee
(`docs/initial-design.md:575`, `:806`) — an audit.json that dropped the base severity or the reason
would let a profile silently suppress a blocker. Carrying the full enforcement detail on every item is
what keeps the artifact honest. It builds on US1 but is separable — US1's document is viable before this
carry guarantee is independently pinned.

**Independent Test**: Project a `ShipDecision` containing a warning that is a relaxed base-`Blocking`
item and assert the document shows, on that item, both `baseSeverity = blocking` and
`effectiveSeverity = advisory` together with the mode, profile, maturity, and a non-empty reason; assert
the same six fields are present on every blocker, warning, and passing item.

**Acceptance Scenarios**:

1. **Given** a `ShipDecision` whose warnings include a base-`Blocking` item relaxed to effective
   `Advisory`, **When** it is projected, **Then** that item's rendered detail shows the base severity
   (`blocking`) unchanged, the effective severity (`advisory`), the run mode, the profile, the maturity,
   and the non-empty reason — all six fields present.
2. **Given** any blocker, warning, or passing item, **When** the document is inspected, **Then** the
   item carries all six enforcement fields (base severity, maturity, mode, profile, effective severity,
   reason) verbatim from its F023 decision — none dropped, none re-derived.
3. **Given** a finding item, **When** it is rendered, **Then** its identity carries both the finding's
   declared id and its governed path; a gate item's identity carries the declared gate id verbatim —
   neither is re-parsed or re-derived.

---

### User Story 4 - Total over any well-typed ship decision (Priority: P2)

The projection must succeed for every `ShipDecision` the upstream rollup can produce — clean empty
route, blockers-only, warnings-only, all three sections populated — with no failure mode of its own,
because its input is an already-validated typed value.

**Why this priority**: Totality is what lets later rows call the projection unconditionally without
error handling. It is a property over US1's behavior, valuable once the document shape exists.

**Independent Test**: Property-based projection over generated well-typed `ShipDecision`s asserting the
function always returns a document and never throws, including the clean empty decision (no items, pass,
clean), a blockers-only decision, and a decision with all three sections populated.

**Acceptance Scenarios**:

1. **Given** an empty `ShipDecision` (no blockers, warnings, or passing items; verdict `Pass`; basis
   `Clean`), **When** it is projected, **Then** a valid document with three present, empty sections and
   `verdict = pass` / `exitCodeBasis = clean` is produced — a success, not an error.
2. **Given** a `ShipDecision` with blockers but no warnings (or warnings but no blockers), **When** it
   is projected, **Then** the populated and empty sections coexist, each present.
3. **Given** any well-typed `ShipDecision`, **When** it is projected, **Then** the function returns a
   document and never throws.

---

### Edge Cases

- **Empty/clean decision** — no blockers, warnings, or passing items; verdict `Pass`; basis `Clean`: a
  valid document with three present, empty sections and the pass/clean verdict, never an error or a
  "fail by default" placeholder (FR-009).
- **Blockers-only / warnings-only / passing-only decision**: every section is always present; the
  populated section carries its items and the others render as present, empty arrays (FR-005).
- **A relaxed base-`Blocking` item in the warnings section**: rendered with both `baseSeverity` and
  `effectiveSeverity` so the no-hide rule is observable; never collapsed to a single severity (FR-011).
- **The same finding id on several governed paths**: each `(id, path)` item renders as a distinct entry
  with its own path; the id is not deduplicated across paths (FR-004, inherited from F024 item identity).
- **A `GateId` or governed path string containing the id separator** (e.g. a colon): the document
  renders the declared id/path string verbatim; it neither re-parses nor re-derives it (FR-008, FR-010).
- **Verdict / basis cross-check**: the document renders the `ShipDecision`'s own `Verdict` and
  `ExitCodeBasis` verbatim; it does not recompute the verdict from the blockers list or invent a basis
  (FR-002, FR-003) — both are echoed from the already-typed value.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single projection from the F024 `ShipDecision` to an `audit.json`
  document, rendering the decision's verdict, exit-code basis, and the three-way blockers / warnings /
  passing partition with each item's identity and enforcement detail.
- **FR-002**: The document MUST render the decision's `Verdict` verbatim from the `ShipDecision` value
  (a closed `pass` / `fail` token); it MUST NOT recompute the verdict from the rendered item sections.
- **FR-003**: The document MUST render the decision's `ExitCodeBasis` verbatim (a closed `clean` /
  `blocked` token); it MUST NOT derive a numeric process exit code (that is the later `fsgg ship` host
  edge) and MUST NOT invent a basis absent from the decision.
- **FR-004**: For each enforced item the document MUST record its identity — a gate by its declared
  `GateId`, a finding by its declared `FindingId` paired with its governed path — with the same finding
  id on different paths rendered as distinct entries, and a gate appearing exactly once.
- **FR-005**: The document MUST render the three sections (blockers, warnings, passing) as the
  `ShipDecision`'s mutually-exclusive, jointly-exhaustive partition — every item in exactly one section,
  no item dropped or duplicated. Each section MUST always be present; an empty section MUST render as a
  present, empty array.
- **FR-006**: For each item the document MUST carry all six F023 enforcement fields verbatim — base
  severity, maturity, run mode, profile, effective severity, and the reason — so the no-hide rule is
  observable; it MUST re-derive, re-classify, and re-order none of them.
- **FR-007**: The projection MUST be deterministic: identical `ShipDecision` inputs MUST produce a
  byte-for-byte identical document, with each item section in the decision's documented composite order
  (gates before findings, gates by `GateId`, findings by path then finding-id token) preserved verbatim,
  and object fields in a stable documented order.
- **FR-008**: The projection MUST be pure and total: no file, process, clock, network, or git access; it
  MUST NOT throw for any well-typed `ShipDecision`; an empty/clean decision MUST be a valid success.
- **FR-009**: An empty/clean decision (no items; verdict `Pass`; basis `Clean`) MUST project to a valid
  document with three present, empty sections and the pass/clean verdict — never an error and never a
  "fail by default" fallback.
- **FR-010**: The document MUST render the declared `GateId`, `FindingId`, and governed-path identifiers
  verbatim; it MUST NOT re-parse a `GateId` to recover a domain or re-normalize a path.
- **FR-011**: The document MUST express the closed verdict, exit-code-basis, and severity vocabularies as
  stable declared tokens; a relaxed base-`Blocking` item rendered in the warnings section MUST show both
  its base and effective severity (never collapsed to one), so a profile can never hide the underlying
  verdict.
- **FR-012**: The document MUST NOT contain raw YAML, host/absolute paths, wall-clock timestamps, any
  environment-derived value, a numeric process exit code, a provenance/attestation reference, or a
  cache-eligibility verdict — only declared identifiers, the closed verdict/basis/severity vocabularies,
  and the carried enforcement detail.
- **FR-013**: The document MUST carry a declared schema version identifying the audit.json contract, so
  consumers can detect contract changes.
- **FR-014**: The capability MUST require no installed FS.GG package in any inspected repository and add
  no third-party runtime dependency beyond what the upstream typed values already require.

### Key Entities *(include if feature involves data)*

- **audit.json document**: The deterministic, versioned, machine-readable projection of one
  `ShipDecision`. Sections: schema version, the whole-change verdict, the exit-code basis, and the three
  item sections (blockers, warnings, passing).
- **Enforced-item entry**: One blocker, warning, or passing item as rendered — its identity (gate id, or
  finding id + governed path) and its six-field enforcement detail (base severity, maturity, run mode,
  profile, effective severity, reason).
- **Verdict / exit-code-basis tokens**: The closed `pass`/`fail` and `clean`/`blocked` vocabularies,
  rendered verbatim from the `ShipDecision`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Projecting a ship decision with B blockers, W warnings, and P passing items yields a
  document containing exactly those B + W + P items, each in its correct section with its declared
  identity and complete enforcement detail — 100% of items represented, zero items in a wrong or extra
  section.
- **SC-002**: Projecting the same ship decision any number of times yields byte-for-byte identical
  documents (0% variance).
- **SC-003**: Two ship decisions equal as values but assembled from differently-ordered upstream inputs
  project to identical documents (0% order-dependence).
- **SC-004**: The document renders the decision's own verdict and exit-code basis verbatim for 100% of
  inputs — `pass`/`clean` when blockers are empty, `fail`/`blocked` otherwise — with zero recomputation
  divergence from the `ShipDecision` value.
- **SC-005**: 100% of rendered items carry all six enforcement fields (base severity, maturity, mode,
  profile, effective severity, reason); every relaxed base-`Blocking` warning shows both its base and
  effective severity.
- **SC-006**: The projection returns a document for 100% of well-typed ship decisions, including the
  empty/clean decision and single-section decisions, and never throws; the three sections are always
  present (empty as an empty array).
- **SC-007**: Every projected document carries a schema version and contains zero occurrences of raw
  YAML, host paths, timestamps, environment values, a numeric exit code, a provenance reference, or a
  cache-eligibility verdict.

## Assumptions

- **Scope is the pure projection only.** This feature produces the audit.json *document value* from a
  `ShipDecision`. It does not read git, compute a route, roll up a verdict, parse `.fsgg`, set a process
  exit code, or expose a CLI command — those are the F016 snapshot, F015/F019 route, F024 rollup, and the
  later `fsgg ship` row. The slice continues the pure-core-first pattern of F020/F021 (the route.json and
  gates.json projections), which landed their document value before any host wiring.
- **Input is the already-validated F024 `ShipDecision`.** The projection trusts the upstream typing: it
  re-derives nothing, re-validates nothing, re-partitions nothing, and has no failure mode of its own
  (mirrors F020's totality rationale). The verdict and exit-code basis are echoed from the value, not
  recomputed.
- **"Provenance references"** named in the implementation-plan audit.json row (`:197`) are **deferred**:
  the F024 `ShipDecision` carries no provenance, attestation, or artifact-digest field, and inventing one
  here would fabricate Phase-Release semantics. This row emits the fields the upstream value actually
  carries — verdict, blockers, warnings, passing, exit-code basis — and stops there, exactly as F020
  deferred "expected artifacts" the `Gate`/`RouteResult` did not carry.
- **The numeric process exit code is the later `fsgg ship` row.** This document renders the typed
  exit-code *basis* (`clean`/`blocked`); translating it to a numeric process exit belongs to the host
  edge, not the pure projection (the F024/`ExitCodeBasis` design split).
- **Cache eligibility and profile dials** are out of scope: the `ShipDecision` carries the resolved
  enforcement detail already; this row renders it and evaluates no cache/freshness (Phase 11) and reads
  no `.fsgg/policy.yml`.
- **Determinism excludes any clock or environment input**: stable output depends only on the
  `ShipDecision` value, so no wall-clock time or host-derived value enters the document; each section's
  order is the decision's already-fixed composite order, preserved verbatim.
- **Home and serialization mechanism are plan-time decisions.** Whether the projection lives in a new
  sibling library (the F020 `RouteJson` / F021 `GatesJson` shape) or alongside F024, and whether the
  deterministic JSON is produced by the net10.0 shared-framework `System.Text.Json` (the F020/F021
  mechanism, no new package) or a hand-rolled writer, are settled in `/speckit-plan`; the spec fixes only
  the observable document contract (content, order, stability, version, exclusions).
