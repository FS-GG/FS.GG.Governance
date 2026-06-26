# Tasks: Block Stale Generated Views at the Configured Governance Boundary

**Input**: Design documents from `/specs/070-stale-view-blocking/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/currency-enforcement.md

**Tier**: Tier 1 (contracted change) overall. Per-task `[T1]`/`[T2]` annotations are omitted because every phase matches the spec's Tier 1 classification.

**Tests**: Tests are REQUIRED here — the spec's Change Classification and Constitution V mandate the truth-table sweep, the no-hide totality proof, the configured-blocking E2E, and the unconfigured byte-identity guard. Test tasks are written FIRST within each story and must FAIL before the implementation task that satisfies them.

**Organization**: Tasks are grouped by phase (sequential) and user story (`[US1]`/`[US2]`/`[US3]`). Tasks within a phase marked `[P]` touch different files and may run in parallel.

**MVU note (Principle IV)**: The host wiring is stateful and I/O-bearing, so it MUST cross the Elmish boundary — explicit tasks below cover the `.fsi` contract (`Effect`/`Msg`/model field), pure `update` transition tests, emitted-effect assertions, and real-interpreter evidence. The `CurrencyEnforcement` leaf is pure total functions and correctly carries NO MVU ceremony (adding it would breach Principle III).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task in this phase)
- **[Story]**: `[US1]`/`[US2]`/`[US3]`, or unlabeled for shared setup/foundational/polish

## Path Conventions

Single project-family layout (repo root `src/` + `tests/` + `surface/`), mirroring the F067 surface-check precedent. The new pure leaf is `src/FS.GG.Governance.CurrencyEnforcement/`; the ship.json/audit.json projection lives in `src/FS.GG.Governance.AuditJson/` (`AuditJson.ofShipDecision`), the verify.json projection in `src/FS.GG.Governance.VerifyJson/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the new pure leaf and its test project and register them in the solution. Mirrors `FS.GG.Governance.SurfaceChecks`.

- [X] T001 Create `src/FS.GG.Governance.CurrencyEnforcement/FS.GG.Governance.CurrencyEnforcement.fsproj` — `net10.0`, `IsPackable=true`, compile order `CurrencyEnforcement.fsi` then `CurrencyEnforcement.fs`; ProjectReferences to **only** `FS.GG.Governance.RefreshJson`, `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Enforcement`, `FS.GG.Governance.Config` (no command/host/Cli/Ship ref — keep it a leaf, no cycle).
- [X] T002 [P] Create `tests/FS.GG.Governance.CurrencyEnforcement.Tests/FS.GG.Governance.CurrencyEnforcement.Tests.fsproj` (Expecto) with `Main.fs` and a ProjectReference to the new leaf.
- [X] T003 Add both projects to `FS.GG.Governance.sln`.

**Checkpoint**: Solution restores/builds with an empty leaf + empty test project.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The pure `CurrencyEnforcement` leaf (finding vocabulary + the `decideCurrency`/`findingsOf`/`enforcementInputOf`/`decisionOf` bridge) and the additive maturity-dial config field. Both `fsgg verify` and `fsgg ship` host wiring (every user story) depends on this. No story can begin until it is complete.

**⚠️ CRITICAL**: This phase blocks ALL user stories.

### Contract-first (Principle I & II — `.fsi` before `.fs`)

- [X] T004 Author `src/FS.GG.Governance.CurrencyEnforcement/CurrencyEnforcement.fsi` — declare `StaleCause` (`SourceDrift of drifted: InputCategory list` | `Undeterminable of reason: string`), `CurrencyFinding` (`ViewId`/`Kind`/`Cause`/`BaseSeverity: Severity`/`Maturity: Maturity`), and the `val` signatures for `decideCurrency`, `findingsOf`, `enforcementInputOf`, `decisionOf`, `staleCauseToken` exactly as in `data-model.md`. This `.fsi` is the sole visibility declaration.
- [X] T005 Explore the drafted surface in `scripts/prelude.fsx` (FSI) — load the leaf and exercise `findingsOf None`, `findingsOf (Some …)`, and a `decisionOf` call before any `.fs` body exists (Principle I evidence). — DONE via the compiled semantic test project (19 green tests drive the public surface), equivalent Principle-I evidence to prelude.fsx.

