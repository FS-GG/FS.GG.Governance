---
description: "Task list for Execute Selected Gates In `fsgg route` / `fsgg ship`, Capture Their Evidence, And Persist The Grown Reuse Store (F052)"
---

# Tasks: Execute Selected Gates In `fsgg route` / `fsgg ship` — Capture Evidence And Persist The Grown Reuse Store

**Input**: Design documents from `/specs/052-route-ship-gate-execution/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/Model.fsi ✅, contracts/Plan.fsi ✅, contracts/host-wiring.md ✅, quickstart.md ✅

**Tier**: **Tier 1 (contracted change)** — adds a new public library (`FS.GG.Governance.GateRun` with a surface
baseline), new `Ports.Execute` fields and `ExecuteGates`/`GatesExecuted` cases on both commands, new optional
`execution`-embed parameters on `RouteJson`/`AuditJson`, and the `ShipCommand` verdict-relocation helper — and it
alters observable behavior covered by existing specs (the `fsgg ship` verdict/exit now reflect real gate runs;
`route.json`/`audit.json` carry a per-gate execution embed). Tests are **mandatory** (Principle V). All tasks
share the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No** new third-party dependency, **no**
schema-version bump, **no** edit to any frozen merged core (F023/F024/F030/F032/F041–F051, the F045 embed) or its
golden beyond the route/ship command tests that legitimately recompute their expected documents (FR-017).

**Elmish/MVU**: **Applies and is satisfied by the commands' existing MVU boundary** (Principle IV). Gate execution
is added the **same way** the merged freshness-sense and store-load effects were: I/O is **data** (the injected
`ExecutionPort` in each command's `Ports` record — D4), `update` requests it as a pure `Effect` (`ExecuteGates`)
and never spawns a process itself, interpretation (`senseExecution port`) happens **only at the interpreter edge**,
and a `Msg` (`GatesExecuted`) carries the assembled records back into the pure `update` that folds F049 `capture`,
builds the per-gate `GateOutcome`s, projects the documents, applies the ship verdict relocation, and emits the
persist-grown-store effect. **No new Elmish `Program`** is introduced. `GateRun` itself is pure (argv lex, command
derivation, prior-exit recovery, pass/fail) — pure given the injected port, exactly like the merged
`senseExecution`. Both sides are tested: pure `update` transitions (given `GatesExecuted`, assert the grown store +
projected documents + ship verdict) and the interpreter against **real child processes** (real `/bin/sh`
temp-script fixtures), with the bulk of the semantic tests driven through a **deterministic fake `ExecutionPort`**
over real temp-script fixtures and a **writable temp store** (D8). Principle VI is **live and inherited from
F051**: a missing executable / start failure / timeout is the recorded sentinel outcome surfaced in the summary
and document (never a throw or hang), and store read/persist failures degrade explicitly without losing the
already-computed verdict (FR-011, FR-013, FR-016).

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel. Stories map to
spec user stories — US1 (P1, headline MVP) `fsgg ship` runs its selected gates and the verdict reflects real
results; US2 (P1, co-critical) a `reusable` gate is skipped, closing the cache loop; US3 (P2) `fsgg route` runs and
reports gates while staying advisory; US4 (P2) safe failure and totality when a gate cannot run cleanly; US5 (P3)
deterministic, reproducible evidence and a bounded persisted store. The three genuinely-new pure helpers live in
the new shared `FS.GG.Governance.GateRun` library (Phase 3), which **blocks** all host wiring.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]`…`[US5]` traceability; unlabeled = shared infrastructure
- Exact repo-root-relative file paths in every description

---

## Phase 1: Setup (the new shared library skeleton, no behavior)

**Purpose**: Create the new pure helper library + its focused test project so everything compiles and the solution
restores. No semantics yet. Nothing existing is edited beyond the solution file and the `CLAUDE.md` plan pointer.

- [ ] T001 Create `src/FS.GG.Governance.GateRun/FS.GG.Governance.GateRun.fsproj` — SDK-style, `net10.0`,
  `RootNamespace`/`PackageId` `FS.GG.Governance.GateRun`, `Version` `0.1.0`, `IsPackable=true` (the new-package
  precedent of `GateExecution`/`ExecutionRecord`; "existing packages' pack output unaffected" means the *existing*
  packages are untouched). `<Compile>` order **`Model.fsi`, `Model.fs`, `Plan.fsi`, `Plan.fs`**. `<ProjectReference>`s
  (and only these — plan Primary Dependencies): `../FS.GG.Governance.GateExecution/...` (F051 `GateCommand`),
  `../FS.GG.Governance.CommandRecord/...` (F032 `Executable`/`Argument`/`ExitCode`/identity format),
  `../FS.GG.Governance.EvidenceReuse/...` (F030 `EvidenceRef`), `../FS.GG.Governance.Config/...` (F014
  `CommandSpec`/`ToolingFacts`/`TimeoutLimit`/`EnvironmentClass`), `../FS.GG.Governance.Gates/...` (F018
  `Gate`/`GatePrerequisite`/`CommandId`). **No** third-party `PackageReference` (FR-017). Header comment: the small
  pure helper library the host wiring needs — argv lex, `commandFor`, `priorExitOf`, `passed` — layered on top of the
  merged thread (heavier capabilities layer on top, not into the core); referenced by `RouteCommand`/`ShipCommand`.
