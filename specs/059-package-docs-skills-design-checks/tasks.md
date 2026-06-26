# Tasks: Package / Docs / Skills / Design Deterministic Checks (F24)

**Input**: Design documents from `/specs/059-package-docs-skills-design-checks/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md, contracts/surface-check-finding.md, contracts/package-checks.md, contracts/docs-checks.md, contracts/skill-checks.md, contracts/design-checks.md, contracts/verify-json-surfacechecks.md

**Tier**: **Tier 1 (contracted change)** — full chain owed: `.fsi` for every new module, new surface baselines, real test evidence, and the documented additive `verify.json` section. New public projects (a shared `SurfaceChecks` core, its `Dispatch` dispatcher, and four domain libraries `PackageChecks`/`DocsChecks`/`SkillChecks`/`DesignChecks`); two extended projects (`VerifyJson` additive overload, `VerifyCommand` edge wiring). **No** dependency added, **no** `capabilities.yml` schema change, **no** enforcement-truth-table change (FR-013, FR-014) — the only observable host change is the additive `surfaceChecks` section, byte-identical when empty (D8). Tests are in scope (Constitution V; plan lists every `.Tests` project).

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`/`US4`/`US5`; setup/foundational/cross-cutting/polish tasks carry no story tag
- Discipline (Constitution I/II): for every **new** module draft its `.fsi` and a compiling stub before the real `.fs` body; for the **extended** `VerifyJson`/`VerifyCommand` draft the new `.fsi` shape first, then make the `.fs` match (they must compile together). Semantic tests call the loaded public surface (`<Domain>Checks.evaluate`, `Interpreter.sense<Domain>`, `Composition.run`, `VerifyJson.ofVerifyResultWithSurfaceChecks`), never internals (Constitution I).

**Design note — project graph (resolves the data-model's one open layout micro-decision).** The data-model's "single shared core" carrying both `Model` and `Composition` is **not buildable as one project**: each domain library references `SurfaceChecks` for `SurfaceFinding`/`SurfaceCheckRequest`, and `Composition.DomainFactBundle` references the four domain `Model` types — a project-level reference cycle. We take the data-model's named alternative: the shared core `FS.GG.Governance.SurfaceChecks` carries **`Model` only** (refs `Config` + `Enforcement`), and the pure dispatcher `Composition` lives in a tiny `FS.GG.Governance.SurfaceChecks.Dispatch` project (refs `SurfaceChecks` + `Config` + `ProductSurfaces` + the four domain libraries). The four domain libraries reference **only** `SurfaceChecks` (`Model`) — never `Dispatch`, never each other — so no pack depends on another (FR-008) and there is no cycle. This makes the `src` count **6** and the committed surface-baseline count **6** (the data-model's original single-core default would have been 5 but does not build); the test count stays **5** (`SurfaceChecks.Tests` covers both core and dispatcher). plan.md §Project Structure / §Scale/Scope and data-model §1.1 are updated to this `Dispatch`-split structure so all three artifacts agree.

**Design note — pure pack + host sensor split (Constitution IV, FR-007).** Every domain library bundles three modules: `Model` (closed fact vocabulary), `<Domain>Checks.evaluate` (pure/total, no I/O), and `Interpreter` (the **sole** filesystem/process seam, an injected port executed only at the edge — the F054 `ReleaseFactsSensing` shape). `DesignChecks` is render-fenced: its `Model` and `evaluate` carry **zero** rendering/UI/registry dependency; only `Interpreter.DesignPort` reads a catalog, via `System.IO`/`System.Text.Json` exclusively (FR-007, SC-004).

**Design note — what is foundational vs. story-owned.** The shared `SurfaceFinding`/`SurfaceCheckRequest`/`CheckDomain` vocabulary and `enforcementInputOf` (reusing F023 verbatim), plus every domain's `Model` **fact** type and a compiling stub for `evaluate`/`Interpreter`/`Composition`, are foundational — the whole project graph must compile before any story body lands and `Composition.fsi` cannot compile without the four `Model` fact types. Each story then replaces its stubbed `evaluate` + `Interpreter` with real bodies, adds its fixtures, and its tests: **package** in US1, **docs** in US2, **skill** in US3, **design** in US4, the **advisory** cross-cutting guarantee in US5. The real `Composition.run` dispatch and the additive `fsgg verify` surfacing land in the integration phase once all four packs exist.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the six new `src` projects, the five test projects, and wire the solution. Mirror the `ReleaseFactsSensing` (Model + pure pack + Interpreter in one project) and `RouteExplain` pure-leaf precedents exactly.

- [X] T001 [P] Create the shared core `src/FS.GG.Governance.SurfaceChecks/FS.GG.Governance.SurfaceChecks.fsproj` (net10.0, `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.Config` + `FS.GG.Governance.Enforcement`) with compile order `Model.fsi` → `Model.fs` — mirror `FS.GG.Governance.RouteExplain.fsproj`.
- [X] T002 [P] Create the four domain projects, each `src/FS.GG.Governance.<Domain>Checks/FS.GG.Governance.<Domain>Checks.fsproj` (net10.0, `GenerateDocumentationFile`, `IsPackable=true`) with compile order `Model.fsi` → `Model.fs` → `<Domain>Checks.fsi` → `<Domain>Checks.fs` → `Interpreter.fsi` → `Interpreter.fs`: `PackageChecks` (refs `SurfaceChecks`, `Config`, `GateExecution`), `DocsChecks` (refs `SurfaceChecks`, `Config`), `SkillChecks` (refs `SurfaceChecks`, `Config`), `DesignChecks` (refs `SurfaceChecks`, `Config` — **no** rendering/UI/registry ref, FR-007). Mirror `FS.GG.Governance.ReleaseFactsSensing.fsproj`.
- [X] T003 [P] Create the dispatcher `src/FS.GG.Governance.SurfaceChecks.Dispatch/FS.GG.Governance.SurfaceChecks.Dispatch.fsproj` (net10.0, `GenerateDocumentationFile`, `IsPackable=true`; refs `SurfaceChecks`, `Config`, `ProductSurfaces`, and all four domain libraries) with compile order `Composition.fsi` → `Composition.fs`. Depends on T001/T002 existing (project references).
- [X] T004 [P] Create the five test projects (Expecto + Expecto.FsCheck/FsCheck, each with a `Main.fs` Expecto entry — mirror `tests/FS.GG.Governance.ReleaseFactsSensing.Tests`): `tests/FS.GG.Governance.SurfaceChecks.Tests` (refs `SurfaceChecks`, `SurfaceChecks.Dispatch`, `Config`, `ProductSurfaces`, `Enforcement`, and the four domain libs — it builds `DomainFactBundle`s); `tests/FS.GG.Governance.PackageChecks.Tests`, `…DocsChecks.Tests`, `…SkillChecks.Tests`, `…DesignChecks.Tests` (each refs its domain lib + `SurfaceChecks` + `Config`; `PackageChecks.Tests` also refs `GateExecution`).
- [X] T005 Add all six `src` + five `tests` projects to `FS.GG.Governance.sln` (mirror the `ReleaseFactsSensing` + `…ReleaseRules` solution-folder entries); confirm `dotnet build FS.GG.Governance.sln` resolves the new graph with empty/stub modules and **no reference cycle**. Depends on T001–T004.

