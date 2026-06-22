---
description: "Task list for 038-agent-review-record implementation"
---

# Tasks: Agent-Review Record — Auditable Review-Record Core

**Feature branch**: `038-agent-review-record`
**Spec**: `specs/038-agent-review-record/spec.md`
**Plan**: `specs/038-agent-review-record/plan.md`

**Input**: Design documents from `/specs/038-agent-review-record/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/review-record-api.md,
contracts/review-record-identity-format.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the spec
is itself a faithful-capture / injective-identity / sensed-exclusion / determinism / totality contract — the
tests *are* the deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`) for
traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no new
third-party dependency). No per-task tier annotations needed — all tasks share the feature tier.

**Elmish/MVU**: **Not applicable** — pure, total functions over supplied values, no state, no I/O, no workflow
(plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks. The actual
review (sending a request to a model, receiving a verdict), computing the response digest from raw bytes, sensing
the review's timestamp, and the cache lookup / verdict invalidation / advisory promotion / calibration are later
host edges, out of scope.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No semantics
yet.

- [X] T001 Create `src/FS.GG.Governance.ReviewRecord/FS.GG.Governance.ReviewRecord.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.ReviewRecord`, `Version` `0.1.0`, `IsPackable=true` (override
  `Directory.Build.props` like PromptIsolation/SensedMetadata/AgentReviewKey). `<Compile>` order: `Model.fsi`,
  `Model.fs`, `ReviewRecord.fsi`, `ReviewRecord.fs`. **Two** `<ProjectReference>`s — to
  `../FS.GG.Governance.PromptIsolation/FS.GG.Governance.PromptIsolation.fsproj` (provides `ReviewRequest` +
  `PromptIsolation.render` directly and F035 `ModelId`/`ModelVersion`/`ReviewerPromptHash` + F029 `ArtifactHash`
  transitively) and to `../FS.GG.Governance.SensedMetadata/FS.GG.Governance.SensedMetadata.fsproj` (provides
  `SensedMetadatum` + `markTimestamp`/`markDuration`) — the F033 multi-sibling-reference shape (plan D1, FR-006).
  **No third-party `PackageReference`** (FR-009). Add a header comment mirroring the PromptIsolation `.fsproj`
  (pure total core; PromptIsolation+SensedMetadata graph; reuses F037 `ReviewRequest`, F035 model/prompt, F029
  `ArtifactHash`, F034 `SensedMetadatum` verbatim; no Gates/Snapshot/VerdictReuse/host/CLI coupling).
- [X] T002 [P] Create `tests/FS.GG.Governance.ReviewRecord.Tests/FS.GG.Governance.ReviewRecord.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`, `FsCheck`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no new package);
  `<ProjectReference>`s to the new core and to `FS.GG.Governance.PromptIsolation` and
  `FS.GG.Governance.SensedMetadata` (for real `ReviewRequest`/`ModelId`/`ArtifactHash`/`SensedMetadatum`
  literals). `<Compile>` order: `Support.fs`, `CaptureTests.fs`, `IdentityTests.fs`, `SensedBoundaryTests.fs`,
  `IdentityFormatTests.fs`, `ArtifactSetTests.fs`, `NoBytesTests.fs`, `DeterminismTests.fs`,
  `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh GUIDs
  and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies so
the library and tests compile and tests can FAIL before implementation. **⚠️ No story work begins until this
phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.ReviewRecord/Model.fsi` — the SOLE public surface for the types
  (data-model.md): `open FS.GG.Governance.PromptIsolation.Model` (brings `ReviewRequest`, reused verbatim), `open
  FS.GG.Governance.AgentReviewKey.Model` (brings `ModelId`/`ModelVersion`/`ReviewerPromptHash`, transitive via
  F037), `open FS.GG.Governance.FreshnessKey.Model` (brings `ArtifactHash`, transitive), and `open
  FS.GG.Governance.SensedMetadata.Model` (brings `SensedMetadatum`); the three new newtypes `ResponseDigest of
  string`, `RecordedVerdict of string`, `RecordIdentity of string`; the `ReproducibleFacts` record (`{ Request:
  ReviewRequest; Model: ModelId; ModelVersion: ModelVersion; PromptHash: ReviewerPromptHash; ReviewedArtifacts:
  ArtifactHash list; ResponseDigest: ResponseDigest; Verdict: RecordedVerdict }`); and the `ReviewRecord` record
  (`{ Reproducible: ReproducibleFacts; Sensed: SensedMetadatum list }`). Curated doc comments in the
  PromptIsolation/SensedMetadata `.fsi` style: each digest/verdict is an opaque supplied token (no validation, no
  parsing; an empty string is a literal value); `ResponseDigest`/`RecordedVerdict` carry **no** response/artifact
  bytes; `RecordIdentity` is the byte-stable canonical identity (mirrors F032 `CommandIdentity`/F033
  `ProvenanceIdentity`); `Sensed` is held structurally apart and excluded from identity (the F032/F033 honesty
  boundary). No access modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.ReviewRecord/ReviewRecord.fsi` — the SOLE public surface for the
  operations (contracts/review-record-api.md): the curried `val build: request: ReviewRequest -> model: ModelId
  -> modelVersion: ModelVersion -> promptHash: ReviewerPromptHash -> reviewedArtifacts: ArtifactHash list ->
  responseDigest: ResponseDigest -> verdict: RecordedVerdict -> sensed: SensedMetadatum list -> ReviewRecord`
  (sensed last — the F032 convention, D7); `val canonicalId: record: ReviewRecord -> RecordIdentity`; `val
  identityValue: identity: RecordIdentity -> string`. Doc comments state purity/totality and the laws (`build`
  pairs the six facts + sensed verbatim with no reorder/dedup/capture/I/O, L-B1..L-B5; `canonicalId` is
  deterministic and INJECTIVE over `record.Reproducible` only, never reading `record.Sensed`, per
  contracts/review-record-identity-format.md, L-I1..L-I8; reads no clock/filesystem/git/environment/network,
  invokes no model, hashes no bytes). (Naming note: the `ReviewRecord` operations module and the
  `Model.ReviewRecord` record type are distinct CLR entities sharing a name by intent — the F029/F032/F033
  precedent.)
- [X] T006 Add stub `src/FS.GG.Governance.ReviewRecord/Model.fs` and
  `src/FS.GG.Governance.ReviewRecord/ReviewRecord.fs` — real type definitions in `Model.fs` (the three newtypes +
  the two records are plain data, define them fully); `build`, `canonicalId`, and `identityValue` as `failwith
  "not implemented"` stubs in `ReviewRecord.fs`. No `private`/`internal`/`public` modifiers (Principle II).
  Confirm `dotnet build src/FS.GG.Governance.ReviewRecord/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F038 design-first section to `scripts/prelude.fsx` — `#r` the new Debug DLL plus the
  PromptIsolation, AgentReviewKey, FreshnessKey, and SensedMetadata DLLs; `open` the needed `Model` namespaces +
  the `PromptIsolation`/`SensedMetadata`/`ReviewRecord` operation modules; construct the quickstart.md literal
  `ReviewRequest` (a `QuestionText` instruction; an `Excerpt` and a `DigestOnly` payload); `build` a record from
  it + literal `ModelId`/`ModelVersion`/`ReviewerPromptHash`, `[ ArtifactHash "sha256:abc" ]`, `ResponseDigest
  "sha256:resp"`, `RecordedVerdict "pass"`, and `[ markTimestamp (SensedLabel "at") (SensedTimestamp
  "2026-06-22T10:00:00Z") ]`; and `printfn` the intended results: all six facts read back via `rec038.Reproducible`;
  the identity via `identityValue (canonicalId rec038)` matches contracts/review-record-identity-format.md;
  dropping the sensed timestamp (`{ rec038 with Sensed = [] }`) leaves the identity byte-identical; flipping the
  verdict changes the identity. This is the Principle-I FSI proof; it documents the shape even while bodies are
  stubbed.
