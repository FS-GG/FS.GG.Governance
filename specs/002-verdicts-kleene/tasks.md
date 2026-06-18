---
description: "Task breakdown — Verdicts: Three-Valued Kleene Composition (F02)"
---

# Tasks: Verdicts — Three-Valued Kleene Composition

**Feature**: `002-verdicts-kleene` (F02) · **Tier 1** · depends only on F01 (kernel core)

**Input**: Design documents from `/specs/002-verdicts-kleene/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Verdict.fsi](./contracts/Verdict.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. The spec mandates test evidence (Principle V) and the order-independence
guarantees (FR-005/006, SC-001) are FsCheck properties. Semantic tests are written against the
**public surface** and precede `Verdict.fs` (Principle I).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file, or independent).
- **[Story]**: `US1`/`US2`/`US3` per spec; unlabelled tasks are shared/cross-cutting.
- Tier matches the spec (Tier 1) throughout — no per-task `[T1]`/`[T2]` annotation needed.
- **Elmish/MVU (Principle IV): N/A** — `Verdict` is a pure value algebra with no state machine,
  I/O, retries, or user interaction (plan Constitution Check; first MVU boundary is F08). No
  `Model`/`Msg`/`Effect`/interpreter tasks are emitted; see T015.

## Path Conventions

Single existing library + its test project (plan "Project Structure"):
- Library: `src/FS.GG.Governance.Kernel/`
- Tests: `tests/FS.GG.Governance.Kernel.Tests/`
- FSI/surface scaffolding (reused from F01): `scripts/prelude.fsx`, `surface/FS.GG.Governance.Kernel.surface.txt`

---

## Phase 1: Setup — contract + compile wiring (Spec → FSI, Principle I)

**Purpose**: Land the curated signature and make the assembly compile against it before any
behaviour exists. The `.fsi` is the design-first artifact (Principle I/II).

- [X] T001 Copy [`contracts/Verdict.fsi`](./contracts/Verdict.fsi) verbatim to `src/FS.GG.Governance.Kernel/Verdict.fsi` (the curated public surface; `Verdict` union + the `[<CompilationRepresentation(ModuleSuffix)>] Verdict` module with `all`/`any`/`negate`).
- [X] T002 Add `Verdict.fsi` then `Verdict.fs` to the `<Compile>` list in `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`, ordered **before** `Kernel.fsi`/`Kernel.fs` (dependency-free first, per plan Structure Decision).
- [X] T003 Create a compiling stub `src/FS.GG.Governance.Kernel/Verdict.fs`: the `Verdict` type body plus `all`/`any`/`negate` bodies as `failwith "not implemented"`, with **no** `private`/`internal`/`public` modifiers on top-level bindings (Principle II). Confirm `dotnet build src/FS.GG.Governance.Kernel` is clean. (Enables the FSI pass T004 and the surface reflection in Phase 5.)

**Checkpoint**: assembly builds with the stable signature; downstream can `#r` the dll.

---

## Phase 2: Foundational — FSI design pass + test scaffold (Principle I)

**Purpose**: Exercise the contract in FSI (the honest audience) and register the failing test
file, before writing the real `Verdict.fs`. **Blocks all user-story implementation.**

- [X] T004 Extend `scripts/prelude.fsx` with the Verdict sketch from [quickstart.md](./quickstart.md) §"FSI sketch" (construct the three kinds; `all`/`any`/`negate` examples; identities; an order-independence equality). Run `dotnet fsi scripts/prelude.fsx` against the T003 stub: confirm the **shapes typecheck through the public surface** (calls will `failwith` until Phase 3+ — that is expected). If any shape is awkward, fix `Verdict.fsi` (T001) and `contracts/Verdict.fsi` together before proceeding (FSI-first discipline).
- [X] T005 Create `tests/FS.GG.Governance.Kernel.Tests/VerdictTests.fs` with an Expecto `[<Tests>]` `testList "Verdict"` skeleton (empty/`ftestCase` placeholders), `open FS.GG.Governance.Kernel`, and `open FsCheck`. Add `VerdictTests.fs` to the `<Compile>` list in `tests/FS.GG.Governance.Kernel.Tests/FS.GG.Governance.Kernel.Tests.fsproj` **before** `Main.fs`. Confirm `dotnet test` discovers it.

