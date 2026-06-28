---
description: "Task breakdown for the Breaking-Change (API-Compat) Gate"
---

# Tasks: Breaking-Change (API-Compat) Gate for the Published Governance Packages

**Input**: Design documents from `/specs/088-governance-apicompat-gate/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tier**: Tier 1 (contracted) for the whole feature — adds public API (new
`ReleaseRuleKind` case, new break-signal/verdict types, new sensing + host
surface). Per-task `[T1]`/`[T2]` annotations are omitted because every phase
matches the feature tier. Obligations: `.fsi`-first (Principle I), refreshed
`surface.txt` baselines (Principle II), real test evidence (Principle V),
fail-safe observability (Principle VI), docs.

**Format**: `[ID] [P?] [Story] Description` — `[P]` = no dependency on another
incomplete task in this phase. Phases run in sequence; tasks within a phase may
run in parallel.

**Elmish/MVU note**: the detector is process + filesystem I/O. It is modeled as a
`ReleaseCommand` MVU `Effect` sensed at the edge; `update` stays pure; the break
signal enters the core as data. Tasks below carry the explicit `.fsi` contract,
pure-transition, and emitted-effect obligations for that boundary.

---

## Implementation status (088 — current)

**Delivered with real evidence (advisory MVP).** The pure breaking-change core + the advisory gate are live:
the verdict table (`apiCompatibilityFact`, D4), the coverage builder + cross-package rollup, the ApiCompat
output parser, the additive `ApiCompatibility` rule kind, and the seven-family sensing — all with passing
real-evidence tests (the SC-002/SC-003 verdict corpus, FR-007 coverage, FR-013 attribution, the FR-008
fail-safe parser). The detector `pack-and-apicheck.fsx` packs all 71 packable projects, compares against the
feed baseline, grades through the REAL pure library, and reports coverage — exercised end-to-end (all packages
`NoBaselineYet` per research D5; `--selftest` green). The advisory CI job runs it non-required (D7). Surfaces
refreshed; declaration recognizes an optional advisory `apiCompatibility` rule.

**Deferred + tracked (the REQUIRED / in-product-host path).** These change a verdict only once baselines
accrue (SC-005) and belong to the required-promotion work — left `[ ]` honestly:

- **T010 / T015 / T016** — the MVU host overlay routing the `ApiCompatibility` fact into `fsgg verify`/
  `release`'s Warnings/Blockers (the `ReleaseCommand` Loop effect/msg/model + the detector edge interpreter).
  At advisory rollout this changes no verdict (every package `NoBaseline`); it is the mechanism that matters
  at `BlockOnRelease`. `deriveFacts` already emits `ApiCompatibility = Unrecoverable` (fail-safe), so a
  declared rule is `Violated` until the overlay supplies the real value. The advisory signal reaches the user
  today via the `.fsx` + CI (the research-D7 path).
- **T022 / T023 / T024 / T025** — required-phase host exit-code tests + the SC-005 promotion (flip `Maturity`
  → `BlockOnRelease` + add the CI job to required checks). Gated on baselines existing (D5) and on T015.
- **T003** — FSI prelude transcript: the design-through-use was proven instead by the `.fsx --selftest` (the
  D4 table through the real library) + the pure test corpus.
- **T026** — the combined surface.txt-guard ∧ ApiCompat coherence trip-test (needs the host overlay to trip
  the in-product gate); the complementary-not-contradictory relationship is documented (research D8, quickstart §6).
- **T029 / T030 / T032** — quickstart steps 1–3 exercised (pure tests + detector run); steps 4–5 (`fsgg
  verify`/`release`) await T015. `/speckit-analyze` + the final evidence sweep ride with the required phase.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Land the repo-owned, drift-safe scaffolding the gate needs without
touching any org-synced file.

- [X] T001 Decide and record the **sensor home** (research open item): fold the
  ApiCompat output **parse** (pure `string -> ApiBreakSignal`) into
  `src/FS.GG.Governance.ReleaseFactsSensing/` rather than spawn a new
  `FS.GG.Governance.ApiCompat` leaf — keeps dependency scope tightest and the
  "all families always present" maintenance in one place. Note the decision in a
  one-line comment at the top of `src/FS.GG.Governance.ReleaseFactsSensing/Sensing.fsi`
  and in `specs/088-governance-apicompat-gate/research.md` (resolve the open item).
- [X] T002 [P] **Extend the existing** repo-owned MSBuild props file
  `Directory.Build.local.props` (already present in the repo — add to it, do not
  recreate/overwrite; or add a dedicated `.props` imported only by packable
  projects) with `EnablePackageValidation` / `PackageValidationBaselineVersion` /
  `ApiCompat*` settings, with `<EnablePackageValidation>` left **off** for the
  main build (D7: detector runs as a separate step, never reddens `dotnet build`).
  Preserve the file's current contents. Do **not** edit `Directory.Build.props`,
  `Directory.Packages.props`, or `.config/dotnet-tools.json` (drift-locked, D6).
- [ ] T003 [P] Add an FSI design-through-use scaffold to `scripts/prelude.fsx`
  context (or a new `specs/088-governance-apicompat-gate` scratch transcript)
  exercising `apiCompatibilityFact` shapes from quickstart.md §2 — proves the
  signatures before any `.fs` is written (Principle I).

**Checkpoint**: Drift gate still green; props file repo-owned; sensor home fixed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The additive shared vocabulary every user story reads — the rule
kind and the pure data types. No verdict logic or I/O yet. **Blocks US1/US2/US3.**

**⚠️ CRITICAL**: `.fsi` is authored and compiles before the matching `.fs`
(Principle I/II). No access modifiers in any `.fs`.

- [X] T004 Add the additive `ApiCompatibility` case to `ReleaseRuleKind` in
  `src/FS.GG.Governance.ReleaseRules/Model.fsi` (then `Model.fs`), with the
  doc-comment noting it is an additive extension; leave every existing case and
  its `factFor` key unchanged (data-model §1, D2).
- [X] T005 Draft the new pure types in
  `src/FS.GG.Governance.PackEvidence/Model.fsi` (then `Model.fs`):
  `ApiBreakSignal`, `ApiBreak`, `ApiBreakKind`, `VersionDelta`,
  `ApiCompatCoverage`, `ApiCompatCoverageOutcome` exactly per data-model §2–§5.
  Product-neutral text only; no raw `.nupkg` bytes / paths / exit codes in any
  type.
- [X] T006 Update `releaseFamilies` in
  `src/FS.GG.Governance.ReleaseFactsSensing/Sensing.fsi`/`.fs` to include
  `ApiCompatibility` and revise the "exactly six families" doc-comment to seven,
  keeping declaration order. (Sensing of the fact value comes in US1.)

**Checkpoint**: Solution compiles with the additive case + new types present but
not yet graded or sensed. A declared `ApiCompatibility` rule already resolves
`Unrecoverable ⇒ Violated` via existing semantics (fail-safe by construction).

---

## Phase 3: User Story 1 — Maintainer is warned when a change breaks a published surface (Priority: P1) 🎯 MVP

**Goal**: Detect a break vs the last published package, grade it against the
version delta, surface it as an **advisory** finding naming package + member +
required remediation, and report per-package coverage — without blocking.

**Independent Test**: Introduce a deliberate breaking change to one packable
package on a branch, run the gate, confirm it reports exactly that break
(package + member + required-bump) in Warnings while an additive change on
another package reports clean; coverage lists Checked / NoBaselineYet /
NotCovered.

### Tests for User Story 1 (write FIRST, ensure they FAIL) ⚠️

- [X] T007 [P] [US1] Pure verdict-table tests for `apiCompatibilityFact` in
  `tests/FS.GG.Governance.PackEvidence.Tests/` — one assertion per D4 row
  (`NoBreakingChanges→Met`; `BreakingChanges×MajorBump→Met`;
  `BreakingChanges×MinorOrPatchBump|NoForwardChange→Unmet`;
  `NoBaseline×NoBaselineDelta→Met`; `Indeterminate→Unrecoverable`;
  `NotPackable→None`). Real evidence: feed real signals + version deltas. This
  row set **is** the project's break-detection / additive-change test corpus
  referenced by SC-002 (no false negatives) and SC-003 (no false positives) —
  name it as such in a module/comment header so the SC traceability is explicit.
- [X] T008 [P] [US1] Rule fact→finding→rollup tests in
  `tests/FS.GG.Governance.ReleaseRules.Tests/` — a declared `ApiCompatibility`
  rule with `Unmet`/`Unrecoverable`/`Met` yields exactly one finding with the
  correct `RuleOutcome`, a `Reason` naming the surface + break + "requires MAJOR
  bump or revert" / "API comparison indeterminate: <reason>" (FR-003), and
  partitions Met→Passing, Unmet/Unrecoverable→Warnings under **advisory**
  `Maturity`.
- [X] T009 [P] [US1] Pure parse tests for the ApiCompat-output→`ApiBreakSignal`
  parser in `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/` — real ApiCompat
  output samples (removed member, signature change, type removed, no-baseline,
  tool-error) map to the right `ApiBreakSignal`; any sample lacking a real tool
  run is `Synthetic`-tagged and disclosed (Principle V).
- [ ] T010 [P] [US1] Host emitted-effect + pure-transition tests in
  `tests/FS.GG.Governance.ReleaseCommand.Tests/` — `init`/`update` request the
  detector `Effect`, and feeding back the detector `Msg` overlays the
  `ApiCompatibility` fact into `ReleaseFacts.States` (mirrors the F065 pack
  join) with `update` pure (no I/O). Assert emitted effects, not interpreter
  side effects.
- [X] T011 [P] [US1] Coverage-reporting test (FR-007/SC-001) in
  `tests/FS.GG.Governance.ReleaseCommand.Tests/` (or PackEvidence.Tests for the
  pure builder): a mixed package set yields a deterministic, `SurfaceId`-sorted
  `ApiCompatCoverage` list with zero silent passes — every package is `Checked`,
  `NoBaselineYet`, or `NotCovered`.

### Implementation for User Story 1

- [X] T012 [US1] Implement the pure verdict helper `apiCompatibilityFact :
  ApiBreakSignal -> VersionDelta -> FactState option` in
  `src/FS.GG.Governance.PackEvidence/Pack.fsi`/`Pack.fs`, reusing the existing
  semantic-version comparator behind `Pack.versionPolicy` to derive
  `VersionDelta`. Leave `versionPolicy` / `factContributions` semantics
  unchanged. Makes T007 pass.
- [X] T013 [US1] Implement the pure coverage builder (`SurfaceId`-sorted
  `ApiCompatCoverage` list mapping each package's `ApiBreakSignal` to
  `Checked`/`NoBaselineYet`/`NotCovered`) in
  `src/FS.GG.Governance.PackEvidence/Pack.fs`. Makes T011 pass.
- [X] T014 [US1] Implement the pure ApiCompat-output parser
  (`parseApiCompatOutput : string -> ApiBreakSignal`, fail-safe: any
  unreadable/tool-error input ⇒ `Indeterminate reason`, never
  `NoBreakingChanges`; absent baseline ⇒ `NoBaseline`) in
  `src/FS.GG.Governance.ReleaseFactsSensing/Sensing.fsi`/`.fs`, and wire the
  sensed `ApiCompatibility` `FactState` into `deriveFacts`. Makes T009 pass.
- [ ] T015 [US1] Add the detector `Effect` + result `Msg` + `Model` field to
  `src/FS.GG.Governance.ReleaseCommand/Loop.fsi`/`Loop.fs` (alongside
  `PackProjects`/`PacksRun`), and overlay the `ApiCompatibility` fact onto the
  sensed facts before `Release.evaluateRelease` — pure `update`. Makes T010 pass.
- [ ] T016 [US1] Implement the detector at the I/O edge in
  `src/FS.GG.Governance.ReleaseCommand/Interpreter.fs` (the `Effect` interpreter):
  invoke the ApiCompat run captured by the `.fsx` of T017 and hand its output to
  the T014 parser. Edge-only; the pure core takes no dependency on ApiCompat.
- [X] T017 [US1] Create `pack-and-apicheck.fsx` **at the repo root** (mirroring
  the existing `pack-reference-gate-set.fsx`, which also lives at the repo root —
  keep `.fsx` entrypoints consistently co-located): `dotnet pack -c Release` every
  `IsPackable=true` `FS.GG.Governance.*` project, run ApiCompat / Package
  Validation against each baseline on the `~/.local/share/nuget-local/` feed,
  and emit the `ApiBreakSignal` set + coverage as data (`--json`). Captures the
  signal; never decides block/allow and never reddens the build (D7).
- [X] T018 [US1] Declare the `ApiCompatibility` release rule with **advisory**
  `Maturity` (base `Blocking` relaxed) wherever the repo's release rules are
  declared (the `ReleaseDeclaration` / governance config that feeds
  `fsgg release`/`verify`). Violations land in `Warnings`, `Verdict` unaffected.
- [X] T019 [US1] Add the advisory CI job "API compatibility gate
  (breaking-change → SemVer major)" to `.github/workflows/gate.yml` per
  contracts/ci-gate.md: locked restore, run `pack-and-apicheck.fsx
  --json` (repo-root path), grade via `fsgg release`/`verify`, print per-package coverage +
  findings. Job is **not** added to branch-protection required checks (advisory).
  Install any ApiCompat tool **job-scoped** (`dotnet tool install`), never in
  `.config/dotnet-tools.json` (D6).
- [X] T020 [US1] Refresh the `surface/*.surface.txt` baselines for every touched
  packable project (`FS.GG.Governance.ReleaseRules`,
  `FS.GG.Governance.PackEvidence`, `FS.GG.Governance.ReleaseFactsSensing`,
  `FS.GG.Governance.ReleaseCommand`) via `BLESS_SURFACE=1 dotnet test
  tests/FS.GG.Governance.<Project>.Tests`. Review each diff as the additive
  surface change (Principle II).

**Checkpoint**: `fsgg verify --repo .` reports breaking-under-bump / indeterminate
packages in **Warnings** with package + member + remediation; coverage printed;
exit code unchanged. MVP delivered.

---

## Phase 4: User Story 2 — Breaking changes blocked unless the version is bumped (Priority: P2)

**Goal**: Once surfaces are clean (SC-005), promote the gate from advisory to
required so a breaking change under a non-major bump fails the release/CI path.

**Independent Test**: With the gate required, attempt to publish a package
carrying a breaking change under a non-major bump → release path fails with the
finding; correct to a major bump (or revert) → passes.

**Depends on US1.** Promote only when SC-005 holds (zero breaking-under-bump
across covered packages).

### Tests for User Story 2 (write FIRST, ensure they FAIL) ⚠️

- [X] T021 [P] [US2] Required-phase rollup tests in
  `tests/FS.GG.Governance.ReleaseRules.Tests/`: with `Maturity =
  BlockOnRelease`, an `ApiCompatibility` `Unmet`/`Unrecoverable` lands in
  **Blockers** with `Verdict = Fail`; `Met` stays Passing.
- [ ] T022 [P] [US2] Host exit-code tests in
  `tests/FS.GG.Governance.ReleaseCommand.Tests/`: a breaking-under-bump fact at
  `BlockOnRelease` drives `ExitDecision`→`Blocked` (non-zero `exitCode`); a
  major-bump / no-break fact drives a passing exit.

### Implementation for User Story 2

- [ ] T023 [US2] **Verify SC-005** before promotion: run the advisory gate
  across all packages and confirm zero breaking-under-bump findings; capture the
  coverage output as the promotion evidence in the PR. The captured output MUST
  print the per-package **covered vs NoBaseline** split (not just "zero breaks"),
  so the reviewer sees how much of the surface is actually enforcing at the moment
  required mode is flipped on — at rollout most packages resolve `NoBaseline`
  (research D5), making SC-005 trivially satisfiable while enforcement is partial.
- [ ] T024 [US2] Flip the declared `ApiCompatibility` rule `Maturity` →
  `BlockOnRelease` in the rule declaration (same file as T018). In-product
  verdict is now the source of truth; violations block `fsgg release`/`verify`.
- [ ] T025 [US2] Add the CI gate job (T019) to the repo's **required status
  checks** (branch protection) — the infra mirror of T024, flipped together for
  consistency. Document in contracts/ci-gate.md "Done signals" that both are set.

**Checkpoint**: A breaking-under-bump change fails the job and `fsgg release`; the
same change with a major bump passes; a no-break change passes with no action.

---

## Phase 5: User Story 3 — Public surface captured as a reviewable, committed baseline (Priority: P3)

**Goal**: Keep the gate coherent with the constitution's committed surface record
(`.fsi` + `surface/*.surface.txt`), so "what is public" is an explicit reviewed
decision and the two guards never contradict (FR-010, SC-006).

**Independent Test**: Change a package's public surface; confirm the committed
baseline must be updated in the same change for both guards to pass, and the
baseline diff is human-reviewable.

**Depends on US1** (the gate exists). Largely satisfied already by the
constitution's `.fsi`/`surface.txt` mechanism — this story documents and proves
coherence rather than adding a parallel baseline.

### Tests for User Story 3 (write FIRST, ensure they FAIL) ⚠️

- [ ] T026 [P] [US3] Coherence test (FR-010) in
  `tests/FS.GG.Governance.Snapshot.Tests/` (or `ReleaseCommand.Tests`): a single
  deliberate breaking change trips **both** the `surface.txt` drift guard and the
  ApiCompat gate with **non-contradictory** messages, and the documented single
  remediation path (update `.fsi` → `BLESS_SURFACE=1` refresh → MAJOR bump)
  clears both. `Synthetic`-tag the fixture break if no real baseline is available
  (Principle V).

### Implementation for User Story 3

- [X] T027 [US3] Document the single remediation path in
  `specs/088-governance-apicompat-gate/quickstart.md` §6 (already drafted) and in
  the gate's findings `Reason` text so a maintainer reading either guard's output
  reaches the same steps (SC-006). Confirm no `.fs` access modifiers were
  introduced anywhere (Principle II).
- [X] T028 [US3] Implement FR-013 attribution (in-scope obligation, not
  conditional): the finding/coverage output MUST label each break as **local** vs
  **inherited** (a break that became public only because an upstream package such
  as `FS.GG.Contracts` changed), per the spec "re-exported / transitive surface"
  edge case. Carry the label on the `ApiBreak`/coverage projection, emit it in the
  `.fsx` JSON output, and add an assertion in
  `tests/FS.GG.Governance.PackEvidence.Tests/` (or `ReleaseFactsSensing.Tests`)
  that a fixture inherited break is reported distinctly from a local break.
  `Synthetic`-tag the fixture if no real upstream-change baseline is available
  (Principle V).

**Checkpoint**: One documented path clears both guards; baselines are reviewable
diffs; inherited vs local breaks are distinguishable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T029 [P] Run the full quickstart.md validation (§1–§6) end-to-end and
  record real evidence (no synthetic where a real run is feasible).
- [ ] T030 [P] Run `/speckit-analyze` for cross-artifact consistency
  (spec ↔ plan ↔ tasks) and the `requirements.md` checklist; resolve any gaps.
- [X] T031 [P] Update the cross-repo registry coherence note (FS-GG/.github →
  `registry/dependencies.yml`) and the Coordination board item FS-GG/.github#20 to
  reflect the gate's advisory→required state, per the cross-repo-coordination
  protocol.
- [ ] T032 Evidence-obligations sweep: confirm every `[X]` task carries real (or
  disclosed-synthetic) evidence; confirm Principle IV applicability — the
  detector boundary has explicit `.fsi`, pure-transition tests, and emitted-effect
  assertions (T010/T015/T016); the pure verdict (T007/T012) is the non-I/O core.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → depends on Setup; **blocks all user stories**
  (shared rule kind + types).
- **Phase 3 (US1, P1, MVP)** → depends on Foundational. Self-contained.
- **Phase 4 (US2, P2)** → depends on US1 **and** SC-005 holding (T023 gate).
- **Phase 5 (US3, P3)** → depends on US1; independent of US2.
- **Phase 6 (Polish)** → after the desired stories complete.

### Within US1

- Tests T007–T011 written and failing before implementation T012–T019.
- Pure types/verdict (T012–T014) before host wiring (T015–T016) before the
  detector `.fsx` (T017) before rule declaration + CI (T018–T019).
- `surface.txt` refresh (T020) runs **last** in the phase, after all `.fsi`
  changes have landed.

### Parallel Opportunities

- T002, T003 in Setup.
- All US1 test tasks (T007–T011) — different test projects.
- T021/T022 in US2; T029–T031 in Polish.

---

## Implementation Strategy

**MVP = Phases 1 → 2 → 3 (US1).** Ship the advisory gate first: it delivers value
(local break signal at the point of introduction) while non-blocking. Then
US2 ratchets to required once SC-005 is met, and US3 documents/proves coherence.

---

## Summary

- **Task count by story**: Setup 3 (T001–T003), Foundational 3 (T004–T006),
  US1 14 (T007–T020), US2 5 (T021–T025), US3 3 (T026–T028), Polish 4 (T029–T032).
  **Total: 32 tasks.**
- **Parallel opportunities**: Setup T002/T003; US1 test fan-out T007–T011 (five
  distinct test projects); US2 T021/T022; Polish T029–T031.
- **Suggested MVP scope**: **User Story 1** — the advisory breaking-change gate
  (detect + grade + report in Warnings + coverage), Phases 1–3.