**Checkpoint**: Solution restores and builds with empty/stub modules; the domain → `SurfaceChecks` and `Dispatch` → domains reference directions are acyclic.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the shared finding vocabulary (real), every domain's `Model` fact type (real) plus a compiling stub for each `evaluate`/`Interpreter`, the `Composition` `.fsi` + stub, and the surface-drift harnesses. **No story body may begin until the whole graph compiles and the contracts are exercisable.**

**⚠️ CRITICAL**: Blocks US1–US5.

- [X] T006 Author `src/FS.GG.Governance.SurfaceChecks/Model.fsi` **and** `Model.fs` together (real, must compile as a pair): the closed `CheckDomain` (`PackageDomain|DocsDomain|SkillDomain|DesignDomain`); `FindingLocation { File: GovernedPath; Detail: string }`; `SurfaceFinding { Domain; Surface: SurfaceId; Code: string; Location; BaseSeverity: Severity; Maturity; EvidenceTag: EvidenceTag option; IsInputState: bool; Message: string }`; `SurfaceCheckRequest { Domain; Surface: SurfaceId; Class: SurfaceClass; Path: GovernedPath; EvidenceTag: EvidenceTag option }`; the total render helpers `checkDomainToken`/`severityToken` (token tables, no clock/locale); and `enforcementInputOf: finding -> mode: RunMode -> profile: Profile -> EnforcementInput` building the F023 input from `BaseSeverity` + `Maturity` (reuse only — no truth-table logic). No access modifiers in `.fs` (Constitution II). (data-model §1; contracts/surface-check-finding.md C1/C4.)
- [X] T007 [P] Author `src/FS.GG.Governance.PackageChecks/Model.fsi` + real `Model.fs`: `SurfaceTokens of string list`; `FsiBaselineFact` (`BaselineMatches | BaselineDrift of added*removed | BaselineAbsent of SurfaceTokens | BaselineUnreadable of string`); `TranscriptOutcome` (`TranscriptPasses | TranscriptCompileFailed of string | TranscriptResultChanged of expected*actual | TranscriptUnlocatable of string`); `TranscriptFact { ExampleId; Source: GovernedPath; Outcome }`; `PackageFacts { BaselineSource: GovernedPath; Baseline; Transcripts: TranscriptFact list }` (data-model §2.1). Depends on T006.
- [X] T008 [P] Author `src/FS.GG.Governance.DocsChecks/Model.fsi` + real `Model.fs`: `LinkOutcome` (`LinkResolves | LinkDangling of string`); `LinkFact { Source; LinkText; Target; Outcome }`; `ReferenceOutcome` (`ReferenceResolves | ReferenceStale of string`); `ReferenceFact { Source; Reference; Outcome }`; `DocsFacts { Sources: GovernedPath list; Links; References; Unreadable: string list }` (data-model §3). Depends on T006.
- [X] T009 [P] Author `src/FS.GG.Governance.SkillChecks/Model.fsi` + real `Model.fs`: `PathContractOutcome` (`PathHolds | PathUnresolved of string | PathEscapesBounds of string`); `PathContractFact { Claimed; Outcome }`; `TaskListOutcome` (`TaskListConsistent | TaskListInconsistent of string`); `MirrorOutcome` (`NoMirrorDeclared | MirrorInSync | MirrorMissing of string | MirrorDrifted of mirror*detail`); `SkillFacts { SkillId; PathContract; TaskList; Mirror; Unreadable: string list }` (data-model §4). Depends on T006.
- [X] T010 [P] Author `src/FS.GG.Governance.DesignChecks/Model.fsi` + real `Model.fs`: `ResolveOutcome` (`Resolves | Absent of string`); `TokenFact`/`CaptureFact`/`ControlFact` (`{ <kind>; Outcome }`); `ContrastFact { Pair; Ratio: decimal; Threshold: decimal; Meets: bool }`; `DesignFacts { Tokens; Captures; Controls; Contrasts; CatalogUnavailable: string list }` (data-model §5). **No** rendering type referenced. Depends on T006.
- [X] T011 Author each domain's `<Domain>Checks.fsi` (the pure `evaluate: SurfaceCheckRequest -> <Domain>Facts -> SurfaceFinding list`) + a compiling stub `<Domain>Checks.fs` returning `[]`, and each `Interpreter.fsi` (the injected `<Domain>Port` record + `realPort` + `sense<Domain>: port -> request -> <Domain>Facts`) + a compiling stub `Interpreter.fs`. **Four independent `[P]` units, one per domain, for traceability + parallelism** — author them concurrently:
  - **T011a [P]** `PackageChecks`: `PackagePort` (`RegenerateSurface`/`ReadBaseline`/`WriteBaseline`/`RunTranscript`, `realPort repo exec`) (data-model §2.3). Depends on T007.
  - **T011b [P]** `DocsChecks`: `DocsPort` (`ReadSource`/`ResolveTarget`/`ResolveSymbol`) (data-model §3). Depends on T008.
  - **T011c [P]** `SkillChecks`: `SkillPort` (`ReadManifest`/`ResolvePath`/`ReadMirror`) (data-model §4). Depends on T009.
  - **T011d [P]** `DesignChecks`: `DesignPort` (`ReadTokenCatalog`/`ReadCaptureCatalog`/`ReadControlCatalog`/`ReadContrastCatalog`) (data-model §5). Depends on T010.
