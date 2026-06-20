# Quickstart / Validation Guide: Route Gate Selection (F019)

Validates the F019 acceptance scenarios and success criteria against the **public** surface of
`FS.GG.Governance.Route`, the way a downstream caller (the later `fsgg route`/`fsgg ship` commands and
the route/audit JSON emitter) will use it: take a validated F018 `GateRegistry`, an F015 `RouteReport`
for a change, and an F017 `FindingReport`, and select the route. Pure — no git, no filesystem, no
network.

## Prerequisites

- .NET SDK with `net10.0` (per `Directory.Build.props`).
- Build the solution: `dotnet build FS.GG.Governance.sln`.
- The feature consumes already-typed F015/F017/F018 outputs. No `.fsgg` files and **no installed
  FS.GG package** are required in any inspected repo (FR-013, SC-006).

## Run the tests

```bash
dotnet test tests/FS.GG.Governance.Route.Tests/FS.GG.Governance.Route.Tests.fsproj
```

Expected: all green. The suite maps one-to-one to the user stories, and each test assembles its
inputs from the **real** upstream functions (`Gates.buildRegistry`, `Routing.route`,
`Findings.findUnknownGovernedPaths`) over real `TypedFacts` — the genuine downstream inputs, never
mocks (research D8):

| Test file | Covers | Asserts |
|---|---|---|
| `SelectionTests.fs` | US1 (SC-001) | a `Routed`-to-`d` path selects exactly the `d` gates; a gate in an unreached domain is absent; `UnmatchedInRoot`/`OutOfScope` select nothing; two domains ⇒ the union; join is on `Gate.Domain = DomainId` id equality |
| `TraceTests.fs` | US2 (SC-002) | each selected gate names its selecting path(s), domain, matching glob, declared cost; a multi-path gate appears once with all selecting paths; fields are declared ids only |
| `FindingsCarryTests.fs` | US3 (SC-003) | a non-empty F017 report is carried byte-identically; an empty report ⇒ empty finding list (a success); a finding-bearing `UnmatchedInRoot` path selects no gate yet its finding is present |
| `CostRollupTests.fs` | US4 (SC-004) | the rollup counts the distinct selected gates per `Cost` tier (a shared gate once); empty selection ⇒ all-zero; identical on re-run |
| `DeterminismTests.fs` | US5 (SC-005, SC-006) | FsCheck: `select` twice ⇒ byte-identical; candidate paths AND registry gates permuted ⇒ unchanged; `select` total (never throws), empty inputs ⇒ empty successful route |
| `SurfaceDriftTests.fs` | Principle II | public surface matches `surface/FS.GG.Governance.Route.surface.txt` |

## FSI smoke (the downstream call, end to end)

The `scripts/prelude.fsx` sketch exercises the whole F015→F017→F018→F019 chain over a fixture, as a
downstream caller would:

```fsharp
// (prelude loads the packed/referenced libraries)
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Route

let facts      = (* a Valid TypedFacts fixture: domains build/docs, checks, path-map globs *)
let registry   = Gates.buildRegistry facts
let report     = Routing.route facts [ (* changed candidate paths *) ]
let findings   = Findings.findUnknownGovernedPaths facts report
let result     = Route.select registry report findings

// result.SelectedGates  — sorted by GateId; each carries its Gate + the selecting path(s)/glob(s)
// result.Findings        — the F017 report, unchanged
// result.Cost            — { Cheap; Medium; High; Exhaustive } counts over the distinct selected gates
```

Expected: `SelectedGates` is exactly the union of the reached domains' gates (GateId order); each
names the changed path(s) and the glob each won on; `Findings` is the F017 report verbatim; `Cost`
counts each distinct selected gate once. Re-running, or permuting the candidate paths / registry
gates, yields a byte-identical `result`.

## Acceptance → evidence map

| Spec criterion | Where validated |
|---|---|
| SC-001 union of reached domains' gates; unreached absent | `SelectionTests.fs` |
| SC-002 every gate names path/domain/glob/cost; multi-path gate once | `TraceTests.fs` |
| SC-003 findings carried byte-identically; empty ⇒ empty success; unrouted selects nothing | `FindingsCarryTests.fs` |
| SC-004 per-tier cost over distinct gates; empty ⇒ zero; stable | `CostRollupTests.fs` |
| SC-005 twice-identical + permutation-invariant | `DeterminismTests.fs` |
| SC-006 no I/O, never throws, no installed FS.GG package | `DeterminismTests.fs` + library has no git/filesystem reference |
| SC-007 declared id newtypes + cost only; no severity/enforcement/freshness/ship verdict | `TraceTests.fs` + `contracts/Model.fsi` surface |

## Out of scope to validate here (FR-011)

Severity, enforcement, freshness/cache, gate execution, ship verdict, and route/audit JSON / CLI are
later rows — this guide validates only the typed `RouteResult` they consume.
