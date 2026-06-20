# Quickstart: Deterministic gates.json Projection (F021)

A validation/run guide for the gates.json projection — the pure, total
`GatesJson.ofGateRegistry : GateRegistry -> string`. It shows how to exercise the feature end-to-end and
maps each acceptance scenario / success criterion to the test that proves it. Implementation bodies live
in `GatesJson.fs` and the test files (authored in the implement phase); this guide is how you *run and
verify* the feature, not its source.

## Prerequisites

- .NET SDK with `net10.0` (from `Directory.Build.props`).
- The solution builds: `dotnet build FS.GG.Governance.sln`.
- This row adds `src/FS.GG.Governance.GatesJson` and `tests/FS.GG.Governance.GatesJson.Tests` to the
  solution. No new third-party package — serialization is the BCL `System.Text.Json`.

## Run the tests

```bash
# the F021 suite only
dotnet test tests/FS.GG.Governance.GatesJson.Tests/FS.GG.Governance.GatesJson.Tests.fsproj

# the whole solution (confirms no regression in the upstream rows)
dotnet test FS.GG.Governance.sln
```

Expected: the F021 project is green, and the full solution stays green (no existing project's surface
changed).

## Exercise it in FSI (the design-first transcript)

`scripts/prelude.fsx` is extended with an F021 sketch that assembles the **real** F018 registry and
prints the projected document. After `dotnet build`:

```bash
dotnet fsi scripts/prelude.fsx
```

Expected output (shape; literal values come from the real `buildRegistry`):

- `[F21] schemaVersion = fsgg.gates/v1`
- `[F21] document (<n> bytes):` followed by the compact `gates.json` — one `gates` entry per declared
  gate, in `GateId` order, each with `id`/`domain`/`description`/`cost`/`timeout`/`owner`/`maturity`/
  `productCheck`/`prerequisites`/`freshnessKey`.
- `[F21] deterministic? true` — projecting the same registry twice is byte-identical.
- `[F21] empty registry → { "schemaVersion": "fsgg.gates/v1", "gates": [] }` — a valid success.

Minimal shape of the sketch (assemble the real registry, then project):

```fsharp
open FS.GG.Governance.Gates
open FS.GG.Governance.GatesJson

let f21Registry = Gates.buildRegistry f18Facts   // the real F014→F018 facts from earlier in the prelude
let f21Json = GatesJson.ofGateRegistry f21Registry
printfn "[F21] %s" f21Json
```

## What this feature does NOT do (verify by absence)

- It does **not** read git, parse `.fsgg`, assemble the registry, select gates for a change, or write a
  file — persisting to `.fsgg/gates.json` is the later `fsgg` CLI edge.
- The document carries **no** severity, profile, mode, enforcement, cache-eligibility verdict,
  per-change selection, `selectingPaths`, route trace, `findings`, cost rollup, ship verdict, raw YAML,
  host path, timestamp, or environment value (the exclusion sweep test asserts this).

## Acceptance scenario → evidence map

| Spec item | Proven by (test file · case) |
|---|---|
| US1 §1 — every gate by id + carried metadata, none invented | `ProjectionTests` · all-gates-present + metadata-verbatim (SC-001) |
| US1 §2 — prerequisites recorded; none → present empty list | `ProjectionTests` · prerequisites-carried + empty-prereq-list (FR-004) |
| US1 §3 / US4 §1 — empty registry → valid empty `gates` document | `ProjectionTests` / `TotalityTests` · empty-registry (FR-009, SC-006) |
| US2 §1 — same registry projected twice is byte-identical | `DeterminismTests` · twice-identical (FsCheck) (SC-002) |
| US2 §2 — registries equal as values from differently-ordered checks → identical | `DeterminismTests` · permutation-invariance (SC-003) |
| US2 §3 — schema version present + stable field order | `DeterminismTests` · schemaVersion + top-level/gate field-order (FR-013) |
| US2 §4 / US3 §4 — no timestamp/host path/raw YAML/severity/enforcement/selection/verdict | `DeterminismTests` / `CarryTests` · exclusion sweep + positive allowlist (SC-007, FR-011/FR-012) |
| US3 §1 — freshness-key inputs present, no cache verdict | `CarryTests` · freshnessKey-5-inputs (SC-004, FR-014) |
| US3 §2 — optional command present when `Some`, explicit `null` when `None` | `CarryTests` · command-string-vs-null (SC-004, FR-014 §2) |
| US3 §3 — product-check flag carried verbatim | `CarryTests` · productCheck-carried (FR-014 §3) |
| US4 §2 — mixed present/absent prerequisites & optional commands render per-gate, no leak | `TotalityTests` / `ProjectionTests` · mixed-shape gates |
| US4 §3 — totality: returns a document, never throws, over any well-typed registry | `TotalityTests` · FsCheck totality (SC-006) |
| SC-005 — cost/timeout/owner/maturity/productCheck verbatim, no enforcement/weighted-cost translation | `ProjectionTests` / `CarryTests` · token + verbatim assertions |
| free-text description with JSON-significant chars round-trips | `CarryTests` · JSON-special description round-trip (edge case, FR-012) |
| domain id containing the `:` separator renders the `GateId` verbatim | `ProjectionTests` · separator-in-domain (edge case, FR-008/FR-010) |
| Principle II — public surface = exactly `GatesJson` (`ofGateRegistry` + `schemaVersion`) | `SurfaceDriftTests` · baseline drift + nothing-private |
| Engineering Constraints — one-way `GatesJson → Gates → Config`, no new package | `SurfaceDriftTests` · transitive dependency assertion |
