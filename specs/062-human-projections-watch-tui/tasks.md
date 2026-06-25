# Tasks: Human Projections — Plain Text, Spectre.Console, Watch, and Optional TUI Over the Immutable Report Objects (F27)

> **Implementation status (2026-06-25).** This row landed the two new **libraries** that F27 is built
> on — both fully built and green with real-core test evidence (Principle V), integrated into the
> solution:
>
> - **`FS.GG.Governance.HumanText`** (pure P1 foundation): `RenderMode` + `ReportView` + `HumanText`,
>   one ANSI-free deterministic projection per report object, rendered from a shared `ReportView`
>   (one structure behind every human surface). Tests: `FS.GG.Governance.HumanText.Tests` (36 tests
>   — ANSI-free, determinism, blocked-verdict + FR-012 input signal, smoke snapshots, report-object
>   parity vs `*Json`, dependency boundary, non-contractual-text, surface drift). Committed surface
>   baseline + smoke snapshots.
> - **`FS.GG.Governance.HumanRender`** (P2/P3, sole Spectre owner): `RichRender.emit` (rich banner +
>   width-resilient tables, degrade-to-plain verbatim, JSON no-op), the pure `Watch` debounce MVU,
>   and the pure `Tui` navigation MVU, each with an interpreter-edge `run`. Tests live in the extended
>   `FS.GG.Governance.Cli.Tests` (rich/degrade/width/json-noop, watch debounce/read-only/safe-failure,
>   TUI parity/read-only, HumanRender surface drift). Spectre.Console pinned centrally and referenced
>   ONLY by HumanRender (boundary test enforces FR-013/SC-007).
>
> **`[DEFERRED]` tasks** — the **host wiring** (T021–T026 per-command plain delegation; T032/T033 Cli
> capability-sense + render-mode dispatch; T039/T043 `watch`/`tui` subcommand + `--watch`/`--plain`
> flag wiring) is **not** landed. Reason: each host's `renderText`/`renderJson` is a host-specific
> console **summary** (changed-path counts, "wrote …" lines) distinct from the persisted `*.json`
> contract, and the legacy multi-subcommand `Cli` is built on the older Kernel/Host `Route`/
> `Explanation` types — not the F19–F26 report objects. Replacing those summaries and threading the
> new subcommands through cleanly is an invasive, per-host change that would cascade into the existing
> host-test suites; it is split out so this row keeps the full 60-project suite byte-identical and
> green. The libraries above are exactly the surface that wiring will consume. `[PARTIAL]`: T038 (the
> `Watch.run` FileSystemWatcher edge is implemented but lacks an end-to-end settle test), T049 (docs),
> T050 (the new + adjacent projects are green; a full-solution `dotnet test` sweep is the remaining
> gate).

> **Re-synced to `plan.md` (2026-06-25).** This file was first generated before `/speckit-plan` ran. The plan is
> now populated (with `research.md`, `data-model.md`, `quickstart.md`, and `contracts/`), and it **refines the
> earlier structural assumptions** (plan.md "tasks.md re-sync" note). The retargets applied here:
>
> 1. **Rich/watch/tui live in a dedicated `FS.GG.Governance.HumanRender` library — the sole Spectre owner — not in
>    `FS.GG.Governance.Cli`.** The plan introduces `HumanRender` as the one CLI-host presentation library that owns
>    the Spectre.Console dependency and the watch/tui MVU (plan D2/D3). The Spectre reference and every rich/watch/
>    tui task now target `HumanRender`, not `Cli`.
> 2. **Spectre.Console is pinned centrally in `Directory.Packages.props`** with a NEED/SCOPE/OWNER comment (the
>    YamlDotNet precedent), then PackageReference'd **only** by `HumanRender` (plan D3, FR-005/FR-013/SC-007).
> 3. **`HumanText` carries a third pure module, `ReportView`** (the navigable view-model behind both the rich
>    tables and the TUI), alongside `RenderMode` and `HumanText` (data-model §3, contracts/report-view.md). Compile
>    order: `RenderMode` → `HumanText` → `ReportView`.
> 4. **`FS.GG.Governance.Cli.Tests` already exists** — it is **extended** in place for the `HumanRender` edge
>    (rich/degrade/width, watch, tui), not created. It gains project references to `HumanText` + `HumanRender`.
> 5. **Tier 1 confirmed.** New public projection API (`HumanText`, `HumanRender`), a new dependency, and new public
>    CLI command/flag vocabulary (`--plain`/`--watch`/`fsgg watch`/`fsgg tui`) each force Tier 1: curated `.fsi`,
>    committed surface baselines for **both** new libraries, the dependency justification, real test evidence, and
>    docs. No report-object/verdict/exit-code/JSON-schema change — every JSON golden stays byte-identical.
>
> The P1 plain-text tasks (`HumanText` + per-command delegation) are unchanged in substance. No task is marked
> `[X]` until it has real, exercised evidence (Constitution V; the vertical-slice rule).

**Input**: `spec.md` (US1–US4, FR-001..FR-013, SC-001..SC-008, edge cases), `plan.md`, `data-model.md`,
`contracts/{render-mode,human-text-projection,report-view,rich-render,watch-mvu,tui-mvu,cli-surface}.md`,
`quickstart.md`.

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]` may
run in parallel (different files, no incomplete-task dependency).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`/`US4`; setup / foundational / polish tasks carry no story tag
- Discipline (Constitution I/II): for every **new** module draft its `.fsi` and a compiling stub before the real
  `.fs` body; semantic tests call the loaded public surface, never internals. This feature is **Tier 1** — both new
  libraries ship curated `.fsi` + committed surface baselines.

