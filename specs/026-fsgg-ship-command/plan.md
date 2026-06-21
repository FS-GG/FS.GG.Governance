# Implementation Plan: `fsgg ship` Host Command (Protected-Branch Verdict)

**Branch**: `026-fsgg-ship-command` (active spec; git branch currently `main`) | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/026-fsgg-ship-command/spec.md`

## Summary

Land the **second host edge** of the Phase-2 line — the **`fsgg ship`** command — the design's
protected-branch gate. It is the host sibling of the merged `fsgg route` command (F022): where `route`
*showed* the selected gates and **always exited 0** because "selecting many gates is information, never a
failure," `ship` turns the same routed change into a pass/fail **merge verdict** under a chosen run mode
and profile, writes the **`audit.json`** document a protected branch reads, and **exits with a numeric
code CI can block on**. Pointed at a repository root it (1) selects the changed-path scope — the same
`--paths`/`--since`/default base-head sensing F022 established; (2) loads and validates the project's
`.fsgg` catalog (F014); (3) routes the change (F015), builds the whole-catalog gate registry (F018),
computes unknown-governed-path findings (F017), and selects the gates it reaches (F019) — the exact F022
composition; (4) **rolls the selection up into a `ShipDecision`** under the chosen mode/profile via
`Ship.rollup` (F024), which reuses the F023 enforcement levers and effective severity; (5) **projects
that decision to `audit.json`** via `AuditJson.ofShipDecision` (F025) verbatim; (6) **persists** it to
disk; (7) prints a deterministic human-or-JSON summary of the verdict and its blockers/warnings/passing
partition; and (8) **maps the decision's `ExitCodeBasis` to a numeric exit code** — `Clean` → 0, `Blocked`
→ a single distinct non-zero "blocked" code reserved for a blocked merge and used for nothing else.

The **one genuinely new behavior** relative to `fsgg route` is the **verdict and its consequence**: the
exit-code taxonomy gains a `Blocked` category that route deliberately omitted, and `update` now composes
two further pure cores (`Ship.rollup`, `AuditJson.ofShipDecision`) after `Route.select`. Everything else
— scope sensing, catalog load+validate, routing, registry, findings, selection, the MVU boundary, the
injected fakeable ports, the compute-then-write-with-atomic-rename discipline, the safe-failure taxonomy
for tool errors — is the F022 shape reused. It **re-derives, re-sorts, re-classifies, and re-serializes
nothing** the pure cores already fixed: severity/verdict/partition come from F023/F024 and the document
bytes come from F025, unchanged.

Because the command performs **multi-step external I/O** ending in a **blocking process exit code**, it
is a stateful host edge and MUST be modeled through an **Elmish/MVU boundary** (Constitution Principle
IV; spec "Boundary discipline" assumption) — the exact `Loop`(pure)/`Interpreter`(edge)/`Program`(thin)
shape F022 established. The pure composition decision (scope → load → route → registry → findings →
select → **rollup → project** → persist → summarize → **exit-from-basis**) lives in a pure `update`; all
I/O is `Effect` data executed only by an interpreter at the edge, behind **injected, fakeable ports**, so
the whole composition *including the verdict and the exit-code mapping* is exercised deterministically
with faked git and filesystem (FR-013, SC-007) — no real `git` process in the test.

The work lands as a new optional project **`FS.GG.Governance.ShipCommand`** plus its test project —
continuing the one-row-one-project rhythm of F014–F025, at the composition/edge tier (like
`RouteCommand`), not another pure leaf. It **reuses verbatim**, via project references: the eight F022
cores (`Config`, `Snapshot`, `Routing`, `Findings`, `Gates`, `Route` — note **not** `RouteJson`/`GatesJson`,
which `ship` does not project) **plus** the three Phase-5/projection cores this row newly composes —
`Enforcement` (F023, for the `recognizeMode`/`recognizeProfile` lever parsers and the `RunMode`/`Profile`
types), `Ship` (F024, for `rollup` and the `ShipDecision` vocabulary), and `AuditJson` (F025, for
`ofShipDecision`). It adds **no new third-party `PackageReference`**: it issues no git of its own (calls
`Snapshot`), reads no `.fsgg` of its own (calls `Config`), enforces/rolls-up nothing of its own (calls
`Enforcement`/`Ship`), and serializes nothing of its own (calls `AuditJson`); the only new I/O is writing
the returned document string to disk via the same injected `ArtifactWriter` port F022 introduced. The
byte-stability of `audit.json` is **inherited unchanged** from the F025 projection — this feature
introduces no clock, absolute path, or environment value into the persisted document, and the exit code
is a deterministic function of the verdict.

This is the design's *"Run `fsgg ship --mode gate --profile standard --json` as a protected boundary"*
acceptance item and the `fsgg ship` row of `docs/initial-implementation-plan.md`. The remaining Phase-2
row — GitHub Actions branch-protection guidance (how to wire this exit code into a protected branch) —
stays out of scope, as do release/provenance attestation references and cache/freshness evaluation
(deferred exactly as F024/F025 deferred them).

**Confirmed during planning (the plan-time reconciliations the spec deferred — research D1–D11):**

- **Project home (D1)**: a new project `FS.GG.Governance.ShipCommand` (OutputType `Exe`) mirroring
  `RouteCommand`, referencing the six reused F022 cores plus `Enforcement`/`Ship`/`AuditJson`. The
  single-packed-`fsgg`-tool unification (one packed tool that dispatches the `route` and `ship` verbs) is
  an explicitly **deferred** follow-up; for this slice the project is `IsPackable=false` (built and
  tested as an Exe referenced by its test project, exactly as `RouteCommand.Tests` references its Exe), so
  this row does not ship a second NuGet tool that also claims the `fsgg` `ToolCommandName` F022 already
  owns.
- **Boundary (D2)**: the same local MVU/effect algebra as F022 — pure `Loop` (`parse`/`init`/`update`/
  `render`/`exitCode`) + edge `Interpreter` (`realPorts`/`step`/`run`) + thin `Program`.
- **Ports (D3)**: the **identical** F022 `Ports` bundle — reused `Config.Loader.FileReader` (catalog) +
  `Snapshot.Ports` (git) + the new-in-F022 `ArtifactWriter` (persist) + `OutputSink` (summary). The only
  artifact written is `audit.json` (one write, not two).
- **Scope (D4)**: mirror F022's scope surface — `--paths` / `--since <rev>` / default base-head, the same
  mutually-exclusive semantics — so a base-blocking change can be driven deterministically in tests via
  `--paths` without a real diff.
- **Levers (D5)**: `--mode <m>` / `--profile <p>`, parsed in the pure `parse` via
  `Enforcement.recognizeMode`/`recognizeProfile`; an `Unrecognized` value is a `UsageError`
  (exit 2, no artifact). Omitted flags default to **`--mode gate --profile standard`** (the design's
  canonical protected-branch invocation); the applied levers are recorded in the audit document because
  every `EnforcedItem` carries its F023 `Mode`/`Profile` (F025 emits them).
- **Exit taxonomy (D6)**: `ExitDecision = Success | Blocked | UsageError' | InputUnavailable | ToolError`
  → numeric **`0 / 1 / 2 / 3 / 4`**. `Blocked = 1` is the single code reserved for a blocked merge
  verdict and is **distinct from every tool-failure code** (usage 2, input-unavailable 3, tool-error 4) —
  the FR-009/SC-004 load-bearing distinction. `ExitCodeBasis.Clean → Success`, `ExitCodeBasis.Blocked →
  Blocked`.