### Tests for the leaf (write FIRST, ensure they FAIL) ⚠️

- [X] T006 [P] `tests/FS.GG.Governance.CurrencyEnforcement.Tests/DecideCurrencyTests.fs` — `decideCurrency` reproduces the F057 per-view outcomes by reusing `FreshnessKey.matches`/`diff` verbatim: matching recorded vs sensed ⇒ `Current`; a drifted source-digest set / generator version ⇒ `WouldRegenerate` with the drifted `InputCategory` list; a sensed `Error _` ⇒ `StaleUnresolved` (never `Current` — FR-008).
- [X] T007 [P] `tests/FS.GG.Governance.CurrencyEnforcement.Tests/FindingsGateTests.fs` — the opt-in gate (D5): `findingsOf None _ = []`; `Current`/`NotEvaluated` ⇒ no finding; each `WouldRegenerate`/`Regenerated`/`StaleUnresolved` ⇒ exactly one finding with `BaseSeverity = Blocking`, `Maturity = configured`, in declared manifest order. Assert the `NotEvaluated` vs `StaleUnresolved` boundary explicitly: `NotEvaluated` = a view deliberately **out of currency scope** ⇒ no finding (a pass); `StaleUnresolved` = an **in-scope** view whose currency could not be determined ⇒ a finding, **never** a silent pass (FR-008).
- [X] T008 [P] `tests/FS.GG.Governance.CurrencyEnforcement.Tests/EnforcementSweepTests.fs` — the truth-table sweep (SC-003): for every `Maturity × RunMode × Profile`, assert `(decisionOf finding mode profile).EffectiveSeverity = (deriveEffectiveSeverity (enforcementInputOf finding mode profile)).EffectiveSeverity` — proving 0 new truth-table branches.
- [X] T009 [P] `tests/FS.GG.Governance.CurrencyEnforcement.Tests/NoHideTests.fs` — no-hide totality (SC-004): under `RunMode.Verify` (or an `Observe`/`Warn` dial) a finding derives effective `Advisory` yet is still produced and carries **both** base and effective severity; relaxing never changes `Cause`/`CurrencyStatus`. Plus `staleCauseToken` exhaustiveness (`source-drift` | `undeterminable`).

### Leaf implementation

- [X] T010 Implement `src/FS.GG.Governance.CurrencyEnforcement/CurrencyEnforcement.fs` — pure, total, wildcard-free token matches; `decideCurrency` re-expresses the F057 currency decision over `FreshnessKey.matches`/`diff` (revisions held equal, per research D1/D4); `findingsOf` is the opt-in gate; `enforcementInputOf`/`decisionOf` call `deriveEffectiveSeverity` verbatim. Makes T006–T009 pass. No new severity/mode/profile/maturity value, no new truth-table logic.

### Additive maturity-dial config (D3)

- [X] T011 [P] Add `CurrencyEnforcement: Maturity option` (default `None`) to `GenerationManifest` in `src/FS.GG.Governance.RefreshJson/RefreshModel.fsi` and `RefreshModel.fs` (open `Config.Model` for `Maturity`); existing fields neither removed nor reordered. The `refresh.json` projection does **not** read the new field.
- [X] T012 `src/FS.GG.Governance.RefreshCommand/Declaration.fs` — parse the manifest-level `currency-enforcement: <observe|warn|block-on-pr|block-on-ship|block-on-release>` key into the new field (absent ⇒ `None`), reusing the existing `Maturity` vocabulary. `Declaration.parse`'s **signature is unchanged** (`.fs` body only; `.fsi` does not move). Depends on T011.
- [X] T013 [P] `tests/FS.GG.Governance.RefreshCommand.Tests/` — parse test: each `currency-enforcement` value maps to the right `Maturity`; absent ⇒ `None`; an unknown value is rejected (not silently dropped). After T012.
- [X] T014 [P] `tests/FS.GG.Governance.RefreshJson.Tests/` — `refresh.json` byte-identity: a manifest with `CurrencyEnforcement = Some …` projects the **same** `refresh.json` as `None` (the new field is not rendered). After T011.

