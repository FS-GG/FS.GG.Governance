---
description: "Task breakdown for the Governance-side fs-gg-ui rename guard"
---

# Tasks: Governance-side fs-gg-ui rename guard

**Input**: Design documents from `/specs/083-fs-gg-ui-rename-guard/`

**Prerequisites**: plan.md, spec.md (US1–US3), research.md (D1–D4), data-model.md
(ForbiddenToken / ProvenanceAllowlist / ScanResult), contracts/rename-guard.contract.md (R1–R7),
quickstart.md

**Tier**: Tier 2 (test + docs only). No public F# surface, no `.fsi`, no surface-area baseline
(FR-007, SC-004). The guard's "API" is the observable behaviour of the Expecto suite; the tests
**are** the deliverable. Constitution Principle I is satisfied by spec-first + tests-first; there
is no production `.fs`/`.fsi` to sketch.

**Tests**: REQUIRED — they are the whole feature. Every contract rule R1–R7 is a named Expecto
test asserting a success criterion. Real evidence (Principle V): R1/R2 scan the **real**
git-tracked tree; the red path (R3–R7) passes **literal input strings to the pure `scanText`** —
the matcher's real domain input, so these are ordinary real-evidence unit tests, **not**
synthetic-evidence substitutes (no `Synthetic` token applies; Principle V's disclosure rule is for
faked *dependencies*, not literal arguments to a pure function). The only sense of "synthetic" here
is *no committed tripwire*: those red-path literals live in the guard's own **scan-excluded** test
source (see `GuardSelfExclusion` below), never as a tracked fixture the production scan would read
(edge case "Guard's own fixtures").

