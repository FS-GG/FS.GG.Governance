# Feature Specification: Deterministic cache-eligibility.json Projection

**Feature Branch**: `042-cache-eligibility-json`

**Created**: 2026-06-22

**Status**: Draft

**Change classification**: Tier 1 — adds new public API surface (a new packable library `FS.GG.Governance.CacheEligibilityJson` and its `CacheEligibilityJson` module). Requires the full artifact chain: spec, plan, `.fsi`, surface-area baseline, test evidence, and docs (see plan.md Constitution Check).

**Input**: User description: "start the next item in the implementation plan." — resolved to the next unstarted Governance-owned row of the **route/audit cache-eligibility emission** thread in `docs/initial-implementation-plan.md` (Phase 2 / Phase 11: *"Emit deterministic route and audit JSON with selected gates … and cache eligibility …"*). F041 landed the pure `CacheEligibilityReport` core (the evaluated per-gate verdict); this row continues the maintainer-confirmed pure-core-first rhythm of F020/F021/F025 by adding the **pure projection** that renders that report into a deterministic document — the serialized value only. The integration of the verdict into route.json / audit.json, the host wiring that resolves each gate's `FreshnessInputs`, and any real cache store stay deferred to later rows.

## Overview

F041 produced the typed `CacheEligibilityReport` — for the gates a routed change selected, one evaluated, gate-attributed cache-eligibility verdict each: `Reusable` (naming the reusable evidence reference) or `MustRecompute` (naming the cause — no prior evidence, or exactly the changed freshness-input categories) — one entry per selected gate, every gate preserved, in deterministic `GateId`-ordinal order. But it serializes nothing.

This feature is the **cache-eligibility.json projection**: the pure, total function that renders a `CacheEligibilityReport` into a deterministic, versioned `cache-eligibility.json` document — the stable, machine-readable per-change cache-eligibility contract that the later route/audit emission rows, CI cost dashboards, agents, and generated readiness views read instead of an in-memory value. It is the missing serialized half of the design's *"… and cache eligibility …"* emission, restricted to the fields the upstream report already typed — exactly as F020's route.json restricted itself to what `RouteResult` carried, and F025's audit.json to what `ShipDecision` carried.

It renders exactly what `CacheEligibilityReport` carries — the per-gate entries, each with its declared `GateId` and its two-outcome verdict (`reusable` carrying the opaque evidence reference, or `mustRecompute` carrying its cause: a `noPriorEvidence` token, or the named list of changed freshness-input categories) — into a JSON shape with a declared schema version, deterministic field order, and the report's already-fixed `GateId`-ordinal collection order preserved verbatim, with no clock, host path, raw freshness inputs, hash, or product vocabulary.

It honours F041's two hard rules. **No-hide**: every `mustRecompute` entry names its cause — `noPriorEvidence`, or the exact changed categories (never truncated, never the identity categories) — so the document always says *why* a gate must recompute. **Recompute-by-default / necessary-not-sufficient**: a `reusable` entry asserts only "prior evidence may be reused for this gate"; the document carries no skip action, severity, ship verdict, or exit-code basis, evaluates no cache against a real store, computes no freshness key or hash, resolves none of the inputs, and never dereferences the opaque evidence reference. It maps no numeric process exit code and invents no provenance reference.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Render a cache-eligibility report to a deterministic cache-eligibility.json (Priority: P1)

A tool (or a later route/audit emission row) holds a computed `CacheEligibilityReport` and needs a single, machine-readable `cache-eligibility.json` document that records, per selected gate, whether prior evidence may be reused (and which evidence) or the gate must recompute (and why) — so CI cost views, agents, and humans read one stable artifact instead of an in-memory value.

**Why this priority**: This is the feature's reason to exist — turning the F041 report into a durable, shareable cache-eligibility contract. Without it the evaluated verdict is never emitted; with just this story the report becomes a shareable, inspectable artifact (the MVP).

**Independent Test**: Project a real upstream-assembled `CacheEligibilityReport` (built by F041 `CacheEligibility.evaluate` over real candidate gates and a real F030 `ReuseStore`) and assert the resulting document contains exactly one entry per gate, each with its declared gate id and its verdict (reusable + evidence reference, or must-recompute + cause), every value tracing back to the report.

**Acceptance Scenarios**:

1. **Given** a report containing a `Reusable` entry, **When** it is projected, **Then** the document records that gate with a `reusable` verdict carrying the exact opaque evidence reference from the report — and no `mustRecompute` cause for it.
2. **Given** a report containing a `MustRecompute NoPriorEvidence` entry, **When** it is projected, **Then** the document records that gate with a `mustRecompute` verdict whose cause is the `noPriorEvidence` token — and no evidence reference.
3. **Given** a report containing a `MustRecompute (InputsChanged cats)` entry, **When** it is projected, **Then** the document records that gate with a `mustRecompute` verdict whose cause names exactly the changed freshness-input categories `cats` from the report, in the report's order — none dropped, none added.

