---
description: "Task list for F09 · 009-adapter-spi — the adapter SPI & composition root: a domain plugs in by supplying only its own vocabulary; lifting is faithful and composition is deterministic & order-independent; starts Milestone M3 (the adoption bar)"
---

# Tasks: The Adapter SPI & Composition Root — A Domain Plugs In By Supplying Only Its Own Vocabulary

**Input**: Design documents from `/specs/009-adapter-spi/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Adapter.fsi](./contracts/Adapter.fsi), [contracts/Composition.fsi](./contracts/Composition.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature whose headline guarantees — the five-part SPI
being **total** (a missing component does not compile), an example adapter governing itself with
**100 % kernel reuse** (SC-001), **faithful lifting** (a lifted rule's `(verdict, provenance)` and
its `render`/`hash`/`reads`/`isReified` byte-for-byte identical to the standalone original,
SC-002), **deterministic, order-independent** cross-domain composition (every permutation yields an
identical least fixed point and merged route, SC-003/SC-007), the **removal/boundary** test
(SC-004), **two unrelated** domains adopting with zero cross-copying (SC-005), the composed catalog
running through the **unchanged** kernel (SC-006), and the **new** Spi surface-drift +
dependency-hygiene baseline (SC-008) — are only credible with real evidence (Principle V). Per
Principle I the semantic tests are written against the **public** surface (through the built
`FS.GG.Governance.Adapters.Spi` library / `scripts/prelude.fsx`) and MUST FAIL before the matching
`Adapter.fs`/`Composition.fs` bodies exist.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations —
every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A — PASS** (Constitution Principle IV; plan Constitution Check row IV). This is
a **pure** value/fold layer — no multi-step state, no I/O, no `Model`/`Msg`/`Effect`, no
interpreter (FR-013), exactly as for F01–F07. There are therefore **no** `Model`/`Msg`/`Effect`/
`init`/`update`/interpreter tasks; the evidence-obligations note (T010) records this N/A explicitly.
Wiring a *composed* catalog into a running loop is the already-shipped F08 effects shell and the
F12 CLI, not this feature.

**Synthetic-evidence discipline (Principle V).** F09 ships **no concrete production adapter** (F10
delivers Spec Kit, F11 the design system). Generality is proven by **two unrelated, neutral example
adapters** authored in the test project (`tests/.../ExampleAdapters.fs`). These are **synthetic
example domains** — illustrative, not real adopters — so: **each example adapter MUST carry a
`// SYNTHETIC: example domain — illustrative, not a real adopter (real adapters are F10/F11)`
comment at its definition, and every test asserting through them MUST carry the token `Synthetic`
in its test name**, and the example domains MUST be listed in the PR description. Everything else
(the kernel facts/rules fed through the built library) is **real** evaluation and needs no marker.

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (with rationale on the line).
  Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and
  document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — different file, no dependency on another incomplete task in the phase.
- **[Story]**: the user story the task serves (US1–US5); omitted for setup/foundational/polish.
- Every task names an exact file path.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: stand up the new pure project, its test project, and the curated contracts so the
whole surface type-checks against `failwith`-stubs before any real body exists (Principle I).