- [X] T008 Write `tests/FS.GG.Governance.ReviewRecord.Tests/Support.fs` — real, literally-constructible builders
  (Principle V, no mocks): a `baseRequest` built via `PromptIsolation.assemble` (a `QuestionText`, an `Excerpt
  (excerpt (SizeBound …) …)`, a `DigestOnly (ArtifactHash …)`); literal `ModelId`/`ModelVersion`/
  `ReviewerPromptHash`/`ArtifactHash`/`ResponseDigest`/`RecordedVerdict` builders; a `markTimestamp`/`markDuration`
  sensed-metadatum helper (note: `markDuration` needs a `SensedDuration` literal, owned by F032
  `FS.GG.Governance.CommandRecord` — transitively referenced via F034 — so `open
  FS.GG.Governance.CommandRecord.Model` if used; `markTimestamp` alone, needing only F034's `SensedTimestamp`,
  suffices for every example/identity test); a `buildOf` wrapper over `ReviewRecord.build`; an `expectedIdentity` oracle mirroring
  contracts/review-record-identity-format.md (UTF-8 `byteLen`; the `req=<len>:<F037 render>` segment via
  `PromptIsolation.renderedValue (PromptIsolation.render request)`; the `mid`/`mver`/`pph`/`resp`/`vdt`
  length-prefixed scalar segments; the `art=<count>;<len>:<h>;…` deduped/ordinal-sorted set segment; joined by
  `'\n'`, no trailing newline) for example-test oracles; FsCheck generators for each newtype string (incl. empty,
  multi-byte, and tag/separator/fence-hostile content), `ArtifactHash list` (order/duplicate preserving),
  `ReviewRequest`, `SensedMetadatum list`, and a full `ReproducibleFacts`/`ReviewRecord`; and the `findRepoRoot
  (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from the PromptIsolation/AgentReviewKey
  `Support.fs` precedent. No I/O beyond repo-root resolution.
- [X] T009 Write `tests/FS.GG.Governance.ReviewRecord.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now FAILS
only because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Capture a completed agent review as one auditable record (Priority: P1) 🎯 MVP

