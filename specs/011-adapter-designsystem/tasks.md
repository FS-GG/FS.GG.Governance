---
description: "Task list for F11 · 011-adapter-designsystem — the design-system adapter: a second, unrelated domain adopts the kernel from fixtures. The second concrete production adapter; supplies only its own five SPI components and reuses 100% of the kernel, sharing NONE of F10's shape (no Phase/whenPhase/merge fence/dial). Domain #2 of Milestone M3 (the adoption bar)."
---

# Tasks: The Design-System Adapter — A Second, Unrelated Domain Adopts The Kernel From Fixtures

**Input**: Design documents from `/specs/011-adapter-designsystem/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/DesignSystem.fsi](./contracts/DesignSystem.fsi), [contracts/Catalog.fsi](./contracts/Catalog.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature whose headline guarantees — the adapter being
fully specified by **exactly the five** SPI components with **100% kernel reuse**, **no**
artifact-authoring operation, and **no** phase/`whenPhase`/merge-fence/dial machinery copied from
F10 (SC-001); the **tier split** — the deterministic token/contrast/surface rules giving definite
verdicts and being the `Blocking` set, the `Opaque` judgement rules staying out of `Deterministic`
and routing to an agent with their `Question`, and `adoptNewPolicy` (`HumanOnly`) never resolving
deterministically (SC-002); the **fixture token tree** evaluating the full catalog with **no**
rendering library on the path and **zero** rendering/token/colour/layout vocabulary in the
kernel/SPI surfaces (SC-003); every rule **rendering to a sentence and explaining** itself
(SC-004); a deterministic rule's hash being **invariant** under commutative re-ordering and
structurally-equal `Opaque` rules producing the **same** cache key (SC-005); the **faithful lift** —
a lifted rule's `(verdict, provenance)`/render/hash/reads byte-for-byte identical to the standalone
original when composed alongside the **real F10 adapter** (SC-006); the **adoption bar** — two
unrelated domains coexisting, neither referencing the other (SC-007); and the **new** DesignSystem
surface-drift + dependency-hygiene baseline (DesignSystem → BCL/FSharp.Core/Spi/Kernel only, **not
F10**, SC-008) — are only credible with real evidence (Principle V). Per Principle I the semantic
tests are written against the **public** surface (through the built
`FS.GG.Governance.Adapters.DesignSystem` library / `scripts/prelude.fsx`) and MUST FAIL before the
matching `DesignSystem.fs`/`Catalog.fs` bodies exist.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations —
every task matches; `[T1]`/`[T2]` omitted throughout.

**Elmish/MVU**: **N/A — PASS** (Constitution Principle IV; plan Constitution Check row IV). This is
a **pure** value/fold layer — no multi-step state, no I/O, no `Model`/`Msg`/`Effect`, no
interpreter (FR-015), exactly as for F01–F07, F09, and F10. There are therefore **no**
`Model`/`Msg`/`Effect`/`init`/`update`/interpreter tasks; the evidence-obligations note (T017)
records this N/A explicitly. **Sensing** a live design system into `DesignSystemFact`s (reading the
token tree, capturing rendered output, computing contrast ratios, hashing artifact content) and
**wiring** the adapter into a running loop is the already-shipped F08 effects shell and the F12
CLI, not this feature — tests feed `DesignSystemFact`s drawn from a fixture token tree directly.

**Synthetic-evidence discipline (Principle V).** The design-system adapter **is the real adopter
under test** — its rules are fed **real** `DesignSystemFact`s drawn from a **real fixture token
tree** through the built library and assert real verdicts/provenance/render/hash/routes (the
deterministic-engine "prefer real evaluation" path; no marker needed). The fixture token tree is
the domain's **real input**, not a mock. **There is NO synthetic domain in this feature** — the
faithful-lift proof composes the design-system adapter alongside the **real F10 Spec Kit adapter**
(a stronger composition test than F10's, which used a synthetic toy domain). So no `Synthetic`
marker is owed anywhere; the only cross-feature reference is the **test** project → F10 (the
shipped library never references F10, enforced by the dependency-hygiene test T040).

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

**Purpose**: stand up the new pure project, its test project, the fixture token tree, and the
curated contracts so the whole surface type-checks against `failwith`-stubs before any real body
exists (Principle I).

- [X] T001 Create the new pure library project `src/FS.GG.Governance.Adapters.DesignSystem/FS.GG.Governance.Adapters.DesignSystem.fsproj` — `net10.0` (inherits `Directory.Build.props`), a **single** `ProjectReference` to `../FS.GG.Governance.Adapters.Spi/FS.GG.Governance.Adapters.Spi.fsproj` (which transitively brings the kernel), and **ZERO** `PackageReference` (BCL + `FSharp.Core` + Spi + Kernel only — **no rendering library, no serializer**, FR-016). Compile order in the fsproj: `DesignSystem.fsi`, `DesignSystem.fs`, `Catalog.fsi`, `Catalog.fs`. Set `RootNamespace` to `FS.GG.Governance.Adapters.DesignSystem`. **It MUST NOT reference `FS.GG.Governance.Adapters.SpecKit` (F10)** (FR-005/FR-016).
- [X] T002 Copy `specs/011-adapter-designsystem/contracts/DesignSystem.fsi` → `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fsi` and `specs/011-adapter-designsystem/contracts/Catalog.fsi` → `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fsi` verbatim (the curated public surface, Principle II — these are the SOLE visibility declaration; the `.fs` carry NO `private`/`internal`/`public` on top-level bindings).
- [X] T003 Add `failwith`-stub bodies `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` and `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` that satisfy the two `.fsi` (declare the types real where cheap — `DesignArtifactRef` (5 cases, **plain**, NO `RequireQualifiedAccess` — research D4), `DesignSystemFact` (7 cases), `DesignChange` record; function/value bodies `failwith "F11"`), so `dotnet build src/FS.GG.Governance.Adapters.DesignSystem` compiles. **There is deliberately NO `Phase`/`PhaseReached`/`whenPhase`/`ConstitutionDial`** — the keystone *absences* that prove this domain did not copy domain #1 (FR-005, research D3/D8).
- [X] T004 [P] Create the test project `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/FS.GG.Governance.Adapters.DesignSystem.Tests.fsproj` — Expecto + FsCheck (centrally pinned, `IsPackable=false`, `GenerateProgramFile=false`), a `ProjectReference` to `../../src/FS.GG.Governance.Adapters.DesignSystem/...` **and** (for the faithful-lift proof ONLY) to `../../src/FS.GG.Governance.Adapters.SpecKit/...` (F10) — the only place in the feature that references F10 (D9). Compile-order item group: `FixtureFacts.fs`, `ProjectFact.fs`, `DesignSystemTests.fs`, `CatalogTests.fs`, `LiftTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`. Add `Main.fs` (Expecto `runTestsInAssembly` entry point).
- [X] T005 [P] Extend `scripts/prelude.fsx` with the F11 FSI sketch from [quickstart.md](./quickstart.md) (author the adapter → five-component / no-F10-shape → tier split → advisory-by-default + token-surface fence → evidence/taint via F05 → render/explain + commutative hash → faithful lift alongside F10), drafted against the two `.fsi` with `failwith`/`Unchecked` where a body is absent. The point of this pass is that the SHAPES type-check against the two contracts and read naturally.
- [X] T006 Add `src/FS.GG.Governance.Adapters.DesignSystem` and `tests/FS.GG.Governance.Adapters.DesignSystem.Tests` to `FS.GG.Governance.sln`.

**Checkpoint**: `dotnet build` is clean with the stubs; `dotnet fsi scripts/prelude.fsx` type-checks
the F11 sketch against the two contracts; the solution lists the two new projects; the library
fsproj has **zero** `PackageReference` and **no** reference to F10.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: implement the entire `DesignSystem` module — the domain vocabulary, the artifact map,
the kernel wiring, and the four probes (incl. the F05 evidence-honesty probe) (plan module 1, the
shared infrastructure every catalog rule and every story builds on) — and author the test-side
fixture token tree and the consumer-side coproduct that composes the design-system adapter with the
**real F10 adapter**. **No user-story work can begin until this phase is complete.**

- [X] T007 Declare `DesignArtifactRef` (5 cases — `TokenDocument`/`GeneratedTokenSurface`/`RenderedCapture`/`InteractionStateSpec`/`PagePatternSpec`, **plain**, NO `RequireQualifiedAccess` — no case-name collisions, research D4), `DesignSystemFact` (7 cases — `PolicySelected of policy`/`DesignRule of ruleId`/`SurfaceObservation of probe * subject * met`/`MeasurementState of measurementId * EvidenceState`/`VerdictRestsOn of verdictId * measurementId`/`ArtifactPresent of DesignArtifactRef`/`DesignGov of RuleOutcome`), and `DesignChange` (`{ Surfaces: Set<DesignArtifactRef> }`) in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` exactly per `DesignSystem.fsi`. `MeasurementState` carries an **authored** `EvidenceState` (never `AutoSynthetic` — that is computed by `Evidence.effective`, D5). **No `PhaseReached` fact and no `Phase` field on `DesignChange`** (FR-005, research D3).
- [X] T008 Implement `DesignSystem.toRef : DesignArtifactRef -> ArtifactRef` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — the artifact mapping (FR-002), total and **injective** (distinct artifact kinds map to distinct `ArtifactRef`s — a faithful-lift precondition). Depends on T007.
- [X] T009 Implement `DesignSystem.identify : DesignSystemFact -> FactId` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — keyed by the **entity** a fact is about (`PolicySelected` by a fixed key — one selected policy; `MeasurementState` by `measurementId`; `SurfaceObservation` by `(probe, subject)` → a later fact supersedes / dedup), with value-distinguishing facts (`DesignRule`, `VerdictRestsOn`, `ArtifactPresent`) keyed by full value, **injective on value-bearing facts** (data-model L0, Hazard 4). Depends on T007.
- [X] T010 Implement `DesignSystem.bridge : JudgeId -> Bridge<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — `Embed = DesignGov`, `Project` the inverse partial map, `Judge = judge`, `ArtifactHash = fun _ _ -> ""` (the pure adapter holds no content; F08 supplies real hashes at the edge, FR-015). The **unchanged** F04 bridge wiring — no new cross-cutting code (research D2). Depends on T007.
- [X] T011 [P] Implement `DesignSystem.surfaceMatches : generated -> source -> Check<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — the token-drift probe over `SurfaceObservation ("surface-matches", generated, _)`: `Met` when `met`, `Unmet` when not, `Unknown` when the generated surface is absent (`ArtifactPresent` distinguishes absent from failing). The `generated`/`source` `DesignArtifactRef`s appear in the `Reads`/`Args` so render/hash distinguish them (data-model Pr1–Pr4). A missing fixture is **never a silent `Met`** (Principle VI). Depends on T007.
- [X] T012 [P] Implement `DesignSystem.contrastMeets : policy -> surface -> Check<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — the colour/contrast probe over `SurfaceObservation ("contrast-meets", surface, _)`, with `policy` carried as a `LiteralArg` so render/hash distinguish policies (`contrastMeets "AntAA" s` ≠ `contrastMeets "WCAGAAA" s`); `Unknown` when the surface fixture is absent — a missing contrast fixture is never a silent `Met` (edge case, Pr3/Pr4). Depends on T007.
- [X] T013 [P] Implement `DesignSystem.surfaceObserved : name -> subject -> Check<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — the shared deterministic surface probe over `SurfaceObservation (name, subject, _)` (the shape behind spacing-scale, control-height, intent-coverage, visual-state); `Met`/`Unmet`/`Unknown` exactly as `surfaceMatches` (Pr1–Pr3). Depends on T007.
- [X] T014 Implement `DesignSystem.evidenceMeasured : Check<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — the F05 taint realization: build `Evidence.build [ (m,s) for MeasurementState (m,s) ] [ (v,m) for VerdictRestsOn (v,m) ]`; on `Ok graph` report `Met` iff NO node's `Evidence.effective` state is `Synthetic`/`AutoSynthetic`, else `Unmet` (with the offending id); on `Error e` report `Unmet` with the `GraphError` (a malformed graph distinguishable from a real taint, Principle VI). The `AutoSynthetic` taint propagates down the `VerdictRestsOn` chain by the **kernel's** least fixed point — the adapter ships **no** graph engine (research D7, data-model E1–E3). Depends on T007.
- [X] T015 Implement `DesignSystem.probes : Probe<DesignSystemFact> list` in `src/FS.GG.Governance.Adapters.DesignSystem/DesignSystem.fs` — the declared atomic-predicate vocabulary the catalog composes (component 3), carried for the contract and testing; the `Catalog` rules' checks remain authoritative for evaluation (research D2). Depends on T011–T014.
- [X] T016 [P] Author the **fixture token tree** under `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/fixtures/` (a few JSON/RON files — a token document, a generated token surface, an interaction-state spec, a page-pattern spec — **no rendering dependency**, FR-010) **and** `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/FixtureFacts.fs`, a TEST-only helper that reads those fixture files (BCL `System.Text.Json` only — no new package; this is test scaffolding, the shipped adapter stays pure) and lifts them to `DesignSystemFact`s. This is the domain's **real input** (Principle V), and the substrate every catalog evaluation/explanation test runs against. **Sensing is out of scope** (FR-015) — this helper is the test harness, not the adapter.
- [X] T017 Author `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/ProjectFact.fs` — the consumer-authored closed `ProjectFact` coproduct (`Design of DesignSystemFact | SpecKit of SpecKitFact | Governance of RuleOutcome`), the single-case active patterns (`(|DesignP|_|)`, `(|SpecKitP|_|)`), the `inject` constructors, and the project `Identify`/`Bridge` that **agree with** `DesignSystem.identify`/`DesignSystem.bridge` on injected `DesignSystemFact`s (data-model L0/L3, F09 law L3) — composing the design-system adapter alongside the **real F10 Spec Kit adapter** (D9; this is the **only** place F11 references F10). Record the **evidence-obligations** note at the top (a comment block): (a) **Principle IV is N/A** — pure value/fold layer, no `Model`/`Msg`/`Effect`/interpreter (no boundary tasks owed); (b) the design-system adapter is the **real adopter under test** (real evaluation, no marker), composed against the **real F10 adapter** — **no synthetic domain in this feature**; (c) **sensing is out of scope** (FR-015) — tests feed fixture-drawn facts directly; (d) the shipped library never references F10 — only this test project does (SC-008). Depends on T007.

**Checkpoint**: the whole `DesignSystem` module is implemented against `DesignSystem.fsi` and
builds; the fixture token tree + `FixtureFacts.fs` produce real `DesignSystemFact`s; the
`ProjectFact` coproduct composing the real F10 adapter compiles; the N/A-Principle-IV and
no-synthetic-domain disclosures are written down. Every user story can now begin.

---

## Phase 3: User Story 1 — A second, unrelated domain governs a design language from the five SPI components, without copying domain #1's shape (Priority: P1) 🎯 MVP

**Goal**: assemble the single `Adapter<DesignSystemFact, DesignArtifactRef, DesignChange>` value from
**exactly** the five SPI components + the F04 `Bridge`, with the rule **catalog** as a set of reified
`CheckRule`s — and **no** inference/arbitration/evidence/render/hash/explain/severity/routing code,
**no** artifact-authoring operation, and **no** `Phase`/`whenPhase`/merge-fence/dial machinery copied
from F10 in the adapter (SC-001).

**Independent Test**: build `Catalog.adapter judge`; confirm it governs fixture-drawn
`DesignSystemFact`s end-to-end through the kernel (`Adapter.toRules` → `FixedPoint.evaluate` →
`Route.route`, `Check.render`/`Check.explain`) and that the adapter module contains none of the
cross-cutting facilities, no write-artifact function, and **none of F10's shape** (no `Phase`, no
`whenPhase`, no merge fence, no dial, no reference to F10).

### Tests for User Story 1 ⚠️ (write FIRST; must FAIL before T019–T028)

- [X] T018 [P] [US1] **V1** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/DesignSystemTests.fs`: the adapter is fully specified by the **five** components + `Bridge` — assert `Catalog.adapter judge` carries `Identify`/`ToRef`/`Probes`/`Rules`/`Fences`/`Bridge`, that `adapter.Fences.Length = 1` (token-surface only — NO merge fence) and `adapter.Rules.Length = 15`, and that it governs fixture-drawn facts via kernel entry points only (`Adapter.toRules` → `FixedPoint.evaluate adapter.Identify … supplied` derives the expected facts; `Route.route adapter.Fences adapter.Rules mode change` routes). The "100% kernel reuse — no inference/arbitration/evidence/render/hash/explain/severity/routing code, **no artifact-authoring operation**, and **no F10 shape** (no `Phase`/`whenPhase`/merge-fence/dial, no reference to F10)" claim is a **structural** property: verify by inspection / a dependency-symbol review note in the test header (the adapter's definitions call only `FS.GG.Governance.Kernel` + Spi APIs; no `System.IO`, no rendering type, no write-token/write-capture/write-spec function, no `Phase` type) — NOT a runtime assertion (FR-003/FR-004/FR-005, SC-001). Also assert (cheap, faithful-lift preconditions): `DesignSystem.toRef` is **injective** over all five `DesignArtifactRef` cases (FR-002), and `DesignSystem.identify` is **injective on value-bearing facts** while keying `PolicySelected`/`MeasurementState`/`SurfaceObservation` by entity so a later fact supersedes (data-model L0).

### Implementation for User Story 1

- [X] T019 [P] [US1] Implement the deterministic **blocking** rule `Catalog.tokenDrift : CheckRule<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` via `CheckRule.blocking` over `DesignSystem.surfaceMatches GeneratedTokenSurface TokenDocument`; `Deterministic`, `Blocking` (FR-007). `RuleId "token-drift"`.
- [X] T020 [P] [US1] Implement the deterministic **blocking** rule `Catalog.contrastPolicy : CheckRule<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` via `CheckRule.blocking` over `DesignSystem.contrastMeets policy GeneratedTokenSurface`; `Deterministic`, `Blocking` (FR-007). `RuleId "contrast-policy"`.
- [X] T021 [P] [US1] Implement the deterministic **blocking** rule `Catalog.tokenSurfaceGate : CheckRule<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` via `CheckRule.blocking` over `DesignSystem.surfaceObserved "token-surface-gate" GeneratedTokenSurface`; `Deterministic`, `Blocking` (FR-007/FR-009) — the high-stakes surface `tokenSurfaceFence` names. `RuleId "token-surface-gate"`.
- [X] T022 [P] [US1] Implement the deterministic **blocking** rule `Catalog.evidenceMeasured : CheckRule<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` via `CheckRule.blocking` over `DesignSystem.evidenceMeasured` (T014); `Deterministic`, `Blocking` — honesty about evidence is non-negotiable, no flag flips it (research D7, data-model E1–E3). `RuleId "evidence-measured"`.
- [X] T023 [P] [US1] Implement the four deterministic **advisory** rules in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` via `CheckRule.rule` over `DesignSystem.surfaceObserved`: `spacingScale` (`"spacing-scale" GeneratedTokenSurface`), `controlHeightDefaults` (`"control-height" GeneratedTokenSurface`), `intentCoverage` (`"intent-coverage" GeneratedTokenSurface`), `visualStateResolution` (`"visual-state" InteractionStateSpec`); each `Deterministic`, `Advisory` (FR-007). `RuleId`s `"spacing-scale"`/`"control-height"`/`"intent-coverage"`/`"visual-state"`.
- [X] T024 [P] [US1] Implement the six `AgentReviewed` **advisory** judgement rules in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` via `CheckRule.asking` over an `Opaque` check carrying the rule's `Question` as the agent's prompt (so each is forced out of `Deterministic` and routes to a reviewer, FR-008): `renderedMatchesIntent` ("Does the rendered control match the spec intent? List divergences."), `fourValues` ("Are the four values — natural/certain/meaningful/growing — honoured?"), `pagePatternCorrect` ("Is the page pattern correct for this composition?"), `colourInformational` ("Does colour carry information here, or is it decoration?"), `motionRestraint` ("Is motion used with restraint?"), `elevationLayering` ("Is elevation/overlay layering correct?"); each `AgentReviewed`, `Advisory` (FR-007). `RuleId`s `"rendered-matches-intent"`/`"four-values"`/`"page-pattern"`/`"colour-informational"`/`"motion-restraint"`/`"elevation-layering"`.
- [X] T025 [P] [US1] Implement the `HumanOnly` **blocking** rule `Catalog.adoptNewPolicy : CheckRule<DesignSystemFact>` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` — adopting a new design policy (e.g. switching to Material), over an `Opaque` check (a `HumanOnly` decision is not reified); `HumanOnly`, `Blocking` — `toRule` escalates it to a person and it never resolves by engine or agent (FR-007, edge case). `RuleId "adopt-new-policy"`.
- [X] T026 [US1] Implement `Catalog.catalog : CheckRule<DesignSystemFact> list` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` — all **fifteen** rules in a stable order, at their fixed severities (the four deterministic + the `HumanOnly` `adoptNewPolicy` are `Blocking`; the rest `Advisory` — there is **no dial** to vary this, data-model C3/D8). Depends on T019–T025.
- [X] T027 [US1] Implement `Catalog.tokenSurfaceFence : Fence<DesignChange>` and `Catalog.fences : Fence<DesignChange> list` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` — `{ Name = "token-surface"; Trips = fun c -> c.Surfaces.Contains GeneratedTokenSurface }` and `fences = [ tokenSurfaceFence ]`. A **surface** fence, NOT a lifecycle/merge fence (the keystone difference from F10 — the design domain has no phases, research D3/D8); kept short by design (FR-009). The fence carries no rules; `Route.route` partitions by `Severity & Fenced & Gate`.
- [X] T028 [US1] Implement `Catalog.adapter : JudgeId -> Adapter<DesignSystemFact, DesignArtifactRef, DesignChange>` in `src/FS.GG.Governance.Adapters.DesignSystem/Catalog.fs` — the five components (`DesignSystem.identify`, `DesignSystem.toRef`, `DesignSystem.probes`, `catalog`, `fences`) + `DesignSystem.bridge judge`. Takes **only** a `JudgeId` (no dial — research D8). Supplies NOTHING else (SC-001). Depends on T026, T027.

**Checkpoint**: US1 is independently testable — the single adapter, supplying only its own five
components and sharing **none** of F10's shape, governs fixture-drawn `DesignSystemFact`s entirely
through the kernel (the thesis of the adoption bar for domain #2). **MVP.**

---

## Phase 4: User Story 2 — The tiered catalog: deterministic token/contrast checks block, visual-judgement rules route to an agent, policy adoption is human-only (Priority: P1)

**Goal**: prove the tier split over the catalog assembled in US1 — the deterministic token-drift /
contrast-policy / token-surface-gate / evidence-measured rules produce definite verdicts and are the
`Blocking` set; the `Opaque` judgement rules stay out of the `Deterministic` tier, never resolve to
`Pass`/`Fail`, and route to an agent whose prompt is their `Question`; `adoptNewPolicy` (`HumanOnly`)
escalates to a person and never resolves deterministically (FR-006/FR-007/FR-008, SC-002).

**Independent Test**: evaluate the deterministic rules over fixture facts and confirm definite
verdicts + `Blocking`; evaluate an `Opaque` judgement rule and confirm `AgentReviewed` /
`isReified = false` / carries its `Question` / never `Pass`/`Fail`; evaluate `adoptNewPolicy` and
confirm it routes to a person and stays undecided by the engine.

### Tests for User Story 2 ⚠️ (write FIRST)

- [X] T029 [P] [US2] **V2 (deterministic block)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/CatalogTests.fs`: over fixture facts, assert `tokenDrift`/`contrastPolicy`/`tokenSurfaceGate`/`evidenceMeasured` produce **definite** verdicts — `Check.eval` is `Pass` for a satisfied `SurfaceObservation … true`, `Fail` for `… false` (drift / contrast violation, data-model Pr1/Pr2, edge case "generated surface drifts") — and are the catalog's `Blocking` rules (`Severity = Blocking`). Also assert a **missing** fixture ⇒ `Uncertain` (`Unknown`), never a silent `Pass` (Pr3, edge case "contrast fixture missing").
- [X] T030 [P] [US2] **V2 (Opaque judgement routes, never resolves)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/CatalogTests.fs`: for each of the six `AgentReviewed` rules, assert `Check.isReified r.Check = false` (kept out of `Deterministic`, FR-008), `r.Tier = AgentReviewed`, `Check.eval facts r.Check = Uncertain` for **any** facts (never `Pass`/`Fail`, data-model C4), and that `toRule` routes it to a review whose prompt is the rule's `Question` (the question is non-empty and carried through). Edge case "judgement rule, no agent available": it stays `Uncertain`/advisory.
- [X] T031 [P] [US2] **V2 (HumanOnly escalates) + advisory-by-default + the surface fence** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/CatalogTests.fs`: assert `adoptNewPolicy.Tier = HumanOnly`, `Check.eval facts adoptNewPolicy.Check = Uncertain` for any facts, and `toRule` emits `Escalated` — a person decides, never the engine (data-model C4, edge case). Then assert the **advisory-by-default + token-surface fence** routing: `Route.route adapter.Fences adapter.Rules Gate plainChange` (a change NOT touching `GeneratedTokenSurface`) yields `Blocking = []`, while `Route.route adapter.Fences adapter.Rules Gate surfaceChange` (touching `GeneratedTokenSurface`) yields the deterministic/`HumanOnly` blocking set (data-model §3, edge case "default posture is advisory"). The host (F08/F12) chooses the mode — the adapter ships no run-mode logic (D8).

**Checkpoint**: every rule is routed by its tier — the few deterministic contract-bearing checks
block, the visual-judgement rules route to an agent with their question, policy adoption escalates
to a person; the default posture is advisory and only a token-surface change trips the single fence.

---

## Phase 5: User Story 3 — The adapter runs against a fixture token tree: no rendering dependency, no rendering vocabulary in generic code (Priority: P1)

**Goal**: prove the adapter is exercised entirely against the **fixture token tree** with **no**
rendering library on the path, the probes report a three-valued `Outcome` over fixtures as data, and
**zero** rendering/token/colour/layout vocabulary appears in the kernel or SPI surfaces — all design
vocabulary is confined to the adapter's closed `DesignSystemFact`/`DesignArtifactRef` (FR-010/FR-011,
SC-003).

