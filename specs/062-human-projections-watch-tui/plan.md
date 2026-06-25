# Implementation Plan: Human Projections — Plain Text, Spectre.Console, Watch, and Optional TUI Over the Immutable Report Objects (F27)

**Branch**: `062-human-projections-watch-tui` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/062-human-projections-watch-tui/spec.md`

## Summary

After F26, every `fsgg` command resolves to an **immutable, presentation-free report object** — `Route.RouteResult`
(`route`), `RouteExplain.RouteExplanation` (`explain`), `Ship.ShipDecision` (`ship`/`verify`),
`ReleaseReport.ReleaseReport` (`release`), and `CacheEligibility.CacheEligibilityReport` (`evidence`) — projected
to deterministic, `schemaVersion`-headed JSON by the `*Json` libraries (`RouteJson`, `VerifyJson`, `ReleaseJson`,
…). The JSON is the automation contract. What is missing is a **disciplined human experience** over those same
objects: today each command host's plain-text summary is produced ad-hoc inside its own `Loop.render`, with no
single surface guaranteeing the human view and the JSON are projections of the **same** report value, and there is
no rich, watch, or TUI surface at all.

This row adds **presentation-only projections** of the existing report objects and changes **nothing** about the
reports, verdicts, exit-code schemes, or JSON contracts (F18–F26 reused verbatim). In priority order:

1. **Plain-text projection from the immutable report objects (P1, MVP).** A new **pure** library
   `FS.GG.Governance.HumanText` carries one ANSI-free, deterministic projection **per report object**
   (`ofRouteResult`, `ofRouteExplanation`, `ofShipDecision`, `ofVerifyDecision`, `ofReleaseReport`,
   `ofCacheEligibilityReport`), each mirroring the input tuple of the matching `*Json.of*` so the human view and
   the JSON are provably derived from one report value (FR-001). Every command host's existing plain-text `render`
   branch is **replaced by a delegation** to `HumanText`; the JSON path is untouched and every JSON golden stays
   **byte-identical** (FR-010, SC-002). The plain text is human-readable and **non-contractual**, held only to
   smoke-snapshot stability (FR-003, FR-011, SC-003). **No new dependency.** This is the single-source-of-truth
   guarantee the roadmap exit criterion demands.
2. **Rich (Spectre.Console) rendering for interactive terminals (P2).** A new CLI-host-tier presentation library
   `FS.GG.Governance.HumanRender` adds a color-coded verdict banner and grouped gate/finding/blocker tables —
   **terminal-width resilient**, **degrading to the `HumanText` plain text** on non-TTY / `NO_COLOR` /
   explicit-plain (FR-004, FR-006). It is the **sole owner** of the rich-rendering dependency; no pure core, no
   `*Json`, and no `HumanText` references it (FR-005, FR-013, SC-007).
3. **`watch` projection over route/evidence/check reports (P2).** `HumanRender` adds a debounced, read-only watch
   MVU: a pure `update` coalesces a burst of file-change events into a **single** re-render after the window
   settles; the interpreter edge senses changes (`FileSystemWatcher`), re-runs the **existing** route/evidence/
   check evaluation, and re-projects the report. **"check" denotes the `verify` gate-check report** — the watched
   triad is `route` (`RouteResult`), `evidence` (`CacheEligibilityReport`), and `verify`-check
   (`Ship.ShipDecision`); no new "check" report object exists. No verdict, rule, exit-code, or contract changes
   (FR-007, FR-008, SC-005, SC-006); a transiently-unreadable tree surfaces a clear input signal, superseded by
   the next settled re-render (FR-012).
4. **Optional read-only TUI (P3).** `HumanRender` adds a minimal interactive navigator over a **pure** `ReportView`
   view-model (projected in `HumanText` from the same report objects): selected gates, proof/explanation trees,
   blockers, evidence references. Read-only — no verdict, gate, or contract (FR-009, SC-006).

The work **composes the leaf-plus-projection precedent**: `HumanText` is a pure, total projection library exactly
like the `*Json` libraries (the only difference is its output is human text, not JSON), and the report objects it
renders already exist and are unchanged. The only new I/O — TTY/`NO_COLOR` sensing, file-change sensing, the
debounce timer, terminal writes, and the TUI key loop — lives at the CLI-host edge inside `HumanRender`'s MVU
interpreters, never in a pure core (FR-013). The one new runtime dependency (Spectre.Console) is centrally pinned
and confined to `HumanRender` (the YamlDotNet/`Config` precedent for a justified, isolated dependency).

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **One source of truth, two projections (D1).** `HumanText` is a pure projection library mirroring `*Json`; each
   command host's plain-text branch delegates to it over the **same** report object that produces its JSON. JSON
   stays byte-identical (FR-001, FR-010, SC-001, SC-002).
2. **Presentation lives in one new CLI-host library `FS.GG.Governance.HumanRender` (D2).** Because the repo has
   **multiple host executables** today (`RouteCommand` is the packed `fsgg`; ship/verify/release/cache/refresh are
   `IsPackable=false` standalone exes pending a future single-tool unification; the legacy `Cli` is a separate
   tool), "the CLI host" is realized as **one shared presentation library** the host edges call — the sole owner
   of the rich dependency and the watch/tui MVU. This is the concrete answer to FR-005/FR-013/SC-007's "CLI host
   only."
3. **Spectre.Console is the rich/TUI library — a justified, isolated new dependency (D3).** Centrally pinned in
   `Directory.Packages.props` with a NEED/SCOPE/OWNER comment (the YamlDotNet precedent), referenced **only** by
   `HumanRender`. It never enters a pure core, the first-useful-product library, `*Json`, or `HumanText`.
4. **Watch = `FileSystemWatcher` + pure debounce MVU (D4).** BCL sensing (no new dependency), a pure debounce/
   coalesce `update`, a polling fallback for unreliable filesystems; read-only, emits no contract.
5. **TUI = minimal read-only navigator on Spectre, in-row (D5).** The navigable model is the pure
   `HumanText.ReportView`; the interactive shell reuses the Spectre dependency. A richer free-form TUI
   (e.g. Terminal.Gui) is explicitly a possible **bounded follow-up**, not this row.
6. **Render mode is pure; sensing is an effect (D6).** `RenderMode = Json | Plain | Rich`; `selectMode` is total
   (`Json` always wins; `Rich` iff TTY ∧ ¬`NO_COLOR` ∧ ¬explicit-plain; else `Plain`). TTY/`NO_COLOR` detection
   is an edge `Effect`.
7. **Determinism of rendered content; JSON is the only contract (D7).** Rendered content is deterministic for
   identical repo state (stable ordering, normalized paths, no clock/username/env); smoke snapshots are stable
   though layout is non-contractual; every JSON golden stays byte-identical (FR-011, SC-003, SC-008).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`, `LangVersion=latest`).

