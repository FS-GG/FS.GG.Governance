# Tasks: Generated-Product Capabilities (F23)

**Input**: Design documents from `/specs/058-generated-product-capabilities/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md, contracts/capabilities-schema-v2.md, contracts/classification.md, contracts/migration.md

**Tier**: **Tier 1 (contracted change)** — full chain owed: `.fsi`, surface baselines, test evidence, **and a documented `capabilities.yml` schema migration**. One new public project (`FS.GG.Governance.ProductSurfaces`); five extended projects (`Config`, `Findings`, `RouteJson`, `RouteCommand`, and their tests). The `.fsgg` four-file schema is unchanged **except** `capabilities.yml`, which moves to `schemaVersion: 2`; `project.yml`/`policy.yml`/`tooling.yml` stay at `1`. Tests are in scope (Constitution V; plan lists every `.Tests` project).

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`/`US4`; setup/foundational/cross-cutting/polish tasks carry no story tag
- Discipline (Constitution I/II): for the **new** module draft its `.fsi` and a compiling stub before the real `.fs` body; for the **extended** modules draft the new `.fsi` shape first, then make the `.fs` match (they must compile together). Semantic tests call the loaded public surface (`Schema.validate`, `Schema.supportedVersionFor`, `ProductSurfaces.classify`, `Findings.findUnknownGovernedPaths`, `RouteJson.ofRouteResult`), never internals.

**Design note — vocabulary placement**: All product vocabulary lives in `FS.GG.Governance.Config.Model` (the catalog) and the new leaf adapter `FS.GG.Governance.ProductSurfaces` — never in the kernel (FR-014, SC-007). `ProductSurfaces` is a pure leaf (refs `Config` + `Routing`), mirroring the `Routing`/`Route`/`RouteExplain`/`Findings` decomposition: it consumes the already-typed F014 facts and the already-computed F015 route report verbatim — it re-parses no YAML, re-routes nothing, senses no git (contracts/classification.md).

**Design note — what is foundational vs. story-owned**: The shared **catalog vocabulary** (the six new `SurfaceClass` cases, `GeneratedProductTier`, the three newtypes, the extended `Surface`/`Check` records) and the **per-file version function** (`supportedVersionFor`, capabilities→`2`) are foundational — every story builds on them and the 24 migrated fixtures cannot validate without them. The *new-kind parsing*, *classification*, *Findings widening*, and *route surfacing* land in US1; *tier selection* in US2; *standalone* in US3; the *unsupported-version rejection + migration doc* in US4.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the one new `src` project, its test project, and wire the solution. Mirror an existing pure-leaf library (`FS.GG.Governance.RouteExplain` / `…Findings`) exactly.

- [X] T001 [P] Create `src/FS.GG.Governance.ProductSurfaces/FS.GG.Governance.ProductSurfaces.fsproj` (net10.0, `GenerateDocumentationFile`, `IsPackable=true`; refs: `FS.GG.Governance.Config` and `FS.GG.Governance.Routing`) with compile order `Model.fsi` → `Model.fs` → `ProductSurfaces.fsi` → `ProductSurfaces.fs` — mirror `FS.GG.Governance.RouteExplain.fsproj`.
- [X] T002 [P] Create `tests/FS.GG.Governance.ProductSurfaces.Tests/FS.GG.Governance.ProductSurfaces.Tests.fsproj` (Expecto + FsCheck; refs `ProductSurfaces`, `Config`, `Routing`) with a `Main.fs` Expecto entry — mirror `tests/FS.GG.Governance.Findings.Tests`.
- [X] T003 Add both new projects to `FS.GG.Governance.sln` (mirror the `RouteExplain` + `…Findings` solution-folder entries); confirm `dotnet build FS.GG.Governance.sln` resolves the new graph with the stub modules.

**Checkpoint**: Solution restores and builds with empty/stub `ProductSurfaces` modules.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the shared catalog vocabulary, the per-file version function, the migrated + new fixtures, the new module's `.fsi` + compiling stub, and the surface-drift harness. **No story body may begin until the contracts compile and the fixtures exist.**

**⚠️ CRITICAL**: Blocks US1/US2/US3/US4.

