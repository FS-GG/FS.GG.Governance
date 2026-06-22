# Implementation Plan: Per-Gate Freshness-Inputs Resolution Core

**Branch**: `043-freshness-inputs-resolution` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/043-freshness-inputs-resolution/spec.md`

## Summary

Land the missing **pure join** that unblocks the route/audit cache-eligibility *host wiring* row of
`docs/initial-implementation-plan.md`. F029 fixed the ten-field `FreshnessInputs` value, F030 decided reuse
from it, and F041 rolled that decision up per selected gate — but none can run against a real routed change,
because a routed change's selected gate (F018/F019) carries only its **five-field freshness-key identity**
(check, domain, cost, environment, command), **not** the full `FreshnessInputs`. Four of those fields are what
F029 needs; cost is deliberately not a freshness input; and the remaining six (rule hash, covered-artifact
hashes, command version, generator version, base/head revisions) are repository facts nothing has supplied to
the gate.

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (every prior row landed a pure, total,
deterministic core before any host edge or projection consumed it), this row delivers a single new packable
core, **`FS.GG.Governance.FreshnessResolution`**, whose `resolve` joins each selected gate's carried
freshness-key identity (dropping cost) with a supplied bundle of **already-sensed** repository facts,
**fabricating nothing**: a gate with every required fact resolves to a complete `FreshnessInputs` shaped to feed
F041 verbatim; a gate missing any required fact yields a **no-hide `Unresolved`** outcome naming exactly the
missing facts, recompute-safe by construction. The result is a deterministic, gate-attributable report — one
outcome per selected gate, ordered by `GateId`, every gate preserved.

The core **senses nothing** (no git, filesystem, clock, environment, network, command), **computes no hash,
freshness key, or digest**, **evaluates no cache eligibility** (F030/F041), **renders no JSON** (F042/the
projection rows), **persists nothing**, **maps no exit code**, and **adds no CLI**. Its sole output is the typed
`FreshnessResolutionReport`.

The capability provides (full vocabulary in [data-model.md](./data-model.md); the value contract in
[contracts/freshness-resolution-outcome.md](./contracts/freshness-resolution-outcome.md); the signatures + laws
in [contracts/freshness-resolution-api.md](./contracts/freshness-resolution-api.md)):

- **`FreshnessResolution.resolve`** = `Gate list -> SensedFacts -> FreshnessResolutionReport` — the pure, total
  join: one attributed outcome per gate, ordered by `GateId` ordinal with a structural tiebreak (duplicates
  preserved); each gate `Resolved` with its complete `FreshnessInputs` or `Unresolved` naming every missing
  sensed fact.
- **`SensedFacts`** — the new supplied bundle (research D4): repo-wide facts as `option` (rule hash, generator
  version, base/head), per-gate covered artifacts and per-command command versions as `Map`s where key-present
  means *sensed* (even when empty) and key-absent means *not sensed*.
- **Accessors** — `entries`, `candidate` (the recompute-safe F041 bridge: `Some CandidateGate` for resolved,
  `None` for unresolved), `isResolved`, `missingFacts`, and `missingFactToken` (the stable no-hide token
  vocabulary, mirroring F029 `categoryToken` and the F042 reuse-tokens-verbatim precedent).

The library reuses the F018 `Gate`/`GateId`/`FreshnessKey` + `gateIdValue`, the F029 `FreshnessInputs` + its
newtypes, the F041 `CandidateGate`, and the F014 `Config` newtypes **verbatim** — all arriving through a single
`ProjectReference` to `FS.GG.Governance.CacheEligibility` (F041), exactly as F042 referenced only F041 (research
D2). Every match over the closed `MissingFact`/`ResolutionOutcome` unions is wildcard-free. The merged F018/
F029/F030/F041 cores and their `surface/*.surface.txt` baselines are **untouched**; the new project + its test
project are purely additive (FR-014, SC-008).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi`/`.fs` pairs (`Model`,
then `FreshnessResolution`), plus one new test project.

**Primary Dependencies**: One `ProjectReference` — **`FS.GG.Governance.CacheEligibility`** (F041, supplying
`CandidateGate` and, transitively, F029 `FreshnessInputs` + newtypes, F018 `Gate`/`GateId`/`FreshnessKey` +
`gateIdValue`, F030 `EvidenceReuse`, F014 `Config` newtypes). Transitive project references flow to the
compiler — no `DisableTransitiveProjectReferences`, the F042 precedent. **No new third-party
`PackageReference`** (FR-013): the join is pure F# over typed values — no serialization, so the library stays
`System.*`/`FSharp.Core`-only. Test frameworks already on the central feed (`Directory.Packages.props`):
**Expecto**, **Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the report is an in-value typed result of the
supplied gates and sensed facts. The only test-side I/O is the surface-drift baseline read (and its
`BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`FreshnessResolution.resolve` + accessors)
over **real** upstream values — real F018 `Gate`s, real F029 newtypes, and real F041 `CacheEligibility.evaluate`
to prove a resolved candidate is accepted without adaptation (Principle V — no mock, no clock read, no
hand-built oracle, no git). Concerns: (1) **carry** — resolved fields equal carried identity (cost dropped) +
sensed facts verbatim (US1, SC-001); (2) **no fabrication / no-hide** — a gate missing facts is `Unresolved`
naming exactly and every gap, no placeholder produced (US2, SC-002); (3) **consistent command absence** —
`Command = None` resolves with absent command + version, never unresolved on that basis (US1 edge, SC-003); (4)
**determinism + completeness** — one attributed entry per gate, ordered by `GateId`, duplicates preserved,
byte-identical for value-equal inputs regardless of order/cwd/clock/filesystem (US3, SC-005/SC-006); (5)
**totality** — a well-formed report across {0,1,many} gates × {all,partial,none} sensed facts, never throws
(US3, SC-004); (6) **F041 bridge** — `candidate` of resolved accepted by F041, `candidate` of unresolved =
`None` (recompute-safe, SC-007, FR-004); (7) **sensed-empty vs unsensed** — explicit empty covered set resolves,
unsensed is unresolved, never conflated (Edge, FR-003); (8) **surface drift + scope hygiene** — the assembly
references only `CacheEligibility` (+ transitive cores) and renders the committed surface (Principle II,
additive-only). Carry, no-hide, determinism, order-independence, and totality are FsCheck properties; the worked
examples are pinned to [contracts/freshness-resolution-outcome.md](./contracts/freshness-resolution-outcome.md),
plus the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **honest resolution, totality, determinism, and gate-attributable
no-hide attribution**, not latency; the join is a small per-gate map over a handful of supplied facts (Spec:
*"Determinism is the contract, not performance"*).

**Constraints**: Pure / total / deterministic (FR-008/FR-009): no file, process, clock, network, or git access;
no hash / freshness key / digest computed; no cache eligibility evaluated; the supplied newtypes consumed
opaquely (never parsed, re-hashed, or fabricated); never throws for any well-typed input; an empty gate list is
a valid success. Honest resolution (FR-003): nothing fabricated, defaulted, or zero-filled — a missing fact
yields a no-hide `Unresolved`. Recompute-safe by construction (FR-004): `Unresolved` carries no `FreshnessInputs`
and `candidate` returns `None` for it. The merged F018/F029/F030/F041 cores and their baselines are not modified
(FR-014).

**Scale/Scope**: One new `src/` library (`FreshnessResolution` — `Model.fsi/fs` + `FreshnessResolution.fsi/fs`);
one new test project; one new surface baseline `surface/FS.GG.Governance.FreshnessResolution.surface.txt`; two
solution entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md`
plan pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `FreshnessResolution.fsi` and exercised in `scripts/prelude.fsx` (a new F043 section) before any `.fs` body exists; semantic tests call the public `resolve` / accessors, never private join helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers, and every join / token helper stays unexposed by its absence from the `.fsi` (the F029/F041 precedent). A new `surface/FS.GG.Governance.FreshnessResolution.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F018–F042 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | A per-gate `List.map` join + a `List.sortWith` on `gateIdValue` (ordinal) with a structural tiebreak + a handful of exhaustive closed-DU `match`es (the `MissingFact`/`ResolutionOutcome` tokens). No SRTP, reflection outside the surface test, custom operators, type providers, or non-trivial CEs. Records over hierarchies; `option`/`Map` over sentinels. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — a pure total join from typed values to a typed report. Like F029/F030/F041, this is a pure core needing no MVU ceremony. The host edge that actually senses git/filesystem and supplies `SensedFacts`, and any real cache store, are later rows (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real F018 `Gate` + real F029 newtypes + a real `SensedFacts` bundle; the F041 bridge is proven by feeding `candidate` results into real `CacheEligibility.evaluate` (Principle V — no mock, no clock read, no hand-built oracle, no git). Tests fail before `resolve` matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **N/A (totality + no-hide stand in)** | No operationally-significant event exists to observe — no startup, store load/save, divergence, freshness-expiry, or scan to log (a pure in-value join, like Principle IV). The safe-failure spirit is met by **totality** (`resolve` never throws, swallows, or drops a gate) and by the **no-hide** rule (a missing fact is never silently defaulted — it is named in an `Unresolved` outcome). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly (`FS.GG.Governance.FreshnessResolution`) and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests, docs. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F018/F029/F030/F041 consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-013) — the join needs no serialization; references only the sibling pure core `CacheEligibility` (F041) — and its transitive pure cores `EvidenceReuse` / `Gates` / `FreshnessKey` / `Config` — no git / filesystem / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principles IV and VI are N/A
(no stateful/I/O workflow, and no operationally-significant event to observe — totality + no-hide stand in for
safe failure); I, II, III, V all have concrete targets and pass. The single sibling reference (research D2) is
the F041 core whose `CandidateGate` this row produces; it pulls in nothing impure and is the only cross-core
coupling.

## Project Structure

### Documentation (this feature)

```text
specs/043-freshness-inputs-resolution/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D8 (one-new-core lib, single F041 reference, Gate-list input,
│                        #            SensedFacts option/Map bundle, two-outcome closed union + recompute-safe bridge,
│                        #            no-hide collect-all, GateId-ordinal determinism, accessor set + missingFactToken)
├── data-model.md        # Phase 1 — the new vocabulary + the field-by-field join + the laws (reuses F018/F029/F041/F014)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── freshness-resolution-api.md       # the public signatures + their laws + the scope guard
│   └── freshness-resolution-outcome.md   # the observable report value: shape, provenance, tokens, worked examples
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.FreshnessResolution/                   # NEW — the pure freshness-inputs resolution (join) core
├── Model.fsi                                               # NEW — SensedFacts, MissingFact, ResolutionOutcome, entry, report
├── Model.fs                                                # NEW — the new vocabulary (no access modifiers)
├── FreshnessResolution.fsi                                 # NEW — resolve + accessors (sole operations surface)
├── FreshnessResolution.fs                                  # NEW — the pure, total join + token helpers (private by omission)
└── FS.GG.Governance.FreshnessResolution.fsproj            # NEW — packable; references CacheEligibility; System.* + FSharp.Core

tests/FS.GG.Governance.FreshnessResolution.Tests/          # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                              # NEW — real Gate + SensedFacts builders + FsCheck generators + the F041 evaluate helper (no mocks)
├── ResolveTests.fs                                         # NEW — US1: carry — identity verbatim, sensed verbatim, cost dropped (SC-001)
├── UnresolvedTests.fs                                      # NEW — US2: no fabrication; names exactly + every missing fact (no-hide) (SC-002)
├── CommandAbsenceTests.fs                                  # NEW — US1 edge: Command=None ⇒ absent command+version, never unresolved (SC-003)
├── DeterminismTests.fs                                     # NEW — US3: order-independent, byte-identical reports; no I/O (SC-005)
├── CompletenessTests.fs                                    # NEW — US3: one attributed entry per gate, GateId order, duplicates preserved (SC-006)
├── TotalityTests.fs                                        # NEW — US3: well-formed report across the full cross-product, never throws (SC-004)
├── CandidateBridgeTests.fs                                 # NEW — candidate of resolved accepted by F041; of unresolved = None (SC-007, FR-004)
├── SensedEmptyTests.fs                                     # NEW — Edge: sensed-empty covered set resolves; unsensed is unresolved (FR-003)
├── SurfaceDriftTests.fs                                    # NEW — Principle II surface baseline + CacheEligibility(+transitive)-only scope guard
├── Main.fs                                                 # NEW — Expecto entry point
└── FS.GG.Governance.FreshnessResolution.Tests.fsproj      # NEW — references FreshnessResolution (+ CacheEligibility/Gates/FreshnessKey/Config); test packages

surface/FS.GG.Governance.FreshnessResolution.surface.txt   # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                         # EDIT — append a short F043 FSI section (design-first proof)
FS.GG.Governance.sln                                        # EDIT — add the two new projects
CLAUDE.md                                                   # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.FreshnessResolution` (the
established one-new-sibling-core-per-row rhythm — F029 `FreshnessKey`, F030 `EvidenceReuse`, F041
`CacheEligibility` — research D1), compiling `Model.fsi/fs` then `FreshnessResolution.fsi/fs`, referencing only
the sibling pure core `CacheEligibility` (F041) — to produce the `CandidateGate` it consumes and reuse the
`gateIdValue` / `FreshnessInputs` / `Gate` vocabulary that arrives transitively (research D2/D3). A sibling test
project exercises the public surface with real upstream values and proves the F041 bridge by feeding `candidate`
results into `CacheEligibility.evaluate`. The library is additive: no existing `src/`, `surface/`, or merged
test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
