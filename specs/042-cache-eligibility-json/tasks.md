---
description: "Task list for 042-cache-eligibility-json implementation"
---

# Tasks: Deterministic cache-eligibility.json Projection

**Feature branch**: `042-cache-eligibility-json`
**Spec**: `specs/042-cache-eligibility-json/spec.md`
**Plan**: `specs/042-cache-eligibility-json/plan.md`

**Input**: Design documents from `/specs/042-cache-eligibility-json/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/cache-eligibility-json-api.md, contracts/cache-eligibility-json-document.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a carry / determinism / versioned-schema / no-hide / totality / exclusions contract — the
tests *are* the deliverable's proof. Every input is a real, upstream-assembled `CacheEligibilityReport`
built by F041 `CacheEligibility.evaluate` over real candidate gates (real F018 `GateId`, real F029
`FreshnessInputs`) against a real F030 `ReuseStore` assembled via `EvidenceReuse.record`; output is parsed
back with real `System.Text.Json` (`JsonDocument`). No mock, no clock read, no hand-built JSON oracle, no
real cache lookup, no freshness key/hash computed. No mocks ⇒ no `Synthetic` disclosure needed.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`…`[US4]`) for
traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface — the `FS.GG.Governance.CacheEligibilityJson`
assembly + module — and a new `surface/*.surface.txt` baseline, with no new third-party dependency). All
tasks share the feature tier; no per-task tier annotations needed.

**Elmish/MVU**: **Not applicable** — a pure, total, deterministic projection from one already-typed value
(`CacheEligibilityReport`) to a `string` (plan Constitution Check, Principle IV = N/A). No
`Model`/`Msg`/`Effect`/`update`/interpreter tasks. The embedding of the verdict into route.json / audit.json,
the host wiring that resolves each gate's `FreshnessInputs`, and any real cache store are later edges, out of
scope. Principle VI is likewise N/A — no operationally-significant event to observe; **totality** stands in for
safe failure (`ofReport` never throws).

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new projection library + test project so everything compiles and the solution
restores. No semantics yet.

- [X] T001 Create `src/FS.GG.Governance.CacheEligibilityJson/FS.GG.Governance.CacheEligibilityJson.fsproj` —
  SDK-style, `RootNamespace`/`PackageId` `FS.GG.Governance.CacheEligibilityJson`, `Version` `0.1.0`,
  `IsPackable=true` (override `Directory.Build.props`, the AuditJson/RouteJson/GatesJson precedent).
  `<Compile>` order: `CacheEligibilityJson.fsi`, then `CacheEligibilityJson.fs`. **One**
  `<ProjectReference>` — to `../FS.GG.Governance.CacheEligibility/FS.GG.Governance.CacheEligibility.fsproj`
  (F041, provides `CacheEligibilityReport` / `CacheEligibilityEntry` / `CacheEligibilityVerdict` and the
  `entries` accessor; the cause/token types `RecomputeCause` / `EvidenceRef` (F030), `GateId` (F018), and
  `InputCategory` (F029) are *defined upstream* and arrive **transitively** through F041 — see data-model.md
  and contracts/cache-eligibility-json-api.md for their origins); the transitive pure cores `EvidenceReuse`
  (F030), `Gates` (F018), `FreshnessKey` (F029), `Config` (F014) arrive through F041 and need no direct reference (the F025 "references only `Ship`, the rest arrive transitively"
  precedent; plan Technical Context, research D2). **No third-party `PackageReference`** (FR-014) —
  serialization is the net10.0 shared-framework `System.Text.Json` (`Utf8JsonWriter`). Add a header comment
  mirroring the AuditJson `.fsproj`: pure total emit-only projection of F041's `CacheEligibilityReport`;
  reuses F018 `gateIdValue` + F030 `referenceValue` + F029 `categoryToken` verbatim; one-way dependency
  `CacheEligibilityJson -> CacheEligibility -> …`; no RouteJson/AuditJson/GatesJson/Enforcement/Ship/
  Snapshot/Routing/host/CLI coupling.
