---
description: "Task list for 072-sdd-first-class-integration"
---

# Tasks: SDD First-Class Reference Integration (Template + Tutorials)

**Input**: Design documents from `/specs/072-sdd-first-class-integration/`

**Prerequisites**: plan.md, spec.md, research.md (D0–D8), data-model.md, contracts/reference-provider.md, quickstart.md

**Tests**: Tests are central to this feature — the layered worked example, failure-path
examples, and surface-drift guard ARE the deliverable (FR-008/FR-009). Included accordingly.

**Change classification**: **Tier 1** (spec Assumptions, plan Constitution Check). The
feature adds one new public surface (the reference provider) + new sample/test projects;
the generic-core baselines stay byte-identical (SC-006). Tier matches across all phases, so
no per-task `[T1]` annotations are emitted.

**Elmish/MVU applicability**: Principle IV is satisfied **by reuse** — the stateful/I/O
scaffold workflow is 071's `Loop` (pure) + `Interpreter` (edge), consumed unchanged. This
feature adds **no** new product workflow; the reference provider is a pure data value and
the only new I/O (`dotnet build`, fixture seeding) is test-edge process execution. No new
`.fsi` `Model`/`Msg`/`Effect`/`init`/`update`/interpreter tasks are required (plan
Constitution Check IV; research D7). The genericity guard (T0-surface) is the standing proof.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `[US1]`/`[US2]`/`[US3]`; unlabeled tasks are setup/foundational/polish

## Path Conventions

Single-repo F# library layout (the repo's only shape). New top-level `samples/`, plus
`tests/`, `surface/`, `fixtures/`, `docs/tutorials/` per plan Project Structure.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project skeletons, solution wiring, and the empty directories the later
phases write into. No behavior yet.

- [X] T001 Create the reference-provider project at `samples/FS.GG.Governance.Sample.SddReferenceProvider/FS.GG.Governance.Sample.SddReferenceProvider.fsproj` — `net10.0`, `<IsPackable>false</IsPackable>`, a single `ProjectReference` to `../../src/FS.GG.Governance.Scaffold/FS.GG.Governance.Scaffold.fsproj`, and **no** third-party `PackageReference` (FSharp.Core only). `Compile` order: `SddReferenceProvider.fsi` then `SddReferenceProvider.fs` (plan Project Structure, Constitution Engineering Constraints; research D1).
- [X] T002 Create the test project at `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests.fsproj` — `<IsPackable>false</IsPackable>`, `<GenerateProgramFile>false</GenerateProgramFile>`, Expecto + `Microsoft.NET.Test.Sdk` + `YoloDev.Expecto.TestSdk` package refs, `ProjectReference`s to the sample provider AND `../../src/FS.GG.Governance.ScaffoldManifestJson/...` (for the manifest projection), `Compile` order `Support.fs; WorkedExampleTests.fs; FailurePathTests.fs; SurfaceDriftTests.fs; Main.fs` (mirror `tests/FS.GG.Governance.Scaffold.Tests/*.fsproj`).
- [X] T003 [P] Add `Main.fs` to the test project (`module …Tests.Main`; `[<EntryPoint>] let main argv = runTestsInAssemblyWithCLIArgs [] argv`), copying the convention from `tests/FS.GG.Governance.Scaffold.Tests/Main.fs`.
- [X] T004 [P] Add both new projects to `FS.GG.Governance.sln` (`dotnet sln FS.GG.Governance.sln add …`) and create the empty target directories `fixtures/sdd-reference/` and `docs/tutorials/` so later golden/tutorial tasks have a home.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` resolves both new (empty-bodied) projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The reference provider value and its genericity guard. The `provider` binding
is consumed by **every** user story (US1 worked example, US2 clone, US3 worked subject), so
it must exist first. The surface guard (SC-006) is the standing no-regression proof.

**⚠️ CRITICAL**: No user-story work can begin until the `provider` value compiles and the
core baselines are proven unchanged.

- [X] T005 Principle-I FSI pass: in `scripts/prelude.fsx` (or a sibling `scripts/sdd-reference.fsx`), `#r` the built `FS.GG.Governance.Scaffold.dll`, sketch the reference `provider` record + pure `Emit`, and exercise it against a literal `ScaffoldRequest` so the contract is driven in FSI **before** the `.fs` body (Constitution Principle I; plan Constitution Check I).
- [X] T006 Author `samples/FS.GG.Governance.Sample.SddReferenceProvider/SddReferenceProvider.fsi` exposing exactly `val providerId : Model.ProviderId` and `val provider : Model.TemplateProvider` (no access modifiers in the `.fs`; visibility lives here — Principle II). Curated surface per contracts/reference-provider.md R1 and data-model.md §1.
- [X] T007 Implement `samples/FS.GG.Governance.Sample.SddReferenceProvider/SddReferenceProvider.fs` — `providerId = ProviderId "fsgg.sample.sdd-reference"`; `provider` with `ContractVersion = { Major = 1; Minor = 0 }` and a **pure** `Emit : ScaffoldRequest -> Result<ProviderEmission, ProviderError>` that derives `<App>` from `request.Target`'s leaf name and returns the fixed file set from data-model.md §2 / contracts R2 (`<App>.sln`, `src/<App>/<App>.fsproj`, `src/<App>/Program.fs`, `tests/<App>.Tests/<App>.Tests.fsproj`, `tests/<App>.Tests/Tests.fs`, `README.md`). Emitted contents are literal strings; dependency closure = FSharp.Core only; no clock/guid/env/throw; deterministic order (research D2/D6; contract R1 obligations 1–5).
- [X] T008 Create the additive surface baseline `surface/FS.GG.Governance.Sample.SddReferenceProvider.surface.txt` by running the drift test once with `BLESS_SURFACE=1` (after T009). Confirm the two **core** baselines `surface/FS.GG.Governance.Scaffold.surface.txt` and `surface/FS.GG.Governance.ScaffoldManifestJson.surface.txt` are **untouched** in `git diff` (SC-006; contract R6).
- [X] T009 Write `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/SurfaceDriftTests.fs` (model on `tests/FS.GG.Governance.RefreshJson.Tests/SurfaceDriftTests.fs`): reflect the sample provider's public surface against its own baseline (`BLESS_SURFACE=1` regen path), **and** add assertions that the two core baselines equal their committed bytes — the SC-006 no-delta guard (contract R6; quickstart Scenario 5). Reflection lives ONLY in this test (Principle III).

