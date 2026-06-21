# Implementation Plan: `fsgg route` Host Command

**Branch**: `022-fsgg-route-command` (active spec; git branch currently `main`) | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/022-fsgg-route-command/spec.md`

## Summary

Land the first **host edge** that wires the Phase-2 pure cores together over a real repository: the
**`fsgg route`** command. Pointed at a repository root, it (1) selects the changed-path scope —
explicit `--paths`, `--since <rev>`, or the default sensed base/head range; (2) loads and validates
the project's `.fsgg` catalog (F014); (3) routes the changed paths to capabilities (F015); (4) builds
the whole-catalog gate registry (F018); (5) computes unknown-governed-path findings (F017); (6) selects
the gates the change reaches (F019); (7) projects the two deterministic documents — whole-catalog
`gates.json` (F021) and per-change `route.json` (F020); (8) **persists** them to disk; and (9) prints a
deterministic human-or-JSON summary. It re-derives, re-sorts, and re-classifies nothing those cores
already fixed, and computes **no ship verdict** — no merge decision, severity, profile, mode,
enforcement, cache-eligibility verdict, blockers, or exit-code-from-blockers (those are `fsgg ship` /
`audit.json` / Phase 5 / Phase 11).

Because the command performs **multi-step external I/O** — git sensing, catalog file reads, two
artifact writes, and a summary write — it is a stateful host edge and MUST be modeled through an
**Elmish/MVU boundary** (Constitution Principle IV; spec FR-012, "Boundary discipline" assumption),
unlike the immediately-preceding pure projections F020/F021. The pure composition decision (scope →
load → route → registry → findings → select → project → persist-plan → summarize → exit) lives in a
pure `update`; all I/O is represented as `Effect` data and executed only by an interpreter at the edge,
behind **injected, fakeable ports** so the whole composition is exercised deterministically with faked
git and filesystem (FR-012, SC-007) — no real `git` process in the test.

The work lands as a new optional, packable project **`FS.GG.Governance.RouteCommand`** plus its test
project — continuing the one-row-one-project rhythm of F014–F021, but as the **composition/edge tier**
(like `Host`/`Cli`), not another pure leaf. It **reuses verbatim**, via project references: F014
`Config` (catalog load+validate behind its injected `FileReader`), F016 `Snapshot` (git sensing behind
its injected `Ports`), F015 `Routing`, F017 `Findings`, F018 `Gates`, F019 `Route`, F020 `RouteJson`,
F021 `GatesJson`. It adds **no new third-party `PackageReference`**: it issues no git of its own (it
calls `Snapshot.Interpreter`), reads no `.fsgg` of its own (it calls `Config.Loader`), and serializes
nothing of its own (it calls the F020/F021 projections); the only new I/O is writing the returned
document strings to disk via `System.IO` and a new injected `ArtifactWriter` port. The byte-stability of
the artifacts is **inherited unchanged** from the F020/F021 projections — this feature introduces no
clock, absolute path, or environment value into the persisted documents.

This is the design's *"Route a local scoped change cheaply and explain selected gates"* acceptance item
and the `fsgg route` row of `docs/initial-implementation-plan.md` (line 381), sliced to `route` alone;
`fsgg ship --mode gate --profile standard --json`, `audit.json`, and the branch-protection guidance
remain later Phase-2 / Phase-5 rows.

**Confirmed during planning (the four plan-time reconciliations the spec deferred — research D1, D5,
D8, D9):**

- **Project home (D1)**: a new packable project `FS.GG.Governance.RouteCommand` (OutputType `Exe`,
  `PackAsTool`, `ToolCommandName fsgg`) that references the eight cores above. It is the composition/edge
  tier — *not* an extension of the older kernel-era `FS.GG.Governance.Host`/`FS.GG.Governance.Cli`
  (distinct lineage over the kernel MVU; spec "Project home / command surface" assumption). The `route`
  verb is the only subcommand this row ships; the `fsgg` tool name leaves room for `ship` later.
- **Boundary (D2)**: a local MVU/effect algebra (pure `Model`/`Msg`/`Effect`/`init`/`update` + an
  edge interpreter), the same shape `Host.Loop`/`Host.Interpreter` and `Snapshot`/`Config.Loader` use —
  not the heavier Elmish `Program` runtime (Principle IV permits a local algebra for CLIs/small tools).
- **Output locations (D5)**: `gates.json` → `.fsgg/gates.json` (the design's whole-catalog location,
  `docs/initial-design.md:431`); `route.json` → `readiness/route.json` by default (the design's
  canonical `readiness/<id>/route.json` minus the `<id>` segment, which comes from the SDD work-item
  model that does not exist in this Governance-only skeleton — spec "Output location" assumption). Both
  overridable via flags.
- **Flag surface (D8)**: `fsgg route [--repo <dir>] [--paths <p> ...] [--since <rev>] [--json]
  [--gates-out <path>] [--route-out <path>]`. `--paths` and `--since` are mutually exclusive (usage
  error if both); neither ⇒ default base/head.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (from `Directory.Build.props`), `LangVersion latest`.

**Primary Dependencies**: Project references only — `FS.GG.Governance.Config` (F014),
`FS.GG.Governance.Snapshot` (F016), `FS.GG.Governance.Routing` (F015), `FS.GG.Governance.Findings`
(F017), `FS.GG.Governance.Gates` (F018), `FS.GG.Governance.Route` (F019),
`FS.GG.Governance.RouteJson` (F020), `FS.GG.Governance.GatesJson` (F021). **No new third-party
`PackageReference`.** The only impure primitives are `System.IO` (writing the two document strings; the
catalog read and git sensing are delegated to `Config`/`Snapshot`) and `System.Environment.Exit` at the
`Program` edge — both from the `net10.0` shared framework, the same `System.*`/FSharp.Core-only posture
as `Host`.

**Storage**: The filesystem — but only as **outputs**. The command writes `.fsgg/gates.json` and
`readiness/route.json` (paths overridable). All reads (catalog files, git) go through the already-built
`Config`/`Snapshot` edges; this feature owns no new read path.

**Testing**: `dotnet test` (Expecto + FsCheck via VSTest). Pure-side tests drive `update` with literal
`Model`/`Msg` values and assert the next `Model` + emitted `Effect`s (no I/O). Edge tests run the
interpreter against **faked ports** (in-memory `FileReader`, an in-memory git `Ports` over a literal
`RawSensing`/fixed snapshot, and an in-memory `ArtifactWriter` capturing writes) — and, for at least one
end-to-end proof, against a **real temp git repo** with a real catalog (the `Snapshot` `withTempRepo`
fixture pattern), asserting the bytes written match `RouteJson.ofRouteResult` / `GatesJson.ofGateRegistry`
of the same typed inputs (SC-001, SC-007). Real evidence preferred; any synthetic carries the `Synthetic`
token and a use-site disclosure (Principle V).

**Target Platform**: Cross-platform .NET command-line tool; validated on the Linux dev host.

**Project Type**: Optional packable F# tool (composition/edge tier) plus one test project — the
`Host`/`Cli` shape (Exe referenced by its test project), not the pure-leaf shape of F020/F021.

**Performance Goals**: Not throughput-bound. The design promise is *targeted, cheap* routing — the
command does work proportional to the changed-path set and the declared catalog, runs the read-only git
sensing once, and writes two small documents. Determinism, not latency, is the contract.

**Constraints**: The pure `update` is total and performs no I/O, no git, no clock (Principle IV). The
persisted artifacts inherit F020/F021 byte-stability and carry no wall-clock, machine-absolute path, or
environment-derived value (FR-006, SC-005). The interpreter NEVER throws — every failure (not-a-repo,
unresolved revision, missing/invalid catalog, unwritable output) surfaces as a diagnostic + a
category-mapped non-zero exit code, and no partial/malformed artifact is ever written (FR-010, FR-013,
SC-004). A change that selects many gates or yields many findings is **information**, never a failure
(FR-009, FR-011).

**Scale/Scope**: One new production project (`FS.GG.Governance.RouteCommand`: a pure MVU `Loop` module,
an edge `Interpreter` module with the injected `Ports`, and a thin `Program` entry) + one test project.
Eight inward project references; zero new packages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | Public surface drafted as `.fsi` (`Loop.fsi`, `Interpreter.fsi`) and exercised in `scripts/prelude.fsx` before any `.fs` body; semantic tests call the packed surface (`update`, `Interpreter.run`), not internals. |
| II. Visibility in `.fsi` | PASS | Every public module gets a curated `.fsi` (`Loop`, `Interpreter`); the `.fs` files carry no `private`/`internal`/`public` modifiers; a surface-drift baseline is added (Polish phase). |
| III. Idiomatic Simplicity | PASS | Plain records/DUs/pipelines; reuses existing cores verbatim — no SRTP, reflection, type providers, custom operators, or non-trivial CEs. Argv parsing is a small explicit matcher. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **PASS — and load-bearing here** | This is the row's whole point: multi-step external I/O (git sensing, catalog reads, two artifact writes, summary emit). Modeled as pure `Model`/`Msg`/`Effect`/`init`/`update` + an edge interpreter executing effects through injected ports — the `Host`/`Snapshot` pattern. `update` is pure; I/O is data; interpretation only at the edge. |
| V. Test Evidence Is Mandatory | PASS | Pure transition tests (Model+Msg → Model+Effects), interpreter tests against faked ports, and at least one real-temp-git + real-catalog end-to-end proof (SC-007). Synthetic uses disclosed with the `Synthetic` token. |
| VI. Observability & Safe Failure | PASS | Each failure category (not-a-repo / unavailable git, unresolved revision, missing-or-invalid catalog, unwritable output) emits a distinct, actionable diagnostic and maps to a distinct non-zero exit code; tool defect is never reported as bad input or vice-versa (FR-010, SC-004). No silent failure; the interpreter never throws. |
| Change Classification | **Tier 1** | New public API surface (a new project with public `.fsi` modules) and a new user/CI command contract (flags, exit codes, on-disk artifact locations). Full chain: spec, plan, `.fsi`, surface baseline, tests, docs. No new dependency. |
| Engineering Constraints | PASS | `net10.0`; curated `.fsi` per public module; MVU boundary; surface baseline + drift test; no new package (git/filesystem/serialization all delegated to existing edges); generic — no rendering package IDs/paths assumed; honors the `~/.local/share/nuget-local/` pack location if/when packed. |

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/022-fsgg-route-command/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — the plan-time reconciliations (D1–D10)
├── data-model.md        # Phase 1 — Model/Msg/Effect, ports, exit taxonomy, artifact locations
├── quickstart.md        # Phase 1 — build/run/test validation guide + acceptance→evidence map
├── contracts/           # Phase 1 — public .fsi contracts + the command/artifact wire contract
│   ├── Loop.fsi                     # pure MVU surface (Model/Msg/Effect/init/update + render)
│   ├── Interpreter.fsi              # edge surface (Ports, realPorts, run)
│   └── fsgg-route-command.md        # CLI contract: flags, exit codes, written-artifact locations
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.RouteCommand/          # NEW — the host-edge composition tier
├── FS.GG.Governance.RouteCommand.fsproj    # OutputType Exe; PackAsTool; ToolCommandName fsgg;
│                                            #   ProjectReferences: Config, Snapshot, Routing,
│                                            #   Findings, Gates, Route, RouteJson, GatesJson
├── Loop.fsi / Loop.fs                       # PURE MVU: RunRequest, ScopeSelector, Model, Msg,
│                                            #   Effect, ExitDecision, init, update, exitCode, render
├── Interpreter.fsi / Interpreter.fs         # EDGE: Ports (FileReader + Snapshot Ports +
│                                            #   ArtifactWriter + OutputSink), realPorts, step, run
└── Program.fs                               # thin argv → parse → Interpreter.run → exit edge

tests/FS.GG.Governance.RouteCommand.Tests/   # NEW
├── FS.GG.Governance.RouteCommand.Tests.fsproj
├── Support.fs                               # in-memory FileReader / git Ports / ArtifactWriter fakes;
│                                            #   real-temp-git + real-catalog fixture helper
├── ScopeParseTests.fs                       # argv → RunRequest; --paths/--since exclusivity (US2/US4)
├── LoopTests.fs                             # pure update: scope→load→route→…→persist-plan→exit
├── InterpreterTests.fs                      # faked-ports edge: artifacts written = F020/F021 bytes
├── FailureTests.fs                          # the four failure categories → distinct diag + exit code
├── EndToEndTests.fs                         # real temp git + real catalog: full composition (SC-007)
├── SurfaceDriftTests.fs                     # surface baseline for Loop + Interpreter
└── Main.fs                                  # Expecto entry

surface/FS.GG.Governance.RouteCommand.surface.txt   # NEW public-surface baseline
```

