# Implementation Plan: Deterministic audit.json Projection

**Branch**: `025-audit-json-projection` (active spec; git branch currently `main`) | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/025-audit-json-projection/spec.md`

## Implementation Progress

**Status: 🟢 SHIPPED — implementation complete; 21 tests green over the real `Ship.rollup` chain.** The
new packable library `FS.GG.Governance.AuditJson` (`AuditJson.ofShipDecision : ShipDecision -> string` +
`schemaVersion = "fsgg.audit/v1"`) renders the F024 `ShipDecision` to the deterministic, versioned
`audit.json` document via a hand-driven `Utf8JsonWriter` walk — emit-only, no new dependency. The
solution now lists the src + test projects; the public surface baseline is committed and drift-checked.

| Phase | Tasks | Status | Evidence |
|---|---|---|---|
| Phase 0 — research (D1–D8, resolved context) | — | 🟢 done | [research.md](./research.md) |
| Phase 1 — data model + contracts + quickstart | — | 🟢 done | [data-model.md](./data-model.md), [contracts/AuditJson.fsi](./contracts/AuditJson.fsi), [contracts/audit-json-document.md](./contracts/audit-json-document.md), [quickstart.md](./quickstart.md) |
| Phase 2 — `tasks.md` | T001–T029 | 🟢 done | [tasks.md](./tasks.md) (all 29 `[X]`) |
| Phase 3 — implementation (`.fsi`→`.fs`, tests, surface baseline, sln) | T001–T029 | 🟢 done | `src/FS.GG.Governance.AuditJson/` (`AuditJson.fsi`/`.fs`), `tests/FS.GG.Governance.AuditJson.Tests/` (21 green: Projection/Determinism/Carry/Totality/SurfaceDrift), `surface/FS.GG.Governance.AuditJson.surface.txt`, prelude F025 block, [readiness/README.md](./readiness/README.md) + [readiness/transcript.md](./readiness/transcript.md) |

**Test evidence (Principle V).** All inputs are real `ShipDecision`s built through the genuine F024
`Ship.rollup` over real `RouteResult` × `RunMode` × `Profile`; outputs inspected with a read-only
`System.Text.Json.JsonDocument`. One disclosed synthetic case (a `ShipDecision` built directly with a
JSON-special `reason`, un-derivable from `rollup`) carries `Synthetic` in its test name and a use-site
`// SYNTHETIC:` comment — see [readiness/README.md](./readiness/README.md). FsCheck determinism/totality
properties drive the real `rollup`, not hand-built values.

## Summary

F025 is the **audit.json projection**: a single pure, total function
`AuditJson.ofShipDecision : ShipDecision -> string` (plus a `schemaVersion` constant) that renders the
F024 `ShipDecision` into a deterministic, versioned `audit.json` document — the stable, machine-readable
whole-change verdict contract the later `fsgg ship` command, CI gates, branch-protection checks, agents,
and generated readiness views read instead of an in-memory value. It is the design's
`readiness/<id>/audit.json` artifact (`docs/initial-design.md`, `docs/initial-implementation-plan.md:197`),
restricted to the fields the upstream `ShipDecision` already types.

The document carries the decision's `verdict` (`pass`/`fail`) and `exitCodeBasis` (`clean`/`blocked`)
**verbatim**, and the three-way `blockers`/`warnings`/`passing` partition — each an always-present array
in F024's already-fixed composite order — with every item carrying its identity (a gate by its declared
`GateId`; a finding by its `FindingId` token plus governed `path`) and a nested `enforcement` object
carrying all six F023 fields (base severity, maturity, mode, profile, effective severity, reason)
**verbatim**. Carrying both base and effective severity on every item makes the design's no-hide rule
observable: a relaxed base-`Blocking` warning is always self-explaining and a profile can never hide the
underlying verdict (`docs/initial-design.md:575`, `:806`).

This continues the **pure-core-first** slice of F020 (`route.json`) and F021 (`gates.json`): a new
packable sibling library `FS.GG.Governance.AuditJson` that layers `System.Text.Json` serialization on
top of the pure `Ship` core, in a separate project, adding **no new dependency**. It is emit-only — it
recomputes no verdict (FR-002), derives no numeric process exit code (FR-003 — the later `fsgg ship`
host edge), invents no provenance reference (deferred to the Release phase; the `ShipDecision` carries
none), and evaluates no cache/freshness verdict (Phase 11).

