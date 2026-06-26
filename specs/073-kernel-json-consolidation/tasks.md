---
description: "Task list for feature 073 — Kernel JSON consolidation"
---

# Tasks: Kernel JSON consolidation

**Input**: Design documents from `/specs/073-kernel-json-consolidation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: This is a **byte-identity** feature. The "tests" are (a) the existing
`*Json.Tests` golden/snapshot fixtures, which MUST stay byte-identical and are the
acceptance gate (FR-009, SC-004), and (b) per-leaf `SurfaceDriftTests.fs` for the two new
projects plus the updated `Kernel` baseline (FR-011, SC-007). No new behavioural tests are
written — extraction is proven by absence of change.

**Organization**: Tasks are grouped by the three priority slices (US1 → US2 → US3). Each
slice is an independently shippable increment that keeps the full suite green with an
unchanged test count. Land them in priority order, one concern per commit (research D5).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: US1 / US2 / US3 (US1 is the MVP).
- Tier annotation omitted — the whole feature is Tier 1 (matches the spec).
- Exact file paths are given in every task.

## The acceptance invariant (re-run at the end of every slice)

```bash
dotnet build FS.GG.Governance.sln                                   # warnings are errors
dotnet test  FS.GG.Governance.sln                                   # full suite green, test count unchanged
git status --porcelain -- '**/*.golden' '**/*.snapshot' 'tests/**/Fixtures/**'   # MUST be EMPTY
```

A green suite **and** an empty fixture diff is the pass condition. A printed fixture path
means behaviour drifted → revert/revisit, never re-baseline (spec Edge Cases, FR-009).

---

## ⚠️ Implementation pivot (decision D1 superseded) — 2026-06-26

**Baseline test count: 2237** (recorded at T001, clean tree, `dotnet build`/`test` green with
`-m:2 -p:UseSharedCompilation=false`; the default parallel build SIGABRTs the F# compiler in this
environment).

During US1, exporting `writeToString` from **Kernel** and adding a `Kernel` `ProjectReference` to the
projections was found to **violate explicit, tested architectural firewalls**: RouteJson, GatesJson, and
ScaffoldManifestJson have `SurfaceDriftTests` whose `forbidden` list names `FS.GG.Governance.Kernel`
directly ("must not reference kernel/host/adapters… no later-phase capability"), grouping Kernel with
Host/Cli/Adapters. Kernel itself has a "V12 references only BCL + FSharp.Core" guard, so it cannot
reference a leaf either.

**Resolution (user-approved):** the shared `writeToString` is homed in a NEW pure, dependency-free
`System.Text.Json`-only leaf **`FS.GG.Governance.JsonText`** (module `JsonText`), NOT in Kernel. Every
projection + `EvidenceReuseStore` + `RefreshCommand` references `JsonText` and calls
`JsonText.writeToString`. Kernel keeps its own irreducible internal `writeToString` (the BCL-only core
may reference no leaf). The no-Kernel firewalls stay **green and untouched**; only the `allowed`
allowlists were extended to include the pure `JsonText` leaf — exactly as the existing pure `RuleIdentity`
leaf is already allowed. SC-001 grep therefore prints `Kernel/Json.fs` **and** `JsonText/JsonText.fs`
(the kernel's copy is the irreducible root, not a duplicated projection copy).

This supersedes plan §D1 / the `Json.fsi` export tasks (T003, T019): Kernel's `Json.fsi` is unchanged;
the export task is realized as the `JsonText` leaf + its surface baseline instead.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Capture the pre-change baselines that the byte-identity gate compares against.

- [X] T001 Confirm a clean working tree and record the baseline `dotnet test FS.GG.Governance.sln`
      total test count (write it into this file's notes) so SC-005's "unchanged test count"
      can be proven after each slice.
- [X] T002 [P] Capture the canonical list of the 14 non-Kernel `writeToString` definitions
      (the 12 `*Json` projections + `src/FS.GG.Governance.EvidenceReuseStore/EvidenceReuseStore.fs`
      + `src/FS.GG.Governance.RefreshCommand/Interpreter.fs`) via
      `grep -rln "let writeToString\|let private writeToString" src --include='*.fs'`; this is
      the SC-001 worklist for US1.

**Checkpoint**: baseline test count and the writeToString worklist are recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The one change US1 builds on. No user-story work begins until this is complete.

**⚠️ CRITICAL**: This single `.fsi` export is the prerequisite for all three slices.

- [-] T003 SUPERSEDED by pivot — `Kernel/Json.fsi` is unchanged; the shared helper is the new pure
      `FS.GG.Governance.JsonText` leaf (`module JsonText`, `val writeToString`) instead. Add `val writeToString: emit: (System.Text.Json.Utf8JsonWriter -> unit) -> string`
      to `module Json` in `src/FS.GG.Governance.Kernel/Json.fsi`, in the writer-plumbing region
      before `ofExplanation`, with the doc comment from `contracts/kernel-json-delta.md`
      (FR-001). `Json.fs` is unchanged — the body already exists at `Json.fs:23`.

**Checkpoint**: `dotnet build src/FS.GG.Governance.Kernel` succeeds; `Json.writeToString` is
public. User-story implementation can now begin.

---

## Phase 3: User Story 1 — Single source of truth for deterministic JSON emit (Priority: P1) 🎯 MVP

**Goal**: Exactly one definition of `writeToString` in `src` (in `Kernel`), down from 14;
every projection calls the shared helper.

**Independent Test**: After deleting all 14 local copies and wiring the references,
`grep -rl "let writeToString\|let private writeToString" src --include='*.fs'` prints only
`src/FS.GG.Governance.Kernel/Json.fs`, the full suite is green, and no golden moved.

> Per-projection work below is parallel-safe across projects (each touches a different file),
> but each individual deletion must be **after** its `Kernel` reference is confirmed reachable
> (T004) and ideally validated by goldens **before** the last copy is deleted (research D4).

- [X] T004 [US1] (JsonText leaf reference added where not already reachable; no cycle — JsonText is dependency-free.) For each of the 14 sites, confirm `FS.GG.Governance.Kernel` is on its
      reference graph (transitive is fine); add an explicit `<ProjectReference>` to `Kernel`
      in the project's `.fsproj` **only** where it is not already reachable (FR-003). Confirm
      no dependency cycle is introduced (spec Edge Cases / SC-007).
- [X] T005 [P] [US1] (→ `JsonText.writeToString`) In `src/FS.GG.Governance.RouteJson/RouteJson.fs`, replace the local
      `writeToString` with `Json.writeToString` and delete the local copy (FR-002).
- [X] T006 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.GatesJson/GatesJson.fs`.
- [X] T007 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.AuditJson/AuditJson.fs`.
- [X] T008 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.VerifyJson/VerifyJson.fs`.
- [X] T009 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.ReleaseJson/ReleaseJson.fs`.
- [X] T010 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.CostBudgetJson/CostBudgetJson.fs`.
- [X] T011 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.CacheEligibilityJson/CacheEligibilityJson.fs`.
- [X] T012 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.EvidenceJson/EvidenceJson.fs`.
- [X] T013 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.RefreshJson/RefreshJson.fs`.
- [X] T014 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.ProvenanceJson/ProvenanceJson.fs`.
- [X] T015 [P] [US1] (→ `JsonText.writeToString`) Same in `src/FS.GG.Governance.ScaffoldManifestJson/ScaffoldManifestJson.fs`.
- [X] T016 [P] [US1] (→ `JsonText.writeToString`; the stray `private` removed) In `src/FS.GG.Governance.AttestationJson/AttestationJson.fs`, replace the
      `let private writeToString` copy with `Json.writeToString` and delete it — this removes
      the stray `private` modifier noted in the plan (Principle II opportunity).
