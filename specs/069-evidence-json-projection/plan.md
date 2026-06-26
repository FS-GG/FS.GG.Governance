# Implementation Plan: Effective-Evidence `evidence.json` Projection Host

**Branch**: `069-evidence-json-projection` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/069-evidence-json-projection/spec.md`

## Summary

Make the effective-evidence world a **first-class, deterministic, versioned `evidence.json` artifact** and a
dedicated **information-only host** that emits it — the Governance-owned sibling of `route.json` /
`audit.json` / `verify.json` / `cache-eligibility.json`. The evidence world is *already computed* today:
`FS.GG.Governance.Cli`'s `Project.evidenceReport : Host.Model<ProjectFact> -> ProjectEvidenceReport` folds the
SpecKit task states, design-system measurements, and review-cache outcomes into a node list (each node carrying
**declared** `EvidenceState`, **effective** `EvidenceState`, `Freshness`, and `Source`), the dependency edges,
disclosures, and host failures — running `Kernel.Evidence.build`/`effective` internally. What is missing is a
trustworthy *projection* of that report: the current inline `Cli.evidenceJson` emits `"kind":"evidence"` with
**no `schemaVersion`**, **silently drops `report.Failures`**, leaves **`Kernel.Evidence.build`'s `GraphError`
swallowed to `Map.empty`** upstream (no graph failure is surfaced anywhere), and renders freshness as a bare
`Fresh`/`Stale`/`null` string with **no stale cause and no named missing facts**.

This feature delivers a new pure projection leaf **`FS.GG.Governance.EvidenceJson`**
(`schemaVersion = "fsgg.evidence/v1"`, `ofReport : EvidenceDocument -> string`) plus a new standalone host
**`FS.GG.Governance.EvidenceCommand`** (`fsgg evidence`), built on the same MVU boundary as the
`cache-eligibility` host. The host reuses the existing F12 project-sensing edge and `Project.evidenceReport`
**verbatim**, then composes `Kernel.Evidence.build`/`effective` at its own edge to surface graph failures **by
name** (FR-004) instead of swallowing them, and composes `FreshnessResolution`/`EvidenceReuse` cause vocabulary
to name *why* a node is stale or unresolved (FR-003). It writes `readiness/evidence.json`.

This is a **Tier 1** contracted change: one new packable public projection module + one new host surface join
the API. It is **purely additive** — it changes **no** existing projection signature, **no** existing artifact
or schema version, and **no** verdict or exit-code basis. `route.json` / `audit.json` / `verify.json` /
`cache-eligibility.json` and their goldens stay byte-identical (SC-005); the inline `Cli.evidenceJson` emitter
and `Project.evidenceReport` are **left untouched** (the new host re-runs `Evidence.build` at its own edge to
recover the failure the existing bridge discards). The host's own exit code reflects only operational outcome,
never a ship/merge verdict (FR-007).

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`), matching the rest of the solution.

**Primary Dependencies** (all in-repo; **no new external/NuGet dependency** — `System.Text.Json` from the
shared framework only, exactly as every sibling `*Json` projection):

- **NEW** `FS.GG.Governance.EvidenceJson` — a **pure, packable projection leaf**. Defines the
  `EvidenceDocument` wire model and `ofReport : EvidenceDocument -> string` + `schemaVersion: string`.
  References only **`FS.GG.Governance.Kernel`** (`EvidenceState`, `Freshness`, `GraphError<'id>`) and the
  freshness-cause vocabulary projects **`FS.GG.Governance.FreshnessResolution`** (`MissingFact`,
  `missingFactToken`) and **`FS.GG.Governance.EvidenceReuse`** (`RecomputeCause`) — for naming *why* a node is
  stale/unresolved. It takes a ProjectReference on no command/host/Cli project, so it stays a leaf and cannot
  introduce a cycle (mirrors `CacheEligibilityJson → CacheEligibility` only).
