---
description: "Task list for 039-advisory-promotion-gate implementation"
---

# Tasks: Advisory-to-Blocking Promotion Gate — the Single-Sample-Noise Guardrail

**Feature branch**: `039-advisory-promotion-gate`
**Spec**: `specs/039-advisory-promotion-gate/spec.md`
**Plan**: `specs/039-advisory-promotion-gate/plan.md`

**Input**: Design documents from `/specs/039-advisory-promotion-gate/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/advisory-promotion-api.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the spec is
itself an advisory-by-default / three-basis / inclusive-comparator / no-hide / totality / determinism contract — the
tests *are* the deliverable's proof. Every value is a real, literally-constructible typed token (a real F030
`EvidenceRef`, literal counts/thresholds, a literal `SignOff`); no mock, no clock read, no model invoked, no file
read, no bytes hashed.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`) for
traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no new
third-party dependency). No per-task tier annotations needed — all tasks share the feature tier.

**Elmish/MVU**: **Not applicable** — a pure, total, deterministic decision over supplied values; no state, no I/O, no
workflow (plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks. The
*actual* review, sensing whether deterministic evidence exists, counting independent reviews, and capturing a human
sign-off are a later host edge, out of scope.

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

- [X] T001 Create `src/FS.GG.Governance.AdvisoryPromotion/FS.GG.Governance.AdvisoryPromotion.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.AdvisoryPromotion`, `Version` `0.1.0`, `IsPackable=true` (override
  `Directory.Build.props` like ReviewRecord/PromptIsolation/AgentReviewKey). `<Compile>` order: `Model.fsi`,
  `Model.fs`, `AdvisoryPromotion.fsi`, `AdvisoryPromotion.fs`. **One** `<ProjectReference>` — to
  `../FS.GG.Governance.EvidenceReuse/FS.GG.Governance.EvidenceReuse.fsproj` (provides `EvidenceRef` directly; F029
  `FreshnessKey` / F014 `Config` arrive transitively but are unused) — the F036 single-sibling-reference shape (plan
  D1/D3, FR-009). **No third-party `PackageReference`** (FR-011). Add a header comment mirroring the EvidenceReuse
  `.fsproj`: pure total decision core; reuses F030 `EvidenceRef` verbatim for the backing-evidence basis; no
  Gates/Snapshot/Enforcement/Findings/VerdictReuse/ReviewRecord/host/CLI coupling.
- [X] T002 [P] Create `tests/FS.GG.Governance.AdvisoryPromotion.Tests/FS.GG.Governance.AdvisoryPromotion.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`, `FsCheck`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no new package);
  `<ProjectReference>`s to the new core and to `FS.GG.Governance.EvidenceReuse` (for real `EvidenceRef` literals).
  `<Compile>` order: `Support.fs`, `AdvisoryDefaultTests.fs`, `EligibilityTests.fs`, `ConfidenceComparatorTests.fs`,
  `TotalityTests.fs`, `DeterminismTests.fs`, `NecessaryNotSufficientTests.fs`, `NonEmptyEligibilityTests.fs`,
  `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh GUIDs and
  the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies so the
library and tests compile and tests can FAIL before implementation. **⚠️ No story work begins until this phase is
complete.**

- [X] T004 Write `src/FS.GG.Governance.AdvisoryPromotion/Model.fsi` — the SOLE public surface for the types
  (data-model.md): `open FS.GG.Governance.EvidenceReuse.Model` (brings `EvidenceRef`, reused verbatim); the closed
  three-case `PromotionBasis = DeterministicBackingEvidence | RepeatedReviewConfidence | HumanSignOff` (FR-002); the
  two confidence newtypes `ConfirmationCount of int` and `ConfidenceThreshold of int` (FR-004); the opaque
  `SignOff of string` (FR-002); the `AdvisoryReason = NoPermittedBasis | ConfidenceBelowThreshold of ConfirmationCount
  * ConfidenceThreshold` (FR-005); the `PromotionFacts` record (`{ BackingEvidence: EvidenceRef option; Confirmations:
  ConfirmationCount; ConfidenceThreshold: ConfidenceThreshold; SignOff: SignOff option }`); and the `PromotionDecision
  = StaysAdvisory of AdvisoryReason | EligibleToBlock of PromotionBasis * PromotionBasis list` (the head+tail encoding
  makes an empty-basis promotion unrepresentable — FR-001/L-D7). Curated doc comments in the EvidenceReuse/ReviewRecord
  `.fsi` style: `EvidenceRef`/`SignOff` are opaque supplied tokens (no validation, no parsing, no dereferencing; an
  empty string is a literal value); the model's own self-confidence is **not** a basis (FR-002); the finding/verdict is
  **not** a field — the verdict is an opaque fact this core never produces/interprets/re-scores (FR-007). No access
  modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.AdvisoryPromotion/AdvisoryPromotion.fsi` — the SOLE public surface for the
  operations (contracts/advisory-promotion-api.md): `val decide: facts: PromotionFacts -> PromotionDecision`; `val
  satisfiedBases: decision: PromotionDecision -> PromotionBasis list`; `val signOffValue: signOff: SignOff -> string`;
  `val confirmationValue: count: ConfirmationCount -> int`; `val thresholdValue: threshold: ConfidenceThreshold ->
  int`. Doc comments state purity/totality/determinism and the laws (`decide` is advisory-by-default and eligible iff
  ≥1 basis, naming every satisfied basis in the fixed order *DeterministicBackingEvidence, RepeatedReviewConfidence,
  HumanSignOff*, L-D1..L-D13; `satisfiedBases` is `[]` for advisory and the full non-empty list for eligible,
  L-S1/L-S2; the unwrappers L-U1..L-U3; reads no clock/filesystem/git/environment/network, invokes no model, hashes no
  bytes, runs no review, makes no cache/verdict-store/lookup/invalidation, builds no review record). The `confidenceMet`
  helper is **absent** from the `.fsi` (private by omission, Principle II).