**Primary Dependencies**: **FSharp.Core 10.1.301 only** for the pure `HumanText` library (no presentation, no
BCL-beyond-`System` dependency — it is a string-building projection exactly like the `*Json` libraries, which use
`System.Text.Json`; `HumanText` needs only `System.Text`). **One NEW runtime dependency: Spectre.Console**
(centrally pinned in `Directory.Packages.props`; NEED = color/table/width-resilient rich rendering + a minimal
read-only TUI, FR-005/FR-006/FR-009; SCOPE = referenced **only** by `FS.GG.Governance.HumanRender`; OWNER = CLI
host maintainer) — the first new runtime package since YamlDotNet (F014), justified and isolated identically.
Watch sensing uses **BCL `System.IO.FileSystemWatcher`** (no package). Project references reused verbatim by
`HumanText`: `Route` (`RouteResult`), `RouteExplain` (`RouteExplanation`), `Ship` (`ShipDecision`, `Verdict`,
`ExitCodeBasis`), `ReleaseReport` (`ReleaseReport`, `PreconditionEvidence`), `CacheEligibility`
(`CacheEligibilityReport`), `Gates` (`GateId`), `GateRun` (`GateOutcome`), `Findings`, `Config`.

**Storage**: None. No new artifact, no database, no network. `HumanText` writes nothing (it returns strings);
`HumanRender` writes only to the terminal (stdout/stderr) at its interpreter edge. The watch projection **reads**
the working tree to re-run the existing evaluation and **writes no new contract artifact** (FR-008) — it reuses
whatever the underlying route/evidence evaluation already produces.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck / FsCheck 2.16.6 (repo standard). New test project
`FS.GG.Governance.HumanText.Tests` (pure projection: ANSI-free, determinism, blocked-verdict, report-object
parity vs `*Json`, smoke snapshots, non-contractual-text guard, the `ReportView` parity). The existing
`FS.GG.Governance.Cli.Tests` is **extended** for the `HumanRender` edge (rich over a Spectre test console,
degrade-to-plain on non-TTY/`NO_COLOR`/explicit-plain, width resilience, JSON-stays-ANSI-free, watch debounce,
watch read-only, watch safe-failure, TUI parity + read-only). Each command host's `.Tests` is extended for the
plain delegation + JSON byte-identity. Spectre's `IAnsiConsole`/`TestConsole` (recording, fixed width) drives the
rich/TUI tests deterministically without a real terminal. Smoke snapshots are committed text files re-blessable
via `BLESS_SNAPSHOT=1`. FSI semantic tests load the public surface (`HumanText.of*`, `RenderMode.selectMode`,
`ReportView.viewOf*`), never internals (Constitution I).

