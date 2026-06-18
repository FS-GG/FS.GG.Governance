---
description: "Task list for F07 · 007-routing-severity-modes — the light routing layer: stakes & fences (forbid-trumps-permit, never positional), run modes, and the explainable Route; starts Milestone M2"
---

# Tasks: Routing, Stakes & Run Modes — Light by Default, Always Explained

**Input**: Design documents from `/specs/007-routing-severity-modes/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Route.fsi](./contracts/Route.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a Tier 1 feature whose headline guarantees (light-by-default ⇒
`Routine` + empty `Blocking` in every mode, forbid-trumps-permit order-independence, the
run-mode enforcement matrix with stakes-identical-across-modes, the drift-proof gate whose
`Statement` IS `Check.render`, the mandatory non-empty reason, byte-for-byte determinism,
totality over empty fences / empty rules, zero probe-or-review executed, and zero-dependency
hygiene) are only credible with real evidence (Principle V). Per Principle I the semantic tests
are written against the **public** surface (through the built library / `scripts/prelude.fsx`)
and FAIL before the matching `Route.fs` body exists.

**Tier**: whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations —
every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A (pure derivation)** — `stakesOf`/`route`/`renderRoute` map supplied values
(`fences`, `rules`, `mode`, the abstract `change`) to a `Stakes`/`Route`/`string`. No multi-step
state, no I/O, no retries, no agent call, no clock, no background work — exactly the "simple pure
function" Principle IV exempts (spec Assumptions; plan Constitution Check row IV). Acting on a
route — sensing facts, running probes, dispatching reviews, recording verdicts, logging
disclosures — is the **F08** edge interpreter's job, modelled there (FR-016). No
`Model`/`Msg`/`Effect`/interpreter-boundary tasks here; recorded once in the evidence-obligations
note (T021).

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (with rationale on the line).
  Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and
  document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another *incomplete* task in this phase (parallel-safe hint).
- **[Story]**: `[US1]`..`[US5]`; unlabelled tasks are shared setup/foundational/polish.
- Exact file paths are given in every task.

> **File-coupling caveat.** Unlike F06 (three independent modules in separate files), F07 ships
> **one** new source file — `src/FS.GG.Governance.Kernel/Route.fs` — and **one** new test file —
> `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`. All three folds (`stakesOf`, `route`,
> `renderRoute`) live in `Route.fs`, and every story's tests live in `RouteTests.fs`. So there is
> **no genuine cross-story file parallelism** within implementation/test work: tasks editing
> `Route.fs` are sequential, and tasks editing `RouteTests.fs` are sequential, even across
> stories. `[P]` therefore marks only the genuinely different files (the `.fsi` copy, the two
> `.fsproj` edits, the `prelude.fsx` sketch, the surface baseline, and the read-only hygiene
> check). The stories remain independently *testable* (each `RouteTests` sub-list asserts its
> story's behaviour in isolation), but their *authoring* is ordered to avoid edit conflicts.

> **Build order.** `Route.*` compiles **after** `Contract.*`: `Route` references F06
> `ContractEntry`/`Contract.ofRules` (→ F04 `CheckRule`/`Severity`/`SpecSource` → F03
> `Check.render` → F02 `Verdict`) and F01 `RuleId`. `Route` and `Json` are independent, so
> `Route.fsi`/`Route.fs` are appended **after** `Json.fs` (append-only `.fsproj` edit; plan
> Structure Decision, data-model §6).

> **Scenario numbering.** Test scenarios continue the kernel's running V-series. Quickstart
> §"Validation scenarios" lists **V40–V47**; this breakdown maps them to the stories
> (V40 → US1 light-by-default; V41/V42/V46 → US2 fenced gate + drift-proof; V44 → US3 run-mode
> matrix; V43 → US4 order-independent precedence; V45/V47 → US5 reason + short/filterable/
> deterministic render) plus the cross-cutting **V11** (re-blessed surface) / **V12** (unchanged
> dependency hygiene).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the new file into the build and exercise the contract in FSI first
(Principle I — the design pass happens before any `Route.fs` body).

- [X] T001 [P] Copy the curated contract verbatim into the kernel as
  `src/FS.GG.Governance.Kernel/Route.fsi` — it must match
  `specs/007-routing-severity-modes/contracts/Route.fsi` byte-for-byte (quickstart done-when). Do
  not add `Route.fs` yet.
- [X] T002 Add `Route.fsi` and `Route.fs` to the `<Compile>` list in
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`, **after** `Json.fs`, in this
  order: `Route.fsi`, `Route.fs` (data-model §6). Create a minimal stub
  `src/FS.GG.Governance.Kernel/Route.fs` (the four types `Stakes`, `Fence<'change>`, `RunMode`,
  `Route` are declared in the `.fsi`; the `stakesOf`/`route`/`renderRoute` bodies are
  `failwith "not impl"` stubs filled in later phases) so the project compiles. No
  `private`/`internal`/`public` on any top-level binding (Principle II).
