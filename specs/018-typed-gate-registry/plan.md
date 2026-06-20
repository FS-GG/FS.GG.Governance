# Implementation Plan: Typed Gate Registry

**Branch**: `018-typed-gate-registry` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/018-typed-gate-registry/spec.md`

## Implementation Progress

**Status: ✅ COMPLETE — all 29 tasks done, 21/21 semantic tests green, full solution green (320 tests).**

> **Legend:** ✅ done (real evidence) · 🟡 in progress · ⬜ not started.

| Phase | Tasks | Status |
|---|---|---|
| 1 — Setup (library, test project, contracts, fixtures, prelude, readiness) | T001–T009 | ✅ |
| 2 — Foundation (Model, `defaultTimeout`, command index, `gateIdOf`, sort + skeleton) | T010–T012 | ✅ |
| 3 — US1: stable gate identity per check (MVP) | T013–T015 | ✅ |
| 4 — US2: trustworthy registry (FsCheck invariants) (MVP) | T016–T017 | ✅ |
| 5 — US3: deterministic, explainable registry | T018–T019 | ✅ |
| 6 — US4: product-check flag + freshness key + command timeout | T020–T023 | ✅ |
| 7 — US5: deterministic `GateId` order | T024–T025 | ✅ |
| 8 — Polish (surface baseline + drift, scope hygiene, quickstart, README/plan) | T026–T029 | ✅ |

**Evidence**: `surface/FS.GG.Governance.Gates.surface.txt` (exactly the `Model` + `Gates` modules,
nothing private); FSI transcript + SC-traceability in
[`readiness/README.md`](./readiness/README.md); `Gates → Config`-only dependency proven by
`SurfaceDriftTests`. No synthetic evidence (research D4: no never-triggered branch to exercise).

## Summary

Define the Phase-2 **Gate identities** row: a typed `GateId` and a `Gate` registry assembled from the
already-validated F014 typed facts. A single **pure, total** function `buildRegistry : TypedFacts ->
GateRegistry` projects each declared capability `Check` into one `Gate` carrying the metadata the
design's *Gate identities* table fixes — `Id` (= `domain:checkId`), `Domain`, `Description`,
`Prerequisites`, `Cost`, `Timeout`, `Owner`, `Maturity`, `ProductCheck`, and `FreshnessKey`. The
registry is the single source of stable gate identity that the remaining Phase-2 rows (`fsgg route` /
`fsgg ship`, route/audit JSON, `.fsgg/gates.json`) and Phase 5 (enforcement) / Phase 11 (cost & cache)
consume by `GateId`.

Because the input is `Valid TypedFacts` — which F014's `Schema.validate` has already proven to have
unique check ids and resolved cross-references — assembly is **total and emits no diagnostics**: it
*preserves* those guarantees by construction (an injective `GateId` per check) and the feature
*proves* the preservation with property tests, rather than re-checking already-proven invariants
(research D4; the F017 precedent). The work lands as a new optional, packable library
**`FS.GG.Governance.Gates`** plus its test project — the same shape as Config, Routing, Snapshot, and
Findings — referencing **only** `FS.GG.Governance.Config`. It adds **no new third-party dependency**
and, unlike Findings, takes **no Routing dependency**: the registry is a projection of the declared
catalog, independent of any change. The boundary is a plain pure function — no MVU, no ports —
because the feature performs no I/O, senses no git, and holds no state (FR-013): it only projects
already-typed inputs, exactly as F015 `route` and F017 `findUnknownGovernedPaths` do (research D2).

The feature stops at the typed `GateRegistry`. Held firm by FR-015, it does **not** select gates for a
route; run or execute any gate/check/command; assign base/effective severity or profile/mode/maturity
enforcement; compute evidence freshness or cache reuse; decide a ship verdict; or emit
`.fsgg/gates.json`, route/audit JSON, or any CLI command. Those are later Phase-2 / Phase-5 / Phase-11
rows that consume this registry.

**Confirmed during planning (maintainer-confirmed scope reconciliations — research D4/D5/D6):**

- **No diagnostics layer.** `Valid TypedFacts` is already validated, so duplicate-id / dangling-
  prerequisite / cycle diagnostics cannot fire on real input. The registry is a total function that
  preserves F014's guarantees by construction and proves them by FsCheck (research D4). The spec's
  US2 / FR-005–007 were reconciled from "emit diagnostics" to "preserve & prove guarantees."
- **Prerequisites = declared command reference only.** `GatePrerequisite = RequiresCommand of
  CommandId`, populated from `Check.Command`. Gate-to-gate prerequisites (and the topological order /
  cycle handling they would need) are **deferred to Phase 10**, where the cost-tiered generated-
  product checks and a real prerequisite declaration land — cost tiers are an expense ordering, not a
  run-prerequisite, so deriving edges from them would invent semantics (research D5).
- **Product-check = environment heuristic.** `ProductCheck = (Check.Environment = Release)` in the
  MVP — the only declared signal, since F014 carries no check↔surface link; Phase 10 refines it via
  product-domain tagging (research D6).
- **Project home**: a new sibling library `FS.GG.Governance.Gates` → `Config` only; no new package,
  no Routing edge (research D1).
- **Boundary shape**: a single pure total `buildRegistry : TypedFacts -> GateRegistry`; no
  `Model`/`Msg`/`Effect`/`update` (research D2).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: **No new third-party dependency.** One new `ProjectReference` —
`FS.GG.Governance.Config` (the typed-fact model). Its own code is BCL + FSharp.Core only; the
transitive YamlDotNet edge arrives via Config and is unused here. **No Routing reference** (research
D1). Test-only packages remain the centrally pinned Expecto/FsCheck/VSTest set in
`Directory.Packages.props`.

**Storage**: None. Pure in-memory values; no file, process, clock, or network access of any kind.

**Testing**: `dotnet test` (Expecto + FsCheck via VSTest). The pure projection is exercised through
its public surface over real in-memory `TypedFacts` — the actual values a downstream caller passes,
not mocks (research D10): per-check projection + full metadata field set, the FsCheck registry
invariants (distinct ids, count parity, prerequisites resolve, total assembly), twice-identical +
permutation-invariant determinism, the product-check environment split, freshness-key content, and
command-vs-default timeout. A surface-drift test guards `surface/FS.GG.Governance.Gates.surface.txt`;
an FSI/prelude transcript assembles a registry from a fixture.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host. No platform
capability is touched (no git executable, no filesystem) — like F017, this row reaches nothing.

**Project Type**: Optional packable F# class library plus one test project — the same shape as
Config, Routing, Snapshot, and Findings.

**Performance Goals**: Deterministic projection, not throughput. Per declared check the work is a
constant-time field projection plus, for a check with a command, one lookup in the tooling command
map — O(checks) with an O(commands) index build — then one ordinal sort of the gates. Byte-for-byte
stable output for identical inputs (SC-003/SC-006). No wall-clock, environment, or host-path value
enters the result.

**Constraints**: Pure and total (FR-013/FR-007) — no I/O, git, or clock; never throws; an empty
registry is a valid success (FR-014). `TypedFacts.Tooling` (and `Policy`) are `option`: an absent
`tooling.yml` (`Tooling = None`) yields an empty command-timeout index, so every gate takes
`defaultTimeout` — the projection unwraps with `Option.defaultValue []` and never assumes `Some`.
Deterministic, `GateId`-ordinal-sorted output unchanged under
input re-ordering (FR-011, SC-006). No re-validation of already-validated facts and no diagnostic
channel (research D4). Gates carry only declared id newtypes, a composed description, a bounded
timeout, a product-check bool, and a carried freshness key — no raw YAML, host paths, or timestamps
(FR-004, SC-004). Requires no installed FS.GG package in any inspected repo (FR-016). Out of scope
held firm by FR-015.

**Scale/Scope**: One new production project (`src/FS.GG.Governance.Gates`) and one test project
(`tests/FS.GG.Governance.Gates.Tests`). Public modules are `Model` and `Gates`, each with a curated
`.fsi` and a single combined surface baseline. One closed `GatePrerequisite` set (`RequiresCommand`).
**No** change to any existing project's public surface — Config is referenced as-is (its existing
public types suffice).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design —
still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/Model.fsi`](./contracts/Model.fsi) and [`contracts/Gates.fsi`](./contracts/Gates.fsi) fix the public surface before any `.fs` exists. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `Model.fsi` and `Gates.fsi` are the sole public surface; `.fs` files carry no top-level access modifiers. Add `surface/FS.GG.Governance.Gates.surface.txt` + a surface-drift test. No existing baseline changes (no cross-feature surface touch). |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs, a `Map<CommandId, TimeoutLimit>` index over the tooling commands, list map/sort. A single pure function is the *simplest* boundary for a pure projection (vs MVU ceremony), justified in research D2. **Refusing the never-triggered diagnostic layer (D4) is itself the simplicity choice** — no dead validation branch. Any `mutable` fold accumulator is disclosed at the use site. No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. |
| IV. Elmish/MVU boundary | **PASS** | Principle IV mandates the MVU boundary only for **stateful or I/O** features. This feature performs no I/O, senses no git, holds no multi-step state (FR-013) — it is a pure total projection of already-typed inputs, the "single rule evaluation / pure function" case the principle explicitly exempts. The same call F015 `route` and F017 `findUnknownGovernedPaths` made and the constitution blesses. |
| V. Test evidence mandatory | **PASS** | Tests run through the public surface over **real in-memory `TypedFacts`** — the genuine downstream input, not fakes (research D10). No network/git/agent is reachable. **No synthetic evidence is anticipated** — every case is reachable from real `Valid TypedFacts`, a direct dividend of D4 (no never-triggered branch to exercise). Any literal standing in for an un-derivable case would carry `Synthetic` in the test name + a use-site disclosure and be listed in the PR. |
| VI. Observability & safe failure | **PASS** | Each gate is a stable-id, located (domain), explained (description) record — the diagnostic surface this feature *produces* for later route/audit. An empty registry is a distinct successful outcome, never an error (FR-014). The function is total — no swallowed exception, because there is no operation that can throw and the inputs are already validated. A tool defect is a test failure, never a malformed gate. |
| Change Classification | **Tier 1** | New public, packable surface (a registry library), new public `.fsi`s, new surface baseline. Adds a new *project* but **no new third-party dependency** and **no change to any existing project's public surface**. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency direction (`Gates → Config`; Kernel/Host/adapters/Routing/Snapshot/Findings/CLI unaffected and do not reference Gates in this feature). No new third-party `PackageReference`; the kernel stays BCL-only and never sees the gate-registry vocabulary (FR-016). This is a *layered* capability in a separate project — exactly the constitution's prescription. |

