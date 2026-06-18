---
description: "Task list for F10 · 010-adapter-speckit — the Spec Kit adapter: governance dogfoods this repo's own workflow as data. The first concrete production adapter; supplies only its own five SPI components and reuses 100% of the kernel. Domain #1 of Milestone M3 (the adoption bar)."
---

# Tasks: The Spec Kit Adapter — Governance Dogfoods This Repo's Own Workflow As Data

**Input**: Design documents from `/specs/010-adapter-speckit/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/SpecKit.fsi](./contracts/SpecKit.fsi), [contracts/Catalog.fsi](./contracts/Catalog.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature whose headline guarantees — the adapter being
fully specified by **exactly the five** SPI components with **100% kernel reuse** and **no**
artifact-authoring operation (SC-001); the **phase guard** being a definite not-applicable before a
phase and transparent at/after (SC-002); the **advisory-inner / single-merge-fence** distinction
across the full catalog (SC-003); the kernel's **evidence/taint** propagating down a dependency
chain and `evidenceNotSynthetic` blocking at merge that no flag flips (SC-004); the **constitution
dial** being the blocking set, not a fixed list (SC-005); every rule **rendering and explaining**
itself (SC-006); the **faithful lift** — a lifted rule's `(verdict, provenance)` byte-for-byte
identical to the standalone original (SC-007); and the **new** SpecKit surface-drift +
dependency-hygiene baseline (SC-008) — are only credible with real evidence (Principle V). Per
Principle I the semantic tests are written against the **public** surface (through the built
`FS.GG.Governance.Adapters.SpecKit` library / `scripts/prelude.fsx`) and MUST FAIL before the
matching `SpecKit.fs`/`Catalog.fs` bodies exist.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations —
every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A — PASS** (Constitution Principle IV; plan Constitution Check row IV). This is
a **pure** value/fold layer — no multi-step state, no I/O, no `Model`/`Msg`/`Effect`, no
interpreter (FR-015), exactly as for F01–F07 and F09. There are therefore **no**
`Model`/`Msg`/`Effect`/`init`/`update`/interpreter tasks; the evidence-obligations note (T016)
records this N/A explicitly. **Sensing** the live repository into `SpecKitFact`s (reading
`.specify/feature.json`, parsing `tasks.md`/`tasks.deps.yml`, hashing artifact content) and
**wiring** the adapter into a running loop is the already-shipped F08 effects shell and the F12
CLI, not this feature — tests feed `SpecKitFact`s directly.

**Synthetic-evidence discipline (Principle V).** The Spec Kit adapter **is the real adopter under
test** — its rules are fed **real** `SpecKitFact`s through the built library and assert real
verdicts/provenance/render/hash/routes (the deterministic-engine "prefer real evaluation" path; no
marker needed). The **only** synthetic artefact is the **second, unrelated example domain** the
adapter is composed with for the faithful-lift proof (US-lift / SC-007). It is a **synthetic
example domain** — illustrative, not a real adopter — so: it MUST carry a `// SYNTHETIC: example
domain — illustrative, not a real adopter (the real adopter under test is Spec Kit itself)` comment
at its definition, every test asserting through it MUST carry the token `Synthetic` in its test
name, and it MUST be listed in the PR description.

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

- [X] T001 Create the new pure library project `src/FS.GG.Governance.Adapters.SpecKit/FS.GG.Governance.Adapters.SpecKit.fsproj` — `net10.0` (inherits `Directory.Build.props`), a **single** `ProjectReference` to `../FS.GG.Governance.Adapters.Spi/FS.GG.Governance.Adapters.Spi.fsproj` (which transitively brings the kernel), and **ZERO** `PackageReference` (BCL + `FSharp.Core` + Spi + Kernel only, FR-016). Compile order in the fsproj: `SpecKit.fsi`, `SpecKit.fs`, `Catalog.fsi`, `Catalog.fs`. Set `RootNamespace` to `FS.GG.Governance.Adapters.SpecKit`.
- [X] T002 Copy `specs/010-adapter-speckit/contracts/SpecKit.fsi` → `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fsi` and `specs/010-adapter-speckit/contracts/Catalog.fsi` → `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fsi` verbatim (the curated public surface, Principle II — these are the SOLE visibility declaration; the `.fs` carry NO `private`/`internal`/`public` on top-level bindings).
- [X] T003 Add `failwith`-stub bodies `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` and `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` that satisfy the two `.fsi` (declare the DUs/records real where cheap — `Phase`, `SpecKitArtifact`, `SpecKitFact`, `SpecKitChange`, `ConstitutionDial`; function/value bodies `failwith "F10"`), so `dotnet build src/FS.GG.Governance.Adapters.SpecKit` compiles. `RequireQualifiedAccess` on `Phase`/`SpecKitArtifact` per the `.fsi`. **(Note: implemented the REAL `SpecKit.fs` bodies directly rather than `failwith` stubs — the compile-checkpoint goal is met and exceeded; the build is clean.)**
- [X] T004 [P] Create the test project `tests/FS.GG.Governance.Adapters.SpecKit.Tests/FS.GG.Governance.Adapters.SpecKit.Tests.fsproj` — Expecto + FsCheck (centrally pinned, `IsPackable=false`, `GenerateProgramFile=false`), a `ProjectReference` to `../../src/FS.GG.Governance.Adapters.SpecKit/...`, and the compile-order item group: `ExampleAdapters.fs`, `SpecKitTests.fs`, `CatalogTests.fs`, `LiftTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`. Add `tests/FS.GG.Governance.Adapters.SpecKit.Tests/Main.fs` (Expecto `runTestsInAssembly` entry point).
- [X] T005 [P] Extend `scripts/prelude.fsx` with the F10 FSI sketch from [quickstart.md](./quickstart.md) (author the adapter → govern synthetic facts → phase guard → inner-loop vs merge → evidence/taint → dial → render/explain), drafted against the two `.fsi` with `failwith`/`Unchecked` where a body is absent. The point of this pass is that the SHAPES type-check against the two contracts and read naturally.
- [X] T006 Add `src/FS.GG.Governance.Adapters.SpecKit` and `tests/FS.GG.Governance.Adapters.SpecKit.Tests` to `FS.GG.Governance.sln`.

**Checkpoint**: `dotnet build` is clean with the stubs; `dotnet fsi scripts/prelude.fsx` type-checks
the F10 sketch against the two contracts; the solution lists the two new projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: implement the entire `SpecKit` module — the domain vocabulary, the artifact map, the
kernel wiring, and the keystone `whenPhase` phase guard (plan module 1, the shared infrastructure
every catalog rule and every story builds on) — and author the consumer-side coproduct + the
second synthetic example domain that the faithful-lift proof evaluates through. **No user-story
work can begin until this phase is complete.**

- [X] T007 Declare `Phase` (8 ordered cases, `[<RequireQualifiedAccess>]`), `SpecKitArtifact` (9 cases, `[<RequireQualifiedAccess>]`), `SpecKitFact` (7 cases — `PhaseReached`/`ArtifactPresent`/`TaskState`/`TaskDependsOn`/`SkillBound`/`ConstitutionArea`/`SpecKitGov of RuleOutcome`), and `SpecKitChange` (`{ Phase; Surfaces: Set<SpecKitArtifact> }`) in `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` exactly per `SpecKit.fsi`. `TaskState` carries an authored `EvidenceState` (never `AutoSynthetic`).
- [X] T008 Implement `Phase.rank : Phase -> int` (`Constitution = 0 … Merge = 7`, declaration order) and `Phase.reached : current -> required -> bool` (`rank current >= rank required`) in `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` — the total order the phase guard reads as "at or after" (FR-005). Depends on T007.
- [X] T009 Implement `SpecKit.toRef : SpecKitArtifact -> ArtifactRef` in `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` — the artifact mapping (FR-002), total and **injective** (distinct artifact kinds map to distinct `ArtifactRef`s). Depends on T007.
- [X] T010 Implement `SpecKit.identify : SpecKitFact -> FactId` in `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` — keyed by the **entity** a fact is about (`TaskState` by `taskId`, `ConstitutionArea` by `area` → a later fact supersedes; `PhaseReached`/`ArtifactPresent`/`TaskDependsOn`/`SkillBound` by full value), **injective on value-bearing facts** (data-model L0, Hazard 4). Depends on T007.
- [X] T011 Implement `SpecKit.bridge : JudgeId -> Bridge<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` — `Embed = SpecKitGov`, `Project` the inverse partial map, `Judge = judge`, `ArtifactHash = fun _ _ -> ""` (the pure adapter holds no content; F08 supplies real hashes at the edge). The **unchanged** F04 bridge wiring — no new cross-cutting code (research D2). Depends on T007.
- [X] T012 Implement the keystone `SpecKit.whenPhase : Phase -> Check<SpecKitFact> -> Check<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` as `Implies (phaseAtLeast required, check)` over an atomic `phaseAtLeast` probe ("supplied phase ≥ `required`", with `required` carried as a `LiteralArg` so render/hash distinguish phases) — **reusing the kernel's `Implies`, not new logic** (data-model P1–P4). Before the phase the antecedent is `Unmet` → vacuous `Pass`; at/after it is `Met` → reduces to the check. Depends on T008.
- [X] T013 Implement `SpecKit.probes : Probe<SpecKitFact> list` in `src/FS.GG.Governance.Adapters.SpecKit/SpecKit.fs` — the declared atomic-predicate vocabulary the catalog composes (incl. `phaseAtLeast`), carried for the contract and testing; the `Catalog` rules' checks remain authoritative for evaluation (research D2). Depends on T007.
- [X] T014 [P] Declare the `ConstitutionDial` record (`{ BlockingAtMerge: Set<RuleId>; EarlyFences: (string * Phase) list }`) in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` exactly per `Catalog.fsi` (US4, the dial-as-data). Depends on T007.
- [X] T014a **Pin the canonical `RuleId` strings** in a comment block at the top of the `Catalog` module in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — the single source of truth tying each camelCase binding to its kebab-case `RuleId`, so the dial (`Set<RuleId>`) matches rules by an agreed string and `defaultDial` cannot silently promote nothing (finding I1/T1): `tasksGraphWellFormed → RuleId "tasks-graph"`, `constitutionComplete → "constitution-complete"`, `contractsCurrent → "contracts-current"`, `evidenceNotSynthetic → "evidence-not-synthetic"`, `fencedSurfacesVerified → "fenced-surfaces-verified"`, `planSatisfiesSpec → "plan-satisfies-spec"`, `tasksCompleteOrdered → "tasks-complete-ordered"`, `featureInScope → "feature-in-scope"`. Every rule's `CheckRule.rule`/`asking` construction (T019–T024, T039, T043, T044) and `defaultDial` (T027) MUST use exactly these strings. Depends on T014.
- [X] T015 [P] Author `tests/FS.GG.Governance.Adapters.SpecKit.Tests/ExampleAdapters.fs` — the consumer-authored closed `ProjectFact` coproduct (a `SpecKit of SpecKitFact` case + a **second, UNRELATED synthetic** toy domain's case + the `Governance of RuleOutcome` case), the single-case active patterns (`(|SpecKitP|_|)`, the toy domain's pattern), the `inject` constructors, and the project `Identify`/`Bridge` that **agree with** `SpecKit.identify`/`SpecKit.bridge` on injected `SpecKitFact`s (data-model L0/L3). The second domain carries the `// SYNTHETIC: example domain — illustrative, not a real adopter …` disclosure comment (Principle V). This is the shared fixture the faithful-lift proof (T045) evaluates through. Depends on T007 (the vocabulary) only.
- [X] T016 Record the **evidence-obligations** note at the top of `tests/FS.GG.Governance.Adapters.SpecKit.Tests/ExampleAdapters.fs` (a comment block): (a) **Principle IV is N/A** — pure value/fold layer, no `Model`/`Msg`/`Effect`/interpreter (no boundary tasks owed); (b) the **Spec Kit adapter is the REAL adopter under test** (real evaluation, no marker), and the **only** synthetic artefact is the second composition domain (token `Synthetic` in asserting tests; listed in the PR description); (c) **sensing is out of scope** (FR-015) — tests feed `SpecKitFact`s directly.