- [X] T006 Add `src/FS.GG.Governance.AdvisoryPromotion/Model.fs` and
  `src/FS.GG.Governance.AdvisoryPromotion/AdvisoryPromotion.fs` — real type definitions in `Model.fs` (the union, the
  three newtypes, the two records are plain data, define them fully); `decide`, `satisfiedBases`, `signOffValue`,
  `confirmationValue`, `thresholdValue` as `failwith "not implemented"` stubs in `AdvisoryPromotion.fs`. No
  `private`/`internal`/`public` modifiers (Principle II). Confirm `dotnet build
  src/FS.GG.Governance.AdvisoryPromotion/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F039 design-first section to `scripts/prelude.fsx` — `#r` the new Debug DLL plus the
  EvidenceReuse DLL; `open FS.GG.Governance.EvidenceReuse.Model`, `open FS.GG.Governance.AdvisoryPromotion.Model`, and
  `open FS.GG.Governance.AdvisoryPromotion`; construct the quickstart.md / contract worked examples as literal
  `PromotionFacts` and `printfn` the intended `decide` results: bare finding (`None`/`ConfirmationCount 0`/
  `ConfidenceThreshold 3`/`None`) ⇒ `StaysAdvisory NoPermittedBasis`; 2-of-3 confirmations ⇒ `StaysAdvisory
  (ConfidenceBelowThreshold (ConfirmationCount 2, ConfidenceThreshold 3))`; `Some (EvidenceRef "e")` ⇒ `EligibleToBlock
  (DeterministicBackingEvidence, [])`; 3-of-3 ⇒ `EligibleToBlock (RepeatedReviewConfidence, [])`; `Some (SignOff "u")`
  ⇒ `EligibleToBlock (HumanSignOff, [])`; all three ⇒ `EligibleToBlock (DeterministicBackingEvidence,
  [RepeatedReviewConfidence; HumanSignOff])`; lone review (`ConfirmationCount 1`/`ConfidenceThreshold 1`) ⇒
  `StaysAdvisory (ConfidenceBelowThreshold …)` (the no-single-sample floor). This is the Principle-I FSI proof; it
  documents the shape even while bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.AdvisoryPromotion.Tests/Support.fs` — real, literally-constructible builders
  (Principle V, no mocks): `facts` helpers that assemble a `PromotionFacts` from supplied levers; literal
  `EvidenceRef`/`SignOff`/`ConfirmationCount`/`ConfidenceThreshold` builders; the seven worked-example records from
  contracts/advisory-promotion-api.md with their expected `decide` results, for example-test oracles; FsCheck
  generators for `EvidenceRef option`, `SignOff option`, `ConfirmationCount` and `ConfidenceThreshold` over the full
  non-negative **and negative** int range (totality), `SignOff` strings (incl. empty + multi-byte), and a full
  `PromotionFacts`; and the `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from the
  EvidenceReuse/ReviewRecord `Support.fs` precedent. No I/O beyond repo-root resolution.
