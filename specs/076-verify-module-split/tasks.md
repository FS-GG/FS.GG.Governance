---
description: "Task list for the Verify god-module split (Phase C)"
---

# Tasks: Verify god-module split (Phase C)

**Input**: Design documents from `/specs/076-verify-module-split/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/ ✅ (`verifycommand-seam-modules.md`, `verifyjson-seam-modules.md`)

**Tests**: No NEW behavioral test category is required (spec Assumptions; plan
Constitution Check V). Acceptance instrumentation already exists — byte-identical
goldens/snapshots + green suite + the two reflective surface-drift tests. Tasks
below therefore RUN the existing suites as the per-commit gate rather than
authoring new tests; the one additive structural test is a required scope guard
over the new modules (T022), which discharges Principle I coverage of the new public
seam surface together with the existing golden suites (see Notes §D1).

**Tier**: Tier 1 / contracted — additive public seam-module surface; the two
surface baselines are re-blessed ADDITIVELY once (FR-004). Every command/projection
golden + snapshot stays byte-identical (FR-005/SC-002).

**Elmish/MVU applicability**: `Loop.update` stays the sole owner of `Model`/`Msg`/
`Effect` and is NOT moved (Principle IV, plan Constitution Check IV). The three
extracted host folds are PURE helpers `update`/`projectExecuted` *call* — no
`update` case, `Msg`, `Effect`, or interpreter boundary moves. `Loop.fsi` and the
interpreter/Program are byte-identical. So the MVU-boundary task obligations reduce
to: keep `Loop.fsi` frozen, keep the folds host-`Model`-free (decompose `previewOf`),
and prove transitions unchanged via the existing Loop/EndToEnd/rollup goldens.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: `US1` (host folds), `US2` (projection seams), `US3` (ADR).
- **FR-010 (one seam per commit)** governs Phases 3–4: each seam task is its own
  commit so its test run isolates any golden drift. Seam tasks WITHIN a story edit
  the same host/entry `.fs` file, so they are **sequential, not `[P]`**, even though
  the two stories run in parallel with each other.
- Exact file paths are given in every task.

---

## Phase 1: Setup (capture the acceptance floor)

**Purpose**: Record the pre-refactor baselines that FR-005/FR-006/SC-002/SC-003 are
measured against. No code changes.

- [X] T001 Confirm clean build at the current tip and record the pre-refactor
  per-project test floor: run `dotnet test tests/FS.GG.Governance.VerifyJson.Tests`
  and `dotnet test tests/FS.GG.Governance.VerifyCommand.Tests`; note each pass count
  in the implementation notes. These counts are the FR-006/SC-003 floor (later runs
  MUST be ≥ this, modulo the known Cli `dotnet pack` timeout flake). (quickstart §0)
- [X] T002 [P] Snapshot the two surface baselines and the goldens as the
  byte-identity reference: confirm `surface/FS.GG.Governance.VerifyCommand.surface.txt`
  (640 lines) and `surface/FS.GG.Governance.VerifyJson.surface.txt` (7 lines) are
  committed and clean, and that `tests/FS.GG.Governance.VerifyCommand.Tests/goldens/`
  + the `VerifyJson.Tests` golden fixtures are clean in `git status`. Any later byte
  diff to these is the drift failure signal (FR-005, edge "Golden drift as a failure
  signal").
- [X] T003 [P] Record the current LOC of the two god modules for the SC-001 measure:
  `src/FS.GG.Governance.VerifyCommand/Loop.fs` (~1,009) and
  `src/FS.GG.Governance.VerifyJson/VerifyJson.fs` (~582), so the post-split shrink is
  demonstrable.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: None. This refactor has no shared foundational layer — the seven seams
are independent extractions and the existing test/surface instrumentation is already
in place. US1, US2, and US3 may begin immediately after Phase 1.

**Checkpoint**: Phase 1 baseline captured → all three stories can proceed (US1 ∥ US2
∥ US3).

---

## Phase 3: User Story 1 - Verify host loop reads as base pipeline + opt-in layers (Priority: P1) 🎯 MVP

**Goal**: Extract the three optional host folds (surface-check, view-currency,
release-preview) out of `VerifyCommand/Loop.fs` into named sibling modules called
through explicit seams, leaving `Loop` as the base pipeline (cache-eligibility → gate
execution → cost-budget → provenance). `Loop.fs` measurably shrinks toward Route's
~620 LOC.

**Independent Test**: Build `FS.GG.Governance.VerifyCommand`, run
`tests/FS.GG.Governance.VerifyCommand.Tests` — every command golden + snapshot
byte-identical, full suite green, base `update` no longer references the other
layers' accumulators inline, each fold in its own module. (spec US1)

**Story dependency**: Independent of US2 (different project). Seam tasks T010→T012
are SEQUENTIAL (each edits `Loop.fs` + the `.fsproj` `<Compile>` order). Order is
deferred-cost-ascending: the two clean lifts first, the decomposed one last.

### Implementation for User Story 1 (one seam = one commit, FR-010)

- [X] T010 [US1] Extract the **surface-check fold** into
  `src/FS.GG.Governance.VerifyCommand/SurfaceFold.fsi` + `SurfaceFold.fs` (new public
  module `FS.GG.Governance.VerifyCommand.SurfaceFold`). Draft the `.fsi` FIRST from
  `contracts/verifycommand-seam-modules.md` (`surfaceBlocks`, `foldSurfaceVerdict` —
  pure over `Profile`/`SurfaceFinding list`/`ShipDecision`, already host-`Model`-free;
  verbatim from `Loop.fs:404,416`). Add the `.fsi`/`.fs` pair to
  `FS.GG.Governance.VerifyCommand.fsproj` `<Compile>` **before** `Loop.fs` (research
  D5). Move the bindings; replace the in-place code in `Loop.fs` with calls to the new
  module (change no emitted byte). Build + `dotnet test
  tests/FS.GG.Governance.VerifyCommand.Tests` → all goldens/rollup/SurfaceChecksE2E
  green & byte-identical. Commit `076 …: extract SurfaceFold`.
- [X] T011 [US1] Extract the **view-currency fold** into
  `src/FS.GG.Governance.VerifyCommand/ViewCurrencyFold.fsi` + `ViewCurrencyFold.fs`
  (`…VerifyCommand.ViewCurrencyFold`). `.fsi` first: `viewCurrencyBlocks`,
  `foldViewCurrencyVerdict`, `viewCurrencyDetail` — pure over `Profile`/
  `CurrencyFinding list`; verbatim from `Loop.fs:433,437,451`. **Pin
  `viewCurrencyDetail`'s concrete return type in the `.fsi`** — it is
  `(CurrencyFinding * EnforcementDecision) list` (each finding paired with its
  Verify-mode `EnforcementDecision`, `Loop.fs:451`); do not leave it inferred (C1 /
  contract `.fsi` must be complete). Add the pair to the
  `.fsproj` `<Compile>` **before** `Loop.fs`. Move bindings; rewire `Loop` call sites.
  Build + `dotnet test tests/FS.GG.Governance.VerifyCommand.Tests` → Currency/rollup
  goldens green & byte-identical. Commit `076 …: extract ViewCurrencyFold`.
  (after T010 — same `Loop.fs`/`.fsproj`)
- [X] T012 [US1] Extract the **release-readiness preview** into
  `src/FS.GG.Governance.VerifyCommand/ReleasePreview.fsi` + `ReleasePreview.fs`
  (`…VerifyCommand.ReleasePreview`). `.fsi` first: `previewFrom` lifts verbatim
  (`Loop.fs:481`); `previewOf` (`Loop.fs:495`) takes the host `Model` so **decompose**
  it per the Phase B `baseHeadOf` precedent → expose `previewOf'` taking
  `decl/sensed/snapshot` directly (host-`Model`-free), and keep a ONE-LINE wrapper in
  `Loop` that projects `model.ReleaseDecl`/`model.ReleaseSensed` into the call
  (data-model invariant 3; contracts §ReleasePreview). Add the pair to the `.fsproj`
  `<Compile>` **before** `Loop.fs`. Build + `dotnet test
  tests/FS.GG.Governance.VerifyCommand.Tests` → ReleasePreview/EndToEnd goldens green
  & byte-identical. Commit `076 …: extract ReleasePreview`. (after T011 — same
  `Loop.fs`/`.fsproj`)