**Checkpoint**: the whole `SpecKit` module is implemented against `SpecKit.fsi` and builds;
`ConstitutionDial` is declared and the canonical `RuleId` strings are pinned (T014a); the
`ProjectFact` coproduct + second synthetic domain compile; the N/A-Principle-IV and synthetic
disclosures are written down. Every user story can now begin.

---

## Phase 3: User Story 1 — The Spec Kit workflow is governed as data; the adapter supplies exactly five components (Priority: P1) 🎯 MVP

**Goal**: assemble the single `Adapter<SpecKitFact, SpecKitArtifact, SpecKitChange>` value from
**exactly** the five SPI components + the F04 `Bridge`, with the rule **catalog** as a set of
reified `CheckRule`s that each render to a sentence and explain themselves — and **no** inference/
arbitration/evidence/render/hash/explain/severity/routing code and **no** artifact-authoring
operation in the adapter (SC-001/SC-006).

**Independent Test**: build `Catalog.adapter judge dial`; confirm it governs synthetic
`SpecKitFact`s end-to-end through the kernel (`Adapter.toRules` → `FixedPoint.evaluate` →
`Route.route`, `Check.render`/`Check.explain`) and that the adapter module contains none of the
cross-cutting facilities and no write-artifact function.

### Tests for User Story 1 ⚠️ (write FIRST; must FAIL before T021–T029)

