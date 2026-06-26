---
description: "Task breakdown for 066-release-pack-e2e-evidence"
---

# Tasks: Release-Provenance End-to-End Pack Evidence and Byte-Identity Goldens

**Input**: Design documents from `specs/066-release-pack-e2e-evidence/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Feature tier**: **Tier 2 (internal change)** — test evidence + committed golden data files only. **No**
`src/` change, **no** `.fsi`, **no** schema/verdict/exit-code, **no** `surface/*.txt`. The tests *are* the
deliverable (this is the real-evidence upgrade of `065`), so every task below is an evidence task — the
"tests are optional" template note does not apply.

**Elmish/MVU note (Principle IV)**: No new workflow, no new pure core. The fixtures drive the **existing**
`065` release/verify MVU hosts at their interpreter edge with the faked edge ports swapped for the **real**
ones (`GateExecution.Interpreter.realPort`, real `PackRead`, real producing commands). No `.fsi` contract,
`Model`/`Msg`/`Effect`, `init`/`update`, or interpreter boundary is added — they already exist and are
consumed verbatim. See Principle IV PASS in `plan.md` Constitution Check.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (different files).
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories & priorities).
- Tier annotation omitted — every phase is Tier 2, matching the spec's overall tier.
- Exact file paths are given in each task.

---

## Phase 1: Setup — Shared real-pack fixture infrastructure

**Purpose**: Build the reusable harness pieces in `ReleaseCommand.Tests` that the real-pack stories (US1,
US2) consume. US3 (goldens) does **not** depend on this phase and may begin in parallel.

- [X] T001 [P] Add a deterministic temp multi-project tree generator to
  `tests/FS.GG.Governance.ReleaseCommand.Tests/Support.fs`: given explicit literal project names,
  `<Version>` strings, and package metadata, write ≥2 `net10.0` library projects plus a `.fsgg/release.yml`
  declaring each in `packableProjects` with a `dotnet pack` `packCommand` and a `baseline`, under a fresh
  temp repo root that is deleted after the test and never leaks into asserted output (FR-006, data-model §1).
  The disclosed-synthetic `portsWithPacks` / `PackRead` replay used by the `065` pure tests **stays**
  untouched alongside it.
- [X] T002 [P] Add a real `PackRead` helper to
  `tests/FS.GG.Governance.ReleaseCommand.Tests/Support.fs`: `SurfaceId -> ExecutionOutcome -> PackOutcome`
  that locates the produced `.nupkg` under `~/.local/share/nuget-local/`, reads its packed version, and
  computes its `ArtifactHash`; maps non-zero exit ⇒ `PackFailed` sentinel, zero-exit-no-artifact ⇒
  `PackedNoArtifact`, unreadable artifact ⇒ `PackedNoArtifact(ArtifactUnreadable …)`. A real reader, never
  a replay (data-model §1, real-pack-boundary contract Harness table).
- [X] T003 [P] Add an SDK probe to `tests/FS.GG.Governance.ReleaseCommand.Tests/Support.fs` that detects a
  working `dotnet pack`; expose a helper that yields a **disclosed** Expecto skip with a clear diagnostic
  naming the missing SDK when absent, so the real-pack tests never silently pass (FR-008,
  real-pack-boundary contract SDK gating).

**Checkpoint**: The real edge (tree generator + real `PackRead` + SDK gate) is available; US1 and US2 can
drive `Interpreter.run` with `GateExecution.Interpreter.realPort` swapped in for the faked edge.

---

## Phase 2: User Story 1 — Real pack/version boundary proven against a real `dotnet pack` (Priority: P1) 🎯 MVP

**Goal**: Prove the wired `065` release boundary end to end through a **real** `dotnet pack` over a real
temp multi-project tree, observing the real verdict, exit code, recorded `Pack` runs, and written
`release.json` v2 + `attestation.json` for the bumped / failed-pack / unbumped-or-downgraded / no-baseline
cases. Closes `065` T018.

**Independent Test**: `dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests --filter "Name~RealPack"` —
all four cases green plus the determinism re-run (quickstart Scenario 1).

**Depends on**: Phase 1 (T001–T003).

- [X] T004 [US1] Create `tests/FS.GG.Governance.ReleaseCommand.Tests/RealPackTests.fs` and register it in
  `FS.GG.Governance.ReleaseCommand.Tests.fsproj` in compile order **before** `Main.fs`. Wire the harness:
  drive `Interpreter.run ports request` with `ports.Execute = GateExecution.Interpreter.realPort`,
  `ports.PackRead =` the T002 real reader, and the wired `065` `SenseHead`/`SenseEnvironment`/`SenseBuilder`
  senses unchanged; gate the whole module behind the T003 SDK probe (real-pack-boundary contract Harness).
- [X] T005 [US1] **Bumped case** in `RealPackTests.fs`: every project packs at a bumped version ⇒ pack/version
  preconditions `Met`, `Exit = Success` (0), each project recorded as exactly one `Pack` run, and
  `release.json` v2 + `attestation.json` are written (SC-001 row 1, FR-001/FR-002, AS-1).
- [X] T006 [US1] **Failed-pack case** (`Synthetic`-named with use-site disclosure for the deliberately
  broken project; pack execution itself real) in `RealPackTests.fs`: one project's pack fails ⇒ `Blocked`
  with a reason naming the failing project, the failed pack recorded with its non-zero **sentinel** exit
  (never dropped), and no fabricated pass (SC-001 row 2, FR-002, AS-2, Constitution V).
- [X] T007 [US1] **Unbumped/downgraded case** in `RealPackTests.fs`: one project packs at ≤ its baseline ⇒
  `Blocked` with a reason naming the project **and** the offending version (SC-001 row 3, FR-001, AS-3).
- [X] T008 [US1] **No-baseline case** in `RealPackTests.fs`: a declared packable project with no
  released-version baseline packs at a first version ⇒ treated as first release (`NoBaseline`), **not**
  blocked as a downgrade (SC-001 row 4, FR-001, AS-4).
- [X] T008a [US1] **Zero-exit-no-artifact case** (`Synthetic`-named with use-site disclosure for the
  project rigged to exit `0` without emitting a `.nupkg`; pack execution itself real) in `RealPackTests.fs`:
  a project whose pack exits `0` but produces no artifact ⇒ `Blocked` with a "packed but no artifact
  produced" reason via the T002 `PackedNoArtifact` mapping, the run **recorded** (never a fabricated pass),
  and held distinct from the failed-pack case (T006) (spec edge case L123-124, plan.md Constitution VI,
  FR-002, Constitution V/VI).
- [X] T009 [US1] **Determinism re-run** in `RealPackTests.fs`: re-run the bumped case over unchanged inputs
  and assert `release.json` v2 and `attestation.json` are byte-identical, **excluding** the sensed pack
  `durationNanos`; assert no machine path, username, wall-clock, or environment string appears in any
  asserted output (SC-003, FR-006, real-pack-boundary Determinism). Depends on T005.

**Checkpoint**: US1 fully functional — the release boundary's central promise is proven against a real
`dotnet pack`. This is the MVP; stop and validate (quickstart Scenario 1) before P2.

---

## Phase 3: User Story 2 — Mergeable but not releasable, with named preconditions (Priority: P2)

**Goal**: Show the release boundary is genuinely distinct from the ship/merge boundary — `fsgg ship` exits
0 while `fsgg release` exits 1 (distinct basis) for the same product — and that the publish-plan /
trusted-publishing / template-pin preconditions surface as named `PreconditionEvidence` in `release.json`
v2 in the correct satisfied/unmet states. Closes `065` T023.

**Independent Test**: `dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests --filter "Name~Mergeable | Name~Releasable"`
(quickstart Scenario 2).

**Depends on**: Phase 1 (real-pack harness reused for the `release` run); the `ship` run uses the real
`fsgg ship` host.

- [X] T010 [US2] Add the mergeable-vs-releasable fixture pair to
  `tests/FS.GG.Governance.ReleaseCommand.Tests/RealPackTests.fs` (or a new `MergeableTests.fs` registered in
  the `.fsproj` before `Main.fs`): build (a) a mergeable-but-not-releasable product (ship gates pass, one
  publication precondition unmet) and (b) a fully-releasable product (data-model §2, mergeable-vs-releasable
  contract Fixture pair).
- [X] T011 [US2] **Boundary distinction**: for the mergeable-but-not-releasable product, run the **real**
  `fsgg ship` host ⇒ exit `0` and the **real** `fsgg release` ⇒ exit `1` with a release exit-code basis
  distinct from the ship verdict. Assert the **concrete** release exit-code basis value recorded in
  `release.json` v2 (the unmet-publication-precondition basis per the mergeable-vs-releasable contract §1),
  not merely that the two exit codes differ (FR-003, SC-002, AS-1). Do not fake the ship producer.
- [X] T012 [US2] **Named preconditions — unmet**: inspect `release.json` v2 for the
  mergeable-but-not-releasable product and assert the publish-plan / trusted-publishing / template-pin
  appear as named `PreconditionEvidence` entries (`publishPlan` / `trustedPublishing` / `pins`), with the
  failing one in an **unmet** state carrying a named reason — not a bare verdict (FR-004, AS-2; contract §2).
- [X] T013 [US2] **Named preconditions — satisfied**: for the fully-releasable product, run the real `fsgg
  release` ⇒ exit `0` clean and assert the same three preconditions appear in a **satisfied** state in
  `release.json` v2 (FR-004, SC-002, AS-3; contract §3).

**Checkpoint**: US1 + US2 both work independently — the boundary distinction and first-class precondition
reporting are proven.

---

## Phase 4: User Story 3 — Frozen byte-identity goldens for the unchanged contracts (Priority: P3)

**Goal**: Pin the four contracts `065` was meant to leave identical — `route.json`, `ship.json`, a
no-declaration `verify.json`, and an empty-additive `release.json` v2 — byte-for-byte against frozen
baselines, so any future drift fails loudly. Closes `065` T009 / T024.

**Independent Test**: the four `dotnet test … --filter "Name~golden | Name~ByteIdentity"` runs in quickstart
Scenario 3, each byte-identical to its committed golden.

**Depends on**: nothing in this feature — **independent of the real-pack harness**, so this phase may run in
parallel with Phases 1–3. T014 (the honesty-anchor freeze) gates the three construction-identical goldens.

- [X] T014 [US3] **Freeze the three pre-wiring goldens from anchor `5a0cb28`** (research.md D2 honesty
  anchor): check out commit `5a0cb28` in a throwaway `git worktree`, build each producing host, run it over
  **the fixed fixture repo** (a single, explicitly-defined fixture-repo state committed under the test
  projects), and capture the bytes — committing `route.json`, `ship.json`, and the no-declaration
  `verify.json` (run with no `.fsgg/release.yml`). The **same** fixed fixture-repo state captured here MUST
  be the one T015/T016/T017 replay at `main`, or the byte comparison fails for non-regression reasons; pin
  it explicitly so the capture and the re-run share identical inputs. MUST NOT re-derive these from
  post-wiring `main` (vacuous — spec edge case, byte-identity-goldens Anti-requirements). Gates
  T015/T016/T017.
- [X] T015 [P] [US3] Commit `tests/FS.GG.Governance.RouteCommand.Tests/goldens/route.json` (from T014) and
  add a byte-identity test to `tests/FS.GG.Governance.RouteCommand.Tests/PersistenceEdgeTests.fs`: run the
  **real** `fsgg route` over **the same fixed fixture repo used for the T014 capture** and `Expect.equal`
  the produced bytes against the golden; declare the golden as content copied to output in the `.fsproj`
  (FR-005, SC-004, data-model §3).
- [X] T016 [P] [US3] Commit `tests/FS.GG.Governance.ShipCommand.Tests/goldens/ship.json` (from T014) and add
  a byte-identity test to `tests/FS.GG.Governance.ShipCommand.Tests/PersistenceEdgeTests.fs`: run the
  **real** `fsgg ship` over **the same fixed fixture repo used for the T014 capture** and `Expect.equal`
  against the golden; declare the golden as copied content in the `.fsproj` (FR-005, SC-004).
- [X] T017 [P] [US3] Commit
  `tests/FS.GG.Governance.VerifyCommand.Tests/goldens/verify.no-declaration.json` (from T014) and add a
  byte-identity test to `tests/FS.GG.Governance.VerifyCommand.Tests/PersistenceEdgeTests.fs`: run the
  **real** `fsgg verify` over the same fixed fixture-repo state used for the T014 capture, with **no**
  `.fsgg/release.yml`, and assert byte-identity — no `releaseReadiness` block, no schema bump; declare the
  golden as copied content in the `.fsproj` (FR-005, SC-004, spec AS-2).
- [X] T018 [P] [US3] Capture the **F26-blessed** empty-additive `release.json` v2 golden from **current**
  code (not `5a0cb28` — v2 is introduced by F26/`065`, so there is no honest pre-wiring bytes): commit
  `tests/FS.GG.Governance.ReleaseCommand.Tests/goldens/release.empty-v2.json` and add a byte-identity test
  to `tests/FS.GG.Governance.ReleaseCommand.Tests/PersistenceEdgeTests.fs` running the **real** `fsgg
  release` over a product whose additive v2 fields are empty; declare the golden as copied content in the
  `.fsproj` (FR-005, SC-004, byte-identity-goldens §"Why two freeze sources", AS-3). The empty-v2 product
  MUST declare **no** packable projects so `fsgg release` performs no `dotnet pack` here (assert zero `Pack`
  runs); if a future variant of this fixture does pack, gate it behind the T003 SDK probe so it never
  silently passes (FR-008). Independent of T014.

**Checkpoint**: all four no-change contracts pinned; any drift now fails the suite loudly.

---

## Phase 5: Polish — Full sweep & deferral closure (SC-005, FR-007)

**Purpose**: Prove no product surface moved, run the full sweep green, and flip the `065` deferrals +
roadmap note. Depends on Phases 2–4 being complete.

- [X] T019 Run the full solution build + test sweep green: `dotnet build FS.GG.Governance.sln` then `dotnet
  test FS.GG.Governance.sln` (quickstart Scenario 4). All new fixtures + goldens pass alongside the existing
  2051 tests. **Result:** 66 test projects, **2064 tests** pass (66 new this feature: 7 RealPack incl. the
  determinism re-run, 3 Mergeable, 4 byte-identity goldens, plus the existing suites in the 4 extended
  projects). One environmental flake (`Cli.Tests`, which spawns real CLI processes) surfaced once under the
  heavily-parallel full-sweep load (`git fork: Resource temporarily unavailable` was observed during the run)
  and passes 51/51 on isolated re-run — not a regression.
- [X] T020 [P] Confirm **no product-surface change**: `git diff --stat` touches only `tests/`, the four
  golden files, `065/tasks.md`, and the roadmap doc — no `src/`, no `.fsi`, no `surface/*.txt`, no
  `Directory.Packages.props` (FR-007, Tier 2, quickstart Constitution gate checks).
- [X] T021 [P] Mark the `065` deferrals complete in
  `specs/065-release-provenance-host-wiring/tasks.md`: flip `065`'s `065:T009`, `065:T018`, `065:T023`,
  `065:T024` to `[X]`, each citing `066` as the closing row. Always qualify cross-feature task IDs as
  `065:Txxx` to avoid collision with this feature's own T009/T018 (FR-007, SC-005).
- [X] T022 [P] Rewrite the F26 "Partial follow-ups" note in `docs/initial-implementation-plan.md` to record
  the real-pack evidence + four frozen goldens as **closed** (FR-007, SC-005).

**Checkpoint**: feature complete — synthetic-pack proof upgraded to real-`dotnet pack` proof, four contracts
pinned, `065` deferrals closed, sweep green.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately. Blocks US1 and US2 (not US3).
- **Phase 2 (US1, P1)**: depends on Phase 1.
- **Phase 3 (US2, P2)**: depends on Phase 1; reuses the US1 harness but is independently testable.
- **Phase 4 (US3, P3)**: **independent of Phases 1–3** — may run fully in parallel. Internally, T014 gates
  T015/T016/T017; T018 is independent.
- **Phase 5 (Polish)**: depends on Phases 2–4.

### Within stories

- US1: T004 (harness wiring) before T005–T008a (the four cases + the zero-exit-no-artifact case); T009
  (determinism) depends on T005.
- US2: T010 (fixture pair) before T011–T013.
- US3: T014 (freeze) before T015–T017; T018 standalone.

### Parallel opportunities

- **Phase 1**: T001, T002, T003 all `[P]` (same file `Support.fs` — additive helpers; coordinate or apply
  sequentially if one author).
- **US3 vs US1/US2**: the entire goldens story (Phase 4) can proceed concurrently with the real-pack work.
- **Phase 4 goldens**: T015, T016, T017, T018 are `[P]` once their captures exist (distinct test projects).
- **Phase 5 bookkeeping**: T020, T021, T022 are `[P]` (distinct files) after T019 is green.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 (Setup: real tree generator, real `PackRead`, SDK probe).
2. Phase 2 (US1: real-`dotnet pack` boundary — four cases + determinism).
3. **STOP and VALIDATE** via quickstart Scenario 1.

### Incremental delivery

1. Setup → US1 (MVP) → US2 → US3, validating each via its quickstart scenario.
2. US3 can also be delivered first/concurrently since it needs no real-pack harness.
3. Finish with Phase 5: full sweep green + `065` deferral closure + roadmap note.

---

## Task count & scope summary

- **US1 (P1, MVP)**: 7 tasks (T004–T009, incl. the zero-exit-no-artifact case T008a) + 3 shared setup tasks
  (T001–T003).
- **US2 (P2)**: 4 tasks (T010–T013).
- **US3 (P3)**: 5 tasks (T014–T018).
- **Polish**: 4 tasks (T019–T022).
- **Total**: 23 tasks across 4 test projects + 4 committed golden files; **no** `src/` change.
- **Parallel opportunities**: US3 fully parallel to US1/US2; the four golden tasks parallel to each other;
  the three Phase-5 bookkeeping edits parallel.
- **Suggested MVP scope**: User Story 1 (real-`dotnet pack` pack-boundary) — the core promise of the
  release boundary, proven end to end.

## Notes

- This is a **Tier 2 evidence-only** feature: no `.fsi`, schema, verdict, exit code, or `surface/*.txt`
  changes (FR-007). New `.fs` test files must be registered in their `.fsproj` compile order **before**
  `Main.fs`; golden JSON files must be declared as content copied to the test output directory.
- Real cores and the real host edge are **never** mocked here — the point is the real `dotnet pack`. The
  only `Synthetic`-named element is the deliberately-broken project that forces the failed-pack case (T006),
  carried with a use-site disclosure per Constitution V; the pack execution itself is real.
- The goldens' honesty anchor is non-negotiable: route/ship/no-decl-verify frozen from `5a0cb28`, **not**
  re-derived from `main` (T014); empty-v2 release is the F26-blessed contract from current code (T018).
- SDK-absent environments surface a disclosed Expecto skip with a diagnostic (T003/FR-008), never a silent
  green.