- [X] T004 Extend `src/FS.GG.Governance.Config/Model.fsi` **and** `Model.fs` together (must compile as a pair): add the six product `SurfaceClass` cases (`PackageSurface`, `DocsSurface`, `SkillSurface`, `DesignSurface`, `SampleAppSurface`, `GeneratedProductRoot`); the closed ordered `GeneratedProductTier` DU (`StructuralScan|RestoreBuild|FocusedTests|FullVerify|ReleaseValidation`) with `generatedProductTierRank: GeneratedProductTier -> int` (1..5) and `generatedProductTierToken`; the three single-string newtypes (`EvidenceTag`/`TemplateProfile`/`Baseline`); the three optional `Surface` fields (`EvidenceTag`/`TemplateProfile`/`Baseline`, all `option`); the optional `Check.Tier: GeneratedProductTier option`; and a total `surfaceClassToken` render helper covering every case (data-model §1). No access modifiers in `.fs` (Constitution II). (Config surface baseline re-blessed in Phase 7.)
- [X] T005 Extend `src/FS.GG.Governance.Config/Schema.fsi` **and** `Schema.fs`: **replace** `val supportedSchemaVersion: SchemaVersion` with `val supportedVersionFor: file: FsggFile -> SchemaVersion` (`Capabilities → 2`; `Project`/`Policy`/`Tooling → 1`), and route `validate`'s per-file version check through it so a v2 `capabilities.yml` with an MVP-shaped body validates (data-model §2.1, D1). The *rejection diagnostic* for a non-2 `capabilities.yml` is US4 (T031). Depends on T004.
- [X] T006 Migrate the **24** existing `capabilities.yml` fixtures under `tests/FS.GG.Governance.Config.Tests/fixtures/**/.fsgg/capabilities.yml` from `schemaVersion: 1` → `schemaVersion: 2` (the one-line bump only — an MVP-shaped v1 body is a valid v2 body, contracts/migration.md). Leave each fixture's `project.yml`/`policy.yml`/`tooling.yml` at `1`. The fixture whose intent is *unsupported version* (`malformed-unsupported-schema-version`) is **not** bumped — it stays an out-of-range version. Depends on T005.
- [X] T007 [P] Author `src/FS.GG.Governance.ProductSurfaces/Model.fsi` + a compiling stub `Model.fs`: `TierAlternative` (`CheaperLocalTier of GeneratedProductTier | NoCheaperLocalTier`), `ClassificationReason` (`OnlySurface | HighestPrecedenceKind | OrdinalSurfaceTiebreak`), `ProductClassification` (`Path`/`Capability`/`Surface`/`Class`/`SelectedTier`/`TierIsDeclared`/`Alternative`/`Reason`/`Explanation`), `ProductSurfaceReport { Classifications: ProductClassification list }`, plus any token helpers (data-model §3). Depends on T004 (uses `SurfaceClass`/`GeneratedProductTier`/`DomainId`/`SurfaceId`/`GovernedPath`).
- [X] T008 [P] Author `src/FS.GG.Governance.ProductSurfaces/ProductSurfaces.fsi` (`val classify: facts: TypedFacts -> report: RouteReport -> profile: ProfileId -> ProductSurfaceReport`) + a compiling stub `ProductSurfaces.fs` returning `{ Classifications = [] }`. Depends on T007.
- [X] T009 Exercise every `.fsi` surface to prove it compiles and composes before/with the `.fs` bodies (Constitution I): `dotnet build FS.GG.Governance.sln` checks each `.fsi` against its `.fs`, and a smoke semantic test loads and calls `Schema.supportedVersionFor`, `ProductSurfaces.classify` (stub), `RouteJson.ofRouteResult` verbatim. Depends on T004–T008.
- [X] T010 [P] Add `tests/FS.GG.Governance.ProductSurfaces.Tests/Support.fs` — helpers that build `TypedFacts`/`RouteReport`/`ProfileId` inputs for `classify` from a `.fsgg` fixture (reuse `Config.loadAndValidate` + `Routing.route`, never mocked) and small in-memory builders for precedence/tier unit cases. Depends on T008.
- [X] T011 [P] Add the new Config fixtures (no version bump needed beyond v2):
  - `product-surface-all-kinds` — a v2 `capabilities.yml` declaring **one surface of every new kind** (`package` with a `baseline` + `evidenceTag`, `generatedProduct` with a `templateProfile`, `docs`, `skill`, `design`, `sampleApp`) plus tiered checks across all five `GeneratedProductTier`s and a `pathMap` covering each (contracts/capabilities-schema-v2.md worked example). Drives US1/US2.
  - `generated-product-standalone` — the same product checked out with **no monorepo paths** (paths resolve only within the product root). Drives US3.
  - `malformed-product-path-escapes-root` — a declared surface path with a monorepo-only `..` escape. Drives US3 (`PathEscapesRoot`).
  Depends on T006.