**Decisions confirmed at planning** (full rationale in [research.md](./research.md)):
- **D1** New sibling library `FS.GG.Governance.AuditJson`, one `ProjectReference` to `Ship`.
- **D2** Hand-driven `Utf8JsonWriter`, compact output, **no new package** (FR-014).
- **D3** Six **hidden, exhaustive, wildcard-free** token helpers owned by AuditJson (verdict, basis,
  severity, maturity, mode, profile); identity tokens **reused** from public upstream
  (`gateIdValue`, `findingIdToken`).
- **D5** Tagged item entries (`kind:"gate"`/`"finding"`) with a nested `enforcement` object; three
  always-present section arrays.
- **D6** Section order inherited from the `ShipDecision` verbatim; re-sort nothing.
- **D7** Tests drive the **real** `Ship.rollup` chain; inspect emitted bytes via `JsonDocument`.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (the constitution's exclusive stack; matches all sibling projects).

**Primary Dependencies**: `FS.GG.Governance.Ship` (F024) as the sole `ProjectReference` — it brings
`Enforcement`, `Route`, `Config`, `Gates`, `Findings`, `Kernel` transitively. Serialization is the
`net10.0` shared-framework `System.Text.Json` (`Utf8JsonWriter`); **no new third-party package** (FR-014).

**Storage**: N/A — the projection is pure; it reads no file and writes no file (it returns a `string`).
Persisting `audit.json` to disk is the later `fsgg ship` host row.

**Testing**: Expecto + FsCheck (already centrally pinned), driven by `dotnet test` via the YoloDev /
VSTest adapters. Inputs are real `ShipDecision`s built through `Ship.rollup`; outputs inspected with a
read-only `System.Text.Json.JsonDocument`. No mocks (Principle V).

**Target Platform**: Cross-platform .NET library (Linux/macOS/Windows); no platform-specific surface.

**Project Type**: Single packable F# library + its test project — the F020/F021 shape.

**Performance Goals**: None beyond linear, single-pass emission over the decision's items; the document
is small and bounded by the change's gate/finding count.

**Constraints**: PURE and TOTAL (no file/process/clock/network/git; never throws for a well-typed
`ShipDecision`). DETERMINISTIC (byte-for-byte identical for identical input; order inherited from F024).
Library stays `System.*`/`FSharp.Core`-only. Visibility lives in `AuditJson.fsi`; a surface baseline is
committed and drift-checked.

**Scale/Scope**: One module, two public members, ~one source file pair (`.fsi`/`.fs`); five test files +
entry point; one surface baseline. No public API change to any existing project (additive new library).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | `AuditJson.fsi` drafted as a contract ([contracts/AuditJson.fsi](./contracts/AuditJson.fsi)) and exercised via `scripts/prelude.fsx` ([quickstart.md](./quickstart.md)) before any `.fs` body; semantic tests call the packed public surface, not private helpers. |
| II. Visibility lives in `.fsi` (no access modifiers in `.fs`); surface baseline | **PASS** | Sole public surface is `AuditJson.fsi` (two members); every writer/token helper is hidden by absence. A committed `surface/FS.GG.Governance.AuditJson.surface.txt` baseline + reflective drift test (blessable via `BLESS_SURFACE=1`) mirror the GatesJson precedent. |
| III. Idiomatic Simplicity | **PASS** | Plain functions + a linear `Utf8JsonWriter` walk; exhaustive `match` over closed DUs; no custom operators, SRTP, reflection (outside the surface test), type providers, or non-trivial CEs. No justification debt. |
| IV. Elmish/MVU boundary for stateful/I/O work | **PASS (N/A)** | The projection is a pure, total function with no state, I/O, retries, or workflow — the constitution's explicit "simple pure function" exemption. No MVU ceremony. |
| V. Test Evidence Is Mandatory (real evidence; synthetic disclosed) | **PASS** | Tests fail before / pass after; inputs are real `ShipDecision`s via `Ship.rollup` over a real route chain ([research D7](./research.md)). No mocks; any hand-built edge literal is an ordinary value, not synthetic evidence. |
| VI. Observability and Safe Failure | **PASS (N/A)** | No critical path: the function is total and cannot fail for a well-typed input, so there is no failure to log or degrade. It emits no diagnostics by design (it returns a value). |
| Change Classification | **Tier 1** | Adds new public API surface (a new packable library + module). Requires the full chain: spec, plan, `.fsi`, surface baseline, test evidence, docs — all planned here. |
| Engineering: target framework | **PASS** | `net10.0`, no narrower target. |
| Engineering: dependency minimalism | **PASS** | **No new `PackageReference`** (FR-014); serialization is shared-framework `System.Text.Json`. One `ProjectReference` (`Ship`). |
| Engineering: genericity / operating rule | **PASS** | No rendering package id, template, target, or path assumed; renders only the typed `ShipDecision`. Governance inspects, never requires rendering. |
| Engineering: `FS.GG.Governance.*` identity; pack to `~/.local/share/nuget-local/` | **PASS** | New library is `FS.GG.Governance.AuditJson`, `IsPackable=true`, same pack output as siblings. |

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/025-audit-json-projection/
├── plan.md                       # This file (/speckit-plan output)
├── research.md                   # Phase 0 — D1–D8 + resolved context
├── data-model.md                 # Phase 1 — consumed value, produced value, rules, determinism, totality, exclusions
├── quickstart.md                 # Phase 1 — build/test, FSI smoke, acceptance→evidence map
├── contracts/
│   ├── AuditJson.fsi             # Phase 1 — curated public surface (schemaVersion + ofShipDecision)
│   └── audit-json-document.md    # Phase 1 — wire contract: field order, tokens, sample, exclusions
├── readiness/
│   └── README.md                 # Phase 2/3 — required FSI transcripts + SC-001…SC-007 traceability (tasks T009/T028)
├── spec.md                       # Feature spec (input)
└── tasks.md                      # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.AuditJson/
├── FS.GG.Governance.AuditJson.fsproj   # IsPackable=true; ProjectReference → Ship; no new PackageReference
├── AuditJson.fsi                        # compiled first — the sole public surface
└── AuditJson.fs                         # the pure projection + hidden writer/token plumbing

tests/FS.GG.Governance.AuditJson.Tests/
├── FS.GG.Governance.AuditJson.Tests.fsproj  # refs AuditJson + Ship/Route/Enforcement/Config/Gates/Findings; Expecto/FsCheck
├── Support.fs                # real-chain fixture builders (RouteResult → Ship.rollup) + JsonDocument helpers
├── ProjectionTests.fs        # US1 — verdict/basis + sections + identity + detail
├── DeterminismTests.fs       # US2 — byte-identical, permutation-invariant, schemaVersion, excluded-token sweep
├── CarryTests.fs             # US3 — six enforcement fields verbatim; no-hide base+effective severity; identity
├── TotalityTests.fs          # US4 — empty/clean valid; single-section; property: never throws
├── SurfaceDriftTests.fs      # Principle II — surface baseline + dependency-scope guard
└── Main.fs                   # Expecto entry point

surface/
└── FS.GG.Governance.AuditJson.surface.txt   # committed public-surface baseline (blessable)

FS.GG.Governance.sln          # add both new projects
```

**Structure Decision**: A new packable sibling library `FS.GG.Governance.AuditJson` with a paired test
project — the exact shape F020 (`RouteJson`) and F021 (`GatesJson`) established for a pure JSON
projection ([research D1](./research.md)). Source compiles `.fsi` before `.fs` so the curated surface
precedes the implementation (Principle II). The library references **only** `FS.GG.Governance.Ship`;
every rendered type arrives transitively, keeping the dependency direction one-way and the library
`System.*`/`FSharp.Core`-only (no new package — [research D2](./research.md)). The test project adds the
upstream references needed to assemble the real `ShipDecision` chain ([research D7](./research.md)). The
committed `surface/` baseline + reflective drift test enforce the visibility contract.

## Complexity Tracking

> No Constitution Check violations. Complexity Tracking is empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
</content>
