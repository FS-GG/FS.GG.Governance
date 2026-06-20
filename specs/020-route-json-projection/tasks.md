---
description: "Task list for F020 - 020-route-json-projection: the pure route.json projection — a single pure, total `RouteJson.ofRouteResult : RouteResult -> string` (plus a `schemaVersion` constant) that renders the F019 `RouteResult` into a deterministic, versioned `route.json` document via a hand-driven `System.Text.Json` `Utf8JsonWriter` walk. Lists each selected gate with its declared `GateId` + carried F018 metadata + route trace + carried freshness-key INPUTS, carries the F017 findings unchanged in F017 order, and renders the per-tier `CostRollup` with every declared tier present. Re-derives/re-sorts/re-classifies NOTHING; byte-identical for identical input; NO new third-party dependency; and NO severity/profile/mode/enforcement/cache-eligibility verdict/ship verdict/blockers/warnings/exit-code/raw-YAML/host-path/timestamp/environment value, NO round-trip parse, NO CLI/audit.json."
---

# Tasks: Deterministic route.json Projection

**Feature branch**: `020-route-json-projection` (active spec; git branch currently `main`)
**Spec**: [`specs/020-route-json-projection/spec.md`](./spec.md)
**Plan**: [`specs/020-route-json-projection/plan.md`](./plan.md)

