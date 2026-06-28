# Implementation Plan: Governance-side fs-gg-ui rename guard

**Branch**: `083-fs-gg-ui-rename-guard` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/083-fs-gg-ui-rename-guard/spec.md`

## Summary

The org-wide P5 rename of the UI version machinery (`fs-skia-ui-*` → `fs-gg-ui-*`, ADR-0003)
has a governance-side checkbox: *verify no straggling reference to the legacy version machinery
remains in FS.GG.Governance, and keep it that way*. A full-tree scan confirms **zero** legacy
version-machinery identifiers here — the only `fs-skia-ui` text is legitimate historical-repository
provenance prose (the predecessor `EHotwagner/FS-Skia-UI` repo) in four documentary files.

The deliverable is therefore **verification made durable**: a self-contained Expecto regression
guard that (a) proves the absence of the legacy version-machinery token set today, (b) fails if any
is introduced tomorrow (pointing at the canonical `fs-gg-ui` replacement and the offending file),
and (c) does not flag the legitimate provenance. The guard scans the **git-tracked** tree only
(`git ls-files`, excludes `bin/`/`obj/`/untracked → deterministic), distinguishes machinery from a
repo name by a **suffix-anchored** match (the machinery tokens always carry a `-version`/`-bom`/
`Version`/`/v<n>` suffix; the bare repo name never does), and additionally allowlists the four
provenance files. The guard's own test directory is excluded from the production scan, because the
red-path tests carry the legacy tokens verbatim as the *input strings* they assert `scanText`
matches (with the regexes themselves assembled from fragments) — so the guard never self-trips on
its own scaffolding. Tier 2: no `src/` change, no `.fsi`, no surface-area baseline.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard).

**Primary Dependencies**: Expecto + `Microsoft.NET.Test.Sdk` + `YoloDev.Expecto.TestSdk` (the
repo-wide test convention under central package management); `FS.GG.Governance.Tests.Common` for
`RepositoryHelpers.repoRoot`. No new package. No production `ProjectReference`.

**Storage**: N/A. Reads the git-tracked working tree (text files) only.

**Testing**: Expecto via `dotnet test`. New project `FS.GG.Governance.RenameGuard.Tests`.

**Target Platform**: Linux/Windows dev + CI (the guard shells `git ls-files`; git is present in
every checkout/CI that already runs the suite).

**Project Type**: Single repository; test-only addition.

**Performance Goals**: One `git ls-files` + a streamed line scan over ~2,300 tracked files;
sub-second, well within the existing suite budget. No build/scaffold (unlike feature 078).

**Constraints**: Self-contained (FR-009 — no dependency on this repo's own governance platform);
deterministic on a clean checkout (FR-008); provenance byte-identical (FR-006).

**Scale/Scope**: 1 new test project (3 files: `.fsproj`, `Main.fs`, the guard `.fs`) + `.sln`
registration + a docs cross-link recording the closed checkbox. ~150–200 LOC of guard + tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic tests → Impl | ✅ N/A-adapted | No new public F# surface to sketch in FSI; the "API" is a test guard. Spec authored first; tests *are* the deliverable. |
| II. Visibility in `.fsi` | ✅ | No public module added → no `.fsi`, no baseline (Tier 2, FR-007). The guard helpers are private to the test module. |
| III. Idiomatic simplicity | ✅ | Plain functions, one regex set, `git ls-files` + line scan. No SRTP/reflection/type-providers/custom CE. The one regex per token-class is justified inline (case/separator variants). |
| IV. Elmish/MVU boundary | ✅ N/A | Principle IV names "scanning a repository" as an I/O workflow, but its trigger is *stateful* I/O (multi-step state, retries, persistence, recovery). This guard has none: a single one-shot `git ls-files` + read folded into one **pure** projection `(tracked tree, token set, allowlist) → Violation list`, with no durable `Model`. There is also no public `.fsi` surface (Tier 2) on which to host `Model`/`Msg`/`Effect`. The lone I/O edge is isolated in a thin reader (`scanTrackedTree`), kept out of the pure matcher (`scanText`) — satisfying IV's *spirit* (I/O as an auditable edge, pure core) without an MVU loop. |
| V. Test evidence is mandatory | ✅ | Real evidence throughout. R1/R2 scan the **real** tracked tree (no mocks). The red-path tests (R3–R7) pass **literal input strings to the pure `scanText`** — that is the matcher's *real* domain input, so these are ordinary real-evidence unit tests, **not** synthetic-evidence substitutes; no `Synthetic` token applies (Principle V's disclosure machinery is for faked dependencies, not literal arguments to a pure function). "No committed tripwire" is the only sense of *synthetic* here: the red-path literals live in the guard's own (scan-excluded) test source, never as a tracked fixture the production scan would read (edge case "Guard's own fixtures"). |
| VI. Observability & safe failure | ✅ | Failure diagnostic names the file + the offending legacy identifier + the canonical `fs-gg-ui` replacement (FR-004/FR-005). Distinguishes a real straggler from provenance via the suffix-anchor + allowlist (FR-003). |

**Change Classification**: Tier 2 (internal/test change). No public API, `.fsi`, or baseline
touched. Gate: PASS. No Complexity Tracking entries required.

**Operating rule**: the guard inspects only this repo's own tree; it makes no assumption about
rendering package IDs/paths and requires no external governance platform (FR-009). PASS.

## Project Structure

### Documentation (this feature)

```text
specs/083-fs-gg-ui-rename-guard/
├── plan.md              # This file
├── research.md          # Phase 0 output — the four design decisions
├── data-model.md        # Phase 1 output — the token sets + allowlist as data
├── quickstart.md        # Phase 1 output — run/validate the guard
├── contracts/
│   └── rename-guard.contract.md   # the guard's behavioral contract (R1–R7)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
tests/
└── FS.GG.Governance.RenameGuard.Tests/          # NEW — self-contained guard project
    ├── FS.GG.Governance.RenameGuard.Tests.fsproj # Expecto + Tests.Common only; no prod ProjectReference
    ├── RenameGuardTests.fs                        # the guard: token sets, suffix-anchored scan,
    │                                              #   tracked-tree reader, allowlist, R1–R7 tests
    └── Main.fs                                     # Expecto entry point (mirrors ReferenceGateSet.Tests)

FS.GG.Governance.sln                               # register the new test project (tests folder)

docs/reports/2026-06-27-fs-gg-ui-rename-guard-governance-checkbox.md  # NEW — a short note recording
                                                   #   the closed governance-side P5 checkbox and
                                                   #   pointing at the guard as durable evidence (SC-005)
```

**Structure Decision**: A **dedicated new test project** `FS.GG.Governance.RenameGuard.Tests`,
mirroring feature 079's `FS.GG.Governance.ReferenceGateSet.Tests` precedent (one guard → one
project). This maximizes self-containment (FR-009): the guard needs **no production
`ProjectReference`** — it only reads files and shells `git` — so it references only
`FS.GG.Governance.Tests.Common` (for `RepositoryHelpers.repoRoot`) plus the three test packages.
Extending an existing repo-hygiene project (e.g. `DocsChecks.Tests`) was rejected because those
carry production references and a docs-checks domain identity unrelated to a rename guard; a
dedicated project keeps the guard legible and independently runnable. See research.md §D1.

## Complexity Tracking

> No Constitution Check violations. Section intentionally empty.
