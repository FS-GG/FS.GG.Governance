# Implementation Plan: Ship Verdict Rollup (Pure Core)

**Branch**: `024-ship-verdict-rollup` (active spec; git branch currently `main`) | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/024-ship-verdict-rollup/spec.md`

## Implementation Progress

**Status: ✅ COMPLETE** (2026-06-21) — all 26 tasks `[X]`; the full solution and the F024 suite are green.

| Phase | Tasks | Status | Evidence |
|---|---|---|---|
| 1. Setup | T001–T011 | ✅ | New `FS.GG.Governance.Ship` leaf + test project build; `.fsi` copied verbatim; real fixture builders + FsCheck generators in `Support.fs`; prelude F024 sketch; readiness README. |
| 2. Foundation | T012–T014 | ✅ | Hidden `gateToInput`/`findingToInput` (D3/D4), item-identity builder, and `itemSortKey` in `Ship.fs` — exhaustive closed-DU matches, no wildcard. |
| 3. US1 (rollup MVP) | T015–T016 | ✅ | `Ship.rollup` maps → partition → verdict/basis; `RollupTests` (6 cases incl. protected-boundary-blocks-with-no-gate, SC-001 property) green. |
| 4. US2 (worked example) | T017–T018 | ✅ | `WorkedExampleTests` (SC-002 warning, same-rollup blocker+warning, lever-only flip, SC-003 carry) green; behavior falls out of reused F023 derivation — no special-casing. |
| 5. US3 (determinism/totality) | T019–T022 | ✅ | `DeterminismTests` (SC-004 + shuffle-invariance), `TotalityTests` (empty route, never-throws, partition law SC-006, ordering), `CarryTests` (SC-003, no-hide, shared-gate single-count) green. |
| 6. Polish | T023–T026 | ✅ | Surface baseline `surface/FS.GG.Governance.Ship.surface.txt` blessed + `SurfaceDriftTests` (FR-012/SC-007 exclusions + `Ship → {Enforcement, Route}` dependency); quickstart re-run; readiness filled; this header. |

**Test evidence**: `dotnet test tests/FS.GG.Governance.Ship.Tests` ⇒ **24 passed, 0 failed**. Full
solution ⇒ no regressions. FSI smoke (`scripts/prelude.fsx`, F024 block) against the real body
reproduces the empty-route clean pass, the inner/light warning, and the gate/light blocker+warning
fail. **No synthetic evidence** — every input is a real, literally-constructible typed value
(Principle V). The two `.fsi` are the sole public surface; `gateToInput`/`findingToInput`/`itemSortKey`
are hidden by absence (Principle II); the rollup is a pure total leaf (Principle IV N/A).

## Summary

Land the **second Phase-5 pure core**: the single total, deterministic function that rolls an
already-routed change (the F019 `RouteResult`) plus a chosen run **mode** and **profile** up into one
whole-change **ship decision** — a closed `pass`/`fail` verdict, the deterministic **blockers** and
**warnings** lists, every item's full enforcement detail, and a typed **exit-code basis** (clean vs
blocked). It is the pure decision the later `audit.json` projection and `fsgg ship` host command will
consume unchanged — exactly as F019's `Route.select` was the pure value F020's `route.json` and F022's
`fsgg route` consumed.

The core **reuses** F023 `deriveEffectiveSeverity` for every per-item decision (FR-003) and the F019
`RouteResult`/F018 `Gate`/F017 finding values verbatim (the spec's "reuse, don't re-derive" rule). It
adds only the whole-change rollup: a deterministic gate/finding → F023-enforcement-input mapping
(FR-013, the plan-time reconciliation), a three-way `Blockers`/`Warnings`/`Passing` partition of every
enforced item, and the verdict + exit-code-basis derived from that partition. It honours the design's
hard rule that a profile **must never hide the underlying verdict** (`docs/initial-design.md:575`,
`:806`): every item carries its base severity, effective severity, mode, profile, maturity, and reason,
so a relaxed blocker is always visible as a self-explaining warning.

Because the feature is a **pure, total, side-effect-free value-to-value computation** — no multi-step
state, no I/O, no clock, no JSON, no `policy.yml` — it is a **pure leaf** like F015/F017/F018/F019/F021/
F023, **not** an Elmish/MVU edge (Constitution Principle IV triggers only once behavior includes
stateful workflow or I/O, which this row deliberately excludes — spec "Boundary discipline"). It
computes **no** `audit.json` (the next row), runs **no** command and sets **no** process exit code (the
`fsgg ship` row, which translates the basis to a numeric exit), and evaluates **no** cache/freshness
(Phase 11).

**Confirmed during planning (the plan-time reconciliations the spec deferred — research D1–D8):**

- **Project home (D1)**: a new pure-leaf packable project **`FS.GG.Governance.Ship`** (`Model.fsi`/`.fs`
  for the result vocabulary + `Ship.fsi`/`.fs` for the `rollup` entry point), `IsPackable=true`. The
  ship lineage's pure core — sibling-to-be of the `audit.json` projection and the `fsgg ship` command,
  mirroring `Route` → `RouteJson` → `RouteCommand`.
- **References (D2)**: two direct project references — `FS.GG.Governance.Enforcement` (F023) and
  `FS.GG.Governance.Route` (F019); `Gates`/`Findings`/`Config` flow transitively. **No new third-party
  `PackageReference`** (`System.*`/FSharp.Core only) — the rollup needs no serialization, git, clock, or
  filesystem primitive.
- **Gate → enforcement input (D3)**: a `block-on-*` maturity ⇒ base `Blocking`; `observe`/`warn` ⇒ base
  `Advisory`; the gate's `Maturity` is passed to F023 verbatim. The only mapping consistent with F023's
  own `maturityFloor` (the `ProductCheck`-style fact heuristic F018 used).
- **Finding → enforcement input (D4)**: `GovernedRootUnknown` ⇒ base `Advisory` + maturity-equivalent
  `warn` (always passing); `ProtectedBoundaryUnknown` ⇒ base `Blocking` + `block-on-ship` (floor =
  `gate`), so an escalated protected-boundary finding **blocks at `--mode gate` even when the change
  selected no gate** (the spec edge case), and relaxes to a warning below it.
- **Result shape (D5)**: `ShipDecision` exposes `Verdict`, `ExitCodeBasis`, and a three-way mutually
  exclusive, jointly exhaustive `Blockers`/`Warnings`/`Passing` partition — making the 1:1 accounting
  (`|B|+|W|+|P| = N+M`, SC-006) and the no-hide rule directly checkable, with no item stored twice.
- **Identity & order (D6)**: each item carries an `EnforcedItemId` (`GateItem of GateId` /
  `FindingItem of FindingId * GovernedPath`); each list is sorted by a stable composite key (gates
  before findings, gates by `GateId`, findings by `(Path, finding-id token)`), reusing F018
  `gateIdValue` and F017 `findingIdToken`.
- **Dedup / accounting (D7)**: no re-dedup — F019 already union-deduped selected gates by `GateId`; the
  rollup maps 1:1 over each distinct selected gate and each finding exactly once.
- **Worked example (D8)**: reproduced by a `BlockOnShip` gate at `inner`/`light` (a warning) and a
  same-rollup `BlockOnShip`-blocks / `BlockOnRelease`-warns pair at `gate`/`light` (SC-002).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (from `Directory.Build.props`), `LangVersion latest`.

**Primary Dependencies**: Two project references — `FS.GG.Governance.Enforcement` (F023, for
`RunMode`/`Profile`/`Severity`/`EnforcementInput`/`EnforcementDecision`/`deriveEffectiveSeverity`) and
`FS.GG.Governance.Route` (F019, for `RouteResult`/`SelectedGate`); `Gates`/`Findings`/`Config` flow
transitively. **No new third-party `PackageReference`** — pure value logic over closed DUs and lists.

**Storage**: None. The feature performs no I/O and persists no artifact (FR-012).

**Testing**: `dotnet test` (Expecto + FsCheck via the VSTest adapters — the F023 test shape). Tests
drive the **public** surface (`Ship.rollup`, the `Ship.Model` values) through the built library /
prelude, never private helpers (Principle V). FsCheck properties assert **totality** over the
enumerated cross-product of gate maturities × finding zones × modes × profiles (SC-005), **determinism**
by evaluating twice (SC-004), **base-severity carry** (output base ≡ mapped base, SC-003), and the
**partition law** (disjoint + exhaustive, `|B|+|W|+|P| = N+M`, SC-006). Example tests pin the design's
worked example (SC-002) and the empty-route edge case. All inputs are real typed values built from
F018/F017/F019 constructors — no mocks, no synthetic evidence anticipated; any `Synthetic` token would
carry a use-site disclosure (Principle V).

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host.

**Project Type**: Optional packable F# library (pure leaf) plus one test project — the
F015/F017/F018/F019/F021/F023 shape (`.fsi`/`.fs` pairs referenced by a test project), not the
`Host`/`RouteCommand` edge shape.

**Performance Goals**: Not throughput-bound. The rollup is O(N+M) over the routed items, a single map +
partition + two sorts. Determinism and totality, not latency, are the contract.

**Constraints**: **Total** (defined for every combination incl. the empty `RouteResult`),
**deterministic** (no clock/environment/host-path/input-arrival influence — FR-009, SC-004), **never
throws** (FR-008). Echoes base severity unchanged (FR-006, SC-003), drops no item (FR-010, SC-006),
re-derives/re-sorts/re-classifies nothing the upstream cores fixed, computes no `audit.json`/exit
code/cache/freshness/`policy.yml` dial (FR-012, SC-007).

**Scale/Scope**: One new production project (`FS.GG.Governance.Ship`: `Model` then `Ship` module pairs)
+ one test project. Two inward project references; zero new packages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic tests → Implementation | PASS | `.fsi` drafted in `contracts/` and exercised in FSI before any `.fs`; semantic tests drive the public surface (quickstart). |
| II. Visibility in `.fsi`, not `.fs` | PASS | Two curated `.fsi` (`Model`, `Ship`); the gate/finding mappings and sort-key helper are hidden by absence. No access modifiers in `.fs`. |
| III. Idiomatic simplicity | PASS | Functions over closed DUs/records, `List.map`/`List.partition`/`List.sortBy` pipelines. No custom operators, SRTP, reflection, type providers, or non-trivial CEs. No justification owed. |
| IV. Elmish/MVU boundary | PASS (N/A) | Pure, total, side-effect-free value-to-value computation — no multi-step state, I/O, retries, or background work. A pure leaf, not an MVU edge (spec "Boundary discipline"; the F019/F023 precedent). |
| V. Test evidence mandatory | PASS | Expecto + FsCheck assert totality/determinism/carry/partition over real typed values; tests fail before, pass after. No mocks; no synthetic evidence anticipated. |
| VI. Observability & safe failure | PASS (N/A) | A total never-throwing pure function with no I/O has no failure path to log; reasons are carried per item. No silent failure exists to forbid. |
| Tier classification | Tier 1 | New public API surface (new packable project + module surface). Requires `.fsi`, surface-area baseline, test evidence, docs — all in scope below. |
| Engineering: net10.0, curated `.fsi`, surface baseline | PASS | `net10.0` inherited; `Model.fsi`/`Ship.fsi` curated; `surface/FS.GG.Governance.Ship.surface.txt` baseline added + drift test. |
| Engineering: dependency minimalism | PASS | Two inward project refs (`Enforcement`, `Route`); zero new third-party packages. Core stays off git/filesystem/Skia/NuGet/templates. |
| Engineering: genericity / operating rule | PASS | No rendering package ids, template names, or paths; consumes only product-neutral typed values. Governs itself with standard Spec Kit. |

No violations. **Complexity Tracking is empty** (nothing to justify).

## Project Structure

### Documentation (this feature)

```text
specs/024-ship-verdict-rollup/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0: D1–D8 reconciliations
├── data-model.md        # Phase 1: types & invariants
├── quickstart.md        # Phase 1: validation/run guide
├── contracts/           # Phase 1: Model.fsi, Ship.fsi, ship-decision.md
│   ├── Model.fsi
│   ├── Ship.fsi
│   └── ship-decision.md
├── spec.md              # Feature spec (input)
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Ship/                       # NEW pure-leaf packable project (D1)
├── FS.GG.Governance.Ship.fsproj                 # IsPackable=true; refs Enforcement + Route; no new pkgs
├── Model.fsi                                     # Verdict, ExitCodeBasis, EnforcedItemId, EnforcedItem, ShipDecision
├── Model.fs                                       # (no access modifiers; visibility via .fsi)
├── Ship.fsi                                       # val rollup: RouteResult -> RunMode -> Profile -> ShipDecision
└── Ship.fs                                        # mapping (D3/D4) + partition + verdict + sort (hidden helpers)

