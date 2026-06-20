# Feature Specification: Unknown Governed Path Findings

**Feature Branch**: `017-unknown-governed-path-findings`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan" — the next Governance-owned, unchecked row of Phase 2 (*Governance Ship Walking Skeleton And Catalog MVP*) in `docs/initial-implementation-plan.md` is **"Add unknown governed path findings only inside governed roots or protected boundaries."** It is the decision F015 path→capability routing deliberately deferred: routing classifies a path as `UnmatchedInRoot` but, by its own words, "carries no domain and asserts no finding/severity (deferred, FR-007/FR-016)." This feature makes that deferred call.

## Overview

F015 answered "which capability domain does each path belong to?" and split every path into three routing outcomes: `Routed` (matched a capability glob), `UnmatchedInRoot` (under the declared governed root but matched no glob), and `OutOfScope` (not under the governed root at all). F015 stopped exactly there — it explicitly **did not decide** whether an `UnmatchedInRoot` path is a problem, leaving that to "a later Phase-2 row." This feature is that row.

It takes the routing outcomes (F015) and the declared surface classification (F014 `Surface` / `SurfaceClass`) and decides, for each candidate path, whether it is an **unknown governed path finding** — a path that lives inside a region the project has declared it governs, yet has not been classified by any capability glob. The whole point is to do this **without global default-deny**: a file that is simply out of scope, or that the project has explicitly declared as a `Routine` (unmanaged) region, is *not* a finding. Only paths inside the governed root that are neither matched nor declared-routine — and, more emphatically, paths that fall on a declared **protected boundary** — become findings.

This closes the two halves of the Phase-2 exit criteria that F015 left open: *"Routine unclassified files do not trigger global default-deny behavior"* and *"Unknown paths under declared governed roots produce explicit findings."*

This feature is **pure**, like F015: it performs no I/O, senses no git (that is F016), parses no YAML (that is F014), and routes no globs (that is F015). It consumes their already-typed outputs and yields a typed, deterministic finding set: identical inputs produce a byte-identical finding list.

This feature stops at the typed findings. It does **not** assign severity, base/effective enforcement, profile/mode/maturity adjustment; it does not build the gate registry or `GateId`s; it does not compute evidence freshness, decide a ship verdict, or emit route/audit JSON or any CLI command. Those are later Phase-2 and Phase-5 rows that consume these findings.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Flag an unknown path inside the governed root (Priority: P1)

A change touches a path that lives under the project's declared governed root, but no capability path-map glob matches it and the project has not declared that region as routine. The maintainer (or CI) needs that surfaced as an explicit finding — "this path is inside the part of the tree you govern, but nothing classifies it" — so an unclassified file cannot slip into a governed area unnoticed.

**Why this priority**: This is the core value of the row and the second half of the Phase-2 exit criteria. Without it, a brand-new file dropped under a governed root would be invisible to the gate. It is the MVP: independently valuable even before protected-boundary escalation, multi-plane handling, or determinism guarantees are layered on.

**Independent Test**: With fixture facts declaring a governed root and a path map that does not cover `src/Kernel/New.fs`, classify a routing outcome set in which `src/Kernel/New.fs` is `UnmatchedInRoot`; assert exactly one unknown-governed-path finding is produced for that path, located on that path, with a fix hint, and that a sibling `Routed` path in the same set produces no finding.

**Acceptance Scenarios**:

1. **Given** a path classified `UnmatchedInRoot` by routing and not covered by any declared `Routine` surface, **When** findings are computed, **Then** exactly one unknown-governed-path finding is produced for that path, carrying the path and a fix hint, and no finding is produced for any `Routed` path in the same set.
2. **Given** a set of candidate paths with a mix of `Routed`, `UnmatchedInRoot`, and `OutOfScope` outcomes, **When** findings are computed, **Then** a finding is produced for each non-routine `UnmatchedInRoot` path and for no other.
3. **Given** no candidate path is `UnmatchedInRoot` (every path routed or out of scope), **When** findings are computed, **Then** the finding set is empty and that is a successful result, not an error.

