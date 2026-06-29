# Quickstart: Validate the first-class `spectre-console` skill

Runnable validation scenarios that prove the feature works end-to-end. Each maps to a success
criterion. Run from the FS.GG.Governance repo root unless noted. See `contracts/skill-contract.md`
and `data-model.md` for the detailed content/structure each step checks (not duplicated here).

**Prerequisites**: the renamed skill exists at `.claude/skills/spectre-console/SKILL.md` in the
canonical source (`FS-GG/.github`) and in each in-scope repo; `AGENTS.md` markers re-pointed; .NET
SDK 10 + Spectre.Console 0.57.x restored (`dotnet restore`).

## S1 — First-class shape & dual-trigger description (SC-004)

```sh
# Peer-shape: same frontmatter+body shape as cross-repo-coordination.
# NOTE: cross-repo-coordination is NOT installed in this repo's .claude/skills/ — it lives only in
# the canonical FS-GG/.github source, so compare against the canonical copy:
head -6 .claude/skills/spectre-console/SKILL.md
head -6 ~/projects/.github/.claude/skills/cross-repo-coordination/SKILL.md
# Dual trigger present in the description:
grep -iE "rendering|working with .*Spectre" .claude/skills/spectre-console/SKILL.md | head   # topic cue
grep -iE "locally but .*(differ|fail).*CI"  .claude/skills/spectre-console/SKILL.md | head   # symptom cue
```

**Expected**: `name: spectre-console` + a `description` line; the same `--- name/description ---`
frontmatter shape as `cross-repo-coordination` (which carries `name` + `description` only — the new
skill's added `metadata.source` provenance is an allowed additive field per FR-007, not a deviation);
both the topic cue *and* the local-vs-CI symptom cue appear in `description`.

## S2 — Part A primer covers the widgets at working depth (SC-001 support)

```sh
grep -iE "^#+ .*(markup|table|panel|rule|tree|prompt|live|status|profile|capabilit)" \
  .claude/skills/spectre-console/SKILL.md
grep -iE "https?://" .claude/skills/spectre-console/SKILL.md   # link-out to upstream docs
```

**Expected**: headings/coverage for markup, tables, panels, rules/trees, prompts, live/status, and
capability profiles; at least one upstream-docs link (exhaustive API detail is linked, not
restated).

## S3 — Part B conventions are grounded in real code (SC-001, SC-006)

```sh
# Each cited path must exist (claims checkable against source, not memory):
ls src/FS.GG.Governance.HumanRender/RichRender.fsi src/FS.GG.Governance.HumanText/RenderMode.fsi \
   src/FS.GG.Governance.HumanText/ReportView.fsi tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs
# The skill must cite them:
grep -nE "HumanRender|RenderMode|ReportView|RenderSupport" .claude/skills/spectre-console/SKILL.md
```

**Expected**: every cited path resolves; Part B references the rendering boundary
(`HumanRender`/`HumanText`), the `RenderMode` Json-is-contract / Plain+Rich-projection parity rule,
the degrade-to-zero-ANSI rule, and the `RenderSupport.fs` deterministic-render pattern. A reader can
make a small rich-output change at the sanctioned edge preserving parity + degrade (SC-001).

## S4 — Deterministic-render pattern is usable (SC-002)

```sh
# The fixed-width, host-independent render the skill points to actually compiles/runs:
dotnet test tests/FS.GG.Governance.Cli.Tests -c Release --filter "WidthResilience"
```

**Expected**: green. The skill's deterministic-rendering guidance matches the live pattern in
`RenderSupport.fs` (fixed-width `StringWriter`-backed `IAnsiConsole`, pinned capabilities), so a
reader can produce a host-independent render test without inventing the pattern.

## S5 — Headless-fidelity diagnostic preserved (SC-003)

```sh
# Reproduce the 092 divergence locally (no CI round-trip):
GITHUB_ACTIONS=true dotnet test tests/FS.GG.Governance.Cli.Tests -c Release --filter "WidthResilience"
dotnet test tests/FS.GG.Governance.Cli.Tests -c Release --filter "WidthResilience"
# Provenance still present in the absorbed section:
grep -nE "091|#32|#34|#37|28376202121|28377734248" .claude/skills/spectre-console/SKILL.md
```

**Expected**: both runs green with the fix in place (the reproduce→classify→fix narrative still
leads here); the classification table (invisible-byte vs genuine overflow) and the full provenance
are present — no diagnostic detail lost vs 092.

## S6 — Single-source, byte-identical distribution (SC-005, SC-008)

```sh
# Byte-identical across canonical + in-scope repos:
diff ~/projects/.github/.claude/skills/spectre-console/SKILL.md \
     ~/projects/FS.GG.Governance/.claude/skills/spectre-console/SKILL.md
diff ~/projects/.github/.claude/skills/spectre-console/SKILL.md \
     ~/projects/FS.GG.SDD/.claude/skills/spectre-console/SKILL.md
# AGENTS.md references the body, does not restate it:
grep -n "spectre-console/SKILL.md" ~/projects/FS.GG.Governance/AGENTS.md ~/projects/FS.GG.SDD/AGENTS.md
```

**Expected**: no diff between copies; each `AGENTS.md` marker references the `spectre-console/SKILL.md`
path (single source), and the excluded repos (FS.GG.Rendering, FS.GG.Templates) carry no copy.

## S7 — No orphaned reference after rename (SC-008)

```sh
for r in ~/projects/.github ~/projects/FS.GG.Governance ~/projects/FS.GG.SDD; do
  echo "== $r =="; grep -rn "spectre-console-headless-fidelity" "$r" --include="*.md" --include="*.yml" --include="*.json" 2>/dev/null
done
```

**Expected**: any remaining mentions are intentional historical prose (e.g. the 092 spec describing
the prior name); no *path/link/marker* still points at the removed
`spectre-console-headless-fidelity` directory.

## S8 — Advisory: the skill gates nothing (SC-007)

```sh
grep -rni "spectre-console" .github/workflows/ 2>/dev/null || echo "no workflow dependency (expected)"
```

**Expected**: no CI workflow references the skill; removing or not reading it changes no
build/test/publish/merge outcome.

---

**Done when** S1–S8 pass: the skill reads as a first-class peer with a dual-trigger description
(S1), carries a working-depth Part A primer (S2) and code-grounded Part B conventions (S3–S4),
preserves the 092 diagnostic with provenance (S5), is byte-identical single-source across the
Spectre-using repos with a referencing `AGENTS.md` (S6), leaves no orphaned reference after the
rename (S7), and gates nothing (S8).
