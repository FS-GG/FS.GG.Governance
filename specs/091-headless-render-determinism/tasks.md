---
description: "Task list for Headless Render-Width Test Determinism"
---

# Tasks: Headless Render-Width Test Determinism

**Input**: Design documents from `/specs/091-headless-render-determinism/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/width-resilience.md, quickstart.md

**Tier**: Tier 2 (internal change) — no public API/`.fsi`/surface-baseline/contract change, no new
published version. Change confined to `tests/FS.GG.Governance.Cli.Tests/` and the publish workflow's
test invocation (plan.md → Constitution Check; FR-006).

**Tests**: This feature *is* a test-determinism fix — the "implementation" lives in test code. The
deliverable is the semantic test discipline itself (Principle I/V), so test edits are the work, not an
optional add-on.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1` / `US2` / `US3` per spec.md
- Tier annotation omitted throughout — every phase matches the Tier 2 overall classification

## Phase ordering & the US1 vs priority note

Phases run in sequence; tasks within a phase may run in parallel. Although **US1 and US2 are both P1**,
US1 (drop the gate filter) cannot land safely until the tests are deterministic — so the implementation
order is **US2 (determinism) → US3 (assertion hardening) → US1 (gate)**, with US3 (P2) slotted before
US1 because dropping the filter should expose the *final* assertion to CI, not an interim one. This
ordering is a dependency fact, not a re-prioritization.

---

## Phase 1: Setup & Baseline

**Purpose**: Confirm the root cause empirically and the Spectre API shape before changing code.