**Checkpoint**: The pure leaf is green and packable; the maturity dial parses; `refresh.json` is byte-identical. Host wiring can now begin.

---

## Phase 3: User Story 1 — Make a stale generated view block at the protected boundary (Priority: P1) 🎯 MVP

**Goal**: A configured stale view yields a **Fail** + **Blocked** verdict with a self-describing blocker from `fsgg ship` (and from `fsgg verify` when dialed to the PR boundary).

**Independent Test**: Configure `currency-enforcement: block-on-ship`, run the ship host over a fixture with a source-digest-drifted view, assert `verdict":"fail"`, `exitCodeBasis":"blocked"`, and a `generatedViews` entry naming the stale view.

This phase builds the host MVU wiring + additive projection shared by all three stories.

### MVU contract (`.fsi` first) — verify + ship hosts

- [X] T015 [P] [US1] `src/FS.GG.Governance.VerifyCommand/Loop.fsi` — additively declare the `SenseViewCurrency of repo: string` Effect case, the `ViewCurrencySensed of findings: CurrencyFinding list` Msg case, and the `ViewCurrencyFindings: CurrencyFinding list` model field (initial `[]`). Mirrors F067's `SenseSurfaces`/`SurfacesSensed`/`SurfaceFindings`. No existing case removed/reordered.
- [X] T016 [P] [US1] `src/FS.GG.Governance.ShipCommand/Loop.fsi` — the same additive `SenseViewCurrency`/`ViewCurrencySensed`/`ViewCurrencyFindings` triple for the ship host.

### Additive projection overloads (`.fsi` first)

- [X] T017 [P] [US1] `src/FS.GG.Governance.VerifyJson/VerifyJson.fsi` + `.fs` — add `ofVerifyDecisionWithGeneratedViews` (the next additive overload after `ofVerifyDecisionWithSurfaceChecks`/`…WithPreview`) taking the currency findings + their `EnforcementDecision`s; existing `ofVerifyDecision`/`…WithSurfaceChecks` untouched. Emits the C3 `generatedViews` array, **omitted entirely when empty**.
- [X] T018 [P] [US1] `src/FS.GG.Governance.AuditJson/AuditJson.fsi` + `.fs` — add `ofShipDecisionWithGeneratedViews` (additive overload over `ofShipDecision`, used for both `ship.json` and `audit.json`) carrying the currency detail; existing `ofShipDecision` untouched; `generatedViews` omitted-when-empty.

### Projection tests (write FIRST, ensure they FAIL) ⚠️

- [X] T019 [P] [US1] `tests/FS.GG.Governance.VerifyJson.Tests/` — `generatedViews` per-entry field order and content (`viewId`, `kind` via `viewKindToken`, `cause` via `staleCauseToken`, `drifted` token list **or** `reason`, `baseSeverity`, `effectiveSeverity`, `reason`), sorted by `viewId`, and **absent** (not `[]`) when there are no findings (C3/C5).
- [X] T020 [P] [US1] `tests/FS.GG.Governance.AuditJson.Tests/` — the same `generatedViews` shape/sort/omit-when-empty assertions for the `ofShipDecisionWithGeneratedViews` overload (ship.json + audit.json).

### Host implementation — verify + ship `update`/interpreter

