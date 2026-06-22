---
description: "Task list for 043-freshness-inputs-resolution implementation"
---

# Tasks: Per-Gate Freshness-Inputs Resolution Core

**Feature branch**: `043-freshness-inputs-resolution`
**Spec**: `specs/043-freshness-inputs-resolution/spec.md`
**Plan**: `specs/043-freshness-inputs-resolution/plan.md`

**Input**: Design documents from `/specs/043-freshness-inputs-resolution/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/freshness-resolution-api.md, contracts/freshness-resolution-outcome.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a carry / no-fabricate / no-hide / determinism / totality / recompute-safe contract — the tests
*are* the deliverable's proof. Every input is a **real** F018 `Gate` (its carried five-field `FreshnessKey`)
plus real F029 newtypes (`RuleHash`, `ArtifactHash`, `CommandVersion`, `GeneratorVersion`, `Revision`) bundled
into a real `SensedFacts`; the F041 bridge is proven by feeding `candidate` results into real
`CacheEligibility.evaluate`/`evaluateGate`. No mock, no clock read, no hand-built oracle, no git, no hash/
freshness-key/digest computed. No mocks ⇒ no `Synthetic` disclosure needed.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`…`[US3]`) for
traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface — the `FS.GG.Governance.FreshnessResolution`
assembly + its two modules `Model` and `FreshnessResolution` — and a new `surface/*.surface.txt` baseline,
with no new third-party dependency). All tasks share the feature tier; no per-task tier annotations needed.

**Elmish/MVU**: **Not applicable** — a pure, total, deterministic join from already-typed values
(`Gate list` + `SensedFacts`) to a `FreshnessResolutionReport` (plan Constitution Check, Principle IV = N/A).
No `Model`/`Msg`/`Effect`/`update`/interpreter tasks (the `Model` module here is a *vocabulary* module, not an
MVU model). The host edge that actually senses git/filesystem and supplies `SensedFacts`, the F041 evaluation,
the JSON projections, and any real cache store are later edges, out of scope. Principle VI is likewise N/A — no
operationally-significant event to observe; **totality** (`resolve` never throws) and the **no-hide** rule (a
missing fact is named, never silently defaulted) stand in for safe failure.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new resolution library + test project so everything compiles and the solution
restores. No semantics yet.

- [X] T001 Create `src/FS.GG.Governance.FreshnessResolution/FS.GG.Governance.FreshnessResolution.fsproj` —
  SDK-style, `RootNamespace`/`PackageId` `FS.GG.Governance.FreshnessResolution`, `Version` `0.1.0`,
  `IsPackable=true` (the CacheEligibility/CacheEligibilityJson packable-core precedent). `<Compile>` order:
  `Model.fsi`, `Model.fs`, `FreshnessResolution.fsi`, `FreshnessResolution.fs`. **One** `<ProjectReference>` —
  to `../FS.GG.Governance.CacheEligibility/FS.GG.Governance.CacheEligibility.fsproj` (F041, provides
  `CandidateGate` and, transitively, F029 `FreshnessInputs` + newtypes, F018 `Gate`/`GateId`/`FreshnessKey` +
  `gateIdValue`, F030 `EvidenceReuse`, F014 `Config` newtypes; the transitive pure cores arrive through F041
  and need no direct reference — the F042 "references only `CacheEligibility`, the rest arrive transitively"
  precedent; plan Technical Context, research D2). **No third-party `PackageReference`** (FR-013) — the join is
  pure F# over typed values, no serialization, so the library stays `System.*`/`FSharp.Core`-only. Add a header
  comment: pure total deterministic join of each gate's carried `FreshnessKey` (dropping `Cost`) with a supplied
  `SensedFacts` bundle into per-gate `FreshnessInputs`; senses nothing, computes no hash/freshness-key/digest,
  evaluates no cache eligibility, renders no JSON, persists nothing; one-way dependency
  `FreshnessResolution -> CacheEligibility -> …`; no RouteJson/AuditJson/Enforcement/Ship/Snapshot/Routing/
  host/CLI coupling.