**Design note — one source of truth, two projections (FR-001, FR-003, SC-001, SC-002).** Every `fsgg` command
already resolves to an **immutable, presentation-free report object** (`Route.RouteResult`,
`RouteExplain.RouteExplanation`, `Ship.ShipDecision`, `ReleaseReport.ReleaseReport`,
`CacheEligibility.CacheEligibilityReport`) projected to deterministic JSON by the `*Json` libraries. F27 adds a
**second projection** of those **same** objects — `HumanText` (pure plain text) plus a Spectre rich/TUI view in
`HumanRender` — and **changes neither the report objects nor the JSON**. Each command host's `Loop.render`
plain-text path is **replaced by a delegation to `HumanText`**, so the human view and the JSON view derive from one
report value; the JSON path is **untouched** and every JSON golden stays **byte-identical** (FR-010, SC-002).

**Design note — presentation lives in one CLI-host library (Constitution IV, FR-013, SC-007).** `HumanText.*`
(RenderMode / HumanText / ReportView) is pure / total / no-I/O with **no** presentation dependency. The Spectre
rich renderer, the TTY/`NO_COLOR`/width sensing, the watch file-sensing + debounce, and the TUI key loop all live
in **`FS.GG.Governance.HumanRender`** — the sole owner of Spectre. The render-mode decision (`selectMode`) and the
report→view projections are **pure**; only capability sensing, file-change sensing, terminal writes, and the TUI
loop are `Effect`s executed at the `HumanRender` interpreter edge.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the pure `HumanText` projection project, the `HumanRender` presentation project (sole Spectre
owner), and the new test project; pin Spectre centrally; extend `Cli.Tests`; wire the solution. Mirror the
`RouteJson`/`VerifyJson` pure-projection precedent and the YamlDotNet central-pin precedent.

- [X] T001 [P] Create `src/FS.GG.Governance.HumanText/FS.GG.Governance.HumanText.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`). ProjectReferences (read-only, the report-object cores the
  projections render): `FS.GG.Governance.Route` (`RouteResult`), `FS.GG.Governance.RouteExplain`
  (`RouteExplanation`), `FS.GG.Governance.Ship` (`ShipDecision`), `FS.GG.Governance.ReleaseReport`
  (`ReleaseReport`), `FS.GG.Governance.CacheEligibility` (`CacheEligibilityReport`), plus `FS.GG.Governance.Gates`
  (`GateId`) + `FS.GG.Governance.GateRun` (`GateOutcome`) for the auxiliary inputs the `RouteJson`/`VerifyJson`
  signatures carry, and `FS.GG.Governance.Findings`/`FS.GG.Governance.Config` as the projections need. **No
  presentation package reference.** Compile order: `RenderMode.fsi` → `RenderMode.fs` → `HumanText.fsi` →
  `HumanText.fs` → `ReportView.fsi` → `ReportView.fs`. Mirror `FS.GG.Governance.RouteJson.fsproj`.
- [X] T002 [P] Pin **Spectre.Console** centrally in `Directory.Packages.props` (a single `<PackageVersion
  Include="Spectre.Console" Version="X.Y.Z"/>`) with a `NEED / SCOPE / OWNER` comment block mirroring the
  YamlDotNet entry: NEED = color/table/width-resilient rich rendering + a minimal read-only TUI
  (FR-005/FR-006/FR-009); SCOPE = PackageReference'd **only** by `FS.GG.Governance.HumanRender`; OWNER = CLI host
  maintainer. **Choose a concrete pinned version** when authoring this task — pin the latest stable Spectre.Console
  release that targets `net10.0` (record the exact `X.Y.Z`, not a range; no floating version). No
  `PackageReference` is added here — only the version pin. (Plan D3; FR-005, FR-013, SC-007.)
- [X] T003 [P] Create `src/FS.GG.Governance.HumanRender/FS.GG.Governance.HumanRender.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=false` — a host-edge library). `PackageReference` **Spectre.Console**
  (the only project in the repo that may). ProjectReferences: `FS.GG.Governance.HumanText` (for `RenderMode`,
  `ReportView`, the plain degrade text) and the report cores it re-runs/reads as needed. Compile order:
  `RichRender.fsi` → `RichRender.fs` → `Watch.fsi` → `Watch.fs` → `Tui.fsi` → `Tui.fs`. This is the **sole**
  Spectre owner (FR-005, FR-013, SC-007).
- [X] T004 [P] Create the new test project `tests/FS.GG.Governance.HumanText.Tests` (Expecto + Expecto.FsCheck/
  FsCheck, `Main.fs` entry — mirror `tests/FS.GG.Governance.RouteJson.Tests`). References `HumanText` and every
  report-object core it constructs fixtures from. Owns the pure-projection tests (ANSI-free, determinism,
  blocked-verdict, parity, smoke snapshots, ReportView parity, dependency-boundary, non-contractual-text).
- [X] T005 [P] Extend the existing `tests/FS.GG.Governance.Cli.Tests` (it already exists) for the `HumanRender`
  edge: add ProjectReferences to `FS.GG.Governance.HumanText` and `FS.GG.Governance.HumanRender`. This project
  owns the rich-over-`TestConsole` / degrade-to-plain / width-resilience tests, the watch debounce/read-only/
  safe-failure tests, and the TUI parity/read-only tests.
