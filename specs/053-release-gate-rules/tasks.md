---
description: "Task list for Pure Release-Gate Readiness Rules Core (F053)"
---

# Tasks: Pure Release-Gate Readiness Rules Core

**Input**: Design documents from `/specs/053-release-gate-rules/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅ (D1–D7), data-model.md ✅,
contracts/Model.fsi ✅, contracts/Release.fsi ✅, quickstart.md ✅

**Tier**: **Tier 1 (contracted change)** — adds a new public library (`FS.GG.Governance.ReleaseRules`) with a
curated `.fsi` pair and a new surface baseline. It introduces a new pure library but **no** new third-party
dependency, **no** schema, **no** schema-version bump, and **no** edit to any frozen merged core (F014 `Config`,
F023 `Enforcement`, F024 `Ship`) or golden baseline. All tasks share the feature tier; no per-task `[T1]`/`[T2]`
annotations needed. Tests are **mandatory** (Principle V).

**Elmish/MVU**: **Not applicable** (Principle IV — plan Constitution Check). The core has no multi-step state,
I/O, retries, user interaction, or background work — it is the constitution's named "single rule evaluation /
fact store" pure-function case. Facts arrive as typed input; the verdict is a return value. There is no
interpreter edge, no `Effect`/`Msg`, no `Program`. The I/O-bearing rows (sensing, the `fsgg release` host
command, the `release.json` projection) are out of scope here and will honor Principle IV when they arrive.
Principle VI is satisfied **by construction**: an absent/unrecoverable fact is the explicit `Unrecoverable`
`FactState` ⇒ a `Violated` finding with a distinct reason (FR-005) — never a throw or a swallowed exception;
there is no I/O path to fail.

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel. Stories map to
spec user stories — US1 (P1, headline MVP) evaluate declared rules against provided facts ⇒ one finding per rule;
US2 (P2) roll findings up into a verdict + exit-code basis reusing F023/F024 verbatim; US3 (P3) no-hide
visibility and determinism. The whole feature is one small new pure library plus its test project — no host
wiring, no schema, no document.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]`…`[US3]` traceability; unlabeled = shared infrastructure
- Exact repo-root-relative file paths in every description

---

## Phase 1: Setup (the new pure library skeleton, no behavior)

**Purpose**: Create the new pure library + its focused test project so everything compiles and the solution
restores. No semantics yet. Nothing existing is edited beyond the solution file and the `CLAUDE.md` plan pointer.

- [X] T001 Create `src/FS.GG.Governance.ReleaseRules/FS.GG.Governance.ReleaseRules.fsproj` — SDK-style, `net10.0`,
  `RootNamespace`/`PackageId` `FS.GG.Governance.ReleaseRules`, `Version` `0.1.0`, `IsPackable=true` (matching the
  other optional packable cores — `Ship`/`Enforcement`/`Config`; research D5). `<Compile>` order **`Model.fsi`,
  `Model.fs`, `Release.fsi`, `Release.fs`**. `<ProjectReference>`s (and only these — plan Primary Dependencies,
  research D5): `../FS.GG.Governance.Config/...` (F014 — `Maturity`/`BlockOnRelease`, `SurfaceId`,
  `SurfaceClass`/`ReleaseSurface`, `EnvironmentClass`/`Release`), `../FS.GG.Governance.Enforcement/...` (F023 —
  `Severity`, `RunMode`/`Release`, `Profile`/`Release`, `EnforcementInput`, `EnforcementDecision`,
  `deriveEffectiveSeverity`), `../FS.GG.Governance.Ship/...` (F024 — `Verdict`/`ExitCodeBasis` result types).
  **No** reference to `Route`/`Gates`/`Findings` (a release rule is not an F018 gate or F017 finding — research
  D1/D5). **No** third-party `PackageReference` (FR-008). Header comment: the pure release-gate core — the closed
  release rule-kind vocabulary, the typed facts input, the per-rule evaluation, and the verdict rollup — layered
  on top of the merged thread (heavier capabilities layer on top, not into the core).