**Goal**: `build request model modelVersion promptHash reviewedArtifacts responseDigest verdict sensed` assembles
one immutable `ReviewRecord` carrying all six audit facts exactly as supplied (in `record.Reproducible`) plus the
sensed list (in `record.Sensed`) — none dropped, altered, or invented; total over zero-artifact / empty-digest /
empty-verdict / empty-sensed inputs. The heart of the design's auditability constraint.

**Independent Test**: Supply a rendered review request, a response digest, a model identity, a prompt identity, a
set of artifact digests, and a final verdict as already-formed values; build the record; assert it carries each of
the six facts exactly as supplied and that re-building from identical inputs yields an equal record. No model
invoked, no I/O.

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../CaptureTests.fs` — (1) **faithful carriage** (SC-001, US1 #1, L-B2): for `r =
  build req m v p arts resp vdt sensed`, assert `r.Reproducible = { Request = req; Model = m; ModelVersion = v;
  PromptHash = p; ReviewedArtifacts = arts; ResponseDigest = resp; Verdict = vdt }` and `r.Sensed = sensed`, over
  example and FsCheck-generated inputs — `ReviewedArtifacts` keeps supplied order + duplicates, `Sensed` kept
  whole/in order; (2) **build determinism** (US1 #2, L-B5): `build`-ing the same eight args twice yields records
  equal by structural equality; (3) **sensed held apart** (L-B3): `sensed` appears in `r.Sensed` and nowhere in
  `r.Reproducible` (no reproducible field has type `SensedMetadatum`/list); (4) **totality / degenerate inputs**
  (FR-002, L-B1, Edge Cases): a request over zero artifacts (`ReviewedArtifacts = []`), `ResponseDigest ""`,
  `RecordedVerdict ""`, and `sensed = []` each produce ordinary complete records, never malformed, never throwing.

### Implementation for User Story 1

- [X] T011 [US1] Implement `build` in `ReviewRecord.fs` — `build request model modelVersion promptHash
  reviewedArtifacts responseDigest verdict sensed = { Reproducible = { Request = request; Model = model;
  ModelVersion = modelVersion; PromptHash = promptHash; ReviewedArtifacts = reviewedArtifacts; ResponseDigest =
  responseDigest; Verdict = verdict }; Sensed = sensed }`: assemble the supplied facts verbatim, **no** reorder,
  dedup, normalization, capture, hashing, or I/O (L-B4, FR-005). Plain record construction only. Run T010: green.

**Checkpoint**: US1 is functional — a completed review is captured as one immutable record carrying all six audit
facts + sensed metadata, total over degenerate inputs. MVP reached for the capture guarantee.

---

## Phase 4: User Story 2 — Derive a deterministic, injective identity over the record's reproducible facts (Priority: P1)

**Goal**: `canonicalId record` derives a pure, deterministic, byte-stable, **injective** `RecordIdentity` over
`record.Reproducible` only — the F029/F032/F035 tagged, length-prefixed encoding (contracts/review-record-
identity-format.md): identical reproducible facts (artifacts compared as a set) ⇒ byte-identical identity; any
single differing reproducible fact ⇒ a different identity; records differing only in `record.Sensed` ⇒ identical
identity (the honesty boundary). `identityValue` unwraps it. Co-P1 with US1 — a record you cannot identify
reproducibly is not yet auditable.

**Independent Test**: Build two records from identical reproducible facts ⇒ byte-equal identities; change exactly
one reproducible fact ⇒ identity changes; supply only differing sensed metadata between two otherwise-identical
records ⇒ identity unchanged. The identity is a single deterministic string over supplied values — no clock, file,
or hash-of-bytes read.

### Tests for User Story 2 (write first; must FAIL against stubs)

- [X] T012 [P] [US2] `tests/.../IdentityTests.fs` — (1) **stability** (SC-002, US2 #1, L-I3): identical
  reproducible facts ⇒ `canonicalId` byte-equal; (2) **injectivity** (SC-002, US2 #2, L-I4/L-I7): changing exactly
  one reproducible fact — the `Request` (any change that alters `PromptIsolation.render request`: instructions, an
  artifact payload, an excerpt's content/bound/truncation, a digest, payload order/count), `Model`, `ModelVersion`,
  `PromptHash`, a distinct artifact digest, `ResponseDigest`, or `Verdict` — yields a different identity, over
  example + FsCheck-generated pairs; (3) **independence** (L-I7): two records whose embedded requests render
  identically but whose `Model`/`ModelVersion`/`PromptHash` differ yield different identities; (4) **cross-field
  injectivity** (L-I6): the same opaque string placed in two different fields (e.g. `ResponseDigest` vs `Verdict`,
  `ModelVersion` vs `PromptHash`) yields different identities; field content containing tag/separator/fence
  characters is read as data by length and forges no boundary; (5) **`identityValue` round-trip** (L-V1):
  `identityValue (canonicalId r)` equals the canonical string, total.
- [X] T013 [P] [US2] `tests/.../SensedBoundaryTests.fs` — (SC-003, US2 #3, L-I2): two records identical in every
  reproducible fact but differing only in `record.Sensed` (including one with `Sensed = []` and one with a
  non-empty sensed list) ⇒ byte-identical `canonicalId`; an FsCheck property over arbitrary reproducible facts and
  arbitrary differing sensed lists confirms `canonicalId` never depends on `record.Sensed`.
- [X] T014 [P] [US2] `tests/.../IdentityFormatTests.fs` — example tests pinned to contracts/review-record-
  identity-format.md: the worked example (request `assemble (QuestionText "Explain the API?") [ DigestOnly
  (ArtifactHash "sha:a") ]`; model `gpt`; version `2026-06`; prompt hash `ph1`; artifacts `[ ArtifactHash "sha:a"
  ]`; response digest `sha:resp`; verdict `pass`; one sensed timestamp) renders `canonicalId |> identityValue`
  **byte-for-byte** equal to the documented block — the `req=<byteLen R>:<F037 render R>` segment (its embedded
  `\n` consumed inside `req` by length), then `mid=3:gpt`, `mver=7:2026-06`, `pph=3:ph1`, `art=1;5:sha:a`,
  `resp=8:sha:resp`, `vdt=4:pass`, joined by `'\n'` with no trailing newline; and the sensed timestamp appears
  **nowhere** in the block. `expectedIdentity` (T008) agrees over additional example records. Includes the
  empty-set form `art=0;` and empty scalar forms (`resp=0:`, `vdt=0:`).
- [X] T015 [P] [US2] `tests/.../ArtifactSetTests.fs` — (D4, L-I5): reordering `ReviewedArtifacts`, or supplying
  duplicate digests, yields the **same** `canonicalId` (the `art` segment is deduplicated and ordinal-sorted before
  encoding — the F035 `artSegment`); adding or removing a **distinct** digest changes the identity; an FsCheck
  property over permutations/duplications confirms set-invariance; the zero-artifact record gives `art=0;` and
  identifies deterministically.

### Implementation for User Story 2

- [X] T016 [US2] Implement `canonicalId` and `identityValue` in `ReviewRecord.fs` per contracts/review-record-
  identity-format.md — reuse the F035 `byteLen` (UTF-8 `Encoding.UTF8.GetByteCount`) + `StringBuilder`
  `seg`/`artSegment` idiom: render `record.Reproducible` (never `record.Sensed`, L-I2) as length-prefixed tagged
  segments joined by `'\n'`, no trailing newline, in fixed order — `req=<byteLen R>:R` where `R =
  PromptIsolation.renderedValue (PromptIsolation.render record.Reproducible.Request)` (L-I previous; the F033
  embedded-record analogue); `mid`/`mver`/`pph` from the unwrapped F035 newtypes; the `art=<count>;…` set segment
  over the **deduped, ordinal-sorted** unwrapped `ArtifactHash` list; `resp`/`vdt` from the unwrapped new newtypes;
  wrap in `RecordIdentity`. `identityValue (RecordIdentity s) = s`. `System.Text`/BCL string building only; no
  clock/filesystem/git/environment/network, no model, no byte hashing (L-I1/L-I8, FR-005). Run T012–T015: green.

**Checkpoint**: US1 + US2 — the captured record has a deterministic, injective identity over its reproducible
facts that excludes sensed metadata. The record is auditable: citable, comparable, deduplicable. Full core
functional.

---

## Phase 5: User Story 3 — Keep response and artifact content out of the record — digests only, no bytes (Priority: P2)

**Goal**: The record carries **no** raw response bytes and **no** raw, unbounded artifact bytes — the response is
only its `ResponseDigest`, the reviewed artifacts are only their `ArtifactHash` digests, and the **only** artifact
content present is inside the F037-bounded `Request` (whose excerpts are bounded by construction). This guarantee
holds **by construction** of the types (no field has a raw-bytes shape); the test pins it over the public surface.

**Independent Test**: Supply a response digest ⇒ the record carries the digest and no response bytes; supply
artifact digests ⇒ the record carries the digests and no raw artifact bytes; confirm the only content the record
holds is the F037-bounded request, and there is no form that attaches raw, unbounded response or artifact content.

### Tests for User Story 3 (write first; must FAIL against stubs, then pass once T011 lands)

- [X] T017 [P] [US3] `tests/.../NoBytesTests.fs` — (1) **response is a digest only** (SC-004, US3 #1): a built
  record exposes the reviewer response solely as `record.Reproducible.ResponseDigest` (a `ResponseDigest` newtype)
  — assert by pattern match that no record field carries response bytes in any other form; (2) **artifacts are
  digests only** (US3 #2): `record.Reproducible.ReviewedArtifacts` is an `ArtifactHash list` carrying no raw
  artifact bytes; (3) **only the F037-bounded request carries content** (US3 #3): the sole content-bearing field is
  `record.Reproducible.Request`, whose `Excerpt` payloads are `BoundedExcerpt` (bounded by construction) and whose
  `DigestOnly` payloads carry only an `ArtifactHash` — there is no `ReviewRecord`/`ReproducibleFacts` constructor or
  field by which raw, unbounded response or artifact content attaches outside the bounded request (a documented
  compile-time guarantee plus a value-level check). **Note**: this is an absence-by-construction guarantee — the
  surface (T018/Phase 7) contains no type or operation that could carry raw bytes; this test pins the positive
  shape of what the record *does* carry.

**Checkpoint**: US1 + US2 + US3 — the auditable, identified record carries only digests + the F037-bounded request;
no raw response or unbounded artifact content can enter it.

---

## Phase 6: Cross-cutting guarantees — purity, totality, determinism (covers all stories)

**Purpose**: Pin the trust guarantees of `build`/`canonicalId`: purity (no clock/filesystem/git/environment/
network read, no model invoked, no bytes hashed, nothing persisted), determinism, and totality across degenerate
inputs.

### Tests (write/extend; must pass once implementation is complete)

- [X] T018 [P] `tests/.../DeterminismTests.fs` — (SC-005, FR-005): building a record and deriving its identity is
  byte-for-byte identical when performed in different working directories, at different times, and with unrelated
  repository / filesystem state changed between operations; no model invoked, no clock/filesystem/git/environment/
  network read, no bytes hashed, nothing persisted. Mirror the PromptIsolation/AgentReviewKey purity-test
  precedent (change cwd / touch a temp file between two computations; assert equal `ReviewRecord`s by structural
  equality and byte-equal `RecordIdentity`s). Includes an FsCheck property that `build` and `canonicalId` are pure
  functions of their inputs (same inputs ⇒ same record + same identity string).

**Checkpoint**: The purity, determinism, and totality contracts hold across all stories.

---

## Phase 7: Surface governance & polish (Tier-1 baseline, scope hygiene)

**Purpose**: Lock the public surface (Principle II) and prove the assembly's reference graph stays minimal
(SC-006). Bless the baseline only after the surface is final.

- [X] T019 `tests/.../SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the F029/F030/F035/F036/F037
  precedent): enumerate the public surface of `FS.GG.Governance.ReviewRecord` and compare byte-for-byte to
  `surface/FS.GG.Governance.ReviewRecord.surface.txt`, with the `BLESS_SURFACE=1` re-bless path; plus a
  **scope-hygiene** assertion (contracts/review-record-api.md scope guard, SC-006) that the assembly references
  **only** `FSharp.Core`, `FS.GG.Governance.PromptIsolation`, `FS.GG.Governance.SensedMetadata`, and — transitively
  — `FS.GG.Governance.AgentReviewKey`, `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.CommandRecord`,
  `FS.GG.Governance.Config`, plus the BCL — and **not** `Gates`, `Snapshot`, `Route`/`Routing`, `Findings`,
  `EvidenceReuse`, `VerdictReuse`, any `Adapters.*`, `Host`, `Cli`, `Ship`, `Enforcement`, or `AuditJson`.
  **Note**: FR-007's *behavioral* negatives (no verdict promotion / interpretation / thresholding, no cache key /
  verdict store / lookup / invalidation, no model invocation, no byte hashing, no persistence, no JSON projection,
  no CLI) are satisfied **by construction** — the surface contains only the five types + `build`/`canonicalId`/
  `identityValue` (no such operation exists to call) — and are guarded by this reference-graph + surface-drift
  check, **not** by a positive behavioral test (absence of a feature is not directly assertable).
