# Implementation Plan: Unknown Governed Path Findings

**Branch**: `017-unknown-governed-path-findings` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/017-unknown-governed-path-findings/spec.md`

## Summary

Make the decision **F015 routing deliberately deferred**: turn its per-path `UnmatchedInRoot`
outcome (which "carries no domain and asserts no finding/severity") into an explicit, typed
**unknown-governed-path finding** — *without* global default-deny. Consuming the F015 `RouteReport`
and the F014 declared `Surface` classification, a single **pure, total** function classifies each
candidate path: a path inside the governed root that no capability glob matched and no declared
`Routine` surface covers becomes a finding; an `OutOfScope` or `Routed` or routine-covered path
stays silent; a path on a declared `ProtectedSurface` boundary is escalated to a distinct,
surface-identifying flavor. This closes the two Phase-2 exit criteria F015 left open —
*"Routine unclassified files do not trigger global default-deny behavior"* and *"Unknown paths
under declared governed roots produce explicit findings."*

The work lands as a new optional, packable library **`FS.GG.Governance.Findings`** plus its test
project — the same shape as Config (F014), Routing (F015), and Snapshot (F016). It references
**`FS.GG.Governance.Config`** (typed-fact newtypes `GovernedPath`/`SurfaceId`/`Surface`/`TypedFacts`)
and **`FS.GG.Governance.Routing`** (the `RouteReport`/`PathRouting`/`RoutingResult` it consumes),
and adds **no new third-party dependency**. The boundary is a plain pure function — no MVU, no
ports — because the feature performs no I/O, senses no git, and holds no state (FR-011): it only
classifies already-typed inputs, exactly as F015 `route` does (research D2).

The feature stops at the typed `FindingReport`. Held firm by FR-013, it does **not** assign
severity, base/effective enforcement, or profile/mode/maturity adjustment; build the gate registry
or any `GateId`; compute evidence freshness; decide a ship verdict; or emit route/audit JSON or any
CLI command. Those are later Phase-2 / Phase-5 rows that consume these findings.

**Confirmed during planning:**

- **Project home**: a new sibling library `FS.GG.Governance.Findings` → `Config` + `Routing`; no
  new package (research D1). It is the natural consumer of *both* predecessors; the dependency
  direction stays one-way (`Findings → Routing → Config`) and the kernel stays untouched.
- **Boundary shape**: a single pure total `findUnknownGovernedPaths : TypedFacts -> RouteReport ->
  FindingReport`; no `Model`/`Msg`/`Effect`/`update` (research D2). The MVU boundary is for
  stateful/I/O features; this is neither (Principle IV / FR-011).
- **FR-007 precedence — maintainer-confirmed**: `Protected > Routine > Ordinary`. A path declared
  *both* routine and protected is **escalated, not suppressed** — the fail-safe posture; the
  finding message names both surfaces so the contradiction is fixable (research D4,
  [contracts/precedence.md](./contracts/precedence.md)).
- **Plane handling**: the decision is path+surface keyed, never plane keyed, so US5 uniformity and
  cross-plane dedup hold by construction; no `ChangePlane` type is introduced in this MVP — FR-010
  permits the plane to be retained but does not require it (research D5).
- **Surface membership**: the same segment-prefix relation F015 used for the governed root,
  reproduced as a tiny local helper over `Surface.Paths` rather than exposing `Routing.inRoot`
  (research D3); paths arrive already normalized, so nothing is re-normalized (FR-014).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: **No new third-party dependency.** Two new `ProjectReference`s —
`FS.GG.Governance.Config` (typed-fact model) and `FS.GG.Governance.Routing` (the routing outcomes).
Its own code is BCL + FSharp.Core only; the transitive YamlDotNet edge arrives via Config and is
unused here. Test-only packages remain the centrally pinned Expecto/FsCheck/VSTest set in
`Directory.Packages.props`.

**Storage**: None. Pure in-memory values; no file, process, clock, or network access of any kind.

**Testing**: `dotnet test` (Expecto + FsCheck via VSTest). The pure classifier is exercised
through its public surface over real in-memory `TypedFacts` and real `RouteReport`s — the actual
values a downstream caller passes, not mocks (research D7): finding-vs-no-finding per routing
outcome, routine suppression, protected escalation + identity, `Protected > Routine` precedence,
twice-identical + permutation-invariant determinism, message content, and cross-plane dedup. A
surface-drift test guards `surface/FS.GG.Governance.Findings.surface.txt`; an FSI/prelude transcript
routes a fixture then classifies it.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host. No platform
capability is touched (no git executable, no filesystem) — unlike F016, this row reaches nothing.

**Project Type**: Optional packable F# class library plus one test project — the same shape as
Config, Routing, and Snapshot.

**Performance Goals**: Deterministic classification, not throughput. Per candidate path the work is
its routing-outcome match plus, for an in-root miss, a membership test against the declared
surfaces — O(paths × surfaces) — then one ordinal sort of the findings. Byte-for-byte stable output
for identical inputs (SC-004). No wall-clock, environment, or host-path value enters the result.

**Constraints**: Pure and total (FR-011/FR-012) — no I/O, git, or clock; never throws; an empty
finding set is a valid success. Deterministic, ordinally-sorted output unchanged under input
re-ordering (FR-009, SC-004). No global default-deny: `OutOfScope` and `Routine` paths are silent
(FR-003/FR-004). Findings carry only normalized `GovernedPath`s, declared `SurfaceId`s, a zone, and
a fix-hint message — no raw YAML, host paths, timestamps, or extra product vocabulary (FR-008,
SC-006). Requires no installed FS.GG package in any inspected repo (FR-015). Out of scope held firm
by FR-013.

**Scale/Scope**: One new production project (`src/FS.GG.Governance.Findings`) and one test project
(`tests/FS.GG.Governance.Findings.Tests`). Public modules are `Model` and `Findings`, each with a
curated `.fsi` and a single combined surface baseline. One closed `FindingId` set
(`UnknownGovernedPath`, `UnknownProtectedBoundaryPath`). **No** change to any existing project's
public surface — Config and Routing are referenced as-is (their existing public types suffice).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/Model.fsi`](./contracts/Model.fsi), [`contracts/Findings.fsi`](./contracts/Findings.fsi), and [`contracts/precedence.md`](./contracts/precedence.md) fix the public surface and the decision contract before any `.fs` exists. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `Model.fsi` and `Findings.fsi` are the sole public surface; `.fs` files carry no top-level access modifiers. Add `surface/FS.GG.Governance.Findings.surface.txt` + a surface-drift test. No existing baseline changes (no cross-feature surface touch). |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs, a private segment-prefix membership helper, list filter/map/sort. A single pure function is the *simplest* boundary that fits a pure classifier (vs MVU ceremony), justified in research D2. Any `mutable` fold accumulator is disclosed at the use site. No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. |
| IV. Elmish/MVU boundary | **PASS** | Principle IV mandates the MVU boundary only for **stateful or I/O** features. This feature performs no I/O, senses no git, holds no multi-step state (FR-011) — it is a pure total classification of already-typed inputs, exactly the "single rule evaluation / pure function" case the principle explicitly exempts ("do not need Elmish ceremony"). The same call F015 `route` made and the constitution blesses. |
| V. Test evidence mandatory | **PASS** | Tests run through the public surface over **real in-memory `TypedFacts` + real `RouteReport`s** — the genuine downstream input, not fakes (research D7). No network/git/agent is reachable. No synthetic evidence anticipated; any literal standing in for an un-derivable case carries `Synthetic` in the test name + a use-site disclosure and is listed in the PR. |
| VI. Observability & safe failure | **PASS** | Each finding is a stable-id, located, explained record with a fix hint (FR-008) — the diagnostic surface this feature *produces*. An empty finding set is a distinct successful outcome, never an error and never a fabricated "all clear" (FR-012). The function is total — no swallowed exception, because there is no operation that can throw. A tool defect is a test failure, never a finding. |
| Change Classification | **Tier 1** | New public, packable surface (a classifier library), new public `.fsi`s, new surface baseline. Adds a new *project* but **no new third-party dependency** and **no change to any existing project's public surface**. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency direction (`Findings → Routing → Config`; Kernel/Host/adapters/Snapshot/CLI unaffected and do not reference Findings in this feature). No new third-party `PackageReference`; the kernel stays BCL-only and never sees the surface/finding vocabulary (FR-015). This is a *layered* capability in a separate project — exactly the constitution's prescription. |

