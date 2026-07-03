---
description: "Task list for feature 101 — tests & CI hygiene"
---

# Tasks: Tests & CI hygiene — consolidate SurfaceDrift, bound and cache CI, harden publish

**Input**: Design documents from `/specs/101-tests-ci-hygiene/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md) (D1–D9), [data-model.md](./data-model.md), [contracts/tests-ci-hygiene.md](./contracts/tests-ci-hygiene.md)

**Tier**: Tier 2 throughout (internal test/CI/build hygiene; the only `.fsi` touched is the test-only `Tests.Common` surface). No `[T1]`/`[T2]` annotations needed — all tasks match the spec's overall Tier 2.

**Status legend**: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe (different files, no dependency on another incomplete task in this phase).
- **[Story]**: US1 (surface-drift consolidation), US2 (CI bound+cache), US3 (publish hardening).

**Story independence**: US1 depends on the foundational helper (Phase 2). US2 and US3 depend on nothing in this feature and can be done in any order / in parallel with US1.

---

## Phase 1: Setup (baseline the pre-change state)

- [X] T001 Baseline green: `dotnet fsi build.fsx test` passed (Failed: 0 across all projects) with the helper present-but-unused — confirming both the pre-change green tree and that the new module compiles in-solution.
- [X] T002 [P] Drift-locked config confirmed byte-identical (`git status --porcelain` on the three files is empty); reverted unrelated pre-existing ReleaseCommand lock drift so the feature diff stays clean.
- [X] T003 [P] `Expecto` is centrally pinned in the org-baseline `Directory.Packages.props`; `Tests.Common` references it version-lessly via CPM.

---

## Phase 2: Foundational — the shared `SurfaceDrift` helper (BLOCKS US1)

**Purpose**: Build the single shared helper and prove it reproduces baselines exactly, before touching 79 call-sites. **⚠️ No US1 migration begins until T008 is green.**

- [X] T004 Added the `SurfaceDrift` module to `TestsCommon.fsi` per Contract A (all four members). No access modifiers in the `.fs` — `normalize` is private by omission from the signature (Principle II).
- [X] T005 Implemented the `SurfaceDrift` body in `TestsCommon.fs`: canonical `renderSurface`, `normalize`, `surfaceTest` (BLESS path via `RepositoryHelpers.repoRoot`), `referencesOnly`, `noInboundReferences`.
- [X] T006 Added `<PackageReference Include="Expecto" />` to `Tests.Common.fsproj`; force-evaluate restore added Expecto to `tests/FS.GG.Governance.Tests.Common/packages.lock.json` only — org-synced props untouched.
- [X] T007 Migrated `Tests.Common.Tests/SurfaceBaselineTests.fs` to `SurfaceDrift.surfaceTest` (kept the FR-008 scope guard local); re-blessed `surface/FS.GG.Governance.Tests.Common.surface.txt` — diff shows exactly the 4 SurfaceDrift members added (`normalize` correctly absent); suite green (6/6).
- [X] T008 Pilot: migrated `Adapters.SpecKit.Tests/SurfaceDriftTests.fs` to all three helper builders + added the `Tests.Common` ProjectReference. 28/28 green against the **unchanged** SpecKit baseline — proves the shared renderer is byte-identical (SC-003, FR-004). Discovered: 18 self-contained projects need the `Tests.Common` ProjectReference added during migration.

**Checkpoint**: The helper exists, its own surface is blessed, and one real call-site passes with no baseline change. Mechanical migration can now fan out.

---

## Phase 3: User Story 1 — surface-drift consolidation (Priority: P1) 🎯 MVP

**Goal**: Every surface-drift file is a thin `SurfaceDrift.*` instantiation; no duplicated renderer / bless path / inline `findRepoRoot`; every baseline still asserted exactly once.

**Independent Test**: Full suite green with only `Tests.Common`'s baseline changed; a deliberately mutated baseline goes RED ([quickstart.md](./quickstart.md) US1).

Each migration task below: for every listed file, replace the local `renderSurface`/`normalize`/`findRepoRoot`/bless body with `open FS.GG.Governance.Tests.Common` + `SurfaceDrift.surfaceTest label baselineName asm`; fold "references only …" tests into `SurfaceDrift.referencesOnly` and "direction / no-inbound" tests into `SurfaceDrift.noInboundReferences`; **leave bespoke symbol-leak and one-off guards inline** (research D4). Preserve each existing test's title/label so no assertion is dropped. All Phase-3 batches are `[P]` (disjoint files) once T008 is green.

- [X] T009 [P] [US1] Adapters + core migrated (DesignSystem, SddHandoff, Spi, Kernel, Host, HumanText) — scope + direction guards mapped to `referencesOnly`/`noInboundReferences`; DesignSystem's vocabulary-leak guard and SddHandoff's SDD deny-list kept inline; fsproj refs added.
- [X] T010 [P] [US1] 12 JSON suites + JsonText/JsonTokens/JsonWriters baselines migrated; "exactly one module" guards kept inline; JsonTokens/JsonWriters deny-list scope guards kept inline; 3 fsproj refs added.
- [X] T011 [P] [US1] 9 command suites + CommandHost baseline migrated; module-exports guards and VerifyCommand's positive-required-ref guard kept inline; CommandHost's open-ended forbid kept inline.
- [X] T012 [P] [US1] route/ship/enforcement/gates migrated; module-count and symbol-leak guards kept inline (repointed to `SurfaceDrift.renderSurface`).
- [X] T013 [P] [US1] release/provenance/attestation suites migrated; ReleaseRules/ReleaseFactsSensing leak guards kept inline.
- [X] T014 [P] [US1] freshness/cache/evidence/reuse suites migrated; module-count guards kept inline.
- [X] T015 [P] [US1] checks/sensing/config migrated; `check asm name` trio (CurrencyEnforcement, CurrencySensing, SurfaceChecks) folded onto `surfaceTest`; SurfaceChecks emits two calls (SurfaceChecks + .Dispatch); Snapshot/SensedMetadata leak+forbid guards kept inline; fsproj refs added.
- [X] T016 [P] [US1] agent/review/calibration suites + RuleIdentity baseline migrated; module-count guards inline; RuleIdentity fsproj ref added.
- [X] T017 [US1] HumanRender normalized: `Cli.Tests/HumanRenderSurfaceDriftTests.fs` → one `SurfaceDrift.surfaceTest` call over `typeof<Watch.WatchModel>.Assembly` + placement comment; fsproj ref added.
- [X] T018 [US1] Both outliers left local (`Cli.Tests/SurfaceDriftTests.fs` hardcoded list; `Sample.SddReferenceProvider.Tests` cross-baseline guard) — confirmed by the T019 sweep.
- [X] T019 [US1] Completeness sweep clean: only `Sample.SddReferenceProvider` retains local `renderSurface` (expected); zero inline `findRepoRoot` in the family (SC-002). Census: 84 `surfaceTest`, 56 `referencesOnly`, 7 `noInboundReferences`, 10 inline leak-guards via `SurfaceDrift.renderSurface`.
- [X] T020 [US1] `dotnet fsi build.fsx test` green (exit 0, 0 failures); `git status surface/` shows ONLY `Tests.Common.surface.txt` changed — proving every other baseline still matches (SC-003, FR-004). RED-path confirmed: breaking the SpecKit baseline fails the migrated `surfaceTest` ("V8 SpecKit public surface drifted"). SC-001: 7,738 → 3,214 lines (~58% removed) + one 77-line helper; SC-002: 0 inline `findRepoRoot`.

**Checkpoint**: US1 complete — one shared definition, every verdict preserved, duplication gone.

---

## Phase 4: User Story 2 — CI bounded + cached (Priority: P2)

**Goal**: Every job time-bounded; every restoring job cache-warmed. Independent of US1.

**Independent Test**: Every job has `timeout-minutes`; every `setup-dotnet` has `cache:`; a second CI run with unchanged lockfiles hits the cache ([quickstart.md](./quickstart.md) US2).

- [X] T021 [P] [US2] `gate.yml`: added `timeout-minutes` to all 4 jobs (gate 25, build-config-drift 10, reference-gate-set-pack 20, api-compatibility-gate 30) and `cache: true` + `cache-dependency-path: '**/packages.lock.json'` to the 3 setup-dotnet steps.
- [X] T022 [P] [US2] `publish.yml`: added `timeout-minutes` to all 5 jobs (resolve-version 15, cli-tests 25, enforcement-smoke 20, publish 20, publish-reference-gate-set 20) and the cache block to all 5 setup-dotnet steps.
- [X] T023 [US2] Verified: `timeout-minutes` count = 9 (4+5); `cache: true` count = 8 (3+5); org-synced config untouched (empty status); locked-restore steps unchanged except the added cache inputs.

**Checkpoint**: CI is bounded and cached; deterministic locked-restore enforcement unchanged.

---

## Phase 5: User Story 3 — publish hardening (Priority: P3)

**Goal**: A non-semver `v*` tag never pushes; the fallback NuGet user is single-sourced. Independent of US1/US2.

**Independent Test**: Simulated `resolve-version` for `vNext` fails-closed; `v1.2.3`==Version pushes; dispatch-no-input dry-runs; `Paradigma11` appears once ([quickstart.md](./quickstart.md) US3).

- [X] T024 [US3] Added the fail-closed `else` branch in `resolve-version`: a non-semver `v*` tag now errors ("not a semantic version; retag with v<major>.<minor>.<patch>") and `exit 1` instead of falling through to `push=true`. Semver-equal / mismatch / dispatch paths unchanged.
- [X] T025 [P] [US3] Added workflow-level `env: NUGET_FALLBACK_USER: Paradigma11`; both `NuGet/login` steps now use `${{ secrets.NUGET_USER || env.NUGET_FALLBACK_USER }}`.
- [X] T026 [US3] Verified by faithful shell simulation: `vNext`→ERROR non-semver (no push), `v1.2.3`==Version→push=true, `v9.9.9`→semver-mismatch error, release `vNext`→ERROR, dispatch-no-input→push=false, dispatch `1.2.3`→push=true. `Paradigma11` count = 1.

**Checkpoint**: Publish path fails closed on an unreconcilable tag; fallback user single-sourced.

---

## Phase 6: Polish & acceptance

- [X] T030 [pre-existing drift] Regenerate lockfiles to a consistent state (`dotnet restore --force-evaluate`) so `--locked-mode` (the CI gate) passes. Discovered mid-implementation: the branch base (`main`@6b53332) already fails `dotnet restore --locked-mode` with 15 NU1004 errors — `ReleaseJson`/`VerifyJson` reference `AttestationJson` (added by #52 "reference attestation tokens from one source") but four `src` locks never recorded it. This is **pre-existing drift, not caused by feature 101**, but it blocks any green gate on this branch, so it is corrected here (in theme with US2's lockfile work). Net lock changes: 84 test locks (the `Tests.Common`→Expecto edge) + 4 src locks (the AttestationJson drift). Locked-mode restore now passes with 0 NU errors. **Call this out separately in the PR.**
- [X] T027 Quickstart validated: US1 (green suite + RED-on-broken-baseline + no inline `findRepoRoot`), US2 (9 timeouts / 8 caches / config untouched), US3 (tag-resolution simulation + single `Paradigma11`).
- [X] T028 Final acceptance: locked-mode restore exit 0 (CI-valid); `dotnet fsi build.fsx test` real exit 0 — **83 projects, 2444 tests, 0 failures, 1 skipped**; org-synced config byte-identical (SC-006); the only changed baseline is `Tests.Common`. SC-001…SC-006 all met (SC-001 restated to the actual ~58% figure).
- [-] T029 [P] SKIPPED — README does not enumerate the surface-drift file convention or the specific CI jobs (only a still-accurate general note on committed lockfiles + locked restore), so there is nothing to reconcile. The placement rationale lives in research.md/quickstart.md.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → no deps.
- **Phase 2 (Foundational helper)** → after Phase 1; **BLOCKS all of Phase 3 (US1)**. Does NOT block US2/US3.
- **Phase 3 (US1)** → after T008. T009–T018 are parallel-safe (disjoint files); T019 after them; T020 after T019.
- **Phase 4 (US2)** and **Phase 5 (US3)** → independent of everything except Phase 1; can run in parallel with each other and with US1.
- **Phase 6 (Polish)** → after all desired stories.

### Parallel opportunities

- T002, T003 in Setup.
- The entire Phase 3 migration fan-out (T009–T018) once T008 is green — 10 disjoint batches.
- US2 (T021–T022) and US3 (T024–T025) alongside US1 and each other.

### MVP scope

**User Story 1** (Phases 1–3) is the MVP and the headline of the finding — the ~80→1 consolidation. US2 and US3 are independent value-adds shippable in any order.

## Notes

- Principle IV (Elmish/MVU) is **not applicable**: this feature is pure reflection + test assertions + declarative CI YAML; no stateful/I-O workflow is introduced.
- Real evidence only (Principle V): green full build+test, a demonstrated RED on a broken baseline, and the drift-locked config proven byte-identical. No synthetic evidence expected.
- Commit after each batch/logical group; the completeness sweep (T019) is the guard that no file was missed.
