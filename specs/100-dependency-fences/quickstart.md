# Quickstart — validate the dependency fences

Runnable scenarios that prove the feature. All commands run from the repo root.

## Prerequisites

- .NET `net10.0` SDK (as pinned by the repo).
- A clean working tree on branch `100-dependency-fences`.

## Scenario 1 — the three guards are green on the fixed tree (FR-001/003/005/006)

```sh
dotnet test tests/FS.GG.Governance.DependencyFences.Tests
```

**Expected**: all fence tests pass — the direct-`YamlDotNet` set matches the documented
owner allowlist, no executable references another executable, and at most one project
claims `ToolCommandName=fsgg`.

## Scenario 2 — each guard turns red when its fence is broken (SC-004)

Prove the guard actually bites (revert each edit after observing red):

- **YAML**: add `<PackageReference Include="YamlDotNet" />` to a non-owner project (e.g.
  `FS.GG.Governance.Findings`) → the YAML guard fails naming that project.
- **Exe leaf**: add a `<ProjectReference>` from one exe to another (e.g. `ShipCommand` →
  `RouteCommand`) → the exe-leaf guard fails naming the `Exe → Exe` edge.
- **Single `fsgg`**: set `<ToolCommandName>fsgg</ToolCommandName>` on a second project →
  the `fsgg` guard fails naming the second claimant.

## Scenario 3 — the refactored commands behave identically (FR-004)

```sh
dotnet test tests/FS.GG.Governance.RouteCommand.Tests \
            tests/FS.GG.Governance.Cli.Tests \
            tests/FS.GG.Governance.EvidenceCommand.Tests
```

**Expected**: all existing semantic + `SurfaceDriftTests` pass. Surface baselines that
moved with a relocated module reflect the new owning library (a relocation, not a widened
API). `fsgg route` and `fsgg-evidence` produce the same output as before.

## Scenario 4 — no forbidden files changed (FR-007/008, SC-005)

```sh
git diff --name-only origin/main... -- \
  Directory.Build.props Directory.Packages.props .config/dotnet-tools.json
```

**Expected**: empty output (org-synced build config untouched). Confirm separately that no
`PackageId` changed and no JSON contract file under the contracts/schemas was modified.

## Scenario 5 — full build + test suite (FR-009)

```sh
dotnet build && dotnet test
```

**Expected**: green build and full suite — the real acceptance evidence for this feature.

## Scenario 6 (P3, optional) — centralized VersionPrefix (FR-010)

```sh
# Before vs after: no packable tool's effective version should change unexpectedly.
dotnet msbuild src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj \
  -getProperty:Version
```

**Expected**: baseline-only projects inherit the single `<VersionPrefix>` from
`Directory.Build.local.props`; packable tools keep their intended versions.