**Target Platform**: Cross-platform .NET libraries + the existing `fsgg` CLI executables (Linux/macOS/Windows).
Rich/watch/TUI are interactive-terminal features that degrade cleanly on non-TTY everywhere (FR-004).

**Project Type**: Presentation-only expansion — one new pure projection library (`HumanText`), one new CLI-host
presentation library (`HumanRender`, the sole owner of the new dependency + the watch/tui MVU), and additive
plain-text delegation wired into each existing command host edge; single-solution F# layout.

**Performance Goals**: Not a hot path. `HumanText.of*` is a single linear pass over an already-computed report.
The rich renderer builds a Spectre render tree once per command. The watch loop is event-driven and **debounced**
so a burst of edits costs one re-render, not N (FR-007, SC-005); each re-render pays only the cost of the existing
route/evidence evaluation it re-runs.

**Constraints**: Rendered **content** is deterministic and **byte-identical** for identical repository state (no
wall-clock / abs-path / username / environment / input-order dependence; stable ordering, normalized paths), so
smoke snapshots are stable even though the layout is non-contractual (FR-011, SC-003). Color/ANSI appear **only**
in interactive terminal output and **never** in JSON, non-TTY/piped output, or `NO_COLOR`/explicit-plain (FR-004,
SC-002, SC-004); `--json` always overrides to the byte-identical contract (SC-002). The presentation dependency is
confined to `HumanRender`; no pure core, `*Json`, or `HumanText` references it (FR-013, SC-007). Watch and TUI are
**read-only** — no verdict, rule, exit-code, or contract change (FR-008, SC-006). Input-vs-tool-defect diagnostics
preserved: a transiently-unreadable tree (watch) or a malformed input surfaced in a one-shot render produces a
clear input signal — no swallowed error, no crash, **no fabricated report** (FR-012, Constitution VI).