- **Output location (D7)**: `audit.json` → `<repo>/readiness/audit.json` by default (the `route.json`
  sibling location F022 established, minus the SDD `<id>` segment that does not exist in this
  Governance-only skeleton), overridable via `--audit-out <path>`.
- **Summary (D8)**: a pure `render` — human text by default; on `--json`, the **F025 `audit.json`
  document text verbatim** (it *is* the deterministic machine contract, so the stdout JSON inherits
  F025 byte-stability and equals the persisted file).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (from `Directory.Build.props`), `LangVersion latest`.

**Primary Dependencies**: Project references only — the six reused F022 cores
`FS.GG.Governance.Config` (F014), `FS.GG.Governance.Snapshot` (F016), `FS.GG.Governance.Routing` (F015),
`FS.GG.Governance.Findings` (F017), `FS.GG.Governance.Gates` (F018), `FS.GG.Governance.Route` (F019) —
**plus** the three cores this row newly composes: `FS.GG.Governance.Enforcement` (F023),
`FS.GG.Governance.Ship` (F024), `FS.GG.Governance.AuditJson` (F025). **No new third-party
`PackageReference`.** The only impure primitives are `System.IO` (writing the one document string; the
catalog read and git sensing are delegated to `Config`/`Snapshot`) and `System.Environment.Exit` at the
`Program` edge — both from the `net10.0` shared framework, the same `System.*`/FSharp.Core-only posture
as `RouteCommand`. It does **not** reference `RouteJson`/`GatesJson` (it projects neither).