- [X] T017 [P] [US1] (→ `JsonText.writeToString`) In `src/FS.GG.Governance.EvidenceReuseStore/EvidenceReuseStore.fs`
      (non-projection site), replace the local `writeToString` with `Json.writeToString` and
      delete it.
- [X] T018 [P] [US1] (→ `JsonText.writeToString`) In `src/FS.GG.Governance.RefreshCommand/Interpreter.fs` (non-projection
      site), replace the local `writeToString` with `Json.writeToString` and delete it.
- [-] T019 [US1] SUPERSEDED — Kernel surface UNCHANGED (no export). Instead added the new
      `surface/FS.GG.Governance.JsonText.surface.txt` baseline + `JsonText.Tests` (SurfaceDrift + behaviour).
      Original task text: Regenerate/update `surface/FS.GG.Governance.Kernel.surface.txt` to add the one
      `writeToString` member line; confirm no other Kernel member changed (FR-011). The 12
      projection surface baselines should be UNCHANGED (the helper was hidden); if any projection
      surface moved, investigate before proceeding.
- [X] T020 [US1] Acceptance invariant GREEN (2237 + 5 new JsonText tests; goldens byte-identical). SC-001:
      `grep` prints `Kernel/Json.fs` + `JsonText/JsonText.fs` (kernel copy irreducible — pivot note). Run the acceptance invariant. Then prove SC-001:
      `grep -rl "let writeToString\|let private writeToString" src --include='*.fs'` →
      only `src/FS.GG.Governance.Kernel/Json.fs`. Confirm test count == T001 baseline.