---

### User Story 2 - A stable, versioned schema for CI, cost views, and agents (Priority: P1)

CI pipelines, cost dashboards, agents, scripts, and snapshot tests consume cache-eligibility.json as a contract. They need the same `CacheEligibilityReport` to always produce a byte-identical document, with a declared schema version they can branch on, and a deterministic field and collection order that makes diffs meaningful.

**Why this priority**: A non-deterministic or unversioned document is unusable as a CI / cost-tracking contract — snapshot tests would flap and consumers could not detect schema changes. Determinism and a version stamp are what make the artifact a *contract* rather than a dump; they are inseparable from US1's value.

**Independent Test**: Project the same report twice and assert byte-for-byte equality; project two reports that are equal as values but were assembled from differently-ordered candidate inputs and assert identical output; assert the document carries a schema-version field and that the entries appear in the report's `GateId`-ordinal order.

**Acceptance Scenarios**:

1. **Given** the same `CacheEligibilityReport`, **When** it is projected twice, **Then** the two documents are byte-for-byte identical.
2. **Given** two reports that are equal as values but were produced from differently-ordered candidate inputs, **When** each is projected, **Then** the documents are identical (the entries in the report's stable `GateId`-ordinal order — and the structural duplicate tiebreak — preserved verbatim).
3. **Given** any projected document, **When** it is inspected, **Then** it carries a declared schema version identifying the cache-eligibility.json contract, and object fields appear in a stable documented order.
4. **Given** any projected document, **When** it is inspected, **Then** it contains no wall-clock timestamp, host/absolute path, raw freshness input, computed freshness key or hash, environment value, numeric process exit code, severity, or ship verdict — only declared gate identifiers, the closed verdict/cause vocabularies, the named changed-input categories, and the opaque evidence reference rendered verbatim.

---

### User Story 3 - The no-hide rule is visible on every must-recompute entry (Priority: P2)

A reviewer reading cache-eligibility.json must be able to see *why* each gate that must recompute does so. Every `mustRecompute` entry must carry its cause — `noPriorEvidence`, or the named list of the exact changed freshness-input categories — so a recompute is always self-explaining and the document never hides the reason a gate could not reuse evidence.

**Why this priority**: The no-hide rule is F041's governance guarantee carried up from F029/F030 — a document that dropped the cause would let a cache miss look arbitrary. Carrying the named cause on every must-recompute entry is what keeps the artifact honest. It builds on US1 but is separable — US1's document is viable before this carry guarantee is independently pinned.

**Independent Test**: Project a report whose entries include a `MustRecompute (InputsChanged [c1; c2])` and a `MustRecompute NoPriorEvidence`, and assert the document shows, on the first, the named categories `c1, c2` exactly, and on the second, the `noPriorEvidence` token — and that every `mustRecompute` entry carries one of these named causes (never an empty or opaque cause).

**Acceptance Scenarios**:

1. **Given** a `MustRecompute (InputsChanged cats)` entry whose `cats` names several changed categories, **When** it is projected, **Then** the entry's rendered cause lists exactly those categories, in the report's order — none omitted, none added, never truncated to the first difference.
2. **Given** a `MustRecompute NoPriorEvidence` entry, **When** it is projected, **Then** the entry's rendered cause is the `noPriorEvidence` token — distinct from an `inputsChanged` cause with an empty category list.
3. **Given** any `mustRecompute` entry, **When** the document is inspected, **Then** it carries exactly one named cause from the closed cause vocabulary; no `mustRecompute` entry is rendered without a cause.

---

### User Story 4 - Total over any well-typed cache-eligibility report (Priority: P2)

The projection must succeed for every `CacheEligibilityReport` the upstream roll-up can produce — empty report, all-reusable, all-must-recompute, mixed, and duplicate-`GateId` entries — with no failure mode of its own, because its input is an already-validated typed value.

**Why this priority**: Totality is what lets later rows call the projection unconditionally without error handling. It is a property over US1's behavior, valuable once the document shape exists.

**Independent Test**: Property-based projection over generated well-typed `CacheEligibilityReport`s asserting the function always returns a document and never throws, including the empty report, an all-reusable report, an all-must-recompute report, and a report with two entries sharing a `GateId`.

**Acceptance Scenarios**:

1. **Given** an empty report (no entries), **When** it is projected, **Then** a valid document with a present, empty entries collection and the schema version is produced — a success, not an error and never a placeholder entry.
2. **Given** a report whose entries are all reusable (or all must-recompute), **When** it is projected, **Then** every entry renders with its verdict, and the document is valid.
3. **Given** any well-typed report, **When** it is projected, **Then** the function returns a document and never throws.

---

### Edge Cases

- **Empty report** — no entries: a valid document with a present, empty entries collection and the schema version, never an error or a "must recompute by default" placeholder (FR-009).
- **All-reusable / all-must-recompute report**: every entry is always rendered; the populated collection carries its entries in the report's order (FR-005).
- **Duplicate `GateId` entries** — two entries under the same gate id (F041 keeps both, never merged): the document renders **two** distinct entries under that gate id, each with its own verdict, in the report's already-fixed order (the F041 structural tiebreak) — neither merged nor deduplicated (FR-005, inherited from F041).
- **`InputsChanged []`** (an empty changed-category list) renders as an `inputsChanged` cause with an empty, present category collection — distinct from `noPriorEvidence`; never collapsed to one another (FR-006).
- **A `GateId` or evidence-reference string containing the id separator or other punctuation** (e.g. a colon): the document renders the declared id / reference string verbatim; it neither re-parses nor re-derives it (FR-008, FR-010).
- **Verdict echo**: the document renders each entry's own `Verdict` verbatim; it re-runs no reuse decision, re-ranks no evidence, and invents no verdict (FR-002) — every verdict is echoed from the already-typed report value.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single projection from the F041 `CacheEligibilityReport` to a `cache-eligibility.json` document, rendering one entry per gate, each with the gate's declared identity and its cache-eligibility verdict.
- **FR-002**: The document MUST render each entry's `Verdict` verbatim from the report value — a closed `reusable` / `mustRecompute` outcome; it MUST NOT re-run the reuse decision, re-rank evidence, or recompute a verdict from any other field.
- **FR-003**: For a `reusable` verdict the document MUST render the carried evidence reference verbatim as an opaque string; it MUST NOT parse, dereference, validate, or re-derive it, and MUST NOT emit any skip action, reuse policy, or enforcement meaning alongside it (a reusable verdict is necessary-not-sufficient).
- **FR-004**: For a `mustRecompute` verdict the document MUST render its cause — either the `noPriorEvidence` token, or an `inputsChanged` cause naming exactly the changed freshness-input categories carried by the report, in the report's order — so the cause is never hidden and `noPriorEvidence` is distinguishable from `inputsChanged` with an empty category list.
- **FR-005**: The document MUST render every entry the report carries, in the report's already-fixed `GateId`-ordinal order (with its structural duplicate tiebreak) preserved verbatim — every entry in exactly one position, none dropped, merged, deduplicated, or reordered. The entries collection MUST always be present; an empty report MUST render as a present, empty collection.
- **FR-006**: For each entry the document MUST carry the gate's declared `GateId` and the verdict's full content verbatim from the report — the evidence reference for `reusable`, the named cause (and its named changed-input categories) for `mustRecompute`; it MUST re-derive, re-classify, and re-order none of them. (FR-002 governs *that the verdict is echoed, not recomputed*; FR-006 governs *that its full payload — evidence reference / cause / categories — is carried completely*.)
- **FR-007**: The projection MUST be deterministic: identical `CacheEligibilityReport` inputs MUST produce a byte-for-byte identical document, with the entries in the report's documented order preserved verbatim and object fields in a stable documented order. (FR-005 governs *entry completeness and the report's entry order*; FR-007 governs *byte-for-byte stability and the fixed object-field order* — together they make the document a diffable contract.)
- **FR-008**: The projection MUST be pure and total: no file, process, clock, network, or git access; no cache lookup against a real store; no freshness key or hash computed; none of the freshness inputs resolved; it MUST NOT throw for any well-typed `CacheEligibilityReport`; an empty report MUST be a valid success.
- **FR-009**: An empty report MUST project to a valid document with a present, empty entries collection and the schema version — never an error and never a "must recompute by default" fallback entry.
- **FR-010**: The document MUST render the declared `GateId` and evidence-reference identifiers verbatim; it MUST NOT re-parse a `GateId` to recover a domain or check, and MUST NOT normalize or interpret the evidence reference.
- **FR-011**: The document MUST express the closed verdict (`reusable` / `mustRecompute`) and cause (`noPriorEvidence` / `inputsChanged`) vocabularies, and the changed-input-category vocabulary, as stable declared tokens, so consumers can branch on them without string-scraping free text.
- **FR-012**: The document MUST NOT contain raw freshness inputs, a computed freshness key or hash, host/absolute paths, wall-clock timestamps, any environment-derived value, a numeric process exit code, a severity, a ship verdict, an exit-code basis, or a provenance/attestation reference — only declared gate identifiers, the closed verdict/cause/category vocabularies, and the opaque evidence reference rendered verbatim.
- **FR-013**: The document MUST carry a declared schema version identifying the cache-eligibility.json contract, so consumers can detect contract changes.
- **FR-014**: The capability MUST require no installed FS.GG package in any inspected repository and add no third-party runtime dependency beyond what the upstream typed values already require.

