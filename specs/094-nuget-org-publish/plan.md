# Implementation Plan: Publish Governance packages to public nuget.org

**Branch**: `094-nuget-org-publish` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/094-nuget-org-publish/spec.md`

## Summary

Add a **public nuget.org** publish leg to this repo's `publish.yml`, alongside the existing org GitHub Packages feed push, for both publishable governance artifacts — the `FS.GG.Governance.Cli` tool and the content-only `FS.GG.Governance.ReferenceGateSet`. Authentication is **Trusted Publishing (OIDC)** per ADR-0013 (no long-lived key): the publish jobs request `id-token: write`, run `NuGet/login@v1` to mint a short-lived key, then `dotnet nuget push … --source https://api.nuget.org/v3/index.json --skip-duplicate`. The push is byte-identical (same `.nupkg`, no re-pack), org-feed-first, gated behind the existing quality gates, and fails closed (a missing trust policy 401s). Each package gains the ADR-0012 §5 listing metadata (license, readme, repository URL, icon). Because the reference gate set has **no feed-publish path today** (`gate.yml` only packs it into a throwaway temp feed to guard byte-identity), this feature also establishes its publish to both feeds — and it must run from `publish.yml`, the single workflow file the Governance trust policy is registered against.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (the existing `pack-reference-gate-set.fsx` is **invoked as-is, not edited**; **no new or changed product F# code**). The deliverables are GitHub Actions YAML + MSBuild project/props metadata.

**Primary Dependencies**: GitHub Actions (`actions/checkout@v4`, `actions/setup-dotnet@v4`, `NuGet/login@v1`); the `dotnet` CLI (`pack`, `nuget push`, `msbuild -getProperty`); the checked-in `pack-reference-gate-set.fsx` (already the single packer for the gate set).

**Storage**: N/A. Distribution targets are two NuGet feeds: org GitHub Packages (`https://nuget.pkg.github.com/FS-GG/index.json`) and public nuget.org (`https://api.nuget.org/v3/index.json`).

**Testing**: CI real-evidence. The existing pre-publish gates stay authoritative — `cli-tests` + `enforcement-smoke` (green-by-omission) for the CLI, and the G1–G7 reference-set guard for the gate set. New evidence: a real release run shows both packages accepted by both feeds; a dry-run dispatch shows no push; a run without a trust policy 401s and publishes nothing.

**Target Platform**: GitHub Actions `ubuntu-latest` (producer). Consumers install from the default public nuget.org feed with no FS-GG credential.

**Project Type**: Single-repo CI/release + packaging change.

**Performance Goals**: N/A (release-time workflow).