- [X] T012 [P] Add the US4 (versioning) fixtures: `migration-v1-capabilities` (`capabilities.yml` at `schemaVersion: 1`); `malformed-v2-duplicate-id` (two surfaces/checks of the **new** kinds sharing an id); `malformed-v2-unknown-field` (a field not in the v2 schema on a new-kind surface); `malformed-v2-version-three` (`capabilities.yml` at `schemaVersion: 3`). Depends on T006.
- [X] T013 [P] Add `tests/FS.GG.Governance.ProductSurfaces.Tests/SurfaceDriftTests.fs` — load the public surface, compare to `surface/FS.GG.Governance.ProductSurfaces.surface.txt`, honor `BLESS_SURFACE=1` (mirror the existing surface-drift test). Baseline committed in Phase 7 once `.fs` bodies stabilize.

**Checkpoint**: Vocabulary + version function compile; the migrated MVP-shaped Config suite is green; new module `.fsi` + stub compile; fixtures and the new surface harness are in place — story work can begin.

---

## Phase 3: User Story 1 — Declare a product's full surface set; route + classify; no hidden protected surface (Priority: P1) 🎯 MVP

**Goal**: A v2 `capabilities.yml` can declare a surface of every new kind; a change under any one routes to its capability and is **classified** (`ProductSurfaces.classify`) with the expected `SurfaceClass`; a routine path under no surface produces **no** entry (light-by-default); and a new protected surface (`package`/`release`/`generatedProduct`) placed under a governed root with no metadata becomes an `UnknownProtectedBoundaryPath` finding — never routine, never a silent pass. The classification is surfaced through `fsgg route` (human + `route.json` at `fsgg.route/v2`).

**Independent Test**: Against `product-surface-all-kinds`, change a file under each declared surface in turn ⇒ each yields a `ProductClassification` with the expected `Class` and matched `Capability` (SC-001); add a public-API path under a `package`/`release`/`generatedProduct` boundary with no `pathMap` glob ⇒ `findUnknownGovernedPaths` emits `UnknownProtectedBoundaryPath` (SC-002); a routine path matching no surface ⇒ no finding and no classification (FR-004); `fsgg route` renders the classifications and persists a `fsgg.route/v2` `route.json` whose printed bytes equal the file.

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T014 [P] [US1] `tests/FS.GG.Governance.Config.Tests/SchemaTests.fs` (extend) — each new `kind` token parses to the right `SurfaceClass`; the optional `evidenceTag`/`templateProfile`/`baseline` attrs and the check `tier` token parse onto their records; a **subset** declaration (only some new kinds) is `Valid`, not an error (edge "subset declaration", FR-004); a malformed `kind`/`tier` scalar ⇒ `MalformedValue` naming the field (contracts/capabilities-schema-v2.md). Uses `product-surface-all-kinds`.
- [X] T015 [P] [US1] `tests/FS.GG.Governance.ProductSurfaces.Tests/ClassifyTests.fs` — `classify` membership: a change under each declared surface kind yields exactly one `ProductClassification` with the expected `Class` + matched `Capability`; a routed path under **no** declared surface yields **no** entry (FR-004); membership uses the same `Glob.matches`/segment-prefix relation as routing (contracts/classification.md §1–2).
- [X] T016 [P] [US1] `tests/FS.GG.Governance.ProductSurfaces.Tests/PrecedenceTests.fs` — multi-match precedence (FR-008, D6): a path under two kinds wins by the documented `SurfaceClass` total order (`Reason = HighestPrecedenceKind`); co-kind ties break by ordinal-first `SurfaceId` (`OrdinalSurfaceTiebreak`); a single cover is `OnlySurface`; **re-ordering** the authored surfaces does not change the winner (SC-005). (Tier value asserted in US2 — here assert `Class`/`Surface`/`Reason` only.)
- [X] T017 [P] [US1] `tests/FS.GG.Governance.Findings.Tests/` (extend) — the escalating-boundary widening (FR-003, SC-002): an `UnmatchedInRoot` path under a `PackageSurface`, `ReleaseSurface`, or `GeneratedProductRoot` ⇒ `Id = UnknownProtectedBoundaryPath` / `Zone = ProtectedBoundaryUnknown sid`; under a `DocsSurface`/`SkillSurface`/`DesignSurface`/`SampleAppSurface`/`GeneratedView` ⇒ ordinary `UnknownGovernedPath`; under a `Routine` surface ⇒ suppressed; a `Routed` path ⇒ never a finding. Assert against real facts, not internals.
- [X] T018 [P] [US1] `tests/FS.GG.Governance.RouteJson.Tests/` (extend) — `ofRouteResult` (now taking a `ProductSurfaceReport`) emits `schemaVersion: "fsgg.route/v2"` and a deterministic `productSurfaces` array, each entry `{ path, capability, surface, class, tier, tierDeclared, alternative }`, sorted by path then surface; an empty report ⇒ an empty (present) array; tokens are exhaustive (a future `SurfaceClass`/tier case is a compile error, no wildcard) (data-model §5, contracts/classification.md §CLI/JSON). **As-built:** `ProductSurfacesEmbedTests.fs` asserts the additive section + empty-report-byte-identity; the golden lives at `golden/route-product-surfaces.json` (T041).
- [X] T019 [P] [US1] `tests/FS.GG.Governance.RouteCommand.Tests/` (extend) — real-filesystem `fsgg route` end-to-end via `Interpreter.run` over `product-surface-all-kinds`: human output lists each routed product path with `capability · class · tier · alternative`; the persisted `route.json` is `fsgg.route/v2` with the `productSurfaces` array; the `--json` stdout equals the persisted file byte-for-byte (one source of truth). **As-built:** an `InterpreterTests` end-to-end over a product catalog (`productCatalog`) asserts route.json carries `productSurfaces` and the summary shows the `product surfaces:` block.

