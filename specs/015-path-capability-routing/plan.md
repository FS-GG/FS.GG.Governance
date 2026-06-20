# Implementation Plan: Path-to-Capability Routing with Deterministic Glob Precedence

**Branch**: `015-path-capability-routing` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/015-path-capability-routing/spec.md`

## Summary

Deliver the **first consumer of the F014 typed facts**: a pure function that takes the typed
capability facts (governed root, declared domains, and the `glob → capability-domain` path map)
plus a caller-supplied set of normalized repository-relative paths, and answers — deterministically
and explainably — **which capability domain each path belongs to**. The deliverable named by the
implementation-plan row is *deterministic glob precedence*: when several path-map globs match one
path, exactly one wins by a **total, reproducible precedence order** (exact-literal › greater
literal specificity › single-segment `*` over cross-segment `**` › final lexicographic tiebreak),
and the result records which glob won and why.

The work lands as a new, optional, **product-neutral routing library**
`FS.GG.Governance.Routing` plus its test project. The library references only
`FS.GG.Governance.Config` (for the typed-fact model — `GovernedPath`, `DomainId`, `PathMapEntry`,
`CapabilityFacts`, `ProjectFacts`, `TypedFacts`) and **adds no new third-party dependency**; its own
code is BCL + `FSharp.Core` only. Like the Config validation core, the entire surface is a **pure
total function** with no I/O, no git, and no clock (FR-011), so the Constitution's MVU/I-O boundary
(Principle IV) needs no Elmish ceremony — the candidate path set and the facts are inputs, the route
report is a value.

The feature stops at the routing result. Out of scope, held firm by FR-016: git/CI changed-path
sensing, the *finding* severity/scoping for unmatched in-root paths, surface-class assignment, the
gate registry, profile/mode enforcement, and the `route`/`ship` commands and their JSON. Those are
later Phase-2 rows that consume this routing result.

**Confirmed during planning:**

- **Glob engine**: a hand-written, BCL-only segment matcher over the closed MVP syntax
  (literal, `?`, `*`, `**`); no regex translation and no external glob/filesystem library
  (research D2).
- **Precedence**: a total, deterministic specificity key with a final lexicographic tiebreak;
  a tie *before* the tiebreak emits `AmbiguousRoute` yet still resolves to one winner (research D3).
- **Candidate paths**: consumed as already-normalized `GovernedPath` values (the F014 form);
  Routing performs **no** path normalization and does not re-validate the catalog (research D8,
  FR-003/FR-014).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: **No new third-party dependency.** One new `ProjectReference` —
`FS.GG.Governance.Config` — for the shared typed-fact model. Routing's own code is BCL + `FSharp.Core`
only (it does not use YamlDotNet; it consumes Config's already-parsed facts). Test-only packages
remain the centrally pinned Expecto/FsCheck/VSTest set already in `Directory.Packages.props`.

**Storage**: None. Routing reads no filesystem, runs no process, senses no git state. The candidate
path set is supplied by the caller; the typed facts are supplied by the caller (produced by Config).
The pure core touches no I/O at all (FR-011).

**Testing**: `dotnet test` (Expecto via VSTest). Tests exercise the public library surface (not
private helpers): the glob matcher over real fixture path/glob strings (one accepting case per MVP
construct — literal, `?`, `*`, `**` — and representative rejects); the precedence ladder (one fixture
per FR-005 rung); the `route` entry over fixture `TypedFacts` (US1 routed, US3 in-root-unmatched and
out-of-scope); ambiguity and conflict and unsupported-syntax diagnostics; determinism (route twice →
byte-identical report) and order-independence (FsCheck permutation of path-map entries → identical
report, SC-002/SC-003); a surface-drift check against
`surface/FS.GG.Governance.Routing.surface.txt`; and an FSI/prelude transcript that loads the built
library and routes a fixture path set.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host.

**Project Type**: Optional packable F# class library plus one test project — the same shape as the
Config library and the adapter libraries.

**Performance Goals**: Deterministic, bounded routing rather than throughput. Matching is
O(candidates × globs × segments); a single `route` sorts its output once. Byte-for-byte stable report
for identical input (SC-002). No wall-clock, environment, random, or host-filesystem value enters the
report.

**Constraints**: Total, deterministic precedence — a path matching ≥1 glob is never left unrouted, and
the winner is identical across runs and across re-ordering of the path map (FR-005, FR-012, SC-002/
SC-003). Ambiguity is never a silent arbitrary pick — equally-specific competitors emit
`AmbiguousRoute` and still resolve deterministically (FR-006). Unsupported glob constructs are a
diagnostic, never a silent never-match (FR-010). Pure function, no I/O, no normalization, no catalog
re-validation (FR-011, FR-003, FR-014). Out of scope held firm by FR-016.

**Scale/Scope**: One new production project (`src/FS.GG.Governance.Routing`) and one test project
(`tests/FS.GG.Governance.Routing.Tests`). Public modules are `Model`, `Glob`, and `Routing`, each with
a curated `.fsi` and a single combined surface baseline. Closed MVP glob syntax (literal, `?`, `*`,
`**`). One precedence key with four ordered rungs plus the lexicographic tiebreak. One routing
diagnostic id per failure class named in the spec (`AmbiguousRoute`, `ConflictingGlobBinding`,
`UnsupportedGlobSyntax`).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/Model.fsi`](./contracts/Model.fsi), [`contracts/Glob.fsi`](./contracts/Glob.fsi), [`contracts/Routing.fsi`](./contracts/Routing.fsi), and [`contracts/glob-precedence.md`](./contracts/glob-precedence.md) define the public surface and the precedence contract before implementation. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `Model.fsi`, `Glob.fsi`, `Routing.fsi` are the sole public surface; `.fs` files carry no top-level access modifiers. Add `surface/FS.GG.Governance.Routing.surface.txt` and a surface-drift test. |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs, a hand-written segment matcher, list folds/sorts for ordering. The `**` matcher is a `let rec` backtracking walk — recursion for genuine branching structure, which Principle III explicitly blesses (parser-combinator/tree-walk shape), not state-hiding. Any `mutable` accumulator in a sort/fold is disclosed at the use site. No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. |
| IV. Elmish/MVU boundary | **PASS** | Routing is a pure total function with no I/O, no multi-step state, no retries, and no convergence loop, so it needs no MVU ceremony — the same library allowance Config's pure `Schema.validate` uses (Principle IV; [`research.md`](./research.md) D7 records why). The candidate path set and typed facts are inputs; the route report is a value. |
| V. Test evidence mandatory | **PASS** | Tests run against real fixture path/glob strings and fixture `TypedFacts`; determinism and order-independence are property-tested; each MVP glob construct, each precedence rung, and each diagnostic id has a real fixture. No synthetic evidence is anticipated (no agent, network, clock, or filesystem); if any appears it carries `Synthetic` in the name and a use-site disclosure. |
| VI. Observability & safe failure | **PASS** | Every routing diagnostic carries a stable id, the path and/or glob involved, and a fix hint (FR-013). Ambiguity is reported, never a silent pick (FR-006); unsupported syntax is a diagnostic, never a silent never-match (FR-010); the report distinguishes routed / in-root-unmatched / out-of-scope (FR-007, FR-008). Routing assumes valid F014 facts (FR-014); a broken invariant is a fail-fast/test failure, never a `RoutingDiagnostic`. |
| Change Classification | **Tier 1** | New public, packable surface (a routing library), new public `.fsi`, new surface baseline. Adds a new *project* but **no new third-party dependency** — consistent with a contracted-change tier. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency direction (Routing → Config → YamlDotNet + FSharp.Core; Kernel/Host/adapters/CLI unaffected and do not reference Routing in this feature). No new third-party dependency is introduced; the kernel stays BCL-only. |