### Key Entities *(include if feature involves data)*

- **cache-eligibility.json document**: The deterministic, versioned, machine-readable projection of one `CacheEligibilityReport`. Sections: schema version and the per-gate entries collection (in the report's `GateId`-ordinal order).
- **Cache-eligibility entry**: One gate's verdict as rendered — its declared `GateId` and its verdict (a `reusable` verdict carrying an opaque evidence reference, or a `mustRecompute` verdict carrying its cause).
- **Verdict / cause / category tokens**: The closed `reusable` / `mustRecompute` verdict vocabulary, the `noPriorEvidence` / `inputsChanged` cause vocabulary, and the changed-freshness-input-category vocabulary — all rendered verbatim from the report.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Projecting a report with N entries yields a document containing exactly N entries, each with its declared gate id and its verdict (and, for must-recompute, its named cause) tracing back to the report — 100% of entries represented, zero dropped, merged, or invented.
- **SC-002**: Projecting the same report any number of times yields byte-for-byte identical documents (0% variance).
- **SC-003**: Two reports equal as values but assembled from differently-ordered candidate inputs project to identical documents (0% order-dependence).
- **SC-004**: The document renders each entry's own verdict verbatim for 100% of inputs — `reusable` with its evidence reference, or `mustRecompute` with its named cause — with zero recomputation divergence from the report value.
- **SC-005**: 100% of `mustRecompute` entries carry a named cause (`noPriorEvidence` or `inputsChanged` with its exact changed categories in the report's order); zero must-recompute entries are rendered without a cause, and `noPriorEvidence` is always distinguishable from `inputsChanged []`.
- **SC-006**: The projection returns a document for 100% of well-typed reports, including the empty report and duplicate-`GateId` reports, and never throws; the entries collection is always present (empty as an empty collection).
- **SC-007**: Every projected document carries a schema version and contains zero occurrences of raw freshness inputs, a computed freshness key or hash, host paths, timestamps, environment values, a numeric exit code, a severity, a ship verdict, or a provenance reference.

## Assumptions

- **Scope is the pure projection only.** This feature produces the cache-eligibility.json *document value* from a `CacheEligibilityReport`. It does not read git, compute a route, select gates, resolve freshness inputs, look up or store evidence in a real cache, set a process exit code, or expose a CLI command — those are the F016 snapshot, F015/F019 route, the deferred host input-resolution row, and a later cache-store row. The slice continues the pure-core-first pattern of F020/F021/F025 (the route.json, gates.json, and audit.json projections), which each landed their document value before any host wiring.
- **Input is the already-validated F041 `CacheEligibilityReport`.** The projection trusts the upstream typing: it re-derives nothing, re-validates nothing, re-runs no reuse decision, and has no failure mode of its own (mirrors F020/F025 totality rationale). Each verdict and cause is echoed from the value, not recomputed.
- **A standalone per-change document, not an edit to route.json / audit.json.** This row adds a new sibling projection library (the F020 `RouteJson` / F021 `GatesJson` / F025 `AuditJson` shape) and leaves the merged F020 `RouteJson` and F025 `AuditJson` cores and their surface baselines untouched. *Embedding* the cache-eligibility verdict into route.json / audit.json — and the host wiring that resolves each selected gate's full `FreshnessInputs` (the F019 route currently carries only F018 `Gate.FreshnessKey`, the MVP identity, not the F029 inputs F030/F041 consume) — is a later integration row, kept out of this pure projection exactly as F020 deferred fields the upstream value did not carry.
- **The opaque evidence reference is rendered, never interpreted.** F030/F041 model the evidence reference as an opaque edge token; this row renders its string verbatim and neither dereferences nor validates it (the F033 opaque-token precedent).
- **No real cache, no freshness compute.** The report already holds the evaluated verdicts; this row renders them and performs no cache lookup against a store on disk, computes no freshness key or hash, and resolves none of the freshness inputs (Phase-11 host/storage rows).
- **Determinism excludes any clock or environment input**: stable output depends only on the `CacheEligibilityReport` value, so no wall-clock time or host-derived value enters the document; the entries appear in the report's already-fixed `GateId`-ordinal order, preserved verbatim.
- **Home and serialization mechanism are plan-time decisions.** Whether the projection lives in a new sibling library (the F020/F021/F025 shape — assumed here as `FS.GG.Governance.CacheEligibilityJson`) and whether the deterministic JSON is produced by the net10.0 shared-framework serializer (the F020/F021/F025 mechanism, no new package) or a hand-rolled writer are settled in `/speckit-plan`; the spec fixes only the observable document contract (content, order, stability, version, exclusions, the no-hide cause guarantee).
</content>
