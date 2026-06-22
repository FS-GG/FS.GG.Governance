# Feature Specification: Embed Cache-Eligibility Verdicts in route.json and audit.json

**Feature Branch**: `045-cache-eligibility-embed`

**Created**: 2026-06-22

**Status**: Draft

**Change classification**: Tier 1 — alters existing public API surface on two merged libraries (`FS.GG.Governance.RouteJson` / F020 and `FS.GG.Governance.AuditJson` / F025) and changes the observable content of their committed `route.json` / `audit.json` contracts (a new per-gate cache-eligibility section + schema-version handling). Requires the full artifact chain: spec, plan, `.fsi`, updated surface-area baselines, test evidence, and docs, plus re-blessing dependent golden baselines (F028 audit.json snapshots, any committed route.json fixtures).

**Input**: User description: "next item in plan." — resolved to the explicit next row of the **route/audit cache-eligibility emission** thread (`docs/initial-implementation-plan.md`, Phase 2 / Phase 11: *"Emit deterministic route and audit JSON with selected gates … and cache eligibility …"*). The pure cores are all merged — F041 `CacheEligibility.evaluate` (the evaluated per-gate verdict), F042 `CacheEligibilityJson.ofReport` (the standalone document), F043 `FreshnessResolution.resolve` (the join), F044 `fsgg cache-eligibility` (the host command emitting the standalone sidecar). What no row has yet done is **integrate the verdict into the canonical route.json / audit.json documents** so consumers read it on the one artifact they already consume instead of a separate sidecar. Maintainer-confirmed scope (this session): **both** route.json and audit.json, continuing the pure-projection-first rhythm — the projection cores change here; the host wiring that produces a real report inside `fsgg route` / `fsgg ship` is a later row.

## Overview

F020 produced the deterministic `route.json` — the selected gates, their route trace, the carried F017
findings, and the per-tier cost rollup — and **carried each selected gate's freshness-key inputs forward
"for a later cache step" while deliberately evaluating no cache-eligibility verdict** (F020 FR-014, FR-011).
F025 produced the deterministic `audit.json` — the ship verdict, the exit-code basis, and the
blockers/warnings/passing partition with full enforcement detail — and likewise **excluded any
cache-eligibility verdict** (F025 FR-012). F041 has since produced the evaluated per-gate
`CacheEligibilityReport`, and F042/F044 emit it as a standalone `cache-eligibility.json` sidecar. This
feature is **the embed**: it relaxes those two exclusions and renders the per-gate cache-eligibility
verdict **inside** route.json and audit.json, so the cache step that F020/F025 anticipated finally
appears on the canonical artifacts.

Concretely, each projection gains a second input — an F041 `CacheEligibilityReport` — and renders, per
**gate**, that gate's evaluated verdict alongside the content it already emits, matched by `GateId`. In
route.json the verdict attaches to each selected-gate entry; in audit.json it attaches to each **gate**
item in the blockers/warnings/passing partition (findings carry no cache-eligibility verdict — cache
reuse is gate-scoped). The verdict vocabulary is F042's verbatim: a closed `reusable` token carrying the
opaque evidence reference, or a `mustRecompute` token carrying its no-hide cause (`noPriorEvidence`, or
`inputsChanged` naming exactly the changed freshness-input categories). The projections remain pure,
total, deterministic, byte-stable, and versioned; they compute no hash, no freshness key, and no cache
decision, and never dereference the opaque evidence reference.

The embed honours the thread's two hard rules verbatim. **No-hide**: every `mustRecompute` entry names
its cause; a gate that has **no** evaluated verdict (because no cache step ran, or because the upstream
resolution left it unresolved) is rendered with an explicit, legible *not-evaluated / recompute-by-default*
marker — never silently reusable, never omitted, never zero-filled. **Recompute-by-default /
necessary-not-sufficient**: a `reusable` entry asserts only "prior evidence may be reused for this gate";
embedding it changes **nothing** about the documents' existing verdict, enforcement, severity, cost, route
trace, or findings content — it adds information beside them. A profile or ship verdict can never be hidden
by a cache verdict, and a cache verdict can never relax a blocker.