- [X] T006 Add the two new `src` projects and the new test project to `FS.GG.Governance.sln` (mirror an existing
  projection's solution-folder entries) and confirm `dotnet build FS.GG.Governance.sln` resolves the graph with
  empty/stub modules and **no reference cycle**: `HumanText` → report cores only; `HumanRender` → `HumanText` +
  Spectre; `Cli` → `HumanText` + `HumanRender` (no direct Spectre ref). Depends on T001–T005.
- [X] T007 [P] Capture the **pre-F27 JSON goldens** as the byte-identity baseline: confirm/record the committed
  `route.json` / `ship.json` / `verify.json` / `release.json` / evidence goldens that SC-002 holds byte-identical,
  so any later accidental JSON drift is caught. (No production change — a baseline checkpoint.)

**Checkpoint**: Solution restores and builds with stub modules; Spectre is pinned centrally and referenced **only**
by `HumanRender`; `Cli.Tests` sees both new libraries; the JSON goldens are recorded as the byte-identity baseline.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the **render-mode model**, the **`HumanText` projection surface**, and the shared **`ReportView`
view-model** (`.fsi` + compiling stubs over the real report objects), plus the surface-drift harness and the shared
real-report fixtures. **No story body may begin until the whole graph compiles and the projection + view surfaces
are exercisable.**

**⚠️ CRITICAL**: Blocks US1–US4.

- [X] T008 [P] Author `src/FS.GG.Governance.HumanText/RenderMode.fsi` + real `RenderMode.fs` (contracts/
  render-mode.md): `RenderMode = Json | Plain | Rich`; `ColorCapability = { IsTty: bool; NoColorEnv: bool;
  ExplicitPlain: bool; Width: int option }`; `selectMode: explicitJson:bool -> ColorCapability -> RenderMode` —
  **`Json` always wins**; else `Rich` iff `IsTty && not NoColorEnv && not ExplicitPlain`; else `Plain` (FR-004).
  Pure, total, no-I/O (the actual TTY/`NO_COLOR`/width sensing is a `HumanRender` `Effect`, T032). `Width` is **not**
  part of the mode decision — consumed at render time as a safe default. Field/case order per the contract.
- [X] T009 [P] Author `src/FS.GG.Governance.HumanText/HumanText.fsi` + a compiling stub `HumanText.fs`
  (contracts/human-text-projection.md, data-model §2): one pure, deterministic, ANSI-free projection **per report
  object**, all stubbed to `""`, each signature **mirroring the input tuple of the matching `*Json.of*`**
  (report-object parity):
  - `ofRouteResult: RouteResult -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string`
    *(mirrors `RouteJson.ofRouteResult`; `route`)*
  - `ofRouteExplanation: RouteExplanation -> string` *(`explain` — `RouteExplain.RouteExplanation`)*
  - `ofShipDecision: ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string`
    *(`Ship.ShipDecision`; `ship`)*
  - `ofVerifyDecision: ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string`
    *(`verify` reuses the same `Ship.ShipDecision`, mirroring `VerifyJson.ofVerifyDecision`; may share
    `ofShipDecision`'s body, kept as a named entry for command parity)*
  - `ofReleaseReport: ReleaseReport -> string` *(`ReleaseReport.ReleaseReport`; `release`)*
  - `ofCacheEligibilityReport: CacheEligibilityReport -> string` *(`evidence`; from `Project.evidenceReport`)*

  No presentation dependency. Depends on T008.
- [X] T010 [P] Author `src/FS.GG.Governance.HumanText/ReportView.fsi` + a compiling stub `ReportView.fs`
  (contracts/report-view.md, data-model §3): the navigable view-model both the rich tables and the TUI render.
  `ReportNode = Leaf of label:string * detail:string option | Group of title:string * children:ReportNode list`;
  `ReportView = { Title: string; ExitStatus: string; Sections: ReportNode list }`; one `viewOf*` per report object
  with the **same input tuples as the §2 `of*` functions** (`viewOfRouteResult`, `viewOfRouteExplanation`,
  `viewOfShipDecision`, `viewOfVerifyDecision`, `viewOfReleaseReport`, `viewOfCacheEligibilityReport`), all stubbed.
  Pure, no Spectre, no I/O — the single structure behind every human surface (parity, FR-001/FR-009). Depends on
  T009 (shares the report-core opens).
- [X] T011 Exercise every `.fsi` against its `.fs` before the real bodies (Constitution I): `dotnet build
  FS.GG.Governance.sln`, plus a smoke semantic test `tests/FS.GG.Governance.HumanText.Tests/SmokeTests.fs` that
  loads and calls `RenderMode.selectMode`, every `HumanText.of*`, and every `ReportView.viewOf*` stub over a
  minimal real report fixture. Depends on T008–T010.
- [X] T012 [P] Add `tests/FS.GG.Governance.HumanText.Tests/SurfaceDriftTests.fs` — load the project's public
  surface, compare to `surface/FS.GG.Governance.HumanText.surface.txt`, honor `BLESS_SURFACE=1` (mirror the
  existing surface-drift test). Baseline committed in Phase 7 once bodies stabilize. Depends on T011.
- [X] T013 [P] Add shared test support `tests/FS.GG.Governance.HumanText.Tests/Support.fs` — builders for **real**
  report objects in each shape the stories assert: a clean/empty report, a blocked report (selected gates + unmet
  preconditions + blockers), a warnings-only report, and a missing/malformed-**input** report (for the FR-012
  one-shot path), for route / ship / verify / release / evidence / explain. Reuse the **real** report-object
  constructors from the F18–F26 cores (never a hand-mocked summary). Depends on T011.

**Checkpoint**: The render-mode model is real; every `HumanText.of*` and `ReportView.viewOf*` is a compiling stub
over the real report types; the smoke test exercises the surface; the surface-drift harness and real-report
fixtures are in place — story work can begin.

---

## Phase 3: User Story 1 — Human plain-text view rendered from the same report object as JSON (Priority: P1) 🎯 MVP

**Goal**: Each command's human output becomes a **disciplined plain-text projection of the same report object** that
produces its JSON. `HumanText.of*` (a render of `ReportView`) emits the verdict, selected gates / unmet
preconditions, blockers, warnings, and exit status — deterministic, ANSI-free, smoke-snapshot-stable. Each command
host's `Loop.render` plain-text branch is **replaced by a delegation to `HumanText`**; the JSON branch is untouched
and every JSON golden stays byte-identical (FR-001/FR-002/FR-003, SC-001/SC-002/SC-003).

**Independent Test**: For each command, run it against a fixture working tree with and without `--json`. The
plain-text view reports the same verdict/blockers/exit status as the JSON, both derive from one report object, the
plain text contains **no** ANSI escapes, and it matches its committed smoke snapshot for identical repo state.

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T014 [P] [US1] `tests/FS.GG.Governance.HumanText.Tests/AnsiFreeTests.fs` — every `HumanText.of*` over every
  fixture (clean / blocked / warnings) contains **no** ANSI/CSI escape sequence (assert no `ESC[`), for route,
  ship, verify, release, evidence, explain (FR-002, SC-002; acceptance US1.1).
- [X] T015 [P] [US1] `tests/FS.GG.Governance.HumanText.Tests/DeterminismTests.fs` — each `of*` (and each
  `ReportView.viewOf*`) is byte-identical on repeated calls over identical input; FsCheck: no absolute path /
  wall-clock / username / environment leak in the rendered text or view labels (normalized paths, stable ordering)
  (FR-011, SC-003; acceptance US1.3).
- [X] T016 [P] [US1] `tests/FS.GG.Governance.HumanText.Tests/BlockedVerdictTests.fs` — for a **blocked** report,
  the rendered text states the blocking reason(s) and the process exit status explicitly, matching the report
  object's verdict and exit-code basis — never softened, never separately derived (FR-002; acceptance US1.4).
  **Also** (FR-012, non-watch one-shot path): over a report carrying a missing/malformed **input** signal (e.g. an
  unparseable config surfaced by a one-shot `route`/`evidence` render), the projection renders a clear input signal
  **distinct from a tool defect** — no swallowed error, no crash, **no fabricated report** — so the plain one-shot
  render honors safe-failure exactly as the watch path does (T036).
- [X] T017 [P] [US1] `tests/FS.GG.Governance.HumanText.Tests/SmokeSnapshotTests.fs` — a committed plain-text smoke
  snapshot per command (route/ship/verify/release/evidence/explain) for a fixed report fixture; rendering twice is
  identical and matches the snapshot; honor a `BLESS_SNAPSHOT=1` re-bless path (non-contractual but stable)
  (FR-011, SC-003; acceptance US1.3, edge "empty/clean report").
- [X] T018 [P] [US1] `tests/FS.GG.Governance.HumanText.Tests/ReportParityTests.fs` — **report-object parity**: from
  one report value, the `HumanText.of*` text and the corresponding `*Json.of*` JSON convey the same verdict,
  blockers, and exit status (assert the same facts surface in both projections); and the `ReportView.viewOf*` tree
  carries those same facts — the human view is not a separately-computed summary (FR-001, SC-001; acceptance
  US1.2).

### Implementation for User Story 1

- [X] T019 [US1] `src/FS.GG.Governance.HumanText/ReportView.fs` — replace the stubs with real `viewOf*` bodies:
  each builds the `{ Title; ExitStatus; Sections }` tree from the carried report object only (verdict header,
  selected gates / unmet preconditions, blockers, warnings, evidence/provenance references — stable ordering,
  normalized labels). Pure, deterministic. Depends on T010.
- [X] T020 [US1] `src/FS.GG.Governance.HumanText/HumanText.fs` — replace the stubs with real bodies, each rendering
  its `ReportView` (from T019) to ANSI-free plain text (verdict header, sections, exit-status line); no
  re-derivation. Pure, total, deterministic. Makes T014–T018 pass. Depends on T019.
- [ ] T021 **[DEFERRED]** [P] [US1] Delegate `fsgg route`'s human view to `HumanText.ofRouteResult`: in
  `src/FS.GG.Governance.RouteCommand/Loop.fs`, replace the inline plain-text `render` branch with a call over the
  **same** `RouteResult` (+ `CacheEligibilityReport option` + `(GateId*GateOutcome) list`) the JSON path projects;
  **leave the `Json` branch and the persisted `route.json` byte-identical** (add a `HumanText` project reference to
  `FS.GG.Governance.RouteCommand.fsproj`). (FR-001, FR-010, SC-002.)
- [ ] T022 **[DEFERRED]** [P] [US1] Delegate `fsgg ship`'s human view to `HumanText.ofShipDecision` (over the `Ship.ShipDecision`
  the command resolves) in `src/FS.GG.Governance.ShipCommand/Loop.fs` (same pattern, `ship.json` byte-identical).
- [ ] T023 **[DEFERRED]** [P] [US1] Delegate `fsgg verify`'s human view to `HumanText.ofVerifyDecision` (over the same
  `Ship.ShipDecision` that `VerifyJson.ofVerifyDecision` projects) in `src/FS.GG.Governance.VerifyCommand/Loop.fs`
  (`verify.json` byte-identical).
