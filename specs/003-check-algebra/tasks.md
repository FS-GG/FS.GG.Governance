---
description: "Task list for F03 · 003-check-algebra — the reified Check rule algebra"
---

# Tasks: Check — The Reified, Inspectable Rule Algebra

**Input**: Design documents from `/specs/003-check-algebra/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Check.fsi](./contracts/Check.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a Tier 1 feature whose headline guarantees (execution-free
inspection, commutative-hash canonicalization, eval↔explain agreement, totality) are only
credible with real evidence (Principle V). Per Principle I the semantic tests are written
against the public surface and FAIL before the matching `Check.fs` body exists.

**Tier**: whole feature is **Tier 1** (plan Constitution Check). No per-task tier
annotations — every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A** — pure, applicative value algebra; no `bind`, no state machine,
no I/O, no effects (FR-012, plan Principle IV row). No `Model`/`Msg`/`Effect`/interpreter
boundary tasks. This is recorded once in the evidence-obligations task (T024).

## Implementation notes (2026-06-18)

All 27 tasks implemented and `[X]`. All evidence is **real** (real `Check` values + real
probes, incl. the throwing-`Eval` probe of V4); no synthetic fixtures, no `// SYNTHETIC:`
disclosures (T024). Two small, honest deviations from the literal task text:

- **`render` includes declared reads (inputs), not just args.** `Check.fsi` specifies
  render uses "probe names, declared args, **inputs**" and the quickstart illustration is
  `contrastRatio(token:text, 4.5)` — so the atom render is `Name(reads…, args…)`. The
  data-model §render table listed args only; the `.fsi` is authoritative, so reads are
  included (hashed positionally as the §hash table already required). V5 pins the exact string.
- **V4 exercises all four structure-only folds directly.** `reads`/`isReified` were
  implemented in the same pass as `render`/`hash`, so V4 asserts all four succeed on the
  throwing-`Eval` probe with no deferred-assert split (the T012/T022 hand-off was unneeded).
  V11 still independently pins `reads`/`isReified` semantics.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another *incomplete* task in this phase (parallel-safe hint).
- **[Story]**: `[US1]`..`[US4]`; unlabelled tasks are shared setup/foundational/polish.
- Exact file paths are given in every task.

> **File-coupling caveat.** All six interpreters live in one file,
> `src/FS.GG.Governance.Kernel/Check.fs`, and all semantic tests live in one file,
> `tests/FS.GG.Governance.Kernel.Tests/CheckTests.fs`. So tasks that touch the *same* one
> of those two files are **not** `[P]` with each other even when they belong to different
> stories — `[P]` is reserved here for genuinely different files (e.g. the FSI sketch, the
> two `.fsproj` edits, the surface baseline). Stories remain independently *testable*, but
> within `Check.fs`/`CheckTests.fs` the work is sequential to avoid edit conflicts.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the new files into the build and exercise the contract in FSI first
(Principle I — the design pass happens before any `Check.fs` body).

- [X] T001 [P] Copy the curated contract verbatim into the kernel as
  `src/FS.GG.Governance.Kernel/Check.fsi` (it must match `specs/003-check-algebra/contracts/Check.fsi`
  byte-for-byte per the quickstart done-when). Do not add a `Check.fs` yet.
- [X] T002 Add `Check.fsi` then `Check.fs` to the `<Compile>` list in
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`, **after** `Kernel.fs`
  (Check depends on `Verdict` and `Kernel.FactSet<'fact>`; plan Structure Decision).
  Create a minimal stub `src/FS.GG.Governance.Kernel/Check.fs` (the types are in the
  `.fsi`; bodies are filled in later phases) so the project compiles.
- [X] T003 [P] Add `CheckTests.fs` to the `<Compile>` list in
  `tests/FS.GG.Governance.Kernel.Tests/FS.GG.Governance.Kernel.Tests.fsproj`, **before**
  `Main.fs`. Create an empty `tests/FS.GG.Governance.Kernel.Tests/CheckTests.fs` exposing
  an empty Expecto `testList "Check"` so the test project compiles and Main can reference it.
- [X] T004 [P] Extend `scripts/prelude.fsx` with the short `Check` FSI sketch from
  quickstart §"FSI sketch" (build `contrast`/`tone` probes, compose with `.&` and `==>`,
  fold all six ways). This is the Principle-I design pass: if any shape is awkward, fix
  `Check.fsi` (T001) **before** writing `Check.fs` bodies.