- [X] T012 Author `src/FS.GG.Governance.SurfaceChecks.Dispatch/Composition.fsi` + a compiling stub `Composition.fs`: `DomainFactBundle { Package: Map<SurfaceId,PackageFacts>; Docs: Map<…,DocsFacts>; Skill: Map<…,SkillFacts>; Design: Map<…,DesignFacts> }`; `domainOf: SurfaceClass -> CheckDomain option`; `requestsOf: TypedFacts -> ProductSurfaceReport -> SurfaceCheckRequest list`; `run: TypedFacts -> ProductSurfaceReport -> DomainFactBundle -> SurfaceFinding list`. Stub `run`/`requestsOf` return `[]`, `domainOf` returns `None` (data-model §1.1; contracts/surface-check-finding.md C2/C3). Depends on T011.
- [X] T013 Exercise every `.fsi` against its `.fs` and prove the public surface composes before the real bodies (Constitution I): `dotnet build FS.GG.Governance.sln` checks each pair, and a smoke semantic test in `tests/FS.GG.Governance.SurfaceChecks.Tests/SmokeTests.fs` loads and calls `Composition.run` (stub), each `<Domain>Checks.evaluate` (stub), and `Model.enforcementInputOf` verbatim. Depends on T006–T012.
- [X] T014 [P] Add a `SurfaceDriftTests.fs` to each of the five test projects — load the project's public surface, compare to `surface/FS.GG.Governance.<Project>.surface.txt`, honor `BLESS_SURFACE=1` (mirror the existing surface-drift test). Covers `SurfaceChecks`, `SurfaceChecks.Dispatch`, and the four domain libs (six baselines total). Baselines committed in Phase 9 once `.fs` bodies stabilize. Depends on T013.
- [X] T015 [P] Add `tests/FS.GG.Governance.SurfaceChecks.Tests/Support.fs` — helpers that build `TypedFacts`/`ProductSurfaceReport`/`SurfaceCheckRequest`/`DomainFactBundle` inputs from a `.fsgg` fixture (reuse `Config.loadAndValidate` + `Routing.route` + `ProductSurfaces.classify`, never mocked) plus small in-memory builders for the per-domain fact records. Depends on T013.

**Checkpoint**: The shared vocabulary and every domain `Model` are real and compile; `evaluate`/`Interpreter`/`Composition` stubs compile; the smoke test exercises the public surface; the surface-drift harnesses and shared test support are in place — story work can begin.

---

## Phase 3: User Story 1 — Package/API: `.fsi` baseline drift + FSI transcript currency (Priority: P1) 🎯 MVP

**Goal**: For a package surface F23 routed/classified, `PackageChecks.evaluate` over sensed `PackageFacts` reports **baseline drift** (a normalized token diff, not text — D5) naming the changed members, treats a first-run absent baseline as a fixable `IsInputState` finding (never a silent pass), and reports any published **FSI transcript** that no longer compiles or whose stated result changed. The `Interpreter` sensor is the sole filesystem/process seam, reusing the F051/F052 `ExecutionPort` for FSI runs (no `FSharp.Compiler.Service`). Produced evidence ties to the surface's declared `EvidenceTag`.

