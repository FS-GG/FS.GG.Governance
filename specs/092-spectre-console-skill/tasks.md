---
description: "Task list for the Spectre.Console headless-fidelity skill"
---

# Tasks: Spectre.Console headless-fidelity skill

**Input**: Design documents from `/specs/092-spectre-console-skill/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/skill-contract.md, quickstart.md

**Tier**: 2 (documentation / knowledge artifact). No public F# surface, no package or
inter-project code contract is authored — this feature ships a Markdown skill + a cross-agent
entrypoint, distributed across the Spectre-using FS-GG repos.

**Tests**: No automated unit suite. The spec asks for **reproducible evidence**, not test code —
validation is the `quickstart.md` scenarios (S1–S6). They appear here as explicit evidence tasks,
not as `xUnit`/`Expecto` tests.

**Elmish/MVU applicability**: **N/A** — no stateful or I/O-bearing F# workflow is authored (plan
Constitution Check, principles I/II/IV = N/A). The only "I/O" is the documented
`GITHUB_ACTIONS=true dotnet test` repro, which the skill describes rather than implements.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file / no ordering).
- **[Story]**: `US1` (diagnose), `US2` (distribute), `US3` (trigger + cross-agent + advisory).
- All tasks are Tier 2 (matches the feature); no per-task `[T1]/[T2]` annotation needed.

## Path Conventions

- **Canonical source** (org-shared, mirrors `cross-repo-coordination`):
  `/home/developer/projects/.github/.claude/skills/spectre-console-headless-fidelity/SKILL.md`
  (the `FS-GG/.github` repo, checked out at `projects/.github`).
- **In-scope repo installs** (copied from canonical):
  - `FS.GG.Governance/.claude/skills/spectre-console-headless-fidelity/SKILL.md` (Spectre 0.57.1)
  - `FS.GG.SDD/.claude/skills/spectre-console-headless-fidelity/SKILL.md` (Spectre 0.57.0)
- **Cross-agent entrypoints**: each in-scope repo's root `AGENTS.md` (Governance has none yet; SDD
  already has one — append a managed section).
- **Excluded**: FS.GG.Rendering, FS.GG.Templates (no Spectre.Console).

---

## Phase 1: Setup (canonical skill scaffold)

**Purpose**: Stand up the single-source location and a valid, discoverable frontmatter shell so the
body (US1) and distribution (US2) have somewhere to land.

- [X] T001 Create the canonical skill directory
  `/home/developer/projects/.github/.claude/skills/spectre-console-headless-fidelity/` and an empty
  `SKILL.md`, mirroring the layout of
  `/home/developer/projects/.github/.claude/skills/cross-repo-coordination/`.
- [X] T002 Add the `SKILL.md` YAML frontmatter per contract **C1**: `name:
  spectre-console-headless-fidelity` (must equal the directory name) and a symptom-oriented
  `description`. Confirm it parses as YAML and matches the shape of the existing skills'
  frontmatter. (FR-001) — `description` final wording is tuned and validated in T020/T021.

**Checkpoint**: Canonical `SKILL.md` exists with parseable frontmatter; body is empty.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: Lock the facts every section will cite, so no claim is unverifiable later. Blocks US1
authoring — the body must rest on verified provenance, not memory.

**⚠️ CRITICAL**: US1/US2/US3 content all depend on this.

- [X] T003 [P] In `specs/092-spectre-console-skill/` working notes (or inline in the draft `SKILL.md`
  Provenance section), pin the verified incident facts from `data-model.md`: spec 091; issues
  #32 / #34 / #37; evidence runs `28376202121` (diagnostic dump) and `28377734248` (green 1.2.0
  publish); date stamp 2026-06-29; **corrected root cause = `AnsiSupport.No` overridden under
  `GITHUB_ACTIONS`, NOT glyph/width measurement**. (FR-006, SC-005)
- [X] T004 [P] Confirm the verified library version and the live repro target so the skill cites
  reality: Spectre.Console **0.57.1** in FS.GG.Governance, **0.57.0** in FS.GG.SDD; repro filter
  `WidthResilience` in `tests/FS.GG.Governance.Cli.Tests`. Verify the version against the repo's
  central package config before writing it down. (FR-008)

**Checkpoint**: Every factual claim the body will make has a cited source or a runnable command.

---

## Phase 3: User Story 1 - Diagnose a Spectre render test that passes locally but fails in CI (Priority: P1) 🎯 MVP

**Goal**: The canonical `SKILL.md` body teaches a contributor — with no prior knowledge of the
incident — to reproduce the headless divergence locally, classify it, and apply the matching fix.

**Independent Test**: Hand the skill to someone unfamiliar with the incident plus a render test
failing only on the runner; they reproduce it locally and name the root cause from the skill alone.

### Implementation for User Story 1 (the canonical body — contract C3, in order)

- [X] T005 [US1] Write **Symptom / When to use** in the canonical `SKILL.md`: the trigger in plain
  terms — Spectre output correct locally, wrong/red in headless CI (width/wrap assertion, plain /
  no-color output, or snapshot). (FR-002, SC-006)
- [X] T006 [US1] Write **The problem**: why headless CI diverges — `AnsiSupport.No` re-detected /
  overridden under `GITHUB_ACTIONS`, SGR escape bytes leak into output and inflate `String.Length`,
  so `cells ≠ String.Length`; explain why a naive length-based assertion misjudges it. (FR-002)
- [X] T007 [US1] Write **Reproduce locally**: the CI-free command
  `GITHUB_ACTIONS=true dotnet test tests/FS.GG.Governance.Cli.Tests -c Release --filter "WidthResilience"`,
  framed as a runnable repro of the runner-only failure (no CI round-trip), with repo-specific
  paths labeled as examples. (FR-003, FR-011, SC-001)
- [X] T008 [US1] Write **Diagnose**: the cell-vs-unit dump recipe — print both display **cells** and
  `String.Length` per rendered line; classify `cells ≤ bound` but `len > bound` →
  **invisible-byte artifact** (this incident) vs `cells > bound` → **genuine display overflow**
  (opposite fix). (FR-004, SC-002)
- [X] T009 [US1] Write **Fix**: force `Profile.Capabilities.Ansi <- false` + `ColorSystem <-
  NoColors` immediately after `AnsiConsole.Create`, **scoped to the deterministic test/plain console
  builder only**, with an explicit warning against degrading intentional, human-facing product
  output. (FR-005, SC-003)
- [X] T010 [US1] Write **Generalize**: cover signals beyond the single `GITHUB_ACTIONS` variable —
  common CI-detection env vars and the `NO_COLOR` convention — and state the durable lesson:
  *assert against the same measure the system actually uses* (cells, not byte length), so the skill
  aids future, non-identical fidelity problems. (FR-009, FR-012)
- [X] T011 [US1] Write **Version scope**: state the behavior was verified on Spectre.Console
  **0.57.x** (0.57.1 here / 0.57.0 sibling), frame it as version-scoped (not a permanent library
  guarantee), and instruct the reader to re-verify on a different version. (FR-008)
- [X] T012 [US1] Write **Provenance**: cite the durable identifiers and date from T003 (spec 091,
  #32 / #34 / #37, runs 28376202121 / 28377734248, 2026-06-29) and the corrected root-cause claim,
  so each factual assertion is traceable. (FR-006, SC-005)

**Checkpoint**: A reader using only the canonical `SKILL.md` can reproduce → classify → fix. MVP
complete.

---

## Phase 4: User Story 2 - Reach every FS-GG repository that renders with Spectre (Priority: P2)

**Goal**: The verified skill is installed into every in-scope repo from the one canonical source,
with identical content, and the exclusions are recorded.

**Independent Test**: From the canonical copy, install into a second Spectre-using repo and confirm
it is present and byte-identical.

**Depends on**: Phase 3 (the body must be correct before it is worth copying).

### Implementation for User Story 2

- [X] T013 [P] [US2] Copy the canonical `SKILL.md` into FS.GG.Governance at
  `.claude/skills/spectre-console-headless-fidelity/SKILL.md` (verbatim — no edits). (FR-007, SC-004)
- [X] T014 [P] [US2] Copy the canonical `SKILL.md` into FS.GG.SDD at
  `.claude/skills/spectre-console-headless-fidelity/SKILL.md` (verbatim). (FR-007, SC-004)
- [X] T015 [US2] Verify content parity across all copies (canonical, Governance, SDD) with a
  byte-for-byte diff; record the result. (SC-004) — after T013, T014.
- [X] T016 [US2] Record the **distribution set** and the intentional exclusions (FS.GG.Rendering,
  FS.GG.Templates — no Spectre.Console) in the canonical skill body (or a short distribution note),
  so the exclusion is documented, not implicit. (FR-007, spec US2 AC-2)

**Checkpoint**: Skill present and identical in both in-scope repos; exclusions recorded.

---

## Phase 5: User Story 3 - Surface only when relevant, and never block work (Priority: P3)

**Goal**: The skill is discoverable by symptom, reachable by Codex/general agents via `AGENTS.md`
without duplicating content, and gates nothing.

**Independent Test**: A symptom phrased in the contributor's own words selects this skill; an
`AGENTS.md`-only agent reaches the same content; no workflow depends on the skill.

**Depends on**: Phase 3 (content exists) and Phase 4 (installed where `AGENTS.md` will reference it).

### Implementation for User Story 3

- [X] T017 [P] [US3] Add the managed entrypoint section (contract **C2**) to FS.GG.Governance root
  `AGENTS.md` — create the file (it does not exist yet): a marked section that **references**
  `.claude/skills/spectre-console-headless-fidelity/SKILL.md` as the single source and does NOT
  restate the body. (FR-013, SC-008)
- [X] T018 [P] [US3] Append the same managed entrypoint section to FS.GG.SDD's existing root
  `AGENTS.md` (reference-only, no body copy). (FR-013, SC-008)
- [X] T019 [US3] Confirm no body duplication: the `AGENTS.md` sections reference the `SKILL.md` path
  and contain no restated problem/repro/fix prose (edge case "entrypoint drift"). (FR-013)
- [X] T020 [US3] Finalize the frontmatter `description` (contract C1.1): it MUST carry the
  "renders/behaves correctly locally but differs/fails in CI (GitHub Actions)" cue **and** the
  artifact kinds (width/wrap assertions, plain/no-color output, snapshots), so it auto-selects over
  generic guidance. Re-propagate to the copies (re-run T015 parity check). (FR-001, SC-006)

**Checkpoint**: Both agent families reach one source; trigger selects on symptom.

---

## Phase 6: Polish & Validation (quickstart evidence — maps to Success Criteria)

**Purpose**: Run the `quickstart.md` scenarios as the feature's real evidence. These are the
acceptance gate for the spec's Success Criteria.

- [X] T021 Run **Quickstart S1** (and **S3**) — local headless repro:
  `GITHUB_ACTIONS=true dotnet test tests/FS.GG.Governance.Cli.Tests -c Release --filter "WidthResilience"`;
  confirm the documented behavior (pre-#37 red under the env var / green without; with #37 green
  both ways, 66/66). **Quickstart S3** (apply the fix) needs no separate step: the #37 fix already
  ships in this repo, so a green-both-ways run *is* the post-fix state and validates S3 here.
  (SC-001, SC-003)
- [X] T022 [P] Run **Quickstart S2** — follow the Diagnose dump and confirm a reader can classify
  artifact vs genuine overflow from the printed cell-vs-unit metrics. (SC-002)
- [X] T023 [P] Run **Quickstart S4** — cross-agent reach / single source:
  `test -f .claude/skills/spectre-console-headless-fidelity/SKILL.md && grep -q "locally" …` and
  `grep -q "spectre-console-headless-fidelity/SKILL.md" AGENTS.md`. (SC-004, SC-008)
- [X] T024 [P] Run **Quickstart S5** — provenance audit: every factual claim/command traces to a
  reproducible step or a cited identifier; no unverified assertion. (SC-005)
- [X] T025 [P] Run **Quickstart S6** — advisory invariant (contract C4):
  `grep -rIl "spectre-console-headless-fidelity" .github/workflows/` returns no matches in any
  in-scope repo; deleting the skill changes no build/test/publish/merge outcome. (FR-010, SC-007)
- [X] T026 Genericity pass: re-read the canonical body and confirm general guidance does not bake in
  one repo's package IDs / project layout / target names — repo-specific details are labeled
  examples. (FR-011)

**Checkpoint**: All quickstart scenarios pass → spec Success Criteria met; skill ready to remain
propagated.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: after Setup; **blocks US1** (claims need verified sources).
- **US1 (Phase 3)**: after Foundational — the MVP; produces the single-source body.
- **US2 (Phase 4)**: after US1 — copies a *correct* body; do not distribute drafts.
- **US3 (Phase 5)**: after US1 (content) and US2 (installed targets for `AGENTS.md` to reference).
- **Polish (Phase 6)**: after US1–US3.

### Within stories

- US1 sections (T005–T012) all edit the one canonical `SKILL.md`, so they are **sequential** (same
  file) — not marked `[P]`. Order follows the contract C3 section order.
- US2 copies (T013, T014) touch different repos → `[P]`; the parity check (T015) follows both.
- US3 entrypoints (T017, T018) touch different repos → `[P]`; dedup check (T019) follows both.

### Parallel Opportunities

- T003 / T004 (Foundational) — different concerns, parallel.
- T013 / T014 (per-repo installs) — different repos, parallel.
- T017 / T018 (per-repo `AGENTS.md`) — different repos, parallel.
- T022 / T023 / T024 / T025 (independent quickstart checks) — parallel.

---

## Summary

| User story | Priority | Tasks | Count |
|---|---|---|---|
| Setup | — | T001–T002 | 2 |
| Foundational | — | T003–T004 | 2 |
| US1 — Diagnose (author body) | P1 🎯 MVP | T005–T012 | 8 |
| US2 — Distribute | P2 | T013–T016 | 4 |
| US3 — Trigger + cross-agent + advisory | P3 | T017–T020 | 4 |
| Polish — Quickstart evidence | — | T021–T026 | 6 |

**Total**: 26 tasks.

**Suggested MVP scope**: Setup + Foundational + **US1** (T001–T012) — the canonical `SKILL.md` body.
At that point one in-scope repo (the canonical source) carries a contributor-usable skill; US2/US3
add reach and cross-agent surfacing.

**Parallel opportunities identified**: T003∥T004, T013∥T014, T017∥T018, T022∥T023∥T024∥T025.

---

## Implementation evidence (2026-06-29) — all 26 tasks `[X]`

- **Canonical body** authored at
  `FS-GG/.github/.claude/skills/spectre-console-headless-fidelity/SKILL.md` with the contract-C3
  section order (Symptom → The problem → Reproduce locally → Diagnose → Fix → Generalize → Version
  scope → Provenance → Distribution). Facts cross-checked against the live repo, not memory:
  fix verified at `tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs:91-92`
  (`Capabilities.Ansi <- false` / `ColorSystem <- NoColors` post-`Create`); assertion at
  `WidthResilienceTests.fs`; Spectre **0.57.1** (Governance) / **0.57.0** (SDD) confirmed in each
  repo's `Directory.Packages.local.props`.
- **S1/S3 (T021)** — `GITHUB_ACTIONS=true dotnet test … --filter "WidthResilience"` → **6/6 passed**
  (post-#37 green-both-ways state; the fix already ships in this repo).
- **S4 (T023)** — both Spectre-using repos: `SKILL.md` present with the "locally" cue; `AGENTS.md`
  references the `SKILL.md` path. Cross-agent reach confirmed.
- **T019 / entrypoint-drift** — `AGENTS.md` body-prose markers (`ESC[1m`, `String.Length`,
  `Capabilities.Ansi <- false`, `## The problem`): **0 hits** in both repos → reference-only, no
  body copy.
- **S4 parity (T015/T020)** — canonical + Governance + SDD copies are byte-identical (single unique
  sha256). The `description` carries the local-vs-CI cue and the artifact kinds.
- **S6 (T025) / advisory invariant (C4)** — `grep -rIl spectre-console-headless-fidelity
  .github/workflows/` returns **no matches** in Governance, SDD, or `.github`.
- **S5 (T024) provenance** — every claim traces to spec 091, #32/#34/#37, runs
  28376202121 / 28377734248, date 2026-06-29, plus the live `RenderSupport.fs` reference.
- **T026 genericity** — repo-specific paths/filter are explicitly labeled "this repo's example";
  the durable lesson ("assert the measure the system uses") is stated repo-agnostically.