**Checkpoint**: contract validated in FSI; test project compiles and discovers `Verdict` tests.
User-story work can now begin (all stories share `Verdict.fs` + `VerdictTests.fs`, so within a
story tests precede implementation and the implementation tasks are **sequential**, not `[P]`).

---

## Phase 3: User Story 1 — Combine without losing "uncertain" (Priority: P1) 🎯 MVP

**Goal**: `Verdict.all`/`Verdict.any` implement the Kleene "strong" truth tables so a definite
fail dominates conjunction, a definite pass dominates disjunction, and an `Uncertain` input is
never silently coerced to pass/fail (FR-002, FR-003, FR-007).

**Independent Test**: combine mixed pass/fail/uncertain lists under both operations and confirm
the dominating-result / uncertain-survives behaviour of the data-model truth tables.

### Tests for User Story 1 (write first, must FAIL against the T003 stub)

- [X] T006 [US1] In `VerdictTests.fs`, add semantic tests **V1–V5** from [quickstart.md](./quickstart.md) using **real** verdict values (Principle V): V1 `all` with ≥1 `Fail` ⇒ `Fail` (US1 AS1); V2 `all` no-fail + ≥1 `Uncertain` ⇒ `Uncertain` not `Pass` (US1 AS2, INV-4); V3 `any` with ≥1 `Pass` ⇒ `Pass` (US1 AS3); V4 `any` no-pass + ≥1 `Uncertain` ⇒ `Uncertain` not `Fail` (US1 AS4); V5 all-`Pass` under `all` ⇒ `Pass`, all-`Fail` under `any` ⇒ `Fail` (US1 AS5). Run `dotnet test` — confirm they FAIL.

### Implementation for User Story 1

- [X] T007 [US1] Implement `all` and `any` in `src/FS.GG.Governance.Kernel/Verdict.fs` per the data-model truth tables: `all` = first-match priority `any Fail ⇒ Fail | any Uncertain ⇒ Uncertain | else Pass`; `any` = `any Pass ⇒ Pass | any Uncertain ⇒ Uncertain | else Fail`. Use a single pass over the list (no mutation/recursion-for-state, Principle III). Leave reason aggregation to a shared helper introduced in T010 — for now collect the dominating kind's reasons in list order. Confirm **V1–V5 green** via `dotnet test`.

