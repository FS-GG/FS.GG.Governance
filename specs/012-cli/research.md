# Phase 0 - Research (F12 - 012-cli)

Engineering decisions for the optional CLI tool. The spec intentionally left command-line
option spelling, output schema details, project layout, and installation command to planning.
This document fixes those choices and explains why they fit the existing F08/F09/F10/F11
system. No clarification remains unresolved.

## D1 - Ship a new packable `FS.GG.Governance.Cli` tool, not a library-only helper

- **Decision**: Add `src/FS.GG.Governance.Cli` as a packable executable .NET tool with
  `ToolCommandName` = `fsgg-governance`. The user runs
  `fsgg-governance route|explain|contract|evidence ...`. During development the same surface
  is run with `dotnet run --project src/FS.GG.Governance.Cli -- <command> ...`.
- **Rationale**: FR-001/FR-016 require an optional end-user command surface that can be packed
  and installed from `~/.local/share/nuget-local/`. Keeping it in a separate project preserves
  the constitution's one-way dependency direction: the CLI may depend on Host/SPI/adapters/
  Kernel, but none of them depend on the CLI.
- **Alternatives considered**:
  - Add command helpers to `FS.GG.Governance.Host`. Rejected because Host is the reusable
    effects shell; CLI parsing, process exits, and tool packaging are user-boundary concerns.
  - Make the tool mandatory in the solution build. Rejected: it is optional for consumers and
    must not become a prerequisite for ordinary builds/tests.

## D2 - Use a small local argv parser and fixed command schema

- **Decision**: Implement a local parser for:
  `fsgg-governance <route|explain|contract|evidence> [--root <path>] [--mode sandbox|inner|gate] [--format text|json] [--json] [--scope <path>[,<path>...]] [--review-budget <n>] [--review-store <path>] [--out <path>] [--judge-model <id>] [--judge-version <version>]`.
  Defaults: `--root .`, `--mode inner`, `--format text`, `--review-budget 0`, selected
  domains = all shipped domains, and the default judge identity named in the CLI contract.
- **Rationale**: The parser surface is small and stable; a new parser dependency would add
  maintenance cost without reducing meaningful complexity. The fixed schema gives users and CI
  one documented contract and makes malformed invocations a deterministic usage error.
- **Alternatives considered**:
  - Add `System.CommandLine`. Rejected for F12: dependency minimization is explicit in the
    constitution, and the required grammar is a handful of options.
  - Per-command option sets. Rejected initially; all four commands need the same snapshot,
    mode, output, budget, and store options.

## D3 - Use explicit exit categories with sysexits-compatible codes

- **Decision**: Return `0` for success/advisory/no-gates, `2` for a governed blocking failure,
  `64` for malformed usage, `66` for unavailable input/safe-failure at the command boundary,
  and `70` for unexpected tool defects. The JSON envelope always includes the symbolic category
  and numeric code.
- **Rationale**: CI needs to distinguish "the governed change failed a gate" from "the command
  could not run" (FR-010/FR-017). `2` is intentionally not a generic crash code; it means the
  governance engine produced a blocking result. `64/66/70` follow familiar sysexits meanings
  without requiring a package.
- **Alternatives considered**:
  - Collapse all failures to `1`. Rejected because it erases the spec's required distinction.
  - Treat missing governed artifacts as success. Rejected because safe failure must be visible.

## D4 - Compose the shipped adapters at the CLI root

- **Decision**: Author a concrete project coproduct in `Project.fsi/fs`: `ProjectFact` carries
  Spec Kit facts, design-system facts, project-level governance outcomes, artifact-content
  facts, evidence nodes, and freshness observations. `Project.compose` lifts the F10 Spec Kit
  adapter and F11 design-system adapter through F09 `Composition`, then `Project.toLoopConfig`
  produces the F08 `LoopConfig`.
- **Rationale**: F09 intentionally deferred the concrete project coproduct to the consumer root.
  F12 is the first real root, so it is the correct place to combine the two adapters. This
  proves the adoption bar in an end-user tool while keeping both adapters independent.
- **Alternatives considered**:
  - Run one adapter per command and merge reports by hand. Rejected: that would bypass F09
    composition and risk a second routing/contract implementation.
  - Put the project coproduct in SPI. Rejected: SPI is generic machinery, not this repository's
    concrete product composition.

## D5 - CLI MVU wraps the existing Host MVU

- **Decision**: `Cli.fsi` exposes `Model`, `Msg`, `Effect`, `init`, and `update` for the command
  boundary. The CLI MVU handles parse/normalize, snapshot loading, host execution, output
  writing, and exit selection. The existing F08 `Loop` remains the governance MVU core for
  artifact sensing, review-cache lookup, rule evaluation, and output values.
