---
description: "Task breakdown for 093 — first-class spectre-console skill"
---

# Tasks: First-class Spectre.Console skill (general knowledge + docs)

**Input**: Design documents from `/specs/093-spectre-console-reference/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/skill-contract.md, quickstart.md

**Tier**: Tier 2 documentation/knowledge artifact (matches spec Assumptions + plan Constitution
Check). No F# code, `.fsi`, package, or workflow changes. Per-task `[T1]/[T2]` annotations are
omitted because every phase matches the Tier 2 classification.

**Elmish/MVU (Principle IV)**: **N/A** — this feature authors no stateful/I/O workflow. The skill
*describes* the existing `RenderMode`/`HumanRender` edge; it adds no `Model`/`Msg`/`Effect`. Recorded
in the evidence-obligations task (T025).

**Tests**: The spec does not request an automated unit suite — validation is by the `quickstart.md`
scenarios (S1–S8). The `dotnet test … --filter WidthResilience` runs in T013/T015 are **live
evidence re-runs** of existing tests, not new tests.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file/target).
- **[Story]**: `[US1]`/`[US2]`/`[US3]` traceability; Setup/Foundational/Polish are shared.
- Paths are exact. `~/projects/.github` = the canonical `FS-GG/.github` source of truth.

**Single-file caution**: the canonical body lives in ONE file
(`~/projects/.github/.claude/skills/spectre-console/SKILL.md`). All authoring tasks that edit it
(T005–T014) are **sequential — never `[P]` against each other** despite touching distinct sections.

---

## Phase 1: Setup (Shared groundwork)

**Purpose**: Capture the inputs every later phase verifies against, before any authoring.

- [X] T001 [P] Confirmed all grounding-source pointers exist: `src/FS.GG.Governance.HumanRender/{RichRender,Capability,Tui,Watch}.fsi`,
  `src/FS.GG.Governance.HumanText/{RenderMode,ReportView,HumanText}.fsi`,
  `tests/FS.GG.Governance.Cli.Tests/{RenderSupport.fs,WidthResilienceTests.fs}` — `ls` all resolved.
- [X] T002 [P] Captured 092 baseline from `.claude/skills/spectre-console-headless-fidelity/SKILL.md`
  (byte-identical across this repo + SDD + canonical, confirmed by `diff`). Every element — reproduce
  command, cell-vs-byte dump, classification table, scoped fix + warning, generalize lesson, version
  scope, full provenance — carried verbatim-in-substance into the absorbed Part B pitfall (T014).
- [X] T003 [P] Captured peer shape from the **canonical** `~/projects/.github/.claude/skills/cross-repo-coordination/SKILL.md`
  (`--- name/description ---` core). The new skill matches that core; `metadata.source` is the allowed
  additive provenance field (C1.2).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the renamed canonical skill scaffold. **⚠️ Blocks all body authoring.**

- [X] T004 Created `~/projects/.github/.claude/skills/spectre-console/SKILL.md` with `name: spectre-console`,
  a dual-trigger `description` (topic cue "Work with Spectre.Console … output in this project" + symptom
  cue "behaves correctly locally but differs or fails in CI (GitHub Actions)"), and `metadata.source`
  (spec 091 / #32 / #34 / #37 + 093 evolution). S1 grep confirmed both cues present.
- [X] T005 Body skeleton laid down: Part A (capability/Profile model, markup, tables, panels, rules &
  trees, prompts, live & status, capability profiles, upstream link-out) and Part B (rendering boundary
  → rich/plain/JSON parity → degrade-to-zero-ANSI → deterministic test rendering → headless-fidelity
  pitfall → version scope → provenance), clearly delineated by `# Part A` / `# Part B` headers.

**Checkpoint**: renamed canonical file exists with valid frontmatter and the full section outline.

---

## Phase 3: User Story 1 - Contributor can do real Spectre work, not just fix one bug (Priority: P1) 🎯 MVP

**Goal**: The canonical body carries the full working model — capability/profile mental model, the
widget tour, and the FS-GG conventions — so a newcomer can place a rich-output change at the
sanctioned edge and keep rich/plain/JSON parity + degrade on the first attempt.

**Independent Test**: Hand the skill to someone new to FS-GG rendering; confirm they can name the
rendering boundary, the rich/plain/degrade contract, and the deterministic-test pattern, and make a
small rich-output change using only the skill.

> All T006–T012 edit the same canonical `SKILL.md` → **sequential, not `[P]`**.

- [X] T006 [US1] Authored Part A **Capability / Profile mental model**: the `Profile` facet table
  (Ansi, ColorSystem, Width, Encoding, Unicode/Legacy) sensed on `AnsiConsole.Create`, each pinnable
  post-create, the `Create` re-detects-ANSI gotcha, and the rich-vs-plain surface relationship.
- [X] T007 [US1] Authored the Part A widget tour at working depth — markup & styles (escaping),
  tables (columns/width-wrap), panels (borders/padding/headers), rules & trees, prompts (TTY-only),
  live & status (non-interactive degrade), capability profiles (detect/force) — with the
  `https://spectreconsole.net/` link-out (S2 grep confirmed link present, no upstream restatement).