**Checkpoint**: outcome semantics correct; `Uncertain` provably survives. US1 is independently
testable (reason-string determinism is US2's job).

---

## Phase 4: User Story 2 — Same verdict regardless of order (Priority: P1)

**Goal**: the combined verdict — **outcome and reason string** — is byte-for-byte identical under
reordering and re-nesting; the reason is a function of the *set* of `"; "`-delimited components
(FR-005, FR-006, SC-001). This is the confluence-hazard mitigation (Hazard-2).

**Independent Test**: fix a multiset of sub-verdicts; combine under `all`/`any` in shuffled orders
and re-nestings; assert identical results including reason text.

### Tests for User Story 2 (write first, must FAIL against T007's list-order reasons)

- [X] T008 [P] [US2] In `VerdictTests.fs`, add FsCheck **property** tests V6 and V7: V6 — permuting the input list yields an identical verdict **and reason** for both `all` and `any` (FR-005/006, INV-1, SC-001); V7 — re-nesting (`all [all xs; ys]` vs `all (xs @ ys)`, likewise `any`) yields an identical verdict **and reason** (associativity, INV-2/INV-3, US2 AS3). Use real generated `Verdict` lists (custom FsCheck generator over the three cases with arbitrary reason strings).
- [X] T009 [P] [US2] In `VerdictTests.fs`, add example test V8: a dominating combination with duplicate and shuffled identical reasons yields a dedup'd, ordinal-sorted, position-independent combined reason (US2 AS4, edge "reason determinism under duplication", INV-3). Include the quickstart case `all [Fail "a"; Fail "z"] = all [Fail "z"; Fail "a"] = Fail "a; z"` and the nesting case collapsing to `Fail "a; m; z"`. Run `dotnet test` — confirm V8 (together with V6/V7) FAILS against T007's list-order reasons.

### Implementation for User Story 2

- [X] T010 [US2] In `src/FS.GG.Governance.Kernel/Verdict.fs`, add the reason-aggregation helper per [data-model.md](./data-model.md) §"Reason aggregation": split each contributing reason on the reserved `"; "` separator dropping empty components, `distinct`, sort by `System.String.CompareOrdinal` (culture-invariant, BCL-only), `String.concat "; "`. Wire `all`/`any` (T007) to build their `Fail`/`Uncertain` reason through this helper over the **dominating kind's** reasons only (`Pass` contributes nothing; `any []` identity `Fail ""` is absorbed). Confirm **V6, V7, V8 green** and V1–V5 still green via `dotnet test`.

**Checkpoint**: full byte-for-byte order/nesting/duplication independence holds. US1+US2 both green.

---

## Phase 5: User Story 3 — Negate a verdict (Priority: P2)

**Goal**: `Verdict.negate` flips `Pass`⇄`Fail` and fixes `Uncertain`, with the documented
tag-level involution `negate (negate v) = v` (FR-004, data-model `negate` table).

**Independent Test**: negate each kind and twice; assert `Pass`↔`Fail` swap, `Uncertain` fixed,
and double-negation recovers the tag.

### Tests for User Story 3 (write first, must FAIL against the T003 stub body)

- [X] T011 [P] [US3] In `VerdictTests.fs`, add test V9: `negate (Fail "x") = Pass`, `negate Pass = Fail ""`, `negate (Uncertain "y") = Uncertain "y"` (US3 AS1–AS3); plus a double-negation case asserting tag recovery (`negate (negate v)` matches `v`'s tag for every kind; exact recovery for `Uncertain` and `""`-reasoned pass/fail per data-model note INV-7). Run `dotnet test` — confirm V9 FAILS.

### Implementation for User Story 3

- [X] T012 [US3] Implement `negate` in `src/FS.GG.Governance.Kernel/Verdict.fs`: `Pass -> Fail ""`, `Fail _ -> Pass`, `Uncertain r -> Uncertain r` (Principle III: plain three-arm match). Confirm **V9 green** and all prior tests still green via `dotnet test`.

**Checkpoint**: the full Kleene operator set (`all`/`any`/`negate`) ships as one algebra.

---

## Phase 6: Polish — surface bless, edge coverage, hygiene, docs

**Purpose**: lock the public surface, cover the remaining edges/identities, and re-confirm the
dependency and surface-drift guarantees inherited from F01.

- [X] T013 [P] In `VerdictTests.fs`, add test V10 (edges/identities): `all [] = Pass`, `any [] = Fail ""`, and `all [v] = any [v] = v` (reason preserved) for each kind (FR-008/009, INV-5, edges "empty"/"single sub-verdict"). Add a totality assertion that exercises mixed/empty inputs without throwing (FR-008, SC-003, INV-6). Confirm green.
- [X] T014 Re-bless the kernel surface baseline to include `Verdict` + the `Verdict` module: run `BLESS_SURFACE=1 dotnet test`, then review and commit the regenerated `surface/FS.GG.Governance.Kernel.surface.txt`. Confirm the existing reflective drift test (V11) and dependency-hygiene test (V12) in `SurfaceDriftTests.fs` pass unchanged otherwise (FR-011, SC-005, Principle II).
- [X] T015 [P] No `Verdict.fsi`/`Verdict.fs` source carries a domain reference, fact inspection, or I/O, and the kernel `.fsproj` still has **zero** `PackageReference` (FR-010, SC-005) — verify by inspection and by the green V12 hygiene test. Record in the implement-phase evidence notes that **Principle IV (Elmish/MVU) is N/A** for this pure feature (no interpreter evidence obligation).
- [X] T016 [P] Run the full [quickstart.md](./quickstart.md) "Done-when" checklist end-to-end: `dotnet build` clean; `dotnet test` green (V1–V11 here + F01's suite unchanged); `Verdict.fsi` matches `contracts/Verdict.fsi`; `Verdict.fs` free of visibility modifiers on top-level bindings. Tick the quickstart exit boxes.

---

## Dependencies & Execution Order

### Phase order (sequential)

1. **Phase 1 Setup** (T001–T003) — no deps; lands the contract and a compiling stub.
2. **Phase 2 Foundational** (T004–T005) — depends on T003 (built dll for FSI; compiling assembly for tests). **Blocks all stories.**
3. **Phase 3 US1** (T006–T007) → **Phase 4 US2** (T008–T010) → **Phase 5 US3** (T011–T012). US2's tests (T008/T009) assume `all`/`any` exist (T007); US1 is the MVP and should land first. US3 is independent of US2's reason logic and could be slotted right after US1 if preferred, but priority order P1→P2 keeps US3 last.
4. **Phase 6 Polish** (T013–T016) — depends on all combinators implemented.

### Within each user story

- Tests are written **before** implementation and must FAIL first (Principle I/V): T006→T007, {T008,T009}→T010, T011→T012.
- All three implementation tasks (T007, T010, T012) edit the **same file** `Verdict.fs` — they are **sequential**, not parallel, despite being in different phases.

### Parallel opportunities `[P]`

- T008 and T009 (both add independent test cases; if one engineer owns the file, treat as sequential — `[P]` marks logical independence, not file-disjointness).
- T011 (US3 test) is independent of US2 and may be authored alongside Phase 4.
- T013, T015, T016 in Polish are independent checks across different concerns.
- **Note**: because `Verdict.fs` and `VerdictTests.fs` are each a single file, cross-story *implementation* parallelism is limited; the `[P]` hints reflect logical independence for planning, not safe concurrent edits to one file.

---

## Implementation Strategy

### MVP (User Story 1)

Phase 1 → Phase 2 → Phase 3, then **STOP and validate**: outcome-level Kleene semantics with
`Uncertain` preservation (V1–V5) is the minimum viable verdict algebra and unblocks nothing
downstream until reasons are deterministic — but it proves the core distinction the whole project
rests on.

### Incremental delivery

US1 (outcomes) → US2 (byte-for-byte reproducible reasons, the cacheability guarantee) → US3
(negation completes the operator set F03 needs). Each phase ends green and is independently
testable.

---

## Notes

- **Tier 1 throughout** (new public API + surface-baseline update) — full artifact chain already in place; this phase adds `Verdict.fs`, `VerdictTests.fs`, the re-blessed surface, and the prelude sketch.
- **Principle IV N/A** (pure algebra) — recorded once in T015; no MVU tasks emitted.
- **Real evidence only** (Principle V): every test (V1–V11) uses real `Verdict` values; no synthetic fixtures, so no `// SYNTHETIC:` disclosures are expected.
- Never mark a task `[X]` on a failing assertion; narrow scope and document rather than weaken an assertion (skill discipline).
- Commit after each green phase.

---

## Summary

| User story | Priority | Tasks | Test tasks | Impl tasks |
|---|---|---|---|---|
| US1 — combine without losing uncertain | P1 (MVP) | 2 | T006 | T007 |
| US2 — same verdict regardless of order | P1 | 3 | T008, T009 | T010 |
| US3 — negate a verdict | P2 | 2 | T011 | T012 |
| Setup / Foundational / Polish (shared) | — | 9 | T013 | T001–T005, T014–T016 |

- **Total tasks**: 16 (T001–T016).
- **MVP scope**: User Story 1 (Phases 1–3 → T001–T007).
- **Parallel opportunities**: T008/T009 (US2 test cases), T011 (US3 tests authored alongside US2), and the independent Polish checks T013/T015/T016 — bounded by the single-file `Verdict.fs`/`VerdictTests.fs` reality.