- [X] T002 [P] Create
  `tests/FS.GG.Governance.CacheEligibilityJson.Tests/FS.GG.Governance.CacheEligibilityJson.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`,
  no new package); `<ProjectReference>`s to the new projection library and to `FS.GG.Governance.CacheEligibility`
  (to call `evaluate` / `entries` and build real reports) plus `FS.GG.Governance.EvidenceReuse`,
  `FS.GG.Governance.Gates`, `FS.GG.Governance.FreshnessKey` (for real `ReuseStore`/`record`/`EvidenceRef`,
  `GateId`/`gateIdValue`, `FreshnessInputs`/`InputCategory`/`categoryToken` literals + token oracles).
  `<Compile>` order: `Support.fs`, `ProjectionTests.fs`, `DeterminismTests.fs`, `NoHideTests.fs`,
  `TotalityTests.fs`, `ExclusionsTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contract, FSI proof, compiling stub, test scaffolding) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add a stub `.fs`
body + test scaffolding so the library and tests compile and tests can FAIL before implementation. **⚠️ No
story work begins until this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.CacheEligibilityJson/CacheEligibilityJson.fsi` — the SOLE public
  surface (contracts/cache-eligibility-json-api.md). `open` the upstream model namespaces the signatures
  reference (`FS.GG.Governance.CacheEligibility.Model`, `FS.GG.Governance.EvidenceReuse.Model`,
  `FS.GG.Governance.FreshnessKey.Model`, `FS.GG.Governance.Gates.Model`). Declare exactly two members:
  `val schemaVersion: string` (L-V1) and `val ofReport: report: CacheEligibilityReport -> string`
  (L-R1…L-R11, L-T1…L-T3). Curated doc comments in the AuditJson `.fsi` style: `schemaVersion` is the fixed
  constant `"fsgg.cache-eligibility/v1"`, never derived from a clock/environment/input (FR-013); `ofReport`
  is pure/total/deterministic (no file/process/clock/network/git access, no cache lookup against a real
  store, no freshness key/hash computed, none of the inputs resolved, the opaque evidence reference never
  dereferenced, never throws — FR-007/FR-008), emit-only (re-derives/re-classifies/re-runs/re-orders nothing
  — FR-002/FR-005), renders one entry per report entry in the report's verbatim `GateId`-ordinal order
  (FR-001/FR-005), every `mustRecompute` names its cause (no-hide, FR-004) and a `reusable` asserts only
  "prior evidence may be reused" (necessary-not-sufficient, FR-003), and the document carries none of the
  excluded fields (FR-012). No access modifiers will appear in the matching `.fs`; every writer / token
  helper stays hidden by its absence from this `.fsi` (the `Kernel.Json` / `RouteJson` / `GatesJson` /
  `AuditJson` precedent, Principle II).
- [X] T005 Add `src/FS.GG.Governance.CacheEligibilityJson/CacheEligibilityJson.fs` — the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module CacheEligibilityJson`
  with the real `schemaVersion = "fsgg.cache-eligibility/v1"` constant (it is data, define it fully) and
  `ofReport` as a `failwith "not implemented"` stub. No `private`/`internal`/`public` modifiers (Principle
  II). Confirm `dotnet build src/FS.GG.Governance.CacheEligibilityJson/...` is clean under
  `TreatWarningsAsErrors`.
- [X] T006 [P] Append the F042 design-first section to `scripts/prelude.fsx` (after the F041 section) — `#r`
  the new Debug DLL plus the `CacheEligibility`, `EvidenceReuse`, `Gates`, `FreshnessKey` DLLs; `open` the
  needed model + operation modules; reuse the F041 `evaluate` / `record` / `FreshnessInputs` worked-example
  values where possible and `printfn` the intended documents against the document-contract worked examples
  (quickstart.md): empty report ⇒ `{"schemaVersion":"fsgg.cache-eligibility/v1","entries":[]}`; exact-match
  candidate (`docs:lint`) ⇒ verdict `{"kind":"reusable","evidence":"ev-A"}`; no prior evidence
  (`security:scan`) ⇒ `{"kind":"mustRecompute","cause":{"kind":"noPriorEvidence"}}`; `ruleHash`+`head` moved
  (`build:tests`) ⇒ cause `{"kind":"inputsChanged","categories":["ruleHash","headRevision"]}`; candidates
  `z:a`, `a:b`, `a:a` ⇒ entries ordered `a:a`, `a:b`, `z:a`, byte-identical for any permutation; duplicate
  `GateId` ⇒ two entries under the same gate, neither merged nor deduplicated; `inputsChanged []` distinct
  from `noPriorEvidence`. This is the Principle-I FSI proof; it documents the shape even while the body is
  stubbed.
