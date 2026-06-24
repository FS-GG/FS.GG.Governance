---
description: "Task list for Release-Facts Sensing for the Repository Boundary (F054)"
---

# Tasks: Release-Facts Sensing for the Repository Boundary

**Input**: Design documents from `/specs/054-release-facts-sensing/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅ (D1–D8), data-model.md ✅,
contracts/Model.fsi ✅, contracts/Sensing.fsi ✅, contracts/Interpreter.fsi ✅, quickstart.md ✅

**Tier**: **Tier 1 (contracted change)** — adds a new public library (`FS.GG.Governance.ReleaseFactsSensing`)
with three curated `.fsi` modules and a new surface baseline. It introduces a new library but **no** new
third-party dependency, **no** schema, **no** schema-version bump, and **no** edit to any frozen merged core
(F053 `ReleaseRules`, F016 `Snapshot`, F014 `Config`, every other core) or golden baseline. All tasks share the
feature tier; no per-task `[T1]`/`[T2]` annotations needed. Tests are **mandatory** (Principle V).

**Elmish/MVU**: **Applicable, honored via the injected sensing-port boundary** (Principle IV — plan Constitution
Check). This feature **is** an I/O-bearing sense, so it does not get the pure-function exemption F053 had.
Instead it follows the established **sensing-port** discipline (F016 `Snapshot`, FreshnessSensing), **not** a
full `Program`/`Msg`/`update` loop: I/O is represented as data behind a single injected `RepositoryPort` (the
effect contract), the derivation `Sensing.deriveFacts` is **pure**, and interpretation (`realPort`/`gather`/
`senseRelease`) happens **only at the edge** (`Interpreter`). A full MVU loop is unwarranted — single-shot
sense, no durable state, no retries, no user interaction, no background work (research D2). Semantic tests cover
both sides of the boundary: pure-derivation tests over hand-built `RecoveredEvidence`, and interpreter tests
running `realPort` against a **real** temp fixture repository (Principle V). Principle VI is satisfied **by
construction**: an absent/unreadable/unparseable source or an absent expectation becomes the explicit
`Unrecoverable` `FactState` with a `SensingDiagnostic` naming the family + reason (FR-004) — never a throw, a
swallowed exception, or a fabricated `Met`.

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel. Stories map to
spec user stories — US1 (P1, headline MVP) sense the per-family `FactState` from a governed repository and hand
the `Facts` straight to F053; US2 (P2) surface the observed-evidence `ReleaseSnapshot`; US3 (P3) fail-safe,
deterministic, network-free guarantees. The whole feature is one small new library plus its test project — no
host wiring, no schema, no document, and no edit to any frozen core.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]`…`[US3]` traceability; unlabeled = shared infrastructure
- Exact repo-root-relative file paths in every description

---

## Phase 1: Setup (the new sensing library skeleton, no behavior)

**Purpose**: Create the new library + its focused test project so everything compiles and the solution restores.
No semantics yet. Nothing existing is edited beyond the solution file and the `CLAUDE.md` plan pointer.

- [X] T001 Create `src/FS.GG.Governance.ReleaseFactsSensing/FS.GG.Governance.ReleaseFactsSensing.fsproj` —
  SDK-style, `net10.0`, `RootNamespace`/`PackageId` `FS.GG.Governance.ReleaseFactsSensing`, `Version` `0.1.0`,
  `IsPackable=true` (matching the optional packable cores — `ReleaseRules`/`Snapshot`/`Config`). `<Compile>`
  order **`Model.fsi`, `Model.fs`, `Sensing.fsi`, `Sensing.fs`, `Interpreter.fsi`, `Interpreter.fs`**.
  `<ProjectReference>`s (and **only** these — plan Primary Dependencies, research D5): `../FS.GG.Governance.
  ReleaseRules/...` (F053 — `ReleaseRuleKind`, `FactState`, `ReleaseFacts`, `releaseRuleKindOrdinal`/
  `releaseRuleKindToken`) and `../FS.GG.Governance.Config/...` (F014 — `SurfaceId`). **No** reference to
  `Snapshot`, `Route`, `Gates`, or any hosting/registry SDK (the feature only *mirrors* the sensing shape, it
  does not consume `Snapshot` — plan Structure Decision). **No** third-party `PackageReference` (FR-007). Header
  comment: the release-facts sensing — a single injected `RepositoryPort` edge + a pure per-family derivation —
  layered on top of the merged thread (heavier capabilities layer on top, not into the core).