### Implementation for User Story 1

- [X] T020 [US1] `src/FS.GG.Governance.Config/Schema.fs` — extend strict validation to parse the new `kind` tokens (→ `SurfaceClass`, single-sourced with `surfaceClassToken`), the optional `evidenceTag`/`templateProfile`/`baseline` surface attrs, and the optional check `tier` token (→ `GeneratedProductTier`); a token outside the closed set ⇒ `MalformedValue`; unknown field ⇒ `UnknownField` (reuse the closed diagnostic set — no new `DiagnosticId`). Makes T014 pass. Depends on T004/T005.
- [X] T021 [US1] `src/FS.GG.Governance.ProductSurfaces/Model.fs` — replace the stub with the real record/DU bodies + token helpers (`classificationReasonToken`, `tierAlternative` projection) used by `classify` and `RouteJson`. Makes T007 surface concrete.
- [X] T022 [US1] `src/FS.GG.Governance.ProductSurfaces/ProductSurfaces.fs` — implement `classify`: per routing in normalized-path order, find covering surfaces via `Glob.matches`/segment-prefix; no cover ⇒ no entry; on multi-match apply the documented `SurfaceClass` total order then ordinal-`SurfaceId` tiebreak (set `Reason`); set `Capability`/`Surface`/`Class`; compute a **baseline tier per winning kind** (contracts/classification.md §4 table) with `TierIsDeclared`/`Alternative = NoCheaperLocalTier` as the US1 default (profile escalation + snap-to-declared + cheaper-local are US2); build a deterministic `Explanation` naming capability·class·tier; sort `Classifications` by path then `SurfaceId`. Pure/total, byte-identical. Makes T015/T016 pass. Depends on T021.
- [X] T023 [US1] `src/FS.GG.Governance.Findings/Findings.fs` — widen the escalating-boundary set in `findUnknownGovernedPaths`/`classifyUnmatched` from `ProtectedSurface` only to `ProtectedSurface ∪ PackageSurface ∪ ReleaseSurface ∪ GeneratedProductRoot`; the four non-protected new kinds (`docs`/`skill`/`design`/`sampleApp`) stay ordinary `UnknownGovernedPath`. **Behavior only — no `.fsi`/type change** (`Findings.surface.txt` unchanged). Makes T017 pass.
- [X] T024 [US1] `src/FS.GG.Governance.RouteJson/RouteJson.fsi` + `RouteJson.fs` — bump `schemaVersion` `"fsgg.route/v1"` → `"fsgg.route/v2"`; extend `ofRouteResult` to take the `ProductSurfaceReport` and emit the deterministic `productSurfaces` array via a hand-driven `Utf8JsonWriter` walk (AuditJson/ReleaseJson precedent): per entry `{ path, capability, surface, class (surfaceClassToken), tier (generatedProductTierToken), tierDeclared, alternative }`, sorted by path then surface; exhaustive token helpers (no wildcard). Makes T018 pass. (RouteJson surface baseline re-blessed in Phase 7.) Depends on T021. **As-built (additive, non-breaking):** RouteJson was already at `"fsgg.route/v2"` (F045/F052), so `ofRouteResult` is left byte-identical and a new `ofRouteResultWithProductSurfaces … -> ProductSurfaceReport -> string` emits the `productSurfaces` array only when non-empty (empty ⇒ identical bytes; schemaVersion unchanged).
- [X] T025 [US1] `src/FS.GG.Governance.RouteCommand/Loop.fs` + `Interpreter.fs` — call `ProductSurfaces.classify facts report profile` at the **edge** `Interpreter` (not inside a pure `update`), thread the `ProductSurfaceReport` into `Model`, render it in `renderText` (path → capability·class·tier·alternative), and pass it to `RouteJson.ofRouteResult` in `renderJson`/the persist effect; printed machine output equals the persisted `route.json`. Makes T019 pass. Depends on T022/T024. (RouteCommand surface baseline re-blessed in Phase 7 if `Model` surface changes.) **As-built:** `classify` runs at the edge `update` (pure) using the catalog's default profile (or `standard`); the report is threaded via `Model.Classifications`, rendered as a `product surfaces:` block, and passed to `ofRouteResultWithProductSurfaces` at the persist effect.

