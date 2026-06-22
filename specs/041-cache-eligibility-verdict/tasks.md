---
description: "Task list for 041-cache-eligibility-verdict implementation"
---

# Tasks: Per-Gate Cache-Eligibility Verdict Core

**Feature branch**: `041-cache-eligibility-verdict`
**Spec**: `specs/041-cache-eligibility-verdict/spec.md`
**Plan**: `specs/041-cache-eligibility-verdict/plan.md`

**Input**: Design documents from `/specs/041-cache-eligibility-verdict/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/cache-eligibility-api.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the spec is
itself a recompute-by-default / one-verdict-per-gate / deterministic-ordinal-order / no-hide / totality / determinism
contract — the tests *are* the deliverable's proof. Every value is a real, literally-constructible typed token (real
F018 `GateId`, real F029 `FreshnessInputs`, real F030 `ReuseStore` built via `EvidenceReuse.record`, real F030
`EvidenceRef`); no mock, no clock read, no gate run, no file read, no bytes hashed, no freshness key computed, no JSON
rendered, no cache lookup against a real store.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`) for
traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no new
third-party dependency). No per-task tier annotations needed — all tasks share the feature tier.

**Elmish/MVU**: **Not applicable** — a pure, total, deterministic roll-up over supplied values; no state, no I/O, no
workflow (plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks. The
*actual* cache lookup against a real store, the *resolving* of freshness inputs, running gates, producing evidence, and
rendering JSON are later projection / host edges, out of scope.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No semantics yet.

- [X] T001 Create `src/FS.GG.Governance.CacheEligibility/FS.GG.Governance.CacheEligibility.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.CacheEligibility`, `Version` `0.1.0`, `IsPackable=true` (override
  `Directory.Build.props` like Calibration/AdvisoryPromotion). `<Compile>` order: `Model.fsi`, `Model.fs`,
  `CacheEligibility.fsi`, `CacheEligibility.fs`. **Two** `<ProjectReference>`s — to
  `../FS.GG.Governance.EvidenceReuse/FS.GG.Governance.EvidenceReuse.fsproj` (F030, provides `decide` / `ReuseStore` /
  `ReuseDecision` / `RecomputeCause` / `EvidenceRef`) and
  `../FS.GG.Governance.Gates/FS.GG.Governance.Gates.fsproj` (F018, provides `GateId` / `gateIdValue`); the transitive
  pure cores `FreshnessKey` (F029, `FreshnessInputs` / `InputCategory`) and `Config` (F014, `CheckId` / `DomainId`)
  arrive through F030 / F018 and need no direct reference (the F030 "Config transitive through F029" precedent; plan
  Technical Context, research D3). **No third-party `PackageReference`** (FR-013). Add a header comment mirroring the
  Calibration `.fsproj`: pure total roll-up core; composes F030 `decide` verbatim and reuses F018 `GateId` + F029/F030
  vocabulary; no RouteJson/AuditJson/Enforcement/Ship/Snapshot/Routing/Findings/host/CLI coupling.
- [X] T002 [P] Create `tests/FS.GG.Governance.CacheEligibility.Tests/FS.GG.Governance.CacheEligibility.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`, `FsCheck`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no new package);
  `<ProjectReference>`s to the new core and to `FS.GG.Governance.EvidenceReuse` + `FS.GG.Governance.Gates` (for real
  `ReuseStore`/`EvidenceRef`/`FreshnessInputs`/`GateId` literals). `<Compile>` order: `Support.fs`,
  `RecomputeByDefaultTests.fs`, `ReusableTests.fs`, `AttributionAndOrderTests.fs`, `TotalityTests.fs`,
  `DeterminismTests.fs`, `NecessaryNotSufficientTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh GUIDs and
  the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies so the
library and tests compile and tests can FAIL before implementation. **⚠️ No story work begins until this phase is
complete.**

- [X] T004 Write `src/FS.GG.Governance.CacheEligibility/Model.fsi` — the SOLE public surface for the types
  (data-model.md): `open FS.GG.Governance.Gates.Model` (brings `GateId`, reused verbatim), `open
  FS.GG.Governance.FreshnessKey.Model` (brings `FreshnessInputs` / `InputCategory`, reused verbatim), and `open
  FS.GG.Governance.EvidenceReuse.Model` (brings `ReuseStore` / `ReuseDecision` / `RecomputeCause` / `EvidenceRef`,
  reused verbatim); the `CandidateGate = { Gate: GateId; Inputs: FreshnessInputs }` (FR-009 — both supplied facts, the
  core resolves/derives neither); the closed two-case `CacheEligibilityVerdict = Reusable of EvidenceRef |
  MustRecompute of RecomputeCause` (FR-001/FR-002/FR-010 — the only new union shell, payloads reused verbatim from
  F030); the `CacheEligibilityEntry = { Gate: GateId; Verdict: CacheEligibilityVerdict }` (FR-005); and the single-case
  `CacheEligibilityReport = CacheEligibilityReport of CacheEligibilityEntry list` (FR-006). Curated doc comments in the
  EvidenceReuse/Gates `.fsi` style: `GateId`, `FreshnessInputs`, `EvidenceRef`, `RecomputeCause` are opaque supplied
  facts (no validation, no parsing, no dereferencing, no resolving/re-hashing); the verdict is two outcomes so a
  threshold-unmet/opaque yes-no verdict is unrepresentable (FR-001); `Reusable` is necessary-not-sufficient — no skip
  action, severity, ship verdict, or exit-code basis (FR-010); `MustRecompute` always names its cause (FR-002, no-hide);
  the report preserves every gate, none dropped/merged/duplicated, in `GateId`-ordinal order (FR-006). No access
  modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.CacheEligibility/CacheEligibility.fsi` — the SOLE public surface for the
  operations (contracts/cache-eligibility-api.md): `val evaluate: candidates: CandidateGate list -> store: ReuseStore ->
  CacheEligibilityReport`; `val evaluateGate: candidate: CandidateGate -> store: ReuseStore ->
  CacheEligibilityVerdict`; `val entries: report: CacheEligibilityReport -> CacheEligibilityEntry list`; `val
  isReusable: verdict: CacheEligibilityVerdict -> bool`; `val reusableEvidence: verdict: CacheEligibilityVerdict ->
  EvidenceRef option`; `val recomputeCause: verdict: CacheEligibilityVerdict -> RecomputeCause option`. Doc comments
  state purity/totality/determinism and the laws (`evaluateGate` composes F030 `decide` verbatim and relabels 1-to-1 —
  `Reuse ref ⇒ Reusable ref`, `Recompute cause ⇒ MustRecompute cause` — introducing no new reuse policy, L-G1..L-G6;
  `evaluate` = `List.map evaluateGate` then a `List.sortWith` ordinal on `gateIdValue` with a structural tiebreak,
  yielding exactly one attributed verdict per candidate, order-independent, duplicates kept, empty-is-total,
  L-E1..L-E6; the projections L-P1..L-P4; reads no clock/filesystem/git/environment/network, runs no gate, produces no
  evidence, computes no freshness key/hash, resolves none of the supplied inputs, renders no JSON, makes no cache
  lookup against a real store, persists nothing, maps no exit code). The internal sort-comparator helper is **absent**
  from the `.fsi` (private by omission, Principle II).