- **NEW** `FS.GG.Governance.EvidenceCommand` — the standalone **Exe** host (`ToolCommandName fsgg`, verb
  `evidence`), structured exactly like `FS.GG.Governance.CacheEligibilityCommand`: a pure `Loop`
  (`parse`/`init`/`update`/`render`/`exitCode`) over `Model`+`Msg` emitting `Effect` data, and an edge
  `Interpreter` (real ports) that runs the I/O. It references **`FS.GG.Governance.Cli`** (for
  `Project.evidenceReport`, `Project.compose`/`toLoopConfig`, the `ProjectEvidenceReport`/`EvidenceNodeReport`
  shapes), **`FS.GG.Governance.Host`** (the project sensing loop + `Disclosure`/`Failure`),
  **`FS.GG.Governance.Kernel`** (`Evidence.build`/`effective`/`GraphError`), the freshness cores
  (`FreshnessResolution`/`EvidenceReuse`/`FreshnessSensing`) for US2 enrichment, `FS.GG.Governance.Snapshot`
  for change scope, `FS.GG.Governance.HumanText` for the `--format human`/`--plain` summary, and the new
  `EvidenceJson` projection.
- **REUSED VERBATIM** — `Kernel.Evidence` (`build`/`effective`/`GraphError<'id>` with cases `Cycle` /
  `UnknownNode` / `AutoSyntheticDeclared`), `Kernel.Freshness` (`Fresh`/`Stale`), `Project.evidenceReport`
  (the declared/effective/freshness/dependency/disclosure/failure fold), `FreshnessResolution`
  (`ResolutionOutcome` = `Resolved`/`Unresolved of MissingFact list`, `missingFactToken`), `EvidenceReuse`
  (`RecomputeCause` = `NoPriorEvidence`/`InputsChanged of InputCategory list`), `FreshnessSensing`
  (`senseFreshness`), and the F12 project-sensing path. **No core is re-opened.**

**Storage**: filesystem only — one new deterministic artifact, `readiness/evidence.json` (overridable via
`--out`). No new persisted store, no sidecar. The projection is pure; the host writes atomically (temp+rename),
mirroring the cache-eligibility host.

**Testing**: Expecto, matching the solution. Pure projection tests for `EvidenceJson` (determinism / byte
stability, `schemaVersion` present, declared-vs-effective both shown, graph-failure variants emitted by name
with **no** per-node map, freshness cause + named missing facts, empty-node document, no-hide totality);
pure `Loop` transition + emitted-effect tests and real-`Interpreter` end-to-end tests for the host over the
`tests/golden-fixture/` tree; a re-run byte-identity test (SC-002); surface-drift baseline tests for both new
modules; and an additivity guard asserting the existing `route.json` / `audit.json` / `verify.json` /
`cache-eligibility.json` goldens and `Cli` evidence output are **unchanged** (SC-005). Synthetic inputs, if any,
carry `Synthetic` in the test name with a use-site disclosure (Constitution V).

**Target Platform**: Linux/macOS/Windows CLI (`fsgg evidence`), same as the rest of the host suite.

**Project Type**: CLI command host over a pure projection core (single project family; `src/` + `tests/`).

**Performance Goals**: The projection is `O(nodes + edges)` string assembly with one deterministic sort per
collection; no new traversal beyond the single `Evidence.build`/`effective` pass the report already implies.
No perf target beyond "indistinguishable from the cache-eligibility host on the same repo."

**Constraints**:

- The document MUST be **byte-identical** for identical repository state — no wall-clock, git re-sensing,
  environment, absolute path, locale, or collection-order leakage; node, edge, and (when applicable) failure
  ordering MUST be deterministic (FR-006, SC-002). The pure `ofReport` is the only thing that produces bytes;
  it sorts every collection by a stable key and never reads a clock/env/path.
- A malformed graph MUST surface the `GraphError` **by name** and MUST NOT emit a partial/guessed per-node
  effective map (FR-004, SC-003). The host re-runs `Kernel.Evidence.build` at its edge and branches on
  `Error`, **correcting** the existing `Project.evidenceReport` swallow-to-`Map.empty` without modifying that
  function.
- Declared and effective state MUST **both** appear per node; taint never silently overwrites declared
  (FR-002). `Skipped` MUST be distinct from `Failed`/`Pending`/missing (FR-005) — guaranteed by the closed
  `Kernel.EvidenceState` DU rendered with an exhaustive, wildcard-free token match.
- Information-only: emitting `evidence.json` MUST NOT change any verdict, exit-code basis, truth table, or any
  existing artifact (FR-007). Host exit codes are operational only — `Success` 0, `UsageError` 2,
  `InputUnavailable` 3, `ToolError` 4 — never a ship/merge code (mirrors the cache-eligibility host).
- Purely additive: bumps no existing schema version, alters no existing public projection signature, leaves
  `Cli.evidenceJson` / `Project.evidenceReport` untouched (FR-009).