- [X] T013 [US1] Verify `Loop.fsi` is **byte-identical** to baseline (`git diff
  src/FS.GG.Governance.VerifyCommand/Loop.fsi` is empty) and that `update`/`Msg`/
  `Effect`/the `Phase` ladder are unchanged — `parse`/`init`/`update`/`render`/
  `exitCode`/`applyExecution`/`kindOf` and `Model`/`Msg`/`Effect`/`ArtifactKind`/
  `ScopeSelector`/`OutputFormat`/`UsageError`/`ExitDecision`/`Phase`/`Diagnostic`
  stay put (FR-004, FR-007, Principle IV). Confirm no host-`Model` leaked into a fold
  `.fsi`. **Acyclicity (FR-007)** holds by construction — the folds are intra-project
  leaves placed *before* `Loop` in the linear `<Compile>` order and add **no new
  `ProjectReference`** — so confirming the unchanged reference set is sufficient; no
  separate cyclic-edge check is needed (E1). Re-run the NoMutation + Determinism
  tests. (after T012)

**Checkpoint**: `VerifyCommand/Loop.fs` is the base pipeline + 3 seam calls; 3 fold
modules exist; every host golden byte-identical; `Loop.fsi` frozen. US1 deliverable
done (surface baseline re-bless is the cross-cutting T019).