**Constitution alignment on the boundary (Principle IV).** Principle IV requires the
Model/Msg/Effect/update boundary for features "with multi-step state, external I/O, retries, user
interaction, background work, or operational recovery," and explicitly exempts "simple pure
functions — a fact store, a single rule evaluation, an explanation formatter." F018 is squarely the
exempt case: a deterministic projection from validated typed facts to a typed gate registry, with no
state and no effect. F015 `route` and F017 took the same path for the same reason; this row follows.

**Constitution alignment on simplicity (Principle III / D4).** The most consequential design decision
— *not* building a duplicate/dangling/cycle diagnostic layer — is an application of Principle III, not
a gap: re-validating facts F014 has already proven consistent would be a branch no valid input can
reach, and "complex features … without matching justification … [are] a spec defect." The spec was
reconciled (US2 / FR-005–007) so the contract matches the honest, total design.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/018-typed-gate-registry/
├── plan.md              # This file
├── research.md          # Phase 0 output (D1–D10 + resolved Technical Context)
├── data-model.md        # Phase 1 output (consumed + produced types, invariants, determinism)
├── quickstart.md        # Phase 1 output (validation guide + acceptance→evidence map)
├── contracts/
│   ├── Model.fsi        # gate-domain types: GateId, GatePrerequisite, FreshnessKey, Gate, GateRegistry, gateIdValue
│   └── Gates.fsi        # the pure entry point: defaultTimeout, buildRegistry
├── checklists/
│   └── requirements.md  # spec quality checklist (created by /speckit-specify)
├── readiness/           # FSI transcripts + SC traceability note (created during tasks)
└── tasks.md             # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Gates/                        # NEW optional registry library
├── FS.GG.Governance.Gates.fsproj                  # references Config only; no new package; no Routing
├── Model.fsi                                       # = contracts/Model.fsi
├── Model.fs                                        # GateId/GatePrerequisite/FreshnessKey/Gate/GateRegistry/gateIdValue
├── Gates.fsi                                       # = contracts/Gates.fsi
└── Gates.fs                                        # defaultTimeout + buildRegistry: per-check projection + GateId sort (PURE)

