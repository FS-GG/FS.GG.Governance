---
description: "Task list for feature 075 — CommandHost skeleton extraction (roadmap Phase B)"
---

# Tasks: CommandHost skeleton extraction

**Input**: Design documents from `/specs/075-command-host-skeleton/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: This is a **byte-identity** feature. The acceptance "tests" are (a) the
existing command/projection golden + snapshot suites (`route.json`, `audit.json`,
`verify.json`, refresh, cache-eligibility, release, evidence, and the `*Json`
projections), which MUST stay byte-identical (FR-009, SC-002); and (b) one additive
test project `FS.GG.Governance.CommandHost.Tests` carrying real-value semantic tests
over the leaf surface plus a reflective `SurfaceBaselineTests.fs` (drift + scope
guard) (FR-003/FR-004/SC-005). No new behavioural tests are written for the hosts —
the extraction is proven by **absence** of golden change.

**Organization**: Tasks are grouped by the three spec stories (US1 → US2 → US3). The
leaf is scaffolded once (Phase 2, foundational), then helpers move **one concern per
commit** (FR-013) through US1, with the acceptance invariant re-run after every move.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: US1 / US2 / US3 (US1 is the MVP).
- Tier annotation omitted — the whole feature is Tier 1 (matches the spec).
- Exact file paths are given in every task.

## The acceptance invariant (re-run at the end of every move / slice)

```bash
# This environment SIGABRTs the F# compiler under the default parallel build (see feature 073);
# build/test single-process:
dotnet build FS.GG.Governance.sln -m:2 -p:UseSharedCompilation=false   # warnings are errors
dotnet test  FS.GG.Governance.sln                                       # full suite green
git status --porcelain -- '**/*.golden' '**/*.snapshot' 'tests/**/Fixtures/**'   # MUST be EMPTY
```

A green suite **and** an empty fixture diff is the pass condition. A printed fixture
path means a move changed behaviour → revert/revisit that one concern, never
re-baseline a golden (spec Edge Cases, FR-009). The test count must equal the
recorded baseline **plus only** the additive `CommandHost.Tests` (FR-010, SC-003).

---

## Phase 1: Setup (record the baseline)

**Purpose**: Pin the pre-feature ground truth that every later move is checked against.

- [X] T001 On a clean `075-command-host-skeleton` tree, run the acceptance invariant
  and record the **baseline test count** and a green build/test at the top of this
  file (mirror 073's recorded baseline line).
- [X] T002 [P] Capture the duplication worklist into a scratch note: re-run the
  research audit greps over `src/*Command/Loop.fs` and `Loop.fsi`
  (`grep -n "let under\|let fail\|let revOfCommit\|let baseHeadOf\|let emptySensedFacts\|let describeInvalid\|let persistedContent\|let awaitingPersist\|let tryExecute\|let buildSnapshot\|let kindedRunsOf\|let kindOf\|type ExitDecision\|type GateClassification\|let executionPlan"`)
  and confirm the per-helper site map in research.md is still accurate at HEAD.

**Checkpoint**: baseline test count + the confirmed move worklist are recorded.

---

## Phase 2: Foundational (scaffold the leaf — blocking prerequisite)

**⚠️ CRITICAL**: no helper can move until the leaf project + its test project compile.

- [X] T003 Create `src/FS.GG.Governance.CommandHost/FS.GG.Governance.CommandHost.fsproj`
  modelled on the JsonWriters leaf: `IsPackable=true`, `PackageId=FS.GG.Governance.CommandHost`,
  `Version=0.1.0`, `<Compile Include="CommandHost.fsi" />` then `CommandHost.fs`, and a
  **minimal** initial `ProjectReference` set (add domain-type refs as moves require them —
  research D7). NO host/`Cli`/`*Command`/filesystem/git/process reference.
- [X] T004 Create `src/FS.GG.Governance.CommandHost/CommandHost.fsi` and
  `src/FS.GG.Governance.CommandHost/CommandHost.fs` with the empty module shell
  (`namespace FS.GG.Governance.CommandHost` + `[<CompilationRepresentation(...ModuleSuffix)>] module CommandHost`),
  no members yet (`.fsi`-first — members are added per move).
- [X] T005 Register both `FS.GG.Governance.CommandHost` (src) and
  `FS.GG.Governance.CommandHost.Tests` (test) as Project entries in
  `FS.GG.Governance.sln` (new GUIDs), following the JsonWriters registration pattern.
- [X] T006 Scaffold `tests/FS.GG.Governance.CommandHost.Tests/` —
  `FS.GG.Governance.CommandHost.Tests.fsproj` (Expecto stack: `Expecto`,
  `Expecto.FsCheck`, `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk`;
  `GenerateProgramFile=false`; ProjectReference to the leaf **and** to
  `FS.GG.Governance.Tests.Common` for `findRepoRoot`/`repoRoot`), plus `Main.fs`
  (Expecto entrypoint), an empty `CommandHostTests.fs`, and a stub
  `SurfaceBaselineTests.fs` (filled in US2). Confirm the empty leaf + test project
  build (acceptance invariant; test count = baseline + 0 real tests yet).

**Checkpoint**: `dotnet build` of the empty leaf + test project succeeds; the leaf is
on the graph below the command hosts with no host edge.

---

## Phase 3: User Story 1 — One source of truth for the host skeleton (Priority: P1) 🎯 MVP

**Goal**: Every genuinely-shared host helper lives once in the leaf; the command
hosts consume it and their local copies are gone (FR-001/FR-005/FR-006/FR-007).

**Independent Test**: change a helper in the leaf, confirm all consuming hosts pick up
the change, and `grep` finds no remaining local copy in any command `Loop.fs`.

> Each move task = one commit: add the member to `CommandHost.fsi` (signature first)
> then `CommandHost.fs`; `open FS.GG.Governance.CommandHost` (or qualify) in each
> consuming host; **delete** the local copy; run the acceptance invariant. If the
> fixture diff is non-empty, that helper was not genuinely shared — stop and revisit
> (FR-008). Micro-helper moves (T007–T014) are independent of each other.

### Micro-helper relocations (verbatim — research audit table)

- [X] T007 [P] [US1] Move `under` (repo-relative path joiner) into the leaf; delete
  the copies in `RouteCommand`, `ShipCommand`, `VerifyCommand`, `RefreshCommand`,
  `CacheEligibilityCommand` (and `ReleaseCommand` if present) `Loop.fs`.
- [-] T008 — **SKIPPED (FR-008): `fail` is TYPE-DIVERGENT — it record-updates each host's OWN `Model` and returns its OWN `Effect`; a shared form needs generics (Constitution III). Stays local; recorded research.md §D9.**
  [P] [US1] Move `fail` (failure-transition helper) into the leaf; delete the
  copies in Route/Ship/Verify/Refresh/CacheEligibility/Release `Loop.fs`. (Confirm the
  text is byte-identical across hosts first; the `Model`/`Effect`/`ExitDecision` types
  it touches must resolve identically — if a host's `Model` diverges, keep it local
  and record it.)
- [X] T009 [P] [US1] Move `revOfCommit` **and** `baseHeadOf` together (the latter calls
  the former) into the leaf; `baseHeadOf` takes the snapshot `Range option` view (not a
  host `Model`). Delete copies in Route/Ship/Verify/CacheEligibility `Loop.fs`.
- [X] T010 [P] [US1] Move `emptySensedFacts` (all-empty `SensedFacts`) into the leaf;
  delete copies in Route/Ship/Verify `Loop.fs`.
- [X] T011 [P] [US1] Confirm `describeInvalid` is byte-identical across Route/Ship/Verify,
  then move it into the leaf and delete the local copies.
- [X] T012 [P] [US1] Confirm `persistedContent` + `awaitingPersist` are byte-identical
  across Route/Ship/Verify, then move them into the leaf and delete the local copies.
- [-] T013 — **SKIPPED (FR-008): `tryExecute` is TYPE-DIVERGENT — returns each host's OWN `Model * Effect list`. Stays local; recorded research.md §D9. (`executionPlan` it calls DID move — see T017.)**
  [P] [US1] Confirm `tryExecute` (gate-execution driver) is byte-identical
  across Route/Ship/Verify, then move it into the leaf and delete the local copies.
- [X] T014 [P] [US1] Move the **Verify↔Ship** common `buildSnapshot`, `kindedRunsOf`,
  `kindOf` into the leaf (confirm byte-identical between those two first); delete the
  Verify/Ship copies. **ReleaseCommand's `buildSnapshot` stays local** (different input
  type — research D5; handled in US3).

### Canonical exit + classification + parameterized plan (the gated hard moves)

- [-] T015 — **DEFERRED (FR-008): canonical `ExitDecision`+`exitCode` are built & unit-tested IN THE LEAF, but host ADOPTION is a 6-host public-`Loop.fsi` surface cascade + interpreter/Cli/test ripple, disproportionate to a 5-line map. Bounded follow-up; recorded research.md §D9.**
  [US1] Move the **canonical `ExitDecision`** (superset, with `Blocked`) and
  `exitCode` into the leaf (research D2). In each host's `Loop`, re-export the type
  (`type ExitDecision = CommandHost.ExitDecision`) so its `Loop.fsi` surface is
  preserved, and call `CommandHost.exitCode`. Add behaviour-preserving arms at any host
  `match` that becomes non-exhaustive under the superset (the compiler flags them under
  `TreatWarningsAsErrors`). Acceptance invariant — exit codes unchanged.
- [X] T016 [US1] Move the **superset `GateClassification`** (4 cases, with `Deferred of
  BudgetReason`) into the leaf (research D3); re-export per host. Add an unreachable,
  behaviour-preserving `Deferred` arm at Route's consuming `match` sites. **Pairs with
  T017** (same commit-group — the plan produces classifications).
- [X] T017 [US1] Move the **parameterized `executionPlan`** + `ExecutionPlanParams`
  record into the leaf (research D4, FR-006): shared non-budget prefix; apply
  `BudgetFold` when `Some` (Ship/Verify) else return `CacheDecisionReport []` (Route).
  Wire Route (`BudgetFold = None`), Ship, and Verify to the shared form; Route
  destructures and discards the empty report. **This is the highest-risk move — run the
  acceptance invariant immediately and scrutinise `route.json`/`audit.json`/`verify.json`.
  If any byte moves, revisit the parameterization (or split back to local per FR-008).**
- [X] T018 [US1] Add real-value semantic tests in
  `tests/FS.GG.Governance.CommandHost.Tests/CommandHostTests.fs` exercising the public
  surface (Principle V, real literally-constructed values): `exitCode` over every case;
  `executionPlan` with `BudgetFold = None` (no `Deferred` ever appears) and with
  `Some fold` (Ship/Verify shape); plus the relocated micro-helpers.
- [X] T019 [US1] **Sweep & prove SC-001**: `grep -rn "let under \|let fail \|let revOfCommit\|let baseHeadOf\|let emptySensedFacts\|let describeInvalid\|let persistedContent\|let awaitingPersist\|let tryExecute\|let executionPlan\|let exitCode\|type GateClassification" src/*Command/Loop.fs`
  returns **only** the three documented stay-local keeps (Release `buildSnapshot`,
  `cacheReportOf`, and each host's `ExitCodeBasis -> ExitDecision` policy mapper —
  research D5/D6, data-model FR-008 table); no moved helper is duplicated. Run the
  acceptance invariant.

**Checkpoint**: US1 functional — one home per shared helper, goldens byte-identical,
suite green. Independently shippable (MVP).

---

## Phase 4: User Story 2 — Boundaries and discipline preserved (Priority: P1)

**Goal**: the leaf is pure, sits below the hosts in an acyclic graph, exposes exactly
its shared surface via a curated `.fsi`, and carries a surface baseline + drift test
(FR-002/FR-003/FR-004/FR-011, SC-005).

**Independent Test**: the surface-drift test passes against the baseline, the
scope-guard test passes, and the leaf's project references contain no host/impure
project.

- [X] T020 [US2] Curate `src/FS.GG.Governance.CommandHost/CommandHost.fsi` so it
  declares **exactly** the moved members and nothing more (cross-check against
  `contracts/command-host.fsi.md`); confirm `CommandHost.fs` carries no
  `private`/`internal`/`public` modifiers (Constitution II).
- [X] T021 [US2] Add `surface/FS.GG.Governance.CommandHost.surface.txt` and bless it
  (`BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.CommandHost.Tests/...`).
- [X] T022 [US2] Fill `tests/FS.GG.Governance.CommandHost.Tests/SurfaceBaselineTests.fs`
  with the two reflective tests (mirror JsonWriters): (a) public surface == baseline
  (re-blessable via `BLESS_SURFACE=1`); (b) **scope guard** — the leaf references none
  of `Host`, `Cli`, any `*Command`, and no filesystem/git/process project.
- [X] T023 [US2] Confirm the leaf's final `ProjectReference` list in the `.fsproj` is
  the minimal domain-type set the moved bodies need (research D7), prune any unused
  edge, and confirm the build proves the graph acyclic. Run the acceptance invariant.

**Checkpoint**: US1 + US2 shippable — shared leaf is pure, curated, baseline-pinned,
acyclic, with no host edge.

---

## Phase 5: User Story 3 — Type-divergent helpers correctly stay local (Priority: P2)

**Goal**: helpers that look identical but are type-divergent stayed local with a
recorded reason, and byte-identity proves only genuinely-shared members moved
(FR-008, SC-002).

**Independent Test**: each retained-local helper has a recorded reason and the
byte-identity gate is green.

- [X] T024 [US3] Confirm `ReleaseCommand`'s `buildSnapshot` remains local
  (`grep -n "let buildSnapshot" src/FS.GG.Governance.ReleaseCommand/Loop.fs`) and that
  the divergence reason (different input type) is recorded in research D5 — add a
  one-line at-site comment noting it intentionally stays local.
- [X] T025 [US3] Confirm `cacheReportOf` remains local in
  `CacheEligibilityCommand/Loop.fs` (single defining site — no duplication to remove,
  research D6); add a one-line at-site note.
- [X] T026 [US3] **Prove SC-002**: run the full golden/snapshot suite and the
  `git status --porcelain` fixture check — empty diff demonstrates that only
  behaviour-preserving, genuinely-shared members moved. Also confirm **FR-012**
  held: no single-parameterized `GateRunHost` unification of route/ship/verify was
  introduced (the hosts keep their separate `Loop.fs` `update`/interpreter
  boundaries — that unification is deferred to Phase C).

**Checkpoint**: all three stories functional; divergences recorded; goldens unchanged.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T027 Final full-feature acceptance run of `quickstart.md`: build + test green;
  test count = baseline + only `CommandHost.Tests`; measure **net source reduction**
  across the command hosts (target ≈ 400–500 LOC after the leaf body + `.fsi`, SC-004);
  record the numbers.
- [X] T028 [P] Confirm SC-005 end-to-end: the leaf has a curated `.fsi`, a passing
  surface-drift + scope-guard test, and **zero** host/impure project references;
  confirm the whole-repo dependency graph is acyclic.
- [X] T029 Run the Tier 1 agent-context update (`/speckit-agent-context-update`) so the
  managed `CLAUDE.md` SPECKIT section records the new `FS.GG.Governance.CommandHost`
  leaf as DELIVERED; update the roadmap doc's Phase B status.

---

## Dependencies & Execution Order

### Phase dependencies
- **Setup (Phase 1)** → **Foundational (Phase 2)** → **US1 (Phase 3)** → **US2 (Phase 4)** → **US3 (Phase 5)** → **Polish (Phase 6)**.
- Foundational (T003–T006) blocks all moves: the leaf + test project must compile first.
- US2's surface baseline/drift (T021/T022) can only pass **after** the surface is final,
  i.e. after the US1 moves (T007–T017).

### Within US1
- T007–T014 (micro-helpers) are mutually independent — `[P]`, each its own commit.
- T015 (ExitDecision/exitCode) is independent of the micro-helpers.
- **T016 + T017 are one commit-group** (GateClassification ← executionPlan); land them
  together and gate hard on the goldens.
- T018 (semantic tests) after the members it exercises exist (T007–T017).
- T019 (sweep) last in US1.

### Parallel opportunities
- T007–T014 across different hosts/helpers (`[P]`).
- T028 `[P]` in polish.
- US3 confirmations (T024/T025) are independent reads.

## Implementation strategy

### MVP first (US1 only)
1. Phases 1–2 (baseline + leaf scaffold).
2. Micro-helper moves T007–T014, invariant after each.
3. Canonical exit T015; then the gated T016+T017 commit-group.
4. **STOP and VALIDATE**: acceptance invariant + SC-001 sweep (T019). Ship the MVP.

### Incremental delivery
US2 (purity/surface/acyclicity) and US3 (recorded stay-local divergences) layer on top
without touching host behaviour; each keeps the suite green and the fixtures unchanged.

## Notes
- **Elmish/MVU (Principle IV).** The leaf holds **pure** helpers below `update`; it adds
  no MVU ceremony. The command hosts keep their existing `Model`/`Msg`/`Effect`/`init`/
  `update`/interpreter boundary unchanged — the moves preserve `update` purity. No new
  effect/interpreter contract is introduced.
- **No synthetic evidence (Principle V).** All semantic tests use real, literally-
  constructed domain values; behaviour preservation is proven by real golden suites.
- Never re-baseline a golden to green a move. A moved fixture means the helper was not
  genuinely shared → revert that concern and record the divergence (FR-008).
