# Tasks: Kernel Core — Facts, Rules, Fixed-Point Derivation, Provenance

**Feature**: F01 · `001-kernel-core` | **Tier 1**

**Input**: Design documents in `/specs/001-kernel-core/`
([spec.md](./spec.md), [plan.md](./plan.md), [research.md](./research.md),
[data-model.md](./data-model.md), [quickstart.md](./quickstart.md),
[contracts/Kernel.fsi](./contracts/Kernel.fsi))

**Tests**: REQUESTED. The spec (SC-001..SC-005, FR-011), plan (Constitution
Principles I & V), and quickstart (V1–V12) mandate semantic tests against the
public surface, an FsCheck order-independence property, and a reflective
surface-drift baseline. Test tasks are therefore first-class and authored
**before** the matching implementation (Principle I).

**Elmish/MVU**: **N/A** for this feature. The kernel is a pure reasoner — no
state machine, I/O, retries, or user interaction (plan Constitution Check
Principle IV). No `Model`/`Msg`/`Effect`/interpreter tasks are emitted; this is
recorded in the evidence-obligations task (T021).

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line)

## Format: `[ID] [P?] [Story] Description`

- **[P]** — no dependency on another incomplete task **in the same phase** (safe to parallelize)
- **[US1]/[US2]/[US3]** — owning user story (all three are **P1**); omitted for shared Setup/Foundational/Polish
- Tier annotation omitted throughout — every phase matches the feature's overall **Tier 1**
- Exact file paths are given in each task

> **Terminology (I1).** "supplied" and "asserted" name the same thing — facts given
> to the engine rather than derived. The spec uses both (deliberately, for the
> reader); the contract and data-model standardize on "supplied". Implementation
> code, identifiers, and test names SHOULD prefer **"supplied"** for consistency.

> **Shared-file note.** F01's entire implementation is the single pure function
> `FixedPoint.evaluate` in one file, `src/FS.GG.Governance.Kernel/Kernel.fs`.
> US1 delivers the working function (the MVP); US2 and US3 add provenance-tie-break
> and canonical-ordering refinements **to that same file** plus their own tests.
> Implementation tasks across US1→US2→US3 are therefore **sequential on `Kernel.fs`**
> (not `[P]` with each other), even though each story's *tests* are independent and
> `[P]`. This is called out per-task in Dependencies.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the repository scaffolding F01 introduces and every later
feature reuses (plan "Project Structure" + roadmap §5). No `Kernel.fs` body yet.

- [X] T001 Create the solution and source/test directory tree per plan: `FS.GG.Governance.sln` at repo root; `src/FS.GG.Governance.Kernel/` and `tests/FS.GG.Governance.Kernel.Tests/` directories; add both projects (created below) to the solution.
- [X] T002 [P] Create `Directory.Build.props` at repo root setting shared `net10.0` target, F# language settings, `TreatWarningsAsErrors`, and nullable/warning conventions for all projects.
- [X] T003 [P] Create `Directory.Packages.props` at repo root enabling central package management; pin **test-only** versions for Expecto and FsCheck (D5). No kernel package entries — the kernel stays BCL-only (FR-010, SC-005).
- [X] T004 Create `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj` — `net10.0`, **no `PackageReference`** of any kind, compile order `Kernel.fsi` then `Kernel.fs` (D7: no `IsPackable`/packing in F01). Depends on T002.
- [X] T005 Create `tests/FS.GG.Governance.Kernel.Tests/FS.GG.Governance.Kernel.Tests.fsproj` — Expecto + FsCheck `PackageReference`s (versions from T003), `ProjectReference` to the kernel, compile order `FixedPointTests.fs` then `SurfaceDriftTests.fs`. Depends on T003, T004.
- [X] T006 [P] Create `surface/` directory and an empty placeholder `surface/FS.GG.Governance.Kernel.surface.txt` (the real baseline is generated/committed at T019).
- [X] T007 [P] Create `scripts/prelude.fsx` that `#r`s the built kernel assembly and `open`s `FS.GG.Governance.Kernel` — the FSI entry point used for the Principle I design pass (T009).