- All projection derivation is pure — no clock, host, env, or ordering influence (FR-006); the MVU boundary
  keeps every read/write/sense in the edge `Interpreter` (Principle IV).

**Scale/Scope**: Two new `src/` projects (`EvidenceJson` leaf + `EvidenceCommand` host), their two test
projects, two new surface-drift baselines (`surface/FS.GG.Governance.EvidenceJson.surface.txt`,
`surface/FS.GG.Governance.EvidenceCommand.surface.txt`), the `.sln` additions, and a docs flip of the Phase-6
evidence-projection row. Cores, the Cli root, and every existing projection signature are untouched.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: PASS. Spec written. The two new public surfaces —
  `EvidenceJson.fsi` (the `EvidenceDocument` model, `schemaVersion`, `ofReport`) and the host `Loop.fsi` /
  `Interpreter.fsi` — are drafted and exercised in FSI (`scripts/prelude.fsx`) before any `.fs` body exists.
  Semantic tests call the projection through its packed surface and the host through `parse`/`init`/`update`
  and the real interpreter. No existing public signature changes.
- **II. Visibility Lives in `.fsi`**: PASS. Each new module's `.fsi` is the sole declaration of its surface;
  the `.fs` carries no access modifiers. Two new surface-drift baselines are added; no existing baseline
  changes (nothing existing is re-opened).
- **III. Idiomatic Simplicity**: PASS. The projection is a pure total function over a closed model with an
  exhaustive, wildcard-free token match (the `gateIdValue`/`categoryToken`/`CacheEligibilityJson` precedent).
  The host is the established `Loop`/`Interpreter`/`Program` MVU triple copied from cache-eligibility. No new
  abstraction, operator, SRTP, reflection, or dependency.
- **IV. Elmish/MVU Is the Boundary**: PASS. The host has real I/O (git scope sensing, artifact reads, freshness
  sensing, file write) and multi-step state, so it MUST use MVU — and it does, mirroring
  `CacheEligibilityCommand.Loop`/`Interpreter` exactly: pure `update` emits `Effect` data; the edge interpreter
  executes and feeds `Msg` back. The `EvidenceJson` projection is a pure total function and correctly carries
  **no** MVU ceremony (adding it would violate Principle III).
- **V. Test Evidence Is Mandatory**: PASS. Fail-before/pass-after: a determinism + `schemaVersion` test fails
  before `ofReport` exists; a graph-failure test fails while failures are dropped and passes once named; an
  additivity guard freezes the existing goldens pre-change. Real evidence preferred — the host interpreter runs
  against the real `tests/golden-fixture/` repo tree. Synthetic inputs disclosed per V.
- **VI. Observability and Safe Failure**: PASS. The feature's reason to exist is safe-failure honesty: graph
  failures are named, not swallowed (FR-004); stale/unresolved freshness names its cause/missing facts and
  never guesses (FR-003); an operational failure (unreadable input, tool error) surfaces through the host exit
  code and diagnostics, never as a fabricated "all effective" document (Edge Cases). Diagnostics distinguish
  absent/bad input from a tool defect (Principle VI).

**Change Classification**: **Tier 1** (one new packable public projection module + one new host surface). No
truth-table or verdict change; no existing projection-signature change.

**Result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/069-evidence-json-projection/
├── plan.md              # This file
├── research.md          # Phase 0 output — the seven decisions (D1–D7)
├── data-model.md        # Phase 1 output — the EvidenceDocument model + reused cores
├── quickstart.md        # Phase 1 output — runnable SC-001…SC-006 validation
├── contracts/
│   └── evidence-json.md # Phase 1 output — the evidence.json wire contract
├── checklists/
│   └── requirements.md  # Authored by /speckit-specify (all items pass)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.EvidenceJson/                  # NEW pure, packable projection leaf
│   ├── FS.GG.Governance.EvidenceJson.fsproj        # NEW — IsPackable; refs Kernel + FreshnessResolution + EvidenceReuse
│   ├── EvidenceJson.fsi                             # NEW — EvidenceDocument model, schemaVersion, ofReport
│   └── EvidenceJson.fs                              # NEW — pure, total, exhaustive token match (no wildcard)
│
└── FS.GG.Governance.EvidenceCommand/               # NEW standalone Exe host (ToolCommandName fsgg, verb `evidence`)
    ├── FS.GG.Governance.EvidenceCommand.fsproj     # NEW — Exe; refs EvidenceJson + Cli + Host + Kernel + freshness + Snapshot + HumanText
    ├── Loop.fsi                                     # NEW — pure MVU core surface (mirrors CacheEligibilityCommand.Loop)
    ├── Loop.fs                                      # NEW — pure parse/init/update/render/exitCode
    ├── Interpreter.fsi                              # NEW — Ports + realPorts/step/run
    ├── Interpreter.fs                               # NEW — edge: sense → Host loop → Project.evidenceReport → build → project → write
    └── Program.fs                                   # NEW — thin argv → parse → run → exit edge

