---

description: "Task list for 033-provenance-record implementation"
---

# Tasks: Provenance Core

**Input**: Design documents from `/specs/033-provenance-record/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/provenance-api.md,
contracts/provenance-identity-format.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a complete-carriage / sensed-split / canonical-identity / determinism / purity contract — the
tests *are* the deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no
new third-party dependency — plan Constitution Check). No per-task tier annotations needed — all tasks share
the feature tier.

**Elmish/MVU**: **Not applicable** — three pure, total functions (`build`, `canonicalId`, `identityValue`)
over supplied values; no state, no I/O, no workflow (plan Constitution Check, Principle IV = N/A). No
`Model`/`Msg`/`Effect`/`update`/interpreter tasks. The nine build facts are values handed in, not sensed here.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No
semantics yet.

- [X] T001 Create `src/FS.GG.Governance.Provenance/FS.GG.Governance.Provenance.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.Provenance`, `Version` `0.1.0`, `IsPackable=true` (override
  the `Directory.Build.props` `IsPackable=false` default, like FreshnessKey/CommandRecord/Config).
  `<Compile>` order: `Model.fsi`, `Model.fs`, `Provenance.fsi`, `Provenance.fs`. **Three**
  `<ProjectReference>`s — `../FS.GG.Governance.FreshnessKey/FS.GG.Governance.FreshnessKey.fsproj`,
  `../FS.GG.Governance.CommandRecord/FS.GG.Governance.CommandRecord.fsproj`, and
  `../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj` — and **no other src reference** (never
  reference Gates/Route/Routing/Findings/Snapshot/EvidenceReuse/RouteExplain/Host/Cli — plan D1). This is the
  **first core to reference more than one sibling core** (D1); all three are pure vocab cores, so nothing
  impure is pulled in. **No third-party `PackageReference`** (FR-013, plan D1; the transitive `YamlDotNet` via
  Config is unused). Add a header comment mirroring the CommandRecord `.fsproj` (pure total provenance core
  over already-sensed facts; reuses F029 `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash`, F032
  `CommandRecord` + `canonicalId`/`identityValue`, and F014 `EnvironmentClass` verbatim; sensed durations live
  inside the embedded F032 records, structurally excluded from identity; canonical identity over the
  reproducible facts in the F029/F032 tagged/length-prefixed discipline; no
  sensing/timing/hashing/persistence/rendering/git/filesystem coupling — D1–D5).
- [X] T002 [P] Create `tests/FS.GG.Governance.Provenance.Tests/FS.GG.Governance.Provenance.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  version literals in the `.fsproj`); `<ProjectReference>`s to the new core **and** to all three deps
  `../../src/FS.GG.Governance.FreshnessKey/...`, `../../src/FS.GG.Governance.CommandRecord/...`, and
  `../../src/FS.GG.Governance.Config/...` (the test code constructs real `Revision`/`RuleHash`/
  `GeneratorVersion`/`ArtifactHash`, real `CommandRecord`s via `CommandRecord.build`, and real
  `EnvironmentClass` values). `<Compile>` order: `Support.fs`, `ProvenanceTests.fs`, `IdentityTests.fs`,
  `DeterminismTests.fs`, `PurityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing F032 entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies
+ test scaffolding so the library and tests compile and tests can FAIL before implementation. **⚠️ No story
work begins until this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.Provenance/Model.fsi` — the SOLE public surface for the types
  (contracts/provenance-api.md, data-model.md): `open FS.GG.Governance.FreshnessKey.Model` (for verbatim F029
  `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash`), `open FS.GG.Governance.CommandRecord.Model` (for
  verbatim F032 `CommandRecord`), and `open FS.GG.Governance.Config.Model` (for verbatim F014
  `EnvironmentClass`) — none redefined (FR-010); the two new newtypes `BuilderIdentity of string` and
  `ProvenanceIdentity of string`; and the closed `Provenance` record with its nine fields in the
  data-model order (`SourceCommit: Revision`, `Base: Revision`, `Head: Revision`, `RuleHash: RuleHash`,
  `GeneratorVersion: GeneratorVersion`, `ArtifactDigests: ArtifactHash list`, `CommandRecords: CommandRecord
  list`, `Environment: EnvironmentClass`, `Builder: BuilderIdentity`). Curated doc comments in the F029/F032
  `.fsi` style: all eight declared facts carried, none optional-by-omission (FR-001); the three revisions share
  `Revision` but are distinct facts/identity segments (D2); the artifact digests are a SET in identity, the
  command records an ORDERED list carried whole (D4); the sensed durations live *inside* the embedded
  `CommandRecord`s, reachable via `.CommandRecords.[i].Duration` and excluded from identity (D3);
  `ProvenanceIdentity` wraps the byte-stable canonical rendering. No access modifiers will appear in the
  matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.Provenance/Provenance.fsi` — the SOLE public surface for the operations
  (contracts/provenance-api.md): `val build:` (the nine supplied facts curried in the design row's field order —
  `sourceCommit`, `baseRevision`, `headRevision`, `ruleHash`, `generatorVersion`, `artifactDigests`,
  `commandRecords`, `environment`, `builder` ⇒ `Provenance`); `val canonicalId: provenance: Provenance ->
  ProvenanceIdentity`; `val identityValue: identity: ProvenanceIdentity -> string`. Doc comments stating
  purity/totality and the laws (L-B1..L-B5: `build` total and verbatim-carrying all nine facts, no
  canonicalization here; L-I1..L-I7: `canonicalId` computed only over reproducible facts, each command record
  folded via F032 `CommandRecord.canonicalId` so durations never read, duration-only difference ⇒ equal id, any
  reproducible difference ⇒ different id, artifact digests set-invariant but command-record order significant,
  injective across fields, byte-stable, no hashing; L-V1: `identityValue` unwraps; reads no
  clock/filesystem/git/environment/network, spawns no process, hashes no bytes). Include the F029/F032 naming
  note (operations module `Provenance` vs the `Model.Provenance` type — distinct CLR entities).
- [X] T006 Add stub `src/FS.GG.Governance.Provenance/Model.fs` and
  `src/FS.GG.Governance.Provenance/Provenance.fs` — real type definitions in `Model.fs` (the two newtypes and
  the `Provenance` record are data, define them fully, with the three `open` statements for F029/F032/F014
  vocab); `build`/`canonicalId`/`identityValue` as `failwith "not implemented"` in `Provenance.fs` so the
  assembly compiles. No `private`/`internal`/`public` modifiers (Principle II). Confirm
  `dotnet build src/FS.GG.Governance.Provenance/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F033 design-first section to `scripts/prelude.fsx` after the F032 section — `#r` the new
  Debug DLL plus the FreshnessKey, CommandRecord, and Config DLLs; build the worked example from
  contracts/provenance-identity-format.md (source commit `c0ffee`; base `base1`; head `head2`; rule hash
  `rule-x`; generator version `gen-1`; artifact digests `[ArtifactHash "a1"; ArtifactHash "a2"]`; one command
  record — the F032 worked-example record built via `CommandRecord.build`; environment class `Local`; builder
  identity `ci-runner`); `printfn` the intended calls with expected results: `build …` carries all nine facts
  read back verbatim (command records whole, artifact digests reported as supplied); two provenances differing
  only in the embedded record's `SensedDuration` ⇒ **equal** `canonicalId` while `.CommandRecords.[0].Duration`
  still differs; flip one reproducible fact (e.g. `Head`, an extra `ArtifactHash`, or a command-record argument)
  ⇒ **different** `canonicalId`; reorder/duplicate the artifact digests ⇒ **unchanged** `canonicalId`; reorder
  the command records ⇒ **changed** `canonicalId`; the worked-example identity string equals the contract's
  block byte-for-byte. Expected outputs as inline comments. This is the Principle-I FSI proof; it documents the
  shape even while the body is stubbed.
