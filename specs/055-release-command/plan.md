# Implementation Plan: The `fsgg release` Host Command

**Branch**: `055-release-command` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/055-release-command/spec.md`

## Summary

Wire the missing host that runs the release gate end to end. `fsgg release` reads a governed
repository's declared release configuration, **senses** the six release-rule families from the real
repository (reusing F054 `senseRelease`/`realPort`), **evaluates** the declared rules against the
sensed facts (reusing F053 `evaluateRelease` verbatim), **renders** the verdict as human text and an
optional deterministic `release.json` audit artifact, and **exits** with one of five distinguishable
codes so CI can block a non-compliant release.

The command is a new standalone executable, `FS.GG.Governance.ReleaseCommand`, built to the exact
pure-core + injected-edge shape of the existing `ship`/`route`/`cache-eligibility` commands: a pure
`Loop` MVU boundary (parse → init/update → render → exit-code), an `Interpreter` that binds real
ports at the edge, and a thin `Program.fs` entry. The deterministic `release.json` projection ships as
a separate pure library, `FS.GG.Governance.ReleaseJson`, mirroring the `AuditJson`/`RouteJson`/
`GatesJson`/`CacheEligibilityJson` precedent.

**Confirmed planning decision (this plan):** the new *release declaration surface* (the per-family
rules, expectations, and source layout) is a **row-local adapter** — a new `.fsgg/release.yml` read
through the established `Loader.FileReader` port and parsed by a `Declaration` module inside
`ReleaseCommand`. **F014 `Config`'s frozen four-file schema, schema version, and surface baselines are
NOT edited.** This keeps F014 product-neutral and bounds the blast radius to the two new projects.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`).

**Primary Dependencies**: FSharp.Core 10.1.301; `System.Text.Json` (BCL, deterministic JSON — no new
dependency, the `AuditJson`/`RouteJson` `Utf8JsonWriter` precedent); YamlDotNet 16.3.0 (already pinned,
already used by F014 `Config` — reused by the row-local `release.yml` adapter, **no new package**).
Project references: F053 `ReleaseRules`, F054 `ReleaseFactsSensing`, F014 `Config` (for
`Loader.FileReader`/`fileSystemReader`, `GovernedPath`, `SurfaceId`, `Severity`, `Maturity`), and the
new `ReleaseJson`.

**Storage**: The local governed repository working directory (read-only) plus one explicitly-requested
output file, `release.json` (atomic temp-then-rename write). No database, no network, no registry.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck/FsCheck 2.16.6 (the repo standard). Real temp-repository
fixtures via a `withTempRepo` helper (the F016/F054/ShipCommand precedent): faked ports over real cores
for unit coverage, one real-filesystem end-to-end proof, byte-identical re-run determinism, and a
network-free scope guard mirroring F054.

**Target Platform**: Cross-platform .NET CLI executable (Linux/macOS/Windows); CI gate usage.

**Project Type**: CLI host command (one new executable project) + one new pure projection library, with
matching test projects — single-solution F# layout.

**Performance Goals**: Not a hot path. One pass over six small declared source files plus pure
evaluation/projection; sub-second for a normal repository. No performance-driven mutation needed.

**Constraints**: Deterministic, byte-identical `release.json` for identical repository state (no
timestamps/paths/machine-specific content); network-free (verifiable by the scope guard); fail-safe
(missing/unreadable/unexpected source ⇒ `Unrecoverable`/unmet, never a fabricated `Met` and never a
crash); product-neutral (no hardcoded identity/version/field/pin/posture/path/layout); the governed
repository is never mutated except the requested `release.json`.

**Scale/Scope**: Two new `src` projects (~6 small modules) + two new test projects. No change to F053,
F054, or F014 semantics — this row composes them.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Each new public module is drafted as a
  `.fsi` and exercised in FSI (`scripts/prelude.fsx`) before its `.fs` body; semantic tests call the
  packed/loaded public surface (`Loop.parse`, `Interpreter.run`, `ReleaseJson.ofRelease`,
  `Declaration.parse`), not internals.
- **II. Visibility Lives in `.fsi`** — PASS. Every public module ships a curated `.fsi`
  (`Loop`, `Interpreter`, `Declaration`, `ReleaseJson`); `.fs` bodies carry no access modifiers; a
  surface-drift test + committed baseline is added per new public module
  (`surface/FS.GG.Governance.ReleaseCommand.surface.txt`, `…ReleaseJson.surface.txt`).