**Checkpoint**: `dotnet build` is clean with empty `Check.fs`/`CheckTests.fs`; the FSI
sketch type-checks against the contract.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the data types and the readable authoring surface that every
interpreter and every story depends on.

**⚠️ CRITICAL**: No user-story interpreter work can begin until this phase is complete.

- [X] T005 Confirm the five public types in `src/FS.GG.Governance.Kernel/Check.fsi`
  compile and require no `.fs`-side definition beyond the `.fsi` (records/unions are
  declared in the signature): `ArtifactRef`, `Outcome` (`Met | Unmet | Unknown`),
  `ProbeArg` (`ArtifactArg | LiteralArg | NumberArg`), `Probe<'fact>`, `Check<'fact>`
  (`Atom | All | Any | Not | Implies | Opaque`) and the `Explanation` union — per
  data-model.md §Entities. Adjust `Check.fs` so the assembly still builds. Note that
  **FR-012 (applicative, no `bind`) is enforced structurally by the closed `.fsi` union —
  there is no `bind` to call — not by a test** (it is unfalsifiable by a test); the
  surface-drift baseline (T025) is what guards against a monadic combinator being added.
- [X] T006 Implement the smart constructors / operators in
  `src/FS.GG.Governance.Kernel/Check.fs` `module Check`: `probe`, `allOf`, `anyOf`,
  `not'`, and the operators `(==>)`, `(.&)`, `(.|)` — thin aliases adding no semantics
  (FR-005, contract lines 108–130). No `private`/`internal`/`public` modifiers on any
  top-level binding (Principle II). Verify against the T004 FSI sketch.
- [X] T007 Implement the private `outcomeToVerdict` helper in
  `src/FS.GG.Governance.Kernel/Check.fs` (`Met → Pass`, `Unmet r → Fail r`,
  `Unknown r → Uncertain r`) — used by `eval` and `explain` (data-model.md §eval). Keep
  it out of the `.fsi` (it is internal to the fold, not surface).

**Checkpoint**: types + authoring surface + outcome mapping in place; `dotnet build`
clean. Interpreters can now be implemented per story.

---

## Phase 3: User Story 1 — Build a check and evaluate it to a three-valued verdict (Priority: P1) 🎯 MVP

**Goal**: A composed `Check` value folds to a Kleene three-valued `Verdict`, reusing the
F02 combinators so `Uncertain` is preserved unless dominated.

**Independent Test**: Construct atom / `All` / `Any` / `Not` / `Implies` / `Opaque` over
probes returning a mix of `Met`/`Unmet`/`Unknown` and confirm the verdict matches the
documented Kleene semantics (quickstart V1–V3).

### Tests for User Story 1 (write first; must FAIL before T011)

- [X] T008 [US1] In `tests/FS.GG.Governance.Kernel.Tests/CheckTests.fs` add **V1**: `eval`
  of an atom for each `Outcome` (Met→Pass, Unmet r→Fail r, Unknown r→Uncertain r), and of
  `All`/`Any`/`Not` over mixed met/unmet/unknown children — Fail dominates `All`, Pass
  dominates `Any`, `Uncertain` survives when undominated (US1 AS1–4, FR-006, INV-2).
  **Also add the `eval` order-independence assertion (INV-11, FR-014, SC-003 reason half):**
  for a commutative node with several failing/uncertain children,
  `eval f (All xs) = eval f (All (permute xs))` and `eval f (Any xs) = eval f (Any (permute xs))`
  in **both verdict and aggregated reason** (inherited from F02 reason aggregation, but
  pinned here at the Check level). An FsCheck property or a couple of explicit permutations
  both suffice.
- [X] T009 [US1] In `CheckTests.fs` add **V2**: for representative `a`,`b`,
  `eval facts (a ==> b) = eval facts (Any [Not a; b])` (the implication desugaring,
  US1 AS5, FR-006). Depends on T008 (same file).
- [X] T010 [US1] In `CheckTests.fs` add **V3**: `eval` of an `Opaque (name, f)` node maps
  `f facts`'s outcome to the matching verdict (US1 AS6, FR-006). Depends on T009 (same file).

### Implementation for User Story 1