- [X] T008 [US1] Authored Part B **Rendering boundary**: Spectre confined to HumanRender; cites
  `RichRender.fsi`/`Capability.fsi`/`Tui.fsi`/`Watch.fsi` + `HumanText/`. Lead-in note frames all repo
  identifiers as labeled examples, not the required shape (FR-003).
- [X] T009 [US1] Authored Part B **Rich/plain/JSON parity**: `Json` byte-identical contract always
  wins; `Plain`/`Rich` non-contractual projections of the same `ReportView`; `selectMode` pure/total;
  sensing is an edge Effect. Cites `RenderMode.fsi` + `ReportView.fsi` (quoted the .fsi parity line).
- [X] T010 [US1] Authored Part B **Degrade-to-zero-ANSI**: the `selectMode` rule (`Rich` iff
  `IsTty && not NoColorEnv && not ExplicitPlain`, else `Plain`; `--json` ⇒ `Json`) over the
  `ColorCapability` record; `NO_COLOR`/`TERM=dumb`/redirect/`--plain` degrade. Cites `RenderMode.fsi`.
- [X] T011 [US1] Authored Part B **Deterministic test rendering**: fixed-width StringWriter-backed
  `IAnsiConsole` with every wrap-affecting facet pinned (the real `plainConsole` excerpt). Cites
  `RenderSupport.fs` + `WidthResilienceTests.fs`.
- [X] T012 [US1] Added the **version-scope** section ("verified on Spectre.Console 0.57.x — 0.57.1
  here / 0.57.0 SDD", framed version-scoped, durable conventions called out as outliving the version);
  the Provenance section ties every Part B claim to a code pointer or the cited incident.
- [X] T013 [US1] **S2/S3/S4 green.** S2: all widget headings + upstream link present. S3: every cited
  path resolves (`ls`) and every symbol cited (`grep` HumanRender/RenderMode/ReportView/RenderSupport).
  S4: `dotnet test … --filter WidthResilience` → Passed 6/6. (Structural proxies per the note; the
  hand-to-newcomer trial is the human half.)

**Checkpoint**: MVP — the canonical body alone lets a newcomer do real Spectre work in this repo.

---

## Phase 4: User Story 2 - The headless-fidelity diagnostic survives the upgrade (Priority: P2)

**Goal**: The 092 incident is absorbed as one Part B pitfall section with no loss of detail or
provenance.

**Independent Test**: Run the 092 reproduce → classify → fix scenario against the new skill; confirm
local reproduction, correct classification, matching fix, and intact provenance.

> Edits the same canonical `SKILL.md` → sequential; depends on T005 skeleton + the T002 baseline.

- [X] T014 [US2] Authored the Part B **Headless-fidelity pitfall (absorbed 092)** section preserving
  every baseline element from T002: When-it-applies symptom list, the mechanism (`cells ≠ String.Length`),
  the CI-free reproduce command, the cell-vs-byte diagnostic dump, the classification table
  (invisible-byte vs genuine overflow — opposite fixes), the test/plain-scoped fix + "don't degrade
  product output" warning, the generalize lesson, version scope, and full provenance (091; #32/#34/#37;
  runs 28376202121/28377734248; RenderSupport.fs/WidthResilienceTests.fs; 2026-06-29; corrected root
  cause). No diagnostic detail lost vs 092.
- [X] T015 [US2] **S5 green.** `GITHUB_ACTIONS=true dotnet test … --filter WidthResilience` → Passed
  6/6; plain run → Passed 6/6 (fix in place, reproduce→classify→fix narrative still lands here). `grep`
  confirms all provenance tokens `#32 #34 #37 091 28376202121 28377734248` present in the section.

**Checkpoint**: 092 diagnostic value + provenance fully preserved inside the larger skill.

---

## Phase 5: User Story 3 - First-class shape, discoverability, distribution, advisory stance (Priority: P3)

**Goal**: The skill is a peer of `cross-repo-coordination`, byte-identical across the Spectre-using
repos from one canonical source, reachable via `AGENTS.md`, leaves no orphaned reference after the
rename, and gates nothing.

**Independent Test**: Confirm peer shape + dual-trigger selection; byte-identical copies; `AGENTS.md`
references (not restates) the body; no dangling 092-name path; no workflow dependency.

- [X] T016 [US3] **S1 green.** `head -6` shows the `--- name / description ---` core matching the
  canonical `cross-repo-coordination` peer; `metadata.source` is the allowed additive field (C1.2).
  `grep` confirms BOTH the topic cue and the local-vs-CI symptom cue in `description`.
