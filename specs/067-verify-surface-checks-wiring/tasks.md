---
description: "Task list — `fsgg verify` Surface-Checks Host Wiring (F24 verify-host wiring)"
---

# Tasks: `fsgg verify` Surface-Checks Host Wiring

**Input**: Design documents from `/specs/067-verify-surface-checks-wiring/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/verify-json-surfacechecks.md](./contracts/verify-json-surfacechecks.md)

**Tests**: Required (Constitution Principle V; the spec defines test scenarios). Real cores and the real
`fsgg verify` host are never mocked; only the edge ports (filesystem product root, the four domain `realPort`s)
operate over a **real temp tree**. Any synthetic input carries `Synthetic` in the test name with a use-site
disclosure.

**Change classification**: **Tier 1** (host observable-output + exit-code change, contracted by the F24
`verify.json` `surfaceChecks` section). Cores are reused verbatim — no public core API changes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file / independent).
- **[Story]**: `[US1]`/`[US2]`/`[US3]` — which user story the task serves. Untagged = shared spine.
- Phases run in sequence; tasks within a phase may run in parallel where marked `[P]`.

---

## Phase 1: Setup & pre-wiring baselines

**Purpose**: Add the (unused-yet) project references, the test fixture builders, and — critically — freeze the
**pre-wiring** `verify.json` byte-identity anchor BEFORE any host behavior changes.

- [X] T001 Add the seven surface ProjectReferences to
  `src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj`:
  `FS.GG.Governance.ProductSurfaces`, `FS.GG.Governance.SurfaceChecks`,
  `FS.GG.Governance.SurfaceChecks.Dispatch`, `FS.GG.Governance.PackageChecks`,
  `FS.GG.Governance.DocsChecks`, `FS.GG.Governance.SkillChecks`, `FS.GG.Governance.DesignChecks`
  (all already-built in-repo projects; **no new external/NuGet dependency** — `Directory.Packages.props`
  unchanged). Build the host; it compiles against the new refs without using them yet.
- [X] T002 [P] [US2] **Freeze the pre-wiring byte-identity golden (US2 anchor).** With the host still on
  `ofVerifyDecisionWithPreview … []`, capture a real-`fsgg verify` `verify.json` over a temp repo with **no**
  declared product surfaces into `tests/FS.GG.Governance.VerifyCommand.Tests/goldens/verify-no-surfaces.json`
  (reuse the existing verify golden if one already encodes the no-surface case). This is the SC-002 anchor that
  proves no regression after the `[] → findings` change. Record the producing commit in a header comment.
- [X] T003 [P] Add temp-tree fixture builders to `tests/FS.GG.Governance.VerifyCommand.Tests/Support.fs`: a
  multi-surface product generator declaring a package surface (with a drift-able baseline + `evidenceTag`), a
  docs surface, a skill surface, and a design surface in `.fsgg/capabilities.yml` (schemaVersion 2), plus
  helpers to (a) make the package baseline **drift** (blocking) and (b) make a surface produce an **advisory**
  finding. Mirror the builders already used by `tests/FS.GG.Governance.SurfaceChecks.Tests/Support.fs` and
  `StandaloneTests.fs`.

**Checkpoint**: New refs compile; the pre-wiring no-surface golden is frozen; fixtures exist.

---

## Phase 2: Tests (write first — MUST FAIL before Phase 3) ⚠️

**Purpose**: Author every behavioral test against the real host, register them, and confirm the fail-before
state (US1/US3 fail because no host runs the checks today; US2 passes already and guards the no-regression
property). Add each new file to the test `.fsproj` **before** `Main.fs` and register in `Main.fs`.

- [X] T004 [P] [US1] `tests/FS.GG.Governance.VerifyCommand.Tests/SurfaceChecksE2ETests.fs` — real-filesystem
  `fsgg verify` via `Interpreter.run` over the T003 drifted-package fixture: assert `verify.json` gains a
  `surfaceChecks` entry `package.baseline-drift` carrying the drift detail and the declared `evidenceTag`, and
  the run exits with the **blocking** exit code at `RunMode.Verify` (contract C2/C3, acceptance 2; SC-001).
  Also assert (FR-006, contract C2) the emitted `surfaceChecks` JSON contains **no absolute path, timestamp,
  username, or environment value**, and `evidenceTag` is **omitted** for a surface that declared none.
  **Must FAIL now** (today: no `surfaceChecks`, exit 0).
- [X] T005 [P] [US2] In `SurfaceChecksE2ETests.fs` — real-filesystem `fsgg verify` over a **no-declared-surface**
  repo: assert `verify.json` is **byte-identical** to `goldens/verify-no-surfaces.json` (T002), the section
  omitted and `schemaVersion` unchanged (contract C1; SC-002). Passes now and after wiring (the guard).
- [X] T006 [P] [US3] In `SurfaceChecksE2ETests.fs` — real-filesystem `fsgg verify` over a repo whose **only**
  surface finding is advisory: assert the entry appears with `"severity":"advisory"` and the exit code equals a
  clean run's exit code (contract C3, acceptance 3; SC-003). **Must FAIL now**.
- [X] T007 [P] [US1] `tests/FS.GG.Governance.VerifyCommand.Tests/SurfaceRollupTests.fs` — pure `update` fold:
  feeding a `SurfacesSensed [blockingFinding]` makes the verdict block at `RunMode.Verify`; `SurfacesSensed
  [advisoryFinding]` leaves the exit code unchanged; the truth table is untouched (assert via
  `enforcementInputOf` + the existing `deriveEffectiveSeverity`, no new constant). **Must FAIL now** (no
  `SurfacesSensed` case exists). Covers Constitution IV pure-transition evidence.
- [X] T008 [US1] **Determinism** in `SurfaceChecksE2ETests.fs`: re-run T004 over unchanged inputs and over a
  fixture rebuilt with surfaces/files created in a different order ⇒ byte-identical `verify.json` (FR-005,
  SC-004). Include the **absent-baseline** case (a declared package surface with no committed baseline): two
  consecutive runs are byte-identical and the working tree is unchanged between them (read-only port — FR-012).
  **Must FAIL now**.
- [X] T009b [P] [US2] **Read-only / no side effect** in `SurfaceChecksE2ETests.fs`: after `fsgg verify` over the
  T003 fixture (incl. a no-baseline package surface and declared transcripts), assert the working tree is
  **unchanged** — no `.baseline` written, no file mutated — and **no process was spawned** (the package port is
  read-only: `WriteBaseline` no-op, `ListTranscripts ⇒ Ok []`). The `package.baseline-absent` blocking finding
  still appears, written nowhere (FR-012, contract C4). **Must FAIL now** (no wiring) — and would fail loudly if
  a future change wired the package `realPort` directly.
- [X] T009 [US3] **Safe-failure** (`Synthetic`-disclosed where needed) in `SurfaceChecksE2ETests.fs`: a declared
  surface whose fact source is unreadable / escapes the product root ⇒ a disclosed sensor outcome (e.g.
  `PathEscapesBounds`), **not** a silent pass and **not** a crash; tool-defect vs missing-input distinguished
  (FR-010, research D6, Constitution VI). **Must FAIL now**.
- [X] T010 Register the two new test files in
  `tests/FS.GG.Governance.VerifyCommand.Tests/FS.GG.Governance.VerifyCommand.Tests.fsproj` (before `Main.fs`)
  and `Main.fs`; run the suite and **record the fail-before evidence** for T004/T006/T007/T008/T009/T009b.

**Checkpoint**: All behavioral tests exist and fail for the right reason (the wiring is absent), except the US2
byte-identity guard which passes.

---

## Phase 3: Foundational MVU wiring (the shared spine — makes Phase 2 pass) 🎯 MVP

**Goal**: Classify → sense the four domains → run `Composition.run` → fold into the rollup → project real
findings. This single spine satisfies US1, US2, and US3. **MVP = US1** (a drifted surface blocks `fsgg verify`).

**Independent Test**: T004 (drifted package blocks) goes green; T005 (no-surface byte-identity) stays green.

### 3a — FSI contract growth (Constitution II)

- [X] T011 In `src/FS.GG.Governance.VerifyCommand/Loop.fsi`: declare the new `Effect.SenseSurfaces of
  scope: RepoSnapshot`, `Msg.SurfacesSensed of findings: SurfaceChecks.Model.SurfaceFinding list`, and the new
  `Model` fields `SurfaceFindings: SurfaceChecks.Model.SurfaceFinding list` (default `[]`) and
  `SurfacesPending: bool` (data-model §2). No access modifiers in `.fs` (Constitution II).
- [X] T012 In `src/FS.GG.Governance.VerifyCommand/Interpreter.fsi`: add a **`SenseSurfaces` port to the `Ports`
  record** — `SenseSurfaces: ProductSurfaceReport -> SurfaceChecks.Model.SurfaceFinding list` — wired in
  `realPorts (repo: string)` mirroring the existing `SenseRelease`/`Execute` ports, so `repo` + `ports.Execute`
  are captured at construction time and tests inject a synthetic port (research D7, resolves analyze U1/A2). The
  effect carries only the scope; the interpreter calls `ports.SenseSurfaces report`. Document that the package
  port inside is **read-only** (FR-012).

### 3b — Pure MVU (`update`/`init`)

- [X] T013 In `src/FS.GG.Governance.VerifyCommand/Loop.fs`: initialize `SurfaceFindings = []` and
  `SurfacesPending = false` in `init`/the model defaults; on `Sensed (Ok snap)` add `SenseSurfaces snap` to the
  emitted effect list and set `SurfacesPending = true` (data-model §3). Depends on T011.
- [X] T014 In `src/FS.GG.Governance.VerifyCommand/Loop.fs`: handle `Msg.SurfacesSensed findings` in `update` —
  set `SurfaceFindings = findings`, `SurfacesPending = false`, and join it into the existing
  all-senses-ready readiness gate so `verify.json` is projected only after surfaces (like the release preview)
  have arrived. Makes T007 pass. Depends on T013.

### 3c — Interpreter edge (Constitution IV — sensing + pure run at the edge)

- [X] T015 In `src/FS.GG.Governance.VerifyCommand/Interpreter.fs`: implement the `SenseSurfaces` **port body**
  (wired in `realPorts repo`, closing over `repo` + `ports.Execute`): load `TypedFacts` from the capability
  config (existing loader), classify via `ProductSurfaces.classify` over the **declared surfaces ∩ verify scope**
  → `ProductSurfaceReport` (research D2, FR-001). The `SenseSurfaces snap` effect handler just calls
  `ports.SenseSurfaces report`. Never sense inside a pure `update`. Depends on T012.
- [X] T016 In `src/FS.GG.Governance.VerifyCommand/Interpreter.fs` (inside the `SenseSurfaces` port): fill the
  `SurfaceChecks.Dispatch.Composition.DomainFactBundle` from `emptyBundle` by sensing each **declared** domain.
  Construct the **read-only package port** (FR-012, research D7):
  `let pkg = { PackageChecks.Interpreter.realPort repo ports.Execute with WriteBaseline = (fun _ _ -> Ok ());
  ListTranscripts = (fun _ -> Ok []) }`, then
  `PackageChecks.Interpreter.sensePackage pkg req`. Docs/Skill/Design use their read-only real ports:
  `DocsChecks.Interpreter.senseDocs (realPort repo) req`,
  `SkillChecks.Interpreter.senseSkill (realPort repo) req`,
  `DesignChecks.Interpreter.senseDesign (realPort repo catalogLayout) req` (resolve `catalogLayout` from config
  or the established default). Build requests via `Composition.requestsOf facts report`. Makes T009b pass.
  Depends on T015.
- [X] T017 In `src/FS.GG.Governance.VerifyCommand/Interpreter.fs`: run
  `Composition.run facts report bundle → SurfaceFinding list` and dispatch `Msg.SurfacesSensed findings`; on an
  unexpected sensing exception degrade to a disclosed diagnostic (no fabricated `surfaceChecks`, no crash —
  research D6, FR-010). Makes T009 pass. Depends on T016.

### 3d — Rollup fold + projection (the contract change)

- [X] T018 In `src/FS.GG.Governance.VerifyCommand/Loop.fs`: at the verify rollup, map each
  `model.SurfaceFindings` element through `SurfaceChecks.Model.enforcementInputOf finding RunMode.Verify
  model.Profile` and feed those `EnforcementInput`s into the **existing** `Ship.rollup` at `RunMode.Verify`
  alongside the gate outcomes, so `deriveEffectiveSeverity` computes the verdict (no truth-table change —
  FR-007, FR-008; data-model §5). Reuse the already-resolved profile (research D4). Makes T004/T006 block/advise
  correctly.
- [X] T019 In `src/FS.GG.Governance.VerifyCommand/Loop.fs`: replace the `[]` placeholder with
  `model.SurfaceFindings` at **both** `VerifyJson.ofVerifyDecisionWithPreview` call sites (the primary
  projection and the degraded/empty path). With `SurfaceFindings = []` the bytes are unchanged (keeps T005/T002
  green); non-empty emits the section per contract C2. Depends on T014, T018.

**Checkpoint**: T004 (blocking), T006 (advisory), T007 (fold), T008 (determinism), T009 (safe-failure),
T009b (read-only/no-write) green; T005/T002 byte-identity green. `fsgg verify` now runs the surface checks
end-to-end, read-only and deterministic (MVP delivered).

---

## Phase 4: Goldens, surface baseline, and the no-regression sweep

- [X] T020 [P] Commit a **non-empty** `verify.json` golden generated from the stable
  `ofVerifyDecisionWithPreview … findings` over the T003 drifted fixture under
  `tests/FS.GG.Governance.VerifyCommand.Tests/goldens/verify-surfacechecks.json`; assert byte-identity in
  `SurfaceChecksE2ETests.fs` (deterministic ordering, contract C2). Closes `059` T052. Depends on Phase 3.
- [X] T021 Re-bless the `VerifyCommand` surface baseline for the additive `Loop`/`Interpreter` surface growth
  (`BLESS_SURFACE=1 dotnet test …`) and confirm `tests/FS.GG.Governance.VerifyCommand.Tests/SurfaceDriftTests.fs`
  is green. Verify the change is **additive** (new `Effect`/`Msg`/`Model` members in `Loop.fsi` + the new
  `Ports.SenseSurfaces` field in `Interpreter.fsi` only).
- [X] T022 [P] Confirm **no other host changed** and **no side effect**: `route.json`/`ship.json`/`release.json`
  goldens byte-identical across the suite; `Directory.Packages.props` unchanged; no pure core gains a
  filesystem/process reference; and a `fsgg verify` run leaves the working tree unchanged (no `.baseline` written
  — read-only port, FR-012) — cross-checks T009b at the suite level (FR-009; quickstart "Constitution-gate
  checks").

---

## Phase 5: Bookkeeping & full gate

- [X] T023 [P] Mark `059` tasks **T045 / T048 / T052** complete in
  `specs/059-package-docs-skills-design-checks/tasks.md`, citing `067-verify-surface-checks-wiring` as the
  host-edge slice that landed the verify surface-checks wiring + goldens.
- [X] T024 [P] Update the roadmap note in `docs/initial-implementation-plan.md`: the `fsgg verify`
  surface-checks wiring is closed (the one product-surface thread that was open on a command host); reconcile
  any Phase 9/10 reference to verify surface checks.
- [X] T025 [P] Update `CLAUDE.md`/README if the verify command's documented behavior enumerates its `verify.json`
  sections — add the additive `surfaceChecks` section (non-contractual prose; JSON contract lives in
  `contracts/`).
- [X] T026 Full-solution gate: `dotnet build FS.GG.Governance.sln` clean (warnings-as-errors) then
  `dotnet test FS.GG.Governance.sln` green (no regression); run the [quickstart.md](./quickstart.md) Scenarios
  1–5 + the constitution-gate checks; record the evidence on this line (SC-005).

---

## Dependencies

- **Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5** run in sequence.
- **T002 (freeze pre-wiring golden) MUST precede any Phase 3 host change** — it is the no-regression anchor.
- Within Phase 3: 3a (FSI) → 3b (`update`) and 3c (interpreter) in parallel → 3d (fold + projection) last.
- T020 (non-empty golden) and T021 (surface re-bless) depend on Phase 3 complete.

## Parallel opportunities

- Phase 1: T002, T003 in parallel (T001 first — refs must compile).
- Phase 2: T004/T005/T006/T007/T009b author in parallel (same + sibling files); T010 registers/serializes them.
- Phase 3: T013–T014 (`update`) ∥ T015–T017 (interpreter) after the FSI (T011/T012); converge at T018/T019.
- Phase 5: T023/T024/T025 in parallel; T026 last.

## MVP scope

**User Story 1** (drifted product surface blocks `fsgg verify`): Phase 1 + Phase 2 (US1 tasks) + Phase 3 (the
full spine) + T020/T021. Delivers the missing governance behavior — the surface checks that run in no host today
now run and are enforced at the verify boundary. US2 (byte-identity) is satisfied for free by the spine's
empty-findings short-circuit; US3 (advisory non-escalation) by the `enforcementInputOf` fold.

## Task count

- **Setup (Phase 1)**: 3 — incl. T002 `[US2]` (pre-wiring anchor)
- **Tests (Phase 2)**: 8 — US1: 3 (T004, T007, T008), US2: 2 (T005, T009b), US3: 2 (T006, T009),
  +1 registration (T010)
- **Foundational spine (Phase 3)**: 9 (T011–T019)
- **Goldens/baseline (Phase 4)**: 3
- **Bookkeeping & gate (Phase 5)**: 4
- **Total**: 27 (added T009b read-only/no-write test in remediation)

---

## Implementation notes (reconciliations & disclosed evidence)

These record where the landed implementation refined the task plan, and every synthetic-evidence use
(Constitution V). All 27 tasks above are `[X]` against the real cores + the real `fsgg verify` host; the full
solution suite is green (no host other than verify changed — FR-009).

- **`SenseSurfaces` effect payload (T011/T012 reconciliation).** The two task lines described the effect as
  carrying a `RepoSnapshot` (T011) yet the port as `ProductSurfaceReport -> SurfaceFinding list` (T012) — a
  `RepoSnapshot` alone cannot be classified (classify needs `TypedFacts` + the `RouteReport`, available only
  after the catalog load). Landed design: classification (`ProductSurfaces.classify`) is **pure**, so it runs
  in `update` at `Loaded(Valid facts)` where those inputs are in hand, and the effect carries the resulting
  **`ProductSurfaceReport`**. The edge port does the read-only sensing + `Composition.run` (Constitution IV
  honoured — only I/O is at the edge). This matches T012's port type exactly.

- **Deferred empty-selection projection (T013/T014).** The "nothing to verify" path now waits for
  `SurfacesSensed` before projecting, so a drifted surface on a change that selects **no gates** is still
  folded and reported. Two pre-existing pure `update` tests (`LoopTests` empty-selection, `ReleasePreviewTests`
  `driveTo`) were updated to drive the extra `SurfacesSensed []` step — a faithful reflection of the new flow,
  not a weakened assertion.

- **SYNTHETIC — advisory E2E (T006).** The real disk sensors emit only `Blocking` findings today; the lone
  advisory check (`docs.example-freshness`) is not yet populated by the real docs `Interpreter`. So T006 injects
  the advisory finding through a synthetic `SenseSurfaces` port (`syntheticSurfaceSense`, disclosed at the use
  site and named in the test). The fold + additive projection are exercised for real; only the finding's origin
  is synthetic. Real-evidence path: when the docs sensor populates example facts from disk, swap to a real
  advisory fixture.

- **Determinism harness for the surface E2Es (T004/T008/T020).** The surface **sensors** run for real over a
  real temp tree (the feature under test — real package/docs/skill/design file reads through the read-only
  package port). Git is faked with **fixed** revisions and the gate exec is faked (the established pattern in
  this suite) so the non-surface portion of `verify.json` is byte-deterministic — real `git` commit SHAs would
  vary run-to-run and defeat the byte-identity goldens. Disclosed in the test header.

- **Goldens.** `goldens/verify-no-surfaces.json` (empty/byte-identity anchor, also cross-checked against the
  genuine `VerifyJson.ofVerifyDecision` projection) and `goldens/verify-surfacechecks.json` (the non-empty
  `surfaceChecks` projection: a gate in `passing` but `verdict:blocked` driven solely by the surface finding —
  the fold) are frozen; refresh with `BLESS_GOLDEN=1 dotnet test`.
