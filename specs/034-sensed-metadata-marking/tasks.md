---

description: "Task list for 034-sensed-metadata-marking implementation"
---

# Tasks: Sensed-Metadata Marking Core

**Input**: Design documents from `/specs/034-sensed-metadata-marking/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/sensed-metadata-api.md,
contracts/sensed-metadata-format.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a marking / flagged-rendering / unspoofability / determinism / purity / identity-neutrality
contract — the tests *are* the deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no
new third-party dependency — plan Constitution Check). No per-task tier annotations needed — all tasks share
the feature tier.

**Elmish/MVU**: **Not applicable** — pure, total functions (`markDuration`, `markTimestamp`, `kindOf`,
`kindToken`, `render`, `renderSection`, `renderingValue`) over supplied values; no state, no I/O, no workflow
(plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks. The
timestamp and duration are values handed in (the timestamp opaque, the duration a verbatim F032
`SensedDuration`), never sensed here.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No
semantics yet.

- [X] T001 Create `src/FS.GG.Governance.SensedMetadata/FS.GG.Governance.SensedMetadata.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.SensedMetadata`, `Version` `0.1.0`, `IsPackable=true`
  (override the `Directory.Build.props` `IsPackable=false` default, like FreshnessKey/CommandRecord/Config/
  Provenance). `<Compile>` order: `Model.fsi`, `Model.fs`, `SensedMetadata.fsi`, `SensedMetadata.fs`. **Exactly
  one** `<ProjectReference>` — `../FS.GG.Governance.CommandRecord/FS.GG.Governance.CommandRecord.fsproj` (the
  only sibling, owns F032's `SensedDuration` reused verbatim, FR-008) — and **no other src reference** (never
  reference FreshnessKey/Provenance/Gates/Route/Routing/Findings/Snapshot/EvidenceReuse/RouteExplain/Host/Cli —
  plan D1). `CommandRecord` is a pure vocab core, so nothing impure is pulled in. **No third-party
  `PackageReference`** (FR-011; the transitive `YamlDotNet` via Config→CommandRecord is unused). Add a header
  comment mirroring the Provenance/CommandRecord `.fsproj` (pure total sensed-metadata marking + flagged-
  rendering core over already-measured values; reuses F032 `SensedDuration` verbatim for the duration kind;
  introduces only `SensedLabel`/`SensedTimestamp`/`SensedKind`/`SensedValue`/`SensedMetadatum`/`SensedRendering`;
  every value sensed by construction; flagged rendering in the F029/F032/F033 tagged/length-prefixed/injective
  discipline with a reserved `!…!` marker; no sensing/timing/hashing/persistence/identity/git/filesystem
  coupling — D1–D6).
- [X] T002 [P] Create
  `tests/FS.GG.Governance.SensedMetadata.Tests/FS.GG.Governance.SensedMetadata.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  version literals in the `.fsproj`); `<ProjectReference>`s to the new core **and** to
  `../../src/FS.GG.Governance.CommandRecord/FS.GG.Governance.CommandRecord.fsproj` (the test code constructs
  real `SensedDuration`s). `<Compile>` order: `Support.fs`, `MarkingTests.fs`, `RenderingTests.fs`,
  `DeterminismTests.fs`, `PurityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing F033 entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies
+ test scaffolding so the library and tests compile and tests can FAIL before implementation. **⚠️ No story
work begins until this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.SensedMetadata/Model.fsi` — the SOLE public surface for the types
  (contracts/sensed-metadata-api.md, data-model.md): `open FS.GG.Governance.CommandRecord.Model` (for verbatim
  F032 `SensedDuration`, never redefined — FR-008); in **data-model order**: the newtypes `SensedLabel of
  string` then `SensedTimestamp of string`; the closed `SensedKind = TimestampKind | DurationKind`; the closed
  `SensedValue = TimestampValue of SensedTimestamp | DurationValue of SensedDuration`; the closed
  `SensedMetadatum` record `{ Label: SensedLabel; Value: SensedValue }`; and last the `SensedRendering of string`
  newtype.
  Curated doc comments in the F032/F033 `.fsi` style: every `SensedMetadatum` is sensed by construction — the
  `SensedValue` DU is the flag, no reproducible variant exists (FR-001, D3); `SensedTimestamp` is the only
  genuinely new fact (opaque, supplied, never clocked — D2); the duration arm carries F032's `SensedDuration`
  verbatim; `SensedLabel`/`SensedTimestamp` are opaque tokens whose empty string is a literal value (FR-004,
  Edge cases); `SensedRendering` wraps the byte-stable flagged rendering (mirrors F032 `CommandIdentity`/F033
  `ProvenanceIdentity`). No access modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.SensedMetadata/SensedMetadata.fsi` — the SOLE public surface for the
  operations (contracts/sensed-metadata-api.md): `val markDuration: label: SensedLabel -> duration:
  SensedDuration -> SensedMetadatum`; `val markTimestamp: label: SensedLabel -> timestamp: SensedTimestamp ->
  SensedMetadatum`; `val kindOf: metadatum: SensedMetadatum -> SensedKind`; `val kindToken: kind: SensedKind ->
  string`; `val render: metadatum: SensedMetadatum -> SensedRendering`; `val renderSection: metadata:
  SensedMetadatum list -> SensedRendering`; `val renderingValue: rendering: SensedRendering -> string`. Mark the
  module `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]` (the contract header).
  Doc comments stating purity/totality and the laws (L-M1/L-M2: marking total, carries label/value verbatim,
  sensed by construction, reads no clock; L-K1/L-K2: `kindOf` total, `kindToken` injective `timestamp`/
  `duration`; L-R1..L-R4: `render` starts with `!sensed!=` — distinguishable from any reproducible field —
  carries kind/label/value length-prefixed, injective/unspoofable by data, value verbatim; L-S1/L-S2:
  `renderSection` one order-preserving `!sensed-section!`, empty list ⇒ `!sensed-section!=0;`, separable;
  L-D1/L-P1/L-N1: deterministic, pure, identity-neutral — reads no clock/filesystem/git/environment/network,
  spawns no process, hashes no bytes, computes/alters no reproducible identity; L-U1: `renderingValue` unwraps).
  Include the naming note (operations module `SensedMetadata` vs the `Model` types — distinct CLR entities).
- [X] T006 Add stub `src/FS.GG.Governance.SensedMetadata/Model.fs` and
  `src/FS.GG.Governance.SensedMetadata/SensedMetadata.fs` — real type definitions in `Model.fs` (the three
  newtypes, the two DUs, and the `SensedMetadatum` record are data, define them fully, with the `open
  FS.GG.Governance.CommandRecord.Model` for the verbatim `SensedDuration`); `markDuration`/`markTimestamp`/
  `kindOf`/`kindToken`/`render`/`renderSection`/`renderingValue` as `failwith "not implemented"` in
  `SensedMetadata.fs` so the assembly compiles. No `private`/`internal`/`public` modifiers (Principle II).
  Confirm `dotnet build src/FS.GG.Governance.SensedMetadata/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F034 design-first section to `scripts/prelude.fsx` after the F033 section — `#r` the new
  Debug DLL plus the CommandRecord DLL; build the worked example from contracts/sensed-metadata-format.md and
  quickstart.md: `markDuration (SensedLabel "elapsed") (SensedDuration 1_830_000_000L)` and `markTimestamp
  (SensedLabel "at") (SensedTimestamp "2026-06-21T12:00:00Z")`; `printfn` the intended calls with expected
  results: `kindOf` ⇒ `DurationKind` / `TimestampKind`; `render` of the duration ⇒
  `"!sensed!=duration;7:elapsed;10:1830000000"`; `render` of the timestamp ⇒
  `"!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z"`; `renderSection [ tM; dM ]` ⇒ the byte-exact section block
  `"!sensed-section!=2;47:!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z;41:!sensed!=duration;7:elapsed;10:1830000000"`;
  `renderSection []` ⇒ `"!sensed-section!=0;"`; the empty-label zero-duration spoof example
  `"!sensed!=duration;0:;1:0"`; and the label-is-`!sensed!` spoof example
  `"!sensed!=timestamp;8:!sensed!;20:2026-06-21T12:00:00Z"`. Expected outputs as inline comments. This is the
  Principle-I FSI proof; it documents the shape even while the bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.SensedMetadata.Tests/Support.fs` — real, literally-constructible
  builders (Principle V, no mocks, no clock): helpers to construct `SensedLabel`/`SensedTimestamp` from literals
  (incl. empty string and marker-containing text) and `SensedDuration` from `int64` literals (incl. `0L` and
  large/negative magnitudes); convenience builders that mark a duration/timestamp metadatum with a label;
  FsCheck generators for `SensedLabel`, `SensedTimestamp`, `SensedDuration`, `SensedValue`, `SensedMetadatum`,
  and `SensedMetadatum list` (the label/value strings drawn to *include* the marker characters `!`, `;`, `:`,
  `=` so the unspoofability law is actually exercised); and the `findRepoRoot (DirectoryInfo
  AppContext.BaseDirectory)` / `repoRoot` helper copied from the F033 `Support.fs`. No I/O beyond repo-root
  resolution.
- [X] T009 [P] Write `tests/FS.GG.Governance.SensedMetadata.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because the operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Mark a timestamp or duration as explicitly-flagged sensed metadata (Priority: P1) 🎯 MVP