**Checkpoint**: MVP — every new surface kind declares, routes, and classifies; the no-hide safety property holds; `fsgg route` surfaces the classification at `fsgg.route/v2`. Tier selection is baseline-only; profile escalation/cheaper-local/standalone/version-rejection not yet added.

---

## Phase 4: User Story 2 — Cost-tiered generated-product gate selection (Priority: P2)

**Goal**: `classify` selects the appropriate `GeneratedProductTier` per routed surface: a cheap-by-default baseline per kind, profile escalation on a positive match only (release-oriented profile → `ReleaseValidation` for a `ReleaseSurface`; strict profile → +1 rank), snapped to the **deepest declared tier ≤ target** (else cheapest declared; else `target` with `TierIsDeclared = false`, the F24-pending non-error note); plus a `CheaperLocalTier` alternative when a strictly-cheaper locally-runnable declared tier exists. Deterministic on multi-match; the explanation names the selected tier and the alternative.

**Independent Test**: Against `product-surface-all-kinds`: a `docs/*` change ⇒ `StructuralScan`, no deeper tier pulled in (SC-003); a `release/*` change under a release profile ⇒ `ReleaseValidation` with an explanation naming the tier; `src/**/*.fsi` (package) under `standard` ⇒ `FocusedTests` with `CheaperLocalTier StructuralScan`; a path under two surfaces ⇒ deterministic class + tier independent of declaration/run order (FR-008).

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T026 [P] [US2] `tests/FS.GG.Governance.ProductSurfaces.Tests/TierBaselineTests.fs` — the baseline tier per winning kind (contracts/classification.md §4 table): docs/sampleApp → `StructuralScan`; skill/design/generatedView → `RestoreBuild`; package/generatedProduct → `FocusedTests`; release → `FullVerify`; a cheap change never pulls in a deeper tier without a positive match (SC-003).
- [X] T027 [P] [US2] `tests/FS.GG.Governance.ProductSurfaces.Tests/TierProfileTests.fs` — profile escalation: a release-oriented profile raises a `ReleaseSurface` target to `ReleaseValidation`; a strict profile raises the target by **one** rank; a profile **never** lowers below baseline and **never** raises a kind that did not match (FR-006); profiles read from `PolicyFacts.Profiles`.
- [X] T028 [P] [US2] `tests/FS.GG.Governance.ProductSurfaces.Tests/TierSnapTests.fs` — snap-to-declared: select the deepest declared tiered check (in the winning capability's domain) **not exceeding** the target; if none ≤ target, the cheapest declared; if **no** tiered check declared, `SelectedTier = target` and `TierIsDeclared = false` (FR-016). `CheaperLocalTier t` iff a declared tiered check with `Environment ∈ {Local, LocalOrCi}` and `Tier < SelectedTier` exists, else `NoCheaperLocalTier` (always present, FR-007).
- [X] T029 [P] [US2] `tests/FS.GG.Governance.ProductSurfaces.Tests/TierDeterminismTests.fs` — FsCheck: re-ordering authored surfaces/checks/input paths leaves `SelectedTier`, `Alternative`, and `Explanation` unchanged (SC-005); the `Explanation` names the matched capability, class, selected tier, and (when known) the cheaper local alternative (FR-007).

### Implementation for User Story 2

- [X] T030 [US2] `src/FS.GG.Governance.ProductSurfaces/ProductSurfaces.fs` — replace the US1 baseline-only tier with full tier selection: baseline-per-kind → profile escalation (positive-match only, read from `PolicyFacts.Profiles`) → snap to the deepest declared tier ≤ target (else cheapest declared; else target + `TierIsDeclared=false`) → `CheaperLocalTier`/`NoCheaperLocalTier`; extend `Explanation` to name the tier + alternative. Makes T026–T029 pass; the US1 route/JSON surfacing (T024/T025) already renders the resulting tier/alternative. Depends on T022.

**Checkpoint**: US1 + US2 — cost tiers select cheap-by-default, escalate on positive match, snap to declared depths, and surface a cheaper-local alternative; deterministic on multi-match.

---

## Phase 5: User Story 3 — Run Governance locally without monorepo access (Priority: P2)

**Goal**: A generated product checked out standalone (no monorepo) routes + classifies using only its own declared sources; a declared source resolvable only inside the monorepo (a `..` escape) yields a clear `PathEscapesRoot` input diagnostic — not a fabricated success; and the product carries no build/run-time dependency on the tool.

**Independent Test**: Against `generated-product-standalone` (no monorepo paths), `Config.loadAndValidate` + `Routing.route` + `ProductSurfaces.classify` complete using only the product's own sources (SC-004); `malformed-product-path-escapes-root` ⇒ `Invalid [PathEscapesRoot]` naming the offending surface/field (FR-010); a scope guard confirms the fixture references no tool path.

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T031 [P] [US3] `tests/FS.GG.Governance.ProductSurfaces.Tests/StandaloneTests.fs` — load `generated-product-standalone` with the F014 per-directory `Loader` (reads only the `.fsgg` parent) + route + `classify`: routing and classification complete using only the product's own declared sources; the result depends on nothing outside the product root (SC-004, FR-009).
- [X] T032 [P] [US3] `tests/FS.GG.Governance.Config.Tests/` (extend) — `malformed-product-path-escapes-root` ⇒ `Invalid [PathEscapesRoot]` whose message names the offending surface/field (FR-010, FR-015); a monorepo-only reference is never best-effort resolved into a fabricated success.
- [X] T033 [P] [US3] `tests/FS.GG.Governance.ProductSurfaces.Tests/ScopeGuardTests.fs` — assert the `ProductSurfaces` reachable assembly surface references no network API and performs no I/O/clock/git (the pure-leaf scope-guard precedent); `classify` is a pure function of its three inputs (SC-004/SC-007).

### Implementation for User Story 3

- [X] T034 [US3] Confirm/realize standalone support — the F014 `Loader` already reads only the `.fsgg` parent directory and `PathEscapesRoot` already fires on an escaping path; this story is satisfied by **reuse** plus the fixtures (T011/T012). If T031/T032 surface any gap (e.g. an absolute/monorepo path not normalized), fix it in `Config` (`Loader`/`Schema`) — otherwise record "reuse, no code change" with the green-test evidence on this line. Depends on T031/T032. **As-built: reuse, no code change** — `generated-product-standalone` loads/routes/classifies and `malformed-product-path-escapes-root` yields `PathEscapesRoot` with no `Config` change; green test evidence in `StandaloneTests`/`ProductSchemaV2Tests`.

**Checkpoint**: A generated product is governable standalone using only its own catalog; a monorepo-only reference is a clear input diagnostic; the adapter is a pure, network-free leaf.

---

## Phase 6: User Story 4 — Versioned schema with a safe migration (Priority: P3)

**Goal**: `capabilities.yml` carries `schemaVersion: 2`; a non-2 version is rejected with `UnsupportedSchemaVersion` naming the version **and** pointing at `contracts/migration.md`; duplicate-id rejection, unknown-field rejection, and deterministic ordering all extend to the new fields exactly as the MVP fields; an MVP-version (v1) catalog is handled per the documented migration (rejected with guidance) — never silently mis-parsed.

**Independent Test**: `migration-v1-capabilities` ⇒ `Invalid [UnsupportedSchemaVersion]`, message contains `1` and the migration pointer (SC-006); `malformed-v2-version-three` ⇒ `UnsupportedSchemaVersion` naming `3`; `malformed-v2-duplicate-id` ⇒ `DuplicateId` (both locations); `malformed-v2-unknown-field` ⇒ `UnknownField` naming the field; two loads of `product-surface-all-kinds` order their contents identically (SC-005).

### Tests for User Story 4 ⚠️ (write first, must FAIL before impl)

- [X] T035 [P] [US4] `tests/FS.GG.Governance.Config.Tests/` (extend) — `migration-v1-capabilities` ⇒ `Invalid [UnsupportedSchemaVersion]`, message naming version `1` and pointing at `contracts/migration.md` (FR-011/FR-013/SC-006); `malformed-v2-version-three` ⇒ `UnsupportedSchemaVersion` naming `3`; `project.yml`/`policy.yml`/`tooling.yml` at `1` remain `Valid` (per-file version, D1).
- [X] T036 [P] [US4] `tests/FS.GG.Governance.Config.Tests/` (extend) — strict validation across the expansion: `malformed-v2-duplicate-id` ⇒ `DuplicateId` naming both locations (across the **new** surface/check ids); `malformed-v2-unknown-field` ⇒ `UnknownField` naming the field (FR-012, SC-005).
- [X] T037 [P] [US4] `tests/FS.GG.Governance.Config.Tests/DeterminismTests.fs` (extend) — two loads of `product-surface-all-kinds` produce byte-identical typed facts including the new fields, in id/path-sorted order, regardless of authored order (FR-012, SC-005).

### Implementation for User Story 4

- [X] T038 [US4] `src/FS.GG.Governance.Config/Schema.fs` — the `capabilities.yml` version check (via `supportedVersionFor`, T005) emits `UnsupportedSchemaVersion` whose `Message` names the actual version **and** points at `contracts/migration.md` when it is not `2`; confirm duplicate-id/unknown-field/ordering paths already cover the new fields (extend if a new-field gap appears). Makes T035–T037 pass. Depends on T005/T020.
- [X] T039 [US4] Verify `specs/058-generated-product-capabilities/contracts/migration.md` is the discoverable target the diagnostic names and that its v1→v2 guidance matches the implemented behavior (one-line version bump; MVP body is a structural subset of v2). Update the doc if the diagnostic wording or fixture names drifted. Depends on T038.

**Checkpoint**: All four stories — the schema is versioned, a wrong version is rejected with discoverable migration guidance, and strict validation/determinism hold across every new field.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Surface baselines, the route golden, the product-neutrality guard, docs, and the quickstart validation pass.

- [X] T040 Bless and commit the surface baselines that changed (`BLESS_SURFACE=1 dotnet test …`), then re-run drift green: `surface/FS.GG.Governance.Config.surface.txt` (new DU cases/newtypes/fields/`supportedVersionFor`), `surface/FS.GG.Governance.RouteJson.surface.txt` (schemaVersion + `ofRouteResult` signature), `surface/FS.GG.Governance.ProductSurfaces.surface.txt` (**new**, T013), and `surface/FS.GG.Governance.RouteCommand.surface.txt` **only if** its `Model` surface changed (T025). `Findings.surface.txt` is unchanged (behavior-only, T023) — confirm it did not drift.
- [X] T041 [P] Commit the `route.json` golden baseline carrying the `productSurfaces` array (referenced by T018/T019) under `tests/FS.GG.Governance.RouteJson.Tests/golden/`, generated from the stable `ofRouteResult`.
- [X] T042 [P] `tests/FS.GG.Governance.ProductSurfaces.Tests/ProductNeutralityTests.fs` — the SC-007 guard: no product/surface/path/template-profile/generator identity is hardcoded in `ProductSurfaces` (or in the kernel it consumes). Drive `classify` with **two** fixtures carrying *different* invented surface ids/kinds/paths and assert the report reflects the input verbatim, with no string from the spec's example catalogs appearing unless the fixture supplied it. Closes "verifiable by inspection" with an automated assertion.
- [X] T043 [P] Update `CLAUDE.md` and the Phase 10 roadmap row: F23 `023-generated-product-capabilities` complete (capability catalog expanded to the full product-surface vocabulary; `capabilities.yml` → `schemaVersion: 2` with migration; routing/classification + cost-tier selection + no-hide safety via `fsgg route`/`fsgg.route/v2`); note the per-domain deterministic **checks** remain F24 (`024-package-docs-skills-design-checks`) and that a declared `evidenceTag` without its check is a known non-error state (FR-016).
- [X] T044 Run the `quickstart.md` validation end to end (all six scenarios + the constitution-gate checks): build clean (warnings-as-errors); `Config.Tests`, `ProductSurfaces.Tests`, `Findings.Tests`, `RouteJson.Tests`, `RouteCommand.Tests` green; surface baselines match; a real-host `fsgg route` smoke run shows the `fsgg.route/v2` `productSurfaces` output. Record the evidence on this line.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no deps; T001/T002 parallel, T003 after them.
- **Foundational (Phase 2)** → after Setup. T004 first (shared vocabulary); T005 after T004; T006 after T005; T007/T008 after T004 (T008 after T007); T009 after T004–T008; T010/T011/T012/T013 in parallel after their inputs. **Blocks all stories.**
- **US1 (Phase 3)** → after Foundational. MVP. (`Schema.fs` parsing, `Model.fs`, `ProductSurfaces.fs` classify, `Findings.fs` widening, `RouteJson.fs`, then `RouteCommand`.)
- **US2 (Phase 4)** → after US1's `classify` exists; tier selection layers onto T022.
- **US3 (Phase 5)** → after Foundational + US1 `classify`; mostly reuse + fixtures.
- **US4 (Phase 6)** → after Foundational (`supportedVersionFor`) + US1 (`Schema.fs` parsing of new fields); adds the rejection diagnostic + migration doc.
- **Polish (Phase 7)** → after the desired stories; T040/T041/T044 need the `.fs` bodies stable.

### Within each story

- Tests first and FAILING, then implementation (Constitution V).
- For the new module, `.fsi` (Phase 2) before the real `.fs`; for extended modules, draft the `.fsi` shape then make `.fs` match. `Model` before `Schema` before `ProductSurfaces`; `ProductSurfaces`/`RouteJson` before `RouteCommand`.

### Parallel opportunities

- Phase 1: T001/T002 together.
- Phase 2: T007/T008 after T004 in parallel; T010/T011/T012/T013 in parallel after their inputs.
- Each story's `[P]` test tasks run together; once Foundational lands, US2/US3/US4 tests can be stubbed against US1's contracts.
- Phase 7: T041/T042/T043 are independent `[P]` tasks.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL — vocabulary + version + fixtures) → 3. Phase 3 US1 → **STOP & VALIDATE** (SC-001 every kind routes+classifies; SC-002 no-hide holds; FR-004 light-by-default) → demo `fsgg route` surfacing the classification at `fsgg.route/v2`.