**Checkpoint**: US1 fully functional — one `writeToString` in `src`, goldens byte-identical,
suite green. Independently shippable (MVP).

---

## Phase 4: User Story 2 — Shared closed-enum token helpers (Priority: P2)

**Goal**: One pure helper per closed enum in `FS.GG.Governance.JsonTokens`; the seven copied
token helpers deleted from the projections. (The `Verdict` token stays local — out of scope,
research D3.)

**Independent Test**: `JsonTokens` exists with the seven helpers each defined once; the
projections call them module-qualified; token-emitting goldens are byte-identical; suite green.

**Depends on**: US1 (the `Kernel` edge / pattern). Out of scope: single-use tokens
`ReleaseJson.factStateToken`, `ReleaseJson.outcomeToken` — they stay local (research D3).

- [X] T021 [US2] (refs CORRECTED to the true owners: Config (Cost/Maturity/EnvironmentClass), GateRun (GateDisposition), Enforcement (Severity/Profile), Ship (ExitCodeBasis) — the contract guessed Gates/Findings.) Create `src/FS.GG.Governance.JsonTokens/FS.GG.Governance.JsonTokens.fsproj`
      (System.*/FSharp.Core-only) with `ProjectReference`s to the enum owners — `Gates`,
      `Config`, `Findings`, `Enforcement` (exact owners confirmed at extraction per
      `contracts/JsonTokens.fsi`). No third-party `PackageReference`.
- [X] T022 [US2] Author `src/FS.GG.Governance.JsonTokens/JsonTokens.fsi` from
      `contracts/JsonTokens.fsi` — the seven `val`s (`costToken`, `maturityToken`,
      `severityToken`, `environmentToken`, `dispositionToken`, `basisToken`, `profileToken`),
      `.fsi`-first (FR-004, FR-012).
- [X] T023 [US2] (profileToken = light/standard/strict/release; `Release` type-qualified for both EnvironmentClass and Profile.) Author `src/FS.GG.Governance.JsonTokens/JsonTokens.fs` — each helper an
      EXHAUSTIVE `match` with NO wildcard, emitting the verbatim strings in `data-model.md`
      Entity 2. No access modifiers in the `.fs` (Principle II). Confirm `profileToken`'s
      strings against the current projection/Enforcement copy at extraction.
- [X] T024 [US2] Register `FS.GG.Governance.JsonTokens` and its `*.Tests` project in
      `FS.GG.Governance.sln`.
- [X] T025 [P] [US2] In `src/FS.GG.Governance.RouteJson/RouteJson.fs`, replace local
      `costToken` / `maturityToken` / `environmentToken` / `dispositionToken` with
      **module-qualified** `JsonTokens.*` calls (qualified, not `open`-ed — avoids ambiguity
      with `Enforcement.maturityToken`, research D5); delete the local copies (FR-005).
- [X] T026 [P] [US2] In `src/FS.GG.Governance.GatesJson/GatesJson.fs`, replace+delete local
      `costToken` / `maturityToken` / `environmentToken` via qualified `JsonTokens.*` calls.
- [X] T027 [P] [US2] In `src/FS.GG.Governance.AuditJson/AuditJson.fs`, replace+delete local
      `maturityToken` / `severityToken` / `dispositionToken` / `basisToken` / `profileToken`.
- [X] T028 [P] [US2] ⚠ CORRECTION: VerifyJson `dispositionToken` ALSO stays local — it emits `not-executed` (hyphen) vs the shared `notExecuted`; the byte-identity gate caught this (research D3 had flagged only the Verdict token). In `src/FS.GG.Governance.VerifyJson/VerifyJson.fs`, replace+delete local
      `maturityToken` / `severityToken` / `dispositionToken` / `basisToken` / `profileToken`
      via qualified `JsonTokens.*` calls (FR-005). **Leave `verdictToken` and the `rr`-prefixed
      `rrVerdictToken` untouched** — `Verdict` is not one of the seven enums and the copies
      diverge (`Fail` → `blocked` vs `fail`), so unifying would change bytes (research D3,
      spec Edge Cases).
