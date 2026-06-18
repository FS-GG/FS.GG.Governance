---
description: "Task list for F06 · 006-explanation-output — the JSON explanation serializer, the drift-proof Contract fold, and the pure evidence-freshness predicate; completes Milestone M1"
---

# Tasks: Explanation Output, the Drift-Proof Contract & Evidence Freshness — Making the Kernel's Reasoning Legible

**Input**: Design documents from `/specs/006-explanation-output/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Freshness.fsi](./contracts/Freshness.fsi), [contracts/Contract.fsi](./contracts/Contract.fsi), [contracts/Json.fsi](./contracts/Json.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a Tier 1 feature whose headline guarantees (mirror-shape +
root-verdict = `Check.eval`, byte-for-byte determinism, lossless round-trip, no-probe/opaque
serialization, the drift-proof contract that IS `Check.render`, total `ofRules`/empty catalog,
inclusive-boundary freshness over arbitrary instants, purity, six distinct evidence tokens, and
zero-dependency hygiene) are only credible with real evidence (Principle V). Per Principle I the
semantic tests are written against the **public** surface (through the built library /
`scripts/prelude.fsx`) and FAIL before the matching `.fs` bodies exist.

**Tier**: whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations —
every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A (pure derivation)** — every function maps supplied values to a
`string`/`Map`/`Freshness`. No multi-step state, no I/O, no retries, no agent call, no clock,
no background work — exactly the "simple pure function … explanation formatter" Principle IV
exempts. Reading real artifact modification times, dispatching reviews, recording verdicts, and
persisting/printing the JSON are the **F08** edge interpreter's / **F12** CLI's job, modelled
there (FR-013). No `Model`/`Msg`/`Effect`/interpreter-boundary tasks here; recorded once in the
evidence-obligations note (T023).

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (with rationale on the line).
  Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and
  document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another *incomplete* task in this phase (parallel-safe hint).
- **[Story]**: `[US1]`..`[US4]`; unlabelled tasks are shared setup/foundational/polish.
- Exact file paths are given in every task.

> **File-coupling caveat.** F06 ships **three** independent modules in **separate** source
> files — `Freshness.fs` (US3), `Contract.fs` (US2's fold), and `Json.fs` (the serializers for
> US1, US2's contract JSON, and US4) — and **three** separate test files — `FreshnessTests.fs`,
> `ContractTests.fs`, `JsonTests.fs`. So unlike F05 (one `Evidence.fs`) there is **genuine
> cross-story parallelism**: `Freshness.*` (US3) and `Contract.ofRules`/`render` (US2) are
> different files from `Json.*` and from each other. **But** `Json.fs` is touched by US1
> (explanation), US2 (`ofContract`/`toContract`), and US4 (evidence-state/effective-map), so
> tasks editing `Json.fs` are **not** `[P]` with one another even across stories — `[P]` marks
> genuinely different files (the `.fsi` copies, the `.fsproj` edits, the surface baseline, the
> read-only hygiene check, `Freshness.fs`, `Contract.fs`, and each separate test file). Stories
> stay independently *testable*; within `Json.fs`/`JsonTests.fs` the work is sequential to avoid
> edit conflicts.

> **Scenario numbering.** Test scenarios continue the kernel's running V-series. Quickstart
> §"Validation scenarios" lists **V31–V39**; this breakdown maps them to the stories
> (V31–V34 → US1 explanation JSON, V35–V36 → US2 drift-proof contract, V37–V38 → US3 freshness,
> V39 → US4 evidence-state serialization) plus the cross-cutting **V11** (re-blessed surface) /
> **V12** (unchanged dependency hygiene).

> **Build order.** The three modules compile **after** `CheckRule.*` in the order
> `Freshness.*` → `Contract.*` → `Json.*` (`Json` last): `Contract` references F04
> `CheckRule`/`Severity`/`SpecSource` + F03 `Check.render`; `Json` references F03 `Explanation`,
> F05 `EvidenceState`, and F06 `ContractEntry` (plan Structure Decision, data-model §6).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the new files into the build and exercise the three contracts in FSI first
(Principle I — the design pass happens before any `.fs` body).

- [X] T001 [P] Copy the three curated contracts verbatim into the kernel as
  `src/FS.GG.Governance.Kernel/Freshness.fsi`, `src/FS.GG.Governance.Kernel/Contract.fsi`, and
  `src/FS.GG.Governance.Kernel/Json.fsi` — each must match its
  `specs/006-explanation-output/contracts/*.fsi` byte-for-byte (quickstart done-when). Do not
  add any `.fs` yet.
- [X] T002 Add the six F06 entries to the `<Compile>` list in
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`, **after** `CheckRule.fs`, in
  this exact order: `Freshness.fsi`, `Freshness.fs`, `Contract.fsi`, `Contract.fs`, `Json.fsi`,
  `Json.fs` (so `Json` compiles last; data-model §6). Create minimal stub
  `src/FS.GG.Governance.Kernel/Freshness.fs`, `Contract.fs`, and `Json.fs` (the `Freshness` DU
  and `ContractEntry` record are declared in their `.fsi`; the `decide`/`isFresh`,
  `ofRules`/`render`, and the four `Json` serialize/parse bodies are filled in later phases) so
  the project compiles. No `private`/`internal`/`public` on any top-level binding (Principle II).
- [X] T003 [P] Add `FreshnessTests.fs`, `ContractTests.fs`, and `JsonTests.fs` to the
  `<Compile>` list in `tests/FS.GG.Governance.Kernel.Tests/FS.GG.Governance.Kernel.Tests.fsproj`,
  **before** `Main.fs` (after `EvidenceTests.fs`). Create each file exposing an empty Expecto
  `testList` (`"Freshness"`, `"Contract"`, `"Json"`) so the test project compiles and `Main` can
  reference them.
- [X] T004 [P] Extend `scripts/prelude.fsx` with the FSI design sketch from quickstart §"FSI
  design pass": build a small `All` check, `Check.explain` it, `Json.ofExplanation` + round-trip;
  fold a one-rule catalog with `Contract.ofRules` and confirm `Statement = Check.render` + JSON
  round-trip; the five `Freshness.decide` cases (`[9]`/`[10]`/`[11]`/`[]`/`[3;10;7]`); and
  `Json.ofEvidenceState AutoSynthetic` + `Json.ofEffective id eff` over a tainted graph. This is
  the Principle-I design pass: if any shape is awkward, fix the `.fsi` (T001) **before** writing
  any `.fs` body.

**Checkpoint**: `dotnet build` is clean with the empty stubs; the FSI sketch type-checks against
the three contracts.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the two new plain public types every story's tests reference, and record that —
unlike F05's abstract `EvidenceGraph` — F06 has **no shared smart constructor or hidden
representation**: the three modules are independent folds. After this phase the three stories can
proceed truly in parallel across their separate files (subject to the `Json.fs` coupling note).

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T005 Confirm the public types declared in the F06 `.fsi` compile and are plain (no abstract
  rep, no hidden state): `Freshness` (`Fresh | Stale` — exactly two cases, data-model §1) in
  `src/FS.GG.Governance.Kernel/Freshness.fsi`, and `ContractEntry`
  (`{ Id: RuleId; Severity: Severity; Spec: SpecSource; Statement: string }` — non-generic, drops
  `'fact` for domain-neutrality, FR-012) in `src/FS.GG.Governance.Kernel/Contract.fsi`. The
  matching `.fs` carry the same declarations with **no** `private`/`internal`/`public` on any
  top-level binding (Principle II). Note for downstream tasks: there is **no** foundational
  constructor to share (contrast F05 T005/T006) — `Freshness.decide`, `Contract.ofRules`, and the
  `Json.*` serializers are independent and land in their own story phases.

**Checkpoint**: the `Freshness` DU and the `ContractEntry` record compile; `dotnet build` clean
with the stubs. Each story's derivation can now be built independently.

---

## Phase 3: User Story 1 — Emit a check's explanation as stable, round-trippable JSON (Priority: P1) 🎯 MVP

**Goal**: `Json.ofExplanation` serializes an F03 `Explanation` proof tree to deterministic JSON
that mirrors the tree's surface shape — each node tagged by `kind`, atomic nodes recording probe
`name` + met/unmet/unknown `outcome`, **every** node carrying its rolled-up `verdict` (root =
`Check.eval`) — runs no probe, emits no function (an `OpaqueExplained` node is name + recorded
outcome only), and `Json.toExplanation` parses it back to an equal `Explanation` (atom/opaque
stay distinct). This is the headline of M1: the kernel's reasoning becomes portable data.

**Independent Test**: build a check of each shape, `explain` it, `ofExplanation`, and confirm the
JSON mirrors the tree, records outcomes, carries the root verdict equal to `Check.eval`; serialize
twice → byte-identical; parse back → equal to the original.

### Tests for User Story 1 (write first; must FAIL before T010/T011)

- [X] T006 [P] [US1] In `tests/FS.GG.Governance.Kernel.Tests/JsonTests.fs` add **V31**: build a
  check of each of the six shapes — `Atom` (via `Check.probe`), `All` (`.&`), `Any` (`.|`),
  `Not`, `Implies` (`==>`), and `Opaque` — `Check.explain []` each, `Json.ofExplanation`, and
  assert the JSON mirrors the proof-tree node structure (`kind` ∈
  `atom`/`opaque`/`all`/`any`/`not`/`implies`; `all`/`any` carry `parts`, `not` carries `part`,
  `implies` carries `antecedent`/`consequent` per data-model §3), each atomic node records its
  `name` + met/unmet/unknown `outcome`, **every** node carries a `verdict`, and the root node's
  `verdict` equals `Check.eval [] chk` (SC-001, FR-001).
- [X] T007 [US1] In `JsonTests.fs` add **V32**: serialize the same `Explanation` twice with
  `Json.ofExplanation` and assert the two strings are **byte-for-byte identical** — fixed object-
  key order, structural array order (SC-002, FR-003). Same file as T006 → sequential.
- [X] T008 [US1] In `JsonTests.fs` add **V33** (FsCheck property, Expecto.FsCheck): for arbitrary
  generated `Explanation` trees, `Json.toExplanation (Json.ofExplanation e) = e` — lossless
  round-trip, `AtomExplained` and `OpaqueExplained` stay distinct (SC-003, FR-004, R-J4). Provide
  an `Arbitrary` that builds nested `Explanation` nodes of every kind with arbitrary
  reason/verdict strings. Same file → after T007.
- [X] T009 [US1] In `JsonTests.fs` add **V34**: serialize an explanation containing an
  `OpaqueExplained` node and assert it is emitted by `name` + recorded `outcome` only — **no
  function is serialized and no probe `Eval` runs** during serialization (construct the source
  `Opaque` check from a probe whose `Eval` would throw, `explain` it once, then assert
  `ofExplanation` of the resulting tree neither throws nor re-invokes the probe) (SC-004, FR-002,
  R-J2). Same file → after T008.

### Implementation for User Story 1

- [X] T010 [US1] In `src/FS.GG.Governance.Kernel/Json.fs` implement the shared internal
  tag-discriminated encoders/decoders for `Outcome` and `Verdict` over `System.Text.Json`
  (`Utf8JsonWriter` emit, `JsonDocument` read): `Met → {"tag":"met"}`,
  `Unmet r → {"tag":"unmet","reason":r}`, `Unknown r → {"tag":"unknown","reason":r}`;
  `Pass → {"tag":"pass"}`, `Fail r → {"tag":"fail","reason":r}`,
  `Uncertain r → {"tag":"uncertain","reason":r}` (data-model §3, research D4). Members written in
  the fixed order shown; parse reads fields by name and fails fast on an unknown `tag`
  (Principle VI, R-J5). No visibility modifiers on top-level bindings.
- [X] T011 [US1] In `src/FS.GG.Governance.Kernel/Json.fs` implement `Json.ofExplanation` (a
  `let rec` `Utf8JsonWriter` walk emitting `{"kind":…}` objects per data-model §3, reusing the
  T010 outcome/verdict encoders; non-indented/compact, fixed key order) and `Json.toExplanation`
  (a `let rec` `JsonDocument` walk dispatching on `kind`, reconstructing the exact case so
  `atom`/`opaque` stay distinct). Total over kernel-emitted JSON; malformed/foreign JSON fails
  fast with the `System.Text.Json` exception (Principle VI). Reuses F03 `Explanation` only; runs
  no `Eval`. No visibility modifiers. Makes V31–V34 pass.

**Checkpoint**: MVP — `dotnet test` green for V31–V34; an `Explanation` round-trips through stable
JSON with the root verdict = `Check.eval`. STOP and validate independently.

---

## Phase 4: User Story 2 — Generate the published rule contract as a drift-proof fold (Priority: P1)

**Goal**: `Contract.ofRules` folds an F04 `CheckRule<'fact>` catalog into one `ContractEntry` per
rule (catalog order) carrying `Id`/`Severity`/`Spec` and a `Statement` that **is**
`Check.render rule.Check` — the single source, so the contract cannot drift; `Contract.render`
emits it as deterministic human/agent text; `Json.ofContract`/`toContract` emit + round-trip the
JSON form. Total over the empty catalog.

**Independent Test**: fold a small catalog; assert each entry's `Statement = Check.render` of that
rule's check; mutate a rule's check → its entry changes; reorder the catalog → each rule's own
entry is unchanged; `ofRules [] = []`; contract JSON round-trips.

### Tests for User Story 2 (write first; must FAIL before T014/T015)

- [X] T012 [P] [US2] In `tests/FS.GG.Governance.Kernel.Tests/ContractTests.fs` add **V35**: build
  a small catalog of reified `CheckRule`s (via `CheckRule.rule`/`blocking` as in the quickstart),
  fold with `Contract.ofRules`, and assert (a) one entry per rule in catalog order, each carrying
  the rule's `Id`/`Severity`/`Spec` and `Statement = Check.render rule.Check` (FR-005/006,
  SC-005, R-C1/R-C2); (b) mutating a rule's `Check` changes **that** entry's `Statement`
  accordingly (tracks the selector, cannot drift); (c) reordering the catalog leaves each rule's
  own entry unchanged (per-rule rendering, SC-005). Different file from US1 → `[P]`.
- [X] T013 [US2] In `ContractTests.fs` add **V36**: assert `Contract.ofRules [] = []` and
  `Contract.render [] = ""` (total over the empty catalog, FR-007, SC-006, R-C3); `ofRules` is
  deterministic (same catalog twice → identical); and the contract JSON round-trips —
  `Json.toContract (Json.ofContract c) = c` for a non-empty `c` and `Json.ofContract [] = "[]"`
  (FR-007, SC-003, R-J4). Same file as T012 → sequential. (Round-trip depends on T015.)

### Implementation for User Story 2

- [X] T014 [P] [US2] In `src/FS.GG.Governance.Kernel/Contract.fs` implement `Contract.ofRules`
  (`List.map` each `CheckRule` to a `ContractEntry { Id = rule.Id; Severity = rule.Severity;
  Spec = rule.Spec; Statement = Check.render rule.Check }`, preserving catalog order — drift-proof
  because `Statement` IS the rendered selector, never a separate string; total over `[]`,
  FR-005/006/007, R-C1..R-C3) and `Contract.render` (one deterministic stanza per entry naming
  id/severity/spec/statement; `[]` → `""`, FR-007). Runs no probe, performs no I/O (R-C4).
  Different file (`Contract.fs`) → `[P]` with the `Json.fs` work. No visibility modifiers.
- [X] T015 [US2] In `src/FS.GG.Governance.Kernel/Json.fs` implement `Json.ofContract` (a
  `Utf8JsonWriter` array of `{"id":…,"severity":"advisory"|"blocking","spec":{"document":…,
  "section":…},"statement":…}` objects in catalog order, `RuleId (RuleId s)` emitting the inner
  `s`, fixed key order, data-model §3) and `Json.toContract` (parse back to an equal
  `ContractEntry list`; fail fast on malformed input). Total over the empty contract
  (`"[]"` ⇄ `[]`). Touches `Json.fs` → **not** `[P]` with T010/T011. No visibility modifiers.
  Makes V36's round-trip assertion pass.

**Checkpoint**: US1 + US2 work independently; the published contract is the rendered selector
itself and round-trips as text and JSON. Both P1 stories deliver — reasoning and the enforced
contract are now portable data.

---

## Phase 5: User Story 3 — Decide whether recorded evidence is still fresh (Priority: P2)

**Goal**: `Freshness.decide recorded covered` returns `Fresh` iff `recorded ≥` every instant in
`covered` (equivalently `recorded ≥ max covered`; empty `covered` ⇒ `Fresh`; boundary inclusive),
`Stale` otherwise — a pure function of the supplied `'instant : comparison` values reading no
clock/filesystem/git/network; `isFresh` is the boolean convenience.

**Independent Test**: `decide T [T-1] = Fresh`; `decide T [T+1] = Stale`; `decide T [T] = Fresh`
(inclusive tie); `decide T [] = Fresh`; multi-artifact fresh iff `recorded ≥` the latest covered
instant; identical inputs → identical results regardless of when evaluated.

### Tests for User Story 3 (write first; must FAIL before T018)

- [X] T016 [P] [US3] In `tests/FS.GG.Governance.Kernel.Tests/FreshnessTests.fs` add **V37**:
  assert `Freshness.decide 10 [9] = Fresh`, `decide 10 [11] = Stale`, `decide 10 [10] = Fresh`
  (inclusive boundary, FR-009, R-F3), `decide 10 [] = Fresh` (covers nothing, FR-009, R-F2), and
  multi-artifact `decide 10 [3;10;7] = Fresh` / `decide 10 [3;11;7] = Stale` — fresh iff
  `recorded ≥` the latest covered instant (FR-008, SC-007, R-F1). Different file → `[P]`.
- [X] T017 [US3] In `FreshnessTests.fs` add **V38** (FsCheck property, Expecto.FsCheck): for
  arbitrary `recorded` and `covered` instant lists, `decide` is a pure function of the instants —
  equal inputs give equal results, `decide recorded covered = Fresh` iff `covered = [] ||
  recorded >= List.max covered`, and `isFresh recorded covered = (decide recorded covered =
  Fresh)` (SC-008, FR-010, R-F4). Same file as T016 → sequential.

### Implementation for User Story 3

- [X] T018 [P] [US3] In `src/FS.GG.Governance.Kernel/Freshness.fs` implement `Freshness.decide`
  (`match covered with [] -> Fresh | _ -> if recorded >= List.max covered then Fresh else Stale`,
  using only the `'instant : comparison` constraint — no clock, no I/O, total for every input,
  FR-008/009/010, R-F1..R-F4) and `Freshness.isFresh recorded covered = (decide recorded covered =
  Fresh)`. Different file (`Freshness.fs`) → `[P]` with all other implementation tasks. No
  visibility modifiers. Makes V37–V38 pass.

**Checkpoint**: US1–US3 work independently; the evidence model is now temporally honest —
silently stale evidence is caught by a pure, clock-free predicate.

---

## Phase 6: User Story 4 — Serialize evidence states for the evidence report (Priority: P3)

**Goal**: `Json.ofEvidenceState` maps each of the six F05 `EvidenceState` cases to a distinct,
stable JSON string token (the computed-only `AutoSynthetic → "autoSynthetic"`, never merged with
`synthetic`); `Json.toEvidenceState` maps it back (fail fast on an unknown token); and
`Json.ofEffective project states` serializes an effective-state map to a JSON object keyed by the
**supplied** projection of each node id, keys **ordinal-sorted** for byte-for-byte determinism;
`Json.toEffective` parses back to the equal projected `Map<string, EvidenceState>`.

**Independent Test**: each of the six states serializes to a distinct token that round-trips;
compute `Evidence.effective` over a tainted graph (F05), serialize the node→state map with a
projection, and confirm every node (incl. `AutoSynthetic`) is present and the JSON round-trips to
the equal projected map.

### Tests for User Story 4 (write first; must FAIL before T020)

- [X] T019 [US4] In `tests/FS.GG.Governance.Kernel.Tests/JsonTests.fs` add **V39**: assert each
  of the six `EvidenceState` cases serializes via `Json.ofEvidenceState` to its distinct stable
  token (`"pending"`/`"real"`/`"synthetic"`/`"failed"`/`"skipped"`/`"autoSynthetic"`, data-model
  §3) and round-trips (`toEvidenceState (ofEvidenceState s) = s`, FR-011, SC-003); build a small
  tainted graph with `Evidence.build`, compute `Evidence.effective`, serialize with
  `Json.ofEffective id`, and assert every node — including the `AutoSynthetic` one — is present,
  keys are ordinal-sorted (byte-for-byte deterministic, SC-002), and
  `Json.toEffective (Json.ofEffective id eff)` equals the projected original map (FR-011, SC-003,
  R-J4). Touches `JsonTests.fs` → **not** `[P]` with US1's tests; runs after T011/T015 land in
  `Json.fs`.

### Implementation for User Story 4

- [X] T020 [US4] In `src/FS.GG.Governance.Kernel/Json.fs` implement `Json.ofEvidenceState` /
  `Json.toEvidenceState` (the six-token map of data-model §3 / research D5; emit a quoted JSON
  string; parse fails fast on an unrecognized token, R-J5) and `Json.ofEffective project states`
  / `Json.toEffective`: emit a JSON object `{ "<project id>":"<token>", … }` with keys produced
  by the supplied `project: 'id -> string` and **ordinal-sorted** before writing so output is
  deterministic regardless of `Map` internal ordering (FR-003/011/012, SC-002, R-J3); parse back
  to `Map<string, EvidenceState>` keyed by the projected string. Touches `Json.fs` → **not**
  `[P]` with T011/T015. No visibility modifiers. Makes V39 pass.

**Checkpoint**: all four stories independently testable; F05's declared and computed evidence
states appear in the same JSON report as the explanation, with `AutoSynthetic` visibly marked.

---

## Phase 7: Polish, Cross-Cutting Concerns & M1 Exit

**Purpose**: surface discipline, dependency hygiene, the done-when gate, and the **Milestone M1**
exit (packing the kernel to the local feed).

- [X] T021 Re-bless the API surface baseline:
  `surface/FS.GG.Governance.Kernel.surface.txt` must grow to include the F06 types and modules —
  `Freshness` (the DU + `decide`/`isFresh`), `ContractEntry` + the `Contract` module
  (`ofRules`/`render`), and the `Json` module (the four `of*`/`to*` pairs). Run
  `BLESS_SURFACE=1 dotnet test`, confirm the diff is **exactly** the F06 additions, and commit it
  (FR-014, V11 re-blessed, plan Principle II). While reviewing the diff, confirm the added names
  carry **no domain vocabulary** — node identity for the effective map is via the supplied
  `'id -> string` projection, instants are generic `'instant : comparison` (FR-012, manual
  review). The existing V11 surface-drift test then guards the F06 surface for free.
- [X] T022 [P] Confirm the existing **V12 dependency-hygiene** test (`SurfaceDriftTests.fs`) still
  passes unchanged — the kernel assembly references only BCL/`System.*` + FSharp.Core after
  `Json.*` is added. `System.Text.Json` satisfies `name.StartsWith "System."`, so **zero**
  `<PackageReference>` is added to
  `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj` (FR-012, SC-009, research D1/D7).
  Read-only check → `[P]` alongside T021. (Optionally pin research D1 by asserting
  `typeof<System.Text.Json.Utf8JsonWriter>.Assembly` is a `System.*` assembly.)
- [X] T023 Run the quickstart done-when gate end-to-end: `dotnet build` clean; `dotnet test`
  green (existing 55 + the new V31–V39 + inherited V11 surface-drift + V12 dependency-hygiene);
  confirm each `src/FS.GG.Governance.Kernel/{Freshness,Contract,Json}.fsi` still matches its
  `specs/006-explanation-output/contracts/*.fsi` byte-for-byte and the matching `.fs` carry no
  `private`/`internal`/`public` on top-level bindings; walk the FSI sketch (T004) once more to
  confirm SC-009 (the whole surface is exercised through the public API with nothing beyond the
  base runtime + F01/F03/F04/F05). Record the **evidence-obligations note**: F06 is a pure
  derivation, so Principle IV (Elmish/MVU) is **N/A** (the persisting/printing edge is F08/F12);
  all evidence is **real** — real `Explanation`/`CheckRule`/`EvidenceGraph` values built from real
  checks and declared states, with FsCheck for the round-trip (V33) / purity (V38) *properties* —
  no synthetic fixtures, no `// SYNTHETIC:` disclosures.
- [X] T024 **M1 exit — pack the kernel to the local feed** (research D8, spec Assumptions §Exit):
  flip `IsPackable` to `true` for `src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`
  only (the default is `false` from `Directory.Build.props`; do **not** flip the test project),
  and `dotnet pack` it into `~/.local/share/nuget-local/`. This changes packaging metadata only —
  no `.fsi`, no behaviour, no new dependency. Completing this task completes **Milestone M1 — the
  first useful product** (a pure kernel that stores facts, evaluates rules to a fixed point with
  provenance, taints synthetic evidence, and emits JSON explanations + a drift-proof contract +
  freshness, with zero heavy dependencies).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1 — confirms the two plain public types. Unlike
  F05 there is **no** heavy shared constructor, so this phase is thin; it still BLOCKS the stories
  only in that their tests reference `Freshness`/`ContractEntry`.
- **User stories (Phases 3–6)**: each depends on Foundational. They are independently *testable*
  **and** largely parallel across files — `Freshness.fs` (US3) and `Contract.fs` (US2's fold) are
  separate files from `Json.fs` and each other. The single coupling is `Json.fs`, edited by US1
  (T010/T011), US2 (T015), and US4 (T020): those `Json.fs` tasks serialize among themselves.
  Implement in priority order (US1 → US2 → US3 → US4) for a clean MVP-first increment.
- **Polish + M1 exit (Phase 7)**: depends on all desired stories; T021 (surface re-bless) runs
  after the public surface is final (fixed at T001, re-blessed once the `.fs` build); T024 (pack)
  is the milestone exit and runs last.

### Cross-story / cross-task dependencies

- **US1's `Json.fs` (T010 shared encoders → T011 explanation)** lands the `Outcome`/`Verdict`
  tag-object helpers first; T011 reuses them. T011 → T010 (same file).
- **US2** splits across two files: `Contract.fs` (T014, `[P]`) and `Json.fs` (T015, contract
  JSON). V36's round-trip assertion (T013) depends on T015.
- **US4** (T020) edits `Json.fs` after T011/T015; V39 (T019) depends on T020.
- Same-file ordering (edit-conflict, not logical coupling): within `Json.fs`,
  T010 → T011 → T015 → T020; within each test file the V-scenarios follow in listed order.
- **US3** (T016–T018) is fully independent (`Freshness.fs`/`FreshnessTests.fs`) and may be done at
  any point after Foundational.

### Parallel opportunities

- **Phase 1**: T001, T003, T004 are `[P]` (different files); T002 follows T001.
- **Across stories**: T014 (`Contract.fs`) and T018 (`Freshness.fs`) are `[P]` with the `Json.fs`
  work and with each other; the test-authoring tasks T006/T012/T016 are `[P]` (three different
  test files). The `Json.fs` tasks (T010/T011/T015/T020) and the `JsonTests.fs` tasks
  (T006→T007→T008→T009, T019) are sequential within their file.
- **Phase 7**: T022 (read-only hygiene check) is `[P]` alongside T021 (surface baseline); T023
  then T024 run last.

---

## Task count & MVP

- **Setup**: 4 (T001–T004)
- **Foundational**: 1 (T005) — the `Freshness` DU + the `ContractEntry` record (no shared
  constructor)
- **US1 (P1, MVP)**: 6 — V31–V34 tests + shared outcome/verdict encoders + `ofExplanation`/
  `toExplanation` (T006–T011)
- **US2 (P1)**: 4 — V35/V36 tests + `Contract.ofRules`/`render` + `Json.ofContract`/`toContract`
  (T012–T015)
- **US3 (P2)**: 3 — V37/V38 tests + `Freshness.decide`/`isFresh` (T016–T018)
- **US4 (P3)**: 2 — V39 test + the evidence-state/effective-map serializers (T019–T020)
- **Polish + M1 exit**: 4 (T021–T024) — surface re-bless, dependency hygiene, the done-when gate
  + evidence note, and the M1 pack
- **Total**: 24 tasks.

**Suggested MVP scope**: Phases 1–3 (Setup + Foundational + **User Story 1**) — an `Explanation`
proof tree serializes to deterministic, round-trippable JSON whose root verdict equals
`Check.eval`, runs no probe, and keeps `atom`/`opaque` distinct. This is the headline of M1: the
kernel's reasoning becomes portable, inspectable data. US2 (the drift-proof contract) is co-equal
P1 and lands next; US3 (freshness) and US4 (evidence-state serialization) round out M1, after
which T024 packs the kernel to complete the milestone.
