# Phase 0 Research: Spectre.Console headless-fidelity skill

All items below were resolved from the repo's own conventions and the already-verified incident
(spec 091, issues #32 / #34, PR #37, run 28376202121 / 28377734248). No open NEEDS CLARIFICATION.

## R1 — Skill artifact format

**Decision**: A `.claude/skills/<name>/SKILL.md` with YAML frontmatter (`name`, a symptom-oriented
`description`) and a plain-Markdown body that holds all content — identical in shape to the repo's
existing skills (`speckit-*`) and the global `cross-repo-coordination` skill.

**Rationale**: "Same as the other skills" (user directive). The `cross-repo-coordination` skill is
exactly this shape (minimal frontmatter: `name` + a rich `description`; Markdown body), and it is
the precedent for a hand-authored, cross-repo-distributed knowledge skill.

**Alternatives considered**: A bespoke doc under `docs/` (loses Claude auto-discovery via the skill
`description`); a generated artifact (over-engineered for one document — violates Principle III).

## R2 — Skill name

**Decision**: `spectre-console-headless-fidelity`.

**Rationale**: Discovery is driven primarily by the `description`, but a descriptive kebab name
matches the existing convention (`cross-repo-coordination`). "headless-fidelity" names the precise
class (local-vs-CI render divergence) without over-narrowing to the single 091 symptom.

**Alternatives**: `spectre-console` (too broad — would trigger on any Spectre use);
`spectre-headless-determinism` (ties it to the 091 spec title, narrower than the skill's scope).

## R3 — Codex / general-agent compatibility mechanism

**Decision**: Surface the same content to Codex and general agents via the cross-agent **`AGENTS.md`**
convention — a managed section in each in-scope repo's root `AGENTS.md` that *references* the
`SKILL.md` (single source), rather than restating it.

**Rationale**: Codex reads `AGENTS.md` (the agents.md cross-agent standard). The repo's bundled
agent-context extension already treats `AGENTS.md` as a first-class context file alongside
`CLAUDE.md` / `.github/copilot-instructions.md` / `GEMINI.md` (see
`.specify/extensions/agent-context/`). Referencing (not copying) the SKILL.md keeps one source of
truth and prevents drift (FR-013, edge case "entrypoint drift").

**Alternatives**: Duplicate the full content into `AGENTS.md` (drift risk — rejected);
`AGENTS.md`-only with no Claude skill (loses Claude's frontmatter auto-discovery — rejected, the
user wants "same as the other skills" *and* Codex).

**Caveat (recorded)**: this repo's Spec Kit is initialized for Claude only (`integration: claude`,
`context_file: CLAUDE.md`), so the `AGENTS.md` entrypoint is added explicitly by this feature, not
auto-materialized by the current integration. The agent-context tooling *can* manage `AGENTS.md`
if/when an integration targets it.

## R4 — The incident the skill must encode (verified facts)

**Decision**: Encode this root cause and evidence chain verbatim (corrected from 091's original
hypothesis):

- **Symptom**: `WidthResilience` width-10/20 rich-render assertions pass locally, fail only on the
  GitHub Actions runner; the publish `cli-tests` gate blocks.
- **Root cause**: `AnsiConsole.Create` re-detects ANSI *after* `settings.Ansi <- AnsiSupport.No`;
  under `GITHUB_ACTIONS=true` Spectre force-enables ANSI. `console.Write(Markup …)` then emits SGR
  escapes (`ESC[1m … ESC[0m`) into the "plain" output. The escapes are invisible but inflate
  `String.Length` (`exit status: blocked` 20 → 28), tripping the per-line width assertion.
- **NOT the cause**: glyph/East-Asian-width measurement (091's original plan hypothesis) and the
  em-dash — proven wrong by the diagnostic; the Rounded table renders an identical **20 cells** on
  both hosts.
- **Fix**: in the deterministic test console builder, force `Profile.Capabilities.Ansi <- false`
  and `ColorSystem <- NoColors` *after* `AnsiConsole.Create` (overriding the env re-detection).
  Test-support only — the product console still emits color in real terminals/CI logs by design.

**Rationale**: Real-evidence standard (Principle V). The diagnostic (run 28376202121) and the
green CI publish (run 28377734248) are the evidence.

## R5 — Diagnostic recipe to include

**Decision**: The skill ships a copy-pasteable recipe:

1. **Reproduce locally without CI**: `GITHUB_ACTIONS=true dotnet test <proj> --filter <render-test>`
   (the env var is the trigger; the test flips red locally under it).
2. **Classify the failure** — a one-shot dump printing each rendered line with **both**
   `String.Length` (UTF-16 units, what a naive assertion counts) and Spectre's display-cell count
   (`Spectre.Console.Rendering.Segment(line).CellCount()`). A line whose `cells` are within bound
   but whose `len` is not → invisible-byte artifact (this incident). A line whose `cells` exceed
   bound → genuine display overflow (opposite fix: fix the layout/width, not the escapes).
3. **Optional CI confirmation**: dispatch the publish workflow on a branch and read the gate log.

**Rationale**: This is the exact path that resolved the incident; it generalizes the "is it a real
overflow or a measurement artifact?" decision (edge case + FR-004 + SC-002).

## R6 — Generalization beyond the single trigger

**Decision**: The skill frames the cause as "CI ANSI auto-enable / capability re-detection," noting
`GITHUB_ACTIONS` as the observed signal, and also names the broader levers: the `NO_COLOR`
convention, other CI env signals, and the general rule *assert against the same measure the system
uses* (display cells / visible text), not `String.Length`.

**Rationale**: FR-009 / FR-012 — avoid hard-coding one env var as the only trigger; record the
transferable lesson (test surface vs. real/CI surface fidelity).

## R7 — Version scope

**Decision**: State "verified against Spectre.Console 0.57.x (0.57.1 here, 0.57.0 in the sibling
repo)" and instruct re-verification on a different version.

**Rationale**: FR-008 — the behavior is library-version-scoped, not a permanent guarantee.

## R8 — Distribution & scope

**Decision**: Canonical copy in `FS-GG/.github/.claude/skills/spectre-console-headless-fidelity/`;
install (copy) into FS.GG.Governance and FS.GG.SDD; carry the `AGENTS.md` entrypoint in each.
Exclude FS.GG.Rendering and FS.GG.Templates.

**Rationale**: Scope = repos whose central package config references Spectre.Console (confirmed:
Governance 0.57.1, SDD 0.57.0; Rendering/Templates have none). Mirrors how
`cross-repo-coordination` is distributed (FR-007, Assumptions).