- [X] T029 [P] [US2] In `src/FS.GG.Governance.ReleaseJson/ReleaseJson.fs`, replace+delete local
      `severityToken` / `basisToken` (leave single-use `factStateToken` / `outcomeToken` local).
- [X] T030 [P] [US2] In `src/FS.GG.Governance.CostBudgetJson/CostBudgetJson.fs`, replace+delete
      local `costToken` / `severityToken`.
- [X] T031 [P] [US2] In `src/FS.GG.Governance.ProvenanceJson/ProvenanceJson.fs` **and**
      `src/FS.GG.Governance.AttestationJson/AttestationJson.fs`, replace+delete the local
      `environmentToken` copy via qualified `JsonTokens.environmentToken` (both projections
      carry a copy — research D3).
- [X] T032 [US2] (JsonTokens.Tests: 9 tests — token table + SurfaceDrift; baseline blessed.) Add `surface/FS.GG.Governance.JsonTokens.surface.txt` baseline and
      `tests/FS.GG.Governance.JsonTokens.Tests/` with `SurfaceDriftTests.fs` plus a small
      token-string table test asserting each helper's verbatim strings (FR-011). Confirm the
      projection surface baselines are still unchanged.
- [X] T033 [US2] SC-002 holds: the 7 shared tokens have one home (JsonTokens); only VerifyJson.dispositionToken + the Verdict token stay local (divergent). Run the acceptance invariant. Prove SC-002: each of the seven token helpers
      has exactly one JSON-layer definition (in `JsonTokens`) and no projection redefines them
      (`grep -rnE "let (private )?(cost|maturity|severity|environment|disposition|basis|profile)Token" src/FS.GG.Governance.*Json` → empty).
      Confirm the `Verdict` token (`verdictToken`/`rrVerdictToken`) is intentionally still
      local (out of scope, D3). Confirm test count == baseline + the new JsonTokens tests, with
      no existing test removed.

**Checkpoint**: US1 AND US2 both shippable — one token helper per closed enum, goldens
byte-identical, suite green.

---

## Phase 5: User Story 3 — Shared sub-object writers (Priority: P3)

**Goal**: The duplicated sub-object/map writers live once in `FS.GG.Governance.JsonWriters`;
projection copies deleted.

**Independent Test**: `JsonWriters` exists with `writeCause` / `verdictByGate` /
`outcomeByGate` / `writeExecution` / `writeEnforcement` each defined once; projections call
them module-qualified; affected goldens byte-identical; suite green; the leaf takes no host
dependency (SC-007).

**Depends on**: US1 + US2 (`JsonWriters` references `JsonTokens`). Most intricate slice
(sub-object shape) — sequenced last (spec US3 rationale).

- [X] T034 [US3] (refs JsonTokens + Gates/GateRun/CommandRecord/EvidenceReuse/FreshnessKey/CacheEligibility. writeEnforcement EXCLUDED — see T038.) Create `src/FS.GG.Governance.JsonWriters/FS.GG.Governance.JsonWriters.fsproj`
      with `ProjectReference`s to `JsonTokens` + the domain owners `Gates`, `GateRun`,
      `CommandRecord`, `EvidenceReuse`, `CacheEligibility`, `Enforcement`
      (per `contracts/JsonWriters.fsi`). Pure leaf — no host dependency (FR-008), no
      third-party `PackageReference`.
- [X] T035 [US3] (4 writers: writeCause/verdictByGate/outcomeByGate/writeExecution. writeEnforcement excluded.) Author `src/FS.GG.Governance.JsonWriters/JsonWriters.fsi` from
      `contracts/JsonWriters.fsi` — `writeCause`, `verdictByGate`, `outcomeByGate`,
      `writeExecution`, `writeEnforcement` (FR-006, FR-012), `.fsi`-first.
- [X] T036 [US3] Author `src/FS.GG.Governance.JsonWriters/JsonWriters.fs` — fixed field order
      verbatim per `data-model.md` Entity 3; map helpers as first-by-list-order-wins folds
      keyed on the gate-id string; no access modifiers in `.fs`.