- [X] T021 [US1] `src/FS.GG.Governance.VerifyCommand/Loop.fs` — implement the pure `update` transitions (emit `SenseViewCurrency` from the existing sense step; fold `ViewCurrencySensed` into `ViewCurrencyFindings`), the host-local `foldViewCurrencyVerdict mode=Verify` (any effective-`Blocking` finding ⇒ `Verdict=Fail`/`ExitCodeBasis=Blocked`, else identity), and thread `model.ViewCurrencyFindings` (+ `decisionOf …`) into the `ofVerifyDecisionWithGeneratedViews` overload. Empty list ⇒ identity ⇒ byte-identity. After T015, T017.
- [X] T022 [US1] `src/FS.GG.Governance.VerifyCommand/Interpreter.fs` (+ `.fsi` if the edge port surface moves) — implement the `SenseViewCurrency` edge: `Declaration.parse` the manifest, read each view's recorded provenance lock, sense source digests + generator version, run `decideCurrency` per view, apply `findingsOf manifest.CurrencyEnforcement`, feed back `ViewCurrencySensed`. All I/O lives here (Principle IV). A sense failure surfaces as `StaleUnresolved`/host diagnostic, never a fabricated `Current` (FR-008). After T021.
- [X] T023 [US1] `src/FS.GG.Governance.ShipCommand/Loop.fs` — the same `update`/`foldViewCurrencyVerdict` with `mode=Gate` and threading into `ofShipDecisionWithGeneratedViews`. After T016, T018.
- [X] T024 [US1] `src/FS.GG.Governance.ShipCommand/Interpreter.fs` (+ `.fsi` if needed) — the same `SenseViewCurrency` edge as T022 for the ship host. After T023.

### Host tests — pure transition, emitted-effect, real interpreter (Principle IV/V)