**Constitution alignment on the boundary (Principle IV).** Principle IV requires the
Model/Msg/Effect/update boundary for features "with multi-step state, external I/O, retries, user
interaction, background work, or operational recovery," and explicitly exempts "simple pure
functions — a fact store, a single rule evaluation, an explanation formatter." F017 is squarely the
exempt case: a deterministic function from typed facts + routing outcomes to a typed finding set,
with no state and no effect. The pure/edge separation the principle protects is preserved trivially
(everything is pure). F015 `route` took the same path for the same reason; this row follows it.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/017-unknown-governed-path-findings/
├── plan.md              # This file
├── research.md          # Phase 0 output (D1–D7 + resolved Technical Context)
├── data-model.md        # Phase 1 output (consumed + produced types, invariants, determinism)
├── quickstart.md        # Phase 1 output (validation guide + acceptance→evidence map)
├── contracts/
│   ├── Model.fsi        # finding-domain types: FindingId, FindingZone, UnknownGovernedPathFinding, FindingReport, findingIdToken
│   ├── Findings.fsi     # the pure entry point: findUnknownGovernedPaths
│   └── precedence.md    # suppression/escalation precedence + dedup + ordering + message contract
├── checklists/
│   └── requirements.md  # spec quality checklist (created by /speckit-specify, if present)
├── readiness/           # FSI transcripts + SC traceability note (created during tasks)
└── tasks.md             # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Findings/                     # NEW optional classifier library
├── FS.GG.Governance.Findings.fsproj               # references Config + Routing; no new package
├── Model.fsi                                       # = contracts/Model.fsi
├── Model.fs                                        # FindingId/FindingZone/UnknownGovernedPathFinding/FindingReport/findingIdToken
├── Findings.fsi                                    # = contracts/Findings.fsi
└── Findings.fs                                     # findUnknownGovernedPaths: membership + precedence + dedup + ordering (PURE)

