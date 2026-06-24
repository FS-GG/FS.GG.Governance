---
description: "Task list for Run A Gate's Process Behind An Injected Execution Port And Assemble Its Command Record (F051)"
---

# Tasks: Run A Gate's Process Behind An Injected Execution Port And Assemble Its Command Record

**Input**: Design documents from `/specs/051-gate-execution-port/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/Model.fsi ✅, contracts/Interpreter.fsi ✅, quickstart.md ✅

**Tier**: **Tier 1 (contracted change)** — a new public **impure edge** library
(`FS.GG.Governance.GateExecution`) with a new package identity, a new domain vocabulary, an injected port type,
the real port, the `senseExecution` composition, and a new `surface/*.surface.txt` baseline. No existing public
surface changes; **no** third-party dependency is added (BCL `System.Diagnostics.Process`/`Stopwatch` +
FSharp.Core only); **no** schema version is bumped; **no** F050/F032/F049 type is redefined (their vocabulary is
reused verbatim). Tests are **mandatory** (Principle V). All tasks share the feature tier; no per-task
`[T1]`/`[T2]` annotations needed.

**Elmish/MVU**: **Applies and is live** — this is the codebase's **first I/O edge** in this thread (spawning a
process). Principle IV is satisfied by the **injected-port / interpreter boundary**, the merged `Snapshot`
`senseSnapshot`/`realPorts` and F046 `FreshnessSensing` `realSensor` precedent (plan Constitution Check IV): I/O
is represented as **data** (the injected `ExecutionPort` function value, in `Model`), the logic is **pure given
the port** (`senseExecution` = `port` ∘ pure F050 `recordOf`, starting no process itself), and interpretation
happens **only at the edge** (`realPort` drives `System.Diagnostics.Process`). A one-shot run (one process → one
outcome → one record) owns no durable `Model`/`Msg` stream, so a full Elmish `Program`/`update` would be the
ceremony Principle III warns against — hence the `Model`/`Interpreter` split (no pure middle module; the pure
core it composes is F050 `recordOf`, reused verbatim), **not** a `Model`/`Msg`/`Effect`/`update` quartet. Both
sides are tested (Principle IV's two surfaces): the **pure-given-the-port** side with a deterministic **fake
port** (no process at all) and the **edge** side with `realPort` against **real `/bin/sh` temp-script
fixtures**. Principle VI is **live** (it is an I/O edge): the port is **total** and **never silently swallows** —
a start failure → a recorded `startFailureExitCode` + a captured diagnostic; a timeout → a recorded
`timeoutExitCode`; both sentinels exported so a consumer can distinguish a tool-level failure from an ordinary
gate exit.

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel. Stories map to
spec user stories — US1 (P1, headline MVP) run a real gate → assembled record + close-the-loop; US2 (P1,
co-critical) a failed / missing / overrunning gate is recorded, never thrown; US3 (P2) reproducible identity is
stable across runs, only the duration varies.

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

**Purpose**: Create the new impure edge library + its focused test project so everything compiles and the
solution restores. No semantics yet. Nothing existing is edited beyond the solution file and `CLAUDE.md`.

- [X] T001 Create `src/FS.GG.Governance.GateExecution/FS.GG.Governance.GateExecution.fsproj` — SDK-style,
  `net10.0`, `RootNamespace`/`PackageId` `FS.GG.Governance.GateExecution`, `Version` `0.1.0`, `IsPackable=true`
  with a `PackageId` (the new-package-identity precedent of `ExecutionRecord`/`EvidenceCapture`/`Snapshot`; "pack
  output unaffected" means *existing* packages are untouched, not that this library is unpackable). `<Compile>`
  order **`Model.fsi`, `Model.fs`, `Interpreter.fsi`, `Interpreter.fs`** (the `Snapshot` edge precedent). **Exactly
  one** `<ProjectReference>` — to `../FS.GG.Governance.ExecutionRecord/FS.GG.Governance.ExecutionRecord.fsproj`
  (F050 `recordOf`, and `digestOf` transitively). `FS.GG.Governance.CommandRecord` (F032 `CommandRecord`, the
  reproducible-fact types, `ExitCode`, `CapturedOutput`, `SensedDuration`) and `FS.GG.Governance.Config` (F014
  `TimeoutLimit`) arrive **transitively** through F050 (SDK `ProjectReference`s flow by default) and need **no**
  direct reference — the minimal-reference precedent of F050/F049. **No third-party `PackageReference`** (FR-011):
  the process is started through `System.Diagnostics.Process`/`ProcessStartInfo` and timed with
  `System.Diagnostics.Stopwatch` — both BCL, both the established `Snapshot` interpreter precedent. Add a header
  comment (mirroring the `ExecutionRecord`/`Snapshot` `.fsproj`): the **first and only** process-spawning
  capability in the codebase — the impure **gate-execution port** behind an injected `ExecutionPort`;
  `senseExecution` = edge I/O + the pure F050 `recordOf`; `realPort` is the sole place a process starts; total &
  safe (records, never throws or hangs); reuses F050/F032/F014 vocabulary verbatim, introduces no schema/CLI/
  persisted artifact, referenced by nothing on landing; one-way dependency `GateExecution -> ExecutionRecord ->
  CommandRecord -> Config`.
- [X] T002 [P] Create `tests/FS.GG.Governance.GateExecution.Tests/FS.GG.Governance.GateExecution.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s `Expecto`, `Expecto.FsCheck`, `FsCheck`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no new package).
  `<ProjectReference>`s to the new library **and** `FS.GG.Governance.ExecutionRecord` (call the genuine
  `ExecutionRecord.digestOf` to compute expected digests), `FS.GG.Governance.CommandRecord` (call
  `CommandRecord.canonicalId`/`identityValue`, read the `Reproducible` facts), `FS.GG.Governance.Config`
  (`TimeoutLimit`, and the `CheckId`/`DomainId`/`CommandId`/`EnvironmentClass` the close-loop freshness fixtures
  need), **and — for the close-the-loop round-trip only** — `FS.GG.Governance.EvidenceCapture` (F049
  `referenceOf`/`capture`), `FS.GG.Governance.EvidenceReuse` (F030 `decide`/`empty`), and
  `FS.GG.Governance.FreshnessKey` (F029 `FreshnessInputs`) — exactly as the `Snapshot` test project pulled in
  `Routing` for its SC-001 feed-through and F050's test project pulled in F049/F030/F029 for its round-trip; the
  **production** library references none of these (asserted by T021's scope-hygiene check). The **final**
  `<Compile>` order is `Support.fs`, `SenseTests.fs`, `FailureTests.fs`, `IdentityTests.fs`, `CloseLoopTests.fs`,
  `SurfaceDriftTests.fs`, `Main.fs` — but each entry is added by the task that **creates** its file so the project
  always compiles. At this step wire in **only** `Support.fs` (T009) and `Main.fs` (T010); the later test files
  add their own `<Compile>` entry in the position shown when they are created (T012 `SenseTests.fs`, T016
  `FailureTests.fs`, T018 `IdentityTests.fs`, T015 `CloseLoopTests.fs`, T019 `SurfaceDriftTests.fs`). Do **not**
  list a file before it exists. Mirror `tests/FS.GG.Governance.Snapshot.Tests/...Tests.fsproj`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh GUIDs
  and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` (between `<!-- SPECKIT START/END -->`) at
  `specs/051-gate-execution-port/plan.md`. No other doc changes.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; `dotnet sln list` shows both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stub, test scaffolding) — BLOCKS all stories

**Purpose**: Drop the sole public surface (the two `.fsi`), prove it in FSI (Principle I), add the `Model` types
(real — they are plain declarations, not behavior) and a compiling `Interpreter.fs` (sentinels real;
`realPort`/`senseExecution` stubbed), plus test scaffolding so the library and tests compile and tests can FAIL
before implementation. **⚠️ No story work begins until this phase is complete** — `Interpreter.fsi` declares
`realPort` and `senseExecution`, so the `.fs` must satisfy the full signature to compile.

- [X] T005 Author `src/FS.GG.Governance.GateExecution/Model.fsi` — drop `contracts/Model.fsi` **verbatim**:
  `namespace FS.GG.Governance.GateExecution`; the two `open`s (`FS.GG.Governance.Config.Model` for `TimeoutLimit`,
  `FS.GG.Governance.CommandRecord.Model` for `Executable`/`Argument`/`WorkingDirectory`/`EnvironmentDelta`/
  `ExitCode`/`CapturedOutput`/`SensedDuration`); the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module Model` with the three
  declarations — `GateCommand` (6 fields), `ExecutionOutcome` (`Stdout`/`Stderr: byte[]`, `ExitCode`, `Duration`),
  and `type ExecutionPort = GateCommand -> ExecutionOutcome` — each carrying its curated doc-comment verbatim.
  Reuses F032/F014 types verbatim; introduces **no new** F032/F014 type. **No** access modifiers (Principle II —
  visibility is presence/absence here).
- [X] T006 Add `src/FS.GG.Governance.GateExecution/Model.fs` — the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module Model` with the three
  declarations **fully defined** (record types `GateCommand`/`ExecutionOutcome` and the `ExecutionPort` function-
  type abbreviation — these are data, not behavior, so no stub). The same two `open`s as the `.fsi`. No access
  modifiers (Principle II).
- [X] T007 Author `src/FS.GG.Governance.GateExecution/Interpreter.fsi` — drop `contracts/Interpreter.fsi`
  **verbatim**: `namespace FS.GG.Governance.GateExecution`; the two `open`s (`FS.GG.Governance.CommandRecord.Model`
  for `ExitCode`/`CommandRecord`, `FS.GG.Governance.GateExecution.Model` for `GateCommand`/`ExecutionOutcome`/
  `ExecutionPort`); the `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module
  Interpreter` with exactly four members — `val startFailureExitCode: ExitCode`, `val timeoutExitCode: ExitCode`,
  `val realPort: ExecutionPort`, and `val senseExecution: port: ExecutionPort -> command: GateCommand ->
  CommandRecord` — each carrying its curated sentinel / port-isolation / verbatim-delegation / totality doc-comment
  verbatim. **No** access modifiers (Principle II).
- [X] T008 Add `src/FS.GG.Governance.GateExecution/Interpreter.fs` — the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module Interpreter` satisfying
  `Interpreter.fsi`: define `startFailureExitCode` and `timeoutExitCode` as **real** `ExitCode` constants (values
  an ordinary successful gate would not return — documented at the definition site per data-model.md §Sentinel
  exit codes), and `realPort`/`senseExecution` as `failwith "not implemented"` stubs satisfying the signatures.
  No `private`/`internal`/`public` modifiers (Principle II — process-spawning helpers will live unexported by
  absence from the `.fsi`, added in Phases 3–4). Confirm `dotnet build src/FS.GG.Governance.GateExecution/...` is
  clean under `TreatWarningsAsErrors`.
- [X] T009 [P] Write `tests/FS.GG.Governance.GateExecution.Tests/Support.fs` — real, literally-constructible
  builders (Principle V; **no mocks**) and the **two test surfaces** (Principle IV): (1) a **fake-port builder** —
  `fakePort : byte[] -> byte[] -> ExitCode -> SensedDuration -> ExecutionPort` (and convenience defaults) that
  returns a literal `ExecutionOutcome` regardless of the command, so `senseExecution` can be driven with **no
  process at all** (the pure-given-the-port side). (2) a **`GateCommand` builder** with sensible defaults and
  per-field overrides so a test can perturb exactly one reproducible fact (executable, an argument, argument
  **order**, working dir, a single env-delta entry per class, timeout, captured-output target) — covering the
  three `EnvironmentDelta` classes (`Added`/`Changed`/`Removed`, any empty) and `NoCapturedOutput` plus a captured
  target. (3) **real temp-script fixtures** (the edge side, mirroring the `Snapshot` tests' real `git`): helpers
  that write tiny `/bin/sh` scripts to a temp dir and return a `GateCommand` pointing at `/bin/sh <script>` — a
  script that prints **known** bytes to stdout and stderr and exits `0`; one that exits `7` after writing output;
  one that sleeps far longer than a short `TimeoutLimit`; plus a `GateCommand` naming a **guaranteed-missing**
  executable; and edge fixtures emitting **empty**, **binary/non-UTF-8**, and **large** (~1 MB) output. (4) a
  `repoRoot` finder (the `Snapshot` `Support.fs` precedent) for the surface baseline path. (5) for the close-loop
  tests, an `inputs` builder producing a complete literal `FreshnessInputs` (categories present/distinct) plus a
  `differentInputs`. (6) FsCheck generators for arbitrary `byte[]` and arbitrary well-typed `ExecutionOutcome`/
  `GateCommand`. Temp scripts are created/cleaned **within the fixtures**; **no network, no governed repository**
  anywhere (SC-007). Output digests in assertions are **derived from real captured bytes** (`ExecutionRecord.digestOf`),
  never `Synthetic`/`OutputDigest` literals.
- [X] T010 [P] Write `tests/FS.GG.Governance.GateExecution.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.
- [X] T011 [P] Append an F051 design-first section to `scripts/prelude.fsx` (after the F050 section) — the
  Principle-I FSI proof / documentation-of-record **before** any operation body lands. Exercise the
  `quickstart.md` "Exercise in FSI" sketch verbatim: `#r` the new Debug DLL plus the `ExecutionRecord`,
  `CommandRecord`, `EvidenceReuse`, `EvidenceCapture`, and `FreshnessKey` DLLs; `open System.Text` and the model +
  `Interpreter` modules. **(1) PURE GIVEN THE PORT**: build a deterministic `fakePort` returning literal `Stdout`/
  `Stderr` bytes + `ExitCode 0` + a fixed `SensedDuration`, run `senseExecution fakePort cmd`, and assert
  `record.Reproducible.StdoutDigest = ExecutionRecord.digestOf stdoutBytes` and `StderrDigest = digestOf
  stderrBytes` (`true`), every reproducible fact equals `cmd` (`true`), and `CommandRecord.canonicalId record` is
  defined. **(2) THE REAL EDGE**: write a temp `/bin/sh` script, run `senseExecution Interpreter.realPort realCmd`,
  and print the real captured digests / exit code. **(3) close-the-loop**: `EvidenceCapture.referenceOf record`
  defined and `EvidenceReuse.decide inputs (EvidenceCapture.capture inputs record EvidenceReuse.empty) = Reuse
  (referenceOf record)` (`true`). Documents the shape even while `realPort`/`senseExecution` are stubbed (its
  assertions fail against the stubs — expected).

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles with **only** `Support.fs`
+ `Main.fs` wired (story test files and their `<Compile>` entries arrive in Phases 3–5, each added by the task
that creates the file); `dotnet fsi scripts/prelude.fsx` loads the F051 section (its `realPort`/`senseExecution`
assertions fail against the stubs — expected). The first story test to FAIL against the stubs lands in Phase 3
(T012).

---

## Phase 3: User Story 1 — Run a real gate and obtain its assembled command record (Priority: P1) 🎯 MVP

**Goal**: Implement the composition `senseExecution` (edge I/O + the pure F050 `recordOf`, one expression) and the
**happy path** of `realPort` (spawn once, capture raw stdout/stderr bytes, sense the integer exit code and
wall-clock duration on a clean exit) so a real clean gate assembles into a complete F032 `CommandRecord` whose
`StdoutDigest`/`StderrDigest` are real digests of the captured bytes and whose `canonicalId` derives a reproducible
F049 reference. This is the headline value: the missing bridge from *a process that actually ran* to a
`CommandRecord`. **The MVP** — `senseExecution` is exercised pure-given-the-port (fake) and at the real edge
(clean fixture).

**Independent Test**: Drive `senseExecution` with a deterministic fake port returning known bytes, and with the
real port against a `/bin/sh` script that prints known bytes and exits `0`; assert the returned record's
`StdoutDigest = ExecutionRecord.digestOf <captured stdout>`, `StderrDigest = digestOf <captured stderr>`,
`ExitCode = ExitCode 0`, every other reproducible fact equals the command-to-run verbatim, and `canonicalId` is
defined — then derive an F049 `referenceOf`/`capture` from it. No governed repository, no network.

### Tests for User Story 1 (write first; must FAIL against the stub) ⚠️

- [X] T012 [P] [US1] `tests/FS.GG.Governance.GateExecution.Tests/SenseTests.fs` — drive `senseExecution` **both**
  through a fake port and the real port (US1 acceptance 1 + 2, FR-002/FR-004, SC-001/SC-008): (1) **digests of the
  captured bytes, never swapped** — with **distinct** stdout/stderr bytes, `record.Reproducible.StdoutDigest =
  ExecutionRecord.digestOf <stdout>` and `StderrDigest = digestOf <stderr>`; a fixture whose two streams are
  swapped yields a different record (positions are not interchangeable). (2) **`ExitCode 0` on a clean exit** —
  the real `/bin/sh` "exit 0" fixture records `ExitCode (ExitCode 0)`. (3) **verbatim carriage** — `Executable`,
  `Arguments` **in supplied order** (a reordered-arguments command assembles to a different record),
  `WorkingDirectory`, the `Environment` delta's **three classes preserved** (a `Changed` entry never split into
  `Added`+`Removed`), `Timeout`, and `CapturedOutput` equal the command-to-run exactly. (4) **`canonicalId`
  defined** for both fake and real records. (5) **any size / any bytes** (SC-008) — the empty, binary/non-UTF-8,
  and ~1 MB real fixtures each capture and digest in full (digest equals `digestOf` of the captured bytes), with
  no truncation or decoding. Fake-port assertions must FAIL against the T013 stub; real-edge assertions against
  the T014 stub. Add `SenseTests.fs` to the test `.fsproj` `<Compile>` immediately after `Support.fs`.

### Implementation for User Story 1

- [X] T013 [US1] Implement `senseExecution` in `src/FS.GG.Governance.GateExecution/Interpreter.fs` per
  data-model.md §`senseExecution` — the whole body is one expression: `let outcome = port command in
  ExecutionRecord.recordOf command.Executable command.Arguments command.WorkingDirectory command.Environment
  command.Timeout outcome.ExitCode outcome.Stdout outcome.Stderr command.CapturedOutput outcome.Duration`. It is
  `port` ∘ pure F050 `recordOf` — `outcome.Stdout → StdoutDigest`, `outcome.Stderr → StderrDigest` (never
  swapped), exit code and duration from the outcome, every other reproducible fact carried verbatim from the
  command. **Pure given the port** — it starts no process itself (FR-010); **no** new record shape, normalization,
  or success/exit-code/reuse policy (FR-004/FR-005). No access modifiers (Principle II). After T013 the fake-port
  assertions in T012 go green.
- [X] T014 [US1] Implement the **happy path** of `realPort` in `src/FS.GG.Governance.GateExecution/Interpreter.fs`
  per data-model.md §`realPort` steps 1, 3, 4 (clean exit), 5 — build `ProcessStartInfo` from the `GateCommand`
  (the `Executable`, the **ordered** `Arguments` via `ArgumentList` — no shell string-splitting,
  `WorkingDirectory`, `RedirectStandardOutput`/`RedirectStandardError = true`, `UseShellExecute = false`); apply
  the `Environment` delta's three classes to `psi.Environment` (Added/Changed set, Removed deletes — **applied**,
  not diffed back, research D7); start a `Stopwatch`; `Process.Start`; drain the redirected **base byte streams**
  into in-memory buffers **concurrently** (one on a background read, to avoid pipe-buffer deadlock — raw bytes
  only, never `ReadToEnd()` text, FR-002); `WaitForExit`; capture the drained bytes, the real integer exit code as
  `ExitCode`, and the elapsed `SensedDuration`. **Spawn exactly once** per call (FR-001); confine all process I/O
  to this one binding (FR-010); helpers stay unexported (Principle II). (Start-failure catch + timeout enforcement
  arrive in US2/T017 — same file.) Same file as T013 (sequence after it). After T014 the real-edge assertions in
  T012 go green for the clean fixtures.
- [X] T015 [P] [US1] `tests/FS.GG.Governance.GateExecution.Tests/CloseLoopTests.fs` — close the chain over the
  **real** F049/F030 operations from a record `senseExecution` assembled (US1 acceptance 3, SC-001): (1)
  **`referenceOf` reproducible** — `EvidenceCapture.referenceOf (senseExecution port cmd)` is defined and
  `referenceOf r = referenceOf r` byte-for-byte. (2) **`capture` makes the world reusable** — `EvidenceReuse.decide
  inputs (EvidenceCapture.capture inputs (senseExecution port cmd) EvidenceReuse.empty) = Reuse
  (EvidenceCapture.referenceOf (senseExecution port cmd))`. Drive once via a fake port (deterministic) and once via
  the real clean fixture so the loop closes from a **genuinely executed** gate. The only fixtures needing
  F049/F030/F029 are here (test-only refs from T002). No network, no governed repository (SC-007). Add
  `CloseLoopTests.fs` to the test `.fsproj` `<Compile>` immediately after `IdentityTests.fs` (its final position;
  it may be authored now and run once T013/T014 land). Depends on **T013** (and T014 for the real-edge variant).

**Checkpoint**: US1 is functional — a real clean gate run assembles into a complete F032 `CommandRecord` whose
output digests are real byte-stable digests of the captured output, and that record derives a reproducible F049
reference and a reusable store entry. **This is the shippable MVP**: the chain finally closes from a gate the
system actually ran. Safe-failure (US2) and identity-stability (US3) follow.

---

## Phase 4: User Story 2 — A failed, missing, or overrunning gate is recorded, never thrown (Priority: P1)

**Goal**: Complete `realPort`'s **totality and safe failure** (Principle VI) — a non-zero exit is recorded
verbatim (already a consequence of the T014 happy path: the real exit code flows through); a process-start failure
is **caught** and reified as `startFailureExitCode` + a captured diagnostic (the exception message in the stderr
bytes), never thrown; an overrunning process is **terminated** (`Kill(entireProcessTree = true)`) and recorded as
`timeoutExitCode` + partial output + elapsed duration, **within a bounded time** of the limit — `senseExecution`
never hangs. Co-critical with US1: the first process-spawning edge must be safe, not just happy-path.

**Independent Test**: Run three real fixtures through `senseExecution Interpreter.realPort` — a `/bin/sh` script
exiting `7`, a non-existent executable, and a script sleeping past a short `TimeoutLimit` — and assert each returns
an ordinary `CommandRecord` (real exit code `7` / `startFailureExitCode` / `timeoutExitCode`, captured output
digested) **within a bounded time**, with no exception escaping.

### Tests for User Story 2 (write first; must FAIL against the T014 happy-path-only `realPort`) ⚠️

- [X] T016 [P] [US2] `tests/FS.GG.Governance.GateExecution.Tests/FailureTests.fs` — drive `senseExecution
  Interpreter.realPort` over the failure fixtures (US2 acceptance 1–3, FR-005/FR-006/FR-007/FR-008,
  SC-002/SC-003/SC-004): (1) **non-zero exit recorded, not rejected** — the "exit 7" fixture → `ExitCode (ExitCode
  7)`, captured output digested (`StdoutDigest = digestOf <captured>`); no success/exit-code gating is applied. (2)
  **missing executable → recorded failure, no throw** — a `GateCommand` naming a non-existent executable yields an
  ordinary record with `ExitCode startFailureExitCode` and a **captured diagnostic** in the stderr bytes (the
  digest is `digestOf` of a non-empty buffer); `senseExecution` does **not** throw (assert via `Expect.isOk`/no
  exception). (3) **timeout bounded** — a script sleeping far past a short (e.g. 1s) `TimeoutLimit` is **terminated**
  and returns within a bounded time (assert the wall-clock of the call is comfortably under the script's sleep, not
  the full overrun) carrying `ExitCode timeoutExitCode`, the captured partial output, and an elapsed
  `SensedDuration`; `senseExecution` never hangs. All through the injected port, **no** network, **no** governed
  repository (SC-007). Add `FailureTests.fs` to the test `.fsproj` `<Compile>` immediately after `SenseTests.fs`.

### Implementation for User Story 2

- [X] T017 [US2] Complete `realPort` in `src/FS.GG.Governance.GateExecution/Interpreter.fs` per data-model.md
  §`realPort` steps 2 (start failure) and 4 (overrun) — wrap `Process.Start` in a `try/with`: a `null` process or
  thrown exception → an `ExecutionOutcome` with `ExitCode = startFailureExitCode`, the exception message as the
  **stderr** bytes (the captured diagnostic), empty stdout bytes, and the elapsed duration (FR-007); and bound the
  wait by the `TimeoutLimit` — on overrun, `Kill(entireProcessTree = true)`, capture whatever was drained, set
  `ExitCode = timeoutExitCode`, record the elapsed duration, and return within a bounded time of the limit (FR-006).
  `realPort` is now **total** (FR-008) — never throws, never hangs — for every outcome (clean / non-zero / start
  failure / timeout / empty / binary / large). No access modifiers (Principle II). Same file as T013/T014 (sequence
  after T014). Run T016: green.

**Checkpoint**: US1 + US2 — the edge is total and safe: a failed gate is recorded with its real exit code, a
missing executable becomes a recorded `startFailureExitCode` + diagnostic (no throw), and an overrunning gate is
killed and recorded as `timeoutExitCode` within a bounded time (no hang). A governance run can never crash or hang
because a gate misbehaved, and a failed gate is captured as the evidence the thread exists to record.

---

## Phase 5: User Story 3 — The reproducible identity is stable across runs; only duration varies (Priority: P2)

**Goal**: Verify that `senseExecution`'s record carries the F050/F032 reproducible-identity guarantee through the
edge — two runs of the same deterministic gate over the same world yield **byte-identical** `canonicalId` despite
differing measured `SensedDuration`, the edge leaks no clock/GUID/abs-temp-path/locale/pid/ambient-env into the
reproducible facts, and every reproducible-fact perturbation changes the identity. This phase adds **no new
implementation** — `senseExecution` already delegates to `recordOf`, which excludes the duration (F050 FR-006) —
only the assertions that pin the property at the edge. **Depends on T013/T014.**

**Independent Test**: Run a deterministic `/bin/sh` fixture through `senseExecution Interpreter.realPort` **twice**
and assert the two records' `canonicalId` are byte-identical while their `Duration` may differ; then perturb each
reproducible input in turn (one output byte, an argument, the working directory, an env entry, the exit code) and
assert each perturbation changes `canonicalId`, while a duration-only difference (a fake port varying only
`SensedDuration`) does not.

### Tests for User Story 3 (write first; assert the contract `senseExecution` already satisfies) ⚠️

- [X] T018 [P] [US3] `tests/FS.GG.Governance.GateExecution.Tests/IdentityTests.fs` — (1) **stable across runs**
  (US3 acceptance 1, SC-005, FR-009) — run a deterministic real fixture through `senseExecution Interpreter.realPort`
  twice; `CommandRecord.canonicalId run1 = canonicalId run2` byte-for-byte even though `run1.Duration` may differ
  from `run2.Duration` (and `EvidenceCapture.referenceOf run1 = referenceOf run2`). (2) **identity sensitivity**
  (US3 acceptance 2, SC-006) — using fake-port outcomes from `Support.fs`, perturb exactly **one** reproducible
  input at a time (one output byte of stdout, one of stderr, an argument, argument **order**, the working
  directory, one env-delta entry per class, the exit code) ⇒ `canonicalId` (and `referenceOf`) **differ** (a worked
  example per input plus an FsCheck property). (3) **duration-invariance** (US3 acceptance 3, SC-006) — two
  fake-port outcomes identical in every reproducible fact (incl. byte buffers) and differing **only** in
  `SensedDuration` ⇒ byte-identical `canonicalId` **and** byte-identical `referenceOf` (the duration is never read
  by `recordOf`/`canonicalId`). No filesystem beyond the temp-script fixtures, no clock-reading, no network
  (SC-007/SC-008). Add `IdentityTests.fs` to the test `.fsproj` `<Compile>` immediately after `FailureTests.fs`.

**Checkpoint**: US1 + US2 + US3 — the impure edge confines its non-determinism to the `SensedDuration` alone: two
runs of a deterministic gate assemble byte-identical `canonicalId`/`EvidenceRef`, every reproducible-fact change is
observable, and a duration-only difference is not. The downstream reuse store can reuse a re-run gate's evidence.

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope hygiene, validation)

**Purpose**: Lock the public surface (Principle II), prove the production assembly's reference graph stays minimal
and the change is additive (FR-011, SC-007), and run the quickstart end-to-end. Bless the baseline only after the
surface is final (the Phase-2 `.fsi` are unchanged through implementation).

- [X] T019 `tests/FS.GG.Governance.GateExecution.Tests/SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test
  (the `Snapshot`/`FreshnessSensing` edge precedent, mirror `tests/FS.GG.Governance.Snapshot.Tests/
  SurfaceDriftTests.fs`): enumerate the public surface of the production `FS.GG.Governance.GateExecution` assembly
  and compare byte-for-byte to `surface/FS.GG.Governance.GateExecution.surface.txt`, with the `BLESS_SURFACE=1`
  re-bless path (reflection lives ONLY in this test, never in the library); plus a **scope-hygiene** assertion
  (Principle II, plan Engineering Constraints) that the **production** assembly references **only**
  `FS.GG.Governance.ExecutionRecord` and — transitively — `FS.GG.Governance.CommandRecord` /
  `FS.GG.Governance.Config`, plus `FSharp.Core` / BCL — and **not** `EvidenceCapture`, `EvidenceReuse`,
  `FreshnessKey`, `FreshnessSensing`, `EvidenceReuseStore`, `CacheEligibility`, `RouteJson`, `AuditJson`,
  `Enforcement`, `Ship`, `Snapshot`, `Routing`, `Findings`, any `Adapters.*`, `Host`, `Cli`, and no third-party
  package. (The check inspects the **production** assembly, not the test assembly — the test project's
  F049/F030/F029 references are deliberately excluded.) Add `SurfaceDriftTests.fs` to the test `.fsproj`
  `<Compile>` immediately after `CloseLoopTests.fs` and before `Main.fs`.
- [X] T020 Generate and commit `surface/FS.GG.Governance.GateExecution.surface.txt` via `BLESS_SURFACE=1 dotnet
  test tests/FS.GG.Governance.GateExecution.Tests/...`; review the diff (exactly the `Model` module — `GateCommand`,
  `ExecutionOutcome`, `ExecutionPort` — and the `Interpreter` module — `startFailureExitCode`, `timeoutExitCode`,
  `realPort`, `senseExecution`; **no** process-spawning helper leak, since they are unexported by absence from the
  `.fsi`) and commit it as part of the Tier-1 change. After this, T019 runs green without `BLESS_SURFACE`. **No
  existing baseline is re-blessed** — this row adds one new baseline and touches no merged F029–F050 surface or
  golden (SC-007).
- [X] T021 [P] Verify SC-007 (additive-only) by inspection: `git status` / `git diff` shows **no** edit to any
  merged F029–F050 core (`src/FS.GG.Governance.ExecutionRecord/**`, `src/FS.GG.Governance.CommandRecord/**`,
  `src/FS.GG.Governance.EvidenceCapture/**`, `src/FS.GG.Governance.EvidenceReuse/**`,
  `src/FS.GG.Governance.FreshnessKey/**`, the `Snapshot`/`FreshnessSensing` edges, the F041–F050 cores and host
  commands) or to any existing test project, and `git diff surface/` touches **only** the one new `GateExecution`
  baseline; **no** schema-version bump (no `fsgg.evidence-reuse-store/v1` / `route.json` / `audit.json` token
  changed — this row consumes the F050/F032/F014 vocabulary and introduces none), **no** golden-fixture re-bless
  (do **not** run `BLESS_FIXTURES=1`), **no** new third-party dependency (BCL `System.Diagnostics.Process`/
  `Stopwatch` only). Only NEW files under `src/`, `tests/`, `surface/`, `specs/051-gate-execution-port/`, plus
  `scripts/prelude.fsx` + the `CLAUDE.md` pointer.
- [X] T022 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`; `dotnet fsi
  scripts/prelude.fsx` (the F051 section prints the fake-port digests = `digestOf` `true`, every reproducible fact
  carried `true`, `canonicalId` defined, the real-edge record's real captured digests/exit code, and close-the-loop
  `Reuse` `true`); `dotnet test tests/FS.GG.Governance.GateExecution.Tests/...` (all green under
  `TreatWarningsAsErrors`, incl. `SenseTests` fake + real edge, `FailureTests` non-zero/missing/timeout,
  `IdentityTests` stable-across-runs/sensitivity/duration-invariance, `CloseLoopTests` against the **real**
  F049/F030, and surface drift). Confirm `dotnet build && dotnet test` over the existing projects is unchanged (the
  new library + test project are purely additive). Fix any drift.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-008 covered; the Tier-1 surface is blessed
and guarded with a minimal production reference graph (one `ProjectReference` to `ExecutionRecord`); existing
F029–F050 cores, edges, baselines, and goldens byte-unchanged. **The first process-spawning capability lands** —
`senseExecution`/`realPort` run a real gate behind an injected port, capture its raw output bytes / exit code /
duration, enforce the timeout, and assemble a complete F032 `CommandRecord` that derives a real, reproducible
`EvidenceRef` and folds into the store. The chain now runs from a **gate the system actually ran** all the way to a
durable store entry. The **host wiring** that runs gates inside `fsgg route` / `fsgg ship` and persists the grown
store remains the explicit **following** row.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — `Interpreter.fsi` declares `realPort`
  and `senseExecution`, so the compiling stub `Interpreter.fs`, FSI proof, and test scaffolding (`Support.fs`,
  `Main.fs`) must exist before any story test can be written and FAIL.
- **Phase 3 (US1, MVP)**: depends on Phase 2. `senseExecution` (T013) and `realPort` happy path (T014) are the
  load-bearing bodies; `CloseLoopTests` (T015) depends on T013 (and T014 for the real-edge variant).
- **Phase 4 (US2)**: depends on Phase 2; its `realPort` completion (T017) **sequences after T014** (same file,
  extends the happy-path binding). Independently testable (failure fixtures) once T017 lands.
- **Phase 5 (US3)**: depends on **T013/T014** — test-only, no new implementation; asserts the identity contract
  `senseExecution`/`recordOf` already satisfy.
- **Phase 6 (surface/polish)**: last — bless the baseline only after the surface is final (Phase-2 `.fsi`
  unchanged through implementation).

### Within each story

- Each story's test file is written FIRST and must FAIL against the Phase-2 stub (or, for US2, the T014
  happy-path-only `realPort`), then pass after its implementation task lands (T012→T013/T014; T016→T017). US3's
  T018 asserts the contract `senseExecution` already satisfies once T013/T014 land.
- The two `.fsi` surfaces precede the `.fs` bodies that satisfy them (`Model.fsi`→`Model.fs`,
  `Interpreter.fsi`→`Interpreter.fs`); `Support.fs` (T009) precedes every story test file that consumes its
  builders/fixtures/generators.
- The `Interpreter.fs` bodies are sequential — `senseExecution` (T013), `realPort` happy path (T014), `realPort`
  failure/timeout completion (T017) all edit the same file.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) and T004 `[P]` (CLAUDE.md) are independent of T001 (library `.fsproj`);
  T003 (sln) needs T001 + T002.
- **Phase 2**: T005 (`Model.fsi`)→T006 (`Model.fs`) and T007 (`Interpreter.fsi`)→T008 (`Interpreter.fs`) are
  sequential pairs (`Model` precedes `Interpreter` per compile order); T009/T010/T011 are `[P]` against each other
  (distinct files — `Support.fs`, `Main.fs`, `scripts/prelude.fsx`) and need the compiling stub (DLL name fixed by
  T001).
- **Story test files**: T012 (`SenseTests.fs`), T015 (`CloseLoopTests.fs`), T016 (`FailureTests.fs`), T018
  (`IdentityTests.fs`) are `[P]` across distinct files; all share `Support.fs` (T009) as a prerequisite.
- **Implementation tasks T013→T014→T017 are sequential** — they edit the same `Interpreter.fs`.
- **Phase 6**: T021 `[P]` (git inspection) is independent; T019→T020→T022 are sequential (bless after the surface
  test, validate after the bless).

---

## Implementation Strategy

### MVP scope

The MVP is **User Story 1** — the headline value: a real gate run becomes a complete, reproducible
`CommandRecord` ready for F049. The minimal shippable path is **Phase 1 → Phase 2 → Phase 3** (`senseExecution` +
`realPort` happy path + close-the-loop). **STOP and VALIDATE** after Phase 3: a clean `/bin/sh` fixture run through
`senseExecution Interpreter.realPort` yields a record whose `StdoutDigest`/`StderrDigest` are real digests of the
captured bytes and whose `referenceOf`/`capture` close the loop. Safe-failure totality (US2) is **co-critical** for
a process-spawning edge — ship US1 + US2 together as the safe MVP; identity-stability (US3) and surface governance
follow.

### Incremental delivery

1. Setup + Foundational → library + tests compile, stubs FAIL.
2. + US1 (`senseExecution` + `realPort` happy path + close-the-loop) → **MVP**: a real clean gate assembles into a
   complete F032 record and closes the chain to F049.
3. + US2 (`realPort` failure/timeout completion) → total & safe: failed/missing/overrunning gates recorded, never
   thrown or hung.
4. + US3 → reproducible identity proven stable across runs at the edge (duration the sole variant).
5. + surface governance → Tier-1 baseline blessed, scope-hygiene + additive guarantee pinned.

---

## Task count & summary

- **Total**: 22 tasks (T001–T022).
- **By user story**: US1 (MVP) — 4 (T012 test, T013 `senseExecution`, T014 `realPort` happy path, T015
  close-the-loop test); US2 — 2 (T016 test, T017 `realPort` failure/timeout completion); US3 — 1 (T018,
  test-only). Shared infrastructure / cross-cutting — 15 (Setup 4, Foundational 7, surface/polish 4).
- **Parallel opportunities**: T002+T004 (setup); T009+T010+T011 (Phase-2 scaffolding); T012 / T015 / T016 / T018
  (story test files across distinct files); T021 (git inspection in Phase 6).
- **Suggested MVP**: User Story 1 (Phases 1→3), shipped together with User Story 2 (Phase 4) as the safe MVP for
  the first process-spawning edge.

## Notes

- [P] tasks = different files, no dependencies on another incomplete task in the phase.
- [Story] label maps task to a spec user story for traceability; unlabeled = shared infrastructure.
- The production bodies are small — `Model` is three declarations; `senseExecution` is one expression (`port` ∘
  pure F050 `recordOf`); `realPort` is a single `try/with` BCL pipeline over `ProcessStartInfo` + `Stopwatch` +
  `WaitForExit(timeout)` mirroring the merged `Snapshot` `runGit`. The engineering weight is the **test evidence**
  (real-edge capture, totality/safe-failure against real fixtures, identity stability, close-the-loop) and the
  Tier-1 surface/additive governance, **not** the implementation.
- This is the codebase's **first I/O edge** in this thread — Principle IV is satisfied by the injected-port /
  interpreter boundary (I/O as data, pure given the port, interpretation only at `realPort`), tested on **both**
  sides (fake port + real `/bin/sh` fixtures), the `Snapshot`/F046 precedent. Local mutation (`MemoryStream`/
  `Process`/`Stopwatch`) is disclosed and confined to `realPort` (Principle III).
- Output digests on this path are **derived from real captured bytes** (`ExecutionRecord.digestOf`), never
  `Synthetic` literals — this row **removes the last hand-fabricated outcome** on the capture path (F050 could only
  assemble from supplied bytes; now the bytes are sensed from a real run), so the disclosure discipline is
  satisfied by the **absence** of synthetic data on this path (plan Constitution Check Principle V).
- Verify tests FAIL before implementing; commit after each task or logical group; never weaken an assertion to
  green a build — narrow scope and document.