### Incremental delivery

Setup + Foundational → US1 (declare/route/classify + no-hide + route surfacing, MVP) → US2 (cost-tier selection) → US3 (standalone) → US4 (versioned schema + migration) → Polish. Each story adds value without breaking the prior.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- Reuse, don't reinvent: `classify` consumes the F014 `TypedFacts` and the F015 `RouteReport` **verbatim** (no re-parse/re-route/sense) and matches paths with the F015 `Glob.matches`; `Findings` reuses its existing `classifyUnmatched` machinery; `RouteJson` reuses its `Utf8JsonWriter` projection; the F014 `Loader` (already per-directory) is the standalone entry point. Upstream cores are never mocked in semantic tests (Constitution V).
- **Closed diagnostic set preserved** (FR-015): every new field reuses `UnknownField`/`MalformedValue`/`DuplicateId`/`UnsupportedSchemaVersion`/`PathEscapesRoot`/`DanglingReference` — **no new `DiagnosticId`**.
- **Product-neutral core** (FR-014, SC-007): no product/surface/path/template-profile/generator identity in the kernel — all vocabulary lives in `Config.Model` + `ProductSurfaces`; T042 asserts it.
- Elmish/MVU applicability: `Schema.validate`, `ProductSurfaces.classify`, and `Findings` are **pure, total leaves** — no MVU ceremony (the F014/F015/F017/F031 precedent); `RouteCommand` keeps its existing MVU boundary, with `classify` invoked at the edge `Interpreter`, never inside a pure `update` (T025). The pure transitions are exercised directly (T015/T016/T026–T029); the real-interpreter evidence is T019/T044.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document on the task line.
