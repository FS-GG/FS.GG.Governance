# Tasks: Fix the Slow Governance Solution Build

**Input**: Design documents from `specs/080-fix-slow-solution-build/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/build-command.md](./contracts/build-command.md), [quickstart.md](./quickstart.md)

**Tier**: Tier 2 (build-infra / performance). No `.fsi`, no surface-area baseline, no new
dependency, no `.sln` membership change, compiled outputs functionally equivalent
(FR-003). Tasks that would touch any of those are explicitly forbidden below.

**Tests**: This feature's oracle is **wall-clock measurement** of the documented commands
(NFR-001) plus one small checked-in guard over the wrapper. No product `.fs`/`.fsi` is
added, so there are no Elmish/MVU transition tests — **Principle IV is not applicable**
(the wrapper is a single pure shell-out, justified in plan Constitution Check).

## Format: `[ID] [P?] [Story] Description`

- `[P]` = no dependency on another incomplete task in this phase (different file / no
  ordering need).
- `[US1]`/`[US2]`/`[US3]` = the user story the task serves.
- Phases run in sequence; tasks within a phase may run in parallel where marked `[P]`.
- Status: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on line).

---

## Phase 1: Setup & Baseline Capture (Shared)

**Purpose**: Pin the "before" numbers so the improvement is demonstrable (NFR-001), and
fix the constants the wrapper will encode. No code yet.

- [X] T001 [P] (research.md D8) Re-confirmed on this machine: 24 cores / ~61 GB / SDK
  10.0.301; bounded build 33 s / 162 built / 0 resource errors; no-op 3.4 s. Record the **baseline** in `specs/080-fix-slow-solution-build/research.md`
  (already captured: unbounded full `dotnet build FS.GG.Governance.sln` did NOT finish in
  10 min, 149 `MSB6003`/`MSB6006` errors; `-m:6` → 33 s / 0 errors / 162 built; no-op
  3.3 s). Re-confirm on the implementer's machine with `nproc` + a single timed run of the
  bounded build, and append the machine's `nproc`/RAM + measured numbers as the local
  before/after anchor.
- [X] T002 [P] Confirm the **mechanism constraints** still hold on the current SDK (guards
  against silently picking a broken route later): verify (a) a bogus flag in a temp
  `Directory.Build.rsp` is read (`MSB1001`) yet (b) `-m:6` placed there does NOT bound the
  build, while (c) `-m:6` on the command line does. Delete the temp `Directory.Build.rsp`
  immediately after (it must never be committed). Evidence note in research.md D3.
  **Done (research.md D8):** (a) bogus rsp → `MSB1001 ... came from .../Directory.Build.rsp`
  (rsp honored); temp file deleted immediately, never committed. (c) command-line `-m:6`
  confirmed positively by the 33 s bounded build. (b) re-triggering the full unbounded
  thrash (the >10-min EAGAIN/SIGABRT failure already captured in D1) was deliberately NOT
  repeated — it is the exact failure this feature removes, not a safe step to reproduce;
  D3's prior evidence stands and is cited (disclosed).

**Checkpoint**: Baseline numbers recorded; the "command-line `-m:N` is the only reliable
bound" decision re-validated on this SDK.

---

## Phase 2: Foundational — the checked-in build wrapper (Blocking)

**Purpose**: The single artifact every user story depends on: a checked-in wrapper that
always emits an explicit, bounded, hardware-derived `-m:N` on the MSBuild command line.
**Blocks US1, US2, US3.**

- [X] T003 Create **`build.fsx`** at the repository root (run as `dotnet fsi build.fsx`).
  It MUST:
  - detect logical core count (`System.Environment.ProcessorCount`);
  - compute a **bounded** node count `N` per research D4 — start with
    `N = max(2, min(12, int (ceil (float cores / 4.0))))` (→ 6 on a 24-core host, the
    proven anchor); never unbounded;
  - accept a verb arg `build` (default) | `test`, an optional configuration
    (`Debug` default | `Release`), and forward any trailing `extraArgs` to `dotnet`;
  - shell out to `dotnet <verb> FS.GG.Governance.sln -m:N -c <config> <extraArgs>`;
  - **print** detected cores, chosen `N`, and elapsed ms (NFR-001 / contract C-9);
  - **return the underlying `dotnet` exit code** unchanged (FR-009 / C-5 / C-10).
  Keep it plain idiomatic F# (Principle III); no new dependency.
- [-] T004 [P] **SKIPPED (rationale):** `dotnet fsi build.fsx` is the **sole entry**. The
  OS shims are optional discoverability sugar; skipping keeps the toolchain F#-exclusive and
  the change minimal (Principle III), matches the repo's existing `dotnet fsi build.fsx`
  idiom (reference gate set), and adds no per-OS script to maintain. The README + docs point
  directly at `dotnet fsi build.fsx`, so discovery is covered without a shim.

**Checkpoint**: `dotnet fsi build.fsx` builds the whole solution bounded; `... test` runs
the suite bounded.

---

## Phase 3: User Story 1 — Fast whole-solution build (Priority: P1) 🎯 MVP

**Goal**: A contributor runs the documented build and gets green/red back in well under
5 minutes (SC-001, SC-006), with near-instant no-op rebuilds (SC-002).

**Independent Test**: From a warm checkout run `dotnet fsi build.fsx`; measure wall-clock;
confirm all 162 projects build, 0 resource errors, time < 5 min and ≥ 4× the unbounded
baseline; then re-run for the no-op < 30 s.

- [X] T005 [US1] **Done:** clean `/t:Rebuild` via wrapper after `build-server shutdown` →
  `Build succeeded`, **162** assemblies, **0** `MSB6003`/`MSB6006`, **33 s** (≥ 18× the
  >10-min unbounded baseline; ≪ 5 min). Validate **clean build** via the wrapper: `dotnet build-server shutdown`
  then time `dotnet fsi build.fsx` (clean, e.g. `/t:Rebuild` passed as extraArg or after
  `git clean`-equivalent of `bin`/`obj`). Assert `Build succeeded`, **162** `-> *.dll`
  lines, **0** `MSB6003`/`MSB6006`, elapsed < 5 min and ≥ 4× baseline (SC-001/006/C-2).
  Record numbers in research.md / quickstart §2.
- [X] T006 [US1] **Done:** immediate re-run → **3.4 s** (≪ 30 s); far too fast to have
  recompiled (a real recompile is ~33 s). Validate **no-op incremental**: immediately re-run `dotnet fsi build.fsx`;
  assert nothing recompiles and elapsed < 30 s (SC-002 / FR-005 / C-3). Depends on T005.
- [X] T007 [P] [US1] **Done:** 162 `.fsproj` on disk = 162 referenced in `.sln`; build log
  shows 162 assemblies; `git diff --stat FS.GG.Governance.sln` empty (membership byte-identical
  to `main`). Validate **project-set unchanged**: `find . -name '*.fsproj' | wc -l`
  = 162 and the build-log `-> ` count = 162; confirm `FS.GG.Governance.sln` membership is
  byte-identical to `main` (`git diff --stat FS.GG.Governance.sln` empty) — speed gained
  with no project dropped (FR-002 / SC-004 / C-1).
- [X] T008 [P] [US1] **Done:** injected an error in `Kernel/Check.fs` → wrapper exited **1**,
  `Build FAILED`, real `FS0201` MSBuild diagnostic in the usual form; reverted → green.
  Validate **correctness signal**: introduce a deliberate compile error
  in one project, run `dotnet fsi build.fsx`, assert it FAILS with the usual `FS####`
  MSBuild diagnostic and a non-zero exit code; revert and confirm green again
  (FR-009 / C-5 / C-10).
