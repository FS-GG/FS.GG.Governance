# Feature Specification: Adopt org-shared .NET build config

**Feature Branch**: `main` (feature dir `specs/085-adopt-shared-build-config`)

**Created**: 2026-06-28

**Status**: Draft

**Change Classification**: **Tier 2** (build-infrastructure only — no `.fsi`/baseline change, no behavioral change; the cross-repo `shared-build-config` contract is consumed, not changed). See plan.md "Constitution Check".

**Input**: User description: "next governance item on the project coordination board." → resolved to Coordination board item **FS-GG/FS.GG.Governance#16** (status `Ready`): *"H3 · governance — Adopt shared-build-config (gate already GITHUB_ACTIONS; drop local FSharp.Core pin)"*. Source of truth: `FS-GG/.github` `dist/dotnet/` (ADR-0006, `.github#19`, merged). Contract: `shared-build-config`.

## Overview

The FS-GG org has established a single canonical .NET build baseline — `Directory.Build.props`, `Directory.Packages.props`, and `.config/dotnet-tools.json` living in `FS-GG/.github` `dist/dotnet/` (ADR-0006, contract `shared-build-config`) — that every .NET repo is meant to consume **by sync, not by fork**. The canonical files import a repo-owned `*.local.props` so repo-specific settings survive a re-sync, and a drift check (`sync-build-config.sh --check`) fails CI when a managed file is hand-edited or stale.

Today FS.GG.Governance hand-authors its `Directory.Build.props` and `Directory.Packages.props`, and pins `FSharp.Core` locally. This feature makes Governance a conformant consumer of the shared config: it takes the three managed files verbatim, relocates every repo-specific setting into the two `*.local.props` override files, drops its now-redundant local `FSharp.Core` pin (the org baseline owns it), and wires the drift check into CI — all with **zero change to build behavior, package versions, or test results**.

This is the Governance row of the org-wide H3 adoption sequence; SDD and Rendering carry the analogous rows. It does not touch any `src/` production code or public surface.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Adopt the canonical build config with no behavior change (Priority: P1)

As a Governance maintainer, I want the repo's build configuration to come verbatim from the org source of truth — with all repo-specific settings preserved in local override files — so that the build and the full test suite behave exactly as before while the shared baseline is single-sourced.

**Why this priority**: This is the core of the item. Without it there is no adoption. It is the slice that delivers the coherence value (one org baseline) and must be safe (identical behavior) before anything else.

**Independent Test**: Run the repo's full build and test suite (`dotnet fsi build.fsx` and `dotnet fsi build.fsx test`) before and after the change; every project compiles and every test passes with the same counts, and the resolved package versions (including `FSharp.Core`) are unchanged.

**Acceptance Scenarios**:

1. **Given** the repo before adoption (hand-authored managed files, local `FSharp.Core` pin), **When** the canonical files are synced in and repo-specific settings are moved to `Directory.Build.local.props` / `Directory.Packages.local.props`, **Then** the full solution builds and the entire test suite passes with the same results as before.
2. **Given** the adopted repo, **When** restore runs, **Then** `FSharp.Core` resolves to the same version as before (`10.1.301`) and is declared in exactly one place (the org baseline) — no duplicate-`PackageVersion` restore error.
3. **Given** the adopted repo, **When** the build runs, **Then** every repo-specific compiler setting still takes effect: target framework `net10.0`, warnings-as-errors, the `--nowarn:57` / `WarnOn` promotions, XML doc generation, and `IsPackable=false`.
4. **Given** the adopted repo's two managed `.props` files, **When** they are compared to the org source of truth, **Then** they are byte-identical (carry the canonical "do not edit" marker; no hand edits).

---

### User Story 2 - CI drift gate catches divergence from the source of truth (Priority: P2)

As a Governance maintainer, I want CI to fail when a managed build-config file drifts from the org source of truth, so that the "sync, don't fork" contract is enforced automatically rather than by convention.

**Why this priority**: Adoption without a gate decays — the next hand edit silently re-forks the config. The gate makes the contract durable, but it depends on P1 having landed the canonical files first.

**Independent Test**: With the gate wired, hand-edit any managed file on a branch and observe CI fail on the drift check; revert and observe CI pass.

**Acceptance Scenarios**:

1. **Given** the drift check wired into the per-PR CI gate, **When** all three managed files match the source of truth, **Then** the check reports each file ok and CI passes.
2. **Given** the drift check wired into CI, **When** a managed file is hand-edited (or missing/stale), **Then** the check reports drift and CI fails (non-zero exit).
3. **Given** a drift failure, **When** the file is re-synced (or reverted) to the canonical content, **Then** the check passes again.

