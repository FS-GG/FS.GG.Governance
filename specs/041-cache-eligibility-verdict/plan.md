# Implementation Plan: Per-Gate Cache-Eligibility Verdict Core

**Branch**: `041-cache-eligibility-verdict` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/041-cache-eligibility-verdict/spec.md`

## Summary

Land the one genuinely-deferred Governance-owned line of the **route/audit emission** row (Phase 2 / Phase 11 of
`docs/initial-implementation-plan.md`): *"Emit deterministic route and audit JSON with selected gates … and **cache
eligibility** …"*. The freshness-key core (F029) and the evidence-reuse decision core (F030) landed the *evaluation*
logic for a **single** candidate; route.json (F020) / audit.json (F025) already carry each gate's freshness-key
**inputs** — but no **evaluated cache-eligibility verdict**. This row delivers the missing per-change **roll-up** as
its decision value: a pure core that, for **all** the gates a routed change selected, produces one evaluated,
attributable cache-eligibility verdict **per gate** — *reusable* (naming the reusable evidence) or *must-recompute*
(naming the cause) — in a deterministic, gate-attributable report, so a later projection row can emit that verdict
and a later host row can resolve and supply its inputs.

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F040 each landed a pure, total,
deterministic core before any host edge or projection consumed it), this row delivers a single new packable pure
core, **`FS.GG.Governance.CacheEligibility`** — the per-change roll-up that composes **F030 `EvidenceReuse.decide`
verbatim** once per selected gate (FR-004) and attributes/orders the results by `GateId`. It is the analogue of the
F029/F030 → cache-eligibility step that the JSON reports need.

The core makes **no cache lookup against a real store on disk**, performs **no persistence** (no filesystem,
database, or network read/write), computes **no freshness key or hash itself** (it consumes F029/F030 results),
**resolves none of the freshness inputs** it is given, reads **no clock / filesystem / git / environment**, runs
**no gate** and produces **no evidence**, renders **no JSON** (the projection row does that), maps **no exit code**,
and adds **no CLI**. Its sole output is the typed cache-eligibility report value.

The core provides (full vocabulary in [data-model.md](./data-model.md); the signatures + laws in
[contracts/cache-eligibility-api.md](./contracts/cache-eligibility-api.md)):

- **`CandidateGate`** = `{ Gate: GateId; Inputs: FreshnessInputs }` — one selected gate's stable identity (F018
  `GateId`, verbatim) paired with its already-resolved freshness inputs (F029 `FreshnessInputs`, verbatim). Both are
  supplied facts; the core resolves/derives neither (FR-009, research D2).
- **`CacheEligibilityVerdict`** = `Reusable of EvidenceRef | MustRecompute of RecomputeCause` — the two-outcome
  per-gate verdict, reusing F030 `EvidenceRef` / `RecomputeCause` **verbatim** as its payloads (FR-001/FR-002,
  research D4). The only new union shell.
- **`CacheEligibilityEntry`** = `{ Gate: GateId; Verdict: CacheEligibilityVerdict }` — one verdict attributed to its
  originating gate (FR-005).
- **`CacheEligibilityReport`** = `CacheEligibilityReport of CacheEligibilityEntry list` — the per-change roll-up:
  one entry per candidate, every gate preserved, ordered by `GateId` ordinal (FR-006, research D5).
- **`CacheEligibility.evaluate`** = `CandidateGate list -> ReuseStore -> CacheEligibilityReport` — the roll-up
  (`map evaluateGate` then ordinal sort).
- **`CacheEligibility.evaluateGate`** = `CandidateGate -> ReuseStore -> CacheEligibilityVerdict` — the per-gate
  composition of F030 `decide`, relabelled (FR-004).
- **`CacheEligibility.entries` / `isReusable` / `reusableEvidence` / `recomputeCause`** — the small total
  projections/unwrappers for audit and tests.

The core reuses **F018** `GateId` / `gateIdValue` (from `FS.GG.Governance.Gates.Model`) for the per-gate attribution
key and **F030** `decide` / `ReuseStore` / `ReuseDecision` / `RecomputeCause` / `EvidenceRef` (from
`FS.GG.Governance.EvidenceReuse`) for the composed decision and its payloads; **F029** `FreshnessInputs` /
`InputCategory` and **F014** `CheckId` / `DomainId` arrive transitively (research D3). It introduces only the minimal
new vocabulary the row needs (the candidate pairing, the two-outcome verdict, the per-gate entry, and the report —
exactly FR-012's list). The merged cores and their `surface/*.surface.txt` baselines are **untouched**; `dotnet
build` / `dotnet test` over existing projects stays unchanged, and the new project + its test project are purely
additive (SC-008).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true` inherited
from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test project.

**Primary Dependencies**: Two `ProjectReference`s — **`FS.GG.Governance.EvidenceReuse`** (F030, for the composed
`decide` and the reused `ReuseStore` / `ReuseDecision` / `RecomputeCause` / `EvidenceRef` vocabulary) and
**`FS.GG.Governance.Gates`** (F018, for the reused `GateId` / `gateIdValue` gate-identity vocabulary). The transitive
pure cores **`FreshnessKey`** (F029 — supplies `FreshnessInputs` / `InputCategory`, named here) and **`Config`**
(F014 — supplies `CheckId` / `DomainId`, unnamed) arrive through F030 / F018 and need no direct reference (the F030
"`Config` transitive through F029" precedent; transitive project references flow to the compiler — no
`DisableTransitiveProjectReferences`). **No new third-party `PackageReference`** (FR-013): the roll-up is `List.map`
of a verbatim F030 `decide` + an ordinal sort + `FSharp.Core`. Test frameworks already on the central feed
(`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**,
**YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the report is an in-value result of supplied data.
The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`CacheEligibility.evaluate` / `evaluateGate` /
projections and the `Model` types) over real, literally-constructible values (Principle V — every value is a genuine
typed token: real F018 `GateId`, real F029 `FreshnessInputs`, real F030 `ReuseStore` built via `EvidenceReuse.record`;
no mock, no clock read, no gate run, no file read, no bytes hashed, no JSON rendered). Concerns: (1) **recompute by
default** — empty store ⇒ `MustRecompute NoPriorEvidence`; changed inputs ⇒ `MustRecompute (InputsChanged …)` naming
exactly the changed categories; no candidate yields `Reusable` without a defensible match (SC-001/SC-003, US1); (2)
**reusable when prior evidence matches** — exact-match candidate ⇒ `Reusable` carrying F030's evidence reference, with
F030's most-recent-wins choice (SC-002, US2); (3) **one attributed verdict per gate, deterministic order** — exactly
N entries for N candidates, each carrying its `GateId`, ordered by `gateIdValue` ordinal, duplicates kept, independent
of supply order (SC-006, US3); (4) **totality** — a report/verdict always returned and never throws across the
cross-product of candidate counts (zero, one, many, duplicate `GateId`s) and store states (empty, matching,
non-matching) (SC-004, US3); (5) **determinism / purity** — equal candidates + store ⇒ byte-identical report under
changed cwd / time / filesystem, no I/O, no key computed (SC-005, US3); (6) **necessary-not-sufficient** — a
`Reusable` value carries no skip action, severity, ship verdict, or exit-code basis (SC-007, US3); (7) **surface drift
+ scope hygiene** — the assembly references only `EvidenceReuse` / `Gates` (+ allowed transitive cores) (Principle II,
SC-008). Recompute-by-default, one-per-gate, order-independence, totality, and determinism are FsCheck properties; the
worked examples are pinned to [contracts/cache-eligibility-api.md](./contracts/cache-eligibility-api.md), plus the FSI
proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **recompute-by-default safety, totality, determinism, and
gate-attributable no-hide attribution**, not latency; the roll-up is a small per-gate computation over a handful of
supplied facts (Spec Assumptions: *"Determinism is the contract, not performance"*).

**Constraints**: Pure / total / deterministic (FR-007/FR-008): reads no clock, filesystem, git, environment, or
network; runs no gate; produces no evidence; makes no cache lookup against a real store; computes no hash or freshness
key; resolves none of the supplied freshness inputs; renders no JSON; maps no exit code; persists nothing; adds no
CLI. Composes the existing F030 reuse decision verbatim and introduces no new or divergent reuse policy (FR-004).
Treats the supplied freshness inputs, evidence references, and gate identities as opaque facts produced elsewhere
(FR-009). A *reusable* verdict is necessary-not-sufficient: it carries no skip action and no enforcement meaning
(FR-010). Identical supplied inputs always yield an identical report. The merged cores and baselines are not modified
(FR-014 / SC-008).

**Scale/Scope**: One new `src/` library (`CacheEligibility` — `Model.fsi/fs` + `CacheEligibility.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.CacheEligibility.surface.txt`; two solution entries; a
short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan pointer. Zero changes
to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `CacheEligibility.fsi` and exercised in `scripts/prelude.fsx` (a new F041 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers, and the internal sort-comparator helper stays unexposed by its absence from the `.fsi`. A new `surface/FS.GG.Governance.CacheEligibility.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F040 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | Plain records + two small unions; `evaluateGate` is a two-arm `match` relabel of F030 `decide`, and `evaluate` is `List.map` + a `List.sortWith` on an ordinal `gateIdValue` comparison with a structural tiebreak (no SRTP, reflection outside the surface test, custom operators, type providers, or non-trivial CEs). The reused tokens (`GateId`, `FreshnessInputs`, `RecomputeCause`, `EvidenceRef`) are opened, not re-modeled (research D3). |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — a pure total roll-up over supplied values. Like F023/F030/F040, this is a pure decision core needing no MVU ceremony. The *actual* cache lookup, the *resolving* of freshness inputs, running gates, and rendering JSON are later projection / host edges (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (real F018 `GateId`, real F029 `FreshnessInputs`, real F030 `ReuseStore` via `EvidenceReuse.record`); no clock read, no gate run, no file read, no bytes hashed, no JSON rendered, no mock used. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **N/A (totality stands in)** | No operationally-significant event exists to observe — no startup, store load/save, rule-evaluation divergence, freshness-expiry, or scan to log (a pure in-value roll-up, like Principle IV). The safe-failure spirit is met by **totality**: the functions never throw, swallow a failure, or silently drop. Every combination — no candidates, one candidate, duplicate `GateId`s, empty store, non-matching store, exact match — is an ordinary named verdict/report (Edge Cases); the *must-recompute* outcome always names its cause and the report preserves every gate (the no-hide rule). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F018 `GateId` + F030 vocabulary consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-013); references only the sibling pure cores `EvidenceReuse` (F030) + `Gates` (F018) — and their transitive pure cores `FreshnessKey` / `Config` — no git / filesystem scanning / host / CLI / projection. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principles IV and VI are N/A (no
stateful/I/O workflow, and no operationally-significant event to observe — totality stands in for safe failure); I,
II, III, V all have concrete targets and pass. The two sibling references (research
D3) reuse the F030 reuse decision FR-004 mandates composing and the F018 `GateId` FR-012 names, pull in nothing
impure, and are the only cross-core coupling.

## Project Structure

### Documentation (this feature)

```text
specs/041-cache-eligibility-verdict/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D8 (one-new-core, candidate pairing, references, verdict relabel,
│                        #            report ordering + duplicates, operations surface, necessary-not-sufficient, totality)
├── data-model.md        # Phase 1 — CandidateGate, CacheEligibilityVerdict, CacheEligibilityEntry, CacheEligibilityReport
│                        #            (reuses F018 GateId + F029 FreshnessInputs/InputCategory + F030 ReuseStore/RecomputeCause/EvidenceRef)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   └── cache-eligibility-api.md   # the public signatures + their laws (compose-no-new-policy, recompute-by-default, one-per-gate, ordinal order) + the scope guard
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.CacheEligibility/                  # NEW — the pure per-gate cache-eligibility roll-up core
├── Model.fsi                                           # NEW — CandidateGate, CacheEligibilityVerdict, CacheEligibilityEntry,
│                                                       #       CacheEligibilityReport (sole public surface; reuses F018 + F029 + F030 verbatim)
├── Model.fs                                            # NEW — the matching type defns (no access modifiers)
├── CacheEligibility.fsi                                # NEW — evaluate / evaluateGate / entries / isReusable /
│                                                       #       reusableEvidence / recomputeCause
├── CacheEligibility.fs                                 # NEW — the pure, total roll-up body + the ordinal sort comparator (private by omission)
└── FS.GG.Governance.CacheEligibility.fsproj            # NEW — packable; references EvidenceReuse + Gates; BCL + FSharp.Core

tests/FS.GG.Governance.CacheEligibility.Tests/          # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                           # NEW — real literal builders (FreshnessInputs, ReuseStore via record, GateId) + FsCheck generators (no mocks)
├── RecomputeByDefaultTests.fs                           # NEW — US1: empty store ⇒ NoPriorEvidence; changed inputs ⇒ InputsChanged naming exactly the categories; never Reusable without a match (SC-001/SC-003)
├── ReusableTests.fs                                     # NEW — US2: exact-match ⇒ Reusable carrying F030's reference, most-recent-wins (SC-002)
├── AttributionAndOrderTests.fs                          # NEW — US3: exactly one entry per candidate, attributed, ordered by GateId ordinal, duplicates kept, supply-order independent (SC-006)
├── TotalityTests.fs                                     # NEW — US3: a report/verdict always returned, never throws, across the cross-product (SC-004)
├── DeterminismTests.fs                                  # NEW — US3: equal candidates+store ⇒ equal report under changed cwd/time/fs; no I/O, no key computed (SC-005)
├── NecessaryNotSufficientTests.fs                       # NEW — US3: Reusable carries no skip action / severity / ship verdict / exit-code basis (SC-007)
├── SurfaceDriftTests.fs                                 # NEW — Principle II surface baseline + EvidenceReuse/Gates-only scope guard
├── Main.fs                                              # NEW — Expecto entry point
└── FS.GG.Governance.CacheEligibility.Tests.fsproj       # NEW — references CacheEligibility (+ EvidenceReuse/Gates for the tokens); test packages

surface/FS.GG.Governance.CacheEligibility.surface.txt    # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                      # EDIT — append a short F041 FSI section (design-first proof)
FS.GG.Governance.sln                                     # EDIT — add the two new projects
CLAUDE.md                                                # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.CacheEligibility` (the established
one-new-minimal-core-per-row rhythm, research D1), compiled `Model → CacheEligibility`, referencing the sibling pure
cores `EvidenceReuse` (F030) and `Gates` (F018) only — to compose the reuse decision verbatim and reuse the `GateId`
gate-identity vocabulary (research D3). A sibling test project exercises the public surface with real literal values.
The library is additive: no existing `src/`, `surface/`, or merged test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
