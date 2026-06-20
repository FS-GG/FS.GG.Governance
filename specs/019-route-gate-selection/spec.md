# Feature Specification: Route Gate Selection

**Feature Branch**: `019-route-gate-selection`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan" — the next Governance-owned, unchecked row of Phase 2 (*Governance Ship Walking Skeleton And Catalog MVP*) in `docs/initial-implementation-plan.md`. With the typed facts (F014), path→capability routing (F015), git/CI snapshot (F016), unknown-governed-path findings (F017), and the typed gate registry (F018) all complete, the remaining Phase-2 rows are: *"Add `fsgg route --paths ...` / `fsgg route --since <rev>` / `fsgg ship --mode gate ...`"*, *"Emit deterministic route and audit JSON with selected gates, matched rules, unmatched governed paths, ... cost, cache eligibility, ..."*, and *"Publish the first GitHub Actions guidance for branch protection."* Following this repo's established decomposition — a pure typed core first, then the CLI / JSON / effects rows that serialize and drive it (F014→F015→F016→F017→F018 each landed a pure library before any command consumed it) — this feature is **the pure route-resolution core**: the deterministic join that turns *which domain each changed path belongs to* (F015) plus *which gates exist per domain* (F018) into **the selected gates for a change, with a route trace explaining each selection**. The `fsgg route` / `fsgg ship` CLI, the route/audit JSON serialization, and the ship verdict are the rows that consume this value next.

## Overview

F015 answered "which capability domain does each changed path belong to?" and F018 answered "what is the stable set of gates, one per declared check, and what metadata does each carry?" Neither connected the two. This feature is the connection: given the per-path routing outcomes (F015 `RouteReport`), the gate registry (F018 `GateRegistry`), and the unknown-governed-path findings (F017 `FindingReport`), it computes **which gates a change selects** and **why** — a typed `RouteResult` (a *route trace*) in which every selected gate names the changed path(s) that pulled it in, the affected capability domain, the matching glob (the "rule"), and the gate's declared cost.

The join is exact and declared: a path the routing classified as `Routed` carries a winning `DomainId`; a gate's identity is `domain:checkId` and it carries its `Domain`; so the gates a `Routed` path selects are precisely the registry gates whose `Domain` equals that path's routed domain. A change selects the union of those gates across all its `Routed` paths, deduplicated by `GateId`, each annotated with the path(s) and glob(s) that selected it. Paths that routed nowhere (`UnmatchedInRoot`, `OutOfScope`) select no gate; the unknown-governed-path findings (F017) that some of those `UnmatchedInRoot` paths already produced are carried through onto the route trace so a single value explains both "what runs" and "what is unclassified."

This realizes the design's *"Route to the matching capability gates"* posture (`docs/initial-design.md`, *Routing safety policy*) and its requirement that *"Routes should explain every selected gate in terms of changed path, affected capability, matching rule, ... and cost."* It is the source of the *selected gates*, *matched rules*, *unmatched governed paths*, and *cost* fields that `readiness/<id>/route.json` will later carry.

This feature is **pure and total**, like F015/F017/F018: it performs no I/O, senses no git (that is F016), parses no YAML (that is F014), routes no globs (that is F015), and builds no registry (that is F018). It consumes their already-typed outputs and yields a typed, deterministic route trace: identical inputs produce a byte-identical selected-gate set.