**Storage**: The filesystem — but only as an **output**: the command writes `readiness/audit.json` (path
overridable). All reads (catalog files, git) go through the already-built `Config`/`Snapshot` edges; this
feature owns no new read path.

**Testing**: `dotnet test` (Expecto + FsCheck via VSTest). Pure-side tests drive `update` with literal
`Model`/`Msg` values and assert the next `Model` + emitted `Effect`s (no I/O), including the
`ExitCodeBasis → ExitDecision` mapping on the terminal transition. Edge tests run the interpreter against
**faked ports** (in-memory `FileReader`, an in-memory git `Ports` over a fixed snapshot, a capturing
`ArtifactWriter`/`OutputSink`) and assert the bytes written equal `AuditJson.ofShipDecision` of the
`Ship.rollup` of the same typed inputs (SC-001, SC-002, SC-007), and that the same change under two
lever sets produces the two expected verdicts/exit codes (SC-003). At least one **real-temp-git +
real-catalog** end-to-end proof runs through `realPorts` (the `Snapshot` `withTempRepo` fixture pattern),
asserting the verdict, the persisted bytes, and the exit code. Real evidence preferred; any synthetic
carries the `Synthetic` token and a use-site disclosure (Principle V).

**Target Platform**: Cross-platform .NET command-line tool; validated on the Linux dev host.

**Project Type**: Optional F# tool (composition/edge tier) plus one test project — the `RouteCommand`
shape (Exe referenced by its test project), not the pure-leaf shape of F023–F025.

**Performance Goals**: Not throughput-bound. The command does work proportional to the changed-path set
and the declared catalog, senses git once, rolls up the selection once, and writes one small document.
Determinism and a stable exit code, not latency, are the contract.

**Constraints**: The pure `update` is total and performs no I/O, no git, no clock (Principle IV). The
persisted `audit.json` inherits F025 byte-stability and carries no wall-clock, machine-absolute path, or
environment-derived value (FR-006, SC-005). The interpreter NEVER throws — every failure (not-a-repo,
unresolved revision, missing/invalid catalog, unrecognized lever, unwritable output) surfaces as a
diagnostic + a category-mapped tool-failure exit code, and **no partial/malformed `audit.json` is ever
written** (FR-010, FR-013, SC-004). The blocked-verdict exit code is distinct from every tool-failure
code and a tool failure is never reported as success (FR-009). The no-hide rule is honored end to end: a
base-blocking item relaxed by the profile appears as a self-explaining warning carrying both base and
effective severity (FR-011) — inherited unchanged from F024/F025.