- [X] T007 Write `tests/FS.GG.Governance.CacheEligibilityJson.Tests/Support.fs` — real,
  literally-constructible report builders (Principle V, no mocks), adapted from the F041
  `CacheEligibility.Tests/Support.fs` and F030 precedents: a `gid d c = GateId (d + ":" + c)` helper; a
  complete literal `baseInputs: FreshnessInputs` (every category present and distinct so a single-field
  change is observable) + the single-field `variant*` mutators and the `(InputCategory * (FreshnessInputs ->
  FreshnessInputs)) list` of all categories; a `candidate gate inputs` builder; a `storeOf` helper folding
  `EvidenceReuse.record` over `EvidenceReuse.empty`; a `report candidates store = CacheEligibility.evaluate
  candidates store` convenience and the worked-example reports from the document/api contracts; a
  `JsonDocument` **parse helper** (`parse: string -> JsonDocument`, and small accessors for `schemaVersion`,
  the `entries` array, an entry's `gate` / `verdict` / `verdict.kind` / `evidence` / `cause` /
  `cause.kind` / `cause.categories`) so structural assertions read the parsed tree, not raw substrings; and
  FsCheck generators for arbitrary well-typed reports — `GateId` strings (incl. empty, multi-byte,
  duplicate-inducing reuse of a small label pool, and a `:`-containing value), `FreshnessInputs` (varying
  every category), candidate lists of arbitrary length (incl. `[]`, singletons, duplicate-`GateId`), and
  `ReuseStore` states (empty, matching, non-matching) — built so the resulting report spans empty,
  all-reusable, all-must-recompute, mixed, and duplicate-`GateId`. Plus the `findRepoRoot (DirectoryInfo
  AppContext.BaseDirectory)` / `repoRoot` helper copied from the F041 `Support.fs` precedent. No I/O beyond
  repo-root resolution.
