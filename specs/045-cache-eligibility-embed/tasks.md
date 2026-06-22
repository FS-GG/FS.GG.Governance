---
description: "Task list for F045 — Embed Cache-Eligibility Verdicts in route.json and audit.json"
---

# Tasks: Embed Cache-Eligibility Verdicts in route.json and audit.json (F045)

**Input**: Design documents from `/specs/045-cache-eligibility-embed/`

**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓,
contracts/ ✓ (RouteJson.fsi, AuditJson.fsi, route-json-document.md,
audit-json-document.md), quickstart.md ✓

**Tests**: Included. Principle V (Test Evidence Is Mandatory) and the spec's
testing section require real-evidence semantic tests over the public projection
surface, so test tasks are first-class here — written to FAIL before the `.fs`
bodies carry the verdict.

**Tier**: Tier 1 (contracted change — two public signatures + two committed wire
contracts + schema-version bump). No per-task tier annotation needed; the whole
row is Tier 1.

**Elmish/MVU applicability**: N/A. Both `ofRouteResult` and `ofShipDecision` are
PURE TOTAL projections — no state, I/O, retries, or workflow. Principle IV
explicitly exempts simple pure functions / explanation formatters; no MVU
boundary is introduced (plan Constitution Check, row IV).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another
  incomplete task in this phase)
- **[Story]**: Which user story this task serves (US1–US4)
- Exact file paths are in each description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the F041 dependency into the two edited projects and lay down
the Principle I design-first proof before any `.fs` body changes.

- [X] T001 [P] Add a `ProjectReference` on `../FS.GG.Governance.CacheEligibility/FS.GG.Governance.CacheEligibility.fsproj` (F041) to `src/FS.GG.Governance.RouteJson/FS.GG.Governance.RouteJson.fsproj`. No new third-party `PackageReference` (the F029/F030/F018 token accessors arrive transitively — the F042 precedent). Keep the existing `Route` (F019) reference.
- [X] T002 [P] Add a `ProjectReference` on `../FS.GG.Governance.CacheEligibility/FS.GG.Governance.CacheEligibility.fsproj` (F041) to `src/FS.GG.Governance.AuditJson/FS.GG.Governance.AuditJson.fsproj`. No new third-party package. Keep the existing `Ship` (F024) reference.
- [X] T003 Append an F045 FSI section to `scripts/prelude.fsx` that loads `RouteJson`, `AuditJson`, and `CacheEligibility`, builds a real report with `CacheEligibility.evaluate`, and projects both documents twice — once `Some report`, once `None` — printing the bytes. This is the Principle I design-first proof; it must read cleanly in FSI **before** the `.fs` bodies are written (quickstart §1). Confirm by eye: `Some` ⇒ per-gate `reusable`/`mustRecompute`/`notEvaluated` + `cacheEligibilityEvaluated: true`; `None` ⇒ every gate `notEvaluated` + `false`; `schemaVersion` reads `fsgg.route/v2` / `fsgg.audit/v2`; audit finding items carry no `cacheEligibility`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Commit the two public `.fsi` contracts (the authoritative shapes in
`contracts/RouteJson.fsi` / `contracts/AuditJson.fsi`). These signature changes
ripple to every callsite — production **and test** — and to every test and `.fs`
body.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete — the
new arity and schema-version constants are what the tests and bodies compile
against. Note the new arity will break compilation of the RouteCommand/ShipCommand
**test** projects until T009/T014 update their callsites.

- [X] T004 [P] Edit `src/FS.GG.Governance.RouteJson/RouteJson.fsi` to the contract in `contracts/RouteJson.fsi`: `open FS.GG.Governance.CacheEligibility.Model`; `val ofRouteResult: result: RouteResult -> cache: CacheEligibilityReport option -> string`; bump `schemaVersion` to `"fsgg.route/v2"`. Keep the XML-doc describing the not-evaluated (`None`) state, additivity, and purity. Nothing else on the surface changes.
- [X] T005 [P] Edit `src/FS.GG.Governance.AuditJson/AuditJson.fsi` to the contract in `contracts/AuditJson.fsi`: `open FS.GG.Governance.CacheEligibility.Model`; `val ofShipDecision: decision: ShipDecision -> cache: CacheEligibilityReport option -> string`; bump `schemaVersion` to `"fsgg.audit/v2"`. Keep the XML-doc (finding items carry no verdict, additive/no-hide, purity). Nothing else changes.

