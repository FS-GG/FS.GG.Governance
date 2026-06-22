---
description: "Task list for Persist, Bound, And Prune The Evidence-Reuse Store (F047)"
---

# Tasks: Persist, Bound, And Prune The Evidence-Reuse Store

**Input**: Design documents from `/specs/047-evidence-reuse-store-write/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅ (D1–D9), data-model.md ✅,
contracts/EvidenceReuseStore.fsi ✅, quickstart.md ✅

**Tier**: **Tier 1 (contracted change)** — a new public library + module
(`FS.GG.Governance.EvidenceReuseStore`) with a new package identity and a new `surface/*.surface.txt`
baseline. No existing public surface changes; no third-party dependency is added. Tests are **mandatory**
(Principle V). All tasks share the feature tier; no per-task `[T1]`/`[T2]` annotations needed.

**Elmish/MVU**: **Not applicable** — three pure, total, deterministic value transformations
(`serialise`/`retain`/`prune`) with no I/O (FR-009). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks
(plan Constitution Check, Principle IV exempt — the F042 pure-projection precedent). The on-disk write
(atomic temp+rename, store-path discovery, the writer port) and the production of *real* evidence references
(needs gate execution) are explicit **later host rows** — out of scope. Principle VI is likewise N/A — pure
total functions have no failure path to observe; **totality** stands in for safe failure (no operation throws).

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.
Stories map to spec user stories — US1 (P1, MVP) `serialise`, US2 (P2) `retain`, US3 (P3) `prune`.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]`/`[US2]`/`[US3]` traceability; unlabeled = shared infrastructure
- Exact repo-root-relative file paths in every description

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new pure library + its focused test project so everything compiles and the solution
restores. No semantics yet. Nothing existing is edited beyond the solution file and `CLAUDE.md`.

- [X] T001 Create `src/FS.GG.Governance.EvidenceReuseStore/FS.GG.Governance.EvidenceReuseStore.fsproj` —
  SDK-style, `net10.0`, `RootNamespace`/`PackageId` `FS.GG.Governance.EvidenceReuseStore`, `Version` `0.1.0`,
  `IsPackable=true` with a `PackageId` (override `Directory.Build.props`, the
  `EvidenceReuse`/`CacheEligibility`/`CacheEligibilityJson` precedent — this row adds a **new package identity**;
  "pack output unaffected" in the plan means *existing* packages are untouched, not that this library is
  unpackable). `<Compile>` order: `EvidenceReuseStore.fsi`, then `EvidenceReuseStore.fs`. **One**
  `<ProjectReference>` — to `../FS.GG.Governance.EvidenceReuse/...` (F030
  `ReuseStore`/`RecordedEvidence`/`EvidenceRef`/`record`/`entries`/`decide`). The F029 `FreshnessKey`
  (`FreshnessInputs`/`matches`/`RuleHash`/`ArtifactHash`/`CommandVersion`/`GeneratorVersion`/`Revision`) and
  F014 `Config` (`CheckId`/`DomainId`/`CommandId`/`EnvironmentClass`) types the `.fs` unwraps/opens arrive
  **transitively** through F030 (SDK `ProjectReference`s flow by default) and need **no** direct reference —
  the minimal-reference precedent of F030 (references only `FreshnessKey`), F041 (only `EvidenceReuse`+`Gates`),
  and F042 (only `CacheEligibility`); research D1. This keeps the reference graph minimal and matches the T019
  scope-hygiene assertion exactly. **No third-party `PackageReference`** (FR-011) —
  serialisation is the net10.0 shared-framework `System.Text.Json` (`Utf8JsonWriter`). Add a header comment
  mirroring the `CacheEligibilityJson`/`EvidenceReuse` `.fsproj`: pure total **write half** of the evidence-reuse
  store (serialise/retain/prune); the deterministic inverse of the F046 `FreshnessSensing` reader plus
  bounded-retention + superseded-world pruning; reuses F030 `record`/`entries` + F029 `matches` verbatim;
  one-way dependency `EvidenceReuseStore -> EvidenceReuse -> FreshnessKey -> Config`; computes NO persistence,
  produces NO real evidence, runs NO gate, bumps NO schema version; no host/edge/CLI coupling.
- [X] T002 [P] Create
  `tests/FS.GG.Governance.EvidenceReuseStore.Tests/FS.GG.Governance.EvidenceReuseStore.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  new package). `<ProjectReference>`s to the new library **and** `FS.GG.Governance.EvidenceReuse`,
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config` (to build **real** stores via the genuine
  `EvidenceReuse.record` over real F029 `FreshnessInputs` and to call `EvidenceReuse.decide`/`entries`), **and**
  `FS.GG.Governance.FreshnessSensing` (F046 — the **real** `realStoreReader` the round-trip drives, research D3).
  `<Compile>` order: `Support.fs`, `RoundTripTests.fs`, `DeterminismTests.fs`, `RetentionTests.fs`,
  `PruningTests.fs`, `SafetyTests.fs`, `TotalityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`. Mirror
  `tests/FS.GG.Governance.CacheEligibility.Tests/...Tests.fsproj`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` (between `<!-- SPECKIT START/END -->`) at
  `specs/047-evidence-reuse-store-write/plan.md`. No other doc changes.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; `dotnet sln list` shows both new projects.

---

## Phase 2: Foundational (the `.fsi` contract, FSI proof, compiling stub, test scaffolding) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add a compiling `.fs`
body (real constants + stubbed operations) plus test scaffolding so the library and tests compile and tests
can FAIL before implementation. **⚠️ No story work begins until this phase is complete** — the `.fsi`
declares all five members, so the `.fs` must satisfy the full signature to compile.

- [X] T005 Author `src/FS.GG.Governance.EvidenceReuseStore/EvidenceReuseStore.fsi` — drop
  `contracts/EvidenceReuseStore.fsi` verbatim: `namespace FS.GG.Governance.EvidenceReuseStore`; `open
  FS.GG.Governance.EvidenceReuse.Model` (`ReuseStore`); the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module EvidenceReuseStore` with
  exactly five members — `val schemaVersion: string`, `val defaultRetentionBound: int`,
  `val serialise: store: ReuseStore -> string`, `val retain: maxEntries: int -> store: ReuseStore ->
  ReuseStore`, `val prune: store: ReuseStore -> ReuseStore` — each carrying its curated purity/totality/
  recompute-safety doc-comment verbatim. Reuses merged core types verbatim; redefines none. **No** access
  modifiers anywhere (Principle II — visibility is presence/absence in this `.fsi`).
- [X] T006 Add `src/FS.GG.Governance.EvidenceReuseStore/EvidenceReuseStore.fs` — the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module EvidenceReuseStore` with
  the real `schemaVersion = "fsgg.evidence-reuse-store/v1"` constant and the real `defaultRetentionBound = 256`
  constant (these are data — define them fully; research D8), and `serialise`/`retain`/`prune` as `failwith
  "not implemented"` stubs. No `private`/`internal`/`public` modifiers (Principle II). Confirm `dotnet build
  src/FS.GG.Governance.EvidenceReuseStore/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Write `tests/FS.GG.Governance.EvidenceReuseStore.Tests/Support.fs` — real,
  literally-constructible store builders (Principle V, **no mocks, no re-implemented parser**): an `inputs`
  builder producing a complete literal `FreshnessInputs` (every category present and distinct so loss is
  observable, incl. an `Environment` from each `EnvironmentClass` case, a `Command`/`CommandVersion` both
  `Some` and `None` variant, and a present-but-empty `CoveredArtifacts = []` variant — the sensed-empty edge,
  D5); a `storeOf` helper folding the genuine `EvidenceReuse.record` over `EvidenceReuse.empty` (real F029
  inputs, opaque **`Synthetic`** evidence refs — disclosed; real refs need gate execution, Assumptions); a
  `readBack : string -> ReuseStore option` helper that writes the text to a `Path.GetTempFileName()` temp file,
  calls the **real** `FreshnessSensing.realStoreReader path`, and maps `Ok (Some s) -> Some s` / `Ok None ->
  None` (research D3 — the only public load path reads a file); FsCheck generators for arbitrary well-typed
  `ReuseStore` values (varying every `FreshnessInputs` category, covered-artifact lists incl. `[]` and
  multi-element verbatim order, `Some`/`None` optionals, store length incl. empty/singleton/large, and
  deliberately-superseded entries for pruning) plus a generator for arbitrary candidate `FreshnessInputs`; and
  the `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper from the F041/F042 `Support.fs`
  precedent. No I/O beyond repo-root resolution and the round-trip temp file.
- [X] T008 [P] Write `tests/FS.GG.Governance.EvidenceReuseStore.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.
- [X] T009 [P] Append an F047 design-first section to `scripts/prelude.fsx` (after the F046 section) —
  Principle-I FSI proof **before** any operation body lands: `#r` the new Debug DLL plus the `EvidenceReuse`,
  `FreshnessKey`, `Config`, and `FreshnessSensing` DLLs; `open` the model + operation modules; build a real
  store via the genuine `EvidenceReuse.record` (disclosed **`Synthetic`** evidence refs) and exercise the
  quickstart §2 sketch verbatim — `serialise store` prints a single `fsgg.evidence-reuse-store/v1` document;
  the round-trip through the **real** `FreshnessSensing.realStoreReader` (temp file) prints `round-trip equal:
  true`; `serialise store = serialise store` prints byte-stable `true`; the empty store prints
  `{"schemaVersion":"...","recorded":[]}`; `retain 1 store` prints a length-1 store; `prune store = store`
  prints `true` for a `record`-built store. Documents the shape even while the bodies are stubbed.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because `serialise`/`retain`/`prune` are stubs (not because of compile errors); `dotnet fsi
scripts/prelude.fsx` loads the F047 section (its assertions fail against the stubs — expected).

---

## Phase 3: User Story 1 — Persist a reuse store so a later run can warm the cache (Priority: P1) 🎯 MVP

**Goal**: A pure, total, byte-stable `serialise : ReuseStore -> string` — the deterministic inverse of the
F046 read-only deserializer — so an in-memory `ReuseStore` can be written back in the
`fsgg.evidence-reuse-store/v1` format and the **next** run's reader loads it. This is the whole point of the
row: without it the store is permanently empty and every cache verdict is `mustRecompute noPriorEvidence`.

**Independent Test**: Build a non-empty `ReuseStore` via the genuine `EvidenceReuse.record`, `serialise` it,
feed the bytes to the **real** `FreshnessSensing.realStoreReader` (temp file), and assert the loaded store
**equals** the input (round-trip identity); serialise the same value twice and assert byte-identical output
(determinism); the empty store round-trips as the empty store.

### Tests for User Story 1 (write first; must FAIL against the stub) ⚠️

- [X] T010 [P] [US1] `tests/FS.GG.Governance.EvidenceReuseStore.Tests/RoundTripTests.fs` — drive `serialise`
  then the **real** `readBack` (real `realStoreReader`, never a re-implemented parser — research D3): (1)
  **lossless round-trip** (SC-001, FR-002) — for FsCheck-generated and worked-example non-empty stores,
  `readBack (serialise store) = Some store`, preserving every entry's full freshness-input set and opaque
  evidence reference, in the same **newest-first** order with each entry's `CoveredArtifacts` in its **stored
  list order** (D5/D7 — `ReuseStore` equality is list-order-sensitive). (2) **empty store** (SC-003, FR-005) —
  `serialise EvidenceReuse.empty` is a well-formed `{"schemaVersion":"fsgg.evidence-reuse-store/v1","recorded":[]}`
  document (a present, empty `recorded` array, not an empty/whitespace file) that `readBack`s as the empty
  store; an **absent** file also loads as the empty store (assert via `realStoreReader` on a non-existent path
  ⇒ `Ok None`). (3) **opaque evidence + JSON-escaping edge** (FR-004) — a store whose `EvidenceRef` contains
  characters needing JSON escaping (quotes, backslash, control chars) round-trips **verbatim**, the reference
  never parsed/re-hashed/interpreted. (4) **sensed-empty covered set** — an entry with `CoveredArtifacts = []`
  round-trips as an empty list (the F029/F043 sensed-empty-vs-unsensed distinction preserved, Edge Cases).
- [X] T011 [P] [US1] `tests/FS.GG.Governance.EvidenceReuseStore.Tests/DeterminismTests.fs` — (SC-002, FR-003):
  (1) **byte-for-byte determinism** — `serialise store = serialise store` for worked-example + FsCheck stores,
  including a purity check (identical text when computed in different working directories, at different times,
  with unrelated filesystem state changed between calls; no I/O). (2) **stable field/entry order** — assert the
  top-level key order (`schemaVersion`, `recorded`) and the per-entry field order (`check`, `domain`,
  `command`(if present), `environment`, `ruleHash`, `coveredArtifacts`, `commandVersion`(if present),
  `generatorVersion`, `base`, `head`, `evidence`) via raw-text key positions (D7 — order is part of the
  contract); entries emitted in the store's verbatim newest-first order, `coveredArtifacts` verbatim, never
  re-sorted (D5). (3) **optional-field rendering** (D4) — `Command = None`/`CommandVersion = None` ⇒ the
  property is **omitted** (not `null`); `Some` ⇒ a JSON string; assert byte-stability across the omit choice.

### Implementation for User Story 1

- [X] T012 [US1] Implement `serialise` in `src/FS.GG.Governance.EvidenceReuseStore/EvidenceReuseStore.fs` per
  data-model.md §`serialise` and research D2/D4/D5/D6/D7 — a hidden `writeToString (emit: Utf8JsonWriter ->
  unit) : string` helper over a `MemoryStream` with default (compact, non-indented) options, UTF-8 → string
  (the F042 `CacheEligibilityJson` / F025 `AuditJson` / kernel `Json.fs` precedent); a single linear walk:
  top-level object writes `schemaVersion` (the fixed literal) then `recorded` as an array `[ for entry in
  EvidenceReuse.entries store -> writeEntry w entry ]` in **verbatim newest-first order**; `writeEntry` writes
  the fixed field order, **omitting** `command`/`commandVersion` on `None` (D4), emitting `coveredArtifacts`
  as the `ArtifactHash` strings in **stored list order, verbatim** — never sorted/de-duped (D5), rendering
  `EnvironmentClass` via the **exhaustive, wildcard-free** inverse of the reader's `parseEnv`
  (`Local`→`"local"`/`Ci`→`"ci"`/`LocalOrCi`→`"local-or-ci"`/`Release`→`"release"` — D6, so a new case is a
  compile error here), and rendering every newtype string + the opaque `EvidenceRef` **verbatim** via its
  unwrapped value (FR-004 — never parsed/re-hashed). Pure `Utf8JsonWriter` walk + `FSharp.Core`/`System.Text.Json`
  only; no clock/filesystem/git/environment/network; total — the empty store yields a present empty `recorded`
  array, never throws (FR-005/FR-009). No access modifiers (Principle II). Run T010/T011: green.

**Checkpoint**: US1 is functional — a real store round-trips losslessly through the **real** F046 reader and
serialises byte-stably. **This is the shippable MVP**: the store is now writable and the cache can warm on the
next run (once the deferred host write row lands).

---

## Phase 4: User Story 2 — Keep the store bounded so it cannot grow without limit (Priority: P2)

**Goal**: A pure, total, deterministic `retain : maxEntries:int -> ReuseStore -> ReuseStore` that bounds the
store to a deterministic maximum, keeping the **newest** entries (the head of the newest-first list — the
worlds a future run is most likely to match) and removing only whole entries, so disk/read cost stays flat
without ever causing a stale reuse.

**Independent Test**: Apply `retain` to a store built past the bound and assert the result is within the
bound, retains the newest entries newest-first, every survivor is byte-for-byte one of the inputs, and a
store at/under the bound is returned unchanged (idempotent).

### Tests for User Story 2 (write first; must FAIL against the stub) ⚠️

- [X] T013 [P] [US2] `tests/FS.GG.Governance.EvidenceReuseStore.Tests/RetentionTests.fs` — (SC-004, FR-006,
  D8): (1) **bounded + newest-retained** — for a store longer than the bound, `retain n store` has length ≤
  `max 0 n` and its entries are exactly the **first `n`** of the newest-first input (the head), in order
  (US2 acceptance 1). (2) **idempotent at/under bound** — a store of length ≤ `n` is returned **unchanged** —
  no reorder, no rewrite: `retain n store = store` (US2 acceptance 2). (3) **removal-only** — every retained
  entry is byte-for-byte one of the inputs; nothing mutated or fabricated; survivors are a prefix subset of
  the input. (4) **totality / boundary** — `retain 0`/`retain (negative)` ⇒ the empty store; `retain n` of the
  empty store ⇒ empty; never throws (FsCheck over arbitrary `n` and stores, SC-008). (5) **`defaultRetentionBound`
  is a positive constant usable as the `n` argument** — assert `defaultRetentionBound > 0` and that `retain
  defaultRetentionBound store` obeys the bound; also pin its current documented value (`= 256`, research D8)
  so an unintended change is caught, while noting the exact number is a mechanism detail, not a contract (spec
  Assumptions / `.fsi`).

### Implementation for User Story 2

- [X] T014 [US2] Implement `retain` in `src/FS.GG.Governance.EvidenceReuseStore/EvidenceReuseStore.fs` per
  data-model.md §`retain` and research D8 — `retain maxEntries store` keeps the newest `maxEntries` entries via
  `List.truncate (max 0 maxEntries)` over `EvidenceReuse.entries store`, re-wrapped as a `ReuseStore`. `max 0`
  guards negatives (⇒ empty); `List.truncate` returns the list unchanged when shorter than the bound
  (idempotent at/under bound — no reorder/rewrite). Removes only whole entries — never mutates or fabricates.
  Pure / total / no I/O (FR-006/FR-009). Recompute-safe by construction — removing entries can only turn a
  future `Reuse` into `Recompute`, never the reverse (FR-008; the cross-cutting property is pinned in T018).
  No access modifiers (Principle II). Run T013: green. (Same file as T012/T015 — sequence after T012.)

**Checkpoint**: US1 + US2 — the store is writable **and** bounded: it cannot grow without limit, and eviction
keeps the highest-value (newest) evidence without ever manufacturing a reuse.

---

## Phase 5: User Story 3 — Prune dead evidence the store can never reuse again (Priority: P3)

**Goal**: A pure, total, deterministic `prune : ReuseStore -> ReuseStore` that removes every entry a
strictly-newer entry already `FreshnessKey.matches` (a superseded freshness world for its gate — F030 `decide`
always serves the most-recent full match), keeping the newest entry per world-class in newest-first order, so
the store carries no entry that can never be served — while never causing a reuse that would not otherwise
have happened.

**Independent Test**: Build a store with a strictly-superseded entry, apply `prune`, and assert only entries a
future reuse decision could still serve remain (a newest-first subset); a store with no dead entries (e.g. any
`record`-built store) is unchanged; and for any candidate the pruned store yields a verdict no more permissive
than the unpruned store.

### Tests for User Story 3 (write first; must FAIL against the stub) ⚠️

- [X] T015 [P] [US3] `tests/FS.GG.Governance.EvidenceReuseStore.Tests/PruningTests.fs` — (SC-005, FR-007,
  FR-010, D9): (1) **superseded removed, subset, ordered** — a store with an older entry whose freshness world
  a strictly-newer entry `FreshnessKey.matches` ⇒ `prune` removes the older entry; survivors are a **subset of
  the input in newest-first order** (US3 acceptance 1). (2) **no-op on clean stores** — a store with no dead
  entry (e.g. any `EvidenceReuse.record`-built store, already full-match-deduped, or an all-distinct-worlds
  store) is returned **unchanged**: `prune store = store` (US3 acceptance 2). (3) **verdict-preserving /
  no-more-permissive** — for FsCheck-generated candidates × stores, `EvidenceReuse.decide candidate (prune
  store)` is identical-or-stricter than `decide candidate store` — never more permissive (US3 acceptance 3,
  the no-spurious-reuse invariant; in fact verdict-identical, since the newest full-match and the
  cause-locating entry are both retained). (4) **reuses F029 `matches` verbatim** — the dead-entry criterion
  is exactly `FreshnessKey.matches` (no new policy, FR-010); duplicate-world entries collapse to the newest.
  (5) **totality** — defined for empty / singleton / all-distinct / all-superseded; never throws (SC-008).

### Implementation for User Story 3

- [X] T016 [US3] Implement `prune` in `src/FS.GG.Governance.EvidenceReuseStore/EvidenceReuseStore.fs` per
  data-model.md §`prune` and research D9 — a single fold over `EvidenceReuse.entries store` (newest-first)
  keeping an entry iff **no already-kept (strictly newer) entry** satisfies `FreshnessKey.matches kept.Inputs
  entry.Inputs` — i.e. `record`'s full-match dedup generalised across the whole store — re-wrapped as a
  `ReuseStore`, survivors in their original newest-first order. Reuses the F029 `matches` relation **verbatim**
  (FR-010 — no new freshness-match rule). Pure / total / no I/O (FR-007/FR-009); verdict-preserving hence
  recompute-safe (FR-008; cross-cutting property pinned in T018). No access modifiers (Principle II). Run T015:
  green. (Same file as T012/T014 — sequence after them.)

**Checkpoint**: US1 + US2 + US3 — the full **write half** of the store lifecycle: writable, bounded, and
self-pruning. The store can warm the cache, stays flat in size, and carries no dead entry — all three
operations provably recompute-safe.

---

## Phase 6: Cross-cutting — recompute-safety invariant + totality (spans all stories)

**Purpose**: Pin the two cross-cutting guarantees the whole row turns on — the no-spurious-reuse invariant
(FR-008, SC-006) over all three operations, and the purity/totality of the public surface (FR-009, SC-008).
Depends on T012 + T014 + T016 (all three operations implemented).

- [X] T017 [P] `tests/FS.GG.Governance.EvidenceReuseStore.Tests/SafetyTests.fs` — the cross-cutting
  **recompute-safety invariant** (SC-006, FR-008, data-model §"recompute-safety invariant"): an FsCheck
  property over generated candidates `c` and stores `s` asserting, for **each** operation `op ∈ {
  serialise∘readBack, retain n, prune }`, that it is **never** the case that `EvidenceReuse.decide c s =
  Recompute _` while `EvidenceReuse.decide c (op s) = Reuse _` — i.e. no operation turns a `mustRecompute`
  candidate into a `reusable` one. For `serialise∘readBack` the verdicts are **identical** (round-trip equal
  store, modulo the absent-vs-empty file mapping); for `retain`/`prune` they are identical-or-stricter
  (removing entries can only lose a potential `Reuse`, never create one). This single property is the row's
  load-bearing safety proof.
- [X] T018 [P] `tests/FS.GG.Governance.EvidenceReuseStore.Tests/TotalityTests.fs` — (SC-008, FR-009): an
  FsCheck property over arbitrary well-typed stores (empty, singleton, large, all-distinct, all-superseded,
  duplicate-world, sensed-empty covered sets) and arbitrary `maxEntries` (incl. 0 and negatives) asserting
  every operation returns a value and **never throws**, exercised with **no** filesystem/clock/network access
  (the round-trip's temp file is the only test I/O, isolated to `RoundTripTests`/`Support` — the operations
  themselves touch nothing). `serialise` always returns a parseable `v1` document; `retain`/`prune` always
  return a well-formed `ReuseStore`.

**Checkpoint**: the two cross-cutting contracts hold over generated inputs — no operation manufactures a reuse,
and every operation is pure and total. SC-001…SC-006 and SC-008 are pinned.

---

## Phase 7: Surface governance & polish (Tier-1 baseline, scope hygiene, validation)

**Purpose**: Lock the public surface (Principle II), prove the assembly's reference graph stays minimal and
the change is additive (SC-007), and run the quickstart end-to-end. Bless the baseline only after the surface
is final (the Phase-2 `.fsi` is unchanged through implementation).

- [X] T019 `tests/FS.GG.Governance.EvidenceReuseStore.Tests/SurfaceDriftTests.fs` — a reflective `SurfaceDrift`
  test (the F020–F042 precedent): enumerate the public surface of `FS.GG.Governance.EvidenceReuseStore` and
  compare byte-for-byte to `surface/FS.GG.Governance.EvidenceReuseStore.surface.txt`, with the `BLESS_SURFACE=1`
  re-bless path; plus a **scope-hygiene** assertion (Principle II, plan Engineering Constraints) that the
  assembly references **only** `FS.GG.Governance.EvidenceReuse` and — transitively —
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config`, plus `FSharp.Core` / BCL — and **not**
  `FreshnessSensing`, `CacheEligibility`, `RouteJson`, `AuditJson`, `Enforcement`, `Ship`, `Snapshot`,
  `Routing`, `Findings`, any `Adapters.*`, `Host`, `Cli`, and no third-party package (serialisation is the
  shared-framework `System.Text.Json`). Mirror `tests/FS.GG.Governance.CacheEligibility.Tests/SurfaceDriftTests.fs`.
- [X] T020 Generate and commit `surface/FS.GG.Governance.EvidenceReuseStore.surface.txt` via `BLESS_SURFACE=1
  dotnet test tests/FS.GG.Governance.EvidenceReuseStore.Tests/...`; review the diff (exactly the one public
  module `EvidenceReuseStore` with `schemaVersion` + `defaultRetentionBound` + `serialise` + `retain` +
  `prune`; no `writeToString` / token-writer / fold-helper leak) and commit it as part of the Tier-1 change.
  After this, T019 runs green without `BLESS_SURFACE`. **No existing baseline is re-blessed** — this row adds
  one new baseline and touches no merged F029–F046 surface or golden (SC-007).
- [X] T021 [P] Verify SC-007 (additive-only) by inspection: `git status` / `git diff` shows **no** edit to any
  merged F029–F046 core (`src/FS.GG.Governance.EvidenceReuse/**`, `src/FS.GG.Governance.FreshnessKey/**`,
  `src/FS.GG.Governance.FreshnessSensing/**`, the F041–F046 cores) or to any existing test project, and `git
  diff surface/` touches **only** the one new `EvidenceReuseStore` baseline; no schema-version bump (the
  `fsgg.evidence-reuse-store/v1` token is consumed, never changed), no golden-fixture re-bless (do **not** run
  `BLESS_FIXTURES=1`).
- [X] T022 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`; `dotnet fsi
  scripts/prelude.fsx` (the F047 section prints `round-trip equal: true`, `byte-stable: true`, the empty-store
  document, the length-1 `retain`, and `prune no-op: true`); `dotnet test
  tests/FS.GG.Governance.EvidenceReuseStore.Tests/...` (all green under `TreatWarningsAsErrors`, incl. the
  round-trip against the **real** reader, determinism, retention, pruning, safety, totality, and surface
  drift). Confirm `dotnet build && dotnet test` over the existing projects is unchanged (the new library + test
  project are purely additive). Fix any drift.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-008 covered; the Tier-1 surface is
blessed and guarded with a minimal reference graph; existing F029–F046 cores, baselines, and goldens
byte-unchanged. **The deferred cache-store *write half* (serialise + bounded retention + superseded-world
pruning) is closed** — the read-only evidence-reuse store is now writable, bounded, and self-pruning as a pure
core; the impure on-disk persistence and real-evidence production remain the explicit later host rows.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` surface declares all
  five members, so the compiling stub `.fs`, FSI proof, and test scaffolding (`Support.fs`, `Main.fs`) must
  exist before any story test can be written and FAIL.
- **Phase 3 (US1 `serialise`)**: depends on Phase 2. The MVP.
- **Phase 4 (US2 `retain`)**: depends on Phase 2; its `.fs` edit sequences **after T012** (same file
  `EvidenceReuseStore.fs`), but is otherwise independent of US1's serialise body.
- **Phase 5 (US3 `prune`)**: depends on Phase 2; its `.fs` edit sequences **after T012/T014** (same file).
- **Phase 6 (cross-cutting)**: depends on **T012 + T014 + T016** (all three operations implemented) — the
  safety and totality properties exercise all three.
- **Phase 7 (surface/polish)**: last — bless the baseline only after the surface is final (Phase-2 `.fsi`
  unchanged through implementation).

### Within each story

- Each story's test file is written FIRST and must FAIL against the Phase-2 stub, then pass after its
  implementation task lands (T010/T011→T012; T013→T014; T015→T016).
- The `.fsi` surface precedes the `.fs` body that satisfies it; `Support.fs` precedes every story test file
  that consumes its builders/generators/`readBack` helper.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) and T004 `[P]` (CLAUDE.md) are independent of T001 (library
  `.fsproj`); T003 (sln) needs T001 + T002.
- **Phase 2**: T005 (`.fsi`) precedes T006 (stub `.fs`); T007/T008/T009 are `[P]` against each other (distinct
  files — `Support.fs`, `Main.fs`, `scripts/prelude.fsx`) and need the compiling stub (DLL name fixed by T001).
- **Story test files are all `[P]`** relative to each other (distinct files): T010, T011, T013, T015, T017,
  T018 touch different test files. They share `Support.fs` (T007) as a prerequisite.
- **Implementation tasks T012→T014→T016 are sequential** — they edit the same `EvidenceReuseStore.fs`.
- **Phase 6**: T017/T018 are `[P]` (distinct files), both gated on T016.
- **Phase 7**: T021 `[P]` (git inspection) is independent; T019→T020→T022 are sequential (bless after the
  surface test, validate after the bless).

---

## Task count per user story

| Group | Tasks | Count |
|---|---|---|
| Phase 1 — Setup | T001–T004 | 4 |
| Phase 2 — Foundational (`.fsi` + stub + scaffolding + FSI proof) | T005–T009 | 5 |
| Phase 3 — US1 `serialise` (MVP) | T010–T012 | 3 (2 test, 1 impl) |
| Phase 4 — US2 `retain` | T013–T014 | 2 (1 test, 1 impl) |
| Phase 5 — US3 `prune` | T015–T016 | 2 (1 test, 1 impl) |
| Phase 6 — Cross-cutting (safety + totality) | T017–T018 | 2 (2 test) |
| Phase 7 — Surface & polish | T019–T022 | 4 |
| **Total** | | **22** |

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1 — `serialise`)**, T001–T012 — the project skeleton, the `.fsi` surface +
FSI proof, and the deterministic, byte-stable serialiser proven lossless against the **real** F046 reader.
This is the spec's P1 reason-to-exist slice: the read-only store becomes writable, so the cache can warm on
the next run. Phase 4 (US2, P2) keeps it bounded; Phase 5 (US3, P3) prunes dead evidence; Phase 6 pins the
cross-cutting recompute-safety and totality guarantees; Phase 7 locks the Tier-1 surface and additive-only
hygiene.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify each story's test FAILS against the Phase-2 stub before implementing its body, then passes.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- **Synthetic evidence (Principle V)**: fixtures build stores via the genuine `EvidenceReuse.record` but with
  opaque **`Synthetic`** evidence references (disclosed at the use site and in the PR) — a *real* `EvidenceRef`
  needs gate execution, the deferred row (Assumptions). The round-trip drives the **real**
  `FreshnessSensing.realStoreReader` (real bytes via a temp file), never a re-implemented parser (research D3).
- F029/F030/F041–F046 cores, their surfaces, and their golden baselines stay byte-unchanged; no schema bump
  (`fsgg.evidence-reuse-store/v1` consumed, never changed) — verified in T019/T020/T021 (SC-007).
- The on-disk write (atomic temp+rename, store-path discovery, the writer port, the `--store` write flag, and
  any `fsgg route`/`fsgg ship` wiring) and the production of real evidence references are explicit **later host
  rows** — out of scope here (Assumptions / Out of Scope).
