# Tasks: Promote `governedReferences` to First-Class Routing Facts

**Feature**: `082-route-governed-refs` | **Branch**: `082-route-governed-refs`

**Input**: Design documents from `specs/082-route-governed-refs/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D9), data-model.md,
contracts/consumer-candidatePaths.fsi.md, contracts/host-candidate-seam.md, quickstart.md (V1–V7)

**Tier**: Tier 1 (adds one additive public `.fsi` line + baseline re-bless). Real-evidence
discipline (Constitution V): every behavioral test drives the **real**
`Config→Gates→Routing→Route` pipeline through the host `update` functions — no synthetic routing
facts, no mocks of the selection algorithm. Adapter unit tests run over hand-built `HandoffRead`
JSON fixtures (no I/O).

**Elmish/MVU note**: No new `Effect`/`Msg`/`Port`/`Phase`/`Model` field is introduced. The
existing F081 `LoadHandoffs`/`HandoffsLoaded`/`Ports.Handoffs` edge is reused unchanged; the only
change is a pure candidate-merge inside each host's `update` `Loaded(Valid)` arm (research D2/D6).
Per the Elmish-applicability rule, the new public surface is `.fsi`-curated and the transition is
covered by pure host-`update` tests plus real-pipeline evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: `US1`/`US2`/`US3`, or blank for setup/foundational/polish.
- Tier is `T1` throughout (matches the spec); annotations omitted as they match.

---

## Phase 1: Setup

**Purpose**: Confirm a green starting baseline and locate the fixtures the behavioral tests reuse.

- [X] T001 Confirm a clean pre-feature baseline: run `dotnet fsi build.fsx` (bounded whole-solution
  wrapper) and confirm the adapter + three host test projects are green. Capture that the
  route/ship/verify goldens under `tests/FS.GG.Governance.{Route,Ship,Verify}Command.Tests/goldens/`
  and the adapter surface baseline `surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt` are the
  byte-identical reference for the SC-002 no-op guard later (T021).
- [X] T002 [P] Locate the reusable test fixtures: the reference catalog/path-map routing `src/**`→`build`,
  `tests/**`→`test` already used by the three host test projects (their `Support.fs`), and the F081
  handoff fixtures under `tests/FS.GG.Governance.Adapters.SddHandoff.Tests/fixtures/` and the host
  `Support.fs` handoff-port fakes (`HandoffRouteWiringTests.fs` / `HandoffWiringTests.fs` /
  `HandoffReadinessTests.fs`). No new fixture infrastructure is required beyond a handoff that declares
  `governedReferences` in the `build`/`test` domains — record where it will be added (per-host
  `Support.fs` or `fixtures/`). **Done:** the declaring/empty/malformed handoff JSON is built inline
  in each host's new `GovernedRefRoutingTests.fs` (no shared fixture file needed); the adapter C1–C8
  fixtures are inline JSON in `ConsumerTests.fs`. **Recorded deviation:** the shared `validCatalog`
  routes `src/**`→`package-api` (gates `format`,`build`) and `work/**`→`workflow` (gate `audit`), NOT
  the `build`/`test` domains the spec prose names illustratively — the behavioral assertions use the
  real catalog's gate ids (`package-api:build`, `workflow:audit`); the binding behavior is identical.

**Checkpoint**: Baseline green and reference goldens/fixtures identified.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Ship the one new pure primitive — `Consumer.candidatePaths` — FSI-first with its own
unit tests, surface re-bless, and the host scope-guard verification. This phase changes NO host
behavior (nothing calls `candidatePaths` yet), so every existing golden stays byte-identical here.

**⚠️ CRITICAL**: All three user stories depend on this function existing and being surface-blessed.

- [X] T003 [P1] Add the `val candidatePaths: reads: Reader.HandoffRead list -> GovernedPath list`
  signature (with the doc-comment from the contract) to
  `src/FS.GG.Governance.Adapters.SddHandoff/Consumer.fsi`, immediately after `val consume`. Confirm
  `GovernedPath` is in scope via the existing `open` chain; add `open FS.GG.Governance.Config.Model`
  only if the surface check requires it. FSI before impl (Constitution I). Per
  contracts/consumer-candidatePaths.fsi.md.
- [X] T004 Add the adapter unit tests C1–C8 to
  `tests/FS.GG.Governance.Adapters.SddHandoff.Tests/ConsumerTests.fs` over hand-built `HandoffRead`
  JSON fixtures: C1 `[]`⇒`[]`; C2 consumable doc with no `governedReferences`⇒`[]`; C3 declared
  `src/A/x`,`tests/A/y`⇒normalized+sorted; C4 two docs overlapping⇒union de-duped; C5 consumable +
  malformed⇒only consumable's paths; C6 single version-mismatch doc⇒`[]`; C7 same path twice across
  work items⇒one entry; C8 back-slash raw path⇒normalized via `Reader.parse`. Write to FAIL first
  (the `val` exists but no body yet). (Depends on T003.)
- [X] T005 Implement `candidatePaths` in `src/FS.GG.Governance.Adapters.SddHandoff/Consumer.fs`
  exactly per the reference implementation (`List.choose` keep `Ok` parses → `List.collect` the
  `GovernedReferences[].Paths` → `List.distinct` → `List.sortBy`). Pure + total (Constitution VI):
  an `Error` parse contributes nothing (FR-008). Confirm C1–C8 now PASS. (Depends on T003, T004.)
- [X] T006 Re-bless the adapter surface baseline:
  `BLESS_SURFACE=1 dotnet test FS.GG.Governance.Adapters.SddHandoff.Tests`, then `git diff
  surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt` MUST show exactly ONE added `[Method]`
  `candidatePaths` line (strictly additive — research D7), no removed/altered surface. Confirm
  `SurfaceDriftTests.fs` is green without `BLESS_SURFACE`. (Depends on T005.)
- [X] T007 [P1] Scope-guard verification (research D8): inspect the three host surface-drift /
  scope-guard tests relaxed in F081 to permit the `Adapters.SddHandoff` edge —
  `tests/FS.GG.Governance.RouteCommand.Tests/SurfaceDriftTests.fs`,
  `tests/FS.GG.Governance.ShipCommand.Tests/SurfaceDriftTests.fs`,
  `tests/FS.GG.Governance.VerifyCommand.Tests/{SurfaceDriftTests.fs,ScopeGuardTests.fs,SeamModuleScopeGuardTests.fs}`.
  Confirm they gate at **assembly-reference** granularity (most likely — then NO change). If any
  enumerates the exact adapter members a host may call, extend it ADDITIVELY to permit
  `candidatePaths` alongside `consume`. Record the finding on this task line. **Finding:** all three
  host scope-guards gate at **assembly-reference** granularity (`GetReferencedAssemblies()`, permitting
  `FS.GG.Governance.Adapters.SddHandoff` as a whole — F081's relaxation); none enumerates the exact
  adapter members a host may call, so the new `candidatePaths` call needs **NO** scope-guard change.

**Checkpoint**: `candidatePaths` exists, is unit-proven (C1–C8), surface-blessed (+1 line), and no
host behavior has changed — every route/ship/verify golden is still byte-identical.

---

## Phase 3: User Story 1 — Declared governed surface drives gate selection (Priority: P1) 🎯 MVP

**Goal**: Promote declared `governedReferences` to first-class routing candidates in all three
hosts, so the surface a work item declares it governs selects the domain gates that own it — even
when the sensed diff is empty.

**Independent Test**: Load a handoff whose declared paths route to `build`/`test` with an empty
sensed change set through the real host `update`; assert `build:build`/`test:test` now appear in the
selected gates (and drive ship/verify verdicts) where the pre-feature pipeline selects neither.

> Tests first — write the V1/V3/V4 scenarios and confirm they FAIL before the seam edit (T008–T010).

- [X] T008 [P] [US1] Add the V1 route scenario (SC-001, US1 AS1) to a new
  `tests/FS.GG.Governance.RouteCommand.Tests/GovernedRefRoutingTests.fs` (register in the `.fsproj`
  `<Compile>` list + `Main.fs` if required): drive the real `route` host `update` with a handoff
  declaring `governedReferences` in `build`/`test` and an EMPTY sensed change set; assert
  `result.SelectedGates` contains `build:build` and `test:test`, each with a selecting-path naming
  the declared path and the real matched glob. Write to FAIL (pre-seam: neither gate selected).
- [X] T009 [P] [US1] Add the V3 ship verdict-flip scenario (SC-004, US1 AS2) to a new
  `tests/FS.GG.Governance.ShipCommand.Tests/GovernedRefRoutingTests.fs`: a handoff declaring paths
  under a `block-on-ship` domain + a failing change there, with a sensed diff that does NOT touch
  that domain; run `ship` in a blocking mode; assert the gate is in `Blockers` and the verdict is
  non-shippable — a flip caused solely by the declared surface. Write to FAIL first.
- [X] T010 [P] [US1] Add the V4 verify strict-blocking scenario (SC-004, US1 AS3) to a new
  `tests/FS.GG.Governance.VerifyCommand.Tests/GovernedRefRoutingTests.fs`: a handoff declaring paths
  in a domain whose gate is verify-blocking under `Strict`; run `verify --strict`; assert the verdict
  is blocked by that gate. Write to FAIL first.
- [X] T011 [US1] Apply the candidate-assembly seam edit to
  `src/FS.GG.Governance.RouteCommand/Loop.fs` `update` `Loaded(Valid facts)` arm exactly per
  contracts/host-candidate-seam.md: `let sensed = model.Candidates |> Option.defaultValue []`; `let
  declared = Consumer.candidatePaths model.Handoffs`; `let candidates = sensed @ declared |>
  List.distinct` before `Routing.route facts candidates`. Everything after `Routing.route` (registry,
  findings, `Route.select`, the F081 `consume` gate-union fold) UNCHANGED. No `Loop.fsi` change.
  Confirm T008 now PASSES. (Depends on T005, T008.)
- [X] T012 [P] [US1] Apply the IDENTICAL seam edit to `src/FS.GG.Governance.ShipCommand/Loop.fs`
  (`Ship.rollup` runs after the merge, unchanged). Confirm T009 now PASSES. (Depends on T005, T009;
  parallel-safe with T011/T013 — different file.)
- [X] T013 [P] [US1] Apply the IDENTICAL seam edit to `src/FS.GG.Governance.VerifyCommand/Loop.fs`
  (Verify's empty-selection short-circuit runs after the merge, unchanged). Confirm T010 now PASSES.
  (Depends on T005, T010; parallel-safe with T011/T012 — different file.)

**Checkpoint**: The declared governed surface drives gate selection across `route`/`ship`/`verify`;
V1/V3/V4 green. This is the MVP — the feature's headline behavior works. Pair with US3 (P1 safety
boundary) before considering it shippable.

---

## Phase 4: User Story 2 — A governed-routed gate is traceable to its declared path (Priority: P2)

**Goal**: Prove the provenance recorded on a declared-path-selected gate is the REAL path-map glob
(no synthetic self-glob leak) and that a path present in both sources is merged/de-duplicated once.

**Independent Test**: Inspect a declared-path-selected domain gate's selecting-path list — confirm
it records the declared path + the real matched glob; and that a path in both the sensed and
declared sources yields exactly one selecting-path entry, counted once in the cost rollup.

> These are assertions on the seam already edited in US1 — no new production code; test additions only.

- [X] T014 [P] [US2] Add the V2(a) real-glob-provenance assertion (SC-006, US2 AS1) to
  `tests/FS.GG.Governance.RouteCommand.Tests/GovernedRefRoutingTests.fs`: the declared-path-driven
  domain gate's selecting-path records the REAL path-map glob from `Route.select`, NOT the self-glob
  `consume` uses on the handoff's own `sdd-handoff:*` gates (research D5; H4). Verify in the same
  assertion that the handoff's own evidence/readiness/integrity gates still carry their self-glob
  pre-selection (FR-009 unchanged).
- [X] T015 [P] [US2] Add the V2(b) dedup/count-once assertion (SC-003, US2 AS2) to the same route
  test file: a path present in BOTH the sensed change set and `governedReferences` selects its gate
  with exactly ONE merged, deterministically-ordered selecting-path entry (paths ordered by
  normalized-path ordinal) and is counted ONCE in the cost rollup — zero double-counting (FR-006,
  H3). (Depends on T011.)

**Checkpoint**: Declared-path selection is explainable (real glob) and de-duplicated; US1 + US2
green.

---

## Phase 5: User Story 3 — Absent / empty / bad handoff stays a byte-identical no-op (Priority: P1)

**Goal**: Hard safety boundary — the seam must not change output for anyone who has not declared
`governedReferences`, and a bad document must not widen enforcement. Protects every existing golden.

**Independent Test**: Run route/ship/verify with (a) no handoff, (b) an empty-`governedReferences`
handoff, (c) a malformed/version-mismatched handoff; assert byte-identical output in (a)/(b) and that
(c) still fires the blocking integrity gate while contributing zero routing candidates.

- [X] T016 [P] [US3] Add the V5(a) no-handoff no-op assertion (SC-002, US3 AS1) to each host's
  `GovernedRefRoutingTests.fs` (route/ship/verify): with `model.Handoffs = []`, `candidatePaths = []`,
  so `candidates = sensed @ [] |> List.distinct ≡ sensed` — assert route/ship/verify output is
  byte-identical to the captured pre-feature baseline (H1). (Depends on T011/T012/T013.)
- [X] T017 [P] [US3] Add the V5(b) empty-`governedReferences` no-op assertion (SC-002, US3 AS2) to
  the same per-host files: a consumable handoff that declares an EMPTY `governedReferences`
  list contributes zero candidates ⇒ byte-identical output, AND the handoff's own
  evidence/readiness gates still pre-select as in F081 (the empty list affects ONLY domain-gate
  contribution). (Depends on T011/T012/T013.)
- [X] T018 [P] [US3] Add the V6 bad-document boundary assertion (SC-005, US3 AS3) to the same per-host
  files: a malformed / major-version-mismatched handoff contributes ZERO routing candidates (via
  `candidatePaths` keeping only `Ok` parses) yet its blocking integrity gate STILL appears in the
  verdict (via the unchanged `consume` fold) — no widened enforcement (FR-008, H5).
- [X] T019 [US3] Run the FULL existing route/ship/verify suites and confirm EVERY pre-existing golden
  and snapshot under `tests/FS.GG.Governance.{Route,Ship,Verify}Command.Tests/goldens/` is
  byte-identical to the T001 reference (`git diff --stat` on goldens empty). This is the regression
  gate for the seam edit — no golden may move. (Depends on T011, T012, T013.)

**Checkpoint**: The no-op safety boundary holds in all three hosts; the bad-document boundary holds;
zero existing goldens moved. With US1 + US3 green, the P1 surface is complete and shippable.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Docs lockstep (FR-012) and the end-to-end validation pass.

- [X] T020 [P] Update `docs/decisions/0002-sdd-governance-handoff-contract.md`: move queued item #3
  from "Optional: fold `governedReferences` into `Routing.route` inputs… or ignore" to **Resolved
  (F082)** — "`governedReferences` are first-class routing candidates, merged + de-duplicated with
  the sensed change set before `Routing.route`" (FR-012).
- [X] T021 [P] Update `docs/tutorials/sdd-governance-handoff.md` with a worked example showing a
  declared governed path driving domain-gate selection (declared surface → selected gate with real
  glob), in lockstep with the code (FR-012).
- [X] T022 Run the quickstart end-to-end (quickstart.md V1–V7) through the real pipeline, then the
  bounded whole-solution build `dotnet fsi build.fsx`; confirm the adapter + three host suites are
  green, the surface baseline grew by exactly one line, and the "Done when" checklist in quickstart.md
  is fully satisfied. (Depends on all prior phases.) **Verified:** adapter 39/39 (C1–C8 + the F081
  31), RouteCommand 91/91 (was 85: +V1/V2a/V2b/V5a/V5b/V6), ShipCommand 105/105 (was 101: +V3/V5a/V5b/V6),
  VerifyCommand 85/85 (was 81: +V4/V5a/V5b/V6) — all green; the bounded `dotnet fsi build.fsx` compiles
  the whole solution (0 warnings, 0 errors, ~22 s); the surface baseline grew by exactly ONE
  `[Method] candidatePaths` line; `git diff` over every `*/goldens/*` is empty (the absent/empty/bad
  handoff no-op keeps every existing route/ship/verify golden byte-identical).

---

## Dependencies & Execution Order

### Phase order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)** → **Phase 3 (US1)** → Phase 4 (US2) / Phase 5
  (US3) → **Phase 6 (Polish)**.
- Phase 2 BLOCKS all user stories (they all call `candidatePaths`).
- US2 (Phase 4) and US3 (Phase 5) both depend ONLY on US1's seam edits (T011–T013); given those, US2
  and US3 can proceed in parallel — they add tests on the same already-edited seam.

### Key cross-task dependencies

- T004 after T003 (tests need the `val`); T005 after T003+T004 (impl greens the unit tests);
  T006 after T005 (bless the blessed-by-impl surface).
- T011/T012/T013 each after T005 (the function must exist) and after their matching failing test
  (T008/T009/T010 respectively) — test-fails-before-impl (Constitution V).
- T015 after T011 (dedup assertion needs the route seam live).
- T016–T019 after T011+T012+T013 (no-op/regression guards need all three seams live).
- T020/T021 (docs) independent of code; T022 last.

### Parallel opportunities

- T002 ∥ rest of Setup; T003 and T007 are independent within Phase 2 (different files).
- T008 ∥ T009 ∥ T010 (three different test projects) — write all three failing scenarios together.
- T011 ∥ T012 ∥ T013 (three different `Loop.fs` files) — identical seam edit, no shared file.
- T014 ∥ T015; T016 ∥ T017 ∥ T018 (different assertions / files).
- T020 ∥ T021 (two different doc files).

---

## Implementation Strategy

### MVP scope

**US1 (Phase 3) is the MVP** — it delivers the headline behavior (declared surface drives gate
selection across route/ship/verify). Because US3 is a P1 safety boundary on the SAME seam, ship US1
**and** US3 together: US1 proves the feature works; US3 proves it broke nothing. US2 (P2,
explainability) is a fast follow on the same code.

1. Phase 1 Setup → Phase 2 Foundational (`candidatePaths` + bless; no behavior change yet).
2. Phase 3 US1 → seam edits live; V1/V3/V4 green → **STOP and VALIDATE** the headline flip.
3. Phase 5 US3 → no-op + bad-doc + golden regression guards green (P1 safety net).
4. Phase 4 US2 → provenance/dedup assertions green.
5. Phase 6 → ADR + tutorial + full quickstart pass.

### Per-story counts & traceability

| Story | Priority | Tasks | Maps to |
|-------|----------|-------|---------|
| (Foundational) | — | T003–T007 (5) | `candidatePaths` C1–C8, surface D7, scope-guard D8 |
| US1 | P1 (MVP) | T008–T013 (6) | SC-001, SC-004; V1/V3/V4; FR-001/002/004 |
| US2 | P2 | T014–T015 (2) | SC-003, SC-006; V2; FR-003/006 |
| US3 | P1 | T016–T019 (4) | SC-002, SC-005; V5/V6; FR-005/008/009 |
| Setup | — | T001–T002 (2) | baseline + fixtures |
| Polish | — | T020–T022 (3) | FR-012; quickstart V1–V7 |

**Total: 22 tasks.**

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- Real-evidence discipline: NEVER mark a behavioral task `[X]` on synthetic routing facts — the V1–V6
  scenarios must run through the real host `update`. Mark `[-]` with written rationale if skipped;
  never weaken an assertion to green a build (narrow scope + document instead).
- No new `Effect`/`Msg`/`Port`/`Model` field; no host `.fsi` change; no `src/` *core*
  (Routing/Route/Gates/Config) change — the only new surface is the one adapter `val`.