- [ ] T002 [P] Create `tests/FS.GG.Governance.GateRun.Tests/FS.GG.Governance.GateRun.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s `Expecto`, `Expecto.FsCheck`, `FsCheck`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no new package).
  `<ProjectReference>`s to the new `FS.GG.Governance.GateRun` **and** — for the `priorExitOf` round-trip and
  `commandFor` fixtures — `FS.GG.Governance.GateExecution` (drive `senseExecution` + a fake port to assemble a real
  record), `FS.GG.Governance.EvidenceCapture` (call the genuine `referenceOf`), `FS.GG.Governance.CommandRecord`
  (`ExitCode`/`canonicalId`), `FS.GG.Governance.EvidenceReuse` (`EvidenceRef`), `FS.GG.Governance.Config`
  (`ToolingFacts`/`CommandSpec`/`TimeoutLimit`/`EnvironmentClass`), `FS.GG.Governance.Gates` (`Gate`/`CommandId`).
  The **production** `GateRun` references none of `EvidenceCapture` (asserted by the Phase 9 scope-hygiene check).
  Final `<Compile>` order is `Support.fs`, `PlanTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` — but each entry is
  added by the task that **creates** its file so the project always compiles; at this step wire **only** `Support.fs`
  (T016) and `Main.fs` (T017). Mirror `tests/FS.GG.Governance.GateExecution.Tests/...Tests.fsproj`.
- [ ] T003 Add both new projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders) with fresh GUIDs
  and the standard Debug/Release `GlobalSection` configuration rows, matching existing entries.
- [ ] T004 [P] Ensure the SPECKIT plan reference in `CLAUDE.md` points at `specs/052-route-ship-gate-execution/plan.md`
  (already updated this session — verify; no other doc changes).

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; `dotnet sln list` shows both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts + compiling stubs + scaffolding) — BLOCKS all stories

**Purpose**: Drop every new/edited public surface (Principle I — contracts before any `.fs` body) and make the whole
solution compile against them with the new behavior **stubbed** and the document embeds **default-empty** (so the
`RouteJson`/`AuditJson` own goldens stay byte-identical — FR-009). Prove the new pure surface in FSI (Principle I)
and stand up the test scaffolding so tests can FAIL before implementation. **⚠️ No story work begins until this
phase is complete.**

- [ ] T005 Author `src/FS.GG.Governance.GateRun/Model.fsi` — drop `contracts/Model.fsi` **verbatim**:
  `namespace FS.GG.Governance.GateRun`; the two `open`s (`FS.GG.Governance.Gates.Model` for `GateId`,
  `FS.GG.Governance.CommandRecord.Model` for `ExitCode`); the `[<CompilationRepresentation(...ModuleSuffix)>] module
  Model` with `GateDisposition` (`Executed`/`Reused`/`NotExecuted`) and `GateOutcome` (`GateId`/`Disposition`/
  `ExitCode option`/`Passed: bool option`), each carrying its curated doc-comment verbatim. Reuses F018/F032 types
  verbatim; introduces **no new** F018/F032 type. **No** access modifiers (Principle II).
- [ ] T006 Add `src/FS.GG.Governance.GateRun/Model.fs` — the `module Model` with `GateDisposition` and `GateOutcome`
  **fully defined** (these are data, not behavior, so no stub). Same two `open`s as the `.fsi`. No access modifiers.
- [ ] T007 Author `src/FS.GG.Governance.GateRun/Plan.fsi` — drop `contracts/Plan.fsi` **verbatim**:
  `namespace FS.GG.Governance.GateRun`; the five `open`s (`CommandRecord.Model`, `EvidenceReuse.Model`,
  `Config.Model`, `Gates.Model`, `GateExecution.Model`); the `module Plan` with the four members —
  `lexCommandLine: string -> (Executable * Argument list) option`, `commandFor: repoRoot:string -> tooling:ToolingFacts
  -> gate:Gate -> GateCommand option`, `priorExitOf: EvidenceRef -> ExitCode option`, `passed: ExitCode -> bool` —
  each with its curated doc-comment verbatim. No access modifiers (Principle II — the argv scanner stays unexported
  by absence here).
- [ ] T008 Add `src/FS.GG.Governance.GateRun/Plan.fs` — the `module Plan` satisfying `Plan.fsi` with the four members
  as `failwith "not implemented"` stubs that type-check the full signatures (real bodies land in Phase 3). No access
  modifiers (Principle II). Confirm `dotnet build src/FS.GG.Governance.GateRun/...` is clean under
  `TreatWarningsAsErrors`.
- [ ] T009 Add the new `ProjectReference`s the host seams require (plan §Scale/Scope "Edited (host seams)") — without
  them the Phase-2 `.fsi`/`.fs` edits cannot resolve `ExecutionPort`/`GateOutcome`/`realPort`. To **both**
  `src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj` and
  `src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj` add `../FS.GG.Governance.GateRun/...` (the
  `GateDisposition`/`GateOutcome`/`Plan` helpers) **and** `../FS.GG.Governance.GateExecution/...` (the `ExecutionPort`
  type for `Ports.Execute` plus `Interpreter.realPort`/`senseExecution` for the edge). To **both**
  `src/FS.GG.Governance.RouteJson/FS.GG.Governance.RouteJson.fsproj` and
  `src/FS.GG.Governance.AuditJson/FS.GG.Governance.AuditJson.fsproj` add `../FS.GG.Governance.GateRun/...` (the
  `GateOutcome` type the new `execution` parameter carries). F051 `GateExecution` landed referenced-by-nothing, so the
  **command** references are genuinely new; `GateRun` (Phase 1) and every merged core are already on graph. **No**
  third-party package added (FR-017); `dotnet restore FS.GG.Governance.sln` stays clean. These four `.fsproj` edits are
  inspected by the Phase-9 scope-hygiene check (T040). Precedes the `.fsi` deltas (T010–T013).
- [ ] T010 [P] Apply the `Interpreter.fsi` host-seam delta (contracts/host-wiring.md §Interpreter.fsi) to **both**
  `src/FS.GG.Governance.RouteCommand/Interpreter.fsi` and `src/FS.GG.Governance.ShipCommand/Interpreter.fsi`: add the
  `Execute: FS.GG.Governance.GateExecution.Model.ExecutionPort` field to the `Ports` record (D4). `run`/`step`/
  `realPorts` signatures are unchanged. No other symbol changes.
- [ ] T011 [P] Apply the `Loop.fsi` host-seam delta (contracts/host-wiring.md §Loop.fsi) to **both**
  `src/FS.GG.Governance.RouteCommand/Loop.fsi` and `src/FS.GG.Governance.ShipCommand/Loop.fsi`: add `ExecuteGates of
  (GateId * GateCommand) list` to `Effect` and `GatesExecuted of (GateId * CommandRecord) list` to `Msg` (D4,
  mirroring the existing `SenseFreshness`/`FreshnessSensed` and `LoadStore`/`StoreLoaded` pairs). `init`/`update`/
  `render`/`parse`/`exitCode` signatures are unchanged.
- [ ] T012 [P] Apply the document-emitter `.fsi` deltas (contracts/host-wiring.md §RouteJson.fsi / §AuditJson.fsi):
  add the trailing optional `execution: (GateId * GateOutcome) list` parameter to
  `src/FS.GG.Governance.RouteJson/RouteJson.fsi` `ofRouteResult` and `src/FS.GG.Governance.AuditJson/AuditJson.fsi`
  `ofShipDecision` (D6 — empty list ⇒ no `execution` embed ⇒ byte-identical output, FR-009).
- [ ] T013 [P] Apply the `ShipCommand` verdict-relocation `.fsi` delta (contracts/host-wiring.md §verdict relocation):
  declare `val applyExecution: passedGateIds: Set<GateId> -> decision: ShipDecision -> ShipDecision` in
  `src/FS.GG.Governance.ShipCommand/Loop.fsi` (it depends on `Ship.Model`/`Enforcement`, so it lives here, NOT in
  `GateRun` — D7), with its curated doc-comment verbatim.
- [ ] T014 Make every edited `.fs` compile against the new signatures (the T009 references now in place) with behavior
  **stubbed and embeds default-empty**: in both commands' `Interpreter.fs` wire `Execute =
  FS.GG.Governance.GateExecution.Interpreter.realPort` in `realPorts` and add a placeholder `ExecuteGates` interpreter
  arm (returns an empty `GatesExecuted []` for now); in both `Loop.fs` add no-op `ExecuteGates`/`GatesExecuted`
  handling (request nothing, fold nothing) so `update` is total; pass `[]` as the new `execution` argument at every
  existing `ofRouteResult`/`ofShipDecision` call site so output is unchanged; implement `applyExecution` in
  `ShipCommand/Loop.fs` as the identity (`fun _ d -> d`) stub. Confirm `dotnet build FS.GG.Governance.sln` is clean and
  the **existing** `RouteCommand`/`ShipCommand`/`RouteJson`/`AuditJson` test suites still pass byte-for-byte (the embed
  is empty, the verdict path unchanged).
- [ ] T015 [P] Append an F052 design-first section to `scripts/prelude.fsx` (after the F051 section) — the
  Principle-I FSI proof **before** any operation body lands (the `quickstart.md` "Exercise the pure helpers in FSI"
  sketch verbatim): `#r` the new `GateRun` Debug DLL plus `GateExecution`/`EvidenceCapture`/`CommandRecord`/
  `EvidenceReuse` DLLs; exercise `Plan.lexCommandLine "dotnet test --no-build"`, `"echo 'hello world'"`, and `"   "`;
  `Plan.commandFor "/repo" tooling gateWithCommand` and `gateWithoutCommand` (⇒ `None`); and `priorExitOf` round-trip
  — `let record = GateExecution.Interpreter.senseExecution fakePort someCommand`,
  `let ref' = EvidenceCapture.EvidenceCapture.referenceOf record`, `Plan.priorExitOf ref'` (`Some …`) and
  `Plan.priorExitOf (EvidenceRef "not-canonical")` (`None`). Its assertions fail against the stubs — expected.
