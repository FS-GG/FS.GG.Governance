---
description: "Task list — Per-Finding Rule Identity (068): additive `ruleId` across audit/verify/route + one new `RuleIdentity` leaf"
---

# Tasks: Per-Finding Rule Identity

**Input**: Design documents from `/specs/068-finding-rule-id/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/finding-rule-id.md](./contracts/finding-rule-id.md)

**Tests**: Required (Constitution Principle V; the spec defines Success Criteria SC-001…SC-006). Real cores and
the real projection writers are never mocked. Any synthetic input carries `Synthetic` in the test name with a
use-site disclosure.

**Change classification**: **Tier 1** (three JSON contracts gain an additive per-finding `ruleId` field; one new
public module — `FS.GG.Governance.RuleIdentity` — joins the surface). **No** `schemaVersion` bump, **no**
truth-table or verdict change, **no** projection-signature change. Output stays **byte-identical to the
pre-feature output for any input that produces no findings** (FR-007, SC-003).

**Elmish/MVU applicability**: **N/A** (Constitution IV). The feature adds no I/O, no state, and no multi-step
workflow — it is pure derivation inside the existing pure projections. No `Model`/`Msg`/`Effect` is warranted;
adding MVU ceremony here would violate Principle III (plan Constitution Check IV). Evidence is unit + projection
+ golden + invariance tests, not interpreter evidence.

**As-built evidence notes (068)**:
- The invariance sweep (T011 `RuleIdInvariance`) and the cross-surface match (T013 `CrossSurfaceRuleId`)
  landed in the **projection** test projects (`AuditJson.Tests/RuleIdTests.fs` and
  `VerifyJson.Tests/RuleIdTests.fs`) rather than the command test projects. They drive the genuine
  `Ship.rollup` + the real audit/verify writers over every `Profile × RunMode` — real evidence (Principle V),
  not a host smoke test. The run-level catalog `RuleHash` invariance (contract C4) is a property of the
  existing `FreshnessKey`/freshness layer (the catalog hash is content-of-rule-pack, profile/mode-independent
  by construction); it is documented in those test headers rather than re-derived, because the projection
  holds no catalog. Message-perturbation (T012) landed in `RouteJson.Tests/RuleIdTests.fs` (route findings
  carry the free-text `message`).
- T024 re-blessed every finding-bearing golden additively: the inline `verify.golden.json`, the
  `verify-surfacechecks.json` / `verify-no-surfaces.json` command goldens, **and** the seven
  `fixtures/enforcement/audit-snapshots/*.audit.json` (each verified additive-only — identical after
  stripping the inserted `ruleId`). The enforcement snapshots span the full base × maturity × mode × profile
  cross-product, so they double as SC-004 invariance evidence (the same `ruleId` at every enforcement cell).
- T015 byte-identity guards: the empty-case anchors stayed byte-identical (RouteCommand + ShipCommand golden
  suites green with no re-bless; `verify.no-declaration.json` unchanged) — SC-003 confirmed.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file / independent).
- **[Story]**: `[US1]`/`[US2]`/`[US3]` — which user story the task serves. Untagged = shared spine.
- Phases run in sequence; tasks within a phase may run in parallel where marked `[P]`.

---

## Phase 1: Setup (new leaf project, references, anchors)

**Purpose**: Stand up the dependency-free `RuleIdentity` leaf and its test project, wire the (unused-yet)
ProjectReferences into the three projections, and pin the pre-change empty-case byte-identity anchors BEFORE any
writer changes.

- [X] T001 Create the new leaf project `src/FS.GG.Governance.RuleIdentity/FS.GG.Governance.RuleIdentity.fsproj`
  (mirror the packable leaf shape of `src/FS.GG.Governance.FreshnessKey/….fsproj`: `IsPackable=true`,
  `PackageId=FS.GG.Governance.RuleIdentity`, `RootNamespace=FS.GG.Governance.RuleIdentity`). Reference **only**
  `FSharp.Core` — **no governance ProjectReference**, **no new external/NuGet dependency**
  (`Directory.Packages.props` unchanged) — so it cannot introduce a cycle (research D7). `Compile` order:
  `RuleIdentity.fsi`, then `RuleIdentity.fs`. Add the project to `FS.GG.Governance.sln`. (`.fsi`/`.fs` bodies land
  in Phase 3; stub them minimally so the solution restores.)
- [X] T002 [P] Add a `RuleIdentity` ProjectReference to each of the three projection `.fsproj`s:
  `src/FS.GG.Governance.AuditJson/FS.GG.Governance.AuditJson.fsproj`,
  `src/FS.GG.Governance.VerifyJson/FS.GG.Governance.VerifyJson.fsproj`,
  `src/FS.GG.Governance.RouteJson/FS.GG.Governance.RouteJson.fsproj`. Build the solution; the projections compile
  against the new ref without using it yet (dependency direction stays one-way: projections → `RuleIdentity`).
- [X] T003 [P] Create the test project `tests/FS.GG.Governance.RuleIdentity.Tests/` (mirror a sibling leaf test
  project): `FS.GG.Governance.RuleIdentity.Tests.fsproj` referencing the new leaf + Expecto, with `RuleIdentityTests.fs`,
  `SurfaceBaselineTests.fs`, and `Main.fs` (the `.fs` test bodies land in Phase 2; register them in the `.fsproj`
  before `Main.fs` and in `Main.fs`). Add the project to `FS.GG.Governance.sln`.
- [X] T004 [P] Pin the **empty-case byte-identity anchors** (the SC-003 / contract C1 regression anchors that MUST
  NOT drift): `tests/FS.GG.Governance.VerifyCommand.Tests/goldens/verify.no-declaration.json` (and any empty
  `surfaceChecks` golden), the no-finding `route.json` golden, and the no-finding `ship.json`/`audit.json` golden.
  Record the producing commit in a header comment on each anchor-asserting test so the "no `ruleId`, no schema
  bump, no reordering" claim is falsifiable.
- [X] T005 Record the **evidence obligations** for this feature on the test-plan header (above): unit tests for
  the leaf (T006), projection field+placement tests (T008–T010), the all-profiles × all-modes invariance sweep
  (T011), message-perturbation (T012), cross-surface match (T013), the no-`unattributed` negative (T014), and the
  no-findings byte-identity guards (T015). Note explicitly that **Constitution IV (Elmish/MVU) is N/A** — the
  feature adds no I/O, state, or workflow; it is pure derivation inside the existing pure projections (plan
  Constitution Check IV), so there is no interpreter evidence to produce.

**Checkpoint**: Solution restores and builds with the new leaf + refs; the empty-case anchors are identified and
pinned; the evidence obligations (incl. the MVU-N/A rationale) are recorded; no behavior has changed yet.

---

## Phase 2: Tests (write first — MUST FAIL before Phase 3) ⚠️

**Purpose**: Author every test against the real leaf + real projection writers, register them, and confirm the
fail-before state. The leaf-absent / field-absent tests must fail; the no-findings byte-identity guards (T015)
pass already and protect SC-003.

- [X] T006 [P] [US1] `tests/FS.GG.Governance.RuleIdentity.Tests/RuleIdentityTests.fs` — unit tests for the leaf:
  `gate "d:c"` → `"gate:d:c"`, `boundary "unknownGovernedPath"` → `"boundary:unknownGovernedPath"`,
  `surface "d" "code"` → `"surface:d:code"`, `release "k"` → `"release:k"`, `unattributed "r"` →
  `"unattributed:r"`; the **five prefixes are disjoint**; `ruleIdToken` is total and deterministic (same input ⇒
  byte-identical token); `unattributed` never yields an empty id (data-model §1). **Must FAIL now** (module absent).
- [X] T007 [P] `tests/FS.GG.Governance.RuleIdentity.Tests/SurfaceBaselineTests.fs` — surface-drift baseline test
  for the new module (mirror a sibling leaf baseline). **Must FAIL now** (no baseline established yet); blessed in T025.
- [X] T008 [P] [US1] `tests/FS.GG.Governance.AuditJson.Tests` — over a fixture with ≥1 gate item **and** ≥1
  finding item, assert each emitted item carries `ruleId` as the **first field after `id`** (`GateItem` →
  `gate:<domain>:<check>`; `FindingItem` → `boundary:<token>`, before `path`), the nested `enforcement` object is
  unchanged, and two runs over identical inputs are byte-identical (contract C2; SC-001, FR-002). **Must FAIL now**.
- [X] T009 [P] [US1] `tests/FS.GG.Governance.VerifyJson.Tests` — assert `ruleId` after `id` on each enforced item
  (same shape as audit) **and** on each `surfaceChecks` element (`surface:<domain>:<code>`, after the element's
  leading id field per the F24 order), with all pre-existing fields unchanged (contract C2; SC-001). **Must FAIL now**.
- [X] T010 [P] [US1] `tests/FS.GG.Governance.RouteJson.Tests` — assert `ruleId` after `id` on each
  `UnknownGovernedPathFinding` (`boundary:<token>`, before `path`) and on each selected gate (`gate:<domain>:<check>`,
  after `id`); boundary ids are non-empty and **distinguishable** from gate ids by prefix (contract C2; SC-006,
  FR-008). **Must FAIL now**.
- [X] T011 [P] [US2] Invariance sweep `RuleIdInvariance` in `tests/FS.GG.Governance.VerifyCommand.Tests` **and**
  `tests/FS.GG.Governance.ShipCommand.Tests` — a finding-bearing fixture evaluated under every `Profile`
  (`light`/`standard`/`strict`/`release`) and every `RunMode`: the **set of `ruleId`s is byte-identical** across
  all combinations, **no finding is dropped**, and only `enforcement.effectiveSeverity` may differ; assert the
  sensed catalog `RuleHash` is identical across the same runs (rule-hash anchor, contract C4; SC-002, SC-004,
  FR-003/FR-004/FR-005). **To cover SC-004's full `base-severity × maturity × mode × profile` truth table**
  (not just `profile × mode`), drive the fixture's finding across the base-severity and maturity axes too — or,
  if those axes are already exhausted by the reused `deriveEffectiveSeverity` truth-table tests, assert here only
  the **id-invariance overlay** (the `ruleId` is unchanged at every cell `deriveEffectiveSeverity` already
  covers) and cite that suite in the test header so SC-004's matrix is provably exhausted, not narrowed.
  **Must FAIL now** (no `ruleId` field).
- [X] T012 [P] [US2] Message-perturbation test (in the projection or command test project) — altering a finding's
  `Message`/`Reason` text leaves its `ruleId` **unchanged** (contract C3.3; FR-009, SC-002). **Must FAIL now**.
- [X] T013 [P] [US3] Cross-surface test `CrossSurfaceRuleId` in `tests/FS.GG.Governance.VerifyCommand.Tests` —
  produce `verify.json` and `audit.json` over identical inputs; a finding present in both carries an **identical
  `ruleId`** on both surfaces (contract C3.4; FR-006, SC-005). **Must FAIL now**.
- [X] T014 [P] [US1] Negative test (in the route/verify projection tests) — no projection emits an
  `unattributed:` token for the standard fixtures; every emitted `ruleId` is non-empty and source-prefixed
  (contract C3.5; FR-008/FR-010, SC-006). **Also record, in the test header, the two intentionally-unreachable
  constructors on the current projection paths**: (a) `unattributed:` — its *positive* emission (FR-010's
  disclosed-marker behavior) is unreachable because every current finding has a concrete source, so it is
  covered by the constructor unit test (T006) only, never a positive projection test (research D6); and
  (b) `release:` — authored + unit-tested (T006) but emitted by **no** writer (the three writers cover only
  `gate`/`boundary`/`surface`), so it is present as future-proofing, not dead-by-accident. This keeps both
  unreachable paths a recorded design choice, mirroring the `unattributed:`-absent assertion.
  **Must FAIL now** (no `ruleId` at all yet).
- [X] T015 [P] No-findings **byte-identity guard** tests (the SC-003 anchors from T004) in the Verify/Route/Ship
  command test projects: a no-findings run is byte-identical to the pinned empty-case golden — no `ruleId`, no
  `schemaVersion` change, no field reordering (contract C1; FR-007, SC-003). **Passes now and after** Phase 3 (the guard).
- [X] T016 Register every new test file in its `.fsproj` (before `Main.fs`) and in `Main.fs`; run the affected
  suites and **record the fail-before evidence** for T006–T014 (and that T015 is green).

**Checkpoint**: All behavioral tests exist and fail for the right reason (the leaf/field is absent), except the
no-findings byte-identity guards (T015) which pass.

---

## Phase 3: Foundational implementation — the leaf + four writer edits (US1) 🎯 MVP

**Goal**: Add the `RuleIdentity` leaf, then derive `ruleId` at each projection edge from the source value the
writer already holds. This single spine satisfies US1 (every finding names its rule), and — because the
derivation reads no profile/mode/message — US2 and US3 fall out structurally (proven in Phases 4–5). **MVP = US1.**

**Independent Test**: T008/T009/T010 (projection field present at the contracted position) go green; T015
(no-findings byte-identity) stays green.

### 3a — The leaf (Constitution I/II)

- [X] T017 `src/FS.GG.Governance.RuleIdentity/RuleIdentity.fsi` — the sole declaration of the surface
  (data-model §1): `type RuleId = RuleId of string`; smart constructors `gate: string -> RuleId`,
  `boundary: string -> RuleId`, `surface: string -> string -> RuleId`, `release: string -> RuleId`,
  `unattributed: string -> RuleId`; and the total `ruleIdToken: RuleId -> string`. Module carries the
  `ModuleSuffix` representation as drafted. No access modifiers leak to the `.fs` (Constitution II).
  **Before the `.fs` body (Constitution I.2-3)**: exercise this surface in FSI — load the packed leaf (or a
  prelude `#load`) and call each constructor + `ruleIdToken` to confirm the shape reads naturally (the five
  prefixed tokens, the disjoint prefixes) — and paste the transcript into the test header / PR. The shape is
  validated by use before any implementation defends it.
- [X] T018 `src/FS.GG.Governance.RuleIdentity/RuleIdentity.fs` — pure source-prefixing constructors
  (`gate g` ⇒ `"gate:" + g`, etc.) and `ruleIdToken (RuleId s) = s`; no clock/host/env/ordering input; no access
  modifiers (Constitution II). Makes T006 pass.

### 3b — The four writer edits (the additive contract)

- [X] T019 [US1] `src/FS.GG.Governance.AuditJson/AuditJson.fs` `writeItem` — after each `id` write, emit
  `w.WriteString("ruleId", RuleIdentity.ruleIdToken …)`: `GateItem g` → `gate (gateIdValue g)` (after `id`);
  `FindingItem (fid, _)` → `boundary (findingIdToken fid)` (after `id`, **before** `path`). Dispatch stays
  exhaustive over `EnforcedItemId` (no wildcard). Update the `writeItem` doc comment to record the new field.
  Makes T008 pass.
- [X] T020 [US1] `src/FS.GG.Governance.VerifyJson/VerifyJson.fs` — apply the same `writeItem` edit as audit
  (gate→`gate:`, finding→`boundary:`), **and** in the `surfaceChecks` element writer emit `ruleId` as
  `surface <domain> <code>` after the element's leading id field (data-model §3/§4). **First confirm the actual
  shape of `SurfaceChecks.Model.SurfaceFinding.Domain`/`.Code`**: if they are already `string`, pass them
  directly; if they are wrapped (a `DomainId`/`Code` newtype), use the existing accessor the `surfaceChecks`
  element writer already uses for its leading id field (do **not** invent a token function). Makes T009 pass.
- [X] T021 [US1] `src/FS.GG.Governance.RouteJson/RouteJson.fs` — `writeFinding`: emit
  `boundary (findingIdToken f.Id)` as `ruleId` after `id`, before `path`; `writeSelectedGate`: emit
  `gate (gateIdValue g.Id)` as `ruleId` after `id`. Makes T010 and T014 pass.

**Checkpoint**: T008/T009/T010/T014 green; T015 byte-identity stays green; every emitted finding/item/gate now
carries a non-empty, source-prefixed `ruleId` at the contracted position (MVP — US1 delivered).

---

## Phase 4: User Story 2 — profile/mode/message invariance (P1)

**Goal**: Prove the id (and its mapped rule hash) is invariant across every profile and run mode, and across
message changes — the integrity payload the roadmap row is about.

**Independent Test**: T011 (`RuleIdInvariance`) and T012 (message-perturbation) green over the spine.

- [X] T022 [US2] Confirm `RuleIdInvariance` (T011) and the message-perturbation test (T012) are green and record
  the evidence: the `ruleId` set and the sensed catalog `RuleHash` are byte-identical across all profile/mode
  combinations, no finding is dropped, only `effectiveSeverity` differs, and a `Message`/`Reason` edit leaves
  `ruleId` unchanged. Note the structural argument (the derivation reads only the rule's structural identity —
  research D5; data-model §5) so the guarantee is checkable, not incidental (FR-003/FR-004/FR-005/FR-009;
  SC-002/SC-004).

**Checkpoint**: The "profiles never alter rule hashes / never drop findings / never change rule identity"
guarantee is demonstrated end-to-end at the JSON level.

---

## Phase 5: User Story 3 — group & trace by rule across surfaces (P2)

**Goal**: A finding common to `verify` and `ship` carries the same `ruleId` on both, enabling a join by rule id.

**Independent Test**: T013 (`CrossSurfaceRuleId`) green over the spine.

- [X] T023 [US3] Confirm `CrossSurfaceRuleId` (T013) is green and record the evidence: a finding present in both
  `verify.json` and `audit.json` over identical inputs derives from the same source value through the same
  constructor, so its `ruleId` matches across surfaces (FR-006, SC-005). Note the one-consistent-representation
  property (single prefixed-token grammar across all three projections — contract C2).

**Checkpoint**: All three user stories are independently demonstrated.

---

## Phase 6: Goldens, surface baseline, and the no-regression sweep

- [X] T024 [P] Re-bless every **finding-bearing** golden that gains `ruleId` in one commit
  (`BLESS_GOLDEN=1 dotnet test …`): the inline `VerifyJson`/`AuditJson`/`RouteJson` projection goldens, the
  `verify-surfacechecks.json` golden, and any route/ship finding-bearing command goldens. Verify with
  `git diff -- tests/**/goldens` that the diff is **exactly** the additive `ruleId` field at the contracted
  position — no other byte changes (quickstart "Re-blessing"; contract C2).
- [X] T025 Establish the `RuleIdentity` surface-drift baseline (`BLESS_SURFACE=1 dotnet test …`) and confirm
  `SurfaceBaselineTests.fs` (T007) is green. Confirm the three projection `.fsi` baselines
  (`AuditJson`/`VerifyJson`/`RouteJson`) are **untouched** — the writers gained a field but no public signature
  changed (plan Constitution Check II).
- [X] T026 [P] No-regression sweep: confirm the empty-case anchors (T004) are byte-identical and all
  `schemaVersion`s unchanged; confirm **no other host or pure core changed**, `Directory.Packages.props` is
  unchanged (no new dependency), and no projection-function signature changed (FR-007; SC-003). Cross-checks T015
  at the suite level.

---

## Phase 7: Bookkeeping & full gate

- [X] T027 [P] Flip the open Phase-5 rule-id rows to closed in `docs/initial-implementation-plan.md` — *"Emit
  every finding with rule id …"* and *"Ensure profiles never hide underlying verdicts, alter rule hashes, or
  remove findings from JSON"* — citing `068-finding-rule-id` as the slice that lands per-finding rule identity
  and the JSON-level rule-hash-invariance proof.
- [X] T028 [P] Update `CLAUDE.md`/README if their prose enumerates the per-finding fields emitted by
  `audit.json`/`verify.json`/`route.json` — add the additive `ruleId` field (non-contractual prose; the JSON
  contract lives in `contracts/finding-rule-id.md`).
- [X] T029 Full-solution gate: `dotnet build FS.GG.Governance.sln` clean (warnings-as-errors) then
  `dotnet test FS.GG.Governance.sln` green (no regression); run the [quickstart.md](./quickstart.md) scenarios
  SC-001…SC-006 (incl. the re-bless diff check and the empty-case byte-identity check) and record the evidence on
  this line.

---

## Dependencies

- **Phase 1 → Phase 2 → Phase 3 → Phase 4/5 → Phase 6 → Phase 7** run in sequence.
- **T004 (pin empty-case anchors) MUST precede any Phase 3 writer change** — it is the no-regression anchor.
- Within Phase 3: 3a (the leaf, T017→T018) precedes 3b (the four writer edits, T019/T020/T021, parallel-safe —
  different files).
- T024 (re-bless goldens) and T025 (surface baseline) depend on Phase 3 complete.
- Phases 4 and 5 are pure verification phases that ride on the Phase 3 spine; they may run in parallel once Phase 3 is green.

## Parallel opportunities

- Phase 1: T002/T003/T004 in parallel after T001 (the leaf must exist to reference and test).
- Phase 2: T006–T015 author in parallel (distinct files / sibling test projects); T016 registers and serializes them.
- Phase 3: after the leaf (T017→T018), the three writer edits T019/T020/T021 run in parallel (different files).
- Phase 7: T027/T028 in parallel; T029 last.

## MVP scope

**User Story 1** (every emitted finding names its originating rule): Phase 1 + Phase 2 (US1 tasks) + Phase 3 (the
leaf + four writer edits) + T024/T025. Delivers the core capability and the upstream prerequisite the roadmap
names as blocking the rule-hash guarantee. US2 (profile/mode/message invariance) and US3 (cross-surface join)
follow structurally from the same spine — the derivation reads only the rule's structural identity — and are
demonstrated in Phases 4 and 5 with no additional production code.

## Task count

- **Setup (Phase 1)**: 5 (T001–T005, incl. T005 evidence-obligations / MVU-N/A note)
- **Tests (Phase 2)**: 11 — US1: 5 (T006, T008, T009, T010, T014), US2: 2 (T011, T012), US3: 1 (T013),
  shared: 3 (T007 baseline, T015 byte-identity guard, T016 registration)
- **Foundational spine (Phase 3, US1/MVP)**: 5 (T017–T021)
- **US2 invariance (Phase 4)**: 1 (T022)
- **US3 cross-surface (Phase 5)**: 1 (T023)
- **Goldens/baseline/sweep (Phase 6)**: 3 (T024–T026)
- **Bookkeeping & gate (Phase 7)**: 3 (T027–T029)
- **Total**: 29
