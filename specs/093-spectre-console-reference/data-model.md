# Phase 1 Data Model: First-class Spectre.Console skill

The "data" of this feature is the **content model** of the skill document and its distribution —
there is no runtime entity. Entities below are the structural pieces a reviewer/quickstart checks,
derived from the spec's Key Entities and Requirements.

## E1 — Skill document (`SKILL.md`)

The single source. Renamed from `spectre-console-headless-fidelity` to `spectre-console`.

| Field | Value / rule | Source |
|---|---|---|
| `name` (frontmatter) | `spectre-console` (kebab; equals directory name) | FR-001, FR-012 |
| `description` (frontmatter) | Dual-trigger: selects on **topic** ("working with / rendering Spectre.Console output in this project") **and** the 092 **symptom** ("renders correctly locally but differs/fails in CI" + width/wrap, plain/no-color, CI-only snapshot cues) | FR-011, SC-004 |
| `metadata.source` | Provenance string incl. 092 incident (spec 091 / #32 / #34 / #37) and the 093 evolution | FR-007 |
| Body | Plain Markdown, single source; Part A + Part B (below) | FR-001, FR-006 |

**Validation**: frontmatter parses as YAML; `name` matches directory; `description` contains *both*
trigger cues; reads as a peer of `cross-repo-coordination` (same frontmatter+body shape).

## E2 — Body Part A: generic Spectre.Console primer

The widget/API tour + mental model at the depth a contributor needs to work — not a copy of upstream
docs (links out for exhaustive detail).

| Section | Required content | FR |
|---|---|---|
| Capability / Profile mental model | How Spectre derives a `Profile` (ANSI support, color system, width, output encoding, Unicode/Legacy) on `AnsiConsole.Create`, and how each can be **pinned**; rich vs plain/no-color surface relationship | FR-002 |
| Markup & styles | Markup syntax, escaping, style/color basics | FR-006 |
| Tables | Columns, headers, width/wrap behavior | FR-006 |
| Panels | Borders, padding, headers | FR-006 |
| Rules & trees | Section rules, tree/hierarchy rendering | FR-006 |
| Prompts | Text/confirm/choice prompts (interactive surface) | FR-006 |
| Live & status | Live display, status/spinner surfaces and their non-interactive behavior | FR-006 |
| Capability profiles | Detecting/forcing ANSI, color systems, width, encoding for predictable output | FR-002, FR-006 |
| Link-out | Pointer to upstream Spectre.Console docs for exhaustive API coverage | FR-006 |

**Validation**: each widget is covered at working depth; exhaustive API detail is linked, not
restated (edge case "generic-tutorial bloat").

## E3 — Body Part B: FS-GG project conventions (grounded in real code)

| Section | Required content | Real-code pointer | FR |
|---|---|---|---|
| Rendering boundary | Spectre code lives only at the presentation edge | `src/FS.GG.Governance.HumanRender/` (`RichRender.fsi`, `Capability.fsi`, `Tui.fsi`, `Watch.fsi`); `src/FS.GG.Governance.HumanText/` | FR-003 |
| Rich/plain/JSON parity | Rich is a pure projection that adds/drops no facts vs plain/JSON; `Json` is the byte-identical automation contract and always wins; `Plain`/`Rich` are non-contractual projections of the **same** `ReportView`; `selectMode` is pure/total; capability sensing is an edge Effect | `src/FS.GG.Governance.HumanText/RenderMode.fsi`, `ReportView.fsi` | FR-003 |
| Degrade-to-zero-ANSI | Rich degrades to plain when non-interactive/redirected or color-disabled (`NO_COLOR`, `TERM=dumb`); `selectMode` returns `Rich` iff `IsTty && not NoColorEnv && not ExplicitPlain`, else `Plain` | `RenderMode.fsi` (`ColorCapability`, `selectMode`) | FR-003 |
| Deterministic test rendering | Render to a fixed-width, host-independent console so layout is a pure function of (content, width); pinned capabilities | `tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs`, `WidthResilienceTests.fs` | FR-004 |
| Headless-fidelity pitfall (absorbed 092) | Full reproduce → classify (invisible-byte artifact vs genuine overflow) → fix (scoped to test/plain surface), with no loss of detail | `RenderSupport.fs` (live fix), `WidthResilienceTests.fs`; spec 091 / #32 / #34 / #37 | FR-005, E4 |
| Version scope | Version-dependent behavior labeled "verified on Spectre.Console 0.57.x (0.57.1 here / 0.57.0 SDD)", framed as version-scoped, distinct from durable conventions | — | FR-007, edge case "version drift" |
| Provenance | Every claim ↦ reproducible step, code pointer, or cited incident; the 092 provenance block intact | as above | FR-007 |

**Validation**: each convention is checkable against the cited source (not asserted from memory);
repo-specific identifiers appear as labeled examples, not as the skill's required shape (FR-003).

## E4 — Absorbed incident section (092)

The 092 headless-fidelity diagnostic as one section of Part B (per D3). Preserves: the
local-reproduction command (`GITHUB_ACTIONS=true dotnet test … --filter WidthResilience`), the
cell-vs-byte diagnostic dump, the classification table (invisible-byte artifact vs genuine display
overflow — opposite fixes), the test/plain-surface-scoped fix + the "don't degrade product output"
warning, the generalize lesson ("assert against the measure the system uses"), the version scope,
and the full provenance (spec 091; #32 / #34 / #37; evidence runs `28376202121` / `28377734248`;
`RenderSupport.fs` / `WidthResilienceTests.fs`; date 2026-06-29; corrected root cause).

**Validation**: SC-003 — running the 092 reproduce→classify→fix against the new skill yields the same
outcome with provenance intact; every factual assertion still traces to its cited incident.

## E5 — Per-agent entrypoints

| Entrypoint | Role | Rule |
|---|---|---|
| `SKILL.md` (frontmatter) | Claude native auto-discovery | The single source body |
| `AGENTS.md` managed marker | Codex / general-agent entrypoint | **References** the `SKILL.md` body via its path; never restates it; re-pointed from `spectre-console-headless-fidelity` → `spectre-console` |

**Validation**: FR-009 / SC-008 — both agents reach the same single-source body; content lives in
exactly one place; an agent reading neither format still gets usable content from the plain-Markdown
body; no reference to the prior 092 name is left dangling.

## E6 — Distribution set

| Repo | Role | Spectre version |
|---|---|---|
| `FS-GG/.github` | **Canonical source** | n/a |
| FS.GG.Governance | In-scope install (byte-identical copy + `AGENTS.md`) | 0.57.1 |
| FS.GG.SDD | In-scope install (byte-identical copy + `AGENTS.md`) | 0.57.0 |
| FS.GG.Rendering | **Excluded** (no Spectre rendering) | — |
| FS.GG.Templates | **Excluded** (no Spectre rendering) | — |

**Validation**: FR-008 / SC-005 — present in 100% of Spectre-using repos from one canonical source,
byte-identical across copies, exclusions recorded.

## Cross-entity invariants

- **Advisory** (E1–E6): no build/test/publish/merge step references the skill; removing it changes
  no workflow outcome (FR-010, SC-007).
- **Single-source** (E1, E5): the `SKILL.md` body is the only copy of the content; `AGENTS.md`
  references it (FR-009).
- **No orphan after rename** (E1, E5, E6): the prior name/path and every link to it resolve to
  `spectre-console` across all in-scope repos and the canonical source (FR-012, SC-008).
- **No-regression** (E4): 092's diagnostic detail and provenance are fully preserved (FR-005, SC-003).