- [X] T009 [P] [US1] **Done:** formula spot-check `clamp(2, ceil(cores/4), 12)` →
  cores 2/8/24/64 = N 2/2/6/12 (always ≥ 2, capped 12, scales with cores; the live run
  printed `24 logical cores -> -m:6`). Validate **hardware scaling** intent (FR-006 / C-8): confirm the
  wrapper's printed `N` tracks `ProcessorCount` (e.g. spot-check the formula at cores =
  2, 8, 24, 64 by temporarily overriding the detected value in a scratch run) and that the
  build is not effectively single-threaded (`N ≥ 2`).

**Checkpoint**: MVP — the everyday inner-loop build is fast, honest, and scaling.

---

## Phase 4: User Story 2 — Full test suite as a delivery gate (Priority: P2)

**Goal**: `dotnet fsi build.fsx test` runs the entire suite to completion as the delivery
check, with zero hand-excluded test projects (SC-003 / FR-004).

**Independent Test**: Run `dotnet fsi build.fsx test`; confirm every test project executes
and a final pass/fail summary prints within a single CI job budget (target < 20 min
build+test), no project manually excluded.

- [X] T010 [US2] **Done:** `dotnet fsi build.fsx test` → final summary reached, **81** test
  runs, **2290 passed, 0 failed, 1 named skip**, **0** `MSB6003`/`MSB6006`, total build+test
  **63 s** (≪ 20-min budget), zero hand-excluded projects. Validate **full suite to completion**: run `dotnet fsi build.fsx test`;
  assert the run reaches a final summary, every test project reports, no `MSB6003`/
  `MSB6006` during the build half, and total build+test elapsed is within budget
  (SC-003 / C-4). Record the number. Depends on Phase 2.