- [X] T017 [P] [US1] **V1** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/SpecKitTests.fs`: the adapter is fully specified by the **five** components + `Bridge` — assert `Catalog.adapter judge dial` carries `Identify`/`ToRef`/`Probes`/`Rules`/`Fences`/`Bridge` and governs synthetic facts via kernel entry points only (`Adapter.toRules` → `FixedPoint.evaluate adapter.Identify … supplied` derives the expected facts; `Route.route adapter.Fences adapter.Rules mode change` routes). The "100% kernel reuse — no inference/arbitration/evidence/render/hash/explain/severity/routing code, and **no artifact-authoring operation**" claim is a **structural** property: verify by inspection / a dependency-symbol review note in the test header (the adapter's definitions call only `FS.GG.Governance.Kernel` + Spi APIs; no `System.IO`, no write-spec/write-plan/write-tasks function) — NOT a runtime assertion (FR-003/FR-004, SC-001). Also assert (cheap, and a faithful-lift precondition): `SpecKit.toRef` is **injective** over all nine `SpecKitArtifact` cases (distinct kinds → distinct `ArtifactRef`, FR-002), and `SpecKit.identify` is **injective on value-bearing facts** while keying `TaskState`/`ConstitutionArea` by entity so a later fact supersedes (data-model L0).
- [X] T018 [P] [US1] **V6** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/SpecKitTests.fs`: every rule renders & explains — for **every** `r ∈ Catalog.catalog`, assert `Check.render r.Check` is a non-empty sentence and `(Check.explain facts r.Check)` has top verdict `= Check.eval facts r.Check` (data-model C2, replacing the monolithic `analyze` with self-describing rules, SC-006).