- [X] T009 Write `tests/FS.GG.Governance.AdvisoryPromotion.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now FAILS only
because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — An agent-reviewed finding stays advisory by default (Priority: P1) 🎯 MVP

**Goal**: With no permitted basis satisfied, `decide` returns `StaysAdvisory` carrying its reason — `NoPermittedBasis`
when nothing was attempted, `ConfidenceBelowThreshold (count, threshold)` when a review was attempted but fell short —
and the model's own self-reported confidence never promotes. This is the heart of the design's single-sample-noise
constraint and the phase's exit criterion: a bare agent-reviewed finding is **never** eligible to block.

**Independent Test**: Supply a `PromotionFacts` with no backing evidence, confirmations below threshold or absent, and
no sign-off; assert `decide` is `StaysAdvisory` with the reason naming no permitted basis (or confidence-below-
threshold). No model invoked, no I/O.

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../AdvisoryDefaultTests.fs` — (1) **bare default** (SC-001, US1 #1, L-D3): no basis +
  `ConfirmationCount 0` ⇒ `decide = StaysAdvisory NoPermittedBasis`; (2) **attempted-but-insufficient** (SC-001, US1
  #2, L-D2): a confirmation count `c` with `1 <= c < t` (or `c = 1` for any `t`) and no other basis ⇒ `StaysAdvisory
  (ConfidenceBelowThreshold (ConfirmationCount c, ConfidenceThreshold t))` — an insufficient count is not a basis; (3)
  **self-confidence never promotes** (SC-001, US1 #3, L-D9): there is no field by which the model's own confidence
  enters `PromotionFacts`, so any facts whose only would-be justification is model confidence (i.e. none of the three
  bases present) ⇒ `StaysAdvisory`; (4) **advisory-by-default property** (SC-001, L-D4): an FsCheck property that
  whenever none of the three bases is satisfied (no `Some` evidence, `not (c >= t && c >= 2)`, no `Some` sign-off),
  `decide` is `StaysAdvisory _` — never `EligibleToBlock`.

### Implementation for User Story 1

- [X] T011 [US1] Implement `decide` (and the private `confidenceMet` helper), `satisfiedBases`, `signOffValue`,
  `confirmationValue`, `thresholdValue` in `AdvisoryPromotion.fs` per contracts/advisory-promotion-api.md and
  data-model.md — build the satisfied-basis list in the fixed order via a `[ … ]` comprehension
  (`DeterministicBackingEvidence` when `facts.BackingEvidence = Some _`; `RepeatedReviewConfidence` when `confidenceMet
  facts`, i.e. `c >= t && c >= 2` where `ConfirmationCount c = facts.Confirmations`, `ConfidenceThreshold t =
  facts.ConfidenceThreshold`; `HumanSignOff` when `facts.SignOff = Some _`); then `match` it — `b :: rest ⇒
  EligibleToBlock (b, rest)`; `[] ⇒ StaysAdvisory (if c >= 1 then ConfidenceBelowThreshold (facts.Confirmations,
  facts.ConfidenceThreshold) else NoPermittedBasis)` (L-D1/L-D2/L-D3). `satisfiedBases` projects (`StaysAdvisory _ ⇒
  []`; `EligibleToBlock (b, rest) ⇒ b :: rest`, L-S1/L-S2). The three unwrappers pattern-match their newtype
  (L-U1/L-U2/L-U3). Pure pattern matching + `FSharp.Core` only; no clock/filesystem/git/environment/network, no model,
  no byte hashing, no review, no cache/verdict operation (L-D11/L-D12, FR-006). This single total function serves both
  US1 (advisory branch) and US2 (eligible branch). Run T010: green (advisory branch).

**Checkpoint**: US1 is functional — a bare or insufficiently-corroborated agent-reviewed finding stays advisory with a
named reason, by construction. The design's advisory-by-default safety posture holds. MVP reached for the default.

---

## Phase 4: User Story 2 — A finding becomes eligible to block on a permitted basis, and the basis is named (Priority: P1)

**Goal**: When ≥1 of the three permitted bases is satisfied, `decide` returns `EligibleToBlock` naming **every**
satisfied basis (the no-hide rule) in the fixed order — one basis names just it; two or three name all of them. The
repeated-review confidence basis is satisfied exactly at the inclusive `c >= t` floor with `c >= 2` (a lone review
never clears it). Co-P1 with US1 — together they fix the gate's two outcomes. `decide` is already whole from T011; this
phase validates its eligible branch.

**Independent Test**: Supply a `PromotionFacts` with exactly one permitted basis satisfied ⇒ `EligibleToBlock` naming
that basis; supply two or three ⇒ `EligibleToBlock` naming all of them in fixed order; confirm `decide` is a
deterministic function of the supplied facts.

### Tests for User Story 2 (validate the eligible branch of the completed `decide` from T011)

- [X] T012 [P] [US2] `tests/.../EligibilityTests.fs` — (1) **one basis, named** (SC-002, US2 #1–#3, L-D5/L-D6):
  exactly `Some` backing evidence ⇒ `EligibleToBlock (DeterministicBackingEvidence, [])`; exactly `c >= t && c >= 2`
  ⇒ `EligibleToBlock (RepeatedReviewConfidence, [])`; exactly `Some` sign-off ⇒ `EligibleToBlock (HumanSignOff, [])`;
  (2) **two or three bases, all named in fixed order** (SC-002, US2 #4, L-D6): the worked example `Some (EvidenceRef
  "e")` + `5`/`3` + `Some (SignOff "u")` ⇒ `EligibleToBlock (DeterministicBackingEvidence, [RepeatedReviewConfidence;
  HumanSignOff])`; the three pairwise combinations and the all-three case each name their satisfied bases in the order
  *DeterministicBackingEvidence, RepeatedReviewConfidence, HumanSignOff*; (3) **all-named property** (SC-002, L-D6): an
  FsCheck property that `satisfiedBases (decide facts)` equals exactly the bases satisfied by `facts`, in fixed order;
  (4) **eligible iff a basis** (L-D5): `decide facts` is `EligibleToBlock _` iff at least one basis is satisfied.
- [X] T013 [P] [US2] `tests/.../ConfidenceComparatorTests.fs` — (SC-003, FR-004, L-D8): the repeated-review confidence
  basis (no other basis present) is satisfied **exactly** when `c >= t && c >= 2`, verified across `c < t`, `c = t`,
  and `c > t`; a lone review (`c = 1`) never satisfies it for any `t` (incl. `t = 1` — the no-single-sample floor: the
  contract worked example `None`/`1`/`1`/`None` ⇒ `StaysAdvisory (ConfidenceBelowThreshold …)`, not eligible); `c = 0`
  / absent never satisfies it; for `t >= 2` the floor is invisible (the basis is exactly `c >= t`). An FsCheck property
  over `c`/`t` straddling the threshold (incl. negative/degenerate values) confirms `RepeatedReviewConfidence ∈
  satisfiedBases (decide facts)` ⟺ `c >= t && c >= 2`.

**Checkpoint**: US1 + US2 — the gate's two outcomes are both proven: advisory by default, and eligible-on-a-permitted-
basis naming every satisfied basis, with the inclusive no-single-sample confidence comparator. Full decision core
functional.

---

## Phase 5: User Story 3 — The decision is total, deterministic, and never blocks on its own (Priority: P2)

**Goal**: `decide` is defined for every `PromotionFacts` (any `EvidenceRef option`, any int count/threshold incl.
zero/negative, any `SignOff option`), never throws, never reads a clock/file/model; identical facts ⇒ identical
decision; and an `EligibleToBlock` decision is necessary-not-sufficient — it carries no blocking action and no
calibration claim. Also pins FR-001: `EligibleToBlock` is unrepresentable with an empty basis set. Builds on US1–US2,
so P2.

**Independent Test**: Exercise `decide` across the full cross-product of basis presence/absence and counts straddling
the threshold ⇒ always returns a decision, never throws; call it twice on identical facts ⇒ equal results; inspect an
`EligibleToBlock` value ⇒ it carries only promotion eligibility.

### Tests for User Story 3 (validate totality/determinism/non-empty over the completed `decide` from T011)

- [X] T014 [P] [US3] `tests/.../TotalityTests.fs` — (SC-004, US3 #1, L-D11): an FsCheck property over the full
  cross-product of `BackingEvidence` presence/absence, `SignOff` presence/absence, and arbitrary `ConfirmationCount` /
  `ConfidenceThreshold` ints (including `0`, negatives, and `Int32.Min/MaxValue`) asserts `decide` returns a
  `PromotionDecision` and never throws; every combination is an ordinary named decision (the Edge Cases).
- [X] T015 [P] [US3] `tests/.../DeterminismTests.fs` — (SC-005, US3 #2, L-D12): `decide facts = decide facts` for
  example + FsCheck-generated facts; and a purity check mirroring the EvidenceReuse/ReviewRecord precedent — the
  decision is byte-for-byte / structurally identical when computed in different working directories, at different
  times, and with unrelated repository / filesystem state changed between calls; no model invoked, no
  clock/filesystem/git/environment/network read, no bytes hashed, nothing persisted.
- [X] T016 [P] [US3] `tests/.../NecessaryNotSufficientTests.fs` — (SC-006, US3 #3, L-D13): an `EligibleToBlock`
  decision carries **only** the satisfied `PromotionBasis` set — assert by exhaustive pattern match that
  `PromotionDecision` exposes no blocking action, no `Severity`, no enforcement verdict, and no calibration claim; the
  type is the eligibility verdict and nothing more (necessary, not sufficient — calibration is the sixth row).
- [X] T017 [P] [US3] `tests/.../NonEmptyEligibilityTests.fs` — (FR-001, L-D7): `EligibleToBlock (b, rest)` always
  carries the head `b`, so `satisfiedBases` of any eligible decision is non-empty; assert (a documented compile-time
  guarantee — the head+tail encoding makes an empty-basis promotion unrepresentable, there is no constructor for it)
  plus a value-level FsCheck check that every `EligibleToBlock` produced by `decide` has `satisfiedBases <> []` and
  every `StaysAdvisory` has `satisfiedBases = []` (L-S1/L-S2).

**Checkpoint**: US1 + US2 + US3 — the decision is total over all inputs, deterministic/pure, names every basis, and is
honestly scoped as eligibility (not blocking). All success criteria SC-001..SC-006 are pinned.

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope hygiene)

**Purpose**: Lock the public surface (Principle II) and prove the assembly's reference graph stays minimal (SC-007).
Bless the baseline only after the surface is final.

- [X] T018 `tests/.../SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the F029–F038 precedent): enumerate
  the public surface of `FS.GG.Governance.AdvisoryPromotion` and compare byte-for-byte to
  `surface/FS.GG.Governance.AdvisoryPromotion.surface.txt`, with the `BLESS_SURFACE=1` re-bless path; plus a
  **scope-hygiene** assertion (contracts/advisory-promotion-api.md scope guard, SC-007) that the assembly references
  **only** `FSharp.Core`, `FS.GG.Governance.EvidenceReuse`, and — transitively — `FS.GG.Governance.FreshnessKey` and
  `FS.GG.Governance.Config` (unused here), plus the BCL — and **not** `Gates`, `Snapshot`, `Route`/`Routing`,
  `Findings`, `Enforcement`, `VerdictReuse`, `ReviewRecord`, `PromptIsolation`, `AgentReviewKey`, any `Adapters.*`,
  `Host`, `Cli`, `Ship`, or `AuditJson`. **Note**: FR-007/FR-008's *behavioral* negatives (no verdict
  produce/interpret/re-score/threshold, no cache key / verdict store / lookup / invalidation, no review record build,
  no model invocation, no byte hashing, no persistence, no JSON projection, no CLI, no blocking action, no
  calibration) are satisfied **by construction** — the surface holds only the seven types + `decide`/`satisfiedBases`/
  the three unwrappers (no such operation exists to call) — and are guarded by this reference-graph + surface-drift
  check, not by a positive behavioral test.