---

### User Story 2 - Never default-deny routine or out-of-scope files (Priority: P1)

Most files a change touches are not governed surfaces — build output, scratch notes, files outside the governed root entirely, or regions the project has explicitly declared `Routine` (unmanaged). The maintainer needs these to stay quiet: a governance tool that flagged every unclassified file would be ignored within a day.

**Why this priority**: Co-equal P1 with Story 1 — the row is defined by the *pairing* of "flag unknown governed paths" with "but only inside governed roots or protected boundaries." A finding rule without the suppression rule is global default-deny, which the design explicitly forbids. The two together are the MVP; neither alone is correct.

**Independent Test**: Classify a set containing an `OutOfScope` path and an `UnmatchedInRoot` path that falls within a declared `Routine` surface; assert neither produces a finding, while a third `UnmatchedInRoot` path outside any routine surface does.

**Acceptance Scenarios**:

1. **Given** a path classified `OutOfScope` (outside the declared governed root), **When** findings are computed, **Then** no finding is produced for it, regardless of how many such paths exist.
2. **Given** an `UnmatchedInRoot` path that falls within a declared `Routine` surface, **When** findings are computed, **Then** no finding is produced for it — an explicitly-declared unmanaged region is not an unknown governed path.
3. **Given** a change that touches only out-of-scope and declared-routine paths, **When** findings are computed, **Then** the finding set is empty (no global default-deny).

---

### User Story 3 - Escalate unknown paths on a protected boundary (Priority: P2)

Some governed regions are not merely governed but **protected** — declared as a protected surface that the gate exists to defend. When an unclassified path lands on such a boundary, the maintainer needs it surfaced as a *distinct, more emphatic* finding than an ordinary governed-root unknown, so a later gate can treat the two differently without re-deriving the zone.

**Why this priority**: This is the "or protected boundaries" half of the row title. It builds on Story 1's in-root finding, so it is P2: valuable for protected-boundary rigor, but only meaningful once ordinary governed-root findings exist. This feature *distinguishes* the protected-boundary finding; it does **not** assign it a severity or enforcement level (Phase 5).

**Independent Test**: Classify an `UnmatchedInRoot` path that falls within a declared `ProtectedSurface`; assert the resulting finding is distinguishable (by a distinct id or an explicit zone) from a finding for an `UnmatchedInRoot` path that is in the governed root but in no protected surface.

**Acceptance Scenarios**:

1. **Given** an `UnmatchedInRoot` path within a declared `ProtectedSurface`, **When** findings are computed, **Then** the finding is distinguishable from an ordinary governed-root unknown finding (distinct id or explicit protected-boundary zone), carrying the protected surface's identity.
2. **Given** an `UnmatchedInRoot` path that is within the governed root but in no declared surface, **When** findings are computed, **Then** its finding is the ordinary governed-root flavor, not the protected-boundary flavor.
3. **Given** a path that falls within both an ordinary governed region and a protected surface (overlapping declarations), **When** findings are computed, **Then** a single finding is produced for that path and its flavor is resolved by a documented precedence in which the protected boundary outranks the ordinary governed-root unknown.

---

### User Story 4 - Deterministic, explainable findings (Priority: P2)

A maintainer, CI, and a later route/audit report all need the finding set to be stable and self-explanatory: the same inputs always produce the same findings in the same order, and every finding says which path it concerns and how to resolve it (declare a path-map glob, mark the region routine, or classify the surface).

**Why this priority**: Determinism and explainability are stated design goals (deterministic JSON, readable diagnostics) and a precondition for the later route/audit JSON that consumes these findings. P2: the findings must exist (Stories 1–3) before their ordering and message contract matter, but a non-deterministic or opaque finding set is unusable downstream.

**Independent Test**: Compute findings twice over the same inputs and assert the two finding lists are byte-for-byte identical, including order; assert each finding's message names the offending path and at least one concrete remediation.

