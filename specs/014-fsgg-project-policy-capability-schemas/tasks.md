---
description: "Task list for F014 - 014-fsgg-project-policy-capability-schemas: strict, versioned source schemas for the four `.fsgg` files, deterministic diagnostics, and typed facts."
---

# Tasks: `.fsgg` Project, Policy, Capability, and Tooling Schemas

**Feature branch**: `014-fsgg-project-policy-capability-schemas` (active spec; git branch currently `main`)
**Spec**: [`specs/014-fsgg-project-policy-capability-schemas/spec.md`](./spec.md)
**Plan**: [`specs/014-fsgg-project-policy-capability-schemas/plan.md`](./plan.md)

**Input**: Design documents from `/specs/014-fsgg-project-policy-capability-schemas/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Model.fsi](./contracts/Model.fsi), [contracts/Schema.fsi](./contracts/Schema.fsi), [contracts/Loader.fsi](./contracts/Loader.fsi), [contracts/fsgg-schema.md](./contracts/fsgg-schema.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface + a new runtime dependency). The credible evidence is public-surface testing, not private helpers: the pure `Schema.validate` over real fixture YAML, the `Loader` edge over real fixture directories, determinism and order-independence properties, one malformed fixture per diagnostic id, one accepted fixture per MVP surface class, and a surface-drift check.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations needed; every task matches the feature tier.

**Elmish/MVU (Principle IV)**: **APPLIES, lightly** (research D3). The validation core (`Schema.validate`) is a PURE, TOTAL function and needs no MVU ceremony. The only I/O — reading the four `.fsgg` files and distinguishing absent vs present — is isolated at the edge in `Loader` behind an injected `FileReader` port with a filesystem interpreter. Tasks below keep all decision logic in the pure core and exercise the edge over real directories; a full Elmish `Program` is deliberately not used.

**Synthetic-evidence discipline (Principle V)**: real fixture YAML strings and real fixture directories on the actual filesystem are the default and only evidence; no agent, network, or clock is involved, so no synthetic evidence is anticipated. If any appears it carries `Synthetic` in the test name and a use-site disclosure comment.

**Determinism minimums (FR-012, SC-002)**: every emitted list (domains, path-map entries, surfaces, checks, commands, environment classes, external tools, diagnostics) is sorted by a stable id/normalized-path key before it enters the typed facts; no wall-clock, environment, random, or absolute-host-path value enters any fact.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US3]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional configuration library, its test project, the public contracts, the pinned dependency, and fixture scaffolding so the feature type-checks before behavior lands.

- [X] T001 Add `<PackageVersion Include="YamlDotNet" Version="16.3.0" />` to a new runtime `ItemGroup` in `Directory.Packages.props`, with a comment recording need/scope/owner (parse-to-node only; isolated to `FS.GG.Governance.Config`; kernel stays BCL-only) per the plan's new-dependency justification.
- [X] T002 Create `src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.Config`, with a single `<PackageReference Include="YamlDotNet" />` (version from central management) and no `ProjectReference` (the library does not reference the kernel — research D1).
- [X] T003 Copy `specs/014-fsgg-project-policy-capability-schemas/contracts/Model.fsi` → `src/FS.GG.Governance.Config/Model.fsi`, `contracts/Schema.fsi` → `src/FS.GG.Governance.Config/Schema.fsi`, and `contracts/Loader.fsi` → `src/FS.GG.Governance.Config/Loader.fsi` verbatim as the curated public surface.
- [X] T004 Add `failwith "F014"` stub bodies in `src/FS.GG.Governance.Config/Model.fs`, `src/FS.GG.Governance.Config/Schema.fs`, and `src/FS.GG.Governance.Config/Loader.fs` that satisfy the `.fsi` contracts, in compile order `Model.fs` → `Schema.fs` → `Loader.fs` in the fsproj.
- [X] T005 Create `tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/VSTest packages, `IsPackable=false`, `GenerateProgramFile=false`, and a `ProjectReference` to `src/FS.GG.Governance.Config`.
- [X] T006 [P] Add empty Expecto test modules `tests/FS.GG.Governance.Config.Tests/SchemaTests.fs`, `DiagnosticTests.fs`, `DeterminismTests.fs`, `SurfaceClassTests.fs`, `LoaderTests.fs`, `SurfaceDriftTests.fs`, and `Main.fs` (in compile order; `Main.fs` runs the assembly tests).
- [X] T007 Add `src/FS.GG.Governance.Config` and `tests/FS.GG.Governance.Config.Tests` to `FS.GG.Governance.sln`.
- [X] T008 [P] Create the valid fixture `tests/FS.GG.Governance.Config.Tests/fixtures/valid-complete/.fsgg/{project,policy,capabilities,tooling}.yml` with all four files valid (content per [contracts/fsgg-schema.md](./contracts/fsgg-schema.md)), plus a `valid-reordered/` twin whose domains/surfaces/checks/commands are listed in a different order, and `valid-no-policy/` and `valid-no-tooling/` that omit the respective optional file.
- [X] T009 [P] Create one malformed fixture per diagnostic id under `tests/FS.GG.Governance.Config.Tests/fixtures/malformed-*/`: `duplicate-id` (two `capabilities.yml` `domains` entries sharing an id — the model's form of spec US2 AS1's "two capabilities sharing an id", since a capability id ≡ a domain id), `unknown-field`, `missing-required-field`, `malformed-value`, `missing-schema-version`, `malformed-schema-version`, `unsupported-schema-version`, `path-escapes-root`, `dangling-domain-ref`, `dangling-command-ref` (cross-file), `dangling-default-profile`, `empty-file`, and `missing-required-file` — each with exactly one defect; add a `README.md` mapping each fixture to its `DiagnosticId` and acceptance scenario.
- [X] T010 [P] Create one accepted fixture per MVP surface class under `tests/FS.GG.Governance.Config.Tests/fixtures/surface-*/`: `surface-routine`, `surface-governed-root`, `surface-protected`, `surface-generated-view`, `surface-release`, plus `surface-undeclared-only` (a tree of routine, undeclared files that must yield no surface facts — US3 scenario 3).
- [X] T011 [P] Extend `scripts/prelude.fsx` with an F014 design sketch that references `FS.GG.Governance.Config.Model`/`Schema`/`Loader`, builds a `RawSource` in memory, calls `Schema.validate`, and records the intended typed-fact and diagnostic flow before real bodies land.
- [X] T012 [P] Create `specs/014-fsgg-project-policy-capability-schemas/readiness/README.md` listing required transcripts: an FSI session validating `valid-complete`, a determinism comparison, one diagnostic-per-id run, surface-class classification output, and the surface-baseline drift check. Also include an SC-006 traceability note mapping each downstream question ("what changed", "why a gate would run", "which governed path is unknown") to the typed-fact fields that supply its inputs (path map, surfaces, domains, checks).