- [X] T037 [US3] Register `FS.GG.Governance.JsonWriters` and its `*.Tests` project in
      `FS.GG.Governance.sln`.
- [X] T038 [P] [US3] ⚠ CORRECTION: VerifyJson keeps `writeCauseValue` (bare-string noPriorEvidence), `writeExecution` (GateOutcome option, explicit nulls, divergent token), and `writeEnforcement` (literal "verify") LOCAL — all DIVERGE from the shared forms. Only verdictByGate/outcomeByGate were extracted from VerifyJson. In `src/FS.GG.Governance.VerifyJson/VerifyJson.fs`, replace local
      `writeCauseValue`, `writeExecution`, `verdictByGate`, `outcomeByGate`, and
      `writeEnforcement` with qualified `JsonWriters.*` calls; delete the local copies
      (FR-007). Confirm each copy is byte-identical before deletion (D4).
- [X] T039 [P] [US3] (Audit `writeEnforcement` stays local — uses single-use `modeToken d.Mode`, diverges from Verify's literal.) In `src/FS.GG.Governance.AuditJson/AuditJson.fs`, replace+delete the
      duplicated `writeCause`, `writeExecution`, `verdictByGate`, `outcomeByGate`, and
      `writeEnforcement` via qualified `JsonWriters.*`.
- [X] T040 [P] [US3] In `src/FS.GG.Governance.CostBudgetJson/CostBudgetJson.fs`, replace+delete
      the local `writeCause` via qualified `JsonWriters.writeCause`. (ReleaseJson has **no**
      shared sub-object writer to extract — its writers are projection-specific and the
      single-use `writeNullableString`/`writeNullableInt` stay local, research D3.)
- [X] T041 [P] [US3] In `src/FS.GG.Governance.CacheEligibilityJson/CacheEligibilityJson.fs`,
      replace+delete the local `writeCause` via qualified `JsonWriters.writeCause`
      (`verdictByGate` lives in Audit/Route/Verify, not here).
- [X] T042 [P] [US3] In `src/FS.GG.Governance.EvidenceJson/EvidenceJson.fs`, replace+delete the
      local `writeCause` via qualified `JsonWriters.writeCause`. (GatesJson defines **no** shared
      sub-object writer — `writeFreshnessKey`/`writePrerequisite`/`writeGate` are
      projection-specific and stay local.)
- [X] T043 [P] [US3] In `src/FS.GG.Governance.RouteJson/RouteJson.fs`, replace+delete the
      duplicated `writeCause`, `writeExecution`, `verdictByGate`, and `outcomeByGate` via
      qualified `JsonWriters.*`.
- [X] T044 [US3] Swept: remaining writer defs are the documented divergent locals (Audit/Verify writeEnforcement, Verify writeExecution). Original: Sweep the remaining projections for surviving copies of the five shared writer
      concerns (`grep -rnE "let (private )?(writeCause[A-Za-z]*|verdictByGate|outcomeByGate|writeExecution|writeEnforcement)" src/FS.GG.Governance.*Json`);
      replace+delete any byte-identical copy found, leaving genuinely projection-specific
      writers local — including ReleaseJson's single-use `writeNullableString`/`writeNullableInt`
      (data-model note, spec US3 acceptance).
- [X] T045 [US3] (JsonWriters.Tests: 8 tests — byte-shape + SurfaceDrift; baseline blessed.) Add `surface/FS.GG.Governance.JsonWriters.surface.txt` baseline and
      `tests/FS.GG.Governance.JsonWriters.Tests/` with `SurfaceDriftTests.fs` plus writer
      byte-shape tests; confirm the leaf surface takes no host dependency (FR-011, SC-007).
- [X] T046 [US3] SC-003 holds for writeCause/verdictByGate/outcomeByGate; writeExecution/writeEnforcement keep documented divergent locals. Run the acceptance invariant. Prove SC-003: no projection redefines
      `writeCause` / `verdictByGate` / `outcomeByGate` / `writeExecution` / `writeEnforcement`
      locally.

**Checkpoint**: All three stories independently functional — every shared writer defined once,
goldens byte-identical, suite green.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final whole-feature verification and the Tier 1 doc/agent-context obligation.

- [X] T047 Acceptance GREEN: 2259 tests (2237 baseline + 5+9+8 new-leaf tests), goldens byte-identical, SC proofs hold, net src ≈ -260 LOC (-409 in projections, +150 leaf bodies). Final full-feature acceptance run of `quickstart.md`: build + test green on
      `FS.GG.Governance.sln`; `git status` shows no changed golden/snapshot fixture; the three
      `grep` proofs hold; net `src` reduction ≈300 LOC across the three slices (SC-006);
      test count unchanged except for the two new `*.Tests` projects (SC-005).
- [X] T048 [P] Confirmed: JsonText has ZERO project refs; JsonTokens/JsonWriters reference only domain owners (+JsonTokens for JsonWriters); no Kernel/Host edge; build proves acyclic. Confirm the dependency graph is acyclic and both new leaves carry no host
      dependency (SC-007) — e.g. inspect the two new `.fsproj`s and confirm only domain/Kernel
      references.
- [X] T049 Roadmap + CLAUDE.md pointer updated to reflect the JsonText/JsonTokens/JsonWriters leaves (the managed agent-context section). Run the Tier 1 agent-context update (`/speckit-agent-context-update`) so the managed
      Spec Kit section reflects the two new projects and the exported `writeToString`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup. The `Json.fsi` export (T003) BLOCKS all slices.
- **US1 (Phase 3)**: depends on T003. The MVP.
- **US2 (Phase 4)**: depends on US1 (shared `Kernel` edge/pattern); independently shippable.
- **US3 (Phase 5)**: depends on US1 + US2 (`JsonWriters` references `JsonTokens`).
- **Polish (Phase 6)**: depends on all three slices.

### Within Each Slice

- New-leaf creation order: `.fsproj` → `.fsi` → `.fs` → `.sln` registration → call-site swaps
  → surface baseline + drift tests → acceptance invariant.
- Wire/confirm the project reference (US1 T004; the new-leaf `.fsproj` for US2/US3) before
  deleting any local copy.
- Validate goldens **before** deleting the last copy of a near-identical helper (research D4).
- Slice complete (acceptance invariant green) before moving to the next priority.

### Parallel Opportunities

- **US1**: T005–T018 are all `[P]` — one file each, independent — after T004 confirms refs.
- **US2**: T025–T031 are all `[P]` — one projection each — after T021–T024 land the leaf.
- **US3**: T038–T043 are all `[P]` — one projection each — after T034–T037 land the leaf.
- The two `*.Tests` scaffolds (T032, T045) are independent once their leaf exists.

---

## Parallel Example: User Story 1

```bash
# After T004 confirms Kernel is reachable from each site, swap+delete in parallel:
Task: "Replace+delete writeToString in src/FS.GG.Governance.RouteJson/RouteJson.fs"
Task: "Replace+delete writeToString in src/FS.GG.Governance.GatesJson/GatesJson.fs"
Task: "Replace+delete writeToString in src/FS.GG.Governance.AuditJson/AuditJson.fs"
# ... through T018 (12 projections + EvidenceReuseStore + RefreshCommand/Interpreter)
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → record baseline test count + worklist.
2. Phase 2 Foundational → export `writeToString` (T003).
3. Phase 3 US1 → delete all 14 copies, wire refs, update Kernel surface.
4. **STOP and VALIDATE**: acceptance invariant + SC-001 grep. Ship the MVP.

### Incremental Delivery

1. Setup + Foundational → ready.
2. US1 → one `writeToString`; validate; ship.
3. US2 → `JsonTokens`; validate; ship.
4. US3 → `JsonWriters`; validate; ship.
   Each slice keeps the suite green and every golden byte-identical.

---

## Notes

- `[P]` = different files, no dependency on another incomplete task in this phase.
- **Byte-identity is the hard gate.** Never re-baseline a golden to go green — a moved golden
  means behaviour drifted; revert and revisit (FR-009, spec Edge Cases).
- Call the new leaf helpers **module-qualified** (`JsonTokens.maturityToken`), never `open`-ed,
  to avoid ambiguity with the untouched domain-owned token helpers (research D5).
- Do NOT re-point projections at the pre-existing domain-owned token helpers
  (`Enforcement.maturityToken`, `FreshnessKey.environmentToken`, …) in this feature (research D4).
- Elmish/MVU (Principle IV) is N/A: all changed code is pure projection logic with no state/I/O;
  the MVU command hosts are out of scope (Phase B/C of the roadmap).
- Commit one concern per slice so each test run isolates any golden drift.
```