# FS.GG.Governance Agent Context

FS.GG.Governance is the governance product for FS.GG (rule evaluation, evidence
freshness, routing, profiles, and gate enforcement). Use standard Spec Kit for
all non-trivial work.

This file is the cross-agent (Codex / general-agent) entrypoint. Claude reads
the same skills natively from `.claude/skills/`. Sections below **reference**
their single source — they do not restate it.

<!-- SKILL:spectre-console-headless-fidelity START -->
### Spectre.Console headless fidelity

When Spectre.Console output renders correctly locally but differs or fails in CI
(GitHub Actions) — width/wrap assertions, plain / no-color output, or snapshots
that go red only on the runner — see the single source:
`.claude/skills/spectre-console-headless-fidelity/SKILL.md`.

It covers reproducing the divergence locally (`GITHUB_ACTIONS=true dotnet test …`),
classifying invisible-byte artifact vs genuine display overflow, and the matching
fix. Advisory only — it gates nothing.
<!-- SKILL:spectre-console-headless-fidelity END -->