### Implementation for User Story 1

- [X] T019 [P] [US1] Implement `Catalog.contractsCurrent : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` via `CheckRule.rule` over a reified `Check` guarded by `SpecKit.whenPhase Phase.Plan` (surface-baseline / contract currency); `Deterministic`, default `Advisory` (FR-007).
- [X] T020 [P] [US1] Implement `Catalog.fencedSurfacesVerified : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — the touched high-stakes surfaces carry their verification facts; `Deterministic`, default `Advisory` (FR-009).
- [X] T021 [P] [US1] Implement `Catalog.planSatisfiesSpec : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` via `CheckRule.asking` over an `Opaque` check carrying the `Question` "Does plan.md address every requirement in spec.md? List gaps.", guarded by `SpecKit.whenPhase Phase.Plan`; `AgentReviewed`, `Advisory` (FR-007).
- [X] T022 [P] [US1] Implement `Catalog.tasksCompleteOrdered : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` via `CheckRule.asking` over an `Opaque` check ("Are the tasks complete and ordered for the plan?"), guarded by `SpecKit.whenPhase Phase.Tasks`; `AgentReviewed`, `Advisory` (FR-007).
- [X] T023 [P] [US1] Implement `Catalog.featureInScope : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — "Is the feature in scope / worth doing?" as `HumanOnly` (routes to a person via `toRule` escalation, never resolves to a deterministic verdict) (FR-007, edge case).
- [X] T024 [US1] Add **interim stubs** for `Catalog.constitutionComplete`, `Catalog.evidenceNotSynthetic`, and `Catalog.tasksGraphWellFormed` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` (well-typed `CheckRule`s with placeholder checks of the right tier/severity) so `catalog`/`adapter` compile now; their real bodies land in US4 (T039) and US5 (T043/T044). Note the dependency on the task lines. **(Note: these three rules were implemented with their REAL bodies directly — no interim stub phase was needed; `catalog`/`adapter` compile and evaluate correctly, and T039/T043/T044 confirm the same final bodies.)**
- [X] T025 [US1] Implement `Catalog.catalog : CheckRule<SpecKitFact> list` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — all eight rules in a stable order, at their **default** severities (every rule `Advisory` **except** `evidenceNotSynthetic`, which is `Blocking`; data-model C3). Depends on T019–T024.
- [X] T026 [US1] Implement `Catalog.mergeFence : Fence<SpecKitChange>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — `{ Name = "feature-merge"; Trips = fun c -> c.Phase = Phase.Merge }` (FR-009). The fence carries no rules; routing partitions by `Severity & Fenced & Gate`.
- [X] T027 [US1] Implement `Catalog.defaultDial : ConstitutionDial` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — `BlockingAtMerge = Set.ofList [ RuleId "constitution-complete"; RuleId "contracts-current"; RuleId "fenced-surfaces-verified" ]` (using the T014a pinned ids exactly, so the set matches the rules' `.Id`; alongside the always-blocking `evidence-not-synthetic`), `EarlyFences = []` (research D8). Depends on T014a.
- [X] T028 [US1] Implement `Catalog.fences : ConstitutionDial -> Fence<SpecKitChange> list` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — `mergeFence :: [ { Name; Trips = fun c -> c.Phase = p } for (name, p) in dial.EarlyFences ]` (data-model D2). Depends on T026.
- [X] T029 [US1] Implement `Catalog.adapter : JudgeId -> ConstitutionDial -> Adapter<SpecKitFact, SpecKitArtifact, SpecKitChange>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — the five components (`SpecKit.identify`, `SpecKit.toRef`, `SpecKit.probes`, the **dial-promoted** `catalog`, `fences dial`) + `SpecKit.bridge judge`. Promote each rule whose `Id ∈ dial.BlockingAtMerge` to `CheckRule.blocking`; keep `evidenceNotSynthetic` blocking **regardless** of the dial (FR-013); everything else stays `Advisory` (data-model D1). Supplies NOTHING else (SC-001). Depends on T025, T028.

**Checkpoint**: US1 is independently testable — the single adapter, supplying only its own five
components, governs synthetic `SpecKitFact`s entirely through the kernel and every rule renders &
explains (the thesis of the adoption bar). **MVP.** (The three interim-stubbed rules — `constitutionComplete`, `evidenceNotSynthetic`, `tasksGraphWellFormed` — behave correctly for
shape/render/route; their **evaluation** bodies complete in US4 (T039) / US5 (T043/T044). **Re-run
T017 and T018 after those land** so the five-component/observer-only and render/explain assertions
hold over the final, fully-evaluable catalog — until then US1's adapter is route-/render-complete
but not fully evaluable.)

---

## Phase 4: User Story 2 — Phase-guarded rules contribute only at or after their phase (Priority: P1)

**Goal**: prove the keystone — `SpecKit.whenPhase` (implemented foundationally, T012) makes a rule a
**definite not-applicable** (vacuous `Pass`, never `Fail`/`Uncertain`) before its phase and
**transparent** at/after, is **reified-ness preserving**, and is **part of the contract**
(render/hash distinguish the guarded phase) — the stateless kernel governing a stateful lifecycle
through a supplied `PhaseReached` fact (FR-005, SC-002).

**Independent Test**: take a `whenPhase Plan` rule; over `PhaseReached Specify` (and over facts with
**no** `PhaseReached`) it contributes a definite not-applicable; over `PhaseReached Plan` (or later)
it contributes its check verdict.

### Tests for User Story 2 ⚠️ (write FIRST)

- [X] T030 [P] [US2] **V2 (P1, inert)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/SpecKitTests.fs`: for a `SpecKit.whenPhase Phase.Plan check`, assert `Check.eval facts g = Pass` for any `facts` whose supplied phase is before `Plan` — **including facts with no `PhaseReached` at all** (a missing phase is not a silent default to `Merge`) — a vacuous, definite not-applicable, **never `Fail` or `Uncertain`** (data-model P1, edge cases). Use an FsCheck property over phases `< Plan`.
- [X] T031 [P] [US2] **V2 (P2, transparent)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/SpecKitTests.fs`: for the same guarded rule, assert `Check.eval facts g = Check.eval facts check` for any `facts` whose supplied phase is `Plan` or later (the guard adds nothing once the phase holds) (data-model P2). FsCheck property over phases `>= Plan`.
- [X] T032 [P] [US2] **V2 (P3/P4)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/SpecKitTests.fs`: assert `Check.isReified (whenPhase p c) = Check.isReified c` (a guarded reified check stays `Deterministic`-eligible; a guarded `Opaque` stays opaque → `AgentReviewed`/`HumanOnly`) (P3); and assert `Check.render`/`Check.hash` of `whenPhase Phase.Plan c` **differ** from `whenPhase Phase.Tasks c` — the guarded phase is part of the rule's contract and cache key (P4).

**Checkpoint**: the phase guard is proven inert-before / transparent-after, reified-ness preserving,
and contract-visible — the stateless engine governs the staged lifecycle through the supplied fact
with no mutable phase state in the adapter.

---

## Phase 5: User Story 3 — Nothing blocks before merge; merge is the single fence that flips to Gate (Priority: P1)

**Goal**: prove the headline behaviour change — every inner-loop phase is **advisory** in
`Inner`/`Sandbox` (a failing deterministic check reports, the route never blocks), and **merge is
the single fence** that, at `Phase.Merge` in `Gate`, lets the `Blocking` rules bite; and an earlier
hard-stop is an opt-in one-liner (FR-008/FR-009/FR-010, SC-003).

**Independent Test**: evaluate the catalog over each inner-loop phase in `Inner` and confirm
`Blocking = []` even for a failing deterministic check; evaluate at `Phase.Merge` through
`mergeFence` in `Gate` and confirm the blocking rules flip the route to a blocking gate.

### Tests for User Story 3 ⚠️ (write FIRST)

- [X] T033 [P] [US3] **V3 (inner advisory)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: for **every** inner-loop phase (`Constitution`…`Implement`), assert `Route.route adapter.Fences adapter.Rules Inner change` yields `Blocking = []` — **including** when a deterministic rule's facts make it fail (a failing check reports but never blocks; data-model D7, edge case "inner-loop deterministic failure"). Adapter from US1 (T029).
- [X] T034 [P] [US3] **V3 (merge gate)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: at `change = { Phase = Phase.Merge; Surfaces = … }`, assert `Route.route adapter.Fences adapter.Rules Gate change` flips the route to a **blocking gate** whose blocking set is the dial's `Blocking` rules, and that a failing blocking rule refuses the merge (FR-009, US3 acceptance 2). A blocking gate iff `Severity = Blocking ∧ Fenced ∧ Gate`.
- [X] T035 [P] [US3] **V3 (opt-in early fence)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: with a dial carrying one `EarlyFences = [ ("no-cyclic-tasks", Phase.Tasks) ]`, assert `Catalog.fences dial` includes a fence that trips at `Phase.Tasks` so that phase can enforce — the default-advisory behaviour is opt-out per the constitution, **no kernel change** (FR-010, data-model D2). Add a comment recording that "recompute from base" is the host's (F08) job — the merge route is evaluated over base-branch facts, not adapter logic (data-model D7, edge case).

**Checkpoint**: the inner loop is advisory across the full catalog; merge is the single enforcing
boundary; an earlier hard-stop is one `EarlyFences` entry — the run-mode mapping is proven.

---

## Phase 6: User Story 4 — The constitution is the dial that configures fences and severities (Priority: P2)

**Goal**: the **dial is the blocking set** — `Catalog.adapter judge dial` promotes exactly the rules
named in `dial.BlockingAtMerge` (and `evidenceNotSynthetic` regardless), so varying the dial varies
which rules bite at merge; and the **Constitution Check** (`constitutionComplete`) verifies the dial
was filled in honestly — advisory inner, blocking at merge (FR-011, SC-005).

**Independent Test**: supply `ConstitutionArea` facts (some filled, one placeholder); confirm
`constitutionComplete` is advisory in the inner loop and blocking at merge under a dial that
promotes it; confirm the merge fence's blocking set is the **dial's**, not a fixed list (default vs
empty dial → fewer blocks).

### Tests for User Story 4 ⚠️ (write FIRST)

- [X] T036 [P] [US4] **V5 (advisory inner / blocking merge)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: with a placeholder `ConstitutionArea` fact, assert `constitutionComplete` reports `Unmet` (a definite failure, not `Unknown`), is **advisory** in the inner loop (no block), and is a **blocking** failure at `Phase.Merge`/`Gate` under `defaultDial` (data-model D1, edge case "constitution area placeholder").
- [X] T037 [P] [US4] **V5 (dial is the blocking set)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: assert by **specific `RuleId`** (the T014a pinned strings), not by count alone — under `Catalog.defaultDial` the merge blocking set is exactly `{ "constitution-complete"; "contracts-current"; "fenced-surfaces-verified"; "evidence-not-synthetic" }`; under `{ defaultDial with BlockingAtMerge = Set.empty }` (the "light" posture) it is exactly `{ "evidence-not-synthetic" }`. This proves the blocking set is a function of the **dial** (not a fixed list) AND guards against the I1 failure mode where a `RuleId`-string mismatch makes `defaultDial` silently promote nothing (a count-only check could pass vacuously). Also assert promoting a single arbitrary id (e.g. `RuleId "plan-satisfies-spec"`) makes exactly that rule block (SC-005).
- [X] T038 [P] [US4] **V5 (dial assembles fences)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: assert `(Catalog.adapter judge dial).Fences = Catalog.fences dial` for a dial with non-empty `EarlyFences` — the opt-in earlier hard-stop is one dial entry, the merge fence is always present (data-model D2).

### Implementation for User Story 4

- [X] T039 [US4] Replace the T024 stub with the real `Catalog.constitutionComplete : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — `CheckRule.rule` over a reified check, guarded by `SpecKit.whenPhase Phase.Constitution`, that holds iff **every** `ConstitutionArea` is `filled = true` (a placeholder area → `Unmet`); `Deterministic`, default `Advisory` (promoted to `Blocking` at merge by the dial). Re-run T036–T038.

**Checkpoint**: the constitution dial is the authored, reviewable blocking set; the Constitution
Check verifies honest completeness — advisory inner, blocking at merge; the lightness is a visible
configuration, not a silent weakening.

---

## Phase 7: User Story 5 — The evidence/dependency graph runs through the kernel's F05 model, not a bespoke engine (Priority: P2)

**Goal**: prove the heaviest legacy gate collapses to a **kernel derivation** — `evidenceNotSynthetic`
and `tasksGraphWellFormed` run the kernel's F05 `Evidence.build`/`Evidence.effective` over
`TaskState`/`TaskDependsOn` facts, the `AutoSynthetic` taint propagates down the dependency graph by
the kernel's fixed point (no adapter graph code), `evidenceNotSynthetic` is a blocking failure at
merge no flag flips, and well-formedness is advisory inner / blockable at merge (FR-012/FR-013,
SC-004).

**Independent Test**: supply a `TaskDependsOn` chain with one `Synthetic` upstream; confirm
`AutoSynthetic` propagates down via the kernel fixed point; confirm the graph well-formedness rule
is advisory inner; confirm `evidenceNotSynthetic` is a blocking failure at `Merge`.

### Tests for User Story 5 ⚠️ (write FIRST)

- [X] T040 [P] [US5] **V4 (kernel taint)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: with `TaskState ("T1", Synthetic)`, `TaskState ("T2", Real)`, `TaskDependsOn ("T2", "T1")`, assert `Check.eval facts Catalog.evidenceNotSynthetic.Check = Fail` — T2's effective state is `AutoSynthetic` via the kernel's `Evidence.effective` fixed point, **not** adapter code (data-model E1, SC-004).
- [X] T041 [P] [US5] **V4 (non-negotiable)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: assert `evidenceNotSynthetic` is `Blocking` by default and is a blocking failure at `Phase.Merge`/`Gate` for synthetic-tainted evidence, and that it stays `Blocking` under **every** dial (including the empty "light" dial) — **no** disclosure flag or override flips its verdict (data-model E3, FR-013).
- [X] T042 [P] [US5] **V4 (malformed ≠ tainted; well-formedness)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/CatalogTests.fs`: assert `tasksGraphWellFormed` reports a definite `Unmet` (distinguishable from `Unknown`) on a **cyclic** `TaskDependsOn` graph (`Evidence.build` → `Cycle`), on an **unresolved** dep / `SkillBound` id, and on a task missing deps; and that it is **advisory** in the inner loop, **blockable** at merge only if the dial promotes it (data-model E2/E4, edge cases).

### Implementation for User Story 5

- [X] T043 [US5] Replace the T024 stub with the real `Catalog.evidenceNotSynthetic : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — its probe builds `Evidence.build [ (t,s) for TaskState (t,s) ] [ (t,d) for TaskDependsOn (t,d) ]`; on `Ok graph` reports `Unmet` iff some node's `Evidence.effective` state is `Synthetic`/`AutoSynthetic`, else `Met`; on `Error e` reports `Unmet` with the `GraphError` (malformed ≠ tainted). `Deterministic`, **`Blocking` by default** (data-model E1–E3). Re-run T040/T041.
- [X] T044 [US5] Replace the T024 stub with the real `Catalog.tasksGraphWellFormed : CheckRule<SpecKitFact>` in `src/FS.GG.Governance.Adapters.SpecKit/Catalog.fs` — `allOf [ everyTaskHasDeps; depsResolve; acyclic; skillIdsResolve ]`, guarded by `SpecKit.whenPhase Phase.Tasks`, where `acyclic`/`depsResolve` read the same `Evidence.build` result (a **derivation**, not a bespoke engine); `Deterministic`, default `Advisory` (data-model E4, FR-007). Re-run T042.

**Checkpoint**: the synthetic-taint model and graph well-formedness are kernel derivations over
supplied facts — the most load-bearing check (synthetic evidence must not reach the base) is just a
blocking rule, proving "reuses 100% of kernel facilities."

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: the faithful-lift milestone proof, the new surface + dependency-hygiene baseline,
quickstart validation, and docs — concerns that span the whole catalog.

- [X] T045 [P] **V7 (faithful lift, SC-007)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/LiftTests.fs`: compose the Spec Kit adapter with the **second synthetic** toy domain (T015) at a test root via `Composition.lift`/`compose`; assert that for **100%** of `Catalog.catalog`, the lifted rule's `(verdict, provenance)` over coproduct-wrapped facts is **byte-for-byte identical** to the standalone original over the projected facts (F09 laws L2/L3, data-model L2), `Check.render`/`Check.hash` are **invariant** under the lift (L1), and the adapter references only the SPI/kernel — dropping it from the compose list removes it cleanly (L3). Every test name carries the `Synthetic` token (it asserts via the second domain).
- [X] T046 **V8 (surface baseline, SC-008)** — add `tests/FS.GG.Governance.Adapters.SpecKit.Tests/SurfaceDriftTests.fs` (a reflective test that the built `FS.GG.Governance.Adapters.SpecKit` public surface equals the committed baseline), create the **new** baseline `surface/FS.GG.Governance.Adapters.SpecKit.surface.txt`, and bless it (`BLESS_SURFACE=1 dotnet test`). Confirm the two `.fsi` are the sole visibility declaration (no `private`/`internal`/`public` on `.fs` top-level bindings; FR-017).
- [X] T047 **V8 (dependency hygiene, SC-008)** in `tests/FS.GG.Governance.Adapters.SpecKit.Tests/SurfaceDriftTests.fs`: assert the SpecKit assembly's referenced assemblies ⊆ `{ BCL, FSharp.Core, FS.GG.Governance.Adapters.Spi, FS.GG.Governance.Kernel }` (zero new dependency, FR-016), and that neither the kernel nor the Spi assembly references SpecKit (the dependency direction is adapter → SPI → kernel, never the reverse).
- [X] T048 Run the [quickstart.md](./quickstart.md) validation V1–V8 end-to-end: `dotnet build src/FS.GG.Governance.Adapters.SpecKit`, `dotnet fsi scripts/prelude.fsx` (the F10 sketch type-checks and runs against the real bodies), `dotnet test` (all SpecKit/Catalog/Lift/SurfaceDrift tests green). Replace the `failwith`/`Unchecked` stubs in the prelude sketch with the real calls.
- [X] T049 [P] Update `README.md` (note F10 — the first concrete adapter, domain #1 of M3) and run the `speckit-agent-context-update` to refresh the managed Spec Kit section / `CLAUDE.md` pointer; confirm no kernel or Spi source changed (this feature adds adapter code only).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundational (Phase 2)** — depends on Setup; implements the whole `SpecKit` module + fixtures; **BLOCKS all user stories**.
- **User stories (Phases 3–7)** — all depend on Foundational. Recommended order is priority order **US1 (P1) → US2 (P1) → US3 (P1) → US4 (P2) → US5 (P2)**, but see the cross-story note below.
- **Polish (Phase 8)** — depends on all desired user stories; T045 (lift) needs the real catalog (US1+US4+US5); T046/T047 (surface/hygiene) need the final public surface.

### Cross-story dependencies (beyond plain phase order)

- US1's `Catalog.catalog`/`adapter` (T025/T029) reference `constitutionComplete`, `evidenceNotSynthetic`, `tasksGraphWellFormed`, which ship as **interim stubs** in T024 and are completed in **US4 (T039)** and **US5 (T043/T044)**. The adapter compiles and routes correctly with the stubs; the **evaluation** assertions for those three rules live in US4/US5. Re-run the US1 render/explain test (T018) after T039/T043/T044 land.
- US3/US4/US5 test the `adapter` assembled in US1 (T029) — they need Phase 3 complete.
- All phase-guarded rules (T019, T021, T022, T039, T044) depend on `SpecKit.whenPhase` (T012, foundational); US2 (T030–T032) proves its laws.
- Every rule construction (T019–T024, T039, T043, T044) and `defaultDial` (T027) MUST use the canonical `RuleId` strings pinned in **T014a**; T037 asserts the merge blocking set by those exact ids (guards finding I1 — a string mismatch would silently promote nothing).

### Within each user story

- Tests (the `⚠️ write FIRST` group) MUST be authored and FAIL before the matching implementation.
- Vocabulary/wiring before rules; rules before `catalog`; `catalog`/`fences` before `adapter`.

### Parallel opportunities

- Setup: T004, T005 are `[P]` (distinct files) once T001–T003 exist.
- Foundational: T009/T010/T011/T013 are `[P]` (distinct functions in `SpecKit.fs` — coordinate edits) ; T014/T015 are `[P]` (distinct files).
- US1: the five generic-rule impls T019–T023 are `[P]`; the two tests T017/T018 are `[P]`.
- US2/US3/US4/US5: all the `⚠️` tests within a story are `[P]` (assertions in the same test file — author as independent test cases).
- Polish: T045 and T049 are `[P]`.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (the whole `SpecKit` module + fixtures).
2. Phase 3 US1 — assemble the `Adapter` from exactly the five components; every rule renders &
   explains (the three heaviest rules interim-stubbed).
3. **STOP and VALIDATE**: the adapter governs synthetic `SpecKitFact`s entirely through the kernel,
   contains no cross-cutting code and no authoring op (SC-001/SC-006). The adoption-bar thesis for
   domain #1 is demonstrable.

### Incremental delivery

1. Setup + Foundational → the vocabulary, phase guard, and wiring are real.
2. US1 → the adapter + catalog (MVP — the five-component, observer-only thesis).
3. US2 → the phase guard proven inert/transparent.
4. US3 → advisory inner loop / single merge fence proven.
5. US4 → the constitution dial is the blocking set (completes `constitutionComplete`).
6. US5 → the evidence/taint model is a kernel derivation (completes `evidenceNotSynthetic`,
   `tasksGraphWellFormed`).
7. Polish → faithful lift (the M3 proof), surface + dependency-hygiene baseline, quickstart, docs.

---

## Notes

- `[P]` = different file / independent assertion, no dependency on another incomplete task in the phase.
- `[Story]` maps a task to a user story for traceability; omitted for setup/foundational/polish.
- The Spec Kit adapter is the **real** adopter under test (real evaluation, no marker); only the
  second composition domain (T015) is synthetic — disclosed at its definition, `Synthetic` token in
  asserting test names (T045), listed in the PR description (Principle V).
- **Sensing is out of scope** (FR-015): the live repository is read into `SpecKitFact`s by the F08
  effects shell / F12 CLI; tests feed facts directly.
- This feature adds **adapter code only** — the kernel and the Spi are unchanged; the dependency
  direction is adapter → SPI → kernel, never the reverse.
- Commit after each task or logical group; never mark a failing task `[X]`.
