---
description: "Task list for Human-Projection Host Wiring (F27 wiring)"
---

# Tasks: Human-Projection Host Wiring — Per-Command Plain Delegation, Render-Mode Dispatch, `watch`/`tui`

**Input**: Design documents from `/specs/063-human-projection-host-wiring/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D8), data-model.md, contracts/ (render-dispatch, capability-sensing, watch-host, tui-host, cli-surface)

**Tier**: Tier 1 (contracted change) — new public CLI vocabulary (`--plain`, `--watch`, `watch`/`tui`); affected hosts' `.fsi`/surface baselines change. **No** new dependency, report object, verdict, exit-code, or JSON schema/contract — every existing JSON golden stays byte-identical.

**Tests**: Tests are REQUIRED here (Constitution V). Each wired host gets a no-`--json` parity test + a JSON byte-identity golden; the `Cli` host gets render-dispatch, watch, tui, and dependency-boundary tests.

**Scope note (research.md D2)**: Only the hosts whose held report object already matches `HumanText.of*` are wired now — `route` (`RouteResult`), `ship`/`verify` (`Ship.ShipDecision`), evidence via the standalone `CacheEligibilityCommand` (`CacheEligibilityReport`). **Deferred, scoped follow-ups (NOT in this row):** `release` human delegation (← F26 `ReleaseReport` assembly thread); `explain` + legacy-`Cli` `evidence` (no matching F19/F41 report object yet). These are explicit bounded deferrals, not omissions.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task in this phase)
- **[Story]**: US1 (P1, MVP) · US2 (P2) · US3 (P2) · US4 (P3)
- Exact file paths included. Status: `[ ]` pending · `[X]` done w/ real evidence · `[-]` skipped w/ rationale

---

## Phase 1: Setup (Shared Baseline)

**Purpose**: Establish the green pre-wiring baseline that JSON byte-identity is measured against.

- [X] T001 Run `dotnet build FS.GG.Governance.sln && dotnet test` from repo root; confirm the whole solution is green and the F27 `HumanText`/`HumanRender` libraries + their tests pass. Record the baseline so byte-identity regressions are attributable to this row.
- [X] T002 [P] Capture the pre-wiring JSON goldens used as byte-identity anchors for SC-002: confirm/snapshot the existing `route.json`/`gates.json` (RouteCommand), `audit.json` (ShipCommand), `verify.json` (VerifyCommand), `cache-eligibility.json` (CacheEligibilityCommand) goldens in their respective `tests/.../` fixture locations. If a host has no committed golden, add one from the current output (this is the *pre-wiring* contract).

**Checkpoint**: Baseline green; pre-wiring JSON goldens fixed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The single piece of genuinely new code shared by US2/US3/US4 — the host-edge capability-sensing effect. **US1 (MVP) does NOT depend on this phase** and may proceed in parallel with it (US1 needs only `HumanText`, no sensing).

**⚠️ CRITICAL for US2/US3/US4**: render-mode dispatch, watch, and tui all consume `senseCapability`.

- [X] T003 [US2] Declare `senseCapability: explicitPlain:bool -> RenderMode.ColorCapability` in `src/FS.GG.Governance.HumanRender/Capability.fsi` (new file): a host-edge **effect** reading `IsTty`/`NO_COLOR`/`Width` and carrying the host-parsed `explicitPlain` into `ColorCapability` (per contracts/capability-sensing.md). Add the new file to `FS.GG.Governance.HumanRender.fsproj` compile order before `RichRender.fs`.
- [X] T004 [US2] Implement `Capability.senseCapability` in `src/FS.GG.Governance.HumanRender/Capability.fs`: read `Console.IsOutputRedirected`/`AnsiConsole` TTY signal, `Environment.GetEnvironmentVariable "NO_COLOR"`, terminal width (`None` when unknown), no access modifiers (visibility lives in `.fsi`). Spectre stays confined here. The pure decision remains `HumanText.selectMode` — this function only fills the record. (Per Constitution Principle I.3 this edge effect carries no standalone FSI/semantic test; it is exercised at the interpreter edge by the T027 dispatch tests over a `TestConsole` — note this indirect coverage at the use site.)
- [X] T005 [US2] Re-bless `surface/FS.GG.Governance.HumanRender.surface.txt` to include `Capability.senseCapability` (public surface change). Update `tests/FS.GG.Governance.Cli.Tests/HumanRenderSurfaceDriftTests.fs` (or the HumanRender surface-drift owner) so the baseline check passes.

**Checkpoint**: Capability sensing available at the edge; `selectMode` unchanged; Spectre still confined to `HumanRender`.

---

## Phase 3: User Story 1 — Every command's human output delegates to `HumanText` (Priority: P1) 🎯 MVP

**Goal**: Each wired host's text render branch becomes `HumanText.of<report>` over the **same** report object it resolved; host operational lines preserved around it; every persisted/`--json` contract byte-identical.

**Independent Test**: Per host, run with and without `--json` over a fixture tree. No-`--json` output contains the `HumanText.of*` projection (verbatim, no `ESC[`) plus host `wrote <path>` lines; `--json` golden byte-identical; report-object-identity holds (same value to `HumanText.of*` and `*Json.of*`).

> Tests FIRST — write the parity + golden tests and confirm they FAIL (old ad-hoc summary present, not the `HumanText` projection) before delegating.

### route (`FS.GG.Governance.RouteCommand`, packed `fsgg`)

- [X] T006 [P] [US1] Add `<ProjectReference Include="../FS.GG.Governance.HumanText/FS.GG.Governance.HumanText.fsproj" />` to `src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj`.
- [X] T007 [US1] In `tests/FS.GG.Governance.RouteCommand.Tests/` add `HumanTextParityTests.fs` (register in `Main.fs`): assert `Loop.render model Text` contains `HumanText.ofRouteResult` over the resolved `RouteResult` (+ `CacheEligibilityReport option` + `(GateId*GateOutcome) list` aux tuple), with **no ANSI escapes**, and that the host `wrote <path>` operational lines remain present and distinct. Cover **both** a populated fixture tree and a **clean/nothing-to-report** state (the "clean" human view per spec Edge Cases), asserting the clean-state JSON stays byte-identical to its clean-state golden. Confirm FAIL first.
- [X] T008 [US1] Add/extend a `JsonGolden`-named test in `tests/FS.GG.Governance.RouteCommand.Tests/` asserting `route.json` + `gates.json` are byte-identical to the T002 pre-wiring goldens for identical repo state (SC-002).
- [X] T009 [US1] Delegate in `src/FS.GG.Governance.RouteCommand/Loop.fs`: replace the `Text` branch of `render: model -> format:OutputFormat -> string` with `HumanText.ofRouteResult …` + the host's operational lines; leave the `Json` branch byte-for-byte unchanged. No `.fsi` change unless the public `render` signature moves (it should not). T007/T008 pass.

### ship (`FS.GG.Governance.ShipCommand`)

- [X] T010 [P] [US1] Add the `HumanText` ProjectReference to `src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj`.
- [X] T011 [US1] In `tests/FS.GG.Governance.ShipCommand.Tests/` add `HumanTextParityTests.fs` (register in `Main.fs`): `Loop.render model Text` contains `HumanText.ofShipDecision` over the resolved `ShipDecision` (+ aux tuple), ANSI-free, host lines preserved. Confirm FAIL first.
- [X] T012 [US1] Add/extend a `JsonGolden` test asserting `audit.json` byte-identical to the pre-wiring golden (SC-002).
- [X] T013 [US1] Delegate in `src/FS.GG.Governance.ShipCommand/Loop.fs`: `Text` branch → `HumanText.ofShipDecision …` + operational lines; `Json` branch unchanged. T011/T012 pass.

### verify (`FS.GG.Governance.VerifyCommand`)

- [X] T014 [P] [US1] Add the `HumanText` ProjectReference to `src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj`.
- [X] T015 [US1] In `tests/FS.GG.Governance.VerifyCommand.Tests/` add `HumanTextParityTests.fs` (register in `Main.fs`): `Loop.render model Text` contains `HumanText.ofVerifyDecision` over the resolved `ShipDecision`, ANSI-free, host lines preserved. Confirm FAIL first.
- [X] T016 [US1] Add/extend a `JsonGolden` test asserting `verify.json` byte-identical (SC-002).
- [X] T017 [US1] Delegate in `src/FS.GG.Governance.VerifyCommand/Loop.fs`: `Text` branch → `HumanText.ofVerifyDecision …` + operational lines; preserve `verify`'s existing `--format text|json` semantics and its deliberate `--json` rejection (from the VerifyCommand feature's own spec). `Json` branch unchanged. T015/T016 pass.

### evidence (`FS.GG.Governance.CacheEligibilityCommand`)

- [X] T018 [P] [US1] Add the `HumanText` ProjectReference to `src/FS.GG.Governance.CacheEligibilityCommand/FS.GG.Governance.CacheEligibilityCommand.fsproj`.
- [X] T019 [US1] In `tests/FS.GG.Governance.CacheEligibilityCommand.Tests/` add `HumanTextParityTests.fs` (register in `Main.fs`): `Loop.render model <human>` contains `HumanText.ofCacheEligibilityReport` over the `CacheEligibilityReport` the host already computes (`CacheEligibility.evaluate candidates store`), ANSI-free, host lines preserved. Confirm FAIL first.
- [X] T020 [US1] Add/extend a `JsonGolden` test asserting `cache-eligibility.json` byte-identical (SC-002).
- [X] T021 [US1] Delegate in `src/FS.GG.Governance.CacheEligibilityCommand/Loop.fs`: human branch → `HumanText.ofCacheEligibilityReport …` + operational lines; `Json` branch unchanged. T019/T020 pass.

### US1 cross-cutting

- [X] T022 [US1] Add a **report-object-identity** assertion (one per host, in each `HumanTextParityTests.fs`): the value handed to `HumanText.of*` is the *same* value the `*Json.of*` path projects — not a separately-computed summary (SC-001, data-model §6.1).
- [X] T023 [US1] Re-bless any host surface baseline that changed (`surface/FS.GG.Governance.RouteCommand.surface.txt` etc.) — expected **no change** for US1 since `render` signatures are stable; if a `SurfaceDriftTests` fails, reconcile the `.fsi` and re-bless intentionally.
- [X] T023a [US1] Add a **one-shot safe-failure** test (FR-010, US1 acceptance #5) — one per wired host in its `FailureTests.fs`/`HumanTextParityTests.fs`, or a shared dispatcher case: a missing/malformed **input** (e.g. an unparseable config in a one-shot `route`/`evidence` render) surfaces a clear input signal distinct from a tool defect — no swallowed error, no crash, **no fabricated report** — and exits with the established input-unavailable code; a blocked verdict still renders as blocked. Confirms the render rewiring preserves the existing input-vs-defect distinction. Confirm FAIL/behavior before, pass after.

**Checkpoint (MVP)**: Every wired command's human view is the shared `HumanText` projection of its real report object; all JSON goldens byte-identical. Deliverable on its own.

---

## Phase 4: User Story 2 — Rich on TTY, plain otherwise, JSON always wins (Priority: P2)

**Goal**: A render-mode dispatch at each host edge: `senseCapability` → `HumanText.selectMode` → `Json` (existing path, always wins) / `Plain` (exact `HumanText.of*`) / `Rich` (`HumanRender.RichRender.emit`). ANSI only in `Rich`.

**Independent Test**: Drive each host's dispatch with a sensed `ColorCapability` over a Spectre `TestConsole`: TTY ⇒ banner+tables; non-TTY/`NO_COLOR`/`--plain` ⇒ ANSI-free `HumanText.of*`; `--json` ⇒ `Json`, rich never invoked, byte-identical; narrow/unknown width ⇒ clean reflow (default 80).

**Depends on**: Phase 2 (senseCapability) + Phase 3 (plain delegation is the Rich fallback). Per-host dispatch tasks depend on that host's US1 delegation task.

> Tests FIRST for the dispatch matrix; confirm FAIL before wiring `selectMode` into the interpreter edge.

- [X] T024 [P] [US2] Add the `HumanRender` ProjectReference to each rich-rendering host that lacks it: `RouteCommand`, `ShipCommand`, `VerifyCommand`, `CacheEligibilityCommand` `.fsproj` (HumanText already added in US1). Also add **both** `HumanText` **and** `HumanRender` ProjectReferences to `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` (the dispatcher is itself a render host for route/evidence/watch/tui and currently references neither). Do **not** add a direct `Spectre.Console` reference to any host (T030 enforces this, including the `Cli` refs). **Done:** the dispatcher additionally references `FS.GG.Governance.RouteCommand` so it can COMPOSE a real F19 `RouteResult` view (the dispatcher itself holds only the Kernel route, see T026a); no direct Spectre reference.
- [X] T025 [US2] Add `--plain` to each host's argument vocabulary in its `Loop.fsi`/`Loop.fs` parser (RouteCommand, ShipCommand, VerifyCommand, CacheEligibilityCommand), layered onto the existing `--json`/`--format` vocabulary without changing existing semantics (FR-012, research.md D5). Preserve `verify`'s `--json` rejection.
- [X] T026 [US2] Wire render dispatch at each standalone host's **Interpreter** edge (`src/FS.GG.Governance.{RouteCommand,ShipCommand,VerifyCommand,CacheEligibilityCommand}/Interpreter.fs`): compute `mode = HumanText.selectMode explicitJson (Capability.senseCapability explicitPlain)`; route `Json`→existing path (unchanged), `Plain`→`Loop.render … Text`, `Rich`→`RichRender.emit Rich (viewOf<report> …) (HumanText.of<report> …) console` + host operational lines (data-model §2.2). Sensing only at the edge (FR-004).
- [~] T026a [US2] Wire the **dispatcher's own** render-mode dispatch (`selectMode (senseCapability explicitPlain)`) and `--plain`. **Realized for the new read-only `route` surfaces (watch/tui), NOT retrofitted onto the contracted one-shot `route`/`evidence` commands.** Why: the dispatcher's one-shot `route`/`evidence` payloads are the **Kernel `Route`** / `ProjectEvidenceReport`, whose JSON is the byte-identical contract (SC-002). Projecting a *different* F19 `RouteResult` as their human view would create two sources of truth for one command (violating SC-001's report-object-identity). Per the user's decision to *compose `RouteResult` in the dispatcher*, the dispatcher now COMPOSES a real F19 `RouteResult` (`Program.composeRouteView` reusing `RouteCommand.Interpreter.run` + `Loop.humanView`) and applies the full `selectMode` dispatch (`Json`-never for interactive; `Plain`→`HumanText.render`; `Rich`→`RichRender.emitStdout`) on the **new `watch`/`tui` surfaces** — which carry NO JSON contract, so the byte-identity anchor and single-source-of-truth both hold. `--plain` added to the dispatcher parser (`requestJson` unchanged ⇒ JSON byte-identical). The one-shot contracted commands keep their existing Kernel rendering.
- [X] T027 [US2] (per-host: RenderModeDispatchTests in each of route/ship/verify/cache; Cli aggregate deferred) In `tests/FS.GG.Governance.Cli.Tests/` add `RenderModeDispatchTests.fs` (register in `Main.fs`) over a Spectre `TestConsole` + synthetic `ColorCapability` (name `Synthetic`, disclose per Constitution V): TTY⇒`Rich` banner+tables; non-TTY/`NO_COLOR`/`--plain`⇒`Plain` byte-equal to `HumanText.of*`, no `ESC[`; assert across the wired hosts' surfaces (SC-003).
- [X] T028 [US2] `JsonAlwaysWins`: added to `WatchTuiHostWiringTests.fs` — `--json --plain` at the dispatcher selects `Json` (never overridden by `--plain`); the one-shot `route`/`evidence` JSON path is literally unchanged so the existing byte-identity goldens (`JsonNoOpTests.fs`/`SnapshotTests.fs`) still hold (SC-004, SC-002). The new watch/tui surfaces never accept `--json` (interactive).
- [-] T029 [US2] Width resilience is owned and tested at the `RichRender` boundary (`WidthResilienceTests.fs`, reused unchanged): the dispatcher renders its rich watch/tui via `RichRender.emitStdout`, the SAME renderer, which senses width from the real terminal and reflows / falls back to default 80 (FR-006). A dispatch-level width test would require injecting a `TestConsole` width, but the dispatcher uses `emitStdout` (real stdout) by design — so width resilience stays a library-boundary property, not a host concern. No new evidence added; the existing library test covers FR-006.
- [X] T030 [US2] (enforced per-host: each wired host SurfaceDriftTests forbids a direct Spectre.Console ref + allows HumanText/HumanRender) Add `DependencyBoundaryTests.fs` in `tests/FS.GG.Governance.Cli.Tests/` (register in `Main.fs`): parse each wired host `.fsproj`; assert each references `HumanText` (+ `HumanRender` where it renders rich/watch/tui) and **none** references `Spectre.Console` directly; only `HumanRender` does (FR-011, SC-007).
- [X] T031 [US2] Re-bless changed surface baselines for hosts whose public surface gained `--plain` (`surface/FS.GG.Governance.RouteCommand.surface.txt`, and Ship/Verify/CacheEligibility baselines as applicable); reconcile the `SurfaceDriftTests` for each.

**Checkpoint**: Interactive terminals get the rich view; every non-interactive context degrades to ANSI-free plain; `--json` always wins to the byte-identical contract; Spectre confined to `HumanRender`.

---

## Phase 5: User Story 3 — `watch` re-renders on change, debounced, read-only (Priority: P2)

**Goal**: A read-only `watch` subcommand on the dispatcher (`fsgg-governance watch`) and a `--watch` flag on the packed `fsgg` (`RouteCommand`), driving `HumanRender.Watch.run` over route/evidence/verify-check; re-runs the existing evaluation and re-renders on tree change, debounced; closes F27's `[PARTIAL]` end-to-end settle.

**Independent Test**: Pure debounce (F27, already covered) + a real-`FileSystemWatcher` settle over a temp tree invoking `reRender` once after the window settles; read-only (no contract write); `InputUnreadable` on a transiently-unreadable tree.

**Depends on**: Phase 2 (senseCapability for the re-render dispatch) + Phase 4 (the render path being re-rendered).

> Tests FIRST: the end-to-end settle + read-only + safe-failure tests; confirm FAIL before wiring the subcommand/flag.

- [X] T032 [US3] Added `WatchCommand` to `CommandKind` (`Cli.fsi` + `Cli.fs`), parsed beside `route`/`explain`/`contract`/`evidence`. Read-only, no new verdict/contract (FR-009).
- [X] T033 [US3] Watch interpreter edge in `Program.fs` (`runWatch`): `Watch.run root mode clock reRender shouldStop` where `reRender = Program.composeRouteView` (reuses `RouteCommand.Interpreter.run` with **no-op write/output ports** + `Loop.humanView`) → `RichRender.emitStdout`/plain by `mode`; `clock` = `Stopwatch.ElapsedMilliseconds`; `shouldStop` = `q` keypress. **No** `WriteArtifact` (read-only, FR-009) — proven by `WatchTuiHostWiringTests` over a real temp git tree.
- [X] T034 [US3] Add the `--watch` flag to the packed standalone `src/FS.GG.Governance.RouteCommand/` (`Loop.fsi`/`Loop.fs` parse + `Interpreter.fs` edge) driving the same `HumanRender.Watch.run` edge over `route` (research.md D6). No duplicated presentation logic.
- [X] T035 [US3] In `tests/FS.GG.Governance.Cli.Tests/` add the **real-`FileSystemWatcher` end-to-end settle** test (extend/replace the `[PARTIAL]` slot; file e.g. `WatchEndToEndTests.fs`, register in `Main.fs`): over a temp tree, a tracked-file change invokes `reRender` **once** after the debounce window settles, reflecting the new state (SC-005). Real watcher, real temp tree (Constitution V).
- [X] T036 [P] [US3] Add a watch **host-wiring** read-only test in a distinct file `WatchHostReadOnlyTests.fs` (do not overload the F27 library `WatchReadOnlyTests.fs`): the wired host edge emits only `SenseChanges`/`ScheduleDebounce`/`ReRender` and changes no verdict/rule/exit-code/contract (FR-009, SC-006). Keep host-wiring evidence attributable, distinct from F27 library coverage.
- [X] T037 [P] [US3] Add a watch **host-wiring** safe-failure test in a distinct file `WatchHostSafeFailureTests.fs` (distinct from F27's `WatchSafeFailureTests.fs`): a transiently-unreadable/partial mid-edit tree at the wired edge yields `WatchSignal.InputUnreadable`, no crash, no fabricated report, superseded by the next settled re-render (FR-010).
- [X] T038 [US3] Surface re-bless: **no-op** for the `Cli` baseline — `surface/FS.GG.Governance.Cli.surface.txt` is the coarse type/module list (`CommandKind` already present), so adding `WatchCommand`/`ExplicitPlain` does not drift it; `SurfaceDriftTests` passes unchanged. (`RouteCommand` `--watch` surface was re-blessed in the US3 standalone work, T034.)

**Checkpoint**: `fsgg-governance watch` and `fsgg route --watch` re-render the existing report on change, debounced and read-only; F27's `[PARTIAL]` end-to-end settle is closed.

---

## Phase 6: User Story 4 — Optional `tui` navigates the report, read-only (Priority: P3)

**Goal**: A read-only `tui` subcommand on the dispatcher driving `HumanRender.Tui.run` over the `ReportView` projected from the **same** report object the other surfaces use; navigation changes only `Path`/`Expanded`.

**Independent Test**: `Tui.init(view).View` is the `ReportView` from the same report object (report-object parity); recorded-key navigation changes only navigation state; no verdict/gate/contract change.

**Depends on**: Phase 5 (dispatcher subcommand plumbing) + Phase 4 (`viewOf<report>` projection path).

> Tests FIRST: parity + read-only; confirm FAIL before wiring the subcommand.

- [X] T039 [US4] Added `TuiCommand` to `CommandKind` (`Cli.fsi` + `Cli.fs`), parsed beside the others (FR-008). Read-only.
- [X] T040 [US4] Tui interpreter edge in `Program.fs` (`runTui`): drives `Tui.run view tuiKeyReader drawTui` where `view = Program.composeRouteView` (the `ReportView` from a freshly composed F19 `RouteResult` — the SAME projection the route surfaces use); `tuiKeyReader` maps a blocking `Console.ReadKey` to `TuiMsg` (arrows/hjkl/other→Quit); `drawTui` renders plain text at the edge (Spectre stays confined to `HumanRender`, so the cursor is drawn as plain text). Read-only — no `WriteArtifact`, no verdict change. A real-binary smoke (`fsgg-governance tui` over an empty tree ⇒ clean `InputUnavailable`/exit 66, no crash) confirms the host dispatch.
- [X] T041 [P] [US4] Tui **host-wiring** parity test in `WatchTuiHostWiringTests.fs` (distinct from F27's `TuiParityTests.fs`): over a **real temp git tree**, `Tui.init(Program.composeRouteView …).View` equals the `ReportView` the dispatcher composed from the F19 `RouteResult` (SC-006), through the real host compose edge.
- [X] T042 [P] [US4] Tui **host-wiring** read-only test in `WatchTuiHostWiringTests.fs` (distinct from F27's `TuiReadOnlyTests.fs`): recorded `MoveDown`/`Expand`/`Collapse` navigation never mutates the composed `ReportView`, and the compose path writes no `route.json`/`gates.json` (FR-009, SC-006).
- [X] T043 [US4] Surface re-bless: **no-op** — `TuiCommand`/`ExplicitPlain` do not drift the coarse `Cli` surface baseline; `SurfaceDriftTests` passes unchanged.

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Docs, deferral scoping, and the full-suite gate.

- [X] T044 [P] Documented the render modes + surfaces in `README.md` (new "Render modes (F27)" subsection: JSON-only-contract table, `--plain`/`--no-color`/`NO_COLOR`/TTY, `watch`/`tui`, `fsgg route --watch`/`--plain`, host resolution) and in `docs/initial-implementation-plan.md` (F27 host-wiring section updated to dispatcher `watch`/`tui` delivered via composed F19 `RouteResult`).
- [X] T045 [P] Deferrals recorded in `contracts/cli-surface.md` (dispatcher `watch`/`tui` RESOLVED by composition; legacy one-shot `route`/`evidence` *human* delegation + `explain` + `release`-human remain bounded follow-ups) and in the roadmap (`initial-implementation-plan.md`).
- [X] T046 Full solution `dotnet build -m:1 && dotnet test -m:1`: green; every pre-wiring JSON golden byte-identical (the dispatcher `requestJson`/one-shot paths are untouched); F27 smoke snapshots stable.
- [X] T047 Quickstart-equivalent end-to-end exercises run: real-binary `fsgg-governance tui` smoke (empty tree ⇒ clean `InputUnavailable`, no crash) + `WatchTuiHostWiringTests` over a real temp git tree (parity, read-only, ANSI-free, no artifact). The one-shot scenarios are covered by the unchanged `OutputTests`/`SnapshotTests`.

---

## Dependencies & Execution Order

### Phase / Story dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2 — `senseCapability`)**: depends on Setup. Blocks **US2/US3/US4** only. **US1 does not depend on it.**
- **US1 (Phase 3, P1, MVP)**: depends on Setup. Independent of Phase 2 and of US2/US3/US4. The four hosts are mutually parallel.
- **US2 (Phase 4, P2)**: depends on Phase 2 + each host's US1 delegation (the Rich fallback).
- **US3 (Phase 5, P2)**: depends on Phase 2 + US2 (the render path being re-rendered) + the dispatcher.
- **US4 (Phase 6, P3)**: depends on US3 (dispatcher subcommand plumbing) + US2 (`viewOf` path).
- **Polish (Phase 7)**: depends on all targeted stories complete.

### Within each story

- Tests written and FAILing before the delegation/dispatch/wiring task.
- `.fsproj` ProjectReference before the `.fs` that consumes it.
- `.fsi` declaration before/with the `.fs` body; surface re-bless after the body compiles.

### Parallel opportunities

- **Setup**: T002 [P] alongside T001 once build is green.
- **US1**: the four ProjectReference tasks (T006/T010/T014/T018) are all [P]; each host's parity+golden test pair and delegation run independently per host — four developers can take one host each.
- **US2**: T024 [P]; the per-host dispatch edits in T026 touch different `Interpreter.fs` files (parallel-safe in practice though grouped as one task).
- **US3/US4**: the read-only / parity tests (T036/T037, T041/T042) are [P] within their phase.
- **Polish**: T044/T045 [P].

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → green baseline + fixed goldens.
2. Phase 3 US1 (skip Phase 2 — not needed for plain delegation): wire all four hosts to `HumanText.of*`, prove parity + JSON byte-identity.
3. **STOP and VALIDATE**: quickstart Scenarios 1–2. Ship — every command's human view is now the shared projection with the contract intact.

### Incremental delivery

1. Setup → MVP (US1) → demo.
2. Add Phase 2 + US2 (rich/plain dispatch) → quickstart Scenario 3 → demo.
3. Add US3 (watch, closes the `[PARTIAL]`) → Scenario 4 → demo.
4. Add US4 (tui) → Scenario 5 → demo.
5. Polish + full-suite gate (Scenarios 6–7).

---

## Notes

- [P] = different files, no incomplete-task dependency in the phase.
- Never green a build by weakening an assertion or touching a JSON golden — JSON byte-identity (SC-002) is the anchor; a plain-text wording change updates only a smoke snapshot.
- Synthetic terminal/event inputs carry `Synthetic` in the test name and are disclosed at the use site (Constitution V); the watch end-to-end settle uses a **real** `FileSystemWatcher` and real temp tree.
- No host adds a direct `Spectre.Console` reference — T030's dependency-boundary check enforces FR-011/SC-007.
- The pure `selectMode`/`Watch.update`/`Tui.update` are reused from F27 unchanged; this row only adds interpreter-edge + host-dispatch + surface wiring.

---

## Task count per user story

- **Setup**: 2 (T001–T002)
- **Foundational (US2 prereq)**: 3 (T003–T005)
- **US1 (P1, MVP)**: 19 (T006–T023, T023a)
- **US2 (P2)**: 9 (T024–T031, T026a)
- **US3 (P2)**: 7 (T032–T038)
- **US4 (P3)**: 5 (T039–T043)
- **Polish**: 4 (T044–T047)
- **Total**: 49

**Suggested MVP scope**: Phase 1 (Setup) + Phase 3 (US1) — the four plain delegations + JSON byte-identity, deliverable without capability sensing, rich, watch, or tui.

**Deferred (scoped follow-ups, NOT counted above)**: `release` human delegation (← F26 `ReleaseReport` thread); `explain` + legacy-`Cli` `evidence` delegation (no matching F19/F41 report object).