**Checkpoint**: Contracts committed — the build will not compile until the `.fs`
bodies (T007, T012) and every callsite (T008, T009, T013, T014) match. User
stories may now proceed.

---

## Phase 3: User Story 1 — Read the cache-eligibility verdict on route.json (Priority: P1) 🎯 MVP

**Goal**: Each `selectedGates` entry in `route.json` carries that gate's evaluated
verdict (reusable + evidence, must-recompute + cause, or not-evaluated), matched
by `GateId`, beside all of F020's existing content.

**Independent Test**: Project a real upstream-assembled `RouteResult` together
with a real `CacheEligibility.evaluate`-built report and assert each selected-gate
entry carries its verdict (evidence/cause verbatim), with gates, route trace,
findings, and cost unchanged (spec US1 Independent Test).

### Tests for User Story 1 ⚠️ (write FIRST, ensure they FAIL)

- [X] T006 [P] [US1] Add `CacheEmbedTests.fs` to `tests/FS.GG.Governance.RouteJson.Tests/` and register it in `Main.fs` and `FS.GG.Governance.RouteJson.Tests.fsproj` (ordered before `Main.fs`). Cover the verdict-shape cases over the public `RouteJson.ofRouteResult result (Some report)` using a real `CacheEligibility.evaluate` report: (1) a `Reusable` gate → `{ kind:"reusable", evidence:"<referenceValue ref>" }` with the exact opaque reference (US1.1, SC-001); (2) `MustRecompute NoPriorEvidence` → `{ kind:"mustRecompute", cause:{ kind:"noPriorEvidence" } }`, no `evidence` (US1.2); (3) `MustRecompute (InputsChanged cats)` → `cause.categories` = the changed `categoryToken`s in report order, none dropped/added (US1.3); (4) a selected gate absent from the report → `{ kind:"notEvaluated" }` (US1.4, L2). Real typed values only — no mocks of the cores (Principle V).

### Implementation for User Story 1

- [X] T007 [US1] Edit `src/FS.GG.Governance.RouteJson/RouteJson.fs`: add the `cache: CacheEligibilityReport option` parameter; build the first-by-report-order-wins `verdictByGate: Map<string, CacheEligibilityVerdict>` via `List.fold` over `CacheEligibility.entries` (data-model §"Internal render structures", D4); emit the top-level `cacheEligibilityEvaluated` boolean (`false` for `None`, `true` for `Some _`) as the last top-level field; emit a per-`selectedGates`-entry `cacheEligibility` verdict object as the entry's last field via wildcard-free exhaustive `match`es over `CacheEligibilityVerdict`/`RecomputeCause`, reusing F042's exact tokens (`EvidenceReuse.referenceValue`, `FreshnessKey.categoryToken`); set `schemaVersion` to `"fsgg.route/v2"`. Change **no** other field; the `findings` array stays verdict-free (FR-004). Stays pure/total — never dereferences the evidence ref, computes no key/hash/decision (FR-010, FR-011).
- [X] T008 [US1] Fix the host callsite in `src/FS.GG.Governance.RouteCommand/Loop.fs` (~line 248) to `RouteJson.ofRouteResult result None` — behavior preserved; the emitted document gains the not-evaluated section + v2 only.
- [X] T009 [US1] Update every `RouteJson.ofRouteResult` callsite in `tests/FS.GG.Governance.RouteCommand.Tests` to the new arity by passing `None`: the `projectExpected` helper in `Support.fs:259` (feeds `EndToEndTests.fs:33` and `InterpreterTests.fs:31/89`) and `LoopTests.fs:50`. These tests compute their `expected` route.json **live** via the projection, so they are self-consistent — this is a compile-fix (pass `None`), not a golden re-bless; once updated they assert the v2 + not-evaluated bytes automatically. Without it the suite (T023) will not compile. Depends on T004, T007.

### Surface baseline for User Story 1

- [X] T010 [US1] Re-bless `surface/FS.GG.Governance.RouteJson.surface.txt` via `BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.RouteJson.Tests`, then `git diff` it: the **only** change must be `ofRouteResult` gaining a second `FSharpOption<CacheEligibilityReport>` parameter (quickstart §4). Depends on T004, T007.

**Checkpoint**: `route.json` carries the cache verdict; `RouteJson.Tests` and
`RouteCommand.Tests` green; RouteCommand recompiles. MVP demonstrable on its own.