---

### User Story 3 - Re-sync after a source-of-truth change is a no-edit, one-command operation (Priority: P3)

As a Governance maintainer, I want future updates to the org baseline (e.g. a bumped org-wide `FSharp.Core`) to flow in by re-running the sync with no hand-editing of managed files, so that staying current is cheap and the override files keep all repo-specific settings intact.

**Why this priority**: This is the forward-looking payoff of adopting the sync model rather than a one-time copy. It is the least urgent because it only matters on the *next* baseline change.

**Independent Test**: Re-run the sync against the current source of truth on a clean adopted repo; the managed files are rewritten verbatim, the `*.local.props` files are untouched, and the build + tests stay green.

**Acceptance Scenarios**:

1. **Given** an adopted repo and an updated source of truth, **When** the sync is re-run, **Then** only the three managed files change and the two `*.local.props` override files are left untouched.
2. **Given** a re-sync, **When** the build runs, **Then** repo-specific settings in the override files still apply (the canonical import is last, so local overrides win).

---

### Edge Cases

- **Canonical Build.props omits properties this repo depends on.** The org `Directory.Build.props` carries only determinism, CPM, and the lockfile-restore gate — it does **not** carry `TargetFramework`, `LangVersion`, `Nullable`, or `TreatWarningsAsErrors`. These must all move to the local override or the build breaks (e.g. the framework would default away from `net10.0`). The issue's enumerated list (`WarnOn`, `--nowarn:57`, `IsPackable`, `GenerateDocumentationFile`) is illustrative, not exhaustive: **every** property not present in the canonical file moves to local.
- **`FSharp.Core` re-declared locally.** If the local override also pins `FSharp.Core`, CPM raises a duplicate-`PackageVersion` error (`NU1504`/`NU1011`). The local pin must be dropped, not duplicated.
- **Tool manifest pins a tool this repo does not use.** The canonical `.config/dotnet-tools.json` pins `fake-cli`, but this repo's `build.fsx` is a plain `dotnet fsi` script that shells out to `dotnet build`/`dotnet test` and never invokes the `fake` tool. Adopting the manifest is harmless (an unused, pinned tool) but is **required** for the drift check to pass, since the check covers all three managed files.
- **Org baseline introduces a new setting.** The canonical Build.props adds `Deterministic=true`, which this repo did not set explicitly. This is an intended org default, not a regression.
- **Fresh clone / first restore.** The locked-restore gate is conditioned on `GITHUB_ACTIONS` AND an existing lockfile, so a fresh local clone (and a brand-new project before its lockfile exists) is never wedged — behavior identical to today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repo MUST carry the three org-canonical managed files — `Directory.Build.props`, `Directory.Packages.props`, and `.config/dotnet-tools.json` — byte-identical to the `FS-GG/.github` `dist/dotnet/` source of truth (each bearing the canonical "do not edit / source of truth" marker).
- **FR-002**: All repo-specific MSBuild properties currently in `Directory.Build.props` MUST be relocated to `Directory.Build.local.props` (repo-owned, imported last by the canonical file). This includes at minimum: `TargetFramework` (`net10.0`), `LangVersion`, `Nullable`, `TreatWarningsAsErrors`, `WarnOn` (`3390;1182`), the `--nowarn:57` other-flag, `GenerateDocumentationFile`, and `IsPackable=false`.
- **FR-003**: All repo-specific `PackageVersion` items currently in `Directory.Packages.props` MUST be relocated to `Directory.Packages.local.props` (repo-owned, imported last) — namely `YamlDotNet`, `Spectre.Console`, and the test-only pins (`Expecto`, `Expecto.FsCheck`, `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk`).
- **FR-004**: The local `FSharp.Core` `PackageVersion` MUST be dropped from the repo; `FSharp.Core` is single-sourced from the org baseline. It MUST NOT be re-declared in any local override (to avoid the CPM duplicate-pin error).
- **FR-005**: The adopted configuration MUST preserve the resolved package versions unchanged, including `FSharp.Core` at `10.1.301` and every relocated pin at its current version — no version bumps as part of adoption.
- **FR-006**: The adopted configuration MUST preserve the existing restore-time enforcement: lockfiles committed, restore in locked mode in CI (gated on `GITHUB_ACTIONS` and an existing lockfile), and `NU1603`/`NU1608` promoted to errors. The gate condition is unchanged (already `GITHUB_ACTIONS`).
- **FR-007**: The adopted configuration MUST preserve all compiler behavior previously in effect — warnings-as-errors, the `--nowarn:57` / `WarnOn` set, XML doc generation, `net10.0`, and `IsPackable=false` — via the local override files. (This is the observable *outcome* of the FR-002 property relocation and the FR-005 version preservation, restated as a behavior guarantee; it adds no work beyond those two.)
- **FR-008**: The repo's per-PR CI gate MUST run the source-of-truth drift check, failing the build (non-zero exit) when any managed file diverges from canonical, and passing when all three match.
- **FR-009**: The change MUST NOT modify any `src/` production code, public F# surface (`.fsi`), golden/snapshot fixtures, or surface-area baselines — it is build-infrastructure only.
- **FR-010**: The full build and the complete test suite MUST pass after adoption with results equivalent to before adoption (same projects compile, same tests pass).