- [X] T002 [P] Create
  `tests/FS.GG.Governance.FreshnessResolution.Tests/FS.GG.Governance.FreshnessResolution.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  new package); `<ProjectReference>`s to the new resolution library and to `FS.GG.Governance.CacheEligibility`
  (to call `evaluate`/`evaluateGate` and build the real F041 bridge) plus `FS.GG.Governance.Gates`,
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config` (for real `Gate`/`FreshnessKey`/`gateIdValue`,
  `FreshnessInputs` + newtype literals, `CheckId`/`DomainId`/`CommandId`/`EnvironmentClass`). `<Compile>`
  order: `Support.fs`, `ResolveTests.fs`, `CommandAbsenceTests.fs`, `CandidateBridgeTests.fs`,
  `UnresolvedTests.fs`, `SensedEmptyTests.fs`, `DeterminismTests.fs`, `CompletenessTests.fs`,
  `TotalityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the two `.fsi` contracts, FSI proof, compiling stub, test scaffolding) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`Model.fsi` + `FreshnessResolution.fsi`), prove it in FSI
(Principle I), and add the `Model.fs` vocabulary + a stubbed join body + test scaffolding so the library and
tests compile and the resolve tests can FAIL before implementation. **⚠️ No story work begins until this phase
is complete.**

- [X] T004 Write `src/FS.GG.Governance.FreshnessResolution/Model.fsi` — the vocabulary surface
  (contracts/freshness-resolution-api.md `module Model`, data-model.md). `open` the upstream model namespaces
  the types reference (`FS.GG.Governance.Gates.Model` for `GateId`, `FS.GG.Governance.FreshnessKey.Model` for
  `RuleHash`/`ArtifactHash`/`CommandVersion`/`GeneratorVersion`/`Revision`/`FreshnessInputs`,
  `FS.GG.Governance.Config.Model` for `CommandId`). Declare exactly the five NEW types: `SensedFacts` (the
  six-field option/Map bundle — repo-wide facts as `option`, per-key facts as `Map` where key-present = sensed),
  `MissingFact` (the closed six-case no-hide union in FR-002 field order), `ResolutionOutcome`
  (`Resolved of FreshnessInputs | Unresolved of MissingFact list`), `FreshnessResolutionEntry`
  (`{ Gate: GateId; Outcome: ResolutionOutcome }`), and the single-case wrapper
  `FreshnessResolutionReport of FreshnessResolutionEntry list`. Curated doc comments: the option/Map
  "sensed-empty vs unsensed" distinction (an empty `ArtifactHash list` under a present key is a legitimate
  sensed value; an absent key is *not sensed*); `Unresolved` carries a **non-empty** list (no-hide); the report
  is one entry per gate in `GateId`-ordinal order with a structural tiebreak (duplicates preserved). Reused
  upstream types are **never redefined** (FR-012) — they arrive through the F041 reference.
- [X] T005 Write `src/FS.GG.Governance.FreshnessResolution/FreshnessResolution.fsi` — the SOLE operations
  surface (contracts/freshness-resolution-api.md `module FreshnessResolution`). `open`
  `FS.GG.Governance.Gates.Model` (`Gate`), `FS.GG.Governance.CacheEligibility.Model` (`CandidateGate`), and
  `FS.GG.Governance.FreshnessResolution.Model`. Declare exactly six members:
  `resolve: gates: Gate list -> sensed: SensedFacts -> FreshnessResolutionReport` (the pure total join — one
  attributed outcome per gate, ordered by `GateId` ordinal + structural tiebreak, `resolve [] sensed =
  FreshnessResolutionReport []`); `entries: report -> FreshnessResolutionEntry list` (unwrap);
  `candidate: entry -> CandidateGate option` (the F041 bridge — `Some` for `Resolved`, `None` for `Unresolved`,
  the *only* function producing a `CandidateGate`, so an unresolved gate can never become a candidate, FR-004/
  FR-010); `isResolved: outcome -> bool`; `missingFacts: outcome -> MissingFact list` (`[]` for `Resolved`, the
  non-empty enum-ordered list for `Unresolved`); `missingFactToken: fact -> string` (the stable injective wire
  tokens `ruleHash`/`coveredArtifacts`/`commandVersion`/`generatorVersion`/`baseRevision`/`headRevision`,
  mirroring F029 `categoryToken` and the F042 token precedent). Curated doc comments state the purity/totality
  negatives (no file/process/clock/network/git, no hash/freshness-key/digest, no cache eligibility evaluated,
  the opaque newtypes never parsed/re-hashed/fabricated, never throws). No access modifiers will appear in the
  matching `.fs`; every join / token helper stays hidden by its absence from these two `.fsi` files (the F029/
  F041/F042 precedent, Principle II).
- [X] T006 Add `src/FS.GG.Governance.FreshnessResolution/Model.fs` — the NEW vocabulary as real type
  definitions (they are data; define them fully — `SensedFacts`, `MissingFact`, `ResolutionOutcome`,
  `FreshnessResolutionEntry`, `FreshnessResolutionReport`), `open`ing the same upstream namespaces as
  `Model.fsi`. No `private`/`internal`/`public` modifiers (Principle II). Confirm
  `dotnet build src/FS.GG.Governance.FreshnessResolution/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 Add `src/FS.GG.Governance.FreshnessResolution/FreshnessResolution.fs` — the
  `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module FreshnessResolution` with
  `resolve` and `candidate` as `failwith "not implemented"` stubs (the behavioral join + the F041 bridge), and
  the pure-data accessors implemented fully since they are total trivial DU maps: `entries`
  (unwrap the single-case), `isResolved` (`match`), `missingFacts` (`match`), and `missingFactToken` (the
  exhaustive closed-DU token map, **wildcard-free** so a future `MissingFact` case is a compile error here). No
  access modifiers (Principle II). Confirm the build is clean under `TreatWarningsAsErrors`.
