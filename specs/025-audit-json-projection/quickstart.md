# Quickstart: Deterministic audit.json Projection (F025)

A runnable validation guide for `AuditJson.ofShipDecision`. It proves the feature end-to-end over a
**real** upstream chain — a real F019 `RouteResult` rolled up by the real F024 `Ship.rollup` at a real
mode/profile — inspecting the **emitted bytes** with a read-only `System.Text.Json.JsonDocument`. See
[`contracts/AuditJson.fsi`](./contracts/AuditJson.fsi) for the surface and
[`contracts/audit-json-document.md`](./contracts/audit-json-document.md) for the wire shape.

## Prerequisites

- .NET SDK with `net10.0` (per `Directory.Build.props`).
- The new projects added to `FS.GG.Governance.sln`:
  - `src/FS.GG.Governance.AuditJson` — references **only** `FS.GG.Governance.Ship`. **No new
    third-party `PackageReference`** (serialization is shared-framework `System.Text.Json`).
  - `tests/FS.GG.Governance.AuditJson.Tests` — references `AuditJson`, plus `Ship`, `Route`,
    `Enforcement`, `Config`, `Gates`, `Findings` to assemble the real `ShipDecision` chain.

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test  tests/FS.GG.Governance.AuditJson.Tests/FS.GG.Governance.AuditJson.Tests.fsproj
dotnet test  FS.GG.Governance.sln
```

Regenerate the surface baseline intentionally (after a reviewed surface change):

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.AuditJson.Tests/FS.GG.Governance.AuditJson.Tests.fsproj
```

## FSI smoke (the real chain, then the projection)

`scripts/prelude.fsx` already builds the upstream route/ship fixtures. The AuditJson sketch projects a
rolled-up decision:

```fsharp
open FS.GG.Governance.Ship
open FS.GG.Governance.AuditJson

// `route` is the real F019 RouteResult the prelude assembles; mode/profile are F023 values.
let decision = Ship.rollup route RunMode.Gate Profile.Standard
let json = AuditJson.ofShipDecision decision

printfn "%s" json
printfn "schemaVersion = %s" AuditJson.schemaVersion
// Determinism smoke: identical input ⇒ identical bytes.
printfn "deterministic = %b" (AuditJson.ofShipDecision decision = AuditJson.ofShipDecision decision)
```

Run it:

```bash
dotnet fsi scripts/prelude.fsx
```

## Acceptance → evidence map

| Spec item | Validation | Test file |
|---|---|---|
| US1 / FR-001 / SC-001 | A real rolled-up decision projects to a doc with `verdict`, `exitCodeBasis`, and every blocker/warning/passing item with identity + enforcement detail | `ProjectionTests.fs` |
| US1 AC2 / FR-009 | A `Pass` decision with empty blockers → `verdict:"pass"`, `exitCodeBasis:"clean"`, present empty `blockers` | `ProjectionTests.fs` |
| US1 AC3 / FR-005 / SC-001 | Warnings + passing render in their own sections; no item in two sections | `ProjectionTests.fs` |
| FR-002 / FR-003 / SC-004 | `verdict` / `exitCodeBasis` echoed verbatim, never recomputed; no numeric exit code | `ProjectionTests.fs` |
| US2 AC1 / FR-007 / SC-002 | Same decision projected twice ⇒ byte-for-byte identical | `DeterminismTests.fs` |
| US2 AC2 / SC-003 | Two value-equal decisions from differently-ordered route inputs ⇒ identical bytes (fixed + property) | `DeterminismTests.fs` |
| US2 AC3 / FR-013 | `schemaVersion` present and equal to `"fsgg.audit/v1"`; fixed top-level field order | `DeterminismTests.fs` |
| US2 AC4 / FR-012 / SC-007 | Excluded-token sweep: no `exitCode` number, provenance, cache verdict, timestamp, host path, raw YAML, env value | `DeterminismTests.fs` |
| US3 AC1 / FR-011 | A relaxed base-`Blocking` warning shows `baseSeverity:"blocking"` **and** `effectiveSeverity:"advisory"` + mode/profile/maturity/reason | `CarryTests.fs` |
| US3 AC2 / FR-006 / SC-005 | Every item carries all six `enforcement` fields verbatim from its F023 decision | `CarryTests.fs` |
| US3 AC3 / FR-004 / FR-010 | Gate identity = declared `GateId`; finding identity = `findingIdToken` + governed `path`; same id on two paths ⇒ distinct entries; separator-in-id rendered verbatim | `CarryTests.fs` |
| US4 AC1 / FR-009 / SC-006 | Empty/clean decision ⇒ valid doc, three present empty arrays, pass/clean | `TotalityTests.fs` |
| US4 AC2/AC3 / FR-008 | Blockers-only / warnings-only / all-populated; property: never throws over generated decisions | `TotalityTests.fs` |
| Principle II / Tier 1 | Public surface equals the committed baseline; only the `AuditJson` module is exported; references only Ship/Enforcement/Route/Config/Gates/Findings/BCL/FSharp.Core | `SurfaceDriftTests.fs` |

## Out of scope (do not test for here)

- The `fsgg ship` CLI command, its stdout/exit-code wiring, and the numeric process exit code (later row).
- Provenance/attestation references and artifact digests (Release phase — the `ShipDecision` carries none).
- Cache-eligibility / freshness evaluation (Phase 11).
- Re-deriving the verdict, re-partitioning items, or re-sorting sections (F024's responsibility).
- Reading `.fsgg`, git, or any filesystem/clock input (the projection is pure and total).
</content>