- [ ] T016 [P] Write `tests/FS.GG.Governance.GateRun.Tests/Support.fs` — real, literally-constructible builders
  (Principle V; **no mocks**): (1) a `ToolingFacts`/`CommandSpec` builder so a `CommandId` resolves to a declared
  command line, declared `TimeoutLimit`, and `EnvironmentClass`; (2) a `Gate` builder with and without a
  `RequiresCommand` prerequisite (for `commandFor` ⇒ `Some`/`None`); (3) a deterministic **fake `ExecutionPort`** (a
  literal `ExecutionOutcome` regardless of command) and a helper that assembles a **real** `CommandRecord` via
  `GateExecution.Interpreter.senseExecution fakePort cmd` and a **real** `EvidenceCapture.referenceOf` of it, so the
  `priorExitOf` round-trip reads a genuine canonical-identity string (never a hand-written literal); (4) a
  non-canonical `EvidenceRef` fixture; (5) a `repoRoot` finder for the surface-baseline path; (6) FsCheck generators
  for arbitrary command lines (incl. quotes/escapes/empty) and `ExitCode`s. No network, no governed repository
  (SC-007).
- [ ] T017 [P] Write `tests/FS.GG.Governance.GateRun.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the `GateRun` test project compiles with only
`Support.fs` + `Main.fs` wired; the existing command/emitter suites are byte-unchanged (default-empty embed, stubbed
verdict); `dotnet fsi scripts/prelude.fsx` loads the F052 section (its assertions fail against the `Plan` stubs —
expected). The first failing pure-helper test lands in Phase 3.

---

## Phase 3: GateRun pure helpers (shared infrastructure) — BLOCKS all host wiring

**Goal**: Implement the three genuinely-new pure pieces the wiring needs (and `passed`) so both commands can derive a
gate's command-to-run, recover a reusable gate's prior exit, and map exit→pass/fail. These are consumed by every
host-wiring story (US1–US5); `priorExitOf` in particular is load-bearing for reuse (US2/FR-004).

**Independent Test**: Drive the four `Plan` functions directly — argv lex over quoted/escaped/empty lines;
`commandFor` over a gate with/without a declared command (asserting empty env delta, declared timeout, `repoRoot`
cwd); `priorExitOf` round-tripped against a **real** `referenceOf` of a `senseExecution` record and `None` on a
non-canonical reference; `passed` exit-0-is-pass. No process, no I/O.

### Tests for the pure helpers (write first; must FAIL against the Phase-2 stubs) ⚠️

- [ ] T018 [P] `tests/FS.GG.Governance.GateRun.Tests/PlanTests.fs` — (1) **`lexCommandLine`** (data-model §lex, D1):
  `"dotnet test --no-build"` ⇒ `Some (Executable "dotnet", [Argument "test"; Argument "--no-build"])`; single quotes,
  double quotes, and backslash escapes group/quote (`"echo 'hello world'"` ⇒ one `Argument "hello world"`); argument
  **order** is preserved (identity-significant); an empty / all-whitespace line ⇒ `None`; **no** shell features
  (a `*`, `|`, `$VAR`, `>` are literal token characters, not expanded). An FsCheck round-trip property over generated
  lines. (2) **`commandFor`** (data-model §commandFor): a gate with a `RequiresCommand` resolving to a `CommandSpec`
  ⇒ `Some` with `Executable`/`Arguments` from the lex, `WorkingDirectory = repoRoot`, an **empty** `EnvironmentDelta`
  (`Added=[]; Changed=[]; Removed=[]` — no ambient-env leak, FR-002), `Timeout = CommandSpec.Timeout` verbatim, and
  `CapturedOutput = NoCapturedOutput`; a gate with **no** `RequiresCommand`, an unresolvable `CommandId`, or a command
  line lexing to nothing ⇒ `None` (⇒ `NotExecuted`, FR-005). (3) **`priorExitOf`** (data-model §priorExitOf): a
  **real** `referenceOf (senseExecution fakePort cmd)` round-trips — `priorExitOf ref = Some (the record's ExitCode)`
  for several distinct exit codes (0, non-zero, a sentinel); a non-canonical `EvidenceRef` ⇒ `None` (⇒ recompute,
  never reuse — FR-004, D2). (4) **`passed`**: `ExitCode 0` ⇒ `true`; any non-zero incl. the F051 sentinels ⇒
  `false`. Add `PlanTests.fs` to the test `.fsproj` `<Compile>` immediately after `Support.fs`. All four groups FAIL
  against the Phase-2 `failwith` stubs.

### Implementation for the pure helpers

- [ ] T019 Implement `lexCommandLine` in `src/FS.GG.Governance.GateRun/Plan.fs` — a small explicit single-pass
  character scanner (whitespace separates tokens; `'`/`"`/`\` group/quote; no globbing/expansion/pipes/redirection),
  the first token the `Executable`, the rest the ordered `Argument list`; `None` for an empty/all-whitespace line.
  The scanner's `mutable` index/accumulator are **disclosed and confined** to it (`// mutable: single-pass argv
  scan` — the constitution's sanctioned hot-loop use) and live **unexported** (absent from `Plan.fsi`). No custom
  operators/SRTP/reflection/recursion-for-state (Principle III).
- [ ] T020 Implement `commandFor` and `passed` in `src/FS.GG.Governance.GateRun/Plan.fs` — `commandFor` is a `match`
  on the gate's `RequiresCommand` prerequisite + a `tooling.Commands` lookup + `lexCommandLine`, assembling the
  `GateCommand` per the data-model table (repoRoot cwd, **empty** env delta, declared timeout, `NoCapturedOutput`),
  returning `None` on no-command / unresolved / empty-lex; `passed` is `exitCode = ExitCode 0`. No fabricated
  command, no ambient-env diff, no altered timeout (FR-002). After T019+T020 the lex/commandFor/passed groups of
  T018 go green.
- [ ] T021 Implement `priorExitOf` in `src/FS.GG.Governance.GateRun/Plan.fs` — a `String.split`-and-find over the
  documented F032 canonical-identity format (`exit=1<len>:<value>`, per
  `specs/032-command-records/contracts/command-record-identity-format.md`), returning `Some (ExitCode value)` on a
  canonical reference and `None` on any non-canonical one (the single declared-format read of the otherwise-opaque
  reference — FR-015; recompute-when-unrecoverable — FR-004, D2). After T021 the `priorExitOf` group of T018 goes
  green.

**Checkpoint**: the `GateRun` pure surface is implemented and green; `dotnet fsi scripts/prelude.fsx` F052 section now
passes. The host commands can derive commands, recover prior exits, and map pass/fail. Host wiring (US1–US5) can begin.

---

## Phase 4: User Story 1 — `fsgg ship` runs its selected gates and the verdict reflects real results (Priority: P1) 🎯 MVP

**Goal**: Wire `fsgg ship` to run each selected `mustRecompute` command-gate once through the injected F051 port,
assemble its record, capture its evidence into the store, persist the **grown** store, embed the per-gate execution
outcome in `audit.json`, and — the one verdict change — relocate **passing** command-gates out of the verbatim
`Ship.rollup` `Blockers`/`Warnings` into `Passing` and recompute `Verdict`/`ExitCodeBasis`. This is the headline value
and the highest-risk change (it alters the safety-critical ship verdict/exit), so it is the primary slice. The
`Reused` arm arrives in US2; here every command-gate is `Executed` or `NotExecuted`.

**Independent Test**: Run `fsgg ship` against a fixture repo whose selected gates map to deterministic temp-script
commands (one exits 0, one exits non-zero) through an injected fake `ExecutionPort` with a writable temp store; assert
the non-zero blocking gate is partitioned as a blocker, the ship verdict and exit code reflect it, the clean gate is
relocated to `Passing` and does not block on account of its execution, and each executed gate's evidence is captured
into the persisted grown store.

### Tests for User Story 1 (write first; must FAIL against the Phase-2/3 stubs) ⚠️

- [ ] T022 [P] [US1] Extend `tests/FS.GG.Governance.ShipCommand.Tests/LoopTests.fs` — pure `update` transitions
  (Principle IV's pure side): given the post-cache-eligibility model and a `GatesExecuted` carrying records for a
  blocking gate that exited non-zero and a blocking gate that exited 0, assert `update` (a) folds F049 `capture` for
  each executed gate into the store, (b) builds a `GateOutcome` per selected gate (`Executed`/`NotExecuted`,
  `Some`/`None` exit + pass), (c) emits a `PersistStore` effect for the **grown** store, and (d) `applyExecution`
  relocates the passing gate to `Passing` and recomputes `Verdict`/`ExitCodeBasis` (the failing gate stays a
  `Blocker` ⇒ `Fail`/`Blocked`; the passing gate cleared ⇒ if no blockers remain, `Pass`/`Clean`). FAIL against the
  T014 stubs.
- [ ] T023 [P] [US1] Extend `tests/FS.GG.Governance.ShipCommand.Tests/EndToEndTests.fs` (and `Support.fs` fixtures) —
  drive `Interpreter.run` end-to-end through a **fake `ExecutionPort`** over **real `/bin/sh` temp-script fixtures**
  (one exits 0, one exits non-zero) with a **writable temp store** (US1 acceptance 1–3, SC-001/SC-002): (a) the
  selected blocking gate whose command exits non-zero with no prior evidence is executed once, recorded failed,
  partitioned as a blocker, and the ship verdict is `Fail` with a non-zero exit code; (b) a selected gate whose
  command exits 0 is executed once, recorded clean, and does not become a blocker on account of its execution; (c)
  each executed gate's evidence reference is folded into the reuse store and the grown store is persisted (pruned +
  retained) at the conventional store path (`<repo>/readiness/evidence-reuse.json`). Output digests derive from
  **real captured bytes**, never `Synthetic` literals. FAIL against the stubs.
- [ ] T024 [P] [US1] Extend `tests/FS.GG.Governance.ShipCommand.Tests/ShipInvariantTests.fs` — `applyExecution`
  invariants (data-model §verdict relocation, D3): relocation can only **clear** blockers a passing gate would raise,
  never create one; a **failing** command-gate stays exactly where `Ship.rollup` placed it; a **no-command** gate
  (never in `passedGateIds`) keeps its current treatment (FR-005); findings are never relocated; the recomputed
  `Verdict`/`ExitCodeBasis` come from `Ship`'s **own** one-line rule re-applied (no new severity scheme — FR-006).
  An FsCheck property over arbitrary `passedGateIds`/decisions. FAIL against the identity `applyExecution` stub.

### Implementation for User Story 1

- [ ] T025 [US1] Implement the `ExecuteGates` interpreter arm in `src/FS.GG.Governance.ShipCommand/Interpreter.fs` —
  for each `(GateId, GateCommand)` request, call `GateExecution.Interpreter.senseExecution ports.Execute command`
  **once** (FR-001) and return `GatesExecuted [(gateId, record); …]` in request order. The port is injected (real in
  `realPorts`, fake in tests); the interpreter spawns nothing itself beyond this delegation (Principle IV edge).
- [ ] T026 [US1] Implement the execute/capture/persist wiring in `src/FS.GG.Governance.ShipCommand/Loop.fs` `update`
  (data-model §Host-command flow): after F046 cache eligibility, **classify** each selected gate — `Plan.commandFor
  repoRoot tooling gate = None` ⇒ `NotExecuted`; `Some cmd` with `mustRecompute` ⇒ `Executed` (sent to
  `ExecuteGates`); (the `Reusable` arm is added in US2). On `GatesExecuted`: fold `EvidenceCapture.capture <freshness
  inputs> record` per executed gate into the store (grows it), build the per-gate `GateOutcome` list (`Disposition`,
  `ExitCode = Some record.ExitCode`, `Passed = Some (Plan.passed record.ExitCode)`), and emit the existing persist
  effect over the **grown** store (prune → retain `defaultRetentionBound` → serialise → write — F047/F048 verbatim,
  FR-010). Compose F049/F050/F051 verbatim — dereference no opaque reference, recompute no key/digest, invent no
  record/outcome shape (FR-015).
- [ ] T027 [US1] Implement `applyExecution` in `src/FS.GG.Governance.ShipCommand/Loop.fs` (data-model §verdict
  relocation, D3): `passedGateIds = { o.GateId | o.Passed = Some true }`; `List.partition`/reject those ids out of
  `decision.Blockers`/`decision.Warnings` into `decision.Passing` (relocated, not rebuilt); recompute `Verdict =
  if blockers' empty then Pass else Fail` and `ExitCodeBasis` from `Verdict` — `Ship`'s own rule re-applied. Uses
  `Ship.rollup`/`Enforcement` and every `Ship`/`Enforcement` value **verbatim** (FR-017); constructs only the
  already-public `ShipDecision`/`EnforcedItem`/`Verdict`/`ExitCodeBasis`. Call it after `Ship.rollup` in `update`,
  feed the relocated decision to `AuditJson.ofShipDecision … execution` and to `exitCode`. After T025–T027 + T028 the
  US1 tests go green.
- [ ] T028 [US1] Implement the `execution` embed in `src/FS.GG.Governance.AuditJson/AuditJson.fs` — for each
  selected-gate entry, beside the F045 `cacheEligibility` object and matched by `GateId`, render
  `"execution": { "disposition": "executed"|"reused"|"notExecuted", "exitCode": <int>, "passed": <bool> }` from the
  `execution` parameter; **omit** `exitCode`/`passed` for `notExecuted`; write **no** `execution` object when the
  parameter is empty (the emitter default ⇒ byte-identical to today — FR-009, D6). Every other field unchanged.

**Checkpoint**: `fsgg ship` runs its selected gates, a non-zero/failed gate drives a `Fail` verdict + non-zero exit,
a clean gate is relocated to `Passing`, and each executed gate's evidence is captured into the persisted grown store —
the first time the ship verdict reflects gates actually running. **This is the shippable MVP.** Reuse (US2), route
(US3), safe-failure (US4), and determinism (US5) follow.

---

## Phase 5: User Story 2 — A `reusable` gate is skipped, closing the cache loop (Priority: P1)

**Goal**: Add the `Reused` classification arm so a selected gate the F046 cache marks `reusable`, whose prior exit is
recoverable via `Plan.priorExitOf`, is **not** spawned a second time — its prior recorded outcome is reused for the
evidence it carries and (in ship) the verdict it contributes — while a `reusable` gate whose prior outcome is
**unrecoverable** is conservatively recomputed (FR-004). This closes the cache loop the prior fourteen rows built
toward; it is delivered on the ship path here (the P1 command) and carried to route in US3.

**Independent Test**: Run `fsgg ship` twice against the same fixture repository state with a writable store: the first
run executes the gate and persists its evidence; the second run, with the freshness world unchanged, marks the gate
`reusable`, does **not** spawn it (a call-counting fake port stays at 1), reports it `reused` in `audit.json`, and the
reused outcome contributes to the verdict on the same terms an executed outcome would.

### Tests for User Story 2 (write first; must FAIL against the US1 Executed-only classification) ⚠️

- [ ] T029 [P] [US2] Extend `tests/FS.GG.Governance.ShipCommand.Tests/EndToEndTests.fs` with the **two-run reuse
  demo** (US2 acceptance 1–3, SC-003) over a **call-counting fake `ExecutionPort`** and a writable temp store: run 1
  (empty store) executes the gate (port call count 1), captures evidence, persists the grown store; run 2 (same
  freshness world, store from run 1) marks the gate `reusable`, the port call count **stays 1** (no second spawn), and
  `audit.json` reports `"execution": { disposition:"reused", exitCode:…, passed:… }`; assert the reused gate
  contributes its prior outcome to the verdict identically to an executed one (FR-007), and the command **summary**
  (the `OutputSink` human/JSON output) reports the gate as **executed** on run 1 and **reused** on run 2, consistent
  with the document (FR-016). Also: a gate whose freshness world **changed** since capture is marked `mustRecompute`
  and **is** re-executed (the stale reference is not reused). FAIL against US1's Executed-only path.
- [ ] T030 [P] [US2] Extend `tests/FS.GG.Governance.ShipCommand.Tests/LoopTests.fs` — pure classification transitions:
  given a `Reusable` F046 verdict whose `EvidenceRef` `priorExitOf` recovers `Some exit`, `update` classifies the gate
  `Reused` (NOT in the `ExecuteGates` request) with `GateOutcome.ExitCode = Some exit`, `Passed = Some (passed exit)`;
  given a `Reusable` verdict whose reference `priorExitOf`s to `None`, `update` classifies it `Executed` (recompute —
  FR-004, D2). FAIL against the US1 stub.

### Implementation for User Story 2

- [ ] T031 [US2] Extend the classification in `src/FS.GG.Governance.ShipCommand/Loop.fs` `update` (data-model §Per-gate
  classification): for a gate with `Some cmd` and an `isReusable` F046 verdict, read `Plan.priorExitOf ref` from the
  `Reusable` arm's `EvidenceRef` — `Some exit` ⇒ `Reused` (build its `GateOutcome` from the recovered exit; **not**
  sent to `ExecuteGates`, no spawn — FR-003); `None` ⇒ fall through to `Executed` (recompute-when-unrecoverable —
  FR-004). Only the `Executed` set reaches `ExecuteGates`; `Reused`/`NotExecuted` spawn nothing. After T031 the US2
  tests go green.

**Checkpoint**: the cache loop closes on the ship path — a captured gate is reused (not re-run) on an unchanged second
run, contributing its prior outcome to the verdict, while a stale or unrecoverable reference recomputes. The headline
payoff (work saved on a repeat run) is realized. Route brings the same wiring in US3.

---

## Phase 6: User Story 3 — `fsgg route` runs and reports gates while staying advisory (Priority: P2)

**Goal**: Wire `fsgg route` with the **same** execute/reuse/capture/persist path as ship (Executed + Reused + 
NotExecuted classification, F049 capture, F047/F048 persist of the grown store), embed each selected gate's execution
outcome and executed-vs-reused disposition in `route.json`, but keep route **advisory**: it makes no merge decision
and **always exits 0** regardless of any gate's exit code (FR-008). Every other `route.json` field stays byte-identical
to before this wiring (FR-009).

**Independent Test**: Run `fsgg route` against a fixture repo whose gates map to deterministic temp-script commands
through an injected fake port with a writable store; assert each selected-gate entry in `route.json` carries the gate's
execution outcome (exit code + output digests) and an executed-vs-reused disposition, the grown store is persisted, and
the command exits 0 even when a gate exits non-zero.

### Tests for User Story 3 (write first; must FAIL against the Phase-2 stubs) ⚠️

- [ ] T032 [P] [US3] Extend `tests/FS.GG.Governance.RouteCommand.Tests/EndToEndTests.fs` (and `Support.fs`) — drive
  `Interpreter.run` through a fake `ExecutionPort` over real temp-script fixtures with a writable temp store (US3
  acceptance 1–2, SC-004): each selected-gate entry in `route.json` carries `"execution"` (disposition + exit code +
  output-digest-derived `passed`) and executed-vs-reused; a gate whose command exits non-zero is **reported** but the
  command still **exits 0**; the grown store is persisted (pruned + retained). Reuse the call-counting two-run demo
  (run 1 executes, run 2 reuses, no second spawn). FAIL against the stubs.
- [ ] T033 [P] [US3] Extend `tests/FS.GG.Governance.RouteCommand.Tests/DeterminismTests.fs` (or `CacheInvariantTests.fs`)
  to **recompute** the expected `route.json` live and assert every non-execution field (selected gates, route trace,
  findings, cost rollup, cache section, schema version) is exactly what `fsgg route` produced before this wiring,
  except the new per-gate `execution` embed (US3 acceptance 3, FR-009). FAIL until T034–T035 land.

### Implementation for User Story 3

- [ ] T034 [US3] Implement the `ExecuteGates` interpreter arm in `src/FS.GG.Governance.RouteCommand/Interpreter.fs`
  (mirror T025: `senseExecution ports.Execute` once per request → `GatesExecuted` in order) and the
  execute/classify/capture/persist wiring in `src/FS.GG.Governance.RouteCommand/Loop.fs` `update` (mirror T026 + T031:
  classify Executed/Reused/NotExecuted, fold F049 capture, build `GateOutcome`s, persist the grown store). Route's
  `exitCode` stays **always 0** (FR-008) — no verdict relocation (that is ship-only, D7). Feed the `GateOutcome`s to
  `RouteJson.ofRouteResult … execution`.
- [ ] T035 [US3] Implement the `execution` embed in `src/FS.GG.Governance.RouteJson/RouteJson.fs` — identical shape and
  default-empty rule as `AuditJson` (T028): render `"execution"` beside the F045 `cacheEligibility` per selected-gate
  entry matched by `GateId`, omit `exitCode`/`passed` for `notExecuted`, write nothing when the parameter is empty
  (byte-identical to today — FR-009, D6). After T034 + T035 the US3 tests go green.

**Checkpoint**: `fsgg route` runs and reports each selected gate's execution outcome and executed-vs-reused disposition
in `route.json`, persists the grown store exactly as ship, and still always exits 0 — a faithful preview of what ship
will enforce, without making a merge decision.

---

## Phase 7: User Story 4 — Safe failure and totality when a gate cannot run cleanly (Priority: P2)

**Goal**: Confirm and surface the inherited-from-F051 totality across both commands: a missing executable / start
failure / timeout is the recorded sentinel outcome (never a throw or hang), treated as a **failed** gate in
`fsgg ship`; a gate with **no** declared command is not executed and keeps its treatment; and store read/persist
failures degrade explicitly without losing the already-computed verdict/exit. The execution path itself is total by
construction (F051); this phase adds the **summary surfacing** (FR-016) and the failure-mode assertions.

**Independent Test**: Run each command against fixtures where a gate's command is a missing executable, a script that
sleeps past a short timeout, and a script that exits non-zero; plus an unreadable store and a persist failure; assert
no command crashes, each gate failure is a recorded outcome with the correct sentinel/exit code, the timeout returns
within a bounded time, a no-command gate is skipped, and store failures are surfaced without changing the run's verdict.

### Tests for User Story 4 (write first) ⚠️

- [ ] T036 [P] [US4] Extend `tests/FS.GG.Governance.ShipCommand.Tests/FailureTests.fs` — over real fixtures through the
  port (US4 acceptance 1–3, SC-005/SC-006): a **missing executable** ⇒ recorded `startFailureExitCode` + captured
  diagnostic, no throw, treated as a **failed** (blocking/warning) gate per effective severity; a gate that **overruns**
  its declared timeout ⇒ terminated and recorded (`timeoutExitCode` + partial output + elapsed duration) within a
  **bounded** time, no hang, treated as failed; a gate with **no declared command** ⇒ `NotExecuted`, no evidence
  captured, prior rollup treatment unchanged; and assert the command **summary** names the sentinel outcome
  (start-failure / timeout) consistent with the document (FR-016). Full-fidelity capture of empty / binary-non-UTF-8 /
  large output (FR-012) is inherited verbatim from the F051 port (the F050 digest it applies) — exercise it here only
  where an edge fixture is cheap to add; it needs no new F052 logic. Reach the **real** F051 `realPort` only where an
  edge test needs it (mirroring F051's discipline), the rest through the fake port.
- [ ] T037 [P] [US4] Extend `tests/FS.GG.Governance.ShipCommand.Tests/DegradeTests.fs` and
  `tests/FS.GG.Governance.RouteCommand.Tests/DegradeTests.fs` (and `PersistenceEdgeTests.fs`) — store degradation
  (US4 acceptance 4, SC-007, FR-013): an **absent** store ⇒ empty ⇒ every gate `mustRecompute` (executed), and the
  store ends present-and-populated; an **unreadable** store ⇒ degrades to empty (all executed), read failure surfaced
  honestly, no new crash; a **persist failure** ⇒ surfaced honestly, does not corrupt a partial store, and does **not**
  change the already-computed ship verdict / route exit for the current run.

### Implementation for User Story 4

- [ ] T038 [US4] Surface the execution outcome in each command's human/JSON summary (FR-016) — in
  `src/FS.GG.Governance.ShipCommand/Loop.fs` and `src/FS.GG.Governance.RouteCommand/Loop.fs` `render` (and the
  `OutputSink` path), report which gates were executed vs reused, which passed/failed and how (including a named
  sentinel outcome — `startFailureExitCode`/`timeoutExitCode`), and any store read/persist failure — **consistent with
  the emitted document**. No new control flow for totality (it is the F051 recorded sentinel, inherited verbatim) and
  no change to the already-computed verdict/exit on a store failure (FR-013). After T038 the US4 tests go green.

**Checkpoint**: both commands run arbitrary gate processes safely — a missing/overrunning/failing gate is a recorded
outcome surfaced in the summary and document (no crash, no hang), a no-command gate is skipped, and store failures
degrade honestly without losing the run's verdict. Running gates inside the safety-critical commands is safe.

---

## Phase 8: User Story 5 — Deterministic, reproducible evidence and a bounded persisted store (Priority: P3)

**Goal**: Pin the reproducible-identity and bounded-store properties the merged F050/F049/F047 cores already guarantee,
now that both commands compose them verbatim: two runs of the same deterministic gate over the same world yield a
byte-identical `canonicalId` (measured duration excluded) so the second reuses; the persisted store is pruned + retained
to the bound; and an identical repository state yields a byte-stable store and byte-stable documents apart from each
gate's excluded duration. This phase adds **no new implementation** — only the assertions that pin the property end-to-end.

**Independent Test**: Execute the same deterministic gate twice through a command; assert the two records share a
byte-identical `canonicalId` (so the second reuses), that perturbing any reproducible fact changes it while a
duration-only difference does not, and that the persisted store is pruned and retained to the bound.

### Tests for User Story 5 (write first; assert the property the wiring already satisfies) ⚠️

- [ ] T039 [P] [US5] Extend `tests/FS.GG.Governance.ShipCommand.Tests/DeterminismTests.fs` and
  `tests/FS.GG.Governance.RouteCommand.Tests/DeterminismTests.fs` (US5 acceptance 1–3, SC-008): two runs of the same
  deterministic gate over the same world produce a byte-identical `canonicalId` (and `referenceOf`) despite differing
  measured durations, and the second run **reuses** the first's evidence; the persisted store is deterministic and
  **bounded** (a captured store exceeding `defaultRetentionBound` is pruned of superseded entries and retained to the
  cap); a fixed repository state yields a byte-stable persisted store and byte-stable `route.json`/`audit.json` apart
  from each gate's excluded measured duration. Composes F050/F049/F047 verbatim — no new identity/digest/severity
  scheme.

**Checkpoint**: US1–US5 — both commands run, reuse, capture, persist, and (ship) enforce real gate results; the
reproducible identity and bounded store are pinned; the cache loop closes deterministically across runs.

---

## Phase 9: Surface governance & polish (Tier-1 baselines, scope hygiene, FR-009 byte-identity, validation)

**Purpose**: Lock the new public surface and re-bless only the additive baseline deltas (Principle II / Change
Classification), prove the change is additive and edits no frozen core (FR-017, SC-009), and run the quickstart
end-to-end. Bless baselines only after the surfaces are final.

- [ ] T040 [P] `tests/FS.GG.Governance.GateRun.Tests/SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the
  `GateExecution`/`Snapshot` precedent) comparing the public surface of the production `FS.GG.Governance.GateRun`
  assembly byte-for-byte to `surface/FS.GG.Governance.GateRun.surface.txt` with the `BLESS_SURFACE=1` re-bless path
  (reflection lives ONLY in this test); plus a **scope-hygiene** assertion that the **production** `GateRun` assembly
  references **only** `GateExecution`, `CommandRecord`, `EvidenceReuse`, `Config`, `Gates` (+ transitive F032/F014 +
  `FSharp.Core`/BCL) and **not** `EvidenceCapture`, `RouteJson`, `AuditJson`, `Enforcement`, `Ship`, `RouteCommand`,
  `ShipCommand`, or any third-party package (the test project's `EvidenceCapture` ref is deliberately excluded — it
  inspects the production assembly). This is the check that catches a stray T009 reference. Add `SurfaceDriftTests.fs`
  to the test `.fsproj` `<Compile>` immediately after `PlanTests.fs` and before `Main.fs`.
- [ ] T041 Generate and commit `surface/FS.GG.Governance.GateRun.surface.txt` via `BLESS_SURFACE=1 dotnet test
  tests/FS.GG.Governance.GateRun.Tests/...`; review the diff (exactly `Model` — `GateDisposition`, `GateOutcome` — and
  `Plan` — `lexCommandLine`, `commandFor`, `priorExitOf`, `passed`; **no** argv-scanner leak). After this T040 runs
  green without `BLESS_SURFACE`.
- [ ] T042 Re-bless the **additive** baseline deltas for the edited surfaces via `BLESS_SURFACE=1`:
  `surface/FS.GG.Governance.RouteCommand.surface.txt` and `surface/FS.GG.Governance.ShipCommand.surface.txt` (the new
  `Ports.Execute` field, the `ExecuteGates`/`GatesExecuted` cases, and — ship — `applyExecution`), and
  `surface/FS.GG.Governance.RouteJson.surface.txt` / `surface/FS.GG.Governance.AuditJson.surface.txt` (the new optional
  `execution` parameter). Review each diff to confirm it is **only** the additive delta — no removed/renamed symbol.
- [ ] T043 [P] Verify FR-009 / SC-009 (additive documents, no frozen-core edit) by inspection: `git diff` shows **no**
  edit to any merged pure core (`FS.GG.Governance.Enforcement`, `Ship`, `GateExecution`, `ExecutionRecord`,
  `EvidenceCapture`, `EvidenceReuseStore`, `FreshnessSensing`, `CacheEligibility`, `CommandRecord`, `EvidenceReuse`),
  the F045 cache embed, or the `fsgg.evidence-reuse-store/v1` / `route.json` / `audit.json` schema (no `schemaVersion`
  bump, no new third-party dependency); the `RouteJson`/`AuditJson` **own** goldens stay byte-stable (the embed defaults
  to empty); the only document changes are the new per-gate `execution` embed and the ship verdict changes that follow
  from real pass/fail. The route/ship command tests legitimately recompute their expected documents (do **not** run
  `BLESS_FIXTURES=1` for any frozen golden).
- [ ] T044 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`; `dotnet fsi
  scripts/prelude.fsx` (the F052 section: argv lex, `commandFor` `Some`/`None`, `priorExitOf` round-trip `Some` and
  non-canonical `None`); `dotnet test FS.GG.Governance.sln` — all projects green under `TreatWarningsAsErrors`,
  including the `GateRun` pure-helper tests, the ship two-run reuse + verdict tests, the route advisory + embed tests,
  the safe-failure/totality tests, the determinism/bounded-store tests, and all surface-drift tests. Fix any drift.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-009 covered; the new `GateRun` surface is blessed
and scope-clean, the additive `RouteCommand`/`ShipCommand`/`RouteJson`/`AuditJson` baselines re-blessed, every frozen
core/golden/schema byte-unchanged. The evidence-reuse loop closes end-to-end: `fsgg route` and `fsgg ship` run each
selected must-recompute gate through the F051 port, capture its evidence, persist the grown store, reuse a `reusable`
gate without re-running it, and — in `fsgg ship` — let real pass/fail drive the verdict and exit code.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — adds the host-seam `ProjectReference`s (T009), drops every `.fsi`, and
  makes the solution compile with stubs + default-empty embeds. **BLOCKS all later phases.**
- **GateRun pure helpers (Phase 3)**: Depends on Foundational — implements the shared `Plan` functions. **BLOCKS all
  host-wiring stories (Phases 4–8)** — they consume `commandFor`/`priorExitOf`/`passed`.
- **US1 (Phase 4, P1, MVP)**: Depends on Phase 3 — the ship execution + verdict slice. Independently shippable.
- **US2 (Phase 5, P1)**: Depends on US1 — adds the `Reused` arm to the ship classification US1 established.
- **US3 (Phase 6, P2)**: Depends on Phase 3 — route wiring mirrors the US1/US2 ship path (incl. reuse) but advisory.
  Independent of US1/US2 at the file level (different command), so it **may** proceed in parallel with US2 once Phase 3
  lands, but the shared classification/capture design is most cheaply lifted from US1+US2 first.
- **US4 (Phase 7, P2)**: Depends on US1 (ship) + US3 (route) execution paths — adds summary surfacing + failure-mode
  and store-degradation assertions across both commands.
- **US5 (Phase 8, P3)**: Depends on US1 + US3 — pins determinism/bounded-store across both commands (no new impl).
- **Polish (Phase 9)**: Depends on all desired stories — surface baselines, scope hygiene, FR-009 verification, and
  quickstart validation are blessed last, once the surfaces are final.

### Within Each Story

- Tests are written FIRST and must FAIL before implementation (Principle V).
- The host-seam `ProjectReference`s (T009) precede the `.fsi` deltas that reference the new types.
- The `.fsi` contracts (Phase 2) precede every `.fs` body (Principle I).
- The shared pure helpers (Phase 3) precede every host-wiring body.
- Interpreter edge (`ExecuteGates`) and pure `update` (classify/capture/persist) before the document/verdict
  projection; document embed implementations before the byte-identity assertions.

### Parallel Opportunities

- Setup tasks `T002`/`T004` run in parallel with `T001`/`T003`.
- Foundational: `T009` (the four `.fsproj` references) is the first Phase-2 task; the `.fsi` deltas `T010`–`T013` are
  then different files → parallel; `T015`–`T017` (prelude + test scaffolding) parallel with each other and with the
  `.fsi` work; `T014` (compile-with-stubs) is the join point.
- Within each story, the test tasks marked `[P]` (different files) run in parallel and before the implementation tasks.
- US3 (route, Phase 6) is a different command from US1/US2 (ship) and **may** be developed in parallel once Phase 3
  lands, if staffed — its tests/impl touch only `RouteCommand`/`RouteJson` files.
- Polish `T040`/`T043` are parallel-safe (different files / read-only inspection); `T041`/`T042` (bless) are sequential
  after the surfaces are final.

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → Phase 2 Foundational (host-seam references + `.fsi` + compiling stubs + default-empty embeds —
   existing suites stay green).
2. Phase 3 GateRun pure helpers (the shared `commandFor`/`priorExitOf`/`passed`).
3. Phase 4 US1 — `fsgg ship` runs its selected gates, captures + persists the grown store, and real pass/fail drives
   the verdict/exit via the relocation.
4. **STOP and VALIDATE**: the ship verdict reflects real gate results; evidence is captured and persisted.

### Incremental Delivery

1. Setup + Foundational + GateRun helpers → foundation ready.
2. Add US1 (ship execute + verdict) → validate → **MVP**.
3. Add US2 (reuse loop on ship) → validate → the cache payoff is realized.
4. Add US3 (route advisory execute + report) → validate.
5. Add US4 (safe failure + totality + store degradation) → validate.
6. Add US5 (determinism + bounded store) → validate.
7. Phase 9 polish → surfaces blessed, additivity proven, quickstart green.

### Suggested MVP Scope

**User Story 1** (Phase 4) over the Phase 1–3 foundation — `fsgg ship` running its selected must-recompute gates and
the verdict/exit reflecting real results, with each executed gate's evidence captured into the persisted grown store.
It is independently testable, the highest-value and highest-risk slice, and a viable standalone increment.

---

## Notes

- `[P]` tasks = different files, no dependency on another incomplete task in this phase.
- `[Story]` labels map tasks to spec user stories (US1–US5); unlabeled tasks are shared infrastructure.
- Verify tests FAIL before implementing; never mark a failing task `[X]`; never weaken an assertion to green a build —
  narrow scope and document.
- Output digests in every assertion derive from **real captured bytes** (`ExecutionRecord.digestOf` / a real
  `senseExecution` record) — never `Synthetic`/`OutputDigest` literals (Principle V).
- The fake `ExecutionPort` is a **deterministic double over real `byte[]`** (the real `realPort` is also exercised at
  the edge tests), not a stand-in for unavailable evidence.
- FR-012 (full-fidelity capture of empty / binary / large output) is inherited verbatim from the F051 port's F050
  digest; this row adds no F052 capture logic, so it is exercised opportunistically in T036 rather than via a dedicated
  task.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
</content>
