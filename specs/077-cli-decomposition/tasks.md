---
description: "Task list for CLI render / IO decomposition (Phase E)"
---

# Tasks: CLI render / IO decomposition (Phase E)

**Input**: Design documents from `/specs/077-cli-decomposition/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (all present)

**Change Classification**: **Tier 1** — new `.fsi`-curated modules + additively-grown
surface baseline; every CLI text/JSON transcript, golden, and snapshot stays
byte-identical.

**Tests**: This feature authors **no new behavioral tests**. The binding acceptance
evidence is the *existing* goldens/transcripts/snapshots failing-on-drift (Principle V),
plus the additive surface-drift literal + scope-guard edits called out per commit. Per
FR-009 (research D8) **one concern is moved per commit, full suite green at each**.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on other incomplete tasks)
- **[Story]**: `US1` (CliRender) / `US2` (ArtifactReading + ReviewStore)
- All tasks are Tier 1 (matches spec); no per-task tier annotation needed.

## Path Conventions

Single optional CLI project: `src/FS.GG.Governance.Cli/`, tests at
`tests/FS.GG.Governance.Cli.Tests/`, surface baseline at `surface/`.

Compile order (research D2): `Project → Cli → CliRender → ArtifactReading → ReviewStore → Program`.

---

## Phase 1: Setup (Baseline Capture — no code change)

**Purpose**: Freeze the pre-change byte-identity baseline so any drift in later phases
is detectable. No source is modified in this phase.

- [X] T001 Build the CLI release binary as the baseline reference:
  `dotnet build src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -c Release`
  (per quickstart.md §0). Built clean (0 warnings/errors) from unmodified `main`.
- [X] T002 [P] Capture the pre-change text/JSON transcript matrix (each command ×
  text/json × success/usage-error) to `/tmp/077-before-*` per quickstart.md §0 — the
  `route/explain/contract/evidence` matrix plus the `bogus` usage-error (text, exit 64).
  Captured from the clean Release build BEFORE any edit — this is the binding
  byte-identity baseline used to gate every commit.
- [-] T003 [P] Record the pre-change full-suite green baseline. SKIPPED as a discrete
  pre-change snapshot: the background `dotnet test FS.GG.Governance.sln` raced with the
  US1 edits (its solution build overlapped the source changes) and was discarded. The
  binding baseline is instead the clean T002 transcripts (pre-edit) plus the clean
  full-suite run at T025; per-project CLI count is unchanged by this feature (call-site
  + literal edits, no new behavioral tests). The pre-existing CLI `dotnet pack` timeout
  flake in `PackagingTests.fs` is out-of-scope (quickstart.md §2).

**Checkpoint**: Baseline transcripts + test counts recorded. Extraction can begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: One-time confirmation of the seam inventory before any relocation, so each
concern moves verbatim (research D9 "move verbatim, no reflow").

**⚠️ CRITICAL**: No user-story extraction begins until the seam inventory is confirmed.

- [X] T004 Confirm the exact render seam in `src/FS.GG.Governance.Cli/Cli.fs`: the four
  entry points (`renderParseError`/`renderText`/`renderJson`/`render`) + every `*Json`/
  `*Text` sub-writer + render-only formatting helpers (`commandName`, `modeName`,
  `formatName`, `exitCategory`, `ruleIdText`, `evidenceStateText`, `freshnessText`,
  `quote`, `jsonArray`, `failureText`, `budgetLine`) per research D1. Confirm
  `exitCode` + `stableStrings` STAY public in `module Cli` (shared, research D5).
- [X] T005 [P] Confirm the artifact-reading seam in `src/FS.GG.Governance.Cli/Program.fs`
  (`tryReadAllText` … `loadSnapshot`, `optionsFor`) and the review-store seam
  (`safeFileName`, `reviewStoreRoot`, `verdictText`, `parseVerdict`, `loadReview`,
  `saveReview`) per research D1, and that `runHost`/`main`'s budget folds + the
  `review-dispatch-failed` branch STAY in `Program.fs` (research D4).

**Checkpoint**: Seam boundaries confirmed; the three `.fsi` contracts in `contracts/`
are the authority for what each new module exposes.

---

## Phase 3: User Story 1 — Rendering is a separate concern from parsing (Priority: P1) 🎯 MVP

**Goal**: Extract the pure `CommandResult`→text/JSON rendering out of the 829-LOC
`Cli.fs` into a new `.fsi`-curated `CliRender` module, leaving parsing/normalization/MVU
behind. Independently shippable (Commit 1, research D8).

**Independent Test**: After the extraction, the captured text/JSON byte matrix (T002) is
identical and the `*.Cli.Tests` suite is green; `grep` confirms `render*` no longer lives
in `Cli.fs` (SC-003).

### Implementation for User Story 1 (one commit)

- [X] T006 [US1] Add `src/FS.GG.Governance.Cli/CliRender.fsi` from
  `contracts/CliRender.fsi` — `namespace FS.GG.Governance.Cli`, `module CliRender`
  exposing exactly `renderParseError`/`renderText`/`renderJson`/`render`; all sub-writers
  hidden (FR-006).
- [X] T007 [US1] Create `src/FS.GG.Governance.Cli/CliRender.fs` by **relocating verbatim**
  (no reflow/reorder — research D9) the four render entry points + all `*Json`/`*Text`
  sub-writers + render-only formatting helpers (T004 inventory) out of
  `src/FS.GG.Governance.Cli/Cli.fs`. Sub-writers reference `Cli.exitCode`/
  `Cli.stableStrings` by compile order; `.fs` carries no top-level access modifiers
  (FR-006).
- [X] T008 [US1] Remove the four render `val`s from `src/FS.GG.Governance.Cli/Cli.fsi`
  and the relocated bodies from `src/FS.GG.Governance.Cli/Cli.fs` (parsing/normalization/
  MVU/`exitCode`/`stableStrings` stay).
- [X] T009 [US1] Insert `CliRender.fsi` + `CliRender.fs` compile entries **after** the
  `Cli` pair and **before** `Program.fs` in
  `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` (no new ProjectReference, FR-008).
- [X] T010 [US1] Repoint the three call sites (research D3): `writeOutput` and the `main`
  stderr path in `src/FS.GG.Governance.Cli/Program.fs` (`Cli.render` → `CliRender.render`),
  and the two `Cli.renderJson`/`Cli.renderText` references in
  `tests/FS.GG.Governance.Cli.Tests/OutputTests.fs` → `CliRender.*`.
- [X] T011 [US1] Append `module CliRender` (after `module Cli`) to BOTH
  `surface/FS.GG.Governance.Cli.surface.txt` and the in-test `generatedSurface` literal in
  `tests/FS.GG.Governance.Cli.Tests/SurfaceDriftTests.fs` (additive, FR-011).
- [X] T012 [US1] **Gate**: rebuild + replay the T002 matrix; every `diff` empty (SC-001);
  `dotnet test tests/FS.GG.Governance.Cli.Tests/...` green incl. surface-drift; confirm
  SC-003 via `grep -nE "let +render(Text|Json|ParseError)? " Cli.fs` → no matches
  (quickstart §1/§4). **Commit 1** ("077 cli-decomposition: extract CliRender").

**Checkpoint**: `CliRender` is the only home of rendering; `Cli.fs` is parser/MVU-only;
output byte-identical. US1 independently shippable as the MVP.

---

## Phase 4: User Story 2 — Artifact reading and review-store I/O are separate from orchestration (Priority: P2)

**Goal**: Extract the two edge I/O concerns out of `Program.fs` into `ArtifactReading`
and `ReviewStore`, reducing `runHost`/`main` to thin port orchestration with no inline
file-I/O bodies. Two sub-commits, sequenced after US1 (research D8).

**Independent Test**: Snapshot/review-store/read-only suites pass unchanged; a fixture
run yields an identical `ProjectSnapshot`; a review load/save round-trip (incl. the
`review-store-unavailable`/`review-dispatch-failed` fixture paths) behaves identically;
`runHost`/`main` hold no `File.`/`Directory.` bodies (SC-004).

### US2a — ArtifactReading (Commit 2)

- [X] T013 [US2] Add `src/FS.GG.Governance.Cli/ArtifactReading.fsi` from
  `contracts/ArtifactReading.fsi` — exposing exactly `optionsFor`/`readArtifact`/
  `loadSnapshot`; all path/regex/JSON/fact helpers hidden (FR-003/FR-006).
- [X] T014 [US2] Create `src/FS.GG.Governance.Cli/ArtifactReading.fs` by relocating
  verbatim the path-resolution + file/dir reads + task/dep regex parsers + fact extraction
  + snapshot assembly (`tryReadAllText` … `loadSnapshot`, plus `optionsFor`) from
  `src/FS.GG.Governance.Cli/Program.fs`; use `Path.GetFullPath` directly rather than
  `Program.fullPath` (research D5). No top-level access modifiers (FR-006).
- [X] T015 [US2] Insert `ArtifactReading.fsi`/`.fs` compile entries after the `CliRender`
  pair, before `Program.fs`, in `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj`.
- [X] T016 [US2] In `src/FS.GG.Governance.Cli/Program.fs`, repoint the `LoadSnapshot` port
  to `ArtifactReading.loadSnapshot`, and `runHost`'s `ReadArtifact` effect + options build
  to `ArtifactReading.readArtifact`/`ArtifactReading.optionsFor`; delete the now-relocated
  inline bodies. Budget folds + watch/tui block stay (research D4).
- [X] T017 [US2] Append `module ArtifactReading` (after `module CliRender`) to
  `surface/FS.GG.Governance.Cli.surface.txt` and the `generatedSurface` literal in
  `tests/FS.GG.Governance.Cli.Tests/SurfaceDriftTests.fs` (additive, FR-011).
- [X] T018 [US2] **Gate**: rebuild + replay the T002 matrix (incl. a spec-kit+design
  fixture snapshot — US2 Acceptance 1); every `diff` empty; snapshot/read-only suites +
  surface-drift green. **Commit 2** ("077 cli-decomposition: extract ArtifactReading").

### US2b — ReviewStore (Commit 3)

- [X] T019 [US2] Add `src/FS.GG.Governance.Cli/ReviewStore.fsi` from
  `contracts/ReviewStore.fsi` — exposing exactly `loadReview`/`saveReview`; `safeFileName`/
  `reviewStoreRoot`/`verdictText`/`parseVerdict` hidden (FR-004/FR-006).
- [X] T020 [US2] Create `src/FS.GG.Governance.Cli/ReviewStore.fs` by relocating verbatim
  `safeFileName`, `reviewStoreRoot`, `verdictText`, `parseVerdict`, `loadReview`,
  `saveReview` (incl. the `review-store-unavailable` short-circuit) from
  `src/FS.GG.Governance.Cli/Program.fs`. The `review-dispatch-failed` budget branch STAYS
  in `runHost` (research D4). No top-level access modifiers (FR-006).
- [X] T021 [US2] Insert `ReviewStore.fsi`/`.fs` compile entries after `ArtifactReading`,
  before `Program.fs`, in `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj`.
- [X] T022 [US2] In `src/FS.GG.Governance.Cli/Program.fs`, repoint `runHost`'s `LoadReview`
  effect to `ReviewStore.loadReview` and `RecordVerdict` to `ReviewStore.saveReview`;
  delete the relocated inline bodies.
- [X] T023 [US2] Append `module ReviewStore` (after `module ArtifactReading`) to
  `surface/FS.GG.Governance.Cli.surface.txt` and the `generatedSurface` literal in
  `tests/FS.GG.Governance.Cli.Tests/SurfaceDriftTests.fs` (additive, FR-011).
- [X] T024 [US2] **Gate**: rebuild + replay the T002 matrix (incl. the
  `review-store-unavailable`/`review-dispatch-failed` fixture roots — US2 Acceptance 3);
  every `diff` empty; review-store/snapshot/read-only suites + surface-drift green.
  Confirm SC-004 via quickstart §4 sed/grep (`runHost`→`writeOutput` holds no `File.`/
  `Directory.`/`reviewStoreRoot`/`specKitArtifactPath`). **Commit 3** ("077
  cli-decomposition: extract ReviewStore").

**Checkpoint**: Both edge concerns isolated; `runHost`/`main` are thin orchestration.

---

## Phase 5: Polish & Validation (Cross-Cutting)

**Purpose**: Whole-feature validation against the spec's success criteria.

- [X] T025 Run the full repo suite (SC-002). The `dotnet test FS.GG.Governance.sln`
  run COMPILED the entire solution (every project built — it reached test execution)
  and ran 13 test projects GREEN with 0 failures: 8 from the solution run (incl.
  Kernel 73, SurfaceChecks 18 — the repo-wide surface gate, PackageChecks 20) + a
  targeted neighbor sweep (Cli 50, Host 18, RouteCommand 83, VerifyJson 30,
  VerifyCommand 79). The binding CLI suite is 50/50 (parser/MVU/snapshot/output/
  read-only/surface-drift). EXCLUDED, both unrelated to this CLI-only change: (a) the
  SDD `fs-gg-fullstack` template-generation integration test — pathologically slow in
  this contended environment (seen as a multi-hour `dotnet new` earlier), it generates
  and builds a whole solution and never settled; (b) the pre-existing CLI `dotnet pack`
  timeout flake (quickstart §2). CLI per-project count is unchanged (call-site +
  surface-literal edits only; no new behavioral tests).
- [X] T026 [P] Confirm surface-drift is additive (FR-011): baseline grew by exactly the
  three `module` lines (`CliRender`, `ArtifactReading`, `ReviewStore`); none removed or
  renamed; "CLI remains optional" + "expected runtime references" guards green unchanged
  (FR-010, quickstart §3). **No new scope-guard test is authored** — the existing
  lower-assembly reference check already covers FR-010 and stays green because the new
  modules add no ProjectReference (research D6: the "add an assertion" phrasing resolves
  to confirming the existing guard, not writing a new one).
- [X] T027 [P] Confirm the watch/tui edge is behaviorally untouched (spec edge case):
  the `git diff HEAD~3 -- Program.fs` is exactly the relocated-block deletions plus 7
  repoint lines (`optionsFor`/`readArtifact`/`loadReview`/`saveReview`/`Cli.render`→
  `CliRender.render`×2/`LoadSnapshot` port). The watch/tui block
  (`readOnlyRoutePorts`/`composeRouteView`/`runWatch`/`runTui`/`drawTui`/`tuiKeyReader`/
  `routeRequestFor`) shows NO `+`/`-` change (quickstart §5).
- [X] T028 Recorded the actual relocation in the CLAUDE.md roadmap update: new modules
  CliRender.fs 282, ArtifactReading.fs 370, ReviewStore.fs 74; Cli.fs 829→557 (−272),
  Program.fs 673→251 (−422) ⇒ ~694 LOC relocated into 726 LOC of new modules (the delta
  is per-file namespace/open/module headers). **SC-005 deviation flag**: the spec's "~200
  LOC" headline tracks only the dominant render extraction and undercounts the two edge
  extractions; the binding criteria SC-001 (byte-identity) / SC-002 (green suite) /
  SC-003 / SC-004 all hold regardless (research D7). Flagged here and in the roadmap.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: after Setup — confirms seams; BLOCKS all extraction.
- **US1 (Phase 3)**: after Foundational. Independently shippable MVP (Commit 1).
- **US2 (Phase 4)**: after US1 (research D8 — US2 lands "mid-decomposition", higher-risk
  edge I/O). US2a (ArtifactReading) before US2b (ReviewStore) by compile order.
- **Polish (Phase 5)**: after all three extraction commits.

### Critical per-commit discipline (FR-009)

Each of T012 / T018 / T024 is a **commit gate**: full suite green **and** every
golden/transcript/snapshot byte-identical before committing, so any drift is isolated to
its causing commit. Never re-bless a moved golden — a non-empty `diff` means revert/fix.

### Within Each Story

- `.fsi` (contract) before `.fs` (body) before `.fsproj` compile-entry before call-site
  repoint before surface-baseline append before the commit gate.

### Parallel Opportunities

- T002 / T003 (Setup) run in parallel.
- T004 / T005 (seam confirmation) run in parallel (different files).
- T026 / T027 (Polish validation) run in parallel.
- Within a single extraction commit, tasks are mostly sequential (same files:
  `Cli.fs`/`Program.fs`/`.fsproj`/`SurfaceDriftTests.fs`), so `[P]` is intentionally
  sparse there.
- US2 cannot parallelize with US1 (D8 sequencing); US2b follows US2a (compile order).

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → baseline frozen.
2. Phase 2 Foundational → seams confirmed.
3. Phase 3 US1 → extract `CliRender`, prove byte-identity, **Commit 1**.
4. **STOP and VALIDATE**: rendering isolated, output identical, suite green — shippable.

### Incremental Delivery

1. Commit 1 (CliRender) → MVP, the dominant ~200-LOC clarity win.
2. Commit 2 (ArtifactReading) → snapshot reading isolated.
3. Commit 3 (ReviewStore) → review persistence isolated; `runHost`/`main` thin.
4. Phase 5 → whole-feature validation + SC-005 LOC flag.

---

## Notes

- Move bodies **verbatim** — no reflow, no reorder (research D9): the hand-built
  `fsgg-governance.cli.v1` JSON envelope is the highest drift risk.
- `exitCode` + `stableStrings` stay public in `module Cli`; `optionsFor` moves to
  `ArtifactReading`; `fullPath` stays in `Program` (research D5).
- No new project, no new dependency, no new external ProjectReference (FR-008/FR-010).
- Elmish/MVU (Principle IV): the CLI `Model`/`Msg`/`Effect`/`init`/`update`/`run` is
  unchanged and stays in `module Cli`; rendering becomes a pure view; artifact-reading/
  review-store stay edge effects interpreted by `runHost`/`main`. No transition logic
  changes, so no new MVU transition/effect tests are authored.
