# Feature Specification: First-class Spectre.Console skill (general knowledge + docs)

**Feature Branch**: `093-spectre-console-reference`

**Created**: 2026-06-29

**Status**: Draft

**Input**: User description: "make the spectre.console skill a first class skill with general knowledge/docs... like all the others."

## Context & Relationship to 092

Feature 092 shipped `spectre-console-headless-fidelity` — a **narrow incident-capture** skill that
teaches one thing: diagnose and fix a Spectre render that is correct locally but red in CI. That is
the shape of a single troubleshooting note, not the shape of this repo's other skills.

The repo's established skills (`cross-repo-coordination`, the `speckit-*` family) are **first-class
knowledge artifacts**: they carry the *whole* working model of their topic — the mental model, the
conventions used in *this* project, the runbook, and the pitfalls — so a contributor or agent can
*do the work*, not just survive one bug. This feature elevates the Spectre skill to that bar:
comprehensive, project-grounded Spectre.Console knowledge, with the 092 headless-fidelity incident
demoted from "the whole skill" to "one pitfall section within the larger skill."

This is an **evolution of the existing skill**, not a second parallel skill. The 092 content is
preserved (no regression in diagnostic value) and absorbed into the larger document.

**Resolved direction (Session 2026-06-29):** rename the existing skill to a general `spectre-console`
skill (one skill), and carry **both** a generic Spectre.Console primer (Part A) and the FS-GG
rendering conventions (Part B), with the headless-fidelity incident as a pitfalls section in Part B.

## Clarifications

### Session 2026-06-29

- Q: Should this *evolve the existing* `spectre-console-headless-fidelity` skill into a general
  skill, or add a *second* general skill alongside it? → **A: Evolve into one skill.**
  Rename/restructure the existing `spectre-console-headless-fidelity` skill into a general
  `spectre-console` skill, with the headless-fidelity incident absorbed as one "pitfalls" section.
  One skill, not two; the prior 092 name/path and any references to it MUST keep resolving to the
  new skill (no orphaned link).
- Q: How broad/grounded should the "general knowledge" be? → **A: Both — generic primer +
  project conventions.** The body carries (Part A) a generic Spectre.Console primer — the widget/API
  tour (markup, tables, panels, rules/trees, prompts, live/status, capability profiles) at the depth
  a contributor needs — AND (Part B) the FS-GG project conventions (Spectre confined to the
  HumanRender edge, the rich/plain/JSON projection parity, the degrade-to-zero-ANSI rule,
  deterministic test rendering, and the absorbed headless-fidelity pitfall). Exhaustive upstream API
  detail is linked out, not restated, to avoid becoming a copy of upstream docs.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A contributor can do real Spectre work from the skill, not just fix one bug (Priority: P1)

A contributor (or agent) is asked to add or change rich console output in an FS-GG repo — a new
panel, a table column, a degrade path, a `--rich` projection. They open the skill and get the
working model they need: how Spectre decides capabilities (ANSI / color system / width / encoding /
unicode), the difference between the rich and plain/JSON surfaces, where Spectre is allowed to live
in this codebase, how rich output must degrade when non-interactive, and how to render
deterministically in tests. They implement the change correctly the first time without reverse-
engineering the conventions from scattered source files.

**Why this priority**: This is the gap that makes the current skill *not* first-class. A single
incident note cannot guide ordinary Spectre work; a comprehensive skill can. This is the core value
of the upgrade — without it, the feature is pointless.

**Independent Test**: Hand the skill to someone who has never touched this repo's rendering code and
give them a small rich-output change (e.g. add a section to a report panel). Confirm they can name
the rendering boundary, the rich/plain/degrade contract, and the deterministic-test pattern, and
make the change, using only the skill.

**Acceptance Scenarios**:

1. **Given** a contributor new to FS-GG rendering, **When** they read the skill's mental-model
   section, **Then** they can correctly explain how Spectre derives capabilities (ANSI, color
   system, width, encoding, unicode) and what forces rich output to degrade to plain.
2. **Given** a task to add or modify rich console output, **When** they follow the skill's
   project-conventions section, **Then** they place the Spectre code in the sanctioned layer and
   keep the rich surface a pure projection that adds/drops no facts versus the plain/JSON surfaces.