- [X] T011 [P] [US2] **Done:** the default run shows exactly one named skip —
  `Skipped WorkedExample.emitted skeleton `dotnet build`s first-attempt (real evidence)` —
  with `FSGG_REAL_EVIDENCE` unset; the opt-in path (set `FSGG_REAL_EVIDENCE=1`) is delivered
  by feature 078. No test project removed from the `.sln`. Verify the **pathological test stays isolated** (FR-010 / research
  D5): confirm the default `dotnet fsi build.fsx test` shows the SDD `fs-gg-fullstack`
  template-generation / worked-example real-build path as a **named skip**
  (`FSGG_REAL_EVIDENCE` unset), and that setting `FSGG_REAL_EVIDENCE=1` opts it in. No
  test project is removed from the `.sln` to make the suite finish.
- [X] T012 [P] [US2] **Done:** SC-005 posture confirmed — the full delivery-gate suite now
  runs to completion in 63 s via `dotnet fsi build.fsx test`, so this feature's own delivery
  records the verification step as **run to completion** (not "skipped due to build/suite
  time"). Captured in the delivery write-up / quickstart results table.

**Checkpoint**: The delivery gate can be honestly run end-to-end.

---

## Phase 5: User Story 3 — Speed without losing coverage/correctness (Priority: P3)

**Goal**: Prove the gain is structural (bounded scheduling), not from building/testing
less (FR-002, FR-003, SC-004).

**Independent Test**: Diff the built project set and outputs before/after; confirm same
projects and functionally-equivalent assemblies.

- [X] T013 [US3] **Done (with noted micro-deviation):** the wrapper changes only build
  *scheduling* — the bounded build emits the same **162** assemblies; `git status` shows
  **no** change to any `.fsi`, `Directory.Build.props`, `Directory.Packages.props`, surface
  baseline, or `.sln` membership (all verified empty/untouched). **Deviation from T013's
  literal "no `.fsproj`":** exactly one **test** `.fsproj` gains a single `<Compile>` line
  wiring the T014 guard — this is the guard mandated by T014, touches no production project,
  no `.sln` membership, and no surface; the Tier 2 invariant (functionally-equivalent
  *product* outputs) holds. Validate **output equivalence** (FR-003 / C-6): build the solution on
  `main` (bounded, for sanity) and on this branch via the wrapper; compare the set of
  emitted assembly names/paths under `*/bin/Debug/net10.0/` — identical set. Confirm
  `git diff --stat` shows **no** change to any `.fsproj`, `.fsi`, `Directory.Build.props`,
  `Directory.Packages.props`, or surface-area baseline (Tier 2 invariant).
- [X] T014 [P] [US3] **Done:** `SolutionBuildWrapperTests.fs` (Expecto, in the existing 078
  "bounded build" test project — no new project, so `.sln` stays byte-identical). For real
  evidence it runs `dotnet fsi build.fsx --print-command` (a new dry-run flag) and inspects
  the **actual emitted** command, asserting it (a) targets `FS.GG.Governance.sln`, (b) carries
  an explicit `-m:N`, (c) with N in [2,12]; plus a replicated-formula range check + the 24→6
  anchor. Proven load-bearing: green on the real wrapper (3/3), **red** when the `-m:` bound
  is dropped, green again on revert. (Text-scrape was rejected — a comment mentioning `-m:`
  satisfied it spuriously.) Add a small **checked-in guard** asserting the fix can't silently
  regress: a test (Expecto, in a lightweight existing or new tests project — NO `.fsi`/
  surface change) that reads `build.fsx` and asserts it (a) references
  `FS.GG.Governance.sln`, (b) always passes an explicit `-m:` bound (never unbounded), and
  (c) the computed `N` is `≥ 2` and `≤` a sane cap. This freezes research D2/D3/D4 so a
  later edit that drops the bound fails CI. Keep it minimal; name any synthetic input per
  Principle V (none expected — it reads the real file).

**Checkpoint**: The metric cannot be gamed by building/testing less.

---

## Phase 6: Documentation & Cross-Cutting

**Purpose**: Make the wrapper *the* documented command so the bound applies uniformly
(FR-007 / FR-008 / C-7).

- [X] T015 [P] **Done:** added a "Building & testing" section to `README.md` pointing at
  `dotnet fsi build.fsx` / `dotnet fsi build.fsx test`, with the one-line explanation that
  the bound avoids `dotnet fsc` over-subscription and scales with cores. Update **`README.md`**: the whole-solution build/test instructions point to
  `dotnet fsi build.fsx` / `dotnet fsi build.fsx test` (replacing/annotating the bare
  `dotnet build` references), with one line explaining the bound exists to avoid
  compiler-process over-subscription and that it scales with cores.
- [X] T016 [P] **Done:** updated `docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`
  (the "verified on `main`" line) to reference the `dotnet fsi build.fsx` wrapper, preserving
  the verified-on-`main` intent. Update **`docs/`** references to the full-solution command (e.g.
  `docs/2026-06-18-governance-kernel-speckit-implementation-plan.md:21`) to the wrapper,
  preserving the "verified on `main`" intent.
- [X] T017 **Done:** ran the quickstart end to end; its pass-criteria table is ticked with
  measured results (33 s clean / 3.4 s no-op / 162 projects / equivalent outputs / compile
  error still fails / 63 s full suite / observability printed). No doc↔wrapper drift.
- [X] T018 [P] **Done:** no `Directory.Build.rsp` anywhere; `bin`/`obj` confirmed git-ignored;
  `git status` shows only the intended files (`build.fsx`, guard `.fs` + its `.fsproj`
  `<Compile>` line, README/docs, `.specify/feature.json`, the spec dir). Final hygiene: confirm no stray `Directory.Build.rsp` / build artifacts are
  staged (`git status` clean except intended files), and that `bin`/`obj` remain ignored.

---

## Dependencies & Execution Order

- **Phase 1 (Setup/Baseline)** → no deps; do first to anchor numbers.
- **Phase 2 (Wrapper)** → depends on Phase 1; **BLOCKS** all user stories. T004 depends on
  T003.
- **Phase 3 (US1)**, **Phase 4 (US2)**, **Phase 5 (US3)** → each depends only on Phase 2;
  can proceed in parallel once the wrapper exists. Within US1, T006 depends on T005.
- **Phase 6 (Docs)** → depends on the wrapper (T003) and is best done after US1/US2
  validate the commands.

### Parallel opportunities

- Phase 1: T001 ‖ T002.
- Phase 3: T007 ‖ T008 ‖ T009 (T005 → T006 ordered).
- Phase 4: T011 ‖ T012 alongside T010.
- Phase 5: T013 ‖ T014.
- Phase 6: T015 ‖ T016 ‖ T018 (T017 after the wrapper + docs).
- Across stories: once Phase 2 is done, US1/US2/US3 can be validated by different people in
  parallel.

---

## Implementation Strategy

**MVP = Phase 1 → Phase 2 → Phase 3 (US1).** That alone delivers the highest-value
outcome: a fast, honest whole-solution build via the documented command. Stop and validate
US1 independently (quickstart §2–§3) before proceeding. Then add US2 (delivery-gate suite)
and US3 (anti-gaming guard), then Phase 6 docs. Commit per task or logical group.

## Task count

- Setup/Baseline: 2 · Foundational (wrapper): 2
- US1: 5 · US2: 3 · US3: 2 · Docs/cross-cutting: 4
- **Total: 18**

## Notes

- **Forbidden in every task** (Tier 2 invariants): editing any `.fsi`, any surface-area
  baseline, `FS.GG.Governance.sln` membership, `Directory.Packages.props`, or any product
  `.fs`; adding a dependency; committing a `Directory.Build.rsp` or env-var-based bound
  (FR-008). Any of these means the task scope is wrong.
- The exact `N` formula (T003) is the one tuning knob — calibrate against SC-001/002/006 on
  real hardware; `6/24` is the proven anchor, the formula generalizes it.
