# Quickstart / Validation Guide: Unknown Governed Path Findings (F017)

Validates the F017 acceptance scenarios and success criteria against the **public** surface of
`FS.GG.Governance.Findings`, the way a downstream caller (the later `route`/`ship` commands) will
use it: route a candidate path set with F015, then classify the outcomes with F017. Pure — no git,
no filesystem, no network.

## Prerequisites

- .NET SDK with `net10.0` (per `Directory.Build.props`).
- Build the solution: `dotnet build FS.GG.Governance.sln`.
- The feature consumes already-typed F014 `TypedFacts` and an F015 `RouteReport`. No `.fsgg`
  files and **no installed FS.GG package** are required in any inspected repo (FR-015, SC-005).

## Run the tests

```bash
dotnet test tests/FS.GG.Governance.Findings.Tests/FS.GG.Governance.Findings.Tests.fsproj
```

Expected: all green. The suite maps one-to-one to the user stories:

| Test file | Covers | Asserts |
|---|---|---|
| `FindingDecisionTests.fs` | US1, US2 (SC-001, SC-002) | non-routine `UnmatchedInRoot` ⇒ exactly one finding on that path with a fix hint; `Routed`, `OutOfScope`, routine-covered ⇒ none |
| `PrecedenceTests.fs` | US3 (SC-003) | protected boundary ⇒ `UnknownProtectedBoundaryPath` / `ProtectedBoundaryUnknown sid`; ordinary vs protected distinguishable; routine∩protected ⇒ single escalated finding (`Protected > Routine`) |
| `DeterminismTests.fs` | US4 (SC-004, SC-006) | compute twice ⇒ byte-identical; FsCheck permutation of paths & surfaces ⇒ unchanged; every message names the path + ≥1 remediation, no raw YAML/host paths |
| `PlaneUniformityTests.fs` | US5 (SC-007) | same path "from" each plane ⇒ same decision; duplicate path in `Routings` ⇒ single finding |
| `SurfaceDriftTests.fs` | Principle II | public surface matches `surface/FS.GG.Governance.Findings.surface.txt` |

## FSI smoke check (Principle I)

`scripts/prelude.fsx` exercises the packed surface end-to-end. Representative shape:

```fsharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings

// Facts declaring a governed root, a path map that does NOT cover src/Kernel/New.fs,
// and a ProtectedSurface over src/Kernel. (Built in-memory; see Support.fs in tests.)
let facts : TypedFacts = (* … fixture … *)

let report =
    Routing.route facts
        [ normalizePath "src/Kernel/New.fs"     // in-root, unmatched  → finding
          normalizePath "src/Kernel/Eval.fs"    // routed              → no finding
          normalizePath "scratch.txt" ]         // out of scope        → no finding

let found = Findings.findUnknownGovernedPaths facts report
found.Findings
|> List.iter (fun f ->
    printfn "[F17] %s  %A  %s" (Model.findingIdToken f.Id) f.Zone f.Message)
```

Expected: exactly one finding, for `src/Kernel/New.fs`, with
`Id = UnknownProtectedBoundaryPath`, `Zone = ProtectedBoundaryUnknown (SurfaceId "…")`, and a
message naming the path and a remediation. Removing the `ProtectedSurface` declaration yields the
ordinary `UnknownGovernedPath` / `GovernedRootUnknown` flavor instead.

## Acceptance → evidence map

| Spec item | Evidence |
|---|---|
| SC-001 | `FindingDecisionTests` — one finding on the unmatched path; routed paths produce none |
| SC-002 | `FindingDecisionTests` — out-of-scope + routine ⇒ zero findings; non-routine in-root unknown still found |
| SC-003 | `PrecedenceTests` — protected escalation distinguishable + carries `SurfaceId`; overlap resolves by precedence |
| SC-004 | `DeterminismTests` — twice-identical + reorder-invariant |
| SC-005 | whole suite runs pure with no git/network/installed package |
| SC-006 | `DeterminismTests` message assertions — only paths, declared ids, zone, fix hint |
| SC-007 | `PlaneUniformityTests` — per-plane parity + cross-plane dedup |

See [`contracts/precedence.md`](./contracts/precedence.md) for the normative decision contract and
[`data-model.md`](./data-model.md) for the type shapes. Implementation bodies and full fixtures
live in `tasks.md` / the implementation phase, not here.
