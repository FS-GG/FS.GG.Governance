# Implementation Plan: Generated-Product Capabilities (F23)

**Branch**: `058-generated-product-capabilities` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/058-generated-product-capabilities/spec.md`

## Summary

Carry the MVP capability catalog (F014/F016) past routing-only into the **full product-surface vocabulary**.
This row **expands** `.fsgg/capabilities.yml` so a generated product can *declare* the surfaces it owns
(package/API + baselines, generated roots + template profiles, sample apps, docs/examples, skills, design
artifacts, release surfaces, evidence tags), *routes and classifies* changes under each new surface kind,
selects a **cost tier** (structural scan → restore/build → focused tests → full verify → release
validation) for each routed product surface, closes the **"new protected surface hidden under a governed
root"** safety hole, and runs **standalone** without monorepo access — surfaced through the existing
`fsgg route` host. The concrete per-domain deterministic *checks* (baseline drift, FSI transcripts, docs
links, skill path contracts, design tokens) are the **next** feature (F24) and out of scope; a declared
evidence tag without its check is a known, non-error state this row produces (FR-016).

The work composes the existing machinery rather than introducing a parallel router (Assumptions: "routing
reuse, not a new router"):

1. **Extend `FS.GG.Governance.Config`** (Tier-1 schema change) — bump `capabilities.yml` to
   `schemaVersion: 2` (per-file; the other three `.fsgg` files untouched at v1), extend the closed
   `SurfaceClass` DU with six product kinds, add a closed ordered `GeneratedProductTier` DU, add optional
   product attributes (`EvidenceTag`/`TemplateProfile`/`Baseline` on `Surface`; `Tier` on `Check`), and
   extend strict validation to the new fields **reusing the closed diagnostic set** (no new `DiagnosticId`).
2. **Add `FS.GG.Governance.ProductSurfaces`** (new pure library) — `classify : TypedFacts -> RouteReport ->
   ProfileId -> ProductSurfaceReport`: per routed path, the matched capability, surface classification,
   selected cost tier, cheaper-local alternative, and explanation; deterministic precedence on multi-match.
3. **Extend `FS.GG.Governance.Findings`** (behavior only, no public type change) — widen the
   escalating-boundary set to `ProtectedSurface ∪ PackageSurface ∪ ReleaseSurface ∪ GeneratedProductRoot`
   so a new protected surface under a governed root yields `UnknownProtectedBoundaryPath`, never a silent
   pass (FR-003, SC-002).
4. **Wire `fsgg route`** — `RouteCommand` calls `classify` at the edge and renders the classification +
   selected tier; `RouteJson` bumps `schemaVersion` to `"fsgg.route/v2"` and emits a deterministic
   `productSurfaces` array (the standalone entry point, D8).

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **Hard version bump, per-file (D1).** `capabilities.yml` is `schemaVersion: 2`; a v1 (or any non-2)
   `capabilities.yml` is rejected with `UnsupportedSchemaVersion` pointing at `contracts/migration.md`.
   `project.yml`/`policy.yml`/`tooling.yml` stay at v1. `Schema.supportedSchemaVersion: SchemaVersion`
   becomes `Schema.supportedVersionFor: FsggFile -> SchemaVersion`. 24 `capabilities.yml` fixtures migrate.
2. **Expanded vocabulary lives in `Config` + `ProductSurfaces` only (D2–D5).** The kernel stays
   product-neutral: no product identity, surface identity, path, template profile, or generator is
   hardcoded in core (FR-014, SC-007).
3. **CLI wiring this row (D8).** The classification is surfaced through the existing `fsgg route` host;
   no new command. Standalone reuses the F014 `Loader` (already reads only the `.fsgg` parent directory);
   a monorepo-only declared path is the existing `PathEscapesRoot` diagnostic (FR-010).

## Implementation outcome (as-built, 2026-06-25)

Delivered and verified green across the whole solution (51 test assemblies, 1654 tests, 0 failures).
**One deviation from the original D8 route-surfacing design**, decided with the user and reconciled to the
merged codebase:

- **Route surfacing is additive, not a version bump (D8 revised).** The plan/data-model assumed
  `RouteJson` was at `"fsgg.route/v1"` and that this row would bump it to `"fsgg.route/v2"` and change
  `ofRouteResult` to take the `RouteReport`/`ProductSurfaceReport`. In the merged tree, **F045/F052 already
  moved `RouteJson` to `"fsgg.route/v2"`** with `ofRouteResult : RouteResult -> CacheEligibilityReport
  option -> (GateId*GateOutcome) list -> string` (a gate/cache/execution wire contract with goldens).
  Following the plan literally would have broken that contract. Instead — matching the additive F045/F052
  precedent — `ofRouteResult` is left **byte-identical**, and a new
  `ofRouteResultWithProductSurfaces : … -> ProductSurfaceReport -> string` emits an additive
  `productSurfaces` array **only when non-empty** (empty ⇒ byte-identical output, so every existing golden
  is untouched and `schemaVersion` stays `"fsgg.route/v2"`). `RouteCommand` calls `classify` at the edge
  `update` (pure) using the catalog's declared **default profile** (or `standard` when no policy is
  declared), threads the report through `Model.Classifications`, renders a `product surfaces:` summary
  block, and passes it to `ofRouteResultWithProductSurfaces` at the persist effect. T024/T025 are realized
  this way; T018/T019 assert the additive section (RouteJson embed + a real-Interpreter end-to-end over a
  product catalog).

- **Migration blast radius (T006, as-built).** The `capabilities.yml` → v2 change is repo-wide: besides the
  24 Config fixtures, the inline catalog strings in the `RouteCommand`/`ShipCommand`/`VerifyCommand`/
  `CacheEligibilityCommand` test suites carry their own `capabilities.yml` at v1 and were migrated to v2.
  `project.yml`/`policy.yml`/`tooling.yml` stayed at v1 throughout (per-file versioning, D1).

Everything else landed as planned. The full per-task status is in [tasks.md](./tasks.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`).

