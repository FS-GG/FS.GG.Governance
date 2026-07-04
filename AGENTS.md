# FS.GG.Governance Agent Context

FS.GG.Governance is the governance component for FS.GG (rule evaluation, evidence
freshness, routing, profiles, and gate enforcement). Use standard Spec Kit for
all non-trivial work.

This file is the cross-agent (Codex / general-agent) entrypoint. Claude reads
the same skills natively from `.claude/skills/`. Sections below **reference**
their single source — they do not restate it.

<!-- SKILL:spectre-console START -->
### Spectre.Console

For working with Spectre.Console output in this project — the capability/profile
model, the widget tour, the rich/plain/JSON projection conventions, deterministic
test rendering, and the headless-fidelity pitfall (renders correctly locally but
differs/fails in CI) — see the single source:
`.claude/skills/spectre-console/SKILL.md`. Advisory only — it gates nothing.
<!-- SKILL:spectre-console END -->