tests/FS.GG.Governance.Gates.Tests/                # NEW semantic tests
├── FS.GG.Governance.Gates.Tests.fsproj            # references Gates (+ Config transitively)
├── Support.fs                                       # in-memory TypedFacts fixture builders (checks, commands)
├── GateBuildTests.fs                                # US1: per-check projection + full metadata field set (SC-001)
├── RegistryInvariantTests.fs                        # US2: FsCheck distinct ids / count parity / prereqs resolve / total (SC-002)
├── DeterminismTests.fs                              # US3/US5: twice-identical + permutation + GateId order + id-only fields (SC-003/SC-006)
├── MetadataTests.fs                                 # US4: product-check env split + freshness key + command/default timeout (SC-004/SC-005)
├── SurfaceDriftTests.fs                             # baseline drift check
└── Main.fs

surface/FS.GG.Governance.Gates.surface.txt           # NEW public surface baseline
scripts/prelude.fsx                                 # extend with an F018 facts→registry sketch
FS.GG.Governance.sln                                # add Gates project and Gates test project
CLAUDE.md                                            # SPECKIT block repointed to this plan
```

**Structure Decision**: a new `FS.GG.Governance.Gates` class library, sibling to
Kernel/Host/adapters/Config/Routing/Snapshot/Findings, is the home for the gate registry. It
references **only** `FS.GG.Governance.Config` and adds no third-party dependency, keeping the
dependency direction one-way (`Gates → Config`) and the kernel/host untouched. It takes **no Routing
edge** because the registry is a projection of the declared catalog, independent of any change — gate
*selection* by route is a later row that will reference both Routing and Gates. Splitting `Model` (the
gate types) from `Gates` (the assembler) mirrors the F014/F015/F016/F017 pure-core layout and lets the
surface baseline and the projection logic be reviewed independently. The registry lives in the
product-neutral Governance layer, never the kernel, because the gate vocabulary must not reach the
kernel (FR-016).

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