**Checkpoint**: `dotnet build` succeeds on an empty kernel skeleton; solution restores; test project references resolve.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Lock the public surface and validate its ergonomics in FSI **before**
any implementation (Constitution Principle I, "Spec → FSI → Semantic Tests →
Implementation"). BLOCKS all user-story work.

**⚠️ CRITICAL**: No story implementation may begin until the `.fsi` is in place and the FSI sketch has confirmed the shapes.

- [X] T008 Copy the curated contract verbatim into `src/FS.GG.Governance.Kernel/Kernel.fsi` from [`contracts/Kernel.fsi`](./contracts/Kernel.fsi) — `FactId`, `RuleId`, `ProvenanceStep`, `FactAssertion<'fact>`, `FactSet<'fact>`, `Rule<'fact>`, `EvaluationResult<'fact>`, and `module FixedPoint` with `val evaluate`. This `.fsi` is the **sole** surface declaration (Principle II). Depends on T004.
- [X] T009 Add a minimal compiling stub for `src/FS.GG.Governance.Kernel/Kernel.fs` (e.g. `evaluate` returning the normalized supplied facts with `Rounds = 0`), with **no** `private`/`internal`/`public` modifiers on any top-level binding (Principle II). Just enough to build the assembly so `scripts/prelude.fsx` can load it. Depends on T008.
- [X] T010 Run the Principle I FSI design pass through `scripts/prelude.fsx` (quickstart "FSI sketch"): define `identify` for a toy `'fact`, supply asserted facts, define 2–3 chained monotonic rules, call `FixedPoint.evaluate`. Confirm the contract's shapes are ergonomic; if awkward, **fix `Kernel.fsi` (and `contracts/Kernel.fsi`) before proceeding** — FSI is the honest audience. Depends on T009.

**Checkpoint**: public surface is frozen and FSI-validated. User stories can begin.

---

## Phase 3: User Story 1 — Derive new facts from facts and rules (Priority: P1) 🎯 MVP

**Goal**: `FixedPoint.evaluate` forward-chains monotonic rules over supplied facts
to the least fixed point and reports the round count — the inference engine itself.

**Independent Test**: Supply toy facts + chained rules (A⇒B, B⇒C), evaluate, and
confirm `Facts` = supplied ∪ correct closure (no spurious/missing) and `Rounds`
equals chain depth.

### Tests for User Story 1 (write first; must FAIL before T015) ⚠️

- [X] T011 [P] [US1] In `tests/FS.GG.Governance.Kernel.Tests/FixedPointTests.fs`, add **V1**: supply facts + chained rules (A⇒B, B⇒C); assert `result.Facts` = supplied + exact transitive closure, no spurious or missing entries (US1 AS1; FR-001/002).
- [X] T012 [P] [US1] In `FixedPointTests.fs`, add **V2**: rules whose preconditions are unmet (and the "no rules" case); assert `result.Facts` = exactly the supplied facts and `result.Rounds = 0` (US1 AS2; edge cases "no rules"/"unmet"; also covers empty-supplied → empty `Facts`).
- [X] T013 [P] [US1] In `FixedPointTests.fs`, add **V3**: a bounded monotone rule set including a self-referential chain (a rule whose output also satisfies its own precondition); assert evaluation terminates / quiesces (US1 AS3; FR-003; SC-003).
- [X] T014 [P] [US1] In `FixedPointTests.fs`, add **V10**: assert `result.Rounds = 0` on a no-derivation input and `= 2` on a depth-2 chain (FR-008; D4).

### Implementation for User Story 1

- [X] T015 [US1] Implement the core fixed-point loop in `src/FS.GG.Governance.Kernel/Kernel.fs` (replacing the T009 stub), per data-model "Derivation/evaluation rules" steps 1–4: **(1)** normalize supplied — `Id := identify Value`, force `Provenance := []`, dedup by `FactId` first-occurrence-wins; **(2)** synchronous round — apply *every* rule's `Apply` to the *same* immutable snapshot; **(3)** discard produced assertions whose `Id` is already known; **(4)** commit new facts, increment `Rounds` on any addition, repeat until quiescence. The single `mutable` accumulator for the pass is the one blessed by Principle III — disclose it with a comment at the use site. No top-level visibility modifiers. **FR-009 (opacity)**: `Value` is never pattern-matched or branched on — opacity is guaranteed structurally by the generic `'fact` signature frozen in T008, not by a runtime test; add a use-site comment stating this. **Depends on T010**; makes T011–T014 pass.

**Checkpoint**: MVP — the engine derives correct closures and reports rounds. V1, V2, V3, V10 green. (Provenance content/order refined in US2/US3.)

---

## Phase 4: User Story 2 — Understand why each derived fact holds (Priority: P1)

**Goal**: Every derived fact carries a `ProvenanceStep` naming its producing rule
and the exact input `FactId`s; asserted facts carry empty provenance; multi-chain
facts get a deterministic justification.

**Independent Test**: Multi-step derivation — read a derived fact's `Provenance`
(names rule + exact inputs); read an asserted fact's `Provenance` (`[]`).

### Tests for User Story 2 (write first; must FAIL before T019/refinements) ⚠️

- [X] T016 [P] [US2] In `FixedPointTests.fs`, add **V4**: multi-step derivation; assert a known-derived fact's `Provenance` names the producing `RuleId` and the exact input `FactId`s it consumed (US2 AS1; FR-004; SC-002 chain-reconstruction with no gaps).
- [X] T017 [P] [US2] In `FixedPointTests.fs`, add **V5**: an asserted (supplied) fact has `Provenance = []` (US2 AS2; FR-005).
- [X] T018 [P] [US2] In `FixedPointTests.fs`, add **V6**: a fact derivable by two distinct chains; assert the recorded provenance is the deterministic first-establishing step under the `(FactId, RuleId)` tie-break (US2 AS3; D2).

### Implementation for User Story 2

- [X] T019 [US2] Refine the round-commit step (3) in `src/FS.GG.Governance.Kernel/Kernel.fs` to **record provenance and apply the D2 tie-break**: among candidates new this round, group by `FactId` and keep the single `ProvenanceStep` selected by the total order on `(FactId, RuleId)`; preserve each rule's `Note`/inputs; re-assign the fact's own `Id := identify Value` (D3, overriding any rule-supplied `Id`). Asserted facts keep `Provenance = []` from T015 step 1. **Depends on T015**; makes T016–T018 pass. (Same file as T015 — sequential, not `[P]`.)

**Checkpoint**: Derivations are explainable and deterministically justified. V4, V5, V6 green alongside US1.

---

## Phase 5: User Story 3 — Same answer regardless of rule order (Priority: P1)

**Goal**: The result (facts **and** per-fact provenance) is the least fixed point —
identical under any rule ordering and across repeated runs — with facts deduplicated
by `FactId` and emitted in canonical order.

**Independent Test**: Shuffle rule order several ways; confirm identical `Facts` and
identical per-fact `Provenance` every run.

### Tests for User Story 3 (write first; must FAIL before T022) ⚠️

- [X] T020 [P] [US3] In `FixedPointTests.fs`, add **V7** (FsCheck **property**): for a generated facts+rules set, permute the rule order N ways; assert identical `Facts` **and** identical per-fact `Provenance` across all permutations (US3 AS1; FR-006; SC-001). Use a fixed/seeded generator config for reproducibility.
- [X] T021 [P] [US3] In `FixedPointTests.fs`, add **V8**: two facts that map to the same `identify` id are produced; assert a single deduplicated entry in `Facts` (US3 AS2; FR-007). Add **V9**: run the same evaluation twice; assert byte-for-byte identical results (SC-001). (Evidence-obligations note moved to its own task T028.)

### Implementation for User Story 3

- [X] T022 [US3] Finalize step (5) "Emit" in `src/FS.GG.Governance.Kernel/Kernel.fs`: return `Facts` **sorted canonically by `FactId`** and `Rounds`, guaranteeing byte-for-byte reproducibility independent of rule order or production order (SC-001). Confirm dedup-by-`FactId` (from T015 step 1 + T019 selection) holds for facts produced by multiple rules/assertions. **Depends on T019**; makes T020–T021 pass. (Same file — sequential, not `[P]`.)

**Checkpoint**: All three P1 stories pass. The engine is deterministic, order-independent, deduplicated, and fully justified. V1–V10 green.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Surface-drift baseline, dependency-hygiene proof, documented
precondition, and the quickstart exit-criteria sweep — the Tier 1 artifact chain.

- [X] T023 [US3] In `tests/FS.GG.Governance.Kernel.Tests/SurfaceDriftTests.fs`, implement **V11**: reflect over the built kernel assembly's public surface and assert it equals `surface/FS.GG.Governance.Kernel.surface.txt` (FR-011; Principle II; D6). Depends on T022 (stable surface).
- [X] T024 Generate and commit the real baseline `surface/FS.GG.Governance.Kernel.surface.txt` from the built assembly (replacing the T006 placeholder); confirm V11 (T023) enforces it green. Depends on T023.
- [X] T025 [P] Add **V12** as a reflective test in `tests/FS.GG.Governance.Kernel.Tests/SurfaceDriftTests.fs`: load the built kernel assembly and assert its referenced assemblies are **BCL-only** (only `System.*` / `FSharp.Core`; no third-party package dependency), so the zero-heavy-deps guarantee is enforced in CI rather than via a manual `dotnet list package` step (FR-010; SC-005). (U1: V12 form pinned to the reflective check.)
- [X] T026 [P] Document the **FR-012 precondition** for consumers — rules are monotonic (add-only); negated/aggregated/recursively-negated facts are supplied from a lower stratum, never derived in the same fixed point — as a doc-comment in `Kernel.fsi`/`Kernel.fs` and a short note in `README.md` or `docs/`. Confirms the "locks decision #4" scope (plan Summary).
- [X] T027 Run the quickstart **Done-when** sweep ([quickstart.md](./quickstart.md) §Done-when): `dotnet build` clean; `dotnet test` green (V1–V12); `Kernel.fsi` matches `contracts/Kernel.fsi`; `Kernel.fs` has no `private`/`internal`/`public` top-level modifiers; surface baseline committed and enforced; zero heavy deps; no packing. Record evidence (command output) and mark exit criteria. Depends on T024, T025, T026.
- [X] T028 [P] Record the feature's **evidence-obligations** note (Principle V) in the PR description: all V1–V10 use **real** facts/rules/evaluation — no synthetic fixtures, no `// SYNTHETIC:` disclosures expected in F01; **Elmish/MVU Principle IV is N/A** (pure reasoner — no `Model`/`Msg`/`Effect`/interpreter); and **FR-009** (fact opacity) is guaranteed structurally by the generic `'fact` signature (T008), not a runtime test. (F1: split out of the former T021.)

---

## Dependencies & Execution Order

### Phase order (sequential)

1. **Setup (P1 tasks T001–T007)** — no dependencies; start immediately.
2. **Foundational (T008–T010)** — depends on Setup; **BLOCKS all stories** (freezes the `.fsi`).
3. **US1 (T011–T015)** → **US2 (T016–T019)** → **US3 (T020–T022)** — see story note below.
4. **Polish (T023–T028)** — depends on US3 (stable surface) being complete; T028 (evidence note) has no code dependency.

### User-story dependencies (the shared-file reality)

- All three stories are **P1** and share the single file `Kernel.fs`. Their
  **tests** are independent and `[P]`; their **implementation** tasks are
  sequential: **T015 → T019 → T022**. US2 refines the provenance/tie-break of the
  loop US1 builds; US3 finalizes the canonical emit ordering over both. This is the
  one place this feature deviates from "stories fully parallel" — recorded honestly
  rather than faked.
- MVP = Setup + Foundational + **US1** (T001–T015): a working derive-to-fixed-point
  engine with round counting. US2/US3 harden provenance determinism and ordering.

### Within each story

- Tests (T011–T014, T016–T018, T020–T021) are authored **before** their
  implementation task and must **fail** first (Principle I).
- All tests against the public surface only — no internal helpers (SC-004).

### Parallel opportunities

- **Setup**: T002, T003, T006, T007 are `[P]` (distinct files); T001 creates the
  tree, T004 needs T002, T005 needs T003+T004.
- **Each story's tests** are `[P]` with one another (all append independent test
  cases — coordinate the single merge into `FixedPointTests.fs`).
