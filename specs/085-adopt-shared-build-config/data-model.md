# Phase 1 Data Model: Config-artifact inventory

This feature has no runtime data model; its "entities" are build-config artifacts. This file is the authoritative before/after inventory and the exact org-canonical vs repo-specific partition (the contract the tasks must realize).

## Artifacts

| Artifact | Owner | Managed by sync? | Role |
|---|---|---|---|
| `Directory.Build.props` | org (`FS-GG/.github`) | ✅ verbatim | Determinism + CPM + lockfile-restore gate; imports local last |
| `Directory.Packages.props` | org | ✅ verbatim | CPM enablement + org `FSharp.Core` baseline; imports local last |
| `.config/dotnet-tools.json` | org | ✅ verbatim | Pinned tool manifest (`fake-cli 6.1.4`) |
| `Directory.Build.local.props` | **repo** | ❌ never | Repo-specific MSBuild properties |
| `Directory.Packages.local.props` | **repo** | ❌ never | Repo-specific `PackageVersion` pins |
| `.github/workflows/gate.yml` | repo | ❌ | CI: locked-restore+build (existing) **+ new drift-check job** |
| `packages.lock.json` (×165) | repo | ❌ | Locked dependency graph; must stay valid |

**Import order invariant**: each canonical file imports its `*.local.props` **last**, so any property/pin in a local file overrides the org default (MSBuild last-write-wins). A package pinned in the org baseline (`FSharp.Core`) MUST NOT be re-declared locally (CPM `NU1504`/`NU1011`).

## `Directory.Build.props` — before → after

**Before (hand-authored, single file)** holds both org-canonical and repo-specific properties.

**After**:

- `Directory.Build.props` (verbatim canonical) — `Deterministic=true`; `ManagePackageVersionsCentrally=true`; `CentralPackageTransitivePinningEnabled=true`; `RestorePackagesWithLockFile=true`; `RestoreLockedMode` (cond. `GITHUB_ACTIONS=='true' And Exists(...packages.lock.json)`); `WarningsAsErrors=$(WarningsAsErrors);NU1603;NU1608`; `Import Directory.Build.local.props` (last).
- `Directory.Build.local.props` (NEW, repo-owned) — the moved properties:

| Property | Value |
|---|---|
| `TargetFramework` | `net10.0` |
| `LangVersion` | `latest` |
| `Nullable` | `enable` |
| `TreatWarningsAsErrors` | `true` |
| `WarnOn` | `3390;1182` |
| `OtherFlags` | `$(OtherFlags) --nowarn:57` |
| `GenerateDocumentationFile` | `true` |
| `IsPackable` | `false` |

**Delta vs today**: only addition is `Deterministic=true` (org default, intended). Everything else is relocated, not changed.

## `Directory.Packages.props` — before → after

**After**:

- `Directory.Packages.props` (verbatim canonical) — CPM property group + `FSharp.Core 10.1.301` baseline + `Import Directory.Packages.local.props` (last).
- `Directory.Packages.local.props` (NEW, repo-owned) — `<ItemGroup>`(s) of `PackageVersion` only (no CPM property group):

| Package | Version |
|---|---|
| `YamlDotNet` | `16.3.0` |
| `Spectre.Console` | `0.57.1` |
| `Expecto` | `10.2.3` |
| `Expecto.FsCheck` | `10.2.3` |
| `FsCheck` | `2.16.6` |
| `Microsoft.NET.Test.Sdk` | `18.6.0` |
| `YoloDev.Expecto.TestSdk` | `0.15.6` |

**Dropped**: local `FSharp.Core` `PackageVersion` (org baseline owns it; identical version `10.1.301`, so no resolution change).

## `.config/dotnet-tools.json` — before → after

- **Before**: absent in this repo.
- **After**: verbatim canonical — `{ version:1, isRoot:true, tools: { "fake-cli": { version:"6.1.4", commands:["fake"] } } }`. Dormant (no build path invokes it); present only for drift-gate parity.

## `gate.yml` — before → after

- **Before**: one job `gate` — checkout, setup-dotnet, `dotnet restore --locked-mode`, `dotnet build --no-restore`.
- **After**: unchanged `gate` job **+** new job `build-config-drift` — checkout this repo, checkout `FS-GG/.github` into `_org-build/`, run `_org-build/scripts/sync-build-config.sh --check .` (fails on exit 1).

## Invariants the tasks must preserve

1. `Directory.Build.props` and `Directory.Packages.props` byte-identical to source of truth (`--check` → ok).
2. `FSharp.Core` declared exactly once (org baseline), resolves to `10.1.301`.
3. All compiler behavior + all non-FSharp.Core pins preserved via local files.
4. Locked restore green; 165 lockfiles unchanged (or regenerated+committed only if restore proves otherwise).
5. Empty diff over `src/`, `tests/`, `*.sln`, `samples/`, `docs/`, `*.fsi`, `**/*.fs`, `build.fsx`, goldens, baselines.