- [ ] T024 **[DEFERRED]** [P] [US1] Delegate `fsgg release`'s human view to `HumanText.ofReleaseReport` (over the
  `ReleaseReport.ReleaseReport`) in `src/FS.GG.Governance.ReleaseCommand/Loop.fs` (`release.json` byte-identical).
- [ ] T025 **[DEFERRED]** [P] [US1] Delegate `fsgg explain`'s and `fsgg evidence`'s human views to `HumanText.ofRouteExplanation`
  (over `RouteExplain.RouteExplanation`) / `HumanText.ofCacheEligibilityReport` (over the
  `CacheEligibility.CacheEligibilityReport` from `Project.evidenceReport`) at **both** dispatch sites: the
  multi-subcommand `src/FS.GG.Governance.Cli/Cli.fs` (the `explain`/`evidence` subcommands) **and** the standalone
  `src/FS.GG.Governance.CacheEligibilityCommand/Loop.fs`, which prints the evidence view directly (confirmed: it
  has its own `Loop.fs`). Add the `HumanText` project reference to both hosts; keep each host's JSON
  byte-identical.
- [ ] T026 **[DEFERRED]** [US1] Host parity goldens: extend each command host's tests
  (`tests/FS.GG.Governance.RouteCommand.Tests`, `…ShipCommand.Tests`, `…VerifyCommand.Tests`,
  `…ReleaseCommand.Tests`, `…CacheEligibilityCommand.Tests`, and the CLI-host evidence/explain tests) to assert
  (a) the no-`--json` run prints the `HumanText` projection with **no** ANSI escapes and (b) the `--json` run's
  golden is **byte-identical** to the pre-F27 baseline (T007). Depends on T021–T025.