- [X] T002 [P] Create `tests/FS.GG.Governance.ReleaseRules.Tests/FS.GG.Governance.ReleaseRules.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s `Expecto`, `Expecto.FsCheck`, `FsCheck`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no new package).
  `<ProjectReference>`s to the new `FS.GG.Governance.ReleaseRules` **and**, for constructing the reused
  primitives in fixtures, `FS.GG.Governance.Config` (`Maturity`/`SurfaceId`), `FS.GG.Governance.Enforcement`
  (`Severity`/`RunMode`/`Profile`/`EnforcementDecision`), `FS.GG.Governance.Ship` (`Verdict`/`ExitCodeBasis`).
  `<Compile>` order is `Support.fs`, then the per-story test files added by the task that creates them, then
  `SurfaceDriftTests.fs`, then `Main.fs`; at this step wire **only** `Support.fs` (T010) and `Main.fs` (T011).
  Mirror `tests/FS.GG.Governance.Ship.Tests/...Tests.fsproj`.
- [X] T003 Add both new projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders) with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching existing entries.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` at `specs/053-release-gate-rules/plan.md` (verify;
  no other doc changes).

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; `dotnet sln list` shows both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts + compiling stubs + FSI proof + test scaffolding) — BLOCKS all stories

**Purpose**: Drop every new public surface (Principle I — contracts before any `.fs` body), define the pure data
vocabulary, make the whole solution compile with the two functions **stubbed**, prove the surface in FSI
(Principle I), and stand up the test scaffolding so tests can FAIL before implementation. **⚠️ No story work
begins until this phase is complete.**

- [X] T005 Author `src/FS.GG.Governance.ReleaseRules/Model.fsi` — drop `contracts/Model.fsi` **verbatim**:
  `namespace FS.GG.Governance.ReleaseRules`; the three `open`s (`FS.GG.Governance.Config.Model`,
  `FS.GG.Governance.Enforcement.Enforcement`, `FS.GG.Governance.Ship.Model`); the
  `[<CompilationRepresentation(...ModuleSuffix)>] module Model` with `ReleaseRuleKind` (the six closed kinds),
  `FactState` (`Met`/`Unmet`/`Unrecoverable`), `ReleaseRule`, `ReleaseFacts`, `RuleOutcome`
  (`Satisfied`/`Violated`), `ReleaseFinding`, `EnforcedReleaseFinding`, and `ReleaseDecision`, each carrying its
  curated doc-comment verbatim. Reuses F014/F023/F024 types verbatim; introduces **no new** F014/F023/F024 type.
  **No** access modifiers (Principle II).
- [X] T006 Add `src/FS.GG.Governance.ReleaseRules/Model.fs` — the `module Model` with all eight types **fully
  defined** (these are data, not behavior, so no stub). Same three `open`s as the `.fsi`. No access modifiers.
- [X] T007 Author `src/FS.GG.Governance.ReleaseRules/Release.fsi` — drop `contracts/Release.fsi` **verbatim**:
  `namespace FS.GG.Governance.ReleaseRules`; `open FS.GG.Governance.ReleaseRules.Model`; the
  `[<CompilationRepresentation(...ModuleSuffix)>] module Release` with the six members — `releaseRuleKindToken:
  ReleaseRuleKind -> string`, `releaseRuleKindOrdinal: ReleaseRuleKind -> int`, `factFor: ReleaseFacts ->
  ReleaseRuleKind -> FactState`, `evaluate: ReleaseRule list -> ReleaseFacts -> ReleaseFinding list`, `rollup:
  ReleaseFinding list -> ReleaseDecision`, `evaluateRelease: ReleaseRule list -> ReleaseFacts ->
  ReleaseDecision` — each with its curated doc-comment verbatim. No access modifiers (Principle II — the per-rule
  classifier, the `EnforcementInput` builder, and the three-way partition helper stay unexported by absence here).
- [X] T008 Add `src/FS.GG.Governance.ReleaseRules/Release.fs` — the `module Release` satisfying `Release.fsi`. The
  two pure-data lookups `releaseRuleKindToken`/`releaseRuleKindOrdinal` and `factFor` **may be fully defined now**
  (total `match` on the closed kind; `Map.tryFind` defaulting absent ⇒ `Unrecoverable`) since the story tests
  read them; `evaluate`/`rollup`/`evaluateRelease` are `failwith "not implemented"` stubs that type-check the full
  signatures (real bodies land in Phases 3–4). No access modifiers (Principle II). Confirm `dotnet build
  src/FS.GG.Governance.ReleaseRules/...` is clean under `TreatWarningsAsErrors`.