**Checkpoint**: `provider` resolves through the packed `Scaffold` surface; the surface-drift
test is green and proves the generic core gained no knowledge.

---

## Phase 3: User Story 1 — Adopter: empty dir → buildable, governed product (Priority: P1) 🎯 MVP

**Goal**: An automated, layered worked example that takes an empty temp directory through a
disclosed lifecycle precondition and the runtime layer via the seam, `dotnet build`s the
result, and asserts a byte-stable manifest golden — the proof the 071 seam works for a real
customer (FR-001/FR-003/FR-004/FR-009; SC-001/SC-002/SC-003).

**Independent Test**: Run `dotnet test tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests`
selecting only the worked-example list against a fresh temp dir; the runtime skeleton
appears, builds first-attempt with no hand-editing, and the manifest matches the golden.

### Tests & evidence for User Story 1

- [X] T010 [US1] Write `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/Support.fs`: the disclosed lifecycle-precondition stand-in (a few literal `.fsgg/`/`work/`/`readiness/` paths seeded as `ScaffoldRequest.ReservedPaths`, marked `// SYNTHETIC: lifecycle layer is sibling-owned fsgg-sdd init output — research D4`), a fresh-temp-dir helper, a `dotnet build` runner that returns exit code + detects a missing SDK, a `repoRoot` walk, and the golden path constant `fixtures/sdd-reference/scaffold-manifest.golden.json` (data-model §3; contract R3; Principle V disclosure).
- [X] T011 [US1] Write the happy-path test in `WorkedExampleTests.fs`: seed the lifecycle stand-in, run `Interpreter.run (Interpreter.realPorts target) { Request = req; Provider = Some SddReferenceProvider.provider }`, assert terminal `Outcome = Scaffolded`, the §R2 files exist under `target` each `ProviderOwned`, and `Collisions = []` (contract R3 steps 1–3; quickstart Scenario 1).
- [X] T012 [US1] Add the **real-evidence build step** to `WorkedExampleTests.fs`: `dotnet build <target>/<App>.sln` ⇒ exit 0, no hand-editing (FR-004, SC-002). When the SDK is absent, **skip with a named prerequisite rationale** (not a failure), distinguishable from a tool defect (research D3; Principle VI; quickstart edge table).
- [X] T013 [US1] Generate the committed manifest golden `fixtures/sdd-reference/scaffold-manifest.golden.json` by projecting the terminal manifest with `ScaffoldManifestJson.ofManifest` and regenerating via `BLESS_FIXTURES=1` (data-model §4: `Provider = Some (providerId, {1;0})`, `Outcome = Scaffolded`, `Generated` ascending by `RelativePath` all `ProviderOwned`, `Collisions = []`).
- [X] T014 [US1] Add the golden + determinism assertions to `WorkedExampleTests.fs`: project the run with `ScaffoldManifestJson.ofManifest` and assert **byte-for-byte** equality to the golden, then run a **second** fresh-temp scaffold and assert the same golden (no absolute path/clock/env leakage) (FR-008, SC-003, SC-005; contract R3 steps 5–6; quickstart Scenario 4). Depends on T011, T013.

