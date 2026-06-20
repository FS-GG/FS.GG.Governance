---
description: "Task list for F021 - 021-gates-json-projection: the pure gates.json projection — a single pure, total `GatesJson.ofGateRegistry : GateRegistry -> string` (plus a `schemaVersion` constant) that renders the F018 `GateRegistry` into a deterministic, versioned `gates.json` WHOLE-CATALOG document via a hand-driven `System.Text.Json` `Utf8JsonWriter` walk. Lists each declared gate by its `GateId` with its carried F018 metadata (domain, description, cost, timeout, owner, maturity, productCheck, prerequisites) and its carried freshness-key INPUTS. Re-derives/re-sorts/re-classifies NOTHING; byte-identical for identical input; NO new third-party dependency; and NO severity/profile/mode/enforcement/cache-eligibility verdict/per-change selection/selectingPaths/route trace/findings/cost rollup/ship verdict/raw-YAML/host-path/timestamp/environment value, NO round-trip parse, NO CLI/route.json/audit.json."
---

# Tasks: Deterministic gates.json Projection

**Feature branch**: `021-gates-json-projection` (active spec; git branch currently `main`)
**Spec**: [`specs/021-gates-json-projection/spec.md`](./spec.md)
**Plan**: [`specs/021-gates-json-projection/plan.md`](./plan.md)