- [X] T003 [P] Add `RouteTests.fs` to the `<Compile>` list in
  `tests/FS.GG.Governance.Kernel.Tests/FS.GG.Governance.Kernel.Tests.fsproj`, **before**
  `Main.fs` (after the last existing test file). Create `RouteTests.fs` exposing an empty Expecto
  `testList "Route"` so the test project compiles and `Main` can reference it.
- [X] T004 [P] Extend `scripts/prelude.fsx` with the FSI design sketch from quickstart §"FSI
  sketch": a domain-neutral `Set<string>` change, the two declared fences (`mergeFence`,
  `secFence`), the light/fenced `stakesOf` calls, the permutation-equality check, a real F03/F04
  blocking rule, the `Gate`/`Inner` run-mode comparison, the light-at-`Gate` case, the
  `Statement = Check.render` drift check, the non-empty-reason checks, and `renderRoute`. This is
  the Principle-I design pass: if any shape is awkward, fix `Route.fsi` (T001) **before** writing
  any `Route.fs` body.

**Checkpoint**: `dotnet build` is clean with the stub; the FSI sketch type-checks against the
contract.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the four new public types every story's tests reference, and record that — like
F06 — F07 has **no shared smart constructor or hidden representation**: the three folds are total
functions over plain records/DUs and reused F02/F03/F04/F06 types.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T005 Confirm the public types declared in `src/FS.GG.Governance.Kernel/Route.fsi` compile
  and are plain (no abstract rep, no hidden state): `Stakes` (`Routine | Fenced of name: string`
  — exactly two cases, data-model §1), `Fence<'change>`
  (`{ Name: string; Trips: 'change -> bool }` — generic over the abstract change), `RunMode`
  (`Sandbox | Inner | Gate` — exactly three cases), and `Route`
  (`{ Stakes: Stakes; Advisory: ContractEntry list; Blocking: ContractEntry list; Reason: string }`
  — non-generic, drops `'change`/`'fact`, reuses F06 `ContractEntry`). The matching `Route.fs`
  carries the same declarations with **no** `private`/`internal`/`public` on any top-level
  binding (Principle II). Note for downstream tasks: there is **no** foundational constructor to
  share — `stakesOf`, `route`, and `renderRoute` are independent total folds.

**Checkpoint**: the four types are visible through the built library and referenced by the empty
`RouteTests` list; user stories can now proceed (sequentially within `Route.fs`/`RouteTests.fs`).

---

## Phase 3: User Story 1 - Light by default: an ordinary change earns no gates (Priority: P1) 🎯 MVP

**Goal**: An ordinary change tripping no declared fence is classified `Routine` and routed with
an empty `Blocking` set in every run mode, carrying a reason that says it is light because no
fence matched (FR-006, SC-001).

**Independent Test**: Route a change against a non-empty fence set none of which match (and
against the empty fence set); confirm `Stakes = Routine`, `Blocking = []`, and a non-empty
`Reason` naming "no fence matched", in `Sandbox`, `Inner`, and `Gate`.

### Tests for User Story 1 ⚠️ (write FIRST; must FAIL before T008/T009)

- [X] T006 [US1] In `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`, add **V40 (light by
  default)**: with the empty fence set and with a non-empty fence set none of which trip the
  change, assert `Route.stakesOf` returns `Routine`; and assert `Route.route` yields
  `Blocking = []` and `Stakes = Routine` in all three run modes (`Sandbox`/`Inner`/`Gate`) — a
  routine change is never escalated by run mode alone (US1 scenarios 1–2, FR-006, SC-001). Build
  real `Fence` values whose `Trips` returns `false` for the change. Tests must FAIL against the
  T002 stub.

### Implementation for User Story 1