---

## Phase 4: User Story 2 — Read the cache-eligibility verdict on audit.json (Priority: P1)

**Goal**: Each `kind:"gate"` item (in blockers/warnings/passing) in `audit.json`
carries its verdict matched by `GateId`; `kind:"finding"` items carry none; the
ship verdict, exit-code basis, and six-field enforcement detail are untouched.

**Independent Test**: Project a real `ShipDecision` + report and assert every gate
item carries its verdict and every finding item carries none, with verdict/basis/
enforcement byte-identical to the F025-only projection (spec US2 Independent Test).

### Tests for User Story 2 ⚠️ (write FIRST, ensure they FAIL)

- [X] T011 [P] [US2] Add `CacheEmbedTests.fs` to `tests/FS.GG.Governance.AuditJson.Tests/` and register it in `Main.fs` and `FS.GG.Governance.AuditJson.Tests.fsproj`. Cover, over `AuditJson.ofShipDecision decision (Some report)`: each `kind:"gate"` item — across blockers, warnings, and passing — carries its verdict matched by `GateId`, evidence/cause verbatim (US2.1, SC-001); each `kind:"finding"` item carries **no** `cacheEligibility` field (US2.2, SC-002, L4); a gate item absent from the report → `{ kind:"notEvaluated" }` (US2.3). Real `ShipDecision` + real `CacheEligibility.evaluate` report.

### Implementation for User Story 2

- [X] T012 [US2] Edit `src/FS.GG.Governance.AuditJson/AuditJson.fs`: add the `cache: CacheEligibilityReport option` parameter; reuse the same first-wins `verdictByGate` map build and the same verdict/cause render as T007; emit the top-level `cacheEligibilityEvaluated` flag last; emit a per-`kind:"gate"`-item `cacheEligibility` field (last field of the item) in every section; leave `kind:"finding"` items unchanged (FR-004); set `schemaVersion` to `"fsgg.audit/v2"`. No other field changes — verdict, exitCodeBasis, sections, and enforcement detail stay byte-identical (FR-008).
- [X] T013 [US2] Fix the host callsite in `src/FS.GG.Governance.ShipCommand/Loop.fs` (~line 286) to `AuditJson.ofShipDecision decision None` — behavior preserved; document gains the not-evaluated section + v2 only.
- [X] T014 [US2] Update every `AuditJson.ofShipDecision` callsite in `tests/FS.GG.Governance.ShipCommand.Tests` to the new arity by passing `None`: `LoopTests.fs:50` and any `expected` computation in `EndToEndTests.fs` / `Support.fs`. These tests compute their `expected` audit.json **live** via the projection — this is a compile-fix (pass `None`), not a golden re-bless; once updated they assert the v2 + not-evaluated bytes automatically. Without it the suite (T023) will not compile. Depends on T005, T012.
- [X] T015 [US2] Update the F028 fixture generator `tests/FS.GG.Governance.EnforcementFixtures.Tests/Generator.fs` to call `ofShipDecision (rollup …) None` (the snapshots are projected with no report). Depends on T005, T012.
- [X] T016 [US2] Re-bless the 7 `fixtures/enforcement/audit-snapshots/*.audit.json` golden snapshots via `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests`, then `git diff fixtures/enforcement/audit-snapshots/`: the **only** per-file delta must be `schemaVersion` → `fsgg.audit/v2`, a trailing top-level `cacheEligibilityEvaluated: false`, and each gate item gaining `cacheEligibility: { kind:"notEvaluated" }`. If any other byte moved, the embed touched an existing field — fix the body (T012), do not bless it (quickstart §5, SC-004). Depends on T012, T015.

### Surface baseline for User Story 2

- [X] T017 [US2] Re-bless `surface/FS.GG.Governance.AuditJson.surface.txt` via `BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.AuditJson.Tests`, then `git diff`: the **only** change must be `ofShipDecision` gaining a second `FSharpOption<CacheEligibilityReport>` parameter. Depends on T005, T012.

**Checkpoint**: `audit.json` carries the per-gate verdict; F028 snapshots
re-blessed additively; `AuditJson.Tests`, `ShipCommand.Tests`, and
`EnforcementFixtures.Tests` green.

---

## Phase 5: User Story 3 — The verdict never hides, never blocks, never fabricates (Priority: P1)

