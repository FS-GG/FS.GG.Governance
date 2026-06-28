# Quickstart: Validate the fs-gg-ui rename guard

A run guide proving the guard works end-to-end. Details live in
[data-model.md](./data-model.md) and [contracts/rename-guard.contract.md](./contracts/rename-guard.contract.md).

## Prerequisites

- .NET SDK `net10.0`, git on `PATH` (used by `git ls-files`), a clean checkout of the feature branch.
- The new project `tests/FS.GG.Governance.RenameGuard.Tests` registered in `FS.GG.Governance.sln`.

## Run the guard

```bash
dotnet test tests/FS.GG.Governance.RenameGuard.Tests
```

**Expected**: all R1–R7 tests green. R1 demonstrates zero legacy version-machinery identifiers in
the tracked tree on the current `main` (SC-001) — the citable evidence for the cross-repo P5
checkbox (SC-005).

## Validate it can go red (SC-002)

The red path is exercised *inside* the suite (R3/R4/R5) by passing literal input strings to the pure
`scanText` matcher (real evidence of the matcher, not synthetic-evidence). Those literals live in the
guard's own test source, which the production scan excludes (`guardSelfExclusion`) so it never
self-trips — no committed tripwire elsewhere. To convince yourself manually:

```bash
# Add a real straggler to a tracked file, then run the production scan:
printf '<FsSkiaUiVersion>1.0.0</FsSkiaUiVersion>\n' >> Directory.Packages.props
git add Directory.Packages.props
dotnet test tests/FS.GG.Governance.RenameGuard.Tests   # R1 turns RED, naming the file + FsGgUiVersion
git restore --staged --worktree Directory.Packages.props # revert
dotnet test tests/FS.GG.Governance.RenameGuard.Tests   # GREEN again
```

The failure message names the file, line, offending identifier, and the canonical `fs-gg-ui`
replacement (R7).

## Validate provenance is untouched (SC-003)

```bash
git diff --stat main -- \
  .specify/memory/constitution.md docs/governance-design/index.md \
  docs/initial-design.md docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md
# Expected: empty (no lineage text rewritten); the guard passes with them present.
```

## Validate Tier 2 honored (SC-004)

```bash
git diff --stat main -- '*.fsi'        # expected: empty
git diff --stat main -- src/           # expected: empty (no production code)
# No surface-area baseline file appears in the diff.
```

## Close the checkbox (SC-005)

Cite the green `FS.GG.Governance.RenameGuard.Tests` run on the cross-repo P5 rename issue: the
governance-side repo carries no legacy version machinery, and the guard keeps it that way.
