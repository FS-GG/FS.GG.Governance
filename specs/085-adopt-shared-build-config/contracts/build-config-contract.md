# Contract: `shared-build-config` conformance (consumer side)

This feature **consumes** the org `shared-build-config` contract (source of truth `FS-GG/.github` `dist/dotnet/`, ADR-0006). It does **not** change the contract or the org registry. This document states the conformance obligations this repo must satisfy and how each is verified.

## C1 — Managed files are byte-identical to the source of truth

**Obligation**: `Directory.Build.props`, `Directory.Packages.props`, and `.config/dotnet-tools.json` equal the canonical files in `FS-GG/.github` `dist/dotnet/`, each carrying the `Source of truth: FS-GG/.github` marker; no hand edits.

**Verification**: `<.github>/scripts/sync-build-config.sh --check .` reports `ok: <file>` for all three and exits `0`.

## C2 — Repo-specific settings live only in `*.local.props`

**Obligation**: All repo-specific MSBuild properties and `PackageVersion` pins live in `Directory.Build.local.props` / `Directory.Packages.local.props` (imported last by the canonical files). These two files are **not** managed by the sync and are exempt from `--check`.

**Verification**: `--check` lists only the three managed files; the two `*.local.props` are untouched by `--adopt`/re-sync. Build output proves the local properties take effect.

## C3 — Org baseline owns `FSharp.Core`

**Obligation**: `FSharp.Core` is declared exactly once (the org baseline, `10.1.301`); it is **not** re-declared in any local file.

**Verification**: `dotnet restore` produces no `NU1504`/`NU1011` duplicate-`PackageVersion` error; the resolved `FSharp.Core` is `10.1.301` (unchanged).

## C4 — Restore enforcement preserved

**Obligation**: Lockfiles committed; CI restore runs in locked mode (gate `GITHUB_ACTIONS=='true' And Exists(packages.lock.json)`); `NU1603`/`NU1608` promoted to errors. Fresh local clone restores unblocked.

**Verification**: `dotnet restore FS.GG.Governance.sln --locked-mode` green in CI; a fresh local `dotnet restore` (no `GITHUB_ACTIONS`) succeeds without locked mode.

## C5 — Drift gate in CI

**Obligation**: The per-PR CI gate runs `--check` and fails (non-zero) on any managed-file divergence; passes when all match.

**Drift-check exit semantics** (from `sync-build-config.sh`):

| Condition | `--check` output | Exit |
|---|---|---|
| all three files match | `ok: <file>` ×3 | `0` |
| a managed file differs | `DRIFT (differs): <file>` | `1` |
| a managed file missing | `DRIFT (missing): <file>` | `1` |

**Verification**: green gate on the adopted repo; red gate when a managed file is hand-edited on a branch; green again on revert/re-sync (SC-005).

## C6 — No behavior / surface change

**Obligation**: No change to resolved package versions, compiler behavior, public F# surface, goldens, or baselines.

**Verification**: `dotnet fsi build.fsx test` green with the same counts as pre-change `main`; empty `git diff` over `src/`, `tests/`, `*.fsi`, `**/*.fs`, `*.sln`, `samples/`, `docs/`, `build.fsx`, goldens, baselines.

---

## Out of contract scope (recorded)

- **No registry/ADR change** — `shared-build-config` is already recorded in the org registry; this is pure consumer adoption.
- **Reusable workflow migration** — when `FS-GG/.github#18` (`contract-coherence.yml`, `workflow_call`) lands, the C5 self-contained checkout job should be replaced by a `uses:` call. Bounded follow-up, not part of this feature.