- [X] T009 Append an F053 design-first section to `scripts/prelude.fsx` (after the F052 section) — the
  Principle-I FSI proof **before** any operation body lands (the `quickstart.md` "Exercise the pure core in FSI"
  sketch verbatim): `#r` the new `ReleaseRules` Debug DLL plus the `Config`/`Enforcement`/`Ship` DLLs; build one
  blocking rule per kind; assert one-finding-per-rule + all-satisfied on `allMet`; the absent-fact ⇒ `Violated`
  fail-safe; the blocking violation ⇒ `Fail`/`Blocked` rollup; the maturity-relaxed violation ⇒ `Pass` + visible
  `Warning`; all-met ⇒ `Pass`/`Clean`/no-blockers; determinism (re-evaluate = equal); no-hide (output kind
  multiset = declared kind multiset); empty rule set ⇒ `Pass`/`Clean`. Its assertions over `evaluate`/`rollup`
  fail against the stubs — expected.
- [X] T010 [P] Write `tests/FS.GG.Governance.ReleaseRules.Tests/Support.fs` — real, literally-constructible
  builders (Principle V; **no mocks**): a `blocking`/`advisory` rule builder (`{ Kind; Surface = SurfaceId …;
  BaseSeverity; Maturity }`); a `ReleaseFacts` builder from a `(ReleaseRuleKind * FactState) list`; the
  `allKinds` list (the six closed kinds, for one-fixture-per-kind coverage); a `repoRoot` finder for the
  surface-baseline path; and FsCheck generators for arbitrary `ReleaseRule`s (random kind/surface/severity/
  maturity) and `ReleaseFacts` maps. No network, no governed repository, no process (SC-004).