**Independent Test**: Against a package-surface fixture with a committed `.fsi` baseline + one passing transcript: clean ⇒ zero findings; change the public surface ⇒ `package.baseline-drift` naming the change, revert ⇒ no drift (SC-001); delete the baseline ⇒ regenerated + `package.baseline-absent` input-state finding; break/alter a transcript ⇒ the matching transcript finding naming the example; the produced evidence carries the declared `EvidenceTag` (acceptance 1.4).

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T016 [P] [US1] Add committed fixtures under `tests/FS.GG.Governance.PackageChecks.Tests/fixtures/`: a real `.fsi` baseline pair (committed baseline + a changed surface adding/removing a member), a passing FSI transcript (compiles + evaluates to its stated result) and a broken one (won't compile) plus a result-changed variant, and a `.fsgg` catalog declaring the package surface with an `evidenceTag` + `baseline`.
- [X] T017 [P] [US1] `tests/FS.GG.Governance.PackageChecks.Tests/EvaluateTests.fs` — drive `PackageChecks.evaluate` with hand-built `PackageFacts`: `BaselineMatches` ⇒ no finding; `BaselineDrift(added,removed)` ⇒ one Blocking `package.baseline-drift` naming the members; `BaselineAbsent` ⇒ one `IsInputState` `package.baseline-absent`; `BaselineUnreadable` ⇒ `IsInputState` `package.baseline-unreadable`; `TranscriptCompileFailed` ⇒ Blocking `package.transcript-compile` naming the example; `TranscriptResultChanged` ⇒ Blocking `package.transcript-result` naming both values; `TranscriptUnlocatable` ⇒ `IsInputState` `package.transcript-unlocatable` (contracts/package-checks.md C1–C3).
- [X] T018 [P] [US1] `tests/FS.GG.Governance.PackageChecks.Tests/SensorTests.fs` — `Interpreter.sensePackage` over the real on-disk fixtures through `realPort` (reusing a real `GateExecution.ExecutionPort` for transcripts): committed unchanged surface ⇒ `BaselineMatches`; changed surface ⇒ `BaselineDrift`; deleted baseline ⇒ baseline written + `BaselineAbsent`; passing transcript ⇒ `TranscriptPasses`, broken ⇒ `TranscriptCompileFailed`; an unlocatable transcript path ⇒ `TranscriptUnlocatable` (every exception caught, mapped to an input fact — FR-012).
- [X] T019 [P] [US1] `tests/FS.GG.Governance.PackageChecks.Tests/DeterminismTests.fs` — repeated `evaluate` over identical `PackageFacts` yields byte-identical findings; FsCheck: reordering the `Transcripts` list leaves the sorted findings unchanged (FR-010, SC-005, C4); assert no abs-path/clock/username in any `Message`.
- [X] T020 [P] [US1] `tests/FS.GG.Governance.PackageChecks.Tests/EvidenceTagTests.fs` — a request whose surface declared an `EvidenceTag` ⇒ every emitted finding's `EvidenceTag` equals the declared tag; a surface with no tag ⇒ `None` (FR-009, SC-007, acceptance 1.4).

### Implementation for User Story 1

- [X] T021 [US1] `src/FS.GG.Governance.PackageChecks/PackageChecks.fs` — replace the stub `evaluate`: emit the Blocking baseline-drift / transcript-compile / transcript-result findings and the `IsInputState` baseline-absent/unreadable/transcript-unlocatable findings per contracts/package-checks.md, each carrying `request.EvidenceTag`, `Location.File = normalizePath`d source, a deterministic `Message`; sort by `(member/example locus, Source, Code)`. Pure/total/no-I/O. Makes T017/T019/T020 pass. Depends on T007.
- [X] T022 [US1] `src/FS.GG.Governance.PackageChecks/Interpreter.fs` — replace the stub: `realPort repo exec` regenerates the surface token set, reads/writes the committed baseline, and runs each transcript by shelling FSI through the injected `ExecutionPort`; `sensePackage` compares as a normalized token diff (D5), writes + reports `BaselineAbsent` on first run, and catches every exception mapping to `*Unreadable`/`*Unlocatable` input facts (FR-012, FR-007). Makes T018 pass. Depends on T011/T021.

**Checkpoint**: MVP — a package surface's API drift and broken/stale FSI transcripts are caught deterministically, evidence ties to the declared tag, the only seam is the `Interpreter`. Other domains and the host surfacing not yet wired.

---

## Phase 4: User Story 2 — Docs/examples: link + reference currency (Priority: P2)

**Goal**: For a docs/examples surface, `DocsChecks.evaluate` reports a dangling link (`docs.link-currency` naming file + link + target) and a stale symbol/anchor reference (`docs.reference-currency` naming the stale reference); a clean surface yields **zero** findings (zero false positives). Scope is currency of the declared docs sources, not a full FsDocs build. An unreadable source is an `IsInputState` finding, never a fabricated pass. The `Interpreter.DocsPort` is the sole filesystem seam.

**Independent Test**: A docs fixture with a valid internal link, a valid reference, and a valid symbol reference ⇒ zero findings; break a link target ⇒ `docs.link-currency`; remove/rename a referenced symbol/anchor ⇒ `docs.reference-currency`; each names its exact location (SC-002).

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T023 [P] [US2] Add committed fixtures under `tests/FS.GG.Governance.DocsChecks.Tests/fixtures/`: a docs source with a live internal link + a valid reference + a valid symbol reference; a variant with a dangling internal link; a variant with a removed/renamed referenced symbol/anchor; and a `.fsgg` catalog declaring the docs surface.
- [X] T024 [P] [US2] `tests/FS.GG.Governance.DocsChecks.Tests/EvaluateTests.fs` — drive `DocsChecks.evaluate` with hand-built `DocsFacts`: all-resolve ⇒ zero findings (zero false positives, acceptance 2.1); `LinkDangling target` ⇒ Blocking `docs.link-currency` with `Location.File`=source, `Location.Detail`=link text, `Message` naming the target; `ReferenceStale symbol` ⇒ Blocking `docs.reference-currency` naming the stale reference; `Unreadable` source ⇒ `IsInputState` `docs.source-unreadable` (contracts/docs-checks.md C1/C2/C5). Also assert each emitted finding carries the surface's declared `EvidenceTag`, and `None` when the surface declared none (FR-009, SC-007).
- [X] T025 [P] [US2] `tests/FS.GG.Governance.DocsChecks.Tests/SensorTests.fs` — `Interpreter.senseDocs` over the real fixtures through `realPort`: scans the declared sources, extracts links + references, resolves each (`LinkResolves`/`LinkDangling`, `ReferenceResolves`/`ReferenceStale`), and records an unreadable source in `Unreadable` (every exception caught, FR-012).
- [X] T026 [P] [US2] `tests/FS.GG.Governance.DocsChecks.Tests/DeterminismTests.fs` — repeated `evaluate` over identical `DocsFacts` ⇒ byte-identical; FsCheck: reordering `Links`/`References` leaves the sorted-by-`(Source, locus)` findings unchanged (FR-010, SC-005, C4).

### Implementation for User Story 2

- [X] T027 [US2] `src/FS.GG.Governance.DocsChecks/DocsChecks.fs` — replace the stub `evaluate`: emit the Blocking link/reference findings and the `IsInputState` source-unreadable findings per contracts/docs-checks.md, each carrying `request.EvidenceTag`, normalized `Location.File`, deterministic `Message`; sort by `(Source, locus, Code)`. **Also emit the example-freshness advisory at its boundary (contracts/docs-checks.md C3)**: a docs example whose "match the current product surface" verdict is judgement-heavy ⇒ a `BaseSeverity = Advisory` finding (compile/evaluate staleness stays deterministic via the package transcripts) — produced here so `DocsChecks.fs` is written once, asserted by US5/T050. Pure/total/no-I/O. Makes T024/T026 pass; produces the advisory that T050 asserts. Depends on T008.
- [X] T028 [US2] `src/FS.GG.Governance.DocsChecks/Interpreter.fs` — replace the stub: `realPort repo` reads a docs source, resolves an internal path/anchor target, and resolves a symbol; `senseDocs` scans the declared sources, extracts and resolves each link/reference, and catches every exception into `Unreadable` (FR-012, FR-007). Makes T025 pass. Depends on T011/T027.

**Checkpoint**: US1 + US2 — package and docs surfaces are checked; docs rot is caught with exact locations; clean docs pass with zero false positives.

---

## Phase 5: User Story 3 — Skills: path contracts, task lists, mirrors (Priority: P2)

**Goal**: For a skill surface, `SkillChecks.evaluate` reports a claimed path that does not resolve or escapes the skill's declared bounds (`skill.path-contract` naming skill + path), an inconsistent task list (`skill.task-list`), and a missing/drifted declared mirror (`skill.mirror`); a skill that declares **no** mirror is not an error; a conformant skill yields zero findings. The `Interpreter.SkillPort` is the sole filesystem seam.

**Independent Test**: A skill fixture whose path contract holds, task list is consistent, and mirror is in sync ⇒ zero findings; a claimed path that does not resolve (or escapes bounds) ⇒ `skill.path-contract`; an inconsistent task list ⇒ `skill.task-list`; a drifted/removed mirror ⇒ `skill.mirror`; a no-mirror skill ⇒ not an error (SC-003).

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T029 [P] [US3] Add committed fixtures under `tests/FS.GG.Governance.SkillChecks.Tests/fixtures/`: a conformant skill (paths resolve in-bounds, consistent task list, in-sync mirror); a skill claiming a path that does not resolve; a skill claiming a path that escapes its bounds; a skill with an inconsistent task list; a skill with a drifted mirror and one with a missing mirror; a skill that declares no mirror; and a `.fsgg` catalog declaring the skill surface(s).
- [X] T030 [P] [US3] `tests/FS.GG.Governance.SkillChecks.Tests/EvaluateTests.fs` — drive `SkillChecks.evaluate` with hand-built `SkillFacts`: `PathHolds`/`MirrorInSync`/`NoMirrorDeclared`/`TaskListConsistent` ⇒ no finding; `PathUnresolved`/`PathEscapesBounds` ⇒ Blocking `skill.path-contract` naming skill + path; `TaskListInconsistent` ⇒ Blocking `skill.task-list`; `MirrorMissing`/`MirrorDrifted` ⇒ Blocking `skill.mirror`; `Unreadable` ⇒ `IsInputState` naming the source (contracts/skill-checks.md C1–C4). Also assert each emitted finding carries the surface's declared `EvidenceTag`, and `None` when the surface declared none (FR-009, SC-007).
- [X] T031 [P] [US3] `tests/FS.GG.Governance.SkillChecks.Tests/SensorTests.fs` — `Interpreter.senseSkill` over the real fixtures through `realPort`: reads the manifest, resolves each claimed path (resolves? within bounds?), assesses task-list consistency, and reads the mirror (`None` ⇒ declared-absent ⇒ `NoMirrorDeclared`); every exception caught into `Unreadable` (FR-012).
- [X] T032 [P] [US3] `tests/FS.GG.Governance.SkillChecks.Tests/DeterminismTests.fs` — repeated `evaluate` over identical `SkillFacts` ⇒ byte-identical; FsCheck: reordering `PathContract` leaves the sorted-by-`(skill, locus)` findings unchanged (FR-010, SC-005).

### Implementation for User Story 3

- [X] T033 [US3] `src/FS.GG.Governance.SkillChecks/SkillChecks.fs` — replace the stub `evaluate`: emit the Blocking path-contract/task-list/mirror findings and `IsInputState` unreadable findings per contracts/skill-checks.md (`NoMirrorDeclared` ⇒ no finding), each carrying `request.EvidenceTag`, normalized `Location.File`, deterministic `Message`; sort by `(skill id, locus, Code)`. Pure/total/no-I/O. Makes T030/T032 pass. Depends on T009.
- [X] T034 [US3] `src/FS.GG.Governance.SkillChecks/Interpreter.fs` — replace the stub: `realPort repo` reads the manifest, resolves a claimed path (and the sensor checks declared bounds), and reads a mirror; `senseSkill` produces `SkillFacts`, catching every exception into `Unreadable` (FR-012, FR-007). Makes T031 pass. Depends on T011/T033.

**Checkpoint**: US1–US3 — package, docs, and skill surfaces are checked; a skill cannot silently break its own path/task/mirror contracts; no-mirror is correctly a non-error.

---

## Phase 6: User Story 4 — Design/rendering: token / capture / contrast / control (Priority: P3, render-fenced)

**Goal**: For a design surface, `DesignChecks.evaluate` reports a missing token/capture/control (`design.token`/`design.capture`/`design.control`) and a sub-threshold contrast pair (`design.contrast` reporting `Ratio` vs `Threshold`, a deterministic numeric compare); all-resolve ⇒ zero findings; an absent/unreadable catalog is an `IsInputState` finding. **The load-bearing contract**: `DesignChecks.Model` + `evaluate` + the `.fsproj` carry **zero** rendering/UI/registry dependency — the catalog is read only by `Interpreter.DesignPort` via `System.IO`/`System.Text.Json` (FR-007, SC-004).

**Independent Test**: Design-catalog fixtures (token/capture/contrast/control) + a surface referencing valid entries ⇒ zero findings; reference a missing token / absent capture / unmapped control / sub-threshold contrast ⇒ the matching `design.*` finding; inspection of the committed surface + `.fsproj` references confirms no rendering/UI/registry dependency (SC-004, acceptance 4.3).

### Tests for User Story 4 ⚠️ (write first, must FAIL before impl)

- [X] T035 [P] [US4] Add committed fixtures under `tests/FS.GG.Governance.DesignChecks.Tests/fixtures/`: real token/capture/control/contrast catalog files (JSON), a design surface referencing only resolving entries, and variants referencing a missing token, an absent capture, an unmapped control, and a sub-threshold contrast pair; plus a `.fsgg` catalog declaring the design surface.
- [X] T036 [P] [US4] `tests/FS.GG.Governance.DesignChecks.Tests/EvaluateTests.fs` — drive `DesignChecks.evaluate` with hand-built `DesignFacts`: all-resolve + `Meets=true` ⇒ zero findings; `Absent entry` on token/capture/control ⇒ Blocking `design.token`/`design.capture`/`design.control` naming the entry; `Meets=false` ⇒ Blocking `design.contrast` reporting ratio vs threshold; `CatalogUnavailable` ⇒ `IsInputState` `design.catalog-unavailable` naming the catalog (contracts/design-checks.md C1/C3). Also assert each emitted finding carries the surface's declared `EvidenceTag`, and `None` when the surface declared none (FR-009, SC-007).
- [X] T037 [P] [US4] `tests/FS.GG.Governance.DesignChecks.Tests/SensorTests.fs` — `Interpreter.senseDesign` over the real catalog fixtures through `realPort`: reads each catalog via `System.IO`/`System.Text.Json`, resolves token/capture/control membership and the contrast ratio/threshold, and records an absent/unreadable catalog in `CatalogUnavailable` (every exception caught, FR-012).
- [X] T038 [P] [US4] `tests/FS.GG.Governance.DesignChecks.Tests/RenderFenceTests.fs` — **the SC-004 guard**: load the `FS.GG.Governance.DesignChecks` reachable assembly surface and assert it references **no** rendering/UI/registry/network API and the pure pack performs no I/O; assert the committed `.fsproj` references only `SurfaceChecks` + `Config` (no Skia/rendering/UI/registry ref). Closes acceptance 4.3 with an automated assertion.
- [X] T039 [P] [US4] `tests/FS.GG.Governance.DesignChecks.Tests/DeterminismTests.fs` — repeated `evaluate` over identical `DesignFacts` ⇒ byte-identical; FsCheck: reordering `Tokens`/`Captures`/`Controls`/`Contrasts` leaves the sorted-by-`(kind, entry id)` findings unchanged (FR-010, SC-005).

### Implementation for User Story 4

- [X] T040 [US4] `src/FS.GG.Governance.DesignChecks/DesignChecks.fs` — replace the stub `evaluate`: emit the Blocking `design.<kind>` and contrast findings and the `IsInputState` catalog-unavailable findings per contracts/design-checks.md, each carrying `request.EvidenceTag`, deterministic `Message`; sort by `(kind, entry id, Code)`. Pure/total/no-I/O, **no rendering reference**. Makes T036/T039 pass. Depends on T010.
- [X] T041 [US4] `src/FS.GG.Governance.DesignChecks/Interpreter.fs` — replace the stub: `realPort repo catalogLayout` reads the four catalogs via `System.IO`/`System.Text.Json` only; `senseDesign` resolves each fact and catches every exception into `CatalogUnavailable` (FR-012, FR-007). Makes T037 pass; keeps the render fence (T038) green. Depends on T011/T040.

**Checkpoint**: US1–US4 — all four deterministic domains are implemented; the design domain is checked with the rendering dependency fenced into the sensor (verified by inspection).

---

## Phase 7: Integration — `Composition` dispatch + additive `fsgg verify` surfacing (FR-008, SC-008, D8)

**Purpose**: Implement the real pure dispatcher and surface the findings through `fsgg verify` additively (byte-identical when empty). Depends on all four packs (US1–US4).

### Tests ⚠️ (write first, must FAIL before impl)

- [X] T042 [P] `tests/FS.GG.Governance.SurfaceChecks.Tests/CompositionTests.fs` — `requestsOf` over a report with one package + one docs + one skill classification ⇒ exactly three requests, each with the correct `Domain` and the surface's declared `EvidenceTag`; a report of only boundary classes (`Routine`/`GovernedRoot`/`ProtectedSurface`/`GeneratedView`/`ReleaseSurface`/`SampleAppSurface`/`GeneratedProductRoot`) ⇒ `[]`; `domainOf` maps the four product classes correctly and everything else to `None` (contracts/surface-check-finding.md C2, acceptance 1/2).
- [X] T043 [P] `tests/FS.GG.Governance.SurfaceChecks.Tests/OrderIndependenceTests.fs` — a bundle whose facts produce package + docs + skill findings ⇒ `Composition.run` yields exactly three independent groups; FsCheck: shuffling `report.Classifications` and the bundle maps yields byte-identical output (sorted by `(Surface id, CheckDomain ordinal, File, Detail, Code)`) (FR-008, SC-008, C3, acceptance 3).
- [X] T044 [P] `tests/FS.GG.Governance.VerifyJson.Tests/SurfaceChecksEmbedTests.fs` — `ofVerifyResultWithSurfaceChecks … []` is **byte-identical** to `ofVerifyResult` on the same inputs (existing golden untouched); a non-empty findings list emits a `surfaceChecks` array sorted in `Composition.run` order, each element `{ domain, surface, code, file, detail, severity, inputState, evidenceTag?, message }` with `evidenceTag` omitted when `None`, no abs-path/clock/username, `schemaVersion` unchanged (contracts/verify-json-surfacechecks.md C1/C2).
- [X] T045 [P] `tests/FS.GG.Governance.VerifyCommand.Tests/SurfaceChecksE2ETests.fs` — real-filesystem `fsgg verify` via `Interpreter.run`: a repo with **no** declared product surfaces ⇒ `verify.json` is byte-identical to the pre-F24 golden (additive section omitted, every existing golden untouched); a repo with a package surface whose baseline drifted ⇒ `verify.json` gains a `surfaceChecks` entry `package.baseline-drift` with the declared `evidenceTag`, and the exit code reflects the Blocking finding at `RunMode.Verify`; a repo whose only surface finding is advisory ⇒ the entry appears with `"severity":"advisory"` and the exit code is unchanged from a clean run (contracts/verify-json-surfacechecks.md C3, acceptance 1–3). **Landed in `067-verify-surface-checks-wiring`** (the host-edge slice): `SurfaceChecksE2ETests.fs` + `SurfaceRollupTests.fs`, real package sensor over a real temp tree; advisory case disclosed-synthetic (the real docs sensor doesn't yet emit `docs.example-freshness`).

### Implementation

- [X] T046 `src/FS.GG.Governance.SurfaceChecks.Dispatch/Composition.fs` — replace the stub: `domainOf` maps `PackageSurface→PackageDomain`, `DocsSurface→DocsDomain`, `SkillSurface→SkillDomain`, `DesignSurface→DesignDomain`, all other classes ⇒ `None`; `requestsOf` builds one request per applicable classification, looking up `EvidenceTag` from `facts.Capabilities.Surfaces` by id; `run` dispatches each request to its pack's `evaluate` over the bundle's facts for that `SurfaceId` (absent ⇒ contributes nothing), aggregates, and sorts by `(Surface id token, CheckDomain ordinal, Location.File, Location.Detail, Code)`. Pure/total/no-I/O; empty report/bundle ⇒ `[]`. Makes T042/T043 pass. Depends on T021/T027/T033/T040.
- [X] T047 `src/FS.GG.Governance.VerifyJson/VerifyJson.fsi` + `VerifyJson.fs` — add the overload `ofVerifyResultWithSurfaceChecks (existing params) -> findings: SurfaceFinding list -> string` that emits the additive `surfaceChecks` array **only when non-empty** via the existing hand-driven `Utf8JsonWriter` walk (ReleaseJson/RouteJson precedent), with exhaustive `severity`/`domain` token helpers (no wildcard); `ofVerifyResult` stays byte-identical, `schemaVersion` unchanged. Makes T044 pass. (VerifyJson surface baseline re-blessed in Phase 9.)
- [X] T048 `src/FS.GG.Governance.VerifyCommand/Interpreter.fs` + `Loop.fs` — at the edge `Interpreter` (never inside a pure `update`), after classification run the applicable `Interpreter.sense<Domain>` sensors to fill a `DomainFactBundle`, call `Composition.run facts report bundle`, thread the `SurfaceFinding list` into `Model` + `renderText`, fold the findings into the existing rollup at `RunMode.Verify` via `Model.enforcementInputOf`/`deriveEffectiveSeverity` (no truth-table change), and pass them to `ofVerifyResultWithSurfaceChecks` at the persist effect; empty ⇒ byte-identical output. Add the `SurfaceChecks`/`SurfaceChecks.Dispatch`/four-domain project references to `VerifyCommand.fsproj`. Makes T045 pass. Depends on T046/T047. **Landed in `067`**: a new `SenseSurfaces` effect + `SurfacesSensed` msg + `Model.SurfaceFindings`/`SurfacesPending` + a read-only `Ports.SenseSurfaces`; the package domain is wired through a **read-only** port (`WriteBaseline` no-op, `ListTranscripts ⇒ Ok []` — FR-012); projection via the already-present `ofVerifyDecisionWithPreview` findings param; VerifyCommand surface baseline re-blessed for the additive growth.

**Checkpoint**: The four packs compose deterministically and order-independently; `fsgg verify` surfaces the findings additively at `RunMode.Verify`, byte-identical when empty, enforcement/exit-codes unchanged.

---

## Phase 8: User Story 5 — Judgement-heavy checks stay advisory (Priority: P3)

**Goal**: A judgement-heavy check sets `BaseSeverity = Advisory`; `deriveEffectiveSeverity` (reused verbatim) guarantees a base-Advisory `SurfaceFinding` never escalates to Blocking under any `(RunMode, Profile)` pair, so a run whose only findings are advisory passes the gate. No new machinery — the guarantee is a test over the existing enforcement reuse (the docs example-freshness advisory boundary in C3 is the first producer).

**Independent Test**: Produce an advisory `SurfaceFinding` on a fixture; across every `(RunMode, Profile)` pair `deriveEffectiveSeverity` returns Advisory; a `fsgg verify` run whose only surface findings are advisory keeps a clean-run exit code (SC-006).

- [X] T049 [P] [US5] `tests/FS.GG.Governance.SurfaceChecks.Tests/AdvisoryMatrixTests.fs` — for a `SurfaceFinding` with `BaseSeverity = Advisory`, assert `Model.enforcementInputOf` + `deriveEffectiveSeverity` returns Advisory across **every** `(RunMode, Profile)` pair; a mix of advisory + Blocking ⇒ only the Blocking can change the verdict; a run of only advisory ⇒ the verdict is unblocked (FR-011, SC-006, contracts/surface-check-finding.md C4). Verifies against real `Enforcement`, never mocked.
- [X] T050 [P] [US5] `tests/FS.GG.Governance.DocsChecks.Tests/AdvisoryBoundaryTests.fs` — assert the docs example-freshness advisory **produced in T027** is labelled `BaseSeverity = Advisory` and is distinguishable from a deterministic (Blocking) finding (acceptance 5.1); no `DocsChecks.fs` edit here (the advisory is authored once in T027, avoiding a cross-phase same-file change). Depends on T027.

**Checkpoint**: All five stories — advisory findings inform but never block under any mode/profile; the deterministic checks land without judgement ambiguity leaking into blocking verdicts.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Bless the new surface baselines, the verify golden, run the neutrality/render-fence/standalone/reuse guards, update docs, and run the quickstart validation.

- [X] T051 Bless and commit the six new surface baselines (`BLESS_SURFACE=1 dotnet test …`), then re-run drift green: `surface/FS.GG.Governance.SurfaceChecks.surface.txt`, `…SurfaceChecks.Dispatch.surface.txt`, `…PackageChecks.surface.txt`, `…DocsChecks.surface.txt`, `…SkillChecks.surface.txt`, `…DesignChecks.surface.txt`; re-bless `surface/FS.GG.Governance.VerifyJson.surface.txt` **only** for the new overload (existing signature unchanged) and `…VerifyCommand.surface.txt` **only if** its `Model`/`Loop` surface changed (T048).
- [X] T052 [P] Commit the pre-F24 `verify.json` golden used by T044/T045's empty case (byte-identity anchor) under `tests/FS.GG.Governance.VerifyCommand.Tests/golden/` (or reuse the existing verify golden if present), and a non-empty `surfaceChecks` golden, generated from the stable `ofVerifyResultWithSurfaceChecks`. **Landed in `067`**: `goldens/verify-no-surfaces.json` (empty anchor, also cross-checked against the genuine `VerifyJson.ofVerifyDecision` projection) and `goldens/verify-surfacechecks.json` (non-empty: a passing gate but `verdict:blocked` driven solely by the folded surface finding).
- [X] T053 [P] `tests/FS.GG.Governance.SurfaceChecks.Tests/NeutralityTests.fs` — the product-neutrality guard: no product/surface/path identity is hardcoded in the shared core, the dispatcher, or any pack; drive `Composition.run` with two fixtures carrying different invented surface ids/paths and assert the findings reflect the input verbatim, with no string from the spec's example catalogs unless the fixture supplied it (FR-007). **Explicitly assert `DesignFacts`/`DesignChecks` hardcode no token/capture/control/contrast identity** (constitution "the repo does not own design-system choices, controls, themes" — every design entry is caller-supplied via the catalog, never a literal in the pack).
- [X] T054 [P] Update `CLAUDE.md` and the M8 roadmap row: F24 `024-package-docs-skills-design-checks` complete (four deterministic adapter rule packs — package `.fsi`-baseline drift + FSI transcripts, docs link/reference currency, skill path/task/mirror, design token/capture/contrast/control — plus the shared `SurfaceChecks` core + `Dispatch` composition, surfaced additively through `fsgg verify`; advisory checks stay advisory); note F23's "declared evidence tag, no check" gap is now closed for these four domains (FR-015), and that the data-model's `Composition` single-core micro-decision was resolved to a `Dispatch` split to break the project cycle.
- [X] T056 [P] **Standalone, no-monorepo guard (FR-016, SC — closes the C1 gap).** `tests/FS.GG.Governance.SurfaceChecks.Tests/StandaloneTests.fs` (+ a no-monorepo fixture under each sensor's `fixtures/` as needed) — check out a generated product **standalone** (paths resolve only within the product root, the F014 per-directory `Loader` reads only the `.fsgg` parent) and run each domain's `Interpreter.sense<Domain>` + `evaluate`: every sensor reads **only** under the loader root and completes using the product's own declared sources, with **no** read escaping the product root (the new I/O sensors are *not* automatically standalone-safe — this asserts it, the F23 `StandaloneTests` precedent). A declared source resolvable only via a monorepo `..` escape ⇒ a clear `IsInputState` input diagnostic naming the source, never a fabricated pass (FR-012/FR-016). Depends on the four sensors (T022/T028/T034/T041).
- [X] T057 [P] **No-new-vocabulary guard (FR-013, FR-014 — closes the G1 gap).** `tests/FS.GG.Governance.SurfaceChecks.Tests/ReuseGuardTests.fs` — assert this row introduces **no** new `SurfaceClass` case, **no** `capabilities.yml` schema-version/field change, and **no** new `DiagnosticId`: `Composition.domainOf` maps **only** the existing F23 product classes (`PackageSurface`/`DocsSurface`/`SkillSurface`/`DesignSurface`) and every other existing class to `None` (a future `SurfaceClass` is a compile error via the exhaustive match, not a silent remap); and the `verify` rollup reuses `deriveEffectiveSeverity` verbatim (no truth-table constant added). Reuse-only, asserted by inspection of the loaded surfaces + `domainOf` totality.
- [X] T058 Run the `quickstart.md` validation end to end (all six scenarios + the constitution-gate checks): build clean (warnings-as-errors); the five focused test projects + the whole solution green (no regression); the six surface baselines match; the design render-fence inspection shows zero rendering/registry dependency; the standalone guard (T056) shows no sensor read escapes the product root; a real-host `fsgg verify` smoke run shows byte-identical output on the empty case and an additive `surfaceChecks` section on the non-empty case. Record the evidence on this line.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no deps; T001/T002/T003/T004 parallel (T003 needs T001/T002 to reference), T005 after them.
- **Foundational (Phase 2)** → after Setup. T006 first (shared vocabulary); T007–T010 after T006 in parallel; T011a–d each after its own `Model` (T007–T010 respectively), in parallel; T012 after T011a–d; T013 after T006–T012; T014/T015 after T013. **Blocks all stories.**
- **US1 (Phase 3)** → after Foundational. MVP. (`PackageChecks.fs` evaluate, then `Interpreter.fs` sensor.)
- **US2 (Phase 4)** → after Foundational; independent of US1 (different library).
- **US3 (Phase 5)** → after Foundational; independent of US1/US2.
- **US4 (Phase 6)** → after Foundational; independent of US1–US3.
- **Integration (Phase 7)** → after all four packs (US1–US4); `Composition.run` dispatches to every `evaluate`, the verify wiring runs every sensor.
- **US5 (Phase 8)** → after Foundational for T049 (needs only `SurfaceChecks.Model` + `Enforcement`); T050 after US2's `DocsChecks.fs` (T027).
- **Polish (Phase 9)** → after the desired stories + Integration; T051/T052/T058 need the `.fs` bodies + verify wiring stable; **T056 (standalone)** needs the four sensors (T022/T028/T034/T041); **T057 (reuse guard)** needs `Composition.domainOf` (T046).

### Within each story

- Tests first and FAILING, then implementation (Constitution V).
- For every new module, `.fsi` + compiling stub (Phase 2) before the real `.fs`; `Model` (Phase 2) before `evaluate` before `Interpreter`; `Composition.run`/`VerifyJson` before `VerifyCommand` wiring.

### Parallel opportunities

- Phase 1: T001–T004 together (T003/T005 sequence after).
- Phase 2: T007–T010 after T006 in parallel; T011a–d in parallel after their models; T014/T015 in parallel after T013.
- **Once Foundational lands, US1/US2/US3/US4 are fully independent** (four separate libraries with no cross-reference, FR-008) and can be staffed in parallel; each story's `[P]` test tasks run together.
- Phase 7: T042/T043/T044/T045 (tests) in parallel; Phase 9: T052/T053/T054/T056/T057 are independent `[P]` tasks.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL — shared vocabulary + every `Model` + stubs compile) → 3. Phase 3 US1 → **STOP & VALIDATE** (SC-001 baseline drift caught 100%, unchanged ⇒ zero drift, transcript failures named, evidence tied to the declared tag). The package domain detects API drift + broken examples with no other domain implemented.

### Incremental delivery

Setup + Foundational → US1 (package, MVP) → US2 (docs) → US3 (skill) → US4 (design, render-fenced) → Integration (compose + `fsgg verify` surfacing) → US5 (advisory guarantee) → Polish. Each domain is an independent library that adds value without breaking the prior; the additive `verify.json` section keeps every existing golden byte-identical when empty.

### Parallel team strategy

After Foundational, Developer A takes US1 (package + the reusable `.fsi`-baseline/transcript machinery), B takes US2 (docs), C takes US3 (skill), D takes US4 (design). They converge on Integration (Phase 7) once all four packs land; US5 + Polish follow.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- **Reuse, don't reinvent**: `evaluate` consumes caller-supplied facts (no sensing); the host sensors read the real source through an injected port only (the F054 `ReleaseFactsSensing` shape); the package transcript sensor shells FSI through the existing F051/F052 `GateExecution.ExecutionPort` (no `FSharp.Compiler.Service`); enforcement reuses F023 `deriveEffectiveSeverity` verbatim (no truth-table change); evidence ties through the existing `EvidenceCapture`/`EvidenceReuse` machinery. Upstream cores (`Config`/`ProductSurfaces`/`Enforcement`/`GateExecution`) are never mocked in semantic tests (Constitution V).
- **No new vocabulary in the kernel or `Config`** (FR-007, FR-013): all F24 types live in `SurfaceChecks` + the four domain libraries; no new `SurfaceClass`, no `capabilities.yml` schema/field change, no new `DiagnosticId`, no enforcement-truth-table change.
- **Determinism is mandatory** (FR-010, SC-005): every `evaluate` sorts and `normalizePath`s; no clock/abs-path/username/environment in any `Message`/`Code`/`Detail`; a per-domain determinism test (T019/T026/T032/T039) plus the order-independence test (T043) enforce it.
- **Render fence** (FR-007, SC-004): only each `Interpreter` references `System.IO`; `DesignChecks` carries zero rendering/UI/registry dependency, asserted by T038.
- **Elmish/MVU applicability**: every `evaluate` and `Composition.run` is a **pure, total leaf** — no MVU ceremony (the F046/F053 precedent); each `Interpreter` is an edge sensor with an injected port, invoked only at `VerifyCommand`'s `Interpreter`, never inside a pure `update` (T048). Pure transitions are exercised directly (T017/T024/T030/T036/T042/T043/T049); real-interpreter evidence is T018/T025/T031/T037/T045/T055.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document on the task line.

---

## Delivery status (implementation pass — 2026-06-25)

**Delivered with real, passing test evidence (78 F24 tests + the VerifyJson embed tests green):**

- Setup + Foundational (T001–T015): six `src` projects + five test projects wired into the solution; the
  shared `SurfaceChecks.Model`, every domain `Model`, the four `evaluate` packs, the four `Interpreter`
  sensors, and the pure `Composition` dispatcher all build and compose. No reference cycle.
- US1 package (T016–T022): `.fsi` baseline-drift (normalized token diff), first-run baseline-absent,
  transcript currency through the **real** F051 `GateExecution` port (real `dotnet fsi` runs), determinism,
  evidence-tag binding.
- US2 docs (T023–T028), US3 skill (T029–T034), US4 design (T035–T041): pure `evaluate` + real on-disk
  sensors + determinism + the SC-004 render-fence guard (DesignChecks references no rendering/UI/registry).
- Composition (T042/T043/T046): `requestsOf`/`domainOf`/`run` order-independent + deterministic.
- VerifyJson additive overload (T044/T047): `ofVerifyDecisionWithSurfaceChecks` — **byte-identical to
  `ofVerifyDecision` when findings are empty** (the existing VerifyCommand goldens stay green — verified by
  the 44 VerifyCommand tests), the documented element shape when non-empty.
- US5 advisory (T049/T050), neutrality (T053), standalone (T056), reuse guard (T057): all green.
- Surface baselines (T051): six new baselines blessed; `FS.GG.Governance.VerifyJson.surface.txt` re-blessed
  for the one added overload; `VerifyCommand` baseline unchanged.

**Deferred (honest status — not marked `[X]`):**

- **T048 — `fsgg verify` host edge wiring.** Surfacing the F24 findings through `VerifyCommand` requires
  inserting an async edge sense-step between the rollup and the verify-doc projection at **both** projection
  points (the empty-selection short-circuit and the post-gate-execution `projectExecuted`) and folding the
  F24 findings into the `Ship.rollup` `ShipDecision` so a Blocking finding moves the verdict. That is a
  substantial restructuring of a load-bearing command with its own goldens and exit-code/relocation logic;
  it was deferred rather than rushed to avoid destabilizing the working host. The **user-observable JSON
  contract it would emit is already implemented and tested** at the `VerifyJson` layer
  (`ofVerifyDecisionWithSurfaceChecks`).
- **T045 — real-filesystem `fsgg verify` E2E** and **T052 — the verify golden** depend on T048.

**Contract resolutions made during implementation (documented deviations):**

- `PackageChecks.Interpreter.PackagePort` gains a `ListTranscripts` reader (the data-model's `RunTranscript`
  needs a discovered list to drive); `DesignChecks.Interpreter.DesignPort` gains a `ReadDescriptor` reader
  (the surface's referenced entries are read at the sole seam, with `repo` in scope). Both keep the render/
  filesystem fence intact.
- `DocsChecks.Model.DocsFacts` gains an `Examples: ExampleFact list` field so the judgement-heavy
  example-freshness **advisory** (contracts/docs-checks.md C3) is produced once in `DocsChecks.evaluate`
  (asserted by T050). Automated example judgement is out of scope (inherently judgement-heavy); the fact is
  caller-supplied.
- The VerifyJson overload is named `ofVerifyDecisionWithSurfaceChecks` (matching the real base entry point
  `ofVerifyDecision`), not the spec's illustrative `ofVerifyResultWithSurfaceChecks` (there is no
  `ofVerifyResult` in the codebase — the F056 entry point is `ofVerifyDecision`).
