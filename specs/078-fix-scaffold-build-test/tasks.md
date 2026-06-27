---
description: "Task list for feature 078 — bound the scaffold real-evidence build test"
---

# Tasks: Bound the scaffold real-evidence build test so the suite never hangs and the routine run stays fast

**Input**: Design documents from `/specs/078-fix-scaffold-build-test/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md (D1–D10) ✅, data-model.md ✅, contracts/test-harness-contract.md ✅, quickstart.md ✅

**Tests**: This feature *is* a test/test-support change — the deliverables are test-support
primitives and the tests that exercise them. Test tasks are therefore first-class (not
optional). Every cut-off/not-run outcome must be a *named* skip (Principle VI).

**Tier**: Whole feature is **Tier 2** (internal/test-support; no `.fsi`, no surface
baseline, no production behavior, no golden re-bless). Per-task tier annotations are
omitted because every task matches the feature tier.

**Scope guardrails (apply to every task)**:

- Touch ONLY `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/` (`Support.fs`,
  the *build* test in `WorkedExampleTests.fs`, the new `BoundedBuildTests.fs`, and the
  `.fsproj`). No production code, no `.fsi`, no surface baseline, no golden (FR-008, D8).
- The two non-build worked-example tests stay **byte-identical**; the committed golden
  `fixtures/sdd-reference/scaffold-manifest.golden.json` is NOT regenerated (FR-006/FR-008).
- Commit one concern at a time.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the same phase.
- **[Story]**: US1 / US2 / US3 traceability.

---

## Phase 1: Setup

**Purpose**: Register the new test module and capture the pre-change baseline.

- [X] T001 Add `<Compile Include="BoundedBuildTests.fs" />` to
  `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests.fsproj`,
  ordered **after** `WorkedExampleTests.fs` and **before** `Main.fs` (so the new module
  sees `Support` and Expecto discovery is unchanged). Create an empty placeholder
  `BoundedBuildTests.fs` with the module header
  `module FS.GG.Governance.Sample.SddReferenceProvider.Tests.BoundedBuildTests` so the
  solution still compiles. (D8, D9)
- [X] T002 [P] Record the baseline: `git diff --stat fixtures/sdd-reference/scaffold-manifest.golden.json surface/`
  is empty now (must stay empty at the end — FR-008/SC-005), and note the current
  worked-example project test count for the additive `+1` check in T015.
  **Done.** Baseline diff empty (confirmed). Baseline project test count = **10**
  (WorkedExample 3, FailurePath 6, SurfaceDrift 1) → after this feature **11** (the additive
  `+1` forced-stall test).

**Checkpoint**: Solution compiles with an empty new module; baseline recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared data-model values every story's build path depends on. `runBounded`
(US1) returns a `BuildAttempt` and consumes `buildBudget`, so both must exist first.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T003 [US1][US3] Extend the `BuildAttempt` DU in
  `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/Support.fs` with the new third
  case `TimedOut of budget: TimeSpan * partialOutput: string` (keep `Built` and `SdkMissing`
  unchanged). The three cases stay total and mutually exclusive so a timeout can never
  silently become a pass. (data-model §1, D3)
- [X] T004 [US1] Add the finite budget **and the two named margin constants** to `Support.fs`:
  - `let buildBudget : TimeSpan` — default `TimeSpan.FromSeconds 120.`, overridable via env
    `FSGG_BUILD_BUDGET_SECONDS` (parsed to seconds; absent/non-numeric/≤0 ⇒ the 120 s default —
    a malformed override NEVER yields an unbounded wait, FR-001/FR-010).
  - `let killDrainMargin : TimeSpan` — `TimeSpan.FromSeconds 5.`; the bounded post-`Kill` drain
    wait in `runBounded` (T006) so reading the partial output after a tree-kill cannot itself
    block.
  - `let boundAssertionMargin : TimeSpan` — `TimeSpan.FromSeconds 2.`; the **assertion
    tolerance** the forced-stall test (T005) allows over the budget when checking the bound.

  Naming both margins as constants keeps the two distinct "small margin" notions unambiguous and
  the timing assertion reproducible (FR-010; resolves the previously-undefined margin). (data-model §2, D5)

**Checkpoint**: `Support.fs` exposes the new outcome case and the finite, enforced budget.

---

## Phase 3: User Story 1 — The suite never hangs on the scaffold build (Priority: P1) 🎯 MVP

**Goal**: The real-evidence build step is bounded — it completes within budget or is cut
off (whole process tree killed) and reported as a named timeout skip; it never waits
indefinitely, and a nested build does not exhaust process/handle resources.

**Independent Test**: Run `BoundedBuildTests.fs` — a real sleeper under a sub-second budget
is cut off as `TimedOut`, the call returns within `budget + margin`, and the spawned process
is gone (`dotnet test …SddReferenceProvider.Tests` completes in ~1–2 s for this test).

### Tests for User Story 1 (write FIRST — must FAIL before T006/T007 exist)

- [X] T005 [US1] Implement the forced-stall test in
  `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/BoundedBuildTests.fs`: a
  `[<Tests>] testList "BoundedBuild"` with `test "bounded: a stalled build is cut off within
  budget+margin"`. It runs `Support.runBounded` against a real OS-selected **sleeper**
  (`sleep <n>` on Unix; `ping -n <n> 127.0.0.1` on Windows — a redirect-safe wait, unlike
  `timeout`, which throws when stdin is redirected) under a **`TimeSpan.FromMilliseconds 500`**
  budget and asserts via `System.Diagnostics.Stopwatch`: (a) the outcome is `TimedOut <budget>`,
  (b) the call returned within `budget + Support.boundAssertionMargin`, (c) the spawned process
  is no longer alive. On a platform with no sleeper, emit a named `skiptest "PLATFORM: no
  sleeper available to force a stall"` — never a silent green.
  **Fail-before/pass-after (Principle V) — exact red state:** this test calls
  `Support.runBounded`, which T006 introduces, so *before T006 the test does not compile* (the
  symbol is absent) — that is the genuine red; do **not** describe it as "the unbounded runner
  would hang" (it never invokes the old runner). To obtain a *behavioral* fail-before, land
  `runBounded` first as a throwaway stub returning `Built(0, "")` (no bounded wait, no kill): the
  `TimedOut` and `budget + margin` assertions then go red, and completing the real T006 body
  greens them. (D9, quickstart Scenario 2, contract §"bounded" test)

