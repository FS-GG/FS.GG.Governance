# Implementation Plan: Publish the Reference Gate Set as a Content Package

**Branch**: `086-reference-gate-set-package` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/086-reference-gate-set-package/spec.md`

## Summary

Publish the validated reference `.fsgg` gate set (`samples/sdd-reference-gate-set/.fsgg/` — `governance.yml`, `policy.yml`, `capabilities.yml`, `tooling.yml`) as a **content-only** NuGet package `FS.GG.Governance.ReferenceGateSet`, so the Templates overlay drift gate (Templates#14) has one published, versioned source of truth to `git diff --exit-code` against instead of a hand-copied overlay (US1, the H3 unblocker).

**Technical approach**: a single packaging project that packs the four files **directly from the sample directory** (no second copy — FR-002/FR-003) into `contentFiles/any/any/.fsgg/`, with **no assembly output** (`IncludeBuildOutput=false`) so installing it imposes no governance runtime dependency (FR-007). The package version is **derived deterministically from the four contained `schemaVersion` declarations** by a checked-in pack script (mirroring the existing `build.fsx` idiom, including a `--print-version` dry-run hook), and packing is **gated on the existing G1–G7 reference-set guard** so the shipped artifact is provably the tested artifact (FR-004). The package is registered as a versioned cross-repo contract in the org registry (`FS-GG/.github`, ADR-0001 — FR-008); the org-feed push depends on admin provisioning (.github#21) and is deferred — local/CI `dotnet pack` to `~/.local/share/nuget-local/` is the done-definition (per spec Distribution-scope assumption).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo baseline, `Directory.Build.local.props`). This feature authors **no public F# surface** — it adds an MSBuild packaging project (no `Compile` items), a checked-in `.fsx` pack/version script, and one guard test in F#.

**Primary Dependencies**: MSBuild SDK pack (`dotnet pack`); the four on-disk YAML files under `samples/sdd-reference-gate-set/.fsgg/` as the single source. No new third-party `PackageReference` in the package (content-only). The version-derivation script uses BCL only (`System.IO` + `System.Text.RegularExpressions` to read `schemaVersion:` lines) — no YamlDotNet needed.

**Storage**: Files only — the four reference YAMLs (unchanged, single-sourced), one `.fsproj` (no assembly), one pack/version `.fsx`, one guard test, one solution entry, one CI job; plus the cross-repo registry/compatibility entries in `FS-GG/.github`.

**Testing**: Expecto + YoloDev (repo-wide convention). Real evidence (Principle V): (a) the existing G1–G7 guard continues to load the on-disk `.fsgg` through the real `Config → Gates → Routing → Route → Enforcement` pipeline; (b) a new guard asserts the **packed `.nupkg` contents are byte-identical** to the on-disk samples (SC-002) and that the **derived version** matches the documented rule and **changes on a simulated `schemaVersion` bump** (SC-003), by invoking the pack script's `--print-version` hook.

**Target Platform**: Linux CI (`ubuntu-latest`, GitHub Actions) + local dev. Consumers are any NuGet restore host (Templates#14 drift gate).

**Project Type**: Packaging / distribution infrastructure (content package), not a code-library feature — closest precedent is spec 085 (build-infra) and 079 (the reference set + guard this feature publishes).

**Performance Goals**: N/A. Pack is a one-shot CI/local step; it reuses the bounded `build.fsx` test run for the guard.

**Constraints**: 0 drift between tested and shipped (byte-identical, FR-003/SC-002); content-only — **no `lib/`, no dependency group** in the `.nupkg` (FR-007/SC-005); a **single source** for the four files (no duplicated copy, FR-002); pack **must fail** when G1–G7 are red (FR-004); the materialized location must be **predictable and version-stable** (FR-005/FR-009); the package contract must be **registered** in the org registry before it is treated as consumable (FR-008/SC-006). Locked-restore graph (085) must stay green — the package adds no `PackageReference` to the solution's restore graph.

**Scale/Scope**: ~6 new/edited files in this repo (1 `.fsproj`, 1 `.fsx`, 1 test `.fs` (+ `Main.fs`/`.fsproj` if a new test project), 1 `.sln` edit, 1 `gate.yml` job) + 1 cross-repo registry PR in `FS-GG/.github`. **No `src/`, no `.fsi`, no surface baseline, no existing sample file is modified.**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: No public F# API is authored, so the FSI-sketch step is vacuous in the literal sense. The analogue is honored: spec first; the "semantic tests" are real — the existing G1–G7 load through the real pipeline, plus a new test that unzips the **real** packed `.nupkg` and compares bytes / asserts the **real** script-emitted version. No internals asserted. ✅
- **II. Visibility lives in `.fsi`, not `.fs`**: **No public `.fs` module is added** (the packaging project has no `Compile` items; the pack `.fsx` is a script, not a packed library; the guard test is internal). No `.fsi`, no surface-area baseline change. ✅
- **III. Idiomatic simplicity**: The plainest mechanism that works — pack the files from their existing location with `Pack="true"`/`PackagePath`; derive the version with a small regex over four `schemaVersion:` lines in a script shaped exactly like the existing `build.fsx` (with the same `--print-*` dry-run hook). No MSBuild regex gymnastics, no code generation, no new dependency. ✅
- **IV. Elmish/MVU boundary**: N/A — no multi-step stateful/I/O F# workflow. The pack script is a linear build step (read → compose → pack), and `Process.Start` I/O lives only at the script edge, mirroring `build.fsx`. ✅
- **V. Test evidence is mandatory**: Real evidence only — actual packed `.nupkg`, actual on-disk YAML, actual script-emitted version, actual G1–G7 run. **No synthetic fixtures.** The bump-distinguishability check (SC-003) mutates a *copy* of a sample on disk in a temp dir and re-derives — real I/O, no mock. ✅
- **VI. Observability & safe failure**: The pack step fails **loud and closed** — non-zero exit with an actionable message — when G1–G7 are red (FR-004) or when a `schemaVersion:` line is missing/unparseable (distinguishes malformed input from a tool defect, per Principle VI). ✅
- **Change Classification — Tier 1 (contracted change)**: this introduces a **new published package contract** (a new cross-repo surface), so it is Tier 1 and carries the registry + compatibility + ADR obligations (FR-008/SC-006). The usual Tier-1 `.fsi`/surface-baseline obligations are **vacuously satisfied**: the package ships **no assembly and no API surface** — the contract surface is the *package id + content layout + version-derivation rule*, which is what gets registered. Documented here rather than skipped.
- **Dependency minimization (Engineering Constraints)**: **zero new dependencies.** The package is content-only (no `PackageReference`, no `lib/`); the version script is BCL-only; the guard test reuses the existing Expecto stack. Pack output goes to the constitution-mandated `~/.local/share/nuget-local/`. ✅
- **Operating rule (governance MAY inspect, MUST NOT be required by rendering)**: unaffected — this publishes a governance *content* artifact; it imposes nothing on rendering and assumes no rendering identity. ✅

**Result: PASS** (pre-design; re-checked post-design at end of Phase 1 — still PASS). No Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/086-reference-gate-set-package/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions (content-pack mechanism, version rule, pack-gating)
├── data-model.md        # Phase 1 — bundle / package / version-rule / registry-entry entities
├── quickstart.md        # Phase 1 — produce, verify byte-identity, simulate a bump, consume
├── contracts/
│   └── reference-gate-set-package.contract.md   # package id, content layout, version rule, consumer location
└── checklists/
    └── requirements.md  # (from /speckit-specify, if present)
```