**Input**: Design documents from `/specs/021-gates-json-projection/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/GatesJson.fsi](./contracts/GatesJson.fsi), [contracts/gates-json-document.md](./contracts/gates-json-document.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; new public `.fsi`; new surface baseline). Credible evidence is **public-surface** testing only: `GatesJson.ofGateRegistry` exercised over **real upstream-assembled inputs** — a real `GateRegistry` from the genuine F018 `Gates.buildRegistry` over real in-memory `TypedFacts` (research D7), never private helpers and never mocks (Principle V). The **emitted bytes** are inspected by a read-only `JsonDocument` parse, exactly as the kernel's `Json` tests and F020's `RouteJson` tests do. Driving the real `buildRegistry` re-exercises the F018 assembler, catching any projection-time mismatch a mock would hide. No network, git, agent, clock, or filesystem is reachable, so **no synthetic evidence is anticipated** — every case (empty/single/many-gate, with/without prerequisites, with/without freshness command) is reachable from real upstream outputs. Any literal standing in for an un-derivable case carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — `FS.GG.Governance.Gates` (with Config transitive) is referenced as-is (its existing public types and the `gateIdValue` renderer suffice); the only new baseline is `surface/FS.GG.Governance.GatesJson.surface.txt`.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** — this feature is a pure, total render of one already-typed, already-ordered value to a string (FR-008): no I/O, no git sensing, no clock, no multi-step state, no retries, no effect. It is exactly the "single pure function / explanation formatter" case Principle IV explicitly exempts from MVU ceremony (plan Constitution Check; the same call F019 `select`, F020 `ofRouteResult`, and the kernel's `Json.ofExplanation` made). The boundary is one pure function `ofGateRegistry : GateRegistry -> string` plus a `schemaVersion` constant — no `Model`/`Msg`/`Effect`/`update`/interpreter.

**Determinism minimums (FR-007, SC-002/SC-003)**: field order is the single ordering decision the projection makes — the fixed `Utf8JsonWriter` call sequence (top-level `schemaVersion` → `gates`, and each object's documented field order per [contracts/gates-json-document.md](./contracts/gates-json-document.md)). Collection order is inherited from `GateRegistry` verbatim (gates by `GateId` ordinal — fixed by F018 `buildRegistry`, not by source-check order; each gate's prerequisites in their carried order) — re-sorting **nothing**. No `Map` iteration (so no key-sort step). Default `Utf8JsonWriter` options ⇒ compact, no whitespace variance. No clock/host/environment value enters the document. Consequence: byte-identical across runs (SC-002) and identical for value-equal registries assembled from differently-ordered declared checks (SC-003, inherited from F018's `GateId`-ordinal sort).

**Carry/exclusion minimums (FR-002/FR-005/FR-006/FR-014, FR-011/FR-012, SC-005/SC-007)**: the F018 `Gate` supplies every gate field **verbatim** (`id` via `gateIdValue` never re-parsed, `domain`/`description`/`cost`/`timeout`/`owner`/`maturity`/`productCheck`/`prerequisites`/`freshnessKey` inputs). The carried `TimeoutLimit` renders verbatim — **no** timeout re-derived or re-defaulted (FR-006); `Maturity`/`Cost` render as the declared F014 vocabulary — **no** maturity-as-enforcement, **no** weighted cost scalar (FR-005). The carried `FreshnessKey` emits its **inputs** only (`check`/`domain`/`cost`/`environment`/`command`, the `None` command as explicit JSON `null`), **never** a cache-eligibility verdict (FR-014). The empty prerequisite list renders as a present empty array (FR-004). The writer has **no** code path that reads or writes severity, profile, mode, enforcement, cache verdict, per-change selection, `selectingPaths`, route trace, `findings`, cost rollup, ship verdict, blockers, warnings, exit code, raw YAML, host/absolute path, timestamp, or environment value — none exist on `GateRegistry`/`Gate`/`FreshnessKey`. The exclusion-sweep test asserts the emitted text contains none of those tokens.

**Totality minimums (FR-008/FR-009, SC-006)**: `ofGateRegistry` pattern-matches only closed DUs (exhaustively, **no wildcard** — a future cost/maturity/environment/prerequisite case is a compile error here, research D3) and unwraps single-case newtypes — no partial function, no division, no parse, no I/O — so it cannot throw for any well-typed `GateRegistry`. The empty registry projects to `{ "schemaVersion": "fsgg.gates/v1", "gates": [] }` — a valid success, never an error and never a placeholder gate.

**Scope-guard minimums (FR-011/FR-015)**: emit-only — **no** round-trip parse (`toGateRegistry` is a later consumer's concern), **no** severity/profile/mode/enforcement, **no** `FreshnessKey` evaluation (inputs carried, verdict never), **no** per-change selection or route trace (that is route.json / F019–F020), **no** ship verdict/blockers/exit-code basis (audit.json), **no** `fsgg` CLI host (persisting to `.fsgg/gates.json` is a later host edge). The library lives in the product-neutral Governance layer, requires no FS.GG package installed in any inspected repo, and adds **no** new third-party `PackageReference` — serialization is the net10.0 shared-framework `System.Text.Json` the kernel's `Json.fs` and F020's `RouteJson.fs` already use.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US4]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional gates.json-projection library `FS.GG.Governance.GatesJson`, its test project, the public contract (copied verbatim), the real upstream-assembly + `JsonDocument` read helpers, the prelude sketch, and the readiness note. **No new third-party dependency** — the library references **only** `FS.GG.Governance.Gates` (Config arriving transitively, research D1); its own code is `System.Text.Json` (BCL shared framework) + FSharp.Core.

- [X] T001 Create `src/FS.GG.Governance.GatesJson/FS.GG.Governance.GatesJson.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.GatesJson`, `RootNamespace=FS.GG.Governance.GatesJson`, with exactly **one** `<ProjectReference>` — `../FS.GG.Governance.Gates/FS.GG.Governance.Gates.fsproj` — and **no** `<PackageReference>` (Config arrives transitively via Gates; `System.Text.Json` is in the net10.0 shared framework, research D1/D2). Compile order: `GatesJson.fsi` → `GatesJson.fs`. Add an fsproj header comment (mirroring the F020 fsproj) noting this is the gates.json *projection* — the emit-only, pure render of an F018 `GateRegistry` to the deterministic versioned WHOLE-CATALOG document string; it layers serialization on top of the pure `Gates` assembler in a separate project (constitution: heavier capabilities layer on top, not into the core), adds no dependency, and reaches no git/filesystem/clock.
- [X] T002 Copy `specs/021-gates-json-projection/contracts/GatesJson.fsi` → `src/FS.GG.Governance.GatesJson/GatesJson.fsi` verbatim as the curated public surface (Principle II — this `.fsi` is the SOLE public surface: `ofGateRegistry` + `schemaVersion`; the matching `GatesJson.fs` carries no top-level access modifiers and keeps every writer/token helper hidden, the `Kernel/Json.fs` + `RouteJson.fs` precedent).
- [X] T003 Add a `failwith "F021"` stub body in `src/FS.GG.Governance.GatesJson/GatesJson.fs` (plus a placeholder `schemaVersion` binding) that satisfies the `GatesJson.fsi` contract, so the library compiles against the contract before any real projection logic lands (Principle I).
- [X] T004 Create `tests/FS.GG.Governance.GatesJson.Tests/FS.GG.Governance.GatesJson.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/YoloDev.Expecto.TestSdk packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.GatesJson`, `src/FS.GG.Governance.Gates`, and `src/FS.GG.Governance.Config` (the tests assemble real `TypedFacts`, call the real `Gates.buildRegistry` to build a real `GateRegistry`, and read the emitted bytes via `System.Text.Json.JsonDocument`).
- [X] T005 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.GatesJson.Tests/`: `Support.fs`, `ProjectionTests.fs`, `DeterminismTests.fs`, `CarryTests.fs`, `TotalityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T006 Add `src/FS.GG.Governance.GatesJson` and `tests/FS.GG.Governance.GatesJson.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Implement the real upstream-assembly + JSON read helpers in `tests/FS.GG.Governance.GatesJson.Tests/Support.fs` over **real** values (no mocks): (a) reuse/adapt the F018 fixture style to build a real `Config.Model.TypedFacts` (declared domains, capability checks spanning ≥2 cost tiers and ≥2 maturities, a gate with a command prerequisite and a `Some` freshness command, a gate with no prerequisite and a `None` freshness command, and — for the separator edge case — a domain id containing a `:`) — the genuine `Valid TypedFacts` a downstream caller holds; (b) a `registryOf : TypedFacts -> GateRegistry` convenience calling the real `Gates.buildRegistry` (so every fixture is a real F018 registry, not a hand-built value); (c) `JsonDocument` read helpers — `parse : string -> JsonDocument`, plus small accessors (e.g. `gateIds`, a per-gate field reader, a `freshnessKey` reader, field-presence/absence probes, and a top-level/per-object field-order extractor) for read-only inspection of the emitted bytes. These produce/inspect REAL outputs, never fakes.
- [X] T008 [P] Extend `scripts/prelude.fsx` with an F021 design sketch that `#r`s the built `FS.GG.Governance.GatesJson` assembly, opens the namespace, projects the existing F018 `f18Registry` (the real F014→F018 facts already assembled earlier in the prelude) via `GatesJson.ofGateRegistry`, prints `GatesJson.schemaVersion`, prints the document bytes + length, asserts byte-identity on a second projection, and projects an empty registry (`Gates.buildRegistry` over facts with no declared checks, or `{ Gates = [] }`) to show the empty-but-valid `{ "schemaVersion": "fsgg.gates/v1", "gates": [] }` document — recording the intended projection flow before the real body lands (Principle I; mirrors [quickstart.md](./quickstart.md) §FSI smoke). **Design-first, like F020's T008**: written here as the design record, it will *throw at runtime* while `ofGateRegistry` is the `failwith "F021"` stub (T003) and only runs green once Foundation/US1 land; T028 re-runs it end-to-end against the real body.
- [X] T009 [P] Create `specs/021-gates-json-projection/readiness/README.md` listing the required FSI transcripts (the `f18Registry` projection showing the full document with every declared gate in `GateId` order + each gate's metadata/prerequisites/freshness-key inputs; a gate with a command prerequisite and a `Some` freshness command beside a gate with `[]` prerequisites and a `null` command; a twice-identical determinism run; an empty-registry document with an empty `gates` array) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.GatesJson` and `dotnet test tests/FS.GG.Governance.GatesJson.Tests` compile against the stub; the solution lists the two new projects; the single Gates reference resolves (Config transitively); the Support helpers assemble a real `GateRegistry` and parse a string with `JsonDocument`.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the `schemaVersion` constant, the hidden closed-enum token helpers + sub-object writers, and the top-level `ofGateRegistry` writer skeleton (the fixed-order `Utf8JsonWriter` walk) — everything the stories specialize. **No user-story work begins until this phase is complete.**

- [X] T010 Implement `GatesJson.schemaVersion` in `src/FS.GG.Governance.GatesJson/GatesJson.fs` as the fixed declared contract-version constant (`"fsgg.gates/v1"` per [contracts/gates-json-document.md](./contracts/gates-json-document.md), FR-013) — a plain string literal, never derived from a clock/environment/input.
- [X] T011 Implement the **hidden** closed-enum token helpers in `src/FS.GG.Governance.GatesJson/GatesJson.fs` (absent from `GatesJson.fsi`, mirroring `Kernel/Json.fs` and `RouteJson.fs`): `costToken : Cost -> string` (`cheap`/`medium`/`high`/`exhaustive`), `maturityToken : Maturity -> string` (`observe`/`warn`/`blockOnPr`/`blockOnShip`/`blockOnRelease`), and `environmentToken : EnvironmentClass -> string` (`local`/`ci`/`localOrCi`/`release`) — the exact token tables in [research D3](./research.md) and [contracts/gates-json-document.md](./contracts/gates-json-document.md). Each `match` is **exhaustive over the closed DU with no wildcard** (research D3), so a future case is a compile error here, never a silently mis-tokened field.
- [X] T012 Implement the **hidden** sub-object writers in `src/FS.GG.Governance.GatesJson/GatesJson.fs` against a `Utf8JsonWriter`, each emitting its documented field order verbatim (FR-007, [contracts/gates-json-document.md](./contracts/gates-json-document.md)): `writeFreshnessKey` (`check`, `domain`, `cost`, `environment`, `command` — `CommandId option` as string or JSON `null`, never a cache verdict, FR-014); `writePrerequisite` (`RequiresCommand c` → `{ "requiresCommand": "<commandId>" }`). Newtype unwraps (`DomainId`/`Owner`/`CheckId`/`CommandId`/`TimeoutLimit`) happen at the use site. Disclose any `mutable`/`for` writer idiom at its use site (Principle III — the plain BCL `Utf8JsonWriter` idiom).
- [X] T013 Implement the `ofGateRegistry` writer skeleton in `src/FS.GG.Governance.GatesJson/GatesJson.fs`: create a `Utf8JsonWriter` over a pooled buffer with **default** (compact) options; write the top-level object in the FIXED order `schemaVersion` → `gates` (array); flush and decode the buffer to a UTF-8 string. Walk `registry.Gates` in order writing each gate object (US1 fills the gate fields) — preserving the registry's existing `GateId` order, **re-sorting nothing** (FR-007). PURE and TOTAL — never throws; the empty registry yields `{ "schemaVersion": "fsgg.gates/v1", "gates": [] }`, a valid success (FR-008/FR-009).

**Checkpoint**: the library builds with the real `schemaVersion` + token helpers + sub-object writers + the top-level walk; `ofGateRegistry` over an empty registry returns the empty-but-valid document; the document parses as one top-level object with fields in the fixed order; the surface compiles against `GatesJson.fsi`.

---

## Phase 3: User Story 1 - Render a gate registry to a deterministic gates.json (Priority: P1) 🎯 MVP

**Goal**: project a real `GateRegistry` so the document lists each declared gate by its `GateId` with its carried F018 metadata (domain, description, cost, timeout, owner, maturity, productCheck, prerequisites) — every value tracing back to a `Gate` in the registry, none invented; a gate with no prerequisites records a present-and-empty prerequisite list; and an empty registry projects to a valid document with an empty `gates` array (FR-009).

**Independent Test**: project a real `registryOf` fixture with ≥2 gates (one with a command prerequisite, one with none); parse the document and assert one `gates[*]` per `registry.Gates` with matching `id`/`domain`/`description`/`cost`/`timeout`/`owner`/`maturity`/`productCheck`/`prerequisites`; assert the no-prerequisite gate carries a present empty `prerequisites` array; assert no gate absent from the registry appears; assert the empty registry yields an empty `gates` array.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T014 [P] [US1] In `tests/FS.GG.Governance.GatesJson.Tests/ProjectionTests.fs`, add projection tests over a real `registryOf` fixture, inspecting the emitted bytes via `JsonDocument`: (1) every declared gate is present exactly once, by declared `id` (via `gateIdValue`), with its carried `domain`/`description`/`cost`/`timeout`/`owner`/`maturity`/`productCheck`/`prerequisites` matching the `Gate` verbatim (US1 AS1, **SC-001**); (2) a gate carrying ≥1 prerequisite records each `{ requiresCommand }` in carried order, and a gate with **no** prerequisites records a **present-and-empty** `prerequisites` array — never an omitted field (US1 AS2, FR-004); (3) **no** gate that `registry.Gates` did not contain appears, and no gate/prerequisite/cost/timeout/freshness key is invented (US1 AS1 / FR-003); (4) the empty registry projects to a present-and-empty `gates` array — never an error and never a placeholder gate (US1 AS3 / FR-009); (5) a gate whose **`DomainId` itself contains the gate-id separator** (a colon, e.g. domain `a:b` + check `tests` → `GateId` `"a:b:tests"`, a two-colon id — *not* the ordinary single-colon `domain:check` form) renders `id` and `domain` **verbatim** — the emitted `id` equals `gateIdValue g.Id` and the emitted `domain` equals the declared `DomainId` unwrapped (`"a:b"`), with no re-parse and no separator re-derivation (spec edge case "domain identifier containing the gate-id separator", FR-008/FR-010). The `DomainId` carrying a colon is a legitimately-typed value (no charset smart constructor) fed through the real `buildRegistry`, so this stays real evidence; (6) a gate carrying the default timeout vs. a command-derived timeout both render as the declared `TimeoutLimit` (int seconds) **verbatim** — the projection re-derives no timeout (spec edge case, FR-002/FR-006); (7) a gate `description` containing JSON-special characters (`"`, `\`, a newline) round-trips: the value read back from the parsed `JsonDocument` equals the input string exactly — faithful carry with escaping delegated to the writer, never manual (spec edge case, FR-002/FR-012).

### Implementation for User Story 1

- [X] T015 [US1] Complete the per-gate object writer inside `ofGateRegistry` (`src/FS.GG.Governance.GatesJson/GatesJson.fs`, building on T013): for each `Gate g` emit the documented field order — `id` (via `gateIdValue g.Id`, verbatim, never re-parsed, FR-010), `domain` (`DomainId` unwrapped), `description` (verbatim, escaped by the writer), `cost` (T011 `costToken`, declared tier — **not** a weighted scalar, FR-005), `timeout` (int seconds verbatim — **not** re-derived, FR-006), `owner` (`Owner` unwrapped), `maturity` (T011 `maturityToken`, declared verbatim — **not** enforcement, FR-005/FR-011), `productCheck` (bool), `prerequisites` (array via `writePrerequisite`, in carried order — present-and-empty when none, FR-004), and `freshnessKey` (object via `writeFreshnessKey` — asserted in US3). Carry the `Gate` verbatim — re-derive nothing (FR-002). Free-text `description` is written through the `Utf8JsonWriter` string API so JSON-escaping is the writer's job — **no** manual escaping or pre-processing (FR-002, asserted by T014(7)).

**Checkpoint**: a real registry projects to a document listing exactly its declared gates with full carried metadata, one entry per gate in `GateId` order, no invented gate, present-and-empty prerequisite arrays where appropriate, and the empty registry a valid empty-`gates` document — the MVP. US1 stands alone.

---

## Phase 4: User Story 2 - A stable, versioned schema for CI and agents (Priority: P1)

**Goal**: identical inputs produce a byte-identical document; value-equal registries assembled from differently-ordered declared checks produce identical documents; the document carries a declared `schemaVersion` and a stable documented field order; and it contains no clock/host/environment value and none of the excluded enforcement/verdict/selection/raw-YAML tokens.

**Independent Test**: project the same `GateRegistry` twice and assert byte-for-byte equality; project two registries built (via the real `buildRegistry`) from differently-ordered declared checks and assert identical strings; assert the `schemaVersion` field equals `GatesJson.schemaVersion` and the top-level field order is `schemaVersion`,`gates`; run the exclusion sweep over the emitted text.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T016 [P] [US2] In `tests/FS.GG.Governance.GatesJson.Tests/DeterminismTests.fs`, add an FsCheck **twice-identical** property and a fixed-fixture equality: `ofGateRegistry r = ofGateRegistry r`, byte-for-byte (US2 AS1, **SC-002**). **Generator provenance**: generate each `GateRegistry` by driving the real `Gates.buildRegistry` over generated `TypedFacts` (research D7) so inputs stay real upstream-assembled values; any directly-constructed arbitrary carries the `Synthetic` token + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- [X] T017 [P] [US2] In the same file, add a **permutation-invariance** test: build two `GateRegistry`s that are value-equal but assembled from **differently-ordered declared checks** (shuffle the capability-check declarations in the `TypedFacts` passed to `Gates.buildRegistry`, whose `GateId`-ordinal sort fixes the gate order) and assert the two emitted strings are identical (US2 AS2, **SC-003**).
- [X] T018 [P] [US2] In the same file, add a **schema-version + field-order** test: parse the document and assert the `schemaVersion` field equals `GatesJson.schemaVersion`, assert the top-level field order is exactly `schemaVersion`, `gates`, and assert each `gates[*]` object's field order is exactly `id`, `domain`, `description`, `cost`, `timeout`, `owner`, `maturity`, `productCheck`, `prerequisites`, `freshnessKey` and each `freshnessKey` object's order is `check`, `domain`, `cost`, `environment`, `command` (US2 AS3, FR-013, [data-model.md](./data-model.md) field-order table).
- [X] T019 [P] [US2] In the same file, add the **exclusion sweep** (US2 AS4, **SC-007**, FR-011/FR-012) over a real multi-gate, prerequisite-bearing registry so the sweep covers populated sections: (a) a **deny-token** check — the emitted text contains none of `severity`, `profile`, `mode`, `enforcement`, `cacheEligib`, `selectingPaths`, `findings`, ship `verdict`, `blockers`, `warnings`, `exitCode`, `expectedArtifacts`, a cost rollup, raw YAML, a wall-clock timestamp, or any environment-derived value; (b) a **positive allowlist** check — parse the document and assert the only string values present are declared id strings, the declared `Cost`/`Maturity`/`EnvironmentClass` vocabulary, the carried gate metadata, the carried free-text description, and the carried freshness-key inputs (no host/absolute path can appear by construction — `GateRegistry`/`Gate`/`FreshnessKey` carry none, FR-012).

### Implementation for User Story 2

- [X] T020 [US2] Confirm/complete determinism in `ofGateRegistry` (`src/FS.GG.Governance.GatesJson/GatesJson.fs`): default (compact) `Utf8JsonWriter` options (no indentation), fixed field order throughout, the gate collection emitted in `GateRegistry`'s existing `GateId` order with **no** re-sort, **no** `Map` iteration, and **no** clock/host/environment value introduced. Note explicitly if no change was needed beyond Foundation/US1. **Determinism is a property of the Foundation walk + US1 writers; record here whether any residual input-order or option-default leakage required a fix.**

**Checkpoint**: the document is byte-stable, permutation-invariant, version-stamped, fixed-field-ordered, and free of every excluded token — usable as a CI/agent contract and a golden snapshot. US1 + US2 together are the co-equal P1 MVP pairing.

---

## Phase 5: User Story 3 - Freshness keys carried forward, enforcement excluded (Priority: P2)

**Goal**: each gate carries its declared freshness-key **inputs** (`check`/`domain`/`cost`/`environment`/`command`, the `None` command as explicit JSON `null`) and its `productCheck` flag verbatim — but no cache-eligibility verdict, severity, profile, mode, or enforcement field anywhere.

**Independent Test**: project a real `GateRegistry` and assert each gate's `freshnessKey` carries the five declared inputs; assert a gate whose freshness key carries `Some` command renders the command string and one whose key carries `None` renders explicit `null`; assert `productCheck` is carried verbatim; assert no cache/enforcement/severity/profile/mode field appears.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T021 [P] [US3] In `tests/FS.GG.Governance.GatesJson.Tests/CarryTests.fs`, add carry-through tests over real fixtures, inspecting the emitted bytes: (1) each gate's `freshnessKey` carries `check`/`domain`/`cost`/`environment`/`command` — the carried key **inputs**, no cache verdict computed or emitted (US3 AS1, **SC-004**, FR-014); (2) a gate whose `FreshnessKey.Command` is `Some` renders the `command` as the declared `CommandId` string, and a gate whose command is `None` renders an **explicit JSON `null`** — distinguishable from a present command, never silently dropped or invented (US3 AS2, **SC-004**, FR-014 §2); (3) the `productCheck` flag is carried verbatim from the registry (not re-derived from the environment) (US3 AS3, FR-002 — product-check is a carried-metadata field); (4) **no** severity, profile, mode, enforcement, cache-eligibility verdict, per-change selection, route trace, ship verdict, blocker, warning, or exit-code field appears anywhere in the document (US3 AS4, FR-011) — assert via field-absence probes, complementing the US2 text sweep; (5) cost/timeout/owner/maturity/productCheck render verbatim with **no** enforcement translation and **no** weighted cost scalar (**SC-005**, FR-005, complementing T014).

### Implementation for User Story 3

- [X] T022 [US3] Confirm the `freshnessKey` object and `productCheck` field in `ofGateRegistry` (`src/FS.GG.Governance.GatesJson/GatesJson.fs`): each gate's `freshnessKey` written via `writeFreshnessKey` (T012) carrying only the five declared inputs with the `None` command as explicit `null` (FR-014); `productCheck` carried verbatim (FR-002 — a carried-metadata field). Verify there is **no** code path emitting a cache verdict, severity, profile, mode, or enforcement field. Note explicitly if no change was needed beyond Foundation/US1.

**Checkpoint**: each gate carries its freshness inputs (with `Some`/`None` command faithfully distinguished) and its product-check flag, and the document expresses no cache-eligibility verdict or enforcement field — faithful scope held.

---

## Phase 6: User Story 4 - Total over any well-typed gate registry (Priority: P2)

**Goal**: `ofGateRegistry` returns a document for every `GateRegistry` F018 can produce — empty, single-gate, many-gate, gates with and without prerequisites and optional freshness commands — and never throws; the empty registry is a valid success and no gate's optional field leaks onto another.

**Independent Test**: FsCheck over generated well-typed `GateRegistry`s asserting `ofGateRegistry` always returns a (parseable) string and never throws, including the empty registry, a single gate, and a large many-gate registry with mixed prerequisites and freshness keys.

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T023 [P] [US4] In `tests/FS.GG.Governance.GatesJson.Tests/TotalityTests.fs`, add the **empty-registry** and **mixed-shape** tests: (1) `ofGateRegistry` over the empty registry (`Gates = []`) returns a valid document with an empty `gates` array, never throwing (US4 AS1, FR-009, **SC-006**); (2) a registry whose gates **mix** present and absent prerequisites and `Some`/`None` freshness commands projects with every gate rendering its own shape — no gate's optional field leaks onto another (US4 AS2).
- [X] T024 [P] [US4] In the same file, add an FsCheck **totality** property over generated well-typed `GateRegistry`s (including empty, single-gate, many-gate, mixed-shape): `ofGateRegistry` **always returns a parseable string and never throws** (US4 AS3, **SC-006**). **Generator provenance (resolve before writing the test):** generate each `GateRegistry` by driving the **real** `Gates.buildRegistry` over an FsCheck generator of `TypedFacts` (research D7) so the inputs stay real upstream-assembled values, keeping the "no synthetic evidence" stance honest. If, and only if, a case is unreachable through `buildRegistry` and a `GateRegistry` value must be constructed directly, that arbitrary is **synthetic**: name the property with the `Synthetic` token, add a use-site `// SYNTHETIC:` disclosure naming the case and why `buildRegistry` can't produce it, and list it in the PR (Principle V). Prefer the real-assembler generator.

### Implementation for User Story 4

- [X] T025 [US4] Confirm `ofGateRegistry` (`src/FS.GG.Governance.GatesJson/GatesJson.fs`) is total: only closed-DU `match`es (exhaustive, no wildcard — `Cost`/`Maturity`/`EnvironmentClass`/`GatePrerequisite`) + single-case newtype unwraps + a `Utf8JsonWriter` walk — no partial function, parse, division, or I/O. Verify the empty registry flows through the same code path as a populated one (no special-casing) and that each gate's optional fields are read only from that gate. Note explicitly if no change was needed beyond earlier phases.

**Checkpoint**: the projection returns a document for 100% of well-typed registries, including the empty registry, and never throws — callable unconditionally by later rows.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: lock the public surface, prove the dependency boundary, and finish the docs/evidence.

- [X] T026 [P] Generate `surface/FS.GG.Governance.GatesJson.surface.txt` capturing exactly the public `GatesJson` module (`schemaVersion`, `ofGateRegistry` — the `.fsi` surface), nothing private (no token helpers, sub-object writers, or buffer plumbing).
- [X] T027 In `tests/FS.GG.Governance.GatesJson.Tests/SurfaceDriftTests.fs`, add the surface-drift test asserting the built public surface matches `surface/FS.GG.Governance.GatesJson.surface.txt` (Principle II, with `BLESS_SURFACE=1` regen path), assert "exactly the `GatesJson` module, nothing private" (no token helpers, sub-object writers, or buffer plumbing), and assert the `GatesJson → Gates → Config` one-way dependency (no kernel/host/adapters/route/snapshot/CLI edge; no new third-party `PackageReference`) — mirroring the F020 `SurfaceDriftTests` dependency assertion.
- [X] T028 [P] Verify [quickstart.md](./quickstart.md) end-to-end: run the documented `dotnet test` and the prelude FSI smoke (the real `Gates.buildRegistry` then `GatesJson.ofGateRegistry`), confirm the acceptance→evidence map holds, and fill `specs/021-gates-json-projection/readiness/README.md` with the real FSI transcripts (T009) and the SC-001…SC-007 traceability note.
- [X] T029 [P] Update [`specs/021-gates-json-projection/plan.md`](./plan.md) with an **Implementation Progress** header (status table + evidence summary, mirroring the F020 plan) once the suite is green, and confirm `CLAUDE.md`'s SPECKIT block points at this plan.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundation (Phase 2)** — depends on Setup; **BLOCKS all user stories** (the `schemaVersion`, the token helpers, the sub-object writers, and the top-level `ofGateRegistry` walk everything specialises).
- **User Stories (Phases 3–6)** — all depend on Foundation. US1 (P1) is the MVP. US2 (P1) proves determinism/version/exclusion over the document US1 produces. US3/US4 (P2) build on it: US3 asserts the freshness/product-check carry the Foundation writers wire, US4 proves totality across all of it.
- **Polish (Phase 7)** — depends on all desired user stories being complete.

### User-story dependencies

- **US1 (P1)** — after Foundation; no dependency on other stories (the core gate-object render).
- **US2 (P1)** — after Foundation; reads the same document US1 produces (determinism/version/field-order/exclusion are properties of the whole walk). Independently testable.
- **US3 (P2)** — after Foundation; freshness/product-check carry-through is independent of which gates are present (asserts faithful carry + `Some`/`None` command). Independently testable.
- **US4 (P2)** — after the document is *correct* (US1–US3); proves its *totality* over every well-typed input.

### Within each user story

- Tests are written first and MUST FAIL before implementation (Principle I/V).
- `schemaVersion` + token helpers + sub-object writers + top-level skeleton (Foundation) before any story.
- Each story is independently completable and testable; complete a story before moving to the next priority.

### Parallel opportunities

- **Setup**: T005, T007, T008, T009 are `[P]` (distinct files) once T001–T004 exist.
- **Tests across stories**: T014, T016–T019, T021, T023, T024 are `[P]` — distinct test files (`ProjectionTests`/`DeterminismTests`/`CarryTests`/`TotalityTests`), no shared state.
- **Stories**: once Foundation is done, US1–US4 test-writing can proceed in parallel by different developers; the implementation tasks (T015, T020, T022, T025) all touch `GatesJson.fs`, so serialize those edits (or have one owner sweep them in phase order — most are "confirm/complete" since the Foundation walk + US1 writers already cover them).
- **Polish**: T026, T028, T029 are `[P]`; T027 depends on T026.

---

## Parallel Example: cross-story test authoring

```bash
# After Foundation (Phase 2), launch the per-story test files together (distinct files):
Task: "ProjectionTests.fs  — US1 every gate + carried metadata + prerequisites + empty registry (T014)"
Task: "DeterminismTests.fs — US2 twice-identical + permutation + version/field-order + exclusion sweep (T016–T019)"
Task: "CarryTests.fs       — US3 freshness-key inputs + Some/None command + productCheck + no enforcement (T021)"
Task: "TotalityTests.fs    — US4 empty + mixed-shape + FsCheck totality (T023–T024)"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: a real registry projects to a document listing exactly its declared gates with full carried metadata, one entry per gate in `GateId` order, present-and-empty prerequisite arrays where appropriate, and the empty registry a valid empty-`gates` document.

### Incremental delivery

1. Setup + Foundation → foundation ready.
2. US1 → the gate registry renders to a document → the MVP.
3. US2 → the document is a deterministic, versioned, exclusion-clean contract (the co-equal P1).
4. US3 → freshness inputs + product-check carried, enforcement excluded.
5. US4 → totality proven over every well-typed input.
6. Polish → surface baseline + dependency assertion + readiness/quickstart.

---

## Notes

- `[P]` = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- The four `GatesJson.fs` implementation tasks (T015, T020, T022, T025) edit one file — serialize them in phase order; most are "confirm/complete," since the Foundation writers (T010–T013) already wire the projection.
- Tests inspect the **emitted bytes** via read-only `JsonDocument` (the kernel `Json`-test + F020 `RouteJson`-test precedent), never private helpers — `ofGateRegistry` and `schemaVersion` are the entire public surface.
- **No synthetic evidence is anticipated** (research D7) — every case (empty/single/many-gate, with/without prerequisites, with/without freshness command, separator-in-domain, JSON-special free text) is reachable from real `Gates.buildRegistry` outputs, and the FsCheck properties (T016, T024) generate their `GateRegistry`s by driving the real `buildRegistry` rather than constructing values directly. Any unavoidable literal or directly-constructed FsCheck arbitrary carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- Scope guards (FR-011/FR-012/FR-015): no round-trip parse, severity, enforcement, cache verdict, per-change selection, `selectingPaths`, route trace, findings, cost rollup, ship verdict, blockers, warnings, exit code, raw YAML, host path, timestamp, environment value, CLI, route.json, or audit.json — the projection stops at the document string, adds no third-party dependency.
