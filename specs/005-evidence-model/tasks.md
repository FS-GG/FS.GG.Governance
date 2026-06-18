---
description: "Task list for F05 · 005-evidence-model — the EvidenceState vocabulary, the abstract cycle-rejecting EvidenceGraph, and the transitive synthetic-taint closure (effective)"
---

# Tasks: Evidence Model & Synthetic Taint — Tracking What's Real and Propagating Doubt

**Input**: Design documents from `/specs/005-evidence-model/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Evidence.fsi](./contracts/Evidence.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a Tier 1 feature whose headline guarantees (transitive
`AutoSynthetic` propagation, auto-clear on `Synthetic → Real`, determinism across
permutations, cycle/`AutoSynthetic`/`UnknownNode` rejection, real-only upgrade,
synthetic-outranks-inherited, totality incl. the empty graph) are only credible with real
evidence (Principle V). Per Principle I the semantic tests are written against the public
surface and FAIL before the matching `Evidence.fs` body exists.

**Tier**: whole feature is **Tier 1** (plan Constitution Check). No per-task tier
annotations — every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A (pure derivation)** — `build` is a pure validating constructor and
`effective` is a pure function from a graph to a `Map<'id, EvidenceState>`. No multi-step
state, no I/O, no retries, no agent call, no background work — exactly the "simple pure
function" Principle IV exempts. Reading a node's true declared state and the disclosure
logging around a bypass are the F08 edge interpreter's job, modelled there. No
`Model`/`Msg`/`Effect`/interpreter-boundary tasks here; recorded once in the
evidence-obligations task (T015).

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (with rationale on the line).
  Never mark a failing task `[X]`; never weaken an assertion to green a build.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another *incomplete* task in this phase (parallel-safe hint).
- **[Story]**: `[US1]`..`[US4]`; unlabelled tasks are shared setup/foundational/polish.
- Exact file paths are given in every task.

> **File-coupling caveat.** The whole implementation lives in one file,
> `src/FS.GG.Governance.Kernel/Evidence.fs` (the internal graph representation, `build`,
> `nodes`, `dependencies`, and `effective`), and all semantic tests live in one file,
> `tests/FS.GG.Governance.Kernel.Tests/EvidenceTests.fs`. So tasks that touch the *same* one
> of those two files are **not** `[P]` with each other even when they belong to different
> stories — `[P]` is reserved here for genuinely different files (the FSI sketch, the two
> `.fsproj` edits, the surface baseline, the read-only hygiene check). Stories remain
> independently *testable*, but within `Evidence.fs`/`EvidenceTests.fs` the work is
> sequential to avoid edit conflicts.

> **Scenario numbering.** Test scenarios continue the kernel's running V-series. Quickstart
> §"Validation scenarios" lists **V21–V29**; this breakdown maps them one-to-one to the
> stories (V21/V22/V24 → US1 propagation, V23 → US2 auto-clear, V26 → US3 cycle/refusal,
> V27 → US4 honest states, V25/V28/V29 → cross-cutting determinism/totality/accessor-contract in
> polish).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the new files into the build and exercise the contract in FSI first
(Principle I — the design pass happens before any `Evidence.fs` body).

- [X] T001 [P] Copy the curated contract verbatim into the kernel as
  `src/FS.GG.Governance.Kernel/Evidence.fsi` — it must match
  `specs/005-evidence-model/contracts/Evidence.fsi` byte-for-byte (quickstart done-when). Do
  not add an `Evidence.fs` yet.
- [X] T002 Add `Evidence.fsi` then `Evidence.fs` to the `<Compile>` list in
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`, **after** `Kernel.fs` and
  **before** `Check.fsi` (F05's only real predecessor is F01's `Kernel.*`/`Verdict.*`; it
  references none of `Verdict`/`Check`/`CheckRule`, and `Check.*`/`CheckRule.*` do not depend
  on it — placing it right after the F01 core documents that relationship in the build order;
  plan Structure Decision). Create a minimal stub
  `src/FS.GG.Governance.Kernel/Evidence.fs` (the unions are declared in the `.fsi`; the
  abstract `EvidenceGraph<'id>` rep and the `build`/`nodes`/`dependencies`/`effective` bodies
  are filled in later phases) so the project compiles.
