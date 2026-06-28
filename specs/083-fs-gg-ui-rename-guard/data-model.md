# Phase 1 Data Model: Governance-side fs-gg-ui rename guard

This feature has no domain entities in the production sense (Tier 2, no `src/` change). The
"model" is the set of constants and the small value types the guard is built from. They live as
private bindings inside `RenameGuardTests.fs`.

## Entity: ForbiddenToken (the legacy version-machinery set)

The token classes the guard forbids, each with the suffix-anchored regex that distinguishes
machinery from the bare repo name, and the canonical replacement surfaced on failure (FR-004).

| Class | Legacy form(s) | Suffix-anchored pattern (case-insensitive) | Canonical replacement |
|-------|----------------|---------------------------------------------|-----------------------|
| CPM property | `FsSkiaUiVersion` (+ separator variants) | `Fs[ _.\-]?Skia[ _.\-]?Ui[ _.\-]?Version` | `FsGgUiVersion` |
| Contract id (version) | `fs-skia-ui-version` (+ variants) | `fs[ _.\-]skia[ _.\-]ui[ _.\-]version` | `fs-gg-ui-version` |
| Contract id (bom) | `fs-skia-ui-bom` (+ variants) | `fs[ _.\-]skia[ _.\-]ui[ _.\-]bom` | `fs-gg-ui-bom` |
| Snapshot-tag namespace | `fs-skia-ui/v*`, `fs-skia-ui/v1` | `fs[ _.\-]skia[ _.\-]ui/v([0-9]|\*)` | `fs-gg-ui/v*` |

**Validation rule**: a tracked file that is neither under `ProvenanceAllowlist` nor under
`GuardSelfExclusion`, containing ≥1 match of any pattern, is a violation. The bare repo name
`FS-Skia-UI` / `EHotwagner/FS-Skia-UI` matches **no** pattern (no
`version`/`bom`/`/v<n>` suffix) — verified against the real tree.

**Representation**: a list of records `{ Class: string; Pattern: Regex; Replacement: string }`
so a failure can name the class and its replacement. The patterns are assembled from fragments
(not written as one literal suffix-bearing token) so the guard source does not self-match (R6).

## Entity: ProvenanceAllowlist (legitimate historical references)

The four documentary files whose lineage prose legitimately names the predecessor repository and
must stay byte-identical (FR-006). Repo-relative, forward-slash paths.

```
.specify/memory/constitution.md
docs/governance-design/index.md
docs/initial-design.md
docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md
```

**Validation rule**: a match found inside an allowlisted path is not a violation. The allowlist
narrows *where* prose is tolerated; it never disables a pattern. Updatable when provenance moves
(edge case "allowlist drift") without weakening the ban.

## Entity: GuardSelfExclusion (the guard's own test sources)

The guard's own test source — `tests/FS.GG.Governance.RenameGuard.Tests/` — is excluded from the
**production** scan, because the red-path tests (R3–R5) necessarily carry **literal** legacy tokens
(`<FsSkiaUiVersion>…`, `fs-skia-ui-version`, `Fs_Skia_Ui_Version`, `FS-SKIA-UI/V2`, …) as the *input
strings* they assert `scanText` matches. Those literals live in a tracked file, so without this
exclusion the production `scanTrackedTree` would read them and self-trip R1 (and the feature would
itself introduce the tokens FR-001 forbids). Repo-relative, forward-slash path prefix:

```
tests/FS.GG.Governance.RenameGuard.Tests/
```

**Two complementary mechanisms keep the guard from self-matching (R6), addressing two distinct
sources:**

1. **Pattern source** — the `ForbiddenToken.Pattern` regexes are assembled from string *fragments*
   (not written as one literal suffix-bearing token), so the pattern *definitions* themselves are
   not legacy tokens.
2. **Test-input source** — the red-path *input literals* cannot be fragment-assembled away without
   making the tests illegible, so the guard's own test directory is excluded from the production
   scan (the "allowlisted-by-construction" path named in contract R6).

This exclusion is distinct from `ProvenanceAllowlist`: provenance is *documentary prose*; this is
the *guard's own scaffolding*. Both are skipped by `scanTrackedTree`; neither disables a pattern.

## Entity: ScanResult (per-run output of the pure matcher)

```
type Violation = { File: string; Line: int; Class: string; Matched: string; Replacement: string }
```

- `scanText: path:string -> contents:string -> Violation list` — the **pure** matcher (no I/O);
  applies every `ForbiddenToken.Pattern` line by line; returns one `Violation` per match. Used
  directly by the red-path tests with literal input strings — real evidence of the pure matcher,
  not synthetic-evidence; the literals live in the `GuardSelfExclusion`-skipped test source, so no
  committed tripwire.
- `scanTrackedTree: unit -> Violation list` — the I/O edge: `git ls-files` from `repoRoot`, read
  each file that is **neither** under `ProvenanceAllowlist` **nor** under `GuardSelfExclusion`, fold
  `scanText`. A `git` failure throws (loud, Principle VI).

**Failure-message shape** (FR-005/SC-006): `"<File>:<Line> contains legacy <Class> '<Matched>' —
rename to the fs-gg-ui root (<Replacement>)."`

## State transitions

None. The guard is a single pure projection from (tracked tree, token set, allowlist) → pass/fail.
No persistence, no MVU workflow (Constitution IV N/A — see plan Constitution Check).