---

## Phase 4: User Story 2 - Verify JSON projection split along its four feature seams (Priority: P1)

**Goal**: Split `VerifyJson/VerifyJson.fs` into four seam modules (`Core`,
`SurfaceChecks`, `ReleaseReadiness`, `GeneratedViews`) behind a thin composing
`VerifyJson` entry that keeps `schemaVersion` + the four public entry points
byte-identical and emits the SAME byte stream.

**Independent Test**: Build `FS.GG.Governance.VerifyJson`, run
`tests/FS.GG.Governance.VerifyJson.Tests` — the four entry points keep identical
signatures, every golden + determinism fixture byte-identical, `VerifyJson.fsi`
frozen. (spec US2)

**Story dependency**: Independent of US1 (different project) → Phase 4 may run in
PARALLEL with Phase 3. Seam tasks T014→T017 are SEQUENTIAL (each edits
`VerifyJson.fs` + the `.fsproj` `<Compile>` order). **`Core` MUST go first** — it is
compiled first and the other seams/entry reference its writers (data-model compile
order; research D3).

### Implementation for User Story 2 (one seam = one commit, FR-010)

- [X] T014 [US2] Extract the **core verdict writers** into
  `src/FS.GG.Governance.VerifyJson/Core.fsi` + `Core.fs` (`…VerifyJson.Core`). `.fsi`
  first from `contracts/verifyjson-seam-modules.md`: public `writeCore` (the
  orchestrator the entry calls) only; `verdictToken`, `dispositionToken` (the
  hyphenated `not-executed` divergence — STAYS local, NOT re-unified with
  `JsonTokens`, FR-009/research D4), `writeCauseValue`/`writeEnforcement`/`writeCache`/
  `writeExecution`/`writeItem`/`writeSection`/`gateItemIds`/`writeCurrency` stay
  private (absent from `.fsi`). Bindings from `VerifyJson.fs:53–221,444`. Add the pair
  to `FS.GG.Governance.VerifyJson.fsproj` `<Compile>` **first** (before any other
  seam and before `VerifyJson.fs`). Rewrite the entry bodies to call `Core.writeCore`
  in the identical order. Build + `dotnet test
  tests/FS.GG.Governance.VerifyJson.Tests` → GoldenTests/DeterminismTests
  byte-identical. Commit `076 …: extract VerifyJson.Core`.
- [X] T015 [US2] Extract the **surface-checks writer** into
  `src/FS.GG.Governance.VerifyJson/SurfaceChecks.fsi` + `SurfaceChecks.fs`
  (`…VerifyJson.SurfaceChecks`). `.fsi` first: public `writeSurfaceFinding`
  (`VerifyJson.fs:288`). Add to `.fsproj` `<Compile>` after `Core`, before
  `VerifyJson.fs`. Rewire `ofVerifyDecisionWithSurfaceChecks` to call it under the
  same `if not (List.isEmpty findings)` guard. Build + `dotnet test
  tests/FS.GG.Governance.VerifyJson.Tests` → SurfaceChecksEmbed golden byte-identical.
  Commit `076 …: extract VerifyJson.SurfaceChecks`. (after T014 — depends on `Core`;
  same `VerifyJson.fs`/`.fsproj`)