- [X] T002 [P] Create `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/FS.GG.Governance.ReleaseFactsSensing.
  Tests.fsproj` — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s `Expecto`,
  `Expecto.FsCheck`, `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from
  `Directory.Packages.props`, no new package). `<ProjectReference>`s to the new `FS.GG.Governance.
  ReleaseFactsSensing` **and**, for constructing the reused primitives + the F053 hand-off in fixtures,
  `FS.GG.Governance.ReleaseRules` (`ReleaseRuleKind`/`FactState`/`Release.evaluate`) and `FS.GG.Governance.
  Config` (`SurfaceId`). `<Compile>` order is `Support.fs`, then the per-story test files added by the tasks
  that create them, then `SurfaceDriftTests.fs`, then `Main.fs`; at this step wire **only** `Support.fs` (T011)
  and `Main.fs` (T012). Mirror `tests/FS.GG.Governance.Snapshot.Tests/...Tests.fsproj`.
- [X] T003 Add both new projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders) with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching existing entries.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` at `specs/054-release-facts-sensing/plan.md`
  (verify; no other doc changes).

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; `dotnet sln list` shows both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts + compiling stubs + FSI proof + test scaffolding) — BLOCKS all stories

**Purpose**: Drop every new public surface (Principle I — contracts before any `.fs` body), define the pure data
vocabulary, make the whole solution compile with the derivation + edge functions **stubbed**, prove the surface
in FSI (Principle I), and stand up the test scaffolding so tests can FAIL before implementation. **⚠️ No story
work begins until this phase is complete.**

- [X] T005 Author `src/FS.GG.Governance.ReleaseFactsSensing/Model.fsi` — drop `contracts/Model.fsi` **verbatim**:
  `namespace FS.GG.Governance.ReleaseFactsSensing`; the two `open`s (`FS.GG.Governance.Config.Model`,
  `FS.GG.Governance.ReleaseRules.Model`); the `[<CompilationRepresentation(...ModuleSuffix)>] module Model` with
  `ReleaseExpectations`, `SourceLayout`, the four evidence records (`VersionEvidence`, `MetadataEvidence`,
  `PinsEvidence`, `PostureEvidence`), `RecoveredEvidence`, the four `*Fact` records (`VersionFact`,
  `MetadataFact`, `PinsFact`, `PostureFact`), `SensingDiagnostic`, `ReleaseSnapshot`, and `SensedRelease`, each
  carrying its curated doc-comment verbatim. Reuses F053 `ReleaseFacts`/`FactState`/`ReleaseRuleKind` and F014
  `SurfaceId` verbatim; introduces **no new** fact or family vocabulary. **No** access modifiers (Principle II).
- [X] T006 Add `src/FS.GG.Governance.ReleaseFactsSensing/Model.fs` — the `module Model` with all the types
  **fully defined** (these are data, not behavior, so no stub). Same two `open`s as the `.fsi`. No access
  modifiers. (`Model` precedes `Sensing` precedes `Interpreter` in compile order.)
- [X] T007 Author `src/FS.GG.Governance.ReleaseFactsSensing/Sensing.fsi` — drop `contracts/Sensing.fsi`
  **verbatim**: `namespace FS.GG.Governance.ReleaseFactsSensing`; the two `open`s
  (`FS.GG.Governance.ReleaseRules.Model`, `FS.GG.Governance.ReleaseFactsSensing.Model`); the
  `[<CompilationRepresentation(...ModuleSuffix)>] module Sensing` with the two members — `releaseFamilies:
  ReleaseRuleKind list` and `deriveFacts: expectations:ReleaseExpectations -> recovered:RecoveredEvidence ->
  SensedRelease` — each with its curated doc-comment verbatim. **No** access modifiers (the per-family
  classifiers — version dotted-numeric "bumped past", metadata containment, pin resolution, posture subset — and
  the snapshot builders stay unexported by absence here).
- [X] T008 Add `src/FS.GG.Governance.ReleaseFactsSensing/Sensing.fs` — the `module Sensing` satisfying
  `Sensing.fsi`. `releaseFamilies` **may be fully defined now** (the six `ReleaseRuleKind` in declaration order,
  `VersionBump .. Provenance`, used by tests and the derivation). This declaration order **is** the
  `releaseRuleKindOrdinal` order, so the `releaseFamilies` list, the `deriveFacts` per-family iteration, and the
  diagnostics ordering are one and the same; define `releaseFamilies` consistently with `releaseRuleKindOrdinal`
  (e.g. ordered by it) so the two cannot diverge. `deriveFacts` is a `failwith "not implemented"`
  stub that type-checks the full signature (real body lands in Phases 3–4). No access modifiers (Principle II).
  Confirm `dotnet build src/FS.GG.Governance.ReleaseFactsSensing/...` is clean under `TreatWarningsAsErrors`.
