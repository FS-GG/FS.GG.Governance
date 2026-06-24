---
description: "Task list for Digest Captured Output And Assemble A Command Record From An Execution Outcome (F050)"
---

# Tasks: Digest Captured Output And Assemble A Command Record From An Execution Outcome

**Input**: Design documents from `/specs/050-execution-record/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/ExecutionRecord.fsi ✅, quickstart.md ✅

**Tier**: **Tier 1 (contracted change)** — a new public library + module
(`FS.GG.Governance.ExecutionRecord`) with a new package identity and a new `surface/*.surface.txt` baseline. No
existing public surface changes; **no** third-party dependency is added (BCL `SHA256` + FSharp.Core only); **no**
schema version is bumped; **no** new type is introduced (F032 `OutputDigest`/`CommandRecord` vocabulary reused
verbatim). Tests are **mandatory** (Principle V). All tasks share the feature tier; no per-task `[T1]`/`[T2]`
annotations needed.

**Elmish/MVU**: **Not applicable** — two pure, total, deterministic value transformations (`digestOf`/`recordOf`)
with no state and no I/O (FR-008, plan Constitution Check Principle IV exempt — the F049 `EvidenceCapture` / F047
`EvidenceReuseStore` pure-core precedent). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks, and — because
this row introduces **no new type** — **no `Model.fsi`/`Model.fs`** either. Hashing in-memory `byte[]` is pure
computation, not I/O. The impure gate-execution edge that *would* need an MVU boundary (spawning a process,
reading real stdout/stderr, timing the run, sensing the facts) is the explicitly out-of-scope **following** row
(the gate-execution port). Principle VI is likewise N/A — pure total functions have no failure path to observe;
**totality** stands in for safe failure (neither operation throws).

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel. Stories map to
spec user stories — US2 (P1) `digestOf` (the content-addressed digest; **built first** because `recordOf`
composes it on the two output positions), US1 (P1, headline MVP) `recordOf` close-the-loop, US3 (P2) verbatim
delegation / additive guarantee.

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

- [X] T001 Create `src/FS.GG.Governance.ExecutionRecord/FS.GG.Governance.ExecutionRecord.fsproj` — SDK-style,
  `net10.0`, `RootNamespace`/`PackageId` `FS.GG.Governance.ExecutionRecord`, `Version` `0.1.0`,
  `IsPackable=true` with a `PackageId` (the new-package-identity precedent of
  `EvidenceCapture`/`CommandRecord`/`EvidenceReuseStore`; "pack output unaffected" in the plan means *existing*
  packages are untouched, not that this library is unpackable). `<Compile>` order: **`ExecutionRecord.fsi`,
  then `ExecutionRecord.fs`** (no `Model` file — this row introduces no new type). **Exactly one**
  `<ProjectReference>` — to `../FS.GG.Governance.CommandRecord/FS.GG.Governance.CommandRecord.fsproj` (F032
  `build`, `OutputDigest`, the reproducible-fact types `Executable`/`Argument`/`WorkingDirectory`/
  `EnvironmentDelta`/`ExitCode`/`CapturedOutput`, `SensedDuration`, `CommandRecord`). `FS.GG.Governance.Config`
  (F014 `TimeoutLimit`) arrives **transitively** through F032 (SDK `ProjectReference`s flow by default) and
  needs **no** direct reference — the minimal-reference precedent of F049/F047; plan "Primary Dependencies".
  **No third-party `PackageReference`** (FR-010): the single new computation is a SHA-256 hash via
  `System.Security.Cryptography.SHA256.HashData` rendered with `System.Convert.ToHexString` — both BCL, both
  already used in the codebase (the F040 `Snapshot` interpreter). Add a header comment mirroring the
  `EvidenceCapture`/`EvidenceReuseStore` `.fsproj`: the pure value-only **content-addressing bridge** that
  hashes a gate's captured output bytes into the byte-stable `OutputDigest`s F032 requires (`digestOf`) and
  assembles a complete F032 `CommandRecord` from a captured execution outcome by composing `digestOf` on the two
  output positions and **delegating the rest to `CommandRecord.build` verbatim** (`recordOf`); the **first and
  only** place in the codebase that hashes output bytes (the gap F032 left open at D3); one-way dependency
  `ExecutionRecord -> CommandRecord -> Config`; runs NO gate, senses NO fact, times NOTHING, persists NOTHING,
  bumps NO schema version, adds NO CLI, introduces NO new type; referenced by nothing on landing.
- [X] T002 [P] Create `tests/FS.GG.Governance.ExecutionRecord.Tests/FS.GG.Governance.ExecutionRecord.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  new package). `<ProjectReference>`s to the new library **and** `FS.GG.Governance.CommandRecord` (build real
  `CommandRecord`s via the genuine `CommandRecord.build`, call `CommandRecord.canonicalId`/`identityValue`),
  `FS.GG.Governance.Config` (`TimeoutLimit`, and the `CheckId`/`DomainId`/`CommandId`/`EnvironmentClass` the
  freshness fixtures need), **and — for the close-the-loop round-trip only** — `FS.GG.Governance.EvidenceCapture`
  (F049 `referenceOf`/`capture`), `FS.GG.Governance.EvidenceReuse` (F030 `decide`/`empty`/`record`), and
  `FS.GG.Governance.FreshnessKey` (F029 `FreshnessInputs`) — exactly as F049's test project pulled in extra
  projects for its round-trip. The **final** `<Compile>` order is `Support.fs`, `DigestTests.fs`,
  `RecordTests.fs`, `CloseLoopTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` — but each entry is added by the task
  that **creates** its file so the project always compiles. At this step wire in **only** `Support.fs` (T007) and
  `Main.fs` (T008); the later test files add their own `<Compile>` entry in the position shown when they are
  created (T010 `DigestTests.fs`, T012 `RecordTests.fs`, T014 `CloseLoopTests.fs`, T016 `SurfaceDriftTests.fs`).
  Do **not** list a file before it exists. Mirror `tests/FS.GG.Governance.EvidenceCapture.Tests/...Tests.fsproj`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` (between `<!-- SPECKIT START/END -->`) at
  `specs/050-execution-record/plan.md`. No other doc changes.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; `dotnet sln list` shows both new projects.

---

## Phase 2: Foundational (the `.fsi` contract, FSI proof, compiling stub, test scaffolding) — BLOCKS all stories

**Purpose**: Drop the sole public surface (`.fsi`), prove it in FSI (Principle I), and add a compiling `.fs`
body (stubbed operations) plus test scaffolding so the library and tests compile and tests can FAIL before
implementation. **⚠️ No story work begins until this phase is complete** — the `.fsi` declares both functions,
so the `.fs` must satisfy the full signature to compile.

- [X] T005 Author `src/FS.GG.Governance.ExecutionRecord/ExecutionRecord.fsi` — drop
  `contracts/ExecutionRecord.fsi` **verbatim**: `namespace FS.GG.Governance.ExecutionRecord`; the two `open`s
  (`FS.GG.Governance.Config.Model` for `TimeoutLimit`, `FS.GG.Governance.CommandRecord.Model` for the run-fact
  types, `OutputDigest`, `CapturedOutput`, `SensedDuration`, `CommandRecord`); the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module ExecutionRecord` with
  exactly two members — `val digestOf: bytes: byte[] -> OutputDigest` and the ten-argument
  `val recordOf: executable -> arguments -> workingDirectory -> environment -> timeout -> exitCode -> stdout:
  byte[] -> stderr: byte[] -> capturedOutput -> duration -> CommandRecord` — each carrying its curated
  content-addressing / totality / verbatim-delegation / duration-invariance / close-the-loop doc-comment
  verbatim. Reuses merged F032/F014 core types verbatim; introduces **no new type**. **No** access modifiers
  anywhere (Principle II — visibility is presence/absence in this `.fsi`).
- [X] T006 Add `src/FS.GG.Governance.ExecutionRecord/ExecutionRecord.fs` — the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module ExecutionRecord` with
  `digestOf`/`recordOf` as `failwith "not implemented"` stubs satisfying the `.fsi` signature. No
  `private`/`internal`/`public` modifiers (Principle II). Confirm `dotnet build
  src/FS.GG.Governance.ExecutionRecord/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Write `tests/FS.GG.Governance.ExecutionRecord.Tests/Support.fs` — real,
  literally-constructible builders (Principle V; **no mocks**), the **derived-not-synthetic** discipline being
  the whole point of this row: (1) **real byte buffers** via `System.Text.Encoding.UTF8.GetBytes` and explicit
  `[| ... |]` literals — equal-content pairs, a single-byte-changed pair, a single-byte-added pair, a
  single-byte-removed pair, a reordered pair, the empty buffer `[||]`, a binary/non-textual buffer (bytes that
  are not valid UTF-8), and a large buffer (e.g. `Array.create 1_000_000 0uy`) — covering FR-002/FR-003 and the
  Edge cases. (2) an `outcome` builder wrapping the ten `recordOf` arguments — `(Executable …) [ Argument … ]
  (WorkingDirectory …) env (TimeoutLimit …) (ExitCode …) stdoutBytes stderrBytes capturedOutput (SensedDuration
  …)` — with sensible defaults and per-field overrides, so a test can perturb exactly **one** reproducible fact
  (executable, an argument, argument **order**, working dir, env-delta as a set, timeout, exit code, **a byte of
  stdout**, **a byte of stderr**, captured-output outcome) or **only** the `SensedDuration`; cover the edge
  outcomes — empty stdout/stderr (`[||]`), a non-zero `ExitCode`, and all three captured-output outcomes. (3)
  for the close-the-loop tests, an `inputs` builder producing a complete literal `FreshnessInputs` (every
  category present/distinct so a mismatch is observable, incl. an `Environment` from each `EnvironmentClass`
  case and `Command`/`CommandVersion` `Some`/`None` variants) plus a `differentInputs`. (4) FsCheck generators
  for arbitrary `byte[]` and arbitrary well-typed `recordOf` outcomes (varying every reproducible fact and both
  byte buffers). **No** filesystem/clock/process/network access anywhere in `Support.fs` (SC-008) — all values
  are in-memory literals/buffers; output digests are **derived from real bytes**, never synthetic `OutputDigest`
  literals.
- [X] T008 [P] Write `tests/FS.GG.Governance.ExecutionRecord.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.
- [X] T009 [P] Append an F050 design-first section to `scripts/prelude.fsx` (after the F049 section) —
  Principle-I FSI proof **before** any operation body lands. Exercise the `quickstart.md` "Exercise in FSI"
  sketch verbatim: `#r` the new Debug DLL plus the `CommandRecord`, `EvidenceReuse`, `EvidenceCapture`, and
  `FreshnessKey` DLLs; `open System.Text` and the model + `ExecutionRecord` modules; assert
  `digestOf outA = digestOf outB` (equal content → equal digest, `true`), `digestOf outA <> digestOf outC` (one
  byte differs → digest differs, `true`), `digestOf [||] <> digestOf outA` (empty defined & distinct, `true`),
  `recordOf … = CommandRecord.build … (digestOf outA) (digestOf …) …` (recordOf = build ∘ digestOf, `true`),
  `canonicalId record = canonicalId slower` and `referenceOf record = referenceOf slower` (duration-invariance,
  `true`), `referenceOf record <> referenceOf changed` (one output byte flips the reference, `true`), and the
  close-the-loop `EvidenceReuse.decide inputs (capture inputs record empty) = Reuse (referenceOf record)`
  (`true`). Documents the shape even while the bodies are stubbed (its assertions fail against the stubs —
  expected).

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles with **only** `Support.fs`
+ `Main.fs` wired (the story test files — `DigestTests.fs`, `RecordTests.fs`, `CloseLoopTests.fs`,
`SurfaceDriftTests.fs` — and their `<Compile>` entries arrive in Phases 3-6, each added by the task that creates
the file); `dotnet fsi scripts/prelude.fsx` loads the F050 section (its assertions fail against the stubs —
expected). The first story test to FAIL against the stubs lands in Phase 3 (T010).

---

## Phase 3: User Story 2 — The digest is content-addressed and reproducible (Priority: P1)

**Goal**: A pure, total, byte-stable `digestOf : byte[] -> OutputDigest` — the content-addressing primitive, the
**first and only place in the codebase that hashes output bytes** (the gap F032 left open at D3). Its body is
`OutputDigest (System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData bytes).ToLowerInvariant())`
— SHA-256 over the raw bytes, lowercase hex, wrapped in the F032 `OutputDigest` newtype **reused verbatim**.
This is built **first** because `recordOf` (US1, Phase 4) composes it on the two output positions — without it
the headline MVP cannot land.

**Independent Test**: Digest two byte sequences that are equal and assert the digests are byte-identical; digest
two that differ by a single byte (changed/added/removed/reordered) and assert the digests differ; digest `[||]`
and assert it is defined and distinct from every non-empty digest. No I/O.

### Tests for User Story 2 (write first; must FAIL against the stub) ⚠️

- [X] T010 [P] [US2] `tests/FS.GG.Governance.ExecutionRecord.Tests/DigestTests.fs` — drive `digestOf` over the
  real byte buffers from `Support.fs`: (1) **content agreement** (SC-002, FR-002, US2 acceptance 1) — two
  byte-identical buffers ⇒ `digestOf a = digestOf b` byte-for-byte (a worked example **and** an FsCheck property
  over arbitrary `byte[]` round-tripped through a copy). (2) **content sensitivity** (SC-003, FR-002, US2
  acceptance 2) — a single byte **changed**, **added**, **removed**, or **reordered** ⇒ `digestOf a <> digestOf
  b` (worked examples for each of the four perturbations, plus an FsCheck property that any non-equal buffers
  give non-equal digests). (3) **empty-input totality + distinctness** (FR-003, FR-008, Edge "Empty captured
  output") — `digestOf [||]` is defined (never throws), equals the fixed empty-SHA-256 digest, and is distinct
  from `digestOf` of every non-empty buffer. (4) **binary + large totality** (FR-008, Edge "Binary"/"Large") —
  `digestOf` of a non-textual buffer and of a ~1 MB buffer are defined, fixed-form, and never throw. (5)
  **determinism / byte-stability** (FR-009, SC-005) — `digestOf b = digestOf b` byte-for-byte for FsCheck
  buffers (no clock/GUID/path/locale/env leakage, no dependence on array identity — digest a fresh copy and the
  original, assert equal). (6) **identical streams** (Edge "Identical stdout and stderr bytes") — equal stdout
  and stderr buffers ⇒ equal digests (content alone). All with **no** filesystem/clock/process/network (SC-008).
  Add `DigestTests.fs` to the test `.fsproj` `<Compile>` immediately after `Support.fs` (the project must compile
  before this test can FAIL against the stub).

### Implementation for User Story 2

- [X] T011 [US2] Implement `digestOf` in `src/FS.GG.Governance.ExecutionRecord/ExecutionRecord.fs` per
  data-model.md §`digestOf` (D1) — the four-step BCL pipeline `OutputDigest
  (System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData bytes).ToLowerInvariant())`. A
  function of byte **content** only; reuses the codebase's existing SHA-256 precedent (the F040 `Snapshot`
  interpreter), adds **no** third-party dependency, exposes **no** policy knob (FR-011). Pure / total / no I/O
  (FR-001/FR-003/FR-008). No access modifiers (Principle II). Run T010: green.

**Checkpoint**: US2 is functional — a gate's captured output bytes derive a deterministic, byte-stable,
content-addressed `OutputDigest`. This is the load-bearing primitive `recordOf` composes.

---

## Phase 4: User Story 1 — A real execution becomes a complete, reproducible command record (Priority: P1) 🎯 MVP

**Goal**: A pure, total, byte-stable `recordOf` — exactly `CommandRecord.build` with `digestOf` composed on the
two output positions — so a captured execution outcome (raw stdout/stderr bytes + the seven supplied facts +
the sensed duration) assembles into a complete F032 `CommandRecord` ready to hand to F049 `referenceOf`/`capture`,
and the run's evidence reference is itself reproducible. This is the headline point of the row: the only step
that closes the last pure gap between a real execution and a store entry (until now F032 `build` could only be
fed hand-written digest literals and F049 could only derive references from synthetic records). **Depends on
T011** (`digestOf`).

**Independent Test**: Build a captured execution outcome (raw stdout/stderr bytes + reproducible facts + a
duration), call `recordOf`, and assert `CommandRecord.canonicalId` of the result, and `EvidenceCapture.referenceOf`
over it (F049), are defined, reproducible values — and that `EvidenceCapture.capture` of that record makes its
freshness world reusable. No I/O.

### Tests for User Story 1 (write first; must FAIL against the stub) ⚠️

- [X] T012 [P] [US1] `tests/FS.GG.Governance.ExecutionRecord.Tests/RecordTests.fs` (US1 carriage portion) —
  drive `recordOf` over real outcomes from `Support.fs`: (1) **digest in the correct position, never swapped**
  (FR-005, US1 acceptance 1) — `record.Reproducible.StdoutDigest = digestOf stdout` and
  `record.Reproducible.StderrDigest = digestOf stderr`; with **distinct** stdout/stderr bytes, swapping the two
  buffers yields a **different** record (positions are not interchangeable). (2) **verbatim carriage** (FR-005,
  US1 acceptance 1) — executable, arguments **in supplied order** (a reordered-arguments outcome assembles to a
  different record), working directory, the env delta's **three classes preserved** (a `Changed` entry never
  split into `Added`+`Removed`), timeout, exit code, and captured-output outcome are all carried into the record
  exactly as supplied. (3) **duration carried only in `Duration`** (FR-005) — `record.Duration` is the supplied
  `SensedDuration` and **no** reproducible field reads it. (4) **determinism / byte-stability** (FR-009, SC-005,
  US1 acceptance 2) — `recordOf` of identical bytes + facts yields the byte-identical record (an FsCheck property
  over arbitrary outcomes; no clock/GUID/path/locale/env leakage, no dependence on collection identity). No
  filesystem/clock/process/network (SC-008). Add `RecordTests.fs` to the test `.fsproj` `<Compile>` immediately
  after `DigestTests.fs`.

### Implementation for User Story 1

- [X] T013 [US1] Implement `recordOf` in `src/FS.GG.Governance.ExecutionRecord/ExecutionRecord.fs` per
  data-model.md §`recordOf` (D2) — the whole body is one expression: `CommandRecord.build executable arguments
  workingDirectory environment timeout exitCode (digestOf stdout) (digestOf stderr) capturedOutput duration`. It
  is `build` composed with `digestOf` on the two output fields, **nothing more** — no new record shape, no
  normalization, no reuse/success policy; every other fact (incl. arguments in order and the env delta's three
  classes) and the duration are carried by `build` verbatim. Pure / total / no I/O (FR-004/FR-007/FR-008). No
  access modifiers (Principle II). (Same file as T011 — sequence after T011, which `recordOf` composes.) Run
  T012: green.

**Checkpoint**: US1 is functional — a captured execution outcome assembles into a complete F032 `CommandRecord`
whose output digests are real byte-stable digests of the raw output. **This is the shippable MVP**: raw captured
output can finally become a record (once the deferred gate-execution port produces the captured outcome and the
host row wires it in). The close-the-loop verification with F049/F030 follows in Phase 5.

---

## Phase 5: User Story 3 — Assembly is purely additive: no new record shape, no policy, no I/O + close-the-loop (Priority: P2)

**Goal**: Verify that `recordOf` (already implemented in T013) **delegates to `CommandRecord.build` verbatim** —
introducing no new `CommandRecord` representation, no normalization, and no reuse/success policy — so the only
new computation is the two digests and the result is a plain F032 record indistinguishable from one `build` would
produce; and verify the chain closes through the **already-merged** F049/F030 path with no new code. This phase
adds **no new implementation** — `recordOf` is one expression — only the assertions that pin the additive
guarantee and the close-the-loop contract. **Depends on T013** (`recordOf`).

**Independent Test**: For an outcome whose stdout/stderr bytes have known digests, assert `recordOf outcome`
equals `CommandRecord.build` of the same facts with those digests substituted for the raw bytes; and assert
`canonicalId` + F049 `referenceOf` over `recordOf outcome` are reproducible, `capture` makes the world reusable,
and any single output-byte change flips the reference.

### Tests for User Story 3 (write first; assert the contract `recordOf` already satisfies once T013 lands) ⚠️

- [X] T014 [P] [US3] Extend `tests/FS.GG.Governance.ExecutionRecord.Tests/RecordTests.fs` (US3 portion — same
  file as T012, sequence after it): (1) **verbatim delegation = build ∘ digestOf** (SC-007, FR-004, US3
  acceptance 1, Independent Test) — `recordOf executable args wd env timeout exit stdout stderr captured duration
  = CommandRecord.build executable args wd env timeout exit (digestOf stdout) (digestOf stderr) captured
  duration` byte-for-byte (a worked example **and** an FsCheck property over arbitrary outcomes — no field
  reordered, no env-delta class merged/split, arguments in the same order, duration only in `Duration`). (2)
  **failed run recorded, not rejected** (US3 acceptance 2, FR-004, Edge "Failed run / applied timeout") — an
  outcome with a **non-zero** `ExitCode` (and one whose timeout applied) assembles to an ordinary complete
  record (no success/exit-code gating — that is the out-of-scope host row). No filesystem/clock/process/network
  (SC-008).
- [X] T015 [P] [US1] `tests/FS.GG.Governance.ExecutionRecord.Tests/CloseLoopTests.fs` — drive the
  record-to-store chain over the **real** F049/F030 operations (close-the-loop, US1 acceptance 3 + US2
  record-level): (1) **canonicalId + F049 referenceOf reproducible** (SC-001, FR-007, US1 acceptance 1) —
  `CommandRecord.canonicalId (recordOf …)` and `EvidenceCapture.referenceOf (recordOf …)` are defined and
  `referenceOf r = referenceOf r` byte-for-byte. (2) **capture makes the world reusable** (SC-001, FR-007, US1
  acceptance 3) — `EvidenceReuse.decide inputs (EvidenceCapture.capture inputs (recordOf …) EvidenceReuse.empty)
  = Reuse (EvidenceCapture.referenceOf (recordOf …))`. (3) **single-reproducible-fact perturbation changes
  identity + reference** (SC-003, FR-007, US2 acceptance 4) — perturbing any one reproducible fact (executable,
  an argument, argument **order**, working dir, env-delta as a set, timeout, exit code, **a byte of stdout**, **a
  byte of stderr**, captured-output outcome) ⇒ `canonicalId` **and** `referenceOf` change (an FsCheck property
  per fact; a one-output-byte change flips the reference so a changed output is never served as fresh). (4)
  **duration-invariance of identity + reference** (SC-004, FR-006, US2 acceptance 3) — two outcomes identical in
  every reproducible fact (incl. output bytes) and differing **only** in `SensedDuration` ⇒ byte-identical
  `canonicalId` and byte-identical `referenceOf` (neither `digestOf` nor `canonicalId` reads the duration). The
  only fixtures needing F049/F030/F029 are here; `Path`/clock/process/network are untouched (SC-008) — the
  capture core itself touches nothing. Add `CloseLoopTests.fs` to the test `.fsproj` `<Compile>` immediately
  after `RecordTests.fs`.

**Checkpoint**: US1 + US2 + US3 — the pure content-addressing bridge is complete: a gate's captured output bytes
digest deterministically (US2), assemble into a complete F032 record by verbatim delegation (US1 + US3), and that
record derives a reproducible F049 reference and a reusable store entry (close-the-loop) with the sensed duration
never leaking into the identity. The chain `recordOf → referenceOf → capture → serialise/persist` now runs from
**raw captured output** all the way to a durable store entry — every step pure except the as-yet-unbuilt process
spawn.

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope hygiene, validation)

**Purpose**: Lock the public surface (Principle II), prove the assembly's reference graph stays minimal and the
change is additive (SC-007), and run the quickstart end-to-end. Bless the baseline only after the surface is
final (the Phase-2 `.fsi` is unchanged through implementation).

- [X] T016 `tests/FS.GG.Governance.ExecutionRecord.Tests/SurfaceDriftTests.fs` — a reflective `SurfaceDrift`
  test (the F020–F049 precedent): enumerate the public surface of `FS.GG.Governance.ExecutionRecord` and compare
  byte-for-byte to `surface/FS.GG.Governance.ExecutionRecord.surface.txt`, with the `BLESS_SURFACE=1` re-bless
  path; plus a **scope-hygiene** assertion (Principle II, plan Engineering Constraints) that the production
  assembly references **only** `FS.GG.Governance.CommandRecord` and — transitively — `FS.GG.Governance.Config`,
  plus `FSharp.Core` / BCL — and **not** `EvidenceCapture`, `EvidenceReuse`, `FreshnessKey`, `FreshnessSensing`,
  `EvidenceReuseStore`, `CacheEligibility`, `RouteJson`, `AuditJson`, `Enforcement`, `Ship`, `Snapshot`,
  `Routing`, `Findings`, any `Adapters.*`, `Host`, `Cli`, and no third-party package. Mirror
  `tests/FS.GG.Governance.EvidenceCapture.Tests/SurfaceDriftTests.fs`. (The scope-hygiene check inspects the
  **production** `FS.GG.Governance.ExecutionRecord` assembly, not the test assembly — the test project's
  F049/F030/F029 references are deliberately excluded.) Add `SurfaceDriftTests.fs` to the test `.fsproj`
  `<Compile>` immediately after `CloseLoopTests.fs` and before `Main.fs`.
- [X] T017 Generate and commit `surface/FS.GG.Governance.ExecutionRecord.surface.txt` via `BLESS_SURFACE=1
  dotnet test tests/FS.GG.Governance.ExecutionRecord.Tests/...`; review the diff (exactly the one public module
  `ExecutionRecord` with `digestOf` + `recordOf`, no helper leak — there are no private helpers, no new type) and
  commit it as part of the Tier-1 change. After this, T016 runs green without `BLESS_SURFACE`. **No existing
  baseline is re-blessed** — this row adds one new baseline and touches no merged F029–F049 surface or golden
  (SC-007).
- [X] T018 [P] Verify SC-007 (additive-only) by inspection: `git status` / `git diff` shows **no** edit to any
  merged F029–F049 core (`src/FS.GG.Governance.CommandRecord/**`, `src/FS.GG.Governance.EvidenceCapture/**`,
  `src/FS.GG.Governance.EvidenceReuse/**`, `src/FS.GG.Governance.FreshnessKey/**`, the F041–F049 cores and host
  commands) or to any existing test project, and `git diff surface/` touches **only** the one new
  `ExecutionRecord` baseline; **no** schema-version bump (no token is changed — this row consumes the F032
  vocabulary and introduces none), **no** golden-fixture re-bless (do **not** run `BLESS_FIXTURES=1`), **no** new
  third-party dependency (BCL `SHA256` only). Only NEW files under `src/`, `tests/`, `surface/`,
  `specs/050-execution-record/`, plus `scripts/prelude.fsx` + the `CLAUDE.md` pointer.
- [X] T019 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`; `dotnet fsi
  scripts/prelude.fsx` (the F050 section prints content-agreement `true`, one-byte-differs `true`, empty-distinct
  `true`, recordOf = build ∘ digestOf `true`, duration-invariance `true`, one-output-byte different-reference
  `true`, close-the-loop `true`); `dotnet test tests/FS.GG.Governance.ExecutionRecord.Tests/...` (all green under
  `TreatWarningsAsErrors`, incl. digest agreement/sensitivity/totality, record carriage + verbatim delegation,
  close-the-loop against the **real** F049/F030, and surface drift). Confirm `dotnet build && dotnet test` over
  the existing projects is unchanged (the new library + test project are purely additive). Fix any drift.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-008 covered; the Tier-1 surface is
blessed and guarded with a minimal reference graph (one production `ProjectReference`); existing F029–F049 cores,
baselines, and goldens byte-unchanged. **The content-addressing gap is closed as a pure core** — a gate's
captured output bytes now hash to a byte-stable `OutputDigest` and assemble into a complete F032 `CommandRecord`
that derives a real, reproducible `EvidenceRef` and folds into the store, mirroring how F047/F049 delivered the
pure halves before their impure edges were wired. The impure gate-execution port (spawning the process, reading
real stdout/stderr, timing, sensing the facts) and the host wiring remain the explicit **following** rows.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` declares both functions,
  so the compiling stub `.fs`, FSI proof, and test scaffolding (`Support.fs`, `Main.fs`) must exist before any
  story test can be written and FAIL.
- **Phase 3 (US2 `digestOf`)**: depends on Phase 2. Built **first** because `recordOf` composes `digestOf` on
  the two output positions.
- **Phase 4 (US1 `recordOf`, MVP)**: depends on Phase 2 **and T011** (`digestOf` body) — `recordOf` is `build`
  with `digestOf` on the two output fields; its `.fs` edit sequences **after T011** (same file
  `ExecutionRecord.fs`).
- **Phase 5 (US3 additive + close-the-loop)**: depends on **T013** (`recordOf` body). No new implementation —
  test-only; its `RecordTests.fs` extension is the same file as T012 (sequence after T012), and
  `CloseLoopTests.fs` drives the real F049/F030 path.
- **Phase 6 (surface/polish)**: last — bless the baseline only after the surface is final (Phase-2 `.fsi`
  unchanged through implementation).

### Within each story

- Each story's test file is written FIRST and must FAIL against the Phase-2 stub, then pass after its
  implementation task lands (T010→T011; T012→T013). US3's T014/T015 assert the contract `recordOf` already
  satisfies once T013 lands.
- The `.fsi` surface precedes the `.fs` body that satisfies it; `Support.fs` (T007) precedes every story test
  file that consumes its builders/generators.
- The two `.fs` bodies are sequential — `digestOf` (T011) precedes `recordOf` (T013) because `recordOf` composes
  it (and both edit `ExecutionRecord.fs`).

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) and T004 `[P]` (CLAUDE.md) are independent of T001 (library
  `.fsproj`); T003 (sln) needs T001 + T002.
- **Phase 2**: T005 (`.fsi`) precedes T006 (stub `.fs`); T007/T008/T009 are `[P]` against each other (distinct
  files — `Support.fs`, `Main.fs`, `scripts/prelude.fsx`) and need the compiling stub (DLL name fixed by T001).
- **Story test files**: T010 (`DigestTests.fs`), T012/T014 (`RecordTests.fs`), T015 (`CloseLoopTests.fs`) are
  `[P]` across distinct files; T012→T014 are sequential (same file). All share `Support.fs` (T007) as a
  prerequisite.
- **Implementation tasks T011→T013 are sequential** — they edit the same `ExecutionRecord.fs`, and `recordOf`
  composes `digestOf`.
- **Phase 6**: T018 `[P]` (git inspection) is independent; T016→T017→T019 are sequential (bless after the
  surface test, validate after the bless).

---

## Implementation Strategy

### MVP scope

The MVP is **User Story 1 (`recordOf` close-the-loop)** — the headline value: a gate's captured output becomes a
complete, reproducible command record ready for F049. Because `recordOf` composes `digestOf`, the minimal
shippable path is **Phase 1 → Phase 2 → Phase 3 (US2 `digestOf`) → Phase 4 (US1 `recordOf`)**. US2 is not
optional polish — it is the content-addressing primitive the MVP is built on (a digest that varied with anything
but content would defeat reuse or mask a changed output). **STOP and VALIDATE** after Phase 4: `recordOf`
assembles a complete record whose `StdoutDigest`/`StderrDigest` are real digests of the raw bytes; the
close-the-loop assertion (`decide inputs (capture inputs (recordOf …) empty) = Reuse (referenceOf (recordOf …))`)
lands in Phase 5.

### Incremental delivery

1. Setup + Foundational → library + tests compile, stubs FAIL.
2. + US2 `digestOf` → deterministic, byte-stable, content-addressed digest (the primitive).
3. + US1 `recordOf` → **MVP**: captured output bytes assemble into a complete F032 record by verbatim delegation.
4. + US3 → proven additive (verbatim `build ∘ digestOf`, failed run recorded) and the chain closed through the
   real F049/F030 path (close-the-loop, perturbation, duration-invariance).
5. + surface governance → Tier-1 baseline blessed, scope-hygiene + additive guarantee pinned.

---

## Task count & summary

- **Total**: 19 tasks (T001–T019).
- **By user story**: US2 (`digestOf`) — 2 (T010 test, T011 impl); US1 (`recordOf`, MVP) — 3 (T012 test, T013
  impl, T015 close-the-loop test); US3 (additive/verbatim delegation) — 1 (T014, test-only). Shared
  infrastructure / cross-cutting — 13 (Setup 4, Foundational 5, surface/polish 4).
- **Parallel opportunities**: T002+T004 (setup); T007+T008+T009 (Phase-2 scaffolding); T010 / T012 / T015 (story
  test files across distinct files); T018 (git inspection in Phase 6).
- **Suggested MVP**: User Story 1 (`recordOf`), reachable only after User Story 2 (`digestOf`) — Phases 1→4.

## Notes

- [P] tasks = different files, no dependencies on another incomplete task in the phase.
- [Story] label maps task to a spec user story for traceability; unlabeled = shared infrastructure.
- The two production bodies are tiny — `digestOf` is a four-step BCL pipeline, `recordOf` is one expression
  (`build ∘ digestOf` on the two output fields, data-model.md) — the engineering weight of this row is the test
  evidence (content agreement/sensitivity, totality, verbatim delegation, close-the-loop, duration-invariance)
  and the Tier-1 surface/additive governance, **not** the implementation.
- Output digests on this path are **derived from real byte buffers**, never `Synthetic` literals — this row
  *removes* the last synthetic-evidence stand-in on the capture path (F049 could only derive references from
  hand-written digests; now the digests are real), so the disclosure discipline is satisfied by **absence** of
  synthetic data on the digest path (plan Constitution Check Principle V).
- Verify tests FAIL before implementing; commit after each task or logical group; never weaken an assertion to
  green a build — narrow scope and document.
</content>
</invoke>
