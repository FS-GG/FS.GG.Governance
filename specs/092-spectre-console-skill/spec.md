# Feature Specification: Spectre.Console headless-fidelity skill

**Feature Branch**: `092-spectre-console-skill`

**Created**: 2026-06-29

**Status**: Draft

**Input**: User description: "create a spectre.console skill that adheres to the quality standards of this project and also includes the information about the encountered test/headless fidelity problem."

## Clarifications

### Session 2026-06-29

- Q: Which agents must the skill support, and in what artifact format / distribution? → A: Use the **same structure and distribution as the repo's existing skills** — a `.claude/skills/<name>/SKILL.md` (YAML frontmatter `name` + symptom-oriented `description`; a plain-Markdown body holding all the content), canonical in `FS-GG/.github` and copied into the Spectre-using repos, exactly as `cross-repo-coordination` is. **Additionally make it Codex- and general-agent-compatible** by surfacing the *same* content through the cross-agent `AGENTS.md` convention — the convention Codex reads and the one the repo's bundled agent-context tooling already recognizes alongside `CLAUDE.md` / `.github/copilot-instructions.md` / `GEMINI.md`. The SKILL.md Markdown body is the single source of content; the `AGENTS.md` entrypoint references/surfaces it so no substance is duplicated.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Diagnose a Spectre render test that passes locally but fails in CI (Priority: P1)

A contributor has a Spectre.Console rendering test (width/wrap assertion, plain/no-color
output, or a snapshot) that is green on their machine but red — or subtly different — on the
GitHub Actions runner. They recognize the symptom, open the skill, and follow its recipe to
reproduce the divergence on their own machine (no CI round-trip), identify the true cause, and
apply the matching fix.