### Documentation for User Story 1

- [X] T015 [US1] Write `docs/tutorials/adopter-onboarding.md` (FR-005): empty dir → scaffold → govern → verify → ship. **Anchor only the scaffold/build/manifest steps** to a command/assertion the worked-example test runs (embedding/linking the T013 golden); present **govern/verify/ship as cross-references** to the existing Governance surfaces (prior features), stating plainly they are not exercised by this feature's e2e check (FR-005, SC-005). Include the lifecycle/runtime **ordering note**: the lifecycle layer is a sibling-owned `fsgg-sdd init` precondition that must precede the runtime scaffold (spec Edge Cases "Lifecycle layer skipped"). State the `<15 min`, no-boilerplate outcome (SC-001) and the `fsgg-sdd init` boundary disclaimer (FR-013; contract R7).

**Checkpoint**: US1 is independently runnable — the headline "empty governed directory →
buildable, governed product" is demonstrated, built, and golden-asserted end-to-end (MVP).

---

## Phase 4: User Story 2 — Provider author clones the reference (Priority: P2)

**Goal**: Prove a third party can clone the reference provider, change only what it emits,
and run through the **identical** seam with no tool change — including the explicit
version-mismatch refusal (FR-006/FR-011; SC-004). Also pins the no-provider parity and
collision refusals that keep the seam strictly opt-in and safe (FR-010/SC-007).

**Independent Test**: Following only `provider-author.md` + the reference provider, produce a
minimal custom provider, select it in the worked-example run, and confirm the seam resolves
and invokes it through the same path; the version-mismatch clone refuses cleanly.

### Tests & evidence for User Story 2