- [X] T011 [US1] Implement `Check.eval : FactSet<'fact> -> Check<'fact> -> Verdict` in
  `src/FS.GG.Governance.Kernel/Check.fs` as a `let rec` fold reusing the F02 combinators:
  `Atom p → outcomeToVerdict (p.Eval facts)`; `All cs → Verdict.all (List.map (eval facts) cs)`;
  `Any cs → Verdict.any (...)`; `Not c → Verdict.negate (eval facts c)`;
  `Implies (a,b) → eval facts (Any [Not a; b])`; `Opaque (_,f) → outcomeToVerdict (f facts)`
  (data-model.md §eval table, FR-006). Empty `All → Pass`, empty `Any → Fail ""`
  inherited from F02. Make T008–T010 pass.

**Checkpoint**: MVP — a reified check evaluates to a three-valued verdict. STOP and
validate V1–V3 green independently.

---

## Phase 4: User Story 2 — Inspect a check without running it: render and hash (Priority: P1)

**Goal**: `render` and `hash` fold a check from structure alone, never executing a probe
`Eval`; commutative nodes hash permutation-invariantly while positional structure is
preserved.

**Independent Test**: render/hash a check with no facts and confirm no `Eval` ran;
`hash (All [a;b]) = hash (All [b;a])` but `hash (a==>b) ≠ hash (b==>a)` and reordered
probe `Args` change the hash; `Opaque` hashes by name only (quickstart V4–V8).

### Tests for User Story 2 (write first; must FAIL before T017–T018)

- [X] T012 [US2] In `CheckTests.fs` add **V4** (the keystone never-executes proof): build
  a `boom` probe whose `Eval` is `fun _ -> failwith "executed"`; assert `render`, `hash`,
  `reads`, `isReified` all succeed on a check containing it and only `eval` throws
  (US2 AS1, FR-007/008, SC-001, INV-1). Note: `reads`/`isReified` land in Phase 6 —
  guard those two asserts with a `// pending T021/T022` marker or split them to T023, but
  keep render/hash assertions live here.
- [X] T013 [US2] In `CheckTests.fs` add **V5**: `render` of a composed check (no facts)
  produces a deterministic readable string and re-rendering the identical value yields the
  identical string; authoring order is preserved (US2 AS1, FR-007). Depends on T012.
- [X] T014 [US2] In `CheckTests.fs` add **V6** (FsCheck property): for any `All`/`Any`,
  every permutation of its members hashes identically (US2 AS3, FR-008, SC-002, INV-3).
  Use the FsCheck integration already pinned (Expecto.FsCheck). **Also cover the
  duplicate-members edge case (spec §Edge Cases):** a commutative node hashes
  deterministically regardless of duplicate count/position, e.g.
  `hash (All [a; a; b]) = hash (All [b; a; a])`. Depends on T013.
- [X] T015 [US2] In `CheckTests.fs` add **V7**: `hash (a ==> b) ≠ hash (b ==> a)` for
  distinct `a`,`b`, and a probe with reordered `Args` hashes differently (positional;
  US2 AS4/AS5, FR-008, SC-002, INV-4). Depends on T014.
- [X] T016 [US2] In `CheckTests.fs` add **V8**: re-hashing an identical check yields the
  identical key, and an `Opaque` node hashes from its name only — two opaques with the
  same name but different `Eval` functions hash identically (US2 AS2/AS6, FR-008, INV-5).
  Depends on T015.

### Implementation for User Story 2

- [X] T017 [US2] Implement `Check.render : Check<'fact> -> string` in
  `src/FS.GG.Governance.Kernel/Check.fs` as a structure-only `let rec` fold (never calls
  `Eval`, takes no facts): `Atom p → Name + "(args)"`, `All → "all of [..]"`,
  `Any → "any of [..]"`, `Not → "not (..)"`, `Implies → "(a) implies (b)"` (positional),
  `Opaque (n,_) → "opaque \"n\""` (data-model.md §render). Deterministic; preserves
  authoring order (does NOT canonicalize). Makes T012–T013 pass.
- [X] T018 [US2] Implement `Check.hash : Check<'fact> -> string` in
  `src/FS.GG.Governance.Kernel/Check.fs` as an execution-free SHA-256 fold
  (`System.Security.Cryptography.SHA256` + `System.Text.Encoding.UTF8`): combine
  components prefix-free (each leaf hashed to fixed-width hex first); `All`/`Any` children
  are **ordinal-sorted** (`String.CompareOrdinal`) before combining (commutative
  canonicalization); `Atom` (`Name`,`Args`,`Reads` in order), `Implies` (a then b) and the
  arg/read lists stay **positional**; `Opaque` contributes its name only
  (data-model.md §hash, Hazard 3). Makes T014–T016 pass.