- **Rationale**: The constitution requires an MVU boundary for I/O-bearing workflows, and F08
  already provides the inner governance boundary. F12 adds process concerns around it; keeping
  those concerns in a second, small MVU layer makes them testable without changing Host.
- **Alternatives considered**:
  - Put argument parsing and exit logic directly in `Program.fs`. Rejected because it would
    make the command contract hard to test through the public surface.
  - Change Host to know about command kinds. Rejected because Host should stay domain-neutral.

## D6 - Budget fresh reviews at the edge, cache hits remain free

- **Decision**: Default review budget is `CacheOnly` (`0`). The CLI edge counts review requests,
  cache hits, cache misses, fresh dispatch attempts, pending reviews, and budget exhaustion.
  Cache hits never consume budget. A fresh dispatch occurs only when `freshDispatches < budget`;
  otherwise the review is left pending/uncertain and the evidence output records the exhausted
  key.
- **Rationale**: The spec's decision #5 belongs at the CLI boundary where a user or CI grants
  cost/latency. Host still emits `DispatchReview` for cache misses; F12's edge interpreter is
  where the command decides whether to spend. This keeps cost consent outside pure evaluation.
- **Alternatives considered**:
  - Let Host dispatch and make the judge port fail when over budget. Rejected because it would
    count as an attempted dispatch and blur "not attempted due to budget" with "dispatch failed".
  - Add a global environment variable. Rejected; explicit per-invocation options are auditable.

## D7 - Deterministic output uses existing kernel renderers plus one stable envelope

- **Decision**: Text uses existing `Route.renderRoute` and `Contract.render` where available,
  plus compact CLI-owned summaries for explanation/evidence. JSON uses a CLI envelope with
  fixed fields and stable order, embedding kernel JSON shapes from `Json.ofExplanation`,
  `Json.ofContract`, and `Json.ofEffective` rather than serializing those structures a second
  way. No implicit current time is emitted.
- **Rationale**: FR-009/SC-002 require byte-for-byte stable JSON. Reusing kernel JSON for
  kernel values avoids drift; the CLI envelope adds command metadata, exit category, review
  budget state, failures, and report type in one stable place.
- **Alternatives considered**:
  - One JSON shape per command with no common envelope. Rejected because CI needs a consistent
    exit and failure contract.
  - Include wall-clock run duration by default. Rejected because it breaks deterministic output.

## D8 - Evidence report folds declared/effective/freshness/review facts without hiding states

- **Decision**: `evidence` returns a report with evidence nodes, declared state, effective state
  (including `AutoSynthetic` as its own state), freshness, dependencies, review cache hits and
  misses, pending reviews, budget exhaustion, disclosures, and safe failures. Missing inputs are
  input/safe-failure records, not tool defects.
- **Rationale**: F05 and F06 already model evidence state and freshness; F08 records disclosures
  and safe failures. The CLI's job is to bring those facts into one command output and preserve
  distinctions that matter to trust: synthetic vs auto-synthetic, stale vs failed, pending vs
  skipped, cache miss vs over-budget.
- **Alternatives considered**:
  - Only report pass/fail route state. Rejected; the feature exists to make evidence auditable.
  - Collapse all non-real evidence to "not real". Rejected because the spec explicitly requires
    distinct states.

## D9 - Read-only guarantee is tested by filesystem snapshots and git diff

- **Decision**: Commands may read the governed root, read/write the review cache outside the
  root, and write an explicit `--out` report. Tests run commands against fixture roots and this
  repository's `.specify` tree, then assert governed files are unchanged.
- **Rationale**: FR-014/SC-009 make read-only operation a product contract. The easiest honest
  evidence is a real filesystem run with tracked snapshots before/after.
- **Alternatives considered**:
  - Rely on code inspection. Rejected because a CLI tool's contract is operational.
  - Put the review cache under `.specify`. Rejected because that would mutate the governed root
    by default.

## D10 - Packaging and surface drift are first-class success criteria

- **Decision**: Add `surface/FS.GG.Governance.Cli.surface.txt`, a CLI surface-drift test, and a
  packaging smoke test that packs to `~/.local/share/nuget-local/`, installs/runs
  `fsgg-governance`, and verifies the same command contract as the built project.
- **Rationale**: F12's deliverable is a tool artifact, not only a test runner. Surface drift and
  packaged smoke runs prove both constitution Principle II and SC-008/SC-010.
- **Alternatives considered**:
  - Delay packaging tests to release. Rejected because packaging is part of this feature's
    acceptance criteria.