- [X] T008 Write `tests/FS.GG.Governance.CacheEligibilityJson.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because `ofReport` is a stub (not because of compile errors).

---

## Phase 3: User Story 1 — Render a cache-eligibility report to a deterministic cache-eligibility.json (Priority: P1) 🎯 MVP

**Goal**: Project a real upstream-assembled `CacheEligibilityReport` into a single machine-readable document
that records, per selected gate, exactly one entry — its declared `gate` id and its verdict (`reusable` +
the opaque evidence reference, or `mustRecompute` + its named cause), every value tracing back to the report.
This is the feature's reason to exist: turning the F041 verdict into a durable, shareable artifact.

**Independent Test**: Project a real `CacheEligibilityReport` (from `CacheEligibility.evaluate` over real
candidate gates and a real `ReuseStore`); parse the output and assert exactly one entry per gate, each with
its declared gate id and its verdict (reusable + evidence reference, or must-recompute + cause), every value
tracing back to the report.

### Tests for User Story 1 (write first; must FAIL against the stub)

- [X] T009 [P] [US1] `tests/.../ProjectionTests.fs` — over real reports parsed back with `JsonDocument`
  (SC-001, SC-004, L-R1/L-R2/L-R3/L-R6): (1) **one entry per report entry, in order** — for a report with N
  entries, the document's `entries` array has exactly N elements, the i-th carrying `gate = gateIdValue
  es.[i].Gate` and rendering `es.[i].Verdict`; no entry dropped, merged, deduplicated, reordered, or invented
  (also an FsCheck property over arbitrary reports — `entries` length and `gate` sequence equal the report's
  via `CacheEligibility.entries`). (2) **reusable carries its evidence verbatim** (US1 #1, L-R3) — a report
  with a `Reusable (EvidenceRef "ev-A")` entry ⇒ that entry's `verdict` is `{kind:"reusable",
  evidence:"ev-A"}` with the exact `referenceValue ref` and **no** `cause` field. (3) **mustRecompute /
  noPriorEvidence** (US1 #2, L-R4) — a `MustRecompute NoPriorEvidence` entry ⇒ `verdict` is
  `{kind:"mustRecompute", cause:{kind:"noPriorEvidence"}}` with **no** `evidence` field. (4) **mustRecompute
  / inputsChanged names exactly the categories** (US1 #3, L-R4) — a `MustRecompute (InputsChanged cats)`
  entry ⇒ `cause` is `{kind:"inputsChanged", categories:[…]}` whose strings are `categoryToken c` for each
  `c` in `cats`, in the report's order — none dropped, none added. (5) **gate id verbatim across a `:`
  separator** (L-R6) — a `gid "build" "tests"` entry renders `gate:"build:tests"`, never re-parsed.

### Implementation for User Story 1

- [X] T010 [US1] Implement `ofReport` in `CacheEligibilityJson.fs` per the data-model.md walk and the
  document/api contracts — a hidden `writeToString (emit: Utf8JsonWriter -> unit) : string` helper (the
  `Json.fs`/`AuditJson.fs` compact-default precedent), hidden exhaustive token/sub-object writers, and the
  single linear walk: top-level object `schemaVersion` then `entries` array `[ for entry in
  CacheEligibility.entries report -> writeEntry w entry ]`; `writeEntry` writes `gate` (`gateIdValue
  entry.Gate`) then the tagged `verdict` object — `Reusable ref ⇒ {kind:"reusable", evidence:referenceValue
  ref}`, `MustRecompute cause ⇒ {kind:"mustRecompute", cause:<cause-object>}`; the cause object —
  `NoPriorEvidence ⇒ {kind:"noPriorEvidence"}`, `InputsChanged cats ⇒ {kind:"inputsChanged",
  categories:[categoryToken c for c in cats]}`. Every `match` rendering a token is **exhaustive over the
  closed DU with NO wildcard** (L-R10, research D4), so a future verdict/cause/category case is a compile
  error here. Pure `Utf8JsonWriter` walk + `FSharp.Core`/`System.Text.Json` only; no clock/filesystem/git/
  environment/network, no cache lookup against a real store, no freshness key/hash, no input resolved, no
  evidence dereferenced, nothing persisted (FR-007/FR-008). This single total linear walk serves **all four
  stories** (US1 carry, US2 determinism/order, US3 no-hide cause, US4 totality). Run T009: green.

**Checkpoint**: US1 is functional — a real report projects to a document with one faithful entry per gate,
each verdict and its payload tracing back to the report value. The MVP artifact exists.

---

## Phase 4: User Story 2 — A stable, versioned schema for CI, cost views, and agents (Priority: P1)

**Goal**: The same `CacheEligibilityReport` always produces a byte-identical document, with a declared schema
version consumers can branch on and a deterministic field/collection order that makes diffs meaningful — and
the document is restricted to the declared vocabularies (no clock, host path, raw inputs, hash, env value,
exit code, severity, ship verdict, or provenance reference). `ofReport` is already whole from T010; this
phase validates its determinism, versioning, order, and exclusions.

**Independent Test**: Project the same report twice ⇒ byte-for-byte equal; project two value-equal reports
assembled from differently-ordered candidate inputs ⇒ identical output; assert a present `schemaVersion`,
the `entries` in the report's `GateId`-ordinal order, and that no excluded field appears.

### Tests for User Story 2 (validate the finished `ofReport` from T010)

- [X] T011 [P] [US2] `tests/.../DeterminismTests.fs` — (SC-002, SC-003, US2 #1/#2/#3, L-R7/L-T2/L-T3):
  (1) **byte-for-byte determinism** — `ofReport report = ofReport report` for example + FsCheck-generated
  reports, including a purity check mirroring the F041 precedent (identical text when computed in different
  working directories, at different times, with unrelated filesystem state changed between calls; no I/O).
  (2) **order-independence at the source** — two reports equal as values but assembled from
  differently-ordered candidate inputs project to byte-identical documents (the worked example: candidates
  `z:a`, `a:b`, `a:a` ⇒ `entries` ordered `a:a`, `a:b`, `z:a` for any permutation), because F041 fixed the
  order and `ofReport` preserves it verbatim. (3) **versioned schema + stable field order** — every document
  parses to a present `schemaVersion = "fsgg.cache-eligibility/v1"`; assert the top-level field order
  (`schemaVersion`, `entries`), per-entry order (`gate`, `verdict`), per-verdict order (`kind` then payload),
  and per-cause order (`kind` then `categories`) via the raw-text key positions, not just the parsed tree
  (the order is part of the contract, FR-007). (4) **`schemaVersion` is a fixed constant** (L-V1) —
  `CacheEligibilityJson.schemaVersion = "fsgg.cache-eligibility/v1"` and equals the document's field for
  every report.
- [X] T012 [P] [US2] `tests/.../ExclusionsTests.fs` — (SC-007, US2 #4, FR-012, L-R11): over example + FsCheck
  reports, parse the document and assert the **only** keys present are drawn from the declared set
  (`schemaVersion`, `entries`; per entry `gate`, `verdict`; per verdict `kind` + (`evidence` | `cause`); per
  cause `kind` + optional `categories`) — and that **no** wall-clock timestamp, host/absolute path, raw
  freshness input, computed freshness key or hash, environment-derived value, numeric process exit code,
  `severity`, ship `verdict`/`exitCodeBasis`, or provenance/attestation reference appears (assert the absence
  of those field names and of any value that is not a declared gate id / closed token / category token /
  opaque evidence reference). Confirm `verdict.kind ∈ {reusable, mustRecompute}`, `cause.kind ∈
  {noPriorEvidence, inputsChanged}`, and each `categories` element is one of the F029 `categoryToken`
  strings — closed vocabularies, branchable, not free text (FR-011).

**Checkpoint**: US1 + US2 — the document is a *contract*: byte-identical for identical (and value-equal)
reports, version-stamped, stably ordered, and carrying only the declared vocabularies. Snapshot tests and CI
consumers can depend on it.

---

## Phase 5: User Story 3 — The no-hide rule is visible on every must-recompute entry (Priority: P2)

**Goal**: Every `mustRecompute` entry carries its cause — `noPriorEvidence`, or the named list of the exact
changed freshness-input categories in the report's order — so a recompute is always self-explaining and
`noPriorEvidence` is never confused with `inputsChanged []`. Builds on US1; validated over the finished
`ofReport`.

**Independent Test**: Project a report whose entries include a `MustRecompute (InputsChanged [c1; c2])` and a
`MustRecompute NoPriorEvidence`; assert the first shows exactly `c1, c2` (in order) and the second the
`noPriorEvidence` token, and that every `mustRecompute` entry carries one named cause (never empty/opaque).

### Tests for User Story 3 (validate the no-hide carry of the finished `ofReport`)

- [X] T013 [P] [US3] `tests/.../NoHideTests.fs` — (SC-005, US3 #1/#2/#3, L-R4/L-R5): (1) **inputsChanged
  names exactly the categories, in order, never truncated** — a `MustRecompute (InputsChanged cats)` with
  several changed categories ⇒ `cause.categories` is exactly `[categoryToken c for c in cats]`, none omitted,
  none added, never truncated to the first difference (drive the multi-category case from the `allCategories`
  table in `Support.fs`). (2) **noPriorEvidence ≠ inputsChanged []** (L-R5) — a `MustRecompute
  NoPriorEvidence` entry has a `cause` with `kind:"noPriorEvidence"` and **no** `categories` field, whereas a
  `MustRecompute (InputsChanged [])` entry has `kind:"inputsChanged"` with `categories: []` present — assert
  the two are structurally distinct and never collapse. (3) **every mustRecompute names a cause** — an
  FsCheck property over arbitrary reports: every entry whose `verdict.kind = "mustRecompute"` carries exactly
  one `cause` object from the closed vocabulary; **no** `mustRecompute` entry is rendered without a cause, and
  no `cause` is empty or opaque.

**Checkpoint**: US1 + US2 + US3 — the artifact is honest: every cache miss is self-explaining, and the
distinction between "no prior evidence" and "inputs changed (none, or these)" is observable.

---

## Phase 6: User Story 4 — Total over any well-typed cache-eligibility report (Priority: P2)

**Goal**: `ofReport` succeeds for every `CacheEligibilityReport` the upstream roll-up can produce — empty,
all-reusable, all-must-recompute, mixed, and duplicate-`GateId` — returning a valid document and never
throwing, so later rows call it unconditionally without error handling.

**Independent Test**: Property-based projection over generated well-typed reports asserts `ofReport` always
returns a parseable document and never throws, including the empty report, an all-reusable report, an
all-must-recompute report, and a report with two entries sharing a `GateId`.

### Tests for User Story 4 (validate totality of the finished `ofReport`)

- [X] T014 [P] [US4] `tests/.../TotalityTests.fs` — (SC-006, US4 #1/#2/#3, L-T1/L-R8/L-R9): (1) **totality
  property** — an FsCheck property over arbitrary well-typed reports (incl. empty, all-reusable,
  all-must-recompute, mixed, duplicate-`GateId`) asserts `ofReport` returns a string that parses as a valid
  document and never throws. (2) **empty report** (L-R9) — `ofReport (CacheEligibility.evaluate [] store)`
  parses to `{schemaVersion, entries: []}` with a present, empty `entries` array — a success, never an error
  and never a "must recompute by default" placeholder entry. (3) **all-reusable / all-must-recompute** — every
  entry renders with its verdict and the document is valid. (4) **duplicate `GateId` kept** (L-R8) — two
  report entries sharing a `GateId` (two candidates under `build:tests` against an empty store ⇒ both
  `MustRecompute NoPriorEvidence`) render as **two** distinct `entries` elements under that gate id, in the
  report's order, neither merged nor deduplicated.

**Checkpoint**: US1 + US2 + US3 + US4 — the projection is total over the full report space, deterministic,
honestly scoped, and carry-faithful. All success criteria SC-001…SC-007 are pinned.

---

## Phase 7: Surface governance & polish (Tier-1 baseline, scope hygiene, docs, validation)

**Purpose**: Lock the public surface (Principle II) and prove the assembly's reference graph stays minimal.
Bless the baseline only after the surface is final.

- [X] T015 `tests/.../SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the F020–F041 precedent):
  enumerate the public surface of `FS.GG.Governance.CacheEligibilityJson` and compare byte-for-byte to
  `surface/FS.GG.Governance.CacheEligibilityJson.surface.txt`, with the `BLESS_SURFACE=1` re-bless path;
  plus a **scope-hygiene** assertion (contracts/cache-eligibility-json-api.md scope guard, Principle II) that
  the assembly references **only** `FS.GG.Governance.CacheEligibility` and — transitively —
  `FS.GG.Governance.EvidenceReuse`, `FS.GG.Governance.Gates`, `FS.GG.Governance.FreshnessKey`,
  `FS.GG.Governance.Config`, plus `FSharp.Core` / BCL — and **not** `RouteJson`, `AuditJson`, `GatesJson`,
  `Enforcement`, `Ship`, `Snapshot`, `Routing`, `Findings`, any `Adapters.*`, `Host`, `Cli`, and no
  third-party package (serialization is the shared-framework `System.Text.Json`). FR-012's *behavioral*
  exclusions and FR-008's purity negatives are satisfied **by construction** — the surface holds only
  `schemaVersion` + `ofReport`, and ExclusionsTests (T012) pins the document content — guarded by this
  reference-graph + surface-drift check.
