---
description: "Task list for Emit Real Cache-Eligibility Verdicts From fsgg route and fsgg ship (F046)"
---

# Tasks: Emit Real Cache-Eligibility Verdicts From `fsgg route` and `fsgg ship`

**Input**: Design documents from `/specs/046-route-ship-cache-eligibility/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/FreshnessSensing.fsi ✅, contracts/route-command-delta.md ✅, contracts/ship-command-delta.md ✅

**Tier**: Tier 1 (contracted change — new public `FreshnessSensing` library + new public surface on two host
commands `RouteCommand`/`ShipCommand`). Tests are **mandatory** (Principle V). The two safety invariants
(information-not-verdict, no-new-failure) are load-bearing and each maps to a test (data-model §6, L1–L7).
Tasks omitting `[T1]`/`[T2]` inherit the feature tier (T1).

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.
Stories map to spec user stories (US1 P1 `route` / US2 P1 `ship` / US3 P2 degrade / US4 P3 determinism).
US1 and US2 are **both P1** and touch disjoint files — once Phase 2 lands they can proceed in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]`/`[US2]`/`[US3]`/`[US4]` traceability; unlabeled = shared infrastructure
- Exact repo-root-relative file paths in every description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new shared `FreshnessSensing` edge library + its test project, wire them into the
build, and re-point the plan reference. Nothing existing is edited beyond the solution file and `CLAUDE.md`.

- [X] T001 Create `src/FS.GG.Governance.FreshnessSensing/FS.GG.Governance.FreshnessSensing.fsproj` — `net10.0`,
  `IsPackable=false`; `ProjectReference`s to `Config` (F014 `CommandId`), `Gates` (F018 `Gate`), `FreshnessKey`
  (F029 `RuleHash`/`ArtifactHash`/`CommandVersion`/`GeneratorVersion`/`Revision`), `FreshnessResolution` (F043
  `SensedFacts`), `EvidenceReuse` (F030 `ReuseStore`); **no** new third-party `PackageReference` (plan
  Engineering Constraints). Compile order `FreshnessSensing.fsi`→`FreshnessSensing.fs`. Mirror the `.fsproj`
  shape of `src/FS.GG.Governance.FreshnessResolution/FS.GG.Governance.FreshnessResolution.fsproj`.
- [X] T002 [P] Create `tests/FS.GG.Governance.FreshnessSensing.Tests/FS.GG.Governance.FreshnessSensing.Tests.fsproj` —
  references the new library **and** the cores (for genuine expected-`SensedFacts`/`ReuseStore` construction via
  the public F029/F030 constructors, no core mocks) plus the central test packages (Expecto, Expecto.FsCheck,
  FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk). Declare `<Compile>` order (`Support.fs` first,
  `Main.fs` last). Mirror `tests/FS.GG.Governance.FreshnessResolution.Tests/...Tests.fsproj`.
