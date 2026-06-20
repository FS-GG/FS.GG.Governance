---
description: "Task list for F017 - 017-unknown-governed-path-findings: turn F015 routing's deferred `UnmatchedInRoot` outcome into an explicit, typed unknown-governed-path finding — a single pure, total classifier over F014 declared surfaces and the F015 RouteReport, with no global default-deny, routine suppression, protected-boundary escalation (`Protected > Routine > Ordinary`), cross-plane dedup, and byte-identical deterministic output."
---

# Tasks: Unknown Governed Path Findings

**Feature branch**: `017-unknown-governed-path-findings` (active spec; git branch currently `main`)
**Spec**: [`specs/017-unknown-governed-path-findings/spec.md`](./spec.md)
**Plan**: [`specs/017-unknown-governed-path-findings/plan.md`](./plan.md)

**Input**: Design documents from `/specs/017-unknown-governed-path-findings/`

## Progress (2026-06-20 — implemented, all tests green)

| Phase | Tasks | Status |
|---|---|---|
| 1 · Setup | T001–T009 | ✅ done |
| 2 · Foundation | T010–T013 | ✅ done |
| 3 · US1 (P1 MVP) | T014–T017 | ✅ done |
| 4 · US2 (P1 MVP) | T018–T019 | ✅ done |
| 5 · US3 (P2) | T020–T023 | ✅ done |
| 6 · US4 (P2) | T024–T026 | ✅ done |
| 7 · US5 (P3) | T027–T029 | ✅ done |
| 8 · Polish | T030–T033 | ✅ done |

**33/33 tasks ✅** · `dotnet test FS.GG.Governance.sln` green (Findings: 23/23) · surface baseline
committed + drift-checked · prelude F017 sketch + readiness transcript captured.

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Model.fsi](./contracts/Model.fsi), [contracts/Findings.fsi](./contracts/Findings.fsi), [contracts/precedence.md](./contracts/precedence.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; new public `.fsi`s; new surface baseline). Credible evidence is **public-surface** testing only: `Findings.findUnknownGovernedPaths` exercised over **real in-memory `TypedFacts` + real `RouteReport`s** — the genuine values a downstream caller passes, never private helpers and never mocks (Principle V, research D7). No network, git, agent, or filesystem is reachable from this feature, so no synthetic evidence is anticipated; any literal standing in for an un-derivable case carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — Config and Routing are referenced as-is; the only new baseline is `surface/FS.GG.Governance.Findings.surface.txt`.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** — this feature is a pure, total classification of already-typed inputs (FR-011): no I/O, no git sensing, no clock, no multi-step state, no retries. It is exactly the "single rule evaluation / pure function" case Principle IV explicitly exempts from MVU ceremony (plan Constitution Check; the same call F015 `route` made). The boundary is one pure function `findUnknownGovernedPaths : TypedFacts -> RouteReport -> FindingReport` — no `Model`/`Msg`/`Effect`/`update`/interpreter. The pure/edge separation the principle protects is satisfied trivially: everything is pure.

**Decision contract**: [`contracts/precedence.md`](./contracts/precedence.md) is the single normative source for membership (segment-prefix), the `Protected > Routine > Ordinary` ladder, the ordinal-first `SurfaceId` tiebreak, dedup, ordering, and the message contract. Tasks implement and test *against* it; they do not re-decide it.

**Determinism minimums (FR-009, SC-004)**: `FindingReport.Findings` is sorted by `String.CompareOrdinal Path` then `String.CompareOrdinal (findingIdToken Id)`; the protected-zone `SurfaceId` is the ordinal-first matching surface; candidate routings are deduped by grouping on normalized path and keeping one routing per path (the kept value is unambiguous — `Routing.route` is a pure function of the path, so every duplicate in a path-group carries an identical `RoutingResult`; precedence.md §"Deduplication"). Re-ordering the candidate paths OR the authored surfaces leaves the output byte-identical.

**No-default-deny minimums (FR-003/FR-004/FR-005, SC-002)**: `OutOfScope` and `Routed` paths are silent; an `UnmatchedInRoot` path inside a declared `Routine` surface is silent (unless also protected — then escalated, never silenced). Only non-routine `UnmatchedInRoot` paths become findings.