3. **Given** a need to test the new rendering, **When** they follow the skill's deterministic-
   rendering guidance, **Then** they produce a width-/host-independent test without inventing the
   pattern themselves.

---

### User Story 2 - The headless-fidelity diagnostic survives the upgrade (Priority: P2)

A contributor hits the exact 092 symptom (render correct locally, red in CI). They reach the same
reproduce → classify (invisible-byte artifact vs genuine overflow) → fix knowledge that 092
provided — now as one pitfalls section inside the larger skill — with no loss of detail or
provenance.

**Why this priority**: The upgrade must not regress the capability 092 already delivered. The
incident knowledge is hard-won and must remain reachable and accurate; it is P2 because it is
preserved-existing-value rather than the new value the feature adds (P1).

**Independent Test**: Run the 092 quickstart's diagnose/classify/fix scenario against the new skill
and confirm the reader still reproduces locally, classifies correctly, and applies the matching fix
— with the same durable provenance (spec 091, #32/#34/#37) intact.

**Acceptance Scenarios**:

1. **Given** the headless-fidelity symptom, **When** a contributor consults the new skill, **Then**
   they reach the local-reproduction, classification, and fix guidance equivalent to 092's, with no
   diagnostic detail lost.
2. **Given** the absorbed incident section, **When** a reviewer checks its claims, **Then** every
   factual assertion still traces to the cited incident provenance and a reproducible step.

---

### User Story 3 - First-class shape, discoverability, distribution, and advisory stance (Priority: P3)

The skill is consistent with the repo's other first-class skills: the same `SKILL.md`
frontmatter+body shape; a `description` that selects it both by topic ("working with
Spectre.Console output") and by the original symptom ("renders correctly locally but differs in
CI"); a single canonical source distributed to the Spectre-using repos with identical content; a
cross-agent `AGENTS.md` entrypoint that references (never copies) the body; and no gate — consulting
it is never a precondition for work.

**Why this priority**: Consistency and reach matter, but the skill delivers its core value (P1/P2)
from a single repo even before perfect trigger tuning and full distribution. P3 because it is fit-
and-finish over the knowledge itself.

**Independent Test**: Confirm the skill matches the frontmatter/body shape of `cross-repo-
coordination`; that a topic phrase *and* the CI-symptom phrase both plausibly select it; that the
canonical copy is byte-identical across in-scope repos; that `AGENTS.md` references the body without
restating it; and that no workflow depends on the skill.

**Acceptance Scenarios**:

1. **Given** the repo's existing skills, **When** the new skill is compared to them, **Then** it has
   the same shape (frontmatter `name`+`description`, plain-Markdown single-source body) and reads as
   a peer, not an outlier.
2. **Given** either a topic query ("how do we render with Spectre here") or the CI-symptom query,
   **When** an agent selects a skill, **Then** the description plausibly selects this skill over
   generic guidance for both.
3. **Given** a Codex/general agent in an in-scope repo, **When** it reads `AGENTS.md`, **Then** it
   reaches the same single-source body a Claude agent gets from `SKILL.md`, with no duplicated copy.
4. **Given** any build/test/publish/merge workflow, **When** the skill is absent or unread, **Then**
   the workflow still completes — the skill gates nothing.

---

### Edge Cases

- **Regression of 092 value**: broadening the document must not dilute or drop the headless-fidelity
  diagnostic. The pitfalls section MUST retain the reproduce/classify/fix detail and its provenance.
- **Generic-tutorial bloat**: a first-class skill must stay useful and grounded, not balloon into a
  copy of upstream Spectre docs. General library detail is included only where it is needed to work
  in *this* codebase; the skill links out for exhaustive API coverage rather than restating it.
- **Drift from the real code**: project-convention claims (the rendering boundary, the
  rich/plain/JSON projection contract, the degrade rule) MUST match the codebase as it is, and be
  framed so they remain checkable against the source, not asserted from memory.
- **Version drift**: behavior tied to a Spectre.Console version (e.g. the ANSI re-detection that
  caused the incident) MUST stay version-scoped and labeled, distinct from durable conventions that
  do not depend on the patch version.
- **Identity/links after rename**: if the skill is renamed, the prior name/path (092's
  `spectre-console-headless-fidelity`) and any references to it MUST resolve to the new skill so no
  consumer is orphaned.
- **Entrypoint drift**: the `AGENTS.md` entrypoint MUST reference the one body, never restate it, so
  the two cannot diverge.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The skill MUST be a **first-class knowledge artifact** in the same form and depth as
  the repo's existing non-`speckit` skill (`cross-repo-coordination`): a `SKILL.md` with YAML
  frontmatter (`name`, a `description` that selects on both topic and symptom) and a plain-Markdown
  single-source body that carries the full working model of the topic — not a single troubleshooting
  note.
- **FR-002**: The skill MUST teach the **Spectre.Console mental model** a contributor needs to work
  in this codebase: how capabilities are derived and can be pinned (ANSI support, color system,
  width, output encoding, unicode/legacy), and the relationship between the rich surface and the
  plain/no-color surface.
- **FR-003**: The skill MUST document **how FS-GG actually renders** with Spectre as project
  conventions: where Spectre code is allowed to live (the presentation edge), that rich output is a
  pure projection that adds and drops no facts versus the plain/JSON projections, and the rule that
  rich output degrades to zero-ANSI plain text when non-interactive/redirected or color-disabled
  (`NO_COLOR`, `TERM=dumb`). Repo-specific identifiers MUST be presented as examples, not as the
  skill's required shape.
- **FR-004**: The skill MUST give a **deterministic-rendering pattern for tests**: how to render to
  a fixed-width, host-independent console so layout is a pure function of (content, width), suitable
  for assertions and snapshots.
- **FR-005**: The skill MUST **absorb the 092 headless-fidelity incident** as a pitfalls section
  covering local reproduction (no CI round-trip), classification of invisible-byte artifact vs
  genuine display overflow, and the matching fix scoped to the test/plain surface — with no loss of
  diagnostic detail relative to 092.
- **FR-006**: The skill MUST carry **both** (Part A) a **generic Spectre.Console primer** — the
  widget/API tour at working depth: markup/styles, tables, panels, rules/trees, prompts, and
  live/status surfaces, plus capability profiles — **and** (Part B) the **FS-GG project conventions**
  (FR-003). It MUST link out to upstream documentation for exhaustive API detail rather than
  restating it, so it stays a grounded skill rather than a copy of upstream docs.
- **FR-007**: Every factual claim, command, convention, and behavior MUST be backed by a
  reproducible step, a pointer to the real code it describes, or a cited incident — no unverified
  assertions. Version-dependent behavior MUST be labeled with the verified Spectre.Console version
  and framed as version-scoped.
- **FR-008**: The skill MUST be distributable from a single canonical source (`FS-GG/.github`) into
  the Spectre-using repos (FS.GG.Governance, FS.GG.SDD) with identical content, and MUST record the
  intentionally excluded repos (FS.GG.Rendering, FS.GG.Templates — no Spectre rendering).
- **FR-009**: The skill's content MUST be reachable by Codex/general agents via the cross-agent
  `AGENTS.md` convention, **referencing the `SKILL.md` body as the single source** rather than
  duplicating it; an agent reading neither format MUST still get usable content from the
  plain-Markdown body.
- **FR-010**: The skill MUST remain **advisory** — consulting it is never a precondition for
  completing or merging work, and no build/test/publish/merge step may depend on it.
- **FR-011**: The skill's `description` MUST select the skill from **both** a topic phrasing
  ("working with / rendering Spectre.Console output in this project") **and** the original
  CI-fidelity symptom ("renders correctly locally but differs/fails in CI"), so the upgrade does not
  lose 092's symptom-triggered discoverability.
- **FR-012**: The skill MUST be **renamed** from `spectre-console-headless-fidelity` to a general
  `spectre-console` skill, leaving **no orphaned reference**: the prior name/path and any links to it
  (including each repo's `AGENTS.md` entrypoint and the 092 spec/skill artifacts) MUST resolve to the
  renamed skill, and the rename MUST be propagated to every in-scope repo and the canonical source.

### Key Entities

- **Skill document (`SKILL.md`)**: the single source, named `spectre-console` (renamed from
  `spectre-console-headless-fidelity`) — frontmatter (`name`, dual topic+symptom `description`) plus
  a plain-Markdown body covering: **Part A** a generic Spectre primer (mental model + widget/API
  tour) and **Part B** the FS-GG conventions (rendering boundary, rich/plain/JSON parity, degrade
  rule, deterministic-test rendering, the absorbed headless-fidelity pitfall), plus version scope and
  provenance.
- **Absorbed incident section**: the 092 headless-fidelity diagnostic (reproduce/classify/fix +
  provenance), now one section of the larger skill rather than the whole skill.
- **Per-agent entrypoints**: Claude `SKILL.md` (frontmatter auto-discovery) and the `AGENTS.md`
  reference for Codex/general agents — both pointing at the one body, never duplicating it.
- **Distribution set**: canonical source (`FS-GG/.github`) + in-scope Spectre-using repos
  (FS.GG.Governance, FS.GG.SDD) + recorded exclusions (FS.GG.Rendering, FS.GG.Templates).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A contributor with no prior FS-GG rendering experience, using only the skill, can make
  a small rich-output change correctly — placing the code at the sanctioned edge and preserving the
  rich/plain/JSON parity and degrade contract — on their first attempt.
- **SC-002**: Using only the skill, a contributor can produce a deterministic, host-independent
  render test (fixed width, no host dependence) without inventing the pattern.
- **SC-003**: The headless-fidelity diagnostic is fully preserved: running the 092 reproduce →
  classify → fix scenario against the new skill yields the same correct outcome, with provenance
  intact.
- **SC-004**: The skill reads as a peer of the repo's other first-class skills — same shape and
  comparable depth — and is selected by *both* a topic query and the CI-symptom query over generic
  guidance.
- **SC-005**: The skill is available in 100% of the Spectre-using FS-GG repos from one canonical
  source, byte-identical across copies, with exclusions recorded.
- **SC-006**: 100% of the skill's factual claims/commands trace to a reproducible step, a code
  pointer, or a cited incident; a reviewer can verify each, and every version-dependent claim is
  labeled with its verified version.
- **SC-007**: No repository build, test, publish, or merge step depends on the skill; removing or not
  reading it changes no workflow outcome.
- **SC-008**: Both a Claude agent (via `SKILL.md`) and a Codex/general agent (via `AGENTS.md`) reach
  the same single-source body from one in-scope checkout, with no duplicated or divergent copy, and
  no reference to the prior 092 skill name is left dangling.

## Assumptions

- **Artifact type**: "Skill" means a `SKILL.md`-shaped knowledge artifact like the existing
  `.claude/skills/` entries (notably `cross-repo-coordination`), plus the cross-agent `AGENTS.md`
  entrypoint — not an F# code change. No public F# surface, `.fsi`, or MVU boundary is in scope.
- **Change classification**: Tier 2 (documentation / knowledge artifact). No public API, package, or
  inter-project code contract changes; the only cross-repo aspect is copying the same document into
  sibling repos and updating their `AGENTS.md`.
- **Skill identity (resolved)**: evolve the existing 092 skill into **one** general skill named
  `spectre-console` (rename from `spectre-console-headless-fidelity`, absorb the incident as a
  pitfalls section); the prior name/path keeps resolving to the renamed skill — one skill, not two.
- **Knowledge scope (resolved)**: carry **both** a generic Spectre.Console primer (Part A) and the
  FS-GG project conventions (Part B), linking out for exhaustive upstream API detail rather than
  restating it.
- **Canonical source & distribution**: canonical copy in `FS-GG/.github`, installed into the
  Spectre-using product repos by copying, with the `AGENTS.md` entrypoint carried alongside —
  mirroring 092 and `cross-repo-coordination`.
- **Distribution scope**: in scope = repos whose central package config references Spectre.Console
  (currently FS.GG.Governance @ 0.57.1, FS.GG.SDD @ 0.57.0); excluded = FS.GG.Rendering,
  FS.GG.Templates (no Spectre rendering).
- **Verified version**: version-dependent behavior is written as verified against Spectre.Console
  0.57.x (0.57.1 here / 0.57.0 sibling), framed as version-scoped, not a permanent guarantee.
- **Source of truth for conventions**: the FS-GG rendering conventions stated in the skill are
  derived from, and checkable against, the actual repo code (the HumanRender presentation edge and
  the rich/plain/JSON projection), not asserted from memory.
- **Advisory stance**: per the constitution's Local Skills section, the skill is an advisory aid;
  this feature introduces no gate, hook, or mandatory skill-loading step.
- **Authoring vs. enforcement**: this feature delivers the skill artifact and its distribution; it
  changes no product code, test, or CI workflow in the consuming repos.
