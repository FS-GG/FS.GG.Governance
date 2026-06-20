# Feature Specification: Typed Gate Registry

**Feature Branch**: `018-typed-gate-registry`

**Created**: 2026-06-20

**Status**: Draft (reconciled at plan time — see Assumptions)

**Input**: User description: "start the next item in the implementation plan" — the next Governance-owned, unchecked row of Phase 2 (*Governance Ship Walking Skeleton And Catalog MVP*) in `docs/initial-implementation-plan.md` is **"Define typed `GateId` metadata with prerequisites, cost, timeout, owner, maturity, product-check flag, and freshness key."** It establishes the stable gate identities that the *next* rows — `fsgg route` / `fsgg ship` and the route/audit JSON — select, explain, and enforce. The field set is fixed by the design's **Gate identities** table (`docs/initial-design.md`).

## Overview

The Phase-2 rows so far have produced the *facts* a gate needs: F014 typed the `.fsgg` capability/policy/tooling declarations, F015 routed each path to a capability domain, F016 sensed which paths changed, and F017 flagged unknown governed paths. None of them produced a **gate** — a stable, named unit of governance that a route can *select*, an evidence record can *attach to*, and an audit can *explain*. This feature defines that unit.

It takes the declared, **already-validated** F014 typed facts (the declared `Check`s, plus the `tooling.yml` commands they reference) and assembles a **typed gate registry**: a deterministically ordered set of `Gate` entries, each carrying a stable `GateId` and the metadata the design's *Gate identities* table fixes — owning **domain**, human **description**, **prerequisites**, **cost**, **timeout**, **owner**, **maturity**, a **product-check** flag, and a **freshness key**. The registry is the single source of stable gate identity that every later row references: routes name selected gates by `GateId`, evidence records key on the gate's freshness key, and audit JSON explains a verdict gate-by-gate.

Assembly is **total**: it consumes `Valid TypedFacts`, which F014 has already proven to have unique check ids and resolved cross-references (`Check.Command` resolves even when `tooling.yml` is absent). The registry therefore does not re-validate those invariants — it **preserves** them by construction (a stable, injective `GateId` per declared check) and the feature **proves** the preservation with property tests over arbitrary valid facts. There is no half-built registry and no failure mode of its own beyond what its validated inputs already guarantee.

This feature is **pure**, like F014/F015/F017: it performs no I/O, senses no git (that is F016), parses no YAML (that is F014), routes no globs (that is F015), and runs no gate. It consumes already-typed facts and yields a typed, deterministic registry value: identical facts produce a byte-identical registry.

This feature stops at the **typed registry**. It does **not** select gates for a route, run or execute any gate/check/command, compute base or effective severity, apply profile/mode/maturity enforcement, compute evidence freshness or cache reuse, decide a ship verdict, or emit `.fsgg/gates.json`, route/audit JSON, or any CLI command. Those are the remaining Phase-2 rows and Phase 5/11 that *consume* this registry. **Gate-to-gate prerequisite declaration and product-domain tagging are not in F014's MVP schema**; deriving a gate dependency graph from them is deferred to Phase 10 (capability/product-adapter expansion) — this row carries the prerequisite and product-check *fields* and populates them from the signals F014 already declares (see Assumptions).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Give every declared capability check a stable gate identity (Priority: P1)

A project declares capability checks in `.fsgg/capabilities.yml` (typed by F014). For routing, evidence, and audit to refer to "the check that must pass on this boundary" consistently across runs, machines, and tools, each declared check needs a **stable gate identity** carrying its governance metadata. The maintainer (and every downstream row) needs a registry that turns the declared checks into typed `Gate` entries — each with a stable `GateId`, its owning domain, a human description, cost, timeout, owner, and maturity — so a route can name a selected gate by id and an audit can explain it without re-deriving it from raw facts.

**Why this priority**: This is the core value of the row and the foundation every later Phase-2 row stands on — there is no `fsgg route` selected-gate list, no evidence-to-gate attachment, and no audit gate explanation without stable gate ids. It is the MVP: independently valuable even before product-check/freshness enrichment or dependency ordering are layered on.