- [X] T008 [P] Append the F043 design-first section to `scripts/prelude.fsx` (after the F042 section) — `#r`
  the new Debug DLL plus the `CacheEligibility`, `EvidenceReuse`, `Gates`, `FreshnessKey`, `Config` DLLs;
  `open` the needed model + operation modules; build the three headline paths from
  contracts/freshness-resolution-outcome.md worked examples and `printfn` them: (A) **resolve** a fully-sensed
  command-bearing gate `build:tests` and feed `entries report |> List.choose candidate` straight into
  `CacheEligibility.evaluate cands store` (accepted without adaptation, FR-010); (B) **unresolved** — drop
  `RuleHash`, `Base`, the gate's covered-artifacts key, and the command version and show `lint:style` names
  exactly `[ruleHash; coveredArtifacts; commandVersion; baseRevision]` with `candidate = None`; (C)
  **determinism** — `resolve` the same gates in two orders and show value-equal reports ordered by `GateId`.
  Also show example (D) sensed-empty (`map [g, []]` ⇒ `Resolved … CoveredArtifacts = []`) vs unsensed (key
  absent ⇒ `Unresolved [MissingCoveredArtifacts; …]`) and (E) a duplicate-`GateId` pair ⇒ two entries. This is
  the Principle-I FSI proof; it documents the shape even while the join body is stubbed.
- [X] T009 Write `tests/FS.GG.Governance.FreshnessResolution.Tests/Support.fs` — real,
  literally-constructible builders (Principle V, no mocks), adapted from the F041/F042 `Support.fs` precedents:
  a `gid d c = GateId (d + ":" + c)` helper and a `gate` builder assembling a real F018 `Gate` from a real
  `FreshnessKey { Check; Domain; Cost; Environment; Command }` (vary `Command` between `Some c` and `None`,
  and `Cost` so its drop is observable); a complete literal `fullSensed: SensedFacts` carrying every repo-wide
  fact, the covered-artifacts key for each test gate (with a distinct non-empty `ArtifactHash list`), and the
  command version for each declared command — so a single dropped fact is observable; single-fact `without*`
  mutators (`withoutRuleHash`, `withoutGeneratorVersion`, `withoutBase`, `withoutHead`,
  `withoutCovered gateId`, `withoutCommandVersion cmd`) and a `(MissingFact * (SensedFacts -> SensedFacts))`
  table of all six gaps; a `storeOf` helper folding `EvidenceReuse.record` over `EvidenceReuse.empty` and a
  `cacheReport cands store = CacheEligibility.evaluate cands store` convenience for the bridge tests; the
  worked-example gates/sensed-facts from the outcome contract (A–E); and FsCheck generators for arbitrary
  well-typed inputs — `Gate` lists of arbitrary length (incl. `[]`, singletons, duplicate-`GateId` via a small
  label pool, `Command = Some`/`None`, `:`-containing ids) and `SensedFacts` states (all present, partial,
  all absent; covered-artifacts present-empty / present-nonempty / absent). Plus the
  `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from the F041/F042
  `Support.fs` precedent. No I/O beyond repo-root resolution.
- [X] T010 Write `tests/FS.GG.Governance.FreshnessResolution.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because `resolve`/`candidate` are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Assemble complete freshness inputs per selected gate (Priority: P1) 🎯 MVP

