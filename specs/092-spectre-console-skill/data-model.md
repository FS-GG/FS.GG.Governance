# Phase 1 Data Model: Spectre.Console headless-fidelity skill

The "data model" of a documentation artifact is its content structure and the relationships
between its parts. No runtime entities or storage.

## Entity: Skill document (`SKILL.md`) — the single source

| Field | Type | Rules |
|---|---|---|
| `name` (frontmatter) | string (kebab) | `spectre-console-headless-fidelity`; matches its directory name. |
| `description` (frontmatter) | string | Symptom-oriented so it auto-selects: must contain the "renders correctly locally but differs/fails in CI" cue and name Spectre.Console (FR-001, SC-006). |
| Body (Markdown) | sections | The required sections below, in order. This body is the **only** copy of the content (FR-001, FR-013). |

**Required body sections** (each maps to a requirement):

| Section | Content | Satisfies |
|---|---|---|
| Symptom / When to use | The trigger in plain terms: Spectre output correct locally, wrong/red in CI. | FR-002, SC-006 |
| The problem | Why headless CI diverges: `AnsiSupport.No` re-detected/overridden under `GITHUB_ACTIONS`; SGR escapes inflate `String.Length`; `cells ≠ String.Length`. | FR-002 |
| Reproduce locally | `GITHUB_ACTIONS=true dotnet test … --filter <render-test>` — no CI round-trip. | FR-003, SC-001 |
| Diagnose | The cell-vs-unit dump; how to classify *artifact* vs *genuine overflow*. | FR-004, SC-002 |
| Fix | Force `Capabilities.Ansi=false` + `ColorSystem=NoColors` post-`Create` (test/plain surface only); warn against degrading product output. | FR-005, SC-003 |
| Generalize | `NO_COLOR`, other CI signals; "assert the measure the system uses." | FR-009, FR-012 |
| Version scope | Verified Spectre.Console 0.57.x; re-verify elsewhere. | FR-008 |
| Provenance | Durable refs + date: spec 091, #32, #34, #37; runs 28376202121 / 28377734248. | FR-006, SC-005 |

## Entity: Per-agent entrypoints

| Instance | Location | Role | Rules |
|---|---|---|---|
| Claude skill | `.claude/skills/spectre-console-headless-fidelity/SKILL.md` | Native auto-discovery via frontmatter `description`. | Is the source body. |
| Codex/general entrypoint | repo-root `AGENTS.md` (managed section) | Points Codex/general agents at the skill body. | **References**, never copies (FR-013, edge case "entrypoint drift"). |
| Plain-Markdown fallback | the same `SKILL.md` read as text | Any agent that reads neither format still gets usable content. | Body must be readable standalone. |

**Relationship**: one Skill document ← referenced by → N per-agent entrypoints. No entrypoint
holds an independent copy of the content.

## Entity: Incident provenance

| Field | Value |
|---|---|
| Spec | 091 (headless render determinism) |
| Issues | #32 (the gap), #34 (the publish it blocked), #37 (the fix) |
| Evidence runs | 28376202121 (diagnostic dump), 28377734248 (green publish of 1.2.0) |
| Date stamped | 2026-06-29 |
| Corrected claim | Root cause = ANSI override under `GITHUB_ACTIONS`, **not** glyph measurement |

## Entity: Distribution set

| Field | Value |
|---|---|
| Canonical source | `FS-GG/.github/.claude/skills/spectre-console-headless-fidelity/` |
| In scope (install) | FS.GG.Governance (0.57.1), FS.GG.SDD (0.57.0) |
| Excluded | FS.GG.Rendering, FS.GG.Templates (no Spectre.Console) |
| Propagation | Copy canonical → repo `.claude/skills/…`; add/maintain `AGENTS.md` entrypoint. |
| Invariant | Content identical across copies (SC-004). |