Because no cache step runs inside the pure projection, and the existing `fsgg route` (F022) / `fsgg ship`
(F026) host commands do not yet resolve freshness inputs, the projection treats the report as **optional**:
when a caller supplies no report (today's route/ship commands), the document carries a present
cache-eligibility section explicitly marked *not evaluated* — distinguished from an evaluated
*must-recompute*. This keeps the existing host commands emitting valid, honest documents unchanged in every
other respect, and lets the later host row supply a real report once it resolves inputs. The host wiring
that resolves `FreshnessInputs` and runs `evaluate` inside `fsgg route` / `fsgg ship`, the real cache
store, and the standalone F042/F044 sidecar are all out of scope here.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the cache-eligibility verdict on route.json (Priority: P1)

A CI cost view, agent, or human reads a change's `route.json` and needs to see, per selected gate,
whether prior evidence may be reused (and which evidence) or the gate must recompute (and why) — on the
same artifact that already tells them which gates the change selected and why — instead of correlating a
separate sidecar by gate id.

**Why this priority**: This is the embed's reason to exist — putting the evaluated verdict where the
freshness inputs already live (F020 FR-014). With just this story, route.json becomes the single per-change
artifact carrying both routing and cache eligibility (the MVP of the embed).

**Independent Test**: Project a real upstream-assembled `RouteResult` together with a real F041
`CacheEligibilityReport` (built by `CacheEligibility.evaluate` over the same gates) and assert each
selected-gate entry in the document carries its verdict — reusable + the exact opaque evidence reference,
or must-recompute + its cause — every value tracing back to the report, with all of route.json's existing
content (gates, route trace, findings, cost) unchanged.

**Acceptance Scenarios**:

1. **Given** a `RouteResult` and a report in which a selected gate is `Reusable`, **When** route.json is
   projected, **Then** that gate's entry carries a `reusable` verdict with the exact opaque evidence
   reference from the report — and the gate's existing identity, metadata, route trace, and freshness-key
   inputs are unchanged.
2. **Given** a report in which a selected gate is `MustRecompute NoPriorEvidence`, **When** route.json is
   projected, **Then** that gate's entry carries a `mustRecompute` verdict whose cause is the
   `noPriorEvidence` token — and no evidence reference.
3. **Given** a report in which a selected gate is `MustRecompute (InputsChanged cats)`, **When** route.json
   is projected, **Then** that gate's entry carries a `mustRecompute` verdict whose cause names exactly the
   changed freshness-input categories `cats`, in the report's order — none dropped, none added.
4. **Given** a selected gate present in route.json but **absent** from the report, **When** route.json is
   projected, **Then** that gate's entry carries an explicit *not-evaluated / recompute-by-default* marker
   — never a reusable verdict, never an omitted gate.

---

### User Story 2 - Read the cache-eligibility verdict on audit.json (Priority: P1)

A reviewer, CI gate, or branch-protection check reads a change's `audit.json` and needs to see, per
**gate** in the verdict, that gate's cache-eligibility — without consulting a second artifact — while the
ship verdict, exit-code basis, and the no-hide enforcement detail on every item stay exactly as F025
renders them.

**Why this priority**: audit.json is the verdict contract CI and branch protection already read; carrying
the per-gate cache verdict there means the cost story and the ship story live on one artifact. It is
co-P1 with US1 because the maintainer scoped both projections into this row.

**Independent Test**: Project a real `ShipDecision` together with a real `CacheEligibilityReport` and
assert every **gate** item (in blockers, warnings, and passing) carries its verdict matched by `GateId`,
while every **finding** item carries none, and the verdict, exit-code basis, and six-field enforcement
detail on every item are byte-identical to the F025-only projection.

**Acceptance Scenarios**:

1. **Given** a `ShipDecision` and a report, **When** audit.json is projected, **Then** each gate item — in
   whichever of the blockers/warnings/passing sections it sits — carries its cache-eligibility verdict
   matched by `GateId`, and the item's identity, base/effective severity, mode, profile, maturity, and
   reason are unchanged.
2. **Given** a finding item (finding id + governed path), **When** audit.json is projected, **Then** that
   item carries **no** cache-eligibility verdict — the verdict is gate-scoped only.
3. **Given** a gate item present in audit.json but **absent** from the report, **When** audit.json is
   projected, **Then** that item carries the explicit *not-evaluated / recompute-by-default* marker, never
   a reusable verdict.
4. **Given** any audit.json, **When** it is inspected, **Then** the cache-eligibility verdicts never change
   the ship verdict, the exit-code basis, or any item's section or enforcement detail — they are additive
   information only.

---

### User Story 3 - The verdict never hides, never blocks, never fabricates (Priority: P1)

Whoever reads either document must never be misled by the cache verdict: every must-recompute names its
cause, a gate with no evaluated verdict is legibly marked rather than guessed, and a reusable verdict
neither relaxes a blocker nor asserts anything was actually skipped. The opaque evidence reference is
echoed verbatim and never dereferenced; no raw freshness input, hash, or computed key appears.

**Why this priority**: The no-hide and recompute-by-default guarantees are the governance invariants that
make the verdict safe to put on the canonical artifacts. An embed that silently dropped a cause, defaulted
an unevaluated gate to reusable, or let a cache verdict downgrade a blocker would be worse than no embed.
It is inseparable from US1/US2's value.

**Independent Test**: Project documents covering each verdict shape plus a not-evaluated gate; assert every
must-recompute names its cause; assert no gate is ever reusable without an explicit report entry saying so;
assert the documents contain no raw freshness inputs, no hash, no computed freshness key, no skip/severity
field tied to the cache verdict; assert the evidence reference is rendered verbatim and the document is
identical to F025/F020-only output in every non-cache field.

**Acceptance Scenarios**:

1. **Given** any `mustRecompute` entry, **When** the document is inspected, **Then** its cause is present
   and complete — `noPriorEvidence`, or the full changed-category list (never truncated, never the
   unchanged identity categories).
2. **Given** a not-evaluated gate (no report, or absent from the report), **When** the document is
   inspected, **Then** the gate is marked not-evaluated / recompute-by-default and is never marked
   reusable.
3. **Given** any document, **When** it is inspected, **Then** it carries no raw freshness inputs, no
   artifact/rule hash, no computed freshness key, and no severity/enforcement/skip field derived from the
   cache verdict; the opaque evidence reference is rendered verbatim and never resolved.
4. **Given** the same inputs, **When** either document is projected with no report supplied, **Then** the
   non-cache content is byte-identical to the F020/F025-only projection of those inputs, and the
   cache-eligibility section reads *not evaluated*.

---

### User Story 4 - A stable, versioned contract for consumers (Priority: P2)

CI pipelines, agents, and snapshot tests consume route.json / audit.json as contracts. The same inputs must
produce byte-identical documents; the schema version must let a consumer detect that the cache-eligibility
section now exists; and the per-gate cache order must be deterministic so diffs stay meaningful.

**Why this priority**: Determinism and a version signal are what keep the artifacts usable as contracts
after the shape changes; they build on US1/US2 but are separable from the verdict content itself.

**Independent Test**: Project the same inputs twice and assert byte-for-byte equality; project value-equal
inputs assembled from differently-ordered upstreams and assert identical output; assert each document
declares a schema version reflecting the new contract and that the cache-eligibility entries follow the
document's existing gate order (route.json: `GateId` ordinal; audit.json: the `ShipDecision` composite
order).

