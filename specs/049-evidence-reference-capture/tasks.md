---
description: "Task list for Capture A Real Evidence Reference From An Executed Gate (F049)"
---

# Tasks: Capture A Real Evidence Reference From An Executed Gate

**Input**: Design documents from `/specs/049-evidence-reference-capture/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅ (D1–D7), data-model.md ✅,
contracts/EvidenceCapture.fsi ✅, quickstart.md ✅

**Tier**: **Tier 1 (contracted change)** — a new public library + module
(`FS.GG.Governance.EvidenceCapture`) with a new package identity and a new `surface/*.surface.txt` baseline.
No existing public surface changes; no third-party dependency is added; no schema version is bumped. Tests are
**mandatory** (Principle V). All tasks share the feature tier; no per-task `[T1]`/`[T2]` annotations needed.

**Elmish/MVU**: **Not applicable** — two pure, total, deterministic value transformations
(`referenceOf`/`capture`) with no state and no I/O (FR-007, plan Constitution Check Principle IV exempt — the
F030 `EvidenceReuse` / F047 `EvidenceReuseStore` pure-core precedent). No `Model`/`Msg`/`Effect`/`update`/
interpreter tasks, and — because this row introduces **no new type** — **no `Model.fsi`/`Model.fs`** either
(research D4). The impure gate-execution edge that *would* need an MVU boundary (running gates inside `fsgg
route`/`fsgg ship`, sensing each output digest, building the `CommandRecord`, recording during a run) is the
explicitly out-of-scope **following** row (the F048 analogue). Principle VI is likewise N/A — pure total
functions have no failure path to observe; **totality** stands in for safe failure (neither operation throws).

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel. Stories map to
spec user stories — US2 (P1) `referenceOf` (the reproducible reference; **built first** because `capture`
composes it), US1 (P1, headline MVP) `capture` close-the-loop, US3 (P2) capture additive/recompute-safety.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]`/`[US2]`/`[US3]` traceability; unlabeled = shared infrastructure
- Exact repo-root-relative file paths in every description

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new pure library + its focused test project so everything compiles and the solution
restores. No semantics yet. Nothing existing is edited beyond the solution file and `CLAUDE.md`.

- [X] T001 Create `src/FS.GG.Governance.EvidenceCapture/FS.GG.Governance.EvidenceCapture.fsproj` — SDK-style,
  `net10.0`, `RootNamespace`/`PackageId` `FS.GG.Governance.EvidenceCapture`, `Version` `0.1.0`,
  `IsPackable=true` with a `PackageId` (the new-package-identity precedent of
  `EvidenceReuse`/`CommandRecord`/`EvidenceReuseStore`; "pack output unaffected" in the plan means *existing*
  packages are untouched, not that this library is unpackable). `<Compile>` order: **`EvidenceCapture.fsi`,
  then `EvidenceCapture.fs`** (no `Model` file — research D4). **Exactly two** `<ProjectReference>`s — to
  `../FS.GG.Governance.EvidenceReuse/...` (F030 `EvidenceRef`/`ReuseStore`/`record`/`decide`/`empty`) and
  `../FS.GG.Governance.CommandRecord/...` (F032 `CommandRecord`/`canonicalId`/`identityValue`). The F029
  `FreshnessKey` (`FreshnessInputs`) and F014 `Config` types the `.fsi`/`.fs` opens arrive **transitively**
  through F030/F032 (SDK `ProjectReference`s flow by default) and need **no** direct reference — the
  minimal-reference precedent of F030/F041/F042/F047; data-model.md "Dependency direction". **No third-party
  `PackageReference`** (FR-009) — the bodies are BCL + FSharp.Core only (they build no string, hash no bytes,
  parse nothing). Add a header comment mirroring the `EvidenceReuseStore`/`EvidenceReuse` `.fsproj`: the pure
  value-only **capture bridge** that derives the F032 reproducible identity as an `EvidenceRef` and folds it
  into a `ReuseStore` via the F030 `record` convention verbatim; one-way dependency
  `EvidenceCapture -> EvidenceReuse + CommandRecord -> FreshnessKey -> Config`; runs NO gate, senses NO fact,
  resolves NO freshness, persists NOTHING, hashes NO bytes, bumps NO schema version, adds NO CLI; referenced by
  nothing on landing.
- [X] T002 [P] Create `tests/FS.GG.Governance.EvidenceCapture.Tests/FS.GG.Governance.EvidenceCapture.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  new package). `<ProjectReference>`s to the new library **and** `FS.GG.Governance.EvidenceReuse` (build real
  stores via the genuine `EvidenceReuse.record`/`empty` and call `EvidenceReuse.decide`/`entries`),
  `FS.GG.Governance.CommandRecord` (build real `CommandRecord`s via `CommandRecord.build`),
  `FS.GG.Governance.FreshnessKey` (`FreshnessInputs`), `FS.GG.Governance.Config` (`CheckId`/`DomainId`/
  `CommandId`/`EnvironmentClass`), **and — for the persistence round-trip only** —
  `FS.GG.Governance.EvidenceReuseStore` (F047 `serialise`) + `FS.GG.Governance.FreshnessSensing` (F046
  `realStoreReader`). the **final** `<Compile>` order is `Support.fs`, `ReferenceTests.fs`,
  `CaptureTests.fs`, `PersistenceRoundTripTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` — but each entry is added
  by the task that **creates** its file so the project always compiles. At this step wire in **only** `Support.fs`
  (T007) and `Main.fs` (T008); the later test files add their own `<Compile>` entry in the position shown when
  they are created (T010 `ReferenceTests.fs`, T012 `CaptureTests.fs`, T015 `PersistenceRoundTripTests.fs`, T016
  `SurfaceDriftTests.fs`). Do **not** list a file before it exists — the `.fsproj` must reference no missing
  source. Mirror `tests/FS.GG.Governance.EvidenceReuseStore.Tests/...Tests.fsproj`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` (between `<!-- SPECKIT START/END -->`) at
  `specs/049-evidence-reference-capture/plan.md`. No other doc changes.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; `dotnet sln list` shows both new projects.

---

## Phase 2: Foundational (the `.fsi` contract, FSI proof, compiling stub, test scaffolding) — BLOCKS all stories

**Purpose**: Drop the sole public surface (`.fsi`), prove it in FSI (Principle I), and add a compiling `.fs`
body (stubbed operations) plus test scaffolding so the library and tests compile and tests can FAIL before
implementation. **⚠️ No story work begins until this phase is complete** — the `.fsi` declares both functions,
so the `.fs` must satisfy the full signature to compile.

- [X] T005 Author `src/FS.GG.Governance.EvidenceCapture/EvidenceCapture.fsi` — drop
  `contracts/EvidenceCapture.fsi` **verbatim**: `namespace FS.GG.Governance.EvidenceCapture`; the three
  `open`s (`FS.GG.Governance.CommandRecord.Model`, `FS.GG.Governance.FreshnessKey.Model`,
  `FS.GG.Governance.EvidenceReuse.Model`); the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module EvidenceCapture` with
  exactly two members — `val referenceOf: record: CommandRecord -> EvidenceRef` and
  `val capture: inputs: FreshnessInputs -> record: CommandRecord -> store: ReuseStore -> ReuseStore` — each
  carrying its curated purity/totality/duration-invariance/injectivity/close-the-loop/recompute-safety
  doc-comment verbatim. Reuses merged core types verbatim; introduces **no new type**. **No** access modifiers
  anywhere (Principle II — visibility is presence/absence in this `.fsi`).
- [X] T006 Add `src/FS.GG.Governance.EvidenceCapture/EvidenceCapture.fs` — the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module EvidenceCapture` with
  `referenceOf`/`capture` as `failwith "not implemented"` stubs satisfying the `.fsi` signature. No
  `private`/`internal`/`public` modifiers (Principle II). Confirm `dotnet build
  src/FS.GG.Governance.EvidenceCapture/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Write `tests/FS.GG.Governance.EvidenceCapture.Tests/Support.fs` — real, literally-constructible
  builders (Principle V; **no mocks**), the **derived-not-synthetic** discipline being the whole point of this
  row: (1) a `recordOf` builder wrapping `CommandRecord.build (Executable …) [ Argument … ] (WorkingDirectory
  …) env (TimeoutLimit …) (ExitCode …) (OutputDigest …) (OutputDigest …) capturedOutput (SensedDuration …)`
  with sensible defaults and per-field overrides, so a test can perturb exactly **one** reproducible fact
  (executable, an argument, argument **order**, working dir, env-delta as a set, timeout, exit code, stdout
  digest, stderr digest, captured-output outcome) or **only** the `SensedDuration`; cover the edge records —
  empty stdout/stderr `OutputDigest ""`, a non-zero `ExitCode`, and all three captured-output outcomes
  (`NoCapturedOutput`, `CapturedAt (CapturedOutputPath "")`, `CapturedAt (CapturedOutputPath "x")`). (2) an
  `inputs` builder producing a complete literal `FreshnessInputs` (every category present/distinct so a
  mismatch is observable, incl. an `Environment` from each `EnvironmentClass` case and `Command`/
  `CommandVersion` `Some`/`None` variants), plus a `differentInputs` for the recompute-safety tests. (3) a
  `storeOf` helper folding the genuine `EvidenceReuse.record` over `EvidenceReuse.empty` to build a
  **non-empty** prior store (real F029 inputs; disclosed `Synthetic` evidence refs are acceptable for the
  *prior* entries that this row does not derive — the captured ref under test is `referenceOf record`,
  derived). (4) FsCheck generators for arbitrary well-typed `CommandRecord`s (varying every reproducible fact)
  and `FreshnessInputs`. **No** filesystem/clock/process/network access anywhere in `Support.fs` (SC-008) — all
  values are in-memory literals.
- [X] T008 [P] Write `tests/FS.GG.Governance.EvidenceCapture.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.
- [X] T009 [P] Append an F049 design-first section to `scripts/prelude.fsx` (after the F048 section) —
  Principle-I FSI proof **before** any operation body lands: `#r` the new Debug DLL plus the `CommandRecord`,
  `EvidenceReuse`, and `FreshnessKey` DLLs; `open` the model + `EvidenceCapture` modules; build a real
  `CommandRecord` and a `slower` twin differing **only** in `SensedDuration`, and exercise the quickstart §
  "Exercise in FSI" sketch verbatim — `referenceOf record = referenceOf slower` prints `true`
  (duration-invariance); after `capture inputs record EvidenceReuse.empty`,
  `EvidenceReuse.decide inputs grown = Reuse (referenceOf record)` prints `true` (close-the-loop); a
  **different** world prints `Recompute` (recompute-safety); `referenceOf record = referenceOf record` prints
  byte-stable `true`. Documents the shape even while the bodies are stubbed (its assertions fail against the
  stubs — expected).

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles with **only** `Support.fs`
+ `Main.fs` wired (the story test files — `ReferenceTests.fs`, `CaptureTests.fs`, `PersistenceRoundTripTests.fs`,
`SurfaceDriftTests.fs` — and their `<Compile>` entries arrive in Phases 3-7, each added by the task that creates
the file); `dotnet fsi scripts/prelude.fsx` loads the F049 section (its assertions fail against the stubs —
expected). The first story test to FAIL against the stubs lands in Phase 3 (T010).

---

## Phase 3: User Story 2 — The reference is reproducible: the sensed duration never leaks (Priority: P1)

**Goal**: A pure, total, byte-stable `referenceOf : CommandRecord -> EvidenceRef` — the reproducible reference
derivation — that wraps the F032 canonical identity (`EvidenceRef (CommandRecord.identityValue
(CommandRecord.canonicalId record))`), so a real run records a **reproducible** reference (a function of *what
ran and what it produced*, never the sensed wall-clock or a fresh GUID). This is built **first** because
`capture` (US1, Phase 4) composes it — without it the headline MVP cannot land.

**Independent Test**: Build two `CommandRecord`s identical in every reproducible fact but differing in their
`SensedDuration`; assert `referenceOf` returns the byte-identical `EvidenceRef` for both. Perturb any single
reproducible fact and assert the reference changes. No I/O.

### Tests for User Story 2 (write first; must FAIL against the stub) ⚠️

- [X] T010 [P] [US2] `tests/FS.GG.Governance.EvidenceCapture.Tests/ReferenceTests.fs` — drive `referenceOf`
  over real `CommandRecord`s from `Support.fs`: (1) **duration-invariance** (SC-002, FR-002, US2 acceptance 1)
  — two records identical in every reproducible fact and differing **only** in `SensedDuration` ⇒
  `referenceOf a = referenceOf b` byte-for-byte (a worked example **and** an FsCheck property over arbitrary
  records × two durations). (2) **reproducible-fact sensitivity / injectivity** (SC-003, FR-003, US2 acceptance
  2) — each single-field perturbation (executable, an argument, **argument order**, working directory, the
  env-delta compared as a set, timeout, exit code, stdout digest, stderr digest, and each of the three
  captured-output outcomes pairwise) ⇒ a **different** reference. (3) **totality + determinism** (FR-007,
  SC-005) — `referenceOf` is total over the edge records (empty `OutputDigest ""`, non-zero `ExitCode`, all
  captured-output outcomes), never throws, and `referenceOf r = referenceOf r` byte-for-byte for FsCheck
  records (no clock/GUID/path/locale/env leakage), all with **no** filesystem/clock/process/network (SC-008). Add
  `ReferenceTests.fs` to the test `.fsproj` `<Compile>` immediately after `Support.fs` (the project must compile
  before this test can FAIL against the stub).

### Implementation for User Story 2

- [X] T011 [US2] Implement `referenceOf` in `src/FS.GG.Governance.EvidenceCapture/EvidenceCapture.fs` per
  data-model.md §`referenceOf` (D1) — the whole body is `EvidenceRef (CommandRecord.identityValue
  (CommandRecord.canonicalId record))`. It hashes **no** bytes and computes **no** new identity — it reuses
  F032's already-byte-stable canonical rendering (which projects only `record.Reproducible` and structurally
  cannot read `record.Duration`, hence duration-invariant) and inherits F032's injective `canonicalId` (hence
  injective over reproducible facts). Pure / total / no I/O (FR-001/FR-007/FR-008). No access modifiers
  (Principle II). Run T010: green.

**Checkpoint**: US2 is functional — a real execution's reproducible facts derive a byte-stable, duration-
invariant, injective `EvidenceRef`. This is the load-bearing primitive the MVP composes.

---

## Phase 4: User Story 1 — A real execution becomes a reusable store entry (Priority: P1) 🎯 MVP

**Goal**: A pure, total, byte-stable `capture : FreshnessInputs -> CommandRecord -> ReuseStore -> ReuseStore`
— exactly `EvidenceReuse.record inputs (referenceOf record) store` — so an already-executed gate's evidence
folds into the store against its resolved freshness world and a **later** run under the same world **reuses**
that real evidence instead of recomputing. This is the headline point of the row: the only step that lets the
store grow from a real run (F047/F048 persist/prune/retain machinery has, until now, operated on a store that
can never gain a real entry). **Depends on T011** (`referenceOf`).

**Independent Test**: Build a `CommandRecord` and a `FreshnessInputs`, `capture` into the empty store, and
assert `EvidenceReuse.decide inputs result = Reuse r` where `r = referenceOf record` — the captured world is
now reusable and serves exactly the derived reference. No I/O.

### Tests for User Story 1 (write first; must FAIL against the stub) ⚠️

- [X] T012 [P] [US1] `tests/FS.GG.Governance.EvidenceCapture.Tests/CaptureTests.fs` (US1 portion) — drive
  `capture`: (1) **close-the-loop into the empty store** (SC-001, FR-005, US1 acceptance 1) — `EvidenceReuse.decide
  inputs (capture inputs record EvidenceReuse.empty) = Reuse (referenceOf record)`; the result is a one-entry
  store (Edge "Empty store"). (2) **determinism / byte-stability** (SC-005, FR-008, US1 acceptance 2) —
  `capture inputs record empty = capture inputs record empty` and the same over an FsCheck-generated prior
  store; both the derived `EvidenceRef` and the resulting store are byte-identical (no clock/GUID leakage).
  (3) **no spurious match for a different world** (US1 acceptance 3) — after capturing `inputs`,
  `EvidenceReuse.decide differentInputs result` is `Recompute _` (capture adds no match for an unrelated
  world). No filesystem/clock/process/network (SC-008). Add `CaptureTests.fs` to the test `.fsproj` `<Compile>`
  immediately after `ReferenceTests.fs`.

### Implementation for User Story 1

- [X] T013 [US1] Implement `capture` in `src/FS.GG.Governance.EvidenceCapture/EvidenceCapture.fs` per
  data-model.md §`capture` (D2) — the whole body is `EvidenceReuse.record inputs (referenceOf record) store`.
  It reuses the F030 `record` convention **verbatim** (newest-first, store in / store out, immutable,
  full-match dedup) and introduces **no** new reuse policy, store representation, or evidence representation;
  it is **mechanical, not policy** — records whatever record it is handed, including a non-zero `ExitCode` (any
  success/exit-code gating is the out-of-scope host row, D7). Pure / total / no I/O (FR-004/FR-007/FR-008). No
  access modifiers (Principle II). (Same file as T011 — sequence after T011.) Run T012: green.

**Checkpoint**: US1 is functional — a real execution becomes a reusable store entry that serves its own derived
reference. **This is the shippable MVP**: the store can finally grow from a real run (once the deferred host
gate-execution row wires `capture` into `fsgg route`/`fsgg ship`).

---

## Phase 5: User Story 3 — Capture is purely additive: no policy, no clobber, no recompute regression (Priority: P2)

**Goal**: Verify that `capture` (already implemented in T013 as the verbatim F030 `record` fold) is
recompute-safe and additive — into a non-empty store it preserves every prior entry byte-for-byte, makes
**only** the just-captured world reusable, and leaves every other world's `decide` verdict exactly as F030
already decides it; and that re-capturing the same world with a new execution serves the most-recently-captured
reference (F030 newest-first, no new dedup policy). This phase adds **no new implementation** — `capture` is one
line — only the assertions that pin the additive guarantee the whole thread inherits.

**Independent Test**: Capture into a non-empty store and assert every pre-existing entry is unchanged and that
`decide` for any world other than the captured one returns exactly what it returned before the capture.

### Tests for User Story 3 (write first; must FAIL against the stub if run before T013, else assert the contract) ⚠️

- [X] T014 [P] [US3] Extend `tests/FS.GG.Governance.EvidenceCapture.Tests/CaptureTests.fs` (US3 portion —
  same file as T012, sequence after it): (1) **prior entries preserved + recompute-safe** (SC-004, FR-006, US3
  acceptance 1) — `capture` into a non-empty `storeOf` store yields `EvidenceReuse.record`-of the derived
  reference (newest-first); every prior entry is preserved byte-for-byte, and for **every** candidate world
  other than the captured one, `EvidenceReuse.decide candidate result = EvidenceReuse.decide candidate store`
  (an FsCheck property over candidates × stores — capture never fabricates a match for an unrelated world,
  never weakens a prior verdict). (2) **newest-first duplicate capture** (US3 acceptance 2, Edge "Duplicate
  capture") — capturing the **same** world again with a record whose reproducible facts differ (hence a
  different `referenceOf`) makes `decide` for that world serve the **most-recently-captured** reference (F030
  newest-first convention; no new dedup policy here). No filesystem/clock/process/network (SC-008).

**Checkpoint**: US1 + US2 + US3 — the pure capture bridge is complete and provably additive: a real execution
derives a reproducible reference (US2), folds into the store so the captured world is reusable (US1), and does
so without clobbering any prior entry or weakening any other world's verdict (US3).

---

## Phase 6: Cross-cutting — lossless persistence round-trip (FR-010 / SC-007)

**Purpose**: Prove a `capture`-grown store survives the **already-merged** persistence path with the captured
world and the exact derived reference preserved verbatim — closing the loop end-to-end with the real F047
writer and F046 reader, and **no new persistence code** (data-model.md "Lossless persistence round-trip").
Depends on T013 (`capture`).

- [X] T015 [P] `tests/FS.GG.Governance.EvidenceCapture.Tests/PersistenceRoundTripTests.fs` — (SC-007, FR-010):
  grow a store with `capture inputs record EvidenceReuse.empty`, run it through the **real** F047
  `EvidenceReuseStore.serialise`, re-read the text through the **real** F046 `FreshnessSensing.realStoreReader`
  (a `Path.GetTempFileName()` temp file is the **only** test I/O — the capture core itself touches nothing),
  and assert the re-read store **equals** the grown store — the captured entry's full freshness world **and**
  the exact derived `EvidenceRef` preserved byte-for-byte (the reference rendered verbatim, never re-parsed or
  re-hashed by persistence). Include an FsCheck property over arbitrary `(record, inputs)` pairs. Mirror the
  F047 `RoundTripTests.fs` `readBack` helper shape. EvidenceReuseStore + FreshnessSensing appear here as
  **test-only** `ProjectReference`s (T002) — the production library has **no** such dependency (data-model.md
  "Dependency direction"). **Note the reader contract**: `FreshnessSensing.realStoreReader` is a `StoreReader =
  string -> Result<ReuseStore option, string>` whose argument is a **file path**, so write `serialise grown` to a
  `Path.GetTempFileName()` temp file, read it back via `realStoreReader path`, and assert `Ok (Some grown)` (mirror
  F047 `readBack`); the temp file is the only test I/O. Add `PersistenceRoundTripTests.fs` to the test `.fsproj`
  `<Compile>` immediately after `CaptureTests.fs`.

**Checkpoint**: a real captured reference round-trips losslessly through the existing store-persistence path —
the capture core composes cleanly with F047/F046 with zero new persistence code.

---

## Phase 7: Surface governance & polish (Tier-1 baseline, scope hygiene, validation)

**Purpose**: Lock the public surface (Principle II), prove the assembly's reference graph stays minimal and the
change is additive (SC-006), and run the quickstart end-to-end. Bless the baseline only after the surface is
final (the Phase-2 `.fsi` is unchanged through implementation).

- [X] T016 `tests/FS.GG.Governance.EvidenceCapture.Tests/SurfaceDriftTests.fs` — a reflective `SurfaceDrift`
  test (the F020–F047 precedent): enumerate the public surface of `FS.GG.Governance.EvidenceCapture` and
  compare byte-for-byte to `surface/FS.GG.Governance.EvidenceCapture.surface.txt`, with the `BLESS_SURFACE=1`
  re-bless path; plus a **scope-hygiene** assertion (Principle II, plan Engineering Constraints) that the
  production assembly references **only** `FS.GG.Governance.EvidenceReuse` and `FS.GG.Governance.CommandRecord`
  and — transitively — `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config`, plus `FSharp.Core` / BCL —
  and **not** `FreshnessSensing`, `EvidenceReuseStore`, `CacheEligibility`, `RouteJson`, `AuditJson`,
  `Enforcement`, `Ship`, `Snapshot`, `Routing`, `Findings`, any `Adapters.*`, `Host`, `Cli`, and no
  third-party package. Mirror `tests/FS.GG.Governance.EvidenceReuseStore.Tests/SurfaceDriftTests.fs`. (The
  scope-hygiene check inspects the **production** `FS.GG.Governance.EvidenceCapture` assembly, not the test
  assembly — the test project's F047/F046 references are deliberately excluded.) Add `SurfaceDriftTests.fs` to the
  test `.fsproj` `<Compile>` immediately after `PersistenceRoundTripTests.fs` and before `Main.fs`.
- [X] T017 Generate and commit `surface/FS.GG.Governance.EvidenceCapture.surface.txt` via `BLESS_SURFACE=1
  dotnet test tests/FS.GG.Governance.EvidenceCapture.Tests/...`; review the diff (exactly the one public module
  `EvidenceCapture` with `referenceOf` + `capture`, no helper leak — there are no private helpers) and commit
  it as part of the Tier-1 change. After this, T016 runs green without `BLESS_SURFACE`. **No existing baseline
  is re-blessed** — this row adds one new baseline and touches no merged F029–F048 surface or golden (SC-006).
- [X] T018 [P] Verify SC-006 (additive-only) by inspection: `git status` / `git diff` shows **no** edit to any
  merged F029–F048 core (`src/FS.GG.Governance.EvidenceReuse/**`, `src/FS.GG.Governance.CommandRecord/**`,
  `src/FS.GG.Governance.FreshnessKey/**`, `src/FS.GG.Governance.EvidenceReuseStore/**`,
  `src/FS.GG.Governance.FreshnessSensing/**`, the F041–F048 cores and host commands) or to any existing test
  project, and `git diff surface/` touches **only** the one new `EvidenceCapture` baseline; **no** schema-version
  bump (no token is changed — this row consumes the F032 identity and F030 store, introducing none), **no**
  golden-fixture re-bless (do **not** run `BLESS_FIXTURES=1`), **no** new third-party dependency. Only NEW files
  under `src/`, `tests/`, `surface/`, `specs/049-*`, plus `scripts/prelude.fsx` + the `CLAUDE.md` pointer.
- [X] T019 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`; `dotnet fsi
  scripts/prelude.fsx` (the F049 section prints duration-invariance `true`, close-the-loop `true`,
  different-world `Recompute`, byte-stable `true`); `dotnet test
  tests/FS.GG.Governance.EvidenceCapture.Tests/...` (all green under `TreatWarningsAsErrors`, incl. reference
  derivation, capture close-the-loop, recompute-safety, the persistence round-trip against the **real** F047
  writer + F046 reader, and surface drift). Confirm `dotnet build && dotnet test` over the existing projects is
  unchanged (the new library + test project are purely additive). Fix any drift.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-008 covered; the Tier-1 surface is
blessed and guarded with a minimal reference graph; existing F029–F048 cores, baselines, and goldens
byte-unchanged. **The capture gap is closed as a pure core** — an actually-executed gate's `CommandRecord` now
derives a real, reproducible `EvidenceRef` and folds into the store so the captured world is reusable, mirroring
how F047 delivered the pure write half before F048 wired it. The impure gate-execution edge (running gates,
sensing digests, building the `CommandRecord`, recording during a run) remains the explicit **following** row.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` declares both functions,
  so the compiling stub `.fs`, FSI proof, and test scaffolding (`Support.fs`, `Main.fs`) must exist before any
  story test can be written and FAIL.
- **Phase 3 (US2 `referenceOf`)**: depends on Phase 2. Built **first** because `capture` composes `referenceOf`.
- **Phase 4 (US1 `capture`, MVP)**: depends on Phase 2 **and T011** (`referenceOf` body) — `capture` is
  `EvidenceReuse.record inputs (referenceOf record) store`; its `.fs` edit sequences **after T011** (same file
  `EvidenceCapture.fs`).
- **Phase 5 (US3 additive)**: depends on **T013** (`capture` body). No new implementation — test-only; its test
  file is the same `CaptureTests.fs` as T012 (sequence after T012).
- **Phase 6 (persistence round-trip)**: depends on **T013** (`capture` body) — drives the real F047/F046 path.
- **Phase 7 (surface/polish)**: last — bless the baseline only after the surface is final (Phase-2 `.fsi`
  unchanged through implementation).

### Within each story

- Each story's test file is written FIRST and must FAIL against the Phase-2 stub, then pass after its
  implementation task lands (T010→T011; T012→T013). US3's T014 asserts the contract `capture` already
  satisfies once T013 lands.
- The `.fsi` surface precedes the `.fs` body that satisfies it; `Support.fs` (T007) precedes every story test
  file that consumes its builders/generators.
- The two `.fs` bodies are sequential — `referenceOf` (T011) precedes `capture` (T013) because `capture`
  composes it (and both edit `EvidenceCapture.fs`).

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) and T004 `[P]` (CLAUDE.md) are independent of T001 (library
  `.fsproj`); T003 (sln) needs T001 + T002.
- **Phase 2**: T005 (`.fsi`) precedes T006 (stub `.fs`); T007/T008/T009 are `[P]` against each other (distinct
  files — `Support.fs`, `Main.fs`, `scripts/prelude.fsx`) and need the compiling stub (DLL name fixed by T001).
- **Story test files**: T010 (`ReferenceTests.fs`), T012/T014 (`CaptureTests.fs`), T015
  (`PersistenceRoundTripTests.fs`) are `[P]` across distinct files; T012→T014 are sequential (same file). All
  share `Support.fs` (T007) as a prerequisite.
- **Implementation tasks T011→T013 are sequential** — they edit the same `EvidenceCapture.fs`, and `capture`
  composes `referenceOf`.
- **Phase 7**: T018 `[P]` (git inspection) is independent; T016→T017→T019 are sequential (bless after the
  surface test, validate after the bless).

---

## Implementation Strategy

### MVP scope

The MVP is **User Story 1 (`capture` close-the-loop)** — the headline value: a real execution becomes a
reusable store entry. Because `capture` composes `referenceOf`, the minimal shippable path is **Phase 1 → Phase
2 → Phase 3 (US2 `referenceOf`) → Phase 4 (US1 `capture`)**. US2 is not optional polish — it is the reproducible
primitive the MVP is built on (a reference that varied with the sensed duration would defeat reuse entirely).
**STOP and VALIDATE** after Phase 4: `decide inputs (capture inputs record empty) = Reuse (referenceOf record)`.

### Incremental delivery

1. Setup + Foundational → library + tests compile, stubs FAIL.
2. + US2 `referenceOf` → reproducible, duration-invariant, injective reference (the primitive).
3. + US1 `capture` → **MVP**: the captured world is reusable and serves the derived reference.
4. + US3 → proven additive / recompute-safe (no new code, only assertions).
5. + persistence round-trip → proven lossless through the real F047/F046 path.
6. + surface governance → Tier-1 baseline blessed, scope-hygiene + additive guarantee pinned.

---

## Task count & summary

- **Total**: 19 tasks (T001–T019).
- **By user story**: US2 (`referenceOf`) — 2 (T010 test, T011 impl); US1 (`capture`, MVP) — 2 (T012 test, T013
  impl); US3 (additive) — 1 (T014, test-only). Shared infrastructure / cross-cutting — 14 (Setup 4,
  Foundational 5, persistence round-trip 1, surface/polish 4).
- **Parallel opportunities**: T002+T004 (setup); T007+T008+T009 (Phase-2 scaffolding); T010+T012+T015 (story
  test files across distinct files); T018 (git inspection in Phase 7).
- **Suggested MVP**: User Story 1 (`capture`), reachable only after User Story 2 (`referenceOf`) — Phases 1→4.

## Notes

- [P] tasks = different files, no dependencies on another incomplete task in the phase.
- [Story] label maps task to a spec user story for traceability; unlabeled = shared infrastructure.
- The two production bodies are **one line each** (data-model.md) — the engineering weight of this row is the
  test evidence (reproducibility, injectivity, close-the-loop, recompute-safety, lossless persistence) and the
  Tier-1 surface/additive governance, **not** the implementation.
- Evidence references on the capture path are **derived** from real `CommandRecord`s, never `Synthetic`
  literals — this row *removes* a synthetic-evidence use rather than adding one (plan Constitution Check
  Principle V). The only `Synthetic` refs that may appear are *prior* store entries the recompute-safety tests
  fold in, which this row does not derive.
- Verify tests FAIL before implementing; commit after each task or logical group; never weaken an assertion to
  green a build — narrow scope and document.
</content>
</invoke>