**Goal**: `markDuration` and `markTimestamp` turn an already-measured duration / wall-clock timestamp into a
typed `SensedMetadatum` carrying its label and value, sensed by construction; `kindOf`/`kindToken` report the
closed kind. There is no representation of a marked timestamp or duration that is reproducible (the `SensedValue`
DU is the flag). Total over the zero-length-duration, empty-label, and same-label/different-kind edges.

**Independent Test**: Take a measured `SensedDuration` and a supplied `SensedTimestamp`, mark each with a label,
and assert each result reports its kind (`DurationKind` / `TimestampKind`), its label, and its carried value,
and is sensed by its very type. A zero-length duration, an empty label, and two same-label/different-kind
metadata are all ordinary complete values; marking never throws. (US1 acceptance #1–#3.)

### Tests for User Story 1 (write first; must FAIL against the stub)

- [X] T010 [P] [US1] `tests/.../MarkingTests.fs` — (a) carriage (L-M1): `markDuration L d` ⇒ `{ Label = L;
  Value = DurationValue d }` and `markTimestamp L t` ⇒ `{ Label = L; Value = TimestampValue t }` — `.Label` and
  `.Value` read back verbatim for literal facts (SC-001, US1 #1/#2); (b) kind (L-K1): `kindOf (markDuration L d)
  = DurationKind`, `kindOf (markTimestamp L t) = TimestampKind`; `kindToken DurationKind = "duration"`,
  `kindToken TimestampKind = "timestamp"`, the two distinct (L-K2, US1 #3); (c) sensed by construction (L-M2):
  every marked value's `.Value` is a `SensedValue` — assert by exhaustive match that the only inhabitants are
  `TimestampValue`/`DurationValue` (no reproducible variant — FR-001); (d) totality edge cases — a zero-length
  `SensedDuration 0L`, an empty `SensedLabel ""`, two metadata sharing a label but differing in kind (one
  timestamp, one duration), and a large/negative-magnitude duration — each produces an ordinary complete
  metadatum, marking never throws (SC-001, Edge cases); (e) FsCheck totality: over generated labels/values,
  `markDuration`/`markTimestamp` always return and round-trip the label + value, and `kindOf` always agrees with
  the constructor used.

### Implementation for User Story 1

- [X] T011 [US1] Implement `markDuration`, `markTimestamp`, `kindOf`, `kindToken` in `SensedMetadata.fs` per
  contracts/sensed-metadata-api.md — `markDuration label duration = { Label = label; Value = DurationValue
  duration }`; `markTimestamp label timestamp = { Label = label; Value = TimestampValue timestamp }` (pure record
  construction, no clock, no normalization); `kindOf` matches `.Value` (`TimestampValue _ -> TimestampKind`,
  `DurationValue _ -> DurationKind`); `kindToken` the total two-case map (`TimestampKind -> "timestamp"`,
  `DurationKind -> "duration"`). Run T010: all carriage / kind / sensed-by-construction / totality assertions go
  green.

**Checkpoint**: US1 is functional — an already-measured duration or timestamp becomes a typed, sensed-by-
construction `SensedMetadatum` whose kind/label/value are readable, total over every edge case. MVP reached
(minus rendering).

---

## Phase 4: User Story 2 — Render sensed metadata into a deterministic report with an explicit marker (Priority: P1)

**Goal**: `render` produces the byte-stable, unambiguously-flagged rendering of one metadatum — the reserved
`!sensed!=` marker, the kind token, the label, and the value, each length-prefixed in the F029/F032/F033
injective discipline — so it is distinguishable from any reproducible field and unspoofable by its data;
`renderSection` groups a list into one order-preserving `!sensed-section!` (empty list ⇒ `!sensed-section!=0;`)
cleanly separable from a report's reproducible bytes; `renderingValue` unwraps the `SensedRendering`.

**Independent Test**: Render a duration metadatum and a timestamp metadatum; assert each rendering carries the
label, the value, and a `!sensed!=` marker, is visibly distinct from a reproducible field tag, and is byte-equal
on repeat. Render a list as one separable `!sensed-section!`, order-preserving, empty list included. Feed labels/
values containing `!sensed!`/`;`/`:`/`=` and assert they cannot masquerade as the marker or bleed across a field
boundary. (US2 acceptance #1–#4.)

### Tests for User Story 2 (write first)

- [X] T012 [P] [US2] `tests/.../RenderingTests.fs` — (a) marker present & distinguishable (L-R1):
  `renderingValue (render m)` starts with `!sensed!=`, a form no reproducible field tag produces — assert it is
  distinct from the lowercase-letter tags F029/F032/F033 use (US2 #1/#2, SC-002); (b) content present & verbatim
  (L-R2/L-R4): the rendering contains the kind token, the length-prefixed label, and the length-prefixed value,
  where a `DurationValue (SensedDuration ns)` renders `ns` as decimal (incl. `0`, negatives) and a
  `TimestampValue (SensedTimestamp s)` renders `s` verbatim (Edge: long-fraction/large magnitude); (c) byte-exact
  worked examples pinned to contracts/sensed-metadata-format.md: `!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z`,
  `!sensed!=duration;7:elapsed;10:1830000000`, the empty-label zero-duration `!sensed!=duration;0:;1:0`, and the
  label-is-`!sensed!` spoof `!sensed!=timestamp;8:!sensed!;20:2026-06-21T12:00:00Z`; (d) unspoofable / injective
  (L-R3): for labels/values whose text contains `!sensed!`, `;`, `:`, or `=`, `render m = render m'` **iff** `m`
  and `m'` have the same kind, label, and value — no two different metadata render equal, none renders as another
  field or as absence; an empty label renders to the distinct `0:` form (FR-004, SC-002); (e) section (L-S1):
  `renderSection ms` is one `!sensed-section!=<count>;…` whose entries are `render` of each element **in given
  order** (not sorted/deduped — verify a reordered and a duplicate-containing list differ accordingly), each
  length-prefixed; the empty list ⇒ `!sensed-section!=0;`; the two-element worked-example section block matches
  byte-for-byte (US2 #3, SC-004); (f) unwrap (L-U1): `renderingValue (SensedRendering s) = s`. FsCheck the
  injectivity/determinism/unspoofability properties; example-test the worked blocks.

### Implementation for User Story 2

- [X] T013 [US2] Implement `render`, `renderSection`, `renderingValue` in `SensedMetadata.fs` per
  contracts/sensed-metadata-format.md — a length-prefixed scalar helper `<utf8ByteLen> ":" <bytes>` (UTF-8 byte
  length, the F029/F032/F033 discipline); `render m` = `"!sensed!="` + `kindToken (kindOf m)` + `";"` +
  lenPrefix(label string) + `";"` + lenPrefix(valueText), where valueText is the decimal of the `int64`
  nanoseconds for `DurationValue` (verbatim, no rounding/re-scaling — D6) and the opaque string verbatim for
  `TimestampValue`, wrapped as `SensedRendering`; `renderSection ms` = `"!sensed-section!="` + `string
  (List.length ms)` + `";"` + (each element's full `render` string, length-prefixed, in given order, **joined by
  `";"`** — `String.concat ";"`), empty list ⇒ `"!sensed-section!=0;"`, wrapped as `SensedRendering`; `renderingValue (SensedRendering s) = s`.
  Pure string/list operations — no clock/filesystem, no hashing, no identity. Run T012: green. Re-run T010:
  still green.

**Checkpoint**: US1 + US2 — a marked metadatum renders to a byte-stable, explicitly-flagged, unspoofable
`SensedRendering` distinguishable from reproducible fields, and a list renders to one separable, order-preserving
`!sensed-section!`. The full contract surface is delivered.

---

## Phase 5: User Story 3 — The marking and rendering are deterministic, pure, and identity-neutral (Priority: P2)

**Goal**: `markDuration`/`markTimestamp`/`render`/`renderSection` are pure, deterministic functions of the
supplied values: identical inputs ⇒ identical marked value and byte-identical rendering; no clock/filesystem/git/
environment/network is read and no process spawned; and a sensed rendering is identity-neutral — a report's
reproducible bytes are byte-identical regardless of which / how many sensed metadata populate its section.

**Independent Test**: Mark + render the same value twice ⇒ byte-equal. Recompute after changing cwd / creating a
temp file ⇒ unchanged. Model a report as `(reproducibleBytes, renderSection sensed)` and assert
`reproducibleBytes` is byte-identical when the sensed list is empty, singular, or many. (US3 acceptance #1–#3.)

### Tests for User Story 3 (write first)

- [X] T014 [P] [US3] `tests/.../DeterminismTests.fs` — (a) determinism (L-D1): `markDuration`/`markTimestamp`
  called twice yields a structurally identical `SensedMetadatum`, and `render`/`renderSection` called twice
  yields a byte-identical `SensedRendering`, for representative and FsCheck-generated inputs (SC-004, US3 #1);
  (b) identity-neutrality (L-N1/L-S2): a report modeled as `(reproducibleBytes, renderSection sensed)` keeps its
  `reproducibleBytes` byte-identical whether `sensed` is `[]`, one metadatum, or many — adding/removing a sensed
  metadatum from the section never alters the reproducible partition (SC-003, US3 #3). *(The optional stronger
  evidence — a test-only reference to `FS.GG.Governance.Provenance` asserting `canonicalId` is unchanged
  alongside a rendered section — is deferred per plan D5; the self-contained demonstration is sufficient and
  avoids test coupling.)*
- [X] T015 [P] [US3] `tests/.../PurityTests.fs` — a fixed `markDuration`/`markTimestamp`/`render`/`renderSection`
  result is identical when recomputed after changing `Environment.CurrentDirectory` and after creating/deleting
  an unrelated temp file (and across repeated calls), demonstrating no clock/cwd/filesystem influence, no elapsed
  time measured, and no process spawn (SC-005, US3 #2).

**Note**: US3 has no new implementation task — determinism / purity / identity-neutrality are properties of the
`markDuration`/`markTimestamp`/`render`/`renderSection` built in US1+US2 (record construction and the
length-prefixed string/`List.map`/`String.concat` pipeline are pure, and the sensed rendering is a standalone
value referencing no identity-computing core — D5). If T014–T015 reveal a gap, fix the operation (never weaken a
test).

**Checkpoint**: All three stories functional and independently testable — the marking and rendering are
complete, correct, deterministic, pure, and identity-neutral.

---

## Phase 6: Cross-cutting Tier-1 surface obligations

**Purpose**: The surface baseline + scope guard and the no-regression promise.

- [X] T016 `tests/.../SurfaceDriftTests.fs` — the reflective surface test (F029/F030/F031/F032/F033 precedent):
  render the assembly's public surface, compare to `surface/FS.GG.Governance.SensedMetadata.surface.txt` with
  the `BLESS_SURFACE=1` re-bless path; assert exactly the two public modules (`Model`, `SensedMetadata`) export
  and no helper leaks (the length-prefix helper stays unexposed); **scope-hygiene** (an **allow-set whitelist**,
  not a presence assertion — `GetReferencedAssemblies()` on the `SensedMetadata.dll` lists only its *direct*
  refs, so `Config`/`YamlDotNet` may legitimately be absent; the test asserts **every** referenced assembly is
  ∈ the allowed set, never that a specific transitive ref is present — the F033 precedent): the allowed set is
  only `FSharp.Core`, `FS.GG.Governance.CommandRecord`, `FS.GG.Governance.Config` (transitive via CommandRecord),
  and BCL (`System.*`/`netstandard`/`mscorlib`) — NOT `FreshnessKey`/`Provenance`/`Gates`/`Route`/`Routing`/
  `Findings`/`Snapshot`/`EvidenceReuse`/`RouteExplain`/`RouteJson`/`GatesJson`/`AuditJson`/`Enforcement`/`Ship`/
  `Adapters.*`/`Host`/`Cli` (plan D1, contracts negative scope guard). Note the single permitted sibling
  reference (`CommandRecord` — D1), contrasting F033's three-core reference.
- [X] T017 Generate the committed baseline `surface/FS.GG.Governance.SensedMetadata.surface.txt` by running the
  suite once with `BLESS_SURFACE=1`, then review the file by eye against `Model.fsi`/`SensedMetadata.fsi` to
  confirm it contains exactly the intended surface (the two modules `Model`/`SensedMetadata`, the seven vals
  `markDuration`/`markTimestamp`/`kindOf`/`kindToken`/`render`/`renderSection`/`renderingValue`, and the declared
  new types `SensedLabel`/`SensedTimestamp`/`SensedKind`/`SensedValue`/`SensedMetadatum`/`SensedRendering`).
  Commit it. (After T016.)
- [-] T018 [P] README cores pointer — **SKIPPED** per the F033 T018 precedent: `README.md`'s enumerated core
  list is frozen at **F18** (the last detailed entry; F19–F33 were never added), so F034 is likewise not
  appended — partially extending the list for F034 alone would be inconsistent. Verified 2026-06-21: the README
  core enumeration still ends at F18. No README change.

**Checkpoint**: Tier-1 obligations met; the public surface is pinned.

---

## Phase 7: Validation & polish

- [X] T019 Run `dotnet test tests/FS.GG.Governance.SensedMetadata.Tests/...` — all suites green; capture the
  run as evidence (Principle V).
- [X] T020 Run `dotnet test FS.GG.Governance.sln` and confirm the no-regression promise (SC-006): existing
  projects' tests and **every** existing `surface/*.surface.txt` baseline (including F014 Config, F029
  FreshnessKey, F032 CommandRecord, F033 Provenance) are unchanged; only the new project's tests and the new
  surface baseline are added. Confirm `src/**` and existing `surface/**` show no diff.
- [X] T021 [P] Run `dotnet fsi scripts/prelude.fsx` end-to-end and confirm the F034 section's printed results
  now match the real `markDuration`/`markTimestamp`/`kindOf`/`render`/`renderSection` output, including the
  byte-exact worked-example single renderings and the section block (Principle I evidence, closes T007).
- [X] T022 [P] Walk `quickstart.md` top-to-bottom (build → FSI → test → re-bless → no-regression) and fix any
  drift between the guide and reality. Confirm the `CLAUDE.md` plan pointer targets
  `specs/034-sensed-metadata-marking/plan.md` (it already does — leave it if so).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → after Phase 1; **BLOCKS** all stories (the `.fsi` + stubs + Support must exist to
  compile any test).
- **Phase 3 (US1)** → after Phase 2. Delivers `markDuration`/`markTimestamp`/`kindOf`/`kindToken` (the typed,
  sensed-by-construction marking) — the MVP.
- **Phase 4 (US2)** → after **US1** specifically (`render`/`renderSection` operate on the `SensedMetadatum` that
  marking produces; the tests construct metadata via `markDuration`/`markTimestamp`). Independently testable once
  rendering lands (T013).
- **Phase 5 (US3)** → after US1+US2 (determinism/purity/identity-neutrality are properties of the completed
  marking + rendering). No new implementation; tests only.
- **Phase 6 (cross-cutting)** → after the operations exist; T017 after T016.
- **Phase 7 (validation)** → last.

### Within each story

- Tests are written first and must FAIL against the Phase-2 stub before the implementation task greens them.
- `Model.fs` types before the operations that use them (already in place from Phase 2).

### Parallel opportunities

- Phase 1: T002 ‖ (T001→T003).
- Phase 2: T007 ‖ T009 (after T004–T006 land the `.fsi`+stubs; T008 Support precedes the story tests).
- Within a story, the `[P]` test files are independent of each other; the implementation task follows them.
- Phase 5: T014 ‖ T015 (all tests, no new impl).
- Phase 6: T018 ‖ (T016→T017).
- Phase 7: T021 ‖ T022 (after T019–T020).

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (`markDuration`/`markTimestamp`/`kindOf`/`kindToken`
— the typed, sensed-by-construction marking, total over every edge case) → 4. **STOP & VALIDATE**: each marked
value reports its kind/label/value, is sensed by its very type, and zero-duration / empty-label / same-label-
different-kind cases are ordinary values.

### Incremental delivery

US1 (typed sensed-by-construction marking = MVP) → US2 (flagged rendering: `render`/`renderSection`/
`renderingValue`) → US3 (determinism/purity/identity-neutrality proof) → cross-cutting surface obligations →
full-suite validation. Each phase is independently testable and adds value without breaking the previous.

---

## Notes

- Tier 1 throughout: any public-surface change requires re-blessing
  `surface/FS.GG.Governance.SensedMetadata.surface.txt` (`BLESS_SURFACE=1`).
- `[P]` = different files, no in-phase dependency. `[USx]` maps a task to its spec user story.
- Principle IV (Elmish/MVU) is **N/A** — pure total functions, no state/I/O (recorded once here, not per task).
  The timestamp and duration are values handed in (the duration a verbatim F032 `SensedDuration`), never sensed
  here.
- No mocks anywhere (Principle V); all inputs are real, literally-constructible typed values (incl. real F032
  `SensedDuration`s; the facts a host would sense — the clock instant, the elapsed time — are supplied as
  literals, the core's contract). No process is spawned. No `Synthetic` disclosure needed.
- **References exactly one sibling core** (`CommandRecord` — D1), mandated by the verbatim-reuse requirement
  FR-008; it is a pure vocab core, so nothing impure is pulled in and the transitive `YamlDotNet` (via Config) is
  unused. F032 `SensedDuration` is consumed verbatim; not modified (FR-008, SC-006). (Contrast: F033 referenced
  three cores; this row needs only the one that owns `SensedDuration`.)
- **The type is the flag** (D3): the `SensedValue` DU means there is no representation of a marked timestamp or
  duration that is reproducible. The reserved `!…!` marker form (D4) is never produced by a reproducible field
  tag, so a sensed rendering is unmistakably distinct; every label/value is length-prefixed, so marker-containing
  data cannot masquerade (FR-004).
- **Identity-neutrality is structural** (D5): this core computes no reproducible identity and references no
  identity-computing core; the sensed rendering is a standalone separable value. Demonstrated self-containedly
  (T014b), not via a cross-core test.
- This row **closes Phase 11** (Cost, Cache, and Provenance) — its sixth and final line.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
