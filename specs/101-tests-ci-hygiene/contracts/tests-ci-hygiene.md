# Contracts: Tests & CI hygiene (feature 101)

The three authoritative shapes below are what the implementation must satisfy and what the acceptance in `quickstart.md` verifies.

## Contract A — the `SurfaceDrift` test-support surface

Added to `tests/FS.GG.Governance.Tests.Common/TestsCommon.fsi` (curated `.fsi`, Principle II). This is the sole public shape; the `.fs` body carries no access modifiers.

```fsharp
/// The single shared surface-drift check (replaces ~80 per-project copies). Reflection lives here and
/// in the call-sites only — never in a product project.
module SurfaceDrift =

    open System.Reflection
    open Expecto

    /// Canonical reflective projection of an assembly's public surface. Byte-identical to the projection
    /// every committed `surface/*.surface.txt` baseline was blessed against.
    val renderSurface: asm: Assembly -> string

    /// Baseline-equality test: compare `renderSurface asm` to `surface/<baselineName>.surface.txt`
    /// (repo root via RepositoryHelpers.repoRoot). `BLESS_SURFACE=1` rewrites the baseline.
    val surfaceTest: label: string -> baselineName: string -> asm: Assembly -> Test

    /// Scope guard: every referenced-assembly name of `asm` is BCL / FSharp.Core or satisfies `allowed`.
    val referencesOnly: label: string -> allowed: (string -> bool) -> asm: Assembly -> Test

    /// Direction guard: no assembly in `upstream` references `asm`.
    val noInboundReferences: label: string -> upstream: Assembly list -> asm: Assembly -> Test
```

**Invariants**
- `renderSurface` reproduces every existing baseline with no re-bless required (except `Tests.Common`'s own, whose surface this module widens).
- A migrated per-project file is a thin instantiation: `open FS.GG.Governance.Tests.Common`, resolve the target assembly, list `SurfaceDrift.*` calls (plus any local bespoke guard). No per-project `renderSurface`, `normalize`, `findRepoRoot`, or bless path.
- Out of scope (stay local, verbatim): `Cli.Tests/SurfaceDriftTests.fs`, `Sample.SddReferenceProvider.Tests`.

## Contract B — CI job invariants

Applied to every job in `.github/workflows/gate.yml` and `.github/workflows/publish.yml`.

```yaml
# Every job:
jobs:
  <job>:
    runs-on: ubuntu-latest
    timeout-minutes: <explicit finite value>     # REQUIRED on all 9 jobs

# Every restoring job's setup step (the 8 with setup-dotnet):
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
          cache: true                             # REQUIRED
          cache-dependency-path: '**/packages.lock.json'   # REQUIRED
```

**Invariants**
- No job omits `timeout-minutes` (no reliance on the 360-minute default).
- Every `dotnet restore` job caches on the lockfile glob; a lockfile change invalidates the cache.
- No org-synced build-config file is edited — caching is workflow-level only.
- Locked-restore enforcement is unchanged (the cache never suppresses a graph-drift failure).

## Contract C — publish version-resolution + fallback user

Applied to `.github/workflows/publish.yml` `resolve-version` job and the two `NuGet/login` steps.

```text
resolve-version (release / push tags v* branch):
  tag semver && == fsproj <Version>   -> push=true            (unchanged)
  tag semver && != fsproj <Version>   -> error, exit 1        (unchanged)
  tag NOT semver (e.g. vNext)         -> error, exit 1  <-- NEW (was push=true)
  workflow_dispatch, input == Version -> push=true            (unchanged)
  workflow_dispatch, input != Version -> error, exit 1        (unchanged)
  workflow_dispatch, no input         -> push=false (dry run) (unchanged)
```

```yaml
# workflow level:
env:
  NUGET_FALLBACK_USER: Paradigma11        # single declaration

# both publish jobs:
      - uses: NuGet/login@v1
        with:
          user: ${{ secrets.NUGET_USER || env.NUGET_FALLBACK_USER }}   # one source, two refs
```

**Invariants**
- The published `version` is always the evaluated fsproj `<Version>`; the tag is only validated against it, never used as the version source.
- `Paradigma11` appears exactly once in `publish.yml`.
- The dry-run and semver-match paths are behaviorally unchanged.
