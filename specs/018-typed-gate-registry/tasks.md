---
description: "Task list for F018 - 018-typed-gate-registry: project the already-validated F014 typed facts into a typed gate registry — a single pure, total `buildRegistry : TypedFacts -> GateRegistry` that gives every declared capability check a stable, injective `GateId` (`domain:checkId`) carrying the design's *Gate identities* field set (domain, description, prerequisites, cost, timeout, owner, maturity, product-check, freshness key), deterministically `GateId`-ordinal sorted and byte-identical for identical input, with NO diagnostics layer (F014's guarantees are preserved by construction and proven by FsCheck)."
---

# Tasks: Typed Gate Registry

**Feature branch**: `018-typed-gate-registry` (active spec; git branch currently `main`)
**Spec**: [`specs/018-typed-gate-registry/spec.md`](./spec.md)
**Plan**: [`specs/018-typed-gate-registry/plan.md`](./plan.md)

**Input**: Design documents from `/specs/018-typed-gate-registry/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Model.fsi](./contracts/Model.fsi), [contracts/Gates.fsi](./contracts/Gates.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; new public `.fsi`s; new surface baseline). Credible evidence is **public-surface** testing only: `Gates.buildRegistry` exercised over **real in-memory `TypedFacts`** — the genuine values a downstream caller (`route`/`ship`, route/audit JSON, the `gates.json` emitter) passes, never private helpers and never mocks (Principle V, research D10). No network, git, agent, clock, or filesystem is reachable from this feature, so **no synthetic evidence is anticipated** — every case is reachable from real `Valid TypedFacts`, a direct dividend of research D4 (no never-triggered diagnostic branch to exercise). Any literal standing in for an un-derivable case carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — `FS.GG.Governance.Config` is referenced as-is (its existing public types suffice); the only new baseline is `surface/FS.GG.Governance.Gates.surface.txt`.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** — this feature is a pure, total projection of already-typed inputs (FR-013): no I/O, no git sensing, no clock, no multi-step state, no retries. It is exactly the "single rule evaluation / pure function" case Principle IV explicitly exempts from MVU ceremony (plan Constitution Check; the same call F015 `route` and F017 `findUnknownGovernedPaths` made). The boundary is one pure function `buildRegistry : TypedFacts -> GateRegistry` — no `Model`/`Msg`/`Effect`/`update`/interpreter. The pure/edge separation the principle protects is satisfied trivially: everything is pure.

**No-diagnostics minimum (research D4, US2)**: `buildRegistry` consumes `Valid TypedFacts`, which F014's `Schema.validate` has already proven free of duplicate check ids and dangling cross-references (`Check.Command` resolves even when `tooling.yml` is absent). The registry therefore **re-validates nothing and emits no diagnostic** — there is no diagnostic channel and no failure mode. Internal consistency (injective `GateId`, resolvable prerequisites, gate-count parity, total assembly) is **preserved by construction** and **proven by FsCheck** (US2), never caught after the fact. A re-validation/diagnostics layer is a dead branch no valid input can reach — refusing it is the Principle III simplicity choice, not a gap.

**Determinism minimums (FR-011/FR-012, SC-003/SC-006)**: `GateRegistry.Gates` is sorted by `String.CompareOrdinal (gateIdValue Id)`; `Gate.Prerequisites` is at most one element in the MVP (ordinal-stable if extended). Re-ordering the declared `Capabilities.Checks` OR the `Tooling.Commands` leaves every output byte-identical. No wall-clock, environment, or host-path value enters the result.

**Scope-guard minimums (FR-013/FR-015/FR-016, SC-007)**: no gate *selection* for a route, no gate/check/command *execution*, no base/effective severity, no profile/mode/maturity *enforcement*, no evidence freshness/cache reuse, no ship verdict, no `.fsgg/gates.json`/route/audit JSON, no CLI command. Gates carry only declared F014 newtypes (`DomainId`, `Owner`, `Cost`, `Maturity`, `TimeoutLimit`, `CommandId`, `EnvironmentClass`, `CheckId`), a composed description, a bounded timeout, a product-check bool, and a carried freshness key — no raw YAML, host paths, or timestamps. The library lives in the product-neutral Governance layer and requires no FS.GG package installed in any inspected repo; the kernel never sees the gate-registry vocabulary. Takes **no Routing edge** (research D1).

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US5]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional registry library `FS.GG.Governance.Gates`, its test project, the public contracts (copied verbatim), the in-memory fixtures, the prelude sketch, and the readiness note. **No new third-party dependency** — the library references `FS.GG.Governance.Config` **only** (no Routing edge, research D1); its own code is BCL + FSharp.Core (the transitive YamlDotNet edge arrives via Config and is unused here).

- [X] T001 Create `src/FS.GG.Governance.Gates/FS.GG.Governance.Gates.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.Gates`, with exactly one `<ProjectReference>` — `../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj` — and **no** `<PackageReference>` and **no** Routing reference (research D1). Compile order `Model.fs` → `Gates.fs`.
- [X] T002 Copy `specs/018-typed-gate-registry/contracts/Model.fsi` → `src/FS.GG.Governance.Gates/Model.fsi` and `contracts/Gates.fsi` → `src/FS.GG.Governance.Gates/Gates.fsi` verbatim as the curated public surface (Principle II — these `.fsi`s are the SOLE public surface; the matching `.fs` files carry no top-level access modifiers).
- [X] T003 Add `failwith "F018"` stub bodies in `src/FS.GG.Governance.Gates/Model.fs` and `src/FS.GG.Governance.Gates/Gates.fs` that satisfy the `.fsi` contracts, in the fsproj compile order `Model.fs` → `Gates.fs`, so the library compiles against the contracts before any real logic lands (Principle I).
- [X] T004 Create `tests/FS.GG.Governance.Gates.Tests/FS.GG.Governance.Gates.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/VSTest packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.Gates` and `src/FS.GG.Governance.Config` (the tests build real in-memory `TypedFacts` from the Config newtypes).
- [X] T005 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.Gates.Tests/`: `Support.fs`, `GateBuildTests.fs`, `RegistryInvariantTests.fs`, `DeterminismTests.fs`, `MetadataTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T006 Add `src/FS.GG.Governance.Gates` and `tests/FS.GG.Governance.Gates.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Implement the fixture builders in `tests/FS.GG.Governance.Gates.Tests/Support.fs` over **real** Config values (no mocks): (a) a `check` builder producing a real `Config.Model.Check` from `(domain, checkId, command, owner, cost, environment, maturity)` with sensible inert defaults so a test varies only the field under test; (b) a `command` builder producing a real `Config.Model.CommandSpec` from `(commandId, timeout)`; (c) a `factsOf : Check list -> CommandSpec list -> TypedFacts` assembler that sets `Project` to an inert default and `Policy = None`, populates `Capabilities.Checks`, and wraps the commands as `Tooling = Some { ... Commands = commands ... }` — the genuine `Valid TypedFacts` a downstream caller passes (research D10); (d) a `factsNoTooling : Check list -> TypedFacts` variant that sets `Tooling = None` (the absent-`tooling.yml` case, since `TypedFacts.Tooling` is `ToolingFacts option`) so the command index is empty and command lookups fall back to `defaultTimeout` (covers C1/I1). These are REAL downstream inputs, not fakes.
- [X] T008 [P] Extend `scripts/prelude.fsx` with an F018 design sketch that `#r`s the built `FS.GG.Governance.Gates` (+ `Config`) assemblies, opens the namespaces, builds a small in-memory `TypedFacts` declaring two domains and three checks (one check referencing a declared `tooling.yml` command with timeout 600s and running in the `Release` environment; the others command-less and `Local`), calls `Gates.buildRegistry facts`, and prints each gate's `gateIdValue`/cost/timeout/product/prereqs — recording the intended facts→registry flow before real bodies land (Principle I; mirrors [quickstart.md](./quickstart.md) §FSI smoke check).
- [X] T009 [P] Create `specs/018-typed-gate-registry/readiness/README.md` listing the required FSI transcripts (a three-check registry showing `GateId` order + per-gate metadata; a command vs default-timeout split; a `Release` vs `Local`/`Ci` product-check split; a twice-identical + reordered determinism run; an empty-facts → empty-registry run; a `Tooling = None` (absent `tooling.yml`) run where a command-referencing check still takes `defaultTimeout`) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.Gates` and `dotnet test tests/FS.GG.Governance.Gates.Tests` compile against stubs; the solution lists the two new projects; the Config reference resolves; the fixtures build real `TypedFacts`.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the gate-domain model, the `defaultTimeout` constant, the command-timeout index, the per-check `GateId` derivation, the deterministic `GateId`-ordinal sort, and the `buildRegistry` projection skeleton — everything the stories specialize. **No user-story work begins until this phase is complete.**

- [X] T010 Implement `src/FS.GG.Governance.Gates/Model.fs` exactly matching `Model.fsi`: the `GateId` newtype (`GateId of string`), the closed `GatePrerequisite` (`RequiresCommand of command: CommandId`), the `FreshnessKey` record (`{ Check; Domain; Cost; Environment; Command }`), the `Gate` record (the full *Gate identities* field set: `Id`/`Domain`/`Description`/`Prerequisites`/`Cost`/`Timeout`/`Owner`/`Maturity`/`ProductCheck`/`FreshnessKey`), the `GateRegistry` record (`{ Gates }`), and the total `gateIdValue` (`GateId s` → `s`). Reuses the `Config.Model` newtypes (`DomainId`, `Owner`, `Cost`, `Maturity`, `TimeoutLimit`, `CommandId`, `EnvironmentClass`, `CheckId`) — does NOT redefine them.
- [X] T011 Implement the projection primitives in `src/FS.GG.Governance.Gates/Gates.fs`: (a) `defaultTimeout = TimeoutLimit 300` (five minutes — the documented default when a check references no command, never zero or unbounded, FR-010/SC-005); (b) a private command-timeout **index** `Map<CommandId, TimeoutLimit>` built once from `facts.Tooling` — which is a `ToolingFacts option`, so unwrap with `facts.Tooling |> Option.map (fun t -> t.Commands) |> Option.defaultValue []` (an absent `tooling.yml`, `Tooling = None`, yields an empty index and command lookups fall back to `defaultTimeout`) — mapping each `CommandSpec.Id → CommandSpec.Timeout`, so per-check timeout resolution is a single lookup (O(checks) with an O(commands) index build); (c) a private `gateIdOf : Check -> GateId` deriving `GateId "<domainText>:<checkIdText>"` from `Check.Domain`/`Check.Id` — deterministic and **injective** over distinct checks (FR-003/FR-005). Disclose any `mutable` accumulator at its use site (Principle III).
- [X] T012 Implement the `buildRegistry` projection skeleton in `src/FS.GG.Governance.Gates/Gates.fs`: read only `facts.Capabilities.Checks` (the gates' source) and `facts.Tooling` (a `ToolingFacts option`, consumed via the T011 index — `None` ⇒ empty index); map each declared `Check` through a `projectCheck` placeholder producing one `Gate` (fields filled by the stories); sort the resulting list with `List.sortWith (fun a b -> String.CompareOrdinal (gateIdValue a.Id) (gateIdValue b.Id))` (FR-011/FR-012, the single deterministic order); return `{ Gates = sorted }`. PURE and TOTAL — never throws, re-validates nothing, emits no diagnostic (research D4); an empty `Checks` yields `{ Gates = [] }`, a valid success (FR-007/FR-014). At this stage `projectCheck` may fill only a minimal field set; the stories complete the projection.

**Checkpoint**: the library builds with the real Model + `defaultTimeout` + command index + `gateIdOf` + the sort + the projection skeleton; `buildRegistry` over empty facts returns an empty-but-successful `GateRegistry`; `buildRegistry` over non-empty facts returns one gate per check in `GateId` ordinal order; the surface compiles against the `.fsi`s.

---

## Phase 3: User Story 1 - Give every declared capability check a stable gate identity (Priority: P1) 🎯 MVP

**Goal**: each declared `Check` becomes exactly one `Gate` carrying a stable `GateId` (`domain:checkId`), its owning domain, a non-empty human description, cost, owner, maturity, a bounded timeout, and (for a check with a command) a `RequiresCommand` prerequisite; assembling twice yields identical ids; no checks → empty registry.

**Independent Test**: fixture `TypedFacts` declaring two domains and three checks (one referencing a declared `tooling.yml` command); assemble the registry; assert exactly three gates, each with a stable `GateId` (`domain:checkId`) carrying that check's domain, owner, cost, maturity, a timeout, and a non-empty description; assert assembling twice yields identical ids; assert empty facts → empty registry.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T013 [P] [US1] In `tests/FS.GG.Governance.Gates.Tests/GateBuildTests.fs`, add per-check projection tests over real `factsOf` fixtures: N declared checks → exactly N gates, one per check; each gate's `Id = GateId "<domain>:<checkId>"`, `Domain`/`Cost`/`Owner`/`Maturity` equal the declared `Check` fields verbatim, `Description` is non-empty, `Timeout` is present, and `Prerequisites = [RequiresCommand c]` when `Check.Command = Some c` else `[]` (US1 AS1, **SC-001**). Add a determinism-of-ids assertion: assembling the same facts twice yields byte-identical `GateId`s (US1 AS2). Add the empty case: facts with no declared checks → `{ Gates = [] }`, a successful result, not an error (US1 AS3, FR-014).

### Implementation for User Story 1

- [X] T014 [US1] Implement the core field projection in `projectCheck` (`src/FS.GG.Governance.Gates/Gates.fs`): `Id = gateIdOf check` (T011), `Domain = check.Domain`, `Cost = check.Cost`, `Owner = check.Owner`, `Maturity = check.Maturity` (verbatim — maturity is NOT translated to any blocking/advisory decision, that is Phase 5), `Prerequisites = [RequiresCommand c]` when `check.Command = Some c` else `[]` (the declared fact prerequisite; gate-to-gate prereqs are the deferred Phase-10 extension point, research D5), and `Timeout = defaultTimeout` for now (the command-timeout lookup lands in US4/T023). Product-check and freshness-key fields are filled in US4 — leave deterministic placeholders only if the record will not compile otherwise, and note it.
- [X] T015 [US1] Implement the `Description` composer in `src/FS.GG.Governance.Gates/Gates.fs`: a human-readable purpose string composed from the declared ids only (e.g. naming the check id and owning domain) — no raw YAML, host paths, timestamps, or product vocabulary beyond declared ids (FR-004, SC-004). Deterministic for identical input.

**Checkpoint**: every declared check has a stable, deterministic `GateId` and a fully-populated core gate (domain/description/cost/owner/maturity/prereqs/timeout-default); empty facts → empty registry; twice-identical ids. US1 is the MVP foundation.

---

## Phase 4: User Story 2 - A trustworthy, internally-consistent registry (Priority: P1)

**Goal**: prove — over arbitrary valid facts — that the registry is internally consistent by construction: every `GateId` unique, gate count = declared check count, every `RequiresCommand` resolves to a declared command, and assembly always succeeds (never throws, never partial). No diagnostics layer — F014's guarantees are *preserved and proven*, not *re-checked* (research D4).

**Independent Test**: over arbitrary valid `CapabilityFacts` (property-based), assert every assembled gate has a unique `GateId`; every prerequisite reference resolves to a declared command; assembly always succeeds; gate count equals declared check count.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T016 [P] [US2] In `tests/FS.GG.Governance.Gates.Tests/RegistryInvariantTests.fs`, add FsCheck properties over generated **valid** `TypedFacts` (distinct check ids per the F014 contract; commands generated so every `Check.Command`, when `Some`, names a declared command — preserving F014's resolved cross-reference): (1) all assembled `GateId`s are **distinct** — the derivation is injective, none dropped/merged (US2 AS1, **SC-002**); (2) gate **count = declared check count** (parity); (3) every `RequiresCommand c` in every gate resolves to a declared `Tooling.Commands` id — no dangling prerequisite (US2 AS2); (4) `buildRegistry` **never throws and never yields a partial result** over any generated valid facts (US2 AS3, totality). The generators model `Valid TypedFacts`; the suite asserts the preserved guarantees, it does not re-introduce a validator.

### Implementation for User Story 2

- [X] T017 [US2] Confirm the injective `GateId` derivation (T011 `gateIdOf` — `domain:checkId` is injective because F014 guarantees check ids are unique catalog-wide and the domain qualifies them) and the total `buildRegistry` (T012 — no diagnostic channel, no partial state) satisfy the T016 properties. Fix the derivation or assembly only if a property fails; **do not add a re-validation/diagnostics branch** (research D4 — it would be dead code). Note explicitly if no change was needed beyond Foundation/US1.

**Checkpoint**: the registry is provably consistent by construction — unique ids, count parity, resolvable prerequisites, total assembly — with no diagnostics layer. US1 + US2 together are the co-equal P1 MVP pairing.

---

## Phase 5: User Story 3 - Deterministic, explainable registry (Priority: P2)

**Goal**: prove the registry is byte-identical for identical facts and unchanged under input re-ordering, and that every gate's fields name its domain, owner, cost, timeout, maturity, and prerequisites using declared ids only.

**Independent Test**: assemble the registry twice over the same facts, and once with the declared checks and commands reordered; assert the gate lists are byte-for-byte identical including order; assert each gate's fields name its domain, owner, cost, timeout, maturity, and prerequisites using declared ids only.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T018 [P] [US3] In `tests/FS.GG.Governance.Gates.Tests/DeterminismTests.fs`, add: assemble `buildRegistry` twice over identical facts → structural equality of the whole `GateRegistry`, including `Gates` order (US3 AS1, **SC-003**); an FsCheck property that permuting the order of the declared `Capabilities.Checks` AND the `Tooling.Commands` (for fixed content) yields an **identical** `GateRegistry` (the `GateId` ordinal sort makes order independent of declaration order, US3 AS2); and a vocabulary assertion over every produced gate — `Description` and every field carry only declared domain/owner/command/check ids, with **no** raw YAML, host-path separators, timestamps, or product vocabulary beyond declared ids (US3 AS3, FR-004).

### Implementation for User Story 3

- [X] T019 [US3] Confirm/refine the sort (T012) and the `Description` composer (T015) in `src/FS.GG.Governance.Gates/Gates.fs` against the T018 evidence; if the sort key, the permutation-invariance, or any field leaks non-id vocabulary, fix it here. Note explicitly if no change was needed beyond Foundation/US1.

**Checkpoint**: the registry is provably deterministic and every gate is self-describing with declared ids only. US1–US3 pass.

---

## Phase 6: User Story 4 - Mark product-check gates and carry freshness keys (Priority: P2)

**Goal**: each gate carries a `ProductCheck` flag (`true` iff `Check.Environment = Release`, the MVP heuristic) and a non-empty, deterministic `FreshnessKey` naming the declared inputs — and the referenced command's declared timeout (else `defaultTimeout`) — while the feature computes no freshness verdict, caches nothing, reads no clock.

**Independent Test**: assemble a registry from facts in which one check declares the `Release` environment and another an ordinary (`Local`/`Ci`) class; assert the release gate carries `ProductCheck = true` and the other `false`; assert every gate carries a non-empty, deterministic freshness key naming declared inputs; assert a command-referencing gate carries the command's declared timeout and a command-less gate carries `defaultTimeout`; assert no clock is read and no freshness verdict is computed.

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T020 [P] [US4] In `tests/FS.GG.Governance.Gates.Tests/MetadataTests.fs`, add: a product-check split — a `Release`-environment check → gate `ProductCheck = true`; a `Local`/`Ci` check → `false` (US4 AS1, **SC-004**); a freshness-key assertion — every gate carries a non-empty `FreshnessKey` whose fields equal the declared `{ Check; Domain; Cost; Environment; Command }`, byte-identical across two assemblies, with no clock read and no freshness verdict produced (US4 AS2/AS3, SC-004); and a timeout assertion — a gate whose check references a declared command carries that command's declared `TimeoutLimit`, a command-less gate carries `Gates.defaultTimeout` (`TimeoutLimit 300`), and every gate's timeout is bounded (never zero/unbounded) (**SC-005**, FR-010); and a `Tooling = None` case (via `factsNoTooling`, T007d) in which a check **referencing a command** still resolves to `Gates.defaultTimeout` because the command index is empty — proving the absent-`tooling.yml` fallback (C1, FR-010).

### Implementation for User Story 4

- [X] T021 [US4] Add the `ProductCheck` projection to `projectCheck` (`src/FS.GG.Governance.Gates/Gates.fs`): `ProductCheck = (check.Environment = Release)` — the only declared product signal in the MVP (research D6); richer product-domain/surface tagging is the documented Phase-10 extension. An ordinary-source gate's flag is unset.
- [X] T022 [US4] Add the `FreshnessKey` builder to `projectCheck` (`src/FS.GG.Governance.Gates/Gates.fs`): `FreshnessKey = { Check = check.Id; Domain = check.Domain; Cost = check.Cost; Environment = check.Environment; Command = check.Command }` — the declared identity inputs a later freshness/cache step will hash, **carried not evaluated** (FR-009, research D8). The feature computes no freshness verdict, compares no instants, caches nothing, and reads no clock.
- [X] T023 [US4] Wire the command-timeout lookup in `projectCheck` (`src/FS.GG.Governance.Gates/Gates.fs`), replacing the US1/T014 default-only placeholder: `Timeout = match check.Command with Some c -> (defaultTimeout |> fallback via the T011 index lookup of c) | None -> defaultTimeout` — i.e. the referenced command's declared `Timeout` from the T011 index when the check references a command **and that command is in the index**, else `defaultTimeout` — including when `Tooling = None` (empty index) or the check is command-less (FR-010, research D9). Always bounded; never enforces or measures a timeout.

**Checkpoint**: every gate carries a correct product-check flag, a deterministic declared-input freshness key, and a bounded command-or-default timeout — with no clock, no freshness verdict, no cache. US1–US4 pass.

---

## Phase 7: User Story 5 - Deterministic, dependency-respecting gate order (Priority: P3)

**Goal**: the registry exposes gates in a single deterministic order — the `GateId` ordinal sort — stable across runs and unchanged under input re-ordering; the topological order for declared gate-to-gate prerequisites is the documented Phase-10 extension point and produces no edges in this MVP.

**Independent Test**: assemble a registry from several checks across domains; assert the exposed gate order is the deterministic `GateId` ordinal sort, stable across runs and unchanged when the inputs are reordered.

### Tests for User Story 5 (write first; must FAIL before implementation)

- [X] T024 [US5] (sequential after T018 — shares `DeterminismTests.fs`, so not `[P]`) In `tests/FS.GG.Governance.Gates.Tests/DeterminismTests.fs`, add an explicit order test: from several checks across multiple domains, assert `registry.Gates |> List.map (fun g -> gateIdValue g.Id)` equals the same list sorted by `String.CompareOrdinal` — the gates are in `GateId` ordinal order (US5 AS1, **SC-006**); and assert the order is unchanged when the declared checks and commands are presented in reverse/permuted order (US5 AS2). Document in a comment that the gate dependency graph is trivially acyclic in this MVP (no gate-to-gate edges), so the order is the `GateId` sort and the topological order is the deferred Phase-10 extension point (US5 AS3, out of MVP scope).

### Implementation for User Story 5

- [X] T025 [US5] Confirm the single deterministic gate order is the `GateId` ordinal sort (T012 `List.sortWith` over `String.CompareOrdinal (gateIdValue Id)`) and that no topological pass is performed (the MVP produces no gate-to-gate prerequisite edges, so the graph is trivially acyclic — research D5/D7). Note explicitly if no change was needed beyond Foundation. The documented Phase-10 extension point (a topological order placing each gate after its dependencies) is recorded in `data-model.md` / the `Gates.fsi` doc-comment, not implemented here.

**Checkpoint**: the registry exposes one stable `GateId` order, reorder-invariant by construction; the topological extension point is documented and deferred. US1–US5 pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: the surface baseline + drift test, dependency/scope/no-leak hygiene, the quickstart run, and the README/plan legend updates.

- [X] T026 Generate `surface/FS.GG.Governance.Gates.surface.txt` from the built `FS.GG.Governance.Gates` assembly using the repo's surface-baseline convention, then add `tests/FS.GG.Governance.Gates.Tests/SurfaceDriftTests.fs` asserting the built surface matches the baseline (Principle II). Confirm the baseline contains exactly the two modules `Model` and `Gates` and nothing private.
- [X] T027 [P] In `SurfaceDriftTests.fs` (or a dedicated module), add a dependency/scope-hygiene test asserting `FS.GG.Governance.Gates` references only `FS.GG.Governance.Config` (+ FSharp.Core, + transitive unused YamlDotNet) and **not** the kernel/host/adapters/Routing/Snapshot/Findings/CLI (research D1, one-way `Gates → Config`); that no gate field carries raw YAML, host paths, timestamps, or non-id product vocabulary (FR-004/SC-004); and that no severity/enforcement/selection/execution/evidence-freshness/ship-verdict/route-audit-JSON/`gates.json`/CLI symbol is reachable — the FR-015 scope guard confirming no later-phase capability leaked in. Assert the feature reads no `.fsgg`/YAML, senses no git, reads no clock, and requires nothing installed in any inspected repo (FR-013/FR-016): it references only `Config.Model` types, not the Config `Loader`/`Schema` parsing surface.
- [X] T028 [P] Run [quickstart.md](./quickstart.md) end-to-end and record the transcripts named in `readiness/README.md` (three-check registry in `GateId` order with per-gate metadata; command vs `defaultTimeout` split; `Release` vs `Local`/`Ci` product-check split; twice-identical + reordered determinism; empty-facts → empty-registry; `Tooling = None` command-referencing check → `defaultTimeout`), plus the SC-traceability note mapping SC-001…SC-007 to the proving tests.
- [X] T029 [P] Update `README.md` to list the new optional `FS.GG.Governance.Gates` library and link the contracts ([contracts/Model.fsi](./contracts/Model.fsi), [contracts/Gates.fsi](./contracts/Gates.fsi)); flip the `docs/initial-implementation-plan.md` Phase-2 *Gate identities* row ("Define typed `GateId` metadata with prerequisites, cost, timeout, owner, maturity, product-check flag, and freshness key") to ✅, recording that it establishes the stable gate identities the remaining Phase-2 rows (`fsgg route`/`fsgg ship`, route/audit JSON, `.fsgg/gates.json`) and Phase 5/11 consume, and noting the Phase-10 deferrals (gate-to-gate prerequisites + topological order; richer product-check derivation).

**Checkpoint**: full `dotnet test FS.GG.Governance.sln` green; the new surface baseline committed and drift-checked; determinism, consistency-by-construction, and scope-guard hygiene proven; quickstart validated.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundation (Phase 2)**: depends on Setup — BLOCKS all user stories. The Model (T010), the `defaultTimeout`/command-index/`gateIdOf` primitives (T011), and the sort + projection skeleton (T012) are the shared spine every story specializes.
- **User Stories (Phases 3–7)**: all depend on Foundation. US1 and US2 are **co-equal P1** and are the MVP *pairing* (a stable-identity assembler without the consistency guarantee is not safe to build on; spec §US2). US3 (P2) and US4 (P2) layer determinism proofs and the product-check/freshness/timeout fields. US5 (P3) is mostly a proof over the existing `GateId` sort.
- **Polish (Phase 8)**: depends on all user stories.

### Within Each User Story

- Tests are written first and must FAIL before implementation.
- `.fsi` contract (Phase 1) → FSI/prelude sketch (T008) → semantic tests → implementation → surface baseline (Principle I).
- The single projection function `buildRegistry`/`projectCheck` lives in `Gates.fs` and is extended **field by field** across US1 (core fields + description) and US4 (product-check, freshness key, command timeout) — these impl tasks (T014/T015, T021/T022/T023) edit the same file and are therefore **sequential within the file**, not `[P]`. US2/US3/US5 impl tasks (T017/T019/T025) are confirm/refine over that single function.

### Parallel Opportunities

- Setup `[P]` tasks T005, T007, T008, T009 run in parallel after the files they touch exist; T001→T002→T003 and T004→T005 are sequential (same projects/compile lists).
- Foundation: T010→T011→T012 are sequential (T011 reuses T010's Model; T012 reuses T011's primitives) — no `[P]`.
- All story **test** tasks are `[P]` within their story and across stories (distinct files): US1 (T013, `GateBuildTests.fs`), US2 (T016, `RegistryInvariantTests.fs`), US3 (T018) + US5 (T024) share `DeterminismTests.fs` (order T018 before T024 or merge), US4 (T020, `MetadataTests.fs`).
- Polish T027, T028, T029 are `[P]`; T026 precedes T027 if they share `SurfaceDriftTests.fs`.

---

## Suggested MVP Scope

**User Stories 1 + 2 together** (the co-equal P1 pair) are the MVP: give every declared check a stable, injective `GateId` carrying the full *Gate identities* field set (US1) **and** guarantee the registry is internally consistent by construction — unique ids, count parity, resolvable prerequisites, total assembly, proven by FsCheck (US2). Neither half is safe alone: an assembler whose `GateId` could collide or whose prerequisite could dangle would let a silent inconsistency propagate into every downstream gate decision. **User Story 3** (P2) proves determinism + explainability for the byte-stable `gates.json` / route/audit JSON that consume the registry. **User Story 4** (P2) adds the carried product-check flag, freshness key, and command-or-default timeout the later route/freshness rows act on. **User Story 5** (P3) proves the single `GateId` order is stable and reorder-invariant (which holds by construction) and documents the Phase-10 topological extension point.

## Task Count

- Setup: 9 (T001–T009)
- Foundation: 3 (T010–T012)
- US1 (P1, MVP): 3 (T013–T015) — 1 test, 2 implementation
- US2 (P1, MVP): 2 (T016–T017) — 1 test, 1 confirm
- US3 (P2): 2 (T018–T019) — 1 test, 1 confirm/refine
- US4 (P2): 4 (T020–T023) — 1 test, 3 implementation
- US5 (P3): 2 (T024–T025) — 1 test, 1 confirm
- Polish: 4 (T026–T029)
- **Total: 29 tasks**