- **III. Idiomatic Simplicity** — PASS. Plain records, closed DUs, pipelines, exhaustive matches; no
  SRTP/reflection/type-providers/custom CEs/non-trivial active patterns. No new dependency (YamlDotNet
  and System.Text.Json are already pinned/used). Any local mutation in the JSON writer follows the
  disclosed `AuditJson` precedent.
- **IV. Elmish/MVU Is the Boundary** — PASS. The command is a stateful, I/O-bearing workflow, so it is
  modeled as an MVU boundary: `Model`/`Msg`/`Effect`, pure `init`/`update`, and an edge `Interpreter`
  that executes effects (catalog/declaration read, repository sensing, atomic artifact write, stdout)
  and turns results back into `Msg` — the exact ShipCommand shape. `ReleaseJson` and `Declaration` are
  pure leaves and need no MVU ceremony.
- **V. Test Evidence Is Mandatory** — PASS. Tests fail before / pass after; real temp-repo fixtures and
  real upstream cores (F053/F054 never mocked). Synthetic substitutes, if any, are disclosed at the use
  site, carry `Synthetic` in the test name, and are listed in the PR — none are anticipated.
- **VI. Observability and Safe Failure** — PASS. Diagnostics distinguish missing/malformed **input**
  (absent/invalid `release.yml`, absent source files, bad argv) from a **tool defect** in both message
  and exit code; fail-safe families are `Unrecoverable`/unmet, never silently passing; no swallowed
  exceptions in the critical path.

**Change Classification: Tier 1 (contracted change)** — adds new public API surface (two new projects
with public `.fsi`). Requires the full chain: spec, plan, `.fsi`, surface-area baselines, test evidence,
and documentation. No public-API change to any existing project (F014/F053/F054 untouched), so no
migration guidance is owed to existing consumers.

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/055-release-command/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── cli.md           #   fsgg release argv + exit-code contract
│   └── release.schema.md#   release.json deterministic projection contract
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.ReleaseJson/                 # NEW — pure deterministic release.json projection
│   ├── ReleaseJson.fsi                           #   ofRelease : ReleaseDecision -> SensedRelease -> string ; schemaVersion
│   ├── ReleaseJson.fs                            #   Utf8JsonWriter walk; emit-only; exhaustive token helpers
│   └── FS.GG.Governance.ReleaseJson.fsproj       #   refs: ReleaseRules, ReleaseFactsSensing, Config
│
└── FS.GG.Governance.ReleaseCommand/              # NEW — the fsgg release host executable
    ├── Declaration.fsi                           #   parse .fsgg/release.yml -> Result<ReleaseDeclaration, DeclError>
    ├── Declaration.fs                            #   row-local YamlDotNet adapter (F014 schema untouched)
    ├── Loop.fsi                                  #   pure MVU: RunRequest/parse, Model/Msg/Effect, init/update/render, ExitDecision/exitCode
    ├── Loop.fs
    ├── Interpreter.fsi                           #   Ports bundle, realPorts, step, run (edge I/O)
    ├── Interpreter.fs
    ├── Program.fs                                #   [<EntryPoint>] thin host (parse -> realPorts -> run -> exit)
    └── FS.GG.Governance.ReleaseCommand.fsproj    #   refs: ReleaseRules, ReleaseFactsSensing, Config, ReleaseJson, YamlDotNet

tests/
├── FS.GG.Governance.ReleaseJson.Tests/           # NEW — determinism, schema/golden, no-contradiction projection tests
└── FS.GG.Governance.ReleaseCommand.Tests/        # NEW — Parse/Loop/Interpreter/Declaration/EndToEnd/Determinism/Failure/Degrade/ScopeGuard/SurfaceDrift + Support.fs (withTempRepo)

surface/
├── FS.GG.Governance.ReleaseJson.surface.txt      # NEW — committed surface baseline
└── FS.GG.Governance.ReleaseCommand.surface.txt   # NEW — committed surface baseline

FS.GG.Governance.sln                              # EDIT — add the four new projects (mirror ShipCommand entries)
```

**Structure Decision**: Mirror the established command precedent exactly. Host commands are standalone
executables (`RouteCommand`/`ShipCommand`/`CacheEligibilityCommand`), and deterministic JSON projections
are separate pure libraries (`AuditJson`/`RouteJson`/`GatesJson`/`CacheEligibilityJson`). This row adds
`FS.GG.Governance.ReleaseCommand` (the executable, with the row-local `Declaration` adapter inside it)
and `FS.GG.Governance.ReleaseJson` (the pure projection), plus their test projects and surface
baselines. No central `fsgg` dispatcher exists or is introduced.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