**Primary Dependencies**: FSharp.Core 10.1.301; YamlDotNet 16.3.0 (already pinned; the F014 `Config`
parse-to-node — extended, **no new package**); `System.Text.Json` (BCL `Utf8JsonWriter`, the existing
`RouteJson` projection — **no new package**). Project references reused verbatim: `Config` (extended),
`Routing` (`route`/`RouteReport`/`Glob.matches`), `Findings` (extended), `RouteExplain` (the
`AlternativeOutcome` shape, mirrored), `RouteJson`/`RouteCommand` (extended). **No new dependency.**

**Storage**: None new — pure validation/classification over the local `.fsgg` catalog and caller-supplied
candidate paths; the only writes are the existing `route.json` artifact. No database, no network, no
registry.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck/FsCheck 2.16.6 (repo standard). Real `.fsgg` fixture
directories: extend `FS.GG.Governance.Config.Tests` (migrate the 24 v1 `capabilities.yml` fixtures to v2;
add product-surface, generated-product-standalone, and v2-malformed fixtures) and `…Findings.Tests`; new
`FS.GG.Governance.ProductSurfaces.Tests`; extend `…RouteJson.Tests` / `…RouteCommand.Tests` (determinism,
golden, productSurfaces section, one real-filesystem CLI end-to-end). FSI semantic tests load the public
surface, not internals (Constitution I).

**Target Platform**: Cross-platform .NET libraries + the existing `fsgg route` CLI executable
(Linux/macOS/Windows); standalone (no monorepo) and monorepo usage.

**Project Type**: Capability-catalog + product-adapter expansion — extend three existing projects, add one
pure library, extend two route-surfacing projects; single-solution F# layout.

**Performance Goals**: Not a hot path. One catalog parse/validate + pure per-path routing/classification/
tier-selection over the caller's candidate-path set; sub-second. No performance-driven mutation.