- [X] T016 Generate and commit `surface/FS.GG.Governance.CacheEligibilityJson.surface.txt` via
  `BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.CacheEligibilityJson.Tests/...`; review the diff
  (exactly the one public module `CacheEligibilityJson` with `schemaVersion` + `ofReport`; no writer / token
  helper leak) and commit it as part of the Tier-1 change. After this, T015 runs green without
  `BLESS_SURFACE`.
- [X] T017 [P] Update `CLAUDE.md`'s SPECKIT plan reference to point at
  `specs/042-cache-eligibility-json/plan.md` (the active pointer). No other doc changes.
- [X] T018 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`, `dotnet fsi
  scripts/prelude.fsx` (the F042 section prints the expected empty / reusable / noPriorEvidence /
  inputsChanged / ordering / duplicate-gate / `inputsChanged [] ≠ noPriorEvidence` results), and `dotnet test
  tests/FS.GG.Governance.CacheEligibilityJson.Tests/...` — all green under `TreatWarningsAsErrors`. Confirm
  `dotnet build && dotnet test` over the existing projects is unchanged (no existing `src/`, `surface/`, or
  merged test project modified — the new project + test project are purely additive).

**Checkpoint**: Tier-1 surface is blessed and guarded; the assembly's reference graph is minimal
(`CacheEligibility` + transitive cores only); the full solution builds and tests green; existing cores
untouched. **The route/audit emission row's deferred cache-eligibility *projection* line is closed** — the
F041 report now has its deterministic, versioned `cache-eligibility.json` document.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` surface, FSI proof,
  compiling stub, and test scaffolding (`Support.fs`, `Main.fs`) must exist before any story test can be
  written and FAIL.