- [X] T008 Write `tests/FS.GG.Governance.Provenance.Tests/Support.fs` — real, literally-constructible fact
  builders (Principle V, no mocks): helpers to construct `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash`
  /`EnvironmentClass`/`BuilderIdentity` from literals, plus a convenience that assembles a full `Provenance` via
  `Provenance.build` with sensible defaults and per-field overrides (so each test perturbs exactly one fact). Add
  a real `CommandRecord` builder via `CommandRecord.build` (with per-fact overrides incl. `SensedDuration` so
  duration-only-difference pairs and reproducible-difference pairs are easy to make); an artifact-digest list
  builder that can be given entries in arbitrary order / with duplicates (for the set order/dup-invariance
  tests); a command-records list builder preserving order (for the order-significance test); FsCheck generators
  for whole `Provenance`s (varying every reproducible fact, the artifact-digest order/dup, the command-record
  order, and the embedded durations); and the `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` /
  `repoRoot` helper copied from the F032 `Support.fs`. No I/O beyond repo-root resolution.
- [X] T009 [P] Write `tests/FS.GG.Governance.Provenance.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because the operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Capture a build's provenance as one complete, typed value (Priority: P1) 🎯 MVP

**Goal**: `build` assembles the nine supplied facts into one complete `Provenance` from which each fact reads
back verbatim — source commit, base/head revisions, rule hash, generator version, the artifact digests (as
supplied — set treatment is the identity's job), the command records (carried whole, in order, each retaining
all ten of its facts), environment class, and builder identity — total over no-records / no-artifacts /
equal-base-head / failed-or-timed-out-record builds.

**Independent Test**: Supply the nine literal facts and `build` the provenance; assert each fact reads back
verbatim (command records whole and in order, each retaining its ten facts; artifact digests all carried);
a build with no command records, no covered artifacts, equal base/head, and a failed/timed-out embedded record
all yield ordinary complete values. (US1 acceptance #1–#4.)

### Tests for User Story 1 (write first; must FAIL against the stub)

- [X] T010 [P] [US1] `tests/.../ProvenanceTests.fs` — (a) verbatim carriage: for the nine literal facts, assert
  `p.SourceCommit`/`p.Base`/`p.Head`/`p.RuleHash`/`p.GeneratorVersion`/`p.ArtifactDigests`/`p.CommandRecords`
  (same elements, **same order**, each record whole with all ten of its facts incl. `.Duration`)/`p.Environment`
  /`p.Builder` each equal the supplied input (SC-001, SC-002, US1 #1/#2); (b) artifact-digest carriage: every
  supplied digest is present on the value (carriage is verbatim — dedup is the identity's job, L-B4) (US1 #3);
  (c) totality edge cases — empty `CommandRecords`, empty `ArtifactDigests`, `Base = Head`, and an embedded
  `CommandRecord` whose run failed (non-zero exit) or timed out — each produces an ordinary complete provenance,
  `build` never throws (SC-001, US1 #4, Edge cases); (d) FsCheck totality: over generated nine-fact tuples,
  `build` always returns and round-trips every fact.

### Implementation for User Story 1

- [X] T011 [US1] Implement `build` in `Provenance.fs` per contracts/provenance-api.md — assemble the nine
  curried facts into `{ SourceCommit = sourceCommit; Base = baseRevision; Head = headRevision; RuleHash =
  ruleHash; GeneratorVersion = generatorVersion; ArtifactDigests = artifactDigests; CommandRecords =
  commandRecords; Environment = environment; Builder = builder }`. Pure record construction only — no
  clock/filesystem/git, no normalization, sorting, or dedup of the artifact digests or command records (carriage
  is verbatim; canonicalization is `canonicalId`'s job, US2 — L-B4). Run T010: all carriage / totality
  assertions go green.

**Checkpoint**: US1 is functional — a complete, typed nine-fact provenance is built from supplied facts, total
over every edge case, with the sensed durations structurally apart inside the embedded records. MVP reached
(minus identity).

---

## Phase 4: User Story 2 — Mark the sensed metadata and project a stable canonical identity (Priority: P1)

**Goal**: The embedded records' durations are reachable as sensed metadata (`p.CommandRecords.[i].Duration`) and
structurally excluded from the identity; `canonicalId` renders only the reproducible facts to a byte-stable
`ProvenanceIdentity` using the F029/F032 tagged/length-prefixed/injective encoding — folding each command record
via F032 `CommandRecord.canonicalId` — so duration-only differences share an identity, any reproducible
difference changes it, the artifact digests are set-invariant while command-record order is significant, and the
same string in two fields yields different identities; `identityValue` unwraps it.

**Independent Test**: Two provenances sharing every reproducible fact but whose records differ only in duration
⇒ **equal** `canonicalId`; the durations are reachable and distinct from the reproducible facts; changing any
reproducible fact ⇒ **different** `canonicalId`; `canonicalId` computed twice ⇒ byte-identical; `identityValue`
returns the canonical string. (US2 acceptance #1–#4.)

### Tests for User Story 2 (write first)

- [X] T012 [P] [US2] `tests/.../IdentityTests.fs` — (a) sensed split: `p.CommandRecords.[i].Duration` is
  reachable and distinct from the reproducible facts; two provenances equal in every reproducible fact but whose
  embedded records differ only in `SensedDuration` have **equal** `canonicalId` (SC-003/SC-004, US2 #1/#3);
  (b) per-field sensitivity: flipping **any one** reproducible fact — `SourceCommit`, `Base`, `Head`, `RuleHash`,
  `GeneratorVersion`, an added/different `ArtifactHash`, a command record's reproducible fact, the
  command-record **order**, `Environment`, or `Builder` — changes `canonicalId`, tested field-by-field
  (SC-004, US2 #2); (c) injective across fields: the same revision string placed in two of `SourceCommit`/`Base`
  /`Head`, and the same string as `RuleHash` vs `GeneratorVersion`, yield **different** identities (L-I5);
  (d) idempotence: `canonicalId p` computed twice is byte-for-byte equal, and `identityValue (canonicalId p)` is
  the stable canonical string (SC-005, US2 #4); (e) worked example: a provenance built per
  contracts/provenance-identity-format.md renders to that contract's exact block (the durations not appearing in
  it; the `cmds` segment containing the full 135-byte F032 id). FsCheck the duration-invariance and per-field
  sensitivity laws; example-test the worked block and the injective-field cases.

### Implementation for User Story 2

- [X] T013 [US2] Implement `canonicalId` and `identityValue` in `Provenance.fs` per
  contracts/provenance-identity-format.md — render each reproducible field as a tagged, length-prefixed segment
  in the fixed field order (`src`, `base`, `head`, `rule`, `gen`, `art`, `cmds`, `env`, `bld`), joined by
  `'\n'` with no trailing newline, wrapped as `ProvenanceIdentity`. The three revisions, rule hash, generator
  version, and builder render as required strings (`<tag>=<presence><byteLen>:<value>`). **Artifact digests**
  (`art`): unwrap each `ArtifactHash`, **dedup + ordinal-sort** the strings, render as a counted list
  (`art=<count>;<len>:<v>;…`) ⇒ order/dup invariant (the F029 set discipline, L-I3). **Command records**
  (`cmds`): rendered in **given order**, **not** sorted or deduped (D4, L-I4); each entry is the **full F032
  canonical-id string** via `CommandRecord.identityValue (CommandRecord.canonicalId record)` (duration-free —
  F032), length-prefixed so its embedded `\n`/`:`/`;`/`=` cannot bleed across the boundary. **Environment**
  (`env`): render via a local total four-case match to the F029 tokens (`Local`→`local`, `Ci`→`ci`,
  `LocalOrCi`→`localOrCi`, `Release`→`release`), then as a required string (F029's `environmentToken` is not
  public — replicate the match locally, F029 stays untouched, D5). Use UTF-8 byte lengths for all prefixes.
  No durations read; no hashing (FR-011, L-I7). `identityValue (ProvenanceIdentity s) = s`. Pure string/list
  operations. Run T012: green. Re-run T010: still green.

**Checkpoint**: US1 + US2 — the complete provenance carries all nine facts, the sensed durations are
structurally apart inside the embedded records, and `canonicalId` is the byte-stable reproducible-only identity
(set-invariant over the artifact digests, order-significant over the command records). The full contract surface
is delivered.

---

## Phase 5: User Story 3 — The provenance and its identity are deterministic and pure over supplied data (Priority: P2)

**Goal**: `build` and `canonicalId` are pure, deterministic functions of the supplied facts: identical facts ⇒
identical provenance and identical identity; reordering/duplicating the artifact digests never changes the
identity (while command-record order does); no clock/filesystem/git/environment/network is read and no process
is spawned.

**Independent Test**: `build` + `canonicalId` twice from the same facts ⇒ structurally/byte equal. Reorder and
duplicate the artifact digests, rebuild, recompute ⇒ unchanged identity; reorder the command records ⇒ changed
identity. Recompute after changing cwd / creating a temp file ⇒ unchanged. (US3 acceptance #1–#3.)

### Tests for User Story 3 (write first)

- [X] T014 [P] [US3] `tests/.../DeterminismTests.fs` — (a) `build` then `canonicalId` called twice yields a
  structurally identical `Provenance` and byte-identical `ProvenanceIdentity` for representative and
  FsCheck-generated fact tuples (SC-005, US3 #1); (b) artifact-digest set order/dup invariance: supplying the
  artifact digests in a different order, and with duplicate entries, leaves `canonicalId` **unchanged**
  (SC-005, US3 #2, Edge: order/dup); (c) command-record order significance (the contrast): reordering
  `CommandRecords` **does** change `canonicalId` (D4, L-I4). Build the permutations with the Support
  artifact-digest/command-record order helpers and FsCheck generators.
- [X] T015 [P] [US3] `tests/.../PurityTests.fs` — a fixed `build`/`canonicalId` result is identical when
  recomputed after changing `Environment.CurrentDirectory` and after creating/deleting an unrelated temp file
  (and across repeated calls), demonstrating no clock/cwd/filesystem influence and no process spawn (SC-006,
  US3 #3).

**Note**: US3 has no new implementation task — determinism/purity are properties of the `build`/`canonicalId`
built in US1+US2 (record construction and the `List.map`/`List.distinct`/`List.sortWith` + segment-building
pipeline are pure and, for the artifact digests, order-independent by construction). If T014–T015 reveal a gap,
fix the operation (never weaken a test).

**Checkpoint**: All three stories functional and independently testable — the provenance and its identity are
complete, correct, deterministic, and pure.

---

## Phase 6: Cross-cutting Tier-1 surface obligations

**Purpose**: The surface baseline + scope guard and the no-regression promise.

- [X] T016 `tests/.../SurfaceDriftTests.fs` — the reflective surface test (F029/F030/F031/F032 precedent):
  render the assembly's public surface, compare to `surface/FS.GG.Governance.Provenance.surface.txt` with the
  `BLESS_SURFACE=1` re-bless path; assert exactly the two public modules (`Model`, `Provenance`) export and
  no helper leaks; **scope-hygiene**: referenced assemblies are only `FSharp.Core`,
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.CommandRecord`, `FS.GG.Governance.Config`, and BCL
  (`System.*`/`netstandard`/`mscorlib`) — NOT `Gates`/`Route`/`Routing`/`Findings`/`Snapshot`/`EvidenceReuse`/
  `RouteExplain`/`RouteJson`/`GatesJson`/`AuditJson`/`Enforcement`/`Ship`/`Adapters.*`/`Host`/`Cli` (plan D1,
  contracts negative scope guard). Note the three permitted sibling references (the first multi-core
  reference — D1) vs F032's single Config reference. **Note (convention, not test-guarded):** the `env`-token
  set (`local`/`ci`/`localOrCi`/`release`) is replicated locally from F029 (D5) — it currently matches F029's
  private `environmentToken` exactly, but no test enforces that match because F029's helper is not public. This
  is acceptable: the provenance identity is self-contained (it only needs to be injective and byte-stable), so
  a future divergence in F029's tokens would not break this core's contract — only the cosmetic "same as F029"
  parity. Do **not** add a cross-core token test (it would require widening F029's surface).
- [X] T017 Generate the committed baseline `surface/FS.GG.Governance.Provenance.surface.txt` by running the
  suite once with `BLESS_SURFACE=1`, then review the file by eye against `Model.fsi`/`Provenance.fsi` to
  confirm it contains exactly the intended surface (the two modules `Model`/`Provenance`, three vals
  `build`/`canonicalId`/`identityValue`, and the declared new types `BuilderIdentity`/`ProvenanceIdentity`/
  `Provenance`). Commit it. (After T016.)
- [-] T018 [P] README cores pointer — **SKIPPED: list still frozen at F18.** Confirmed `README.md`'s enumerated
  core list ends at `FS.GG.Governance.Gates … (F18, done)`; F19–F33 were never added. Per that precedent the
  list is NOT partially extended for F033 alone — no README change made.

**Checkpoint**: Tier-1 obligations met; the public surface is pinned.

---

## Phase 7: Validation & polish

- [X] T019 Run `dotnet test tests/FS.GG.Governance.Provenance.Tests/...` — all suites green; capture the run
  as evidence (Principle V).
- [X] T020 Run `dotnet test FS.GG.Governance.sln` and confirm the no-regression promise (SC-007): existing
  projects' tests and **every** existing `surface/*.surface.txt` baseline (including F014 Config, F029
  FreshnessKey, F032 CommandRecord) are unchanged; only the new project's tests and the new surface baseline are
  added. Confirm `src/**` and existing `surface/**` show no diff.
