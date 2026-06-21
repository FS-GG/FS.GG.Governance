# Quickstart: Broad-Route Cost Explanation Core (F031)

How to build, FSI-exercise, test, and re-bless the surface for `FS.GG.Governance.RouteExplain`. Mirrors the
F019/F020/F030 workflow.

## Prerequisites

- .NET SDK `net10.0` (repo standard).
- A clean checkout; the new project + test project added to `FS.GG.Governance.sln`.

## Build

```sh
dotnet build src/FS.GG.Governance.RouteExplain/FS.GG.Governance.RouteExplain.fsproj
```

The library references `FS.GG.Governance.Route` and `FS.GG.Governance.Gates` only; `Config` arrives
transitively. No new third-party package.

## Design-first FSI proof (Principle I)

Before any `.fs` body exists, the public surface is exercised in `scripts/prelude.fsx` (a new F031 section).
A representative transcript:

```fsharp
// Reusing the F019 route fixtures: a route selecting an Exhaustive CI gate `build:full`, plus a cheap
// local `build:unit` and a medium `build:integration` in the catalog.
open FS.GG.Governance.RouteExplain

RouteExplain.highCostThreshold        // val it : Cost = High

let ex = RouteExplain.explain route registry
ex.Findings |> List.length            // 1  (only build:full is >= High)
let f = ex.Findings.Head
f.Selected.Gate.Id                    // GateId "build:full"
f.Selected.SelectingPaths             // the verbatim F019 route trace (changed path + matched glob)
f.Alternative                         // CheaperLocalAlternative { Id = GateId "build:unit"; Cost = Cheap; ... }
```

Expected: one finding for the high-cost gate, its F019 trace carried verbatim, and the cheapest same-domain
locally-runnable gate offered as the alternative (or `NoCheaperLocalAlternative` when none qualifies).

## Test

```sh
dotnet test tests/FS.GG.Governance.RouteExplain.Tests/FS.GG.Governance.RouteExplain.Tests.fsproj
```

Covers (see [contracts/route-explain-api.md](./contracts/route-explain-api.md) laws L1–L9):

- **HighCostFindingTests** — one finding per high-cost gate, none below threshold, every `Cost` tier;
  trace/domain/cost carried verbatim (SC-001/SC-002).
- **AlternativeTests** — named alternative is same-domain ∧ strictly cheaper ∧ local; each failing condition
  ⇒ `NoCheaperLocalAlternative`; deterministic cheapest-then-`GateId` tie-break (SC-003/SC-004).
- **DeterminismTests** — `explain` twice equal; selected-gate / registry-gate / selecting-path order & dup
  invariance (SC-005).
- **EmptyRouteTests** — empty route / no high-cost gate ⇒ empty explanation (FR-011).
- **PurityTests** — explanations identical across changed cwd/time/filesystem (SC-006).
- **SurfaceDriftTests** — public surface equals the committed baseline; assembly references only
  Route/Gates/Routing/Findings/Config/BCL/FSharp.Core (SC-007).

Test inputs are **real** `RouteResult`/`GateRegistry` values built through the genuine F014→F019 chain
(`Support.fs` reuses the F019/F020 `facts`/`registryOf`/`selectOf` builders), plus hand-built disordered/
duplicate values where the chain would not naturally produce them (Principle V — no mocks).

## Re-bless the surface baseline (intentional surface change only)

```sh
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.RouteExplain.Tests/FS.GG.Governance.RouteExplain.Tests.fsproj
```

Rewrites `surface/FS.GG.Governance.RouteExplain.surface.txt`. Review the diff; an unexpected change is a
Tier-1 surface drift to justify, not to rubber-stamp.

## Done when

- `dotnet build` and `dotnet test` are green for the new projects and unchanged for all existing ones.
- The surface baseline is committed and the `SurfaceDrift` test passes without `BLESS_SURFACE`.
- No existing `src/`, `surface/`, or merged test project changed (FR-009/SC-007).
</content>