**Independent Test**: With fixture `TypedFacts` declaring two domains and three checks (one referencing a declared `tooling.yml` command), assemble the registry; assert exactly three gates are produced, each with a stable `GateId` derived deterministically from the declared check, carrying that check's domain, owner, cost, maturity, a timeout (from the referenced command or the documented default), and a non-empty description — and that assembling twice yields identical ids.

**Acceptance Scenarios**:

1. **Given** facts declaring N capability checks, **When** the registry is assembled, **Then** exactly one gate is produced per declared check, each carrying a stable `GateId`, its owning domain, a human-readable description, cost, timeout, owner, and maturity.
2. **Given** the same facts assembled on two occasions, **When** the registries are compared, **Then** every gate's `GateId` and metadata are identical — gate identity is a deterministic function of the declared facts, not of assembly order or run.
3. **Given** facts declaring no checks, **When** the registry is assembled, **Then** the registry is empty and that is a successful result, not an error.

---

### User Story 2 - A trustworthy, internally-consistent registry (Priority: P1)

A registry is a contract every later row trusts: routing, evidence freshness, and audit all key on `GateId`. The maintainer needs that contract to be **internally consistent by construction** — every gate id unique, every prerequisite reference resolvable — so no downstream row can be silently misled by a collision or a dangling reference. Because the registry is assembled from `Valid TypedFacts` (which F014 has already proven free of duplicate ids and dangling cross-references), this consistency is a **guarantee the registry preserves**, not an error it must catch after the fact.

**Why this priority**: Co-equal P1 with Story 1 — the row is the pairing of "assign stable gate identities" with "and guarantee they form a consistent contract." A `GateId` derivation that could collide, or a prerequisite that could dangle, would let a silent inconsistency propagate into every downstream gate decision. The two together are the MVP; the assembler alone is not safe to build on. (Note: a *re-validation/diagnostics* layer is unnecessary here precisely because F014 already validated the facts — re-checking already-proven invariants would be dead machinery; the guarantee is established by construction and verified by property tests.)

**Independent Test**: Over arbitrary valid `TypedFacts` (property-based), assert: every assembled gate has a unique `GateId`; every prerequisite reference resolves to a declared command; assembly always succeeds (never throws, never produces a partial registry); and gate count equals the declared check count.

**Acceptance Scenarios**:

1. **Given** any valid facts with N distinct declared checks, **When** the registry is assembled, **Then** the registry contains N gates with N distinct `GateId`s — the `GateId` derivation is injective over distinct checks, so no two gates collide and none is silently dropped or merged.
2. **Given** a gate whose check declares a command prerequisite, **When** the registry is assembled, **Then** that prerequisite resolves to a declared `tooling.yml` command (F014 guarantees the reference; the registry preserves it) — no prerequisite dangles.
3. **Given** any valid facts, **When** the registry is assembled, **Then** assembly succeeds with no error and no partial result — the feature has no failure mode of its own beyond what its validated inputs guarantee.

---

### User Story 3 - Deterministic, explainable registry (Priority: P2)

A maintainer, CI, the later route/audit JSON, and the generated `.fsgg/gates.json` view all need the registry to be stable and self-describing: the same facts always produce the same gates in the same order, and every gate says — without consulting raw YAML — what it governs, what it costs, who owns it, and what it depends on.

**Why this priority**: Determinism and explainability are stated design goals (deterministic JSON, generated gate registry, readable diagnostics) and a precondition for the byte-stable `gates.json` and route/audit JSON that consume the registry. P2: the gates (Stories 1–2) must exist before their ordering and self-description matter, but a non-deterministic or opaque registry is unusable downstream.

**Independent Test**: Assemble the registry twice over the same facts, and once with the declared checks and commands reordered; assert the gate lists are byte-for-byte identical, including order; assert each gate's fields name its domain, owner, cost, timeout, maturity, and prerequisites using declared ids only.