- [X] T017 [P] [US3] Re-pointed this repo's `AGENTS.md` marker to `<!-- SKILL:spectre-console
  START/END -->`; it references `.claude/skills/spectre-console/SKILL.md` and states "Advisory only —
  it gates nothing"; does not restate the body.
- [X] T018 [P] [US3] Installed into FS.GG.Governance: copied canonical → `.claude/skills/spectre-console/SKILL.md`
  (byte-identical, `diff` clean) and removed the old `spectre-console-headless-fidelity/` dir.
- [X] T019 [P] [US3] Installed into FS.GG.SDD: copied canonical (byte-identical), re-pointed its
  `AGENTS.md` marker, removed its old `spectre-console-headless-fidelity/` dir.
- [X] T020 [US3] Removed the old canonical `~/projects/.github/.claude/skills/spectre-console-headless-fidelity/`
  dir; all three old dirs confirmed gone (`ls` → No such file).
- [X] T021 [US3] **S7 sweep clean.** No dangling path/link/marker. Remaining `spectre-console-headless-fidelity`
  mentions are intentional historical prose only: 092/093 spec docs + one provenance/evolution line in
  `SKILL.md` ("evolved from the narrower `spectre-console-headless-fidelity` skill"). No marker, no
  `.claude/skills/` path, no AGENTS.md link points at the removed dir.
- [X] T022 [US3] Exclusions confirmed: FS.GG.Rendering + FS.GG.Templates carry no `spectre-console*`
  skill dir. In-scope determination holds — `Directory.Packages.local.props`: Governance Spectre.Console
  0.57.1, SDD 0.57.0; excluded repos have no Spectre.Console package. (Recorded in data-model E6.)
- [X] T023 [US3] **S8 green.** `grep -rni spectre-console` over `.github/workflows/` in all three repos
  → no match. The skill gates nothing.
- [X] T024 [US3] **S6 green.** `diff` canonical vs Governance and vs SDD `SKILL.md` → no diff
  (byte-identical); each `AGENTS.md` marker references the `spectre-console/SKILL.md` path (single
  source, body in exactly one place).

**Checkpoint**: peer-shaped, single-source, byte-identical, advisory, no orphan.

---

## Phase 6: Polish & Evidence Obligations

- [X] T025 Ran full quickstart **S1–S8** end-to-end (evidence on each task line above): S1 peer shape +
  dual trigger ✓; S2 widget coverage + link-out ✓; S3 cited paths resolve + symbols cited ✓; S4
  WidthResilience 6/6 ✓; S5 headless+plain 6/6 + provenance tokens ✓; S6 byte-identical + AGENTS
  reference ✓; S7 no orphan ✓; S8 no workflow dependency ✓. **Principle IV (Elmish/MVU) is N/A** —
  this feature authored no stateful/I/O workflow, it only *describes* the existing `RenderMode`/
  `HumanRender` edge. **Principle V** satisfied: every claim traces to a live code pointer
  (`RenderMode.fsi`/`ReportView.fsi`/`RichRender.fsi`/`Capability.fsi`/`RenderSupport.fs`), a
  reproducible command, or the already-verified 092 incident — no synthetic evidence.
- [X] T026 [P] Final peer-read pass: Part A stops at working depth and links out to
  `https://spectreconsole.net/` (no upstream-doc bloat); every Part B claim is checkable against the
  cited source (quoted .fsi/`RenderSupport.fs` excerpts, not memory); the document carries the same
  `--- name/description ---` core + sectioned body shape as `cross-repo-coordination`, reading as a
  first-class peer.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: no dependencies; T001–T003 all `[P]`.
- **Phase 2 (Foundational)**: after Setup; T004 → T005 (T005 needs the file). **Blocks all
  authoring.**
- **Phase 3 (US1)**: after Foundational. T006–T012 **sequential** (one file), then T013 validates.
- **Phase 4 (US2)**: after the T005 skeleton; independent of US1 content but **sequential on the same
  file**, so author after US1 to avoid edit conflicts. T014 → T015.
- **Phase 5 (US3)**: distribution tasks (T018/T019) require the body complete (after T015 + T016);
  T020 after T018/T019; T021/T024 after copies exist. T017 (`AGENTS.md`) is a different file → `[P]`.
- **Phase 6 (Polish)**: after all desired stories complete.

### Parallel Opportunities

- Setup T001, T002, T003 (distinct read-only captures).
- Phase 5: T017 (Governance `AGENTS.md`), T018 (Governance skill copy), T019 (SDD repo) touch
  distinct files/repos → `[P]` once the canonical body is final.
- **No `[P]` among T005–T014** — they all edit the single canonical `SKILL.md`.

---

## Summary

| Phase / Story | Tasks | Count |
|---|---|---|
| Phase 1 — Setup | T001–T003 | 3 |
| Phase 2 — Foundational | T004–T005 | 2 |
| Phase 3 — **US1 (P1, MVP)** | T006–T013 | 8 |
| Phase 4 — US2 (P2) | T014–T015 | 2 |
| Phase 5 — US3 (P3) | T016–T024 | 9 |
| Phase 6 — Polish | T025–T026 | 2 |
| **Total** | | **26** |

**Suggested MVP**: Phases 1–3 (US1) — the canonical body alone delivers the core "do real Spectre
work" value from a single repo. US2 preserves the 092 diagnostic; US3 adds shape, distribution, and
the advisory/no-orphan guarantees.

**Parallel opportunities**: 3 in Setup; 3 across the Phase 5 distribution targets (distinct
repos/files). Body authoring is intentionally serial (single-source file).