- **Phase 3 (US1)**: depends on Phase 2. The MVP. T010 implements the **whole** linear walk (all verdict /
  cause branches), since the function must be complete to be total.
- **Phase 4 (US2)**: depends on Phase 2 + T010; its tests (T011, T012) validate determinism / versioning /
  order / exclusions of the same finished `ofReport` — there is no separate US2 implementation task.
- **Phase 5 (US3)**: depends on Phase 2 + T010; T013 validates the no-hide carry of the finished `ofReport`.
- **Phase 6 (US4)**: depends on Phase 2 + T010; T014 validates totality of the finished `ofReport`.
- **Phase 7 (surface/polish)**: last — bless the baseline only after the surface is final (Phase 2 `.fsi`
  unchanged through implementation).

### Within each story

- US1's test (T009) is written FIRST and must FAIL against the Phase-2 stub, then pass after T010. US2/US3/US4
  tests pass against the complete `ofReport` once T010 lands (the single linear walk serves all four stories).
- The `.fsi` surface precedes the `.fs` body that satisfies it; `Support.fs` precedes the story test files
  that consume its builders/generators/parse helper.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) is independent of T001 (library `.fsproj`); T003 (sln) needs both.
- **Phase 2**: T006 `[P]` (prelude FSI section) is independent of the `.fsi`/stub work once the DLL name is
  fixed by T001. T004 (the `.fsi`) precedes T005 (stub `.fs`); T007/T008 (test scaffolding) need the
  compiling stub.