**Checkpoint**: MVP — every command's human terminal output is a faithful, ANSI-free, smoke-snapshot-stable
plain-text projection of the **same** report object that produces its byte-identical JSON. Rich color, watch, and
the TUI are not yet landed.

---

## Phase 4: User Story 2 — Rich Spectre.Console rendering for interactive terminals (Priority: P2)

**Goal**: In an interactive terminal, `HumanRender` renders the same report richly — a color-coded verdict banner
and grouped tables of gates / findings / blockers — **terminal-width resilient** and **degrading to the
`HumanText` plain text** on non-TTY / `NO_COLOR` / explicit-plain. Color/ANSI appear **only** in interactive output
and **never** in JSON or piped output (FR-004, FR-005, FR-006; SC-004, SC-007). The rich renderer lives in
`HumanRender` only.

**Independent Test**: Against TTY / non-TTY / `NO_COLOR` / explicit-plain and narrow-width fixtures (Spectre's
`TestConsole`): a TTY yields a color banner + grouped tables conveying the same facts as plain/JSON; a non-TTY /
`NO_COLOR` / explicit-plain yields ANSI-free plain text byte-equal to `HumanText`; a narrow width reflows/truncates
without corrupting layout; `--json` is ANSI-free and byte-identical regardless of terminal state.

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T027 [P] [US2] `tests/FS.GG.Governance.Cli.Tests/RichRenderTests.fs` — over a Spectre `TestConsole` (recorded
  output), `HumanRender.RichRender.emit Rich view plain console` shows a color-coded verdict banner and grouped
  gate/finding/blocker tables conveying the **same** verdict/blockers the `HumanText`/JSON projections do (FR-005,
  SC-004; acceptance US2.1).
- [X] T028 [P] [US2] `tests/FS.GG.Governance.Cli.Tests/DegradeToPlainTests.fs` — `emit Plain view plain console`
  (the degrade path taken on non-TTY / `NO_COLOR` / explicit-plain) writes the **exact** `HumanText.of*` `plain`
  string with **no** ANSI escapes — degrade is to the precomputed plain projection, not a third rendering (FR-004,
  SC-002, SC-004; acceptance US2.2).
- [X] T029 [P] [US2] `tests/FS.GG.Governance.Cli.Tests/WidthResilienceTests.fs` — render `emit Rich` over a
  `TestConsole` at a range of widths incl. very narrow and an unknown/zero width; assert it reflows or truncates
  cleanly (no overflow / no corrupted layout) and an unknown `Width = None` falls back to the safe default (80)
  rather than failing (FR-006, SC-004; acceptance US2.3, edge "very narrow / unknown width").
- [X] T030 [P] [US2] `tests/FS.GG.Governance.Cli.Tests/JsonStaysAnsiFreeTests.fs` — with `--json` requested in
  **any** terminal/color state (TTY, `NO_COLOR`, explicit-plain), the JSON output contains **no** ANSI escapes and
  is byte-identical to the pre-F27 golden — JSON always overrides and never reaches `RichRender` (FR-004, SC-002;
  acceptance US2.4, edge "--json always wins").

### Implementation for User Story 2

- [X] T031 [US2] Author the rich renderer `src/FS.GG.Governance.HumanRender/RichRender.fsi` + `RichRender.fs`
  (contracts/rich-render.md): `emit: mode:RenderMode -> view:ReportView -> plain:string -> console:IAnsiConsole ->
  unit`. `Rich` ⇒ color-coded verdict banner (`ReportView.Title`) + grouped tables from `Sections`, width-resilient
  (reflow/truncate to `ColorCapability.Width`, safe default 80 when unknown). `Plain` ⇒ writes `plain` verbatim, no
  ANSI. `Json` ⇒ not handled (present in the match for totality). The `plain` arg is the precomputed `HumanText.of*`
  string so the degrade path is byte-equal and `RichRender` need not re-import every report type. Exercisable with
  an injected `TestConsole`/width (T027/T029). Depends on T020 (plain text) and T019 (`ReportView`).