**Structure Decision**: A single new composition/edge project mirroring the `Host` shape — a pure
`Loop` (MVU core) + an `Interpreter` (edge with injected `Ports`) + a thin `Program`. It sits one tier
above the eight pure/edge cores it references and is the only project in the repo (besides the
kernel-era `Cli`) that composes them end-to-end. The artifacts it writes live at `.fsgg/gates.json` and
`readiness/route.json` (overridable), per research D5.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Implementation Progress

**Status: ✅ COMPLETE (2026-06-21).** All 36 tasks (T001–T036) done with real evidence; the full
solution suite is green (no regressions across the 15 projects).

| Phase | Tasks | Status | Evidence |
|---|---|---|---|
| 1. Setup | T001–T010 | ✅ | New packable `FS.GG.Governance.RouteCommand` (Exe/PackAsTool/`fsgg`) + test project, both `.fsi` contracts, faked-port + real-temp-git `Support.fs`, prelude F022 sketch, readiness note. |
| 2. Foundation | T011–T016 | ✅ | Pure `parse`/`init`/`update`/`render`/`exitCode` + edge `step`/`run`/`realPorts` — total, no wildcard exit map, no `GovernedBlocking`. |
| 3. US1 (route → persist) | T017–T021 | ✅ | `LoopTests` (pure composition → Result + 2 WriteArtifact + Done) and `InterpreterTests` (written bytes = F021/F020 projections, summary, exit 0; routine/empty-diff/empty-catalog all exit 0). |
| 4. US2 (scope) | T022–T024 | ✅ | `ScopeParseTests` (paths/since/default, exclusivity, defaults, flags, errors) + `InterpreterTests` three-scope routing (ExplicitPaths bypasses git). |
| 5. US3 (determinism/format) | T025–T028 | ✅ | `InterpreterTests` twice-run byte-identical artifacts + `--json` summary; format suppression; exclusion sweep (no verdict/severity/profile/mode/enforcement/cache/blockers/clock/abs-path). |
| 6. US4 (safe failure) | T029–T031 | ✅ | `FailureTests` four categories → distinct diagnostics + exit 2/3/4, no artifact, interpreter never throws. |
| 7. Polish | T032–T036 | ✅ | Surface baseline `surface/FS.GG.Governance.RouteCommand.surface.txt` + drift/dependency test; one real-temp-git end-to-end proof; quickstart + CLI smoke; this section. |

**Test evidence**: 36 tests green in `tests/FS.GG.Governance.RouteCommand.Tests` (pure `Loop`, faked-port
`Interpreter`, four-category failure, one real-temp-git end-to-end, surface drift). **CLI smoke** (real
repo, real catalog): `fsgg route --since HEAD~1` selects `package-api:build`, writes both artifacts,
exits 0; `--json` emits the machine summary; two runs are byte-identical; not-a-repo → exit 3; usage
error → exit 2. See [readiness/README.md](./readiness/README.md) for the SC→evidence map.

**Real evidence, no synthetic**: every case is reachable from real `TypedFacts`/`RepoSnapshot` through
faked ports plus one real git repo — no `Synthetic`-tokened test, none disclosed.

**One contract correction**: `contracts/Interpreter.fsi` originally typed `Ports.Git` as
`Interpreter.Ports`; the F016 `Ports` record lives at the `Snapshot` namespace level (not inside its
`Interpreter` module), so the shipped surface (and the contract) use `FS.GG.Governance.Snapshot.Ports`.