**Constraints**:
- **Trusted Publishing / OIDC only** (ADR-0013) — no `NUGET_ORG_API_KEY`, no stored push secret; login + push live in `publish.yml` (never a reusable workflow — NuGet/login#6).
- **Byte-identical, no re-pack** — the same `.nupkg` pushed to both feeds (ADR-0012 §3).
- **Org-feed-first ordering**, then nuget.org (ADR-0012 §4).
- **Fail-closed** — a missing policy 401s the run; a dry-run never pushes (FR-006/FR-008).
- **Idempotent** — `--skip-duplicate` on both feeds (version immutability; FR-007).
- **No edits to org-synced build config** — `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json` are drift-checked for byte identity; repo-owned metadata goes in `Directory.Build.local.props` and the `.fsproj` files (constitution Engineering Constraints; spec 088 D6).
- **IDs and versions unchanged** — package IDs permanent (ADR-0003); CLI version = its fsproj `<Version>`; gate-set version = schema-derived (ADR-0007).

**Scale/Scope**: 2 packages · 1 workflow file (`publish.yml`) · 2 `.fsproj` + `Directory.Build.local.props` metadata · README + icon packaging assets · this feature's contracts. Sibling producers (FS.GG.SDD#56, FS.GG.Rendering#40) are out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — Tier 1 (contracted change).** This alters a **package distribution contract** (adds a public channel + listing metadata for two packages) and advances the cross-repo `nuget-org-published` coherence. It does **not** change any public F# API surface, so `.fsi` files and surface-area baselines are intentionally **untouched** — appropriate here, not a defect, because no module surface changes.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | N/A — no F# public surface added. The "design through use" analogue is the workflow contract in `contracts/`, validated by a real CI release run. |
| **II. Visibility lives in `.fsi`** | N/A — no new/changed modules; no baselines touched. |
| **III. Idiomatic simplicity** | Holds. `pack-reference-gate-set.fsx` is reused as-is (pack-only; `--output`/`--no-gate` flags already exist); the workflow does the push. No new abstractions. |
| **IV. Elmish/MVU boundary for I/O** | N/A to product code. The I/O (feed pushes) is CI-orchestrated; the pack script already confines `Process.Start` to its edge. No new stateful F# workflow. |
| **V. Test evidence is mandatory** | Satisfied by **real** CI evidence: the existing gates remain hard pre-push gates, and acceptance is an actual dual-feed publish + a fail-closed run + a dry-run. No synthetic evidence. |
| **VI. Observability & safe failure** | Central: fail-closed on missing policy (loud 401), `--skip-duplicate` idempotency, an explicit "packed but not pushed" dry-run notice, and an assert-the-package-was-produced guard so a green gate + empty pack cannot report success. |

**Engineering Constraints**: `net10.0` ✅; local pack honors `~/.local/share/nuget-local/` ✅; no edits to org-synced props (metadata in `Directory.Build.local.props` + `.fsproj`) ✅; dependency-minimalism — no new package dependency on either artifact (`NuGet/login` is a CI action, not a `PackageReference`) ✅; genericity — nothing rendering-specific introduced ✅.

**Result: PASS.** No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/094-nuget-org-publish/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (auth, ordering, metadata placement, gate-set path)
├── data-model.md        # Phase 1 — the "publishable artifact" model (id, version source, feeds, gates, metadata)
├── quickstart.md        # Phase 1 — runnable validation scenarios (dry-run, dual-feed, fail-closed, consumer install)
├── contracts/
│   ├── publish-workflow-nuget-org.md    # extended publish.yml contract (nuget.org leg, trusted publishing, jobs)
│   └── package-listing-metadata.md      # required §5 listing metadata per package
└── checklists/
    └── requirements.md  # spec quality checklist (from /speckit-specify)
```

### Source Code (repository root)

```text
.github/workflows/
└── publish.yml                          # EXTEND: add nuget.org leg (id-token: write + NuGet/login + push)
                                         #   to the Cli publish job; add a reference-gate-set publish job
                                         #   (pack via pack-reference-gate-set.fsx → push both feeds)

src/FS.GG.Governance.Cli/
└── FS.GG.Governance.Cli.fsproj          # ADD listing metadata (or inherit shared bits from local.props)

packaging/FS.GG.Governance.ReferenceGateSet/
└── FS.GG.Governance.ReferenceGateSet.fsproj  # ADD listing metadata (license/readme/repo/icon)

Directory.Build.local.props              # ADD shared, packable-scoped listing metadata
                                         #   (RepositoryUrl, PackageLicenseExpression=MIT,
                                         #    PackageProjectUrl, PackageIcon, Authors) — repo-owned, drift-exempt

<packaging assets>                       # ADD a packed README + an icon (PackageReadmeFile / PackageIcon targets)

pack-reference-gate-set.fsx              # REUSE as-is (pack-only; --output/--no-gate present).
                                         #   The workflow drives pack + push; no behavior change expected.
```

**Structure Decision**: Single-repo CI/packaging change. All nuget.org publishing for Governance is consolidated into `publish.yml` (the workflow file the Governance Trusted Publishing policy is registered against per FS-GG/.github#103), because nuget.org matches the OIDC token to the repo **and workflow file** where `NuGet/login` runs (ADR-0013). Shared listing metadata lives in the drift-exempt `Directory.Build.local.props`; package-specific bits stay in each `.fsproj`. The org-synced `Directory.Build.props` is not touched.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
