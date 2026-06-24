# Implementation Plan: The `fsgg verify` Host Command

**Branch**: `056-verify-command` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/056-verify-command/spec.md`

## Summary

Wire the pending pre-PR host that runs profile-appropriate product verification end to end. `fsgg verify`
reads a governed repository's declared catalog, **selects** the profile-appropriate checks for the change
scope (reusing F015 `Routing.route` → F018 `Gates.buildRegistry` → F017 `Findings` → F019 `Route.select`),
**rolls** the selection up into a blocking/advisory verdict (reusing F024 `Ship.rollup` — but threaded with
`RunMode.Verify`, **not** `Gate`), **runs** each selected check whose evidence is stale and **reuses** the
prior evidence of each check whose evidence is fresh (reusing the F043/F041 freshness + cache-eligibility
join and the F051/F052 execution port), **surfaces** the freshness/reuse evaluation as first-class
**currency findings** (per check: fresh/reused vs stale/recomputed; plus the changed freshness categories
for a stale generated view), **renders** a clear verdict as human text and an optional deterministic
`verify.json` artifact, and **exits** with one of five distinguishable codes so a pre-PR CI step can fail
early on a blocking-severity check.

The command is a new standalone executable, `FS.GG.Governance.VerifyCommand`, built to the exact
pure-core + injected-edge shape of the existing `route`/`ship`/`cache-eligibility`/`release` commands: a
pure `Loop` MVU boundary (parse → init/update → render → exit-code), an `Interpreter` that binds the
**same** reused edge ports as `ShipCommand` (catalog reads, git sensing, freshness/store sensing, the gate
execution port, an atomic artifact writer, a stdout sink), and a thin `Program.fs`. The deterministic
`verify.json` projection ships as a separate pure library, `FS.GG.Governance.VerifyJson`, mirroring the
`AuditJson`/`RouteJson`/`GatesJson`/`ReleaseJson` precedent.

**Confirmed planning decisions (this plan):**

1. **Verify is the ShipCommand pipeline run in `Verify` enforcement mode.** The F023 `RunMode` enum already
   carries a dedicated `Verify` case (ordinal 3, below `Gate`). Verify reuses `Ship.rollup`/`applyExecution`
   **verbatim** and threads `RunMode.Verify`; the enforcement dials then decide effective severity for the
   pre-PR stage. This is the mechanical expression of **FR-017** (verify is not the merge authority): verify
   **exposes no `--mode` flag** — the mode is fixed to `Verify`, so a developer cannot escalate verify into
   the `Gate`-mode merge verdict. `--profile` remains overridable (default `Standard`).
2. **No new declaration surface and no new sensing.** Unlike F055 `release` (which read a row-local
   `.fsgg/release.yml`), verify composes the cores that already read the **frozen F014 catalog** that
   `route`/`ship` read. There is **no `Declaration` adapter** and **no new repository sensing**: "validate
   generated views/evidence currency" is the existing F043/F041 freshness/cache-eligibility evaluation
   surfaced (and acted on) by this command. The F014 schema and the F015–F052 cores are reused **verbatim,
   not edited** — the blast radius is the two new projects (`VerifyCommand`, `VerifyJson`) and their tests.
3. **Currency findings are a projection, not a new severity path.** A currency finding labels the existing
   per-check freshness/cache-eligibility disposition (fresh/reused, stale/recomputed, recompute-by-default)
   and, for a stale generated view, names the changed freshness categories. The verdict and the
   blocking/advisory split are driven **only** by `Ship.rollup`/`applyExecution` at `RunMode.Verify`; an
   unmet blocking-severity check (whether stale-then-recomputed-to-fail, no-command, or uncertain) is a
   Blocker through the **existing** rollup at its enforcement-assigned severity. Verify never invents a
   second route to `Blocked` and never coerces an uncertain result to pass (research D2).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`).

**Primary Dependencies**: FSharp.Core 10.1.301; `System.Text.Json` (BCL `Utf8JsonWriter` — the
`AuditJson`/`RouteJson`/`ReleaseJson` deterministic-JSON precedent; **no new dependency**). Project
references reused verbatim: F014 `Config` (catalog reads / `Loader.FileReader`), F016 `Snapshot` (git
sensing), F015 `Routing`, F017 `Findings`, F018 `Gates`, F019 `Route`, F023 `Enforcement`, F024 `Ship`,
F043 `FreshnessResolution`, F041 `CacheEligibility`, F046 `FreshnessSensing`/`FreshnessKey`,
F047/F048 `EvidenceReuse`/`EvidenceReuseStore`, F049 `EvidenceCapture`, F051 `GateExecution`,
F052 `GateRun` — the **exact** ShipCommand reference set — plus the new `VerifyJson`. **No YamlDotNet**
(verify reads no new declaration file; that was release-specific).