### Implementation for User Story 1

- [X] T006 [US1] Add `let runBounded (exe: string) (args: string) (workingDir: string option)
  (budget: TimeSpan) : BuildAttempt` to `Support.fs`. It MUST: redirect stdout+stderr with
  `UseShellExecute=false`; attach `OutputDataReceived`/`ErrorDataReceived` handlers that append
  to two `StringBuilder`s and call `BeginOutputReadLine`/`BeginErrorReadLine` AFTER `Start`
  (async capture defeats the `ReadToEnd()` deadlock — D1); wait with `WaitForExit(int
  milliseconds)`; on overrun call `proc.Kill(entireProcessTree = true)` then
  `WaitForExit(int killDrainMargin.TotalMilliseconds)` (the named drain margin from T004) and
  return `TimedOut(budget, <partial output>)` (D2); on a
  start failure (`Process.Start` null / `Win32Exception` / `InvalidOperationException`) return
  `SdkMissing <detail>` (preserves FR-004); on success call the no-arg `WaitForExit()` once to
  flush in-flight async output, then return `Built(ExitCode, stdout+stderr)`. The
  `mutable`/`StringBuilder` accumulation is disclosed at the use site (Principle III).
  (data-model §4, D1/D2/D7) — **run T005 now; it must pass.**
  **Signature deviation (recorded, flagged):** `runBounded` gained a fifth parameter
  `onStarted : int -> unit`, invoked synchronously with the spawned PID right after `Start`
  (`dotnetBuild` passes `ignore`). T005's assertion (c) was first a scan of the OS process
  table by NAME, which proved non-deterministic under the parallel `dotnet test <solution>`
  run (an unrelated concurrent `sleep` is misread as a surviving orphan — observed once in the
  full-suite run). Checking the EXACT spawned PID is the only race-free death check, so the
  bound test now captures the PID via `onStarted` and asserts that specific process is gone.
  This strengthens FR-010 and is confined to test-support. (data-model §4)
