# Tasks: Governance `.fsgg` Slot Rename (`project.yml` → `governance.yml`)

**Input**: Design documents from `/specs/084-governance-yml-rename/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/loader-slot.md ✅

**Tier**: Tier 1 (alters observable loader behavior + cross-product contract per ADR-0005).
The single functional change is one filename constant; the rest is fixtures, test-support,
one doc line, and one new regression test. No public type/member rename (FR-009); the only
`.fsi` edits are doc-comment text.

**Tests**: REQUIRED for this feature — SC-004 mandates the no-fallback contract be
"demonstrated by tests" (research D7). All other behavioral coverage rides existing
real-fixture loader/schema tests re-pointed at `governance.yml`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different files)
- **[Story]**: `US1` / `US2` / `US3` per spec
- Status legend: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped w/ rationale

## Working-tree starting state (informative — confirmed 2026-06-28)

The rename is **half-done in the working tree** (research §Working-tree starting state); the
items below are ALREADY APPLIED on branch `084-governance-yml-rename` and are verified present
on disk, but are **uncommitted and not yet proven green** by a full build+test:

- `src/FS.GG.Governance.Config/Loader.fs` — slot string switched to `"governance.yml"` ✅ present
- `Model.fs` / `Model.fsi` `// ── governance.yml ──` + `Schema.fsi` `Root` doc-comment ✅ present
- All 34 config fixtures + `tests/golden-fixture/.fsgg/` + `samples/sdd-reference-gate-set/.fsgg/`
  (= 36 primary-slot moves) renamed (pure moves; zero `project.yml` primary slots remain under
  `tests/`/`samples/`) ✅
- ~10 test-support/test files re-pointed to `governance.yml` (zero `project.yml` in `tests/**/*.fs`) ✅
- `tests/.../fixtures/README.md` + `samples/sdd-reference-gate-set/README.md` ✅ present

**NOT yet done** (the real remaining work): `README.md` Governance four-file enumeration still
says `project.yml`; the no-fallback regression test does NOT exist; build+test not yet verified
green on the finished state; the whole set is uncommitted (no coherent commit — SC-006).

Tasks for already-applied edits below are kept as **verification tasks** (confirm the on-disk
edit is correct), not re-authoring.

---

## Phase 1: Setup (Shared baseline)

**Purpose**: Confirm the branch and capture the pre-finish scan so completeness is measurable.

- [X] T001 Confirm on branch `084-governance-yml-rename` and capture the SC-001 baseline scan:
  `find tests samples -path '*/.fsgg/*' -name 'project.yml'` and
  `find tests/golden-fixture -name 'project.yml'` both return **empty** (renames already staged —
  34 config fixtures + 1 golden-fixture + 1 sample = 36 primary-slot moves). The **empty scan**,
  not a hard count, is the binding SC-001 check. Record any non-empty result as a missed rename to
  fix in Phase 4.

---

## Phase 2: Foundational (Blocking prerequisite — the load-bearing change)

**Purpose**: The single loader filename constant + `.fsi`/Model comment touches. Every user
story's tests depend on this source change being in place. (Already applied in the working
tree — these tasks VERIFY correctness, not re-author.)

**⚠️ CRITICAL**: All stories' behavioral assertions resolve against this slot string.

- [X] T002 [Foundation] Verify `src/FS.GG.Governance.Config/Loader.fs` `fileSystemReader` reads
  `Project = slot "governance.yml"` with **no `project.yml` fallback** anywhere in the reader
  (contract C2/D1). Confirm the `// ...comes from governance.yml` comment is the only other change.
- [X] T003 [P] [Foundation] Verify the `.fsi`/Model doc-comment touches are comment-only (no
  signature/type/member change): the `// ── governance.yml ──` section-comment in `Model.fs` (~:75)
  and `Model.fsi` (~:115), and the `ProjectFacts.GovernedRoot` doc in `Schema.fsi` (~:46) reading
  `governance.yml`. Verify by the **comment/doc content** (drift-proof); the line numbers are hints
  only. `ProjectFacts` and the `Source.Project` field name are RETAINED (FR-009, D4).

**Checkpoint**: Loader reads the new slot; story work can proceed.

---

## Phase 3: User Story 1 — Loads config from the `governance.yml` slot (Priority: P1) 🎯 MVP

**Goal**: The loader reads the primary slot from `governance.yml`, produces identical typed
facts/gates/routing/diagnostics, and does NOT fall back to the SDD-owned `project.yml`.

**Independent Test**: Drive `Loader.loadAndValidate` / `readSource` against a `governance.yml`
`.fsgg/` and assert `Valid` with identical facts; drive it with `project.yml` present +
`governance.yml` absent and assert the primary slot is reported **absent/missing** (not `Valid`).

### Tests for User Story 1 (REQUIRED — SC-004)