- [X] T001 Create the new pure library project `src/FS.GG.Governance.Adapters.Spi/FS.GG.Governance.Adapters.Spi.fsproj` — `net10.0` (inherits `Directory.Build.props`), a **single** `ProjectReference` to `../FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj`, and **ZERO** `PackageReference` (BCL + `FSharp.Core` only, FR-015). Compile order in the fsproj: `Adapter.fsi`, `Adapter.fs`, `Composition.fsi`, `Composition.fs`.
- [X] T002 Copy `specs/009-adapter-spi/contracts/Adapter.fsi` → `src/FS.GG.Governance.Adapters.Spi/Adapter.fsi` and `specs/009-adapter-spi/contracts/Composition.fsi` → `src/FS.GG.Governance.Adapters.Spi/Composition.fsi` verbatim (the curated public surface, Principle II — these are the SOLE visibility declaration).
- [X] T003 Add `failwith`-stub bodies `src/FS.GG.Governance.Adapters.Spi/Adapter.fs` and `src/FS.GG.Governance.Adapters.Spi/Composition.fs` that satisfy the two `.fsi` (type declarations real where cheap — the records — function bodies `failwith "F09"`), so `dotnet build src/FS.GG.Governance.Adapters.Spi` compiles. NO `private`/`internal`/`public` on top-level bindings (Principle II).
- [X] T004 [P] Create the test project `tests/FS.GG.Governance.Adapters.Spi.Tests/FS.GG.Governance.Adapters.Spi.Tests.fsproj` — Expecto + FsCheck (centrally pinned), a `ProjectReference` to `../../src/FS.GG.Governance.Adapters.Spi/...`, plus `tests/FS.GG.Governance.Adapters.Spi.Tests/Main.fs` (Expecto `runTestsInAssembly` entry point).
- [X] T005 [P] Extend `scripts/prelude.fsx` with the F09 FSI sketch from [quickstart.md](./quickstart.md) (author a toy adapter → govern standalone → faithful lift → compose two → removal), drafted against the two `.fsi` with `failwith`/`Unchecked` where a body is absent. The point of this pass is that the SHAPES type-check against the contracts.
- [X] T006 Add `src/FS.GG.Governance.Adapters.Spi` and `tests/FS.GG.Governance.Adapters.Spi.Tests` to `FS.GG.Governance.sln`.

**Checkpoint**: `dotnet build` is clean with the stubs; `dotnet fsi scripts/prelude.fsx` type-checks
the F09 sketch against the two contracts; the solution lists the two new projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: declare the public value types every story shares, and author the two unrelated
synthetic example domains + the consumer-side coproduct that all the semantic tests evaluate
through. **No user-story work can begin until this phase is complete.**

- [X] T007 Declare the `Adapter<'fact,'artifact,'change>` record (six fields: `Identify`, `ToRef`, `Probes`, `Rules`, `Fences`, `Bridge`) in `src/FS.GG.Governance.Adapters.Spi/Adapter.fs` exactly per `Adapter.fsi` — the totality guarantee (a missing field does not compile, FR-014) is the record itself.
- [X] T008 Declare the `Lifted<'project,'change>` (`{ Rules; Fences }`) and `Composed<'project,'change>` (`{ Catalog; Fences }`) records in `src/FS.GG.Governance.Adapters.Spi/Composition.fs` exactly per `Composition.fsi`.
- [X] T009 [P] Author `tests/FS.GG.Governance.Adapters.Spi.Tests/ExampleAdapters.fs` — **two UNRELATED** neutral toy domains (distinct vocabularies, artifact kinds, probes, fences — e.g. a "document" domain and an unrelated "task" domain) each as an `Adapter<…>` value, PLUS the consumer-authored closed `ProjectFact` coproduct with its `Governance of RuleOutcome` case, the single-case active patterns (`(|DocP|_|)`, `(|TaskP|_|)`), the `inject` constructors, and the project `Identify`/`Bridge`. **Each example domain carries the `// SYNTHETIC: example domain …` disclosure comment** (Principle V). Note in the file that `Probes` is the **declared** probe vocabulary (carried for the contract and for testing); the `Rules`' checks are **authoritative** for evaluation (research D2) — `Probes` is not separately evaluated, so a rule's check may embed a probe the `Probes` list also names. This file is the shared fixture for US1–US5. Depends on T007 (the `Adapter` type) only.
- [X] T010 Record the **evidence-obligations** note at the top of `tests/FS.GG.Governance.Adapters.Spi.Tests/ExampleAdapters.fs` (a comment block): (a) **Principle IV is N/A** — pure value/fold layer, no `Model`/`Msg`/`Effect`/interpreter (no boundary tasks owed); (b) the **synthetic-disclosure** rule for the two example domains (token `Synthetic` in asserting tests; listed in the PR description) — note that the example adapters ARE the system-under-test for a generic SPI (not substitutes for a real dependency), so the synthetic framing is a **deliberate, conservative** application of Principle V (the real adopters are F10/F11); (c) the **real-evaluation** path (kernel facts/rules through the built library) needs no marker.

**Checkpoint**: both `.fs` declare the three public records against their `.fsi`; the two unrelated
synthetic example adapters + the `ProjectFact` coproduct compile and are ready for every story to
evaluate through; the N/A-Principle-IV and synthetic disclosures are written down.