- [X] T009 Author `src/FS.GG.Governance.ReleaseFactsSensing/Interpreter.fsi` + add a stubbed `Interpreter.fs` —
  drop `contracts/Interpreter.fsi` **verbatim**: `namespace FS.GG.Governance.ReleaseFactsSensing`; `open
  FS.GG.Governance.ReleaseFactsSensing.Model`; the `[<CompilationRepresentation(...ModuleSuffix)>] module
  Interpreter` with `RepositoryPort` (the six read functions), `realPort: repoDir:string -> layout:SourceLayout
  -> RepositoryPort`, `gather: port:RepositoryPort -> RecoveredEvidence`, and `senseRelease: port:RepositoryPort
  -> expectations:ReleaseExpectations -> SensedRelease`, each with its curated doc-comment verbatim. In
  `Interpreter.fs` define the `RepositoryPort` record and stub `realPort`/`gather`/`senseRelease` with `failwith
  "not implemented"` type-checking the full signatures (real bodies land in Phases 3 + 5). **No** access
  modifiers (the per-source file readers/parsers and the exception-reifying gather helper stay unexported by
  absence from the `.fsi`). Confirm the whole library builds clean under `TreatWarningsAsErrors`.
- [X] T010 Append an F054 design-first section to `scripts/prelude.fsx` (after the F053 section, ~line 2680) —
  the Principle-I FSI proof **before** any operation body lands (the `quickstart.md` "Exercise in FSI" sketch
  verbatim): `#r` the new `ReleaseFactsSensing` Debug DLL plus the `Config`/`ReleaseRules` DLLs; build the
  product-neutral `ReleaseExpectations`; build an all-satisfying fake `RepositoryPort`; assert exactly six
  families + all `Met` via `senseRelease`; feed `sensed.Facts` straight to `Release.evaluate` and assert one
  finding per family (SC-001); the not-bumped version ⇒ `Unmet` with the other five `Met`; the missing-metadata
  field ⇒ `Unmet` + snapshot names the missing field; the absent source (port `Error`) ⇒ `Unrecoverable`; and
  the determinism check (two `senseRelease` calls structurally equal). Its assertions over `senseRelease`/
  `deriveFacts` fail against the stubs — expected.
- [X] T011 [P] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/Support.fs` — real, literally-
  constructible builders (Principle V; **no mocks**): a `ReleaseExpectations` builder; a default all-satisfying
  fake `RepositoryPort` plus `{ port with Read… = … }` helpers for the per-family violation/absence fixtures;
  hand-built `RecoveredEvidence` builders (for the pure-core tests, no disk); a `withTempDir` helper writing the
  six neutral fixture files + a `SourceLayout` over them (the F016 Snapshot / FreshnessSensing `withTempDir`
  precedent, for the `realPort` edge tests); a `repoRoot` finder for the surface-baseline path; an F053 rule-set
  builder (`releaseFamilies |> List.map (fun k -> { Kind = k; … })`) for the hand-off assertion; and FsCheck
  generators for arbitrary `ReleaseExpectations` × `RecoveredEvidence`. No network, no registry, no publishing
  provider (SC-004).
- [X] T012 [P] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching `tests/FS.GG.Governance.Snapshot.Tests/Main.fs`.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the `ReleaseFactsSensing` test project compiles
with only `Support.fs` + `Main.fs` wired; `dotnet fsi scripts/prelude.fsx` loads the F054 section (its
`senseRelease`/`deriveFacts` assertions fail against the stubs — expected). The first failing semantic test
lands in Phase 3.

---

## Phase 3: User Story 1 — Sense the per-family release fact state from a governed repository (Priority: P1) 🎯 MVP

**Goal**: Implement the pure `Sensing.deriveFacts expectations recovered` so that, for each of the six families
in `releaseRuleKindOrdinal` order, it classifies exactly one `FactState` — `Met` only when recovered evidence
satisfies the caller's declared expectation, `Unmet` when recovered-but-unsatisfied, `Unrecoverable` when the
evidence is `Error` **or** the caller declared no expectation for it (fail-safe, never a fabricated `Met`) — and
assembles `Facts.States` as a `Map.ofList` over **all six** `(kind, state)` pairs. The output `Facts` IS the
F053 `ReleaseFacts`, handed straight to `Release.evaluate` with no adaptation. This is the irreducible core of
the row — without a real repository turned into the typed facts, the F053 core can only run against fixtures.

**Independent Test**: Build a fixture repository whose six families are in a known state (some satisfying, some
violating their expectation), sense the facts against the matching declared expectations, and assert exactly one
fact state per family, each classification correct, and that `sensed.Facts` is accepted by `Release.evaluate`
with no further adaptation (one finding per declared rule).

### Tests for User Story 1 (write first; must FAIL against the Phase-2 stub) ⚠️

- [X] T013 [P] [US1] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/DeriveFactsTests.fs` —
  `DeriveFactsTests` (US1 acc. 1–2, FR-001/FR-003/FR-009, data-model §derivation): over hand-built
  `RecoveredEvidence` (no disk — the pure-core unit tests), an all-satisfying expectation+evidence set ⇒ all six
  families `Met` and `Facts.States.Count = 6`; a not-bumped version (`Declared` equals `VersionBaseline`) with
  the other five satisfied ⇒ `VersionBump = Unmet` and the other five `Met`; an all-violating set (each family's
  recovered evidence fails its expectation) ⇒ each family `Unmet`. **Pin `releaseFamilies` directly**: it equals
  the six `ReleaseRuleKind` in declaration order (`VersionBump .. Provenance`), all six distinct, count 6. Add
  `DeriveFactsTests.fs` to the test `.fsproj` `<Compile>` immediately after `Support.fs`. FAILs against the
  `failwith` stub.