- [X] T001 [P] Reproduced via FSI against the real `blockedView`/`blockedPlain`: locally (and even under
  the `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 LC_ALL=C TERM=dumb` shell) the suite PASSES — maxLineLen
  equals the forced width at every width (200→138, 80→80, 40→40, 20→20, 10→10), with `Capabilities.Unicode`
  defaulting to `true` on this host. The overflow is specific to a CI host that infers `Unicode=false`
  (legacy console), which changes box-glyph/unbreakable-token measurement; the local invariant-locale
  shell does NOT flip that inference, so the canonical pre-fix red is the headless GitHub Actions run (#32).
- [X] T002 [P] Confirmed against Spectre.Console 0.57.1: `Profile.Capabilities.Unicode` is settable cleanly;
  `Profile.Capabilities.Legacy` is settable but marked **obsolete** (FS0044) — handled with a narrow,
  documented `#nowarn "44"` in `RenderSupport.fs` (the property is the only handle for the legacy-console
  capability, default `false`). `Ansi`/`ColorSystem` remain as used. Setting `Capabilities` requires the
  `Spectre.Console.Ansi` assembly, which is already in the test project's reference set.

**Checkpoint**: Root cause reproduced; the exact capability levers to pin are confirmed.

---

## Phase 2: User Story 2 - Width tests pass identically on every host (Priority: P1) 🎯 MVP precondition

**Goal**: Make the width suite's console a pure function of (content, width) by pinning every
wrap-affecting capability, so local and headless CI yield identical results (FR-001, FR-002; contract C2).

**Independent Test**: Run the WidthResilience tests locally and under the headless-like shell on the same
commit — identical pass across the full 200/80/40/20/10 matrix, no host branching/skip/`[<Ignore>]`.

- [X] T003 [US2] Extended `plainConsole` in `RenderSupport.fs` to a fully-pinned deterministic console:
  alongside `Ansi=No` / `ColorSystem=NoColors` / `AnsiConsoleOutput(sw)` / `Profile.Width=width` it now pins
  UTF-8 output encoding via a `Utf8StringWriter` (overrides `Encoding`), `Capabilities.Unicode <- true`, and
  `Capabilities.Legacy <- false` (the latter under the documented `#nowarn "44"`). No host/env branching
  (data-model.md "Deterministic test console"; research.md R2; contract C2).
- [X] T004 [US2] `WidthResilienceTests.fs` renders into the pinned `plainConsole` — kept the builder name, so
  no call-site change. The full width matrix `[200; 80; 40; 20; 10]` and the safe-default-width case are
  intact (FR-004).
- [X] T005 [US2] Determinism verified: Scenario 1 (local) and Scenario 2 (headless/invariant shell) both
  green — 6/6 across the full matrix, identical results (SC-001 local proxy, SC-002). Canonical SC-001 is
  the headless GitHub Actions run on the merge commit (T012).

**Checkpoint**: Width matrix is host-independent locally; precondition for dropping the gate filter met.

---

## Phase 3: User Story 3 - Assertions reflect the real wrapping contract (Priority: P2)

**Goal**: Assert the renderer's actual folding contract — a line MAY extend to an unbreakable token's
boundary when that token exceeds the forced width — while still rejecting runaway/corrupted layout
(FR-003, FR-007; contract C1). Hardens determinism against future host/token drift.

**Independent Test**: Feed content whose smallest unbreakable token exceeds the forced width (e.g. a
~17-char glob at width 10) and confirm the asserted invariant is the documented folding behavior — no
spurious failure, no silently weakened check.

- [X] T006 [US3] In `WidthResilienceTests.fs`, `longestUnbreakableToken` is DERIVED by walking the real
  `blockedView` tree (`viewStrings`: title, leaf labels/details, group titles, exit status) plus
  `blockedPlain`, tokenizing on whitespace, and taking the max length (= 15, `'block-on-ship'`). The fixed
  chrome the 2-column Rounded table line carries is added explicitly and named:
  `borderColumns = 3` (vertical rules for 2 columns) + `paddingColumns = 4` (default 1-left/1-right × 2) +
  `indentColumns = 0` (not nested) = `chromeColumns = 7`. The table shape is read from
  `RichRender.emitRich` (data-model.md "Folding-invariant assertion"; research.md R3).
- [X] T007 [US3] Replaced the per-line `Expect.isLessThanOrEqual line.Length width` with
  `line.Length <= max(width, longestUnbreakableToken + chromeColumns)`, keeping the `out.Length > 0` check
  and the 10/20 widths. At fit-widths (200/80/40) `15 + 7 = 22 < width`, so `bound` collapses to `width` —
  identical to the pre-fix check. No assertion weakened (FR-003, FR-007, SC-004; contract C1).
- [X] T008 [P] [US3] Negative control (Scenario 5): tightened the bound to `width - 1` → suite went **red**
  (4 of 6 failed), confirming the assertion still rejects overflow; reverted. Local proxy for SC-005.

**Checkpoint**: Assertion encodes the true folding contract and still catches genuine overflow.

---

## Phase 4: User Story 1 - The publish gate covers width resilience again (Priority: P1)

**Goal**: Restore full `Cli.Tests` coverage on the publish gate by removing the temporary exclusion, now
that the tests are deterministic (FR-005; contract C3). Depends on Phases 2–3 being green.

**Independent Test**: Inspect the publish workflow's `cli-tests` step — it runs the project with no
`FullyQualifiedName!~WidthResilience` filter — and a deliberately-broken width assertion fails the gate.

- [X] T009 [US1] Dropped `--filter "FullyQualifiedName!~WidthResilience"` from the `cli-tests` job's
  `Test (FS.GG.Governance.Cli)` step — it now runs
  `dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release --no-restore`,
  and the preceding comment now records the spec 091 fix (and that it closes #32) instead of describing the
  exclusion. Locked-restore step, `enforcement-smoke` job, and publish sequencing untouched (FR-005;
  research.md R4; contract C3).
- [X] T010 [US1] Gate scope verified: Scenario 3 (full `Cli.Tests` green locally with no filter — 66/66,
  incl. RichRender/DegradeToPlain/Tui, FR-007) and Scenario 4 (`grep` shows `WidthResilience` only in the
  historical comment, no exclusion filter — SC-003).
  **SC-005 evidence note**: SC-005 ("a deliberate regression turns the gate red") is satisfied by the
  combination of the T008 local negative-control proxy (assertion demonstrably goes red on overflow) and
  the structural Scenario 4 check (the gate now runs the un-filtered suite, so that red would block the
  gate). It is **not** demonstrated by a live red publish run — that would require a throwaway CI push and
  is out of scope for the gate-restoration task.

**Checkpoint**: The publish gate exercises the full suite including WidthResilience.

---

## Phase 5: Polish, Evidence & Housekeeping

**Purpose**: Honest evidence record, full quickstart validation, and the issue/board closure that the
spec counts as "done".

- [X] T011 [P] Full quickstart.md validation run: Scenario 1 (local 6/6), Scenario 2 (headless/invariant
  6/6), Scenario 3 (full suite 66/66), Scenario 4 (no exclusion filter — comment only), Scenario 5
  (negative control red then reverted), Scenario 6 (bound is `max(width, longestUnbreakableToken + chrome)`,
  derived, widths 10/20 retained). All pass / inspect clean.
- [X] T012 Evidence obligations (Principle V): the width suite renders the **real** `ReportView` projected
  from the genuine `Ship.rollup` decision (`RenderSupport.fs`) — no synthetic evidence; the fix makes the
  evidence honest cross-host, not weaker. **Principle IV (Elmish/MVU) is N/A** — pure render + assert, no
  stateful/I/O workflow. Canonical **SC-001** evidence = the headless GitHub Actions `cli-tests` run on the
  merge commit (the now-unfiltered gate exercises WidthResilience); it goes green where the excluded gate
  could not have. **SC-005** = the T008 local negative-control plus the now-unfiltered gate (T010 note),
  not a live red publish run. No new published version cut (FR-006).
- [X] T013 Closed issue **FS-GG/FS.GG.Governance#32** and moved its Coordination board item to **Done**,
  noting the approach (capability pinning + folding-contract assertion + filter removal) (FR-008, SC-006;
  research.md R5), via the `cross-repo-coordination` protocol.

**Checkpoint**: Coverage hole closed, gate restored, issue/board reflect completion.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup & Baseline)**: no dependencies — start immediately.
- **Phase 2 (US2)**: after Phase 1 (T002 confirms the API levers).
- **Phase 3 (US3)**: after Phase 2 — the assertion is exercised against the pinned console.
- **Phase 4 (US1)**: after Phases 2–3 — drop the filter only once the *final* deterministic tests are green
  locally, so CI inherits the finished state.
- **Phase 5 (Polish/Housekeeping)**: after Phase 4 — validation + closure last.

### Story dependencies (note on priorities)

- **US2 (P1)** is the precondition for everything; it has no story dependencies.
- **US3 (P2)** depends on US2 (asserts against the pinned console).
- **US1 (P1)** depends on US2 + US3 landing green. Both P1 stories ship together; US1's dependency on US2
  is why it is sequenced last among the P1/P2 work.

### Parallel opportunities

- T001 ‖ T002 (Phase 1 — independent baseline vs. API check).
- T008 ‖ T006/T007 only after the assertion edits exist — in practice run T008 right after T007.
- T011 is independent within Phase 5; T012/T013 follow once CI is green.

---

## Implementation strategy

### MVP

The minimal shippable increment is **US2 → US1**: pin the console capabilities (US2), then drop the gate
filter (US1). That alone restores deterministic full-suite coverage. **US3** is durability hardening that
should land in the **same PR** (the spec applies both levers by default) — even if pinning alone greens
the matrix, US3 makes the suite honest and stable against future host/token drift; it does not relax
coverage.

### Suggested single-PR flow

1. Phase 1 baseline (confirm root cause + API).
2. Phase 2 (US2) — pin the deterministic console; matrix green locally + headless-like.
3. Phase 3 (US3) — assert `max(width, longestToken)`; negative-control proves it still fails on overflow.
4. Phase 4 (US1) — drop the publish-gate filter; full suite green, no exclusion.
5. Phase 5 — quickstart validation, evidence record, close #32 + board → Done.

---

## Notes

- Hard constraints (do NOT violate to get green): keep widths 10 and 20 (FR-004/SC-004); no
  `[<Ignore>]`, host-conditional skip, or environment branching (SC-002/C2); no `src/` product change,
  no `.fsi`/surface-baseline/contract change, no new published version (FR-006); other Spectre tests must
  stay green (FR-007).
- Files in play: `tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs`,
  `tests/FS.GG.Governance.Cli.Tests/WidthResilienceTests.fs`, `.github/workflows/publish.yml`, plus
  issue/board housekeeping.
- Commit after each phase or logical group; the negative-control break in T008 must be reverted before commit.
