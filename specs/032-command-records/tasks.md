---

description: "Task list for 032-command-records implementation"
---

# Tasks: Command-Record Core

**Input**: Design documents from `/specs/032-command-records/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/command-record-api.md,
contracts/command-record-identity-format.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a complete-carriage / sensed-split / canonical-identity / determinism / purity contract — the
tests *are* the deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no
new third-party dependency — plan Constitution Check). No per-task tier annotations needed — all tasks share
the feature tier.

**Elmish/MVU**: **Not applicable** — three pure, total functions (`build`, `canonicalId`, `identityValue`)
over supplied values; no state, no I/O, no workflow (plan Constitution Check, Principle IV = N/A). No
`Model`/`Msg`/`Effect`/`update`/interpreter tasks. The ten run facts are values handed in, not sensed here.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No
semantics yet.

- [X] T001 Create `src/FS.GG.Governance.CommandRecord/FS.GG.Governance.CommandRecord.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.CommandRecord`, `Version` `0.1.0`, `IsPackable=true` (override
  the `Directory.Build.props` `IsPackable=false` default, like FreshnessKey/EvidenceReuse/RouteExplain/Config).
  `<Compile>` order: `Model.fsi`, `Model.fs`, `CommandRecord.fsi`, `CommandRecord.fs`. **One**
  `<ProjectReference>` — `../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj` — and **no other src
  reference** (never reference Gates/Route/Routing/Findings/Snapshot/FreshnessKey/EvidenceReuse/RouteExplain/
  Host/Cli — plan D1). **No third-party `PackageReference`** (FR-013, plan D1; the transitive `YamlDotNet` via
  Config is unused). Add a header comment mirroring the RouteExplain `.fsproj` (pure total command-record core
  over already-sensed facts; reuses F014 `TimeoutLimit` verbatim; reproducible/sensed split is structural;
  canonical identity over the nine reproducible facts in the F029 tagged/length-prefixed discipline; no
  execution/timing/hashing/persistence/git/filesystem coupling — D1–D6).
- [X] T002 [P] Create `tests/FS.GG.Governance.CommandRecord.Tests/FS.GG.Governance.CommandRecord.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  version literals in the `.fsproj`); `<ProjectReference>`s to the new core **and** to
  `../../src/FS.GG.Governance.Config/...` (the test code constructs real `TimeoutLimit` values). `<Compile>`
  order: `Support.fs`, `RecordTests.fs`, `IdentityTests.fs`, `DeterminismTests.fs`, `PurityTests.fs`,
  `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing F031 entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies
+ test scaffolding so the library and tests compile and tests can FAIL before implementation. **⚠️ No story
work begins until this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.CommandRecord/Model.fsi` — the SOLE public surface for the types
  (contracts/command-record-api.md, data-model.md): `open FS.GG.Governance.Config.Model` (for the verbatim
  F014 `TimeoutLimit`, no redefinition — FR-009); the ten newtypes (`Executable`, `Argument`,
  `WorkingDirectory`, `ExitCode`, `OutputDigest`, `EnvVarName`, `EnvVarValue`, `CapturedOutputPath`,
  `SensedDuration of nanoseconds: int64`, `CommandIdentity`); the three env-delta records (`AddedVar`,
  `ChangedVar`, `RemovedVar`) and `EnvironmentDelta`; the closed `CapturedOutput` DU (`CapturedAt of
  CapturedOutputPath | NoCapturedOutput`); the `ReproducibleFacts` record (the nine reproducible facts) and
  the `CommandRecord` record (`{ Reproducible: ReproducibleFacts; Duration: SensedDuration }`). Curated doc
  comments in the F029/F031 `.fsi` style: the duration is the only sensed fact, held structurally apart and
  excluded from identity (D2, FR-004); a `Changed` var carries `Old`+`New` and is never split into add+remove
  (D4, FR-002); `NoCapturedOutput` is the explicit, total absence, distinct from an empty path (D5, FR-011);
  `CommandIdentity` wraps the byte-stable canonical rendering. No access modifiers will appear in the matching
  `.fs`.