**Checkpoint**: `dotnet build src/FS.GG.Governance.Config` and `dotnet test tests/FS.GG.Governance.Config.Tests` compile against stubs; the solution lists the two new projects; YamlDotNet restores.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the typed model, the YAML-node reading + strict-walk machinery, path normalization, schema-version handling, the `RawSource`/`FileSlot` input, and the `Loader` edge — everything US1/US2/US3 build on. **No user-story work begins until this phase is complete.**

- [X] T013 Implement `src/FS.GG.Governance.Config/Model.fs`: all scalar newtypes, closed enums (`Cost`, `EnvironmentClass`, `Maturity`, `SurfaceClass`), identity newtypes, the per-file fact records, `TypedFacts`, `Diagnostic`/`DiagnosticId`/`FsggFile`/`Locator`, `Validation`, and a total `diagnosticIdToken` returning the stable wire token for each id — exactly matching `Model.fsi`.
- [X] T014 Implement a strict YAML-node reading layer inside `src/FS.GG.Governance.Config/Schema.fs` over YamlDotNet `YamlStream`/`YamlNode`: parse a file's content to a root mapping, detect non-mapping roots, detect duplicate keys, and expose a strict-field walk that yields `UnknownField`/`MissingRequiredField`/`MalformedValue` diagnostics with a `Locator` (field path + line where YamlDotNet provides it). No object-graph deserialization (research D2).
- [X] T015 Implement deterministic path normalization in `src/FS.GG.Governance.Config/Schema.fs` (research D5): unify `/`/`\` separators, drop `.` segments, resolve `..` segments, collapse repeats, keep relative to the governed root, preserve case; return either a normalized `GovernedPath` or a `PathEscapesRoot` diagnostic when `..` escapes the root. Pure string logic only — never `Path.GetFullPath` (SC-002/SC-005). Disclose any `mutable` accumulator at the use site.
- [X] T016 Implement `schemaVersion` handling in `src/FS.GG.Governance.Config/Schema.fs`: `supportedSchemaVersion`, and a reader that maps absent → `MissingSchemaVersion`, non-integer → `MalformedSchemaVersion`, and any integer `<> supported` → `UnsupportedSchemaVersion` (`> supported` is "upgrade the tool"; `< supported` is rejected rather than silently accepted — FR-006, no historical versions exist for the MVP).
- [X] T017 Implement `FileSlot`, `RawSource`, and the `validate` skeleton in `src/FS.GG.Governance.Config/Schema.fs`: required/optional dispatch (research D4 — `Project`/`Capabilities` required → `MissingRequiredFile` when `Absent`; `Policy`/`Tooling` optional → facts `None` when `Absent`), `EmptyFile` for a whitespace-only `Present` file, and the rule that ANY invalid present file makes the whole result `Invalid` with NO typed facts (FR-006/FR-015). Per-file parsers are stubbed here and filled in US1/US2.
- [X] T018 Implement the `Loader` edge in `src/FS.GG.Governance.Config/Loader.fs`: the `FileReader` port type, `fileSystemReader` (the only `System.IO` use; missing file → `Ok None`, read failure → `Error`), `readSource` (assemble `RawSource` from the injected reader, never swallowing an `Error` — Principle VI), and `loadAndValidate` (compose `fileSystemReader` + `readSource` + `Schema.validate`). Derive `RawSource.Root` from `fsggParentDir` as the normalization/bounds-check anchor ONLY; never copy it into `TypedFacts` (the emitted `ProjectFacts.GovernedRoot` comes from `project.yml`), so no absolute host path can leak (SC-002/SC-005).
- [X] T019 [P] Add a deterministic list-ordering helper in `src/FS.GG.Governance.Config/Schema.fs` that sorts each emitted collection by its stable id/normalized-path key (ordinal), reused by every per-file parser so re-ordering authored entries cannot change typed facts (FR-012, D8).

**Checkpoint**: the library builds with real Model + edge + skeleton; `validate` returns well-formed `Invalid` results for absent-required/empty files; per-file content parsing is still stubbed.

---

## Phase 3: User Story 1 - Declare a governed product and get typed facts (Priority: P1) 🎯 MVP

**Goal**: a directory with four valid `.fsgg` files validates to deterministic, YAML-free typed facts exposing identity, domains, path map, classified surfaces, checks with per-entry metadata, default profile, command allow-list, environment classes, and timeouts.

**Independent Test**: point `Loader.loadAndValidate` at `fixtures/valid-complete` and assert `Valid` with the full typed-fact set, no raw YAML, and stable ordering.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T020 [P] [US1] In `tests/FS.GG.Governance.Config.Tests/SchemaTests.fs`, add tests that validate `fixtures/valid-complete` and assert `Valid` with: `ProjectFacts` (id, sorted domains, normalized governed root, package surfaces, refs); `PolicyFacts` (sorted profiles, default profile, and the optional `branchPolicy`/`reviewBudget` placeholders round-tripped when present — FR-003); `CapabilityFacts` (sorted domains, path map, surfaces, checks with owner/cost/environment/maturity); and `ToolingFacts` (commands with timeouts, environment classes, external tools).
- [X] T021 [P] [US1] In `SchemaTests.fs`, add a no-leakage test asserting no field of the typed facts contains raw YAML punctuation/fragments or any identifier the schemas do not define (FR-010, SC-005), and a test that `valid-no-policy`/`valid-no-tooling` yield `Valid` with the optional facts `None` (FR-015).
- [X] T022 [P] [US1] Add an FSI transcript in `specs/014-fsgg-project-policy-capability-schemas/readiness/` that loads the built library and validates `valid-complete`, capturing the typed facts (US1 independent test evidence).

### Implementation for User Story 1

- [X] T023 [US1] Implement the `project.yml` parser in `src/FS.GG.Governance.Config/Schema.fs`: strict-walk to `ProjectFacts` (id, domains with within-file uniqueness, normalized governed root + package surfaces, optional refs), using T014–T016 and T019.
- [X] T024 [US1] Implement the `policy.yml` parser in `Schema.fs`: `PolicyFacts` (profiles unique+sorted, default profile, optional `branchPolicy`/`reviewBudget` placeholders) — parsed and validated, not enforced.
- [X] T025 [US1] Implement the `capabilities.yml` parser in `Schema.fs`: `CapabilityFacts` with domains, path-map entries (normalized globs), surfaces (incl. `kind`→`SurfaceClass` mapping, owner, maturity), and checks (domain, optional command, owner/cost/environment/maturity); all lists sorted (T019).
- [X] T026 [US1] Implement the `tooling.yml` parser in `Schema.fs`: `ToolingFacts` with the command allow-list (id, command, timeout, environment), environment classes, and external tool/version expectations; all lists sorted.
- [X] T027 [US1] Implement the valid-case cross-reference resolution + `TypedFacts` assembly in `Schema.fs`: wire the per-file parsers into `validate`, resolving references (deferring the dangling-miss diagnostics to US2) and returning `Valid typedFacts`.

**Checkpoint**: `valid-complete` (and the optional-file variants) validate to the full typed-fact set; US1 is independently testable.

---

## Phase 4: User Story 2 - Reject malformed declarations with actionable diagnostics (Priority: P1)

**Goal**: every malformed-input class named in the spec produces its own distinct, stable, located diagnostic and a non-success result with no typed facts.

**Independent Test**: feed each `fixtures/malformed-*` product and assert the expected `DiagnosticId`, file, locator, and `Invalid` result.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T028 [P] [US2] In `tests/FS.GG.Governance.Config.Tests/DiagnosticTests.fs`, add one test per fixture from T009 asserting `Invalid` carrying the expected `DiagnosticId` with a non-empty `Locator` and `Message`, and asserting NO typed facts are emitted for the rejected declaration (FR-006).
- [X] T029 [P] [US2] In `DiagnosticTests.fs`, add the cross-file dangling-reference test: a `check.command` naming a command absent from (or with `tooling.yml` absent) → `DanglingReference`, not silently dropped (spec cross-file edge case).
- [X] T030 [P] [US2] In `DiagnosticTests.fs`, add a diagnostic-ordering test: a fixture with multiple defects returns diagnostics in deterministic (file, locator, id) order, byte-stable across two runs.

### Implementation for User Story 2

- [X] T031 [US2] Wire `UnknownField`, `MissingRequiredField`, and `MalformedValue` emission from the strict-walk layer (T014) into all four per-file parsers, with accurate `Locator`s.
- [X] T032 [US2] Implement `DuplicateId` detection across domains, surfaces, checks, commands, and profiles (within-file; and capability ids — which are domain ids — across files where uniqueness is required, spec "Duplicate ids across files"), naming both occurrences in the message.
- [X] T033 [US2] Wire the three `schemaVersion` diagnostics (T016) and `EmptyFile`/`MissingRequiredFile` (T017) through every file's entry path.
- [X] T034 [US2] Implement `PathEscapesRoot` emission (T015) for every declared path: governed root, package surfaces, path-map globs, and surface paths.
- [X] T035 [US2] Implement `DanglingReference` resolution: `PathMapEntry.Capability` and `Check.Domain` against declared domains; `Check.Command` against `ToolingFacts.Commands` (cross-file, including when `tooling.yml` is absent); `PolicyFacts.DefaultProfile` against declared profiles.
- [X] T036 [US2] Implement deterministic diagnostic ordering (sort by file, then locator, then id) and guarantee `validate` returns `Invalid` with NO `TypedFacts` whenever any diagnostic exists.

**Checkpoint**: each malformed fixture returns its expected diagnostic; ordering is stable; no facts leak on failure. US1 + US2 both pass.

---

## Phase 5: User Story 3 - Cover the real product surface shapes with fixtures (Priority: P2)

**Goal**: each MVP surface class is expressible and classifies into its own typed category; routine/undeclared files produce no protected-surface or governed-root fact.

**Independent Test**: validate each `fixtures/surface-*` product and assert the surface's `SurfaceClass`; validate `surface-undeclared-only` and assert no surface facts.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T037 [P] [US3] In `tests/FS.GG.Governance.Config.Tests/SurfaceClassTests.fs`, add a test per surface fixture asserting the surface classifies into the matching `SurfaceClass` (`Routine`, `GovernedRoot`, `ProtectedSurface`, `GeneratedView`, `ReleaseSurface`) with owner/maturity preserved (US3 scenarios 1–2).
- [X] T038 [P] [US3] In `SurfaceClassTests.fs`, add the `surface-undeclared-only` test asserting `Valid` with no `ProtectedSurface`/`GovernedRoot` facts for undeclared files (US3 scenario 3, light-by-default).

### Implementation for User Story 3

- [X] T039 [US3] Confirm/extend the `kind`→`SurfaceClass` mapping in `capabilities.yml` parsing (from T025) covers all five classes with `MalformedValue` for any out-of-set `kind`, and preserves owner/maturity per surface.
- [X] T040 [US3] Ensure undeclared paths emit no surface or governed-root fact (no path-heuristic inference — research D6); add a guard/assertion path so only explicitly-declared surfaces produce facts.

**Checkpoint**: all five surface classes classify correctly; undeclared files stay routine. US1–US3 pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: determinism/order-independence proofs, the edge over real directories, the surface baseline, dependency hygiene, and the quickstart run.

- [X] T041 [P] In `tests/FS.GG.Governance.Config.Tests/DeterminismTests.fs`, add: validate `valid-complete` twice and assert structural equality of the whole `Validation` (SC-002); and an FsCheck property that permuting authored domains/surfaces/checks/commands (the `valid-reordered` shape) yields identical typed facts (FR-012).
- [X] T042 [P] In `tests/FS.GG.Governance.Config.Tests/LoaderTests.fs`, exercise the `Loader` edge over real fixture directories: `loadAndValidate` on `valid-complete`; an absent optional file → `Valid`/`None`; a present-but-invalid optional file → `Invalid`; and a `FileReader` `Error` surfaced (not swallowed). Distinguish absent vs invalid (FR-015). Also assert host-path independence: loading the same `valid-complete` content from two different absolute parent directories yields an identical `Validation` (no absolute host path leaks into the facts — SC-002/SC-005, I3).
- [X] T043 Generate `surface/FS.GG.Governance.Config.surface.txt` from the built `FS.GG.Governance.Config` assembly using the repo's surface-baseline tooling/convention, then add `tests/FS.GG.Governance.Config.Tests/SurfaceDriftTests.fs` asserting the built surface matches the baseline (Principle II).
- [X] T044 [P] In `SurfaceDriftTests.fs` (or a dedicated module), add a dependency-hygiene test asserting `FS.GG.Governance.Config` references only `YamlDotNet` (+ FSharp.Core) and not the kernel/host/adapters (research D1, plan Engineering Constraints). This test doubles as the FR-016 scope guard: the absence of git/CI, routing, gate-registry, and enforcement dependencies confirms no later-phase capability leaked into this feature.
- [X] T045 [P] Run [quickstart.md](./quickstart.md) end-to-end and record the transcripts named in `readiness/README.md` (valid → facts, determinism, diagnostic-per-id, surface classes, absent-vs-invalid), plus the SC-006 traceability note confirming the typed facts carry every input the three downstream questions require.
- [X] T046 [P] Update `README.md` to list the new optional `FS.GG.Governance.Config` library and link the `.fsgg` schema contract ([contracts/fsgg-schema.md](./contracts/fsgg-schema.md)).

**Checkpoint**: full `dotnet test` green; surface baseline committed and drift-checked; determinism and edge proven; quickstart validated.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundation (Phase 2)**: depends on Setup — BLOCKS all user stories.
- **User Stories (Phases 3–5)**: all depend on Foundation. US1 and US2 are co-equal P1; US2's diagnostic emission builds on the per-file parsers landed in US1 (run US1 then US2, or share a developer). US3 depends on the `capabilities.yml` parser from US1 (T025).
- **Polish (Phase 6)**: depends on all user stories.

### Within Each User Story

- Tests are written first and must FAIL before implementation.
- `.fsi` contract (Phase 1) → FSI sketch (T011) → semantic tests → implementation (Principle I).
- Model/edge/skeleton (Foundation) before per-file parsers (US1) before diagnostics (US2).

### Parallel Opportunities

- Setup fixtures T008–T012 are all `[P]`.
- All US1 tests (T020–T022), all US2 tests (T028–T030), and both US3 tests (T037–T038) are `[P]` within their story.
- Most Polish tasks (T041, T042, T044, T045, T046) are `[P]`; T043 must precede T044 only if they share a file.
- The four per-file parsers in US1 (T023–T026) touch the same `Schema.fs` and are therefore sequential, not `[P]`.

---

## Suggested MVP Scope

**User Story 1 + User Story 2** (both P1) together are the MVP: a product can write the four `.fsgg` files and either confirm a valid declaration with typed facts (US1) or learn exactly what is wrong (US2). US3 (P2) proves the surface vocabulary covers the MVP shapes and can follow as a fast second increment.

## Task Count

- Setup: 12 (T001–T012)
- Foundation: 7 (T013–T019)
- US1 (P1): 8 (T020–T027) — 3 tests, 5 implementation
- US2 (P1): 9 (T028–T036) — 3 tests, 6 implementation
- US3 (P2): 4 (T037–T040) — 2 tests, 2 implementation
- Polish: 6 (T041–T046)
- **Total: 46 tasks**
