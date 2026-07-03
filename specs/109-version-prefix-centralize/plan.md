# Implementation Plan: Centralize an intentional VersionPrefix for the baseline-only projects

**Branch**: `109-version-prefix-centralize` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/109-version-prefix-centralize/spec.md`

## Summary

Replace the **accidental `1.0.0` MSBuild default** carried by the 104 version-less `.fsproj` files
with a single, intentional `<VersionPrefix>0.1.0</VersionPrefix>` in the repo-owned, drift-exempt
`Directory.Build.local.props` (deferred low tail of #53 / epic #44, closes #63). `0.1.0` is the
repo's established baseline (the ~66 explicitly-versioned libraries already pin it). Explicit
`<Version>` pins win over the prefix, so the CLI (`1.2.0`) and Kernel (`0.1.1`) are untouched.

Phase 0 established that this regresses **no published artifact**: the only two published packages —
the CLI `fsgg-governance` (published version read from its fsproj `<Version>` by `publish.yml`) and
`ReferenceGateSet` (version `schemaVersion`-derived and injected via `pack-reference-gate-set.fsx
-p:Version=…`) — both derive their version independently of the centralized default. The three
`PackAsTool` commands that move `1.0.0 → 0.1.0` (`fsgg`, `fsgg-evidence`, `fsgg-cache-eligibility`)
are **unpublished** and assert-free, so aligning them to `0.1.0` is the intended correction, not a
regression (user-ratified decision D2). Acceptance is a graph-wide before/after
`dotnet msbuild -getProperty:Version` diff, a byte-identical org-synced-config `git diff`, and a green
full build+test.

## Technical Context

**Language/Version**: F# on .NET `net10.0`. The deliverable is **one MSBuild property** in an existing
XML props file plus its explanatory comment; no `.fs`/`.fsi` changes.

**Primary Dependencies**: MSBuild `Version`/`VersionPrefix` inheritance semantics; the existing
`Directory.Build.local.props` import chain; `dotnet msbuild -getProperty:Version` (verification).

**Storage**: N/A.

**Testing**: Real-evidence verification via before/after effective-version map (quickstart Steps 0–2)
plus the existing full suite staying green. **No new test project** — there is no drift-prone allowlist
to guard (research D4); the invariant is checked directly and cheaply by the version-map diff at review
time.

**Target Platform**: local `dotnet` + GitHub Actions `ubuntu-latest` (gate.yml build/test; publish.yml
unaffected).

**Performance Goals**: N/A (build-config hygiene).

**Constraints**:
- **No edits to org-synced build config** — `Directory.Build.props` / `Directory.Packages.props` /
  `.config/dotnet-tools.json` stay byte-identical (the version property goes in the drift-exempt
  `Directory.Build.local.props`).
- **No published-version regression** — the CLI and ReferenceGateSet published versions are invariant
  by construction (both version sources bypass the default).
- **No product behavior / API / JSON change** — Tier 2; existing surface baselines and semantic tests
  untouched and green.
- **Tight scope** — do **not** strip the explicit `<Version>` from the ~66 already-pinned libraries
  (scope creep + api-gate risk); only the version-less projects inherit the new prefix.

**Scale/Scope**: 1 edited file (`Directory.Build.local.props`) · 104 version-less projects re-pointed
from `1.0.0`→`0.1.0` by inheritance · 0 new projects · 0 code files · README/docs unchanged (the
declaration-site comment is the only prose).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — Tier 2 (internal build-config hygiene).** No public API, no `.fsi`, no JSON
contract, no behavioral change. No surface baseline moves.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | N/A to a build property — there is no F# surface. The "contract" is the version-baseline invariant ([contracts/version-baseline.md](./contracts/version-baseline.md)), verified by the before/after map. |
| **II. Visibility lives in `.fsi`** | Untouched — no `.fs`/`.fsi` changes. |
| **III. Idiomatic simplicity** | Central. One property beats 104 per-project edits or MSBuild conditionals; the value matches the existing baseline rather than inventing a scheme. |
| **IV. Elmish/MVU boundary for I/O** | N/A — declarative build metadata, no stateful workflow or I/O. |
| **V. Test evidence is mandatory** | Real evidence: graph-wide before/after `-getProperty:Version` diff + green full suite + byte-identical org-config diff. No synthetic evidence. |
| **VI. Observability & safe failure** | The declaration-site comment records the value and its no-regression rationale so a future reader cannot silently "fix" it into a published regression. |

**Engineering Constraints**: `net10.0` ✅; no edits to org-synced props (prefix → `Directory.Build.local.props`) ✅; no new dependency ✅; nothing rendering-specific ✅; `FS.GG.Governance.*` identity preserved ✅; ADR-0003 package-ID permanence untouched (this changes `Version`, not any `PackageId`) ✅.

**Result: PASS.** No violations; Complexity Tracking intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/109-version-prefix-centralize/
├── plan.md              # This file
├── research.md          # Phase 0 — D1 value, D2 tool alignment, D3 published invariance, D4 verification
├── data-model.md        # Phase 1 — the build-graph entities the change acts on + the evidence map
├── contracts/
│   └── version-baseline.md   # C1–C6 — the authoritative version-baseline invariant the quickstart asserts
├── quickstart.md        # Phase 1 — before/after map, published-invariance, org-config diff, build+test
└── checklists/
    └── requirements.md  # spec quality checklist (all pass)
```

### Source Code (repository root)

```text
Directory.Build.local.props     # EDIT (only change): add a commented <PropertyGroup> with
                                #   <VersionPrefix>0.1.0</VersionPrefix> — the single centralized source.
                                #   Repo-owned, imported last, drift-exempt.

# Inherit the new prefix (no per-file edits — behavior via inheritance):
src/FS.GG.Governance.RouteCommand/            # fsgg               1.0.0 → 0.1.0 (unpublished)
src/FS.GG.Governance.EvidenceCommand/         # fsgg-evidence      1.0.0 → 0.1.0 (unpublished)
src/FS.GG.Governance.CacheEligibilityCommand/ # fsgg-cache-elig.   1.0.0 → 0.1.0 (unpublished)
packaging/FS.GG.Governance.ReferenceGateSet/  # build-time prop → 0.1.0; PUBLISHED version unchanged
… ~100 tests/adapters/sample (IsPackable=false)  # 1.0.0 → 0.1.0 (never packs; harmless)

# Untouched (explicit <Version> wins over the prefix):
src/FS.GG.Governance.Cli/                     # 1.2.0  (also = its published version)
src/FS.GG.Governance.Kernel/                  # 0.1.1
… ~65 other explicitly-pinned libraries       # 0.1.0

# Untouched org-synced config (byte-identical):
Directory.Build.props · Directory.Packages.props · .config/dotnet-tools.json
```

**Structure Decision**: A single property in `Directory.Build.local.props` realizes the whole feature
through MSBuild inheritance; no project files are individually edited, no new test project is created
(research D4). The already-pinned libraries and the two published packages are deliberately left alone,
keeping the diff to one line + a comment and the blast radius provable by a version-map diff.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