- [X] T019 Generate and commit `surface/FS.GG.Governance.AdvisoryPromotion.surface.txt` via `BLESS_SURFACE=1 dotnet
  test tests/FS.GG.Governance.AdvisoryPromotion.Tests/...`; review the diff (exactly the two public modules — the
  `Model` types `PromotionBasis`/`ConfirmationCount`/`ConfidenceThreshold`/`SignOff`/`AdvisoryReason`/`PromotionFacts`/
  `PromotionDecision`, and `decide`/`satisfiedBases`/`signOffValue`/`confirmationValue`/`thresholdValue`; no
  `confidenceMet` helper leak) and commit it as part of the Tier-1 change. After this, T018 runs green without
  `BLESS_SURFACE`.
- [X] T020 [P] Update `CLAUDE.md` — confirm the SPECKIT plan reference points at
  `specs/039-advisory-promotion-gate/plan.md` (already the active pointer). No other doc changes.
- [X] T021 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`, `dotnet fsi
  scripts/prelude.fsx` (the F039 section prints the expected advisory-default / per-basis-eligible / all-three /
  no-single-sample-floor results), and `dotnet test tests/FS.GG.Governance.AdvisoryPromotion.Tests/...` — all green
  under `TreatWarningsAsErrors`. Confirm `dotnet test FS.GG.Governance.sln` over the existing projects is unchanged (no
  existing baseline rewritten, no existing test changes outcome — SC-007).

**Checkpoint**: Tier-1 surface is blessed and guarded; the assembly's reference graph is minimal (EvidenceReuse +
transitive cores only); the full solution builds and tests green; existing cores untouched.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` surface, FSI proof, and
  compiling stubs must exist before any story test can be written and FAIL.