**Storage**: The local governed repository working directory (read-only) plus one explicitly-requested
output file, `verify.json` (atomic temp-then-rename write), and the opt-in evidence-reuse store write the
shared F048 cores already perform under `--persist-store`. No database, no network, no registry.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck/FsCheck 2.16.6 (repo standard). Real temp-repository fixtures
via a `withTempRepo` helper (the ShipCommand/ReleaseCommand precedent): faked ports over real cores for
unit coverage, one real-filesystem end-to-end proof, byte-identical re-run determinism, a no-mutation
guard, and a network-free scope guard.

**Target Platform**: Cross-platform .NET CLI executable (Linux/macOS/Windows); pre-PR CI step usage.

**Project Type**: CLI host command (one new executable project) + one new pure projection library, with
matching test projects — single-solution F# layout.

**Performance Goals**: Not a hot path. One scope sense + one catalog pass + pure selection/rollup + the
already-bounded gate executions (only the stale checks recompute); sub-second beyond the checks' own cost.
No performance-driven mutation needed.

**Constraints**: Deterministic, byte-identical `verify.json` for identical repository state and identical
check outcomes (no timestamps/abs-paths/usernames/machine-specific content); printed machine output equals
the persisted file verbatim (one source of truth); network-free own logic (verifiable by a scope guard);
fail-safe (missing/unreadable/unexpected input ⇒ unmet/uncertain or a distinct non-success exit, never a
fabricated passing verdict and never a crash); product-neutral (no hardcoded identity/version/field/path/
profile/check identity); the governed repository is never mutated except the requested `verify.json` and
the opt-in store write; no partial artifact on a tool error.

**Scale/Scope**: Two new `src` projects (`VerifyCommand` ≈ Loop/Interpreter/Program; `VerifyJson` ≈ one
projection module) + two new test projects + two surface baselines. No change to any existing project's
semantics or public surface — this row composes them.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Each new public module is drafted as a `.fsi`
  and its composition proven before its `.fs` body — either by exercising it in FSI (`scripts/prelude.fsx`) or,
  per the F055 precedent, by `dotnet build` of both `src` projects plus the semantic suites loading the packed
  public surface (T009 records which method was used). Semantic tests call the public surface (`Loop.parse`,
  `Interpreter.run`, `VerifyJson.ofVerifyDecision`), not internals.
- **II. Visibility Lives in `.fsi`** — PASS. Every public module ships a curated `.fsi` (`Loop`,
  `Interpreter`, `VerifyJson`); `.fs` bodies carry no access modifiers; a surface-drift test + committed
  baseline is added per new public module (`surface/FS.GG.Governance.VerifyCommand.surface.txt`,
  `…VerifyJson.surface.txt`).
- **III. Idiomatic Simplicity** — PASS. Plain records, closed DUs, pipelines, exhaustive matches; no
  SRTP/reflection/type-providers/custom CEs/non-trivial active patterns. No new dependency. Any local
  mutation in the JSON writer follows the disclosed `AuditJson` precedent.
- **IV. Elmish/MVU Is the Boundary** — PASS. The command is a stateful, I/O-bearing workflow, so it is
  modeled as an MVU boundary: `Model`/`Msg`/`Effect`, pure `init`/`update`/`render`, and an edge
  `Interpreter` that executes effects (scope sense, catalog read, freshness/store sense, gate execution,
  atomic artifact write, optional store write, stdout) and turns results back into `Msg` — the exact
  ShipCommand shape. `VerifyJson` is a pure leaf and needs no MVU ceremony.
- **V. Test Evidence Is Mandatory** — PASS. Tests fail before / pass after; real temp-repo fixtures and
  real upstream cores (F015–F052 never mocked — only the edge ports are faked). Synthetic substitutes, if
  any, are disclosed at the use site, carry `Synthetic` in the test name, and are listed in the PR — none
  are anticipated.
- **VI. Observability and Safe Failure** — PASS. Diagnostics distinguish missing/malformed **input**
  (absent/invalid catalog, git-sensing failure, bad argv) from a **tool defect** (write/execution-port
  failure) in both message and exit code; freshness/store sensing failures degrade to a safe default + a
  non-fatal currency note that never perturbs the verdict or exit; no swallowed exceptions in the critical
  path; no partial artifact on a tool error.