- [X] T020 Generate and commit `surface/FS.GG.Governance.ReviewRecord.surface.txt` via `BLESS_SURFACE=1 dotnet
  test tests/FS.GG.Governance.ReviewRecord.Tests/...`; review the diff (exactly the two public modules — the
  `Model` types `ResponseDigest`/`RecordedVerdict`/`RecordIdentity`/`ReproducibleFacts`/`ReviewRecord`, and
  `build`/`canonicalId`/`identityValue`; no helper / segment-encoder leak) and commit it as part of the Tier-1
  change. After this, T019 runs green without `BLESS_SURFACE`.
- [X] T021 [P] Update `CLAUDE.md` — confirm the SPECKIT plan reference points at
  `specs/038-agent-review-record/plan.md` (already the active pointer). No other doc changes.
- [X] T022 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`, `dotnet fsi
  scripts/prelude.fsx` (the F038 section prints the expected capture / identity / sensed-exclusion / verdict-flip
  results), and `dotnet test tests/FS.GG.Governance.ReviewRecord.Tests/...` — all green under
  `TreatWarningsAsErrors`. Confirm `dotnet test FS.GG.Governance.sln` over the existing projects is unchanged (no
  existing baseline rewritten, no existing test changes outcome — SC-006).

**Checkpoint**: Tier-1 surface is blessed and guarded; the assembly's reference graph is minimal; the full
solution builds and tests green; existing cores untouched.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` surface, FSI proof, and
  compiling stubs must exist before any story test can be written and FAIL.