- [X] T016 [US2] Extract the **release-readiness writers** into
  `src/FS.GG.Governance.VerifyJson/ReleaseReadiness.fsi` + `ReleaseReadiness.fs`
  (`…VerifyJson.ReleaseReadiness`). `.fsi` first: public `writeReleaseReadiness` only
  (`VerifyJson.fs:432`); the `rr*` + `writePackProject`/`writePackageEvidence`/
  `writeVersionPolicy`/`writeAttestationRef` helpers (`VerifyJson.fs:317–431`) stay
  private (minimal additive surface, FR-004). Add to `.fsproj` `<Compile>` after
  `Core`, before `VerifyJson.fs`. Rewire `ofVerifyDecisionWithPreview` to call it
  under the same `match preview with Some p -> … | None -> ()` guard. Build + `dotnet
  test tests/FS.GG.Governance.VerifyJson.Tests` → ReleaseReadinessPreview golden
  byte-identical. Commit `076 …: extract VerifyJson.ReleaseReadiness`. (after T014;
  same `VerifyJson.fs`/`.fsproj`)
- [X] T017 [US2] Extract the **generated-views writers** into
  `src/FS.GG.Governance.VerifyJson/GeneratedViews.fsi` + `GeneratedViews.fs`
  (`…VerifyJson.GeneratedViews`). `.fsi` first: public `writeGeneratedViews`
  (`VerifyJson.fs:550`); `writeGeneratedView` (`:528`) stays private. Add to `.fsproj`
  `<Compile>` after `Core`, before `VerifyJson.fs`. Rewire
  `ofVerifyDecisionWithGeneratedViews` under the same `if not (List.isEmpty
  generatedViews)` guard. Build + `dotnet test
  tests/FS.GG.Governance.VerifyJson.Tests` → generated-views golden byte-identical.
  Commit `076 …: extract VerifyJson.GeneratedViews`. (after T014; same
  `VerifyJson.fs`/`.fsproj`)
- [X] T018 [US2] Verify `src/FS.GG.Governance.VerifyJson/VerifyJson.fsi` is
  **byte-identical** to baseline (`git diff` empty) — `schemaVersion` + the four entry
  points keep identical names/signatures (FR-003/FR-004/SC-004) — and that
  `VerifyJson.fs` is now a thin composition over the four seams (each entry appends
  writers in the same wire order, byte-identity argument research D3). Confirm no new
  `ProjectReference` and the library stays `System.*`/`FSharp.Core`-only (FR-007).
  **Acyclicity (FR-007)** holds by construction — `Core`/`SurfaceChecks`/
  `ReleaseReadiness`/`GeneratedViews` are leaves placed *before* `VerifyJson.fs` in
  the linear `<Compile>` order with no new project edge — so the unchanged reference
  set is sufficient evidence; no separate cyclic-edge check is needed (E1). (after
  T017)

**Checkpoint**: `VerifyJson.fs` is a thin entry over 4 seam modules; four entry
points frozen; every projection golden byte-identical.

---

## Phase 5: User Story 3 - GateRunHost unification decision recorded as an ADR (Priority: P2)

**Goal**: Record the pursue/defer/drop verdict on the `route → ship → verify`
`GateRunHost` unification as a numbered ADR. Documentation only — no code change.

**Independent Test**: Exactly one new ADR exists under `docs/decisions/` taking an
unambiguous position with rationale referencing Phase B's clean golden diff; route/
ship/verify host skeletons untouched. (spec US3)

**Story dependency**: Fully independent (touches only `docs/`) → may run in parallel
with US1/US2 at any time.

### Implementation for User Story 3

- [X] T019 [P] [US3] Write `docs/decisions/0003-gaterunhost-unification.md` recording
  the verdict **DEFER** (research D6) on unifying `route`/`ship`/`verify` into one
  parameterized `GateRunHost`. State: the gate to *consider* it is satisfied (Phase B
  `075` shipped byte-identical, CLAUDE.md/roadmap); why defer (a full `GateRunHost`
  strictly contains and exceeds the already-deferred `exitCode`+`ExitDecision`
  six-host surface cascade, and Phase C's committed scope does not require it,
  FR-008); the re-entry condition (revisit when the deferred `exitCode`/`ExitDecision`
  adoption lands or a fourth gate-run host appears); and the consequence (route/ship/
  verify left unchanged). Use the next free ADR number (confirm `0003` is unused under
  `docs/decisions/`). (SC-005, FR-008)
- [X] T020 [P] [US3] Confirm the FR-008 second clause: `git diff --stat
  src/FS.GG.Governance.RouteCommand src/FS.GG.Governance.ShipCommand` shows NO changes
  — the deferral leaves the sibling host skeletons untouched. (quickstart §4)

**Checkpoint**: One ADR under `docs/decisions/`; route/ship untouched.

---

## Phase 6: Polish & Cross-Cutting (after US1 + US2 seams land)