- [X] T021 [P] Run `dotnet fsi scripts/prelude.fsx` end-to-end and confirm the F033 section's printed results
  now match the real `build`/`canonicalId`/`identityValue` output, including the worked-example identity block
  (Principle I evidence, closes T007).
- [X] T022 [P] Walk `quickstart.md` top-to-bottom (build → FSI → test → re-bless → no-regression) and fix any
  drift between the guide and reality. Confirm the `CLAUDE.md` plan pointer targets
  `specs/033-provenance-record/plan.md` (it already does — leave it if so).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → after Phase 1; **BLOCKS** all stories (the `.fsi` + stubs + Support must exist
  to compile any test).
- **Phase 3 (US1)** → after Phase 2. Delivers `build` (the complete nine-fact provenance) — the MVP.
- **Phase 4 (US2)** → after **US1** specifically (`canonicalId`/`identityValue` operate on the `Provenance`
  that `build` produces; the tests construct provenances via `build`). Independently testable once identity
  lands (T013).
- **Phase 5 (US3)** → after US1+US2 (determinism/purity are properties of the completed `build`/`canonicalId`).
  No new implementation; tests only.
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

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (`build` — the complete nine-fact provenance, total
over every edge case) → 4. **STOP & VALIDATE**: every declared fact reads back verbatim, the command records are
carried whole and in order, no-records/no-artifacts/equal-base-head/failed-or-timed-out-record builds are
ordinary values.