**Goal**: For each selected gate, lift its carried five-field `FreshnessKey` (dropping `Cost`) and the six
supplied sensed facts into a complete F029 `FreshnessInputs`, shaped to feed F041 verbatim. This is the
feature's reason to exist — the bridge that lets the already-built reuse (F030) and cache-eligibility (F041)
cores run against a real routed change.

**Independent Test**: Supply a selected gate with a known carried identity and a bundle of known sensed facts;
confirm the resolved `FreshnessInputs` carry the gate's four identity fields verbatim and the six sensed fields
verbatim, with `Cost` dropped, and that `candidate` of the resolved entry is accepted by
`CacheEligibility.evaluate` without adaptation.

### Tests for User Story 1 (write first; must FAIL against the Phase-2 stub)

- [X] T011 [P] [US1] `tests/.../ResolveTests.fs` — the carry path (US1 #1/#2, SC-001, L-carry): for a
  fully-sensed gate, assert the `Resolved` `FreshnessInputs` field-by-field — `Check`/`Domain`/`Environment`/
  `Command` equal the gate's carried `FreshnessKey` verbatim (incl. `Command`'s `option` preserved), and
  `RuleHash`/`CoveredArtifacts`/`CommandVersion`/`GeneratorVersion`/`Base`/`Head` equal the supplied
  `SensedFacts` verbatim; assert **`Cost` never appears** in the resolved value (a gate whose `Cost` differs
  but whose other fields match resolves to the same `FreshnessInputs`). Drive both the worked example A and an
  FsCheck property over fully-sensed gates asserting carry with zero fabricated/defaulted/zero-filled values.
- [X] T012 [P] [US1] `tests/.../CommandAbsenceTests.fs` — the consistent-absence edge (US1 edge, SC-003,
  L-command-absent): a gate with `Command = None`, all repo-wide facts and covered artifacts sensed, resolves
  to `Resolved` with `Command = None` **and** `CommandVersion = None`, and is **never** `Unresolved` on a
  command-version basis (no `MissingCommandVersion` for a command-less gate) — even when `CommandVersions` is
  empty or omits every command. Drive worked example B plus an FsCheck property over command-less gates.
- [X] T013 [P] [US1] `tests/.../CandidateBridgeTests.fs` — the F041 bridge (US1 #3 + FR-004, SC-007,
  L-candidate/L-recompute-safe): (1) `candidate` of every `Resolved` entry is `Some { Gate = e.Gate; Inputs =
  <the resolved FreshnessInputs> }` and is accepted by real `CacheEligibility.evaluate`/`evaluateGate`
  **without adaptation** (feed `entries report |> List.choose candidate` into `evaluate` over a real
  `ReuseStore` and assert it produces one verdict per resolved gate); (2) `candidate` of every `Unresolved`
  entry is `None` (recompute-safe by construction — no path from `Unresolved` to a candidate); (3) a `Resolved`
  outcome carries no reuse decision, skip, severity, ship verdict, or exit-code basis (necessary-not-sufficient,
  FR-011 — asserted structurally: the entry holds only `Gate` + `Outcome`). FsCheck property over mixed
  reports: `List.choose candidate (entries report)` has exactly as many elements as the report has `Resolved`
  entries.

### Implementation for User Story 1

