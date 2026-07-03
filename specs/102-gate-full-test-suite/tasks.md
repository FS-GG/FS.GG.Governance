---
description: "Task list for 102-gate-full-test-suite"
---

# Tasks: The gate runs the full test suite on every PR

**Input**: Design documents from `/specs/102-gate-full-test-suite/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/gate-test-job.md, quickstart.md

**Tier**: 2 (CI configuration only — no product API/`.fsi`/contract/test-assertion change).

**Shape note**: This feature's three user stories are three *properties of one job block* in one file
(`.github/workflows/gate.yml`): US1 = the suite runs and a red assertion fails the gate; US2 = the job is
bounded + cached; US3 = the job's name binds the already-registered required status check. So there is a
single implementation task (T003) that delivers all three, followed by per-story validation. There is no
foundational code to build and no `.fsi`/MVU surface (Principle IV: N/A — declarative CI YAML calling the
existing `build.fsx`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (independent, read-only, or different concern)
- **[Story]**: US1 / US2 / US3 (or blank for cross-cutting)

---

## Phase 1: Setup & baseline evidence

**Purpose**: Confirm the starting state and capture the "before" that makes the fix's value measurable.

- [X] T001 [P] Capture the baseline gap: confirm `.github/workflows/gate.yml` has no job that runs the
  suite (only `gate` builds; `reference-gate-set-pack` runs one project), and that publish.yml runs only
  `Cli.Tests`. Evidence: `grep -n 'build.fsx test\|dotnet test' .github/workflows/gate.yml` returns nothing.
- [X] T002 [P] Confirm the required-check contract already exists (read-only, no mutation):
  `gh api repos/FS-GG/FS.GG.Governance/rulesets/18430843 --jq '.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context'`
  MUST include the exact string `Full test suite (dotnet fsi build.fsx test)`. Records that FR-008/009 need
  no ruleset edit (data-model Entity 2).

---

## Phase 2: Implementation (delivers US1 + US2 + US3)

- [X] T003 [US1][US2][US3] Add the `full-test-suite` job to `.github/workflows/gate.yml` per
  contracts/gate-test-job.md Contract A — as a sibling of the existing jobs:
  - `name: Full test suite (dotnet fsi build.fsx test)` **byte-exact** (US3 — binds the required check; a
    typo blocks every PR on a perpetually-pending check)
  - `runs-on: ubuntu-latest`, `timeout-minutes: 30` (US2 — bound; FR-004)
  - `actions/checkout@v4` → `actions/setup-dotnet@v4` with `cache: true` +
    `cache-dependency-path: '**/packages.lock.json'` (US2 — cache; FR-006)
  - `Restore (locked)` step: `dotnet restore FS.GG.Governance.sln --locked-mode` with the actionable
    regenerate-hint on failure (US1 — FR-003; mirrors the `gate` job)
  - `Test (full suite, bounded)` step: `dotnet fsi build.fsx test -c Debug --no-restore` (US1 — whole
    solution via the bounded entrypoint; FR-002/FR-005)
  - **No** `continue-on-error`, **no** `matrix`, **no** retry wrapper (FR-010; single-job per research D3)

---

## Phase 3: Validation — US1 (P1) 🎯 MVP · a failing test blocks the merge

**Independent Test**: quickstart US1.

- [X] T004 [US1] Local green baseline: `dotnet restore FS.GG.Governance.sln --locked-mode` then
  `dotnet fsi build.fsx test -c Debug --no-restore` → all projects `Passed!`, exit 0. (SC-003 clean side)
- [X] T005 [US1] Total-coverage proof: the run enumerates 83 test DLLs, not 2 —
  `... | grep -c 'Test run for .*\.Tests\.dll'` → 83. (FR-002 / SC-002)
- [X] T006 [US1] RED proof (the crux of H1): flip one assertion in a project that does NOT run in CI today
  (e.g. `FS.GG.Governance.EvidenceJson.Tests`), re-run `build.fsx test -c Debug --no-restore --no-build` →
  that project `Failed!`, exit non-zero; **revert** → green again. Capture both outputs. (SC-001 / SC-003)

---

## Phase 4: Validation — US2 (P2) · bounded & cached

**Independent Test**: quickstart US2.

- [X] T007 [P] [US2] Rendered-job inspection: `grep -n -A20 'full-test-suite:' .github/workflows/gate.yml`
  shows exact name, `timeout-minutes: 30`, the cache block, locked restore, and the `build.fsx test`
  invocation — not a raw unbounded `dotnet test`. (FR-004/FR-005/SC-003)
- [X] T008 [US2] Cache-warm (observed on CI after the PR opens): the job's second run with unchanged
  lockfiles reports a NuGet cache hit / faster restore in the "Set up .NET" step. (FR-006/SC-005)

---

## Phase 5: Validation — US3 (P3) · durable required check

**Independent Test**: quickstart US3.

- [X] T009 [US3] Confirm the job `name` in `gate.yml` equals the required-check context recorded in T002
  byte-for-byte (diff the two strings). On the PR, the `Full test suite (dotnet fsi build.fsx test)` check
  transitions from pending → reporting. No ruleset write is performed. (FR-008/FR-009/SC-004)

---

## Phase 6: Cross-cutting & integrity

- [X] T010 [P] Blast-radius check: `git diff --name-only origin/main...HEAD` touches ONLY
  `.github/workflows/gate.yml` (+ `specs/102-*`). NOT `Directory.Build.props`,
  `Directory.Packages.props`, `.config/dotnet-tools.json`, nor any `*.fs`/`*.fsi`/`*.fsproj`.
  (FR-011/SC-006; `build-config-drift` stays green on CI)
- [X] T011 Integrity check: confirm no test was suppressed to green the gate — no new `continue-on-error`,
  no blanket retry, no wholesale skip. Any headless-fragile test surfaced on CI (H2/H3 class, #46/#47) is
  fixed or narrowly quarantined with a `Synthetic`/skip rationale comment and a tracking issue, never
  blanket-masked. (FR-010/SC-006; research D6)
- [X] T012 Run the quickstart.md "Definition of done" checklist end-to-end and check each box.

---

## Dependencies & Execution Order

- **Phase 1** (T001–T002): read-only, parallel, no deps — do first to record the "before".
- **Phase 2** (T003): the one implementation task; depends on nothing but Phase 1's findings (the exact
  required-check name from T002).
- **Phases 3–5** (validation): depend on T003. T004→T005→T006 are sequential (same working tree, the RED
  proof mutates then reverts). T007/T009 are static and can run in parallel with each other and with
  T004–T006. T008 needs the PR live on CI.
- **Phase 6**: after implementation; T010 is parallel-safe; T011/T012 close out.

## MVP scope

**User Story 1 (T003 + T004–T006)** is the whole point and the MVP: the suite runs on every PR and a
regression that compiles but breaks an assertion fails the gate. US2 (bound/cache) and US3 (required-check
binding) are delivered by the *same* T003 job block and are verified in Phases 4–5.

## Parallel opportunities

- T001 ∥ T002 (both read-only).
- T007 ∥ T009 ∥ T010 (all static inspections, independent).

## Task count

- US1: 4 (T003 shared + T004, T005, T006) · US2: 2 (T003 shared + T007, T008) ·
  US3: 2 (T003 shared + T009) · Setup: 2 · Cross-cutting: 3. Total distinct tasks: 12.