- [X] T007 [US1] Rewrite `dotnetBuild (slnPath: string) : BuildAttempt` in `Support.fs` as a
  thin wrapper over `runBounded`: `runBounded "dotnet" (sprintf "build \"%s\"
  -maxcpucount:1 --disable-build-servers" slnPath) (Some <slnDir>) buildBudget`. The
  `-maxcpucount:1 --disable-build-servers` flags collapse the MSBuild worker fan-out and stop
  persistent build-server processes, killing the nested resource-contention pathology
  (FR-007, US1 acceptance #3). Delete the old inline `ReadToEnd()`/`WaitForExit()` body.
  (data-model §4, D6/D7)

**Checkpoint**: The bound is real and proven (T005 green); a stalled build cannot hang the
suite and a nested build no longer fans out workers. US1 is independently shippable.

---

## Phase 4: User Story 2 — The routine run stays fast (Priority: P2)

**Goal**: The heavyweight real `dotnet build` is gated behind an explicit opt-in so the
default run skips it with a named rationale (no scaffold, no build) and completes < 30 s;
the two non-build sibling tests still run on every default run.

**Independent Test**: Default run of the project completes < 30 s with the build test a
named opt-out skip; `FSGG_REAL_EVIDENCE=1` run actually executes the build.

**Depends on**: Phase 3 (US1) — the build it gates must already be bounded.

### Implementation for User Story 2

- [X] T008 [US2] Add the run-configuration gate to `Support.fs`:
  `let realEvidenceEnabled : unit -> bool` — true iff `FSGG_REAL_EVIDENCE` equals exactly `1`,
  **OR** `CI` is *truthy*, where truthy means **set and, trimmed + case-insensitive, NOT one of
  `""` / `0` / `false`** (so GitHub Actions' `CI=true` and a bare `CI=1` both enable; `CI=0` /
  `CI=false` / unset do not) — the canonical CI full-evidence path, FR-005 — and
  `let realEvidenceSkipReason : string =
  "REAL-EVIDENCE OPT-OUT: set FSGG_REAL_EVIDENCE=1 (or run under CI) to exercise the real
  dotnet build"`. Keep the env read in this one place so the gate is testable. (data-model
  §3, D4)
- [X] T009 [US2] Gate the build test
  `"emitted skeleton \`dotnet build\`s first-attempt (real evidence)"` in
  `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/WorkedExampleTests.fs`: when
  `realEvidenceEnabled ()` is false, `skiptest realEvidenceSkipReason` and return
  immediately — do NOT scaffold and do NOT build (keeps the default project run fast, SC-002,
  D4/D10). Only when enabled does it run the worked example + `dotnetBuild`. Edit ONLY this
  one test block; the two sibling tests stay byte-identical (FR-006/FR-008). (quickstart
  Scenario 1)

**Checkpoint**: Default run is fast (< 30 s) and the skipped build is named, never silently
absent; the opt-in lane still runs the real build. US1 + US2 both hold.

---

## Phase 5: User Story 3 — The real-evidence guarantee is preserved (Priority: P3)

**Goal**: When the build runs, the outcome resolves to exactly one of four observable
results via a total, ordered match — a correct skeleton passes first-attempt, a non-zero
build still FAILS (never absorbed by a skip), the missing-SDK skip stays distinct from the
timeout skip.

**Independent Test**: With the SDK present and opt-in active, a correct skeleton passes
(exit 0) and a broken one fails; absent SDK ⇒ the named missing-SDK skip, distinct from the
timeout and opt-out skips.

**Depends on**: Phase 4 (US2) — it extends the same gated build-test block.

### Implementation for User Story 3

- [X] T010 [US3] Complete the build test's match in `WorkedExampleTests.fs` (inside the
  enabled branch from T009) into a total, ordered classification over `BuildAttempt`:
  `SdkMissing detail` ⇒ `skiptestf "PREREQUISITE: .NET SDK not available to build the emitted
  skeleton (%s)" detail`; `TimedOut(budget, partial)` ⇒ `skiptestf "BUDGET EXCEEDED: dotnet
  build exceeded %O; partial output: %s" budget partial` (a NEW named skip, textually distinct
  from the missing-SDK and opt-out messages, FR-002/FR-009); `Built(exitCode, output)` ⇒
  `Expect.equal exitCode 0 (sprintf "dotnet build MyApp.sln must succeed first-attempt:\n%s"
  output)`. A non-zero `Built` is NEVER rewritten to a skip (FR-003/SC-003). The four skip/
  pass/fail outcomes are mutually distinguishable. (data-model §1/§5, D3/D10, contract
  Outcomes table)

**Checkpoint**: All three skip reasons are distinct and named; a genuine compile failure
still fails. All user stories independently functional.

---

## Phase 6: Polish & Validation (Cross-Cutting)

**Purpose**: Prove the success criteria end-to-end and confirm zero drift.

- [X] T011 [P] Quickstart Scenario 1 (US2/SC-002, SC-001): run
  `dotnet test tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/...fsproj` in the
  default config — completes < 30 s, the two non-build tests pass, the build test is a named
  opt-out skip, the bound test passes, the run terminates.