- **Story test files are all `[P]`** relative to each other (distinct files): T009, T011, T012, T013, T014
  touch different test files. They share `Support.fs` (T007) as a prerequisite and the finished `ofReport`
  (T010).
- **Phase 7**: T017 `[P]` (CLAUDE.md) is independent of the surface test; T015→T016→T018 are sequential.

---

## Task count per user story

- **Setup (Phase 1)**: 3 tasks (T001–T003).
- **Foundational (Phase 2)**: 5 tasks (T004–T008).
- **US1 (Phase 3)**: 2 tasks (T009 test, T010 impl) 🎯 MVP.
- **US2 (Phase 4)**: 2 tasks (T011 determinism/version/order, T012 exclusions; impl shared with T010).
- **US3 (Phase 5)**: 1 task (T013 no-hide; impl shared with T010).
- **US4 (Phase 6)**: 1 task (T014 totality; impl shared with T010).
- **Surface & polish (Phase 7)**: 4 tasks (T015–T018).
- **Total**: 18 tasks.

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)** — the project skeleton, the `.fsi` surface + FSI proof, and the linear
projection walk proven on the carry path: a real F041 report renders to a document with one faithful entry
per gate, each verdict and payload tracing back to the report value. This is the spec's P1 reason-to-exist
slice — the F041 verdict becomes a durable, inspectable artifact. Phase 4 (US2, P1) makes it a *contract*
(determinism, versioned schema, stable order, exclusions); Phase 5 (US3, P2) pins the no-hide cause carry;
Phase 6 (US4, P2) pins totality over the full report space; Phase 7 locks the Tier-1 surface and
reference-graph hygiene.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify US1's test (T009) FAILS against the Phase-2 stub before implementing T010, then passes; US2/US3/US4
  tests pass once T010 lands (one linear walk serves all four stories).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- Commit after each task or logical group; keep existing `src/`, `surface/`, and merged test projects
  untouched. F018 `gateIdValue`, F029 `categoryToken`, F030 `referenceValue`, and F041 `CacheEligibility.entries`
  are consumed verbatim, never modified (FR-014); the F041 report's already-fixed `GateId`-ordinal order
  (with its structural duplicate tiebreak) is preserved verbatim (FR-005) — `ofReport` re-orders nothing.