tests/FS.GG.Governance.Findings.Tests/             # NEW semantic tests
├── FS.GG.Governance.Findings.Tests.fsproj          # references Findings (+ Routing/Config transitively)
├── Support.fs                                       # in-memory TypedFacts + RouteReport fixture builders
├── FindingDecisionTests.fs                          # US1/US2: finding vs no-finding per routing outcome + routine suppression (SC-001/SC-002)
├── PrecedenceTests.fs                               # US3: protected escalation + identity + Protected>Routine overlap (SC-003)
├── DeterminismTests.fs                              # US4: twice-identical + FsCheck permutation + message content (SC-004/SC-006)
├── PlaneUniformityTests.fs                          # US5: per-plane parity + cross-plane dedup (SC-007)
├── SurfaceDriftTests.fs                             # baseline drift check
└── Main.fs

surface/FS.GG.Governance.Findings.surface.txt        # NEW public surface baseline
scripts/prelude.fsx                                 # extend with an F017 route→classify sketch
FS.GG.Governance.sln                                # add Findings project and Findings test project
CLAUDE.md                                            # SPECKIT block repointed to this plan
```

**Structure Decision**: a new `FS.GG.Governance.Findings` class library, sibling to
Kernel/Host/adapters/Config/Routing/Snapshot, is the home for the unknown-governed-path
classifier. It references `FS.GG.Governance.Config` and `FS.GG.Governance.Routing` and adds no
third-party dependency, keeping the dependency direction one-way (`Findings → Routing → Config`)
and the kernel/host untouched. Splitting `Model` (the finding types) from `Findings` (the
classifier) mirrors the F014/F015/F016 pure-core layout and lets the surface baseline and the
decision logic be reviewed independently. The classifier lives here, not in Routing, because F015
explicitly deferred this decision and stays scoped to routing; it lives in the product-neutral
Governance layer, never the kernel, because the surface/finding vocabulary must not reach the
kernel (FR-015).

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
