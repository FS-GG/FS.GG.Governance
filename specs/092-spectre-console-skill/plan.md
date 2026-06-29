# Implementation Plan: Spectre.Console headless-fidelity skill

**Branch**: `092-spectre-console-skill` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/092-spectre-console-skill/spec.md`

## Summary

Author a portable, advisory **skill** that captures the verified Spectre.Console headless
test-fidelity incident (spec 091 / #32 / #37): a rich-render surface that is correct locally but
fails on the GitHub Actions runner because `AnsiSupport.No` is overridden under `GITHUB_ACTIONS`,
leaking SGR escape bytes that inflate `String.Length`. The skill teaches a contributor to
reproduce the divergence locally, distinguish a real display overflow from an invisible-byte
artifact, and apply the matching fix. It is shipped in the **same shape as the repo's existing
skills** — a `SKILL.md` (frontmatter `name`+`description`, Markdown body as single source),
canonical in `FS-GG/.github`, copied into the Spectre-using repos — **plus** an `AGENTS.md`
entrypoint that surfaces the same body for Codex and general agents (Session 2026-06-29
clarification). No content is duplicated.

## Technical Context

**Language/Version**: Markdown (skill document). No F# / runtime code authored by this feature.
The *examples inside* the skill reference F# on .NET `net10.0` with Spectre.Console **0.57.x**
(0.57.1 in this repo; 0.57.0 in the sibling Spectre-using repo).

**Primary Dependencies**: None added. The skill *documents* Spectre.Console behavior and the
existing test harness (`tests/FS.GG.Governance.Cli.Tests`, Expecto); it introduces no package.

**Storage**: Files only — `SKILL.md` + an `AGENTS.md` entrypoint per in-scope repo; canonical copy
in `FS-GG/.github`.

**Testing**: Validation is by the quickstart scenarios (local headless repro via
`GITHUB_ACTIONS=true dotnet test`; the cell-vs-unit dump; cross-agent content check), not an
automated unit suite — this is a documentation artifact.

**Target Platform**: Coding agents operating in the FS-GG repos — Claude (via `.claude/skills/`)
and Codex / general agents (via `AGENTS.md`). Repos in scope: FS.GG.Governance, FS.GG.SDD.

**Project Type**: Documentation / knowledge artifact (a Spec Kit "Local Skill"), distributed
cross-repo. Not a code project.

**Performance Goals**: N/A (static document).

**Constraints**: Advisory only (never a gate); single-source content (SKILL.md body is canonical,
`AGENTS.md` references it — no second copy); every claim backed by a reproducible step or a cited
incident; generic where the advice is general (no repo-specific layout baked into general points).

**Scale/Scope**: One skill; ~one canonical copy + 2 in-scope repo installs. Verified against
Spectre.Console 0.57.x and the GitHub Actions runner.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Rule | Verdict | Note |
|---|---|---|
| I. Spec → FSI → Semantic tests → Implementation | **N/A** | No F# public surface authored; no `.fsi` to sketch. The artifact is a Markdown skill. |
| II. Visibility lives in `.fsi` | **N/A** | No F# module. |
| III. Idiomatic simplicity | **PASS** | Plainest form: one `SKILL.md` + a thin `AGENTS.md` reference. No generator, no tooling, no duplication. |
| IV. Elmish/MVU boundary | **N/A** | No stateful/I/O workflow. |
| V. Test evidence is mandatory | **PASS** | Every claim is backed by a reproducible command or the real, already-verified incident (spec 091 / #37); quickstart re-runs the headless repro as evidence. No synthetic claims. |
| VI. Observability & safe failure | **N/A** | Not a runtime component. |
| Change Classification | **Tier 2** | No public API surface, no package or inter-project code contract. Documentation/knowledge artifact copied across repos. |
| Local Skills (advisory, not a gate) | **PASS** | Skill is advisory; no hook, no mandatory load step, no workflow depends on it (FR-010). |
| Operating rule / Genericity | **PASS** | General guidance avoids assuming one repo's IDs/layout; repo-specific bits are labeled examples (FR-011). |

**Result**: PASS — no violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/092-spectre-console-skill/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (skill name/format, AGENTS.md mechanism, incident facts, fix)
├── data-model.md        # Phase 1 — the skill's content model (sections, entrypoints, provenance, distribution)
├── quickstart.md        # Phase 1 — validation scenarios (maps to SC-001..SC-008)
├── contracts/
│   └── skill-contract.md  # Phase 1 — frontmatter schema + AGENTS.md entrypoint + required body sections
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source artifacts (delivered by implementation, NOT in this feature dir)

```text
# Canonical (org-shared source of truth)
FS-GG/.github/.claude/skills/spectre-console-headless-fidelity/SKILL.md

# Installed per in-scope repo (copied from canonical), e.g.:
FS.GG.Governance/.claude/skills/spectre-console-headless-fidelity/SKILL.md
FS.GG.Governance/AGENTS.md      # managed entrypoint section referencing the skill (single-source)
FS.GG.SDD/.claude/skills/spectre-console-headless-fidelity/SKILL.md
FS.GG.SDD/AGENTS.md
```

**Structure Decision**: Documentation artifact. The single source of content is the `SKILL.md`
Markdown body under `.claude/skills/spectre-console-headless-fidelity/`. Each in-scope repo also
carries an `AGENTS.md` entrypoint whose managed section points Codex/general agents at that skill
(no content copy). The canonical copy lives in `FS-GG/.github` and is propagated by copying,
mirroring `cross-repo-coordination`. FS.GG.Rendering and FS.GG.Templates are excluded (they do not
render with Spectre).

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
