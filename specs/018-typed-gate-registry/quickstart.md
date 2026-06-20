# Quickstart / Validation Guide: Typed Gate Registry (F018)

Validates the F018 acceptance scenarios and success criteria against the **public** surface of
`FS.GG.Governance.Gates`, the way a downstream caller (the later `route`/`ship` commands and the
`gates.json` emitter) will use it: take validated F014 `TypedFacts`, assemble the gate registry.
Pure — no git, no filesystem, no network.

## Prerequisites

- .NET SDK with `net10.0` (per `Directory.Build.props`).
- Build the solution: `dotnet build FS.GG.Governance.sln`.
- The feature consumes already-typed, already-validated F014 `TypedFacts`. No `.fsgg` files and **no
  installed FS.GG package** are required in any inspected repo (FR-016, SC-007).

## Run the tests

```bash
dotnet test tests/FS.GG.Governance.Gates.Tests/FS.GG.Governance.Gates.Tests.fsproj
```

Expected: all green. The suite maps one-to-one to the user stories:

| Test file | Covers | Asserts |
|---|---|---|
| `GateBuildTests.fs` | US1 (SC-001) | N checks ⇒ N gates; each gate's id (`domain:checkId`), domain, cost, owner, maturity, timeout, description match the declared check; twice-identical ids |
| `RegistryInvariantTests.fs` | US2 (SC-002) | FsCheck over arbitrary valid facts: gate ids distinct, gate count = check count, every `RequiresCommand` resolves, assembly never throws / never partial |
| `DeterminismTests.fs` | US3, US5 (SC-003, SC-006) | compute twice ⇒ byte-identical; FsCheck permutation of checks/commands ⇒ unchanged `GateId`-ordered list; fields use only declared ids |
| `MetadataTests.fs` | US4 (SC-004, SC-005) | `Release`-env check ⇒ `productCheck = true`, `Local`/`Ci` ⇒ `false`; every gate carries a non-empty declared-input freshness key; command timeout when present, `defaultTimeout` when absent |
| `SurfaceDriftTests.fs` | Principle II | public surface matches `surface/FS.GG.Governance.Gates.surface.txt` |

No `Synthetic`-disclosed test is anticipated: every case is reachable from real `Valid TypedFacts`
(a direct dividend of research D4 — no never-triggered diagnostic layer to exercise).

## FSI smoke check (Principle I)

`scripts/prelude.fsx` exercises the packed surface end-to-end. Representative shape:

```fsharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates

// Validated facts declaring two domains and three checks; one check ("tests") references a declared
// tooling command "dotnet-test" (timeout 600s) and runs in the Release environment; the others have
// no command and run Local. (Built in-memory; see Support.fs in tests.)
let facts : TypedFacts = (* … fixture … *)

let registry = Gates.buildRegistry facts
registry.Gates
|> List.iter (fun g ->
    printfn "[F18] %s  cost=%A  timeout=%A  product=%b  prereqs=%A"
        (Model.gateIdValue g.Id) g.Cost g.Timeout g.ProductCheck g.Prerequisites)
```

Expected: three gates, in `GateId` ordinal order. The `build:tests` gate carries
`Timeout = TimeoutLimit 600` (from its command), `ProductCheck = true` (Release env),
`Prerequisites = [RequiresCommand (CommandId "dotnet-test")]`, and a `FreshnessKey` naming its check/
domain/cost/environment/command. The command-less gates carry `Timeout = Gates.defaultTimeout`
(`TimeoutLimit 300`), `ProductCheck = false`, and `Prerequisites = []`.

## Acceptance → evidence map

| Spec item | Evidence |
|---|---|
| SC-001 | `GateBuildTests` — N gates with the full metadata field set; twice-identical ids |
| SC-002 | `RegistryInvariantTests` — distinct ids, count parity, prerequisites resolve, total assembly |
| SC-003 | `DeterminismTests` — twice-identical + reorder-invariant |
| SC-004 | `MetadataTests` — product-check split by environment; declared-input freshness key; no clock |
| SC-005 | `MetadataTests` — command timeout vs `defaultTimeout`; always bounded |
| SC-006 | `DeterminismTests` — `GateId` ordinal order, stable and reorder-invariant |
| SC-007 | whole suite runs pure with no git/network/installed package; no JSON/CLI |

See [`data-model.md`](./data-model.md) for the type shapes and [`research.md`](./research.md) (D4–D6)
for the maintainer-confirmed scope reconciliations. Implementation bodies and full fixtures live in
`tasks.md` / the implementation phase, not here.