**Acceptance Scenarios**:

1. **Given** the same inputs, **When** a document is projected twice, **Then** the two documents are
   byte-for-byte identical.
2. **Given** value-equal inputs assembled from differently-ordered upstreams, **When** each is projected,
   **Then** the documents are identical, with cache-eligibility entries in the document's existing gate
   order.
3. **Given** any projected document, **When** it is inspected, **Then** it declares a schema version that
   reflects the embedded cache-eligibility contract, so a consumer can detect the change.

---

### Edge Cases

- **No report supplied** (today's `fsgg route` / `fsgg ship`): the document carries a present
  cache-eligibility section marked *not evaluated*; every non-cache field is byte-identical to the
  F020/F025-only output (FR-008, FR-012).
- **Gate selected/enforced but absent from the report** (e.g. F043 left it unresolved): the gate is marked
  not-evaluated / recompute-by-default; never reusable, never dropped (FR-005, FR-009).
- **Report entry whose `GateId` matches no gate in the document**: the orphan verdict is not emitted (the
  document renders only the gates it already lists) — the projection invents no gate (FR-006).
- **Duplicate `GateId` in the report** (F041 keeps duplicate candidate gates): the projection associates
  the verdict to the document's gate deterministically; the exact reconciliation rule (e.g. first by report
  order) is a plan decision but MUST be deterministic and total (FR-007).