- [ ] T032 **[DEFERRED]** [US2] Author the **capability-sensing effect** at the CLI host edge: sense `IsTty` / `NO_COLOR` /
  explicit-`--plain` / terminal `Width` into a `RenderMode.ColorCapability`, executed only at the interpreter edge
  (no pure function senses) — in `src/FS.GG.Governance.HumanRender` (an edge helper) consumed by the CLI dispatch.
  Pure `selectMode` is **not** changed; this only fills its input (FR-004; data-model §1/§6 D6).
- [ ] T033 **[DEFERRED]** [US2] Wire render-mode selection into CLI dispatch: compute `RenderMode.selectMode` from the sensed
  capability (T032) + `--json`, then route to JSON (existing `*Json` path) / `HumanText` / `HumanRender.RichRender`
  accordingly, in `src/FS.GG.Governance.Cli/Cli.fs` (and any per-command host that prints directly). `--json`
  always wins; `Cli` references `HumanText` + `HumanRender` (no direct Spectre ref). Makes T027–T030 pass. Depends
  on T031, T032.

**Checkpoint**: US1 + US2 — interactive terminals get a scannable color/table view; every non-interactive context
(non-TTY, `NO_COLOR`, explicit-plain, `--json`) gets ANSI-free output identical to the plain/JSON projection; the
Spectre dependency is confined to `HumanRender`.

---

## Phase 5: User Story 3 — `watch` projection re-renders route/evidence/check reports on change (Priority: P2)

**Goal**: An `fsgg ... --watch` (or `fsgg watch`) projection re-runs the existing route/evidence/check evaluation
and re-renders the report when the working tree changes, **debounced** so a burst of edits yields a **single**
re-render. Read-only: no verdict, gate, exit-code, or contract changes (FR-007, FR-008; SC-005, SC-006). The
debounce is **pure** (in `update`); sensing/timer/re-render are `HumanRender` edge effects.

> **"check" report binding (C1).** "route/evidence/check" names three **existing** report objects: `route`
> (`Route.RouteResult`), `evidence` (`CacheEligibility.CacheEligibilityReport`), and `check` = the `verify`
> gate-check report (`Ship.ShipDecision`). No new "check" report object is introduced — the watch re-render
> re-projects whichever of these three the watched command resolves to (FR-007).
>
> **Host binding (C2).** The packed **`fsgg`** is `FS.GG.Governance.RouteCommand` (route-only); the
> multi-subcommand dispatcher is `FS.GG.Governance.Cli`, packed as **`fsgg-governance`**. The new `watch`/`tui`
> **subcommands** are added to the dispatcher (`Cli`, beside `route`/`explain`/`evidence`); the `--plain`/`--watch`
> **flags** also attach to the standalone packed exes (e.g. `fsgg route --watch`) through the shared `HumanRender`
> edge. "`fsgg watch`/`fsgg tui`" in the spec is the generic name and resolves to `fsgg-governance watch/tui` until
> the future single-tool unification (plan.md "Host resolution").

**Independent Test**: A pure-`update` debounce fixture feeding N `ChangeDetected` then one `WindowSettled` yields
exactly one `ReRender` (not one per event); a tracked-file change triggers a re-evaluation reflecting the new
state; the session changes no verdict and emits no new contract; a transiently-unreadable tree yields
`InputUnreadable`, superseded by the next settled re-render — never a crash, never a fabricated report.

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T034 [P] [US3] `tests/FS.GG.Governance.Cli.Tests/WatchDebounceTests.fs` — drive the **pure**
  `HumanRender.Watch.update` with a synthetic burst of `ChangeDetected` within `debounceWindow` then a
  `WindowSettled` ⇒ exactly **one** `ReRender` effect emitted; events spread beyond the window ⇒ one `ReRender`
  each (FR-007, SC-005; acceptance US3.2). Synthetic event burst disclosed at the use site, `Synthetic` in the
  test name (Constitution V).
- [X] T035 [P] [US3] `tests/FS.GG.Governance.Cli.Tests/WatchReadOnlyTests.fs` — across the watch transition the
  only effects are `SenseChanges` / `ScheduleDebounce` / `ReRender`; assert no `Msg` changes a verdict, evaluates a
  new rule, or emits a JSON-contract write of a new shape — `ReRender` re-runs the **existing** route/evidence/
  check evaluation and re-projects via `HumanText`/`RichRender` only (FR-008, SC-006; acceptance US3.1, US3.3).
- [X] T036 [P] [US3] `tests/FS.GG.Governance.Cli.Tests/WatchSafeFailureTests.fs` — a `Rerendered
  (InputUnreadable …)` from a transiently-unreadable/partial tree surfaces a clear **input** signal (distinct from
  a tool defect) in `LastSignal`, no swallowed error, no crash, no fabricated report, and is **superseded** by the
  next settled re-render (FR-012; edge "watch on a transiently-unreadable tree").

### Implementation for User Story 3