- **Phase 3 (US1)**: depends on Phase 2. The MVP (advisory-by-default). T011 implements the **whole** total `decide`
  (advisory *and* eligible branches), since the function must be complete to be total.
- **Phase 4 (US2)**: depends on Phase 2; co-P1 with US1. Its tests (T012/T013) validate the **eligible** branch of the
  same `decide` implemented in T011 — there is no separate US2 implementation task; the single total function serves
  both stories. Sequenced after US1 because the eligible branch is the complement of the advisory default.
- **Phase 5 (US3)**: depends on Phase 2 + T011 (asserts totality/determinism/necessary-not-sufficient/non-empty of the
  finished `decide`); P2.
- **Phase 6 (surface/polish)**: last — bless the baseline only after the surface is final (Phase 2 `.fsi` unchanged
  through implementation).

### Within each story

- Tests are written FIRST and must FAIL against the Phase-2 stubs (US1, T010), then pass after T011. US2/US3 tests
  pass against the complete `decide` once T011 lands.
- `Model` type definitions precede the `AdvisoryPromotion` operation bodies that consume them.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) is independent of T001 (library `.fsproj`); T003 (sln) needs both.
- **Phase 2**: T007 `[P]` (prelude FSI section) is independent of the `.fsi`/stub work once the DLL name is fixed by
  T001. T004/T005 (the two `.fsi` files) can be drafted together; T006 needs both; T008/T009 need the compiling stub.