This feature stops at the typed `RouteResult`. It does **not** assign base/effective severity or profile/mode/maturity enforcement (Phase 5); it does not compute evidence freshness or decide cache reuse (Phase 11 — the gate's carried `FreshnessKey` is propagated, never evaluated); it does not run, execute, or order any gate/check/command; it does not decide a **ship verdict**, blockers, warnings, or exit-code basis (the `fsgg ship` / audit.json row); and it emits no route/audit JSON, no `.fsgg/gates.json`, and no CLI command. Those are the later Phase-2 and Phase-5 rows that consume this route trace.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Select the gates a change must run (Priority: P1)

A change touches one or more paths that route to declared capability domains. The maintainer (or CI) needs to know **which gates apply to this change** — not the whole catalog, only the gates whose domain a changed path actually reached — so a local preview or a protected-boundary check runs exactly the relevant gates and nothing else.

**Why this priority**: This is the core value of the row and the precondition for every later Phase-2 row (`fsgg route`, route.json, `fsgg ship`). Without it there is no notion of "the gates this change selected"; the registry (F018) lists every gate but says nothing about relevance to a change. It is the MVP: independently valuable even before route-trace explanation, finding carry-through, cost rollup, or determinism guarantees are layered on.

**Independent Test**: With a fixture registry containing gates in domains `build` and `docs`, and a routing report in which `src/Kernel/Core.fs` is `Routed` to `build` and a second path is `Routed` to `docs`, select gates for the change; assert the selected set is exactly the `build` gates ∪ the `docs` gates (by `GateId`), and that a registry gate in an unreached domain `release` is **not** selected.

**Acceptance Scenarios**:

1. **Given** a routing report with at least one path `Routed` to domain `d` and a registry containing one or more gates whose `Domain` is `d`, **When** gates are selected, **Then** every gate whose `Domain` is `d` is in the selected set and is annotated with the changed path that reached `d`.
2. **Given** a registry gate whose `Domain` is reached by **no** `Routed` path in the report, **When** gates are selected, **Then** that gate is **not** selected.
3. **Given** two distinct `Routed` paths whose winning domains differ, **When** gates are selected, **Then** the selected set is the union of both domains' gates with no gate omitted and none duplicated.

---

### User Story 2 - Explain every selected gate (route trace) (Priority: P1)

A maintainer who sees a gate selected needs to know **why**: which changed path pulled it in, which capability domain it belongs to, which glob (rule) the path matched, and what the gate costs. A list of gate ids with no justification is unusable for a route preview and cannot be serialized into the explainable route.json the design requires.

**Why this priority**: Co-equal P1 with Story 1 — the design states a route *"should explain every selected gate in terms of changed path, affected capability, matching rule, ... and cost."* Selection without explanation is a regression from the design's stated contract and blocks the route.json row. The two together are the MVP; a bare id set is not.

**Independent Test**: Select gates for a change in which `src/Api/Surface.fs` is `Routed` to domain `api` via glob `src/Api/**`; assert the selected `api` gate carries that path, the domain `api`, the matching glob `src/Api/**`, and the gate's declared cost — and that when two paths both reach `api` the selection records **both** selecting paths for the single shared gate.

**Acceptance Scenarios**:

1. **Given** a gate selected because path `p` `Routed` to its domain via glob `g`, **When** the route trace is read, **Then** the selection names `p`, the gate's `Domain`, the matching glob `g`, and the gate's declared `Cost`.
2. **Given** a single gate reached by more than one `Routed` path, **When** the route trace is read, **Then** the gate appears **once** with all selecting paths (and their globs) recorded against it, in a documented deterministic order.
3. **Given** any selected gate, **When** the route trace is read, **Then** it carries only declared ids (`GateId`, `DomainId`, normalized globs/paths) and the declared cost — no raw YAML, host paths, or product vocabulary beyond declared ids.

---

### User Story 3 - Carry unknown-governed-path findings onto the route (Priority: P2)

A change frequently touches both classified paths (which select gates) and unclassified in-root paths (which F017 already flagged as unknown-governed-path findings). The maintainer needs a **single** route value that shows both the selected gates and the unmatched governed paths, so a route preview and the later route.json explain "what will run" and "what is unclassified" together rather than forcing two disjoint reports.

**Why this priority**: The design's route.json carries *selected gates* **and** *unmatched governed paths* in one record. This builds on Story 1's selection, so it is P2: valuable for a complete route picture, but only meaningful once gate selection exists. This feature **carries through** the F017 findings unchanged; it does **not** re-derive, re-classify, or re-route them.

**Independent Test**: Select a route for a change whose paths include one `Routed` path (selecting a gate) and one `UnmatchedInRoot` path for which F017 produced an `UnknownGovernedPath` finding; assert the resulting route value contains both the selected gate and that finding, with the finding byte-identical to the F017 input.

**Acceptance Scenarios**:

1. **Given** a route computed from a registry, a routing report, and an F017 finding report containing findings, **When** the route trace is read, **Then** it carries exactly those findings, unchanged, alongside the selected gates.
2. **Given** an empty F017 finding report (no unknown governed paths), **When** the route trace is read, **Then** its finding list is empty and that is a successful result, not an error or a fabricated finding.
3. **Given** an `UnmatchedInRoot` path that produced a finding, **When** gates are selected, **Then** that path selects **no** gate (it routed to no domain), and its finding is present on the route — the two facts coexist without contradiction.

---

### User Story 4 - Roll up the route cost (Priority: P2)

Before running anything, a maintainer wants the **total declared cost** of a route — the documented aggregate of the selected gates' declared costs — so a local preview can warn that a change pulls in expensive gates and a later row can suggest cheaper alternatives. Each gate's cost is declared (F018); a route needs them rolled up once, deterministically.

**Why this priority**: Cost is a named field of route.json and a stated design goal (*"Explain broad routes ... [with] cost and cheaper local alternative"*). P2: the gates must be selected (Stories 1–2) before their costs can be rolled up, and the rollup is additive — it must not change which gates are selected.

**Independent Test**: Select a route whose selected gates carry known declared costs; assert the route's rolled-up cost equals the documented aggregate of exactly the distinct selected gates' costs (each shared gate counted once), and that re-running yields the identical rollup.

**Acceptance Scenarios**:

1. **Given** a route selecting a set of distinct gates with declared costs, **When** the cost rollup is read, **Then** it is the documented aggregate over exactly those distinct gates (a gate reached by several paths counted once).
2. **Given** a route selecting no gates (no `Routed` path reached any gate's domain), **When** the cost rollup is read, **Then** it is the documented identity/zero aggregate and the route is a valid, successful empty route.
3. **Given** identical inputs, **When** the cost rollup is computed twice, **Then** the two rollups are identical.

---

### User Story 5 - Deterministic, stable route trace (Priority: P2)

A maintainer, CI, and the later route.json all need the route trace to be stable: the same inputs always produce the same selected gates, selecting paths, findings, and cost in the same order, and re-ordering the input paths or the registry's gates never changes the result. A non-deterministic route is unusable as a CI contract or a golden JSON snapshot.

**Why this priority**: Determinism is a stated design goal (deterministic route/audit JSON) and a precondition for the byte-stable route.json that consumes this value. P2: the route must be *correct* (Stories 1–4) before its *ordering* matters, but a non-deterministic route cannot be serialized into a stable snapshot.

**Independent Test**: Compute a route twice over the same inputs, and once with the input candidate paths and the registry's gate list reordered; assert all three selected-gate lists (and their selecting-path sub-lists, findings, and cost) are byte-for-byte identical, including order.

**Acceptance Scenarios**:

1. **Given** identical inputs, **When** the route is computed twice, **Then** the two route traces are byte-for-byte identical, including the order of selected gates, the selecting paths under each gate, and the findings.
2. **Given** the same inputs with the candidate paths (and/or the registry's gates) presented in a different order, **When** the route is computed, **Then** the route trace is unchanged (ordering depends on documented sort keys — `GateId` ordinal for gates, normalized path for selecting paths — not on input order).
3. **Given** a route, **When** any emitted collection is read, **Then** it is in a single documented deterministic order with no input-order leakage.

---

### Edge Cases

- **Empty change**: No candidate paths, or no `Routed` paths, selects **no** gates — a valid, successful empty route, never an error and never a "select everything" fallback.
- **Empty registry**: A registry with no gates selects nothing for any route; the route is empty and successful (consistent with F018's empty-registry-is-valid outcome).
- **Routed path whose domain has no gate**: A path `Routed` to a domain for which the registry contains no gate selects nothing for that path and is not an error — the domain is declared but carries no check.
- **Many paths, one gate**: Several `Routed` paths reaching the same domain (and thus the same gate) yield **one** selected-gate entry carrying all selecting paths, deduplicated by `GateId`; the gate's cost is counted **once** in the rollup.
- **One path, many gates**: A path `Routed` to a domain with several gates selects **all** of that domain's gates, each annotated with that one selecting path.
- **`UnmatchedInRoot` / `OutOfScope` paths**: Select no gate (they routed to no domain). An `UnmatchedInRoot` path may still carry an F017 finding, which is carried through; an `OutOfScope` path contributes nothing.
- **Findings present but no gates selected**: A change touching only unclassified in-root paths yields an empty selected-gate set **with** the F017 findings present — both are true at once.
- **Routing diagnostics on the report**: F015 may attach routing diagnostics (e.g. `AmbiguousRoute`) to the report; a `Routed` path with an `AmbiguousRoute` diagnostic still selects its (resolved) domain's gates — this feature selects on the resolved routing outcome and does not re-resolve ambiguity.
- **Gate↔domain join is on declared ids only**: Selection matches a gate's declared `Domain` to a path's routed `DomainId` by id equality, never by re-parsing the `GateId` string or re-deriving the domain.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST produce a typed **route result** (route trace) from (a) the F018 `GateRegistry`, (b) the F015 per-path routing outcomes (`RouteReport` / `PathRouting` of `Routed` / `UnmatchedInRoot` / `OutOfScope`), and (c) the F017 unknown-governed-path `FindingReport`. The result MUST be a structured value (not prose) consumable directly by the later `fsgg route` / `fsgg ship` commands and route/audit JSON.
- **FR-002**: For each path the routing classified as **`Routed`** to a `DomainId` `d`, the feature MUST select **every** registry gate whose declared `Domain` equals `d`. The selected-gate set of a change MUST be the **union** of those gates across all `Routed` paths, **deduplicated by `GateId`** — a gate reached by several paths appears exactly once.
- **FR-003**: A path classified **`UnmatchedInRoot`** or **`OutOfScope`** MUST select **no** gate. The feature MUST NOT introduce any "select everything" or default-heavy fallback for unrouted paths; selection requires a positive `Routed` match (preserving the routing safety policy).
- **FR-004**: Each selected gate MUST carry a **route trace** explaining the selection: the `GateId`, the affected `Domain`, the **selecting path(s)** that reached that domain, the **matching glob** each selecting path won on (the F015 `Routed` glob — the "rule"), and the gate's declared `Cost`. A gate selected by more than one path MUST record **all** selecting paths in a documented deterministic order.
- **FR-005**: The feature MUST **carry through** the F017 `FindingReport` onto the route result **unchanged** — it MUST NOT re-derive, re-classify, re-route, or re-order the findings beyond placing them on the route in their already-deterministic order. The route is the single value that explains both selected gates and unmatched governed paths.
- **FR-006**: The feature MUST roll up a **total route cost** as a documented deterministic aggregate of exactly the **distinct selected gates'** declared `Cost` values (each shared gate counted once). An empty selection MUST yield the documented identity/zero aggregate, not an error.
- **FR-007**: Every collection the feature emits — selected gates, the selecting paths/globs under each gate, and the carried findings — MUST be in a **deterministic, documented order** (selected gates by `GateId` ordinal; selecting paths by normalized path ordinal), so identical inputs yield a **byte-identical** route trace, unchanged under re-ordering of the input candidate paths or the registry's gate list.
- **FR-008**: The feature MUST be **pure and total**: no I/O, no git, no clock, never throwing. It MUST consume already-typed F015/F017/F018 outputs and MUST NOT re-parse `.fsgg` YAML, re-normalize paths, re-route globs, re-build the registry, re-classify findings, or sense git.
- **FR-009**: An **empty route** (no selected gates) MUST be a valid, successful outcome, distinct from any error — whether because there are no `Routed` paths, the registry is empty, or no `Routed` path reached a gate's domain. The feature has no failure mode of its own beyond what its already-validated typed inputs guarantee.
- **FR-010**: The feature MUST select gates by **declared id equality** between a gate's `Domain` and a path's routed `DomainId`. It MUST NOT re-parse the `GateId` string to recover a domain, re-derive a domain from a path, or match on any non-declared signal.
- **FR-011**: The feature MUST NOT assign base or effective severity, profile/mode/maturity enforcement, or any blocking decision; MUST NOT compute evidence freshness or decide cache reuse (a gate's declared `FreshnessKey` MAY be carried through but MUST NOT be evaluated); MUST NOT run, execute, or order any gate/check/command; MUST NOT decide a **ship verdict**, blockers, warnings, or exit-code basis; and MUST NOT emit route/audit JSON, `.fsgg/gates.json`, or any CLI command. It produces the typed route those later rows consume.
- **FR-012**: All paths, globs, domains, and gate ids in the route MUST be expressed in the **declared id newtypes** (F014 `GovernedPath`, `DomainId`; F018 `GateId`); never absolute host paths, raw git output, or free-form strings standing in for declared ids. No field carries raw YAML, host paths, or timestamps.
- **FR-013**: The feature MUST require only the already-typed F015 routing outcomes, F017 findings, and F018 registry as inputs. It MUST NOT require any FS.GG package to be installed in the repository under inspection (governance inspects a project; a project never depends on governance), and it MUST live in the product-neutral Governance layer — the kernel never sees this route/gate-selection vocabulary.

### Key Entities *(include if feature involves data)*

- **Route result (route trace)**: The typed result of selecting gates for a change — the deterministic selected-gate set, the carried unknown-governed-path findings, and the rolled-up total cost. A pure, deterministic value, the source of route.json's *selected gates* / *matched rules* / *unmatched governed paths* / *cost* fields.
- **Selected gate**: One registry gate that a change selected, annotated with its route trace — the `GateId`, the affected `Domain`, the selecting path(s) and the glob(s) they matched, and the gate's declared cost. Deduplicated by `GateId`.
- **Selecting path (route reason)**: A changed path that `Routed` to a selected gate's domain, paired with the matching glob it won on — the "why this gate" link from changed path to selected gate.
- **Routing outcome (consumed)**: The per-path F015 `RoutingResult` (`Routed` carrying a `DomainId` + matched glob / `UnmatchedInRoot` / `OutOfScope`) this feature reads but does not recompute.
- **Gate registry (consumed)**: The F018 `GateRegistry` — the stable set of gates, one per declared check, each carrying `Domain`, `Cost`, and the other metadata — this feature selects from but does not rebuild.
- **Unknown-governed-path findings (carried)**: The F017 `FindingReport` carried onto the route unchanged, so one value explains both what runs and what is unclassified.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a fixture registry with gates in two domains and a routing report whose `Routed` paths reach both domains, selecting a route yields exactly the union of those two domains' gates (by `GateId`), with each gate annotated by the changed path(s) that reached its domain, and a gate in an unreached domain is absent.
- **SC-002**: Every selected gate in the route trace names its selecting path(s), its affected domain, the matching glob each selecting path won on, and the gate's declared cost; a gate reached by multiple paths appears once with all selecting paths recorded.
- **SC-003**: A route computed from a registry, a routing report, and a non-empty F017 finding report carries those findings byte-identically alongside the selected gates; an empty finding report yields an empty finding list (a successful result), and `UnmatchedInRoot` / `OutOfScope` paths select no gate.
- **SC-004**: The route's rolled-up cost equals the documented aggregate over exactly the distinct selected gates (each shared gate counted once); an empty selection yields the documented zero/identity aggregate; the rollup is identical on re-run.
- **SC-005**: Computing a route twice over identical inputs, and once with the candidate paths and the registry's gates reordered, yields byte-for-byte identical route traces — selected gates, selecting paths, findings, and cost — including order.
- **SC-006**: The feature performs no I/O and never throws on any typed input, reaches no git or network, and runs without any FS.GG package installed in the repository under inspection.
- **SC-007**: Every field in the route carries only declared id newtypes (`GovernedPath`, `DomainId`, `GateId`) and declared cost — no raw YAML, host paths, timestamps, severity, enforcement, freshness verdict, or ship verdict.

## Assumptions

- This feature is the **join/selection** row that connects F015 routing to F018 gates and carries F017 findings: it consumes the F015 `RouteReport` (per-path `RoutingResult`), the F018 `GateRegistry`, and the F017 `FindingReport`, and produces the typed route trace — the source of route.json's *selected gates*, *matched rules*, *unmatched governed paths*, and *cost*. It is the pure core the `fsgg route` / `fsgg ship` CLI and route/audit JSON rows consume next.
- Gate↔domain selection is on **declared id equality** (a gate's `Domain` to a path's routed `DomainId`); the `GateId` string is never re-parsed to recover a domain.
- The changed paths come from a single governed root (consistent with F015/F016/F017); multi-root scoping is out of scope. The impure sensing of which paths changed is F016; the glob routing is F015; the registry assembly is F018; the unknown-path classification is F017. This feature only joins their typed outputs.
- This feature is **pure and total**, like F015/F017/F018: no I/O, no git, no clock, deterministic; identical inputs produce a byte-identical route trace.
- **Severity, base/effective enforcement, profile/mode/maturity adjustment**, the **ship verdict / blockers / warnings / exit-code basis**, **evidence freshness computation and cache reuse** (Phase 11), gate **execution/ordering**, and **route/audit JSON or any CLI command** are **later rows** (the remaining Phase-2 rows and Phase 5/11) that consume this route; none are implemented here. A gate's declared `FreshnessKey` is carried but never evaluated.
- "Cost rollup" is a documented deterministic aggregate of the declared `Cost` values of the distinct selected gates; the exact aggregate form (e.g. a multiset of cost tiers vs. a summed scalar) is a value-shape detail settled at plan time, consistent with how F018 carries `Cost`.
- The model and selector live in the product-neutral Governance layer (consuming `FS.GG.Governance.Routing`, `FS.GG.Governance.Findings`, and `FS.GG.Governance.Gates` outputs, all of which already depend on `FS.GG.Governance.Config`); the exact project placement and whether this is a new `FS.GG.Governance.Route` library or an addition to an existing one is settled at plan time. The kernel never sees routing, gates, findings, or route traces.
- The supported route surface is the MVP named by the plan row: selected gates with a route trace, carried unknown-governed-path findings, and a cost rollup. Richer route output (expected artifacts/evidence, cheaper-alternative suggestion, profile-adjusted enforcement, cache-eligibility verdict) is out of scope for this row.
</content>
