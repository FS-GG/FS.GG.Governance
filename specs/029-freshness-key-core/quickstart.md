# Quickstart: Freshness Key Computation Core

How to build, FSI-exercise, test, and re-bless the surface for `FS.GG.Governance.FreshnessKey`. This is a
validation/run guide; the type and function details live in [data-model.md](./data-model.md) and
[contracts/](./contracts/).

## Prerequisites

- .NET SDK with `net10.0` (repo standard).
- The repo restores from the central feed (`Directory.Packages.props`); no new package is added by this
  feature.

## Build

```bash
dotnet build FS.GG.Governance.sln
```

Expected: the new `FS.GG.Governance.FreshnessKey` library and `…FreshnessKey.Tests` project compile clean
under `TreatWarningsAsErrors=true`, alongside the unchanged existing projects.

## Design-first FSI proof (Principle I)

Before writing `Model.fs` / `FreshnessKey.fs`, draft the `.fsi` files and exercise the shape in
`scripts/prelude.fsx` (a new `// ── F029 …` section), then:

```bash
dotnet fsi scripts/prelude.fsx
```

Expected transcript highlights:
- `compute` of a fixed `FreshnessInputs` prints a canonical key matching
  [contracts/freshness-key-format.md](./contracts/freshness-key-format.md).
- Reordering / duplicating `CoveredArtifacts` prints the **same** key.
- Flipping the rule hash prints a **different** key and `matches = false`, with `diff` naming `ruleHash`.
- A command-less input (`Command = None`, `CommandVersion = None`) computes a key and matches another
  command-less input with otherwise-equal fields.

## Test

```bash
dotnet test tests/FS.GG.Governance.FreshnessKey.Tests/FS.GG.Governance.FreshnessKey.Tests.fsproj
```

Expected: all green. The suite proves the laws in [contracts/freshness-key-api.md](./contracts/freshness-key-api.md):

| Test file | Proves | SC |
|---|---|---|
| `DeterminismTests` | compute-twice byte-equality; order/duplication invariance of the artifact set | SC-001, SC-002 |
| `DistinctionTests` | single-field change ⇒ key differs & `matches = false`, for **every** category | SC-003 |
| `InjectivityTests` | the same string moved between categories changes the key | SC-004 |
| `InspectionTests` | `diff` locates the differing category; `matches ⇔ diff = []`; predicate/key agreement | SC-005 |
| `PurityTests` | the key is identical across changed cwd, time, and unrelated filesystem state | SC-006 |
| `TotalityTests` | empty artifact set, `None` command/version, base = head, empty strings — all total | FR-011 |
| `SurfaceDriftTests` | public surface equals the committed baseline; assembly references Config/BCL/FSharp.Core only | SC-007 |

## Re-bless the surface baseline (intentional API change only)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.FreshnessKey.Tests/FS.GG.Governance.FreshnessKey.Tests.fsproj
```

Rewrites `surface/FS.GG.Governance.FreshnessKey.surface.txt`. Use only when a public-surface change is
intended (Tier 1), and commit the regenerated baseline with the change.

## Validate the no-regression promise (SC-007)

```bash
dotnet test FS.GG.Governance.sln
```

Expected: the existing projects' tests and their `surface/*.surface.txt` baselines are unchanged; only the
new project's tests are added. No merged core, `.fsi`, or baseline is modified by this feature.
