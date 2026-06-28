# Phase 0 Research: Governance-side fs-gg-ui rename guard

All four open questions the spec leaves to the plan are resolved below. There were no
`NEEDS CLARIFICATION` markers in Technical Context after this pass.

## D1 — Guard home: dedicated project vs. extending a hygiene test

- **Decision**: A new dedicated test project `FS.GG.Governance.RenameGuard.Tests` under `tests/`,
  mirroring feature 079's `FS.GG.Governance.ReferenceGateSet.Tests` (one guard → one project).
- **Rationale**: Maximizes self-containment (FR-009). The guard reads files + shells `git`; it
  needs **no production `ProjectReference`** at all, only `FS.GG.Governance.Tests.Common` for
  `RepositoryHelpers.repoRoot` and the three standard test packages. A dedicated project is
  independently runnable (`dotnet test tests/FS.GG.Governance.RenameGuard.Tests`) and carries a
  single, legible purpose — the rename guard — matching SC-005's "cite the passing guard."
- **Alternatives considered**:
  - *Extend `DocsChecks.Tests`* — rejected: it references production docs-checks code and owns an
    unrelated domain; folding a rename guard in muddies both and adds a needless prod edge.
  - *Extend `SurfaceChecks.Tests`* — rejected: that guard is about `.fsi` surface drift, a Tier-1
    concern; this is a text scan with no surface relationship.
  - *A shell script in CI* — rejected: violates "runs under the standard repo test command"
    (FR-009) and the repo's Expecto regression-guard convention; not citable as a green test.

## D2 — Scanned surface: how to enumerate deterministically

- **Decision**: Shell out to `git ls-files` (run with `WorkingDirectory = RepositoryHelpers.repoRoot`)
  to obtain the tracked file list; scan each as UTF-8 text, line by line.
- **Rationale**: FR-008 demands the **git-tracked** tree, excluding `bin/`/`obj/`/untracked, so the
  guard is deterministic on a clean checkout. `git ls-files` is exactly that surface — no manual
  `bin`/`obj` exclusion glob to drift. Git is present wherever the suite already runs. The process
  call is the single I/O edge, isolated in a thin reader (Principle IV spirit); a `git` failure
  fails the test loudly (Principle VI) rather than silently scanning nothing.
- **Alternatives considered**:
  - *`Directory.EnumerateFiles` from repo root* — rejected: includes `bin/`/`obj/` build outputs
    and untracked scratch files, making the scan non-deterministic and prone to false positives
    from generated artifacts (the exact thing FR-008 excludes).
  - *`libgit2sharp`* — rejected: a new dependency for what one `git ls-files` call does; violates
    dependency-minimalism and FR-009's self-containment.
- **Binary/large-file note**: skip files by extension/NUL-byte sniff is unnecessary — the token
  regex is ASCII-anchored and binary blobs simply won't match; but the reader will tolerate a
  read failure on a non-UTF-8 file by treating it as no-match (logged), never throwing.

## D3 — Token discrimination: machinery vs. the historical repo name

- **Decision**: Match on a **suffix-anchored** set of patterns, one per token class, each requiring
  the version-machinery suffix that the bare repo name never carries:
  - CPM property: `Fs[ _.\-]?Skia[ _.\-]?Ui[ _.\-]?Version` (catches `FsSkiaUiVersion`,
    `Fs_Skia_Ui_Version`, etc.) — anchored on the trailing `Version`.
  - Contract ids: `fs[ _.\-]skia[ _.\-]ui[ _.\-](version|bom)` (catches `fs-skia-ui-version`,
    `fs_skia_ui_bom`, `fs.skia.ui.version`) — anchored on `version`/`bom`.
  - Snapshot-tag namespace: `fs[ _.\-]skia[ _.\-]ui/v[0-9]` (catches `fs-skia-ui/v1`,
    `fs-skia-ui/v*` in a tag context) — anchored on `/v<digit>` (and the literal `fs-skia-ui/v*`).
  - All case-insensitive.
- **Rationale**: Verified against the real tree — the four provenance files contain **only** the
  bare `FS-Skia-UI` / `EHotwagner/FS-Skia-UI` / `fs-skia-ui` (in repo URLs), **never** a
  `-version`/`-bom`/`/v<n>` suffix. So the suffix anchor makes prose non-matching *by
  construction*: `EHotwagner/FS-Skia-UI/blob/main/...` does not match `fs-skia-ui/v[0-9]` (the
  path segment after the slash is `blob`, not `v<digit>`). This is the load-bearing scoping
  insight (edge case: "case/separator variants caught; `FS-Skia-UI` in prose not").
- **Alternatives considered**:
  - *Match bare `fs-skia-ui` everywhere + allowlist the 4 files* — rejected as the **primary**
    mechanism: it would permanently red-flag any future legitimate prose mention of the predecessor
    repo and forces allowlist churn; the suffix anchor is the correct discriminator. The allowlist
    is retained as defense-in-depth (D4), not as the discriminator.

## D4 — Provenance allowlist: belt-and-suspenders + future-proofing

- **Decision**: Keep an explicit allowlist of the four provenance files (`.specify/memory/
  constitution.md`, `docs/governance-design/index.md`, `docs/initial-design.md`,
  `docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md`). A match inside an
  allowlisted file is not a violation. The allowlist is a named, commented constant in the guard.
- **Rationale**: FR-003 mandates it. Although the suffix anchor (D3) already makes today's prose
  non-matching, the allowlist (a) documents the boundary for a reviewer (SC-006 — "distinguish a
  straggler from provenance by reading the guard and its allowlist alone"), and (b) covers the
  edge case where provenance prose is later reworded near a machinery-looking token. It is
  updatable when provenance moves files (edge case "Provenance allowlist drift") without weakening
  the legacy ban — the ban is the regex; the allowlist only narrows *where* prose is tolerated.
- **Guard's own fixtures (edge case) — resolved to BOTH mechanisms**: the red-path tests (R3–R5)
  pass **literal input strings** to the **pure matcher** `scanText` (real evidence of the matcher,
  not synthetic-evidence). Those literals are necessarily legacy tokens written verbatim into the
  tracked test source, so two distinct self-trip sources must be handled, and the design commits to
  **both** (not "either/or" — see data-model §GuardSelfExclusion, contract R6):
  1. **Pattern definitions**: the `ForbiddenToken.Pattern` regexes are assembled from string
     *fragments*, so the patterns themselves are not literal suffix-bearing tokens.
  2. **Test-input literals**: the guard's own test directory
     `tests/FS.GG.Governance.RenameGuard.Tests/` is added to a `guardSelfExclusion` set that
     `scanTrackedTree` skips (distinct from the four-file provenance allowlist). Fragment-assembly
     alone does **not** cover the input literals — this exclusion is what keeps the production scan
     (R1) from self-tripping, and prevents the feature's own test file from introducing the very
     tokens FR-001 forbids. No committed tripwire exists outside that excluded directory.

## Consolidated outcome

- One new self-contained Expecto project (D1), scanning the `git ls-files` surface (D2), with a
  suffix-anchored forbidden-token set that is prose-safe by construction (D3) plus an explicit
  four-file provenance allowlist for documentation and drift-safety (D4). No production code,
  `.fsi`, or baseline touched (Tier 2). Real evidence throughout (Principle V); the red path passes
  literal input strings to the pure matcher (real evidence, not synthetic-evidence), with those
  literals isolated in the `guardSelfExclusion`-skipped test source so the production scan never
  self-trips.