- [X] T005 Write `src/FS.GG.Governance.CommandRecord/CommandRecord.fsi` — the SOLE public surface for the
  operations (contracts/command-record-api.md): `val build:` (the ten supplied facts curried in the design
  row's field order ⇒ `CommandRecord`); `val canonicalId: record: CommandRecord -> CommandIdentity`;
  `val identityValue: identity: CommandIdentity -> string`. Doc comments stating purity/totality and the laws
  (`build` total and verbatim-carrying all ten facts, arguments in order, env delta's three classes preserved;
  `canonicalId` computed only over `record.Reproducible`, duration never read, duration-only difference ⇒ equal
  id, any reproducible difference ⇒ different id, env-delta order/dup invariant but argument order significant,
  captured-output presence/absence/empty all distinct; `identityValue` unwraps; reads no
  clock/filesystem/git/environment/network, spawns no process, hashes no bytes). Include the F029 naming note
  (operations module `CommandRecord` vs the `Model.CommandRecord` type — distinct CLR entities, research
  cross-cutting facts).
- [X] T006 Add stub `src/FS.GG.Governance.CommandRecord/Model.fs` and
  `src/FS.GG.Governance.CommandRecord/CommandRecord.fs` — real type definitions in `Model.fs` (newtypes,
  records, and the `CapturedOutput` DU are data, define them fully, with `open FS.GG.Governance.Config.Model`);
  `build`/`canonicalId`/`identityValue` as `failwith "not implemented"` in `CommandRecord.fs` so the assembly
  compiles. No `private`/`internal`/`public` modifiers (Principle II). Confirm
  `dotnet build src/FS.GG.Governance.CommandRecord/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F032 design-first section to `scripts/prelude.fsx` after the F031 section — `#r` the new
  Debug DLL plus the Config DLL; build the worked example from contracts/command-record-identity-format.md
  (executable `gcc`; arguments `["-c"; "main.c"]`; cwd `/work`; env delta with one added `CI=1`; timeout `30`;
  exit `0`; stdout `sha-out`; stderr `sha-err`; `NoCapturedOutput`; some `SensedDuration`); `printfn` the
  intended calls with expected results: `build …` carries all ten facts read back verbatim; two records
  differing only in `SensedDuration` ⇒ **equal** `canonicalId`; flip one reproducible fact (an argument, or the
  stdout digest) ⇒ **different** `canonicalId`; reorder/duplicate the env-delta entries ⇒ **unchanged**
  `canonicalId`; `NoCapturedOutput` vs `CapturedAt (CapturedOutputPath "")` ⇒ **different** `canonicalId`; the
  worked-example identity string equals the contract's block. Expected outputs as inline comments. This is the
  Principle-I FSI proof; it documents the shape even while the body is stubbed.
- [X] T008 Write `tests/FS.GG.Governance.CommandRecord.Tests/Support.fs` — real, literally-constructible fact
  builders (Principle V, no mocks): helpers to construct an `Executable`/`Argument list`/`WorkingDirectory`/
  `EnvironmentDelta`/`TimeoutLimit`/`ExitCode`/`OutputDigest`/`CapturedOutput`/`SensedDuration` from literals,
  plus a convenience that assembles a full `CommandRecord` via `CommandRecord.build` with sensible defaults and
  per-field overrides (so each test perturbs exactly one fact). Add literal `AddedVar`/`ChangedVar`/`RemovedVar`
  builders; an env-delta builder that can be given entries in arbitrary order / with duplicates (for the
  order/dup-invariance tests); FsCheck generators for `ReproducibleFacts`, `SensedDuration`, and whole
  `CommandRecord`s (varying every reproducible fact, argument order, and env-delta order/duplication); and the
  `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from the F031 `Support.fs`.
  No I/O beyond repo-root resolution.
- [X] T009 [P] Write `tests/FS.GG.Governance.CommandRecord.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because the operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Capture a command run as one complete, typed record (Priority: P1) 🎯 MVP

**Goal**: `build` assembles the ten supplied facts into one complete `CommandRecord` from which each fact reads
back verbatim — executable, ordered arguments, working directory, the env delta partitioned into added /
changed / removed, timeout, exit code, stdout digest, stderr digest, captured-output outcome, and duration —
total over failed / timed-out / argument-less / empty-delta runs.

**Independent Test**: Supply ten literal facts and `build` the record; assert each fact reads back verbatim
(arguments in order; env delta in three distinct classes, a change counted once); a non-zero exit code, a
timed-out run, an empty argument list, and an empty delta all yield ordinary complete records. (US1 acceptance
#1–#4.)

### Tests for User Story 1 (write first; must FAIL against the stub)

- [X] T010 [P] [US1] `tests/.../RecordTests.fs` — (a) verbatim carriage: for a successful run's ten literal
  facts, assert `r.Reproducible.Executable`/`Arguments` (same elements, **same order**)/`WorkingDirectory`/
  `Environment`/`Timeout`/`ExitCode`/`StdoutDigest`/`StderrDigest`/`CapturedOutput` and `r.Duration` each equal
  the supplied input (SC-001, US1 #1); (b) env-delta three-class partition: a run that adds, changes, and
  removes variables ⇒ the `Added`/`Changed`/`Removed` classes carry exactly those entries, a changed variable
  appears **once** in `Changed` (with `Old`+`New`) and is **not** present in `Added`/`Removed` (SC-002, US1 #2,
  Edge: changed var); (c) totality edge cases — non-zero `ExitCode`, a run whose `TimeoutLimit` applied, an
  empty `Argument list`, an entirely empty `EnvironmentDelta`, and `NoCapturedOutput` — each produces an
  ordinary complete record, `build` never throws (SC-001, US1 #3/#4, Edge cases); (d) FsCheck totality: over
  generated ten-fact tuples, `build` always returns and round-trips every fact.

### Implementation for User Story 1

- [X] T011 [US1] Implement `build` in `CommandRecord.fs` per contracts/command-record-api.md — assemble the ten
  curried facts into `{ Reproducible = { Executable = executable; Arguments = arguments; WorkingDirectory =
  workingDirectory; Environment = environment; Timeout = timeout; ExitCode = exitCode; StdoutDigest =
  stdoutDigest; StderrDigest = stderrDigest; CapturedOutput = capturedOutput }; Duration = duration }`. Pure
  record construction only — no clock/filesystem/git, no normalization or reordering of arguments or env-delta
  entries (carriage is verbatim; canonicalization is `canonicalId`'s job, US2). Run T010: all carriage /
  partition / totality assertions go green.

**Checkpoint**: US1 is functional — a complete, typed ten-fact record is built from supplied facts, total over
every edge case, with the sensed duration structurally apart. MVP reached (minus identity).

---

## Phase 4: User Story 2 — Mark the sensed metadata and project a stable canonical identity (Priority: P1)

**Goal**: The duration is reachable as sensed metadata (`record.Duration`) and structurally excluded from the
identity; `canonicalId` renders only `record.Reproducible` to a byte-stable `CommandIdentity` using the F029
tagged/length-prefixed/injective encoding — duration-only differences share an identity, any reproducible
difference changes it, env-delta order/dups are invariant while argument order is significant, and
captured-output presence/absence/empty are three distinct identities; `identityValue` unwraps it.

**Independent Test**: Two records sharing every reproducible fact but differing only in duration ⇒ **equal**
`canonicalId`; the duration is reachable and distinct from `record.Reproducible`; changing any reproducible
fact ⇒ **different** `canonicalId`; `canonicalId` computed twice ⇒ byte-identical; `identityValue` returns the
canonical string. (US2 acceptance #1–#4.)

### Tests for User Story 2 (write first)

- [X] T012 [P] [US2] `tests/.../IdentityTests.fs` — (a) sensed split: `record.Duration` is reachable and
  distinct from `record.Reproducible`; two records equal in every reproducible fact but differing only in
  `SensedDuration` have **equal** `canonicalId` (SC-003/SC-004, US2 #1/#3); (b) per-field sensitivity: flipping
  **any one** reproducible fact — executable, an argument value, **argument order**, working directory, an
  env-delta entry (in any class), timeout, exit code, stdout digest, stderr digest, or captured-output outcome —
  changes `canonicalId`, tested field-by-field (SC-004, US2 #2); (c) captured-output disambiguation:
  `NoCapturedOutput`, `CapturedAt (CapturedOutputPath "")`, and `CapturedAt (CapturedOutputPath "x")` yield
  three pairwise-**different** identities (FR-011, D5); (d) idempotence: `canonicalId record` computed twice is
  byte-for-byte equal, and `identityValue (canonicalId r)` is the stable canonical string (SC-005, US2 #4);
  (e) worked example: a record built per contracts/command-record-identity-format.md renders to that contract's
  exact block (the duration not appearing in it). FsCheck the duration-invariance and per-field-sensitivity
  laws; example-test the worked block and the captured-output cases.

### Implementation for User Story 2

- [X] T013 [US2] Implement `canonicalId` and `identityValue` in `CommandRecord.fs` per
  contracts/command-record-identity-format.md — render each field of `record.Reproducible` as a tagged,
  length-prefixed segment (`<tag>=<presence><byteLen>:<value>`) in the fixed field order (`exe`, `args`, `cwd`,
  `env+`, `env~`, `env-`, `to`, `exit`, `out`, `err`, `cap`), joined by `'\n'` with no trailing newline, wrapped
  as `CommandIdentity`. **Arguments** are rendered in given order as a counted list (`args=<count>;…`, no sort,
  no dedup). **Each env-delta class** renders its entries to canonical per-entry strings, then **dedups and
  ordinal-sorts** them (the F029 set discipline) ⇒ order/dup invariant. `ExitCode`/`TimeoutLimit` render via
  decimal text encoded as required strings; `CapturedOutput` renders `0` for `NoCapturedOutput` and presence
  `1` + path for `CapturedAt`. `record.Duration` is **never read** (D2). `identityValue (CommandIdentity s) =
  s`. Pure string/list operations, no hashing (FR-010). Use UTF-8 byte lengths for the prefixes. Run T012:
  green. Re-run T010: still green.

**Checkpoint**: US1 + US2 — the complete record carries all ten facts, the sensed duration is structurally
apart, and `canonicalId` is the byte-stable reproducible-only identity (order-independent over the env delta,
order-significant over arguments). The full contract surface is delivered.

---

## Phase 5: User Story 3 — The record and its identity are deterministic and pure over supplied data (Priority: P2)

**Goal**: `build` and `canonicalId` are pure, deterministic functions of the supplied facts: identical facts ⇒
identical record and identical identity; reordering/duplicating the env-delta entries never changes the
identity (while argument order does); no clock/filesystem/git/environment/network is read and no process is
spawned.

**Independent Test**: `build` + `canonicalId` twice from the same facts ⇒ structurally/byte equal. Reorder and
duplicate the env-delta entries, rebuild, recompute ⇒ unchanged identity; reorder arguments ⇒ changed identity.
Recompute after changing cwd / creating a temp file ⇒ unchanged. (US3 acceptance #1–#3.)

### Tests for User Story 3 (write first)

- [X] T014 [P] [US3] `tests/.../DeterminismTests.fs` — (a) `build` then `canonicalId` called twice yields a
  structurally identical `CommandRecord` and byte-identical `CommandIdentity` for representative and
  FsCheck-generated fact tuples (SC-005, US3 #1); (b) env-delta order/dup invariance: supplying any class's
  entries in a different order, and with duplicate entries, leaves `canonicalId` **unchanged** (SC-005, US3 #2,
  Edge: order/dup); (c) argument-order significance (the contrast): reordering `Arguments` **does** change
  `canonicalId` (D6, FR-006). Build the permutations with the Support env-delta/order helpers and FsCheck
  generators.
- [X] T015 [P] [US3] `tests/.../PurityTests.fs` — a fixed `build`/`canonicalId` result is identical when
  recomputed after changing `Environment.CurrentDirectory` and after creating/deleting an unrelated temp file
  (and across repeated calls), demonstrating no clock/cwd/filesystem influence and no process spawn (SC-006,
  US3 #3).

**Note**: US3 has no new implementation task — determinism/purity are properties of the `build`/`canonicalId`
built in US1+US2 (record construction and the `List.map`/`List.distinct`/`List.sortWith` + segment-building
pipeline are pure and, for the env delta, order-independent by construction). If T014–T015 reveal a gap, fix
the operation (never weaken a test).

**Checkpoint**: All three stories functional and independently testable — the record and its identity are
complete, correct, deterministic, and pure.

---

## Phase 6: Cross-cutting Tier-1 surface obligations

**Purpose**: The surface baseline + scope guard and the no-regression promise.

- [X] T016 `tests/.../SurfaceDriftTests.fs` — the reflective surface test (F029/F030/F031 precedent): render
  the assembly's public surface, compare to `surface/FS.GG.Governance.CommandRecord.surface.txt` with the
  `BLESS_SURFACE=1` re-bless path; assert exactly the two public modules (`Model`, `CommandRecord`) export and
  no helper leaks; **scope-hygiene**: referenced assemblies are only `FSharp.Core`, `FS.GG.Governance.Config`,
  and BCL (`System.*`/`netstandard`/`mscorlib`) — NOT `Gates`/`Route`/`Routing`/`Findings`/`Snapshot`/
  `FreshnessKey`/`EvidenceReuse`/`RouteExplain`/`RouteJson`/`GatesJson`/`AuditJson`/`Enforcement`/`Ship`/
  `Adapters.*`/`Host`/`Cli` (plan D1, contracts negative scope guard).
- [X] T017 Generate the committed baseline `surface/FS.GG.Governance.CommandRecord.surface.txt` by running the
  suite once with `BLESS_SURFACE=1`, then review the file by eye against `Model.fsi`/`CommandRecord.fsi` to
  confirm it contains exactly the intended surface (the two modules, three vals, and the declared types). Commit
  it. (After T016.)
- [-] T018 [P] README cores pointer — **SKIPPED**: the README's enumerated core list is still frozen at
  F18 (last entry `FS.GG.Governance.Gates … (F18, done)`; F19–F31 were never added), exactly as it was for
  F029/F030/F031. Per the precedent, NOT partially extending the list for F032 alone; no README change.

**Checkpoint**: Tier-1 obligations met; the public surface is pinned.

---

## Phase 7: Validation & polish

- [X] T019 Run `dotnet test tests/FS.GG.Governance.CommandRecord.Tests/...` — all suites green; capture the run
  as evidence (Principle V).
- [X] T020 Run `dotnet test FS.GG.Governance.sln` and confirm the no-regression promise (SC-007): existing
  projects' tests and **every** existing `surface/*.surface.txt` baseline (including F014 Config, F029, F031)
  are unchanged; only the new project's tests and the new surface baseline are added. Confirm `src/**` and
  existing `surface/**` show no diff.
- [X] T021 [P] Run `dotnet fsi scripts/prelude.fsx` end-to-end and confirm the F032 section's printed results
  now match the real `build`/`canonicalId`/`identityValue` output, including the worked-example identity block
  (Principle I evidence, closes T007).
- [X] T022 [P] Walk `quickstart.md` top-to-bottom (build → FSI → test → re-bless → no-regression) and fix any
  drift between the guide and reality. Confirm the `CLAUDE.md` plan pointer targets
  `specs/032-command-records/plan.md` (update it if it still points at F031).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → after Phase 1; **BLOCKS** all stories (the `.fsi` + stubs + Support must exist
  to compile any test).
- **Phase 3 (US1)** → after Phase 2. Delivers `build` (the complete ten-fact record) — the MVP.
- **Phase 4 (US2)** → after **US1** specifically (`canonicalId`/`identityValue` operate on the `CommandRecord`
  that `build` produces; the tests construct records via `build`). Independently testable once identity lands
  (T013).
- **Phase 5 (US3)** → after US1+US2 (determinism/purity are properties of the completed `build`/`canonicalId`).
  No new implementation; tests only.
- **Phase 6 (cross-cutting)** → after the operations exist; T017 after T016.
- **Phase 7 (validation)** → last.

### Within each story

- Tests are written first and must FAIL against the Phase-2 stub before the implementation task greens them.
- `Model.fs` types before the operations that use them (already in place from Phase 2).

### Parallel opportunities

- Phase 1: T002 ‖ (T001→T003).
- Phase 2: T007 ‖ T009 (after T004–T006 land the `.fsi`+stubs; T008 Support precedes the story tests).
- Within a story, the `[P]` test files are independent of each other; the implementation task follows them.
- Phase 5: T014 ‖ T015 (all tests, no new impl).
- Phase 6: T018 ‖ (T016→T017).
- Phase 7: T021 ‖ T022 (after T019–T020).

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (`build` — the complete ten-fact record, total over
every edge case) → 4. **STOP & VALIDATE**: every declared fact reads back verbatim, the env delta is a
three-class partition, failed/timed-out/argless/empty-delta runs are ordinary records.

### Incremental delivery

US1 (complete typed record = MVP) → US2 (sensed split + canonical identity: `canonicalId`/`identityValue`) →
US3 (determinism/purity proof) → cross-cutting surface obligations → full-suite validation. Each phase is
independently testable and adds value without breaking the previous.

---

## Notes

- Tier 1 throughout: any public-surface change requires re-blessing
  `surface/FS.GG.Governance.CommandRecord.surface.txt` (`BLESS_SURFACE=1`).
- `[P]` = different files, no in-phase dependency. `[USx]` maps a task to its spec user story.
- Principle IV (Elmish/MVU) is **N/A** — three pure total functions, no state/I/O (recorded once here, not per
  task). The ten run facts are values handed in, never sensed here.
- No mocks anywhere (Principle V); all inputs are real, literally-constructible typed values (the facts a host
  would sense are supplied as literals — the core's contract). No process is spawned. No `Synthetic` disclosure
  needed.
- F014 `TimeoutLimit` (`FS.GG.Governance.Config.Model`) is consumed verbatim for the *timeout* fact; nothing in
  Config is modified (FR-009, SC-007). The stdout/stderr digests are supplied opaque `OutputDigest` tokens —
  no byte hashing here (FR-010, D3); the duration is `SensedDuration` nanoseconds, excluded from identity
  (D2/D3).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
