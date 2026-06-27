---
description: "Task breakdown for SDD→Governance Handoff Consumer (enforce, not just produce)"
---

# Tasks: SDD→Governance Handoff Consumer (enforce, not just produce)

**Input**: Design documents from `/specs/081-sdd-handoff-consumer/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D9), data-model.md, contracts/
(consumer-surface.md, host-wiring.md, handoff-document.md), quickstart.md

**Tier**: Tier 1 (new public library + additive public `Loop.fsi`/`Interpreter.fsi` on three
hosts + new cross-project dependency + ADR/tutorial lockstep). The full chain applies:
spec → `.fsi` → semantic tests → `.fs` bodies (Constitution I).

**Tests**: REQUIRED. The constitution mandates semantic tests before bodies (Principle I) and
real-evidence tests through the live pipeline (Principle V); every success criterion is a
behavioural assertion. Test tasks are first-class here, not optional.

**Elmish/MVU**: This feature is I/O-bearing (handoff file location + read). Per the skill's MVU
discipline, US1 carries explicit tasks for the `.fsi` contract (`Effect`/`Msg`/`Ports`), the
pure `update` fold, emitted-effect assertions, and real-interpreter evidence. The library's
parse/map/readiness layers are pure (no MVU obligation there).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — different files, no dependency on another incomplete task in the phase.
- **[Story]**: `US1`/`US2`/`US3` (or none for Setup/Foundational/Polish).
- Tier annotations omitted (every phase is Tier 1, matching the spec's overall tier).

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped with written rationale on the line.
- Never mark a failing task `[X]`. Never weaken an assertion to green a build — narrow scope and document it.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new leaf library + test project and register them in the solution
(research D1). No behaviour yet.

- [X] T001 Create the new leaf library project at
  `src/FS.GG.Governance.Adapters.SddHandoff/FS.GG.Governance.Adapters.SddHandoff.fsproj`
  (`net10.0`; model the fsproj on `src/FS.GG.Governance.Adapters.SpecKit/…SpecKit.fsproj`).
  Add `ProjectReference`s to the kernel (`Evidence`), `Config`, `Gates`, and `Route`
  libraries it maps onto (data-model §7). Add **no** SDD `ProjectReference` and **no** new
  package (`System.Text.Json` is BCL; `Directory.Packages.props` unchanged — research D2,
  FR-013/SC-006).
- [X] T002 [P] Create the test project at
  `tests/FS.GG.Governance.Adapters.SddHandoff.Tests/FS.GG.Governance.Adapters.SddHandoff.Tests.fsproj`
  (Expecto + YoloDev.Expecto.TestSdk like the repo's other test projects; `IsPackable=false`;
  `ProjectReference` to the new library).
- [X] T003 Register both new projects in `FS.GG.Governance.sln` (src + tests).

**Checkpoint**: `dotnet build FS.GG.Governance.sln` resolves the two empty projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Draft every public `.fsi` BEFORE any `.fs` body (Constitution I), wire compile
order, stand up the surface-baseline drift + dependency-hygiene guards, and commit the shared
fixtures. Both the library stories AND the host wiring depend on these contracts.

**⚠️ CRITICAL**: No user-story body work begins until this phase is complete.

- [X] T004 [P] Author `src/FS.GG.Governance.Adapters.SddHandoff/Model.fsi` per
  contracts/consumer-surface.md §`Model`: `DeclaredState` (`Pending|Real|Synthetic|Failed|
  Skipped|Deferred|AcceptedDeferral` — **no** `AutoSynthetic`, FR-005), `DeclaredNode`,
  `EvidenceBlock`, `ReadinessBlock`, `GovernedReference`, `Handoff`, `DiagnosticCause`
  (`VersionMismatch|Malformed|AutoSyntheticDeclared|StaleEvidence`), `Diagnostic`, and
  `val supportedContractMajor: int` (= 1). Reuse `Config.Model.GovernedPath` (never redefine).
- [X] T005 [P] Author `…/Reader.fsi` per §`Reader`: `type HandoffRead = { Source; Json }` and
  `val parse: HandoffRead -> Result<Handoff, Diagnostic>` (pure, total, never throws).
- [X] T006 [P] Author `…/Mapping.fsi` per §`Mapping`: `val mapEvidence` and
  `val effectiveStates` (returning `Result<Map<string,EvidenceState>, Diagnostic>`). Reuse
  `Kernel.Evidence.{EvidenceState,build,effective}`.
- [X] T007 [P] Author `…/Readiness.fsi` per §`Readiness`: `val toGate: source:string ->
  block:ReadinessBlock -> Gate` (reuse `Gates.Model.{Gate,GateId,Maturity,Cost,DomainId}`).
- [X] T008 Author `…/Consumer.fsi` per §`Consumer`: `type ConsumeResult = { Gates; Selected;
  Diagnostics }` and `val consume: HandoffRead list -> ConsumeResult`. Reuse
  `Route.Model.{SelectedGate,SelectingPath,RouteResult}`. (After T004–T007.)
- [X] T009 Add ordered `<Compile>` pairs (`.fsi` then `.fs`) to the fsproj in dependency order
  `Model → Reader → Mapping → Readiness → Consumer`, and add minimal compiling `.fs`
  skeletons (bodies `failwith "TODO Txxx"`) so the solution builds and the first tests can be
  authored RED before real bodies (Constitution I).
- [X] T010 Create the surface drift + dependency-hygiene guard `SurfaceDriftTests.fs` in the
  test project (reflective baseline check against
  `surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt`, mirroring the repo's other
  surface-drift tests) and generate the baseline via `BLESS_SURFACE=1`. Include a scope-guard
  asserting the library has **no** dependency on any SDD assembly (SC-006). It will track the
  surface as `.fs` bodies land; final bless is T034.
- [X] T011 [P] Commit fixture handoffs under
  `tests/FS.GG.Governance.Adapters.SddHandoff.Tests/fixtures/`: `satisfied`, `failing`,
  `v2-major`, `malformed`, `missing-required`, `autoSynthetic`, `stale`, `deferred`,
  `readiness-blocking`, `readiness-clean` (quickstart Prereqs). Cross-check the JSON key
  spellings against `contracts/handoff-document.md` (the in-feature read-only shape doc) +
  ADR 0002 + `docs/tutorials/sdd-governance-handoff.md` and the sibling `FS.GG.SDD`
  `017-governance-handoff` contract (research D8) — the fixture + Model are the single
  adjustment point if a spelling differs.

**Checkpoint**: all `.fsi` curated, solution builds with stub bodies, surface baseline +
hygiene guard green, fixtures present. Stories can begin.

---

## Phase 3: User Story 2 — Safe read + version-check the contract (Priority: P1)

**Goal**: Load + version-pin a present handoff; an unrecognized contract major, a malformed
document, or a declared `autoSynthetic` yields a distinct, descriptive diagnostic and **no**
mapped result — never a silent misread, never a crash (FR-002/005/011, SC-004).

**Independent Test**: feed `Reader.parse` a well-formed `v1.x` handoff, a `contractVersion`
major `2`, and a garbage file; confirm `Ok` / `Error VersionMismatch` / `Error Malformed`
with distinct messages and no throw.

> US2 is the read foundation US1's enforcement stands on; sequenced first per plan §Structure
> Decision (US2 reader → US1 evidence + wiring → US3 readiness).

### Tests for User Story 2 (write FIRST, ensure RED)

- [X] T012 [P] [US2] `ReaderTests.fs`: well-formed `v1.x` → `Ok handoff` (every
  `evidence.nodes[].state` ∈ `{pending,real,synthetic,failed,skipped}` round-trips); unknown
  major (`2.0.0`) → `Error {Cause=VersionMismatch}`; malformed JSON → `Error {Cause=Malformed}`;
  missing required field → `Error {Cause=Malformed}`; a node declaring `state:"autoSynthetic"`
  → `AutoSyntheticDeclared`; assert messages are distinct per cause and `parse` never throws
  (FR-002/005/011, SC-004). Unknown additive (minor) fields ignored (research D8).

### Implementation for User Story 2

- [X] T013 [US2] Implement `Model.fs` (the record/union bodies + `supportedContractMajor = 1`).
- [X] T014 [US2] Implement `Reader.parse` in `Reader.fs` using `System.Text.Json`
  (`JsonDocument.Parse` → `JsonElement`, research D2): fail-fast on malformed/missing-required;
  pin `contractVersion` major to `supportedContractMajor`; ignore unknown minor fields; return
  typed `Diagnostic`, never throw (Constitution VI). **`Reader` is the authoritative layer for
  the `autoSynthetic` rejection**: the literal node-state token `"autoSynthetic"` maps to
  `Cause = AutoSyntheticDeclared` (a distinct diagnostic, **not** generic `Malformed` — as T012
  asserts). Mapping/`Evidence.build` keep an independent `autoSynthetic` check as defence-in-depth
  only (research D4), never the sole guard.

**Checkpoint**: T012 green. The consumer reads and refuses-to-misread the contract.

---

## Phase 4: User Story 1 — A produced handoff drives a Governance verdict (Priority: P1) 🎯 MVP

**Goal**: Map declared evidence through `Evidence.build`/`effective`, derive a blocking-capable
evidence gate, and wire the consumer into `route`/`ship`/`verify` so a failing/blocking
declaration changes the verdict relative to a satisfied one — end-to-end (FR-003/004/006/007/
008/010/012, SC-001/003).

**Independent Test**: two temp products differing only in their handoff's declared evidence
(satisfied vs a `failed` node) run through the **real** ship pipeline yield materially
different verdicts traceable to the declared evidence; a product with no handoff is a no-op.

**Depends on US2** (`Reader.parse`). Mapping/Consumer + host wiring land here; US3 reuses the
wiring.

### Tests for User Story 1 (write FIRST, ensure RED)

- [X] T015 [P] [US1] `MappingTests.fs`: one case per ADR-0002 row, each **named for / commented
  with its row** (SC-002): straight-through `pending/real/synthetic/failed/skipped` →
  same `EvidenceState`; `deferred`/`accepted-deferral` → `Skipped` (not `Pending`, FR-004);
  `stale` → underlying state **+** `StaleEvidence` diagnostic (FR-006); `autoSynthetic` →
  rejected (FR-005); `Evidence.effective` taint closure produces a `Failed`/`AutoSynthetic`
  effective state that makes the gate blocking-capable (research D4).
  **SC-002 traceability note**: this task covers the *evidence-mapping* rows only; the remaining
  ADR-0002 rows are exercised elsewhere — `governedReferences` optional in T016, `readiness.* as
  a gate` in T024, `unknown major → version-mismatch` in T012. SC-002's "100% of rows" is
  satisfied jointly by **T012 + T015 + T016 + T024**, each case named for its row.
- [X] T016 [P] [US1] `ConsumerTests.fs`: parse→gates over fixtures — failing evidence ⇒ a
  blocking evidence gate; satisfied ⇒ advisory `warn`; a bad document ⇒ a blocking integrity
  gate + diagnostic and **no** mapped gate for that document (no partial enforce, FR-011);
  zero handoffs ⇒ empty `ConsumeResult`; multiple handoffs loaded in `<id>` order, gates
  sorted by `GateId` (FR-012, research D7); `governedReferences` absent vs present does not
  change correctness (FR-010).
- [X] T017 [P] [US1] `FS.GG.Governance.ShipCommand.Tests` (additive): real-pipeline
  verdict-delta — two fixture products (satisfied vs failing handoff) through
  Config→Gates→Routing→Route→Enforcement→`Ship.rollup` yield `Pass` vs `Fail` with the
  blocking handoff evidence gate in `Blockers` (SC-001); a pure-transition assertion that
  `HandoffsLoaded` keeps `update` pure and that `LoadHandoffs` is the emitted effect; a no-op
  golden — absent handoff ⇒ the host's **existing committed goldens still match byte-for-byte**
  (the absent-handoff fold is identity, so no golden re-bless is needed; SC-003).
- [X] T018 [P] [US1] `FS.GG.Governance.RouteCommand.Tests` (additive): handoff gates appear as
  selected gates in `gates.json` / `route.json`; absent handoff ⇒ the host's **existing committed
  goldens still match byte-for-byte** (no re-bless; SC-003). **Interpreter-side determinism test
  (Principle IV interpreter boundary + FR-012 edge)**: drive the real `Interpreter.Ports.Handoffs`
  port against a temp repo with ≥2 `readiness/<id>/governance-handoff.json` dirs and assert the
  reads come back in stable `<id>` (ordinal) order, and that an empty repo ⇒ `[]`. This covers the
  *impure* file-location/ordering edge that the `Consumer` determinism test (T016) assumes given
  reads.

### Implementation for User Story 1

- [X] T019 [P] [US1] Implement `Mapping.fs`: `mapEvidence` (ADR-0002 row map → `(id,
  EvidenceState) list` + carried `(string*string)` deps, with `deferred→Skipped`,
  `stale→state + StaleEvidence` diagnostic, `autoSynthetic→Error`) and `effectiveStates`
  (`Evidence.build` then `Evidence.effective` for the taint closure) (research D4).
- [X] T020 [US1] Implement `Consumer.fs` `consume`: per document parse (Reader) → map
  (Mapping) → build the evidence gate with **`Config.Model.Maturity.BlockOnShip`** when an
  effective state is `Failed`/`AutoSynthetic`, **`Maturity.Warn`** (advisory) when all satisfied
  — and the blocking handoff-integrity gate (bad document) likewise `BlockOnShip`. `BlockOnShip`
  is the token that `Enforcement.deriveEffectiveSeverity` resolves to **blocking under both
  ship AND verify** (Verify-blocking under the `Strict` profile, advisory under `Light` — feature
  079 precedent), so a single maturity satisfies SC-001 (ship `Fail`) and SC-005 (verify `Fail`).
  Then build the readiness gate (via
  `Readiness.toGate` — body lands in US3/T029) + a blocking integrity gate for a bad document;
  pre-select all gates (relevance = declared work item; `governedReferences` → `SelectingPath`
  provenance when present, synthetic/empty when absent — FR-010); aggregate across documents in
  `<id>` order, sort gates by `GateId`; empty input ⇒ empty result (research D3/D7).
  (After T019.)
- [X] T021 [P] [US1] `FS.GG.Governance.RouteCommand` host wiring (additive, research D6,
  contracts/host-wiring.md): `Loop.fsi`/`Loop.fs` gain `Effect.LoadHandoffs of repo:string` +
  `Msg.HandoffsLoaded of Reader.HandoffRead list`; `Interpreter.fsi`/`Interpreter.fs` gain
  `Ports.Handoffs: string -> Reader.HandoffRead list` (the only I/O — locate
  `readiness/<id>/governance-handoff.json` in stable `<id>` order, read raw JSON); a pure
  `update` fold after `Route.select` unions `consume`'s gates/selected into the registry +
  `RouteResult.SelectedGates` before the `gates.json`/`route.json` projection. `update` stays
  pure. The `Handoffs` port's stable `<id>`-ordering + empty-repo `[]` behaviour is covered by
  the interpreter-side determinism test in T018 (Principle IV interpreter boundary). (After T020.)
- [X] T022 [P] [US1] `FS.GG.Governance.ShipCommand` host wiring — same additive edge as T021;
  the augmented `RouteResult` flows into the existing `Ship.rollup route mode profile`
  unchanged so handoff gates enforce via `deriveEffectiveSeverity` (a blocking evidence gate ⇒
  `Verdict = Fail`). (After T020.)
- [X] T023 [P] [US1] `FS.GG.Governance.VerifyCommand` host wiring — same additive edge as T021,
  keeping `RunMode = Verify`; fold before `Ship.rollup`. (After T020.)

**Checkpoint**: T015–T018 green. A produced handoff demonstrably moves a `ship`/`route`/`verify`
verdict; absence is a true no-op. **MVP delivered** (Setup + Foundational + US2 + US1).

---

## Phase 5: User Story 3 — SDD merge-boundary readiness as a first-class gate (Priority: P2)

**Goal**: Surface the handoff's `readiness.*` block as a typed gate-registry entry that
participates in selection, severity resolution, and roll-up like any other gate — blocking when
non-shippable or carrying blocking diagnostics, advisory otherwise (FR-009, SC-005). Resolves
ADR-0002 queue item #4 (FR-015).

**Independent Test**: a handoff with a non-shippable `shipDisposition` + non-empty
`blockingDiagnosticIds` ⇒ a selected blocking readiness gate contributing to `Fail`; a clean
shippable handoff ⇒ a present, non-blocking gate.

**Reuses US1 host wiring** (the fold already unions `consume`'s readiness gate); this story
fills `Readiness.toGate` and proves it through `verify`.

### Tests for User Story 3 (write FIRST, ensure RED)

- [X] T024 [P] [US3] `ReadinessGateTests.fs`: `Readiness.toGate` over the `readiness-blocking`
  fixture ⇒ `Maturity.BlockOnShip` (non-shippable disposition OR non-empty
  `blockingDiagnosticIds`); over `readiness-clean` ⇒ `Maturity.Warn` (advisory);
  counts/perViewState carried into the gate description (data-model §4, FR-009).
- [X] T025 [P] [US3] `FS.GG.Governance.VerifyCommand.Tests` (additive): blocking readiness
  fixture ⇒ a **selected** readiness gate in `Blockers` contributing to `Fail` **under the
  `Strict` profile** (where `BlockOnShip` is verify-blocking — pin the profile so the assertion
  is meaningful; cf. T020/T026); clean readiness ⇒ a present, non-blocking gate
  (`Passing`/`Warnings`); no-handoff ⇒ the host's existing committed goldens still match
  byte-for-byte (SC-005, SC-003).

### Implementation for User Story 3

- [X] T026 [US3] Implement `Readiness.fs` `toGate`: map `ReadinessBlock` → typed
  `Gates.Model.Gate` with **`Config.Model.Maturity.BlockOnShip`** when non-shippable disposition
  OR non-empty `BlockingDiagnosticIds`, else **`Maturity.Warn`** (advisory); carry
  counts/perViewState into the gate description (research D3, FR-009). `BlockOnShip` is the same
  token T020 pins for the evidence gate — it resolves to blocking under verify (Strict) so SC-005
  holds. (`Consumer.consume` from T020 already composes it.)

**Checkpoint**: T024–T025 green. Declared readiness enforces as a first-class gate through `verify`.

---

## Phase 6: Polish & Cross-Cutting (docs lockstep, surface bless, validation)

**Purpose**: Keep ADR/tutorial in lockstep with the code (FR-014/015), bless the additive
surfaces, and run the quickstart end-to-end.

- [X] T027 [P] Update `docs/decisions/0002-sdd-governance-handoff-contract.md`: close queue item
  #4 and change the merge-boundary readiness row to "first-class gate-registry entry" (or add a
  superseding note); all other rows unchanged (FR-015, research D9). Record the decision the
  spec asks for.
- [X] T028 [P] Update `docs/tutorials/sdd-governance-handoff.md`: the readiness mapping row now
  reads "first-class gate-registry entry", in lockstep with T027/the code (FR-014, research D9).
- [X] T029 Additive re-bless of the three host surface baselines
  (`surface/FS.GG.Governance.RouteCommand.surface.txt`, `…ShipCommand…`, `…VerifyCommand…`)
  for the new `Loop`/`Interpreter` `Effect`/`Msg`/`Ports` lines, and finalize the new-library
  baseline (T010) — both via `BLESS_SURFACE=1`; confirm existing baseline blocks are otherwise
  byte-identical (Tier 1 additive-only).
- [X] T030 Run quickstart.md Scenarios 1–7 (verdict-delta, safe read, ADR rows, readiness gate,
  no-op, determinism, docs lockstep) and the full `dotnet test FS.GG.Governance.sln` green;
  verify SC-006 (no SDD `ProjectReference`/dependency; `governance-handoff@1` registry +
  SDD-owned contract files unchanged).

---

## Dependencies & Execution Order

### Phase order

- **Phase 1 Setup** — no deps; start immediately.
- **Phase 2 Foundational** — after Setup; **blocks all stories** (all `.fsi`, compile order,
  fixtures, guards).
- **Phase 3 US2 (P1)** — after Foundational; the read foundation for US1.
- **Phase 4 US1 (P1, MVP)** — after US2 (`Reader.parse`); Mapping/Consumer + host wiring.
- **Phase 5 US3 (P2)** — after US1 (reuses the host wiring fold); fills `Readiness.toGate`.
- **Phase 6 Polish** — after the stories whose surface/behaviour it documents/blesses.

### Notable cross-task dependencies (beyond plain phase order)

- T008 (Consumer.fsi) after T004–T007.
- T020 (Consumer.fs) after T019 (Mapping.fs); calls `Readiness.toGate` whose **body** lands in
  T026 — Consumer compiles against the `.fsi`/skeleton and is fully green only once US3 lands
  (US3's `verify` test T025 confirms the readiness path end-to-end).
- T021/T022/T023 (host wiring) after T020.
- T029 (surface bless) after all host `.fsi` edits (T021–T023) and the library bodies.

### Within each user story

- Tests written and RED before bodies (Constitution I/V).
- Library bodies before host wiring; host wiring before host tests pass.

---

## Parallel Opportunities

- **Foundational `.fsi`**: T004/T005/T006/T007 in parallel (distinct files); T008 after.
- **US1 tests**: T015/T016/T017/T018 in parallel (distinct files).
- **US1 host wiring**: T021/T022/T023 in parallel (distinct projects) once T020 lands.
- **US3**: T024/T025 in parallel; T026 after.
- **Polish docs**: T027/T028 in parallel.

---

## Task count per user story

- **Setup**: 3 (T001–T003)
- **Foundational**: 8 (T004–T011)
- **US2 (P1)**: 3 (T012–T014) — 1 test, 2 impl
- **US1 (P1, MVP)**: 9 (T015–T023) — 4 tests, 5 impl
- **US3 (P2)**: 3 (T024–T026) — 2 tests, 1 impl
- **Polish**: 4 (T027–T030)
- **Total**: 30 tasks

## Suggested MVP scope

**Setup + Foundational + US2 + US1** (through T023). This delivers the headline board item —
"enforce, not just produce": a produced handoff's declared evidence demonstrably drives a
`route`/`ship`/`verify` verdict, with safe reads and a true no-op on absence. US3 (readiness as
a first-class gate) and the docs/surface polish layer on top without disturbing the MVP.

---

## Implementation notes (recorded deviations & evidence)

- **Test framework**: Expecto + YoloDev (the repo standard); the spec's "xUnit" wording is
  incidental — xUnit is absent from central `Directory.Packages.props` (same recorded deviation
  as feature 079).
- **Host→`Adapters.*` dependency-hygiene guard relaxed (intended, research D6)**: the three host
  test projects' surface-drift guards forbade any `FS.GG.Governance.Adapters.*` edge. F081
  deliberately adds ONE such edge (the SDD-handoff consumer) to each verdict host, so the guard now
  permits `FS.GG.Governance.Adapters.SddHandoff` specifically while still forbidding every other
  adapter (and kernel/host/cli). The hosts reference only the consumer (a pure value/fold leaf), not
  the kernel directly — the graph stays acyclic.
- **Diagnostics surfaced via the gate, not a separate channel (research D5)**: version-mismatch /
  malformed / autoSynthetic diagnostics are realized as a blocking `sdd-handoff:integrity:<id>` gate
  whose description carries the descriptive message, keeping ONE verdict mechanism. No separate
  `Diagnostics` field was threaded into each host `Model`/output, so the absent-handoff no-op stays
  byte-identical (SC-003) with no render change.
- **Surfaces**: new `surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt`; the three host
  baselines re-blessed ADDITIVELY (new `LoadHandoffs` effect / `HandoffsLoaded` msg / `Handoffs`
  field/port; the only non-additive lines are the record constructors' arity growth, inherent to a
  new field).
- **Evidence**: library suite 31/31; RouteCommand 85/85, ShipCommand 101/101 (SC-001 verdict-delta
  through the real pipeline), VerifyCommand 81/81 (SC-005 readiness gate verify-blocking under
  Strict), Host 18/18 — all green. CLI 49/51: the 2 failures are the pre-existing `dotnet pack`
  local-feed MSBuild-node flake (documented out-of-scope in CLAUDE.md), unrelated to this change.