tests/
├── FS.GG.Governance.EvidenceJson.Tests/            # NEW — determinism, schemaVersion, declared-vs-effective,
│   │                                               #        graph-failure-by-name, freshness cause, empty-doc, totality, surface drift
│   ├── FS.GG.Governance.EvidenceJson.Tests.fsproj
│   ├── ProjectionTests.fs
│   ├── DeterminismTests.fs
│   ├── GraphFailureTests.fs
│   ├── FreshnessCauseTests.fs
│   ├── NoHideTests.fs
│   └── SurfaceDriftTests.fs
└── FS.GG.Governance.EvidenceCommand.Tests/         # NEW — parse, pure Loop transitions + emitted effects,
    │                                               #        real-Interpreter E2E over golden-fixture, byte-identity re-run, additivity guard, surface drift
    ├── FS.GG.Governance.EvidenceCommand.Tests.fsproj
    ├── ParseTests.fs
    ├── LoopTests.fs
    ├── InterpreterTests.fs
    ├── EndToEndTests.fs
    ├── AdditivityTests.fs
    └── SurfaceDriftTests.fs

surface/
├── FS.GG.Governance.EvidenceJson.surface.txt        # NEW baseline
└── FS.GG.Governance.EvidenceCommand.surface.txt     # NEW baseline

FS.GG.Governance.sln                                 # EDIT — add the two src + two test projects
docs/initial-implementation-plan.md                  # EDIT — flip the Phase-6 evidence.json row to closed (cite 069)
```

**Structure Decision**: Single-project-family layout (the established repo shape), mirroring the
`cache-eligibility` precedent exactly: a **pure packable projection leaf** (`EvidenceJson`, the analogue of
`CacheEligibilityJson`) plus a **standalone MVU host Exe** (`EvidenceCommand`, the analogue of
`CacheEligibilityCommand`). The projection owns the artifact's deterministic wire model and references only
the pure cores; the host sits on top, reusing the F12 sensing + `Project.evidenceReport` edge verbatim and
composing `Kernel.Evidence` at its boundary to surface graph failures. No existing core, root, or projection
signature changes — the dependency direction stays one-way into the pure cores.

## Phase 0 — Research

See [research.md](./research.md). It resolves: (D1) host+projection split mirroring cache-eligibility, and
**why** the feature is a new artifact rather than an edit to the inline `Cli.evidenceJson`; (D2) the output
path `readiness/evidence.json` (flat, matching every live sibling) and why the design's `readiness/<id>/`
subdir is deferred to SDD integration; (D3) the evidence-graph source — the existing F12 fact pipeline +
`Project.evidenceReport`, reused verbatim, and the host re-running `Evidence.build` to recover the swallowed
`GraphError`; (D4) per-node freshness *cause* — composing `FreshnessResolution`/`EvidenceReuse` vocabulary and
the node↔gate identity join, with the safe-failure default for nodes without a resolved freshness; (D5) the
deterministic wire model + ordering + `schemaVersion`; (D6) the no-hide totality of `ofReport` (every closed
case matched without a wildcard); (D7) the leaf dependency direction (no cycle). No `NEEDS CLARIFICATION`
markers remain.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — the new `EvidenceDocument` model (the `WellFormed`/`Malformed` content
  split, `EvidenceNode`, `NodeFreshness`, the `GraphError<string>` reuse), the per-source mapping from
  `ProjectEvidenceReport`, and the determinism/no-hide invariants.
- [contracts/evidence-json.md](./contracts/evidence-json.md) — the `evidence.json` wire contract: C1
  `schemaVersion` + field order; C2 the per-node object (`id`/`declared`/`effective`/`freshness`/`source`) and
  the declared-vs-effective rule; C3 the graph-failure variant (named, no per-node map); C4 the freshness
  cause/missing-facts grammar; C5 byte-determinism and empty-document; C6 additivity (existing artifacts
  unchanged).
- [quickstart.md](./quickstart.md) — runnable validation scenarios mapping to SC-001…SC-006.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