**Scale/Scope**: 2 new `src` libraries (`HumanText` pure; `HumanRender` CLI-host presentation) + 1 new test
project (`HumanText.Tests`) + the extended `Cli.Tests`; plain-text delegation wired into ≤5 existing command host
edges (route/ship/verify/release + the legacy `Cli` evidence/explain dispatch); 1 new committed surface baseline
(`HumanText`; `HumanRender`'s public surface baseline if it exposes one); 1 new centrally-pinned dependency
(Spectre.Console). **No** report-object change, **no** verdict/rule change, **no** exit-code change, **no** JSON
schema change, **no** new JSON contract. P1 = `HumanText` + per-command plain delegation; P2 = `HumanRender` rich
+ watch; P3 = `HumanRender` minimal TUI.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Every new public module (`HumanText.RenderMode`,
  `HumanText.HumanText`, `HumanText.ReportView`, and `HumanRender`'s `RichRender`/`Watch`/`Tui`) is drafted as
  `.fsi` and exercised through the loaded public surface before any `.fs` body exists (the `*Json` projection
  precedent). Semantic tests call `selectMode`, `of*`, `viewOf*`, the watch `update`, never internals.
- **II. Visibility Lives in `.fsi`** — PASS. Every new public module ships a curated `.fsi`; `.fs` bodies carry no
  access modifiers. New committed surface baseline for `FS.GG.Governance.HumanText`; `HumanRender` ships a curated
  `.fsi` and a baseline for whatever it exposes (its watch/tui `Model`/`Msg` pure surface is testable). The
  plain-text/rich **renderings** are non-contractual (smoke-snapshot only); only the **library API** is surface.
- **III. Idiomatic Simplicity** — PASS. Closed DUs (`RenderMode`, the `ReportView` node tree, watch/tui `Msg`),
  plain records, pipelines, exhaustive matches; no SRTP / reflection / type-providers / custom CEs / non-trivial
  active patterns. The one new dependency (Spectre.Console) is justified in the spec/plan (FR-005, the rich/TUI
  surface cannot be hand-rolled in proportion) and isolated to `HumanRender`, exactly the YamlDotNet treatment.
  Any local mutation in a string builder carries a one-line reason (the `*Json` precedent).
- **IV. Elmish/MVU Is the Boundary** — PASS. `HumanText` is a pure, total projection leaf — no MVU ceremony (the
  `*Json` precedent). The **stateful/I/O** surfaces — `watch` (file-change sensing, debounce timer, re-render) and
  the `tui` (key input, redraw) — are modelled through an explicit MVU boundary in `HumanRender`: pure
  `Model`/`Msg`/`update` (debounce coalescing; navigation/selection), I/O as `Effect` data (`SenseChanges`,
  `ScheduleDebounce`, `ReRender`, `ReadKey`, `Draw`), executed only at the interpreter edge. Render-mode selection
  (`selectMode`) is pure; TTY/`NO_COLOR` sensing is an `Effect`. Pure transition tests + interpreter tests both
  required (data-model §Watch/§Tui).
- **V. Test Evidence Is Mandatory** — PASS. Tests fail-before/pass-after against the **real** report objects (the
  F18–F26 cores never mocked) and a real Spectre `TestConsole` for the rich/TUI/width tests; the watch debounce is
  driven by a synthetic event burst through the **pure** `update` (no real timer needed for the coalescing proof),
  with a real-`FileSystemWatcher` interpreter test where safe. Any synthetic terminal/event input is disclosed at
  the use site, carries `Synthetic` in the test name, and is listed in the PR.
- **VI. Observability and Safe Failure** — PASS. The human projections distinguish a missing/malformed **input**
  (an unparseable config surfaced in a one-shot render; a transiently-unreadable working tree during a watch
  re-render) from a **tool defect**, surfacing a clear input signal with no swallowed error, no crash, and **no
  fabricated report**, superseded by the next settled re-render where applicable (FR-012). A blocked verdict is
  rendered as blocked, never softened (FR-002).

**Change Classification: Tier 1 (contracted change)** — adds new public API surface (the `HumanText` projection
library + `HumanRender`'s public MVU surface), **introduces one new dependency** (Spectre.Console), and adds new
public CLI command/flag vocabulary (`--watch`/`fsgg watch`, `fsgg tui`, an explicit-plain flag). Any one of these
is Tier 1; together they require the full chain: spec, plan, `.fsi` for every new module, the `HumanText` (and
`HumanRender`) surface baselines, the new-dependency justification in `Directory.Packages.props`, test evidence,
and documentation of the new modes/commands. It adds **no** report-object change, **no** verdict/rule change,
**no** exit-code change, and **no** JSON schema/contract change — so the migration surface is limited to the new
CLI modes/commands and the new dependency; every existing JSON golden stays byte-identical (documented in
`contracts/cli-surface.md`).

**Result: PASS — no violations. Complexity Tracking is empty.** (The new dependency is a *justified* addition
under Principle III / Engineering Constraints, not a violation: it states need, central version-pinning, and
owner, and is confined to the host-edge presentation library, never the pure cores or the first-useful-product
library.)

## Project Structure

### Documentation (this feature)

```text
specs/062-human-projections-watch-tui/
├── plan.md                                 # This file (/speckit-plan output)
├── research.md                             # Phase 0 — D1..D7 decisions
├── data-model.md                           # Phase 1 — RenderMode, HumanText projections, ReportView, Watch/Tui MVU
├── quickstart.md                           # Phase 1 — per-story validation scenarios
├── contracts/                              # Phase 1
│   ├── render-mode.md                      #   RenderMode + selectMode (pure; JSON always wins)
│   ├── human-text-projection.md            #   HumanText.of* per report object (pure, ANSI-free, deterministic)
│   ├── report-view.md                      #   ReportView navigable view-model + viewOf* (pure; the TUI/rich source)
│   ├── rich-render.md                      #   HumanRender rich projection (Spectre; degrade-to-plain; width-resilient)
│   ├── watch-mvu.md                        #   watch Model/Msg/Effect + debounce (read-only; FileSystemWatcher edge)
│   ├── tui-mvu.md                          #   optional read-only TUI Model/Msg/Effect over ReportView
│   └── cli-surface.md                      #   new flags/commands (--plain/--watch/fsgg watch/fsgg tui); JSON unchanged
├── checklists/
│   └── requirements.md                     # (already present — spec quality checklist)
└── tasks.md                                # Phase 2 (/speckit-tasks — present; re-sync after this plan, see note)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.HumanText/                       # NEW (P1) — PURE projection library (no presentation dep)
│   ├── RenderMode.fsi / RenderMode.fs                #   RenderMode = Json|Plain|Rich; ColorCapability; selectMode
│   ├── HumanText.fsi / HumanText.fs                  #   ofRouteResult / ofRouteExplanation / ofShipDecision /
│   │                                                 #     ofVerifyDecision / ofReleaseReport / ofCacheEligibilityReport
│   ├── ReportView.fsi / ReportView.fs                #   ReportView node tree + viewOf* (the navigable model for TUI/rich)
│   └── FS.GG.Governance.HumanText.fsproj             #   refs: Route, RouteExplain, Ship, ReleaseReport,
│                                                     #     CacheEligibility, Gates, GateRun, Findings, Config
├── FS.GG.Governance.HumanRender/                     # NEW (P2/P3) — CLI-host presentation: SOLE owner of Spectre
│   ├── RichRender.fsi / RichRender.fs                #   emit : RenderMode -> <report> -> IAnsiConsole -> unit;
│   │                                                 #     banner + grouped tables; degrade to HumanText on Plain; width-resilient
│   ├── Watch.fsi / Watch.fs                          #   pure Model/Msg/update (debounce) + Effect; interpreter (FileSystemWatcher)
│   ├── Tui.fsi / Tui.fs                              #   pure navigation Model/Msg/update over ReportView + Effect; interpreter
│   └── FS.GG.Governance.HumanRender.fsproj           #   refs: HumanText (+ the report cores); PackageReference: Spectre.Console
├── FS.GG.Governance.RouteCommand/                    # EXTEND — plain `render` delegates to HumanText.ofRouteResult;
│   └── Loop.fs(i)                                    #     edge offers rich/watch via HumanRender; JSON path untouched
├── FS.GG.Governance.ShipCommand/                     # EXTEND — render -> HumanText.ofShipDecision; ship.json byte-identical
│   └── Loop.fs(i)
├── FS.GG.Governance.VerifyCommand/                   # EXTEND — render -> HumanText.ofVerifyDecision; verify.json byte-identical
│   └── Loop.fs(i)
├── FS.GG.Governance.ReleaseCommand/                  # EXTEND — render -> HumanText.ofReleaseReport; release.json byte-identical
│   └── Loop.fs(i)
└── FS.GG.Governance.Cli/                             # EXTEND — explain/evidence human view -> HumanText.of*;
    ├── Cli.fs(i)                                     #     render-mode dispatch + rich/watch/tui flags via HumanRender
    └── FS.GG.Governance.Cli.fsproj                   #   + ProjectReference HumanText, HumanRender (NO direct Spectre ref)

tests/
├── FS.GG.Governance.HumanText.Tests/                 # NEW — ANSI-free, determinism, blocked-verdict, report-object
│                                                     #   parity vs *Json, smoke snapshots, non-contractual guard, ReportView parity
└── FS.GG.Governance.Cli.Tests/                       # EXTEND (exists) — rich/degrade/width, JSON-ANSI-free, watch
                                                      #   debounce/read-only/safe-failure, TUI parity/read-only;
                                                      #   per-command plain-delegation + JSON byte-identity goldens

surface/
├── FS.GG.Governance.HumanText.surface.txt            # NEW
└── FS.GG.Governance.HumanRender.surface.txt          # NEW (its pure MVU + emit surface)

Directory.Packages.props                              # EDIT — add Spectre.Console (NEED/SCOPE/OWNER comment)
FS.GG.Governance.sln                                  # EDIT — add 2 src + 1 test project
```

**Structure Decision**: Compose, don't fork — and isolate presentation. `HumanText` is a pure projection library
that sits beside the `*Json` libraries and renders the **same** immutable report objects to human text; every
command host's plain branch delegates to it, so the human view and the JSON are one report object's two
projections (FR-001). All presentation that needs color, a terminal, or a stateful loop — the Spectre rich
renderer, the debounced watch MVU, and the minimal read-only TUI — is concentrated in **one** new CLI-host
library, `FS.GG.Governance.HumanRender`, which is the **sole** owner of the new Spectre.Console dependency. This
makes the FR-013/SC-007 boundary checkable by a single project-reference assertion: no pure core, no `*Json`, and
no `HumanText` references Spectre — only `HumanRender` does, and the host exes compose it. The legacy `Cli` and the
per-command host edges call `HumanRender` at their interpreter edges; `--json` bypasses all of it to the
byte-identical contract.

**Host resolution for the new `watch`/`tui` commands and `--plain`/`--watch` flags.** The repo today packs **two
distinct tools**: `FS.GG.Governance.RouteCommand` is the packed **`fsgg`** (`ToolCommandName=fsgg`, route-only),
and `FS.GG.Governance.Cli` is packed as **`fsgg-governance`** (`ToolCommandName=fsgg-governance`) — the
**multi-subcommand dispatcher** that already parses `route`/`explain`/`evidence` (`Cli.fs` `parseCommand`). The
single published `fsgg` that unifies every subcommand is a **future row** (per Technical Context: ship/verify/
release/cache/refresh are `IsPackable=false` standalone exes "pending a future single-tool unification"). So in
**this** row the new read-only **`watch`/`tui` subcommands** are added to the subcommand dispatcher
(`FS.GG.Governance.Cli`, the `fsgg-governance` tool), where they sit beside `route`/`explain`/`evidence`; the
spec/contract spelling "`fsgg watch`/`fsgg tui`" is the **generic** tool name and resolves to
`fsgg-governance watch`/`fsgg-governance tui` until the single-tool unification lands. The **`--plain`/`--watch`
flags** additionally attach to the standalone packed exes (notably `fsgg route`) through the **same** shared
`HumanRender` edge, so `fsgg route --watch`/`--plain` work without duplicating presentation logic. Either way the
sole owner of presentation is `HumanRender`; no host references Spectre directly.

> **Note — tasks.md re-sync.** `tasks.md` was generated before this plan and assumed the rich/watch/tui code would
> live directly in `FS.GG.Governance.Cli` with Spectre referenced there. This plan refines that to a dedicated
> `FS.GG.Governance.HumanRender` presentation library (the sole Spectre owner) and confirms `Cli.Tests` already
> exists (an *extend*, not a create). The `/speckit-tasks` re-sync **has been applied** in `tasks.md`: the Spectre
> central pin and every rich/watch/tui **implementation** task (the `RichRender`, `Watch`, and `Tui` cores and
> their interpreter edges) target `FS.GG.Governance.HumanRender`, while the rich/watch/tui **tests** remain in the
> extended `FS.GG.Governance.Cli.Tests`. (This note formerly cited specific task numbers that the re-sync
> renumbered; refer to the named tasks in `tasks.md` Phases 4–6 rather than fixed IDs.) The P1 plain-text tasks
> (`HumanText` + per-command delegation) are unaffected.

## Complexity Tracking

> No Constitution Check violations. The single new dependency (Spectre.Console) is a justified, version-pinned,
> host-edge-isolated addition under Principle III / Engineering Constraints, not a violation. This section is
> intentionally empty.