- **Phase 3 (US1)**: depends on Phase 2. The MVP (capture via `build`).
- **Phase 4 (US2)**: depends on Phase 2; co-P1 with US1. `canonicalId` (T016) consumes records built by `build`
  (US1, T011) for its test data, but is its own function; sequenced after US1 because identity is derived over a
  built record.
- **Phase 5 (US3)**: depends on Phase 2 + US1's `build` (T011) — the no-bytes shape is asserted over a built
  record; holds by construction, so T017 goes green as soon as T011 lands. Sequenced after US1/US2 (P2).
- **Phase 6 (cross-cutting)**: depends on US1 + US2 implementations being complete (asserts purity/determinism of
  finished `build`/`canonicalId`).
- **Phase 7 (surface/polish)**: last — bless the baseline only after the surface is final (Phase 2 `.fsi`
  unchanged through implementation).

### Within each story

- Tests are written FIRST and must FAIL against the Phase-2 stubs, then pass after the implementation task.
- `Model` type definitions precede the `ReviewRecord` operation bodies that consume them; `canonicalId` (T016)
  consumes `ReproducibleFacts`/`ReviewRecord` and `PromptIsolation.render`.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) is independent of T001 (library `.fsproj`); T003 (sln) needs both.
- **Phase 2**: T007 `[P]` (prelude FSI section) is independent of the `.fsi`/stub work once the DLL name is fixed
  by T001. T004/T005 (the two `.fsi` files) can be drafted together; T006 needs both; T008/T009 need the
  compiling stub.