- [X] T003 [P] Add both new projects to `FS.GG.Governance.sln`.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` (between `<!-- SPECKIT START/END -->`) at
  `specs/046-route-ship-cache-eligibility/plan.md`.

**Checkpoint**: `dotnet sln list` shows the two new projects; the solution restores (bodies may be stubs).

---

## Phase 2: Foundational (Blocking — the shared sensing edge + design-first proof)

**Purpose**: Build the shared `FreshnessSensing` library both commands depend on (Principle I/II: `.fsi`
first, then `.fs`, then real-bytes tests), bless its surface, and prove the sense→resolve→evaluate→`Some
report` join in FSI **before** either command body changes. **Blocks all user stories** — neither command
can reference the sensor or run the join until this lands.

**⚠️ CRITICAL**: `FreshnessSensing.fsi` is the contract; no command wiring can compile until it exists.

- [X] T005 Author `src/FS.GG.Governance.FreshnessSensing/FreshnessSensing.fsi` — drop
  `contracts/FreshnessSensing.fsi` verbatim: namespace + opens; the `FreshnessSensor` record
  (`SenseRuleHash`/`SenseGeneratorVersion`/`SenseCoveredArtifacts: Gate -> ArtifactHash list option`/
  `SenseCommandVersion: CommandId -> CommandVersion option`, each carrying the `Some [] = sensed-empty` /
  `None = unsensed` doc-comment), the `StoreReader = string -> Result<ReuseStore option, string>` alias, and
  the four vals `realSensor`/`realStoreReader`/`senseFreshness`/`loadStore`. Reuses merged core types verbatim;
  redefines none. No access modifiers anywhere (Principle II).
- [X] T006 Implement `src/FS.GG.Governance.FreshnessSensing/FreshnessSensing.fs` — the impure edge extracted
  from F044's interpreter (research D1), carried **verbatim**: `realSensor repo` computes a real SHA-256
  `RuleHash` over sorted `.fsgg/*.yml` bytes, real `ArtifactHash`es over `src/**` (the F044 MVP coverage), the
  executing assembly version as `GeneratorVersion`, and the coarse command-version digest; `realStoreReader`
  deserializes `fsgg.evidence-reuse-store/v1` via `System.Text.Json` taking the opaque newtype strings verbatim
  (computes NO hash/key/digest — FR-013); `senseFreshness` assembles `SensedFacts` (present Map key = sensed
  even if empty, absent = unsensed — never fabricated), total/guarded (any throw ⇒ `Error`); `loadStore` maps
  `Ok None ⇒ EvidenceReuse.empty`, `Ok (Some s) ⇒ s`, `Error e ⇒ Error e` (no degrade policy here — D2).
  Reaches NO network. Only `System.Security.Cryptography`/`System.IO`/`System.Text.Json` from the net10.0
  shared framework. Makes T008 pass.
- [X] T007 [P] Create `tests/FS.GG.Governance.FreshnessSensing.Tests/Main.fs` (Expecto entry point) and
  `tests/FS.GG.Governance.FreshnessSensing.Tests/Support.fs` (a real temp-directory builder: writes a minimal
  `.fsgg/*.yml` catalog + a couple of `src/**` files, an on-disk well-formed `fsgg.evidence-reuse-store/v1`
  built via the public F029/F030 constructors, and a malformed-store file). Mirror
  `tests/FS.GG.Governance.FreshnessResolution.Tests/Main.fs`/`Support.fs`.
- [X] T008 `tests/FS.GG.Governance.FreshnessSensing.Tests/SensorTests.fs` + `StoreReaderTests.fs` — real-bytes
  evidence (Principle V): `realSensor` over the temp dir yields stable, **ordinal-sorted**, deterministic
  SHA-256 hashes (re-run ⇒ identical; an absent covered surface ⇒ accessor returns `None`, not a fabricated
  empty hash); `senseFreshness` assembles a `SensedFacts` whose present/absent Map keys match the sensed/unsensed
  inputs and is total under a throwing accessor (⇒ `Error`); `loadStore` maps **absent ⇒ `EvidenceReuse.empty`**,
  present-well-formed ⇒ the round-trip-equal `ReuseStore`, present-malformed ⇒ `Error`. (Depends on T006/T007.)
- [X] T009 `tests/FS.GG.Governance.FreshnessSensing.Tests/SurfaceDriftTests.fs` + generate
  `surface/FS.GG.Governance.FreshnessSensing.surface.txt` via `BLESS_SURFACE=1` (Principle II) — the new Tier-1
  baseline for the library's public edge, guarded by the reflective surface assertion with the `BLESS_SURFACE=1`
  re-bless path. Mirror `tests/FS.GG.Governance.CacheEligibilityCommand.Tests/SurfaceDriftTests.fs`.
- [X] T010 [P] Append an F046 section to `scripts/prelude.fsx` — design-first FSI proof (Principle I), **before**
  any command body changes: load the cores + the new `FreshnessSensing.fsi`, build a faked fixed-hash
  `FreshnessSensor` (disclosed `Synthetic`) and an absent `StoreReader`, then run the join verbatim from
  data-model §3 — `FreshnessResolution.resolve selectedGates sensed` → `entries |> List.choose candidate` →
  `CacheEligibility.evaluate candidates store` → assert it yields a `Some`-wrappable `CacheEligibilityReport`
  in which every gate is `mustRecompute`/`noPriorEvidence` over the empty store; and run one **degrade** shape
  (substitute `emptySensedFacts` for a sense `Error`, `EvidenceReuse.empty` for a store `Error`) showing the
  report still builds with no fail. Proves the wire shape over the cores without touching either command.

**Checkpoint**: the shared library builds + its tests are green over real temp-dir bytes; its surface is
blessed; `dotnet fsi scripts/prelude.fsx` runs the F046 join proof; `RouteJson`/`AuditJson` surfaces untouched.

---

## Phase 3: User Story 1 — `fsgg route` carries real cache verdicts in route.json (Priority: P1) 🎯 MVP

**Goal**: `fsgg route` senses each selected gate's freshness facts → assembles F043 `SensedFacts` → `resolve`
→ `evaluate` over the read-only store → passes `Some report` into `RouteJson.ofRouteResult`, so `route.json`'s
cache section is **evaluated** and every selected-gate entry carries a `GateId`-matched verdict. `fsgg route`
still always exits 0; every non-cache field is byte-identical to a pre-wiring run (FR-008, SC-004).

**Independent Test**: run the loop over a fixture repo with selected gates + an absent store ⇒ `route.json`
reports `cacheEligibilityEvaluated: true` and every selected gate reads `mustRecompute`/`noPriorEvidence`;
exit 0; all non-cache fields equal the `None`-projection of the same input.

### Surface contract (Principle I/II — `.fsi` before `.fs`)

- [X] T011 [US1] Edit `src/FS.GG.Governance.RouteCommand/Loop.fsi` per `contracts/route-command-delta.md`:
  `RunRequest.StorePath: string`; `Effect` gains `SenseFreshness of gates: Gate list * baseHead: (Revision
  option * Revision option)` and `LoadStore of path: string`; `Msg` gains `FreshnessSensed of
  Result<SensedFacts, string>` and `StoreLoaded of Result<ReuseStore, string>`; `Phase` gains `Selected`
  (between `Loaded'` and the projection phase); `Model` gains `Snapshot: RepoSnapshot option`/`SelectedGates:
  Gate list`/`Sensed: SensedFacts option`/`Store: ReuseStore option`/`CacheNotes: string list`. `exitCode`
  stays `Success 0 | UsageError' 2 | InputUnavailable 3 | ToolError 4` — **no cache code** (FR-008). Reuses
  merged core types verbatim; redefines none. (`RunRequest.StorePath` is populated by `parse` from a new
  `--store` flag, default `<repo>/readiness/evidence-reuse.json` — D6; implemented in T018, parse-tested in T015.)
- [X] T012 [US1] Edit `src/FS.GG.Governance.RouteCommand/Interpreter.fsi` — `Ports` gains `Freshness:
  FreshnessSensing.FreshnessSensor` and `Store: FreshnessSensing.StoreReader`; `realPorts` signature unchanged.
- [X] T013 [US1] Edit `src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj` — add
  `ProjectReference`s `FreshnessSensing`, `CacheEligibility`, `FreshnessResolution`, `EvidenceReuse`,
  `FreshnessKey`. No third-party `PackageReference`. `RouteCommand` stays the packable `fsgg` tool.

### Tests for User Story 1 (write first — must FAIL before the wiring) ⚠️

- [X] T014 [P] [US1] Edit `tests/FS.GG.Governance.RouteCommand.Tests/Support.fs` (~line 259) — add a faked
  `FreshnessSensor` (fixed literal hashes, disclosed **`Synthetic`** per Principle V; a knob to force one
  accessor `None`) and a faked `StoreReader` (absent ⇒ `Ok None`; a malformed ⇒ `Error` knob), and an
  **expected-report computer** that runs the genuine `FreshnessResolution.resolve`→`candidate`→
  `CacheEligibility.evaluate` over those same faked facts so assertions compare against real core output
  (empty faked store ⇒ every resolved gate `mustRecompute noPriorEvidence`).
- [X] T015 [P] [US1] Edit `tests/FS.GG.Governance.RouteCommand.Tests/LoopTests.fs` (~line 50) — pure
  `init`/`update`: assert `Loaded(Valid)` now emits `[ SenseFreshness(selectedGates, baseHead); LoadStore
  storePath ]` (and **no longer** the write), that the `tryProject` join fires only once both `Sensed` and
  `Store` are `Some`, and that the emitted `routeDoc` equals `RouteJson.ofRouteResult result (Some
  expectedReport)` with `cacheEligibilityEvaluated: true` and each gate verdict `GateId`-matched (SC-001).
  Cover the empty-selection edge ⇒ evaluated section with no per-gate verdicts, exit 0 (spec Edge / US1 sc.3).
  Assert the success-path human/JSON summary lists the per-gate cache outcome (reusable / must-recompute +
  cause / unresolved) consistent with `routeDoc` (FR-015, **C2**). Also extend
  `tests/FS.GG.Governance.RouteCommand.Tests/ScopeParseTests.fs` (**U1** parse coverage): `--store <path>`
  parses into `RunRequest.StorePath`; omitted ⇒ default `<repo>/readiness/evidence-reuse.json`; `--repo .`
  clean-relative form; a missing `--store` value is a `UsageError` **value**, never a throw.
- [X] T016 [P] [US1] Edit `tests/FS.GG.Governance.RouteCommand.Tests/InterpreterTests.fs` — drive
  `Interpreter.run` over faked ports incl. the new `Freshness`/`Store`: the persisted `RouteArtifact` content
  equals a genuine `ofRouteResult result (Some expectedReport)`; an absent store yields `empty` ⇒ all
  `mustRecompute noPriorEvidence`; exit 0 (US1 sc.1/sc.3, L4).
- [X] T017 [P] [US1] Add `tests/FS.GG.Governance.RouteCommand.Tests/CacheInvariantTests.fs` — **L1
  (information-not-verdict)**: project the same `RouteResult` with `Some report` vs the pre-wiring `None` and
  assert every non-cache field of `route.json` (selected gates, route trace, findings, cost rollup, schema
  version `fsgg.route/v2`) and the exit code are byte/value-identical — the cache section is the only delta
  (FR-008, SC-004, US1 sc.2). Additionally assert **FR-013 (no derivation)** (**C1**): no raw freshness input,
  hash, or freshness key appears anywhere in `route.json`, and the opaque evidence reference is rendered only
  via its `referenceValue` (never dereferenced), with no cache-derived severity/enforcement on any field (L6).

### Implementation for User Story 1

- [X] T018 [US1] Implement `src/FS.GG.Governance.RouteCommand/Loop.fs` per the delta: `Sensed (Ok snapshot)`
  also stores `Snapshot = Some snapshot`; `Loaded (Valid facts)` computes `result`/`registry`/`gatesDoc`/
  `selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)`, sets `Phase = Selected`, and emits
  `[ SenseFreshness(selectedGates, baseHeadOf model); LoadStore request.StorePath ]` (**no write here
  anymore**); `FreshnessSensed (Ok)`/`StoreLoaded (Ok)` feed the pure `tryProject` join; the join (both inputs
  `Some`) builds `cacheReport` (data-model §3) and `routeDoc = RouteJson.ofRouteResult result (Some
  cacheReport)`, sets `Phase = Projected`, emits the two `WriteArtifact`s (the existing two-write counter dance
  in `Wrote(_, Ok())` preserved). Extend `parse` to recognize `--store <path>` and default `RunRequest.StorePath`
  to `<repo>/readiness/evidence-reuse.json` when omitted (D6), emitting a `UsageError` **value** on a missing
  flag value — never a throw (**U1**). No access modifiers (Principle II). Makes T014/T015/T017 pass (modulo
  degrade, T031); T016 passes after T020. Same file continues in T019; T020 is `Interpreter.fs` (separate file).
- [X] T019 [US1] In `src/FS.GG.Governance.RouteCommand/Loop.fs` `render` (success): add the cache summary —
  reusable / must-recompute / unresolved gate lines (the F044 pattern via `CacheEligibility.entries` +
  `FreshnessResolution.missingFacts`/`missingFactToken`) plus any `CacheNotes` (FR-015). (Same file as T018 —
  sequential.)
- [X] T020 [US1] Implement `src/FS.GG.Governance.RouteCommand/Interpreter.fs` — `step` handles
  `SenseFreshness(gates, baseHead)` via `FreshnessSensing.senseFreshness ports.Freshness gates baseHead` ⇒
  `FreshnessSensed`, and `LoadStore path` via `FreshnessSensing.loadStore ports.Store path` ⇒ `StoreLoaded`;
  `realPorts repo` adds `Freshness = FreshnessSensing.realSensor repo`, `Store =
  FreshnessSensing.realStoreReader`. Every `step` guarded; `run` never throws. Makes T016 pass.
- [X] T021 [US1] Re-bless `surface/FS.GG.Governance.RouteCommand.surface.txt` via `BLESS_SURFACE=1` and confirm
  the `SurfaceDriftTests` pass; `git diff surface/` shows only the new `Effect`/`Msg`/`Model`/`RunRequest`/
  `Ports` members on this baseline (no `RouteJson` surface change).

**Checkpoint**: `fsgg route` emits `route.json` with an evaluated cache section + real per-gate verdicts;
exit 0; every non-cache field unchanged. **US1 is the shippable MVP.**

---

## Phase 4: User Story 2 — `fsgg ship` carries real cache verdicts without altering the ship verdict (Priority: P1)

**Goal**: `fsgg ship` runs the same pipeline and passes `Some report` into `AuditJson.ofShipDecision`, so each
`kind:"gate"` audit item carries a `GateId`-matched verdict and each `kind:"finding"` item carries none. The
ship pass/fail verdict, the blockers/warnings/passing partition, every enforcement field, the `ExitCodeBasis`,
and the numeric exit code are **identical** to a pre-wiring run (FR-009, SC-002, SC-003). Disjoint files from
US1 ⇒ runs in parallel with Phase 3.

**Independent Test**: run the loop over a fixture ⇒ each gate item carries `mustRecompute`/`noPriorEvidence`,
each finding item none; verdict/partition/enforcement/`ExitCodeBasis`/exit value-identical to the `None`-run.

### Surface contract (Principle I/II — `.fsi` before `.fs`)

- [X] T022 [US2] Edit `src/FS.GG.Governance.ShipCommand/Loop.fsi` — the **identical** additions to
  RouteCommand (T011): `RunRequest.StorePath`; `Effect` `SenseFreshness`/`LoadStore`; `Msg`
  `FreshnessSensed`/`StoreLoaded`; `Model` `Snapshot`/`SelectedGates`/`Sensed`/`Store`/`CacheNotes`; a
  pre-projection `Phase` (`Selected`, between `Loaded'` and `Rolled`). `exitCode` stays `Success 0 | Blocked 1
  | UsageError' 2 | InputUnavailable 3 | ToolError 4` — the `Emitted` → `exitFromBasis decision.ExitCodeBasis`
  mapping is **untouched** (FR-009). (`RunRequest.StorePath` populated by `parse` from `--store`, default
  `<repo>/readiness/evidence-reuse.json` — D6; implemented in T029, parse-tested in T026.)
- [X] T023 [US2] Edit `src/FS.GG.Governance.ShipCommand/Interpreter.fsi` — `Ports` gains `Freshness`/`Store`
  (mirrors T012).
- [X] T024 [US2] Edit `src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj` — add the same
  five `ProjectReference`s (`FreshnessSensing`, `CacheEligibility`, `FreshnessResolution`, `EvidenceReuse`,
  `FreshnessKey`). No third-party `PackageReference`; `IsPackable=false` unchanged.

### Tests for User Story 2 (write first — must FAIL) ⚠️

- [X] T025 [P] [US2] Edit `tests/FS.GG.Governance.ShipCommand.Tests/Support.fs` (~line 265) — faked
  `Freshness`/`Store` ports (disclosed **`Synthetic`**) + the genuine-core expected-report computer (mirrors
  T014).
- [X] T026 [P] [US2] Edit `tests/FS.GG.Governance.ShipCommand.Tests/LoopTests.fs` (~line 50) — pure
  `init`/`update`: `Loaded(Valid)` emits `[ SenseFreshness …; LoadStore … ]` (no write); the join builds
  `auditDoc = AuditJson.ofShipDecision decision (Some expectedReport)` with each `kind:"gate"` item verdict
  `GateId`-matched and each `kind:"finding"` item carrying none (SC-002); the single `WriteArtifact
  AuditArtifact` then `EmitSummary` (single-write dance unchanged). Cover the **finding-only / no-gate** edge ⇒
  findings render as before, no cache verdict on any item, evaluated section with no per-gate verdicts (spec
  Edge, **E1**). Assert the success-path `renderText`/`renderJson` summary lists the per-gate cache outcome
  consistent with `auditDoc` (FR-015, **C2**). Also extend
  `tests/FS.GG.Governance.ShipCommand.Tests/ParseTests.fs` (**U1** parse coverage): `--store <path>` parses into
  `RunRequest.StorePath`; omitted ⇒ default `<repo>/readiness/evidence-reuse.json`; a missing `--store` value is
  a `UsageError` **value**, never a throw.
- [X] T027 [P] [US2] Edit `tests/FS.GG.Governance.ShipCommand.Tests/InterpreterTests.fs` — drive
  `Interpreter.run` over faked ports; persisted `AuditArtifact` equals a genuine `ofShipDecision decision (Some
  expectedReport)`; absent store ⇒ all `mustRecompute noPriorEvidence`.
- [X] T028 [P] [US2] Add `tests/FS.GG.Governance.ShipCommand.Tests/ShipInvariantTests.fs` — the **SC-003
  ship-invariant** (L1): project the same `ShipDecision` with `Some report` vs the `None` pre-wiring projection
  and assert the verdict (pass/fail), the three-way blockers/warnings/passing partition, **every** per-item
  enforcement field, the `ExitCodeBasis`, and the resulting numeric exit are value-identical — a `reusable`
  verdict on a base-blocking gate leaves it a blocker (US2 sc.2/sc.3, FR-009). Fails if any non-cache byte or
  the exit code drifts. Additionally assert **FR-013 (no derivation)** (**C1**): no raw freshness input, hash,
  or freshness key appears anywhere in `audit.json`, the opaque evidence reference is rendered only via its
  `referenceValue` (never dereferenced), and no audit item carries a cache-derived severity/enforcement (L6).

### Implementation for User Story 2

- [X] T029 [US2] Implement `src/FS.GG.Governance.ShipCommand/Loop.fs` per the delta: `Sensed (Ok snapshot)`
  stores `Snapshot`; `Loaded (Valid facts)` computes `result`/`decision = Ship.rollup result mode
  profile`/`selectedGates`, sets `Phase = Selected`, emits `[ SenseFreshness(selectedGates, baseHeadOf model);
  LoadStore request.StorePath ]` (no write here); the join (both inputs `Some`) builds `cacheReport` and
  `auditDoc = AuditJson.ofShipDecision decision (Some cacheReport)`, sets `Phase = Rolled`, emits the **single**
  `WriteArtifact AuditArtifact`; `Wrote (_, Ok())` → `Persisted` + `EmitSummary`; `Emitted` → exit from
  `decision.ExitCodeBasis` (**unchanged**). `renderText` adds the cache summary + `CacheNotes`; `renderJson`
  returns the `auditDoc` verbatim (`--json` stdout == persisted file). Extend `parse` to recognize `--store
  <path>` and default `RunRequest.StorePath` to `<repo>/readiness/evidence-reuse.json` when omitted (D6),
  emitting a `UsageError` **value** on a missing flag value — never a throw (**U1**). Makes T025/T026/T028 pass
  (modulo degrade, T032); T027 passes after T030 (T030 edits `Interpreter.fs`, a separate file).
- [X] T030 [US2] Implement `src/FS.GG.Governance.ShipCommand/Interpreter.fs` — `step` handles `SenseFreshness`
  / `LoadStore` via `FreshnessSensing.senseFreshness` / `loadStore`; `realPorts` wires
  `FreshnessSensing.realSensor` / `realStoreReader`. Guarded; `run` never throws. Then re-bless
  `surface/FS.GG.Governance.ShipCommand.surface.txt` via `BLESS_SURFACE=1` and confirm `git diff surface/`
  shows only this baseline (no `AuditJson` surface change). Makes T027 pass.

**Checkpoint**: `fsgg ship` emits `audit.json` with a per-gate verdict on every gate item and none on findings;
the verdict, partition, enforcement, `ExitCodeBasis`, and exit code are provably unchanged (SC-003).

---

## Phase 5: User Story 3 — Honest degradation when facts can't be sensed or the store can't be read (Priority: P2)

**Goal**: When freshness facts can't be fully sensed or the store is present-but-unreadable, neither command
newly fails or changes its exit code; the document still emits with the cache section honestly degraded
(unresolved gate ⇒ recompute-by-default with named missing facts; unreadable store ⇒ degrade-to-empty), a
non-fatal cache note surfaced in the summary, and **no gate ever silently `reusable`** (FR-010, FR-011,
SC-006; L2/L3). The degrade transitions live in the pure `update` (data-model §5) — implemented in T018/T029,
asserted here.

**Independent Test**: drive each command with a `None`-returning sensor accessor, and separately a malformed
store, ⇒ the document still emits, affected gates are recompute-by-default with named missing facts, no gate
`reusable`, and the exit code is unchanged from the all-resolvable case.

### Tests for User Story 3 (write first — must FAIL until T031/T032 land) ⚠️

- [X] T031 [P] [US3] Add `tests/FS.GG.Governance.RouteCommand.Tests/DegradeTests.fs` — `FreshnessSensed
  (Error)` ⇒ `tryProject` substitutes `emptySensedFacts` (every gate resolves `notEvaluated`), appends a
  `CacheNote`, **no fail**, exit 0; `StoreLoaded (Error)` ⇒ substitutes `EvidenceReuse.empty` (all gates
  `mustRecompute noPriorEvidence`), appends a `CacheNote`, exit 0; an unresolved gate is `notEvaluated` with
  its missing facts named in the summary and **never** `reusable` (L2/L3, FR-010/FR-011, SC-006); assert the
  exit code equals the all-resolvable case. Assert the emitted `CacheNote` names the **missing/malformed input**
  as such (unsensed facts / unreadable store) and is distinct from a fatal tool defect — no swallowed failure
  (Principle VI, **O1**).
- [X] T032 [P] [US3] Add `tests/FS.GG.Governance.ShipCommand.Tests/DegradeTests.fs` — the **same** two degrade
  transitions for `ShipCommand`, additionally asserting the ship verdict / partition / `ExitCodeBasis` / exit
  code are unchanged under both degrade paths (degrade never perturbs the merge decision — FR-009 ∧ FR-011).
  Assert the `CacheNote` names the missing/malformed input distinctly from a fatal tool defect (Principle VI,
  **O1**).

### Implementation for User Story 3

- [X] T033 [US3] Confirm/complete the degrade-not-fail handlers in `src/FS.GG.Governance.RouteCommand/Loop.fs`
  **and** `src/FS.GG.Governance.ShipCommand/Loop.fs` `update` (data-model §5): `FreshnessSensed (Error reason)`
  → `tryProject { m with Sensed = Some emptySensedFacts; CacheNotes = m.CacheNotes @ [note] }`; `StoreLoaded
  (Error reason)` → `tryProject { m with Store = Some EvidenceReuse.empty; CacheNotes = … }`; both
  wildcard-free exhaustive `match`es over `Result`; the existing fatal paths (`Sensed (Error _)` git failure ⇒
  `InputUnavailable`, `Loaded (Invalid)`, `Wrote (_, Error _)` ⇒ `ToolError`) **unchanged**. Makes T031/T032
  pass. (Largely landed in T018/T029; this task closes any gap and the note wording.)

**Checkpoint**: US1, US2 **and** US3 hold — degraded sensing/store never fails the command, never changes the
exit code, never hides a gate or fabricates `reusable`.

---

## Phase 6: User Story 4 — Deterministic, byte-stable artifacts (Priority: P3)

**Goal**: Running either command twice against the same repository state yields byte-identical `route.json` /
`audit.json` including the cache section; each gate's cache verdict appears in the same position as that gate's
existing entry/item (the F045 embed owns ordering) (FR-012, SC-007; L5).

**Independent Test**: project the same fixture twice ⇒ byte-identical documents incl. the cache section; cache
entries follow the document's existing gate order.

### Tests for User Story 4 (write first — must FAIL if any nondeterminism leaks) ⚠️

- [X] T034 [P] [US4] Add `tests/FS.GG.Governance.RouteCommand.Tests/DeterminismTests.fs` — same repo state ⇒
  byte-identical `route.json` over two runs (incl. the cache section); cache verdicts follow the document's
  existing selected-gate order; no wall-clock / cwd / absolute-path text leaks into the cache section. Use
  FsCheck to permute gate discovery order and assert identical `GateId`-ordered output (SC-007, L5).
- [X] T035 [P] [US4] Add `tests/FS.GG.Governance.ShipCommand.Tests/DeterminismTests.fs` — the same byte-stable
  assertion for `audit.json` (incl. the cache section), cache verdicts following each gate item's existing
  position.

**Checkpoint**: all four stories independently functional; both documents are diffable / CI-comparable.

---

## Phase 7: Cross-Cutting — Untouched-Baseline Guard, Quickstart, Manual Exercise

**Purpose**: Prove the SC-008 "untouched" contract, run the quickstart end-to-end, and exercise both commands
against a real repo. Spans all stories; do last.

- [X] T036 [P] Confirm `tests/FS.GG.Governance.RouteJson.Tests`, `tests/FS.GG.Governance.AuditJson.Tests`, and
  `tests/FS.GG.Governance.EnforcementFixtures.Tests` pass **without edits** (still on the `None` path; F028
  golden `audit.json` snapshots unchanged). Do **not** run `BLESS_FIXTURES=1` — no golden fixture changes this
  row (SC-008, L7).
- [X] T037 [P] Verify SC-008 by inspection: `git status` / `git diff` shows **no** edit to any
  F041/F042/F043/F045 core or to F044 (`src/FS.GG.Governance.CacheEligibilityCommand/**`), and `git diff
  surface/` touches **only** the three expected baselines (`FreshnessSensing` new, `RouteCommand`/`ShipCommand`
  re-blessed) — `RouteJson`/`AuditJson` surfaces unchanged.
- [X] T038 Run `quickstart.md` end-to-end: `dotnet fsi scripts/prelude.fsx`; `dotnet build
  FS.GG.Governance.sln`; `dotnet test FS.GG.Governance.sln` (all green incl. US3 degrade + SC-003 invariant);
  `BLESS_SURFACE=1 dotnet test` re-blesses only the three baselines. Fix any drift.
- [X] T039 [P] Manual real-repo exercise (optional check from quickstart §5): `dotnet run --project
  src/FS.GG.Governance.RouteCommand -- route --repo . --json` and `dotnet run --project
  src/FS.GG.Governance.ShipCommand -- ship --repo . --mode gate --profile standard --json`; confirm both emit
  `cacheEligibilityEvaluated: true`, every selected gate reads `mustRecompute`/`noPriorEvidence` (no store on
  disk), `fsgg route` exits 0 and `fsgg ship`'s exit matches its verdict exactly as before.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-008 covered; SC-008 untouched-baseline
guarantee verified.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1 — **blocks all stories** (the shared `FreshnessSensing` edge +
  the design-first join proof). Neither command can reference the sensor until T005/T006 land.
- **US1 (Phase 3)** and **US2 (Phase 4)**: both depend on Phase 2; touch **disjoint files** ⇒ can run in
  parallel. US1 is the MVP.
- **US3 (Phase 5)**: depends on US1 **and** US2 (the degrade handlers live in both `Loop.fs` files, landed in
  T018/T029) — sequence after both; asserts the no-new-failure invariant.
- **US4 (Phase 6)**: depends on US1 **and** US2 (determinism of their outputs) — sequence after both.
- **Cross-cutting (Phase 7)**: depends on every story it guards — do last.

### Within Each User Story

- `Loop.fsi`/`Interpreter.fsi` (the contract) before any `.fs` body (Principle I/II).
- Tests written before/against the wiring and must FAIL before the implementation tasks complete.
- `Loop.fs` before `Interpreter.fs`; surface re-bless after the bodies compile.

### Parallel Opportunities

- **Phase 1**: T002/T003/T004 are `[P]` (T001 first — the `.fsproj` the others reference).
- **Phase 2**: T005→T006 sequential (same module surface→body); T007 then T008/T009 follow; T010 (prelude) is
  `[P]` against the test tasks (different file, needs T006).
- **US1 tests** T014/T015/T016/T017 are `[P]` (different files); impl T018→T019→T020→T021 sequential (T018/T019
  chain through `RouteCommand/Loop.fs`).
- **US2 tests** T025/T026/T027/T028 are `[P]`; impl T029→T030 sequential.
- **US1 (Phase 3) and US2 (Phase 4) run in parallel** — disjoint `RouteCommand`/`ShipCommand` trees.
- **US3** T031/T032 `[P]`; **US4** T034/T035 `[P]`; **Phase 7** T036/T037/T039 `[P]` (T038 gates on green).

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 (Setup) → 2. Phase 2 (Foundational — shared `FreshnessSensing` lib + FSI join proof) →
3. Phase 3 (US1 — `fsgg route`) → **STOP & VALIDATE**: `route.json` carries an evaluated cache section with
real per-gate verdicts, exit 0, every non-cache field unchanged.

### Incremental Delivery

US1 (`route` wire) → US2 (`ship` wire + SC-003 invariant) → US3 (honest degradation) → US4 (determinism) →
Phase 7 (untouched-baseline guard + quickstart). Each adds value without breaking the prior; US1 and US2 can
land in either order (disjoint files).

### Parallel Team Strategy

After Phase 2: Developer A takes US1 (`RouteCommand`), Developer B takes US2 (`ShipCommand`) concurrently; they
converge for US3/US4 (which assert both) and Phase 7.

---

## Task Count Summary

| Group | Tasks | Count |
|---|---|---|
| Phase 1 — Setup | T001–T004 | 4 |
| Phase 2 — Foundational (shared lib + proof) | T005–T010 | 6 |
| Phase 3 — US1 `route` (MVP) | T011–T021 | 11 (4 test, 7 surface/impl) |
| Phase 4 — US2 `ship` | T022–T030 | 9 (4 test, 5 surface/impl) |
| Phase 5 — US3 degrade | T031–T033 | 3 (2 test, 1 impl) |
| Phase 6 — US4 determinism | T034–T035 | 2 (2 test) |
| Phase 7 — Cross-cutting | T036–T039 | 4 |
| **Total** | | **39** |

**Suggested MVP scope**: Phase 1 + Phase 2 + Phase 3 (US1 — `fsgg route`), T001–T021.

## Notes

- `[P]` = different file, no incomplete-dependency in the phase.
- Never mark a failing task `[X]`. The faked `FreshnessSensor`/`StoreReader` in the command unit/interpreter
  tiers use fixed literal hashes — disclosed **`Synthetic`** at the use site and in the PR (Principle V); the
  real sensor/reader path is proven over real temp-dir bytes in the `FreshnessSensing.Tests` (T008).
- **Elmish/MVU (Principle IV, load-bearing)**: sensing/store-load are new `Effect`s, the result `Msg`s feed the
  pure `update`, the cache join **and** the degrade policy are pure (tested as value transitions in
  T015/T026/T031/T032), interpretation stays at the edge against fakeable ports.
- F041/F042/F043/F045 cores, F044 standalone command/sidecar, and the F045/F028 golden baselines stay
  byte-unchanged; no schema bump (`route/v2`, `audit/v2`) — verified in T036/T037 (SC-008, L7).
- **`/speckit-analyze` remediation (folded into existing tasks, no renumbering)**: `--store` flag parse +
  default + parse-test coverage (**U1**) → T011/T015/T018 (route), T022/T026/T029 (ship); FR-013 no-derivation
  assertion (**C1**) → T017 (route), T028 (ship); FR-015 success-path summary-content assertion (**C2**) →
  T015/T026; Principle VI "note names missing/malformed input distinctly from a defect" (**O1**) → T031/T032;
  finding-only / no-gate ship edge (**E1**) → T026. (I2 — the ship surface re-bless folded into T030 vs route's
  standalone T021 — left as a deliberate, cosmetic asymmetry to preserve stable task IDs.)
