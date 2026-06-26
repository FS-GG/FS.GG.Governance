---
description: "Task list for Release-Provenance Host Wiring (F26 wiring)"
---

# Tasks: Release-Provenance Host Wiring — `fsgg release` Pack/Version Boundary, the Attestation Sidecar, `release.json` v2, and the `fsgg verify` Release-Readiness Preview

**Input**: Design documents from `/specs/065-release-provenance-host-wiring/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D8), data-model.md, contracts/ (pack-boundary, attestation-snapshot, release-outputs, verify-preview, shared-declaration)

**Tests**: Required — this is a Tier 1 contracted change. Per Constitution V every behavioural claim lands with fail-before/pass-after evidence over the **real** F26 cores and real hosts; only the edge ports (execution, pack-output reader, artifact writer, release-fact sensor, head-revision sense) are faked, and any synthetic pack output carries `Synthetic` in the test name with a use-site disclosure.

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]` may run in parallel (distinct files, no incomplete-task dependency).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`–`US4`; `[T1]`/`[T2]` tier annotation omitted (whole row is Tier 1, matching the spec)
- All paths are repository-root-relative

## MVU/Elmish discipline (applies to every host-edge story)

Both hosts are existing MVU `Loop`/`Interpreter`/`Program`. Each host story emits explicit tasks for: the curated `.fsi` contract (grown `Model`/`Msg`/`Effect`/`ArtifactKind`/`Ports`), pure `update` transition tests, emitted-`Effect` assertions, and real interpreter-edge evidence (real `dotnet pack`, real atomic write). No I/O enters any pure core (FR-010).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: stand up the new shared declaration leaf project + its test project and register them; no behaviour yet.

- [X] T001 [P] Create `src/FS.GG.Governance.ReleaseDeclaration/FS.GG.Governance.ReleaseDeclaration.fsproj` (net10.0, `Directory.Build.props` inherited) with ProjectReferences `ReleaseRules`, `ReleaseFactsSensing`, `GateExecution`, `ValidationMatrix`, `Config` and a PackageReference to the already-pinned YamlDotNet (no new entry in `Directory.Packages.props`).
- [X] T002 [P] Create `tests/FS.GG.Governance.ReleaseDeclaration.Tests/FS.GG.Governance.ReleaseDeclaration.Tests.fsproj` + `Main.fs` (Expecto entry point, repo-standard).
- [X] T003 Add `src/FS.GG.Governance.ReleaseDeclaration` and `tests/FS.GG.Governance.ReleaseDeclaration.Tests` to `FS.GG.Governance.sln` (after T001, T002).

**Checkpoint**: solution restores with the two empty new projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the shared `ReleaseDeclaration` leaf (both hosts depend on it) and the frozen pre-wiring byte-identity baselines (the SC-005 safety anchor must be captured **before** any wiring changes behaviour).

**⚠️ CRITICAL**: no user-story work begins until the leaf parses and the baselines are frozen.

- [X] T004 Author `src/FS.GG.Governance.ReleaseDeclaration/Declaration.fsi` — curated surface: `PackableProject { Surface; PackCommand; Baseline }`, `ReleaseDeclaration { Rules; Expectations; Layout; PackableProjects; Matrix }`, `DeclError { Reason }`, `val parse: string list -> Result<ReleaseDeclaration, DeclError>` (contracts/shared-declaration.md).
- [X] T005 Implement `src/FS.GG.Governance.ReleaseDeclaration/Declaration.fs` — re-home the F55 rules/expectations/layout parse **verbatim** from `ReleaseCommand.Declaration`, plus the additive `packableProjects` and optional `matrix` parse; PURE/TOTAL, YamlDotNet parse-to-node, a malformed packable/matrix entry ⇒ `Error DeclError` (never partial facts). Depends on T004.
- [X] T006 Remove `src/FS.GG.Governance.ReleaseCommand/Declaration.fs` and `Declaration.fsi`; repoint `ReleaseCommand` `open`/usages at the shared `FS.GG.Governance.ReleaseDeclaration.Declaration` module (no behaviour change for the rules/expectations/layout it already parsed). Depends on T005.
- [X] T007 [P] Port the re-homed declaration tests into `tests/FS.GG.Governance.ReleaseDeclaration.Tests/DeclarationTests.fs` (the F55 rules/expectations/layout cases carried over from `ReleaseCommand.Tests/DeclarationTests.fs` + `ParseTests.fs`), and add additive-parse tests: `packableProjects` (surface/packCommand/baseline?), `matrix`, a `release.yml` with neither section ⇒ `PackableProjects = []` / `Matrix = None` (GD-3 backward-compat), a malformed entry ⇒ `Error DeclError`. Depends on T005.
- [X] T008 [P] Bless `surface/FS.GG.Governance.ReleaseDeclaration.surface.txt` (`BLESS_SURFACE=1 dotnet test …`, then re-run drift green). Depends on T004.
- [ ] T009 [P] Freeze pre-wiring byte-identity baselines as committed goldens for the SC-005 anchors: `route.json`, `ship.json`, a **no-declaration** `verify.json`, and the empty-additive-field (`fsgg.release/v2`) `release.json`, stored under each host's `Tests` fixtures for comparison in T024/T032.

**Checkpoint**: leaf parses and is baselined; the frozen goldens exist. Both hosts can now be wired.

---

## Phase 3: User Story 1 — Every packable project must pack at a bumped version before `fsgg release` passes (Priority: P1) 🎯 MVP

**Goal**: the release host packs every declared packable project through the F51 execution port, builds the real pack evidence, merges `factContributions` over the F54 facts, and calls `evaluateRelease` verbatim — so a failed/unbumped/downgraded pack blocks release with a named reason and the failed `Pack` run recorded (FR-001, FR-002, FR-003; contracts/pack-boundary.md).

**Independent Test**: run `fsgg release` over a temp product with several declared packable projects + a baseline; assert all-bumped ⇒ preconditions `Met`, exit 0; one pack fails ⇒ blocked + sentinel run recorded; one packs unbumped/downgraded ⇒ blocked naming project + version; verdict deterministic for identical state.

### Implementation for User Story 1

- [X] T010 [US1] Add the F26 + execution-port ProjectReferences to `src/FS.GG.Governance.ReleaseCommand/FS.GG.Governance.ReleaseCommand.fsproj`: `PackEvidence`, `Attestation`, `ReleaseReport`, `ValidationMatrix`, `AttestationJson`, `ReleaseDeclaration`, `CommandKind`, `Provenance`, `CostBudget`, `GateExecution`, `Snapshot` (no new external dependency).
- [X] T011 [US1] Grow `src/FS.GG.Governance.ReleaseCommand/Loop.fsi`: `ArtifactKind = ReleaseArtifact | AttestationArtifact`; `Effect` adds `PackProjects of (SurfaceId*GateCommand) list`, `SenseProvenance`, and `kind` on `WriteArtifact`; `Msg` adds `PacksRun of PackOutcome list`, `ProvenanceSensed of Revision*EnvironmentClass*BuilderIdentity`, and `kind` on `Wrote`; `Model` adds `Packs`/`Head`/`Environment`/`Builder`/`PackEvidence`/`Snapshot`/`Attestation`/`Report`/`Matrix`/`AttestationDoc`; `RunRequest` gains `AttestationOut` (data-model.md §3).
- [X] T012 [US1] Grow `src/FS.GG.Governance.ReleaseCommand/Interpreter.fsi`: `Ports` adds `Execute: ExecutionPort`, `PackRead: SurfaceId -> ExecutionOutcome -> PackOutcome`, `SenseHead: unit -> Revision`, `SenseEnvironment: unit -> EnvironmentClass`, `SenseBuilder: unit -> BuilderIdentity` (data-model.md §3.6).
- [X] T013 [US1] Implement the pure transition in `Loop.fs` `update`: on `DeclarationLoaded(Ok decl)` emit `SenseRelease(decl.Layout, decl.Expectations)` + `PackProjects (packCommands decl)` + `SenseProvenance`; on the three-way join (`Sensed` + `PacksRun` + `ProvenanceSensed`) compute `evaluatePack (baselines decl) outcomes`, overlay `factContributions pack` onto the three pack families of `sensed.Facts`, call `Release.evaluateRelease decl.Rules mergedFacts` **verbatim**, carry `ReleaseDecision`/`ExitCodeBasis` unchanged, and `decideMatrix (budgetFor profile Release) ScheduledOrRelease decl.Matrix` (FR-003, GP-4; data-model.md §3.5 steps 1–3,7). Depends on T011.
- [X] T014 [US1] Implement the pack edge in `Interpreter.fs`: interpret `PackProjects` per project via `GateExecution.Interpreter.senseExecution ports.Execute packCommand` → `{ Kind = Pack; Record = record }`, then `ports.PackRead surface outcome` classifying `Packed | PackedNoArtifact | PackFailed` (non-zero ⇒ `PackFailed` sentinel; zero-exit-no-artifact ⇒ `PackedNoArtifact`); the run is carried in **every** case (never dropped), request order preserved, fed back as `PacksRun`; interpret `SenseProvenance` → `ProvenanceSensed(SenseHead(), SenseEnvironment(), SenseBuilder())` (GP-1, GP-2). Depends on T012.
- [X] T015 [US1] Wire the real ports in `Program.fs`: `Execute` = the F51 `GateExecution` execution port (the same `verify`/`ship` use); `PackRead` locates the produced `.nupkg` under the constitution `~/.local/share/nuget-local/`, reads its packed version + computes its `ArtifactHash` (unreadable ⇒ `PackedNoArtifact(ArtifactUnreadable …)`, never a throw); `SenseHead` via the F016 `Snapshot` port; `SenseEnvironment`/`SenseBuilder` the normalized 064 senses (no username/host/clock). Depends on T014.
- [X] T016 [P] [US1] Pure `update` transition tests in `tests/FS.GG.Governance.ReleaseCommand.Tests/LoopTests.fs`: all-`Packed`+`Bumped` ⇒ three families `Met`, decision unblocked; one `PackFailed` ⇒ families `Unmet` ⇒ `evaluateRelease` blocks with named reason; `Unbumped`/`Downgraded` (packed-version-evaluated) ⇒ `VersionBump` `Unmet` ⇒ blocked naming project+version; a zero-exit pack that produced no artifact (`PackedNoArtifact`) ⇒ blocked with the "packed but no artifact produced" reason (spec edge case, distinct from `PackFailed`); `ReleaseDecision`/`ExitCodeBasis` carried verbatim (no re-derivation). Depends on T013.
- [X] T017 [P] [US1] Emitted-`Effect` assertions in `LoopTests.fs`: `DeclarationLoaded(Ok)` emits exactly `SenseRelease` + `PackProjects` + `SenseProvenance`; the composition fires only after all three of `Sensed`/`PacksRun`/`ProvenanceSensed`; a declared matrix is recorded admitted (`RunNow`) at `ScheduledOrRelease` yet **no** matrix-execution effect is emitted (decided, never invoked — FR-009). Depends on T013.
- [ ] T018 [US1] Real-`dotnet pack` pack-boundary fixture in `tests/FS.GG.Governance.ReleaseCommand.Tests/EndToEndTests.fs` over multiple declared packable projects: every project bumped ⇒ preconditions `Met`, exit 0, each pack recorded as a `Pack` run; one pack fails ⇒ blocked with named reason + failed run recorded with its sentinel; one packs unbumped/downgraded ⇒ blocked naming project+version; a project with no released-version baseline (`None`) is treated as a first release (`NoBaseline`) and is **not** blocked as a downgrade (spec edge case) (SC-001). Depends on T015.

**Checkpoint**: `fsgg release` actually packs and blocks on a failed/unbumped pack — the MVP behavioural change is observable.

---

## Phase 4: User Story 2 — `fsgg release` writes `release.json` v2 + the attestation sidecar and blocks distinctly from ship (Priority: P1)

**Goal**: assemble the immutable `ReleaseReport`, project `release.json` v2 (`ofReleaseReport`) + `attestation.json` (`ofAttestation`) from it, write both through the existing atomic writer, with the release verdict a blocking boundary distinct from ship and every existing golden byte-identical (FR-004, FR-005, FR-007; contracts/release-outputs.md, contracts/attestation-snapshot.md).

**Independent Test**: a mergeable-but-not-releasable product ⇒ `fsgg ship` exit 0 while `fsgg release` exit 1 with a distinct basis, `release.json` v2 carries the failing precondition, `attestation.json` carries the compatible-shape marker; a fully releasable product run twice ⇒ both new artifacts byte-identical, `route.json`/`ship.json` byte-identical to baselines.

### Implementation for User Story 2

- [X] T019 [US2] Extend `Loop.fs` `update` (after the T013 join) with the sidecar assembly: `auditSnapshot` with `base = head = sourceCommit` and `artifactDigests` from `Packed` outcomes only (D2), `Attestation.summarize snapshot pack`, `Report.assemble decision sensed pack attestation`, `ReleaseDoc = ReleaseJson.ofReleaseReport report`, `AttestationDoc = AttestationJson.ofAttestation report.Attestation`, then emit `WriteArtifact(ReleaseArtifact, …)` then `WriteArtifact(AttestationArtifact, …)` (data-model.md §3.5 steps 4–9; GA-1, GA-2). Depends on T013.
- [X] T020 [US2] Interpret `WriteArtifact` per `ArtifactKind` in `Interpreter.fs` through the host's **existing** temp+rename `ArtifactWriter`; `Wrote(kind, Error)` ⇒ `ToolError` (exit 4, never `Blocked`); `EmitSummary` fires only after **both** writes succeed (GR-2). Depends on T011.
- [X] T021 [US2] Add the `--attestation-out` flag (default `<repo>/readiness/attestation.json`) to the arg parse + `RunRequest.AttestationOut` in `Loop.fs`/`Program.fs`, mirroring 064's `--provenance-out`. Depends on T011.
- [X] T022 [P] [US2] Re-run determinism fixture in `tests/FS.GG.Governance.ReleaseCommand.Tests/DeterminismTests.fs`: `release.json` v2 + `attestation.json` byte-identical across two runs over unchanged inputs; pack duration retained only as sensed `durationNanos`, excluded from identity (SC-003, GA-3). Depends on T019, T020.
- [ ] T023 [P] [US2] Mergeable-but-not-releasable + fully-releasable pair in `EndToEndTests.fs`: `fsgg ship` exit 0 while `fsgg release` exit 1 with a release basis distinct from ship and the failing precondition in `release.json` v2; a fully-releasable product ⇒ exit 0 clean; assert the publish plan, trusted-publishing posture, and template pins appear as named preconditions in `release.json` v2 — satisfied state for the fully-releasable product, unmet state with a named reason for the not-releasable one (FR-008) (SC-002, GR-4). Depends on T019, T020.
- [ ] T024 [P] [US2] Byte-identity goldens in `tests/FS.GG.Governance.ReleaseCommand.Tests/PersistenceEdgeTests.fs` vs the T009 frozen baselines: `route.json`/`ship.json` unchanged; an empty-additive `release.json` byte-identical to the F26-blessed v2 golden (SC-005, contracts/release-outputs.md anchors). Depends on T019, T009.
- [X] T025 [P] [US2] Attestation invariants test in `tests/FS.GG.Governance.ReleaseCommand.Tests/FailureTests.fs`: subjects come only from `Packed` outcomes (a failed/no-artifact pack ⇒ no attested subject), the `CompatibleShapeNotFormalCompliance` marker is always present (FR-007, GA-1, GA-2). Depends on T019.

**Checkpoint**: the publication boundary writes its two deterministic artifacts and blocks distinctly from ship; existing goldens proven byte-identical. **US1+US2 = P1 MVP complete.**

---

## Phase 5: User Story 3 — `fsgg verify` previews release readiness advisorily, and defers the broad matrix (Priority: P2)

**Goal**: the verify host, when `.fsgg/release.yml` is present, senses F54, evaluates F53 into a `ReleaseDecision`, assembles a `ReleaseReport` with an **empty** `PackEvidenceSet` and an attestation from verify's existing `Audit`, previews it into the advisory `releaseReadiness` block, and records the declared matrix `Deferred` — never changing verify's exit code and byte-identical when no declaration is present (FR-006, FR-009; contracts/verify-preview.md). Verify does **not** pack.

**Independent Test**: `fsgg verify` on a product with a declaration ⇒ `verify.json` carries `releaseReadiness` (`advisory: true`) with the same sensed evidence, exit unchanged; a declared matrix recorded deferred (not run); a no-declaration run byte-identical to its frozen golden.

### Implementation for User Story 3

- [X] T026 [US3] Add ProjectReferences to `src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj`: `ReleaseRules`, `ReleaseFactsSensing`, `PackEvidence`, `Attestation`, `ReleaseReport`, `ValidationMatrix`, `ReleaseDeclaration` (`GateExecution`/`CommandKind`/`Provenance`/`CostBudget`/`Snapshot` already referenced).
- [X] T027 [US3] Grow `src/FS.GG.Governance.VerifyCommand/Loop.fsi`: `Model` adds `ReleasePreview: VerifyReleasePreview option` (never affects `Exit`) and `ReleaseMatrix: MatrixPlan option` (data-model.md §4.1).
- [X] T028 [US3] Grow `src/FS.GG.Governance.VerifyCommand/Interpreter.fsi`: `Ports` adds `SenseRelease: SourceLayout -> ReleaseExpectations -> SensedRelease` (declaration-gated) (data-model.md §4.2).
- [X] T029 [US3] Implement the declaration-gated path in `Loop.fs` `update`: attempt `.fsgg/release.yml` via the existing `Files` port through the shared leaf; **if present and parses** → `evaluateRelease decl.Rules sensed.Facts`, `assemble decision sensed PackEvidenceSet.empty (summarize model.Audit PackEvidenceSet.empty)`, `ReleasePreview = Some (preview report)`, `ReleaseMatrix = Some (decideMatrix (budgetFor profile Verify) InnerLoop decl.Matrix)`, and switch the projection to `VerifyJson.ofVerifyDecisionWithPreview … model.ReleasePreview`; **absent/unparsable** ⇒ `ReleasePreview = None` (byte-identical projection). `PackEvidenceSet.empty = { Verdicts=[]; Runs=[]; NoPackableProjects=true }` constructed at the edge (GV-1, GV-2, GV-3). Depends on T027.
- [X] T030 [US3] Wire the real `SenseRelease` (F54) port in `VerifyCommand/Program.fs`. Depends on T028.
- [X] T031 [P] [US3] Tests in `tests/FS.GG.Governance.VerifyCommand.Tests/EndToEndTests.fs`: with a declaration ⇒ `releaseReadiness` present (`advisory: true`) with the same sensed evidence the release boundary would, verify exit unchanged by the preview; an unreleasable-but-mergeable product still exits per the unchanged F56 five-code scheme; a declared matrix recorded `Deferred` and **not** run (SC-004, GV-1, GV-3). Depends on T029, T030.
- [X] T032 [P] [US3] No-declaration byte-identity test in `tests/FS.GG.Governance.VerifyCommand.Tests/PersistenceEdgeTests.fs`: a `verify.json` run with no `.fsgg/release.yml` byte-identical to the T009 frozen golden (no `releaseReadiness` block, no schema bump) (SC-005, GV-4). Depends on T029, T009.

**Checkpoint**: verify previews release readiness advisorily and defers the matrix; no-declaration `verify.json` proven byte-identical.

---

## Phase 6: User Story 4 — The publication boundary runs standalone and fails safely (Priority: P2)

**Goal**: the release boundary draws only on product-local sources; a product with no packable projects is vacuously satisfied; a missing/unreadable pack output, absent provenance input, or missing publish plan surfaces a clear input-vs-defect diagnostic, blocks release, and emits no hollow attestation; reordering projects/runs changes nothing (FR-013, FR-014; research D8, contracts/pack-boundary.md GP-5, contracts/attestation-snapshot.md GA-4/GA-5).

**Independent Test**: a standalone product ⇒ decision from product-local sources only; no packable projects ⇒ vacuously satisfied with "no packable projects"; corrupt/remove a pack output / provenance input / publish plan ⇒ clear input diagnostic (exit 3, distinct from tool defect 4), blocked, no hollow attestation; reordered projects/runs ⇒ byte-identical.

### Implementation for User Story 4

- [X] T033 [US4] Route the edge signals into the host's existing input-vs-defect categories in `ReleaseCommand` `Loop.fs`/`Interpreter.fs`: `NoPackableProjects` ⇒ vacuously satisfied + reported; `PackedNoArtifact(ArtifactUnreadable …)` / an absent head-revision / a missing publish plan ⇒ `InputUnavailable` (exit 3), blocked, **no** hollow attestation and **no** fabricated pass — never a swallowed error or `ToolError` mis-categorization (FR-014, GA-5). Depends on T015, T019.
- [X] T034 [P] [US4] Standalone fixture in `tests/FS.GG.Governance.ReleaseCommand.Tests/ScopeGuardTests.fs`: a generated product checked out on its own ⇒ the pack/version/publish evaluation and attestation draw only on product-local sources (no monorepo-only path) (FR-013, SC-006). Depends on T033.
- [X] T035 [P] [US4] No-packable-projects fixture in `tests/FS.GG.Governance.ReleaseCommand.Tests/DegradeTests.fs`: empty `PackableProjects` ⇒ pack precondition vacuously satisfied, report states "no packable projects", no pack fabricated (GP-5). Depends on T033.
- [X] T036 [P] [US4] Safe-failure fixtures in `tests/FS.GG.Governance.ReleaseCommand.Tests/FailureTests.fs`: an unreadable pack output / an absent provenance input / a missing publish plan ⇒ a clear input diagnostic naming the source (exit 3, distinct from tool defect 4), release blocked, no hollow attestation, no fabricated pass (FR-014, SC-006). Depends on T033.
- [X] T037 [P] [US4] Reorder-determinism fixture in `tests/FS.GG.Governance.ReleaseCommand.Tests/DeterminismTests.fs`: reordering the declared packable projects / recorded runs ⇒ byte-identical package evidence, release verdict, attestation summary, and report (`evaluatePack` sorts verdicts by `(SurfaceId, ArtifactPath)`; `Runs` preserve real execution order) (FR-011, SC-006, GA-4). Depends on T019.

**Checkpoint**: standalone + safe-failure guarantees hold; the publication boundary never fabricates evidence.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: re-bless the grown host surfaces, prove the dependency boundary, update the deferred-task/roadmap docs, and run the full-suite gate.

- [X] T038 [P] Re-bless `surface/FS.GG.Governance.ReleaseCommand.surface.txt` (grown `Effect`/`Msg`/`Model`/`ArtifactKind`/`Ports`/`RunRequest`).
- [X] T039 [P] Re-bless `surface/FS.GG.Governance.VerifyCommand.surface.txt` (`Model.ReleasePreview` + `ReleaseMatrix` + the preview projection).
- [X] T040 [P] Dependency-boundary check: `Directory.Packages.props` unchanged (no new external/NuGet dependency); the seven F26 baselines unchanged; no pure core gains a filesystem/process reference (FR-010; quickstart "Constitution gate checks").
- [X] T041 Flip the deferred F26 Phase 8 tasks (`T048–T055`, `T057`, `T062`) to complete in `specs/061-verify-release-provenance/tasks.md`, citing this row as the wiring that landed them.
- [X] T042 [P] Update the roadmap "Remaining" note in `docs/initial-implementation-plan.md` to record the F26 release host edge as wired (the last of the three deferred host-wiring passes).
- [X] T043 Full-solution build + test sweep green with all existing goldens byte-identical and the new outputs deterministic (SC-007); run the quickstart.md scenarios 1–5. Depends on all prior phases.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; **blocks all user stories** (both hosts reference the shared leaf; SC-005 baselines must be frozen pre-wiring).
- **US1 (Phase 3, P1)**: depends on Foundational. The MVP slice.
- **US2 (Phase 4, P1)**: depends on US1 (the report assembly extends the same `update` join). US1+US2 together form the coherent P1 boundary.
- **US3 (Phase 5, P2)**: depends on Foundational (the shared leaf); independent of the release host's pack edge (verify does not pack). Conceptually follows US1/US2 per spec priority.
- **US4 (Phase 6, P2)**: depends on US1+US2 wiring (it exercises the release host's edge categories and determinism).
- **Polish (Phase 7)**: depends on all wired stories.

### Within Each User Story

- `.fsi` contract before `.fs` body; pure `update` transition before interpreter edge; emitted-effect + pure-transition tests before/alongside the real-`dotnet pack` E2E evidence (write the test, see it fail, then green).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

### Parallel Opportunities

- Phase 1: T001, T002 in parallel (then T003).
- Phase 2: T007, T008, T009 in parallel after the leaf (T004→T005→T006).
- US1: T016, T017 in parallel after T013; T018 after the edge (T015).
- US2: T022, T023, T024, T025 in parallel after T019/T020.
- US3: T031, T032 in parallel after T029/T030.
- US4: T034, T035, T036, T037 in parallel after T033 (T037 after T019).
- Polish: T038, T039, T040, T042 in parallel; T041 then T043 last.

---

## Summary

| Phase / Story | Tasks | Count |
|---|---|---|
| Phase 1 — Setup | T001–T003 | 3 |
| Phase 2 — Foundational (shared leaf + frozen baselines) | T004–T009 | 6 |
| US1 (P1) — pack/version boundary 🎯 MVP | T010–T018 | 9 |
| US2 (P1) — `release.json` v2 + attestation sidecar | T019–T025 | 7 |
| US3 (P2) — verify advisory preview | T026–T032 | 7 |
| US4 (P2) — standalone + safe failure | T033–T037 | 5 |
| Phase 7 — Polish | T038–T043 | 6 |
| **Total** | | **43** |

**Suggested MVP scope**: Phases 1–4 (Setup + Foundational + **US1 + US2**) — the P1 publication boundary: `fsgg release` packs every declared project, blocks on a failed/unbumped pack, writes `release.json` v2 + `attestation.json`, and blocks distinctly from ship, with every existing golden proven byte-identical. US3 (verify preview) and US4 (standalone/safe-failure) are the P2 increments.

**Parallel opportunities identified**: 23 tasks marked `[P]` (the two setup projects; the foundational tests/baseline freezes; the per-story test suites; and the polish re-bless/boundary/doc tasks), concentrated in the test and documentation work where files are disjoint.