- [X] T003 [P] Add `EvidenceTests.fs` to the `<Compile>` list in
  `tests/FS.GG.Governance.Kernel.Tests/FS.GG.Governance.Kernel.Tests.fsproj`, **before**
  `Main.fs`. Create an empty `tests/FS.GG.Governance.Kernel.Tests/EvidenceTests.fs` exposing
  an empty Expecto `testList "Evidence"` so the test project compiles and `Main` can
  reference it.
- [X] T004 [P] Extend `scripts/prelude.fsx` with the short `Evidence`/`effective` FSI sketch
  from quickstart §"FSI sketch" (steps 1–6): use a plain `string` as the node `'id`; build
  the synthetic-root + real-chain DAG, compute `Evidence.effective` and observe the
  transitive `AutoSynthetic` flow, auto-clear by re-declaring the root `Real`, exercise the
  three `build` refusals (`Cycle`/`AutoSyntheticDeclared`/`UnknownNode`), and the
  non-real-inert + synthetic-outranks cases. This is the Principle-I design pass: if any
  shape is awkward, fix `Evidence.fsi` (T001) **before** writing `Evidence.fs` bodies.

**Checkpoint**: `dotnet build` is clean with the empty `Evidence.fs`/`EvidenceTests.fs`; the
FSI sketch type-checks against the contract.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the data types and the validating graph constructor + accessors that every
story depends on — including the cycle/`UnknownNode`/`AutoSyntheticDeclared` refusals, which
make the DAG invariant unforgeable and so make `effective` provably total. (US3 then *pins*
the refusals at V26.)

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T005 Confirm the public types in `src/FS.GG.Governance.Kernel/Evidence.fsi` compile:
  `EvidenceState` (`Pending | Real | Synthetic | Failed | Skipped | AutoSynthetic` — exactly
  six cases, FR-001), `GraphError<'id>` (`Cycle of 'id list | UnknownNode of 'id |
  AutoSyntheticDeclared of 'id`), and the abstract `[<Sealed>] EvidenceGraph<'id when 'id:
  comparison>` — per data-model.md §Entities. In `src/FS.GG.Governance.Kernel/Evidence.fs`
  define the hidden internal representation of the abstract graph (e.g.
  `{ Nodes: Map<'id, EvidenceState>; Deps: Map<'id, Set<'id>> }`) with **no**
  `private`/`internal`/`public` modifier on the top-level type or any binding — abstraction
  comes from the `.fsi` hiding the rep, not from access keywords (Principle II). Adjust the
  stub so the assembly still builds.