- [X] T012 [P] Quickstart Scenario 3 (FR-005/SC-003): run the same project **twice** to cover
  **both** real-evidence triggers of the T008 gate — (a) `FSGG_REAL_EVIDENCE=1` (explicit local
  opt-in) and (b) `CI=true` with `FSGG_REAL_EVIDENCE` unset (the canonical CI authoritative path,
  the `CI`-truthy half of the gate). In each, the real `dotnet build` executes with the fan-out
  flags and a correct skeleton passes first-attempt; confirm it stays within budget (no
  indefinite wait). Covering the `CI` branch — not just `FSGG_REAL_EVIDENCE` — closes FR-005's
  "MUST still run … in the CI configuration" guarantee.
- [X] T013 [P] Quickstart Scenarios 4 & 5 (FR-003/FR-004/SC-003): confirm a deliberately
  broken skeleton FAILS with captured output (not absorbed by a skip), and that a missing
  `dotnet` yields the named missing-SDK skip distinct from the timeout/opt-out skips.
- [X] T014 [P] Quickstart Scenario 6 (FR-008/SC-005): `git diff --stat
  fixtures/sdd-reference/scaffold-manifest.golden.json surface/` is **empty**; the project's
  own `SurfaceDriftTests` stays green (core baselines byte-identical).
- [X] T015 Whole-suite guarantee (SC-001/SC-005): `dotnet test FS.GG.Governance.sln`
  terminates with no scaffold-build hang; per-project counts unchanged except the additive
  `+1` forced-stall test (vs the T002 baseline) and the build test's config-dependent
  skip/pass.
  **Done.** Full solution run terminated (exit 0), **0 failures** across all 13 test
  projects; SddReferenceProvider.Tests = 10 passed / 1 skipped (the named build opt-out) /
  0 failed = 11 total (baseline 10 → additive `+1` forced-stall test). The first full-suite
  run surfaced a flaky `BoundedBuild` (c) name-scan under parallel load; fixed via the
  exact-PID death check (see T006 deviation note) and re-confirmed green under contention
  (8 concurrent unrelated sleepers) and in a second full-solution run.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: after Setup — BLOCKS all user stories (`runBounded` returns
  `BuildAttempt` and consumes `buildBudget`).
- **US1 (Phase 3)**: after Foundational. MVP — independently shippable; bounding alone
  removes the hang.
- **US2 (Phase 4)**: after US1 (gates the now-bounded build).
- **US3 (Phase 5)**: after US2 (extends the same gated build-test block in
  `WorkedExampleTests.fs`).
- **Polish (Phase 6)**: after the stories whose criteria each task validates.

### File-level serialization (within/across phases)

- `Support.fs` is edited by T003, T004 (foundational) then T006, T007 (US1) then T008
  (US2) — same file, so these are **sequential**, not `[P]`.
- The build-test block in `WorkedExampleTests.fs` is edited by T009 (US2) then T010 (US3) —
  same block, **sequential**.
- `BoundedBuildTests.fs` (T005) is independent of `WorkedExampleTests.fs` and `Support.fs`
  edits once T006 exists.

### Parallel Opportunities

- T002 is `[P]` (read-only baseline) alongside T001.
- The Phase 6 validation tasks T011–T014 are `[P]` (independent runs / read-only diffs);
  T015 runs last as the whole-suite gate.

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (`BuildAttempt.TimedOut` + `buildBudget`).
2. Phase 3: write T005 (fails), land T006 `runBounded` (T005 passes), land T007 fan-out
   flags.
3. **STOP and VALIDATE**: the suite can no longer hang on the scaffold build — ship.

### Incremental Delivery

1. US1 → bound proven, suite always terminates (MVP).
2. US2 → default run fast, build gated behind a named opt-in.
3. US3 → distinct named skips + preserved real-evidence pass/fail.
4. Polish → quickstart + whole-suite + zero-drift confirmation.

---

## Notes

- `[P]` = different files / read-only, no dependency on an incomplete task in the phase.
- One concern per commit; never mark a failing task `[X]`; never weaken an assertion to
  green a build — narrow scope and document it.
- Elmish/MVU applicability: N/A — this is a single bounded subprocess call at the test edge
  (a pure I/O helper), not a stateful multi-step workflow; the production scaffold MVU seam
  (`Interpreter.run`) is untouched (plan Constitution Check, Principle IV).
- Real evidence (Principle V) is on both sides: a real process tree really killed within
  budget (T005), and the real `dotnet build` under opt-in/CI (T012).
