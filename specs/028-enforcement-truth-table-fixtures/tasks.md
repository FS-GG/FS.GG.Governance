---
description: "Task list for Golden Enforcement Truth-Table Fixtures"
---

# Tasks: Golden Enforcement Truth-Table Fixtures

**Input**: Design documents from `/specs/028-enforcement-truth-table-fixtures/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/truth-table-format.md,
contracts/audit-snapshot-set.md, quickstart.md

**Tests**: This feature *is* a coverage/evidence deliverable — the drift-guard, completeness, and snapshot
tests are the product, not optional add-ons. They are included throughout.

**Tier**: The whole feature is **Tier 2** (no new public `src/` surface; no `.fsi`/surface-baseline
changes). Per-task `[T2]` annotations are therefore omitted (all match the overall tier).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Independently authorable — no dependency on another incomplete task in the phase. (Several
  `[P]` test tasks are independent *cases within one test file*, e.g. `TruthTableTests.fs` /
  `AuditSnapshotTests.fs`; they can be written in any order but land in the same file.)
- **[Story]**: `[US1]` = golden truth table (P1, MVP); `[US2]` = audit.json snapshots (P2)
- Exact file paths are given in every task.

## Path conventions

- New test project: `tests/FS.GG.Governance.EnforcementFixtures.Tests/`
- Committed goldens: `fixtures/enforcement/` (top-level), per reconciliation D2
- No `src/`, `surface/`, or existing-test changes (Tier 2, FR-011)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the new test project and wire it into the solution. No fixtures or assertions yet.

- [X] T001 Create `tests/FS.GG.Governance.EnforcementFixtures.Tests/FS.GG.Governance.EnforcementFixtures.Tests.fsproj`
  with `IsPackable=false`, `GenerateProgramFile=false`; `PackageReference`s to `Expecto`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (**no `FsCheck`/`Expecto.FsCheck`** — the table is an
  exhaustive enumeration, not a sampled property; FR-011 minimalism); and `ProjectReference`s
  to the consumed cores: `Enforcement`, `Ship`, `Route`, `AuditJson`, `Gates`, `Findings`, `Config`,
  `Routing` (mirror the `tests/FS.GG.Governance.AuditJson.Tests/*.fsproj` shape, minus the FsCheck refs;
  **add no new package** — FR-011). Order `<Compile Include>` items: `Support.fs`, `Generator.fs`,
  `TruthTableTests.fs`, `RouteClassTests.fs`, `AuditSnapshotTests.fs`, `Main.fs`.
- [X] T002 [P] Add `tests/FS.GG.Governance.EnforcementFixtures.Tests/Main.fs` — the Expecto entry point
  (`runTestsInAssemblyWithCLIArgs [] argv`), copied verbatim from
  `tests/FS.GG.Governance.AuditJson.Tests/Main.fs` with the namespaced module name.
- [X] T003 Register the new project in `FS.GG.Governance.sln` (Project entry + Debug/Release
  `GlobalSection` configuration rows), following the existing `FS.GG.Governance.AuditJson.Tests` entry; then
  confirm `dotnet build FS.GG.Governance.sln` restores and builds the empty test project.

**Checkpoint**: `dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests` runs (zero tests), proving
the project + references compile against the merged cores.

---

## Phase 2: Foundational (Shared generation scaffolding)

**Purpose**: The shared, story-agnostic helpers both stories depend on: repo-root resolution, the
byte-compare-or-bless helper, the closed dial enumerations, and the dial→token rendering. **Blocks US1 and
US2.**

- [X] T004 Create `tests/FS.GG.Governance.EnforcementFixtures.Tests/Support.fs`: (a) `repoRoot` via the
  `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` walk-up (copied from
  `tests/FS.GG.Governance.AuditJson.Tests/Support.fs`); (b) `fixturesDir = repoRoot/fixtures/enforcement`;
  (c) a `blessOrCompare (relPath: string) (generated: string)` helper that writes the file when
  `Environment.GetEnvironmentVariable "BLESS_FIXTURES" = "1"` and otherwise reads the committed bytes and
  returns them for an `Expect.equal` (normalize `\r\n`→`\n`, assert no BOM, single trailing `\n`), with a
  failure message naming the path and the `BLESS_FIXTURES=1 dotnet test` re-bless command (FR-006, D8).
- [X] T005 In `Support.fs`, add the closed dial enumerations in fixed least→most order, name-qualifying the
  two `Release` cases: `allBaseSeverities = [Advisory; Blocking]`,
  `allMaturities = [Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease]`,
  `allModes = [Sandbox; Inner; Focused; Verify; Gate; RunMode.Release]`,
  `allProfiles = [Light; Standard; Strict; Profile.Release]` (matches the `AuditJson.Tests/Support.fs`
  enumerations; research D4).
- [X] T006 Create `tests/FS.GG.Governance.EnforcementFixtures.Tests/Generator.fs` with the dial→token
  renderers exactly per `contracts/truth-table-format.md`: `severityToken`, `maturityToken`, `modeToken`,
  `profileToken`, `routingResultToken` (`routed:<domain>` / `unmatched-in-root` / `out-of-scope`), and a
  `findingToken` that defers to `Findings.findingIdToken` (or `(none)`); plus a `markdownCell` helper that
  escapes `|` as `\|` (the only reason-text transformation) and a `renderTable headers rows` helper
  emitting a well-formed `| … |` table with the `|---|` rule and `\n` newlines, no trailing whitespace.

**Checkpoint**: Token + table-render helpers compile and are unit-callable; no fixture written yet.

---

## Phase 3: User Story 1 — Golden truth table (Priority: P1) 🎯 MVP

**Goal**: A committed, byte-stable Markdown truth table covering the full 240-row primary cross-product plus
the route-class section, with a drift guard that fails on any unintended dial-behavior change.

**Independent Test**: `dotnet test …EnforcementFixtures.Tests` passes when `fixtures/enforcement/truth-table.md`
matches the live cores; hand-editing one reason/effective cell fails the drift guard with a readable diff;
the table reads to show every dial value and the base-advisory / routine-never-default-deny properties.

### Generation (US1)

- [X] T007 [US1] In `Generator.fs`, add `renderPrimaryTable : unit -> string` that folds the four
  enumerations in fixed nested order (base → maturity → mode → profile, innermost varies fastest), calls
  `Enforcement.deriveEffectiveSeverity { BaseSeverity=…; Maturity=…; Mode=…; Profile=… }` per combination,
  and renders columns `| base | maturity | mode | profile | effective | reason |` using the
  `EnforcementDecision.EffectiveSeverity`/`.Reason` **verbatim** (FR-002, research D5). Exactly 240 data
  rows.
- [X] T008 [US1] In `Generator.fs`, add `renderRouteClassTable : unit -> string` that builds minimal real
  `Config.Model.TypedFacts` and runs the genuine `Routing.route` + `Findings.findUnknownGovernedPaths` for
  the four route-class scenarios (routine = out-of-scope; fenced = routed; unknown-governed-path = ordinary
  finding; protected-surface unknown = escalated finding) and renders columns
  `| class | example path | route outcome | finding | note |` per `contracts/truth-table-format.md` and
  data-model.md (FR-003, research D6). Use real, literally-constructible facts/paths — no mocks
  (Principle V). **There is no shared test-support library**, so replicate the minimal `TypedFacts`
  construction (governed root + one path-map glob → domain + one declared protected surface) from
  `tests/FS.GG.Governance.Routing.Tests` / `tests/FS.GG.Governance.Findings.Tests` support into this
  project's `Support.fs`.
- [X] T009 [US1] In `Generator.fs`, add `renderTruthTable : unit -> string` composing the `#` title, the
  "generated — do not edit; regenerate with `BLESS_FIXTURES=1 dotnet test`" note, the `##` primary table
  (T007), and the `##` route-class table (T008) in the fixed order from the contract.

### Tests (US1)

- [X] T010 [P] [US1] Create `tests/FS.GG.Governance.EnforcementFixtures.Tests/TruthTableTests.fs` with a
  **completeness** test (SC-001): parse the generated primary table, assert exactly 240 data rows and 240
  distinct `(base,maturity,mode,profile)` keys (no missing, no duplicate) — counted against
  `2*5*6*4`.
- [X] T011 [US1] In `TruthTableTests.fs`, add the **drift guard** test (SC-002/SC-003): regenerate via
  `renderTruthTable` and `blessOrCompare "truth-table.md"`; assert byte-equality with the committed file,
  with the readable-diff failure message.
- [X] T012 [P] [US1] In `TruthTableTests.fs`, add a **determinism** test: call `renderTruthTable` twice and
  assert the two strings are identical (SC-002, FR-004) — no clock/host/order influence.
- [X] T013 [P] [US1] In `TruthTableTests.fs`, add **property/visibility** assertions read off the generated
  rows: every `observe`/`warn` row has `effective = advisory`; every `base = advisory` row has
  `effective = advisory` (Edge: base-advisory never escalates); the saturated combos (e.g.
  `block-on-release` under `release`/`release`) are present (Edge: unreachable combinations not omitted).
- [X] T014 [P] [US1] Create `tests/FS.GG.Governance.EnforcementFixtures.Tests/RouteClassTests.fs`
  asserting, against the genuine cores: routine path → `OutOfScope` + no finding and never default-deny
  (also verified at `RunMode.Release`/`Profile.Release` — Edge: routine under strictest dials); fenced
  path → `Routed(domain,…)` + no finding; unknown-governed-path → `UnmatchedInRoot` + explicit finding id;
  protected-surface unknown → escalated `UnknownProtectedBoundaryPath` (FR-003).

### Commit the golden (US1)

- [X] T015 [US1] Generate and commit the golden: run
  `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests` to write
  `fixtures/enforcement/truth-table.md`; review the diff (240 primary rows + route-class section); then run
  the suite **without** the bless flag and confirm T010–T014 pass against the committed bytes. Depends on
  T007–T014.

**Checkpoint**: US1 is independently complete — the committed truth table is auditable and drift-proof; the
MVP and the Phase-5 "every enforcement dial has fixture coverage" exit criterion are met.

---

## Phase 4: User Story 2 — audit.json blocking-altering snapshots (Priority: P2)

**Goal**: Committed `fsgg.audit/v1` snapshots for every dial that flips blocking, each produced verbatim by
the merged F025 projection, guarded by byte-equality and demonstrating the no-hide rule.

**Independent Test**: `dotnet test …EnforcementFixtures.Tests` passes when every
`fixtures/enforcement/audit-snapshots/<name>.audit.json` equals `ofShipDecision (rollup …)` for its
scenario; coverage and no-hide assertions hold; changing the projection/verdict shape fails the matching
snapshot.

### Generation (US2)

- [X] T016 [US2] In `Support.fs`, add the real F019 `RouteResult` builders adapted from
  `tests/FS.GG.Governance.AuditJson.Tests/Support.fs` (`mkGate`, `mkSelectedGate`, `mkFinding`, `mkRoute`)
  — real, literally-constructible typed values, no mocks (Principle V, research D7).
- [X] T017 [US2] In `Generator.fs`, define the named scenario set from `contracts/audit-snapshot-set.md` as
  data: each entry = `{ Name; DialUnderTest; Route; Mode; Profile; ExpectedSection }`. Cover the seven
  scenarios: `maturity-withholds-observe`, `maturity-withholds-warn`, `base-advisory-stays-advisory`,
  `profile-relaxes-blocker`, `profile-tightens-to-block`, `mode-below-floor`, `mode-reaches-floor`.
  **The exact mode/profile pairs are provisional**: the F023 maturity-floor / profile-tighten maps live
  only in `Enforcement.fs` and are not exposed, so confirm each pair against `deriveEffectiveSeverity`
  while authoring and **adjust the pair if needed** so the named `DialUnderTest` is the *sole* difference
  that flips the item's section (the relaxes/tightens pair and below/at-floor pair must each differ by
  exactly one dial). T020's partition assertion is the safety net that catches a mis-picked pair.
- [X] T018 [US2] In `Generator.fs`, add `snapshotFor scenario : string = AuditJson.ofShipDecision
  (Ship.rollup scenario.Route scenario.Mode scenario.Profile)` — the verbatim merged projection; introduce
  **no** new/altered schema and no post-processing (FR-008, research D7).

### Tests (US2)

- [X] T019 [P] [US2] Create `tests/FS.GG.Governance.EnforcementFixtures.Tests/AuditSnapshotTests.fs` with a
  **per-scenario byte-equality** test (one Expecto case per scenario via the scenario list): each committed
  `audit-snapshots/<name>.audit.json` equals `snapshotFor scenario` via `blessOrCompare` (FR-008).
- [X] T020 [P] [US2] In `AuditSnapshotTests.fs`, add a **partition** test: parse each emitted document with
  `JsonDocument` and assert the dialed item lands in `scenario.ExpectedSection`
  (`blockers`/`warnings`/`passing`) — never recomputing the verdict (the F025 contract fixed it).
- [X] T021 [P] [US2] In `AuditSnapshotTests.fs`, add the **no-hide** test (FR-009, SC-004): for each
  relaxed-blocker scenario (item in `warnings`), assert its nested `enforcement` object carries both
  `baseSeverity` and `effectiveSeverity` (differing) and a non-empty `reason`.
- [X] T022 [P] [US2] In `AuditSnapshotTests.fs`, add the **coverage** test (FR-010): the set of
  `DialUnderTest` across scenarios includes every blocking-altering dial — maturity, base severity,
  profile, run mode — failing if any is unrepresented.

### Commit the goldens (US2)

- [X] T023 [US2] Generate and commit the snapshots: run
  `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests` to write
  `fixtures/enforcement/audit-snapshots/*.audit.json`; review the diffs (confirm the
  relaxes/tightens pair and below/at-floor pair differ by exactly the dialed lever); then run **without**
  the bless flag and confirm T019–T022 pass. Depends on T016–T022.

**Checkpoint**: US2 is independently complete — the Phase-5 "profile-adjusted blocking is explained without
changing rule truth" exit criterion is met.

---

## Phase 5: Polish & Cross-Cutting

**Purpose**: Documentation pointer, full validation, and the Tier-2 no-regression guarantee.

- [X] T024 [P] Add a short pointer to `README.md` directing readers to `fixtures/enforcement/` (the golden
  enforcement truth table + audit snapshots) and the drift-guard bless command — mirroring the F027 README
  pointer style. **After T015** (and ideally T023) so the pointer never references a not-yet-committed
  golden.
- [X] T025 Run `quickstart.md` end-to-end: the bless flow, the validation flow, and the manual
  drift-guard smoke (hand-edit a cell → guard fails → `git checkout` → passes).
- [X] T026 Tier-2 no-regression check: `dotnet build FS.GG.Governance.sln` + `dotnet test` (whole solution)
  green, and `git status --porcelain src/ surface/` is empty — confirming no merged core, `.fsi`, or
  surface baseline changed (FR-011).
- [X] T027 Evidence-obligations note: confirm Principle IV (Elmish/MVU) is **N/A** (pure fold over closed
  enumerations + edge-only file I/O; no modeled stateful workflow) and Principle V is satisfied by real
  inputs through the genuine cores (no synthetic evidence, so no `Synthetic` disclosure). Record this one
  line in the PR description.

## Contract reconciliation (maintainer-confirmed 2026-06-21)

- **Maturity snapshots land in `passing`, not `warnings`.** `contracts/audit-snapshot-set.md` lists the
  `maturity-withholds-observe` / `maturity-withholds-warn` scenarios' expected partition as `warnings`, but
  that is **unrealizable** through the mandated `ofShipDecision (rollup …)` path: `Ship.rollup` derives base
  severity *from* maturity, so an `Observe`/`Warn` gate is base-`Advisory` and lands in `passing`
  (base == effective) — never in `warnings` (which requires a base-`Blocking` item relaxed to `Advisory`).
  FR-008 forbids a hand-built `ShipDecision` to force the warnings case. Per the maintainer the honest
  outcome is kept: both scenarios assert `passing`; the no-hide *warnings* evidence is carried by
  `profile-relaxes-blocker` and `mode-below-floor` (genuine base-`Blocking`→`Advisory` relaxations). The
  deviation is documented at the `scenarios` definition in `Generator.fs`. All FRs are satisfied: every
  blocking-altering dial (maturity / base severity / profile / run mode) has ≥1 snapshot (FR-010), and the
  no-hide rule is observable in the committed bytes (FR-009).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1 — **blocks both stories**.
- **US1 (Phase 3)** and **US2 (Phase 4)**: both depend on Phase 2. US2 does not depend on US1's *fixtures*,
  but both edit `Generator.fs`/`Support.fs`, so run US1 then US2 unless splitting the files; each is
  independently testable.
- **Polish (Phase 5)**: after both stories (T024 may proceed once either golden exists).

### Within each story

- Generation functions before their tests; tests authored to fail before the golden is blessed, pass after
  (T015/T023 commit step). Commit the golden only after the generators and tests compile and the diff is
  reviewed.

### Parallel opportunities

- T002 ∥ (T001 must precede the build in T003).
- Within Phase 2, T005 and T006 touch different files (Support.fs / Generator.fs) and can overlap after
  T004 lands the Support.fs skeleton.
- US1 tests T010, T012, T013, T014 are `[P]` (independent assertions / different file for T014).
- US2 tests T019–T022 are `[P]` (same file, independent test cases — author together).
- Across stories: with file-splitting, US1 and US2 generation can proceed in parallel after Phase 2.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1.
2. **Stop and validate**: the committed `truth-table.md` is drift-guarded and auditable — the Phase-5
   "every enforcement dial has fixture coverage" criterion is met. Ship as the MVP.

### Incremental delivery

1. Setup + Foundational → scaffolding ready.
2. US1 → committed truth table + drift guard (MVP).
3. US2 → committed audit snapshots + no-hide/coverage guards.
4. Polish → README pointer + full no-regression validation.

---

## Notes

- `[P]` = independently authorable, no dependency on another incomplete task in the phase (may be
  independent cases within one test file).
- Tier 2 throughout: **no** `src/`, `.fsi`, or `surface/*.surface.txt` changes; all values come verbatim
  from the merged F014/F015/F017/F023/F024/F025 cores.
- All goldens are UTF-8 (no BOM), `\n` newlines, no trailing whitespace; intended changes re-bless with
  `BLESS_FIXTURES=1 dotnet test`.
- Never mark a failing task `[X]`; never weaken an assertion to green the build — narrow scope and document.
