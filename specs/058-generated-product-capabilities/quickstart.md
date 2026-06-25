# Quickstart: Generated-Product Capabilities (F23)

Runnable validation scenarios proving a generated product can **declare**, **route**, **classify**, and
**cost-tier** its product surfaces, run standalone, and migrate safely. Details live in
[data-model.md](./data-model.md) and [contracts/](./contracts/); this is the run/validate guide.

## Prerequisites

- .NET SDK `net10.0`; repo restores with `dotnet restore FS.GG.Governance.sln`.
- Build (warnings-as-errors): `dotnet build FS.GG.Governance.sln`.
- Test runner: Expecto. Run a project with `dotnet run --project tests/<Project>`.

## Build & full suite

```bash
dotnet build FS.GG.Governance.sln
dotnet run --project tests/FS.GG.Governance.Config.Tests
dotnet run --project tests/FS.GG.Governance.ProductSurfaces.Tests
dotnet run --project tests/FS.GG.Governance.Findings.Tests
dotnet run --project tests/FS.GG.Governance.RouteJson.Tests
dotnet run --project tests/FS.GG.Governance.RouteCommand.Tests
```

Expected: clean build; all suites green; updated surface baselines match
(`surface/FS.GG.Governance.Config.surface.txt`, `…ProductSurfaces.surface.txt`, `…RouteJson.surface.txt`).

## Scenario 1 — declare every new surface kind; route + classify (US1, SC-001)

Fixture: a v2 `capabilities.yml` declaring one surface of every new kind (package, docs, skill, design,
sampleApp, generatedProduct) plus a baseline and a template profile.

- Change a file under each surface in turn ⇒ `ProductSurfaces.classify` returns a `ProductClassification`
  with the **expected `Class`** and matched `Capability` for each (data-model §3).
- **Verify**: one classification per surface kind; the classification of an overlapping path follows the
  documented precedence (contracts/classification.md).

## Scenario 2 — no-hide safety: new protected surface under a governed root (US1, SC-002)

- Add a new public-API path under a `generatedProduct`/`package`/`release` boundary with **no** path-map
  glob ⇒ `Findings.findUnknownGovernedPaths` emits `UnknownProtectedBoundaryPath`
  (`Zone = ProtectedBoundaryUnknown sid`) — **never** routine, never a silent pass.
- A routine/unmanaged path matching no surface ⇒ **no** finding and **no** product classification
  (light-by-default, FR-004).

## Scenario 3 — cost-tier selection (US2, SC-003)

- Low-stakes change (`docs/*`) ⇒ `SelectedTier = StructuralScan`; no deeper tier pulled in.
- Release-surface change under a release profile ⇒ `SelectedTier = ReleaseValidation`; explanation names the
  tier and the reason.
- A path under two surfaces ⇒ deterministic precedence selects class + tier (order-independent on repeated
  runs and on re-ordered declarations).
- Each classification's `Explanation` names the matched capability, the surface class, the selected tier,
  and (when known) a cheaper local alternative.

## Scenario 4 — standalone, no monorepo (US3, SC-004)

- Check out the generated-product fixture **in isolation** (no monorepo paths). Run
  `Config.loadAndValidate` + `Routing.route` + `ProductSurfaces.classify` ⇒ routing + classification
  complete using only the product's own declared sources.
- A declared surface path that escapes the governed root (a monorepo-only `..` reference) ⇒
  `Invalid [PathEscapesRoot]` naming the offending surface/field (FR-010) — not a fabricated success.
- The product carries no build/run-time dependency on the tool: removing the tool leaves the fixture
  byte-for-byte unchanged and buildable (verified by the scope guard — no tool reference in the fixture).

## Scenario 5 — versioned schema + migration (US4, SC-005/SC-006)

- `capabilities.yml` at `schemaVersion: 1` ⇒ `Invalid [UnsupportedSchemaVersion]`, message naming version
  `1` and pointing at `contracts/migration.md` (fixture `migration-v1-capabilities`).
- v2 catalog with a duplicate surface/check/domain id ⇒ `DuplicateId` (both locations named).
- v2 catalog with an unknown field ⇒ `UnknownField` naming the field.
- v2 catalog at version `3` ⇒ `UnsupportedSchemaVersion` naming the version.
- Two loads of the same valid v2 catalog ⇒ byte-identical ordering (determinism).

## Scenario 6 — `fsgg route` surfaces classification + tier (US1/US2 via CLI, D8)

```bash
dotnet run --project src/FS.GG.Governance.RouteCommand -- <args> --json
```

- Human output lists each routed product path with its capability · class · selected tier · alternative.
- `route.json` carries `schemaVersion: "fsgg.route/v2"` and a deterministic `productSurfaces` array; the
  printed machine output equals the persisted file byte-for-byte.

## Constitution gates exercised

- **I/II**: each new public module drafted as `.fsi` first; visibility lives in `.fsi`; surface baselines
  updated (Config, ProductSurfaces, RouteJson).
- **III**: plain records/closed DUs/pipelines/exhaustive matches; no new dependency.
- **IV**: `ProductSurfaces.classify`, `Schema.validate`, `Findings` are pure leaves (no MVU ceremony);
  `RouteCommand` keeps its existing MVU boundary, with `classify` called at the edge.
- **V/VI**: real-fixture semantic tests fail-before/pass-after; input vs tool-defect diagnostics preserved;
  no swallowed errors, no fabricated classification.