- **Story test files are all `[P]`** relative to each other (distinct files): T010, T012, T013, T014, T015, T016, T017
  touch different test files. They share `Support.fs` (T008) as a prerequisite.
- **Phase 6**: T020 `[P]` (CLAUDE.md) is independent of the surface test; T018→T019→T021 are sequential.

---

## Task count per user story

- **Setup (Phase 1)**: 3 tasks (T001–T003).
- **Foundational (Phase 2)**: 6 tasks (T004–T009).
- **US1 (Phase 3)**: 2 tasks (T010 test, T011 impl) 🎯 MVP.
- **US2 (Phase 4)**: 2 tasks (T012/T013 tests; impl shared with T011).
- **US3 (Phase 5)**: 4 tasks (T014–T017 tests; totality/determinism/necessary-not-sufficient/non-empty by
  construction).
- **Surface & polish (Phase 6)**: 4 tasks (T018–T021).
- **Total**: 21 tasks.

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2)** — the project skeleton, the `.fsi` surface + FSI proof, and the
single total `decide` proven on both outcomes. US1 and US2 are **co-P1** (the spec: together they fix the gate's two
outcomes — advisory by default, eligible on a named permitted basis), so the smallest honest MVP delivers both: a bare
agent-reviewed finding that stays advisory *and* a corroborated one that becomes eligible-to-block naming exactly which
permitted bases authorized it. US3 (totality/determinism/necessary-not-sufficient/non-empty, P2) pins the trust
guarantees over the same finished function; Phase 6 locks the Tier-1 surface and reference-graph hygiene.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify US1's tests FAIL against the Phase-2 stubs before implementing T011, then pass after; US2/US3 tests pass once
  T011 lands.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- Commit after each task or logical group; keep existing `src/`, `surface/`, and merged test projects untouched
  (SC-007). F030 `EvidenceRef` is consumed verbatim, never modified (FR-009).
