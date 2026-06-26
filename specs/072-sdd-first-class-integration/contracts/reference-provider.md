# Contract: SDD Reference Template Provider (worked example over the 071 seam)

**Feature**: `072-sdd-first-class-integration` · **Conforms to**:
`fsgg.template-provider/v1` → `ProviderContractVersion { Major = 1; Minor = 0 }`
([071 provider-contract](../../071-runtime-project-templates/contracts/provider-contract.md)).

This document specifies the **concrete** reference provider and the worked example built on
it. It adds **no** obligation to the generic seam; it is one conforming instance of 071's
contract plus the example/golden/tutorials that exercise it. The generic core's public
surface is **unchanged** (SC-006).

## R1. The reference provider value

```fsharp
namespace FS.GG.Governance.Sample.SddReferenceProvider

module SddReferenceProvider =
    val providerId : Model.ProviderId          // ProviderId "fsgg.sample.sdd-reference"
    val provider   : Model.TemplateProvider    // ContractVersion { Major = 1; Minor = 0 }; pure Emit
```

**Obligations honored** (071 C1):
1. **Describe, do not write.** `Emit` returns a `ProviderEmission` (target-relative paths +
   literal contents); it touches no filesystem/network/env/clock and never throws.
2. **Stay in-bounds.** Every `RelativePath` is relative, `..`-free, not rooted.
3. **Own only runtime files.** No emitted path falls under the request's `ReservedPaths`
   (the lifecycle layer).
4. **Fail cleanly.** A deliberately-broken clone returns `ProviderError`, never throws (used
   only in failure-path tests).
5. **Deterministic.** Identical request ⇒ identical `Files` in identical order (research D6).

## R2. The emitted runtime skeleton (FR-001)

`Emit` describes exactly these target-relative files, where `<App>` = the target
directory's leaf name:

```
<App>.sln                              # package/manifest — the documented build unit
src/<App>/<App>.fsproj                 # source project   (net10.0, FSharp.Core only)
src/<App>/Program.fs                   # entry point      ([<EntryPoint>] returning 0)
tests/<App>.Tests/<App>.Tests.fsproj   # test project     (refs the source project + FSharp.Core)
tests/<App>.Tests/Tests.fs             # buildable trivial test body
README.md                              # generated-path list + the documented build command
```

**Dependency closure**: FSharp.Core only (SDK-bundled) — so `dotnet build <App>.sln`
succeeds first-attempt, offline, on 100% of runs (FR-004, SC-002, research D2).
**Documented toolchain**: `dotnet build <App>.sln`.

## R3. The layered worked example (FR-003)

The automated end-to-end check, against a **real** temp directory:

1. **Lifecycle precondition.** Seed a disclosed minimal lifecycle-layer stand-in (a few
   `.fsgg/`/`work/`/`readiness/` paths) and pass them as `ScaffoldRequest.ReservedPaths`
   (research D4). In production this layer is authored by sibling-owned `fsgg-sdd init`.
2. **Runtime layer via the seam.** `Interpreter.run (Interpreter.realPorts target) { Request = req; Provider = Some provider }`.
3. **Assert** the terminal model: `Outcome = Scaffolded`; the §R2 files exist under `target`;
   each is `ProviderOwned` in the manifest; `Collisions = []`.
4. **Build.** `dotnet build <App>.sln` exits 0 with no hand-editing (FR-004). SDK missing ⇒
   **named skip**, not failure (research D3, Principle VI).
5. **Golden.** `ScaffoldManifestJson.ofManifest manifest` equals
   `fixtures/sdd-reference/scaffold-manifest.golden.json` **byte-for-byte** (FR-008, SC-005).
6. **Determinism.** A second run over a fresh empty target yields the byte-identical golden
   (SC-003).

## R4. Failure-path examples (documented, asserted)

| Case | Setup | Expected (071 surface) |
|---|---|---|
| Contract mismatch (FR-011) | clone the provider at `{ Major = 2 }` | `Refused (ContractMismatch …)`, **no** files written, actionable diagnostic |
| Collision | seed a target file at an emitted path | `Refused (Collision [..])`, nothing written |
| No provider (FR-010, SC-007) | `Provider = None` | `Done(NoProvider)`, zero effects, no manifest write — today's behavior, zero diff |

## R5. Identical treatment & ownership boundary (FR-012, 071 C3/C5)

- The seam applies the **same** safety/recording/reporting to the reference provider, a
  cloned provider, and any third-party provider — delegation differs **only** in emitted
  files. No provider-specific branch exists anywhere in the tool.
- Emitted runtime files are **provider-owned**; the lifecycle/seam tooling owns only
  delegation, safety, recording, and reporting. SDD-flavored template content lives **only**
  in `samples/…SddReferenceProvider`, never in the generic core (FR-002, SC-006).

## R6. Surface posture (SC-006)

- The reference provider has its **own** additive baseline
  `surface/FS.GG.Governance.Sample.SddReferenceProvider.surface.txt` + drift test.
- The drift test additionally asserts the two **core** baselines
  (`FS.GG.Governance.Scaffold.surface.txt`,
  `FS.GG.Governance.ScaffoldManifestJson.surface.txt`) are **byte-identical** to their
  committed form — the SC-006 no-delta guard.
- The sample is `IsPackable=false` and lives under `samples/` (research D1).

## R7. Boundary disclaimer (FR-013)

Production wiring of the seam into `fsgg-sdd init` is owned by the sibling `FS.GG.SDD` repo.
This repository ships a **reference demonstration** and tutorials, not a change to
`fsgg-sdd init`. Tutorials state this plainly.
