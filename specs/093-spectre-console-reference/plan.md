# Implementation Plan: First-class Spectre.Console skill (general knowledge + docs)

**Branch**: `093-spectre-console-reference` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/093-spectre-console-reference/spec.md`

## Summary

Evolve the narrow 092 `spectre-console-headless-fidelity` skill into **one** first-class
`spectre-console` skill that carries the whole working model of Spectre.Console for FS-GG, in the
same form and depth as `cross-repo-coordination`. The renamed `SKILL.md` body carries **Part A** a
generic Spectre primer (capability/profile mental model + a widget/API tour — markup, tables,
panels, rules/trees, prompts, live/status, capability profiles, linking out for exhaustive API
detail) and **Part B** the FS-GG rendering conventions grounded in this repo's real code (the
`HumanRender`/`HumanText` presentation edge; the `RenderMode` Json-is-contract / Plain+Rich-are-
projections rule; degrade-to-zero-ANSI; the deterministic fixed-width test render in
`RenderSupport.fs`), with the **092 headless-fidelity incident absorbed verbatim-in-substance as one
pitfalls section** — no loss of diagnostic detail or provenance (spec 091 / #32 / #34 / #37).

The rename leaves **no orphaned reference**: every in-scope repo's `.claude/skills/` dir, its
`AGENTS.md` entrypoint marker, the canonical `FS-GG/.github` copy, and the 092 spec/skill links must
resolve to the new `spectre-console` skill. Distribution mirrors 092: one canonical source in
`FS-GG/.github`, copied byte-identically into the Spectre-using repos (FS.GG.Governance @ 0.57.1,
FS.GG.SDD @ 0.57.0), with FS.GG.Rendering and FS.GG.Templates recorded as excluded. The skill stays
**advisory** — it gates nothing. This is a Tier 2 documentation/knowledge artifact; no F# code,
`.fsi`, package, or workflow changes.

## Technical Context

**Language/Version**: Markdown (the skill document). No F# / runtime code authored by this feature.
*Examples inside* the skill reference F# on .NET `net10.0` with Spectre.Console **0.57.x** (0.57.1 in
FS.GG.Governance, 0.57.0 in FS.GG.SDD), framed as version-scoped.

**Primary Dependencies**: None added. The skill *documents* Spectre.Console and the existing real
code (`src/FS.GG.Governance.HumanRender/`, `src/FS.GG.Governance.HumanText/`,
`tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs`); it introduces no package or tool.

**Storage**: Files only — one `SKILL.md` per in-scope repo under
`.claude/skills/spectre-console/`, the matching `AGENTS.md` entrypoint marker per repo, and the
canonical copy in `FS-GG/.github`. No skill body content is duplicated outside `SKILL.md`.

**Testing**: Validation is by the quickstart scenarios (peer-shape comparison vs
`cross-repo-coordination`; the 092 reproduce→classify→fix re-run via `GITHUB_ACTIONS=true dotnet
test … --filter WidthResilience`; dual-trigger description check; byte-identical cross-repo diff;
no-orphaned-reference grep; advisory grep over CI workflows), not an automated unit suite — this is a
documentation artifact.

**Target Platform**: Coding agents in the FS-GG repos — Claude (via `.claude/skills/`) and Codex /
general agents (via `AGENTS.md`). In-scope repos: FS.GG.Governance, FS.GG.SDD.

**Project Type**: Documentation / knowledge artifact (a Spec Kit "Local Skill"), distributed
cross-repo. Not a code project; no public F# surface, `.fsi`, or MVU boundary in scope.

**Performance Goals**: N/A (static document).

**Constraints**: Advisory only (never a gate, FR-010). Single-source content (the `SKILL.md` body is
canonical; `AGENTS.md` references it — no second copy, FR-009). Every claim/command/convention
traces to a reproducible step, a real code pointer, or a cited incident — no unverified assertion
(FR-007); version-dependent behavior is labeled with the verified Spectre version. Project-
convention claims must stay checkable against the live code, not asserted from memory (FR-003, edge
case "drift from the real code"). Repo-specific identifiers are presented as examples, not as the
skill's required shape. Must not regress 092's diagnostic value or provenance (FR-005, P2). Must not
balloon into a copy of upstream docs — link out for exhaustive API detail (FR-006, edge case
"generic-tutorial bloat").

**Scale/Scope**: One skill (renamed from 092). One canonical copy + 2 in-scope repo installs + 2
recorded exclusions. Verified against Spectre.Console 0.57.x.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Rule | Verdict | Note |
|---|---|---|
| I. Spec → FSI → Semantic tests → Implementation | **N/A** | No F# public surface authored; no `.fsi` to sketch. The artifact is a Markdown skill. |
| II. Visibility lives in `.fsi` | **N/A** | No F# module changed. |
| III. Idiomatic simplicity | **PASS** | Plainest form: one `SKILL.md` (renamed) + a thin `AGENTS.md` reference per repo. No generator, no tooling, no duplication; the second 092-style parallel skill was rejected in clarification (one skill, not two). |
| IV. Elmish/MVU boundary | **N/A** | No stateful/I/O workflow authored. (The skill *describes* the existing `RenderMode`/`HumanRender` edge; it does not add one.) |
| V. Test evidence is mandatory | **PASS** | Every claim is backed by a reproducible command, a real code pointer (`HumanRender`, `RenderMode.fsi`, `RenderSupport.fs`), or the already-verified 092 incident (spec 091 / #32 / #34 / #37). Quickstart re-runs the headless repro as live evidence. No synthetic claims. |
| VI. Observability & safe failure | **N/A** | Not a runtime component. |
| Change Classification | **Tier 2** | No public API surface, no package or inter-project code contract. Documentation/knowledge artifact copied across repos (matches spec Assumptions). |
| Local Skills (advisory, not a gate) | **PASS** | Skill is advisory; no hook, no mandatory load step, no workflow depends on it (FR-010, SC-007). |
| Operating rule / Genericity | **PASS** | Part A is generic Spectre knowledge; Part B's repo-specific identifiers are labeled examples, not the skill's required shape (FR-003). Governance inspects rendering conventions; it requires nothing of rendering. |

**Result**: PASS — no violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/093-spectre-console-reference/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions (rename strategy, A+B structure, incident absorption, grounding sources, distribution, no-orphan rename)
├── data-model.md        # Phase 1 — the skill's content model (Part A / Part B sections, entrypoints, provenance, distribution set, version scope)
├── quickstart.md        # Phase 1 — validation scenarios (maps to SC-001..SC-008)
├── contracts/
│   └── skill-contract.md  # Phase 1 — frontmatter + dual-trigger description + AGENTS.md entrypoint + required Part A/B body sections + advisory invariant
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source artifacts (delivered by implementation, NOT in this feature dir)

```text
# Canonical (org-shared source of truth) — RENAMED dir
FS-GG/.github/.claude/skills/spectre-console/SKILL.md

