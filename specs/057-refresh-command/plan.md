# Implementation Plan: The `fsgg refresh` Host Command

**Branch**: `057-refresh-command` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/057-refresh-command/spec.md`

## Summary

Wire the Governance-owned regeneration entry point end to end. `fsgg refresh` reads a governed
repository's **row-local generation manifest** (`.fsgg/refresh.yml` — the authored declaration of *which*
generated views exist, *what sources* each derives from, *which generator* produces it, and the
generator-version basis), **senses** the current digest of each declared source, **decides per view**
whether it is current or stale by comparing the view's **recorded provenance** (recorded source digests +
generator version) against the freshly-sensed source digests + generator version — **by digest and
generator version, not by file presence** — reusing the F029 `FreshnessKey` comparator
(`compute`/`matches`/`diff`) verbatim, **regenerates** exactly the stale views by running each view's
declared generator at the edge (the F051/F052 execution port + an atomic temp-then-rename writer),
**records refreshed provenance** for each regenerated view so a re-run detects it as current, **reports** a
clear summary as human text plus an optional deterministic `refresh.json` artifact, and **exits** with one
of six distinguishable codes so a developer (or a scheduled job) can tell *all-current* from
*views-regenerated* from *a view stale and unresolved*.

The command is a new standalone executable, `FS.GG.Governance.RefreshCommand`, built to the exact
pure-core + injected-edge shape of the existing `route`/`ship`/`cache-eligibility`/`release`/`verify`
commands — and is the **closest sibling of `ReleaseCommand`** (it reads a row-local `.fsgg/*.yml`
declaration through an in-project `Declaration` adapter): a pure `Loop` MVU boundary (parse → init/update →
render → exit-code over `Model`/`Msg`/`Effect`), an `Interpreter` that binds the real edge ports (manifest
read, per-view source-digest sensing, recorded-provenance read/write, the generator execution port, an
atomic artifact writer, a stdout sink), and a thin `Program.fs`. The deterministic `refresh.json`
projection ships as a separate pure library, `FS.GG.Governance.RefreshJson`, mirroring the
`AuditJson`/`RouteJson`/`GatesJson`/`ReleaseJson`/`VerifyJson` precedent.

Unlike its read-only siblings, `fsgg refresh` **mutates the working tree by design** — but only the stale
declared views and their recorded provenance, and only in a non-dry run; `--dry-run` performs the identical
currency evaluation and writes nothing (FR-004, FR-013).

**Confirmed planning decisions (this plan):**

1. **The generation manifest is a row-local authored surface; recorded provenance is a generated
   companion.** `.fsgg/refresh.yml` is *authored* (per view: identity/kind, output path, declared
   source(s), the generator command, the generator-version basis) and is **never mutated** by refresh. Each
   view's *recorded provenance* (the source digests + generator version it was last generated from, plus
   the output digest) is a **generated** record that refresh writes on regeneration — so a re-run senses
   the same sources, sees matching recorded provenance, and reports the view current (SC-002). The frozen
   F014 `.fsgg` four-file schema is **reused verbatim, not edited**; `refresh.yml` is a new row-local file
   parsed by an in-project `Declaration` adapter exactly as `ReleaseCommand` parses `release.yml`
   (YamlDotNet parse-to-node; no new package).

2. **Staleness reuses the F029 `FreshnessKey` comparator, made revision-independent.** A view is stale iff
   `FreshnessKey.matches recorded current = false`, where `recorded`/`current` are `FreshnessInputs` that
   differ **only** in the source-digest set (`CoveredArtifacts`) and `GeneratorVersion`; the revision fields
   (`Base`/`Head`) are held **equal** between the two so view currency depends on *sources and generator*,
   never on git position (research D1 — this is the crux distinguishing *view currency* from *gate-evidence
   reuse*, which is correctly per-change). `FreshnessKey.diff recorded current` names the changed categories
   (`CoveredArtifactsCat`/`GeneratorVersionCat`), giving FR-016's "name the drifted source(s) and the
   reason" and the `--dry-run` reason text for free. This **reuses the existing machinery rather than
   introducing a new staleness mechanism** (FR-002); per-view source-digest *sensing* is a thin row-local
   edge helper over the same SHA-256 notion `FreshnessSensing` already uses, not a core edit.

3. **Regeneration runs the view's declared generator at the edge; the pure core names no renderer.** The
   pure `update` emits a `RegenerateView` effect carrying the manifest entry; the edge `Interpreter` runs
   the view's *declared* generator command through the F051/F052 execution port and commits the bytes with
   the atomic temp-then-rename writer, then records refreshed provenance. No product identity, view
   identity, path, generator version, or renderer is hardcoded in core (FR-011) — the renderers the spec
   names (gate metadata, rule catalog, capability/API-surface docs, route projections, baselines) are
   *examples a repository declares in its manifest*, regenerated through the same product-neutral effect.
   A generator that fails is a `ToolError` (exit `4`) with no partially-written view; a view whose source
   cannot be resolved is `stale-unresolved` (exit `1`), never a fabricated "current" (FR-010).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`).

**Primary Dependencies**: FSharp.Core 10.1.301; YamlDotNet 16.3.0 (already pinned, already used by F014
`Config` and `ReleaseCommand`'s `Declaration` — for the row-local `refresh.yml` parse; **no new package**);
`System.Text.Json` (BCL `Utf8JsonWriter` shared-framework — the `AuditJson`/`RouteJson`/`ReleaseJson`
deterministic-JSON precedent; **no new package**). Project references reused **verbatim**: F014 `Config`
(`Loader.FileReader`/`fileSystemReader`), F029 `FreshnessKey` (`compute`/`matches`/`diff`/`categoryToken`),
F051 `GateExecution` + F052 `GateRun` (the generator execution port), plus the new `RefreshJson`. Recorded
provenance persistence reuses the F048 `EvidenceReuseStore` serialization if it fits the view-currency
record, else a thin row-local lock (research D4). **No git sensing** (F016 `Snapshot`): view currency is
digest-based and revision-independent (decision 2), so no `Snapshot` reference is taken.

**Storage**: The local governed repository working directory plus the explicitly-written outputs — the
regenerated stale views (atomic temp-then-rename), each regenerated view's recorded-provenance record, and
the optionally-requested `refresh.json`. In `--dry-run`, nothing is written. No database, no network, no
registry.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck/FsCheck 2.16.6 (repo standard). Real temp-repository fixtures
via a `withTempRepo` helper (the ReleaseCommand/VerifyCommand precedent): faked ports over real cores for
unit coverage, one real-filesystem end-to-end proof (a real stale view regenerated through the public CLI),
byte-identical re-run determinism, a `--dry-run` no-mutation guard, and a network-free scope guard.

**Target Platform**: Cross-platform .NET CLI executable (Linux/macOS/Windows); local + scheduled-job usage.

**Project Type**: CLI host command (one new executable project) + one new pure projection library, with
matching test projects — single-solution F# layout.

**Performance Goals**: Not a hot path. One manifest parse + one source-digest sense per declared source +
pure per-view currency decisions + the declared generator executions (only the stale views regenerate);
sub-second beyond the generators' own cost. No performance-driven mutation needed.

**Constraints**: Deterministic, byte-identical `refresh.json` and byte-identical regenerated views for
identical repository state and identical regeneration outcomes (no timestamps/abs-paths/usernames/
machine-specific content); printed machine output equals the persisted file verbatim (one source of
truth); network-free own logic (verifiable by a scope guard); fail-safe (missing/unreadable/unexpected
input ⇒ `stale-unresolved` or a distinct non-success exit, never a fabricated "all current" report and
never a crash); product-neutral (no hardcoded identity/version/path/view/renderer); the governed repository
is never mutated except the regenerated stale views, their recorded provenance, and the requested
`refresh.json`; no partial view and no partial artifact on a tool error.

**Scale/Scope**: Two new `src` projects (`RefreshCommand` ≈ Declaration/Loop/Interpreter/Program;
`RefreshJson` ≈ one projection module) + two new test projects + two surface baselines. No change to any
existing project's semantics or public surface — this row composes them.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Each new public module is drafted as a `.fsi`
  and its composition proven before its `.fs` body — by `dotnet build` of both `src` projects plus the
  semantic suites loading the packed public surface (the F055/F056 precedent; T-setup records which method
  was used). Semantic tests call the public surface (`Loop.parse`, `Interpreter.run`,
  `RefreshJson.ofRefreshDecision`, `Declaration.parse`), not internals.
- **II. Visibility Lives in `.fsi`** — PASS. Every public module ships a curated `.fsi` (`Declaration`,
  `Loop`, `Interpreter`, `RefreshJson`); `.fs` bodies carry no access modifiers; a surface-drift test +
  committed baseline is added per new public-surface assembly (`surface/FS.GG.Governance.RefreshCommand.surface.txt`,
  `…RefreshJson.surface.txt`).
- **III. Idiomatic Simplicity** — PASS. Plain records, closed DUs, pipelines, exhaustive matches; no
  SRTP/reflection/type-providers/custom CEs/non-trivial active patterns. No new dependency. Any local
  mutation in the JSON writer follows the disclosed `AuditJson`/`ReleaseJson` precedent.
- **IV. Elmish/MVU Is the Boundary** — PASS. The command is a stateful, I/O-bearing, multi-step workflow
  (load manifest → sense sources → decide currency → regenerate → record → render), so it is modeled as an
  MVU boundary: `Model`/`Msg`/`Effect`, pure `init`/`update`/`render`, and an edge `Interpreter` that
  executes effects (manifest read, source-digest sense, recorded-provenance read, generator execution,
  atomic view + provenance write, stdout) and turns results back into `Msg` — the exact ReleaseCommand
  shape. `RefreshJson` and `Declaration.parse` are pure leaves and need no MVU ceremony.
- **V. Test Evidence Is Mandatory** — PASS. Tests fail before / pass after; real temp-repo fixtures and
  real upstream cores (F014/F029/F051/F052 never mocked — only the edge ports are faked). Synthetic
  substitutes, if any, are disclosed at the use site, carry `Synthetic` in the test name, and are listed in
  the PR — none are anticipated (refresh runs against real temp repositories with real generator commands).
- **VI. Observability and Safe Failure** — PASS. Diagnostics distinguish missing/malformed **input**
  (absent/invalid `refresh.yml`, an absent/unreadable declared source, bad argv) from a **tool defect**
  (generator-execution or write-port failure) in both message and exit code; a source whose digest cannot
  be sensed degrades to a `stale-unresolved` finding (never a fabricated "current"); no swallowed
  exceptions in the critical path; no partial view or partial artifact on a tool error.

**Change Classification: Tier 1 (contracted change)** — adds new public API surface (two new projects with
public `.fsi`) and a new authored repository surface (`.fsgg/refresh.yml`). Requires the full chain: spec,
plan, `.fsi`, surface-area baselines, test evidence, and documentation. No public-API change to any
existing project (F014–F056 untouched), so no migration guidance is owed to existing consumers.

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/057-refresh-command/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── cli.md           #   fsgg refresh argv + exit-code contract
│   ├── manifest.md      #   .fsgg/refresh.yml generation-manifest contract
│   └── refresh.schema.md #  refresh.json deterministic projection contract
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.RefreshJson/                  # NEW — pure deterministic refresh.json projection
│   ├── RefreshJson.fsi                            #   schemaVersion ; ofRefreshDecision : RefreshDecision -> string
│   ├── RefreshJson.fs                             #   Utf8JsonWriter walk; emit-only; exhaustive token helpers;
│   │                                              #     per-view currency status (current/regenerated/stale-unresolved)
│   └── FS.GG.Governance.RefreshJson.fsproj        #   refs: the shared RefreshModel types (currency/decision)
│
└── FS.GG.Governance.RefreshCommand/               # NEW — the fsgg refresh host executable
    ├── Declaration.fsi                            #   parse : string list -> Result<GenerationManifest, DeclError>
    ├── Declaration.fs                             #   YamlDotNet parse-to-node; product-neutral; no F014 edit
    ├── Loop.fsi                                   #   pure MVU: RunRequest/parse, Model/Msg/Effect, init/update/render,
    │                                              #             per-view currency via FreshnessKey, ExitDecision/exitCode
    ├── Loop.fs
    ├── Interpreter.fsi                            #   Ports bundle (manifest read, source-digest sense, provenance
    │                                              #     read/write, generator execution, atomic write, stdout), realPorts, step, run
    ├── Interpreter.fs
    ├── Program.fs                                 #   [<EntryPoint>] thin host (parse -> realPorts -> run -> exit)
    └── FS.GG.Governance.RefreshCommand.fsproj     #   refs: Config, FreshnessKey, GateExecution, GateRun, RefreshJson (+ YamlDotNet)

tests/
├── FS.GG.Governance.RefreshJson.Tests/            # NEW — determinism, schema/golden, currency-section, no-fabrication tests
└── FS.GG.Governance.RefreshCommand.Tests/         # NEW — Declaration/Parse/Loop/Interpreter/Currency/Regenerate/EndToEnd/
                                                   #        Determinism/Failure/DryRunNoMutation/ScopeGuard/SurfaceDrift + Support.fs

surface/
├── FS.GG.Governance.RefreshJson.surface.txt       # NEW — committed surface baseline
└── FS.GG.Governance.RefreshCommand.surface.txt    # NEW — committed surface baseline

FS.GG.Governance.sln                               # EDIT — add the four new projects (mirror ReleaseCommand/VerifyCommand entries)
```

**Structure Decision**: Mirror the established command precedent exactly. Host commands are standalone
executables (`RouteCommand`/`ShipCommand`/`CacheEligibilityCommand`/`ReleaseCommand`/`VerifyCommand`), and
deterministic JSON projections are separate pure libraries
(`AuditJson`/`RouteJson`/`GatesJson`/`ReleaseJson`/`VerifyJson`). This row adds
`FS.GG.Governance.RefreshCommand` (the executable, with the in-project `Declaration` manifest adapter) and
`FS.GG.Governance.RefreshJson` (the pure projection), plus their test projects and surface baselines.
Refresh is the **closest sibling of `ReleaseCommand`** — same row-local-`.fsgg/*.yml`-via-`Declaration`
shape, same five edge-port pattern — differing in (a) it *writes* the views it regenerates (and their
recorded provenance) rather than only reading, (b) the per-view currency decision reusing `FreshnessKey`,
(c) the generator-execution effect, (d) the `refresh.json` schema id and six-way exit contract, and (e) the
"nothing to refresh" empty-manifest success report. No central `fsgg` dispatcher exists or is introduced; a
leading bare `refresh` token is tolerated per the existing command precedent. `IsPackable=false` this slice
(the ReleaseCommand precedent — no NuGet tool claims a `fsgg` ToolCommandName yet).

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

## Implementation Outcome (landed)

**Status: COMPLETE.** All 48 tasks done; `dotnet build FS.GG.Governance.sln` clean
(`TreatWarningsAsErrors=true`); 65 semantic tests green (53 `RefreshCommand` + 12 `RefreshJson`).

Delivered exactly as planned — two `src` projects + two test projects + two surface baselines + one golden:

- **`FS.GG.Governance.RefreshJson`** (leaf, packable) — `RefreshModel` (the shared `ViewKind`/
  `GenerationEntry`/`GenerationManifest`/`DeclError`/`CurrencyStatus`/`ViewDecision`/`RefreshOutcome`/
  `RefreshDecision` vocabulary + `viewKindToken`/`viewKindOfToken`) and `RefreshJson.ofRefreshDecision`
  (pure `Utf8JsonWriter` walk, `schemaVersion = "fsgg.refresh/v1"`).
- **`FS.GG.Governance.RefreshCommand`** (Exe, `IsPackable=false`) — `Declaration` (row-local
  `.fsgg/refresh.yml` YamlDotNet adapter), pure `Loop` MVU (parse → init/update/render → exitCode; per-view
  currency via F029 `FreshnessKey.matches`/`diff` with `Base`/`Head` held equal), edge `Interpreter` (the
  seven ports; the F051 process port for generators; SHA-256 source digesting; the `.fsgg/refresh.lock.json`
  recorded-provenance store), and a thin `Program`.

**Decisions confirmed in code:**

1. **Recorded provenance (research D4 / T024):** a minimal deterministic row-local lock
   `.fsgg/refresh.lock.json` (sorted view ids, no clock/abs-path), **not** `EvidenceReuseStore` — the F048
   store is per-change (Base/Head-keyed) and does not fit the revision-independent view-currency triple.
2. **Generator-version basis (research D2):** the manifest's `generatorBasis` string is used verbatim as the
   `GeneratorVersion` token — product-neutral, deterministic, no version-command sensing this slice.
3. **Regeneration (research D3):** the declared generator command **writes its own output**; the edge
   re-digests the output path for the recorded output digest. The atomic temp-then-rename `Write` port is
   used for `refresh.json` (and the lock); a non-zero generator exit is a `ToolError` (exit 4) with no
   recorded provenance.
4. **`RefreshDecision.DryRun`** was added (beyond the data-model's listed fields) so `ofRefreshDecision` can
   emit the schema's top-level `dryRun` boolean from the decision alone.
5. **`Msg.ProvenanceWritten`** was added alongside `Wrote` so the driver distinguishes a lock-write ack from
   the `refresh.json` artifact write (both reify failures to `ToolError`).

**Evidence:** real-filesystem end-to-end regeneration through the F051 process port (a deterministic `cp`
generator), by-digest re-run idempotence, `--dry-run` byte-for-byte no-mutation guard, byte-identical
`refresh.json` + committed golden, network-free + product-neutrality scope guards, and a real-host CLI smoke
run capturing all six exit codes (`readiness/cli-smoke.txt`). F014/F029/F051/F052 untouched. No synthetic
evidence was required.