**Why this priority**: This is the core value and the reason the skill exists — it captures a
real incident (FS.GG.Governance spec 091 / #32 / #37) that cost two premature "fixes" because the
divergence could not be reproduced or explained locally. Without P1, the artifact has no point.

**Independent Test**: Hand the skill to someone unfamiliar with the incident, give them only a
Spectre render test that fails on the runner, and confirm they can (a) reproduce it locally and
(b) name the root cause using nothing but the skill's instructions.

**Acceptance Scenarios**:

1. **Given** a Spectre render test that passes locally but fails in headless CI, **When** the
   contributor follows the skill's local-reproduction step, **Then** they reproduce the same
   failure on their own machine without pushing to CI.
2. **Given** the reproduced failure, **When** the contributor runs the skill's diagnostic recipe,
   **Then** they can distinguish a genuine display-width overflow from an invisible control-byte
   artifact (the two have opposite fixes) and identify which they have.
3. **Given** an identified root cause, **When** the contributor applies the skill's matching fix
   pattern, **Then** the test passes in both the local and the headless environment.

---

### User Story 2 - Reach every FS-GG repository that renders with Spectre (Priority: P2)

A maintainer wants this knowledge available wherever it is relevant — not trapped in one
repository — because more than one FS-GG repo renders console output with Spectre. They install
the skill into each Spectre-using repo from a single canonical source, the way the cross-repo
coordination skill is distributed.

**Why this priority**: The encountered problem is environmental and library-specific, so it will
recur in any FS-GG repo that uses Spectre. A repo-local note (or auto-memory) would not reach the
sibling repos; portability is what makes the capture worth doing. Secondary to P1 because the
content must exist and be correct before it is worth distributing.

**Independent Test**: From the canonical copy, install the skill into a second Spectre-using repo
and confirm it is present and invocable there with identical content.

**Acceptance Scenarios**:

1. **Given** the canonical skill, **When** a maintainer installs it into a Spectre-using FS-GG
   repo, **Then** the skill is available in that repo with content identical to the canonical copy.
2. **Given** a repo that does not render with Spectre, **When** distribution scope is decided,
   **Then** that repo is intentionally excluded (no irrelevant install) and the exclusion is
   recorded.

---

### User Story 3 - Surface only when relevant, and never block work (Priority: P3)

A contributor relies on the skill being discoverable by symptom (it appears when they are dealing
with Spectre output that behaves differently in CI) and on it being an advisory aid — consulting
it is never a precondition for completing or merging work.

**Why this priority**: Consistency with this repository's "Local Skills are advisory, not gates"
stance. Important for fit, but the skill delivers its value (P1) even without perfect trigger
tuning.

**Independent Test**: Read the skill's trigger description against the incident's symptom
("renders correctly locally, differently in CI") and confirm a match; confirm no build, test, or
merge step requires the skill.

**Acceptance Scenarios**:

1. **Given** the skill's trigger description, **When** a contributor describes the symptom in
   their own words, **Then** the description plausibly selects this skill over generic guidance.
2. **Given** any repository workflow (build, test, publish, merge), **When** the skill is absent
   or unread, **Then** the workflow still completes — the skill gates nothing.
3. **Given** a Codex (or other general) agent working in an in-scope repo, **When** it consults the
   repo's `AGENTS.md`, **Then** it reaches the same skill content a Claude agent gets from the
   `SKILL.md`.

---

### Edge Cases

- **Genuine overflow vs. invisible-byte artifact**: the same red assertion can mean either a real
  display-width overflow (a layout bug a user would see) or invisible control/escape bytes
  inflating a length count (an environment artifact). The skill MUST give the reader a way to tell
  them apart, because the fixes are opposite (fix the layout vs. fix the measurement/suppression).
- **Library version drift**: the captured behavior was observed on one Spectre.Console minor
  version; sibling repos may pin a different patch/minor. The skill MUST state the version it was
  verified against and frame the behavior as version-scoped, not eternal.
- **CI providers other than the one in the incident**: the trigger was a specific CI environment
  variable; other runners use different signals (and the `NO_COLOR` convention). The skill MUST
  generalize beyond the single environment variable it was first observed under.
- **Over-correction**: a naive fix could strip color/formatting from real product output, not just
  the deterministic test surface. The skill MUST scope its fix to the test/plain surface and warn
  against degrading intentional human-facing output.
- **Stale provenance**: linked incident issues/specs may be renamed or closed. The skill MUST cite
  durable identifiers and date its claims so a reader can judge currency.
- **Entrypoint drift**: the Claude `SKILL.md` and the `AGENTS.md` entrypoint must not diverge — the
  `AGENTS.md` references the one Skill-document body rather than restating it, so there is no second
  copy to fall out of sync.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The skill MUST be authored in the **same form as the repo's existing skills** — a
  `SKILL.md` with YAML frontmatter (`name`, a symptom-oriented `description`) and a plain-Markdown
  body that holds all the content — consistent with the `speckit-*` skills and the
  cross-repo-coordination skill's shape. The Markdown body is the single source of the skill's
  content.
- **FR-002**: The skill MUST describe the encountered test/headless-fidelity problem in plain
  terms: a Spectre-rendered surface that is correct locally but diverges in headless CI, why the
  divergence happens, and why a naive length-based assertion misjudges it.
- **FR-003**: The skill MUST provide a **local reproduction** step that recreates the headless
  divergence on a developer machine without a CI round-trip.
- **FR-004**: The skill MUST provide a **diagnostic recipe** that lets the reader distinguish a
  genuine display-width overflow from an invisible control/escape-byte artifact, and attribute the
  failure to a specific cause.
- **FR-005**: The skill MUST provide the **fix pattern(s)** for the captured class of problem,
  scoped to the deterministic test/plain surface, with an explicit warning against degrading
  intentional product output.
- **FR-006**: Every factual claim, command, and behavior in the skill MUST be backed by a
  reproducible step or a cited real incident — no unverified assertions (the repository's
  real-evidence standard). The skill MUST record the incident provenance with durable identifiers.
- **FR-007**: The skill MUST be distributable to multiple repositories from one canonical source,
  following the established FS-GG cross-repo skill convention (canonical copy in `FS-GG/.github`,
  installed into product repos by copying — as cross-repo-coordination is), and MUST name the
  repositories in scope (those that render with Spectre) and note which are intentionally excluded.
- **FR-013**: The skill MUST be usable by Codex and other general coding agents, not only Claude.
  Its content MUST be surfaced through the cross-agent `AGENTS.md` convention (the entrypoint Codex
  and most agents read, and the one the repo's agent-context tooling already manages alongside
  `CLAUDE.md` / `.github/copilot-instructions.md` / `GEMINI.md`), **referencing the SKILL.md body
  as the single source rather than duplicating it**. An agent that reads neither a Claude skill nor
  `AGENTS.md` MUST still be able to use the content as a plain-Markdown document.
- **FR-008**: The skill MUST state the library version it was verified against and frame the
  behavior as version-scoped, with guidance to re-verify on a different version.
- **FR-009**: The skill MUST generalize beyond the single CI signal observed in the incident
  (covering the common CI-detection signals and the `NO_COLOR` convention) rather than hard-coding
  one environment variable as the only trigger.
- **FR-010**: The skill MUST remain advisory — consulting it is never a precondition for
  completing or merging work, and no build/test/publish step may depend on it (consistent with the
  repository's Local Skills policy).
- **FR-011**: The skill's guidance MUST NOT assume one repository's package IDs, project layout, or
  target names where the advice is general; repository-specific details MUST be presented as
  examples, not as the skill's required shape (the cross-repo operating rule / genericity).
- **FR-012**: The skill SHOULD record the durable, transferable lesson beyond the single bug — that
  a test surface can diverge from the real/CI surface in fidelity, and that assertions should be
  made against the same measure the system actually uses — so the skill aids future, non-identical
  fidelity problems.

### Key Entities

- **Skill document (`SKILL.md`)**: the single source of the knowledge — frontmatter (`name`,
  symptom-oriented `description`) plus a plain-Markdown body covering identity, problem description,
  reproduction step, diagnostic recipe, fix pattern(s), version scope, and provenance.
- **Per-agent entrypoints**: how each agent discovers the same content — the `.claude/skills/`
  `SKILL.md` for Claude (frontmatter auto-discovery) and an `AGENTS.md` reference for Codex/general
  agents — all pointing at the one Skill-document body, never duplicating it.
- **Incident provenance**: the real, verified events the skill is built from (the headless render
  determinism work and its corrected root cause), referenced by durable identifiers and dated.
- **Distribution set**: the canonical source location (`FS-GG/.github`) plus the target
  repositories that render with Spectre (in scope) and those that do not (excluded), with the
  exclusion recorded.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A contributor unfamiliar with the incident, using only the skill, can reproduce the
  headless divergence on their own machine and state the root cause — without a CI round-trip — on
  their first attempt.
- **SC-002**: Using the skill's diagnostic recipe, a reader correctly classifies a failing render
  assertion as either a genuine display-width overflow or an invisible-byte artifact in every case
  the skill is meant to cover.
- **SC-003**: After applying the skill's fix pattern, a render test that previously failed only in
  headless CI passes in both the local and the headless environment.
- **SC-004**: The skill is available in 100% of the FS-GG repositories that render with Spectre,
  installed from one canonical source, with identical content across copies.
- **SC-005**: 100% of the skill's factual claims and commands are backed by a reproducible step or
  a cited incident; a reviewer can trace each claim to its evidence.
- **SC-006**: When a contributor describes the symptom ("renders correctly locally, differently in
  CI") in their own words, the skill's trigger description selects it over generic guidance.
- **SC-007**: No repository build, test, publish, or merge step depends on the skill; removing or
  not reading it changes no workflow outcome.
- **SC-008**: Both a Claude agent (via the `SKILL.md`) and a Codex/general agent (via `AGENTS.md`)
  can discover and apply the skill from a single in-scope repository checkout, reaching the same
  content with no duplicated or divergent copy.

## Assumptions

- **Artifact type**: "Skill" means a skill document of the same kind as the existing
  `.claude/skills/` entries (e.g. cross-repo-coordination) — a `SKILL.md` whose Markdown body is
  agent-neutral content — **plus** a cross-agent `AGENTS.md` entrypoint so Codex and general agents
  use the same content (per the Session 2026-06-29 clarification). It is not an F# code change;
  there is no public F# surface, `.fsi`, or MVU boundary in scope.
- **Change classification**: Tier 2 (internal/tooling) — the skill adds no public API surface and
  changes no package or inter-project code contract. It is a documentation/knowledge artifact;
  the only cross-repo aspect is copying the same document into sibling repositories.
- **Canonical source & distribution**: the canonical copy lives in the org-shared `FS-GG/.github`
  skills location and is installed into product repos by copying, mirroring how the
  cross-repo-coordination skill is distributed; the `AGENTS.md` entrypoint is carried along with it
  in each in-scope repo. (Note: this repo's Spec Kit is currently initialized for Claude only —
  `integration: claude`, `context_file: CLAUDE.md` — so the `AGENTS.md` entrypoint is added
  explicitly rather than auto-generated by the current integration.)
- **Distribution scope**: the repositories in scope are those whose central package configuration
  references Spectre.Console — currently FS.GG.Governance and FS.GG.SDD. FS.GG.Rendering and
  FS.GG.Templates do not render with Spectre and are excluded.
- **Verified version**: the captured behavior was verified against the Spectre.Console version
  pinned in this repository at the time of the incident (0.57.1; the sibling Spectre-using repo
  pins 0.57.0). The skill is written as version-scoped, not as a permanent library guarantee.
- **Incident as evidence**: the skill is built from the already-completed and verified headless
  render determinism work in this repository (spec 091, issues #32/#34, PR #37) and the related
  cross-repo follow-up; these are real, reproduced events, not hypotheticals.
- **Advisory stance**: per the constitution's Local Skills section, the skill is an advisory aid;
  this feature introduces no gate, hook, or mandatory skill-loading step.
- **Authoring vs. enforcement**: this feature delivers the skill artifact and its distribution;
  it does not change any product code, test, or CI workflow in the consuming repositories.