# Installed per in-scope repo (copied byte-identical from canonical):
FS.GG.Governance/.claude/skills/spectre-console/SKILL.md
FS.GG.Governance/AGENTS.md      # managed marker re-pointed to spectre-console (single-source reference)
FS.GG.SDD/.claude/skills/spectre-console/SKILL.md
FS.GG.SDD/AGENTS.md

# Removed by the rename (must leave no orphan): the old
# .claude/skills/spectre-console-headless-fidelity/ dir and the old AGENTS.md marker
# in every in-scope repo + the canonical source.
```

**Grounding sources for Part B (real code this skill describes, checked against — not authored)**:

- Rendering edge: `src/FS.GG.Governance.HumanRender/` (`Capability.fsi`, `RichRender.fsi`, `Tui.fsi`,
  `Watch.fsi`) and `src/FS.GG.Governance.HumanText/` (`RenderMode.fsi`, `ReportView.fsi`,
  `HumanText.fsi`).
- Render-mode rule: `RenderMode.fsi` — `Json | Plain | Rich`; `selectMode` is pure/total; `Json`
  is the byte-identical automation contract and always wins; `Plain`/`Rich` are non-contractual
  human projections of the **same** `ReportView`; capability sensing is an Effect at the interpreter
  edge, never in the pure function.
- Deterministic test render: `tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs` — a fixed-width
  `StringWriter`-backed `IAnsiConsole` with pinned capabilities; the live fix from 092
  (`Capabilities.Ansi <- false` / `ColorSystem <- NoColors` post-`Create`) and the assertion it
  protects (`WidthResilienceTests.fs`).

**Structure Decision**: Documentation artifact. The single source of content is the renamed
`SKILL.md` body under `.claude/skills/spectre-console/`. Each in-scope repo also carries an
`AGENTS.md` entrypoint whose managed marker points Codex/general agents at that skill (no content
copy). The canonical copy lives in `FS-GG/.github` and is propagated by copying, mirroring
`cross-repo-coordination`. FS.GG.Rendering and FS.GG.Templates are excluded (they do not render with
Spectre). The rename from `spectre-console-headless-fidelity` is propagated everywhere with no
orphaned reference left.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