- [X] T014 [US1] Implement `resolve` (and `candidate`) in `FreshnessResolution.fs` per the data-model.md
  field-by-field join and the api/outcome contracts — a hidden per-gate `resolveGate` that, for each input
  `Gate g` with `fk = g.FreshnessKey` against `SensedFacts s`, collects the missing facts in **FR-002 field
  order** (`MissingRuleHash` if `s.RuleHash = None`; `MissingCoveredArtifacts` if `g.Id` absent from
  `s.CoveredArtifacts`; `MissingCommandVersion` only when `fk.Command = Some c` and `c` absent from
  `s.CommandVersions`; `MissingGeneratorVersion`/`MissingBaseRevision`/`MissingHeadRevision` for the repo-wide
  options) and, if that list is empty, builds `Resolved` with `Check`/`Domain`/`Environment`/`Command` from
  `fk` (dropping `fk.Cost`), `CommandVersion = fk.Command |> Option.bind (fun c -> Map.tryFind c
  s.CommandVersions)`, and the six sensed fields verbatim — else `Unresolved missingFacts`. Map each gate to a
  `FreshnessResolutionEntry`, then `List.sortWith` using the total order defined in data-model.md — first the
  **ordinal** comparison of `gateIdValue entry.Gate` (`System.String.CompareOrdinal`, *not* current-culture
  `compare` on `string`), then, for entries sharing a `GateId`, the F# **structural** `compare` of the whole
  `FreshnessResolutionEntry` — so duplicate `GateId`s are deterministically ordered and **preserved** (research
  D7); wrap in `FreshnessResolutionReport`. `candidate` is the trivial `match` on `Outcome` (`Resolved inputs ⇒ Some { Gate;
  Inputs = inputs }`, `Unresolved _ ⇒ None`). Every `match` over the closed `MissingFact`/`ResolutionOutcome`
  unions is **exhaustive with NO wildcard** (Principle III), so a future case is a compile error here. Pure
  `List`/`Map`/`Option` only; no clock/filesystem/git/environment/network, no hash/freshness-key/digest, no
  cache eligibility evaluated, the opaque newtypes consumed without parsing or re-hashing, nothing fabricated/
  defaulted/zero-filled, never throws (FR-003/FR-008/FR-009). This single total join serves **all three
  stories** (US1 carry, US2 no-hide, US3 determinism/completeness/totality). Run T011–T013: green.

**Checkpoint**: US1 is functional — a real gate + real sensed facts resolves to a complete `FreshnessInputs`
that flows into F041 unchanged. The MVP join exists.

---

## Phase 4: User Story 2 — Never fabricate: name the missing sensed fact instead (Priority: P2)

**Goal**: When a required sensed fact is unavailable, the join never invents, defaults, or zero-fills it;
instead it yields a no-hide `Unresolved` naming exactly and *every* missing fact, recompute-safe by
construction. `resolve` is already whole from T014; this phase validates its no-fabricate / no-hide / sensed-
empty-vs-unsensed behavior.

**Independent Test**: Supply a gate whose sensed-facts bundle is missing one or more required values; confirm
the gate yields an `Unresolved` outcome naming exactly the missing fact(s), that no `FreshnessInputs` is
produced for it, and that no value was fabricated, defaulted, or zero-filled.

### Tests for User Story 2 (validate the finished `resolve` from T014)

- [X] T015 [P] [US2] `tests/.../UnresolvedTests.fs` — no-fabricate + no-hide (US2 #1/#2/#3, SC-002,
  L-no-fabricate/L-no-hide): (1) for each single dropped fact in the `Support.fs` gap table, the gate is
  `Unresolved [thatFact]` and produces **no** `FreshnessInputs` (assert via `missingFacts`/`isResolved`); (2)
  the no-hide rule — a gate missing several required facts is `Unresolved` listing **every** gap, in
  `MissingFact` enum order, never truncated to the first (drive worked example C: `lint:style` ⇒
  `[MissingRuleHash; MissingCoveredArtifacts; MissingCommandVersion; MissingBaseRevision]`, tokens
  `ruleHash`/`coveredArtifacts`/`commandVersion`/`baseRevision` via `missingFactToken`); (3) an `Unresolved`
  entry exposes no fabricated/defaulted/zero-filled hash, version, or revision (it structurally carries only
  the `MissingFact list`). FsCheck property: for any partially-sensed gate, `missingFacts (Resolved/Unresolved)`
  equals exactly the set of facts the bundle omitted (in enum order), neither subset nor superset. (4) **all six
  tokens, injective** — assert `missingFactToken` over the full closed `MissingFact` set yields exactly
  `MissingRuleHash -> "ruleHash"`, `MissingCoveredArtifacts -> "coveredArtifacts"`,
  `MissingCommandVersion -> "commandVersion"`, `MissingGeneratorVersion -> "generatorVersion"`,
  `MissingBaseRevision -> "baseRevision"`, `MissingHeadRevision -> "headRevision"` (pins `generatorVersion` and
  `headRevision`, which worked example C does not exercise) and that the six tokens are pairwise distinct
  (injective), so the stable wire vocabulary cannot drift.