- [X] T037 [US3] Author the watch MVU core `src/FS.GG.Governance.HumanRender/Watch.fsi` + `Watch.fs` pure section
  (contracts/watch-mvu.md, data-model §5): `WatchSignal`/`WatchModel`/`WatchMsg`/`WatchEffect`, `debounceWindow`,
  `init`, and a **pure** `update` with the debounce transition (`ChangeDetected` refreshes `PendingSince` + emits
  `ScheduleDebounce (at+window)`; `WindowSettled` with no later change clears it + emits one `ReRender`;
  `Rerendered` sets `LastSignal`). `init`/`update` do **no** I/O. Makes T034–T036 pass. Depends on T033 (re-render
  reuses the render-mode dispatch).
- [ ] T038 **[PARTIAL]** [US3] Author the watch **interpreter edge** in `src/FS.GG.Governance.HumanRender/Watch.fs`: the
  file-change sensing effect (`FileSystemWatcher`, with a poll fallback for unreliable filesystems — plan D4), the
  debounce timer effect, and the `ReRender` effect that re-runs the existing route/evidence/check evaluation and
  prints via the T033 dispatch. Read-only — writes **no** new contract artifact. Includes a real-`FileSystemWatcher`
  interpreter test over a temp tree driving at least one end-to-end settle where safe (Constitution IV/V). Depends
  on T037.
- [ ] T039 **[DEFERRED]** [US3] Wire the `--watch` flag / `watch` command into CLI parsing + dispatch as a **read-only** mode
  over route/evidence/check. Add the `watch` **subcommand** to the multi-subcommand dispatcher
  `src/FS.GG.Governance.Cli/Cli.fs` (+ `Cli.fsi`) — packed as `fsgg-governance`, beside `route`/`explain`/
  `evidence` — and attach the `--watch` **flag** to the standalone packed `fsgg` (`src/FS.GG.Governance.RouteCommand`)
  through the shared `HumanRender` edge so `fsgg route --watch` works (C2 host binding above). New public command
  surface ⇒ Tier-1 `.fsi` + baseline (T045). Depends on T038.

**Checkpoint**: US1–US3 — the operator inner loop re-renders the route/evidence/check report on change, coalescing
bursts into a single settled re-render, never changing a verdict or emitting a new contract, failing safely on a
transiently-unreadable tree.

---

## Phase 6: User Story 4 — Optional TUI for navigating the report objects (Priority: P3)

**Goal**: An **optional** read-only TUI navigates the same immutable report objects via the `ReportView` —
selected gates, proof/explanation trees, blockers/warnings, evidence/provenance references — letting an operator
drill into why a gate was selected or a precondition failed. Strictly read-only: no verdict changes, no new gate
runs, no contract emitted (FR-009; SC-006). Built on the already-present Spectre dependency in `HumanRender`
(plan D5). **Scope note:** the plan ships the TUI in this row as a minimal navigator; a richer free-form TUI is an
explicit bounded follow-up, not this row.

**Independent Test**: The TUI's `View` is the `ReportView` projected from the **same** report object the
`HumanText`/JSON views use (report-object parity); driving the pure navigation `update` over recorded keypresses
changes no verdict, runs no new gate, and emits no contract.

### Tests for User Story 4 ⚠️ (write first, must FAIL before impl)

- [X] T040 [P] [US4] `tests/FS.GG.Governance.Cli.Tests/TuiParityTests.fs` — `Tui.init view` holds the **same**
  `ReportView` the `HumanText`/JSON views project; assert the navigable nodes (gates, proof/explanation trees,
  blockers, evidence references) carry the same facts and are never separately derived (FR-009, SC-006; acceptance
  US4.1).
- [X] T041 [P] [US4] `tests/FS.GG.Governance.Cli.Tests/TuiReadOnlyTests.fs` — drive the pure `Tui.update` over
  recorded keypresses (`MoveUp`/`MoveDown`/`Expand`/`Collapse`/`Quit`); assert it changes only `Path`/`Expanded`,
  no governance verdict changes, no new gate runs, and no automation contract is emitted; only `ReadKey`/`Draw`/
  `Exit` effects appear (FR-008, FR-009, SC-006; acceptance US4.2).

### Implementation for User Story 4

- [X] T042 [US4] Author the TUI MVU core `src/FS.GG.Governance.HumanRender/Tui.fsi` + `Tui.fs` pure section
  (contracts/tui-mvu.md, data-model §6): `TuiModel = { View: ReportView; Path: int list; Expanded: Set<int list> }`;
  `TuiMsg = MoveUp | MoveDown | Expand | Collapse | Quit`; `TuiEffect = ReadKey | Draw of TuiModel | Exit`; `init`,
  and a **pure** `update` that handles selection/expansion only (no I/O, no evaluation; non-`Quit` ⇒ `Draw` then
  `ReadKey`; `Quit` ⇒ `Exit`). Makes T040/T041 pass. Depends on T019 (`ReportView`).
- [ ] T043 **[DEFERRED]** [US4] Author the TUI **interpreter edge** + `tui` command wiring: the Spectre key-read + redraw loop in
  `src/FS.GG.Governance.HumanRender/Tui.fs`, and the `tui` **subcommand** in the multi-subcommand dispatcher
  `src/FS.GG.Governance.Cli/Cli.fs` + `Cli.fsi` (packed as `fsgg-governance`; spec spelling "`fsgg tui`" is the
  generic name per C2 host binding above); new public command surface ⇒ Tier-1 `.fsi` + baseline (T045). Depends
  on T042, T039 (shares the command plumbing).

**Checkpoint**: All four stories — an optional read-only TUI navigates the same report objects with full parity,
never changing a verdict or emitting a contract.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Bless both surface baselines, enforce the dependency boundary, demonstrate the non-contractual-text
invariant, document the new modes/commands, and gate the full suite.