**Independent Test**: point the adapter at the fixture token tree, supply the corresponding facts,
confirm the full catalog evaluates and explains with no rendering library referenced; then inspect
the kernel/SPI surfaces and confirm no rendering/token/colour/layout vocabulary appears outside the
adapter's closed types.

### Tests for User Story 3 ⚠️ (write FIRST)

- [X] T032 [P] [US3] **V3 (full catalog over fixtures)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/DesignSystemTests.fs`: over the `FixtureFacts.fs` facts (T016), assert **every** `r ∈ Catalog.catalog` produces a `Check.eval` verdict and a `Check.explain` — the full catalog evaluates and explains with **no** rendering library on the path (FR-010, the dependency-hygiene test T040 enforces the no-renderer footprint).
- [X] T033 [P] [US3] **V3 (probes are three-valued over fixtures)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/DesignSystemTests.fs`: assert `surfaceMatches`/`contrastMeets`/`surfaceObserved` read fixture facts and report `Met` (observation `true`), `Unmet` (observation `false`), and `Unknown` (the subject artifact **absent** — `ArtifactPresent` distinguishes absent from failing) — they never render, capture, or author (FR-004/FR-010, data-model Pr1–Pr3, edge cases "generated surface drifts" / "contrast fixture missing").
- [X] T034 [P] [US3] **V3 (no rendering vocabulary leak)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/SurfaceDriftTests.fs`: inspect the committed `surface/` baselines for the kernel and the SPI and assert they carry **zero** rendering/token/colour/layout vocabulary (no `Token`/`Colour`/`Contrast`/`Render`/`Layout`/`DesignArtifactRef` symbols) — all design vocabulary is confined to the adapter's closed `DesignSystemFact`/`DesignArtifactRef`, so removing the adapter removes the design vocabulary entirely (FR-011, data-model N1, edge case "rendering vocabulary leak").

**Checkpoint**: the adapter adopts the kernel **cheaply** (testable from fixtures, no renderer) and
the kernel stayed **generic** (deletable-down-to-neutral) — the boundary is in the right place.

---

## Phase 6: User Story 4 — Every deterministic rule renders to a sentence and hashes stably; advertised equals enforced (Priority: P2)

**Goal**: prove every rule renders to a non-empty human-readable sentence and explains itself with a
root verdict equal to `eval` (so the published `Statement` equals `Check.render` of the value `eval`
ran — advertised = enforced), and that a deterministic rule's hash is invariant under commutative
re-ordering of its sub-checks while positional nodes stay positional (so the F04 agent-review cache
key does not move under cosmetic re-ordering) (FR-012/FR-013, SC-004/SC-005).

**Independent Test**: for 100% of the catalog confirm a non-empty `Check.render`, `Check.explain`
top verdict `= Check.eval`, and `Statement = Check.render`; confirm a deterministic rule's
`Check.hash` is invariant under commutative re-ordering and two structurally-equal `Opaque`
judgement rules produce the same cache key.

### Tests for User Story 4 ⚠️ (write FIRST)

- [X] T035 [P] [US4] **V4 (render & explain — advertised = enforced)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/CatalogTests.fs`: for **every** `r ∈ Catalog.catalog`, assert `Check.render r.Check` is a non-empty sentence, `(Check.explain facts r.Check)` top verdict `= Check.eval facts r.Check`, and the rule's published `Statement` (the `SpecSource`/contract sentence) equals `Check.render r.Check` — what the contract advertises is byte-for-byte what `eval` enforces (data-model C2, SC-004).
- [X] T036 [P] [US4] **V5 (commutative hash + cache-key stability)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/CatalogTests.fs`: assert a deterministic rule combining sub-checks under `allOf`/`anyOf` has `Check.hash` **invariant** under re-ordering of those members (`hash (allOf [a;b]) = hash (allOf [b;a])`) while positional nodes (a probe's ordered `Args`/`Reads`, `Implies`) stay positional (`surfaceMatches g c` ≠ `surfaceMatches c g`); and assert two structurally-equal `Opaque` judgement rules produce the **same** F04 agent-review cache key — so cosmetic re-ordering forces no spurious re-review (data-model H1/Pr4, edge case "commutative re-ordering", SC-005).

**Checkpoint**: the catalog is self-describing and cache-stable — the published contract cannot lie,
and cosmetic re-ordering cannot invalidate cached agent reviews.

---

## Phase 7: User Story 5 — The adapter lifts unchanged and composes alongside the Spec Kit adapter at one root (Priority: P2)

**Goal**: prove the F09 faithful-lift guarantee for domain #2 — for 100% of the catalog the lifted
rule's `(verdict, provenance)`, render, hash, and reads over coproduct-wrapped facts are byte-for-byte
identical to the standalone original — by composing the design-system adapter alongside the **real F10
Spec Kit adapter** at one root, with neither domain referencing the other (FR-014, SC-006/SC-007).

**Independent Test**: evaluate every catalog rule standalone over `DesignSystemFact`s and again lifted
over `ProjectFact`-wrapped facts at a root that **also** carries the real F10 adapter; confirm
`(verdict, provenance)`/render/hash/reads identical for 100% of the catalog; confirm the design-system
adapter references only the SPI and the kernel, never F10, and dropping one `Lifted` removes its domain
cleanly.

### Tests for User Story 5 ⚠️ (write FIRST)

- [X] T037 [P] [US5] **V6 (faithful lift, SC-006)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/LiftTests.fs`: compose the design-system adapter with the **real F10 Spec Kit adapter** (via `ProjectFact.fs`, T017) at a test root through `Composition.lift`/`compose`; assert that for **100%** of `Catalog.catalog`, the lifted rule's `(verdict, provenance)` over coproduct-wrapped facts is **byte-for-byte identical** to the standalone original over the projected facts (F09 laws L2/L3, data-model L2), and `Check.render`/`Check.hash`/`Check.reads`/`Check.isReified` are **invariant** under the lift (L1 — a lifted `Opaque` stays opaque, the agent-review cache key does not move). No `Synthetic` marker — both adapters are real (Principle V).
- [X] T038 [P] [US5] **V7 (adoption bar, SC-007)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/LiftTests.fs`: assert the two unrelated domains (design-system, Spec Kit) coexist at one root — neither references the other, the design-system adapter shares **no** vocabulary or shape with F10 (no `Phase`/`whenPhase`/merge fence), and **dropping** the design-system `Lifted` from the `compose` list removes it cleanly while the Spec Kit domain still evaluates (a cross-domain rule naming the absent design domain goes inert, F09 R2/R3, data-model L3) — the adoption bar is met.

**Checkpoint**: the spine composes without surprise — the design-system adapter lifts unchanged and
coexists with a genuinely unrelated real domain at one root; the F09 guarantee holds for domain #2.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: the new surface + dependency-hygiene baseline, quickstart validation, and docs —
concerns that span the whole catalog.

- [X] T039 **V8 (surface baseline, SC-008)** — add `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/SurfaceDriftTests.fs` (a reflective test that the built `FS.GG.Governance.Adapters.DesignSystem` public surface equals the committed baseline), create the **new** baseline `surface/FS.GG.Governance.Adapters.DesignSystem.surface.txt`, and bless it (`BLESS_SURFACE=1 dotnet test`). Confirm the two `.fsi` are the sole visibility declaration (no `private`/`internal`/`public` on `.fs` top-level bindings; FR-017). (This file also hosts the kernel/SPI no-leak inspection from T034.)
- [X] T040 **V8 (dependency hygiene, SC-008)** in `tests/FS.GG.Governance.Adapters.DesignSystem.Tests/SurfaceDriftTests.fs`: assert the DesignSystem assembly's referenced assemblies ⊆ `{ BCL, FSharp.Core, FS.GG.Governance.Adapters.Spi, FS.GG.Governance.Kernel }` (zero new dependency, **no rendering library**, and crucially **NOT `FS.GG.Governance.Adapters.SpecKit`** — FR-005/FR-016), and that neither the kernel, nor the Spi, nor the SpecKit (F10) assembly references DesignSystem (the dependency direction is adapter → SPI → kernel, never the reverse, and never adapter → adapter).
- [X] T041 Run the [quickstart.md](./quickstart.md) validation V1–V8 end-to-end: `dotnet build src/FS.GG.Governance.Adapters.DesignSystem`, `dotnet fsi scripts/prelude.fsx` (the F11 sketch type-checks and runs against the real bodies), `dotnet test` (all DesignSystem/Catalog/Lift/SurfaceDrift tests green). Replace the `failwith`/`Unchecked` stubs in the prelude sketch with the real calls.
- [X] T042 [P] Update `README.md` (note F11 — the second concrete adapter, domain #2 of M3, adoption by difference) and run `speckit-agent-context-update` to refresh the managed Spec Kit section / `CLAUDE.md` pointer; confirm no kernel, Spi, or SpecKit source changed (this feature adds adapter code only).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundational (Phase 2)** — depends on Setup; implements the whole `DesignSystem` module + the fixture token tree + the `ProjectFact` coproduct (composing the real F10 adapter); **BLOCKS all user stories**.
- **User stories (Phases 3–7)** — all depend on Foundational. Recommended order is priority order **US1 (P1) → US2 (P1) → US3 (P1) → US4 (P2) → US5 (P2)**; once Foundational is done they are otherwise independent (US2–US5 are test phases over the catalog assembled in US1).
- **Polish (Phase 8)** — depends on all desired user stories; T039/T040 (surface/hygiene) need the final public surface; T041 (quickstart) needs the whole feature.

### Cross-story dependencies (beyond plain phase order)

- US1's `Catalog.catalog`/`adapter` (T026/T028) require all fifteen rules (T019–T025) — implemented in US1 with their **real** bodies (no stub phase is needed; unlike F10 no rule's body depends on another story).
- US2–US5 all test the `adapter`/`catalog` assembled in US1 (T026/T028) — they need Phase 3 complete.
- US3 (T032/T033) and US1 (T032's evaluation) run over the fixture token tree authored foundationally (T016).
- US5 (T037/T038) needs the `ProjectFact` coproduct (T017) and the catalog (T026).
- The `evidenceMeasured` catalog rule (T022) wraps the foundational `DesignSystem.evidenceMeasured` probe (T014).

### Within each user story

- Tests (the `⚠️ write FIRST` group) MUST be authored and FAIL before the matching implementation.
- Vocabulary/wiring/probes before rules; rules before `catalog`; `catalog`/`fences` before `adapter`.

### Parallel opportunities

- Setup: T004, T005 are `[P]` (distinct files) once T001–T003 exist.
- Foundational: T011/T012/T013/T014 are `[P]` (distinct probe functions in `DesignSystem.fs` — coordinate edits); T016/T017 are `[P]` (distinct test files).
- US1: the rule impls T019–T025 are `[P]` (distinct bindings in `Catalog.fs` — coordinate edits); T026–T028 are sequential (catalog → fences → adapter).
- US2/US3/US4/US5: all the `⚠️` tests within a story are `[P]` (independent test cases, often in the same file).
- Polish: T042 is `[P]` with T039–T041.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (the whole `DesignSystem` module + fixtures + the coproduct).
2. Phase 3 US1 — assemble the `Adapter` from exactly the five components; the catalog is fifteen
   reified `CheckRule`s; the single fence is over the token surface.
3. **STOP and VALIDATE**: the adapter governs fixture-drawn `DesignSystemFact`s entirely through the
   kernel, contains no cross-cutting code, no authoring op, and **none of F10's shape** (SC-001).
   The adoption-bar thesis for domain #2 — generality **by difference** — is demonstrable.

### Incremental delivery

1. Setup + Foundational → the vocabulary, probes, wiring, fixtures, and coproduct are real.
2. US1 → the adapter + catalog (MVP — the five-component, observer-only, no-F10-shape thesis).
3. US2 → the tier split proven (deterministic block / agent route / human escalate; advisory fence).
4. US3 → fixtures-only, no renderer on the path, no rendering vocabulary in the kernel/SPI.
5. US4 → render-and-hash: advertised = enforced, cache key stable under re-ordering.
6. US5 → the faithful lift alongside the **real F10 adapter** (the M3 adoption-bar proof).
7. Polish → surface + dependency-hygiene baseline (DesignSystem → BCL/FSharp.Core/Spi/Kernel, NOT
   F10), quickstart, docs.

---

## Notes

- `[P]` = different file / independent assertion, no dependency on another incomplete task in the phase.
- `[Story]` maps a task to a user story for traceability; omitted for setup/foundational/polish.
- The design-system adapter is the **real** adopter under test (real evaluation, no marker), and the
  faithful-lift proof composes it against the **real F10 adapter** — **there is no synthetic domain
  in this feature** (a stronger proof than F10's), so no `Synthetic` marker is owed anywhere.
- **Sensing is out of scope** (FR-015): a live design system (the token tree, rendered captures,
  contrast ratios, artifact-content hashes) is read into `DesignSystemFact`s by the F08 effects shell
  / F12 CLI; tests feed fixture-drawn facts directly.
- **The shipped adapter never references F10** (FR-005/FR-016): the two adapters are independent
  siblings. Only the **test** project references F10, solely to prove the faithful lift / adoption
  bar (D9); the dependency-hygiene test (T040) enforces the asymmetry.
- **Generality by difference**: the deliberate **absence** of `Phase`/`whenPhase`/merge-fence/dial/
  `RequireQualifiedAccess` (all present in F10) is the feature's thesis, not a simplification debt.
- This feature adds **adapter code only** — the kernel, the Spi, and F10 are unchanged; the
  dependency direction is adapter → SPI → kernel, never the reverse, and never adapter → adapter.
- Commit after each task or logical group; never mark a failing task `[X]`.
