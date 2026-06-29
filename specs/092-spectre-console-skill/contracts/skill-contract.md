# Contract: Spectre.Console headless-fidelity skill

The skill exposes three contracts. They are the testable interfaces a reviewer / quickstart checks.

## C1 — `SKILL.md` frontmatter schema (same as existing skills)

```yaml
---
name: spectre-console-headless-fidelity        # kebab; equals the directory name
description: <symptom-oriented trigger>          # see C1.1
# optional, mirroring speckit-* skills:
# compatibility: <free text>
# metadata: { author: <...>, source: <...> }
---
```

**C1.1 — `description` trigger contract**: MUST be phrased so an agent selects it from the symptom,
not the library name alone. MUST include both: (a) the cue "renders/behaves correctly locally but
differs or fails in CI (GitHub Actions)"; (b) the artifact kinds — width/wrap assertions, plain /
no-color output, snapshots. (FR-001, SC-006)

**Validation**: frontmatter parses as YAML; `name` matches directory; `description` contains the
local-vs-CI cue. Consistent with `.claude/skills/cross-repo-coordination/SKILL.md`.

## C2 — `AGENTS.md` entrypoint contract (Codex / general agents)

Each in-scope repo's root `AGENTS.md` carries a managed section that references the skill:

```markdown
<!-- SPECKIT START -->  (or an equivalent managed marker pair)
### Spectre.Console headless fidelity
When Spectre.Console output renders correctly locally but differs/fails in CI, see
`.claude/skills/spectre-console-headless-fidelity/SKILL.md` (single source).
<!-- SPECKIT END -->
```

**Validation** (FR-013, SC-008, edge case "entrypoint drift"):
- The section **references** the `SKILL.md` path; it MUST NOT restate the skill's body.
- A Codex/general agent reading `AGENTS.md` reaches the same content a Claude agent gets from the
  `SKILL.md`.
- Content lives in exactly one place (the `SKILL.md` body).

## C3 — Required body sections (content contract)

The `SKILL.md` body MUST contain, in order: **Symptom/When to use → The problem → Reproduce locally
→ Diagnose → Fix → Generalize → Version scope → Provenance** (see `data-model.md` for the
per-section content and the FR each satisfies).

**Validation**:
- **Reproduce locally** contains a runnable, CI-free command (`GITHUB_ACTIONS=true dotnet test …`).
  (FR-003, SC-001)
- **Diagnose** lets the reader classify *invisible-byte artifact* vs *genuine display overflow*
  via the cell-vs-unit comparison. (FR-004, SC-002)
- **Fix** scopes to the test/plain surface and warns against degrading product output. (FR-005)
- **Version scope** names Spectre.Console 0.57.x. (FR-008)
- **Provenance** cites durable identifiers (spec 091, #32/#34/#37) and a date. (FR-006, SC-005)
- Every command/claim is reproducible or cited — no unverified assertion. (FR-006, SC-005)
- General guidance does not assume one repo's IDs/layout; repo-specifics are labeled examples.
  (FR-011)

## C4 — Advisory invariant (cross-cutting)

No build/test/publish/merge step in any repo references or depends on the skill; deleting it
changes no workflow outcome. (FR-010, SC-007)