**Purpose**: The one sanctioned baseline edit (additive re-bless spanning both
projects) and the whole-solution acceptance gate. These DEPEND on all seam tasks
(T010–T017) being complete.

- [X] T021 Re-bless BOTH surface baselines ONCE, additively (the only sanctioned
  baseline edit): `BLESS_SURFACE=1 dotnet test
  tests/FS.GG.Governance.VerifyJson.Tests` and `BLESS_SURFACE=1 dotnet test
  tests/FS.GG.Governance.VerifyCommand.Tests`, then `git diff` the two
  `surface/FS.GG.Governance.Verify*.surface.txt` files. The diff MUST be purely
  ADDED `…CoreModule`/`…SurfaceChecksModule`/`…ReleaseReadinessModule`/
  `…GeneratedViewsModule` (projection) + `…SurfaceFoldModule`/`…ViewCurrencyFoldModule`/
  `…ReleasePreviewModule` (host) blocks — the existing `VerifyJsonModule` and
  `LoopModule` blocks UNCHANGED (FR-004). If any existing line moved, an existing
  surface widened → fix before committing. Re-run both drift tests with no
  `BLESS_SURFACE` → green. (quickstart §2; depends on T010–T017)
- [X] T022 [P] Add a scope-guard structural test over the new seam modules (the Phase
  B/D precedent) asserting the additive module set and no host-`Model` edge into a
  projection seam, in the relevant `*.Tests` project — raising per-project counts
  additively (FR-006 allows; none silently lost). This is the **per-module assertion**
  that, together with the existing golden/determinism suites, discharges the new
  public seam surface under Principle I (see D1 below): the new modules' public
  functions are exercised **transitively** through the host/entry goldens that already
  call them (plan.md Constitution Check §I; spec Assumptions waive a new behavioral
  test category), and their **presence** is pinned by the re-blessed surface-drift
  baseline (T021); this task adds the structural per-module guard so the additive set
  is asserted directly rather than only by the aggregate drift baseline.
- [X] T023 Full-solution gate: `dotnet test` (whole solution). Expect green save the
  known Cli `dotnet pack` timeout flake; per-project counts ≥ the T001 floor
  (SC-003). (quickstart §3; depends on T021)
