# Quickstart: Sensed-Metadata Marking Core (F034)

A validation/run guide for the pure `FS.GG.Governance.SensedMetadata` core. Details live in
[data-model.md](./data-model.md), [contracts/sensed-metadata-api.md](./contracts/sensed-metadata-api.md), and
[contracts/sensed-metadata-format.md](./contracts/sensed-metadata-format.md). No host, no CLI, no clock — every
input is a literally-constructible measured value.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- The merged sibling core `FS.GG.Governance.CommandRecord` (provides F032's `SensedDuration`, reused verbatim).
- No new third-party package is added (FR-011).

## Build

```bash
dotnet build src/FS.GG.Governance.SensedMetadata/FS.GG.Governance.SensedMetadata.fsproj
```

## FSI-exercise (Principle I — design-first proof)

A short F034 section in `scripts/prelude.fsx` exercises the public surface before any `.fs` body is trusted:

```fsharp
open FS.GG.Governance.CommandRecord.Model        // SensedDuration
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Model

// Mark an already-measured duration and an already-measured timestamp.
let dM = SensedMetadata.markDuration  (SensedLabel "elapsed") (SensedDuration 1_830_000_000L)
let tM = SensedMetadata.markTimestamp (SensedLabel "at")      (SensedTimestamp "2026-06-21T12:00:00Z")

// Kinds are readable; the values are sensed by construction.
SensedMetadata.kindOf dM          // DurationKind
SensedMetadata.kindOf tM          // TimestampKind

// Flagged renderings — note the reserved !sensed! marker.
SensedMetadata.render dM |> SensedMetadata.renderingValue
// "!sensed!=duration;7:elapsed;10:1830000000"
SensedMetadata.render tM |> SensedMetadata.renderingValue
// "!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z"

// One clearly-marked, separable section (order-preserving).
SensedMetadata.renderSection [ tM; dM ] |> SensedMetadata.renderingValue
// "!sensed-section!=2;47:!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z;41:!sensed!=duration;7:elapsed;10:1830000000"

// Empty section is an ordinary value, not an error.
SensedMetadata.renderSection [] |> SensedMetadata.renderingValue   // "!sensed-section!=0;"
```

## Test

```bash
dotnet test tests/FS.GG.Governance.SensedMetadata.Tests/FS.GG.Governance.SensedMetadata.Tests.fsproj
```

Validates, against the **public** surface with **real** literal values (Principle V — no mocks, no clock, no
process):

- **Marking (SC-001):** kind/label/value carriage and sensed-by-construction, incl. zero-length duration, empty
  label, same-label/different-kind.
- **Rendering (SC-002):** the `!sensed!` marker is present and distinguishable from a reproducible field;
  unspoofable by data (values containing `!sensed!`/`;`/`:`/`=`); byte-exact worked example pinned to
  `contracts/sensed-metadata-format.md`.
- **Section (SC-004):** one separable `!sensed-section!`, order-preserving, empty-list included.
- **Determinism (SC-004):** marking + rendering the same value twice is byte-equal (FsCheck).
- **Purity (SC-005):** identical results under changed cwd / time / filesystem (FsCheck).
- **Identity-neutrality (SC-003):** a report's reproducible bytes are unchanged regardless of its sensed section.
- **Surface drift + scope (SC-006):** the public surface equals the committed baseline and the assembly references
  only `CommandRecord` / `Config` / BCL / `FSharp.Core`.

## Re-bless the surface baseline (intentional public-surface change only)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.SensedMetadata.Tests/FS.GG.Governance.SensedMetadata.Tests.fsproj
```

Rewrites `surface/FS.GG.Governance.SensedMetadata.surface.txt`. Only run this when a public-surface change is
intended (Tier-1 discipline, Principle II).

## Expected outcomes

- `dotnet build` and `dotnet test` over the **existing** projects (incl. F032) are unchanged (SC-006); the new
  project + test project are purely additive.
- Every marked value is sensed by construction; every rendering carries the explicit marker and is unspoofable;
  the same input always yields byte-identical results.