- [X] T044 [P] Bless `surface/FS.GG.Governance.HumanText.surface.txt` (`RenderMode` + `HumanText` + `ReportView`)
  once the `.fs` bodies are stable (`BLESS_SURFACE=1`). Depends on US1 bodies.
- [X] T045 [P] Bless `surface/FS.GG.Governance.HumanRender.surface.txt` (the `RichRender.emit` signature +
  `Watch`/`Tui` pure MVU surface) and, if `watch`/`tui`/render-mode changed the `Cli` surface, re-bless the `Cli`
  baseline (`BLESS_SURFACE=1`). Depends on US2–US4 bodies (T031, T037, T042, T039, T043).
- [X] T046 [P] Commit the plain-text smoke snapshots produced in T017 (route/ship/verify/release/evidence/explain)
  as the stable baselines.
- [X] T047 [P] [SC-007] `tests/FS.GG.Governance.HumanText.Tests/DependencyBoundaryTests.fs` (or a build check) —
  parse the `.fsproj` references and assert **no** pure core, **no** `*Json`, and **no** `HumanText` project
  references Spectre.Console; **only** `FS.GG.Governance.HumanRender` does (and `Directory.Packages.props` carries
  the single central pin). (FR-013, SC-007; quickstart scenario 6.)
- [X] T048 [SC-008] `tests/FS.GG.Governance.HumanText.Tests/NonContractualTextTests.fs` — demonstrate that a
  deliberate plain-text wording/layout change updates **only** its smoke snapshot while **every** JSON golden stays
  byte-identical and no verdict/exit code changes (a guard proving plain text is non-contractual). (FR-003, SC-008;
  quickstart scenario 5.)
- [ ] T049 **[PARTIAL]** [P] Update docs: document the render modes (JSON / plain / rich), `--plain`/`--no-color`/`NO_COLOR`/TTY
  behavior, `--watch` / `fsgg watch`, and `fsgg tui` in the CLI docs/README; note plain/rich are non-contractual
  and JSON is the only contract; record the Spectre.Console NEED/SCOPE/OWNER.
- [ ] T050 **[PARTIAL]** Full-suite green gate: `dotnet build FS.GG.Governance.sln` + `dotnet test` across all affected projects;
  confirm every pre-F27 JSON golden (route/ship/verify/release/evidence) is byte-identical (SC-002) and all smoke
  snapshots stable (SC-003). Depends on the desired stories being complete.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no dependencies.
- **Foundational (Phase 2)** → depends on Setup; **blocks US1–US4**.
- **US1 (Phase 3)** → depends on Foundational. **MVP.**
- **US2 (Phase 4)** → depends on US1 (rich degrades to the `HumanText` plain projection; renders `ReportView`).
- **US3 (Phase 5)** → depends on US2's render-mode dispatch (re-render reuses it).
- **US4 (Phase 6)** → depends on US1's `ReportView` mapping; the `fsgg tui` command wiring depends on US3's command
  plumbing (T039).
- **Polish (Phase 7)** → depends on the desired stories being complete.

### Within each story

- Tests are written first and must FAIL before implementation.
- `ReportView.fs` bodies (T019) precede `HumanText.fs` (T020), which precedes the host delegations (T021–T025) and
  the rich renderer (T031).
- Pure cores (render-mode, projections, watch/tui `update`) precede their `HumanRender` interpreter edges.

### Parallel opportunities

- Setup T001–T005 + T007 run in parallel; T006 (sln) after them.
- Foundational T008/T009/T010 author the surfaces in order (T009 after T008, T010 after T009); T012/T013 parallel
  after T011.
- **US1 tests T014–T018 run in parallel.** After T019→T020, host delegations **T021–T025 run in parallel**
  (different host projects/files); T026 after.
- US2 tests T027–T030 parallel; US3 tests T034–T036 parallel; US4 tests T040/T041 parallel.
- Polish T044–T049 largely parallel; T050 last.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP and VALIDATE**: every command's human view is
a faithful, ANSI-free, snapshot-stable projection of the same report object as its byte-identical JSON. This alone
delivers the roadmap exit criterion ("operator UX improves without creating a second source of truth").

### Incremental delivery

US1 (plain text, MVP) → US2 (rich color/tables, `HumanRender`) → US3 (watch) → US4 (optional TUI). Each story adds
an independently testable projection without changing any report object, verdict, exit code, or JSON contract.

---

## Notes

- `[P]` = different files, no dependency on another incomplete task in this phase.
- No task is `[X]` without real, exercised evidence; a host delegation is not done until its no-`--json` run is
  exercised and its JSON golden proven byte-identical (the vertical-slice rule).
- **JSON is the only contract**: every change here is presentation-only; JSON goldens stay byte-identical (SC-002),
  plain/rich text is held to smoke-snapshot stability only (SC-003, SC-008).
- **Dependency boundary**: Spectre.Console is pinned centrally (T002) and referenced **only** by
  `FS.GG.Governance.HumanRender` (T003); enforced by T047 (FR-013, SC-007).
- Report-object types are resolved: `route`→`Route.RouteResult`, `explain`→`RouteExplain.RouteExplanation`,
  `ship`/`verify`→`Ship.ShipDecision`, `release`→`ReleaseReport.ReleaseReport`, `evidence`→
  `CacheEligibility.CacheEligibilityReport`; each `HumanText.of*`/`ReportView.viewOf*` mirrors the input tuple of
  the matching `*Json.of*` (route/verify also carry `CacheEligibilityReport option` + `(GateId*GateOutcome) list`).
</content>
</invoke>
