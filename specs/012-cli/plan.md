# Implementation Plan: The CLI Tool - Route, Explain, Contract, and Evidence Reports for a Repo Snapshot

**Branch**: `012-cli` (active spec; git branch currently `main`) | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-cli/spec.md`

## Summary

Add the **F12 command-line tool** as a new optional, packable .NET tool project:
`FS.GG.Governance.Cli`. The tool exposes the four user commands named in the spec:
`route`, `explain`, `contract`, and `evidence`. It runs against a read-only repository
snapshot, supports deterministic text and JSON output, and returns documented exit
decisions for advisory success, governed blocking failure, usage/input errors, and tool
defects.

The implementation reuses the already-shipped system rather than introducing a second
governance engine. F12 supplies the concrete project composition root that lifts the F10
Spec Kit adapter and the F11 design-system adapter into one project coproduct, builds the
F08 `LoopConfig`, and drives the F08 host loop at the command boundary. The CLI owns only
the user boundary: argument parsing, run request normalization, snapshot sensing,
fresh-review budget enforcement, output selection, report-file emission, packaging, and
process exit codes.

Fresh agent review dispatch is cache-only by default. A caller must grant a nonzero
`--review-budget` before the edge may attempt fresh review calls, and the edge must never
dispatch more calls than the granted budget. When the budget is exhausted, the affected
review remains pending/uncertain and the output reports that decision explicitly.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: No new runtime `PackageReference`. The CLI project references
the existing projects: `FS.GG.Governance.Kernel`, `FS.GG.Governance.Host`,
`FS.GG.Governance.Adapters.Spi`, `FS.GG.Governance.Adapters.SpecKit`, and
`FS.GG.Governance.Adapters.DesignSystem`. Command parsing is a small local parser over
`argv` and JSON uses `System.Text.Json` from the shared framework plus existing
`FS.GG.Governance.Kernel.Json` folds. Test-only packages remain the centrally pinned
Expecto/FsCheck/VSTest packages already in `Directory.Packages.props`.

**Storage**: No governed-repository mutation. The four commands read the target root and
may write only caller-selected report files. Review-cache reads/writes go to an explicit
`--review-store` path or the user cache directory outside the governed root by default.
Packaging emits to `~/.local/share/nuget-local/` per the constitution.

**Testing**: `dotnet test`. Tests exercise the public CLI surface and packaged/built
command runner, not private helpers: parser normalization, MVU transitions, budget gating,
snapshot sensing over real fixture directories, text/JSON determinism, exit decisions,
read-only guarantees, surface drift, and packaged tool smoke runs. Existing kernel/host/
adapter tests continue to run through `dotnet test`.

**Target Platform**: Cross-platform .NET CLI tool; validated on the Linux dev host.

**Project Type**: Packable CLI application plus test project. The tool command is
`fsgg-governance`, with subcommands `route`, `explain`, `contract`, and `evidence`.

**Performance Goals**: Deterministic, bounded command runs rather than throughput. A
single invocation reads each required artifact once, de-duplicates artifact reads by
`ArtifactRef`, avoids fresh review dispatch unless budgeted, and produces byte-for-byte
stable JSON for identical explicit inputs.

**Constraints**: Read-only with respect to governed repositories; no second evaluator,
router, evidence engine, contract renderer, or explanation engine; CLI orchestration
through an MVU boundary; stable JSON without implicit wall-clock fields; explicit exit
codes; package optionality so consumers can build/test without installing the tool.

**Scale/Scope**: One new production project and one test project:
`src/FS.GG.Governance.Cli` and `tests/FS.GG.Governance.Cli.Tests`. Public modules are
`Cli` and `Project`, each with curated `.fsi` signatures and a CLI surface baseline.
The concrete composition covers the shipped F10/F11 adapters and leaves external-customer
validation and issue/task conversion to F13.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design - still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | **PASS** | [`contracts/Cli.fsi`](./contracts/Cli.fsi), [`contracts/Project.fsi`](./contracts/Project.fsi), and [`contracts/command-schema.md`](./contracts/command-schema.md) define the public command/API surface before implementation. `tasks.md` must order `.fsi`, FSI/prelude sketch, semantic tests, implementation, then packaging. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `Cli.fsi` and `Project.fsi` are the public surface for the new component. Implementation `.fs` files carry no top-level access modifiers. Add `surface/FS.GG.Governance.Cli.surface.txt` and a surface-drift test. |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs, a small local argv parser, and BCL JSON. No new command-line parser dependency, no reflection-based runtime model, no SRTP, no type providers, and no custom operators. The project coproduct and active-pattern prisms are the same established F09 composition style. |
| IV. Elmish/MVU boundary | **PASS** | The CLI has its own `Model`/`Msg`/`Effect`/`init`/`update` boundary for parse -> snapshot -> host run -> output -> exit. The existing F08 `Loop` remains the governance sense/plan/act core. Fresh-review budget gating is an edge effect, never hidden inside pure evaluation. |
| V. Test evidence mandatory | **PASS** | Tests use real fixture directories, this repository's `.specify` tree, the built CLI entry, and the packaged tool from the local feed. Synthetic pieces are limited to judge responses where real agent calls are not reproducible; those tests must carry `Synthetic` in the name and disclose the reason. |
| VI. Observability & safe failure | **PASS** | Output and exit decisions distinguish malformed invocations, missing/unreadable inputs, review-store failures, budget exhaustion, governed blocking results, and unexpected defects. Evidence output reports declared/effective evidence, freshness, cache hits/misses, pending reviews, disclosures, and safe failures. |
| Change Classification | **Tier 1** | New end-user command surface, new packable artifact, new public `.fsi`, new API baseline, observable process contract, and optional tool packaging. |
| Engineering Constraints | **PASS** | `net10.0`, `FS.GG.Governance.*` identity, pack output under `~/.local/share/nuget-local/`, no new runtime dependency, one-way dependency direction: CLI -> Host/SPI/adapters/Kernel only; none of those projects reference the CLI. |

**Gate result: PASS - no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/012-cli/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- Cli.fsi
|   |-- Project.fsi
|   `-- command-schema.md
`-- tasks.md                 # Created by /speckit-tasks, not by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Cli/                         # NEW packable optional tool
|-- FS.GG.Governance.Cli.fsproj                   # OutputType Exe; PackAsTool true; ToolCommandName fsgg-governance
|-- Project.fsi                                   # = contracts/Project.fsi
|-- Project.fs                                    # concrete F10+F11 composition root and snapshot fact helpers
|-- Cli.fsi                                       # = contracts/Cli.fsi
|-- Cli.fs                                        # parser, CLI MVU core, output/exit decisions
`-- Program.fs                                    # thin argv -> Cli.run -> stdout/stderr/exit edge

tests/FS.GG.Governance.Cli.Tests/                 # NEW semantic + packaging tests
|-- FS.GG.Governance.Cli.Tests.fsproj
|-- fixtures/                                     # repo snapshots for light/advisory/blocking/missing/stale/budget cases
|-- ParserTests.fs
|-- MvuTests.fs
|-- SnapshotTests.fs
|-- OutputTests.fs
|-- PackagingTests.fs
|-- ReadOnlyTests.fs
|-- SurfaceDriftTests.fs
`-- Main.fs

scripts/prelude.fsx                               # extend with F12 command-runner sketch
surface/FS.GG.Governance.Cli.surface.txt          # NEW public surface baseline
FS.GG.Governance.sln                              # add CLI project and CLI test project
```

**Structure Decision**: a new `FS.GG.Governance.Cli` executable/tool project, separate from
the kernel, host, SPI, and adapters. This keeps the dependency direction one-way and makes
the command boundary optional. The production tool references both concrete adapters because
F12 is the first real composition root; tests may run individual domains and the composed
project. The CLI does not alter the existing adapter projects and does not require consumer
repositories to reference or install the tool.

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