- [X] T016 [US2] Create `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/FailurePathTests.fs` with the **no-provider parity** test (`Provider = None` ⇒ `Done(NoProvider)`, zero effects, no manifest write — today's behavior, zero diff: FR-010, SC-007), the **collision** test (seed a target file at an emitted path ⇒ `Refused (Collision [..])`, nothing overwritten — quickstart edge table; contract R4), and the **empty-provider-output** edge test (an in-test provider whose `Emit` returns a `ProviderEmission` with zero files ⇒ terminal `Done(Scaffolded)` with an **empty** `Generated` set and `Collisions = []`, no error — spec Edge Cases "Empty provider output"; quickstart edge table). These pin the seam's opt-in/safety/empty-emission guarantees US1 relies on.
- [X] T017 [US2] Add the **contract-mismatch** test to `FailurePathTests.fs`: an in-test clone of `provider` with `ContractVersion = { Major = 2 }`, selected through the seam ⇒ `Refused (ContractMismatch …)`, **no** files written, actionable diagnostic (FR-011; contract R4; data-model §7). Extends the file from T016 (same file — not parallel-safe with T016).
- [X] T018 [P] [US2] Add a minimal cloned-provider walkthrough fixture/assertion proving the clone path: copy `providerId` + the emitted file set into an in-test custom provider value, run it through the **same** `Interpreter.run` call as US1, and assert only the emitted files differ — **no** edit to `Scaffold`/the tool (FR-006, SC-004; quickstart Scenario 2; contract R5).

### Documentation for User Story 2

- [X] T019 [US2] Write `docs/tutorials/provider-author.md` (FR-006): clone `samples/…SddReferenceProvider` as the starting point, adapt the emitted files to a new stack, register/select it, run the same seam — anchored to T018's clone assertion and T017's version-mismatch example. Restate the ownership boundary (runtime files provider-owned; tool owns only delegation/safety/recording/reporting — FR-012; contract R5) and the `fsgg-sdd init` boundary (FR-013).

**Checkpoint**: US1 AND US2 both pass independently — the seam treats reference, clone, and a
broken clone identically, with no provider-specific branch in the tool.

---

## Phase 5: User Story 3 — Integrator connects readiness to the Governance loop (Priority: P3)

**Goal**: An explanatory handoff tutorial mapping the scaffolded product's SDD readiness /
`governance-handoff.json` outputs to Governance routing/evidence/enforcement, consistent with
ADR 0002 (FR-007; SC-008). No consumer code ships here (plan Deferred / research D8).

**Independent Test**: A reader of `sdd-governance-handoff.md` can correctly state which
readiness fields Governance consumes and how each maps, matching ADR 0002 row-for-row.

- [X] T020 [US3] Write `docs/tutorials/sdd-governance-handoff.md` (FR-007): use the scaffolded governed product as the worked subject; reproduce the readiness→Governance mapping table verbatim from data-model.md §6 / ADR 0002 (`evidence.nodes[].state` straight-through, `deferred → skipped` `[-]`, `autoSynthetic` invalid-in-handoff, `stale` + `staleEvidence`, `governedReferences[*]` advisory enrichment, `readiness.*` advisory inputs, unknown major ⇒ version-mismatch). Cross-reference the sibling **`FS.GG.SDD` repo's** `017-governance-handoff` spec as an **external (cross-repo)** pointer — it is **not** under this repo's `specs/017-*` (which is `017-unknown-governed-path-findings`); the only locally build-verifiable anchor is ADR 0002.
- [X] T021 [P] [US3] Add an assertion that the documented mapping does not silently drift from the accepted contract: verify each row of the §6 table against ADR 0002 (a docs/golden check or an explicit reviewer checklist line per row), so SC-008's 100%-row agreement is enforceable (acceptance scenario 2).
- [X] T022 [P] [US3] State explicitly in the handoff tutorial that no `governance-handoff.json` consumer ships in this repo (it is ADR 0002's queued Governance-side work) and that production `fsgg-sdd init` wiring is sibling-owned (FR-013; plan Deferred / research D8).

**Checkpoint**: All three stories functional — the loop from scaffolded product to governed
product is documented and contract-checked.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Close out evidence discipline, deferral tracking, and end-to-end validation.

- [X] T023 [P] Run the full quickstart (Scenarios 1–6) via `dotnet test -c Release tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests` + the FSI prelude; confirm all pass and the "Done when" checklist in quickstart.md is satisfied.
- [X] T024 [P] Verify the genericity invariant once more end-to-end: `git diff` shows **zero** changes to `src/FS.GG.Governance.Scaffold/*`, `src/FS.GG.Governance.ScaffoldManifestJson/*`, and the two core `surface/*.surface.txt` baselines (SC-006; FR-002).
- [X] T025 Evidence-obligations close-out (Principle V): record real `dotnet build` evidence for the worked example; disclose the lifecycle-precondition stand-in at its use site (done in T010) **and** in the PR description; note that the emitted test project builds but is **not** executed (`dotnet test` deferred — research D2). Confirm Principle IV is N/A-by-reuse (no new MVU surface) in the obligations note.
- [X] T026 [P] Confirm the plan's Deferred / Out-of-Scope items remain explicitly tracked (host wiring, provider discovery, `governance-handoff.json` consumer, running the emitted tests, the lifecycle skeleton itself) — no silent omissions (Constitution: intentional deferral MUST be explicit).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup. **BLOCKS all user stories** — the `provider`
  value (T006/T007) is consumed by US1, US2, and US3.
- **User Stories (Phase 3–5)**: all depend on Foundational. US1 is the MVP; US2 and US3 can
  then proceed in parallel or in priority order.
- **Polish (Phase 6)**: depends on the desired stories being complete.

### Cross-task dependencies (beyond plain phase order)

- T008 (bless surface baseline) runs **after** T009 (the drift test exists to bless from).
- T014 (golden/determinism assert) depends on T011 (happy-path run) and T013 (the golden).
- T017 extends `FailurePathTests.fs` created by T016 — **same file**, not parallel with T016.
- T015/T019/T020 (tutorials) each anchor to their story's test assertions, so author them
  after those tests are green (FR-008 — anchored, not rotting).

### Within each user story

- Tests are authored to FAIL first, then the provider/golden makes them green (the provider
  itself is Foundational, so US1 tests turn green as T011–T014 land).
- Support/fixtures before the tests that use them; tests before the tutorials that anchor to
  them.

### Parallel opportunities

- T003, T004 in Setup.
- US2 and US3 phases run in parallel once Foundational completes (different files/docs).
- Within phases: T018/T021/T022 and the Phase 6 `[P]` tasks (T023/T024/T026) are
  independent. T016→T017 are serialized (same file).

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (provider value + genericity guard).
2. Phase 3 US1 → **STOP and VALIDATE**: empty dir builds + golden matches, independently.
3. Demo the headline "empty governed directory → buildable, governed product."

### Incremental Delivery

US1 (MVP) → US2 (clone + failure paths) → US3 (handoff docs) → Polish. Each story adds value
without changing the generic core or the prior stories' artifacts.

---

## Summary

- **Total**: 26 tasks across 6 phases.
- **Per user story**: US1 (P1, MVP) — 6 (T010–T015); US2 (P2) — 4 (T016–T019); US3 (P3) —
  3 (T020–T022). Setup 4 (T001–T004), Foundational 5 (T005–T009), Polish 4 (T023–T026).
- **Parallel opportunities**: T003/T004 (setup); US2∥US3 phases; T018/T021/T022 and
  T023/T024/T026 within their phases.
- **Suggested MVP scope**: User Story 1 — the layered worked example that builds an empty
  directory into a governed, `dotnet build`-green product with a byte-stable manifest golden,
  on the unchanged 071 seam (SC-001/SC-002/SC-003/SC-006).
</content>
</invoke>