**Checkpoint**: US1 + US2 work independently; a check is evaluable, renderable, and hashable
with the commutative/positional split pinned and execution-freedom proven.

---

## Phase 5: User Story 3 — Explain a result as a proof tree (Priority: P2)

**Goal**: `explain` folds a check over facts into an `Explanation` proof tree mirroring the
check's surface, whose root verdict is identical to `eval`'s.

**Independent Test**: explain a multi-level check; confirm the root verdict equals `eval`'s
and each atom records its met/unmet/unknown outcome (quickstart V9–V10).

### Tests for User Story 3 (write first; must FAIL before T020–T021)

- [X] T019 [US3] In `CheckTests.fs` add **V9** (FsCheck property): for any check and any
  facts, `Explanation.verdict (Check.explain f c) = Check.eval f c` — the two folds never
  disagree (US3 AS1, FR-009, SC-004, INV-6). Depends on T011 (`eval`).
- [X] T020 [US3] In `CheckTests.fs` add **V10**: `explain` of a multi-level composed check
  produces an `Explanation` whose structure mirrors the check and whose atom/opaque nodes
  record the observed `Outcome` (US3 AS2, FR-009, INV-7). Depends on T019.

### Implementation for User Story 3

- [X] T021 [US3] Implement `Explanation.verdict : Explanation -> Verdict` (reads the
  root node's carried verdict) and `Check.explain : FactSet<'fact> -> Check<'fact> -> Explanation`
  in `src/FS.GG.Governance.Kernel/Check.fs`. `explain` mirrors surface structure; each
  node's verdict is computed with the same F02 combinators as `eval` so the root equals
  `eval` (SC-004); for `Implies (a,b)` the node holds `explain a`/`explain b` and its
  verdict is `Verdict.any [Verdict.negate (verdict aExpl); verdict bExpl]`; atom/opaque
  record the observed `Outcome` (data-model.md §explain). Makes T019–T020 pass.

**Checkpoint**: US1–US3 work independently; verdicts are now auditable as proof trees.

---

## Phase 6: User Story 4 — Detect opacity and collect declared reads (Priority: P3)

**Goal**: Two thin structural folds — `reads` (collect declared `ArtifactRef`s) and
`isReified` (true iff no `Opaque` anywhere) — that F04 routing and the tier bridge depend on.

**Independent Test**: a fully structural check is `isReified = true`; inserting an `Opaque`
anywhere flips it to `false`; `reads` returns exactly the atoms' declared `ArtifactRef`s,
`Opaque` contributing none (quickstart V11).

### Tests for User Story 4 (write first; must FAIL before T022–T023)

- [X] T022 [US4] In `CheckTests.fs` add **V11**: `isReified` is `true` for an opaque-free
  check and `false` once an `Opaque` is inserted anywhere; `reads` over declaring probes
  returns exactly those `ArtifactRef`s and `Opaque` adds none (US4 AS1–3, FR-010/011,
  SC-005, INV-8/INV-9). Also activate the two `reads`/`isReified` asserts deferred from
  V4 (T012). Depends on prior `CheckTests.fs` tasks (same file).

### Implementation for User Story 4

- [X] T023 [US4] Implement `Check.reads : Check<'fact> -> ArtifactRef list` (left-to-right
  structural walk collecting every `Atom`'s `Reads`; `Opaque` contributes none; no dedup —
  that is F04 policy) and `Check.isReified : Check<'fact> -> bool` (`true` iff no `Opaque`
  node appears anywhere) in `src/FS.GG.Governance.Kernel/Check.fs`
  (data-model.md §reads / §isReified). Makes T022 (and the deferred V4 asserts) pass.

**Checkpoint**: all six interpreters implemented; all four stories independently testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: totality, surface discipline, dependency hygiene, and the feature exit gate.

- [X] T024 [US-all] In `CheckTests.fs` add **V12** (totality): empty `All`/`Any` and a
  spread of combinator mixes folded through all six interpreters — none throws or returns a
  partial; `All [] → Pass`, `Any [] → Fail ""` (edges, FR-013, SC-006, INV-10). Record here
  the **evidence-obligations note**: F03 is pure/applicative, so Principle IV (Elmish/MVU)
  is **N/A**, and all evidence is **real** (real `Check` values + real probes, incl. the
  throwing-`Eval` probe of V4) — no synthetic fixtures, no `// SYNTHETIC:` disclosures.
- [X] T025 Re-bless the API surface baseline:
  `surface/FS.GG.Governance.Kernel.surface.txt` must grow to include the F03 types
  (`ArtifactRef`, `Outcome`, `ProbeArg`, `Probe`, `Check`, `Explanation`) and the
  `Check`/`Explanation` modules. Run `BLESS_SURFACE=1 dotnet test`, inspect the diff is
  exactly the F03 additions, and commit it (FR-016, plan Principle II). The existing V11
  surface-drift test then guards the Check surface for free. While reviewing the diff,
  **confirm the FR-015 "no domain vocabulary" half**: the added F03 names
  (`ArtifactRef`/`Outcome`/`Probe`/`Check`/`Explanation` + interpreters) carry no software,
  design, or workflow vocabulary — this review is the check for the vocab half of FR-015
  (the dependency/I-O half is tested mechanically by V12 / T026).
- [X] T026 [P] Confirm the existing V12 dependency-hygiene test still passes — the kernel
  assembly references only the BCL + FSharp.Core after `Check.*` is added (SHA-256 is
  `System.Security.Cryptography`, allowed; SC-008). No `<PackageReference>` was added to
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`.
- [X] T027 Run the quickstart done-when gate end-to-end: `dotnet build` clean; `dotnet
  test` green (V1–V12 + inherited surface-drift + dependency-hygiene); confirm
  `src/FS.GG.Governance.Kernel/Check.fsi` still matches `specs/003-check-algebra/contracts/Check.fsi`
  and `Check.fs` carries no `private`/`internal`/`public` on top-level bindings; confirm no
  packing was added (kernel still packs at F06). Walk the FSI sketch (T004) once more to
  confirm SC-007 (a newcomer folds a check all six ways through the public surface alone).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1 — BLOCKS all stories.
- **User stories (Phases 3–6)**: each depends on Foundational. They are independently
  *testable*, but because every interpreter shares `Check.fs` and every test shares
  `CheckTests.fs`, implement them in **priority order** (US1 → US2 → US3 → US4) to avoid
  same-file edit conflicts rather than truly in parallel.
- **Polish (Phase 7)**: depends on all desired stories; T025 (surface re-bless) should run
  after the public surface is final (after T021/T023).

### Cross-story / cross-task dependencies

- T009→T008, T010→T009; T013→T012, T014→T013, T015→T014, T016→T015; T020→T019 — all are
  *same-file* (`CheckTests.fs`) ordering, not logical coupling.
- T011 (`eval`) underlies T019/V9 (explain↔eval property) — write `eval` before the V9
  property can be meaningful.
- V4 (T012) asserts on `reads`/`isReified` that only land in T023 — T022 re-activates those
  two deferred asserts once T023 exists.

### Parallel opportunities

- **Phase 1**: T001, T003, T004 are `[P]` (different files); T002 follows T001.
- **Phase 7**: T026 is `[P]` (read-only check) alongside T025; T024 edits `CheckTests.fs`.
- Across stories, true parallelism is limited by the two shared files — see the
  file-coupling caveat at the top. Different *people* could draft the test block for one
  story while another implements an earlier story's interpreter, but commits must serialize
  on `Check.fs`/`CheckTests.fs`.

---

## Task count & MVP

- **Setup**: 4 (T001–T004)
- **Foundational**: 3 (T005–T007)
- **US1 (P1, MVP)**: 4 — V1–V3 tests + `eval` (T008–T011)
- **US2 (P1)**: 7 — V4–V8 tests + `render` + `hash` (T012–T018)
- **US3 (P2)**: 3 — V9–V10 tests + `explain`/`Explanation.verdict` (T019–T021)
- **US4 (P3)**: 2 — V11 test + `reads`/`isReified` (T022–T023)
- **Polish**: 4 (T024–T027)
- **Total**: 27 tasks.

**Suggested MVP scope**: Phases 1–3 (Setup + Foundational + **User Story 1**) — a reified
check that evaluates to a three-valued verdict. Both P1 stories (US1 eval + US2
render/hash) together deliver the keystone "one value, folded many ways" promise; US3/US4
layer audit and structural folds on top.