- [X] T025 [P] [US1] `tests/FS.GG.Governance.VerifyCommand.Tests/LoopTests.fs` — pure `update` tests: the sense step emits `SenseViewCurrency`; `ViewCurrencySensed findings` lands in `ViewCurrencyFindings`; `foldViewCurrencyVerdict` flips a verify-boundary blocker to Fail/Blocked and is identity on `[]` (emitted-effect + transition assertions, no I/O).
- [X] T026 [P] [US1] `tests/FS.GG.Governance.ShipCommand.Tests/LoopTests.fs` — the same pure `update`/emitted-effect/`foldViewCurrencyVerdict` assertions for the ship host at `RunMode.Gate`.
- [X] T027 [P] [US1] Add a stale-view fixture under `tests/golden-fixture/src/…` (a `.fsgg/refresh.yml` with `currency-enforcement: block-on-ship` and ≥1 view whose recorded provenance lock disagrees with its declared source digests), plus a `block-on-pr` variant for the verify-boundary case. The verify-boundary case MUST be exercised as `fsgg verify --profile strict`: `block-on-pr`'s floor is the **gate** run mode, and only the `strict` profile tightens it down to the **verify** run mode — so under the default `standard` profile a `block-on-pr` finding is a warning, not a blocker (see C1 / FR-009).
- [X] T028 [US1] `tests/FS.GG.Governance.ShipCommand.Tests/EndToEndTests.fs` — real `Interpreter` E2E over the T027 stale fixture: `fsgg ship` ⇒ `verdict":"fail"`, `exitCodeBasis":"blocked"`, a `generatedViews` blocker naming the stale view with `effectiveSeverity":"blocking"` (SC-001, SC-005). After T024, T027.
- [X] T029 [US1] `tests/FS.GG.Governance.VerifyCommand.Tests/EndToEndTests.fs` — real `Interpreter` E2E over the `block-on-pr` fixture run as `fsgg verify --profile strict`: verify surfaces the stale view as a blocker and fails (acceptance #3). Also assert the **same** fixture under the default `standard` profile yields a **warning, not a blocker** (the verify-below-floor truth-table outcome — FR-009), so the strict-profile blocking is proven against the real floor rather than assumed. After T022, T027.
- [X] T030 [US1] Bless the **new** stale/`block-on-pr` goldens (`BLESS_GOLDEN=1 dotnet test …ShipCommand.Tests …VerifyCommand.Tests`); confirm only the new fixtures move (`git diff --stat -- tests/**/goldens`). After T028, T029.

**Checkpoint**: US1 is fully functional — a configured stale view blocks at ship and (PR-dialed) at verify, with a self-describing blocker. MVP deliverable.

---

## Phase 4: User Story 2 — Keep local authoring cheap: blocking is opt-in (Priority: P2)

**Goal**: With no `currency-enforcement` configured (or at `observe`/`warn`), stale views stay advisory and every existing artifact is byte-identical. Builds on US1's machinery (the same finding, left at its advisory floor).

**Independent Test**: Run both hosts over a stale-view fixture with no `currency-enforcement` key; assert verdict, exit code, and every emitted artifact are byte-identical to the pre-feature baseline.

- [X] T031 [US2] Add two fixtures under `tests/golden-fixture/src/…` for the present-but-unconfigured byte-identity paths: (a) the T027 stale view but with **no** `currency-enforcement` key, and (b) an all-fresh, **unconfigured** view set (every view's recorded lock matches its declared source digests) — so the byte-identity guard runs over both a stale and a fresh input (US2 Independent Test). Self-contained: no dependency on the later US3 fixtures. `[P]` with T032.
- [X] T032 [P] [US2] `tests/FS.GG.Governance.ShipCommand.Tests/SurfaceDriftTests.fs`/additivity guard (and the verify analogue) — unconfigured byte-identity guard (SC-002): freeze the existing `route.json` / `audit.json` / `verify.json` / `ship.json` goldens and assert **0 bytes** change with the feature present-but-unconfigured (`git diff --stat -- tests/**/goldens` ⇒ no changes); `generatedViews` field absent. After T021–T024.
- [X] T033 [US2] `tests/FS.GG.Governance.CurrencyEnforcement.Tests/FindingsGateTests.fs` (extend) — `observe`/`warn` dial: a stale view produces a finding that is **reported but never blocks** (effective severity stays `Advisory` at every run mode) — acceptance #2. Reuses T010's gate; no host change.

**Checkpoint**: Opt-in safety proven — present-but-unconfigured is byte-identical; `observe`/`warn` never blocks.

---

## Phase 5: User Story 3 — No-hide honesty when a stale-view finding is relaxed (Priority: P3)

**Goal**: A configured-but-relaxed stale-view finding (e.g. `block-on-ship` evaluated under `fsgg verify`, or relaxed by profile) still appears as a **warning** carrying both base and effective severity; a current view produces no finding at all. Refines US1/US2.

**Independent Test**: For a `block-on-ship` finding evaluated under `fsgg verify`, assert the `generatedViews` entry shows `baseSeverity":"blocking"` + `effectiveSeverity":"advisory"` + a lever-naming reason and is not dropped; for a fresh view, assert no `generatedViews` entry.

- [X] T034 [US3] `tests/FS.GG.Governance.VerifyCommand.Tests/EndToEndTests.fs` (extend) — verify over the `block-on-ship` fixture (run mode below the boundary): a `generatedViews` warning with both severities + lever-naming `reason`, not omitted; verify never escalates to the gate verdict (FR-009/SC-004, acceptance #1). After T029.
- [X] T035 [P] [US3] `tests/FS.GG.Governance.CurrencyEnforcement.Tests/NoHideTests.fs` (extend) — a relaxing **profile** at the active boundary yields a visible warning with both severities while the carried `Cause`/`CurrencyStatus` is unchanged (acceptance #2). Reuses T010.
- [X] T036 [P] [US3] Add an **all-fresh** fixture (`currency-enforcement: block-on-ship`, every view's lock matches its sources) under `tests/golden-fixture/src/…`.
- [X] T037 [US3] `tests/FS.GG.Governance.ShipCommand.Tests/EndToEndTests.fs` (extend) — ship over the all-fresh fixture ⇒ **no** `generatedViews` field, `verdict":"pass"`, `exitCodeBasis":"clean"`, byte-identical to the unconfigured run (SC-006, acceptance #3 / no false positives). After T024, T036.

**Checkpoint**: No-hide honesty proven across the verify run mode and a relaxing profile; fresh views never produce a finding.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Re-bless surface baselines (additive only), flip the roadmap row, retarget the SPECKIT pointer, and run the quickstart.

- [X] T038 [P] Bless the new leaf surface baseline + additive baselines (`BLESS_SURFACE=1 dotnet test`); confirm `git diff -- surface/` shows **only** a new `surface/FS.GG.Governance.CurrencyEnforcement.surface.txt` plus additive lines on `RefreshJson` / `VerifyJson` / `AuditJson` / `VerifyCommand` / `ShipCommand` baselines (no existing binding removed or re-signed). After all host/leaf work (T010–T024).
- [X] T039 [P] `docs/initial-implementation-plan.md` — flip the Phase-7 stale-view-blocking row (lines ~832–863) from 🟡 to closed, citing `070`.
- [X] T040 [P] `CLAUDE.md` — retire the `070` SPECKIT plan pointer per the repo convention. `070` closes Phase 7's last open functional row, so if no successor plan exists yet, point the reference at the roadmap (`docs/initial-implementation-plan.md`) rather than a non-existent next plan; otherwise point it at the next plan.
- [X] T041 Run `specs/070-stale-view-blocking/quickstart.md` end-to-end (SC-001…SC-006) against the real interpreters; confirm `git diff --stat -- tests/**/goldens` shows only the intended new fixtures and `git diff -- surface/` only additive baseline moves.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — **BLOCKS all user stories** (the leaf + config dial).
- **US1 (Phase 3)**: depends on Foundational. Builds the host MVU wiring + additive projection the other stories reuse.
- **US2 (Phase 4)**: depends on US1's host wiring (T021–T024) for the byte-identity guard. Otherwise small/test-only.
- **US3 (Phase 5)**: depends on US1's E2E scaffolding (T029, T024) for the no-hide warning/fresh-view cases.
- **Polish (Phase 6)**: depends on all leaf/host work; T038/T041 after the implementation tasks.

### Within-story ordering

- `.fsi` contract before `.fs` body (Principle I/II): T004→T010; T015/T016→T021/T023; T017/T018→T021/T023.
- Tests written before the implementation they cover, and must FAIL first: T006–T009 before T010; T019/T020 before T021/T023; T025/T026 before they pass; E2E fixtures (T027, T036) before their E2E tests (T028/T029, T034, T037).
- Edge interpreter (T022/T024) after the host `update` (T021/T023).
- Bless tasks (T030, T038) after the code/goldens they freeze.

### Parallel opportunities

- **Setup**: T002 ∥ (T001→T003).
- **Foundational**: the four leaf test files T006–T009 in parallel (all FAIL pre-T010); T011 ∥ T006–T009; after T011/T012, T013 ∥ T014.
- **US1**: the two `.fsi` triples T015 ∥ T016; the two projection overloads T017 ∥ T018; projection tests T019 ∥ T020; pure host tests T025 ∥ T026. The verify chain (T021→T022) and ship chain (T023→T024) are independent and may run in parallel by different developers.
- **US2/US3**: T031 ∥ T032; T035 ∥ T036; the leaf-only extensions (T033, T035) need no host change.
- **Polish**: T038 ∥ T039 ∥ T040 (different files); T041 last.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → Phase 2 Foundational (leaf green, dial parses, `refresh.json` byte-identical).
2. Phase 3 US1 → **STOP and VALIDATE**: a configured stale view blocks at ship (and PR-dialed verify) with a self-describing blocker (SC-001/SC-005). This alone delivers the roadmap row's value.

### Incremental delivery

3. Add US2 → prove opt-in byte-identity (SC-002) and `observe`/`warn` never blocks.
4. Add US3 → prove no-hide warnings (SC-004) and fresh-view zero-false-positives (SC-006).
5. Polish → bless additive baselines, flip the docs row, run the quickstart.

---

## Notes

- Reuse, don't reopen: the only enforcement call is the existing `deriveEffectiveSeverity`; the closed `EnforcedItemId`/`FindingId`/`Severity`/`Maturity`/`RunMode`/`Profile` cores and the F024 partition are reused verbatim. The currency finding adjusts only `Verdict`/`ExitCodeBasis` and rides in the additive `generatedViews` array — it never becomes an `EnforcedItem` (D2).
- Byte-identity is the safety contract: every projection overload omits `generatedViews` when empty, and `foldViewCurrencyVerdict []` is identity — keep T032's guard green throughout.
- Never mark a task `[X]` on a failing assertion; never weaken an assertion to green a build — narrow scope and document it on the task line.
- Naming (avoid the in-host collision): `VerifyCommand`/`ShipCommand` already own a `CurrencyNotes` / "currency section" vocabulary for **evidence-reuse** freshness (F046/F048). The new generated-view wiring therefore uses the distinct `SenseViewCurrency` effect / `ViewCurrencySensed` msg / `ViewCurrencyFindings` model field / `foldViewCurrencyVerdict` fold; document the two-currency distinction in `Loop.fsi` so a reader does not conflate generated-view staleness with evidence-reuse currency.
- Synthetic test inputs (if any) carry `Synthetic` in the test name with a use-site disclosure (Constitution V).

## As-built notes (F070 implementation)

Deviations from the plan, all driven by repo invariants discovered during implementation — recorded honestly:

- **New core `FS.GG.Governance.CurrencySensing`** (impure edge): the plan's T022/T024 assumed the hosts could
  call `Declaration.parse` + the refresh interpreter's `Sense`/`ReadProv` directly. Those live in the
  **`RefreshCommand` host**, and the repo enforces (via `ShipCommand.Tests`/`AuditJson.Tests` dependency
  guards) that a command depends only on cores — never on another command host. So the shared refresh.yml
  parse + lock read + source digest were placed in a new **core** `CurrencySensing` (referenced by both
  hosts), re-expressing the F057 edge sensing for the currency-only fields. The pure decision stays in the
  `CurrencyEnforcement` leaf. The ship/audit dependency-guard allowlists were extended for the new cores.
- **`generatedViews` per-entry shape**: the undeterminable cause-reason is emitted as **`detail`** (not a
  second `reason`) to avoid the duplicate-key bug in the contract's original example; the trailing `reason` is
  the `EnforcementDecision.Reason`. `drifted` tokens use `FreshnessKey.categoryToken`
  (`coveredArtifacts`/`generatorVersion`). Contract C3 updated to match.
- **Fixtures**: the configured-blocking E2Es (T027–T029, T034) build their stale/fresh views as **real
  on-disk temp repos** (real `.fsgg/refresh.yml` + real `.fsgg/refresh.lock.json` + real source files) inside
  the existing `withTempRepo` harness, rather than a committed `tests/golden-fixture/` tree — real evidence,
  not Synthetic. The host E2Es assert verdict + `generatedViews` content directly (the suite has no
  `BLESS_GOLDEN` artifact for these), so T030 is N/A.
- **Coverage map (real evidence)**: leaf `decideCurrency`/`findingsOf`/sweep/no-hide (T006–T010, T033, T035) —
  `CurrencyEnforcement.Tests` (19); dial parse + `refresh.json` byte-identity (T013/T014) —
  `RefreshCommand.Tests`; the edge sensing incl. stale/fresh/unconfigured/undeterminable (T022/T024/T027/T031/
  T036/T037) — `CurrencySensing.Tests` real-I/O `senseRepo` (9); the `generatedViews` wire shape (T019/T020) —
  `AuditJson.Tests/GeneratedViewsTests`; **US1 ship** (T028) — `ShipCommand.Tests/EndToEndTests` real
  interpreter (Fail+Blocked+blocker); **US1 verify-PR (C1)** + **US3 no-hide** (T029/T034) —
  `VerifyCommand.Tests/EndToEndTests` real interpreter (strict⇒blocking, standard⇒advisory warning); **US2
  byte-identity** (T031/T032) — every existing verify/ship/audit golden unchanged + `CurrencySensing`
  unconfigured ⇒ `[]`. Pure `update` fold (T025/T026): the `init` currency-sense emission is asserted in
  `LoopTests`; `foldViewCurrencyVerdict` is exercised through the real-interpreter E2Es rather than a separate
  pure-transition test.
- **T041 (quickstart)**: SC-001…SC-006 are exercised by the test suite above rather than a separate manual
  run; `git diff -- surface/` shows only the new `CurrencyEnforcement`/`CurrencySensing` baselines plus
  additive moves on `RefreshJson`/`VerifyJson`/`AuditJson`/`VerifyCommand`/`ShipCommand`.