> Write the no-fallback test FIRST and confirm it FAILS against a loader that read
> `project.yml`, then PASSES against the in-place `governance.yml` loader (research D7).

- [X] T004 [US1] Add the **no-fallback regression test** to
  `tests/FS.GG.Governance.Config.Tests/LoaderTests.fs` (contract C2, SC-004, Story-1 scenario 2):
  an injected `FileReader` returning `Ok (Some content)` for `"project.yml"` and `Ok None` for
  `"governance.yml"`; assert the result is **Invalid / missing-required primary slot** (NOT
  `Valid`) and that the `project.yml` content is not consumed. Match the existing
  `erroringReader`/`absentReader` injected-reader test style already in the file. Confirm it would
  fail before the slot switch (D7). Additionally assert the ADR-0005 **steady state** (spec Edge
  "Both files present"): an injected reader returning `Ok (Some governanceContent)` for
  `"governance.yml"` AND `Ok (Some projectContent)` for `"project.yml"` loads **`Valid` from the
  `governance.yml` content**, with the `project.yml` content never consumed — the same `Valid` facts
  as the governance-only case (one extra assertion in the same test; no new code path).

### Implementation / verification for User Story 1

- [X] T005 [US1] Verify the existing injected-reader cases in `LoaderTests.fs` are re-pointed to
  `"governance.yml"` keys (currently 2 refs) and the `erroringReader` optional-file case (C5) still
  surfaces `Error` (never swallowed). No `project.yml` key remains as a primary-slot reader key.
- [X] T006 [US1] Run `dotnet test tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj`
  and confirm **all green**, including the new T004 test (C2), the real-fixture `Valid` loads (C1),
  and every malformed-fixture diagnostic reproduced row-for-row from `governance.yml` (C4).

**Checkpoint**: US1 fully functional and independently testable — the MVP. The loader reads
`governance.yml` and provably ignores `project.yml`.

---

## Phase 4: User Story 2 — Fixtures, samples & test-support reference the new slot (Priority: P1)

**Goal**: Every on-disk primary-slot artifact and every test-support helper names
`governance.yml`, so the full suite exercises the renamed slot. (Renames already staged —
these tasks VERIFY completeness and run the dependent suites.)

**Independent Test**: Full `dotnet test` compiles and passes with zero `project.yml` primary-slot
fixtures under `tests/` and `samples/`.

- [X] T007 [P] [US2] Verify all 34 config fixtures under
  `tests/FS.GG.Governance.Config.Tests/fixtures/*/.fsgg/governance.yml` + `tests/golden-fixture/.fsgg/governance.yml`
  (35 under `tests/`) exist as pure moves (byte-identical content; `git diff --cached --stat` shows 0
  insertions/0 deletions for the moves — D2). T001's scan must be empty (the binding check; the 34/35
  counts are descriptive, not assertions to chase).
- [X] T008 [P] [US2] Verify every test-support/test helper that writes or names the primary slot uses
  `governance.yml` (zero `project.yml` in `tests/**/*.fs`): `FS.GG.Governance.Tests.Common`,
  `CacheEligibilityCommand`/`FreshnessSensing`/`RouteCommand`/`VerifyCommand` `Support.fs`,
  `ReleaseCommand/MergeableTests.fs`, `Scaffold.Tests/{Interpreter,Loop,NoProvider}Tests.fs`, and
  `tests/.../fixtures/README.md`.
- [X] T009 [P] [US2] Verify the curated sample `samples/sdd-reference-gate-set/.fsgg/governance.yml`
  + co-located `samples/sdd-reference-gate-set/README.md` name the new slot (FR-005), then run
  `dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests/FS.GG.Governance.ReferenceGateSet.Tests.fsproj`
  and confirm the guard loads `Valid` from `governance.yml` with unchanged invariants
  (gate count, `defaultProfile: light`, no dangling refs — Story-2 scenario 2).

**Checkpoint**: US1 + US2 both green independently; the suite exercises the real renamed slot.

---

## Phase 5: User Story 3 — Adopter & design docs reflect the Governance slot (Priority: P2)

**Goal**: Docs describing the **Governance** primary slot name `governance.yml`; docs describing
the **SDD-owned** `project.yml` are left unchanged (FR-008, judgment-based — research D5).

**Independent Test**: Inspect updated docs — the Governance four-file enumeration names
`governance.yml`; SDD-context `project.yml` references remain intact.

- [X] T010 [US3] Update the `FS.GG.Governance.Config` four-`.fsgg`-files enumeration in `README.md`
  (the line listing `(project.yml, policy.yml, capabilities.yml, tooling.yml)` in the F14 paragraph,
  line ~97 — locate by the literal enumeration text, not the line number) to name **`governance.yml`**
  (FR-007, SC-005). This is the one binding doc target.