- [X] T016 [P] [US2] `tests/.../SensedEmptyTests.fs` — sensed-empty vs unsensed (Edge, FR-003,
  outcome-contract example D): a gate whose covered-artifact set was **sensed as empty**
  (`CoveredArtifacts = map [g.Id, []]`, all else present) resolves to `Resolved { … CoveredArtifacts = []; … }`
  — a legitimate resolved empty set, **never** `MissingCoveredArtifacts`; the *same* gate **absent** from
  `CoveredArtifacts` is `Unresolved [MissingCoveredArtifacts; …]`. Assert the two are structurally distinct and
  never conflated — the present-empty key resolves and the absent key is unresolved on covered artifacts.

**Checkpoint**: US1 + US2 — the join is honest: every gap is named (every gap, never truncated), nothing is
fabricated, an unresolved gate yields no candidate, and "sensed empty" is never confused with "not sensed".

---

## Phase 5: User Story 3 — One attributable outcome per gate, deterministic and total (Priority: P3)

**Goal**: Exactly one outcome per supplied gate, each attributed to its `GateId`, in deterministic `GateId`-
ordinal order with a structural tiebreak (duplicates preserved), byte-identical for value-equal inputs
regardless of input order / cwd / clock / filesystem, and total over the full cross-product of gate counts and
sensed-facts states. `resolve` is already whole from T014; this phase validates determinism, completeness, and
totality.

**Independent Test**: Resolve a set of selected gates supplied in arbitrary order against a fixed sensed-facts
bundle; confirm exactly one outcome per gate, ordered by gate identity, every gate preserved, and that
re-resolving the same inputs under a changed working directory / clock / filesystem yields a byte-identical
report.

### Tests for User Story 3 (validate the finished `resolve` from T014)

- [X] T017 [P] [US3] `tests/.../DeterminismTests.fs` — order-independence + purity (US3 #2/#3, SC-005,
  L-order/L-pure): (1) two input orders of the same gates yield **value-equal** reports, entries ordered by
  `gateIdValue` ordinal with the structural tiebreak (an FsCheck property over shuffled gate lists); (2) a
  purity check mirroring the F041/F042 precedent — `resolve` produces the identical report when computed in
  different working directories, at different times, with unrelated filesystem state changed between calls (no
  I/O performed). Drive worked example C plus FsCheck over arbitrary gates × sensed states.
- [X] T018 [P] [US3] `tests/.../CompletenessTests.fs` — attribution + completeness (US3 #1, SC-006,
  L-attribute/L-complete): for N supplied gates the report has exactly N entries, each carrying its
  originating `GateId` (`entries` length = input length; the multiset of `Gate`s equals the input multiset);
  **no** gate dropped, merged, or silently deduplicated; **duplicate `GateId`s are preserved as separate
  entries**, deterministically ordered by the structural tiebreak (worked example E: two gates sharing a
  `GateId` ⇒ two entries). FsCheck property over duplicate-inducing generators. Also pin the **tiebreak total
  order** (data-model.md): two gates sharing a `GateId` but yielding **distinct `Outcome`s** (e.g. one fully
  sensed ⇒ `Resolved`, one missing a fact ⇒ `Unresolved`) are ordered by the structural `compare` of the whole
  entry and produce a **byte-identical** report regardless of the two input orders — proving the tiebreak is a
  genuine total order, not input-order-stable happenstance.
