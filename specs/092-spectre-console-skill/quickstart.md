# Quickstart: validate the Spectre.Console headless-fidelity skill

These scenarios prove the skill is correct, usable by both agent families, and advisory. They map
to the spec's Success Criteria. Run from a checkout of an in-scope repo (e.g. FS.GG.Governance).

## Prerequisites

- The skill installed at `.claude/skills/spectre-console-headless-fidelity/SKILL.md`.
- The repo's root `AGENTS.md` carrying the managed entrypoint section (C2).
- .NET `net10.0` SDK + the repo's Spectre-using test project (for the live repro).

## Scenario 1 — Local headless reproduction, no CI round-trip (SC-001, SC-003)

The skill's "Reproduce locally" step must recreate the CI-only failure on a dev machine. Using the
original incident's test:

```sh
# Reproduces the headless divergence locally (red under the CI env var):
GITHUB_ACTIONS=true dotnet test tests/FS.GG.Governance.Cli.Tests -c Release --filter "WidthResilience"
```

**Expected**: with the pre-#37 console builder the run fails under `GITHUB_ACTIONS=true` while
passing without it; with the #37 fix in place it passes both ways (66/66). A reader following only
the skill reaches this without pushing to CI.

## Scenario 2 — Classify artifact vs genuine overflow (SC-002)

Follow the skill's "Diagnose" step (the cell-vs-unit dump) and confirm the reader can tell the two
apart: a line where `cells ≤ bound` but `len > bound` → **invisible-byte artifact** (this
incident); a line where `cells > bound` → **genuine display overflow** (opposite fix). The dump
prints both metrics per rendered line.

## Scenario 3 — Apply the fix (SC-003)

Apply the skill's fix (force `Profile.Capabilities.Ansi <- false` + `ColorSystem <- NoColors` after
`AnsiConsole.Create`, in the deterministic test console builder only) and re-run Scenario 1.

**Expected**: the previously CI-only-failing render test passes in both the local and the
`GITHUB_ACTIONS=true` environment; product rendering is unchanged (still colored in real terminals).

## Scenario 4 — Cross-agent reach, single source (SC-004, SC-008)

```sh
# Claude path: the skill exists with parseable frontmatter and the local-vs-CI trigger cue
test -f .claude/skills/spectre-console-headless-fidelity/SKILL.md && \
  grep -q "locally" .claude/skills/spectre-console-headless-fidelity/SKILL.md

# Codex/general path: AGENTS.md references the SKILL.md and does NOT restate its body
grep -q "spectre-console-headless-fidelity/SKILL.md" AGENTS.md
```

**Expected**: both succeed; `AGENTS.md` *references* (not duplicates) the skill, so both agent
families reach the same single source. Across in-scope repos the `SKILL.md` body is identical.

## Scenario 5 — Provenance & real-evidence (SC-005)

Open the skill's "Provenance" section and confirm every factual claim/command traces to a
reproducible step or a cited identifier (spec 091, #32 / #34 / #37, runs 28376202121 /
28377734248) — no unverified assertions.

## Scenario 6 — Advisory, never a gate (SC-007)

Confirm no repo workflow references the skill:

```sh
grep -rIl "spectre-console-headless-fidelity" .github/workflows/ ; echo "exit: $?"   # expect no matches
```

**Expected**: no matches — removing or not reading the skill changes no build/test/publish/merge
outcome.

## Done

All six scenarios pass → the skill satisfies its Success Criteria and is ready to propagate from
the canonical `FS-GG/.github` copy into the in-scope repos.