**Goal**: Prove the governance invariants on both documents — every must-recompute
names its full cause; an unevaluated gate is legibly `notEvaluated`, never
`reusable`; no raw freshness inputs / hash / key / cache-derived enforcement
field; the evidence reference is verbatim and never dereferenced; every non-cache
field is byte-identical to the F020/F025-only output.

**Independent Test**: Project documents covering each verdict shape plus a
not-evaluated gate and the `None` case; assert the no-hide, additivity, and
no-derivation laws (spec US3 Independent Test, laws L2/L3/L7/L8).

> These tasks **extend** the `CacheEmbedTests.fs` files created in T006/T011 (same
> files → not parallel with each other across a project, depend on the impl).

- [X] T018 [US3] Extend `tests/FS.GG.Governance.RouteJson.Tests/CacheEmbedTests.fs` with the no-hide / additivity / no-derivation laws: every `mustRecompute` names its full cause, `inputsChanged` categories in report order, never truncated (L3, SC-005); `None` and absent-from-report gates render `notEvaluated`, never `reusable` (L2, SC-005); the document carries no raw freshness input, no hash, no computed key, no cache-derived severity/skip field, and the evidence ref appears verbatim (L8, SC-007); every non-cache field is byte-identical to the pre-embed (recomputed F020-only) projection of the same `RouteResult` modulo the section + version (L7, SC-004); an orphan report entry whose `GateId` matches no selected gate adds nothing (L5, FR-006). Depends on T006, T007.
- [X] T019 [US3] Extend `tests/FS.GG.Governance.AuditJson.Tests/CacheEmbedTests.fs` with the same no-hide / additivity / no-derivation laws over `ofShipDecision`, plus: a `reusable` verdict on a base-`blocking` gate leaves it in the blockers section with full six-field enforcement detail — the cache verdict alters no verdict, severity, section, or ship outcome (L7, US2.4, SC-004); orphan dropped (L5). Depends on T011, T012.

**Checkpoint**: Both documents proven additive and no-hide; the invariants that
make the verdict safe on the canonical artifacts are under test.

---

## Phase 6: User Story 4 — A stable, versioned contract for consumers (Priority: P2)

**Goal**: Byte-stable, order-independent, versioned documents with deterministic
per-gate cache order and a deterministic duplicate-`GateId` rule.

**Independent Test**: Project the same inputs twice (byte-equal); project
value-equal differently-ordered upstreams (identical); assert each document
declares its v2 schema version and cache entries follow the existing gate order
(spec US4 Independent Test, laws L6/L10/L11).

> Extend the same `CacheEmbedTests.fs` files (or the existing `DeterminismTests.fs`
> per project) — depend on the impl.

- [X] T020 [US4] Add determinism / version / order tests to `tests/FS.GG.Governance.RouteJson.Tests/` (CacheEmbedTests.fs or DeterminismTests.fs): same inputs twice ⇒ byte-identical (L10, SC-003); value-equal report from a differently-ordered entry list ⇒ identical text, cache entries in `GateId`-ordinal order (L10); a duplicate `GateId` in the report resolves to the first entry by report order, deterministically (L6, FR-007); `schemaVersion` = `"fsgg.route/v2"` (US4.3); totality — `None`, `Some (CacheEligibilityReport [])`, empty route, finding-only route each return a document with the section present and never throw (L11, SC-006). Depends on T007.
- [X] T021 [US4] Add the same determinism / version / order / totality tests to `tests/FS.GG.Governance.AuditJson.Tests/` over `ofShipDecision`: byte-stable repeats; value-equal differently-ordered upstreams identical with cache entries in the `ShipDecision` composite item order; duplicate `GateId` → first-by-report-order; `schemaVersion` = `"fsgg.audit/v2"`; totality over `None` / empty report / clean empty decision. Depends on T012.

**Checkpoint**: Both documents are stable, versioned, order-independent contracts.

---

## Phase 7: Polish & Cross-Cutting Verification

**Purpose**: Confirm containment (F042/F044 untouched), the whole suite is green,
and the docs pointer is current.