---

## Phase 3: User Story 1 — A domain plugs in by supplying only five things; everything else is reused (Priority: P1) 🎯 MVP

**Goal**: the five-part SPI is total, and an example adapter governs its own artifacts —
deriving facts, evaluating rules, rendering an explanation — using **only** kernel facilities, with
**no** inference/arbitration/evidence/render/hash/severity/routing code of its own (SC-001).

**Independent Test**: author a single example adapter that supplies only the five components +
`Bridge`; confirm it governs itself end-to-end through the kernel and contains no cross-cutting
code of its own.

### Tests for User Story 1 ⚠️ (write FIRST; must FAIL before T013)

- [X] T011 [P] [US1] **V61** in `tests/FS.GG.Governance.Adapters.Spi.Tests/AdapterTests.fs`: the five-part contract is **total** — assert an `Adapter` value carries exactly the five components + `Bridge`, and document (a comment-guarded, intentionally-commented snippet) that omitting a field **does not compile** (FR-001/FR-014, SC-001). Carries the `Synthetic` token (drives an example adapter).
- [X] T012 [P] [US1] **V62** in `tests/FS.GG.Governance.Adapters.Spi.Tests/AdapterTests.fs`: the example adapter **governs itself through kernel entry points** — assert `Adapter.toRules` → `FixedPoint.evaluate adapter.Identify … supplied` derives the expected facts, `Route.route adapter.Fences adapter.Rules mode change` produces the expected route, and `Check.render`/`Check.explain` produce the expected explanation (FR-002, SC-001). The "reuses 100 % of kernel facilities — **contains no inference/arbitration/render/hash/route code of its own**" claim is a **structural** property: verify it by **inspection / a dependency-symbol check** (the example adapter's definitions call only `FS.GG.Governance.Kernel` + SPI APIs) recorded as a review note in the test file header — NOT as a behavioral runtime assertion. Carries the `Synthetic` token.

### Implementation for User Story 1

- [X] T013 [US1] Implement `Adapter.toRules : Adapter<'fact,'a,'c> -> Rule<'fact> list` in `src/FS.GG.Governance.Adapters.Spi/Adapter.fs` as the thin reuse `adapter.Rules |> List.map (CheckRule.toRule adapter.Bridge)` — the UNCHANGED F04 bridge; no new cross-cutting code (FR-002). Total; no I/O.

**Checkpoint**: US1 is independently testable — a single adapter that supplies only its own
vocabulary governs itself entirely through the kernel (the thesis of the adoption bar). **MVP.**

---

## Phase 4: User Story 2 — An adapter's rules lift into the project coproduct and evaluate unchanged (Priority: P1)

**Goal**: the semantics-preserving lift — `Check.render`/`hash`/`reads`/`isReified` **invariant**
under lifting (the F04 cache key does not move), and a lifted rule's `(verdict, provenance)`
**byte-for-byte identical** to the standalone original (FR-004, SC-002); a lifted `Opaque`/
`AgentReviewed` rule stays out of `Deterministic` and routes to review.

**Independent Test**: evaluate each of an example adapter's rules twice — once over the domain fact
type, once over the lifted rule over the coproduct carrying the same facts — and confirm the two
outcomes (verdict + provenance) and the `render`/`hash` are identical for every rule.

### Tests for User Story 2 ⚠️ (write FIRST; must FAIL before T018–T021)

- [X] T014 [P] [US2] **V64** in `tests/FS.GG.Governance.Adapters.Spi.Tests/AdapterTests.fs`: lifting is **render/hash/reads/isReified invariant** — for every example rule, `Check.render`/`Check.hash`/`Check.reads`/`Check.isReified` of `Lift.check (|DocP|_|) c` equal the original `c` byte-for-byte (the cache key does not move — law L1, SC-002). `Synthetic`.
- [X] T015 [P] [US2] **V63** in `tests/FS.GG.Governance.Adapters.Spi.Tests/AdapterTests.fs`: lifting is **verdict+provenance faithful** — for 100 % of the example adapter's rules, evaluating `Lift.rule inject (|DocP|_|) r` over coproduct-wrapped facts yields the IDENTICAL `(FactId, Verdict, ProvenanceStep)` as evaluating `r` over the domain facts, given the project `Identify` agrees on injected facts (law L2/L3, SC-002). `Synthetic`.
- [X] T016 [P] [US2] **V65** in `tests/FS.GG.Governance.Adapters.Spi.Tests/AdapterTests.fs`: a lifted `Opaque`/`AgentReviewed` rule stays out of the `Deterministic` tier and routes to review exactly as un-lifted (tier + opacity preserved — law L4, US2-3). `Synthetic`.

### Implementation for User Story 2

- [X] T017 [US2] Implement `Lift.check : ('big -> 'small option) -> Check<'small> -> Check<'big>` in `src/FS.GG.Governance.Adapters.Spi/Adapter.fs`: project each atomic/opaque probe's `Eval` fact set (keeping every `FactAssertion`'s `Id`/`Provenance`, re-mapping only `Value`), leaving probe `Name`/`Reads`/`Args` and the `All`/`Any`/`Not`/`Implies`/`Opaque` structure UNTOUCHED so the four execution-free interpreters are invariant (contravariant — law L1/L2). Total.
- [X] T018 [US2] Implement `Lift.checkRule : ('big -> 'small option) -> CheckRule<'small> -> CheckRule<'big>` in `src/FS.GG.Governance.Adapters.Spi/Adapter.fs`: preserve `Id`/`Tier`/`Spec`/`Severity`/`Question`, lift ONLY the `Check` via `Lift.check project` (law L4). Total.
- [X] T019 [US2] Implement `Lift.rule : ('small -> 'big) -> ('big -> 'small option) -> Rule<'small> -> Rule<'big>` in `src/FS.GG.Governance.Adapters.Spi/Adapter.fs` (the docs' `Rule.contramapFacts`): project the input facts, run the inner `Apply`, then `inject` each produced assertion's `Value` back to `'big` keeping its `Id`/`ProvenanceStep` verbatim (invariant; provenance-preserving — law L3). Total.
- [X] T020 [US2] Implement `Lift.fence : ('big -> 'small) -> Fence<'small> -> Fence<'big>` in `src/FS.GG.Governance.Adapters.Spi/Adapter.fs`: re-target `Trips` via `Trips << narrow`, KEEP `Name` (so the composed fence set dedups by name — law L5). Total.
- [X] T021 [US2] Implement `Composition.lift : ('project -> 'dom option) -> ('change -> 'domChange) -> Adapter<'dom,'a,'domChange> -> Lifted<'project,'change>` in `src/FS.GG.Governance.Adapters.Spi/Composition.fs`: `Rules = adapter.Rules |> List.map (Lift.checkRule project)`, `Fences = adapter.Fences |> List.map (Lift.fence narrow)`. Depends on T018+T020. Total; pure.

**Checkpoint**: US1 + US2 work — a domain author's rules lift into a coproduct and evaluate to the
identical verdict, provenance, render, and hash they produced standalone. The lift adds no
behaviour; the cache key is stable.

---

## Phase 5: User Story 3 — Independent adapters compose at one root with explicit, deterministic, order-independent cross-domain coupling (Priority: P1)

**Goal**: the composition root assembles lifted adapters + a small, named cross-domain `Implies`
set into one catalog the **unchanged** kernel evaluates (FR-005, SC-006), with a deduped-by-name
fence union and a `max` composed tier (FR-011); the merged verdict and least fixed point are
**order-independent**, and a blocking result wins regardless of position (FR-007/FR-008,
SC-003/SC-007).

**Independent Test**: compose two example adapters + one cross-domain `Implies` rule; evaluate under
every permutation of adapter-composition order and rule order; confirm an identical least fixed
point and merged route, and that a blocking result from any adapter wins regardless of position.

### Tests for User Story 3 ⚠️ (write FIRST; must FAIL before T026/T027)

- [X] T022 [P] [US3] **V66** in `tests/FS.GG.Governance.Adapters.Spi.Tests/CompositionTests.fs`: the composed catalog evaluates via the UNCHANGED `Composition.toRules bridge composed = composed.Catalog |> List.map (CheckRule.toRule bridge)` → `FixedPoint.evaluate` — assert the kernel gains no adapter-specific code and the dependency direction holds (law C1, FR-005, SC-006). `Synthetic`.
- [X] T023 [P] [US3] **V67** in `tests/FS.GG.Governance.Adapters.Spi.Tests/CompositionTests.fs`: a single cross-domain `Implies` rule at the root (authored over the coproduct via `Lift.check (|DocP|_|) …`) couples two domains; a blocking result wins under F07 precedence (law C3/C4, FR-007, SC-003). `Synthetic`.
- [X] T024 [P] [US3] **V68** (FsCheck **property**) in `tests/FS.GG.Governance.Adapters.Spi.Tests/CompositionTests.fs`: over randomized rule/verdict mixes, **every permutation** of adapter-composition order and rule order yields a byte-for-byte identical least fixed point AND an identical merged `Route`/verdict (commutative, order-free combination — law C2/C3, FR-008, SC-003/SC-007). `Synthetic`.
- [X] T025 [P] [US3] **V73** in `tests/FS.GG.Governance.Adapters.Spi.Tests/CompositionTests.fs` — **composition edge cases** (spec Edge Cases): (a) **duplicate fences** — two adapters naming the same surface dedup to **one** fence in `composed.Fences` and the route stakes over that surface is the **`max`** across the duplicated fences, not double-counted (FR-011, law C5); (b) **single adapter / trivial coproduct** — `compose [one] []` evaluates byte-for-byte identically to that adapter standalone (composition adds no behaviour); (c) **minimal adapter** — an adapter with empty `Rules`/`Fences` composes without error (the five-part contract permits empty rule/fence sets). `Synthetic`.

### Implementation for User Story 3

- [X] T026 [US3] Implement `Composition.compose : Lifted<'project,'change> list -> CheckRule<'project> list -> Composed<'project,'change>` in `src/FS.GG.Governance.Adapters.Spi/Composition.fs`: `Catalog = (lifted |> List.collect (fun l -> l.Rules)) @ crossDomain`, `Fences =` the input fences **deduped by `Name`** (first occurrence under a stable order kept — `List.fold` + seen-set, law C5/FR-011). Adds NO precedence or merge logic (determinism is the kernel's LFP + F07 `Route`; the `max` composed tier is F07's, inherited). Total; pure.
- [X] T027 [US3] Implement `Composition.toRules : Bridge<'project> -> Composed<'project,'change> -> Rule<'project> list` in `src/FS.GG.Governance.Adapters.Spi/Composition.fs` as `composed.Catalog |> List.map (CheckRule.toRule bridge)` — the UNCHANGED F04 bridge (law C1, SC-006). Total; no I/O.

**Checkpoint**: US1–US3 work — independent adapters compose at one reviewable root; cross-domain
coupling is a small named `Implies` set under a fixed, order-independent precedence; fences dedup by
name with a `max` tier; the composed catalog runs through the unchanged kernel.

---

## Phase 6: User Story 4 — Removing one adapter leaves the kernel and the other adapters intact (Priority: P2)

**Goal**: the boundary test — dropping one `Lifted` from `compose`'s list removes exactly that
domain's rules/fences; the kernel and every remaining adapter evaluate unchanged, and a cross-domain
rule naming the removed domain becomes **inert** (its antecedent probe reports `Unmet`, so the
`Implies` is vacuously satisfied), never an error (FR-009, SC-004).

**Independent Test**: compose ≥2 example adapters; remove one; confirm the kernel + remaining
adapter(s) still derive facts, evaluate rules, and explain results unchanged — zero references
break, and the cross-domain rule that named the removed domain goes inert rather than throwing.

### Tests for User Story 4 ⚠️ (write FIRST; must FAIL before T029)

- [X] T028 [P] [US4] **V69** in `tests/FS.GG.Governance.Adapters.Spi.Tests/CompositionTests.fs`: compose two example adapters + a cross-domain rule; drop one `Lifted`; assert the remaining catalog evaluates byte-for-byte as the surviving adapter did, that no reference to the removed domain is required outside the now-inert root rule, and that the cross-domain rule whose antecedent domain is gone is **inert** (`Unmet` → `Implies` vacuously satisfied), not an error (law R1/R2, FR-009, SC-004). `Synthetic`.

### Implementation for User Story 4

- [X] T029 [US4] Confirm the **inert-on-absence** guideline in `tests/FS.GG.Governance.Adapters.Spi.Tests/ExampleAdapters.fs`: author the cross-domain antecedent probe to report **`Unmet`** ("the domain's facts are absent ⇒ not applicable") rather than `Unknown` for an absent domain (law R3, research D9), so removal yields inertness. No new library code — removal is dropping one `Lifted` from the `compose` list (FR-006/FR-009).

**Checkpoint**: US1–US4 work — the boundary holds: the kernel is a library you adopt à la carte,
not a platform; un-adopting one domain leaves the rest intact and degrades a cross-domain rule to
inert.

---

## Phase 7: User Story 5 — A second, unrelated domain adopts the kernel without copying the first's vocabulary or layout (Priority: P2)

**Goal**: the two example domains are genuinely **unrelated** (distinct vocabularies, artifacts,
probes, rules) and compose at one root **without** either being reshaped to resemble the other —
the generality evidence that the abstraction sits at the right altitude (FR-010, SC-005).

**Independent Test**: author two unrelated example adapters; confirm each governs itself end-to-end
through the kernel, neither imports the other's facts/artifacts/probes/rules, and the two compose
in one root without either being reshaped.

### Tests for User Story 5 ⚠️ (write FIRST; must FAIL before T032)

- [X] T030 [P] [US5] **V70** in `tests/FS.GG.Governance.Adapters.Spi.Tests/CompositionTests.fs`: the two unrelated example adapters each govern themselves through the kernel with their OWN vocabulary; assert neither references the other's facts/artifacts/probes/rules (zero cross-copying, FR-010, SC-005). `Synthetic`.
- [X] T031 [P] [US5] **V71** in `tests/FS.GG.Governance.Adapters.Spi.Tests/CompositionTests.fs`: the two unrelated adapters compose at one root via `Composition.lift`/`compose` without either being reshaped to resemble the other, and both evaluate correctly (FR-010, SC-005). `Synthetic`.

### Implementation for User Story 5

- [X] T032 [US5] Confirm in `tests/FS.GG.Governance.Adapters.Spi.Tests/ExampleAdapters.fs` that the second example domain shares **no** vocabulary/layout with the first — distinct `'fact` union, `'artifact` kinds, probes, and fences — and that lifting it into the same `ProjectFact` coproduct needs only its one-line `inject`/active-pattern (FR-006/FR-010). No new library code; this is a fixture-shape guarantee the V70/V71 tests assert against.

**Checkpoint**: all five user stories work — two unrelated domains adopt the kernel cheaply and
compose at one root; the adoption bar (Milestone M3's opening thesis) is met with zero cross-copying.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: the new surface baseline + dependency hygiene, docs, and the full quickstart
validation — the Tier 1 surface-drift discipline and the M3 narrative.

- [X] T033 [P] **V72** in `tests/FS.GG.Governance.Adapters.Spi.Tests/SurfaceDriftTests.fs`: a reflective surface-drift test asserting the Spi public surface matches `surface/FS.GG.Governance.Adapters.Spi.surface.txt`, PLUS a dependency-hygiene test asserting `FS.GG.Governance.Adapters.Spi` references only **BCL + `FSharp.Core` + Kernel** and that the **kernel does NOT reference Spi** (dependency direction adapters → kernel, FR-015/FR-016, SC-008).
- [X] T034 Bless the **new** surface baseline `surface/FS.GG.Governance.Adapters.Spi.surface.txt` via `BLESS_SURFACE=1 dotnet test` after the F09 surface lands; commit the blessed file (Principle II, FR-016).
- [X] T035 [P] Update `docs/governance-design/adapters.md` ("What an adapter supplies" / "Composing several adapters in one project") and confirm `docs/governance-design/theory-and-composition.md` ("Taming lifting boilerplate" — the closed-coproduct trade) match the shipped `Adapter`/`Lift`/`Composition` surface; note any contract refinement made during implementation.
- [X] T036 Run the full quickstart validation: `dotnet build src/FS.GG.Governance.Adapters.Spi`, `dotnet fsi scripts/prelude.fsx` (the F09 sketch runs against the REAL bodies — standalone, faithful lift, compose-two, removal), and `dotnet test` (Adapter + Composition + Surface drift all green; kernel + Host suites unaffected). Record the V61–V73 evidence.
- [X] T037 Update `CLAUDE.md` and the project memory index to mark **F09 · 009-adapter-spi complete** (M3 started — the adoption bar) once T036 is green.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — **BLOCKS all user stories** (declares the shared
  records + the example-adapter fixture).
- **User Stories (Phases 3–7)**: all depend on Foundational. They are written in priority order
  (US1/US2/US3 are co-equal P1; US4/US5 are P2). Because the lift combinators (US2) and `compose`
  (US3) are real library code, the P1 stories are best done **in sequence** (US1 → US2 → US3); the
  P2 stories (US4/US5) are mostly fixture/test guarantees over the US3 machinery and can follow.
- **Polish (Phase 8)**: depends on all desired user stories — the surface baseline is blessed only
  once the public bodies are final.

### Cross-task dependencies (beyond plain phase order)

- T013 (`Adapter.toRules`) needs the `Adapter` record (T007).
- T021 (`Composition.lift`) needs `Lift.checkRule` (T018) + `Lift.fence` (T020).
- T026 (`compose`) + T027 (`toRules`) need `Lifted`/`Composed` (T008) and, for the V67 cross-domain
  rule and the V73 fence-dedup case, `Lift.check`/`Lift.fence` (T017/T020).
- T024 / T025 / T028 (order-independence, composition edge cases, removal) need `compose`/`toRules`
  (T026/T027).
- T034 (bless surface) needs the final public bodies (T013, T017–T021, T026/T027) and T033.
- T036/T037 (quickstart + status) are last — they need every body green.

### Within each user story

- Tests are written FIRST and MUST FAIL before the matching implementation task.
- The `.fsi` (Phase 1) precedes the `.fs` bodies (Principle I).
- Story complete and independently testable before moving to the next priority.

### Parallel opportunities

- T004 + T005 (test scaffold + prelude sketch) run in parallel with the library stubs (T001–T003).
- T009 (example adapters) runs parallel to T007/T008 once the `Adapter` type exists.
- All test tasks within a story marked `[P]` (V-numbers) target distinct asserts in their files and
  can be authored in parallel before their implementation tasks.
- The four `Lift.*` combinators (T017–T020) are independent functions in one file — author the
  signatures together; `Composition.lift` (T021) waits on `checkRule`+`fence`.

---

## Implementation Strategy

### MVP first (User Story 1)

1. Phase 1 Setup → Phase 2 Foundational (the shared records + the example-adapter fixture).
2. Phase 3 US1 — a single adapter governs itself through the kernel via `Adapter.toRules`.
3. **STOP and VALIDATE**: V61/V62 green — the five-part contract is total and reuses 100 % of the
   kernel. This alone proves the adoption-bar thesis for one domain.

### Incremental delivery

1. Setup + Foundational → fixture ready.
2. US1 → standalone adoption (MVP).
3. US2 → faithful lifting (the cache key does not move).
4. US3 → deterministic, order-independent composition at one root.
5. US4 → the removal/boundary proof.
6. US5 → two unrelated domains, zero cross-copying.
7. Polish → surface baseline + hygiene + docs.

---

## Notes

- `[P]` = different file / distinct assert, no dependency on another incomplete task in the phase.
- F09 is **pure** — Principle IV is **N/A** (no `Model`/`Msg`/`Effect`/interpreter); the
  evidence-obligations note (T010) records this.
- F09 ships **no concrete production adapter** — the two example adapters are **synthetic example
  domains** (disclosed at definition, `Synthetic` token in asserting tests, listed in the PR);
  the synthetic framing is the deliberate, conservative Principle V choice recorded in T010.
- **Zero new `PackageReference`** — Spi references only BCL/`FSharp.Core`/Kernel; the kernel does
  **not** reference Spi (T033 hygiene test, SC-008).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and
  document the narrowing on the task line.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