### Incremental delivery

US1 (complete typed provenance = MVP) → US2 (sensed split + canonical identity: `canonicalId`/`identityValue`) →
US3 (determinism/purity proof) → cross-cutting surface obligations → full-suite validation. Each phase is
independently testable and adds value without breaking the previous.

---

## Notes

- Tier 1 throughout: any public-surface change requires re-blessing
  `surface/FS.GG.Governance.Provenance.surface.txt` (`BLESS_SURFACE=1`).
- `[P]` = different files, no in-phase dependency. `[USx]` maps a task to its spec user story.
- Principle IV (Elmish/MVU) is **N/A** — three pure total functions, no state/I/O (recorded once here, not per
  task). The nine build facts are values handed in, never sensed here.
- No mocks anywhere (Principle V); all inputs are real, literally-constructible typed values (incl. real F032
  `CommandRecord`s built via `CommandRecord.build`; the facts a host would sense are supplied as literals — the
  core's contract). No process is spawned. No `Synthetic` disclosure needed.
- **First core to reference three sibling cores** (FreshnessKey + CommandRecord + Config — D1), mandated by the
  verbatim-reuse requirement FR-010; all three are pure vocab cores, so nothing impure is pulled in and the
  transitive `YamlDotNet` (via Config) is unused. F014 `EnvironmentClass`, F029 `Revision`/`RuleHash`/
  `GeneratorVersion`/`ArtifactHash`, and F032 `CommandRecord` (+ its public `canonicalId`/`identityValue`) are
  consumed verbatim; none modified (FR-010, SC-007).
- The sensed durations live inside the embedded F032 records and are structurally excluded from the identity
  (D3) — `canonicalId` folds each record via F032 `CommandRecord.canonicalId`, which never reads `.Duration`.
  The artifact digests are a SET in the identity (order/dup ignored); the command records are an ORDERED list
  whose order is significant (D4).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