**Input**: Design documents from `/specs/020-route-json-projection/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/RouteJson.fsi](./contracts/RouteJson.fsi), [contracts/route-json-document.md](./contracts/route-json-document.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; new public `.fsi`; new surface baseline). Credible evidence is **public-surface** testing only: `RouteJson.ofRouteResult` exercised over **real upstream-assembled inputs** — a real `RouteResult` from the genuine F015→F017→F018→F019 chain (`Gates.buildRegistry` → `Routing.route` → `Findings.findUnknownGovernedPaths` → `Route.select`) over real in-memory `TypedFacts`, never private helpers and never mocks (Principle V, research D7). The **emitted bytes** are inspected by a read-only `JsonDocument` parse, exactly as the kernel's `Json` tests do. Driving the real chain transitively re-exercises the upstream rows, catching any projection-time mismatch a mock would hide. No network, git, agent, clock, or filesystem is reachable, so **no synthetic evidence is anticipated** — every case (empty/single/many-gate, findings-only, shared-gate, all-tiers) is reachable from real upstream outputs. Any literal standing in for an un-derivable case carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — `FS.GG.Governance.Route` (with Gates/Routing/Findings/Config transitive) is referenced as-is (its existing public types and the `gateIdValue`/`findingIdToken` renderers suffice); the only new baseline is `surface/FS.GG.Governance.RouteJson.surface.txt`.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** — this feature is a pure, total render of one already-typed, already-ordered value to a string (FR-008): no I/O, no git sensing, no clock, no multi-step state, no retries, no effect. It is exactly the "single pure function / explanation formatter" case Principle IV explicitly exempts from MVU ceremony (plan Constitution Check; the same call F019 `select` and the kernel's `Json.ofExplanation` made). The boundary is one pure function `ofRouteResult : RouteResult -> string` plus a `schemaVersion` constant — no `Model`/`Msg`/`Effect`/`update`/interpreter.

**Determinism minimums (FR-007, SC-002/SC-003)**: field order is the single ordering decision the projection makes — the fixed `Utf8JsonWriter` call sequence (top-level `schemaVersion` → `selectedGates` → `findings` → `cost`, and each object's documented field order per `contracts/route-json-document.md`). Collection order is inherited from `RouteResult` verbatim (gates by `GateId`, selecting paths by normalized path, findings in F017 order) — re-sorting **nothing**. No `Map` iteration (so no key-sort step). Default `Utf8JsonWriter` options ⇒ compact, no whitespace variance. No clock/host/environment value enters the document. Consequence: byte-identical across runs (SC-002) and identical for value-equal inputs assembled from differently-ordered upstream inputs (SC-003, inherited from F019's permutation-invariance).

**Carry/exclusion minimums (FR-002/FR-005/FR-014, FR-011/FR-012, SC-007)**: the embedded F018 `Gate` supplies every selected-gate field **verbatim** (`id` via `Gates.gateIdValue` never re-parsed, `domain`/`description`/`cost`/`timeout`/`owner`/`maturity`/`productCheck`/`prerequisites`/`freshnessKey` inputs); the F017 `FindingReport` is carried **unchanged** in F017 order (`id` via `Findings.findingIdToken`); the `CostRollup` renders every declared tier including zero — **no** summed scalar. The carried `FreshnessKey` emits its **inputs** only, **never** a cache-eligibility verdict. The writer has **no** code path that reads or writes severity, profile, mode, enforcement, cache verdict, ship verdict, blockers, warnings, exit code, expected artifacts, raw YAML, host/absolute path, timestamp, or environment value — none exist on `RouteResult`/`Gate`/`FreshnessKey`/`FindingReport`. The exclusion-sweep test asserts the emitted text contains none of those tokens.

**Totality minimums (FR-008/FR-009, SC-006)**: `ofRouteResult` pattern-matches only closed DUs (exhaustively, **no wildcard** — a future tier/maturity/zone case is a compile error here, research D3) and unwraps single-case newtypes — no partial function, no division, no parse, no I/O — so it cannot throw for any well-typed `RouteResult`. The empty route projects to `{ schemaVersion, "selectedGates": [], "findings": [], "cost": {0,0,0,0} }` — a valid success, never an error and never a "select everything" fallback. A findings-only route projects with both sections coexisting.

**Scope-guard minimums (FR-011/FR-015)**: emit-only — **no** round-trip parse (`toRouteResult` is a later consumer's concern), **no** severity/profile/mode/enforcement, **no** `FreshnessKey` evaluation (inputs carried, verdict never), **no** ship verdict/blockers/warnings/exit-code basis, **no** `fsgg route`/`fsgg ship` CLI host, **no** audit.json. The library lives in the product-neutral Governance layer, requires no FS.GG package installed in any inspected repo, and adds **no** new third-party `PackageReference` — serialization is the net10.0 shared-framework `System.Text.Json` the kernel's `Json.fs` already uses.

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

**Purpose**: stand up the new optional route.json-projection library `FS.GG.Governance.RouteJson`, its test project, the public contract (copied verbatim), the real upstream-assembly + `JsonDocument` read helpers, the prelude sketch, and the readiness note. **No new third-party dependency** — the library references **only** `FS.GG.Governance.Route` (Gates/Routing/Findings/Config arriving transitively, research D1); its own code is `System.Text.Json` (BCL shared framework) + FSharp.Core.

- [X] T001 Create `src/FS.GG.Governance.RouteJson/FS.GG.Governance.RouteJson.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.RouteJson`, `RootNamespace=FS.GG.Governance.RouteJson`, with exactly **one** `<ProjectReference>` — `../FS.GG.Governance.Route/FS.GG.Governance.Route.fsproj` — and **no** `<PackageReference>` (Gates/Routing/Findings/Config arrive transitively via Route; `System.Text.Json` is in the net10.0 shared framework, research D1/D2). Compile order: `RouteJson.fsi` → `RouteJson.fs`. Add an fsproj header comment (mirroring the F019 fsproj) noting this is the route.json *projection* — the emit-only, pure render of an F019 `RouteResult` to the deterministic versioned document string; it layers serialization on top of the pure `Route` join in a separate project (constitution: heavier capabilities layer on top, not into the core), adds no dependency, and reaches no git/filesystem/clock.
- [X] T002 Copy `specs/020-route-json-projection/contracts/RouteJson.fsi` → `src/FS.GG.Governance.RouteJson/RouteJson.fsi` verbatim as the curated public surface (Principle II — this `.fsi` is the SOLE public surface: `ofRouteResult` + `schemaVersion`; the matching `RouteJson.fs` carries no top-level access modifiers and keeps every writer/token helper hidden, the `Kernel/Json.fs` precedent).
- [X] T003 Add a `failwith "F020"` stub body in `src/FS.GG.Governance.RouteJson/RouteJson.fs` (plus a placeholder `schemaVersion` binding) that satisfies the `RouteJson.fsi` contract, so the library compiles against the contract before any real projection logic lands (Principle I).
- [X] T004 Create `tests/FS.GG.Governance.RouteJson.Tests/FS.GG.Governance.RouteJson.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/VSTest packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.RouteJson`, `src/FS.GG.Governance.Route`, `src/FS.GG.Governance.Gates`, `src/FS.GG.Governance.Routing`, `src/FS.GG.Governance.Findings`, and `src/FS.GG.Governance.Config` (the tests assemble real `TypedFacts`, call the real upstream chain to build a real `RouteResult`, and read the emitted bytes via `System.Text.Json.JsonDocument`).
- [X] T005 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.RouteJson.Tests/`: `Support.fs`, `ProjectionTests.fs`, `DeterminismTests.fs`, `CarryTests.fs`, `TotalityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T006 Add `src/FS.GG.Governance.RouteJson` and `tests/FS.GG.Governance.RouteJson.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Implement the real upstream-assembly + JSON read helpers in `tests/FS.GG.Governance.RouteJson.Tests/Support.fs` over **real** values (no mocks): (a) reuse/adapt the F019 fixture style to build a real `Config.Model.TypedFacts` (declared domains, checks, a `PathMap` of `glob → domain`, surfaces, tooling commands) — the genuine `Valid TypedFacts` a downstream caller holds; (b) a `resultOf : TypedFacts -> GovernedPath list -> RouteResult` convenience chaining the real `Gates.buildRegistry` → `Routing.route` → `Findings.findUnknownGovernedPaths` → `Route.select` (mirroring the F019 `selectOf`); (c) `JsonDocument` read helpers — `parse : string -> JsonDocument`, plus small accessors (e.g. `selectedGateIds`, `findingIds`, `costTier`, field-presence/absence probes, and a top-level field-order extractor) for read-only inspection of the emitted bytes. These produce/inspect REAL outputs, never fakes.
- [X] T008 [P] Extend `scripts/prelude.fsx` with an F020 design sketch that `#r`s the built `FS.GG.Governance.RouteJson` assembly, opens the namespace, projects the existing F019 `f19Result` fixture via `RouteJson.ofRouteResult`, prints `RouteJson.schemaVersion`, prints the document bytes + length, asserts byte-identity on a second projection, and projects the empty route (`Route.select` over an empty change) to show the empty-but-valid document with all-zero cost — recording the intended projection flow before the real body lands (Principle I; mirrors [quickstart.md](./quickstart.md) §FSI smoke).
- [X] T009 [P] Create `specs/020-route-json-projection/readiness/README.md` listing the required FSI transcripts (the `f19Result` projection showing the full document with selected gates in `GateId` order + each gate's metadata/route-trace/freshness-key inputs; the carried governed-root finding; a twice-identical determinism run; an empty-route document with empty sections + all-zero cost) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.RouteJson` and `dotnet test tests/FS.GG.Governance.RouteJson.Tests` compile against the stub; the solution lists the two new projects; the single Route reference resolves (Gates/Routing/Findings/Config transitively); the Support helpers assemble a real `RouteResult` and parse a string with `JsonDocument`.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the `schemaVersion` constant, the hidden closed-enum token helpers + sub-object writers, and the top-level `ofRouteResult` writer skeleton (the fixed-order `Utf8JsonWriter` walk) — everything the stories specialize. **No user-story work begins until this phase is complete.**

- [X] T010 Implement `RouteJson.schemaVersion` in `src/FS.GG.Governance.RouteJson/RouteJson.fs` as the fixed declared contract-version constant (`"fsgg.route/v1"` per [contracts/route-json-document.md](./contracts/route-json-document.md), FR-013) — a plain string literal, never derived from a clock/environment/input.
- [X] T011 Implement the **hidden** closed-enum token helpers in `src/FS.GG.Governance.RouteJson/RouteJson.fs` (absent from `RouteJson.fsi`, mirroring `Kernel/Json.fs`'s `severityToken`/`stateToken`): `costToken : Cost -> string` (`cheap`/`medium`/`high`/`exhaustive`), `maturityToken : Maturity -> string` (`observe`/`warn`/`blockOnPr`/`blockOnShip`/`blockOnRelease`), `environmentToken : EnvironmentClass -> string` (`local`/`ci`/`localOrCi`/`release`), and `zoneToken`/zone-writer for `FindingZone` (`GovernedRootUnknown` → `"governedRootUnknown"`; `ProtectedBoundaryUnknown sid` → `{ "protectedBoundary": "<surfaceId>" }`). Each `match` is **exhaustive over the closed DU with no wildcard** (research D3), so a future case is a compile error here, never a silently mis-tokened field.
- [X] T012 Implement the **hidden** sub-object writers in `src/FS.GG.Governance.RouteJson/RouteJson.fs` against a `Utf8JsonWriter`, each emitting its documented field order verbatim (FR-007, [contracts/route-json-document.md](./contracts/route-json-document.md)): `writeFreshnessKey` (`check`, `domain`, `cost`, `environment`, `command` — `CommandId option` as string or JSON `null`, never a cache verdict, FR-014); `writePrerequisite` (`RequiresCommand c` → `{ "requiresCommand": "<commandId>" }`); `writeSelectingPath` (`{ "path", "matchedGlob" }`); `writeFinding` (`id` via `Findings.findingIdToken`, `path`, `zone` via T011, `message` verbatim). Newtype unwraps (`DomainId`/`Owner`/`CheckId`/`CommandId`/`SurfaceId`/`GovernedPath`/`TimeoutLimit`) happen at the use site. Disclose any `mutable`/`for` writer idiom at its use site (Principle III — the plain BCL `Utf8JsonWriter` idiom).
- [X] T013 Implement the `ofRouteResult` writer skeleton in `src/FS.GG.Governance.RouteJson/RouteJson.fs`: create a `Utf8JsonWriter` over a pooled buffer with **default** (compact) options; write the top-level object in the FIXED order `schemaVersion` → `selectedGates` (array) → `findings` (array) → `cost` (object); flush and decode the buffer to a UTF-8 string. Walk `result.SelectedGates` in order writing each gate object (US1 fills the gate fields), `result.Findings.Findings` in order via `writeFinding` (US3 asserts), and `result.Cost` via the cost writer (US2/cost asserts) — preserving every collection's existing order, **re-sorting nothing** (FR-005/FR-007). PURE and TOTAL — never throws; the empty route yields `{ schemaVersion, "selectedGates": [], "findings": [], "cost": {0,0,0,0} }`, a valid success (FR-008/FR-009).

**Checkpoint**: the library builds with the real `schemaVersion` + token helpers + sub-object writers + the top-level walk; `ofRouteResult` over an empty route returns the empty-but-valid document; the document parses as one top-level object with fields in the fixed order; the surface compiles against `RouteJson.fsi`.

---

## Phase 3: User Story 1 - Render a route result to a deterministic route.json (Priority: P1) 🎯 MVP

**Goal**: project a real `RouteResult` so the document lists each selected gate by its declared `GateId` with its carried F018 metadata (domain, cost, timeout, owner, maturity, productCheck, prerequisites, description) and its route trace (every selecting path + the glob each won on, the gate appearing once however many paths reached it); no non-selected gate appears; and the cost section always presents every declared tier (FR-006).

**Independent Test**: project a real upstream-assembled `RouteResult` with ≥2 selected gates (one reached by ≥2 paths); parse the document and assert one `selectedGates[*]` per `result.SelectedGates` with matching `id`/`domain`/`cost`/`timeout`/`owner`/`maturity`/`productCheck`/`prerequisites`, each carrying its `selectingPaths` (`path` + `matchedGlob`); assert the multi-path gate appears once with all its paths; assert no extra gate id appears; assert `cost` carries all four integer tiers.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T014 [P] [US1] In `tests/FS.GG.Governance.RouteJson.Tests/ProjectionTests.fs`, add projection tests over a real `resultOf` fixture, inspecting the emitted bytes via `JsonDocument`: (1) every selected gate is present exactly once, by declared `id` (via `gateIdValue`), with its carried `domain`/`description`/`cost`/`timeout`/`owner`/`maturity`/`productCheck`/`prerequisites` matching the embedded `Gate` verbatim (US1 AS1, **SC-001**); (2) a gate reached by ≥2 `Routed` paths appears **once** with **all** selecting paths (`path` + `matchedGlob`) in `selectingPaths`, in normalized-path order (US1 AS2, FR-004); (3) **no** gate that `result.SelectedGates` did not contain appears, and no gate/cost/path is invented (US1 AS3 / FR-003); (4) the empty selected-gate route projects to a present-and-empty `selectedGates` array with the all-zero `cost` — never a "select everything" placeholder (US1 AS3 / FR-009); (5) `cost` always carries integer `cheap`/`medium`/`high`/`exhaustive` (every tier present incl. zero), never a summed scalar (FR-006, **SC-005**); (6) a gate whose `DomainId`/`GateId` carries the gate-id separator (e.g. a colon, as in `build:tests`) renders `id` and `domain` **verbatim** — the emitted `id` equals `gateIdValue sg.Gate.Id` and the emitted `domain` equals the declared `DomainId` unwrapped, with no re-parse and no separator re-derivation (spec edge case "domain identifier containing the gate-id separator", FR-008/FR-010); (7) a route whose distinct selected gates all fall in **one** cost tier, and a second route whose gates are **spread across** tiers, each render the per-tier `cost` counts faithfully — the populated tier(s) at their true counts and **zero** for every absent tier (spec edge cases "all gates in one cost tier / spread across tiers", FR-006); (8) a gate `description` (paired with the finding `message` in T021) containing JSON-special characters (`"`, `\`, a newline) round-trips: the value read back from the parsed `JsonDocument` equals the input string exactly — faithful carry with escaping delegated to the writer, never manual (FR-002/FR-005).

### Implementation for User Story 1

- [X] T015 [US1] Complete the per-gate object writer inside `ofRouteResult` (`src/FS.GG.Governance.RouteJson/RouteJson.fs`, building on T013): for each `SelectedGate sg` emit the documented field order — `id` (via `Gates.gateIdValue sg.Gate.Id`, verbatim, never re-parsed, FR-010), `domain`, `description`, `cost` (T011 `costToken`), `timeout` (int seconds), `owner`, `maturity` (T011 `maturityToken`, declared verbatim — **not** enforcement, FR-011), `productCheck` (bool), `prerequisites` (array via `writePrerequisite`), `freshnessKey` (object via `writeFreshnessKey` — asserted in US3), and `selectingPaths` (array via `writeSelectingPath`, in the gate's existing normalized-path order). Implement the `cost` object writer (`cheap`/`medium`/`high`/`exhaustive` ints from `result.Cost`). Carry the embedded `Gate` verbatim — re-derive nothing (FR-002). Free-text values (`description`, finding `message`) are written through the `Utf8JsonWriter` string API so JSON-escaping is the writer's job — **no** manual escaping or pre-processing (FR-002/FR-005, asserted by T014(8)).

**Checkpoint**: a real route projects to a document listing exactly its selected gates with full carried metadata + route trace, deduped to one entry per gate, no non-selected gate present, and a complete cost rollup — the MVP. US1 stands alone.

---

## Phase 4: User Story 2 - A stable, versioned schema for CI and agents (Priority: P1)

**Goal**: identical inputs produce a byte-identical document; value-equal inputs assembled from differently-ordered upstream inputs produce identical documents; the document carries a declared `schemaVersion` and a stable documented field order; and it contains no clock/host/environment value and none of the excluded enforcement/verdict/raw-YAML tokens.

**Independent Test**: project the same `RouteResult` twice and assert byte-for-byte equality; project two `RouteResult`s built from permuted candidate paths + permuted registry gates and assert identical strings; assert the `schemaVersion` field equals `RouteJson.schemaVersion` and the top-level field order is `schemaVersion`,`selectedGates`,`findings`,`cost`; run the exclusion sweep over the emitted text.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T016 [P] [US2] In `tests/FS.GG.Governance.RouteJson.Tests/DeterminismTests.fs`, add an FsCheck **twice-identical** property and a fixed-fixture equality: `ofRouteResult r = ofRouteResult r`, byte-for-byte (US2 AS1, **SC-002**).
- [X] T017 [P] [US2] In the same file, add a **permutation-invariance** test: project two `RouteResult`s that are value-equal but assembled from differently-ordered upstream inputs (shuffle the `GovernedPath list` passed to the chain, and pass a `GateRegistry { Gates = shuffled }` built from a real registry's gates directly to `Route.select`, mirroring F019's T022) and assert the two emitted strings are identical (US2 AS2, **SC-003**).
- [X] T018 [P] [US2] In the same file, add a **schema-version + field-order** test: parse the document and assert the `schemaVersion` field equals `RouteJson.schemaVersion`, and assert the top-level field order is exactly `schemaVersion`, `selectedGates`, `findings`, `cost` (US2 AS3, FR-013).
- [X] T019 [P] [US2] In the same file, add the **exclusion sweep** (US2 AS4, **SC-007**, FR-011/FR-012) over a real findings-bearing, multi-gate route so the sweep covers populated sections: (a) a **deny-token** check — the emitted text contains none of `severity`, `profile`, `mode`, `enforcement`, `cacheEligib`, ship `verdict`, `blockers`, `warnings`, `exitCode`, `expectedArtifacts`, raw YAML, a wall-clock timestamp, or any environment-derived value; (b) a **positive path-allowlist** check (replacing the fragile leading-`/`/drive-letter heuristic) — parse the document and assert every emitted `path` and `matchedGlob` (across all `selectingPaths` and all `findings`) equals a declared `GovernedPath` string from the input route, so no host/absolute path can appear by construction (FR-012).

### Implementation for User Story 2

- [X] T020 [US2] Confirm/complete determinism in `ofRouteResult` (`src/FS.GG.Governance.RouteJson/RouteJson.fs`): default (compact) `Utf8JsonWriter` options (no indentation), fixed field order throughout, every collection emitted in `RouteResult`'s existing order with **no** re-sort, **no** `Map` iteration, and **no** clock/host/environment value introduced. Note explicitly if no change was needed beyond Foundation/US1. **Determinism is a property of the Foundation walk + US1 writers; record here whether any residual input-order or option-default leakage required a fix.**

**Checkpoint**: the document is byte-stable, permutation-invariant, version-stamped, fixed-field-ordered, and free of every excluded token — usable as a CI/agent contract and a golden snapshot. US1 + US2 together are the co-equal P1 MVP pairing.

---

## Phase 5: User Story 3 - Findings and freshness carried forward, enforcement excluded (Priority: P2)

**Goal**: the document carries the F017 findings unchanged in F017 order (empty report → present-and-empty array), and each selected gate carries its declared freshness-key **inputs** (`check`/`domain`/`cost`/`environment`/`command`) — but no cache-eligibility verdict, severity, profile, mode, or enforcement field anywhere.

**Independent Test**: project a `RouteResult` whose carried `FindingReport` is non-empty and assert `findings[*]` matches `result.Findings.Findings` one-to-one (`id`/`path`/`zone`/`message`) in the same order; project an empty-findings route and assert `findings` is `[]`; assert each gate's `freshnessKey` carries the five inputs and that no cache/enforcement token appears.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T021 [P] [US3] In `tests/FS.GG.Governance.RouteJson.Tests/CarryTests.fs`, add carry-through tests over real fixtures, inspecting the emitted bytes: (1) a **non-empty** F017 report → `findings[*]` matches `result.Findings.Findings` one-to-one by `id` (via `findingIdToken`)/`path`/`zone`/`message`, in F017 order, unchanged (US3 AS1, **SC-004**); (2) an **empty** F017 report → `findings` is a present-and-empty array, never omitted, never a fabricated default (US3 AS2); (3) each selected gate's `freshnessKey` carries `check`/`domain`/`cost`/`environment`/`command` (command as string or JSON `null`), the carried key **inputs** (US3 AS3, FR-014); (4) **no** cache-eligibility verdict, severity, profile, mode, or enforcement field appears anywhere in the document (US3 AS4, FR-011) — assert via field-absence probes, complementing the US2 text sweep.

### Implementation for User Story 3

- [X] T022 [US3] Confirm the findings array and `freshnessKey` object in `ofRouteResult` (`src/FS.GG.Governance.RouteJson/RouteJson.fs`): `result.Findings.Findings` written via `writeFinding` (T012) in F017 order, **unchanged** — no re-sort/re-derive/re-classify/filter (FR-005); each gate's `freshnessKey` written via `writeFreshnessKey` (T012) carrying only the five declared inputs (FR-014). Verify there is **no** code path emitting a cache verdict or enforcement field. Note explicitly if no change was needed beyond Foundation/US1.

**Checkpoint**: one document explains both selected gates and unmatched governed paths; findings are byte-faithful to F017; empty findings is a present-and-empty array; each gate carries its freshness inputs and no cache/enforcement verdict.

---

## Phase 6: User Story 4 - Total over any well-typed route result (Priority: P2)

**Goal**: `ofRouteResult` returns a document for every `RouteResult` the upstream rows can produce — empty, single-gate, many-gate, findings-only — and never throws; the empty route and the findings-only route are valid successes.

**Independent Test**: FsCheck over generated well-typed `RouteResult`s asserting `ofRouteResult` always returns a (parseable) string and never throws, including the empty route, a findings-only route (no selected gates but non-empty findings), and a large many-gate route.

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T023 [P] [US4] In `tests/FS.GG.Governance.RouteJson.Tests/TotalityTests.fs`, add the **empty-route** and **findings-only** tests: (1) `ofRouteResult` over the empty route (`SelectedGates = []`, empty `Findings`, all-zero `Cost`) returns a valid document with empty `selectedGates`/`findings` arrays + all-zero `cost`, never throwing (US4 AS1, FR-009, **SC-006**); (2) a route with empty `selectedGates` but **non-empty** `findings` projects with both sections coexisting (US4 AS2).
- [X] T024 [P] [US4] In the same file, add an FsCheck **totality** property over generated well-typed `RouteResult`s (including empty, single-gate, many-gate, findings-only): `ofRouteResult` **always returns a parseable string and never throws** (US4 AS3, **SC-006**). **Generator provenance (resolve before writing the test):** generate each `RouteResult` by driving the **real chain** — an FsCheck generator over `TypedFacts`/candidate-path inputs fed through `resultOf` (`Gates.buildRegistry` → `Routing.route` → `Findings.findUnknownGovernedPaths` → `Route.select`) — so the inputs stay real upstream-assembled values (research D7), keeping the "no synthetic evidence" stance honest. If, and only if, a case is unreachable through the chain and a `RouteResult` value must be constructed directly, that arbitrary is **synthetic**: name the property with the `Synthetic` token, add a use-site `// SYNTHETIC:` disclosure naming the case and why the chain can't produce it, and list it in the PR (Principle V). Prefer the real-chain generator.

### Implementation for User Story 4

- [X] T025 [US4] Confirm `ofRouteResult` (`src/FS.GG.Governance.RouteJson/RouteJson.fs`) is total: only closed-DU `match`es (exhaustive, no wildcard) + single-case newtype unwraps + a `Utf8JsonWriter` walk — no partial function, parse, division, or I/O. Verify the empty and findings-only routes flow through the same code path as a populated route (no special-casing). Note explicitly if no change was needed beyond earlier phases.

**Checkpoint**: the projection returns a document for 100% of well-typed route results, including the empty and findings-only routes, and never throws — callable unconditionally by later rows.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: lock the public surface, prove the dependency boundary, and finish the docs/evidence.

- [X] T026 [P] Generate `surface/FS.GG.Governance.RouteJson.surface.txt` capturing exactly the public `RouteJson` module (`schemaVersion`, `ofRouteResult` — the `.fsi` surface), nothing private (no token helpers, sub-object writers, or buffer plumbing).
- [X] T027 In `tests/FS.GG.Governance.RouteJson.Tests/SurfaceDriftTests.fs`, add the surface-drift test asserting the built public surface matches `surface/FS.GG.Governance.RouteJson.surface.txt` (Principle II, with `BLESS_SURFACE=1` regen path), assert "exactly the `RouteJson` module, nothing private," and assert the `RouteJson → Route` one-way dependency (no kernel/host/adapters/snapshot/CLI edge; no new third-party `PackageReference`) — mirroring the F019 `SurfaceDriftTests` dependency assertion.
- [X] T028 [P] Verify [quickstart.md](./quickstart.md) end-to-end: run the documented `dotnet test` and the prelude FSI smoke (the F015→F017→F018→F019 chain then `RouteJson.ofRouteResult`), confirm the acceptance→evidence map holds, and fill `specs/020-route-json-projection/readiness/README.md` with the real FSI transcripts (T009) and the SC-001…SC-007 traceability note.
- [X] T029 [P] Update [`specs/020-route-json-projection/plan.md`](./plan.md) with an **Implementation Progress** header (status table + evidence summary, mirroring the F019 plan) once the suite is green, and confirm `CLAUDE.md`'s SPECKIT block points at this plan.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundation (Phase 2)** — depends on Setup; **BLOCKS all user stories** (the `schemaVersion`, the token helpers, the sub-object writers, and the top-level `ofRouteResult` walk everything specialises).
- **User Stories (Phases 3–6)** — all depend on Foundation. US1 (P1) is the MVP. US2 (P1) proves determinism/version/exclusion over the document US1 produces. US3/US4 (P2) build on it: US3 asserts the findings/freshness carry the Foundation writers wire, US4 proves totality across all of it.
- **Polish (Phase 7)** — depends on all desired user stories being complete.

### User-story dependencies

- **US1 (P1)** — after Foundation; no dependency on other stories (the core gate-object render + cost rollup).
- **US2 (P1)** — after Foundation; reads the same document US1 produces (determinism/version/field-order/exclusion are properties of the whole walk). Independently testable.
- **US3 (P2)** — after Foundation; findings/freshness carry-through is independent of which gates are selected (asserts coexistence + faithful carry). Independently testable.
- **US4 (P2)** — after the document is *correct* (US1–US3); proves its *totality* over every well-typed input.

### Within each user story

- Tests are written first and MUST FAIL before implementation (Principle I/V).
- `schemaVersion` + token helpers + sub-object writers + top-level skeleton (Foundation) before any story.
- Each story is independently completable and testable; complete a story before moving to the next priority.

### Parallel opportunities

- **Setup**: T005, T007, T008, T009 are `[P]` (distinct files) once T001–T004 exist.
- **Tests across stories**: T014, T016–T019, T021, T023, T024 are `[P]` — distinct test files (`ProjectionTests`/`DeterminismTests`/`CarryTests`/`TotalityTests`), no shared state.
- **Stories**: once Foundation is done, US1–US4 test-writing can proceed in parallel by different developers; the implementation tasks (T015, T020, T022, T025) all touch `RouteJson.fs`, so serialize those edits (or have one owner sweep them in phase order — most are "confirm/complete" since the Foundation walk + US1 writers already cover them).
- **Polish**: T026, T028, T029 are `[P]`; T027 depends on T026.

---

## Parallel Example: cross-story test authoring

```bash
# After Foundation (Phase 2), launch the per-story test files together (distinct files):
Task: "ProjectionTests.fs  — US1 selected gates + carried metadata + route trace + cost (T014)"
Task: "DeterminismTests.fs — US2 twice-identical + permutation + version/field-order + exclusion sweep (T016–T019)"
Task: "CarryTests.fs       — US3 findings unchanged + freshness-key inputs + no cache/enforcement (T021)"
Task: "TotalityTests.fs    — US4 empty + findings-only + FsCheck totality (T023–T024)"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: a real route projects to a document listing exactly its selected gates with full carried metadata + route trace, deduped to one entry per gate, no non-selected gate, complete cost rollup.

### Incremental delivery

1. Setup + Foundation → foundation ready.
2. US1 → the route result renders to a document → the MVP.
3. US2 → the document is a deterministic, versioned, exclusion-clean contract (the co-equal P1).
4. US3 → findings + freshness inputs carried, enforcement excluded.
5. US4 → totality proven over every well-typed input.
6. Polish → surface baseline + dependency assertion + readiness/quickstart.

---

## Notes

- `[P]` = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- The four `RouteJson.fs` implementation tasks (T015, T020, T022, T025) edit one file — serialize them in phase order; most are "confirm/complete," since the Foundation writers (T010–T013) already wire the projection.
- Tests inspect the **emitted bytes** via read-only `JsonDocument` (the kernel `Json`-test precedent), never private helpers — `ofRouteResult` and `schemaVersion` are the entire public surface.
- **No synthetic evidence is anticipated** (research D7) — every case (empty/single/many-gate, findings-only, shared-gate, all-tiers, separator-in-domain, JSON-special free text) is reachable from real upstream-assembled inputs, and the FsCheck totality property (T024) generates its `RouteResult`s by driving the real F015→F019 chain rather than constructing values directly. Any unavoidable literal or directly-constructed FsCheck arbitrary carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- Scope guards (FR-011/FR-012/FR-015): no round-trip parse, severity, enforcement, cache verdict, ship verdict, blockers, warnings, exit code, raw YAML, host path, timestamp, environment value, CLI, or audit.json — the projection stops at the document string, adds no third-party dependency.