### Source Code (repository root)

```text
.                                            # repo root
├── packaging/
│   └── FS.GG.Governance.ReferenceGateSet/
│       └── FS.GG.Governance.ReferenceGateSet.fsproj   # NEW: content-only pack project.
│                                            #   IncludeBuildOutput=false (no lib/, FR-007);
│                                            #   IsPackable=true; PackageId=FS.GG.Governance.ReferenceGateSet;
│                                            #   <None Include="../../samples/sdd-reference-gate-set/.fsgg/*.yml"
│                                            #         Pack="true" PackagePath="contentFiles/any/any/.fsgg/" />
│                                            #   (single source — no copied second set, FR-002/FR-003)
├── pack-reference-gate-set.fsx             # NEW: derive version from the 4 schemaVersions, gate on
│                                            #   G1–G7, dotnet pack -> ~/.local/share/nuget-local/.
│                                            #   `--print-version` dry-run hook (mirrors build.fsx
│                                            #   `--print-command`) so the guard test asserts the
│                                            #   ACTUAL emitted version, not a scraped duplicate rule.
├── samples/sdd-reference-gate-set/.fsgg/   # UNCHANGED — the single source of the four files
│   ├── governance.yml  (schemaVersion 1)
│   ├── capabilities.yml (schemaVersion 2)
│   ├── policy.yml      (schemaVersion 1)
│   └── tooling.yml     (schemaVersion 1)
├── tests/FS.GG.Governance.ReferenceGateSet.Tests/   # EXTENDED (or sibling Pack.Tests project):
│   └── ReferenceGateSetPackageTests.fs     # NEW guard: packed .nupkg bytes == on-disk samples
│                                            #   (SC-002); derived version matches rule (SC-003);
│                                            #   simulated schemaVersion bump => distinguishable
│                                            #   version (SC-003); .nupkg has NO lib/ (SC-005).
├── FS.GG.Governance.sln                    # EDITED: add the packaging project
└── .github/workflows/gate.yml              # EDITED: add a `reference-gate-set-pack` job
                                             #   (guard -> pack -> assert byte-identity)

# CROSS-REPO (FS-GG/.github — filed via cross-repo-coordination skill, ADR-0001):
registry/dependencies.yml                    # ADD: FS.GG.Governance.ReferenceGateSet as a versioned
                                             #   contract consumed by FS.GG.Templates (Templates#14)
docs/registry/compatibility.md               # REGENERATE: compatibility projection includes the package
docs/decisions/ADR-00NN-*.md                 # ADD: decision record for the version-derivation rule
```

**Structure Decision**: a dedicated top-level `packaging/` project keeps the "this packs content, authors no F#" concern structurally separate from `src/` (F# libraries) and `samples/` (worked examples) — the same example/product separation 072 established with `samples/`. The four files stay in `samples/sdd-reference-gate-set/.fsgg/` as the **single source**; the packaging project references them in place. The pack/version logic lives in a repo-root `.fsx` consistent with the existing `build.fsx`, and the new evidence is a guard test alongside the existing G1–G7 guard. FR-008's registry/ADR artifacts live in `FS-GG/.github`, not this repo, and are executed through the **cross-repo-coordination** protocol.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