- [X] T014 [P] [US1] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/HandoffTests.fs` — `HandoffTests`
  (US1 acc. 3, FR-002, SC-001): take `sensed.Facts` from `Sensing.deriveFacts` (and, once T020 lands, from
  `Interpreter.senseRelease`) over a fixture covering all six families and feed it **unchanged** to the F053
  `Release.evaluate rules sensed.Facts` (rules built from `releaseFamilies`) — assert it type-checks with no
  adaptation and produces exactly one finding per declared rule (`findings.Length = rules.Length`), confirming
  the sensing output is exactly the F053 input shape. Add `HandoffTests.fs` to the test `.fsproj` `<Compile>`
  after `DeriveFactsTests.fs`. FAILs against the stub.

### Implementation for User Story 1

- [X] T015 [US1] Implement `Sensing.deriveFacts` in `src/FS.GG.Governance.ReleaseFactsSensing/Sensing.fs`
  (data-model §derivation, research D6): for each family in `releaseRuleKindOrdinal` order, `match (expectation-
  for-family, recovered-for-family)` — `None, _ ⇒ Unrecoverable + snapshot None + diagnostic "no expectation
  declared for <family>"`; `_, Error reason ⇒ Unrecoverable + None + diagnostic "<family> evidence
  unrecoverable: <reason>"`; `Some e, Ok ev ⇒ if satisfies e ev then Met + Some observed else Unmet + Some
  observed`. `satisfies` per family is the D6 comparison (version dotted-numeric "bumped strictly past"; metadata
  field containment; pin resolution; posture subset), each living **unexported**. Build `Facts.States` via
  `Map.ofList` over all six `(kind, state)` pairs (**always six** — FR-009); build the `ReleaseSnapshot` per-
  family `option` (`Some` exactly when `Met`/`Unmet`); order `Diagnostics` by `releaseRuleKindOrdinal`. Total,
  never throws, pure (no I/O/clock/process — FR-008). The classifiers and snapshot builders live unexported
  (absent from `Sensing.fsi`). No `mutable`, no custom operators, no reflection (Principle III). After T015 the
  US1 pure-core tests (T013) and the `deriveFacts` half of T014 go green.

**Checkpoint**: `deriveFacts` turns a recovered-evidence bundle + declared expectations into exactly six
classified fact states whose `Facts` value feeds the F053 core unchanged — the answer to "what is the current
release state of this repository?" The interpreter edge (`senseRelease` over a real port) lands in Phase 5; the
snapshot detail (US2) in Phase 4. **The pure-core MVP is testable now via a hand-built `RecoveredEvidence`.**

---

## Phase 4: User Story 2 — Surface the observed evidence behind each fact (Priority: P2)

**Goal**: Pin and complete the typed `ReleaseSnapshot` of observed evidence the Phase-3 `deriveFacts` already
emits alongside each fact state — the version observed + the baseline compared against, the present + missing
required metadata fields, the resolved-versus-expected pins (+ drifted template names), the observed publishing
posture, and the observed provenance evidence — so a later finding/projection can name concrete specifics. This
layers on the P1 derivation and consumes the **same** recovered evidence; it adds the snapshot-detail assertions
(and any per-`*Fact` builder completion T015 left minimal).

**Independent Test**: Sense a fixture whose package metadata is missing two required fields and whose template
pins have drifted from the expected set; assert the snapshot reports the specific present/missing metadata
fields and the specific resolved-versus-expected pins, and that those specifics correspond to the `Unmet` fact
states for those two families.

### Tests for User Story 2 (write first; must FAIL until the snapshot detail is complete) ⚠️

- [X] T016 [P] [US2] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/SnapshotTests.fs` — `SnapshotTests`
  (US2 acc. 1–2, FR-005, data-model §snapshot): a fixture missing a required metadata field ⇒
  `Snapshot.Metadata = Some { Present = …; Missing = [ the specific field ] }` (both sorted) **and**
  `PackageMetadata = Unmet`; a fixture whose declared version equals the supplied baseline ⇒ `Snapshot.Version =
  Some { Observed = v; Baseline = v }` (both the version read and the baseline compared) **and** `VersionBump =
  Unmet`; a fixture whose pins drifted ⇒ `Snapshot.Pins = Some { Resolved; Expected; Drifted = [ the drifted
  template names ] }` (key-sorted assoc lists) **and** `TemplatePins = Unmet`; assert each `Unrecoverable`
  family's snapshot field is `None` and is named in `Diagnostics` (ordered by `releaseRuleKindOrdinal`). Add
  `SnapshotTests.fs` to the test `.fsproj` `<Compile>` after `HandoffTests.fs`. FAILs until T017.