- [X] T007 [US1] In `src/FS.GG.Governance.Kernel/Route.fs`, implement the `Routine` branch of
  `Route.stakesOf`: when no fence's `Trips` returns `true` (and for the empty fence set), return
  `Routine` (data-model R-S1/R-S3). Pure — runs each `Trips` predicate and nothing else (R-S4).
- [X] T008 [US1] In `src/FS.GG.Governance.Kernel/Route.fs`, implement the `Routine`/light path of
  `Route.route`: compute `stakes = stakesOf fences change`; when `stakes = Routine` (or
  `mode <> Gate`), every folded requirement is `Advisory` and `Blocking = []` regardless of mode;
  set a non-empty `Reason` stating the change is light because no fence matched (data-model
  R-R1/R-R4/R-R6, FR-006/FR-011). (Depends on T007.) Make V40 pass.

**Checkpoint**: V40 green — light-by-default is fully functional and independently testable; the
MVP cost-floor (ordinary change ⇒ no gates) holds.

---

## Phase 4: User Story 2 - A fenced change produces a blocking gate that names rule, fence & check (Priority: P1)

**Goal**: A change that trips a declared fence is `Fenced` (carrying the fence name), and at
`Gate` the relevant blocking-severity rule becomes a blocking gate whose explanation names the
rule, the fence, and the **rendered check** — byte-for-byte `Check.render`, no drift (FR-002/004/
012, SC-002/006). No probe or review is ever executed.

**Independent Test**: Declare a fence that matches the change; route at `Gate` with one
blocking-severity rule; confirm `Stakes = Fenced "<name>"`, `Blocking` is non-empty, the gate's
`Statement` equals `Check.render` of the rule's check, and `renderRoute` names rule + fence +
check — with no probe/agent run.

### Tests for User Story 2 ⚠️ (write FIRST; must FAIL before T011/T012)

- [X] T009 [US2] In `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`, add **V41 (single fence
  trips)** and **V42 (fenced gate explained)**: a change tripping one declared fence has
  `Route.stakesOf = Fenced "<name>"` (US2 scenario 1, FR-004, SC-002); routing it at `Gate` with
  a blocking-severity rule yields a non-empty `Blocking`, and `renderRoute` of that route names
  the rule id, the fence name(s) that raised the stakes, and the rendered check text (US2
  scenario 2, FR-012). Tests must FAIL against the stub.
- [X] T010 [US2] In `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`, add **V46 (drift-proof
  gate)**: assert `(List.head route.Blocking).Statement = Check.render rule.Check` byte-for-byte
  for the fenced-at-`Gate` route (FR-012, SC-006), and assert (V42/SC-010) that constructing and
  rendering the route runs **no** probe and dispatches **no** review — e.g. build the rule's
  `Check` from a probe whose body sets a ref cell / raises, and confirm the cell stays unset
  after `route` + `renderRoute`.

### Implementation for User Story 2

- [X] T011 [US2] In `src/FS.GG.Governance.Kernel/Route.fs`, implement the `Fenced` branch of
  `Route.stakesOf`: when ≥1 fence trips, return `Fenced name` where `name` is the **set** of
  tripped fence `Name`s — de-duplicated, ordinal-sorted, `"; "`-joined (reusing the F02
  reason-combination convention; data-model R-S1/R-S2). A single match is sufficient.
