---
description: "Task list for F016 - 016-git-ci-snapshot-facts: sense a typed, deterministic repository snapshot (resolved diff range, committed changed paths, working-tree dirty/untracked, branch, optional CI/PR context) and normalize changed paths into the F014 GovernedPath form F015 routing consumes."
---

# Tasks: Git/CI Snapshot Facts for the Repository Boundary

**Feature branch**: `016-git-ci-snapshot-facts` (active spec; git branch currently the feature branch)
**Spec**: [`specs/016-git-ci-snapshot-facts/spec.md`](./spec.md)
**Plan**: [`specs/016-git-ci-snapshot-facts/plan.md`](./plan.md)

**Input**: Design documents from `/specs/016-git-ci-snapshot-facts/`

## Progress — all phases complete ✅ (47/47 tasks, real-git evidence)

- ✅ **Phase 1: Setup** (T001–T012) — Snapshot lib + test project, contracts copied, `Config.normalizePath` exposed & surface re-blessed, fixtures + real-git helper, prelude sketch, readiness.
- ✅ **Phase 2: Foundation** (T013–T018) — `Model`, closed read-only `GitCommand` set, pure `planResolution`, ordering/categorization, `assemble` + `senseSnapshot`.
- ✅ **Phase 3: US1 — changed-path set** (T019–T026) — diff `-z` parser, normalized `Changed`, routing feed-through (SC-001).
- ✅ **Phase 4: US2 — working tree** (T027–T032) — `status -z` parser, dirty/untracked planes.
- ✅ **Phase 5: US3 — range resolution** (T033–T035) — every option form + local/CI parity.
- ✅ **Phase 6: US4 — branch + CI context** (T036–T037) — detached-HEAD `None`, injected `CiPort`, no network.
- ✅ **Phase 7: US5 — fail safe & read-only** (T038–T042) — stable diagnostics, byte-identity read-only proof.
- ✅ **Phase 8: Polish** (T043–T047) — determinism/permutation, surface baseline + hygiene, quickstart, README/plan legend.

Verification: `dotnet test FS.GG.Governance.sln` → **276 passed, 0 failed** across all 9 projects (Snapshot: 38; Config: 46, incl. the normalizer tests). The edge tests drive **real `git`** against disposable temp repos (Principle V); no network, no fake git, no hosting-provider API.

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Model.fsi](./contracts/Model.fsi), [contracts/Snapshot.fsi](./contracts/Snapshot.fsi), [contracts/Interpreter.fsi](./contracts/Interpreter.fsi), [contracts/git-sensing.md](./contracts/git-sensing.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; plus an additive Tier-1 touch on `FS.GG.Governance.Config`). Credible evidence is public-surface testing: the pure porcelain parsers and `assemble`/`planResolution` over literal raw fixtures, and the edge `senseSnapshot` over a **real temporary git repository** (Principle V) — never private helpers.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task tier annotations needed. The Config normalizer addition (T002–T004) is the one cross-project Tier-1 touch and is called out where it occurs.

**Elmish/MVU (Principle IV)**: **APPLICABLE** — this is an I/O feature. It is honored in the **lighter port/effect algebra** form the constitution blesses (research D3, as F014's Loader), not the full Elmish `Program`: I/O is injected ports (`GitPort`/`CiPort`), the core (`planResolution`/`assemble`/parsers) is **pure and total**, and interpretation happens only at the edge (`senseSnapshot`). Both sides are tested: pure transition-style tests over hand-built `RawSensing`, and a real-fixture interpreter test (init/commit/modify/add real git). There is no `Msg`/`update` convergence loop because sensing is fixed request/response gather (research D3).

**Synthetic-evidence discipline (Principle V)**: the edge tests drive the **real** `git` against a disposable temp repo (real evidence). The pure parsers/assemble are tested with explicit literal `-z` fixtures — the actual wire bytes, ugly on purpose. No network or agent is reached (no hosting-provider API; the `CiPort` is injected). If a literal porcelain string stands in for a git case that cannot be staged on the host (e.g. a forced type-change), it carries `Synthetic` in the test name and a use-site `// SYNTHETIC:` disclosure.

**Determinism minimums (FR-009/FR-010, SC-002/SC-003/SC-006)**: every snapshot collection is sorted — `Changed` by `Path`, `Dirty`/`Untracked` by value, `Digests` by command token, `Diagnostics` by `(id, operation)`. No raw git output, timing, pid, or absolute host path enters any deterministic field; retained provenance is a `CommandRunDigest` kept separate. Re-ordering raw diff/status entries never changes the snapshot.

**Read-only & no-network minimums (FR-006, SC-005/SC-007)**: read-only is guaranteed by construction (the closed `GitCommand` DU has no mutating subcommand) and proved empirically (before/after byte-identity on a fixture repo). No provider-API call exists anywhere in the library; CI context comes only from the injected `CiPort` over the environment.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US5]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional sensing library, its test project, the public contracts, fixtures, and — as a blocking prerequisite — the **single-sourced path normalizer exposed from `FS.GG.Governance.Config`** (research D7) that `assemble` reuses so the snapshot's `GovernedPath`s are byte-identical to what routing consumes (SC-001). **No new third-party dependency** is added — the library references `FS.GG.Governance.Config` only and drives read-only git via BCL `System.Diagnostics.Process` (plan Technical Context).