**Change Classification: Tier 1 (contracted change)** — adds new public API surface (two new projects with
public `.fsi`). Requires the full chain: spec, plan, `.fsi`, surface-area baselines, test evidence, and
documentation. No public-API change to any existing project (F014–F052 untouched), so no migration guidance
is owed to existing consumers.

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/056-verify-command/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── cli.md           #   fsgg verify argv + exit-code contract
│   └── verify.schema.md #   verify.json deterministic projection contract
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.VerifyJson/                  # NEW — pure deterministic verify.json projection
│   ├── VerifyJson.fsi                            #   ofVerifyDecision : ShipDecision -> CacheEligibilityReport option
│   │                                             #                      -> (GateId * GateOutcome) list -> string ; schemaVersion
│   ├── VerifyJson.fs                             #   Utf8JsonWriter walk; emit-only; exhaustive token helpers; `currency` section
│   └── FS.GG.Governance.VerifyJson.fsproj        #   refs: Ship, Enforcement, CacheEligibility, GateRun, EvidenceReuse
│
└── FS.GG.Governance.VerifyCommand/               # NEW — the fsgg verify host executable
    ├── Loop.fsi                                  #   pure MVU: RunRequest/parse, Model/Msg/Effect, init/update/render,
    │                                             #             ExitDecision/exitCode, applyExecution (reused-verbatim shape)
    ├── Loop.fs
    ├── Interpreter.fsi                           #   Ports bundle (IDENTICAL to ShipCommand's), realPorts, step, run
    ├── Interpreter.fs
    ├── Program.fs                                #   [<EntryPoint>] thin host (parse -> realPorts -> run -> exit)
    └── FS.GG.Governance.VerifyCommand.fsproj     #   refs: the exact ShipCommand reference set + VerifyJson (NOT AuditJson)

tests/
├── FS.GG.Governance.VerifyJson.Tests/            # NEW — determinism, schema/golden, currency-section, no-contradiction tests
└── FS.GG.Governance.VerifyCommand.Tests/         # NEW — Parse/Loop/Interpreter/Execution/EndToEnd/Determinism/Failure/
                                                  #        Degrade/NoMutation/ScopeGuard/SurfaceDrift + Support.fs (withTempRepo)

surface/
├── FS.GG.Governance.VerifyJson.surface.txt       # NEW — committed surface baseline
└── FS.GG.Governance.VerifyCommand.surface.txt    # NEW — committed surface baseline

FS.GG.Governance.sln                              # EDIT — add the four new projects (mirror ShipCommand/ReleaseCommand entries)
```

**Structure Decision**: Mirror the established command precedent exactly. Host commands are standalone
executables (`RouteCommand`/`ShipCommand`/`CacheEligibilityCommand`/`ReleaseCommand`), and deterministic
JSON projections are separate pure libraries (`AuditJson`/`RouteJson`/`GatesJson`/`CacheEligibilityJson`/
`ReleaseJson`). This row adds `FS.GG.Governance.VerifyCommand` (the executable) and
`FS.GG.Governance.VerifyJson` (the pure projection), plus their test projects and surface baselines.
Verify is the **closest sibling of `ShipCommand`** — same pipeline, same edge ports, same reference set —
differing only in (a) the fixed `RunMode.Verify`, (b) the first-class currency-findings projection, (c) the
`verify.json` schema id, and (d) the pre-PR framing/labels and "nothing to verify" empty-selection report.
No central `fsgg` dispatcher exists or is introduced; a leading bare `verify` token is tolerated per the
existing command precedent.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

## Implementation Status (2026-06-24) — COMPLETE

All 39 tasks landed; 63 tests green (19 `VerifyJson.Tests` + 44 `VerifyCommand.Tests`); the full
solution builds clean (`TreatWarningsAsErrors=true`). The plan was followed verbatim:

- **`FS.GG.Governance.VerifyJson`** — `schemaVersion = "fsgg.verify/v1"` + a pure, total
  `ofVerifyDecision : ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string`,
  a hand-driven `Utf8JsonWriter` walk (the AuditJson precedent) with exhaustive token helpers, the
  tagged `id`/`enforcement` (`mode` always `"verify"`)/`cache`/`execution` item shape, and the
  first-class `currency` (`fresh`/`recomputed`/`unresolved`) section. Refs Ship/Enforcement/
  CacheEligibility/GateRun/EvidenceReuse; no YamlDotNet, no AuditJson.
- **`FS.GG.Governance.VerifyCommand`** — the MVU `Loop` (no `Mode` field, `VerifyArtifact`,
  `VerifyDoc`, currency render, "nothing to verify" empty-selection short-circuit) threading the
  fixed `RunMode.Verify` into the verbatim `Ship.rollup`/`applyExecution`; the edge `Interpreter`
  with the IDENTICAL ShipCommand `Ports` bundle; a thin `Program`. `--mode` is an `UnknownFlag`
  (FR-017). The F014–F052 cores are reused **verbatim, not edited**; no `Declaration` adapter, no
  new sensing.
- **Currency is a projection, not a second severity path** (plan decision 3 / research D2):
  the verdict and the blocking/advisory split are driven only by `Ship.rollup`/`applyExecution`;
  an uncertain (exit-125) blocking check is never coerced to pass (FR-005), proved distinctly from
  a clean pass. Freshness/store sensing failures degrade safely with a non-fatal currency note.
- **Determinism / one source of truth**: two runs over identical state write byte-identical
  `verify.json`; `--json` stdout equals the persisted file verbatim; a failed write is a `ToolError`
  (exit 4) with no partial artifact, distinct from `Blocked` (exit 1).
- Surface baselines + golden committed and drift-tested; the real `fsgg verify` host binary was
  smoke-driven against a real temp git repo through its public CLI (vertical slice).