**Scope-guard minimums (FR-013, SC-006)**: no severity, enforcement, profile/mode/maturity, gate registry, `GateId`, evidence freshness, ship verdict, route/audit JSON, or CLI command. Findings carry only normalized `GovernedPath`s, declared `SurfaceId`s, a `FindingZone`, and a fix-hint `Message` — no raw YAML, host paths, timestamps, or product vocabulary beyond declared ids.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US5]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional classifier library `FS.GG.Governance.Findings`, its test project, the public contracts (copied verbatim), the in-memory fixtures, the prelude sketch, and the readiness note. **No new third-party dependency** — the library references `FS.GG.Governance.Config` and `FS.GG.Governance.Routing` only; its own code is BCL + FSharp.Core (the transitive YamlDotNet edge arrives via Config and is unused here).

- [X] T001 Create `src/FS.GG.Governance.Findings/FS.GG.Governance.Findings.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.Findings`, with exactly two `<ProjectReference>`s — `../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj` and `../FS.GG.Governance.Routing/FS.GG.Governance.Routing.fsproj` — and **no** `<PackageReference>` (research D1). Compile order `Model.fs` → `Findings.fs`.
- [X] T002 Copy `specs/017-unknown-governed-path-findings/contracts/Model.fsi` → `src/FS.GG.Governance.Findings/Model.fsi` and `contracts/Findings.fsi` → `src/FS.GG.Governance.Findings/Findings.fsi` verbatim as the curated public surface (Principle II — these `.fsi`s are the SOLE public surface; the `.fs` files carry no top-level access modifiers).
- [X] T003 Add `failwith "F017"` stub bodies in `src/FS.GG.Governance.Findings/Model.fs` and `src/FS.GG.Governance.Findings/Findings.fs` that satisfy the `.fsi` contracts, in the fsproj compile order `Model.fs` → `Findings.fs`, so the library compiles against the contracts before any real logic lands (Principle I).
- [X] T004 Create `tests/FS.GG.Governance.Findings.Tests/FS.GG.Governance.Findings.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/VSTest packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.Findings`, `src/FS.GG.Governance.Routing` (the tests call `Routing.route` to build real `RouteReport`s), and `src/FS.GG.Governance.Config` (to build real `TypedFacts`).
- [X] T005 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.Findings.Tests/`: `Support.fs`, `FindingDecisionTests.fs`, `PrecedenceTests.fs`, `DeterminismTests.fs`, `PlaneUniformityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T006 Add `src/FS.GG.Governance.Findings` and `tests/FS.GG.Governance.Findings.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Implement the fixture builders in `tests/FS.GG.Governance.Findings.Tests/Support.fs` over **real** Config/Routing values (no mocks): (a) a `facts` builder assembling a real in-memory `TypedFacts` with a declared governed root and a `Capabilities.Surfaces` list parameterized by `(SurfaceClass, SurfaceId, paths)` triples (so a test can declare `Routine`/`ProtectedSurface`/inert surfaces), filling each `Surface`'s other required fields — `Owner` and `Maturity` (`Config.Model.Surface = { Id; Class; Paths; Owner; Maturity }`) — with fixed inert defaults that do not affect the F017 decision; (b) a `routeOf : TypedFacts -> string list -> RouteReport` helper that normalizes raw path strings (via `Config.Model.normalizePath`) and calls `FS.GG.Governance.Routing.Routing.route facts paths`, yielding genuine `Routed`/`UnmatchedInRoot`/`OutOfScope` outcomes; and (c) a tiny `routingsWith : PathRouting list -> RouteReport` for the dedup/plane tests that need to hand-build a `Routings` list with a repeated path. These are REAL downstream inputs, not fakes (research D7).
- [X] T008 [P] Extend `scripts/prelude.fsx` with an F017 design sketch that `#r`s the built `FS.GG.Governance.Findings` (+ `Config` + `Routing`) assemblies, opens the namespaces, builds a small in-memory `TypedFacts` (governed root + a path map that does not cover `src/Kernel/New.fs` + a `ProtectedSurface` over `src/Kernel`), calls `Routing.route` over `[src/Kernel/New.fs; src/Kernel/Eval.fs; scratch.txt]`, then `Findings.findUnknownGovernedPaths facts report`, recording the intended route→classify flow before real bodies land (Principle I; mirrors [quickstart.md](./quickstart.md) §FSI).
- [X] T009 [P] Create `specs/017-unknown-governed-path-findings/readiness/README.md` listing the required FSI transcripts (route→classify producing one ordinary finding; the same with a `ProtectedSurface` added producing the escalated flavor; an out-of-scope + routine set producing zero findings; a twice-identical + reordered determinism run; a cross-plane dedup run) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.Findings` and `dotnet test tests/FS.GG.Governance.Findings.Tests` compile against stubs; the solution lists the two new projects; the Config + Routing references resolve; the fixtures build real `TypedFacts`/`RouteReport`s.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the finding-domain model, the private segment-prefix membership helper, the dedup + deterministic ordering machinery, and the `findUnknownGovernedPaths` routing-outcome dispatch skeleton — everything the stories specialize. **No user-story work begins until this phase is complete.**

- [X] T010 Implement `src/FS.GG.Governance.Findings/Model.fs` exactly matching `Model.fsi`: the closed `FindingId` (`UnknownGovernedPath` | `UnknownProtectedBoundaryPath`), the closed `FindingZone` (`GovernedRootUnknown` | `ProtectedBoundaryUnknown of surface: SurfaceId`), the `UnknownGovernedPathFinding` record (`Id`/`Path`/`Zone`/`Message`), the `FindingReport` record (`Findings`), and the total `findingIdToken` (`UnknownGovernedPath` → `"unknownGovernedPath"`, `UnknownProtectedBoundaryPath` → `"unknownProtectedBoundaryPath"`). Reuses `Config.Model.GovernedPath`/`SurfaceId` (does not redefine them).
- [X] T011 Implement the private **segment-prefix membership** helper in `src/FS.GG.Governance.Findings/Findings.fs` per [contracts/precedence.md](./contracts/precedence.md) §"Surface membership": a candidate path *p* is within a surface *s* iff *p* equals or is a segment-prefixed descendant of any path in `s.Paths` (split on `/`, drop `""`/`.` segments, test prefix); decided on the normalized `GovernedPath` form only; an empty `Paths` list matches nothing (FR-014). This is the same relation F015's `inRoot` used, reproduced locally rather than exposed from Routing (research D3). It does NOT re-derive the governed root — it trusts the routing outcome.
- [X] T012 [P] Implement the deterministic **dedup + ordering** helpers in `src/FS.GG.Governance.Findings/Findings.fs` per [contracts/precedence.md](./contracts/precedence.md) §"Deduplication"/§"Ordering": group `report.Routings` by normalized path and keep one routing per path (dedup, FR-010) — the kept value is unambiguous because `Routing.route` is a pure function of the path, so every duplicate in a path-group carries an identical `RoutingResult` (precedence.md §"Deduplication"); and a final `List.sortWith` over findings by `String.CompareOrdinal Path` then `String.CompareOrdinal (findingIdToken Id)` (FR-009). Disclose any `mutable` fold accumulator at its use site (Principle III).
- [X] T013 Implement the `findUnknownGovernedPaths` dispatch skeleton in `src/FS.GG.Governance.Findings/Findings.fs`: read only `facts.Capabilities.Surfaces` and `report.Routings` (ignore `report.Diagnostics`); dedup (T012); for each unique candidate match on `RoutingResult` — `Routed _` → no finding (FR-005), `OutOfScope` → no finding (FR-003), `UnmatchedInRoot` → call a `classifyUnmatched` placeholder (filled by the stories); collect, sort (T012), return `{ Findings = ... }`. PURE and TOTAL — never throws; an empty result is a valid success (FR-011/FR-012). At this stage `classifyUnmatched` may emit only the ordinary flavor or `None`; the stories complete the ladder.

**Checkpoint**: the library builds with the real Model + membership helper + dedup/ordering + classifier skeleton; `findUnknownGovernedPaths` over an all-`Routed`/`OutOfScope` report returns an empty-but-successful `FindingReport`; the surface compiles against the `.fsi`s.

---

## Phase 3: User Story 1 - Flag an unknown path inside the governed root (Priority: P1) 🎯 MVP

**Goal**: a non-routine `UnmatchedInRoot` candidate path becomes exactly one ordinary `UnknownGovernedPath` finding, located on that path, with a fix hint; sibling `Routed` paths produce none.

**Independent Test**: facts declaring a governed root + a path map that does not cover `src/Kernel/New.fs`; route a set in which `src/Kernel/New.fs` is `UnmatchedInRoot` and a sibling is `Routed`; assert exactly one `UnknownGovernedPath` finding on `src/Kernel/New.fs` with a fix-hint message, and none for the routed sibling.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T014 [P] [US1] In `tests/FS.GG.Governance.Findings.Tests/FindingDecisionTests.fs`, add finding-vs-no-finding tests over a real `RouteReport` (built via `Support.routeOf`): a non-routine `UnmatchedInRoot` path → exactly one finding with `Id = UnknownGovernedPath`, `Zone = GovernedRootUnknown`, `Path` = that normalized path, and a non-empty `Message`; a sibling `Routed` path in the same set → no finding (US1 AS1, **SC-001**).
- [X] T015 [P] [US1] In `FindingDecisionTests.fs`, add a mixed-outcome test: a set with `Routed`, `UnmatchedInRoot` (non-routine), and `OutOfScope` paths → a finding for each non-routine `UnmatchedInRoot` path **and for no other** (US1 AS2); and an all-`Routed`/`OutOfScope` set → empty `Findings`, a successful result, not an error (US1 AS3, FR-012).

### Implementation for User Story 1

- [X] T016 [US1] Implement the ordinary rung of `classifyUnmatched` in `src/FS.GG.Governance.Findings/Findings.fs`: an `UnmatchedInRoot` path matched by no escalating/suppressing surface → one finding `{ Id = UnknownGovernedPath; Zone = GovernedRootUnknown; Path; Message }` (precedence ladder rung 3). (Routine suppression and protected escalation are layered in US2/US3.)
- [X] T017 [US1] Implement the ordinary-finding `Message` in `src/FS.GG.Governance.Findings/Findings.fs` per [contracts/precedence.md](./contracts/precedence.md) §"Messages": name the offending normalized path and offer ≥1 concrete remediation ("declare a path-map glob", "mark the region routine", or "classify the surface"); no raw YAML, host path, timestamp, or non-id product vocabulary (FR-008, SC-006).

**Checkpoint**: ordinary in-root unknowns are flagged with a fix hint; routed paths are silent. US1 is the MVP.

---

## Phase 4: User Story 2 - Never default-deny routine or out-of-scope files (Priority: P1)

**Goal**: `OutOfScope` paths and `UnmatchedInRoot` paths within a declared `Routine` surface stay silent; non-routine in-root unknowns in the same set still produce findings. No global default-deny.

**Independent Test**: a set with an `OutOfScope` path, an `UnmatchedInRoot` path inside a declared `Routine` surface, and a third `UnmatchedInRoot` path outside any routine surface → only the third produces a finding.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T018 [P] [US2] In `FindingDecisionTests.fs`, add suppression tests over a real `RouteReport`: an `OutOfScope` path → no finding regardless of count (US2 AS1, FR-003); an `UnmatchedInRoot` path within a declared `Routine` surface → no finding (US2 AS2, FR-004); and the two together with a third non-routine in-root unknown → **zero** findings for the first two and exactly one for the third (US2 AS3, **SC-002** — no global default-deny).

### Implementation for User Story 2

- [X] T019 [US2] Add the routine-suppression rung (ladder rung 2) to `classifyUnmatched` in `src/FS.GG.Governance.Findings/Findings.fs`: an `UnmatchedInRoot` path within ≥1 `Routine` surface (segment-prefix membership, T011) and within no `ProtectedSurface` → **no finding** (FR-004). Inert classes (`GovernedRoot`/`GeneratedView`/`ReleaseSurface`) neither suppress nor escalate — a path covered only by them falls through to the ordinary rung (precedence.md §"Surface membership").

**Checkpoint**: out-of-scope and routine paths are silent; non-routine in-root unknowns still flag. US1 + US2 together are the MVP pairing (no default-deny).

---

## Phase 5: User Story 3 - Escalate unknown paths on a protected boundary (Priority: P2)

**Goal**: an `UnmatchedInRoot` path on a declared `ProtectedSurface` escalates to a distinct `UnknownProtectedBoundaryPath` / `ProtectedBoundaryUnknown sid` finding carrying the surface identity; `Protected > Routine` resolves overlaps to a single escalated finding.

**Independent Test**: an `UnmatchedInRoot` path within a declared `ProtectedSurface` → a finding distinguishable (by id and zone) from an ordinary governed-root unknown and carrying the protected `SurfaceId`; a path within both a `Routine` and a `ProtectedSurface` → a single escalated finding.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T020 [P] [US3] In `tests/FS.GG.Governance.Findings.Tests/PrecedenceTests.fs`, add escalation + distinguishability tests: an `UnmatchedInRoot` path within a declared `ProtectedSurface` → `Id = UnknownProtectedBoundaryPath`, `Zone = ProtectedBoundaryUnknown sid` carrying the surface's `SurfaceId` (US3 AS1, **SC-003**); an `UnmatchedInRoot` path in the governed root but in no declared surface → the ordinary `UnknownGovernedPath` / `GovernedRootUnknown` flavor, distinct from the protected one (US3 AS2).
- [X] T021 [P] [US3] In `PrecedenceTests.fs`, add the `Protected > Routine` overlap test (the worked example from [contracts/precedence.md](./contracts/precedence.md)): one path within BOTH a `Routine{Id="legacy"}` and a `ProtectedSurface{Id="kernel-core"}` → a **single** finding, escalated (`UnknownProtectedBoundaryPath`), never silenced; and a multi-protected-surface case → the zone's `sid` is the **ordinal-first** matching `SurfaceId`; assert the result is unchanged when the two surfaces are authored in the reverse order (FR-007, FR-009).

### Implementation for User Story 3

- [X] T022 [US3] Add the protected-escalation rung (ladder rung 1) to `classifyUnmatched` in `src/FS.GG.Governance.Findings/Findings.fs`: an `UnmatchedInRoot` path within ≥1 `ProtectedSurface` → one finding `{ Id = UnknownProtectedBoundaryPath; Zone = ProtectedBoundaryUnknown sid; ... }` where `sid` is the ordinal-first matching `SurfaceId` (`String.CompareOrdinal` on the underlying string); **protected outranks routine** — this rung is checked before the routine rung (T019), so an overlapping routine+protected path is escalated, never silenced (FR-006/FR-007). The ladder is a total order over the three outcomes, independent of authoring order.
- [X] T023 [US3] Extend the `Message` builder (T017) in `src/FS.GG.Governance.Findings/Findings.fs` for the protected flavor: name the escalating `SurfaceId`, and when the path is *also* within a `Routine` surface, name that routine `SurfaceId` too and state that protected precedence applied — so the contradictory declaration is actionable (precedence.md §"Messages", FR-008). Still no raw YAML, host paths, or timestamps.

**Checkpoint**: protected-boundary unknowns are escalated and self-identifying; overlaps resolve to a single escalated finding by `Protected > Routine`. US1–US3 pass.

---

## Phase 6: User Story 4 - Deterministic, explainable findings (Priority: P2)

**Goal**: prove the finding set is byte-identical for identical inputs and unchanged under input re-ordering, and that every message names the path + ≥1 remediation with no leaked vocabulary.

**Independent Test**: compute findings twice over the same inputs → byte-identical lists including order; reorder the candidate paths and the authored surfaces → unchanged list; every message names the path + a concrete remediation.

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T024 [P] [US4] In `tests/FS.GG.Governance.Findings.Tests/DeterminismTests.fs`, add: compute `findUnknownGovernedPaths` twice over identical inputs → structural equality of the whole `FindingReport`, including `Findings` order (US4 AS1, **SC-004**); and an FsCheck property that permuting the order of the candidate paths AND the authored `Surfaces` (for fixed content) yields an identical `FindingReport` (US4 AS2, FR-009); and a **totality** FsCheck property asserting that over FsCheck-generated `RouteReport`s and `Surface` lists, `findUnknownGovernedPaths` returns a valid `FindingReport` and **never throws** (the no-throw clause of **SC-005** / FR-011/FR-012 totality — the function has no failure mode of its own).
- [X] T025 [P] [US4] In `DeterminismTests.fs`, add the message/vocabulary assertions over every produced finding: the `Message` contains the offending path and ≥1 concrete remediation token, and contains **no** raw YAML, host-path separators, timestamps, or product vocabulary beyond declared domain/surface ids (US4 AS3, **SC-006**); reassert the protected-finding message also names the escalating `SurfaceId` (cross-checks T023).

### Implementation for User Story 4

- [X] T026 [US4] Confirm/refine the ordering (T012) and message (T017/T023) implementations in `src/FS.GG.Governance.Findings/Findings.fs` against the T024/T025 evidence; if the sort key, the permutation-invariance, or any message leaks vocabulary, fix it here. Note explicitly if no change was needed beyond Foundation/US1–US3. **Confirmed: no change needed** — `sortFindings` (T012) and the `ordinaryMessage`/`protectedMessage` builders (T017/T023) already satisfy the T024 permutation/totality properties and the T025 vocabulary assertions; all green.

**Checkpoint**: the finding set is provably deterministic and every finding is self-explanatory. US1–US4 pass.

---

## Phase 7: User Story 5 - Classify every change plane uniformly (Priority: P3)

**Goal**: an unclassified in-root path yields the same finding whichever F016 plane it came from; a path appearing in more than one plane collapses to a single finding by the documented dedup; the union across planes drops nothing.

**Independent Test**: present the same unclassified in-root path once "as committed-changed" and once "as untracked" → identical finding decision; a path appearing in multiple planes (a `Routings` list with a repeated path) → exactly one finding.

### Tests for User Story 5 (write first; must FAIL before implementation)

- [X] T027 [P] [US5] In `tests/FS.GG.Governance.Findings.Tests/PlaneUniformityTests.fs`, add per-plane parity tests: the same unclassified in-root path routed and classified "from" each of the three F016 planes (modeled as separate single-path `RouteReport`s, since the decision is path+surface keyed, not plane keyed) → identical `Id`/`Zone`/`Message` shape (US5 AS1, **SC-007**).
- [X] T028 [P] [US5] In `PlaneUniformityTests.fs`, add the dedup test using `Support.routingsWith`: a `Routings` list containing the same normalized path more than once **with the same `RoutingResult`** (the realistic case — the caller concatenated several routed planes, and routing is a pure function of the path) → exactly **one** finding for that path (US5 AS2, FR-010); and a multi-path multi-plane union → the deterministic union with no path silently dropped (US5 AS3).

### Implementation for User Story 5

- [X] T029 [US5] Confirm the dedup rung (T012) in `src/FS.GG.Governance.Findings/Findings.fs` collapses a repeated path to a single finding by grouping on normalized path and keeping one routing per path (identical by construction — routing is a pure function of the path), and that the plane is neither read into the decision nor retained on the finding in this MVP (FR-010 "MAY be retained"). Note explicitly if no change was needed beyond Foundation (T012 already implements the rung). **Confirmed: no change needed** — `dedupRoutings` (T012) already collapses a repeated path; the plane is neither read nor retained; the US5 `PlaneUniformityTests` (parity + dedup + union) are green.

**Checkpoint**: per-plane parity and cross-plane dedup hold by construction; the union drops nothing. US1–US5 pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: the surface baseline + drift test, dependency/scope/no-leak hygiene, the quickstart run, and the README/plan legend updates.

- [X] T030 Generate `surface/FS.GG.Governance.Findings.surface.txt` from the built `FS.GG.Governance.Findings` assembly using the repo's surface-baseline convention, then add `tests/FS.GG.Governance.Findings.Tests/SurfaceDriftTests.fs` asserting the built surface matches the baseline (Principle II). Confirm the baseline contains exactly the two modules `Model` and `Findings` and nothing private.
- [X] T031 [P] In `SurfaceDriftTests.fs` (or a dedicated module), add a dependency/scope-hygiene test asserting `FS.GG.Governance.Findings` references only `FS.GG.Governance.Config` and `FS.GG.Governance.Routing` (+ FSharp.Core, + transitive YamlDotNet) and **not** the kernel/host/adapters/Snapshot/CLI (research D1, one-way `Findings → Routing → Config`); that no finding field carries raw YAML, host paths, timestamps, or non-id product vocabulary (FR-008/SC-006); and that no severity/enforcement/gate-registry/`GateId`/evidence-freshness/ship-verdict/route-audit-JSON/CLI symbol is reachable — the FR-013 scope guard confirming no later-phase capability leaked in. Assert the feature reads no `.fsgg`/YAML and requires nothing installed in any inspected repo (FR-015): it references only `Config.Model` types + `Routing.Model`, not the Config `Loader`/`Schema` parsing surface.
- [X] T032 [P] Run [quickstart.md](./quickstart.md) end-to-end and record the transcripts named in `readiness/README.md` (route→classify ordinary; +protected escalation; out-of-scope + routine ⇒ zero; twice-identical + reordered determinism; cross-plane dedup), plus the SC-traceability note mapping SC-001…SC-007 to the proving tests.
- [X] T033 [P] Update `README.md` to list the new optional `FS.GG.Governance.Findings` library and link the decision contract ([contracts/precedence.md](./contracts/precedence.md)); flip the `docs/initial-implementation-plan.md` Phase-2 legend row for "unknown governed path findings" (this feature) to ✅, recording that it closes the two F015-deferred exit criteria (*"Routine unclassified files do not trigger global default-deny"* and *"Unknown paths under declared governed roots produce explicit findings"*).

**Checkpoint**: full `dotnet test FS.GG.Governance.sln` green; the new surface baseline committed and drift-checked; determinism, no-default-deny, and scope-guard hygiene proven; quickstart validated.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundation (Phase 2)**: depends on Setup — BLOCKS all user stories. The membership helper (T011), dedup/ordering (T012), and the classifier skeleton (T013) are the shared spine every story specializes.
- **User Stories (Phases 3–7)**: all depend on Foundation. US1 and US2 are **co-equal P1** and are the MVP *pairing* (a finding rule without the suppression rule is global default-deny). US3 (P2) layers the protected-escalation rung *before* the routine rung. US4 (P2) is mostly proofs over the existing sort/message. US5 (P3) is mostly a proof over the existing dedup.
- **Polish (Phase 8)**: depends on all user stories.

### Within Each User Story

- Tests are written first and must FAIL before implementation.
- `.fsi` contract (Phase 1) → FSI/prelude sketch (T008) → semantic tests → implementation → surface baseline (Principle I).
- The single classifier function `findUnknownGovernedPaths`/`classifyUnmatched` lives in `Findings.fs` and is extended **rung by rung** across US1 (ordinary), US2 (routine suppression), US3 (protected escalation) — these impl tasks (T016/T017, T019, T022/T023) edit the same file and are therefore **sequential within the file**, not `[P]`.

### Parallel Opportunities

- Setup `[P]` tasks T005, T007, T008, T009 run in parallel after the files they touch exist; T001→T002→T003 and T004→T005 are sequential (same projects/compile lists).
- Foundation: T012 is `[P]` (ordering/dedup helpers); T010/T011/T013 are sequential (T013 depends on T010+T011+T012).
- All story **test** tasks are `[P]` within their story: US1 (T014–T015), US2 (T018), US3 (T020–T021), US4 (T024–T025), US5 (T027–T028). The shared `FindingDecisionTests.fs` (US1+US2) means T014/T015/T018 touch one file — order them or merge.
- Polish T031, T032, T033 are `[P]`; T030 precedes T031 if they share `SurfaceDriftTests.fs`.

---

## Suggested MVP Scope

**User Stories 1 + 2 together** (the co-equal P1 pair) are the MVP: flag a non-routine `UnmatchedInRoot` path as an explicit finding **and** stay silent for out-of-scope and declared-routine paths — the two halves of the row title ("unknown governed path findings only inside governed roots or protected boundaries"), neither correct alone. **User Story 3** (P2) adds protected-boundary escalation with `Protected > Routine` precedence — the "or protected boundaries" half. **User Story 4** (P2) proves determinism + explainability for the downstream route/audit consumers; **User Story 5** (P3) proves per-plane uniformity + cross-plane dedup (which hold by construction, since the decision is path+surface keyed).

## Task Count

- Setup: 9 (T001–T009)
- Foundation: 4 (T010–T013)
- US1 (P1, MVP): 4 (T014–T017) — 2 tests, 2 implementation
- US2 (P1, MVP): 2 (T018–T019) — 1 test, 1 implementation
- US3 (P2): 4 (T020–T023) — 2 tests, 2 implementation
- US4 (P2): 3 (T024–T026) — 2 tests, 1 confirm/refine
- US5 (P3): 3 (T027–T029) — 2 tests, 1 confirm
- Polish: 4 (T030–T033)
- **Total: 33 tasks**
