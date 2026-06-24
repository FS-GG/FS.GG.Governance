# Quickstart: Release-Facts Sensing

**Feature**: `054-release-facts-sensing` | Validates the spec's user stories against the packed
`FS.GG.Governance.ReleaseFactsSensing` library and its hand-off to the F053 release-gate core.

This is a **validation/run guide**, not the implementation. The contracts live in
[`contracts/`](./contracts/); the entities in [`data-model.md`](./data-model.md); the decisions in
[`research.md`](./research.md).

## Prerequisites

- .NET `net10.0` SDK (repo standard; `Directory.Build.props` sets `Nullable=enable`,
  `TreatWarningsAsErrors=true`, `WarnOn=3390;1182`).
- The merged thread builds: F014 `Config`, F053 `ReleaseRules` (and its deps) pack/build cleanly.

## Build

```bash
dotnet build src/FS.GG.Governance.ReleaseFactsSensing/FS.GG.Governance.ReleaseFactsSensing.fsproj
```

## Exercise in FSI (Principle I — the honest audience)

`scripts/prelude.fsx` gains an **F054** section. It loads the packed library and drives the **public
surface** — `Interpreter.senseRelease` over a fake/real port, then hands `sensed.Facts` straight to the
F053 `Release.evaluate` to prove the output is exactly the F053 input shape (SC-001):

```fsharp
#r ".../FS.GG.Governance.ReleaseFactsSensing.dll"
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model

// Caller-supplied, product-neutral expectations (no hardcoded id/path — FR-011).
let f54Exp =
    { Surface = SurfaceId "pkg"
      VersionBaseline = Some "1.2.0"
      RequiredMetadataFields = Some [ "authors"; "license" ]
      ExpectedPins = Some (Map [ "base", "9.0.0" ])
      RequiredPublishPosture = Some [ "plan-present" ]
      RequiredTrustedPublishing = Some [ "oidc" ]
      RequiredProvenance = Some [ "attestation" ] }

// A fake port whose six families all SATISFY the expectations (US1.1).
let f54MetPort: Interpreter.RepositoryPort =
    { ReadVersion = fun () -> Ok { Declared = "1.3.0" }
      ReadMetadata = fun () -> Ok { PresentFields = [ "authors"; "license" ] }
      ReadPins = fun () -> Ok { Resolved = Map [ "base", "9.0.0" ] }
      ReadPublishPlan = fun () -> Ok { Observed = [ "plan-present" ] }
      ReadTrustedPublishing = fun () -> Ok { Observed = [ "oidc" ] }
      ReadProvenance = fun () -> Ok { Observed = [ "attestation" ] } }

let f54Sensed = Interpreter.senseRelease f54MetPort f54Exp
printfn "[F54] exactly six families? %b" (f54Sensed.Facts.States.Count = 6)            // expect true
printfn "[F54] all Met? %b" (f54Sensed.Facts.States |> Map.forall (fun _ s -> s = Met)) // expect true

// (US1.3 / SC-001) The sensed Facts feed the F053 core with NO adaptation.
let f54Rules =
    Sensing.releaseFamilies
    |> List.map (fun k -> { Kind = k; Surface = SurfaceId "pkg"; BaseSeverity = Blocking; Maturity = BlockOnRelease })
let f54Findings = Release.evaluate f54Rules f54Sensed.Facts            // type-checks: Facts IS ReleaseFacts
printfn "[F54] one finding per family? %b" (f54Findings.Length = 6)   // expect true

// (US1.2) Version NOT bumped past baseline ⇒ Unmet, others Met.
let f54StalePort = { f54MetPort with ReadVersion = fun () -> Ok { Declared = "1.2.0" } } // equals baseline
let f54Stale = Interpreter.senseRelease f54StalePort f54Exp
printfn "[F54] stale version Unmet? %b" (f54Stale.Facts.States.[VersionBump] = Unmet)   // expect true

// (US2.1) Missing metadata field ⇒ Unmet + snapshot names present/missing.
let f54MissPort = { f54MetPort with ReadMetadata = fun () -> Ok { PresentFields = [ "authors" ] } }
let f54Miss = Interpreter.senseRelease f54MissPort f54Exp
printfn "[F54] metadata Unmet? %b" (f54Miss.Facts.States.[PackageMetadata] = Unmet)     // expect true
printfn "[F54] snapshot names missing field? %b"
    (match f54Miss.Snapshot.Metadata with Some m -> m.Missing = [ "license" ] | None -> false) // expect true

// (US3.1 / SC-002) Absent source ⇒ Unrecoverable (never Met, never a throw).
let f54GonePort = { f54MetPort with ReadProvenance = fun () -> Error "absent: provenance record not found" }
let f54Gone = Interpreter.senseRelease f54GonePort f54Exp
printfn "[F54] absent ⇒ Unrecoverable? %b" (f54Gone.Facts.States.[Provenance] = Unrecoverable) // expect true

// (US3.2 / SC-003) Determinism — two senses are structurally identical (compare equal).
printfn "[F54] deterministic? %b" (Interpreter.senseRelease f54MetPort f54Exp = f54Sensed) // expect true
```

## Validate end-to-end (`dotnet test`)

```bash
dotnet test tests/FS.GG.Governance.ReleaseFactsSensing.Tests/FS.GG.Governance.ReleaseFactsSensing.Tests.fsproj
```

The test project drives the **packed public surface** (not private helpers) and covers:

| Story / Criterion | Test focus |
|-------------------|-----------|
| US1 / SC-001 | `senseRelease` over a fixture ⇒ exactly six families, each correctly classified; `sensed.Facts` accepted by `Release.evaluate` with no adaptation (one finding per declared rule) |
| US1.2 / SC-005 | a not-bumped version ⇒ `Unmet` while the other five are `Met`; an all-violating fixture ⇒ each `Unmet` |
| US2 | snapshot names present/missing metadata fields and resolved-vs-expected pins, matching the `Unmet` families |
| US3 / SC-002 | an evidence-**removed** and an evidence-**corrupted** family both ⇒ `Unrecoverable` (not `Met`, not a crash); all six still returned |
| US3.2 / SC-003 | sensing the identical fixture twice ⇒ structurally identical `SensedRelease` (compare equal), including every collection's order |
| US3.3 / SC-004 | **dependency scope-guard** in `SurfaceDriftTests` asserts the assembly references no `System.Net.Http`/`Octokit`/`LibGit2Sharp` — no network is reachable |
| SC-006 | the output family set equals `Sensing.releaseFamilies` across satisfied/violated/all-unrecoverable fixtures |

**Real fixtures (Principle V).** The interpreter test writes a **real temp fixture repository** (the
F016 Snapshot / FreshnessSensing `withTempDir` precedent): six simple neutral files under a temp dir,
read back through `Interpreter.realPort tempDir layout`. The all-unrecoverable case deletes/corrupts the
files. The pure `Sensing.deriveFacts` is additionally unit-tested with hand-built `RecoveredEvidence`
records (no disk). No network, no registry, no publishing provider is contacted (SC-004).

## Surface baseline

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.ReleaseFactsSensing.Tests/...
```
generates `surface/FS.GG.Governance.ReleaseFactsSensing.surface.txt`; the reflective drift test guards it
thereafter, and the scope guard (above) lives alongside it.