- [X] T006 Add `src/FS.GG.Governance.CacheEligibility/Model.fs` and
  `src/FS.GG.Governance.CacheEligibility/CacheEligibility.fs` — real type definitions in `Model.fs` (the one union, the
  two records, the single-case report wrapper are plain data, define them fully); `evaluate`, `evaluateGate`,
  `entries`, `isReusable`, `reusableEvidence`, `recomputeCause` as `failwith "not implemented"` stubs in
  `CacheEligibility.fs`. No `private`/`internal`/`public` modifiers (Principle II). Confirm `dotnet build
  src/FS.GG.Governance.CacheEligibility/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F041 design-first section to `scripts/prelude.fsx` (after the F040 section) — `#r` the new Debug
  DLL plus the EvidenceReuse and Gates DLLs; `open FS.GG.Governance.Gates.Model`, `open
  FS.GG.Governance.FreshnessKey.Model`, `open FS.GG.Governance.EvidenceReuse`, `open
  FS.GG.Governance.EvidenceReuse.Model`, `open FS.GG.Governance.CacheEligibility.Model`, and `open
  FS.GG.Governance.CacheEligibility`; reuse the F030 `f29Inputs` / `f30Store` worked-example values where possible and
  `printfn` the intended results against the contract worked examples (quickstart.md): empty store, one candidate ⇒
  `MustRecompute NoPriorEvidence`; `record inputs0 (EvidenceRef "ev-A") empty`, exact-match candidate ⇒ `Reusable
  (EvidenceRef "ev-A")`; same store, candidate with `RuleHash` differing ⇒ `MustRecompute (InputsChanged [RuleHashCat])`;
  candidate with `RuleHash` and `Head` differing ⇒ `MustRecompute (InputsChanged [RuleHashCat; HeadRevisionCat])`; three
  candidates supplied `z:a`, `a:b`, `a:a` ⇒ report `entries` ordered `a:a`, `a:b`, `z:a` (ordinal) and byte-identical for
  any permutation; two candidates with the same `GateId` but different `Inputs` ⇒ two entries under that gate; no
  candidates ⇒ empty report (total, not an error). This is the Principle-I FSI proof; it documents the shape even while
  bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.CacheEligibility.Tests/Support.fs` — real, literally-constructible builders
  (Principle V, no mocks): a `gid d c = GateId (d + ":" + c)` helper; a complete literal `baseInputs: FreshnessInputs`
  (every category present and distinct so a single-field change is observable) adapted from the F030
  `EvidenceReuse.Tests/Support.fs` precedent, plus the single-field `variant*` mutators (`variantRuleHash`,
  `variantHead`, …) and the `(InputCategory * (FreshnessInputs -> FreshnessInputs)) list` of all categories; a
  `candidate gate inputs = { Gate = gate; Inputs = inputs }` builder; a `storeOf (entries: (FreshnessInputs *
  EvidenceRef) list)` helper folding `EvidenceReuse.record` over `EvidenceReuse.empty`; the worked-example candidates +
  stores from contracts/cache-eligibility-api.md with their expected `evaluateGate` / `evaluate` results, for
  example-test oracles; FsCheck generators for `GateId` strings (incl. empty, multi-byte, duplicate-inducing reuse of a
  small label pool), `FreshnessInputs` (varying every category), a `CandidateGate`, a `CandidateGate list` of arbitrary
  length (incl. `[]`, singletons, and lists with duplicate `GateId`s), `EvidenceRef` strings (incl. empty + multi-byte),
  and a `ReuseStore` (empty, matching, non-matching); and the `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` /
  `repoRoot` helper copied from the Calibration/EvidenceReuse `Support.fs` precedent. No I/O beyond repo-root resolution.
- [X] T009 Write `tests/FS.GG.Governance.CacheEligibility.Tests/Main.fs` — the Expecto entry point (`[<EntryPoint>]
  runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now FAILS only