**Constraints**: Deterministic, byte-identical typed facts, classification report, and `route.json` for
identical input (no timestamps/abs-paths/usernames/order-dependence); printed machine output equals the
persisted `route.json`; strict validation with the **closed** diagnostic set extended to every new field;
input-vs-tool-defect diagnostics preserved (FR-015); product-neutral core (FR-014); standalone with no
monorepo dependency (FR-009); no fabricated classification and no silent pass of a protected surface.

**Scale/Scope**: 1 new `src` library (`ProductSurfaces`) + 1 new test project; 5 extended projects (`Config`,
`Findings`, `RouteJson`, `RouteCommand`, and their tests); ~3 updated surface baselines + 1 new; ~24 fixture
migrations + new fixtures; 1 migration doc. No change to the four-file `.fsgg` schema beyond
`capabilities.yml`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. The new public module (`ProductSurfaces`) and
  every extended public surface (`Config.Model`/`Schema`, `RouteJson`) are drafted as `.fsi` and exercised
  through the packed/loaded public surface before the `.fs` bodies change (the F055–F057 precedent).
  Semantic tests call `classify`, `Schema.validate`, `findUnknownGovernedPaths`, `RouteJson.ofRouteResult`
  — never internals.
- **II. Visibility Lives in `.fsi`** — PASS. New/extended public modules ship curated `.fsi`; `.fs` bodies
  carry no access modifiers. Surface-drift baselines update: `Config` (new DU cases, newtypes, record
  fields, `supportedVersionFor`), `RouteJson` (schemaVersion + signature), and a new `ProductSurfaces`
  baseline. `Findings` has no public type change (baseline unchanged).
- **III. Idiomatic Simplicity** — PASS. Closed DUs, plain records, pipelines, exhaustive matches; no
  SRTP/reflection/type-providers/custom CEs/non-trivial active patterns; no new dependency. Any local
  mutation in the JSON writer follows the disclosed `RouteJson` precedent.
- **IV. Elmish/MVU Is the Boundary** — PASS. `Schema.validate`, `ProductSurfaces.classify`, and `Findings`
  are pure, total leaves — no MVU ceremony needed (the F014/F015/F017/F031 precedent). `RouteCommand` keeps
  its existing MVU boundary; `classify` is invoked at the edge `Interpreter`, not inside a pure `update`.
- **V. Test Evidence Is Mandatory** — PASS. Tests fail-before/pass-after against real `.fsgg` fixtures and
  real upstream cores (`Config`/`Routing` never mocked). A real-filesystem `fsgg route` end-to-end proves
  the CLI surfacing. No synthetic evidence is anticipated; any would be disclosed at the use site, carry
  `Synthetic` in the test name, and be listed in the PR.
- **VI. Observability and Safe Failure** — PASS. Diagnostics distinguish missing/malformed **input**
  (unsupported `capabilities.yml` version with migration pointer, unknown field, malformed kind/tier,
  duplicate id, a monorepo-only `PathEscapesRoot` source) from a **tool defect**, and name the offending
  surface/field (FR-015). No swallowed errors; no fabricated classification; an undeclared-tier evidence tag
  is an explicit non-error note (FR-016), never a silent gap.