**No-new-dependency note (Engineering Constraints):** This feature adds **no** third-party
`PackageReference`. It references the existing `FS.GG.Governance.Config` project for the shared typed
facts and writes a hand-rolled, BCL-only glob matcher (research D2). The constitution's
dependency-minimization rule is satisfied by addition: no FAKE, git, filesystem scanning, or glob
library enters the tree. The transitive YamlDotNet dependency comes only via Config and is not used by
Routing's own code.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/015-path-capability-routing/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── Model.fsi        # routing-domain types: results, reasons, diagnostics
│   ├── Glob.fsi         # pure glob match + specificity/precedence + syntax check
│   ├── Routing.fsi      # the route entry point over TypedFacts + candidate paths
│   └── glob-precedence.md  # the closed glob syntax + total precedence contract
├── readiness/           # FSI transcripts + SC traceability note (created during tasks)
└── tasks.md             # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Routing/                     # NEW optional routing library
├── FS.GG.Governance.Routing.fsproj               # references FS.GG.Governance.Config only
├── Model.fsi                                      # = contracts/Model.fsi
├── Model.fs                                       # RoutingResult/PrecedenceReason/RouteReport/diagnostics
├── Glob.fsi                                       # = contracts/Glob.fsi
├── Glob.fs                                        # segment matcher + specificity key + syntax check
└── Routing.fsi                                    # = contracts/Routing.fsi
└── Routing.fs                                     # route: TypedFacts -> GovernedPath list -> RouteReport

tests/FS.GG.Governance.Routing.Tests/             # NEW semantic tests
├── FS.GG.Governance.Routing.Tests.fsproj
├── Support.fs                                     # fixture builders for TypedFacts / GovernedPath
├── GlobMatchTests.fs                              # one accept per MVP construct + rejects
├── PrecedenceTests.fs                             # one fixture per FR-005 rung (US2)
├── RoutingTests.fs                                # route entry: routed / in-root-unmatched / out-of-scope
├── AmbiguityTests.fs                              # AmbiguousRoute / ConflictingGlobBinding / UnsupportedGlobSyntax
├── DeterminismTests.fs                            # route twice → identical; FsCheck path-map permutation
├── SurfaceDriftTests.fs                           # baseline drift check
└── Main.fs

scripts/prelude.fsx                               # extend with a Routing route sketch
surface/FS.GG.Governance.Routing.surface.txt       # NEW public surface baseline
FS.GG.Governance.sln                              # add Routing project and Routing test project
CLAUDE.md                                          # SPECKIT block repointed to this plan
```

**Structure Decision**: a new `FS.GG.Governance.Routing` class library, sibling to the
Kernel/Host/adapters/Config, is the home for the first config-fact consumer. It references only
`FS.GG.Governance.Config` and adds no third-party dependency, keeping the dependency direction one-way
(Routing → Config) and the kernel/host untouched. Splitting `Glob` (the reusable, separately-testable
match + specificity primitive) from `Routing` (the entry point that applies it across the path map and
classifies in-root/out-of-scope) places the precedence ladder under its own fixtures (SC-006) while the
`route` entry is tested over whole fixture `TypedFacts`. The matcher lives in Routing, not Config,
because it is a *use* of the facts, not part of turning YAML into facts (research D1).

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
