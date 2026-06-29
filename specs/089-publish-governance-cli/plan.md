# Implementation Plan: Publish the Consumer-Bearing Governance CLI to the Org Feed

**Branch**: `089-publish-governance-cli` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/089-publish-governance-cli/spec.md`

## Summary

The spec-081 SDD→Governance handoff consumer (`FS.GG.Governance.Adapters.SddHandoff`) is wired through `route`/`ship`/`verify` and reachable from `FS.GG.Governance.Cli` (CLI → `RouteCommand` → `Adapters.SddHandoff`, confirmed in `RouteCommand/Interpreter.fs`), so a packed tool already carries it. But **no `FS.GG.Governance.Cli` exists on the org GitHub Packages feed (404)** and **there is no publish path in this repo** — `gate.yml` only restores/builds/guards on the *read* side (`packages: read`). The only installable predecessors live on the local dev feed (`1.0.0` @ 2026-06-18, `0.1.1` @ 2026-06-25) and predate the consumer, so a downstream `route --mode gate` against a failing handoff exits `0` (green-by-omission), which blocks FS.GG.Templates#25 and makes the board/registry "done" dishonest.

**Technical approach** — this is a **release + cross-repo-coordination** feature, not a new-F#-surface feature. No pure library code changes; the consumer already exists. The work is:

1. **Reconcile the CLI version** (`src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` `<Version>` `0.1.1` → **`1.1.0`**): strictly greater than every predecessor incl. the stray `1.0.0` (FR-004), minor bump because the tool gains an externally-observable capability (a produced handoff now drives the verdict) relative to the `1.0.0` build. See research D1.
2. **Add a repo-owned publish workflow** (`.github/workflows/publish.yml`), mirroring the org-canonical precedent `FS-GG/FS.GG.SDD/.github/workflows/release.yml`: triggers `release: published` / `push tags: v*` / `workflow_dispatch`; version read from the fsproj via `dotnet msbuild -getProperty:Version` (no hardcoded pin); `packages: write` on the publish job only; `dotnet pack` the CLI then `dotnet nuget push --source https://nuget.pkg.github.com/FS-GG/index.json --api-key ${{ secrets.GITHUB_TOKEN }} --skip-duplicate`. Scoped to **the CLI tool package only** (its dependency closure bundles `Adapters.SddHandoff.dll`); the broader ~70-package publish is out of scope (H4/088-adjacent). See research D2.
3. **Gate the push behind a real-evidence enforcement smoke test** (FR-008, the green-by-omission guard): pack → `dotnet tool install` into a temp dir → run the installed `fsgg-governance route --root <fixture> --mode gate` against a committed **failing-handoff** product fixture and assert it **blocks (exit `2` = `GovernedBlocking`, confirmed in `Cli.fs:341`)**, against a **passing-handoff** fixture assert exit `0`, and (light mode) assert no block. The push runs only if the smoke passes — a consumer-less build cannot be published under the consumer-bearing version. See research D3 + contracts/cli-enforcement.md.
4. **Record the consumer-side coherence verification** of `governance-handoff@1.0.0` by appending a `coherence:` entry to `FS-GG/.github` `registry/dependencies.yml` (cross-repo PR; the `docs/registry/compatibility.md` projection auto-syncs) — **not** a contract surface bump (FR-006). See research D4.
5. **Ratify the first publish** of `FS.GG.Governance.Cli` to the org feed in a local decision record `docs/decisions/0004-publish-governance-cli-org-feed.md` — this is the constitution's `TODO(PACKAGE_IDENTITY)` "ratify when the first package is published" point. See research D5.
6. **Resolve the cross-repo loop**: respond on + close FS.GG.Governance#28 and move its Coordination board item to **Done** once the consumer-bearing CLI is on the feed and the Templates#25 probe flips from SKIP to asserting (FR-009, SC-003/006).

This is a **Tier 1 (contracted) change** — it establishes a published package contract (first publish + a version that the registry range will gate) — but it adds **no F# public API surface**, so no new `.fsi` and no `surface.txt` baseline change apply; the contract obligations that *do* apply are the version, the registry coherence record, the decision record, and real smoke evidence.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (org baseline; SDK `10.0.x` in CI per `gate.yml`). No F# code is added; the only `.fs`-adjacent change is the fsproj `<Version>`.

**Primary Dependencies**:
- **Build/release tooling only** (no new library dependency): `dotnet pack` / `dotnet nuget push` / `dotnet tool install` (SDK-bundled), GitHub Actions, the org GitHub Packages NuGet feed (`https://nuget.pkg.github.com/FS-GG/index.json`).
- **Already-present, unchanged**: the CLI's dependency closure (`FS.GG.Governance.Cli` → `RouteCommand`/`ShipCommand`/`VerifyCommand` → `Adapters.SddHandoff`), packed verbatim into the tool package.
- **Constitution dependency-minimalism**: untouched — nothing enters the pure rule/evidence libraries; this is the CLI/release layer, which the constitution explicitly sanctions ("buildable, testable, documentable, **packable**, **releasable** with normal repository tooling"; "release packaging checks" are kept checks).

**Storage**: N/A. Inputs: the packed `.nupkg`; committed product fixtures (failing/passing `readiness/<id>/governance-handoff.json`) for the smoke test.