- [X] T011 [US3] Per-mention judgment pass over the remaining `docs/` `project.yml` hits
  (`initial-design.md`, `initial-implementation-plan.md`, the 2026-06-18 capability-design report,
  `governance-design/speckit-in-the-system.md`, and the `014` `fsgg-schema.md` contract): update ONLY
  text describing **Governance's** primary slot; LEAVE every reference describing the **SDD-owned**
  `.fsgg/project.yml` or historical design context unchanged (FR-008, D5). Record which were
  changed vs. left, with the per-mention rationale. (`bin/`/`obj/` XML-doc hits are build artifacts —
  ignore, they regenerate; D6.)

**Checkpoint**: All three stories complete; docs name the Governance slot correctly.

---

## Phase 6: Polish — green suite, surface-drift confirm & coherent commit

**Purpose**: Prove the finished rename is whole-build green, forces no baseline re-bless, and
lands as one coherent commit.

- [X] T012 Run `dotnet build FS.GG.Governance.sln` — **no errors** (SC-002).
- [X] T013 Run `dotnet test FS.GG.Governance.sln` — every previously-green test project stays green,
  no regression (SC-003). Confirm surface-drift baselines (`FS.GG.Governance.SurfaceChecks.Tests`,
  `*.Routing.Tests/SurfaceDriftTests.fs`) stay green with **NO re-bless** — only `.fsi` comment text
  changed (D3). If any baseline unexpectedly captures the comment, treat as a Tier-1 additive
  re-bless and record it. Note the pre-existing out-of-scope CLI `dotnet pack` local-feed
  MSBuild-node timeout flake if it appears (not caused by this change).
- [X] T014 Run the quickstart validation end-to-end (`specs/084-governance-yml-rename/quickstart.md`
  steps 1–6); confirm SC-001 scan empty, SC-004 no-fallback green, SC-005 README line, and a clean
  pre-commit tree of only the intended changes.
- [X] T015 Commit the complete change coherently (FR-010, SC-006): the fixture/golden/sample moves +
  `Loader.fs`/`Model.fs`/`Model.fsi`/`Schema.fsi` + all test-support/test edits + the new no-fallback
  test + `README.md` + the doc edits from T011 — **together, in one commit** — so the branch is never
  left half-renamed. Verify `git status --short` is clean afterward and `git show --stat HEAD` shows
  the moves and source/test/doc edits in one set.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1 — BLOCKS all stories (the loader slot is load-bearing).
- **Phase 3 (US1)**: depends on Phase 2. The MVP.
- **Phase 4 (US2)**: depends on Phase 2. Independently testable; US1 + US2 are both P1 and ship together
  (a renamed loader against unrenamed fixtures — or vice versa — leaves the suite red).
- **Phase 5 (US3)**: depends on Phase 2; doc-only, does not gate the suite (P2 fast-follow).
- **Phase 6 (Polish)**: depends on US1 + US2 (+ US3 for the commit) being complete.

### Within / across stories

- T004 (write the failing no-fallback test) before T006 (run the Config suite green).
- T012 (build) before T013 (test); both before T014 (quickstart) before T015 (commit).
- US3 (docs) is independent of US1/US2 behavior and may proceed in parallel once Phase 2 is verified.

### Parallel opportunities

- T003 ∥ T002 (different files).
- T007 ∥ T008 ∥ T009 (different fixture/support/sample trees) — all [P] within US2.
- US3 (T010/T011) may run in parallel with US2 verification.

---

## Suggested MVP scope

**User Story 1 (Phase 3)** — the loader reads `governance.yml` and provably does not fall back to
the SDD-owned `project.yml`, demonstrated by the new no-fallback test (SC-004). This is the
load-bearing behavior; US2 (fixtures/support) is its required co-shipped P1 partner so the suite is
green, and US3 (docs) is the P2 fast-follow. All three land in one coherent commit (T015).

## Task count per story

- Setup: 1 (T001) · Foundational: 2 (T002–T003)
- **US1 (P1)**: 3 (T004–T006) — 1 new test, 2 verification/run
- **US2 (P1)**: 3 (T007–T009) — verification + reference-gate-set suite
- **US3 (P2)**: 2 (T010–T011) — README + judgment doc pass
- Polish: 4 (T012–T015) — build, test, quickstart, coherent commit
- **Total: 15 tasks**

## Notes

- Most source edits are ALREADY APPLIED in the working tree; the genuine remaining authoring is
  T004 (no-fallback test) and T010/T011 (docs). The rest is verify-and-commit.
- Internal names (`ProjectFacts`, `Source.Project`) are retained (FR-009) — do not rename.
- No `schemaVersion` bump (D2). No new dependency. No production core/gate/routing/handoff change.
- Never mark a failing task `[X]`; never weaken an assertion to green the build.
