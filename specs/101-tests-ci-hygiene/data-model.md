# Phase 1 Data Model: Tests & CI hygiene (feature 101)

This feature is test/CI/build hygiene, not a domain model. The "entities" are the shared test-helper surface, the CI-job invariant, and the publish version-resolution decision. Each is defined below as the guarded shape the implementation must satisfy.

## 1. `SurfaceDrift` helper (test-support surface)

Lives in `FS.GG.Governance.Tests.Common` as a curated `.fsi` module. It is the single source for every surface-drift assertion.

| Member | Signature | Meaning |
|---|---|---|
| `renderSurface` | `Assembly -> string` | Canonical reflective projection of an assembly's public surface: `GetExportedTypes()` sorted by `FullName`; each type's public/instance/static/declared members rendered `"  [%A] %s"` and sorted; joined by `\n`. Deterministic — pure function of the assembly's metadata. |
| `surfaceTest` | `label:string -> baselineName:string -> asm:Assembly -> Test` | The baseline-equality test. Reads `surface/<baselineName>.surface.txt` (repo root via `RepositoryHelpers.repoRoot`), compares to `renderSurface asm` after `normalize` (CRLF→LF, `TrimEnd`); `BLESS_SURFACE=1` rewrites the baseline. Fails with a uniform, project-named diagnostic. |
| `referencesOnly` | `label:string -> allowed:(string -> bool) -> asm:Assembly -> Test` | Scope guard: every `asm.GetReferencedAssemblies()` name is BCL/`FSharp.Core` or satisfies `allowed`; else fail listing offenders. |
| `noInboundReferences` | `label:string -> upstream:Assembly list -> asm:Assembly -> Test` | Direction guard: no assembly in `upstream` references `asm` (e.g. kernel/Spi must not reference an adapter). |

**Validation rules**
- `renderSurface` output MUST be byte-identical to what the pre-consolidation per-project renderers produced (verified by every committed baseline still matching without a re-bless, except `Tests.Common`'s own — see §below).
- `surfaceTest` MUST preserve the bless semantics (`BLESS_SURFACE=1` writes `actual + "\n"`).
- Adding this module widens `Tests.Common`'s public surface → `surface/FS.GG.Governance.Tests.Common.surface.txt` is re-blessed once, and its sibling `SurfaceBaselineTests` (itself migrated to `surfaceTest`) guards the new surface.

**Per-project call-site (the shrunk file)** — inputs that vary per project:
- `label` — the test title prefix (e.g. `"V8 SpecKit"`).
- `baselineName` — the `surface/*.surface.txt` stem (e.g. `"FS.GG.Governance.Adapters.SpecKit"`).
- `asm` — the target assembly (`typeof<SomeExportedType>.Assembly`).
- optional bespoke tests appended inline (symbol-leak / one-off guards that stay local per D4).

## 2. CI job invariant

Every job in every workflow file, after this feature, satisfies:

| Attribute | Rule |
|---|---|
| `timeout-minutes` | Present and explicit on **every** job (all 9). No job relies on the 360-minute default. |
| NuGet cache | Every job that runs a `dotnet restore` (the 8 with `actions/setup-dotnet`) enables `cache: true` + `cache-dependency-path: '**/packages.lock.json'`. |
| Locked-restore | Unchanged: `--locked-mode` (or the `Directory.Build.props` `RestoreLockedMode`) still validates the graph; the cache is a download accelerator only. |

The one job without a restore (`build-config-drift`, which only checks out and diffs config) gets a `timeout-minutes` but no cache.

## 3. Publish version-resolution decision

The `resolve-version` job maps `(event, tag/input, fsproj <Version>)` to `{version, push}`. Hardened table:

| Trigger | Condition | Result | Change |
|---|---|---|---|
| `workflow_dispatch` | version input equals fsproj `<Version>` | `push=true` | unchanged |
| `workflow_dispatch` | version input ≠ fsproj `<Version>` | error, exit 1 | unchanged |
| `workflow_dispatch` | no version input | `push=false` (dry run) | unchanged |
| `release` / `push tags v*` | tag is semver **and** = fsproj `<Version>` | `push=true` | unchanged |
| `release` / `push tags v*` | tag is semver **and** ≠ fsproj `<Version>` | error, exit 1 (mismatch) | unchanged |
| `release` / `push tags v*` | tag is **not** semver (e.g. `vNext`) | **error, exit 1 (unreconcilable)** | **NEW — was `push=true`** |

`version` is always the evaluated fsproj `<Version>` (msbuild `-getProperty`), never derived from the tag; the tag is only ever *validated against* it.

## 4. Fallback NuGet user

| Attribute | Before | After |
|---|---|---|
| Declaration sites | 2 (hardcoded `Paradigma11` in `publish` and `publish-reference-gate-set`) | 1 (`env: NUGET_FALLBACK_USER: Paradigma11` at workflow level) |
| Reference | `secrets.NUGET_USER \|\| 'Paradigma11'` in each job | `secrets.NUGET_USER \|\| env.NUGET_FALLBACK_USER` in both jobs |
| Override precedence | `secrets.NUGET_USER` wins | unchanged |