- [X] T001 Create `src/FS.GG.Governance.Snapshot/FS.GG.Governance.Snapshot.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.Snapshot`, with a single `<ProjectReference Include="../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj" />` and **no** `<PackageReference>` (BCL + FSharp.Core only — research D1/D2; the YamlDotNet dep arrives only transitively via Config and is unused by Snapshot's own code). Compile order `Model.fs` → `Snapshot.fs` → `Interpreter.fs`.
- [X] T002 **[Config Tier-1 touch]** Expose F014's path normalization publicly: add `val normalizePath: raw: string -> GovernedPath` to `src/FS.GG.Governance.Config/Model.fsi`, and in `src/FS.GG.Governance.Config/Model.fs` extract the existing normalization logic (separators unified, `.`/`..` resolved, leading `./` stripped, repo-relative — D5) out of `Schema.fs` into this `val`, then have `Schema.fs` call it so there is exactly one implementation (research D7). Behavior of `Schema.validate` MUST be unchanged.
- [X] T003 [P] **[Config Tier-1 touch]** In `tests/FS.GG.Governance.Config.Tests/` add a focused test module asserting `Model.normalizePath` produces the documented normalized form for representative inputs (`./a/b`, `a\\b`, `a/./b`, `a/../c`, nested), and a parity assertion that an existing `Schema.validate` path fixture still normalizes identically (no behavior change). Add the module to that project's compile list before `Main.fs`.
- [X] T004 **[Config Tier-1 touch]** Regenerate/re-bless `surface/FS.GG.Governance.Config.surface.txt` for the new `normalizePath` val and confirm the existing Config surface-drift test goes green (Principle II / Tier 1).
- [X] T005 Copy `specs/016-git-ci-snapshot-facts/contracts/Model.fsi` → `src/FS.GG.Governance.Snapshot/Model.fsi`, `contracts/Snapshot.fsi` → `src/FS.GG.Governance.Snapshot/Snapshot.fsi`, and `contracts/Interpreter.fsi` → `src/FS.GG.Governance.Snapshot/Interpreter.fsi` verbatim as the curated public surface.
- [X] T006 Add `failwith "F016"` stub bodies in `src/FS.GG.Governance.Snapshot/Model.fs`, `Snapshot.fs`, and `Interpreter.fs` that satisfy the `.fsi` contracts, in the fsproj compile order `Model.fs` → `Snapshot.fs` → `Interpreter.fs`.
- [X] T007 Create `tests/FS.GG.Governance.Snapshot.Tests/FS.GG.Governance.Snapshot.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/VSTest packages, `IsPackable=false`, `GenerateProgramFile=false`, a `ProjectReference` to `src/FS.GG.Governance.Snapshot`, **and** a `ProjectReference` to `src/FS.GG.Governance.Routing` (for the SC-001 feed-through test only — the production library does not reference Routing).
- [X] T008 [P] Add empty Expecto test modules `tests/FS.GG.Governance.Snapshot.Tests/Support.fs`, `ResolutionTests.fs`, `ParseTests.fs`, `AssembleTests.fs`, `DeterminismTests.fs`, `SensingTests.fs`, `RoutingFeedTests.fs`, `SurfaceDriftTests.fs`, and `Main.fs` (in compile order; `Main.fs` runs the assembly tests).
- [X] T009 Add `src/FS.GG.Governance.Snapshot` and `tests/FS.GG.Governance.Snapshot.Tests` to `FS.GG.Governance.sln`.
- [X] T010 [P] Implement fixtures + the real-git helper in `tests/FS.GG.Governance.Snapshot.Tests/Support.fs`: (a) a `rawSensing` builder assembling a `Snapshot.RawSensing` from literal field values (raw `-z` strings, resolved ids, plan) for the pure tests; and (b) a `withTempRepo : (string -> 'a) -> 'a` that creates a disposable temp dir, drives the **real** `git` (`init -q`, `config user.email/user.name`, write/`add`/`commit`) to a known state, runs the body against the repo dir, and deletes the dir. These are REAL inputs/repos, not synthetic.
- [X] T011 [P] Extend `scripts/prelude.fsx` with an F016 design sketch that `#r`s the built `FS.GG.Governance.Snapshot` (+ `FS.GG.Governance.Config`) assemblies, opens the namespaces, calls `Snapshot.planResolution`, `Snapshot.assemble` over a small hand-built `RawSensing`, and `Interpreter.senseSnapshot (Interpreter.realPorts ".")` over the repo, recording the intended flow before real bodies land.
- [X] T012 [P] Create `specs/016-git-ci-snapshot-facts/readiness/README.md` listing required transcripts (planResolution per form; assemble over raw fixtures; senseSnapshot over a real temp repo for changed/dirty/untracked; not-a-repo & unknown-ref diagnostics; read-only byte-identity; the routing feed-through) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them.

**Checkpoint**: `dotnet build src/FS.GG.Governance.Snapshot` and `dotnet test tests/FS.GG.Governance.Snapshot.Tests` compile against stubs; the solution lists the two new projects; the Config reference resolves; `Config.normalizePath` is public, tested, and surface-blessed.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the snapshot model, the closed read-only git command vocabulary, the pure range planner, the deterministic-ordering/categorization helpers, and the `assemble` + `senseSnapshot` skeletons — everything the stories build on. **No user-story work begins until this phase is complete.**

- [X] T013 Implement `src/FS.GG.Governance.Snapshot/Model.fs`: `GitRef`, `CommitId`, `BranchName`, `SnapshotOptions`, `DiffRange`, `ChangeKind`, `ChangedPath`, `WorkingTreeState`, `CiEnvironment`, `CiContext`, `CommandRunDigest`, `SensingDiagnosticId`, `SensingDiagnostic`, `RepoSnapshot`, and the total `sensingDiagnosticIdToken` / `changeKindToken` — exactly matching `Model.fsi`. Reuses `FS.GG.Governance.Config.Model.GovernedPath` (does not redefine it). Realizes the FR-010 "no raw output in facts" shape and the FR-011 empty-vs-failure structural distinction.
- [X] T014 Implement the closed read-only command vocabulary in `src/FS.GG.Governance.Snapshot/Interpreter.fs` (pure parts only): the `GitCommand` DU and its `Token` member, plus a pure `argv : GitCommand -> string list` builder producing the exact read-only invocations in [contracts/git-sensing.md](./contracts/git-sensing.md) §1 (`rev-parse --is-inside-work-tree`, `rev-parse --verify <r>^{commit}`, `merge-base`, `diff --name-status -z -M`, `status --porcelain=v1 -z`, `rev-parse --abbrev-ref HEAD`). No mutating subcommand is representable (FR-006, read-only by construction).
- [X] T015 Implement the pure `Snapshot.planResolution` in `src/FS.GG.Governance.Snapshot/Snapshot.fs` per [contracts/git-sensing.md](./contracts/git-sensing.md) §4: map `SnapshotOptions` → `ResolutionPlan` (`Since` precedence, `BaseHead`, base-only, head-only with documented default base, and the all-`None` `Default`; `UseMergeBase=true`). Pure & total, no git (US3 consumes it; US1's edge depends on it — placed in Foundation so the MVP is not blocked on US3).
- [X] T016 [P] Implement deterministic helpers in `src/FS.GG.Governance.Snapshot/Snapshot.fs`: ordering (`Changed` by `Path`, `Dirty`/`Untracked` by value, `Digests` by token, `Diagnostics` by `(id, operation)` — all ordinal) and an exclusive working-tree categorizer; plus the `GovernedPath` normalization plumbing that calls `Config.Model.normalizePath` (T002) so no normalization is re-decided here (FR-002/FR-009, research D7).
- [X] T017 Implement the `Snapshot.assemble` skeleton in `src/FS.GG.Governance.Snapshot/Snapshot.fs`: dispatch `RepoOk=false` → `NotARepository` diagnostic + `Range=None`; any `Error` in `BaseResolved`/`HeadResolved`/`MergeBaseResolved` → matching diagnostic + `Range=None`; all `Ok` → `Some DiffRange`; the genuine empty-diff vs failure structural distinction (FR-011); branch passthrough; and call placeholder diff/status parsers (filled by US1/US2). Returns a well-formed `RepoSnapshot` with sorted collections for the no-change and failure cases. PURE, never throws.
- [X] T018 Implement the `Interpreter.senseSnapshot` skeleton + `Interpreter.realPorts` in `src/FS.GG.Governance.Snapshot/Interpreter.fs`: `realPorts repoDir` builds a `GitPort` that runs `argv` (T014) via `System.Diagnostics.Process` in `repoDir` (capturing stdout, mapping nonzero-exit/thrown to `Error`) and a `CiPort` over `System.Environment`; `senseSnapshot` runs `RepoCheck` + the planned `RevParse`/`MergeBase`/`DiffNameStatus`/`StatusPorcelain`/`CurrentBranch` through `ports.Git`, accumulates one `CommandRunDigest` per command (FR-010), reads `ports.Ci ()`, bundles a `RawSensing`, and returns `Snapshot.assemble raw`. Guards every call so it NEVER throws (FR-008). Diff/status field population is exercised by the stories.

**Checkpoint**: the library builds with real Model + command vocabulary + planResolution + ordering + assemble/senseSnapshot skeletons; `senseSnapshot` over a clean fixture repo returns a well-formed empty-but-successful snapshot; range-failure and not-a-repo produce diagnostics; diff/status still placeholder.

---

## Phase 3: User Story 1 - Sense the changed-path set of a change boundary (Priority: P1) 🎯 MVP

**Goal**: given a base/head, sense the committed changed-path set as normalized repo-relative `GovernedPath`s, feedable straight into `Routing.route` with no re-normalization.

**Independent Test**: a temp fixture repo whose head differs from base by two known files → `senseSnapshot` `Changed` equals those two normalized paths (sorted); routing the set with a fixture `TypedFacts` classifies them with no further normalization.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T019 [P] [US1] In `tests/FS.GG.Governance.Snapshot.Tests/ParseTests.fs`, add `diff --name-status -z` parse tests over literal `-z` fixtures: `A`/`M`/`D`/`T` single records → correct `ChangeKind`, `OldPath=None`; `R096`/`C` three-NUL records → `Renamed`/`Copied` with `Path=newPath`, `OldPath=Some oldPath`; an unknown status letter → a single `UnparsableGitOutput` diagnostic (not a silent drop); a non-ASCII/space path survives via `-z` (FR-012, git-sensing §2).
- [X] T020 [P] [US1] In `tests/FS.GG.Governance.Snapshot.Tests/AssembleTests.fs`, add `assemble` tests over hand-built `RawSensing`: a two-file diff → `Changed` carries both as normalized `GovernedPath`s sorted by `Path`, `Range=Some` from the resolved ids; an empty diff (all resolved, empty `DiffRaw`) → empty `Changed`, empty `Diagnostics`, `Range=Some` — the genuine "nothing changed" outcome distinct from any failure (FR-011, US1 AS2).
- [X] T021 [P] [US1] In `tests/FS.GG.Governance.Snapshot.Tests/SensingTests.fs`, add a real-git test (via `Support.withTempRepo`): commit a base, then change two files and commit → `senseSnapshot ports { Base=Some HEAD~1; Head=Some HEAD }` `Changed` equals those two normalized paths; sensing the same state twice → byte-identical snapshots (US1 AS1/AS3, SC-002).
- [X] T022 [P] [US1] In `tests/FS.GG.Governance.Snapshot.Tests/RoutingFeedTests.fs`, build a fixture `TypedFacts` (governed root + a small path map) and pass `snapshot.Changed |> List.map (fun c -> c.Path)` straight into `FS.GG.Governance.Routing.Routing.route`; assert paths route/!route as expected with **no** re-normalization step in between — the proof that the snapshot form is exactly routing's input (**SC-001**). Include at least one changed path **outside** the governed root and assert the snapshot still carried it (represented, not dropped — FR-002) and that `route` classifies it `OutOfScope`.
- [X] T023 [P] [US1] Add an FSI transcript in `specs/016-git-ci-snapshot-facts/readiness/` that loads the built library and senses the changed set of a small real repo, capturing the `RepoSnapshot` (US1 independent-test evidence).

### Implementation for User Story 1

- [X] T024 [US1] Implement the `diff --name-status -z -M` parser in `src/FS.GG.Governance.Snapshot/Snapshot.fs` per git-sensing §2: split on NUL, decode single vs three-field rename/copy records, map the status letter to `ChangeKind`, and emit `UnparsableGitOutput` for an unknown letter. Disclose any `mutable` accumulator at the use site (Principle III).
- [X] T025 [US1] Wire the diff parser into `assemble`: normalize each `path`/`oldPath` via `Config.Model.normalizePath` (T016), build `ChangedPath` records, and sort `Changed` by `Path` (FR-002/FR-009).
- [X] T026 [US1] Complete the committed-diff path in `senseSnapshot`: run `MergeBase (base, head)` then `DiffNameStatus (mergeBase, head)` (three-dot, research D8) and populate `RawSensing.DiffRaw`, so the real-git test (T021) and the feed-through (T022) pass.

**Checkpoint**: changed-path sensing works over a real repo and feeds routing unchanged. US1 is the MVP.

---

## Phase 4: User Story 2 - Sense working-tree state (dirty and untracked paths) (Priority: P1)

**Goal**: capture tracked-but-modified (dirty) and untracked paths from the working tree, normalized and mutually-exclusively categorized, distinct from the committed `Changed` set.

**Independent Test**: a temp repo with one tracked file modified and one new untracked file (uncommitted) → `Dirty` has the modified, `Untracked` has the new, neither in `Changed`; a clean tree → both empty.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T027 [P] [US2] In `ParseTests.fs`, add `status --porcelain=v1 -z` parse tests over literal `-z` fixtures: `??` → `Untracked`; ` M`/`M `/`MM`/`A `/` D` → `Dirty`; an index-rename record's current (new) path → `Dirty`; assert `Dirty` and `Untracked` are mutually exclusive (git-sensing §3, FR-003).
- [X] T028 [P] [US2] In `AssembleTests.fs`, add `assemble` tests: a `RawSensing` whose `StatusRaw` mixes dirty + untracked → both categories normalized and sorted, with `Dirty` and `Untracked` mutually exclusive (working-tree plane); AND a `RawSensing` whose `DiffRaw` (committed) and `StatusRaw` (dirty) both list the same path → that path appears in **both** the committed `Changed` plane and the working-tree `Dirty` plane (the two planes are reported separately, not cross-exclusive — FR-003, SC-003).
- [X] T029 [P] [US2] In `SensingTests.fs`, add a real-git test (via `withTempRepo`): modify a tracked file and add an untracked file without committing → `senseSnapshot` `WorkingTree.Dirty`/`Untracked` correct (and, for this uncommitted fixture, not present in the committed `Changed` plane); a clean tree → both empty and the snapshot still successful (US2 AS1/AS2/AS3).

### Implementation for User Story 2

- [X] T030 [US2] Implement the `status --porcelain=v1 -z` parser in `src/FS.GG.Governance.Snapshot/Snapshot.fs` per git-sensing §3 (NUL-split; `??`→untracked; other non-space columns→dirty; rename current path→dirty).
- [X] T031 [US2] Wire the status parser into `assemble`: normalize via `Config.Model.normalizePath`, build `WorkingTreeState` with the exclusive categorizer (T016), sort both lists (FR-003/FR-009).
- [X] T032 [US2] Complete the `StatusPorcelain` wiring in `senseSnapshot` (populate `RawSensing.StatusRaw`) so the real-git test (T029) passes.

**Checkpoint**: working-tree dirty/untracked sensing works and is correctly separated from the committed diff. US1 + US2 both pass.

---

## Phase 5: User Story 3 - Resolve a loose range into a concrete diff range (Priority: P2)

**Goal**: prove the pure resolution contract — each option form yields the documented `ResolutionPlan`, identically local vs CI, with the committed diff against the merge base.

**Independent Test**: resolve `--since`, `--base`/`--head`, base-only, head-only, and the default; assert the expected base/head/merge-base; assert the same options resolve identically in local-shaped and CI-shaped contexts over the same commits.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T033 [P] [US3] In `tests/FS.GG.Governance.Snapshot.Tests/ResolutionTests.fs`, add `planResolution` tests covering every row of git-sensing §4: `Since` (precedence over base/head), `BaseHead`, base-only (head=`HEAD`), head-only (base=default), and `Default`; assert `Form`, `BaseRef`/`HeadRef`, and `UseMergeBase=true` — all pure, no repo (US3 AS1/AS2).
- [X] T034 [P] [US3] In `ResolutionTests.fs` (and a real-git check in `SensingTests.fs`), assert local-vs-CI parity: the same `SnapshotOptions` over the same commits resolve to identical `Base`/`Head`/`MergeBase` and identical `Changed` whether sensed in a local-shaped or CI-shaped context, and that diffing against the merge base does not report unrelated upstream commits on a stale base branch (US3 AS3/AS1, SC-004, research D8); and assert the `Default` form (no options) yields an empty committed `Changed` by design (head = `HEAD`), with uncommitted work visible only via the working-tree sets (git-sensing §4, finding U1).

### Implementation for User Story 3

- [X] T035 [US3] Confirm/refine `Snapshot.planResolution` (T015) and the `senseSnapshot` merge-base diff (T026) against the T033/T034 evidence; if a row or the documented-default base needs correction, fix it in `src/FS.GG.Governance.Snapshot/Snapshot.fs` (and `Interpreter.fs` if the edge resolution order is involved). Note here if no change was needed beyond Foundation. **— No change needed beyond Foundation: every `planResolution` row and the documented HEAD default passed `ResolutionTests.fs`; the `MergeBase (base, head)` → `DiffNameStatus (mergeBase, head)` three-dot order in `senseSnapshot` was confirmed by the real-git local/CI-parity test in `SensingTests.fs`.**

**Checkpoint**: the resolution contract is proven for every option form, with local/CI parity. US1–US3 pass.

---

## Phase 6: User Story 4 - Capture branch and optional CI / PR context (Priority: P3)

**Goal**: capture the current branch (absent on detached HEAD) and optional runner-supplied CI/PR context, never fabricated, never via network.

**Independent Test**: sense with a CI context supplied → branch + labels + status-check ids + environment captured in order; sense with none → those fields explicitly absent, branch still captured from git.

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T036 [P] [US4] In `AssembleTests.fs` and `SensingTests.fs`, add: `BranchRaw "HEAD"` → `Branch=None` (detached), any other → `Some (BranchName ...)`; a supplied `CiContext` → `PrLabels`/`RequiredStatusChecks` captured in deterministic order and `Environment` a closed `CiEnvironment`; no CI context → `Ci=None` with the branch still captured (US4 AS1/AS2/AS3, FR-005); assert no provider-API symbol is reachable (context comes only from the injected `CiPort`).

### Implementation for User Story 4

- [X] T037 [US4] Wire branch + CI context in `assemble` and `realPorts`: in `assemble`, map `BranchRaw "HEAD"` → `None` else `Some`, and pass `RawCi` through to `RepoSnapshot.Ci` with ordered label/check lists; in `Interpreter.realPorts`, implement the `CiPort` over `System.Environment` returning `None` when unavailable and never fabricating absent fields (research D9). No network.

**Checkpoint**: branch and optional CI/PR context are captured honestly and deterministically; absence is explicit. US1–US4 pass.

---

## Phase 7: User Story 5 - Fail safe and stay read-only (Priority: P2)

**Goal**: every sensing failure is a stable diagnostic (never a throw, never an empty-looking success); sensing never mutates the repository.

**Independent Test**: not-a-repo and unknown-ref fixtures → stable diagnostics distinct from an empty diff, no exception escapes; a clean fixture repo is byte-identical before and after sensing.

### Tests for User Story 5 (write first; must FAIL before implementation)

- [X] T038 [P] [US5] In `SensingTests.fs`, add real-git failure tests: point `senseSnapshot` at a non-repository temp dir → a `NotARepository` diagnostic, no throw, and NOT an empty-success snapshot (US5 AS1, FR-008/FR-011); request an unknown base ref in a real repo → an `UnknownRef` diagnostic distinct from an empty diff (US5 AS2).
- [X] T039 [P] [US5] In `SensingTests.fs`, add the read-only proof: compute a recursive content+ref hash of the fixture repo (tracked files + `.git/refs` + `HEAD`) before and after `senseSnapshot`, assert byte-identity (US5 AS3, SC-005, FR-006).
- [X] T040 [P] [US5] In `AssembleTests.fs`, assert each `SensingDiagnosticId` (`NotARepository`, `UnknownRef`, `GitCommandFailed`, `UnreadableWorkingTree`, `UnparsableGitOutput`; `GitUnavailable` covered at the edge in T042) is reachable from the matching `RawSensing` error shape, each carrying its operation token + a fix-hint message, sorted by `(id, operation)`; reassert empty-vs-failure is structurally distinct (FR-008/FR-011, SC-005).

### Implementation for User Story 5

- [X] T041 [US5] Complete diagnostics in `Snapshot.assemble`: map `RepoOk=false` and each `RawSensing` `Error` (ref resolution, merge-base, diff, status, branch, parse) to the right `SensingDiagnosticId` with `Operation` = the command/op token and a fix-hint `Message`; ensure `Range=None` on any range-resolution failure and that the `Diagnostics` list is sorted and non-empty exactly when something failed (FR-008/FR-011).
- [X] T042 [US5] Harden `Interpreter.senseSnapshot`/`realPorts` edge guards in `src/FS.GG.Governance.Snapshot/Interpreter.fs`: wrap every `Process` start/run in try/with → `Error` (a missing `git` / process-start failure → `GitUnavailable`); a nonzero exit → the command's `Error` reason; `senseSnapshot` NEVER throws. Read-only is already guaranteed by the closed `GitCommand` set (T014) — assert no mutating argv exists.

**Checkpoint**: failures are explicit and safe; sensing is provably read-only. US1–US5 pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: determinism/permutation proofs, the surface baseline, dependency/scope/no-network hygiene, and the quickstart run.

- [X] T043 [P] In `tests/FS.GG.Governance.Snapshot.Tests/DeterminismTests.fs`, add: `assemble` the same `RawSensing` twice → structural equality of the whole `RepoSnapshot` (SC-002); and an FsCheck property that permuting the order of raw diff/status entries (NUL records) yields an identical `RepoSnapshot` for fixed inputs (FR-009, SC-003).
- [X] T044 Generate `surface/FS.GG.Governance.Snapshot.surface.txt` from the built `FS.GG.Governance.Snapshot` assembly using the repo's surface-baseline convention, then add `tests/FS.GG.Governance.Snapshot.Tests/SurfaceDriftTests.fs` asserting the built surface matches the baseline (Principle II).
- [X] T045 [P] In `SurfaceDriftTests.fs` (or a dedicated module), add a dependency/scope-hygiene test asserting `FS.GG.Governance.Snapshot` references only `FS.GG.Governance.Config` (+ FSharp.Core, + transitive YamlDotNet) and not the kernel/host/adapters/Routing/CLI (research D1); assert no deterministic snapshot field carries raw command output, timing, pid, or an absolute host path (FR-010), and no hosting-provider/network symbol is referenced (SC-007); and assert Snapshot reads no `.fsgg`/YAML and requires nothing installed in the inspected repository (FR-014) — it references only `Config.Model` types + `normalizePath`, not the Config `Loader`/`Schema` parsing surface. This doubles as the FR-013 scope guard: the absence of routing, gate-registry, finding-severity, profile/mode-enforcement, and `route`/`ship`-command dependencies confirms no later-phase capability leaked in.
- [X] T046 [P] Run [quickstart.md](./quickstart.md) end-to-end and record the transcripts named in `readiness/README.md` (planResolution per form; assemble over raw fixtures; senseSnapshot changed/dirty/untracked over a real temp repo; not-a-repo & unknown-ref diagnostics; read-only byte-identity; routing feed-through), plus the SC-traceability note mapping SC-001…SC-007 to the proving tests.
- [X] T047 [P] Update `README.md` to list the new optional `FS.GG.Governance.Snapshot` library and link the sensing contract ([contracts/git-sensing.md](./contracts/git-sensing.md)); flip the `docs/initial-implementation-plan.md` Phase-2 legend rows for "deterministic glob precedence" (F015, ✅) and "git/CI snapshot facts" (this feature) as appropriate.

**Checkpoint**: full `dotnet test FS.GG.Governance.sln` green; surface baselines (Snapshot new + Config updated) committed and drift-checked; determinism, read-only, and no-network proven; quickstart validated.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately. T002–T004 (the Config normalizer exposure + re-bless) are a self-contained mini-unit that BLOCKS `assemble`'s normalization (T016/T025/T031); do them early.
- **Foundation (Phase 2)**: depends on Setup — BLOCKS all user stories. `planResolution` (T015) lives here (not in US3) so the US1 MVP is not blocked on a P2 story.
- **User Stories (Phases 3–7)**: all depend on Foundation. US1 and US2 are co-equal P1 and touch different parsers but both extend `assemble` and `senseSnapshot` (run US1 then US2, or share a developer). US3 (P2) is mostly tests over Foundation's `planResolution`. US4 (P3) and US5 (P2) extend `assemble`/`realPorts`/`senseSnapshot`.
- **Polish (Phase 8)**: depends on all user stories.

### Within Each User Story

- Tests are written first and must FAIL before implementation.
- `.fsi` contract (Phase 1) → FSI sketch (T011) → semantic tests → implementation (Principle I).
- Pure parsers/assemble before edge wiring; the edge (`senseSnapshot`) is filled incrementally per story (committed diff in US1, status in US2, branch/CI in US4, guards in US5).

### Parallel Opportunities

- Setup `[P]` tasks T003, T008, T010, T011, T012 run in parallel (after the files they touch exist); T002→T003→T004 are sequential (same Config surface).
- Foundation T016 is `[P]`; T013/T014/T015/T017/T018 are largely sequential (T017 depends on T013+T016; T018 depends on T014+T015+T017).
- All US1 tests (T019–T023), US2 tests (T027–T029), US3 tests (T033–T034), US5 tests (T038–T040) are `[P]` within their story.
- Most Polish tasks (T043, T045, T046, T047) are `[P]`; T044 precedes T045 if they share a file.
- The shared-function impl tasks (assemble: T017/T025/T031/T037/T041; senseSnapshot: T018/T026/T032/T037/T042) edit `Snapshot.fs`/`Interpreter.fs` and are therefore sequential within each file, not `[P]`.

---

## Suggested MVP Scope

**User Story 1** (P1) alone is the MVP: sensing the committed changed-path set of a real repository and feeding it straight into F015 routing — the missing half of routing, independently valuable before working-tree state, range options, or CI context exist. **User Story 2** (the co-equal P1) makes local previews honest by adding dirty/untracked working-tree state. **User Story 3** (P2) proves the range-resolution contract for local/CI parity; **User Story 5** (P2) hardens fail-safe + read-only; **User Story 4** (P3) adds branch + optional CI/PR context.

## Task Count

- Setup: 12 (T001–T012) — incl. 3 Config-normalizer Tier-1 tasks (T002–T004)
- Foundation: 6 (T013–T018)
- US1 (P1): 8 (T019–T026) — 5 tests, 3 implementation
- US2 (P1): 6 (T027–T032) — 3 tests, 3 implementation
- US3 (P2): 3 (T033–T035) — 2 tests, 1 implementation
- US4 (P3): 2 (T036–T037) — 1 test, 1 implementation
- US5 (P2): 5 (T038–T042) — 3 tests, 2 implementation
- Polish: 5 (T043–T047)
- **Total: 47 tasks**