because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Recompute by default when evidence is absent or stale (Priority: P1) 🎯 MVP

**Goal**: With no prior recorded evidence, or with recorded evidence produced under different freshness inputs, each
candidate's verdict defaults to `MustRecompute` carrying its named cause — `NoPriorEvidence` when no entry shares the
gate, `InputsChanged cats` naming exactly the changed freshness-input categories when the world moved — and no candidate
yields `Reusable` without a defensible F030 match. This is the safety property the whole row exists to protect and the
phase's exit criterion: a cache that is unsure never falsely claims reuse.

**Independent Test**: Evaluate candidate gates against an empty store, and against a store whose entries differ in one or
more freshness inputs; assert every verdict is `MustRecompute` with a named cause, and that no candidate yields
`Reusable`. No gate run, no cache lookup against a real store, no I/O.

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../RecomputeByDefaultTests.fs` — (1) **no prior evidence** (SC-001, US1 #1, L-G2/L-G3):
  a candidate against `EvidenceReuse.empty` (and against a store recording only *other* gates' inputs) ⇒ `evaluateGate
  candidate store = MustRecompute NoPriorEvidence`; (2) **changed inputs named, no-hide** (SC-003, US1 #2, L-G4): a store
  recording `baseInputs` under some `EvidenceRef`, evaluated against a candidate whose `Inputs` is `baseInputs` with one
  category mutated ⇒ `MustRecompute (InputsChanged [thatCat])`, and with several categories mutated ⇒ `MustRecompute
  (InputsChanged cats)` where `cats` is exactly F030's `diff` (no missing category, no spurious category, never truncated
  to the first difference) — drive this from the `allCategories` table in `Support.fs`; (3) **never reusable without a
  match** (SC-001, US1 #3, L-G2): an FsCheck property that for any candidate and any store containing no entry F030
  `decide` deems a defensible match, `isReusable (evaluateGate candidate store) = false`; (4) **recompute-by-default
  property** (SC-001, L-G2): an FsCheck property that whenever `EvidenceReuse.decide candidate.Inputs store` is a
  `Recompute _`, `evaluateGate candidate store` is `MustRecompute _` carrying the same cause — i.e. the relabel is
  information-preserving, introducing no new policy (FR-004).

### Implementation for User Story 1

- [X] T011 [US1] Implement `evaluateGate` (the F030-composing relabel), `evaluate` (the roll-up + the private ordinal
  sort comparator), `entries`, `isReusable`, `reusableEvidence`, `recomputeCause` in `CacheEligibility.fs` per
  contracts/cache-eligibility-api.md and data-model.md — `evaluateGate candidate store = match EvidenceReuse.decide
  candidate.Inputs store with Reuse ref -> Reusable ref | Recompute cause -> MustRecompute cause` (L-G1..L-G5, the verbatim
  compose, no new policy); `evaluate candidates store = candidates |> List.map (fun c -> { Gate = c.Gate; Verdict =
  evaluateGate c store }) |> List.sortWith (fun a b -> match String.CompareOrdinal (gateIdValue a.Gate, gateIdValue
  b.Gate) with 0 -> compare a b | n -> n) |> CacheEligibilityReport` (L-E1..L-E6 — one attributed verdict per candidate,
  ordinal `GateId` order, structural duplicate-`GateId` tiebreak, empty-is-total, no key computed); `entries
  (CacheEligibilityReport xs) = xs` (L-P1); `isReusable` (`Reusable _ ⇒ true`; `MustRecompute _ ⇒ false`, L-P2);
  `reusableEvidence` (`Reusable ref ⇒ Some ref`; `MustRecompute _ ⇒ None`, L-P3); `recomputeCause` (`MustRecompute cause
  ⇒ Some cause`; `Reusable _ ⇒ None`, L-P4). Pure pattern matching + `List.map`/`List.sortWith` + `String.CompareOrdinal`
  + `FSharp.Core` only; no clock/filesystem/git/environment/network, no gate run, no evidence produced, no freshness
  key/hash computed, no input resolved, no JSON, no cache lookup against a real store, nothing persisted (FR-008/FR-011).
  This single total roll-up serves US1 (`MustRecompute` branches), US2 (the `Reusable` branch), and US3 (ordering +
  attribution). Run T010: green (recompute branches).

**Checkpoint**: US1 is functional — an unmatched candidate stays `MustRecompute` with a named cause, by construction.
The recompute-by-default safety posture holds. MVP reached for the default.

---

## Phase 4: User Story 2 — Reusable when prior evidence matches, naming the evidence (Priority: P2)

**Goal**: When a candidate's resolved freshness inputs exactly match a recorded entry, `evaluateGate` returns `Reusable`
carrying that entry's F030 `EvidenceRef` (not a bare boolean), with the same most-recent-wins choice F030 makes — no new
reuse, recency, or matching policy introduced here. `evaluateGate` is already whole from T011; this phase validates its
`Reusable` branch.

**Independent Test**: Record evidence for a candidate's freshness inputs, then evaluate a candidate with matching inputs
⇒ `Reusable` carrying the recorded `EvidenceRef`; with multiple recorded entries for the gate, the reference is the one
F030 chooses. No new policy, no I/O.

### Tests for User Story 2 (validate the `Reusable` branch of the completed `evaluateGate` from T011)

- [X] T012 [P] [US2] `tests/.../ReusableTests.fs` — (1) **exact match ⇒ reusable, naming the evidence** (SC-002, US2 #1,
  L-G5): `storeOf [ baseInputs, EvidenceRef "ev-A" ]` with a candidate whose `Inputs = baseInputs` ⇒ `evaluateGate
  candidate store = Reusable (EvidenceRef "ev-A")`, and `reusableEvidence (evaluateGate …) = Some (EvidenceRef "ev-A")` —
  assert the verdict carries the exact reference, not a bare flag; (2) **most-recent-wins, inherited from F030** (SC-002,
  US2 #2, L-G5): a store with multiple entries for the same inputs recorded under different `EvidenceRef`s ⇒ the verdict
  carries exactly the reference `EvidenceReuse.decide` returns (no new recency policy here) — assert equality against
  `EvidenceReuse.decide candidate.Inputs store` projected through the relabel; (3) **reusable-iff-F030-reuse property**
  (SC-002, FR-004, L-G1): an FsCheck property that `isReusable (evaluateGate c s)` ⟺ `EvidenceReuse.decide c.Inputs s` is
  a `Reuse _`, and that when it is, `reusableEvidence (evaluateGate c s)` carries that same `EvidenceRef`.

**Checkpoint**: US1 + US2 — the gate's two outcomes are both proven: recompute by default naming the cause, and reusable
on an exact match naming the F030 evidence reference, with no new reuse policy introduced. Full per-gate verdict
functional.

---

## Phase 5: User Story 3 — One attributable verdict per gate, deterministic and total (Priority: P3)

**Goal**: `evaluate` returns exactly one verdict per supplied candidate, each attributed to its `GateId`, ordered by
`gateIdValue` ordinal with a structural tiebreak so the order is independent of supply order; every gate is preserved
(none dropped, merged, or duplicated, duplicates kept); the function is total over the full cross-product of candidate
counts and store states and never throws; identical inputs yield a byte-identical report under changed
cwd/clock/filesystem; and a `Reusable` verdict is necessary-not-sufficient. Builds on US1–US2, so P3.

**Independent Test**: Evaluate candidate gates supplied in arbitrary order ⇒ exactly one verdict per gate, ordered by
gate identity, every gate preserved; re-evaluate identical inputs under a changed working directory / clock / filesystem
⇒ byte-identical report; inspect a `Reusable` value ⇒ it carries no skip action / severity / ship verdict / exit-code
basis.

### Tests for User Story 3 (validate attribution/order/totality/determinism/necessary-not-sufficient over the completed `evaluate` from T011)

- [X] T013 [P] [US3] `tests/.../AttributionAndOrderTests.fs` — (SC-006, US3 #1/#2, L-E1..L-E4): (1) **one attributed
  verdict per candidate** — `entries (evaluate candidates store)` has exactly `List.length candidates` items, each `{
  Gate; Verdict }` with `Gate = c.Gate` and `Verdict = evaluateGate c store` for its originating candidate; no candidate
  dropped, merged, or silently duplicated (an FsCheck property over arbitrary candidate lists); (2) **deterministic
  ordinal order** — the worked example (`z:a`, `a:b`, `a:a` ⇒ `a:a`, `a:b`, `z:a`) plus an FsCheck property that
  `entries` is sorted by `String.CompareOrdinal` on `gateIdValue`; (3) **order-independence** (L-E3) — for any
  permutation of the candidate list, `evaluate (perm candidates) store = evaluate candidates store` (byte-identical
  report); (4) **duplicates kept** (L-E4) — two candidates with the same `GateId` but different `Inputs` ⇒ **two** entries
  under that gate, ordered by the structural tiebreak on the entry's `Verdict` (the entry carries no `Inputs`),
  deterministic for any supply order; include the sub-case where the differing `Inputs` yield **equal** `Verdict`s (e.g.
  both `MustRecompute NoPriorEvidence`) ⇒ the two entries are byte-identical, so the report is byte-identical for any
  supply order; (5) **empty is total** (L-E5) —
  `evaluate [] store = CacheEligibilityReport []` and `entries` of it is `[]`.
- [X] T014 [P] [US3] `tests/.../TotalityTests.fs` — (SC-004, US3, L-T1): an FsCheck property over the full cross-product
  of arbitrary `CandidateGate` lists (incl. `[]`, singletons, and lists with duplicate `GateId`s) and arbitrary
  `ReuseStore` states (empty, matching, non-matching) asserts `evaluate` returns a well-formed `CacheEligibilityReport`
  and `evaluateGate` a `CacheEligibilityVerdict`, and neither throws; every combination is an ordinary named
  report/verdict (the Edge Cases — no candidates, one candidate, duplicate gate ids, empty/matching/non-matching store).
- [X] T015 [P] [US3] `tests/.../DeterminismTests.fs` — (SC-005, US3 #3, L-T2/L-E6): `evaluate c s = evaluate c s` and
  `evaluateGate g s = evaluateGate g s` for example + FsCheck-generated inputs; and a purity check mirroring the
  EvidenceReuse/Calibration precedent — the report is structurally identical when computed in different working
  directories, at different times, and with unrelated repository / filesystem state changed between calls; no gate run,
  no cache lookup against a real store, no clock/filesystem/git/environment/network read, no bytes hashed, no freshness
  key computed, none of the supplied inputs resolved, nothing persisted.
- [X] T016 [P] [US3] `tests/.../NecessaryNotSufficientTests.fs` — (SC-007, FR-010, L-G6): the necessary-not-sufficient
  negatives are **by construction**, not fail-then-pass behavioral tests. That `CacheEligibilityVerdict` exposes no skip
  action, no `Severity`, no ship verdict, and no exit-code basis is proven by an **exhaustive pattern match that
  compiles** (every value is `Reusable _` or `MustRecompute _` and nothing more) **plus** the SurfaceDrift +
  reference-graph guard (T017), which together pin that no such member or dependency exists; name the test so it reads as
  the structural/by-construction check it is. The genuine **value-level, fail-then-pass** assertions in this file are the
  no-hide rule and FR-001 at the value level: every `Reusable` produced by `evaluate`/`evaluateGate` yields
  `reusableEvidence _ = Some _` and `recomputeCause _ = None`; every `MustRecompute` yields `recomputeCause _ = Some _`
  and `reusableEvidence _ = None`; and `isReusable` agrees with both — so no verdict is an opaque yes/no, and a
  `MustRecompute` always names its cause.

**Checkpoint**: US1 + US2 + US3 — the roll-up is total over all inputs, deterministic/pure, names its outcome per gate,
attributes one verdict per candidate in deterministic ordinal order with duplicates kept, and is honestly scoped (a
`Reusable` carries no enforcement meaning). All success criteria SC-001..SC-007 are pinned.

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope hygiene)

**Purpose**: Lock the public surface (Principle II) and prove the assembly's reference graph stays minimal (SC-008).
Bless the baseline only after the surface is final.

- [X] T017 `tests/.../SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the F029–F040 precedent): enumerate the
  public surface of `FS.GG.Governance.CacheEligibility` and compare byte-for-byte to
  `surface/FS.GG.Governance.CacheEligibility.surface.txt`, with the `BLESS_SURFACE=1` re-bless path; plus a
  **scope-hygiene** assertion (contracts/cache-eligibility-api.md scope guard, SC-008) that the assembly references
  **only** `FSharp.Core`, `FS.GG.Governance.EvidenceReuse`, `FS.GG.Governance.Gates`, and — transitively —
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config` — plus the BCL — and **not** `Snapshot`, `Route`/`Routing`,
  `Findings`, `Enforcement`, `Ship`, `AuditJson`, `RouteJson`, any `Adapters.*`, `Host`, or `Cli`. **Note**: FR-008's
  *behavioral* negatives (no gate run, no evidence produced, no freshness key/hash computed, no input resolved, no JSON
  projection, no real-store cache lookup, no persistence, no exit-code mapping, no CLI, no skip action, no severity) are
  satisfied **by construction** — the surface holds only the four types + `evaluate` / `evaluateGate` / `entries` /
  `isReusable` / `reusableEvidence` / `recomputeCause` (no such operation exists to call) — and are guarded by this
  reference-graph + surface-drift check, not by a positive behavioral test. **FR-009** (the supplied `GateId` /
  `FreshnessInputs` / `EvidenceRef` are opaque facts — never resolved, fabricated, re-hashed, parsed, or dereferenced,
  and no evidence is produced) is likewise **construction-guarded** here: the surface exposes no resolver/hasher/
  parser/producer, and the only inputs flow in through `CandidateGate` / `ReuseStore`, so this baseline + the absence of
  any such member pin FR-009 with no separate behavioral test.
- [X] T018 Generate and commit `surface/FS.GG.Governance.CacheEligibility.surface.txt` via `BLESS_SURFACE=1 dotnet test
  tests/FS.GG.Governance.CacheEligibility.Tests/...`; review the diff (exactly the two public modules — the `Model` types
  `CandidateGate` / `CacheEligibilityVerdict` / `CacheEligibilityEntry` / `CacheEligibilityReport`, and `evaluate` /
  `evaluateGate` / `entries` / `isReusable` / `reusableEvidence` / `recomputeCause`; no sort-comparator-helper leak) and
  commit it as part of the Tier-1 change. After this, T017 runs green without `BLESS_SURFACE`.
- [X] T019 [P] Update `CLAUDE.md`'s SPECKIT plan reference to point at `specs/041-cache-eligibility-verdict/plan.md`
  (the active pointer). No other doc changes.
- [X] T020 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`, `dotnet fsi
  scripts/prelude.fsx` (the F041 section prints the expected no-prior-evidence / inputs-changed / reusable / ordering /
  duplicate-gate / empty-report results), and `dotnet test tests/FS.GG.Governance.CacheEligibility.Tests/...` — all green
  under `TreatWarningsAsErrors`. Confirm `dotnet test FS.GG.Governance.sln` over the existing projects is unchanged (no
  existing baseline rewritten, no existing test changes outcome — SC-008).