### Implementation for User Story 2

- [X] T017 [US2] Complete the snapshot builders in `src/FS.GG.Governance.ReleaseFactsSensing/Sensing.fs`
  (data-model §snapshot, research D7) — for each recovered family build its `*Fact`: `VersionFact { Observed;
  Baseline }`; `MetadataFact { Present = sorted present; Missing = sorted (required \ present) }`; `PinsFact
  { Resolved = key-sorted; Expected = key-sorted; Drifted = sorted (missing-or-mismatched template names) }`;
  `PostureFact { Observed = sorted; Required = sorted; Missing = sorted (required \ observed) }` (reused by the
  publish-plan, trusted-publishing, and provenance families). Set each `ReleaseSnapshot` per-family `option` to
  `Some` for `Met`/`Unmet` and `None` for `Unrecoverable`; carry `expectations.Surface` onto `Snapshot.Surface`;
  order every collection deterministically (D7). The `*Fact` builders live **unexported**. After T017 the US2
  tests go green. (If T015 already emitted complete `*Fact` values, this task is the verification + any sort/
  drifted-set gap; do not duplicate logic.)

**Checkpoint**: the snapshot names concrete present/missing fields, resolved-vs-expected pins (+ drift), observed
posture, and provenance evidence behind each fact — the auditable detail a later finding/projection consumes,
matching the `Unmet`/`Unrecoverable` families.

---

## Phase 5: User Story 3 — Fail-safe, deterministic, network-free sensing (Priority: P3)

**Goal**: Implement the impure **edge** (`realPort`/`gather`/`senseRelease`) and pin the integrity guarantees:
an absent/unreadable/unparseable source ⇒ `Unrecoverable` (never `Met`, never a crash), every run returns all
six families, two senses of identical repository state are structurally identical (compare equal, every collection ordered), and no run
reaches a network/registry/publishing endpoint. The edge is the **only** impure code (FR-006); `gather` reifies
every thrown exception as `Error`; `senseRelease = gather >> deriveFacts`. This story closes the boundary the
future `fsgg release` host row wires, and pins SC-002/SC-003/SC-004.

**Independent Test**: Sense a real temp fixture from which one family's governing source has been removed and
another's corrupted to be unparseable; assert both ⇒ `Unrecoverable` (not `Met`, not a thrown error) with all
six still returned. Sense the identical fixture twice and assert structurally identical `SensedRelease` (compare equal) including every
collection's order. Assert the assembly references no network SDK.

### Tests for User Story 3 (write first; must FAIL against the Phase-2 edge stub) ⚠️