**Acceptance Scenarios**:

1. **Given** identical facts, **When** the registry is assembled twice, **Then** the gate list is byte-for-byte identical, including ordering of every entry.
2. **Given** the declared checks and commands presented in a different order, **When** the registry is assembled, **Then** the gate list is unchanged (ordering depends on a documented sort key — `GateId` — not on declaration order).
3. **Given** any produced gate, **When** its fields are read, **Then** they identify the gate's domain, owner, cost, timeout class, maturity, and prerequisites using only declared domain/owner/command ids — no raw YAML, host paths, or product vocabulary beyond declared ids.

---

### User Story 4 - Mark product-check gates and carry freshness keys (Priority: P2)

Some gates validate *generated consumers* (packages, docs, generated products) rather than the project's own sources, and every gate has a defined set of **inputs that decide whether prior evidence can be reused**. A later route needs to know which gates are product checks (to scope a generated-product run), and a later freshness computation needs each gate's **freshness key** (the declared input set) — but neither decision is made here. The maintainer needs each gate to carry a **product-check** flag and a **freshness key** so the later rows have the data, without this feature itself computing freshness, caching, or running a product check.

**Why this priority**: The row title names "product-check flag and freshness key" explicitly, but they are *carried* metadata that only later rows act on. P2: the core registry (Stories 1–2) is correct without them; they are additive fields that must be present and deterministic for Phase 11 (cost/cache) and the generated-product expansion to consume — and this feature must hold the line of *declaring* the freshness key without *computing* freshness.

**Independent Test**: Assemble a registry from facts in which one check declares the `Release` environment class and another declares an ordinary (`Local`/`Ci`) class; assert the release-environment gate carries `productCheck = true` and the other `false`, and that every gate carries a non-empty, deterministic freshness key naming declared inputs — while asserting the feature reads no clock and computes no freshness verdict.

**Acceptance Scenarios**:

1. **Given** a declared check whose environment class marks it a release/generated-consumer check, **When** its gate is assembled, **Then** the gate's product-check flag is set, and an ordinary-source gate's flag is not.
2. **Given** any assembled gate, **When** its freshness key is read, **Then** it names the declared inputs used to decide evidence reuse (check identity, domain, cost, environment class, command) deterministically, and the feature itself computes no freshness verdict, caches nothing, and reads no clock.
3. **Given** the same facts, **When** freshness keys are assembled twice, **Then** each gate's freshness key is byte-identical across runs.

---

### User Story 5 - Deterministic, dependency-respecting gate order (Priority: P3)

A later route runs a selected subset of gates; an audit explains them in a sensible order. The maintainer (and the later route/audit rows) needs the registry to expose gates in a **single deterministic order** so downstream consumers do not each re-derive it, and so that when gate-to-gate prerequisites become declarable, the order respects them (a gate after the gates it depends on).

**Why this priority**: A stable order is an additive convenience. P3: the registry is correct and usable without it (consumers could sort themselves), but one stable order avoids drift between route and audit. In this MVP, F014 declares no gate-to-gate prerequisites, so the dependency-respecting order reduces to the deterministic `GateId` sort; the richer topological ordering (and the cycle handling it would need) is deferred together with the gate-to-gate prerequisite declaration to Phase 10.

**Independent Test**: Assemble a registry from several checks across domains; assert the exposed gate order is the deterministic `GateId` ordinal sort, stable across runs and unchanged when the inputs are reordered.

**Acceptance Scenarios**:

1. **Given** gates with only declared command (fact) prerequisites, **When** the registry's order is read, **Then** the gates are in the deterministic `GateId` ordinal order, stable across runs.
2. **Given** the same facts with checks/commands reordered, **When** the order is read, **Then** the order is unchanged.
3. **Given** a future schema that declares gate-to-gate prerequisites, **When** ordering is requested, **Then** the documented extension point is a topological order that places every gate after the gates it depends on (deferred; explicitly out of this MVP's scope).

---

### Edge Cases

- **Empty input**: No declared checks yields an empty registry — a successful result, never an error and never a fabricated gate.
- **A check with no command**: The gate's prerequisite list is empty (no declared command to require); an empty prerequisite set is valid, not an inconsistency. The gate's timeout falls back to the documented default class.
- **A check referencing a command**: The gate carries a `RequiresCommand` prerequisite for that command and takes the command's declared timeout; F014 has already guaranteed the command resolves, so the prerequisite never dangles.
- **Two checks in different domains with the same check id**: F014 rejects duplicate check ids at validation, so this cannot reach the registry; were it ever to (non-F014 facts), the domain-qualified `GateId` (`domain:checkId`) keeps the two gates distinct rather than colliding.
- **A surface declared with no governing check**: Declaring a protected/release surface does not manufacture a gate; a gate requires an actual declared check. (Whether an unguarded protected surface is itself a *finding* is F017's concern, not this registry's.)
- **Timeout / freshness key when the source omits an explicit value**: A gate always carries a timeout class and a freshness key; when no command (hence no explicit timeout) is referenced, a documented default class is used (never unbounded or zero), and the freshness key is derived from the always-present declared identity inputs.
- **Maturity present but enforcement absent**: A gate carries its declared maturity verbatim; it does NOT translate maturity into a blocking/advisory decision (that is Phase 5).
- **Product-check with no declared product signal**: In the MVP, product-check is derived from the declared environment class; a check with no release/product environment class is not a product check. Richer product-domain tagging is Phase 10.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST produce a typed **gate registry** — a structured set of `Gate` entries (not prose) — assembled from the declared, already-validated F014 facts (`Valid TypedFacts`: the `Capabilities.Checks` and the `Tooling.Commands` they reference). The registry MUST be consumable directly by the later route selection, route/audit JSON, and `.fsgg/gates.json` view.
- **FR-002**: Each gate MUST carry a stable **`GateId`** and the metadata fixed by the design's *Gate identities* table: owning **domain**, human-readable **description**, **prerequisites**, **cost**, **timeout**, **owner**, **maturity**, a **product-check** flag, and a **freshness key**. No field in that table may be silently omitted.
- **FR-003**: Each declared capability check MUST yield exactly one gate, and the gate's `GateId` MUST be a **deterministic, injective function of the declared facts** (the domain-qualified check id, `domain:checkId`), stable across runs, machines, and assembly order — never positional, time-derived, or randomly generated.
- **FR-004**: Gate metadata MUST be expressed using the **declared F014 newtypes** — `DomainId`, `Owner`, `Cost`, `Maturity`, `TimeoutLimit`, `CommandId`, `EnvironmentClass`, `CheckId` — and declared identities; never absolute host paths, raw YAML, or free-form strings standing in for declared ids.
- **FR-005**: The `GateId` derivation MUST be **injective over distinct declared checks** so the registry contains exactly one gate per check with no two gates colliding and none silently dropped or merged. The feature MUST NOT re-validate uniqueness of the underlying check ids (F014 already guarantees it); it MUST **preserve** that uniqueness by construction.
- **FR-006**: A gate's **prerequisites** MUST be expressed as a typed list of declared references. In this MVP the only source is the check's declared command (`RequiresCommand`), which F014 has already proven resolvable; the registry MUST preserve that resolvability (no dangling prerequisite). Gate-to-gate prerequisites are not declarable in F014's MVP schema and MUST be deferred (the typed field and its future extension point are defined; no gate-to-gate edges are produced in this MVP).
- **FR-007**: Registry assembly MUST be **total**: over any `Valid TypedFacts` it MUST succeed with no error and no partial result. Because the MVP produces no gate-to-gate prerequisite edges, the gate dependency graph is trivially acyclic; cycle handling for declared gate-to-gate prerequisites is deferred to Phase 10 together with that declaration (documented extension point, FR-006).
- **FR-008**: A gate's **product-check** flag MUST be set iff the declared facts mark the gate's check as validating a generated-consumer / product surface. In this MVP that signal is the check's declared environment class being a release/product class; an ordinary-source gate's flag MUST be unset. Richer product-domain/surface tagging is deferred to Phase 10 (documented).
- **FR-009**: A gate's **freshness key** MUST name the declared inputs used to decide whether prior evidence can be reused (check identity, domain, cost, environment class, command). The feature MUST **carry** the freshness key but MUST NOT compute freshness, compare instants, cache evidence, or read any clock — freshness evaluation is the kernel's `Freshness`/Phase-11 concern.
- **FR-010**: A gate MUST always carry a **timeout** (a `TimeoutLimit`). When the gate's check references a declared command, the command's declared timeout MUST be used; otherwise a documented default `TimeoutLimit` MUST be used. The feature MUST NOT produce an unbounded or zero timeout and MUST NOT itself enforce or measure any timeout.
- **FR-011**: Every collection the feature emits — gates and prerequisite lists — MUST be in a **deterministic, documented order** (`GateId` ordinal), so identical facts yield a **byte-identical** registry, unchanged under re-ordering of the declared checks or commands.
- **FR-012**: Beyond the byte-stable ordering FR-011 mandates, the registry MUST expose a **single, canonical** gate order so consumers (route, audit, `.fsgg/gates.json`) do not each re-derive one. In this MVP that canonical order **is** the `GateId` ordinal sort of FR-011; FR-012's distinct obligation is to fix the documented extension point for declared gate-to-gate prerequisites — a topological order placing every gate after the gates it transitively depends on (deferred to Phase 10).
- **FR-013**: The feature MUST be **pure and total**: no I/O, no git, no clock, never throwing. It MUST consume already-typed F014 facts and MUST NOT re-parse `.fsgg` YAML, re-validate the capability catalog, re-route globs, or sense git.
- **FR-014**: An **empty registry** (no declared checks) MUST be a valid, successful outcome distinct from an error.
- **FR-015**: The feature MUST NOT **select** gates for a route, run or execute any gate/check/command, compute base or effective severity, apply profile/mode/maturity enforcement, compute evidence freshness or cache reuse, decide a ship verdict, or emit `.fsgg/gates.json`, route/audit JSON, or any CLI command. It produces the typed registry those later rows consume.
- **FR-016**: The feature MUST require only the already-typed F014 facts as input. It MUST NOT require any FS.GG package to be installed in the repository under inspection (governance inspects a project; a project never depends on governance), and it MUST live in the product-neutral Governance layer — the kernel never sees this gate-registry vocabulary.

### Key Entities *(include if feature involves data)*

- **Gate**: The typed identity of one unit of governance — a stable `GateId`, owning domain, human description, prerequisites, cost, timeout, owner, maturity, product-check flag, and freshness key. A pure, deterministic value.
- **GateId**: The stable machine id of a gate (the domain-qualified check id), used by route, evidence, and audit JSON to refer to the gate across runs and tools. A deterministic, injective function of the declared facts.
- **Gate registry**: The deterministically ordered set of gates assembled from the declared facts; the single source of stable gate identity for the later Phase-2 and Phase 5/11 rows.
- **Gate prerequisite**: A typed reference to something required before the gate runs. In this MVP, a `RequiresCommand` reference to a declared command; gate-to-gate prerequisites are a deferred extension point.
- **Freshness key (carried)**: The declared set of inputs that a later freshness computation will use to decide evidence reuse; carried by each gate but not evaluated here.
- **Capability check (consumed)**: The declared F014 `Check` (id, domain, command, owner, cost, environment, maturity) this feature reads and turns into a gate, but does not recompute or re-validate.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For fixture facts declaring N checks across known domains, the assembled registry contains exactly N gates, each with a stable `GateId` and the full metadata field set (domain, description, prerequisites, cost, timeout, owner, maturity, product-check flag, freshness key), and assembling twice yields byte-identical gate ids.
- **SC-002**: Over arbitrary valid facts, every gate has a unique `GateId`, every prerequisite resolves to a declared command, gate count equals declared check count, and assembly never throws and never yields a partial registry.
- **SC-003**: Assembling the registry twice over identical facts, and once with the declared checks and commands reordered, yields byte-for-byte identical gate lists, including order.
- **SC-004**: A gate whose check declares a release/product environment class carries `productCheck = true` while an ordinary-source gate carries `false`, and every gate carries a non-empty, deterministic freshness key — with no clock read and no freshness verdict computed.
- **SC-005**: Every gate carries a bounded timeout (the referenced command's declared timeout or a documented default class), never unbounded or zero, and the feature itself enforces or measures no timeout.
- **SC-006**: The registry's gate order is the deterministic `GateId` ordinal sort, stable across runs and unchanged under input reordering.
- **SC-007**: The feature performs no I/O and never throws on any valid input, reaches no git or network, computes no freshness/severity/enforcement/verdict, emits no JSON file or CLI output, and runs without any FS.GG package installed in the repository under inspection.

## Assumptions

- This feature is the Phase-2 *Gate identities* row: it consumes the F014 `Valid TypedFacts` (declared checks + the tooling commands they reference) and produces a typed gate registry only — establishing the stable gate identities that the remaining Phase-2 rows (`fsgg route` / `fsgg ship`, route/audit JSON, `.fsgg/gates.json`) and Phase 5 (enforcement) / Phase 11 (cost & cache) consume.
- The gate metadata field set is fixed by the design's *Gate identities* table (`docs/initial-design.md`): `id`, `domain`, `description`, `prerequisites`, `cost`, `timeout`, `owner`, `maturity`, `productCheck`, `freshnessKey`. The closed `Cost`, `Maturity`, `Owner`, `DomainId`, `TimeoutLimit`, `EnvironmentClass`, and `CommandId` vocabularies are reused from F014 (`FS.GG.Governance.Config`) rather than re-introduced.
- **Reconciled at plan time (maintainer-confirmed):** F014's MVP capability schema declares neither gate-to-gate prerequisites nor a product-check flag, and `Valid TypedFacts` is already validated (unique ids, resolved cross-references). Three consequences shape this row: (1) the registry **does not re-validate** already-proven invariants and **emits no diagnostics** — assembly is total and consistency is preserved by construction and verified by property tests (US2); (2) **prerequisites** are limited to the declared command reference (`RequiresCommand`); gate-to-gate prerequisites and the topological ordering / cycle handling they would need are **deferred to Phase 10**; (3) **product-check** is derived in the MVP from the declared `EnvironmentClass` being a release/product class, with richer product-domain tagging deferred to Phase 10.
- One declared capability `Check` maps to one `Gate`; `GateId = domain:checkId` (domain-qualified, deterministic, injective). A surface alone does not manufacture a gate.
- This feature is **pure and total**, like F014/F015/F017: no I/O, no git, no clock, deterministic. The impure sensing is F016; the glob routing is F015; the YAML validation is F014. This feature only assembles their typed outputs.
- The freshness key is **declared/carried** here; computing freshness (comparing recorded vs covered instants) is the kernel's existing `Freshness` module and Phase 11, and is out of scope. Effective severity / profile-mode-maturity enforcement is Phase 5; gate *selection*, gate *execution*, ship verdicts, `.fsgg/gates.json` emission, route/audit JSON, and any CLI command are later rows — none are implemented here.
- The model and assembler live in the product-neutral Governance layer (consuming `FS.GG.Governance.Config` facts only — no Routing dependency, since gate *selection* by route is a later row); the exact assembly placement (a new sibling library) is settled at plan time. The kernel never sees gates or the registry vocabulary.
- A single governed root / single capability catalog is assumed, consistent with F014–F017; multi-catalog scoping is out of scope.
- The supported registry surface is the MVP named by the plan row: stable gate identities with the fixed metadata set, uniqueness preserved by construction, deterministic ordering, and a documented Phase-10 extension point for gate-to-gate prerequisites and richer product-check derivation.