**Checkpoint**: Tier-1 surface is blessed and guarded; the assembly's reference graph is minimal (EvidenceReuse + Gates +
transitive cores only); the full solution builds and tests green; existing cores untouched. **The route/audit emission
row's deferred cache-eligibility line is closed.**

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` surface, FSI proof, and compiling
  stubs must exist before any story test can be written and FAIL.
- **Phase 3 (US1)**: depends on Phase 2. The MVP (recompute-by-default). T011 implements the **whole** total roll-up
  (`evaluateGate`'s `MustRecompute` *and* `Reusable` branches, plus `evaluate`'s map+sort), since the functions must be
  complete to be total.
- **Phase 4 (US2)**: depends on Phase 2 + T011; its test (T012) validates the **`Reusable`** branch of the same
  `evaluateGate` implemented in T011 — there is no separate US2 implementation task; the single total function serves
  both stories. Sequenced after US1 because the `Reusable` branch is the complement of the recompute default.
- **Phase 5 (US3)**: depends on Phase 2 + T011 (asserts attribution/order/totality/determinism/necessary-not-sufficient
  of the finished `evaluate`/`evaluateGate`); P3.
- **Phase 6 (surface/polish)**: last — bless the baseline only after the surface is final (Phase 2 `.fsi` unchanged
  through implementation).

### Within each story

- Tests are written FIRST and must FAIL against the Phase-2 stubs (US1, T010), then pass after T011. US2/US3 tests pass
  against the complete `evaluate`/`evaluateGate` once T011 lands.
- `Model` type definitions precede the `CacheEligibility` operation bodies that consume them.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) is independent of T001 (library `.fsproj`); T003 (sln) needs both.
- **Phase 2**: T007 `[P]` (prelude FSI section) is independent of the `.fsi`/stub work once the DLL name is fixed by
  T001. T004/T005 (the two `.fsi` files) can be drafted together; T006 needs both; T008/T009 need the compiling stub.
- **Story test files are all `[P]`** relative to each other (distinct files): T010, T012, T013, T014, T015, T016 touch
  different test files. They share `Support.fs` (T008) as a prerequisite.
- **Phase 6**: T019 `[P]` (CLAUDE.md) is independent of the surface test; T017→T018→T020 are sequential.

---

## Task count per user story

- **Setup (Phase 1)**: 3 tasks (T001–T003).
- **Foundational (Phase 2)**: 6 tasks (T004–T009).
- **US1 (Phase 3)**: 2 tasks (T010 test, T011 impl) 🎯 MVP.
- **US2 (Phase 4)**: 1 task (T012 test; impl shared with T011).
- **US3 (Phase 5)**: 4 tasks (T013–T016 tests; attribution/order + totality + determinism + necessary-not-sufficient/
  no-hide, mostly by construction over the finished `evaluate`).
- **Surface & polish (Phase 6)**: 4 tasks (T017–T020).
- **Total**: 20 tasks.

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)** — the project skeleton, the `.fsi` surface + FSI proof, and the total roll-up
proven on the recompute-by-default path: an unmatched or stale candidate stays `MustRecompute` naming its cause, never a
falsely-claimed reuse. This is the spec's P1 safety slice and the minimum that demonstrates the row's reason to exist.
Phase 4 (US2, P2) completes the second outcome (reusable-on-exact-match naming the F030 evidence) over the same finished
function; Phase 5 (US3, P3) pins one-verdict-per-gate attribution, deterministic ordinal order, totality, determinism,
and necessary-not-sufficient scope; Phase 6 locks the Tier-1 surface and reference-graph hygiene.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify US1's tests FAIL against the Phase-2 stubs before implementing T011, then pass after; US2/US3 tests pass once
  T011 lands.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- Commit after each task or logical group; keep existing `src/`, `surface/`, and merged test projects untouched
  (SC-008). F018 `GateId`, F029 `FreshnessInputs`/`InputCategory`, and F030 `decide`/`ReuseStore`/`RecomputeCause`/
  `EvidenceRef` are consumed verbatim, never modified (FR-012/FR-014); F030 `decide` is composed verbatim with no new
  reuse policy (FR-004).
