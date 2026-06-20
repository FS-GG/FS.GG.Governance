# Quickstart: Deterministic route.json Projection (F020)

A runnable validation guide for `RouteJson.ofRouteResult`. It proves the feature end-to-end over a
**real** F015→F017→F018→F019 `RouteResult` (research D7), inspecting the **emitted bytes**. Shapes and
tokens are fixed in [`contracts/route-json-document.md`](./contracts/route-json-document.md); the public
surface is [`contracts/RouteJson.fsi`](./contracts/RouteJson.fsi).

## Prerequisites

- .NET SDK with `net10.0` (per `Directory.Build.props`).
- The new `FS.GG.Governance.RouteJson` project + its test project added to `FS.GG.Governance.sln`
  (one `ProjectReference` → `FS.GG.Governance.Route`; no new package — `System.Text.Json` is in the
  shared framework).

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test  tests/FS.GG.Governance.RouteJson.Tests/FS.GG.Governance.RouteJson.Tests.fsproj
# Full suite (all rows green):
dotnet test FS.GG.Governance.sln
```

Regenerate the surface baseline intentionally (after a reviewed surface change):

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.RouteJson.Tests/FS.GG.Governance.RouteJson.Tests.fsproj
```

## FSI smoke (the real chain, then the projection)

`scripts/prelude.fsx` already builds the F019 fixture `f19Result` from the genuine chain. The F020
sketch projects it:

```fsharp
// ... after f19Result is built (F015 -> F017 -> F018 -> F019) ...
open FS.GG.Governance.RouteJson

let f20Doc = RouteJson.ofRouteResult f19Result
printfn "[F20] schemaVersion = %s" RouteJson.schemaVersion
printfn "[F20] route.json (%d bytes):\n%s" f20Doc.Length f20Doc

// Determinism: identical input -> byte-identical document.
printfn "[F20] deterministic? %b" (RouteJson.ofRouteResult f19Result = f20Doc)

// Empty route -> valid document with empty sections + all-zero cost (a success).
let f20Empty =
    RouteJson.ofRouteResult (Route.select f19Registry (Routing.route f19Facts []) (Findings.findUnknownGovernedPaths f19Facts (Routing.route f19Facts [])))
printfn "[F20] empty-route document:\n%s" f20Empty
```

Run it:

```bash
dotnet fsi scripts/prelude.fsx
```

## Acceptance → evidence map

| Spec item | Validation | Test file |
|---|---|---|
| US1 / SC-001 — every selected gate present with id, carried metadata, route trace; no non-selected gate | Parse the document; assert one `selectedGates[*]` per `result.SelectedGates` with matching `id`/`domain`/`cost`/`timeout`/`owner`/`maturity`/`productCheck`/`prerequisites`, each carrying its `selectingPaths` (`path` + `matchedGlob`); assert no extra gate id appears. | `ProjectionTests.fs` |
| US1 sc2 — one gate, many selecting paths, appears once | A gate reached by ≥2 paths: assert a single `selectedGates` entry with all paths in `selectingPaths`. | `ProjectionTests.fs` |
| US2 / SC-002 — byte-for-byte identical on repeat | `ofRouteResult r = ofRouteResult r` (FsCheck twice-identical + a fixed-fixture equality). | `DeterminismTests.fs` |
| US2 / SC-003 — order-independent | Project two `RouteResult`s from permuted upstream inputs (candidate paths + registry gates); assert identical strings. | `DeterminismTests.fs` |
| US2 sc3 — schema version + stable field order | Assert `schemaVersion` field equals `RouteJson.schemaVersion`; assert top-level field order `schemaVersion`,`selectedGates`,`findings`,`cost`. | `DeterminismTests.fs` |
| US2 sc4 / SC-007 — exclusion sweep | Assert the emitted text contains none of: `severity`, `profile`, `mode`, `enforcement`, `cacheEligib`, ship `verdict`, `blockers`, `warnings`, `exitCode`, `expectedArtifacts`, a host/absolute path, a timestamp. | `DeterminismTests.fs` |
| US3 sc1 / SC-004 — findings carried unchanged, in F017 order | Non-empty `FindingReport`: assert `findings[*]` matches `result.Findings.Findings` one-to-one (`id`/`path`/`zone`/`message`), same order. | `CarryTests.fs` |
| US3 sc2 — empty findings present-and-empty | Empty `FindingReport`: assert `findings` is `[]`, never omitted. | `CarryTests.fs` |
| US3 sc3 / FR-014 — freshness-key inputs present, no cache verdict | Assert each gate's `freshnessKey` carries `check`/`domain`/`cost`/`environment`/`command`; assert no cache-eligibility field anywhere. | `CarryTests.fs` |
| US4 sc1 / SC-006 — empty route is a valid document | `ofRouteResult` over the empty route: assert `selectedGates`/`findings` empty + all-zero `cost`; never throws. | `TotalityTests.fs` |
| US4 sc2 — findings-only route | Empty `selectedGates` + non-empty `findings` coexist. | `TotalityTests.fs` |
| US4 sc3 / SC-006 — total, never throws | FsCheck: `ofRouteResult` returns a string for every generated well-typed `RouteResult`. | `TotalityTests.fs` |
| FR-006 / SC-005 — every cost tier present incl. zero | Assert `cost` always has `cheap`/`medium`/`high`/`exhaustive` integer fields; no summed scalar. | `ProjectionTests.fs` |
| Principle II — surface baseline + dependency hygiene | Surface-drift vs `surface/FS.GG.Governance.RouteJson.surface.txt`; "exactly the `RouteJson` module, nothing private"; one-way dependency (`RouteJson → Route`). | `SurfaceDriftTests.fs` |

## Out of scope (do not test for here)

Round-trip parse (`toRouteResult`), severity/enforcement, cache-eligibility verdict, ship verdict /
blockers / exit code, file writes to `readiness/<id>/route.json`, and any `fsgg route`/`fsgg ship` CLI
behavior — later Phase-2 / Phase-5 / Phase-11 rows (research D6).