**Testing**: A CI enforcement smoke test (real evidence — packs the actual tool, installs it, runs it against real fixtures, asserts exit `2`/`0`). The existing `FS.GG.Governance.Cli.Tests` continue to run as the pre-publish gate (mirroring SDD's `cli-tests` job). No assertion is weakened; the smoke fails-before (a consumer-less build is caught) / passes-after.

**Target Platform**: Linux/CI (`ubuntu-latest`, .NET `10.0.x`); publish runs in a new `.github/workflows/publish.yml`.

**Project Type**: Single F# solution (`FS.GG.Governance.sln`) + CI/release wiring + one cross-repo registry PR. No new projects.

**Performance Goals**: Deterministic publish; `--skip-duplicate` makes re-runs idempotent. No hot path.

**Constraints**:
- **Drift-locked files are off-limits**: `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json` are org-synced and byte-identity drift-checked (`gate.yml` Job 2). The new publish workflow and any tool install are repo-owned / job-scoped — no edits to those files. The fsproj `<Version>` is repo-owned and free to change.
- **Least privilege**: `packages: write` only on the publish job; run-scoped `GITHUB_TOKEN` (no PAT), per the SDD precedent.
- **Version immutability on the feed**: GitHub Packages rejects re-pushing a version; `--skip-duplicate` + a fresh `<Version>` per release avoid a hard failure on re-run (edge case in spec).
- **No general fan-out**: publish is scoped to the CLI package only; do not publish the other packable `FS.GG.Governance.*` projects here.

**Scale/Scope**: One package (`FS.GG.Governance.Cli`), one new workflow, one version bump, one decision record, one cross-repo registry entry, one issue/board resolution. Two small product fixtures for the smoke test.

## Constitution Check

*GATE: evaluated pre-Phase 0 and re-checked post-Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | PASS (N/A for new API) | No new F# public surface — the consumer already exists and is wired. Verification is a **real behavioral smoke test** (packed tool vs real fixtures), which honors the principle's "FSI/packed-library is the honest audience, prefer real evidence" intent. |
| **II. Visibility in `.fsi`** | PASS (N/A) | No `.fs`/`.fsi` modules added or changed; no access-modifier surface to curate. |
| **III. Idiomatic Simplicity** | PASS | Mirrors the existing org publish pattern (SDD `release.yml`); a workflow + a shell/`fsx` smoke. No clever F#, no new abstractions. |
| **IV. Elmish/MVU boundary for I/O** | PASS (N/A) | No new in-process stateful/I/O workflow code; the CLI's MVU host is unchanged. Publishing is CI orchestration, not application I/O code. |
| **V. Test Evidence Mandatory** | PASS | The publish guard is real evidence: pack the actual tool, install it, run `route --mode gate` against real failing/passing handoff fixtures, assert exit `2`/`0`. Fails-before (consumer-less build blocked from publish), passes-after. No synthetic substitution needed. |
| **VI. Observability & Safe Failure** | PASS | Auth/collision/missing-consumer surface explicitly and fail the publish (never a partial/mislabeled push); `--skip-duplicate` is explicit; the guard refuses a green-by-omission artifact. |
| **Genericity / Operating rule** | PASS | Publishes `FS.GG.Governance.Cli` self-identity to the org feed; assumes no rendering package ids/paths. Governance releases itself with standard tooling, as the constitution states. |
| **Dependency-minimalism** | PASS | No new dependency anywhere; SDK-bundled pack/push only. The no-publishing constraint applies to the **core rule/evidence library**, not the CLI/release layer (explicitly "packable/releasable"). |
| **Change Classification** | **Tier 1** | Declared: establishes a published package contract + version the registry range gates. No `.fsi`/`surface.txt` obligation (no F# surface delta); the applicable obligations are version, registry coherence record (FR-006), decision record, and smoke evidence — all tracked in tasks. |

**No violations** → Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/089-publish-governance-cli/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D1–D8
├── data-model.md        # Phase 1 — entities (package, version, fixture, coherence entry)
├── quickstart.md        # Phase 1 — local pack/smoke/dry-run-publish/verify guide
├── contracts/
│   ├── publish-workflow.md   # the publish.yml contract (triggers, version source, perms, push, guard)
│   └── cli-enforcement.md     # the behavioral contract the published CLI must satisfy (exit 2/0)
└── checklists/
    └── requirements.md   # spec quality checklist (from /speckit-specify)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Cli/
└── FS.GG.Governance.Cli.fsproj      # <Version> 0.1.1 → 1.1.0 (only source edit)

.github/workflows/
└── publish.yml                       # NEW — repo-owned publish (mirrors FS-GG/FS.GG.SDD release.yml)

tests/cli-publish-smoke/              # NEW — real-evidence publish guard
├── run.sh (or smoke.fsx)             # pack → tool-install → route --mode gate → assert exit 2/0
└── fixtures/
    ├── failing-handoff/readiness/<id>/governance-handoff.json   # blocks in strict gate (exit 2)
    └── passing-handoff/readiness/<id>/governance-handoff.json   # passes (exit 0)

docs/decisions/
└── 0004-publish-governance-cli-org-feed.md   # NEW — first-publish ratification (TODO(PACKAGE_IDENTITY))
```

Cross-repo (separate PR / actions, outside this checkout):

```text
FS-GG/.github  registry/dependencies.yml      # append a coherence entry (governance-handoff consumer verified)
FS-GG/FS.GG.Governance#28                       # respond + close
Coordination board                              # item #28 → Done
```

**Structure Decision**: Single existing F# solution; the only in-repo source change is the CLI fsproj `<Version>`. Everything else is CI/release wiring (`publish.yml` + a smoke-test directory with two product fixtures) and a decision record, plus a cross-repo registry entry and the issue/board resolution. No new F# projects, modules, or `.fsi`.

## Complexity Tracking

> No constitution violations — section intentionally empty.