**Elmish/MVU**: N/A. Principle IV names "scanning a repository" as I/O, but its trigger is
*stateful* I/O (multi-step state, retries, persistence, recovery) — this guard has none: a single
one-shot `git ls-files` + read folded into one **pure** projection `(tracked tree, token set,
exclusions) → Violation list`, with no durable `Model` and no public `.fsi` to host
`Model`/`Msg`/`Effect` (Tier 2). The lone I/O edge is isolated in a thin reader (`scanTrackedTree`)
kept out of the pure matcher (`scanText`), honoring Principle IV's spirit without an MVU loop (plan
Constitution Check IV).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — different file, no dependency on another incomplete task in the phase.
- **[Story]**: `US1`/`US2`/`US3` (or none for Setup/Foundational/Polish).
- Tier annotations omitted (every phase is Tier 2, matching the spec's overall tier).

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped with written rationale on the line.
- Never mark a failing task `[X]`. Never weaken an assertion to green a build — narrow scope and
  document it.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the dedicated, self-contained guard project and wire it into the build
(research D1). No guard behaviour yet.

- [X] T001 Create the new test project
  `tests/FS.GG.Governance.RenameGuard.Tests/FS.GG.Governance.RenameGuard.Tests.fsproj`
  (`net10.0`; Expecto + `Microsoft.NET.Test.Sdk` + `YoloDev.Expecto.TestSdk` under central package
  management; `IsPackable=false`), modelled on
  `tests/FS.GG.Governance.ReferenceGateSet.Tests/FS.GG.Governance.ReferenceGateSet.Tests.fsproj`.
  Add a **single** `ProjectReference` to `tests/FS.GG.Governance.Tests.Common` (for
  `RepositoryHelpers.repoRoot`) and **no** production `src/` reference and **no** new package
  (FR-009, contract I3). Declare `<Compile>` order `RenameGuardTests.fs` then `Main.fs`.
- [X] T002 [P] Create the Expecto entry point `tests/FS.GG.Governance.RenameGuard.Tests/Main.fs`
  mirroring `tests/FS.GG.Governance.ReferenceGateSet.Tests/Main.fs` (`[<EntryPoint>]` →
  `runTestsInAssemblyWithCLIArgs`).
- [X] T003 Register `FS.GG.Governance.RenameGuard.Tests` in `FS.GG.Governance.sln` under the tests
  solution folder. Depends on T001.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` resolves the new (still test-less) project.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the guard machinery inside
`tests/FS.GG.Governance.RenameGuard.Tests/RenameGuardTests.fs` (all private to the test module —
no public surface, FR-007). Every user-story test depends on this phase. Source the exact patterns,
paths, and shapes from data-model.md.

**⚠️ CRITICAL**: No R1–R7 test can pass until this phase is complete.

- [X] T004 Define the result types in `RenameGuardTests.fs`:
  `type Violation = { File: string; Line: int; Class: string; Matched: string; Replacement: string }`
  (data-model §ScanResult).
- [X] T005 Define the `ForbiddenToken` set as a list of records
  `{ Class: string; Pattern: Regex; Replacement: string }` covering all four classes from
  data-model §ForbiddenToken — CPM property (`…Version`), contract id `…version`, contract id
  `…bom`, snapshot-tag `…/v<n>`/`/v*` — each `RegexOptions.IgnoreCase`, **assembled from string
  fragments** (not written as a literal suffix-bearing token) so the guard source cannot self-match
  (R6, contract). Carries the canonical `fs-gg-ui` replacement per class (FR-004). Depends on T004.
- [X] T006 [P] Define the two scan-exclusion constants in `RenameGuardTests.fs`, each a named,
  commented binding (FR-003, D4, SC-006):
  - `provenanceAllowlist` — the four repo-relative, forward-slash paths from data-model
    §ProvenanceAllowlist (`.specify/memory/constitution.md`, `docs/governance-design/index.md`,
    `docs/initial-design.md`, `docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md`);
    documents the *documentary-prose* boundary.
  - `guardSelfExclusion` — the guard's own test directory prefix
    `tests/FS.GG.Governance.RenameGuard.Tests/` (data-model §GuardSelfExclusion). The red-path tests
    (R3–R5/T013–T015) carry legacy tokens **verbatim** as the input strings they assert match;
    excluding the guard's own source keeps the production scan (R1) from self-tripping on them. This
    is distinct from provenance — it is the guard's own scaffolding, not lineage prose; neither
    constant disables a pattern.
- [X] T007 Implement the **pure** matcher
  `scanText (path: string) (contents: string) : Violation list` — apply every `ForbiddenToken.Pattern`
  line by line, emitting one `Violation` per match with its `Class`/`Matched`/`Replacement` and 1-based
  `Line`. No I/O. Depends on T004, T005.
- [X] T008 Implement the I/O reader `scanTrackedTree () : Violation list` — shell `git ls-files`
  with `WorkingDirectory = RepositoryHelpers.repoRoot`, read each tracked file that is **neither**
  under `provenanceAllowlist` **nor** under `guardSelfExclusion` as UTF-8, fold `scanText`; tolerate
  a non-UTF-8/read failure as no-match (never throw on a single file) but fail **loudly** if `git`
  itself fails (Principle VI, D2, FR-008). Depends on T006, T007.
- [X] T009 Implement the failure-message renderer
  `render (v: Violation) : string` producing
  `"<File>:<Line> contains legacy <Class> '<Matched>' — rename to the fs-gg-ui root (<Replacement>)."`
  (data-model §Failure-message shape; FR-005/SC-006). Depends on T004.

**Checkpoint**: the matcher, reader, allowlist, and renderer compile; the test list is still empty.

---

## Phase 3: User Story 1 - Close the rename's governance checkbox with evidence (Priority: P1) 🎯 MVP

**Goal**: A green, citable production scan proving zero legacy version-machinery identifiers in the
tracked tree on `main` (SC-001), so the cross-repo P5 checkbox can be closed (SC-005).

**Independent Test**: `dotnet test tests/FS.GG.Governance.RenameGuard.Tests` passes on a clean
checkout of `main`; R1 reports an empty `Violation list`.

- [X] T010 [US1] Add R1 test
  `Production scan of the tracked tree finds zero legacy version-machinery identifiers` —
  assert `scanTrackedTree () = []`, printing every offender via `render` on failure (FR-001, FR-008,
  SC-001, contract R1). Depends on Phase 2.
- [X] T011 [US1] Run the suite and confirm R1 green on the real tree
  (`dotnet test tests/FS.GG.Governance.RenameGuard.Tests`); record the run as the citable evidence
  for SC-001. Depends on T010.
- [X] T012 [US1] Create
  `docs/reports/2026-06-27-fs-gg-ui-rename-guard-governance-checkbox.md` — a short note recording the
  closed governance-side P5 checkbox and pointing at `FS.GG.Governance.RenameGuard.Tests` as the
  durable evidence (SC-005, plan §Project Structure). Touch only this new file — no provenance file,
  no `.fsi`, no baseline. Depends on T011.

**Checkpoint**: MVP — the guard passes on `main` and the closed checkbox has a durable evidence
pointer. US1 is independently demoable.

---

## Phase 4: User Story 2 - Catch a future legacy identifier before it merges (Priority: P2)

**Goal**: Any reintroduced legacy identifier (incl. case/separator variants) turns the guard red
with an actionable diagnostic naming the file and the canonical `fs-gg-ui` replacement; canonical
usage and the guard's own source stay green.

**Independent Test**: Feed a literal straggler string to `scanText` → ≥1 `Violation` naming the
`fs-gg-ui` replacement; rewrite with the canonical root → empty. (Exercised in-suite over a temp
dir / literals — no committed tripwire.)

- [X] T013 [P] [US2] Add R3 test
  `A FsSkiaUiVersion reference is caught with the FsGgUiVersion replacement` — assert
  `scanText "fake/Directory.Packages.props" "<FsSkiaUiVersion>1.0.0</FsSkiaUiVersion>"` yields ≥1
  `Violation` with `Class` = CPM property, `Matched` containing `FsSkiaUiVersion`, `Replacement` =
  `FsGgUiVersion` (FR-002, FR-005, SC-002, contract R3). Depends on Phase 2.
- [X] T014 [P] [US2] Add R4 test
  `Legacy contract ids and the fs-skia-ui tag namespace are caught` — literal input strings with
  `fs-skia-ui-version`, `fs-skia-ui-bom`, and `fs-skia-ui/v1` each yields a `Violation` naming the
  correct `fs-gg-ui-version` / `fs-gg-ui-bom` / `fs-gg-ui/v*` replacement (FR-002, FR-004,
  contract R4). Depends on Phase 2.
- [X] T015 [P] [US2] Add R5 test
  `Case and separator variants match; the bare FS-Skia-UI repo name does not` — `scanText` matches
  `Fs_Skia_Ui_Version`, `fs.skia.ui.bom`, `FS-SKIA-UI/V2` (version-pinning variants) **and** returns
  empty for `source-analysis of FS-Skia-UI` and
  `https://github.com/EHotwagner/FS-Skia-UI/blob/main/x.md` (the suffix anchor, D3; edge "variants").
  Depends on Phase 2.
- [X] T016 [P] [US2] Add R6 test
  `Canonical fs-gg-ui identifiers pass and the guard source does not self-trip` — `scanText` over
  `FsGgUiVersion` / `fs-gg-ui-version` / `fs-gg-ui-bom` / `fs-gg-ui/v1` returns empty, AND the
  production scan (R1) yields no violation despite the red-path literals living in the tracked tree.
  Self-non-match rests on **both** mechanisms (data-model §GuardSelfExclusion, contract R6): the
  `Pattern` regexes are **fragment-assembled** (pattern definitions aren't legacy tokens) and the
  guard's own test directory is in `guardSelfExclusion` (its red-path input literals aren't scanned).
  Depends on Phase 2 (and T010 for the production path).
- [X] T017 [P] [US2] Add R7 test
  `A violation message names the file, identifier, and fs-gg-ui replacement` — `render` of a produced
  `Violation` contains the file, line, offending identifier, and canonical replacement (FR-005,
  SC-006, contract R7). Depends on T009.
- [X] T018 [US2] Manually validate SC-002 per quickstart: append `<FsSkiaUiVersion>…` to a tracked
  file, confirm R1 turns red naming the file + `FsGgUiVersion`, then revert and confirm green.
  Record the result; leave the tree clean. Depends on T013–T017.

**Checkpoint**: the guard reliably goes red on legacy roots and stays green on the canonical root —
US1 + US2 both pass.

---

## Phase 5: User Story 3 - Preserve historical provenance untouched (Priority: P3)

**Goal**: The four historical-provenance files are neither rewritten nor flagged; the boundary
between version machinery and the predecessor repo name is frozen.

**Independent Test**: With the four provenance files unchanged, the guard passes; their content is
byte-identical before and after the feature (empty diff).

- [X] T019 [US3] Add R2 test
  `The four provenance files are present, allowlisted, and not flagged` — assert each
  `provenanceAllowlist` path exists under `repoRoot`, is excluded from the scan, and that
  `scanTrackedTree ()` remains empty with them present (FR-003, FR-006, SC-003, contract R2). Include
  a **US3-local** assertion that `scanText` returns empty for the bare provenance prose
  (`source-analysis of FS-Skia-UI`, `https://github.com/EHotwagner/FS-Skia-UI/blob/main/x.md`) so
  US3 is self-contained and does not depend on US2's T015 to prove the suffix anchor spares lineage.
  Depends on Phase 2.
- [X] T020 [US3] Verify SC-003 / contract I2: `git diff --stat main --` over the four provenance
  files is **empty** (no lineage text rewritten); record the empty diff. Depends on T019.

**Checkpoint**: all three stories pass; provenance is provably untouched.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Whole-suite validation and the Tier-2 invariant audit (contract I1/I3, SC-004).

- [X] T021 Run the full guard suite green end-to-end
  (`dotnet test tests/FS.GG.Governance.RenameGuard.Tests` → R1–R7 pass) and run
  `dotnet test FS.GG.Governance.sln` to confirm the new project integrates without regressing the
  neighbours. Depends on Phases 3–5.
- [X] T022 [P] Verify Tier-2 honored (SC-004, contract I1): `git diff --stat main -- '*.fsi'` and
  `git diff --stat main -- src/` are both **empty**, and no surface-area baseline file appears in the
  diff. Record the empty diffs.
- [X] T023 [P] Verify self-containment (FR-009, contract I3): the project's only `ProjectReference`
  is `FS.GG.Governance.Tests.Common`, with no `src/` governance-library reference, and it runs under
  the standard `dotnet test` command.
- [X] T024 [P] Walk quickstart.md end-to-end (run the guard, the red-path manual check, the
  provenance diff, the Tier-2 diffs) and confirm each "Expected" holds. Leave the tree clean.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all user stories (the matcher/reader/allowlist
  must exist before any R-test passes).
- **User stories (Phases 3–5)**: each depends on Foundational. Once Phase 2 is done they are largely
  independent and can proceed in parallel or in priority order P1 → P2 → P3.
- **Polish (Phase 6)**: depends on all desired stories.

### User-story dependencies

- **US1 (P1)**: after Foundational — no dependency on US2/US3.
- **US2 (P2)**: after Foundational — independent of US1/US3 (R6/T016 reuses the R1 production path
  from T010, otherwise standalone).
- **US3 (P3)**: after Foundational — independent of US1/US2.

### Parallel opportunities

- T002 ∥ the rest of Setup once the `.fsproj` exists.
- T006 (allowlist) ∥ T005 (token set) in Phase 2; T007/T008/T009 then chain off them.
- The five red-path tests T013–T017 are all `[P]` (different test bodies, shared read-only machinery)
  and can be written together; T019 (US3) can be written alongside them.
- Polish audits T022/T023/T024 are `[P]`.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1: Setup → project builds.
2. Phase 2: Foundational → matcher/reader/allowlist/renderer compile.
3. Phase 3: US1 → R1 green on `main`, checkbox-evidence note added.
4. **STOP and VALIDATE**: cite the green run on the P5 issue (SC-005). This alone satisfies the
   board item's literal governance-side checkbox.

### Incremental delivery

1. Setup + Foundational → guard machinery ready.
2. US1 → R1 green + docs pointer → **MVP** (close the checkbox).
3. US2 → red-path + diagnostic tests → the guard is now durable against future regressions.
4. US3 → provenance allowlist + empty-diff proof → boundary frozen.
5. Polish → whole-suite + Tier-2 invariant audit.

---

## Notes

- The token set, the two exclusion constants, and the `Violation` shape are authored verbatim from
  data-model.md; the per-rule assertions from contracts/rename-guard.contract.md (R1–R7). Do not
  invent new tokens.
- Real evidence (Principle V): R1/R2 scan the live tree; the red path passes **literal strings to the
  pure matcher** — its real domain input, so real evidence, **not** synthetic-evidence (no `Synthetic`
  token). "Synthetic" here means only *no committed tripwire*: those literals sit in the guard's own
  `guardSelfExclusion`-skipped test source (edge "Guard's own fixtures").
- The load-bearing scoping insight (D3): the suffix anchor (`Version`/`version`/`bom`/`/v<n>`) makes
  prose non-matching *by construction*; the provenance allowlist is defense-in-depth, not the
  discriminator. The `guardSelfExclusion` is a separate concern — it keeps the guard's own red-path
  input literals out of the production scan (resolves the self-trip).
- Commit one phase (or one logical group) at a time; keep the tree clean after every manual
  red-path/diff validation. Stop at any checkpoint to validate a story independently.

## Recorded deviation (T006/T008 — scan exclusions)

The spec/data-model scoped `guardSelfExclusion` to a single prefix: the guard's **test** directory
(`tests/FS.GG.Governance.RenameGuard.Tests/`). But this feature's **own spec directory**
(`specs/083-fs-gg-ui-rename-guard/`) also quotes the full suffix-bearing legacy tokens verbatim as
worked examples (spec/plan/data-model/contracts/tasks) — and those spec files become **tracked on
commit**, which would self-trip R1 (`scanTrackedTree () = []`). The spec dir is the exact same class
of artifact as the test dir (the guard's own scaffolding documenting/testing the ban), so it is
excluded on the identical rationale. `scanExclusions` is therefore a **list of two prefixes** rather
than one. Verified minimal: no **other** tracked spec (older features) names the machinery tokens, so
the ban's coverage elsewhere is unaffected; neither exclusion disables a pattern. Real evidence: R1
re-run against the **fully staged tree** (spec docs + test source tracked) stays green (7/7), and the
planted-straggler red-path still fails loudly outside the excluded dirs. Documented in-code at the
`scanExclusions` binding in `RenameGuardTests.fs`.