tests/FS.GG.Governance.Ship.Tests/                # NEW test project (F023 shape)
├── FS.GG.Governance.Ship.Tests.fsproj            # Expecto + FsCheck; refs Ship (+ Route/Enforcement/Gates/Findings/Config for fixtures)
├── Support.fs                                     # real F018/F017/F019 fixture builders (no mocks)
├── RollupTests.fs                                 # US1: verdict/blockers/warnings/exit-code-basis
├── WorkedExampleTests.fs                          # US2 / SC-002
├── CarryTests.fs                                  # SC-003: base-severity carry, no-hide
├── DeterminismTests.fs                            # SC-004: twice-equal
├── TotalityTests.fs                               # SC-005/SC-006: cross-product, partition law, empty route
├── SurfaceDriftTests.fs                           # Tier 1 surface baseline check
└── Main.fs                                        # Expecto entry

surface/
└── FS.GG.Governance.Ship.surface.txt             # NEW surface-area baseline for the public module
```

**Structure Decision**: New pure-leaf project `src/FS.GG.Governance.Ship` + test project
`tests/FS.GG.Governance.Ship.Tests`, registered in `FS.GG.Governance.sln`, with a `surface/`
baseline — the one-row-one-project rhythm of F014–F023 and the F019 internal split (`Model` value
types + entry-point module). No web/mobile structure applies (a library, not a service).

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