**Change Classification: Tier 1 (contracted change)** — modifies public API surface (`Config.Model`/`Schema`
records/DUs/version function, `RouteJson` schema + signature), adds a new public library, and **evolves the
`capabilities.yml` schema** with a documented migration. Requires the full chain: spec, plan, `.fsi`
updates, surface-area baseline updates, test evidence, and migration documentation. The `capabilities.yml`
public contract change documents compatibility impact and migration guidance (`contracts/migration.md`),
satisfying the Engineering-Constraints "public API changes document compatibility impact and migration
guidance" rule.

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/058-generated-product-capabilities/
├── plan.md                          # This file (/speckit-plan output)
├── research.md                      # Phase 0 — D1..D8 decisions
├── data-model.md                    # Phase 1 — extended types + new module
├── quickstart.md                    # Phase 1 — validation scenarios
├── contracts/                       # Phase 1
│   ├── capabilities-schema-v2.md    #   authored v2 capabilities.yml contract
│   ├── classification.md            #   classify algorithm: precedence + tier-selection + CLI/JSON surfacing
│   └── migration.md                 #   v1→v2 migration guidance (referenced by the diagnostic)
└── tasks.md                         # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.Config/                      # EXTEND (Tier-1 schema change)
│   ├── Model.fsi / Model.fs                      #   SurfaceClass +6 cases; GeneratedProductTier DU;
│   │                                             #     EvidenceTag/TemplateProfile/Baseline newtypes;
│   │                                             #     Surface +3 optional fields; Check +Tier; token helpers
│   └── Schema.fsi / Schema.fs                    #   supportedVersionFor (capabilities→2, rest→1);
│                                                 #     parse/validate new kinds/tier/attrs; migration pointer
├── FS.GG.Governance.Findings/                    # EXTEND (behavior only)
│   └── Findings.fs                               #   widen escalating-boundary set (no .fsi/type change)
├── FS.GG.Governance.ProductSurfaces/             # NEW — pure classification + tier selection
│   ├── Model.fsi / Model.fs                      #   TierAlternative/ClassificationReason/ProductClassification/
│   │                                             #     ProductSurfaceReport + token helpers
│   ├── ProductSurfaces.fsi / ProductSurfaces.fs  #   classify : TypedFacts -> RouteReport -> ProfileId -> ProductSurfaceReport
│   └── FS.GG.Governance.ProductSurfaces.fsproj   #   refs: Config, Routing (+ RouteExplain shape mirror)
├── FS.GG.Governance.RouteJson/                   # EXTEND
│   └── RouteJson.fsi / RouteJson.fs              #   schemaVersion -> "fsgg.route/v2"; productSurfaces section
└── FS.GG.Governance.RouteCommand/                # EXTEND
    ├── Loop.fs / Interpreter.fs                  #   call classify at the edge; thread report into render/JSON

tests/
├── FS.GG.Governance.Config.Tests/                # EXTEND — migrate 24 v1 capabilities.yml -> v2;
│   └── fixtures/                                 #     add surface-package/-docs/-skill/-design/-sampleApp/
│                                                 #     -generatedProduct, generated-product-standalone,
│                                                 #     migration-v1-capabilities, malformed v2 (dup/unknown/version)
├── FS.GG.Governance.Findings.Tests/              # EXTEND — protected-boundary widening cases
├── FS.GG.Governance.ProductSurfaces.Tests/       # NEW — classify/precedence/tier-selection/standalone/determinism
├── FS.GG.Governance.RouteJson.Tests/             # EXTEND — productSurfaces section, golden, determinism
└── FS.GG.Governance.RouteCommand.Tests/          # EXTEND — real-filesystem `fsgg route` end-to-end

surface/
├── FS.GG.Governance.Config.surface.txt           # EDIT — new DU cases/newtypes/fields/supportedVersionFor
├── FS.GG.Governance.RouteJson.surface.txt         # EDIT — signature/schemaVersion change
├── FS.GG.Governance.RouteCommand.surface.txt      # EDIT (if Model surface changes)
└── FS.GG.Governance.ProductSurfaces.surface.txt   # NEW — committed baseline

FS.GG.Governance.sln                               # EDIT — add ProductSurfaces + its test project
```

**Structure Decision**: Compose, don't fork. The feature **extends** the established F014 `Config` catalog
and F015/F017 routing/findings rather than introducing a parallel catalog or router (Assumptions: "routing
reuse, not a new router"). The only new project is one pure leaf library, `FS.GG.Governance.ProductSurfaces`,
mirroring the `Routing`/`Route`/`RouteExplain`/`Findings` decomposition: it consumes the already-typed F014
facts and the already-computed F015 route report and adds the product-surface classification + cost-tier
selection, keeping all product vocabulary out of the kernel (FR-014). The CLI surfacing reuses the existing
`fsgg route` host (`RouteCommand` + `RouteJson`) — the standalone entry point — with `RouteJson`'s schema
bumped one version, exactly the deterministic-JSON precedent the other commands set.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