- **Empty route / clean empty ship decision**: the document has an empty selected-gate (or empty
  blockers/warnings/passing) set and a present, empty cache-eligibility section — never an error (FR-010).
- **Finding-only route / finding items in audit.json**: findings carry no cache verdict; the
  cache-eligibility section concerns gates only (FR-004).
- **`reusable` verdict on a base-`Blocking` gate**: the gate still renders as a blocker with its full
  enforcement detail; the reusable verdict sits beside it and changes neither the section nor the ship
  verdict (FR-008).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The route.json projection MUST accept, in addition to the F019 `RouteResult`, an F041
  `CacheEligibilityReport`, and render per selected gate that gate's cache-eligibility verdict matched by
  `GateId`, alongside all content F020 already emits.
- **FR-002**: The audit.json projection MUST accept, in addition to the F024 `ShipDecision`, an F041
  `CacheEligibilityReport`, and render per **gate** item (in any of blockers/warnings/passing) that gate's
  cache-eligibility verdict matched by `GateId`, alongside all content F025 already emits.
- **FR-003**: Each rendered verdict MUST use F042's verbatim closed vocabulary — `reusable` carrying the
  opaque evidence reference, or `mustRecompute` carrying its cause (`noPriorEvidence`, or `inputsChanged`
  naming exactly the changed freshness-input categories in the report's order) — and MUST derive none of it.
- **FR-004**: The cache-eligibility verdict MUST be gate-scoped: **finding** items in audit.json MUST carry
  no verdict; only gates (route.json selected gates, audit.json gate items) carry one.
- **FR-005**: A gate present in the document but absent from the report MUST render with an explicit
  *not-evaluated / recompute-by-default* marker; it MUST NOT be marked reusable, and MUST NOT be omitted.
- **FR-006**: The projection MUST render verdicts only for gates the document already lists; a report entry
  whose `GateId` matches no listed gate MUST NOT add a gate, and the projection MUST invent no gate, verdict,
  or evidence reference absent from its inputs.
- **FR-007**: Both projections MUST remain deterministic and byte-stable: identical inputs MUST produce a
  byte-for-byte identical document, with cache-eligibility entries following the document's existing gate
  order (route.json: `GateId` ordinal; audit.json: the `ShipDecision`'s composite item order), and any
  duplicate-`GateId` reconciliation resolved by a deterministic, total rule.
- **FR-008**: Embedding a verdict MUST NOT change any existing field of either document — gates, route
  trace, findings, cost, ship verdict, exit-code basis, item sections, and the six-field enforcement detail
  MUST be byte-identical to the pre-embed projection of the same `RouteResult` / `ShipDecision` (modulo the
  new cache-eligibility section and schema-version field); the cache verdict is additive information and
  MUST NOT relax, hide, or alter any enforcement, severity, or ship outcome.
- **FR-009**: Every `mustRecompute` entry MUST name its cause in full (no truncation, no substitution); the
  no-hide rule applies to the cache section exactly as it applies to enforcement.
- **FR-010**: Both projections MUST remain pure and total: no file, process, clock, network, or git access;
  they MUST NOT throw for any well-typed inputs; an empty route / clean empty decision MUST be a valid
  success with a present, empty cache-eligibility section.
- **FR-011**: The projections MUST compute no freshness key, no hash, and no cache decision, MUST render no
  raw freshness inputs, and MUST NOT dereference the opaque evidence reference — they render only what the
  `CacheEligibilityReport` already carries.
- **FR-012**: When no `CacheEligibilityReport` is available to a caller, each document MUST render a present
  cache-eligibility section explicitly marked *not evaluated* (distinct from an evaluated *must-recompute*),
  with every non-cache field byte-identical to the F020/F025-only projection — so existing callers
  (`fsgg route` / `fsgg ship`) emit honest, valid documents without resolving freshness inputs.
- **FR-013**: Each document MUST declare a schema version that reflects the embedded cache-eligibility
  contract, so consumers can detect the change; the version strategy (bump vs. additive-section version) is
  a plan decision but MUST be observable.
- **FR-014**: The change MUST require no installed FS.GG package in any inspected repository and add no
  third-party runtime dependency beyond what the upstream typed values already require.
- **FR-015**: The standalone F042 `cache-eligibility.json` projection and the F044 sidecar MUST be left
  unchanged; this row edits only the F020 route.json and F025 audit.json projections.

### Key Entities *(include if feature involves data)*

- **Enriched route.json document**: F020's route.json plus, per selected-gate entry, that gate's
  cache-eligibility verdict (reusable + evidence reference, or must-recompute + cause, or not-evaluated),
  and an updated schema version. All prior sections unchanged.
- **Enriched audit.json document**: F025's audit.json plus, per **gate** item, that gate's cache-eligibility
  verdict (findings carry none), and an updated schema version. Verdict, basis, partition, and enforcement
  detail unchanged.
- **Cache-eligibility verdict (per gate)**: the F042 vocabulary — `reusable` (opaque evidence reference) |
  `mustRecompute` (`noPriorEvidence` | `inputsChanged` of changed freshness-input categories) |
  *not-evaluated* (no report entry / no report supplied).
- **CacheEligibilityReport (input)**: the F041 evaluated per-gate report, consumed verbatim and matched to
  document gates by `GateId`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a route.json projected with N selected gates and a report, 100% of selected gates carry a
  cache-eligibility verdict (reusable, must-recompute with named cause, or not-evaluated), each tracing to
  the report or to the absence rule; zero gates are reusable without a matching report entry.
- **SC-002**: For an audit.json projected with a report, 100% of **gate** items carry a verdict and 0% of
  **finding** items carry one.
- **SC-003**: Projecting the same inputs any number of times yields byte-for-byte identical documents (0%
  variance); value-equal inputs from differently-ordered upstreams project identically (0% order-dependence).
- **SC-004**: For 100% of inputs, every non-cache field of each document is byte-identical to the
  pre-embed (F020/F025-only) projection of the same `RouteResult` / `ShipDecision` (modulo the added
  cache-eligibility section and schema-version field) — the embed alters no existing field, verdict, or
  enforcement outcome.
- **SC-005**: 100% of `mustRecompute` entries name their cause in full; 100% of gates with no report entry
  render as not-evaluated and never reusable.
- **SC-006**: Both projections return a document for 100% of well-typed inputs (including empty route, clean
  empty decision, finding-only route, and no-report) and never throw; the cache-eligibility section is
  always present (empty or not-evaluated as applicable).
- **SC-007**: Every document contains zero raw freshness inputs, zero hashes, zero computed freshness keys,
  and zero severity/enforcement/skip fields derived from the cache verdict; the opaque evidence reference is
  rendered verbatim and never dereferenced.
- **SC-008**: Each document declares a schema version reflecting the embedded contract; the standalone F042
  `cache-eligibility.json` projection and F044 sidecar are unchanged (zero edits to their cores/baselines).

## Assumptions

- **Scope is the two pure projections only.** This row edits the merged F020 `RouteJson` and F025
  `AuditJson` projections to accept and render an F041 `CacheEligibilityReport`. It does not read git,
  resolve `FreshnessInputs`, run `evaluate`, load a reuse store, or change any CLI — those are the host row.
  It continues the pure-projection-first rhythm (F020/F021/F025/F042 all landed their document value before
  host wiring), now applied to the embed.
- **Both projections, this row.** The maintainer confirmed (this session) that the embed covers route.json
  **and** audit.json together, rather than route.json first with audit.json deferred.
- **The report is an optional second input.** Because the pure projection runs no cache step and the
  existing `fsgg route` (F022) / `fsgg ship` (F026) commands do not yet resolve freshness inputs, a caller
  may supply no report; the document then marks the cache-eligibility section *not evaluated*. This keeps the
  existing host commands emitting valid, unchanged-except-for-the-new-section documents until the host row
  supplies a real report. Whether "optional" is expressed as an `option` parameter, an explicit
  not-evaluated report value, or an overload is a plan decision; the spec fixes only that the not-evaluated
  state is present, legible, and distinct from must-recompute.
- **Editing merged cores + re-blessing baselines is the explicit intent.** Unlike F042/F044 (which stayed
  standalone precisely to avoid touching F020/F025), this is the row that touches them. It updates the two
  projection cores, their `.fsi` and surface baselines, and re-blesses every dependent golden baseline
  (F028 audit.json snapshots, any committed route.json fixtures, F044 expected-output tests that assert
  F020/F025 output). The non-cache content of those baselines MUST be unchanged except for the added
  cache-eligibility section and the schema-version field (SC-004). *(Resolved at plan time: there are no
  committed route.json fixtures — only gitignored `.tmp/`; and F044's tests assert no F020/F025 output
  bytes, only a surface-drift forbidden-reference check, so they need no re-bless. The
  RouteCommand/ShipCommand end-to-end tests recompute their expected route.json/audit.json live via the
  projection, so their callsites are a compile-fix to pass `None`, not a golden re-bless.)*
- **Matching is by `GateId`.** Verdicts attach to document gates by their declared `GateId`, rendered
  verbatim (no re-parsing a `GateId` to a domain). The report may carry duplicate `GateId`s (F041 keeps
  duplicate candidates) while route.json/audit.json list each gate once; the projection reconciles
  deterministically, with the exact rule settled at plan time.
- **Findings are out of cache scope.** Cache eligibility is gate-scoped (it is keyed on a gate's freshness
  inputs); audit.json finding items and route.json's findings section carry no verdict.
- **Out of scope / deferred to later rows:** the host wiring that resolves `FreshnessInputs` and runs
  `evaluate` inside `fsgg route` / `fsgg ship` (so they emit a real report instead of *not evaluated*); the
  real cache store (write/evict/expire); richer freshness sensing; the standalone F042/F044 sidecar (left
  as-is); and Phase 13 (Release & Distribution Readiness). No severity/enforcement/ship semantics change.
- **Home and serialization mechanism are plan-time decisions.** Whether each projection grows a new
  parameter, an overload, or a sibling function, and the exact JSON shape/placement of the cache-eligibility
  section and the schema-version handling, are settled in `/speckit-plan` (reusing the F020/F025
  `System.Text.Json`-or-hand-rolled mechanism, no new package); the spec fixes only the observable contract.