- **Story test files are all `[P]`** relative to each other (distinct files): T010, T012, T013, T014, T015, T017,
  T018 touch different test files. They share `Support.fs` (T008) as a prerequisite.
- **Phase 7**: T021 `[P]` (CLAUDE.md) is independent of the surface test; T019→T020→T022 are sequential.

---

## Task count per user story

- **Setup (Phase 1)**: 3 tasks (T001–T003).
- **Foundational (Phase 2)**: 6 tasks (T004–T009).
- **US1 (Phase 3)**: 2 tasks (T010 test, T011 impl) 🎯 MVP.
- **US2 (Phase 4)**: 5 tasks (T012–T015 tests, T016 impl).
- **US3 (Phase 5)**: 1 task (T017 test; no-bytes holds by construction).
- **Cross-cutting (Phase 6)**: 1 task (T018).
- **Surface & polish (Phase 7)**: 4 tasks (T019–T022).
- **Total**: 22 tasks.

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2)** — the project skeleton, the `.fsi` surface + FSI proof, the
faithful capture (`build`), and the deterministic injective identity (`canonicalId`/`identityValue`). US1 and US2
are **co-P1** (the spec: a record you cannot identify reproducibly is not yet auditable; an identity with no record
to identify is empty), so the smallest honest MVP delivers both: a completed review captured as an immutable record
*and* given a citable, comparable, sensed-excluding identity. US3 (no raw bytes, P2) is a by-construction guarantee
pinned by one test; Phases 6–7 pin purity/determinism/totality and the Tier-1 surface.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify each story's tests FAIL against the Phase-2 stubs before implementing, then pass after.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- Commit after each task or logical group; keep existing `src/`, `surface/`, and merged test projects untouched
  (SC-006).