- [X] T012 [US2] In `src/FS.GG.Governance.Kernel/Route.fs`, complete `Route.route` for the
  fenced-at-`Gate` partition: fold the applicable `rules` into `ContractEntry` requirements via
  F06 `Contract.ofRules` (so each `Statement` IS `Check.render`, drift-proof), and partition an
  entry into `Blocking` iff `entry.Severity = Blocking && stakes is Fenced && mode = Gate`, else
  `Advisory`; entries stay in catalog order; set a non-empty `Reason` naming the stakes, mode,
  and outcome (data-model R-R2/R-R3/R-R4/R-R6, FR-008/010/012). Runs no probe/review (FR-016).
  (Depends on T011 and on T008's light path.) Make V41/V42/V46 pass.
- [X] T013 [US2] In `src/FS.GG.Governance.Kernel/Route.fs`, implement `Route.renderRoute`: a
  deterministic, execution-free fold producing a non-empty block — the stakes line, the `reason:`
  line, then the `blocking (n):` and `advisory (m):` sections in stable order, each header
  **always rendered with its count** (`(0)` when empty — the header is never omitted, data-model
  R-D2); each gate line names the rule id, severity, the rendered `Statement`, and the spec
  source, and blocking gates also name the fence (the `Fenced` names) that raised the stakes
  (data-model §4 R-D1/R-D2/R-D3, FR-012/014). Make the `renderRoute` assertions in V42/V46 pass.

**Checkpoint**: V41/V42/V46 green — a fenced change blocks legibly at `Gate` (rule + fence +
exact rendered check), with zero effects. US1 + US2 (both P1) now both work.

---

## Phase 5: User Story 3 - Run mode decides when a fence actually blocks (Priority: P2)

**Goal**: The same fenced change with the same blocking-severity rule surfaces as advisory in
`Sandbox`/`Inner` and as a blocking gate only in `Gate`, while the `Stakes` classification is
identical across all three modes (FR-008/009, SC-004).

**Independent Test**: Route one fenced change + one blocking rule three times (`Sandbox`, `Inner`,
`Gate`); confirm `Blocking = []` in `Sandbox`/`Inner` and non-empty only in `Gate`, and
`Stakes = Fenced` in all three.

### Tests for User Story 3 ⚠️ (write FIRST)

- [X] T014 [US3] In `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`, add **V44 (run-mode
  matrix)**: for one fenced change and one blocking-severity rule, assert `Blocking = []` in
  `Sandbox` and `Inner` (the requirement appears in `Advisory`), `Blocking` is non-empty in
  `Gate`, and `route.Stakes` is identical (`Fenced "<name>"`) across all three modes — run mode
  changes enforcement, not classification (US3 scenarios 1–3, FR-008/009, SC-004). Also assert
  the light-at-`Gate` case (a `Routine` change at `Gate` still has `Blocking = []`, FR-006).

### Implementation for User Story 3

- [X] T015 [US3] In `src/FS.GG.Governance.Kernel/Route.fs`, confirm/extend the `Route.route`
  partition so the enforcement gate `enforced = (stakes is Fenced) && (mode = Gate)` is the
  **sole** lever that moves a `Blocking`-severity requirement from `Advisory` to `Blocking`, and
  that `Stakes` is computed independently of `mode` (data-model R-R1/R-R3/R-R4, FR-009). This is
  largely realised by T012; this task is the focused verification that the matrix holds in all
  three modes (no separate per-mode code path) and the reason wording reflects the mode. Make V44
  pass.

**Checkpoint**: V44 green — enforcement (`when` it blocks) is cleanly separated from
classification (`whether` it is high-stakes); the inner loop stays fast and `Gate` enforces.

---

## Phase 6: User Story 4 - Deterministic precedence: forbid trumps permit, never positional (Priority: P2)

**Goal**: Stakes combine by a fixed precedence — `Fenced` if **any** fence trips — and the result
(and the whole route) is **identical under any permutation** of the fence list (FR-005, SC-003).
This closes hazard 5 / reinforces decision #4.

**Independent Test**: Declare a fence set where >1 fence matches; compute stakes; permute the
fence order and recompute; confirm stakes (and the full route + `renderRoute`) are identical
across all permutations, and that the change is `Fenced` whenever ≥1 fence matches.

### Tests for User Story 4 ⚠️ (write FIRST)

- [X] T016 [P] [US4] In `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`, add **V43
  (order-independent precedence)** as an FsCheck property: for a generated fence list and change,
  `Route.stakesOf (permute fences) change = Route.stakesOf fences change`, and
  `Route.route (permute fences) rules mode change = Route.route fences rules mode change`
  (including `renderRoute` equality), for every permutation — and a multi-match case yields
  `Fenced` carrying the deduped, ordinal-sorted, `"; "`-joined names (US4 scenarios 1–3, FR-005,
  SC-002/003). Use `List.rev` plus a shuffled generator so reordering is genuinely exercised.

### Implementation for User Story 4

- [X] T017 [US4] In `src/FS.GG.Governance.Kernel/Route.fs`, verify the `Route.stakesOf` `Fenced`
  name from T011 is built from the **set** of tripped names (not first-match / `List.tryFind`),
  so it is order-independent by construction (data-model R-S2; plan: the design-doc sketch used
  positional `List.tryFind` — this surface deliberately strengthens it). Confirm the route's
  partition depends only on stakes/severity/mode, never on fence position. Make V43 pass. (No new
  code expected beyond T011/T012 if they were written set-based; this task hardens and proves the
  property.)

**Checkpoint**: V43 green — multi-fence combination is deterministic by precedence, never
positional; reordering fences never changes the outcome.

---

## Phase 7: User Story 5 - Every route is short, filterable, and self-explaining (Priority: P3)

**Goal**: `Route.Blocking` is exactly the blocking-gate subset, bounded by the applicable rules
(not the catalog); every route — routine or fenced — carries a non-empty `Reason`; and
`renderRoute` is deterministic and execution-free (FR-011/013/014, SC-005/007/008).

**Independent Test**: Route a change with mixed advisory/blocking requirements; filter to
`Blocking` and confirm it is exactly the blocking gates and bounded by the applicable rules; call
`renderRoute` twice and confirm byte-for-byte equality and a non-empty render even for a
`Routine` route with no requirements.

### Tests for User Story 5 ⚠️ (write FIRST)

- [X] T018 [US5] In `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`, add **V45 (reason
  mandatory)**: every route has a non-empty `Reason` — a `Fenced`-at-`Gate` route, an advisory
  route, and a `Routine` route over the empty fence/rule set (`Route.route [] [] Inner change`)
  all assert `route.Reason <> ""` (US5, FR-011, SC-005).
- [X] T019 [US5] In `tests/FS.GG.Governance.Kernel.Tests/RouteTests.fs`, add **V47 (short,
  filterable, deterministic)**: with a mix of advisory and blocking rules at `Gate`, assert
  `Blocking` contains exactly the `Blocking`-severity entries and `Advisory` the rest, the union
  is bounded by the applicable `rules` (not the catalog; FR-013, SC-007); assert
  `renderRoute route = renderRoute route` byte-for-byte and `route` of the same input twice is
  identical (SC-008); and (re)assert no probe/review runs across these calls (SC-010). Also assert
  **totality over the empty rule set with a `Fenced` change at `Gate`**: `Route.route fences []
  Gate fencedChange` yields `Advisory = []`, `Blocking = []`, a non-empty `Reason`, and does not
  throw — the partition is total even when no rule applies to a high-stakes change (FR-015,
  SC-009).

### Implementation for User Story 5

- [X] T020 [US5] In `src/FS.GG.Governance.Kernel/Route.fs`, finalise `renderRoute` formatting for
  the short/filterable/deterministic guarantees: stable section + field order, deterministic
  empty-section handling (each section header **always rendered with its count**, `(0)` when
  empty — never omitted, per data-model R-D2), and a non-empty block for a `Routine`/no-requirement
  route (stakes + reason lines plus the two zero-count headers). Confirm `Blocking` is the
  filterable subset already produced by `route` (no re-derivation). Make V45/V47 pass.

**Checkpoint**: V45/V47 green — the route is consumable by humans and by F08/F12 without
re-deriving anything; the "always explains itself" half of the feature is concrete.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Re-bless the surface, prove the dependency hygiene, and close the artifact chain
(FR-018, SC-011).

- [X] T021 [P] Record the evidence-obligations note in this `tasks.md` (and confirm against the
  plan): Principle IV is **N/A (pure derivation)** for F07 — no `Model`/`Msg`/`Effect`/
  interpreter boundary; all evidence is real (`Fence`/`CheckRule`/`Check` values built from real
  checks, FsCheck for the order-independence/determinism/light-by-default *properties*); **no
  synthetic evidence** is anticipated or used (Principle V). If any task ends up unavoidably
  synthetic, disclose it on the task line per Principle V and `/speckit.implement`.
- [X] T022 Re-bless the kernel surface baseline: run `BLESS_SURFACE=1 dotnet test` so
  `surface/FS.GG.Governance.Kernel.surface.txt` grows to include the F07 public surface
  (`Stakes`, `Fence`, `RunMode`, `Route`, and `Route.stakesOf`/`route`/`renderRoute`); review the
  diff to confirm **only** the intended F07 symbols were added (FR-018). Depends on all of
  Phases 3–7.
- [X] T023 [P] Confirm the cross-cutting drift/hygiene tests in
  `tests/FS.GG.Governance.Kernel.Tests/SurfaceDriftTests.fs` pass against the re-blessed baseline:
  **V11** (reflected public surface = baseline, now guarding the F07 types + module) and **V12**
  (every referenced assembly `name.StartsWith "System."` or is FSharp.Core — **zero new
  dependency**, SC-011). No edit expected beyond the re-blessed baseline; this is the read-only
  verification.
- [X] T024 [P] Run the quickstart end-to-end: `dotnet build src/FS.GG.Governance.Kernel`,
  `dotnet fsi scripts/prelude.fsx` (the F07 sketch prints the expected light/fenced/run-mode/
  render outcomes), and `dotnet test` (all of V40–V47 + V11/V12 green). Confirm the printed
  outcomes match quickstart §"Expected outcomes".
- [X] T025 [P] Update the project memory / roadmap note that **F07 (007-routing-severity-modes)
  is complete and starts M2** — no packing/milestone action (M1 already packed the kernel at
  F06); the kernel still references only BCL + FSharp.Core.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately. T002 depends on T001 (the `.fsi`
  must exist before it is added to `<Compile>`); T003/T004 are `[P]` with T001/T002 (different
  files).
- **Foundational (Phase 2)**: depends on Setup. **BLOCKS all user stories** — the four types must
  be visible before any test references them.
- **User stories (Phases 3–7)**: all depend on Foundational. They are written in priority order
  (US1 → US2 → US3 → US4 → US5) and, because all implementation lands in the single `Route.fs`
  and all tests in the single `RouteTests.fs`, are **sequenced** (not parallel) to avoid edit
  conflicts — see the file-coupling caveat. US3/US4 largely *verify* the `stakesOf`/`route` code
  written in US1/US2 rather than adding new code paths.
- **Polish (Phase 8)**: T022 depends on Phases 3–7 (the `.fs` must compile with the full surface
  before re-blessing); T023/T024 depend on T022; T021/T025 are documentation, `[P]`.

### Cross-story dependencies (non-obvious)

- `Route.route` (T008, T012) calls `Route.stakesOf` (T007, T011) — the light path (T008) and the
  fenced partition (T012) build on the `stakesOf` branches.
- `renderRoute` (T013, finalised T020) consumes the `Route` produced by `route` — author after
  `route` is partitioning correctly.
- US3 (T015) and US4 (T017) assert properties of the T011/T012 code; if those were written
  set-based and mode-gated as specified, US3/US4 add **no** new `Route.fs` code — they harden and
  prove. Keep their tests (T014, T016) authored first regardless (Principle I).

### Parallel opportunities

- Setup: T001, T003, T004 are `[P]` (distinct files); T002 follows T001.
- Within a story the test task precedes the implementation task(s) (Principle I — tests FAIL
  first); they are not `[P]` with each other.
- The FsCheck property test T016 is `[P]` (it only appends a new `testList` entry independent of
  the other V-scenarios, though it shares `RouteTests.fs` — coordinate the single append).
- Polish: T021, T023, T024, T025 are `[P]` after T022.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (CRITICAL — blocks all stories).
2. Phase 3 US1 (V40) → **STOP and VALIDATE**: light-by-default holds — an ordinary change costs
   nothing in any run mode. This alone is a demonstrable, valuable increment (the feature thesis).

### Incremental delivery

1. Setup + Foundational → types visible, build green.
2. US1 (V40) → light by default — MVP.
3. US2 (V41/V42/V46) → fenced gate, legible + drift-proof + effect-free (completes both P1
   stories).
4. US3 (V44) → run-mode enforcement matrix.
5. US4 (V43) → order-independent precedence (closes hazard 5).
6. US5 (V45/V47) → short/filterable/self-explaining render.
7. Polish → re-bless surface, prove hygiene, run quickstart.

Each story is independently testable through its own `RouteTests` sub-assertions; commit after
each task or logical group.

---

## Task count & scope summary

- **Total tasks**: 25 (T001–T025).
- **Per user story**: US1 — 3 (T006–T008) · US2 — 5 (T009–T013) · US3 — 2 (T014–T015) ·
  US4 — 2 (T016–T017) · US5 — 3 (T018–T020). Shared: Setup 4 (T001–T004), Foundational 1 (T005),
  Polish 5 (T021–T025).
- **Parallel opportunities**: T001/T003/T004 (setup, distinct files); T016 (FsCheck property);
  T021/T023/T024/T025 (polish docs + read-only checks). Implementation/test work within
  `Route.fs`/`RouteTests.fs` is sequential by design (single-file coupling).
- **Suggested MVP scope**: **User Story 1** (light by default) — Phases 1–3, validated by V40.
