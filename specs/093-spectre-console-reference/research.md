# Phase 0 Research: First-class Spectre.Console skill

All Technical Context unknowns are resolved below. The spec carried no open `NEEDS CLARIFICATION`
markers (both clarification questions were answered in Session 2026-06-29); this phase records the
decisions, their rationale, and the alternatives rejected.

## D1 — Evolve one skill (rename) vs. add a second parallel skill

- **Decision**: Rename the existing `spectre-console-headless-fidelity` skill to a general
  `spectre-console` skill (one skill). Absorb the 092 incident as one "pitfalls" section.
- **Rationale**: Resolved in spec clarification (Session 2026-06-29, Q1). Two skills covering the
  same library would split discoverability and invite drift; the repo's first-class skills are
  single whole-topic artifacts. One skill matches `cross-repo-coordination`'s shape.
- **Alternatives rejected**: A second `spectre-console` skill beside the 092 one (rejected: parallel
  skills, divergent triggers, orphaned 092 name). Leaving 092 as-is (rejected: it is not first-class
  — the whole point of the feature).

## D2 — Knowledge scope: generic primer + project conventions (Part A + Part B)

- **Decision**: Carry **both** Part A (a generic Spectre.Console primer at working depth — markup/
  styles, tables, panels, rules/trees, prompts, live/status, capability profiles) and Part B (the
  FS-GG conventions). Link out to upstream docs for exhaustive API detail rather than restating it.
- **Rationale**: Resolved in spec clarification (Q2). Part A makes the skill usable for ordinary
  Spectre work (P1, SC-001); Part B grounds it in this codebase. Linking out keeps it a grounded
  skill, not a copy of upstream docs (edge case "generic-tutorial bloat", FR-006).