- [X] T019 [P] [US3] `tests/.../TotalityTests.fs` — totality (US3, SC-004, L-total): an FsCheck property over
  the full cross-product of gate counts (zero, one, many — incl. duplicates) × sensed-facts states (all
  present, partially present, all absent) asserts `resolve` returns a well-formed report and **never throws**;
  the empty report `resolve [] sensed = FreshnessResolutionReport []` is a valid success (not an error); a gate
  missing *every* required sensed fact yields one well-formed `Unresolved` entry naming all gaps, never a
  dropped gate.

**Checkpoint**: US1 + US2 + US3 — the report is gate-attributable, complete, deterministically ordered,
reproducible, and total over the full input space. Success criteria SC-001…SC-007 are pinned; SC-008 (the
additive guarantee) lands in Phase 6 (T023).

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope hygiene, docs, validation)

**Purpose**: Lock the public surface (Principle II) and prove the assembly's reference graph stays minimal.
Bless the baseline only after the surface is final.

- [X] T020 `tests/.../SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the F020–F042 precedent):
  enumerate the public surface of `FS.GG.Governance.FreshnessResolution` (both modules `Model` and
  `FreshnessResolution`) and compare byte-for-byte to
  `surface/FS.GG.Governance.FreshnessResolution.surface.txt`, with the `BLESS_SURFACE=1` re-bless path; plus a
  **scope-hygiene** assertion (contracts/freshness-resolution-api.md scope guard, Principle II) that the
  assembly references **only** `FS.GG.Governance.CacheEligibility` and — transitively —
  `FS.GG.Governance.EvidenceReuse`, `FS.GG.Governance.Gates`, `FS.GG.Governance.FreshnessKey`,
  `FS.GG.Governance.Config`, `FS.GG.Governance.Kernel`, plus `FSharp.Core` / BCL — and **not** `RouteJson`,
  `AuditJson`, `GatesJson`, `CacheEligibilityJson`, `Enforcement`, `Ship`, `Snapshot`, `Routing`, `Findings`,
  any `Adapters.*`, `Host`, `Cli`, and **no** third-party package (FR-013 — no serialization). FR-014's
  additive guarantee and FR-009's purity negatives are satisfied **by construction** — the surface holds only
  the five vocabulary types + `resolve`/`entries`/`candidate`/`isResolved`/`missingFacts`/`missingFactToken` —
  guarded by this reference-graph + surface-drift check.
- [X] T021 Generate and commit `surface/FS.GG.Governance.FreshnessResolution.surface.txt` via
  `BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.FreshnessResolution.Tests/...`; review the diff (exactly
  the two public modules — `Model` with the five vocabulary types, `FreshnessResolution` with the six members;
  no join / token / sort helper leak) and commit it as part of the Tier-1 change. After this, T020 runs green
  without `BLESS_SURFACE`.
- [X] T022 [P] Update `CLAUDE.md`'s SPECKIT plan reference to point at
  `specs/043-freshness-inputs-resolution/plan.md` (the active pointer). No other doc changes.
- [X] T023 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`, `dotnet fsi
  scripts/prelude.fsx` (the F043 section prints the expected resolve / candidate-into-F041 / unresolved-gap-
  naming / determinism / sensed-empty-vs-unsensed / duplicate-gate results), and `dotnet test
  tests/FS.GG.Governance.FreshnessResolution.Tests/...` — all green under `TreatWarningsAsErrors`. Confirm
  `dotnet build && dotnet test` over the existing projects is unchanged (no existing `src/`, `surface/`, or
  merged test project modified — the new project + test project are purely additive, SC-008).

**Checkpoint**: Tier-1 surface is blessed and guarded; the assembly's reference graph is minimal
(`CacheEligibility` + transitive cores only); the full solution builds and tests green; existing cores
untouched. **The KEY GAP is closed** — a routed change's selected gates can now be lifted from their carried
five-field `FreshnessKey` into complete F029 `FreshnessInputs` (or a no-hide `Unresolved`), unblocking the
route/audit cache-eligibility **host wiring** row that runs F041 over the result.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the two `.fsi` surfaces, the
  `Model.fs` vocabulary, the FSI proof, the stubbed join, and the test scaffolding (`Support.fs`, `Main.fs`)
  must exist before any story test can be written and FAIL.