- [X] T006 Implement the `build` smart constructor in
  `src/FS.GG.Governance.Kernel/Evidence.fs` `module Evidence`:
  `build nodes dependencies : Result<EvidenceGraph<'id>, GraphError<'id>>`. `nodes` is
  `('id * EvidenceState) list` (a repeated id keeps its **last** declaration — the nodes form
  a `Map`); each edge `(a, b)` means "`a` rests on `b`". Return the FIRST violation in this
  precedence (data-model.md §`build` table): (1) any node declared `AutoSynthetic` ⇒
  `Error (AutoSyntheticDeclared id)` (FR-002); (2) any dependency endpoint absent from the
  node map ⇒ `Error (UnknownNode id)` (totality); (3) any self-dependency or multi-node loop
  ⇒ `Error (Cycle path)` carrying a witnessing loop (FR-004, decision #4) — detect via a DFS
  cycle check over the `Map`/`Set` adjacency; otherwise `Ok graph`. Total: every input yields
  `Ok` or exactly one `Error` (FR-011). No visibility modifiers on top-level bindings
  (Principle II). This is the shared constructor every story uses; it lands the refusals
  US3/V26 pins (T013). Verify against the T004 FSI sketch.
- [X] T007 Implement the inspection accessors in
  `src/FS.GG.Governance.Kernel/Evidence.fs`: `nodes graph : ('id * EvidenceState) list` and
  `dependencies graph : ('id * 'id) list`, each reading the hidden rep, de-duplicated by id /
  edge, and **ordered deterministically by id** so the accessors are themselves order-free
  (data-model.md §accessors). No visibility modifiers on top-level bindings.

**Checkpoint**: the six-case state, the typed `GraphError`, the abstract `EvidenceGraph`, the
validating `build` (with all three refusals), and the inspection accessors are in place;
`dotnet build` clean. The taint closure can now be built per story.

---

## Phase 3: User Story 1 — Propagate synthetic taint over a dependency graph (Priority: P1) 🎯 MVP

**Goal**: `effective` computes every node's effective state as the transitive least-fixed-point
that upgrades a `Real` node resting — directly or transitively — on a `Synthetic`/`AutoSynthetic`
node to `AutoSynthetic`, leaves a declared `Synthetic` node `Synthetic`, and leaves every other
declared state untouched. The taint reaches the full chain depth and is idempotent over diamonds.

**Independent Test**: build one `Synthetic` root + a chain of `Real` nodes; confirm every real
descendant is `AutoSynthetic` and the root stays `Synthetic`; build a graph with no `Synthetic`
anywhere and confirm every effective state equals its declared state.

### Tests for User Story 1 (write first; must FAIL before T011)

- [X] T008 [US1] In `tests/FS.GG.Governance.Kernel.Tests/EvidenceTests.fs` add **V21**: build
  a `Synthetic` root with a chain of `Real` nodes resting on it (`'id = string`), and assert
  `Evidence.effective` reports the root `Synthetic` and every `Real` descendant
  `AutoSynthetic` (US1 AS1/2, FR-005/006, SC-001, INV-1); **and** build a graph with **no**
  `Synthetic` node anywhere and assert every node's effective state equals its declared state
  — no taint is introduced (US1 AS3, INV-1).
- [X] T009 [US1] In `EvidenceTests.fs` add **V22** (FsCheck property, Expecto.FsCheck): for a
  chain of N `Real` nodes rooted at one `Synthetic` node, arbitrary N, all N descendants are
  reported `AutoSynthetic` — the taint reaches the full transitive depth (US1 AS2, FR-006,
  SC-002, INV-2). Depends on T008 (same file).
- [X] T010 [US1] In `EvidenceTests.fs` add **V24**: a diamond — a `Real` node reaching one
  `Synthetic` root by two distinct paths — is reported `AutoSynthetic` exactly once; the
  closure is idempotent and order-independent (US1 AS4, FR-005, SC-001, INV-4). Depends on
  T009 (same file).

### Implementation for User Story 1

- [X] T011 [US1] Implement `effective graph : Map<'id, EvidenceState>` in
  `src/FS.GG.Governance.Kernel/Evidence.fs` as a **memoized `let rec` DFS** over the DAG
  (Principle III endorses `let rec` for graph walks; the memo table is a single unaliased
  accumulator — prefer an immutable `Map<'id, EvidenceState>` threaded through the recursion so
  no `mutable`/`System.Collections` is needed; if a `mutable` memo is demonstrably plainer,
  disclose the reason at the use site per Principle III). For each node `t` apply the
  documented `effective(t)` rule (data-model.md table): declared `Synthetic` ⇒ `Synthetic`
  (root cause, never `AutoSynthetic`, FR-008); declared `Real` AND some dependency's
  *effective* state ∈ {`Synthetic`, `AutoSynthetic`} ⇒ `AutoSynthetic` (FR-005/006); declared
  `Real` with no effectively-(auto)synthetic dependency ⇒ `Real`; `Pending`/`Failed`/`Skipped`
  ⇒ unchanged (FR-007). Recursion terminates because `build` guarantees a DAG; memoization
  makes it O(V + E) and the result a canonical, order-free `Map` (FR-010/011). No visibility
  modifiers on top-level bindings. Makes T008–T010 pass.

**Checkpoint**: MVP — synthetic taint propagates transitively to full depth and is idempotent
over diamonds. STOP and validate V21/V22/V24 green independently.

---

## Phase 4: User Story 2 — Taint clears automatically when the root cause is upgraded (Priority: P1)

**Goal**: Because `effective` is a pure function of the *current* declared states and edges
(no hidden history), re-declaring a `Synthetic` root as `Real` and recomputing clears the
taint everywhere it had flowed, with no other action; a node still resting on a *different*
remaining synthetic root stays `AutoSynthetic`. (No new implementation — this is a property of
the T011 closure; US2 pins it.)

**Independent Test**: take a graph whose real descendants are `AutoSynthetic` because of one
synthetic root, re-declare that root `Real`, recompute, and confirm all those descendants are
`Real`; with two synthetic roots, upgrade one and confirm only the nodes resting *solely* on it
clear.

### Tests for User Story 2 (write first; pinned by the T011 closure)

- [X] T012 [US2] In `EvidenceTests.fs` add **V23**: (a) build a single-`Synthetic`-root graph
  whose `Real` descendants compute to `AutoSynthetic`, then `build` the same nodes/edges with
  the root re-declared `Real` and assert `effective` now reports every formerly-tainted node
  `Real` — the taint clears with no other change (US2 AS1, FR-009, SC-003, INV-3); (b) build a
  two-`Synthetic`-root graph, upgrade exactly one root to `Real`, and assert only the nodes
  resting **solely** on the upgraded root clear while nodes still resting on the second root
  remain `AutoSynthetic` (US2 AS2, FR-009, INV-3); (c) assert `effective` over the **same**
  declared states computed twice is identical — a pure function carrying no hidden history
  (US2 AS3, FR-010). Depends on prior `EvidenceTests.fs` tasks (same file).

> **No separate implementation task.** Auto-clear is not new behaviour — it falls out of
> `effective` being a history-free pure function (T011). T012 is the headline correctness test
> that pins SC-003.

**Checkpoint**: US1 + US2 work independently; a taint clears on its own once the root cause is
re-declared `Real`. Both P1 stories now deliver the keystone — declared states become an
honest, self-maintaining verdict about trustworthiness.

---

## Phase 5: User Story 3 — Reject cyclic dependency graphs (Priority: P2)

**Goal**: A self-dependency or any multi-node loop is refused at construction; the dependency
structure is a DAG, so the taint closure is a well-defined, terminating least-fixed-point.
(The refusals themselves ship in `build` at T006; this story **pins** them, alongside the
other two `build` guardrails.)

**Independent Test**: attempt to construct a graph with a self-dependency and confirm it is
refused; attempt a multi-node cycle and confirm it is refused; construct an acyclic graph and
confirm it is accepted and computes effective states.

### Tests for User Story 3 (write first; the refusal impl is T006)

- [X] T013 [US3] In `EvidenceTests.fs` add **V26**: assert each `build` refusal in the
  documented precedence — a self-dependency `["a", Real] ["a","a"]` ⇒ `Error (Cycle …)`; a
  multi-node cycle ⇒ `Error (Cycle …)` (US3 AS1/2, FR-004, SC-005, INV-6); a node declared
  `AutoSynthetic` ⇒ `Error (AutoSyntheticDeclared …)` (FR-002, SC-006, INV-7); an edge to an
  undeclared endpoint ⇒ `Error (UnknownNode …)` (totality, FR-011, INV-10); and an acyclic
  graph ⇒ `Ok` whose `effective` computes (US3 AS3, INV-6). Depends on prior `EvidenceTests.fs`
  tasks (same file).

> **No separate implementation task.** All three refusals live in the `build` constructor
> (T006, foundational) so they ship *with* the constructor per the plan; T013 is the headline
> correctness test that pins SC-005 (and FR-002 / the totality refusal).

**Checkpoint**: US1–US3 work independently; a cyclic or `AutoSynthetic`-declaring graph is
unrepresentable, so the closure can never loop or be partial.

---

## Phase 6: User Story 4 — Honest, domain-neutral evidence states (Priority: P3)

**Goal**: Only `Real` nodes are ever tainted. A `Pending`/`Failed`/`Skipped` node resting on
synthetic evidence keeps its declared state; a node declared `Synthetic` that also depends on
synthetic nodes is reported `Synthetic` (the root cause), never `AutoSynthetic`; and the whole
vocabulary is domain-neutral over a generic `'id`. (These are branches of the T011 closure;
US4 pins them.)

**Independent Test**: declare a pending/failed/skipped node depending on a synthetic node and
confirm it is unchanged; declare a node both synthetic and depending on another synthetic and
confirm it is reported `Synthetic`; model a non-software scenario (a finding on simulated data)
and confirm the finding is `AutoSynthetic`.

### Tests for User Story 4 (write first; pinned by the T011 closure)

- [X] T014 [US4] In `EvidenceTests.fs` add **V27**: (a) `Pending`/`Failed`/`Skipped` nodes
  each depending on a `Synthetic` node retain their declared state — never upgraded to
  `AutoSynthetic` (US4 AS1, FR-007, SC-007, INV-8); (b) a node declared `Synthetic` that also
  depends on another `Synthetic` node is reported `Synthetic`, not `AutoSynthetic` (US4 AS2,
  FR-008, SC-006, INV-7); (c) a domain-neutral scenario with `'id = string` naming research
  artifacts — a `Real` "finding" resting on a `Synthetic` "simulated data" node is
  `AutoSynthetic` (US4 AS3, FR-012, INV-12). Depends on prior `EvidenceTests.fs` tasks (same
  file).

> **No separate implementation task.** Real-only upgrade, synthetic-outranks-inherited, and
> domain-neutrality are all branches/properties of the T011 `effective` rule; T014 pins
> SC-006/SC-007 and the FR-012 domain-neutrality.

**Checkpoint**: all four stories independently testable; the full six-case evidence vocabulary
is one coherent, domain-neutral contract.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: determinism, totality, surface discipline, dependency hygiene, and the feature
exit gate.

- [X] T015 [US-all] In `EvidenceTests.fs` add **V25** (FsCheck property, Expecto.FsCheck):
  permuting the `nodes` and/or `dependencies` lists of the same graph yields an **identical**
  `effective` map — a deterministic, order-independent least-fixed-point (FR-010, SC-004,
  INV-5); **and V28** (totality): `build [] []` ⇒ `Ok` of an empty graph whose `effective` is
  the empty `Map`; a lone `Real` node with no deps ⇒ `Real`; and `effective` over every prior
  graph neither throws nor returns a partial (FR-011, SC-008, INV-9); **and V29** (accessor
  contract): build a graph from `nodes`/`dependencies` lists containing a **duplicate id**
  (last declaration wins), a **duplicate edge**, and **unsorted** input, then assert `nodes` and
  `dependencies` return the de-duplicated pairs/edges **ordered by id** — the accessors are
  themselves order-free and history-free (FR-003, data-model §accessors, INV-13). Record here the
  **evidence-obligations note**: F05 is a pure derivation, so Principle IV (Elmish/MVU) is
  **N/A** (the dispatching edge interpreter is F08); all evidence is **real** — real
  `EvidenceGraph` values built from real declared states — no synthetic fixtures, no
  `// SYNTHETIC:` disclosures (the test inputs ARE declared-state graphs). Depends on prior
  `EvidenceTests.fs` tasks (same file).
- [X] T016 Re-bless the API surface baseline:
  `surface/FS.GG.Governance.Kernel.surface.txt` must grow to include the F05 types
  (`EvidenceState`, `GraphError`, the abstract `EvidenceGraph`) and the `Evidence` module
  (`build`/`nodes`/`dependencies`/`effective`). Run `BLESS_SURFACE=1 dotnet test`, confirm the
  diff is **exactly** the F05 additions, and commit it (FR-014, plan Principle II). While
  reviewing the diff, confirm the added names carry **no domain vocabulary** (FR-012, manual
  review) — node identity stays generic `'id`. The existing V11 surface-drift test then guards
  the `Evidence` surface for free.
- [X] T017 [P] Confirm the existing **V12 dependency-hygiene** test still passes — the kernel
  assembly references only the BCL + FSharp.Core after `Evidence.*` is added. F05 introduces
  **no `System.*` reference at all** (only `Map`/`Set`/`List`; lighter than F03/F04, which used
  `SHA256`); no `<PackageReference>` was added to
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj` (FR-012, SC-009).
- [X] T018 Run the quickstart done-when gate end-to-end: `dotnet build` clean; `dotnet test`
  green (V21–V29 + inherited V11 surface-drift + V12 dependency-hygiene); confirm
  `src/FS.GG.Governance.Kernel/Evidence.fsi` still matches
  `specs/005-evidence-model/contracts/Evidence.fsi` and `Evidence.fs` carries no
  `private`/`internal`/`public` on top-level bindings and the `EvidenceGraph<'id>` is abstract;
  confirm no packing was added (the kernel still packs at F06). Walk the FSI sketch (T004) once
  more to confirm SC-009 (the whole surface is exercised through the public API with nothing
  beyond the base runtime and F01). **Reinforces decision #4** (DAG only — cycles rejected,
  keeping the taint a monotone, terminating least-fixed-point).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1 — BLOCKS all stories (every test calls
  `Evidence.build`, and US1's `effective` reads the rep landed here).
- **User stories (Phases 3–6)**: each depends on Foundational. They are independently
  *testable*, but because the whole implementation shares `Evidence.fs` and every test shares
  `EvidenceTests.fs`, implement them in **priority order** (US1 → US2 → US3 → US4) to avoid
  same-file edit conflicts rather than truly in parallel.
- **Polish (Phase 7)**: depends on all desired stories; T016 (surface re-bless) must run after
  the public surface is final (it is fixed at T001 and unchanged thereafter, but re-bless once
  the `.fs` builds).

### Cross-story / cross-task dependencies

- **US1's `effective` (T011) is the one piece of new derivation logic** — US2 (auto-clear),
  US4 (real-only / synthetic-outranks / domain-neutral) are *properties* of that single closure
  and add **no** implementation, only the pins V23 / V27.
- **US3 / V26 (T013) is pinned by the refusals in T006 (foundational)** — no separate impl
  task; the `Cycle`/`UnknownNode`/`AutoSyntheticDeclared` refusals ship with `build`.
- Same-file (`EvidenceTests.fs`) ordering only: T009→T008, T010→T009; T012, T013, T014, T015
  follow the earlier test tasks in the file. These are edit-conflict ordering, not logical
  coupling. All four stories remain independently *testable* once T011 lands.

### Parallel opportunities

- **Phase 1**: T001, T003, T004 are `[P]` (different files); T002 follows T001.
- **Phase 7**: T017 is `[P]` (read-only hygiene check) alongside T016 (surface baseline);
  T015 edits `EvidenceTests.fs`.
- Across stories, true parallelism is limited by the two shared files — see the file-coupling
  caveat at the top. Different *people* could draft one story's test block while another
  implements US1's closure, but commits must serialize on `Evidence.fs`/`EvidenceTests.fs`.

---

## Task count & MVP

- **Setup**: 4 (T001–T004)
- **Foundational**: 3 (T005–T007) — types + the abstract graph rep, `build` (all three
  refusals), and the `nodes`/`dependencies` accessors
- **US1 (P1, MVP)**: 4 — V21 + V22 + V24 tests + the `effective` closure (T008–T011)
- **US2 (P1)**: 1 — V23 test pinning auto-clear/determinism over the T011 closure (T012)
- **US3 (P2)**: 1 — V26 test pinning the foundational `build` refusals (T013)
- **US4 (P3)**: 1 — V27 test pinning real-only / synthetic-outranks / domain-neutral (T014)
- **Polish**: 4 (T015–T018) — V25/V28/V29 determinism+totality+accessor-contract + evidence
  note, surface re-bless, dependency hygiene, the done-when gate
- **Total**: 18 tasks.

**Suggested MVP scope**: Phases 1–3 (Setup + Foundational + **User Story 1**) — synthetic
taint propagates transitively to full depth over a real `EvidenceGraph`, idempotent over
diamonds. This is the entire point of the feature: without the transitive closure there is no
evidence model, only a list of labels. US2 (auto-clear) is co-equal and lands next with no new
code; US3 (cycle rejection) and US4 (honest, domain-neutral states) pin the safety and
vocabulary guarantees that ship in the foundational `build` and the US1 closure respectively.