- [X] T018 [P] [US3] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/InterpreterTests.fs` —
  `InterpreterTests` (US3 acc. 1, FR-004/FR-006/FR-009, SC-002, data-model §edge): write a **real** temp fixture
  repository (the `withTempDir` precedent — six neutral files under a temp dir) and `Interpreter.senseRelease
  (realPort tempDir layout) expectations`; the all-satisfying fixture ⇒ all six `Met`; a fixture with one source
  **deleted** and another **corrupted to be unparseable** ⇒ both families `Unrecoverable` (never `Met`, never a
  thrown error) with all six families still present (`Facts.States.Count = 6`); an all-sources-missing fixture ⇒
  all six `Unrecoverable` and a successful result (not a sensing failure — edge case); a port read function that
  **throws** ⇒ `gather` reifies it as `Error` ⇒ that family `Unrecoverable` (never a crash). Add
  `InterpreterTests.fs` to the test `.fsproj` `<Compile>` after `SnapshotTests.fs`. FAILs against the edge stub.
- [X] T019 [P] [US3] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/DeterminismTests.fs` —
  `DeterminismTests` (US3 acc. 2, FR-008/FR-010, SC-003/SC-006, data-model §derivation): two `senseRelease`
  calls over the identical temp fixture are structurally equal (the two `SensedRelease` values compare equal,
  every list/association-list collection in the snapshot in the same fixed order + the diagnostics order); two
  `deriveFacts` calls over identical
  hand-built `RecoveredEvidence` are equal; the output family set (`Facts.States |> Map.toList |> List.map fst |>
  List.sort`) equals `Sensing.releaseFamilies` **sorted** across the satisfied, the all-violating, and the
  all-unrecoverable fixtures (no family dropped, no family fabricated — SC-006); extra unrelated repository
  artifacts are ignored (no fact for an unrecognized family). Add `DeterminismTests.fs` to the test `.fsproj`
  `<Compile>` after `InterpreterTests.fs`. FAILs against the edge stub.

### Implementation for User Story 3

- [X] T020 [US3] Implement the edge in `src/FS.GG.Governance.ReleaseFactsSensing/Interpreter.fs` (data-model
  §edge, research D3/D4): `realPort repoDir layout` builds a `RepositoryPort` whose six read functions read the
  `layout` paths under `repoDir` via BCL `System.IO`, parse the bytes into the structured `*Evidence`, and return
  `Error` on a missing/unreadable/unparseable file (the **only** filesystem touch; no process, no socket, no
  provider SDK — FR-007); `gather port` runs the six read functions **catching any thrown exception** and
  reifying it as `Error` (FR-004) into a `RecoveredEvidence` bundle; `senseRelease port expectations = gather
  port |> Sensing.deriveFacts expectations`. Total and safe — never throws, always all six families. The per-
  source readers/parsers + the exception-reifying gather helper live **unexported** (absent from
  `Interpreter.fsi`). No `mutable`, no custom operators, no reflection (Principle III). After T020 the US3
  interpreter/determinism tests go green and the `senseRelease` half of T014 (US1 hand-off) is exercised end-to-
  end over a real port.

**Checkpoint**: the edge senses a real repository behind one injected port, fails safe to `Unrecoverable` on any
absent/unreadable/throwing source, returns all six families, is structurally identical across runs, and touches only
local files — SC-002/SC-003/SC-006 covered; the network-free guarantee (SC-004) is pinned by the scope guard in
Phase 6.

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope/network guard, additivity, validation)

**Purpose**: Lock the new public surface and bless its baseline (Principle II / Change Classification), prove the
sensing reaches no network (SC-004), prove the change is additive and edits no frozen core (plan §Scale/Scope),
and run the quickstart end-to-end. Bless the baseline only after the surface is final.

