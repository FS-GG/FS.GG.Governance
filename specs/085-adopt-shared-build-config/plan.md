# Implementation Plan: Adopt org-shared .NET build config

**Branch**: `main` (feature dir `specs/085-adopt-shared-build-config`) | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/085-adopt-shared-build-config/spec.md`

## Summary

Make FS.GG.Governance a conformant **sync-not-fork** consumer of the org-shared .NET build baseline (`FS-GG/.github` `dist/dotnet/`, ADR-0006, contract `shared-build-config`). Take the three canonical files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) verbatim; relocate every repo-specific MSBuild property into `Directory.Build.local.props` and every repo-specific `PackageVersion` into `Directory.Packages.local.props`; drop the local `FSharp.Core` pin (now org-baselined); and wire a drift gate into CI. **Zero change to build behavior, resolved package versions, or test results.**

**Technical approach**: the canonical `Directory.Build.props`/`Directory.Packages.props` each `<Import>` a repo-owned `*.local.props` **last** (last-write-wins), so the takeover is purely mechanical: split today's hand-authored files at the seam "org-canonical vs repo-specific", write the canonical halves verbatim from the source of truth, and move the rest into the two local files. The drift gate is **self-contained** in this repo's `gate.yml` (checkout `FS-GG/.github`, run `scripts/sync-build-config.sh --check .`) because the org reusable coherence workflow (`.github#18`) does not exist yet and is itself blocked by `FS.GG.Contracts`.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (unchanged — preserved via `Directory.Build.local.props`). The feature itself authors no F#: it edits MSBuild props, a tool manifest, and a CI workflow.

**Primary Dependencies**: MSBuild (Directory.Build.props / Central Package Management); the org `shared-build-config` contract (source of truth `FS-GG/.github` `dist/dotnet/`); `sync-build-config.sh` (drift check, lives in `FS-GG/.github`, consumed by checkout).

**Storage**: Files only — two managed `.props`, two repo-owned `*.local.props`, one `.config/dotnet-tools.json`, one CI workflow.

**Testing**: The repo's existing `dotnet fsi build.fsx` / `dotnet fsi build.fsx test` (full solution build + Expecto suite) as the no-behavior-change evidence; the drift check (`sync-build-config.sh --check .`, exit 0/1) as the conformance evidence. No new test project.

**Target Platform**: Linux CI (`ubuntu-latest`, GitHub Actions) + local dev (Linux/Windows/macOS).

**Project Type**: Build infrastructure / repository configuration (not a code feature).

**Performance Goals**: N/A (config change; the bounded `build.fsx` build time is unaffected).

**Constraints**: No behavior change (same warnings-as-errors, `--nowarn:57`/`WarnOn`, doc generation, `IsPackable=false`, `net10.0`); no package-version change (incl. `FSharp.Core 10.1.301`); the two managed files must stay byte-identical to the source of truth; locked restore (165 committed `packages.lock.json`) must keep passing in CI.

**Scale/Scope**: 5 files written (2 managed verbatim, 2 new local, 1 tool manifest) + 1 CI workflow edit. 165-project solution (165 committed `packages.lock.json`); no `src/`/`.fsi`/golden/baseline touched.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: N/A in the FSI sense — no F# surface is authored. The analogue is honored: spec first; the "semantic test" is the real build/test run + the real `--check` exit code (no internals asserted).
- **II. Visibility lives in `.fsi`**: **No `.fs`/`.fsi` touched** — no public surface change, no baseline re-bless. ✅ (FR-009/SC-007.)
- **III. Idiomatic simplicity**: The change is the plainest possible — split files at the org/repo seam, import last. No clever MSBuild. ✅
- **IV. Elmish/MVU boundary**: N/A — no stateful/I/O F# workflow. ✅
- **V. Test evidence is mandatory**: Real evidence only — actual `build.fsx` build+test green, actual locked restore, actual `--check` exit code; no synthetic fixtures. ✅
- **VI. Observability & safe failure**: The drift gate fails **loud** (non-zero exit, explicit message) on divergence; locked-restore failure already prints an actionable regenerate hint. ✅
- **Change Classification**: **Tier 2** (internal/build-infra, no behavioral change, no `.fsi`/baseline change) — consistent with how 078/079/080 build/test-support changes were classified. The cross-repo `shared-build-config` contract is **consumed, not changed** (already recorded in the org registry), so this feature produces **no registry or ADR change**.
- **Dependency minimization (Engineering Constraints)**: One new pinned dev tool, `fake-cli 6.1.4`, enters via the canonical `.config/dotnet-tools.json`. **Need**: byte-identical parity required by the `shared-build-config` drift gate (the file is part of the managed set). **Version-pinning**: pinned by the org source of truth. **Owner**: `FS-GG/.github`. It is unused by this repo's `build.fsx` (which shells `dotnet build`/`test` directly) — harmless. Justified. ✅

**Result: PASS** (pre-design and re-checked post-design — see end of Phase 1). No Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/085-adopt-shared-build-config/
├── plan.md              # This file
├── research.md          # Phase 0 output — decisions (CI wiring, lockfiles, file partition)
├── data-model.md        # Phase 1 output — config-artifact inventory & before/after partition
├── quickstart.md        # Phase 1 output — local adoption + validation walkthrough
├── contracts/
│   └── build-config-contract.md   # the managed-file set + drift-check + local-override boundary
└── checklists/
    └── requirements.md  # (from /speckit-specify)
```

### Source Code (repository root)

```text
.                                       # repo root — the unit of adoption
├── Directory.Build.props               # REPLACED: verbatim org canonical (imports *.local.props last)
├── Directory.Build.local.props         # NEW (repo-owned): TargetFramework, LangVersion, Nullable,
│                                        #   TreatWarningsAsErrors, WarnOn, --nowarn:57,
│                                        #   GenerateDocumentationFile, IsPackable=false
├── Directory.Packages.props            # REPLACED: verbatim org canonical (FSharp.Core baseline only)
├── Directory.Packages.local.props      # NEW (repo-owned): YamlDotNet, Spectre.Console, test pins
├── .config/
│   └── dotnet-tools.json               # NEW: verbatim org canonical (fake-cli 6.1.4)
└── .github/
    └── workflows/
        └── gate.yml                    # EDITED: add a build-config drift-check job

# UNCHANGED (verified by empty diff over these paths — SC-007):
src/**   tests/**   *.sln   samples/**   docs/**   *.fsi   **/*.fs   build.fsx
packages.lock.json (×165)   # must stay valid under locked restore (graph unchanged)
```

**Structure Decision**: The "project" is the repository root itself — adoption operates on the root `Directory.*` files, a new `.config/` tool manifest, and the existing CI workflow. No `src/` project, no test project, no F# module is created or modified. This is the minimal footprint that satisfies the `shared-build-config` contract while keeping every behavior local-overridden.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