- [X] T022 [P] Confirm F042/F044 are untouched (SC-008, FR-015): `dotnet test tests/FS.GG.Governance.CacheEligibilityJson.Tests` and `tests/FS.GG.Governance.CacheEligibilityCommand.Tests` pass, and `git status` shows **zero** edits to `src/FS.GG.Governance.CacheEligibilityJson`, `src/FS.GG.Governance.CacheEligibilityCommand`, and `surface/FS.GG.Governance.CacheEligibilityJson.surface.txt` (quickstart §6). Note: F044's tests assert no F020/F025 output bytes — only a surface-drift forbidden-reference check — so they require no re-bless (resolves the spec Assumption hedge about "F044 expected-output tests").
- [X] T023 Whole-suite gate: `dotnet test FS.GG.Governance.sln` green across the board. The `RouteCommand` / `ShipCommand` end-to-end tests compute their expected bytes live via the projection, so once their callsites pass `None` (T009, T014) they assert the v2 + not-evaluated shape automatically — no separate golden re-bless. Confirm no committed `route.json` fixtures needed re-blessing (only gitignored `.tmp/` exists — plan, line 54). Depends on all prior phases.
- [X] T024 [P] Verify `CLAUDE.md` SPECKIT plan pointer references `specs/045-cache-eligibility-embed/plan.md` (already current — update only if drifted).
- [X] T025 Run the `quickstart.md` validation end-to-end (FSI proof, build, tests, both re-bless paths, F042/F044 containment check, whole-suite gate) and confirm every step matches its stated expected output. Depends on T023.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories** (the
  `.fsi` arity + schema-version constants). The new arity breaks the
  RouteCommand/ShipCommand **test** projects until T009/T014 update their
  callsites.
- **US1 (Phase 3)** and **US2 (Phase 4)**: both depend only on Foundational; they
  touch disjoint files (RouteJson vs AuditJson, RouteCommand vs ShipCommand, and
  their respective test projects) and may run **fully in parallel**.
- **US3 (Phase 5)** and **US4 (Phase 6)**: each task depends on its document's
  impl (T007 for route, T012 for audit) because it extends that document's test
  file; route-side and audit-side tasks remain mutually parallel.
- **Polish (Phase 7)**: depends on all desired stories being complete.

### Within each user story

- Tests written and FAILing before the `.fs` body (T006→T007, T011→T012).
- Body before its production callsite fix, its **test** callsite fix, surface
  re-bless, and golden re-bless.
- The audit golden re-bless (T016) depends on both the body (T012) and the
  generator update (T015).
- The command-test callsite fixes (T009, T014) are required for the suite to
  compile (T023).

### Parallel opportunities

- T001 ‖ T002 (different `.fsproj`s).
- T004 ‖ T005 (different `.fsi`s).
- Entire **US1 phase ‖ US2 phase** once Foundational is done (disjoint files).
- T018 ‖ T019, T020 ‖ T021 (route-side ‖ audit-side test files).
- T022 ‖ T024 in Polish.

### Parallel example (after Phase 2)

```bash
# Two developers / two streams, disjoint files:
Stream A (US1, route):  T006 → T007 → T008 → T009 → T010
Stream B (US2, audit):  T011 → T012 → T013 → T014 → T015 → T016 → T017
# then, each after its impl:
Stream A: T018, T020      Stream B: T019, T021
```

---

## Task count per user story

- **Setup**: 3 (T001–T003)
- **Foundational**: 2 (T004–T005)
- **US1 (route.json verdict, P1, MVP)**: 5 (T006–T010)
- **US2 (audit.json verdict, P1)**: 7 (T011–T017)
- **US3 (no-hide / additive / no-fabricate, P1)**: 2 (T018–T019)
- **US4 (versioned / deterministic contract, P2)**: 2 (T020–T021)
- **Polish**: 4 (T022–T025)
- **Total**: 25 tasks

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)** — `route.json` carrying the per-gate
cache-eligibility verdict is the embed's reason to exist (the freshness inputs
already live there, F020 FR-014). It is independently demonstrable and testable
before audit.json. US2 is co-P1 (maintainer scoped both this row), so the full
delivery is Phases 1–6; US4 (P2) is the determinism/version hardening on top.

## Notes

- All evidence is **real** typed values built through the public F041
  `CacheEligibility.evaluate` and the F030/F029 newtypes — no mocks of the cores
  (Principle V; plan Testing).
- Never mark a task `[X]` on a failing assertion; never weaken an assertion to
  green a build — narrow scope and document instead.
- Commit after each task or logical group; re-bless steps (T010, T016, T017) are
  deliberate, reviewed diffs, not blind blesses. The command-test callsite fixes
  (T009, T014) are compile-fixes, not blesses — those tests recompute expected
  bytes live.