- [X] T021 [P] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/SurfaceDriftTests.fs` — a reflective
  `SurfaceDrift` test (the `Snapshot`/`ReleaseRules` precedent) comparing the public surface of the production
  `FS.GG.Governance.ReleaseFactsSensing` assembly byte-for-byte to
  `surface/FS.GG.Governance.ReleaseFactsSensing.surface.txt` with the `BLESS_SURFACE=1` re-bless path (reflection
  lives ONLY in this test); **plus the dependency scope/network guard** (the `FS.GG.Governance.Snapshot.Tests/
  SurfaceDriftTests.fs` precedent) asserting the production assembly's referenced assemblies include **none** of
  `System.Net.Http`/`System.Net.Sockets`/`Octokit`/`GitHub`/`LibGit2Sharp` (SC-004, FR-007), and that it
  references **only** `ReleaseRules`, `Config` (+ their transitive deps + `FSharp.Core`/BCL) and **not**
  `Snapshot`/`Route`/`Gates` or any third-party package (research D5). Add `SurfaceDriftTests.fs` to the test
  `.fsproj` `<Compile>` immediately after `DeterminismTests.fs` and before `Main.fs`.
- [X] T022 [P] [US3] Write `tests/FS.GG.Governance.ReleaseFactsSensing.Tests/PropertyTests.fs` — `PropertyTests`
  (FsCheck, FR-009/SC-006): over random `ReleaseExpectations` × `RecoveredEvidence`, `(deriveFacts exp
  rec).Facts.States.Count = 6` (always all six families, never partial, never fabricated) and the family-key set
  equals `Sensing.releaseFamilies`; every state is one of `Met`/`Unmet`/`Unrecoverable`; an `Error`-recovered or
  `None`-expectation family is **never** `Met` (the no-fabrication invariant, SC-002); `deriveFacts` never throws
  over arbitrary input. Use the Support.fs generators. Add `PropertyTests.fs` to the test `.fsproj` `<Compile>`
  after `SurfaceDriftTests.fs` and before `Main.fs`.
- [X] T023 Generate and commit `surface/FS.GG.Governance.ReleaseFactsSensing.surface.txt` via `BLESS_SURFACE=1
  dotnet test tests/FS.GG.Governance.ReleaseFactsSensing.Tests/...`; review the diff (exactly `Model` — the
  expectations/layout/evidence/`*Fact`/diagnostic/snapshot/`SensedRelease` types; `Sensing` — `releaseFamilies`,
  `deriveFacts`; `Interpreter` — `RepositoryPort`, `realPort`, `gather`, `senseRelease`; **no** leak of the per-
  family classifiers, the snapshot builders, or the per-source readers/parsers). After this T021 runs green
  without `BLESS_SURFACE`.
- [X] T024 [P] Verify additivity (no frozen-core edit, no schema bump, no new dependency) by inspection: `git
  diff` shows **no** edit to any merged core (`FS.GG.Governance.ReleaseRules`, `Snapshot`, `Config`, or any
  other), **no** schema/`schemaVersion` change, and **no** new third-party `PackageReference`; the only additions
  are the new `ReleaseFactsSensing` `src`/`tests` projects, the new surface baseline, the two `.sln` entries, the
  F054 `scripts/prelude.fsx` section, and the `CLAUDE.md` plan pointer (plan §Scale/Scope). Product-neutrality
  (FR-011) is guaranteed by **API shape**, not a runtime test: the expectations and the `SourceLayout` are
  caller inputs, so confirm by inspection that the library body hardcodes no product/package id, path, field,
  pin, posture, or layout — the scope guard (T021) covers only dependency-level network/SDK leakage.
- [X] T025 Run `quickstart.md` validation end-to-end: `dotnet build
  src/FS.GG.Governance.ReleaseFactsSensing/FS.GG.Governance.ReleaseFactsSensing.fsproj`; `dotnet fsi
  scripts/prelude.fsx` (the F054 section — every `[F54]` line prints `true`); `dotnet test FS.GG.Governance.sln`
  — all projects green under `TreatWarningsAsErrors`, including the `DeriveFactsTests`/`HandoffTests`/
  `SnapshotTests`/`InterpreterTests`/`DeterminismTests`/`SurfaceDriftTests`/`PropertyTests` groups. Fix any drift.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-006 covered; the new `ReleaseFactsSensing`
surface is blessed and scope-clean (no network SDK); every frozen core/golden/schema byte-unchanged. The sensing
turns a real governed repository into exactly six classified fact states + an auditable snapshot, fails safe to
`Unrecoverable`, is structurally identical across runs, reaches no network, and hands `Facts` straight to the F053 core —
ready for the following `fsgg release` host command and `release.json` projection rows.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — drops all three `.fsi`, defines the data model + `releaseFamilies`,
  stubs `deriveFacts`/`realPort`/`gather`/`senseRelease`, proves the surface in FSI, and stands up the test
  scaffolding. **BLOCKS all stories.**
- **US1 (Phase 3, P1, MVP)**: Depends on Phase 2 — implements the pure `deriveFacts`. Independently testable via
  hand-built `RecoveredEvidence`; the F053 hand-off (T014) is exercisable for the pure half immediately and end-
  to-end once US3's edge lands.
- **US2 (Phase 4, P2)**: Depends on US1 — the snapshot detail layers on the same `deriveFacts` derivation /
  recovered evidence.
- **US3 (Phase 5, P3)**: Depends on US1 (and benefits from US2's snapshot for the determinism assertion) —
  implements the impure edge (`realPort`/`gather`/`senseRelease`) and pins the fail-safe/determinism/no-hide
  invariants over a real temp fixture.
- **Polish (Phase 6)**: Depends on all stories — the surface baseline, the scope/network guard, the FsCheck
  property, the additivity check, and the quickstart validation are blessed last, once the surface is final.

### Within Each Story

- Tests are written FIRST and must FAIL before implementation (Principle V); the Phase-6 property/guard tests
  assert invariants the implementation already satisfies.
- The three `.fsi` contracts (Phase 2) precede every `.fs` body (Principle I).
- The pure `deriveFacts` (US1) precedes the snapshot completion (US2) and the impure edge (US3); the edge
  (`senseRelease`) composes `gather >> deriveFacts`, so it depends on a real `deriveFacts`.
- Pure-derivation tests use hand-built `RecoveredEvidence` (no disk); interpreter tests run `realPort` against a
  **real** temp fixture repository (Principle V).

### Parallel Opportunities

- Setup tasks `T002`/`T004` run in parallel with `T001`/`T003`.
- Foundational: `T005`/`T006` (Model) precede `T007`/`T008` (Sensing) precede `T009` (Interpreter); `T010`–`T012`
  (prelude + test scaffolding) run in parallel with each other once the `.fsi`/stubs compile.
- Within each story, the test tasks marked `[P]` (different files) run in parallel and before the implementation
  task.
- Phase-6 `T021`/`T022`/`T024` are parallel-safe (different files / read-only inspection); `T023` (bless) is
  sequential after `T021` and the surface are final.

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → Phase 2 Foundational (all three `.fsi` contracts + data model + `releaseFamilies` + stubs +
   FSI proof + test scaffolding — solution compiles, existing suites untouched).
2. Phase 3 US1 — `deriveFacts` turns a recovered-evidence bundle + declared expectations into exactly six
   classified fact states whose `Facts` feed the F053 core unchanged.
3. **STOP and VALIDATE**: exactly six families, correct `Met`/`Unmet`/`Unrecoverable`, fail-safe on absent
   expectation/evidence, `Facts` accepted by `Release.evaluate`.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. Add US1 (`deriveFacts` + F053 hand-off) → validate → **MVP** (pure core, testable via hand-built evidence).
3. Add US2 (the observed-evidence snapshot detail) → validate → self-explaining specifics.
4. Add US3 (the impure `realPort`/`gather`/`senseRelease` edge + fail-safe/determinism/no-hide) → validate → the
   whole sense over a real repository.
5. Phase 6 polish → surface blessed, no-network proven, additivity proven, quickstart green.

### Suggested MVP Scope

**User Story 1** (Phase 3) over the Phase 1–2 foundation — the pure `Sensing.deriveFacts` turning a declared-
expectations + recovered-evidence pair into exactly six classified fact states, with the fail-safe on absent
expectation/evidence and a `Facts` value the F053 core accepts unchanged. It is independently testable (hand-
built `RecoveredEvidence`, no disk), the irreducible core every later row consumes, and a viable standalone
increment; the impure edge that reads a real repository (US3) and the snapshot detail (US2) layer on top.

---

## Notes

- `[P]` tasks = different files, no dependency on another incomplete task in this phase.
- `[Story]` labels map tasks to spec user stories (US1–US3); unlabeled tasks are shared infrastructure.
- Verify tests FAIL before implementing; never mark a failing task `[X]`; never weaken an assertion to green a
  build — narrow scope and document.
- Principle IV (Elmish/MVU) **is** applicable and is honored via the **injected sensing-port boundary** (not a
  full `Program` loop): I/O is data behind the single injected `RepositoryPort`, `deriveFacts` is pure, and
  interpretation lives only at the `Interpreter` edge — the F016 `Snapshot` / FreshnessSensing precedent
  (research D2).
- The library **reuses** the F053 `ReleaseFacts`/`FactState`/`ReleaseRuleKind` and F014 `SurfaceId` verbatim as
  its vocabulary — it introduces no new fact or family type — and references **only** `ReleaseRules` + `Config`,
  not `Snapshot`/`Route`/`Gates`, which it only mirrors in shape (plan Structure Decision).
- The fixtures are **real**: the pure core consumes hand-built `RecoveredEvidence` (its own real declared input),
  and the edge reads a **real** temp fixture repository through `realPort` (the `withTempDir` precedent) — no
  mocks, no `Synthetic` substitute for an unavailable dependency; any fixture file standing in for a real release
  artifact is disclosed per Principle V. No network, no registry, no publishing provider (SC-004).
- This row stops at the sensed `SensedRelease` (facts + snapshot). It does **not** evaluate rules into findings,
  roll up a verdict (F053), run the `fsgg release` host command, or emit `release.json` (FR-012) — those are
  F053 and following rows that consume this sensing.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
</content>
</invoke>