- **Polish**: T025, T026, T028 are `[P]`; T023→T024→T027 chain on the baseline.
- The three stories' implementation tasks (T015/T019/T022) are **not** `[P]`
  (same file, ordered).

---

## Summary

| Phase | Tasks | Story | Notes |
|------|-------|-------|-------|
| 1 Setup | T001–T007 | — | scaffolding (4 are `[P]`) |
| 2 Foundational | T008–T010 | — | freeze + FSI-validate the `.fsi` (blocks all) |
| 3 US1 (P1) 🎯 MVP | T011–T015 | US1 | derive to fixed point + rounds (4 tests `[P]`) |
| 4 US2 (P1) | T016–T019 | US2 | provenance + D2 tie-break (3 tests `[P]`) |
| 5 US3 (P1) | T020–T022 | US3 | order-independence, dedup, canonical emit (tests `[P]`) |
| 6 Polish | T023–T028 | — | V11 surface drift, V12 deps (reflective), FR-012 doc, exit sweep, evidence note |

- **Task count per story**: US1 = 5 (4 test + 1 impl) · US2 = 4 (3 test + 1 impl) · US3 = 3 (2 test + 1 impl) · Setup = 7 · Foundational = 3 · Polish = 6. **Total = 28.**
- **Parallel opportunities**: Setup T002/T003/T006/T007; each story's test tasks; Polish T025/T026/T028.
- **Suggested MVP**: User Story 1 (T001–T015) — a working, terminating, round-counting fixed-point engine.
- **Tests**: requested and authored test-first (V1–V12 from quickstart; FsCheck property V7; reflective surface baseline V11; reflective BCL-only deps check V12).
- **Elmish/MVU**: N/A (pure reasoner) — recorded in T028; no MVU boundary tasks emitted.