- **Alternatives rejected**: Project-conventions only (rejected: not "first-class general
  knowledge"; a newcomer still can't learn the widget model). Full upstream-API restatement
  (rejected: bloat, drift against upstream, low marginal value over a link).

## D3 — Absorbing the 092 incident without regression

- **Decision**: Fold the entire 092 body — Symptom → The problem → Reproduce locally → Diagnose
  (cell-vs-byte) → Fix → Generalize → Version scope → Provenance — into Part B as a single
  "headless-fidelity pitfall" section, preserving every diagnostic step, the reproduce command, the
  cell-vs-byte classification table, the opposite-fix warning, and the full provenance (spec 091,
  #32 / #34 / #37, evidence runs `28376202121` / `28377734248`, the live `RenderSupport.fs` /
  `WidthResilienceTests.fs` pointers).
- **Rationale**: P2 / FR-005 / SC-003 demand no loss of detail or provenance. The 092 content is
  already verified; absorbing it verbatim-in-substance is the lowest-risk way to preserve value.
- **Alternatives rejected**: Summarizing/condensing the incident (rejected: regresses hard-won
  detail, breaks SC-003). Linking to the old 092 SKILL.md (rejected: that file is removed by the
  rename — would orphan the link).

## D4 — Grounding Part B in real code (anti-drift)

- **Decision**: Derive every Part B convention from, and cite, the live code:
  - Rendering boundary → `src/FS.GG.Governance.HumanRender/` (`Capability.fsi`, `RichRender.fsi`,
    `Tui.fsi`, `Watch.fsi`) and `src/FS.GG.Governance.HumanText/` (`RenderMode.fsi`,
    `ReportView.fsi`, `HumanText.fsi`).
  - Rich/plain/JSON parity rule → `RenderMode.fsi`: `Json | Plain | Rich`; `selectMode` pure/total;
    `Json` is the byte-identical automation contract and always wins; `Plain`/`Rich` are
    non-contractual human projections of the **same** `ReportView`; capability sensing
    (`ColorCapability`: `IsTty`, `NoColorEnv`, `ExplicitPlain`, `Width`) is an Effect at the
    interpreter edge, never inside the pure function.
  - Degrade-to-zero-ANSI rule → `selectMode` returns `Rich` iff `IsTty && not NoColorEnv && not
    ExplicitPlain`, else `Plain`; honoring `NO_COLOR` / `TERM=dumb` / redirection.
  - Deterministic test render → `tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs` (fixed-width
    `StringWriter`-backed `IAnsiConsole`, pinned capabilities) and `WidthResilienceTests.fs`.
- **Rationale**: FR-007 / FR-003 / SC-006 + edge case "drift from the real code": claims must be
  checkable against source, not memory. Citing `.fsi` files (the curated public contract) keeps the
  skill durable across `.fs` refactors.
- **Alternatives rejected**: Describing conventions from memory or from the 092 prose alone
  (rejected: unverifiable, drifts). Embedding large code excerpts (rejected: brittle copies that rot
  — cite the path instead, FR-006).

## D5 — Dual-trigger `description` (topic + symptom)

- **Decision**: The frontmatter `description` must select the skill from **both** a topic phrasing
  ("working with / rendering Spectre.Console output in this project") **and** the original 092
  CI-fidelity symptom ("renders correctly locally but differs/fails in CI"). Keep the 092 symptom
  cues (width/wrap, plain/no-color, CI-only snapshot diffs).
- **Rationale**: FR-011 / SC-004. Broadening the skill must not lose 092's symptom-triggered
  discoverability; both query shapes must beat generic guidance.
- **Alternatives rejected**: Topic-only description (rejected: loses 092 symptom trigger).
  Symptom-only (rejected: a topic query for ordinary Spectre work wouldn't select it).

## D6 — Rename mechanics with no orphaned reference

- **Decision**: Move `.claude/skills/spectre-console-headless-fidelity/SKILL.md` →
  `.claude/skills/spectre-console/SKILL.md` (update `name:` frontmatter to `spectre-console`) in the
  canonical `FS-GG/.github` source and both in-scope repos; re-point each repo's `AGENTS.md` managed
  marker (`<!-- SKILL:spectre-console-headless-fidelity … -->` → `<!-- SKILL:spectre-console … -->`
  with the new path); and update any 092 spec/skill links that name the old skill. Verify with a
  repo-wide grep that no `spectre-console-headless-fidelity` reference resolves to nothing.
- **Rationale**: FR-012 + edge cases "identity/links after rename" and "entrypoint drift". Known
  current references to update (grep evidence): `AGENTS.md` and `.claude/skills/.../SKILL.md` in
  FS.GG.Governance, FS.GG.SDD, and `FS-GG/.github`.
- **Alternatives rejected**: Keeping the old dir as a redirect stub (rejected: two artifacts, drift
  risk; the clarification says one skill). Leaving 092 spec references pointing at the old name
  (rejected: dangling reference, fails SC-008).

## D7 — Distribution & AGENTS.md single-source mechanism (unchanged from 092)

- **Decision**: Canonical copy in `FS-GG/.github`; copied byte-identically into the Spectre-using
  repos (FS.GG.Governance @ 0.57.1, FS.GG.SDD @ 0.57.0); FS.GG.Rendering and FS.GG.Templates recorded
  as excluded (no Spectre rendering). `AGENTS.md` carries a managed marker that **references** the
  `SKILL.md` body — never restates it.
- **Rationale**: FR-008 / FR-009 / SC-005 / SC-008. Mirrors the proven 092 and
  `cross-repo-coordination` distribution shape; no new tooling needed (manual copy, as today).
- **Alternatives rejected**: A sync script/generator (rejected: Principle III simplicity; 092 and
  cross-repo-coordination are distributed by copy, and no such tool exists in `FS-GG/.github`).
  Duplicating the body into `AGENTS.md` (rejected: divergence, fails the single-source invariant).

## D8 — Advisory stance preserved

- **Decision**: The skill gates nothing — no hook, no mandatory load step; no build/test/publish/
  merge workflow references it.
- **Rationale**: FR-010 / SC-007 and the constitution's Local Skills section (skills are advisory
  aids, not gates). Verified by grepping CI workflows for the skill name (expect zero dependency).
- **Alternatives rejected**: Any gate or required-consultation step (rejected: violates the
  constitution and FR-010).

## Cross-repo coordination note

This is a Tier 2 doc artifact whose only cross-repo aspect is copying the same renamed document into
FS.GG.SDD and updating its `AGENTS.md`. No versioned cross-repo contract changes, so no registry/ADR
entry is required; the rename is propagated as part of implementation per D6/D7.