- **Phase 3 (US1)**: depends on Phase 2. The MVP. T014 implements the **whole** per-gate join (all field +
  missing-fact branches), since the function must be complete to be total.
- **Phase 4 (US2)**: depends on Phase 2 + T014; its tests (T015, T016) validate the no-fabricate / no-hide /
  sensed-empty behavior of the same finished `resolve` — there is no separate US2 implementation task.
- **Phase 5 (US3)**: depends on Phase 2 + T014; T017/T018/T019 validate determinism / completeness / totality
  of the same finished `resolve`.
- **Phase 6 (surface/polish)**: last — bless the baseline only after the surface is final (Phase 2 `.fsi`
  files unchanged through implementation).

### Within each story

- US1's tests (T011–T013) are written FIRST and must FAIL against the Phase-2 stub, then pass after T014. US2/
  US3 tests pass against the complete `resolve` once T014 lands (the single per-gate join serves all three
  stories).
- The two `.fsi` surfaces precede the `.fs` bodies that satisfy them; `Model.fsi`/`Model.fs` precede
  `FreshnessResolution.fsi`/`.fs` (compile order); `Support.fs` precedes every story test file that consumes
  its builders / generators / F041 bridge helper.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) is independent of T001 (library `.fsproj`); T003 (sln) needs both.
- **Phase 2**: T008 `[P]` (prelude FSI section) is independent of the `.fsi`/stub work once the DLL name is
  fixed by T001. T004 (`Model.fsi`) → T005 (`FreshnessResolution.fsi`) → T006 (`Model.fs`) → T007 (stub `.fs`)
  follow the compile order; T009/T010 (test scaffolding) need the compiling stub.
- **Story test files are all `[P]`** relative to each other (distinct files): T011, T012, T013, T015, T016,
  T017, T018, T019 touch different test files. They share `Support.fs` (T009) as a prerequisite and the
  finished `resolve` (T014).
- **Phase 6**: T022 `[P]` (CLAUDE.md) is independent of the surface test; T020 → T021 → T023 are sequential.

---

## Task count per user story

- **Setup (Phase 1)**: 3 tasks (T001–T003).
- **Foundational (Phase 2)**: 7 tasks (T004–T010).
- **US1 (Phase 3)**: 4 tasks (T011–T013 tests, T014 impl) 🎯 MVP.
- **US2 (Phase 4)**: 2 tasks (T015 no-hide, T016 sensed-empty; impl shared with T014).
- **US3 (Phase 5)**: 3 tasks (T017 determinism, T018 completeness, T019 totality; impl shared with T014).
- **Surface & polish (Phase 6)**: 4 tasks (T020–T023).
- **Total**: 23 tasks.

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)** — the project skeleton, the two `.fsi` surfaces + `Model` vocabulary +
FSI proof, and the per-gate join proven on the carry path: a real gate + real sensed facts resolves to a
complete F029 `FreshnessInputs` whose `candidate` flows into F041 unchanged, with `Cost` dropped and command
absence handled consistently. This is the spec's P1 reason-to-exist slice — the bridge that lets F030/F041 run
against a real routed change. Phase 4 (US2, P2) pins the no-fabricate / no-hide / sensed-empty honesty; Phase 5
(US3, P3) pins determinism, completeness, and totality; Phase 6 locks the Tier-1 surface and reference-graph
hygiene.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify US1's tests (T011–T013) FAIL against the Phase-2 stub before implementing T014, then pass; US2/US3
  tests pass once T014 lands (one per-gate join serves all three stories).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- Commit after each task or logical group; keep existing `src/`, `surface/`, and merged test projects
  untouched. F018 `Gate`/`GateId`/`FreshnessKey`/`gateIdValue`, F029 `FreshnessInputs` + newtypes, F041
  `CandidateGate`, and F014 `Config` newtypes are consumed **verbatim**, never modified or redefined (FR-012/
  FR-014); `resolve` fabricates nothing and re-derives no hash/freshness-key/digest.
