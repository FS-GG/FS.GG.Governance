# Contract: First-class `spectre-console` skill

The skill exposes five contracts — the testable interfaces a reviewer / quickstart checks. They
extend the 092 contract to the first-class, dual-part, renamed shape.

## C1 — `SKILL.md` frontmatter schema (peer of `cross-repo-coordination`)

```yaml
---
name: spectre-console                              # kebab; equals the directory name (renamed from spectre-console-headless-fidelity)
description: <dual topic + symptom trigger>        # see C1.1
metadata:
  source: <provenance incl. spec 091 / #32 / #34 / #37 and the 093 evolution>
---
```

**C1.1 — `description` dual-trigger contract**: MUST select the skill from **both** (a) a topic
phrasing — "working with / rendering Spectre.Console output in this project" — **and** (b) the 092
CI-fidelity symptom — "renders/behaves correctly locally but differs or fails in CI (GitHub
Actions)", including the artifact cues (width/wrap assertions, plain / no-color output, CI-only
snapshot diffs). (FR-011, SC-004)

**Validation**: frontmatter parses as YAML; `name` == directory == `spectre-console`; `description`
contains both the topic cue and the local-vs-CI symptom cue; same frontmatter+body shape as
`cross-repo-coordination` — compared against the **canonical** copy
`~/projects/.github/.claude/skills/cross-repo-coordination/SKILL.md`, since that skill is not
installed in the consuming repos' `.claude/skills/`. (FR-001, SC-004)

**C1.2 — `metadata.source` is additive**: the peer `cross-repo-coordination` frontmatter carries
`name` + `description` only. The `metadata.source` block above is an **allowed additive provenance
field** (it satisfies FR-007 and matches the prior 092 skill's frontmatter); it is NOT a deviation
from peer shape. "Same shape" means the same `--- name/description ---` core, not a byte-identical
frontmatter.

## C2 — Body structure contract (Part A + Part B)

The `SKILL.md` body MUST carry, clearly delineated:

- **Part A — generic Spectre primer**: capability/Profile mental model + a widget/API tour (markup &
  styles, tables, panels, rules & trees, prompts, live & status, capability profiles), at working
  depth, **linking out** to upstream docs for exhaustive API detail rather than restating it.
  (FR-002, FR-006)
- **Part B — FS-GG conventions**: rendering boundary → rich/plain/JSON parity → degrade-to-zero-ANSI
  → deterministic test rendering → **headless-fidelity pitfall (absorbed 092)** → version scope →
  provenance. (FR-003, FR-004, FR-005, FR-007)

**Validation** (see `data-model.md` E2/E3 for per-section content + the FR each satisfies):
- Part A covers every listed widget at working depth and links out for exhaustive API detail (no
  upstream-doc restatement). (FR-006, edge "generic-tutorial bloat")
- Part B's every convention is **checkable against the cited live code** — `HumanRender`/`HumanText`
  for the boundary, `RenderMode.fsi`/`ReportView.fsi` for the parity + degrade rule, `RenderSupport.fs`
  for deterministic rendering — not asserted from memory. (FR-003, FR-007, SC-006)
- Repo-specific identifiers appear as labeled examples, not as the skill's required shape. (FR-003)
- Version-dependent behavior is labeled "verified on Spectre.Console 0.57.x", framed as
  version-scoped. (FR-007, edge "version drift")

## C3 — Absorbed-incident (no-regression) contract

The 092 headless-fidelity diagnostic MUST survive as one Part B section with **no loss of detail or
provenance**:

- a runnable, CI-free reproduce command (`GITHUB_ACTIONS=true dotnet test … --filter WidthResilience`);
- the cell-vs-byte diagnostic dump and the classification table (invisible-byte artifact vs genuine
  display overflow — **opposite** fixes);
- the fix scoped to the test/plain surface + the "don't degrade product output" warning;
- the generalize lesson and version scope;
- the full provenance (spec 091; #32 / #34 / #37; evidence runs `28376202121` / `28377734248`;
  `RenderSupport.fs` / `WidthResilienceTests.fs`; date 2026-06-29; corrected root cause).

**Validation**: SC-003 — running the 092 reproduce→classify→fix against the new skill yields the same
correct outcome; every absorbed assertion still traces to its cited incident. (FR-005)

## C4 — `AGENTS.md` entrypoint contract (Codex / general agents)

Each in-scope repo's root `AGENTS.md` carries a managed marker that **references** the renamed skill:

```markdown
<!-- SKILL:spectre-console START -->
### Spectre.Console
For working with Spectre.Console output in this project — the capability/profile model, the
rich/plain/JSON projection conventions, deterministic test rendering, and the headless-fidelity
pitfall (renders correctly locally but differs/fails in CI) — see the single source:
`.claude/skills/spectre-console/SKILL.md`. Advisory only — it gates nothing.
<!-- SKILL:spectre-console END -->
```

**Validation** (FR-009, SC-008, edge "entrypoint drift"):
- The marker **references** the `SKILL.md` path; MUST NOT restate the body.
- A Codex/general agent reading `AGENTS.md` reaches the same content a Claude agent gets from
  `SKILL.md`; content lives in exactly one place.
- The old `<!-- SKILL:spectre-console-headless-fidelity … -->` marker and path are gone — re-pointed
  to `spectre-console`. (FR-012)

## C5 — No-orphaned-reference (rename) contract

After the rename, **no** reference to `spectre-console-headless-fidelity` may resolve to a missing
target. The prior name/path and every link to it (each in-scope repo's `AGENTS.md` marker and
`.claude/skills/` dir, the canonical `FS-GG/.github` copy, and the 092 spec/skill links) MUST resolve
to `spectre-console`.

**Validation** (FR-012, SC-008): a repo-wide `grep -rn spectre-console-headless-fidelity` across the
canonical source + both in-scope repos returns only intentional historical mentions (e.g. 092 spec
prose describing the prior name), and every *path/link* reference points at the renamed skill — no
dangling target.

## C6 — Advisory invariant (cross-cutting)

No build/test/publish/merge step in any repo references or depends on the skill; deleting it changes
no workflow outcome.

**Validation** (FR-010, SC-007): grepping CI workflows (`.github/workflows/`) for the skill name
(old or new) returns no dependency; the skill is reachable but never required.