**Acceptance Scenarios**:

1. **Given** identical inputs (same facts, same routing outcomes), **When** findings are computed twice, **Then** the two finding lists are byte-for-byte identical, including the ordering of every finding.
2. **Given** the candidate paths presented in a different input order, **When** findings are computed, **Then** the finding list is unchanged (ordering depends on the finding's documented sort key, not input order).
3. **Given** any produced finding, **When** its message is read, **Then** it identifies the path and offers at least one concrete remediation (e.g. add a path-map glob, declare the region routine, or classify the surface), with no raw YAML, host paths, or product vocabulary beyond declared domain/surface ids.

---

### User Story 5 - Classify every change plane uniformly (Priority: P3)

A snapshot reports unknown-able paths in three planes — committed-changed, dirty, and untracked (F016). A maintainer needs an unclassified path treated the same whichever plane it appears in, so a local preview (which sees dirty/untracked) and a CI run (which sees the committed diff) agree on the same unknown findings for the same paths.

**Why this priority**: Local/CI parity is a stated goal, and this is the additive enrichment that extends Story 1's decision uniformly across F016's planes. P3: the finding decision (Stories 1–3) is correct on a single plane already; multi-plane uniformity is real but additive and must not change the per-path decision.

**Independent Test**: Present the same unclassified in-root path once as committed-changed and once as untracked; assert each yields the same finding decision, and that a path appearing in more than one plane is reported as a single finding with documented deduplication.

**Acceptance Scenarios**:

1. **Given** the same unclassified in-root path supplied as a committed-changed path and, separately, as an untracked path, **When** findings are computed for each, **Then** the finding decision is identical (same flavor, same message shape).
2. **Given** one path that appears in more than one plane of the same input, **When** findings are computed, **Then** exactly one finding is produced for that path, by a documented deduplication rule.
3. **Given** committed, dirty, and untracked planes that together contain several unclassified in-root paths, **When** findings are computed, **Then** the finding set is the deterministic union across planes with no plane silently dropped.

---

### Edge Cases

- **Empty input**: No candidate paths, or no `UnmatchedInRoot` paths, yields an empty finding set as a successful result — never an error and never a fabricated "all clear" finding.
- **Routine surface nested inside a protected surface (or vice versa)**: Overlapping surface declarations over the same path MUST resolve to a single, documented outcome — the precedence between *suppression* (routine) and *escalation* (protected) MUST be explicit and tested, not order-dependent.
- **A path matched by a glob AND inside a protected surface**: A `Routed` path is classified; it is NOT an unknown path even if it also lies on a protected boundary. This feature flags only *unclassified* paths.
- **A protected surface declared over a region with no candidate path in it**: Declaring a protected surface does not, by itself, manufacture findings; a finding requires an actual unclassified candidate path on that boundary.
- **Governed root is a subdirectory**: Paths outside that subtree are `OutOfScope` and never findings; the suppression rule depends on the F015 outcome, not on re-deriving the root.
- **Duplicate candidate paths**: The same path supplied more than once (across or within planes) yields a single finding by the documented deduplication rule.
- **No surfaces declared at all**: With no declared `Routine` or `ProtectedSurface`, every non-routed in-root path is an ordinary governed-root unknown; nothing is suppressed and nothing is escalated.
- **Surface paths vs candidate paths**: Both are normalized governed paths (F014/F015); surface membership is decided on the normalized form, never on raw or host paths.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST produce a typed set of **unknown-governed-path findings** from (a) per-path routing outcomes (the F015 `RoutingResult` of `Routed` / `UnmatchedInRoot` / `OutOfScope`) and (b) the declared surface classification (F014 `Surface` / `SurfaceClass`). The result MUST be a structured value (not prose) consumable directly by the later gate registry, route report, and ship audit.
- **FR-002**: A finding MUST be produced for a candidate path that routing classified as **`UnmatchedInRoot`** and that is **not covered by any declared `Routine` surface** — i.e. a path inside the declared governed root that no capability glob classified and that the project has not declared unmanaged.
- **FR-003**: **No finding** MUST be produced for a path routing classified as `OutOfScope` (outside the declared governed root). The feature MUST NOT implement global default-deny: out-of-scope routine files are silent.
- **FR-004**: **No finding** MUST be produced for an `UnmatchedInRoot` path that falls within a declared `Routine` surface — an explicitly-declared unmanaged region is, by declaration, not an unknown governed path.
- **FR-005**: **No finding** MUST be produced for a path routing classified as `Routed` (matched a capability glob), even when that path also lies within a protected surface — this feature flags only *unclassified* paths.
- **FR-006**: A finding for an `UnmatchedInRoot` path that falls within a declared **`ProtectedSurface`** MUST be **distinguishable** from an ordinary governed-root unknown finding — by a distinct stable id or an explicit "zone" field — and MUST carry the protected surface's declared identity. This realizes the "or protected boundaries" clause of the row.
- **FR-007**: When a single path is covered by overlapping surface declarations (e.g. both a routine and a protected surface, or both an ordinary governed region and a protected surface), the feature MUST resolve it to **one finding (or no finding)** by a **documented, deterministic precedence**: a protected-boundary classification outranks an ordinary governed-root unknown; the precedence between routine suppression and protected escalation MUST be explicitly documented and tested.
- **FR-008**: Each finding MUST be a **stable-id, located, explained** record: a stable diagnostic id, the offending normalized governed path, the relevant declared identity (domain/surface id) where applicable, and a `Message` carrying at least one concrete fix hint (declare a path-map glob, mark the region routine, or classify the surface). No raw YAML, no host paths, and no product vocabulary beyond declared domain/surface ids.
- **FR-009**: Every collection the feature emits — findings and any nested identity lists — MUST be in a **deterministic, documented order** (e.g. by path, then finding id), so identical inputs yield a **byte-identical** finding set, unchanged under re-ordering of the input paths or the authored surface declarations.
- **FR-010**: The feature MUST treat the **three F016 snapshot planes** (committed-changed, dirty, untracked) **uniformly**: an unclassified in-root path produces the same finding decision whichever plane it came from. A path appearing in more than one plane MUST yield a **single** finding by a documented deduplication rule; the plane MAY be retained on the finding but MUST NOT change the finding decision.
- **FR-011**: The feature MUST be **pure and total**: no I/O, no git, no clock, never throwing. It MUST consume already-typed F014 facts and F015 routing outcomes (and normalized candidate paths) and MUST NOT re-parse `.fsgg` YAML, re-normalize paths, re-validate the capability catalog, or sense git.
- **FR-012**: An **empty finding set** (no unclassified in-root paths) MUST be a valid, successful outcome, distinct from any error. The feature has no failure mode of its own beyond what its already-validated typed inputs guarantee.
- **FR-013**: The feature MUST NOT assign severity, base or effective enforcement, profile/mode/maturity adjustment; MUST NOT build the gate registry or any `GateId`; MUST NOT compute evidence freshness, decide a ship verdict, or emit route/audit JSON or any CLI command. It produces the typed findings those later rows consume.
- **FR-014**: All paths and identities in a finding MUST be expressed in the **normalized governed-path form** and the declared id newtypes (F014 `GovernedPath`, `DomainId`, `SurfaceId`); never absolute host paths, raw git output, or free-form strings standing in for declared ids.
- **FR-015**: The feature MUST require only the already-typed F014 facts and F015 routing outcomes as inputs. It MUST NOT require any FS.GG package to be installed in the repository under inspection (governance inspects a project; a project never depends on governance), and it MUST live in the product-neutral Governance layer — the kernel never sees this surface/finding vocabulary.

### Key Entities *(include if feature involves data)*

- **Unknown-governed-path finding**: The typed result for one unclassified path inside a governed/protected region — a stable id, the normalized path, the relevant declared identity, the finding zone, and an explained fix hint. A pure, deterministic value.
- **Finding zone**: Which managed region triggered the finding — an ordinary **governed-root** unknown versus a **protected-boundary** unknown — so a later gate can treat them differently without re-deriving the zone.
- **Routing outcome (consumed)**: The per-path F015 `RoutingResult` (`Routed` / `UnmatchedInRoot` / `OutOfScope`) this feature reads but does not recompute.
- **Routine surface (suppressor)**: A declared `Routine` surface whose paths mark an explicitly-unmanaged region; an `UnmatchedInRoot` path within it is suppressed (no finding).
- **Protected surface (escalator)**: A declared `ProtectedSurface` whose paths mark a protected boundary; an `UnmatchedInRoot` path within it is escalated to the distinct protected-boundary flavor.
- **Candidate path / change plane**: A normalized governed path under consideration and, optionally, the F016 plane (committed-changed / dirty / untracked) it came from; the plane is retained but does not change the decision.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For fixture facts with a known governed root and path map, an `UnmatchedInRoot` candidate path not covered by any routine surface produces exactly one unknown-governed-path finding located on that path with a fix hint, while every `Routed` path in the same set produces none.
- **SC-002**: A candidate set containing out-of-scope paths and `UnmatchedInRoot` paths inside a declared `Routine` surface produces **zero** findings for those paths — no global default-deny — while non-routine in-root unknowns in the same set still produce findings.
- **SC-003**: An `UnmatchedInRoot` path within a declared `ProtectedSurface` produces a finding that is distinguishable (distinct id or explicit zone) from an ordinary governed-root unknown and carries the protected surface's identity; an overlapping routine + protected declaration over one path resolves to a single finding by the documented precedence.
- **SC-004**: Computing findings twice over identical inputs, and once with the candidate paths and surface declarations reordered, yields byte-for-byte identical finding lists including order.
- **SC-005**: The feature performs no I/O and never throws on any typed input, reaches no git or network, and runs without any FS.GG package installed in the repository under inspection.
- **SC-006**: Every finding carries only normalized governed paths, declared domain/surface ids, a finding zone, and a fix-hint message — no raw YAML, host paths, timestamps, or product vocabulary beyond declared ids.
- **SC-007**: The same unclassified in-root path supplied as committed-changed, dirty, or untracked yields the same finding decision, and a path appearing in multiple planes is reported as a single finding by the documented deduplication rule.

## Assumptions

- This feature is the decision F015 deferred: it consumes the F015 `RouteReport` / per-path `RoutingResult` and the F014 declared `Surface` classification, and produces unknown-governed-path findings only — closing the Phase-2 exit criteria *"Routine unclassified files do not trigger global default-deny behavior"* and *"Unknown paths under declared governed roots produce explicit findings."*
- A single governed root is assumed and multi-root scoping is out of scope, consistent with F015 and F016.
- Declared surfaces (`Routine`, `ProtectedSurface`) are expressed as normalized governed paths relative to the governed root (F014); surface membership is decided on that normalized form, never on raw or host paths.
- This feature is **pure and total**, like F015 routing: no I/O, no git, no clock, deterministic. The impure sensing of which paths changed is F016; the glob routing is F015; the YAML validation is F014. This feature only classifies their typed outputs.
- Severity, effective enforcement, profile/mode/maturity adjustment, the gate registry and `GateId`s, evidence freshness, ship verdicts, and route/audit JSON or any CLI command are **later rows** (the remaining Phase-2 rows and Phase 5) that consume these findings; none are implemented here.
- The model and classifier live in the product-neutral Governance layer (consuming `FS.GG.Governance.Config` facts and `FS.GG.Governance.Routing` outcomes); the exact assembly placement is settled at plan time. The kernel never sees git, surfaces, or findings.
- The supported finding surface is the MVP set named by the plan row: unknown governed paths inside governed roots, escalated on protected boundaries, suppressed for routine and out-of-scope paths. Richer findings (owner attribution, suggested capability inference, cost estimation) are out of scope.