- [X] T024 Run the quickstart "Done when" checklist end-to-end (quickstart §"Done
  when"): byte-identical goldens/snapshots (SC-002), additive-only baselines
  (FR-004), four entry points frozen (FR-003/SC-004), `Loop.fs` measurably smaller +
  3 host folds & 4 projection seams in their own modules (SC-001/SC-006), suite green
  ≥ floor (SC-003), exactly one ADR + route/ship/verify untouched (SC-005). Record the
  post-split LOC of `Loop.fs`/`VerifyJson.fs` against the T003 figures for SC-001.
  **Concrete SC-001 bar** (B1): the post-split `src/FS.GG.Governance.VerifyCommand/
  Loop.fs` MUST be **≤ 800 LOC** (down from 1,009, ≥ ~200 LOC moved, trending toward
  Route's 622), and **all three** host folds + **all four** projection seams MUST be
  separate `.fs` files. **Aggregate-LOC note** (F1): SC-001 measures the **per-file**
  shrink of the two god modules — the **aggregate** LOC across the project MAY rise
  (new module headers + `.fsi` files; spec Assumptions L112, plan Scale/Scope). An
  aggregate rise is expected and is NOT an SC-001 failure; only a failure of the
  per-file `Loop.fs ≤ 800` bar is.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: empty (no blocking layer).
- **Phases 3 / 4 / 5 (US1 / US2 / US3)**: all start after Phase 1.
  - US1 (Phase 3) ∥ US2 (Phase 4) ∥ US3 (Phase 5) — different projects / `docs/`.
- **Phase 6 (Polish)**: T021 + T023 + T024 depend on all seam tasks T010–T017; T022
  (now required) is independent and may run as soon as the seam modules exist.

### Within each story (FR-010: one seam per commit)

- **US1**: T010 → T011 → T012 sequential (each edits `Loop.fs` + `.fsproj`), then
  T013 verify. Order ascends by lift cost (clean lifts before the decomposed
  `previewOf'`).
- **US2**: **T014 (`Core`) first** (compiled first; siblings reference it), then T015 /
  T016 / T017 each after T014 but sequential among themselves (same `VerifyJson.fs` +
  `.fsproj`), then T018 verify.
- **US3**: T019 then T020 (verification of the deferral).

### Parallel Opportunities

- T002, T003 (Phase 1) run in parallel with each other.
- Whole stories run in parallel: a dev on US1 (`VerifyCommand`), a dev on US2
  (`VerifyJson`), a dev on US3 (`docs/`) — disjoint files.
- T019, T020 (US3) and T022 (scope guard) are `[P]`.
- Seam tasks WITHIN a story are NOT `[P]` — they share the host/entry `.fs` and the
  `.fsproj` `<Compile>` list.

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 (T001–T003) — capture the floor.
2. Phase 3 (T010–T013) — extract the three host folds, one commit each, goldens
   byte-identical at every commit.
3. Re-bless the VerifyCommand surface (the host slice of T021) and run T023.
4. **STOP & VALIDATE**: `Loop.fs` shrunk toward Route size; host goldens
   byte-identical; `Loop.fsi` frozen. US1 is independently shippable.

### Incremental Delivery

US1 (host folds) → US2 (projection seams) → US3 (ADR) → Polish. Each story leaves the
suite green and every golden byte-identical; the single additive baseline re-bless
(T021) lands once both code stories' seams exist.

---

## Notes

- **Acceptance is byte-identity.** Any byte diff to a command/projection golden or
  snapshot means the extraction changed behavior → investigate and REVERT, never
  re-baseline (FR-005, edge "Golden drift as a failure signal"). The ONLY sanctioned
  baseline edit is the single additive surface re-bless (T021).
- **`.fsi`-first** every seam: draft the curated `.fsi` from `contracts/` before the
  `.fs`; the `.fs` body carries no `private`/`internal`/`public` on top-level
  bindings (Principle II; the `.fsi` is the sole visibility declaration).
- **Folds stay host-`Model`-free** (decompose `previewOf` → `previewOf'`); `update`
  remains the sole `Model` owner (Principle IV / data-model invariant 3).
- **FR-009 divergence**: `dispositionToken`/`verdictToken` stay local in
  `VerifyJson.Core` (NOT re-unified with `JsonTokens`); this is already recorded in
  research D4 — no new research note needed.
- **D1 — Principle I coverage of the new public surface**: the seven new modules gain
  public surface but get **no direct semantic/FSI test of their own functions**.
  That is intentional and pre-justified: their public functions are exercised
  **transitively** by the existing golden/determinism suites that already call the
  composing entry points (`Loop`'s `update`/`projectExecuted`; `VerifyJson`'s four
  entry points), per plan.md Constitution Check §I, and spec Assumptions waive a new
  behavioral test category. Principle II is satisfied by the `.fsi`-first seams + the
  re-blessed surface baseline (T021); the now-required T022 adds the direct
  per-module structural assertion. A golden byte-diff at any seam commit is the
  failing-before/passing-after evidence (Principle V).
- Commit after each seam task; one seam per commit (FR-010).

## Implementation deviations (recorded honestly — Principle V)

- **SC-001 concrete ≤800 bar (T024 / B1) — NOT MET; flagged.** Post-split LOC:
  `VerifyCommand/Loop.fs` **1,009 → 946** (−63); `VerifyJson/VerifyJson.fs` **582 → 122**
  (−460). The per-file shrink is real and directional ("measurably smaller", SC-001
  prose; SC-006 — all 3 host folds + all 4 projection seams are separate `.fs` files),
  but `Loop.fs` does **not** reach the T024 concrete ≤800 figure. The three contracted
  host folds (research D2: `SurfaceFold`/`ViewCurrencyFold`/`ReleasePreview`) are
  genuinely small (~65 LOC net). Reaching ≤800 would require extracting pipeline bodies
  (`verifyPlan`/`tryExecute`/`projectEmpty`/`projectExecuted`) that D2 explicitly KEEPS
  in `Loop` and that the additive-only re-bless (FR-004, exactly three new host module
  types) expects to remain — so the ≤800 number is internally inconsistent with the
  committed three-fold scope. Honored the contracted scope (D2/contracts/FR-004) and
  recorded the gap rather than widening the surface to game the number. A follow-up may
  extract more of the pipeline (a larger Tier-1 surface change) if ≤800 is required.
- Every OTHER Done-when criterion is met: byte-identical goldens/snapshots (SC-002),
  additive-only baselines (FR-004), four entry points + `Loop.fsi`/`VerifyJson.fsi`
  frozen (FR-003/SC-004), suites ≥ floor (SC-003: VerifyJson 30≥28, VerifyCommand
  79≥77), exactly one ADR + route/ship/verify untouched (SC-005).