### Key Entities *(config artifacts)*

- **Managed build-config files**: the three org-canonical files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) taken verbatim from the source of truth; marked "do not edit"; covered by the drift check.
- **Local override files**: the two repo-owned files (`Directory.Build.local.props`, `Directory.Packages.local.props`) imported last by the canonical files; hold every repo-specific property and package pin; **not** managed by the sync.
- **Org baseline pin**: the single org-wide `FSharp.Core` `PackageVersion` (`10.1.301`) that replaces the repo's local pin.
- **Drift gate**: the CI check (`sync-build-config.sh --check`) that compares the three managed files to the source of truth and fails on divergence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The full solution build and the complete test suite are green after adoption, with the same project and test-pass counts as before the change (no behavior change).
- **SC-002**: The source-of-truth drift check reports all three managed files ok and exits 0 on the adopted repo.
- **SC-003**: `FSharp.Core` is declared in exactly one place (the org baseline) and resolves to `10.1.301`; restore produces no duplicate-`PackageVersion` error.
- **SC-004**: Restore enforcement is unchanged: in CI (`GITHUB_ACTIONS` set, lockfile present) restore runs in locked mode and a dependency-graph drift fails restore; a fresh local clone restores without being blocked.
- **SC-005**: Hand-editing any managed file makes the CI drift gate fail (non-zero exit); reverting/re-syncing makes it pass — demonstrated on a branch.
- **SC-006**: Every previously-effective repo-specific setting is still in effect after adoption (target framework `net10.0`, warnings-as-errors, `--nowarn:57`/`WarnOn`, XML doc generation, `IsPackable=false`, and all relocated package pins) — verifiable by an unchanged compiler-warning/build surface.
- **SC-007**: No `src/` production file, `.fsi` surface, golden fixture, or surface baseline differs as a result of the change (verifiable by an empty diff over those paths).

## Assumptions

- **Full canonical set is adopted**, including `.config/dotnet-tools.json` (`fake-cli` `6.1.4`), even though `build.fsx` does not invoke the `fake` tool — required for the drift check to pass; the unused pinned tool is harmless.
- **The issue's property list is non-exhaustive**: every MSBuild property absent from the canonical `Directory.Build.props` (notably `TargetFramework`, `LangVersion`, `Nullable`, `TreatWarningsAsErrors`) is moved to the local override, not just the four named in the issue body.
- **`FSharp.Core` stays at `10.1.301`** — the org baseline equals the repo's current pin, so adoption is a no-version-change relocation.
- **The drift check is wired via the org's reusable coherence workflow** (`.github#18`) or an equivalent CI step that obtains the source-of-truth files/script; the exact CI mechanism (reusable workflow vs. checking out `FS-GG/.github`) is a planning decision, not a spec constraint.
- **The existing `gate.yml` per-PR workflow is the home for the drift gate** (this repo already runs a NuGet-restore lock gate there).
- **The new org `Deterministic=true` default is acceptable** for this repo (intended org behavior, not a regression).
- **This is a consumer-side adoption only**: the source of truth (`dist/dotnet/`, the sync script, ADR-0006) is already merged in `FS-GG/.github`; no changes to the contract or registry are produced here (the contract entry already records `shared-build-config`).
- **Out of scope**: the other open governance items — re-typing `Config.Loader` onto `FS.GG.Contracts` schemas (#14, blocked) and packaging `FS.GG.Governance.ReferenceGateSet` (#15) — are separate board items and not part of this feature.