- [X] T011 [P] Write `tests/FS.GG.Governance.ReleaseRules.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching `tests/FS.GG.Governance.Ship.Tests/Main.fs`.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the `ReleaseRules` test project compiles with only
`Support.fs` + `Main.fs` wired; `dotnet fsi scripts/prelude.fsx` loads the F053 section (its `evaluate`/`rollup`
assertions fail against the stubs — expected). The first failing semantic test lands in Phase 3.

---

## Phase 3: User Story 1 — Evaluate declared rules against provided facts (Priority: P1) 🎯 MVP

**Goal**: Implement `evaluate rules facts` — one finding per declared rule, classified `Satisfied`/`Violated`
from the per-kind `FactState`, with the declared `BaseSeverity` + `Maturity` carried through and a
self-explaining product-neutral reason, sorted by the stable `(kind ordinal, surface id)` key. This is the
irreducible core of the release gate — without a deterministic rule-to-finding evaluation there is nothing for
the verdict, projection, or host command to consume.

**Independent Test**: Construct a rule set covering each of the six kinds plus a matching `ReleaseFacts`,
`evaluate`, and assert one finding per rule with the correct satisfied/violated classification and a reason
naming the kind + surface. No I/O, no host command.

### Tests for User Story 1 (write first; must FAIL against the Phase-2 stub) ⚠️

- [X] T012 [P] [US1] Write `tests/FS.GG.Governance.ReleaseRules.Tests/EvaluateTests.fs` — `EvaluateTests`
  (US1 acc. 1–2, FR-001/FR-002/FR-003, data-model §evaluate): a rule of **each** of the six kinds with every
  governing fact `Met` ⇒ one `Satisfied` finding per rule, finding count = rule count, each reason naming the
  kind token + surface; a blocking rule whose fact is `Unmet` ⇒ a `Violated` finding carrying its declared
  `BaseSeverity`/`Maturity` and a reason naming the unmet expectation; the carried-through `BaseSeverity` +
  `Maturity` equal the rule's declared values (no re-derivation). **Pin the public lookups directly**
  (research D4/D7): `releaseRuleKindToken` returns the exact six wire tokens (`VersionBump → "versionBump"`,
  `PackageMetadata → "packageMetadata"`, `TemplatePins → "templatePins"`, `PublishPlan → "publishPlan"`,
  `TrustedPublishing → "trustedPublishing"`, `Provenance → "provenance"`) and `releaseRuleKindOrdinal` returns
  `0..5` in the closed declaration order (`VersionBump` 0 .. `Provenance` 5), all six distinct. Add
  `EvaluateTests.fs` to the test `.fsproj` `<Compile>` immediately after `Support.fs`. FAILs against the
  `failwith` stub.
- [X] T013 [P] [US1] Write `tests/FS.GG.Governance.ReleaseRules.Tests/FailSafeTests.fs` — `FailSafeTests`
  (US1 acc. 3, FR-005, SC-005, data-model §evaluate): a rule whose governing fact is `Unrecoverable` ⇒ a
  `Violated` finding with the distinct "no recoverable evidence" reason, never `Satisfied`; a rule whose kind is
  **absent** from `facts.States` resolves through `factFor` to `Unrecoverable` ⇒ `Violated` (the absent-key
  fail-safe); `Unmet` and `Unrecoverable` produce **distinct** reason text. Add `FailSafeTests.fs` to the test
  `.fsproj` `<Compile>` after `EvaluateTests.fs`. FAILs against the stub.

### Implementation for User Story 1

- [X] T014 [US1] Implement `evaluate` in `src/FS.GG.Governance.ReleaseRules/Release.fs` (data-model §evaluate):
  a `List.map` over `rules` — for each rule, `factFor facts rule.Kind` decides the `RuleOutcome` (`Met` ⇒
  `Satisfied`; `Unmet`/`Unrecoverable` ⇒ `Violated`) and the product-neutral `Reason` (kind token + governed
  `SurfaceId` + outcome basis "met"/"not met"/"no recoverable evidence" — research D7); build one
  `ReleaseFinding` carrying `Kind`/`Surface`/`Outcome`/the declared `BaseSeverity`+`Maturity`/`Reason`; then a
  stable `List.sortBy (fun f -> releaseRuleKindOrdinal f.Kind, <surface id string> f.Surface)` (research D4). The
  per-rule classifier and any reason helper live **unexported** (absent from `Release.fsi`). No `mutable`, no
  custom operators, no reflection (Principle III). After T014 the US1 tests go green.

**Checkpoint**: `evaluate` emits exactly one finding per declared rule, correctly classified, with the declared
severity carried and a deterministic reason — the answer to "which release expectations are met and which are
not." **This is the shippable MVP.** Rollup (US2) and the integrity properties (US3) follow.

---

## Phase 4: User Story 2 — Roll findings up into a release verdict and exit-code basis (Priority: P2)

**Goal**: Implement `rollup findings` (and the `evaluateRelease = evaluate >> rollup` entry) — derive each
finding's effective severity through F023 `deriveEffectiveSeverity` **verbatim** under `RunMode.Release`/
`Profile.Release`, partition into Blockers/Warnings/Passing by **re-applying the F024 partition rule unchanged**,
and compute `Verdict`/`ExitCodeBasis` reusing the F024 result types verbatim. This is the whole-release answer a
protected-boundary gate enforces; it layers on the P1 findings.

**Independent Test**: Feed a mixed finding set (a blocking violation, a maturity-relaxed advisory violation, a
satisfied rule) through `rollup` and assert the verdict, the exit-code basis, and the disjoint
Blockers/Warnings/Passing partition.

### Tests for User Story 2 (write first; must FAIL against the Phase-2 stub) ⚠️

- [X] T015 [P] [US2] Write `tests/FS.GG.Governance.ReleaseRules.Tests/RollupTests.fs` — `RollupTests` (US2
  acc. 1–3, FR-003/FR-004/FR-010, SC-002/SC-006, data-model §rollup partition table): a finding set with a
  blocking `Violated` rule (effective `Blocking`) ⇒ `Verdict = Fail`, `ExitCodeBasis = Blocked`, the violation
  in `Blockers`; an all-`Satisfied` set ⇒ `Verdict = Pass`, `ExitCodeBasis = Clean`, `Blockers` empty, every
  finding in `Passing`; a `Violated` rule whose declared `Maturity` relaxes it below blocking (base `Blocking` ⇒
  effective `Advisory`) ⇒ `Verdict = Pass` (no blockers) but the violation **visibly present** in `Warnings`,
  never dropped; a base-`Advisory` `Violated` rule lands in `Passing` (F024 never escalates) and stays visible.
  Assert effective severity equals `deriveEffectiveSeverity` called directly with the same `EnforcementInput`
  (verbatim reuse, not a re-derivation). The **paired** relax fixture (SC-006): relaxing only the maturity
  changes the bucket + verdict blocker count but never the finding's `Outcome` truth or its presence. Add
  `RollupTests.fs` to the test `.fsproj` `<Compile>` after `FailSafeTests.fs`. FAILs against the stub.

### Implementation for User Story 2

- [X] T016 [US2] Implement `rollup` and `evaluateRelease` in `src/FS.GG.Governance.ReleaseRules/Release.fs`
  (data-model §rollup, research D1/D2/D6): for each finding build `EnforcementInput { BaseSeverity =
  finding.BaseSeverity; Maturity = finding.Maturity; Mode = RunMode.Release; Profile = Profile.Release }` and
  call `deriveEffectiveSeverity` **verbatim**; pair into `EnforcedReleaseFinding`; place each by **re-applying
  the F024 partition rule** gated by outcome — `Satisfied` ⇒ `Passing`; `Violated` + effective `Blocking` ⇒
  `Blockers`; `Violated` + base `Blocking` relaxed to effective `Advisory` ⇒ `Warnings`; `Violated` + base
  `Advisory` ⇒ `Passing`; each list preserving the `evaluate` order. `Verdict = if Blockers ≠ [] then Fail else
  Pass`; `ExitCodeBasis = if Verdict = Fail then Blocked else Clean` — the F024 `Ship.Model` types reused
  verbatim (FR-004). `evaluateRelease rules facts = rollup (evaluate rules facts)`. The `EnforcementInput`
  builder and the three-way partition helper live **unexported**. Does **not** call `Ship.rollup` (release rules
  are not a `RouteResult` — research D1); edits **no** frozen core (FR-009). After T016 the US2 tests go green.

**Checkpoint**: `evaluate`/`rollup`/`evaluateRelease` produce the whole-release verdict + exit-code basis +
disjoint partition reusing F023/F024 verbatim — a blocking violation fails the release, an advisory violation
warns without blocking, and no satisfied or relaxed rule is hidden.

---

## Phase 5: User Story 3 — No-hide visibility and determinism (Priority: P3)

**Goal**: Pin the integrity properties the US1/US2 behavior already satisfies — every declared rule appears
exactly once (satisfied rules included), two evaluations over identical input are byte-identical, the output
rule-kind multiset equals the declared rule-kind multiset, and the partition drops nothing. This phase adds **no
new implementation** — only the assertions (incl. FsCheck properties) that pin the guarantees end-to-end.

**Independent Test**: Evaluate the same rule set + facts twice and assert byte-identical findings and verdict;
assert the output rule-kind multiset equals the declared rule-kind multiset (no drops, no fabrications).

### Tests for User Story 3 (write first; assert the property the implementation already satisfies) ⚠️

- [X] T017 [P] [US3] Write `tests/FS.GG.Governance.ReleaseRules.Tests/DeterminismTests.fs` — `DeterminismTests`
  (US3 acc. 1–2, FR-006/FR-007, SC-001/SC-003, data-model §evaluate): two `evaluate` calls over identical input
  are structurally equal, and re-ordering the input rules yields the **same** sorted output (order-independence);
  two `rollup` results over identical findings are equal; the output rule-kind multiset (`List.map .Kind |>
  List.sort`) equals the declared rule-kind multiset — satisfied rules present, no finding without a declared
  rule; duplicate same-kind rules each yield their own finding; facts for an undeclared kind invent nothing.
  Add `DeterminismTests.fs` to the test `.fsproj` `<Compile>` after `RollupTests.fs`.
- [X] T018 [P] [US3] Write `tests/FS.GG.Governance.ReleaseRules.Tests/EdgeCaseTests.fs` — `EdgeCaseTests`
  (spec Edge Cases): the **empty** rule set ⇒ zero findings and `evaluateRelease [] _ = { Verdict = Pass;
  Blockers = []; Warnings = []; Passing = []; ExitCodeBasis = Clean }`; an all-advisory-violation set ⇒ `Pass`
  with the violations in `Warnings`/`Passing` per the partition rule (none in `Blockers`). Add `EdgeCaseTests.fs`
  to the test `.fsproj` `<Compile>` after `DeterminismTests.fs`.
- [X] T019 [P] [US3] Write `tests/FS.GG.Governance.ReleaseRules.Tests/PropertyTests.fs` — `PropertyTests`
  (FsCheck, SC-001/FR-006): over random `ReleaseRule list` × `ReleaseFacts`, `|evaluate rules facts| =
  |rules|` (one-in-one-out, none dropped or fabricated) and, for `d = rollup (evaluate rules facts)`,
  `|d.Blockers| + |d.Warnings| + |d.Passing| = |evaluate rules facts|` (no-drop partition invariant); plus
  `d.Verdict = Fail ⟺ d.Blockers ≠ []` and `d.ExitCodeBasis = Blocked ⟺ d.Verdict = Fail`; plus the
  **composition law** `evaluateRelease rules facts = rollup (evaluate rules facts)` over random input (the
  single whole-gate entry equals its two halves — Release.fsi `evaluateRelease`). Use the Support.fs
  generators. Add `PropertyTests.fs` to the test `.fsproj` `<Compile>` after `EdgeCaseTests.fs`.

**Checkpoint**: US1–US3 — the core evaluates, rolls up, hides nothing, and is byte-identical across runs; the
one-in-one-out and no-drop invariants hold over random input. The pure release-gate core is complete.

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope hygiene, additivity, validation)

**Purpose**: Lock the new public surface and bless its baseline (Principle II / Change Classification), prove the
change is additive and edits no frozen core (FR-009), and run the quickstart end-to-end. Bless the baseline only
after the surface is final.

- [X] T020 [P] Write `tests/FS.GG.Governance.ReleaseRules.Tests/SurfaceDriftTests.fs` — a reflective
  `SurfaceDrift` test (the `Ship`/`Enforcement` precedent) comparing the public surface of the production
  `FS.GG.Governance.ReleaseRules` assembly byte-for-byte to `surface/FS.GG.Governance.ReleaseRules.surface.txt`
  with the `BLESS_SURFACE=1` re-bless path (reflection lives ONLY in this test); plus a **scope-hygiene**
  assertion that the production `ReleaseRules` assembly references **only** `Config`, `Enforcement`, `Ship`
  (+ their transitive deps + `FSharp.Core`/BCL) and **not** `Route`, `Gates`, `Findings`, or any third-party
  package (research D1/D5). Add `SurfaceDriftTests.fs` to the test `.fsproj` `<Compile>` immediately after
  `PropertyTests.fs` and before `Main.fs`.
- [X] T021 Generate and commit `surface/FS.GG.Governance.ReleaseRules.surface.txt` via `BLESS_SURFACE=1 dotnet
  test tests/FS.GG.Governance.ReleaseRules.Tests/...`; review the diff (exactly `Model` — the eight types — and
  `Release` — `releaseRuleKindToken`, `releaseRuleKindOrdinal`, `factFor`, `evaluate`, `rollup`,
  `evaluateRelease`; **no** leak of the per-rule classifier, `EnforcementInput` builder, or partition helper).
  After this T020 runs green without `BLESS_SURFACE`.
- [X] T022 [P] Verify FR-009 (no frozen-core edit, no schema bump, no new dependency) by inspection: `git diff`
  shows **no** edit to any merged pure core (`FS.GG.Governance.Config`, `Enforcement`, `Ship`, or any other),
  **no** schema/`schemaVersion` change, and **no** new third-party `PackageReference`; the only additions are the
  new `ReleaseRules` `src`/`tests` projects, the new surface baseline, the two `.sln` entries, the F053
  `scripts/prelude.fsx` section, and the `CLAUDE.md` plan pointer (plan §Scale/Scope).
- [X] T023 Run `quickstart.md` validation end-to-end: `dotnet build
  src/FS.GG.Governance.ReleaseRules/FS.GG.Governance.ReleaseRules.fsproj`; `dotnet fsi scripts/prelude.fsx` (the
  F053 section — every `[F53]` line prints `true`); `dotnet test FS.GG.Governance.sln` — all projects green under
  `TreatWarningsAsErrors`, including the `EvaluateTests`/`FailSafeTests`/`RollupTests`/`DeterminismTests`/
  `EdgeCaseTests`/`PropertyTests`/`SurfaceDriftTests` groups. Fix any drift.

**Checkpoint**: full solution builds clean, all tests green; SC-001…SC-006 covered; the new `ReleaseRules`
surface is blessed and scope-clean; every frozen core/golden/schema byte-unchanged. The pure release-gate core
turns declared release rules + provided facts into one deterministic finding per rule and a release verdict,
reusing the F023/F024 machinery verbatim — ready for the following sensing / `fsgg release` host / `release.json`
projection rows.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — drops both `.fsi`, defines the data model + the pure lookups,
  stubs `evaluate`/`rollup`, proves the surface in FSI, and stands up the test scaffolding. **BLOCKS all stories.**
- **US1 (Phase 3, P1, MVP)**: Depends on Phase 2 — implements `evaluate`. Independently shippable.
- **US2 (Phase 4, P2)**: Depends on US1 — `rollup` consumes the `evaluate` findings; `evaluateRelease` composes
  both.
- **US3 (Phase 5, P3)**: Depends on US1 + US2 — pins determinism / no-hide / partition invariants across both
  (no new impl).
- **Polish (Phase 6)**: Depends on all stories — the surface baseline, scope hygiene, additivity check, and
  quickstart validation are blessed last, once the surface is final.

### Within Each Story

- Tests are written FIRST and must FAIL before implementation (Principle V); US3/Phase-6 tests assert properties
  the implementation already satisfies.
- The `.fsi` contracts (Phase 2) precede every `.fs` body (Principle I).
- `evaluate` (US1) precedes `rollup`/`evaluateRelease` (US2); both precede the determinism/invariant assertions
  (US3) and the surface bless (Phase 6).

### Parallel Opportunities

- Setup tasks `T002`/`T004` run in parallel with `T001`/`T003`.
- Foundational: `T005`/`T006` (Model) precede `T007`/`T008` (Release); `T009`–`T011` (prelude + test scaffolding)
  run in parallel with each other once the `.fsi`/stub compiles.
- Within each story, the test tasks marked `[P]` (different files) run in parallel and before the implementation
  task.
- Phase-6 `T020`/`T022` are parallel-safe (different file / read-only inspection); `T021` (bless) is sequential
  after `T020` and the surface are final.

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → Phase 2 Foundational (`.fsi` contracts + data model + pure lookups + stubs + FSI proof + test
   scaffolding — solution compiles, existing suites untouched).
2. Phase 3 US1 — `evaluate` emits one correctly-classified, deterministically-ordered finding per declared rule.
3. **STOP and VALIDATE**: one finding per rule, correct satisfied/violated, fail-safe on absent facts.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. Add US1 (`evaluate`) → validate → **MVP**.
3. Add US2 (`rollup`/`evaluateRelease`, F023/F024 reuse) → validate → the whole-release verdict.
4. Add US3 (determinism / no-hide / property invariants) → validate.
5. Phase 6 polish → surface blessed, additivity proven, quickstart green.

### Suggested MVP Scope

**User Story 1** (Phase 3) over the Phase 1–2 foundation — `evaluate` turning declared release rules + provided
facts into exactly one classified finding per rule, with the fail-safe on absent facts and a deterministic
order. It is independently testable, the irreducible core every later row consumes, and a viable standalone
increment.

---

## Notes

- `[P]` tasks = different files, no dependency on another incomplete task in this phase.
- `[Story]` labels map tasks to spec user stories (US1–US3); unlabeled tasks are shared infrastructure.
- Verify tests FAIL before implementing; never mark a failing task `[X]`; never weaken an assertion to green a
  build — narrow scope and document.
- Principle IV (Elmish/MVU) is **not applicable** — the core is a pure total function (no state, I/O, retries,
  or user interaction); facts are typed input and the verdict is a return value.
- The fixtures construct **real** F014/F023/F024 primitives and assert effective severity equals
  `deriveEffectiveSeverity` called directly — no mocks, no `Synthetic` literals; the core consumes only its own
  typed declared input, which is the real and only evidence it has (Principle V, SC-004).
- The core reuses F023 `deriveEffectiveSeverity` and the F024 `Verdict`/`ExitCodeBasis` types verbatim and
  **re-applies** the F024 partition rule — it does **not** call `Ship.rollup` (release rules are not a
  `RouteResult` — research D1) and edits no frozen core (FR-009).
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