**Scale/Scope**: One new production project (`FS.GG.Governance.ShipCommand`: a pure MVU `Loop` module, an
edge `Interpreter` module with the injected `Ports`, and a thin `Program` entry) + one test project. Nine
inward project references; zero new packages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | Public surface drafted as `.fsi` (`Loop.fsi`, `Interpreter.fsi`) and exercised in `scripts/prelude.fsx` before any `.fs` body; semantic tests call the packed surface (`parse`/`update`/`render`/`exitCode`, `Interpreter.run`), not internals. |
| II. Visibility in `.fsi` | PASS | Every public module gets a curated `.fsi` (`Loop`, `Interpreter`); the `.fs` files carry no `private`/`internal`/`public` modifiers; a surface-drift baseline `surface/FS.GG.Governance.ShipCommand.surface.txt` is added (Polish phase). |
| III. Idiomatic Simplicity | PASS | Plain records/DUs/pipelines; reuses existing cores verbatim — no SRTP, reflection, type providers, custom operators, or non-trivial CEs. Argv parsing is a small explicit matcher mirroring F022's. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **PASS — and load-bearing here** | Multi-step external I/O (git sensing, catalog reads, one artifact write, summary emit) ending in a **blocking process exit code**. Modeled as pure `Model`/`Msg`/`Effect`/`init`/`update` + an edge interpreter executing effects through injected ports — the F022 pattern. `update` is pure (including the `ExitCodeBasis → exit` decision); I/O is data; interpretation only at the edge. |
| V. Test Evidence Is Mandatory | PASS | Pure transition tests (Model+Msg → Model+Effects, incl. verdict→exit mapping), interpreter tests against faked ports (written bytes = F025 projection of F024 rollup; two-lever-set verdicts; twice-run byte-identical), and at least one real-temp-git + real-catalog end-to-end proof (SC-007). Synthetic uses disclosed with the `Synthetic` token. |
| VI. Observability & Safe Failure | **PASS — load-bearing** | Each tool-failure category (not-a-repo / unavailable git, unresolved revision, missing-or-invalid catalog, unrecognized lever, unwritable output) emits a distinct, actionable diagnostic and maps to a distinct non-zero exit code (2/3/4); the **blocked verdict (1)** is distinct from all of them (FR-009, SC-004); a tool defect is never reported as bad input, a blocked merge, or a success. No silent failure; the interpreter never throws; no partial artifact on failure. |
| Change Classification | **Tier 1** | New public API surface (a new project with public `.fsi` modules) and a new user/CI command contract (flags, the new **blocking** exit code, on-disk `audit.json` location). Full chain: spec, plan, `.fsi`, surface baseline, tests, docs. No new dependency. |
| Engineering Constraints | PASS | `net10.0`; curated `.fsi` per public module; MVU boundary; surface baseline + drift test; no new package (git/filesystem/serialization/enforcement/rollup all delegated to existing cores); generic — no rendering package IDs/paths assumed. `IsPackable=false` this slice (single-`fsgg`-tool unification deferred), so no premature pack/tool-name collision with F022. |

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/026-fsgg-ship-command/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — the plan-time reconciliations (D1–D11)
├── data-model.md        # Phase 1 — Model/Msg/Effect, ports, lever parsing, exit taxonomy, audit location
├── quickstart.md        # Phase 1 — build/run/test validation guide + acceptance→evidence map
├── contracts/           # Phase 1 — public .fsi contracts + the command/artifact wire contract
│   ├── Loop.fsi                     # pure MVU surface (Model/Msg/Effect/init/update/render/exitCode)
│   ├── Interpreter.fsi              # edge surface (Ports, realPorts, step, run)
│   └── fsgg-ship-command.md         # CLI contract: flags, levers, exit codes, written-artifact location
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.ShipCommand/            # NEW — the protected-branch verdict host edge
├── FS.GG.Governance.ShipCommand.fsproj      # OutputType Exe; IsPackable=false (tool unification deferred);
│                                            #   ProjectReferences: Config, Snapshot, Routing, Findings,
│                                            #   Gates, Route, Enforcement, Ship, AuditJson
├── Loop.fsi / Loop.fs                        # PURE MVU: RunRequest (incl. Mode/Profile), ScopeSelector,
│                                            #   Model, Msg, Effect, ExitDecision (incl. Blocked), init,
│                                            #   update (… → rollup → project → persist → exit-from-basis),
│                                            #   render, exitCode
├── Interpreter.fsi / Interpreter.fs          # EDGE: Ports (FileReader + Snapshot Ports + ArtifactWriter +
│                                            #   OutputSink — the F022 bundle), realPorts, step, run
└── Program.fs                               # thin argv → parse → Interpreter.run → exit edge

tests/FS.GG.Governance.ShipCommand.Tests/    # NEW
├── FS.GG.Governance.ShipCommand.Tests.fsproj
├── Support.fs                               # in-memory FileReader / git Ports / ArtifactWriter fakes;
│                                            #   real-temp-git + real-catalog fixture helper (F022 reuse)
├── ParseTests.fs                            # argv → RunRequest; scope exclusivity; --mode/--profile
│                                            #   recognition + defaults; unrecognized lever → UsageError
├── LoopTests.fs                             # pure update: load→route→…→rollup→project→persist→exit;
│                                            #   ExitCodeBasis Clean→Success / Blocked→Blocked
├── InterpreterTests.fs                      # faked-ports edge: audit.json bytes = F025(F024 rollup);
│                                            #   two lever sets → two verdicts/exit codes; twice-run identical
├── FailureTests.fs                          # the tool-failure categories → distinct diag + exit 2/3/4,
│                                            #   each ≠ blocked code 1, no artifact written
├── EndToEndTests.fs                         # real temp git + real catalog: full composition + verdict (SC-007)
├── SurfaceDriftTests.fs                     # surface baseline for Loop + Interpreter
└── Main.fs                                  # Expecto entry

surface/FS.GG.Governance.ShipCommand.surface.txt   # NEW public-surface baseline
```

**Structure Decision**: A single new composition/edge project mirroring the `RouteCommand` shape — a
pure `Loop` (MVU core) + an `Interpreter` (edge with injected `Ports`) + a thin `Program`. It sits one
tier above the nine pure/edge cores it references and is the first command in the repo to emit a real
merge **verdict** with a **blocking** exit code. The artifact it writes lives at `readiness/audit.json`
(overridable), per research D7.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
