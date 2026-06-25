# Feature Specification: Human-Projection Host Wiring — Per-Command Plain Delegation, Render-Mode Dispatch, and the `watch`/`tui` Surfaces

**Feature Branch**: `063-human-projection-host-wiring`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "the plan is docs/initial-implementation-plan.md check if it is current and start next item." — Currency check: the master plan
(`docs/initial-implementation-plan.md`) was last updated in the F27 commit and is current for the F25–F27
Governance narrative, with one stale spot (Phase 10 still reads 🔴 "not started" though specs `058`/`059` already
shipped that capability-catalog expansion — flagged for a separate fix). The detailed F-row plan ends at **F27 ·
M10**; no brand-new feature is queued. The roadmap's remaining work is three **deferred host-wiring passes** (F25
cost-cache, F26 release, F27 human-projection). The selected next item is the **F27 human-projection host wiring**:
F27 (`062-human-projections-watch-tui`) landed the two presentation **libraries** — pure `FS.GG.Governance.HumanText`
and the Spectre-owning `FS.GG.Governance.HumanRender` — fully built, green over the real F18–F26 cores, with surface
baselines and smoke snapshots committed, but **wired into no command host**. F27 explicitly split that wiring out
as its own row (`tasks.md` T021–T026, T032/T033, T038/T039, T043, T049/T050) because each host's console summary
and the legacy `Cli`'s older Kernel/Host types make the per-host change invasive. This feature is that pass: it
makes every `fsgg` command actually *use* the F27 libraries, with **no** report-object, verdict, exit-code, or JSON
schema/contract change.

## Overview

After F27, every `fsgg` command already resolves to an **immutable, presentation-free report object**
(`Route.RouteResult`, `RouteExplain.RouteExplanation`, `Ship.ShipDecision`, `ReleaseReport.ReleaseReport`,
`CacheEligibility.CacheEligibilityReport`) and projects it to its deterministic, `schemaVersion`-headed `*.json`
**contract**. F27 added a *second* projection of those same objects — `HumanText` (pure, ANSI-free plain text built
from a shared `ReportView`) plus `HumanRender` (the Spectre rich renderer, the read-only `watch` debounce MVU, and
the read-only `tui` navigation MVU) — but those libraries are exercised **only by their own tests**. No command a
human runs has changed: each host still emits its own ad-hoc console summary inside `Loop.renderText`, there is no
color/table view, and there is no `watch` or `tui` surface.

This feature **wires the F27 libraries into the hosts**, in priority order:

- **Per-command plain delegation (P1, MVP).** Each command host's human (text) render branch is **replaced by a
  delegation** to the matching `HumanText.of*` over the **same** report object the host already resolved — `route`
  → `ofRouteResult`, `ship` → `ofShipDecision`, `verify` → `ofVerifyDecision`, `release` → `ofReleaseReport`,
  `explain` → `ofRouteExplanation`, `evidence` → `ofCacheEligibilityReport`. The host's own operational lines
  (e.g. `wrote <path>` confirmations) are preserved as host output **around** the report projection, distinct from
  it. The persisted `*.json` contracts are written by the existing `*Json` path and stay **byte-identical**. This
  is the single-source-of-truth payoff F27's roadmap exit criterion demands: the human view and the JSON are now
  provably two projections of one report value.
- **Render-mode dispatch with capability sensing (P2).** A capability-sensing effect at the CLI host edge reads
  `IsTty` / `NO_COLOR` / explicit `--plain` / terminal `Width` into a `RenderMode.ColorCapability`; the pure
  `RenderMode.selectMode` (unchanged) selects `Json` / `Plain` / `Rich`; the host routes to the existing `*Json`
  path / `HumanText` / `HumanRender.RichRender.emit` accordingly. `--json` always wins to the byte-identical
  contract; non-TTY / `NO_COLOR` / `--plain` degrade to the exact `HumanText` plain string; color/ANSI appear only
  in interactive output.
- **`watch` surface (P2).** A `watch` subcommand on the multi-subcommand dispatcher (and a `--watch` flag on the
  packed standalone exes) drives `HumanRender.Watch.run` over `route` / `evidence` / `verify`-check, re-running the
  existing evaluation and re-rendering on working-tree change, debounced. Read-only: no verdict, gate, exit-code,
  or contract change. A transiently-unreadable tree surfaces the `InputUnreadable` signal, superseded by the next
  settled re-render.
- **`tui` surface (P3).** A `tui` subcommand drives `HumanRender.Tui.run` over the `ReportView` projected from the
  same report object, letting an operator navigate selected gates, proof/explanation trees, blockers, and evidence
  references. Read-only — no verdict, gate, or contract change.

This feature **adds no new library, no new report object, no new verdict/rule, no exit-code change, and no JSON
schema/contract change** — the F27 libraries and the F18–F26 cores are reused verbatim. Its surface is exactly the
new public CLI command/flag vocabulary (`--plain`, `--watch`, the `watch`/`tui` subcommands) and the host-edge
wiring; every existing `route`/`ship`/`verify`/`release`/`evidence` JSON golden stays byte-identical.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every command's human output is a delegation to the shared `HumanText` projection (Priority: P1)

An operator runs an `fsgg` command (`route`, `ship`, `verify`, `release`, `explain`, or `evidence`) in a terminal
and reads a human-readable plain-text view that is now produced by the shared `HumanText.of*` projection of the
**same** report object the command resolved — not the host's old ad-hoc summary. The command's own operational
confirmations (e.g. "wrote <path>") still appear as host output, clearly distinct from the report projection. The
same command with `--json` writes the unchanged, byte-identical `*.json` contract. The human view and the contract
are now demonstrably two projections of one report value.

**Why this priority**: This is the wiring that actually delivers F27's headline value ("operator UX improves
without creating a second source of truth"). Until a host delegates to `HumanText`, the library is dead code and
the human view can still silently diverge from the contract. It delivers value alone: even before color, watch, or
a TUI, every command's human view becomes the disciplined shared projection. It is the MVP.

**Independent Test**: For each command, run it against a fixture working tree with and without `--json`. The
no-`--json` human output contains the `HumanText.of*` projection of the resolved report object (verbatim, no ANSI
escapes) plus any host operational lines; the `--json` run's persisted contract is byte-identical to the pre-wiring
golden. A report-object-identity check confirms the host passes the *same* report value to both `HumanText.of*` and
the `*Json` projection.

**Acceptance Scenarios**:

1. **Given** a fixture working tree, **When** `fsgg route` runs without `--json`, **Then** its human output
   contains `HumanText.ofRouteResult` over the resolved `RouteResult` (+ its `CacheEligibilityReport option` +
   `(GateId*GateOutcome) list`), with no ANSI escapes; the host's operational "wrote <path>" confirmations remain
   present and distinct from the report projection.
2. **Given** the same working tree, **When** any of `route`/`ship`/`verify`/`release`/`explain`/`evidence` runs
   with its JSON option, **Then** the persisted `*.json` contract is **byte-identical** to the pre-wiring golden.
3. **Given** one command invocation, **When** it renders both human and JSON, **Then** the human projection and the
   JSON contract are derived from the **same** report value (report-object identity) — the human view is not a
   separately-computed summary.
4. **Given** a blocked outcome, **When** the human view is read, **Then** the blocking reason(s) and the process
   exit status are stated by the `HumanText` projection consistent with the report object's verdict and exit-code
   basis — never softened, never separately derived.
5. **Given** a missing/malformed **input** (e.g. an unparseable config in a one-shot `route`/`evidence` run),
   **When** the command renders, **Then** it surfaces a clear input signal distinct from a tool defect — no
   swallowed error, no crash, no fabricated report — and exits with the established input-unavailable code.

---

### User Story 2 - Interactive terminals get the rich view; non-interactive contexts degrade to plain (Priority: P2)

In an interactive terminal the operator gets `HumanRender`'s rich rendering — a color-coded verdict banner and
grouped gate/finding/blocker tables conveying the same facts as the plain/JSON views — selected automatically from
the sensed terminal capability. When stdout is not a TTY, when `NO_COLOR` is set, or when `--plain` is passed, the
command degrades to the exact `HumanText` plain string with no ANSI escapes. `--json` always overrides to the
byte-identical contract regardless of terminal state.

**Why this priority**: Rich rendering makes a blocked gate scannable at a glance, but it depends on the P1 plain
delegation being the faithful baseline and safe fallback, and on capability sensing being wired at the host edge.
Keeping ANSI strictly out of JSON / non-TTY output is the boundary that protects the automation contract. It is
independently testable over a Spectre `TestConsole` across TTY / non-TTY / `NO_COLOR` / `--plain` / width fixtures.

**Independent Test**: Drive each command's render dispatch with a sensed `ColorCapability` over a recording Spectre
console. A TTY capability yields the color banner + tables; a non-TTY / `NO_COLOR` / `--plain` capability yields the
ANSI-free `HumanText` plain string byte-equal to `HumanText.of*`; `--json` requested in any terminal state yields
ANSI-free, byte-identical JSON that never reaches the rich renderer; a narrow/unknown width reflows or truncates
cleanly.

**Acceptance Scenarios**:

1. **Given** a sensed interactive (TTY, color-enabled, no `--plain`) capability, **When** a command renders,
   **Then** `RenderMode.selectMode` selects `Rich` and the host calls `HumanRender.RichRender.emit` to show the
   color banner + grouped tables.
2. **Given** a non-TTY, `NO_COLOR`, or `--plain` capability, **When** a command renders, **Then** `selectMode`
   selects `Plain` and the host writes the exact `HumanText.of*` string with no ANSI escapes.
3. **Given** `--json` requested, **When** a command runs in any terminal/color state, **Then** `selectMode` selects
   `Json`, the rich renderer is never invoked, and the JSON is ANSI-free and byte-identical to the pre-wiring
   golden.
4. **Given** a very narrow or unknown terminal width, **When** the rich view renders, **Then** it reflows or
   truncates cleanly (unknown width falls back to the safe default) without corrupting the layout.

---

### User Story 3 - `watch` re-renders route/evidence/check reports on change, debounced and read-only (Priority: P2)

An operator iterating locally runs the `watch` surface (`fsgg watch`, resolved to the dispatcher tool, or
`fsgg route --watch` on the packed standalone) over a working tree. The host drives `HumanRender.Watch.run`: it
senses working-tree changes, re-runs the existing route/evidence/check evaluation, and re-renders the report —
**debounced** so a burst of edits yields a single settled re-render. The session changes no verdict, evaluates no
new rule, and emits no new contract. A change arriving mid-edit (a transiently-unreadable tree) surfaces the
`InputUnreadable` signal and is superseded by the next settled re-render.

**Why this priority**: It sharpens the inner-loop UX but depends on the P1/P2 render path already existing as the
thing being re-rendered, and on the dispatcher gaining a subcommand. The pure debounce MVU already exists (F27);
this story wires its interpreter edge (`Watch.run`) and the command surface, and adds the end-to-end settle
coverage F27 left as `[PARTIAL]`. Independently testable via the pure `Watch.update` debounce and a
real-`FileSystemWatcher` settle over a temp tree.

**Acceptance Scenarios**:

1. **Given** a watch session over a working tree, **When** a tracked file changes, **Then** the route/evidence/
   check report is re-evaluated and re-rendered reflecting the new state.
2. **Given** a rapid burst of change events within the debounce window, **When** they arrive, **Then** the view
   re-renders **once** after the burst settles — not once per event.
3. **Given** a watch session, **When** it is running, **Then** it changes no governance verdict, evaluates no new
   rule, changes no exit-code scheme, and emits no new automation contract.
4. **Given** a transiently-unreadable/partial tree during a re-render, **When** the change is sensed, **Then** the
   view surfaces a clear `InputUnreadable` input signal (no crash, no fabricated report), superseded by the next
   settled re-render.

---

### User Story 4 - Optional `tui` navigates the report objects, read-only (Priority: P3)

An operator who wants to explore a report interactively runs the `tui` surface, which drives `HumanRender.Tui.run`
over the `ReportView` projected from the same report object the plain/JSON views use — navigating selected gates,
proof/explanation trees, blockers/warnings, and evidence/provenance references. It is strictly read-only: it never
changes a verdict, runs a new gate, or emits a contract.

**Why this priority**: The TUI is the richest operator surface but the least essential and explicitly "optional" in
the roadmap; it depends on every projection beneath it and on the dispatcher command plumbing from Story 3. The
pure navigation MVU already exists (F27); this story wires its interpreter edge and the `tui` subcommand.
Independently testable in that its `View` is the same `ReportView` the other surfaces project (report-object
parity) and the pure `Tui.update` changes only navigation state.

**Acceptance Scenarios**:

1. **Given** a command's report object, **When** the `tui` surface launches, **Then** its `View` is the
   `ReportView` projected from the **same** report object the plain/JSON views use — never separately derived.
2. **Given** a `tui` session, **When** the operator navigates (move/expand/collapse/quit), **Then** no governance
   verdict changes, no new gate runs, no exit-code scheme changes, and no automation contract is emitted.

---

### Edge Cases

- **Not a TTY / piped output**: when stdout is redirected or piped, the dispatch selects `Plain` and emits the
  ANSI-free `HumanText` projection, so captured output stays clean.
- **Color disabled / explicit plain**: `NO_COLOR` or `--plain` yields `Plain` with no ANSI escapes in any human
  view.
- **`--json` always wins**: requesting JSON yields the deterministic, ANSI-free, byte-identical contract regardless
  of terminal/color state; the rich renderer is never reached.
- **Host operational lines vs. report projection**: a host's own output (e.g. "wrote <path>" confirmations,
  changed-path counts) is preserved as host output distinct from the `HumanText` projection — it never leaks into
  the JSON contract and never alters the report projection.
- **Very narrow / unknown terminal width**: the rich view reflows or truncates cleanly; an unknown width falls back
  to the safe default rather than failing.
- **Watch debounce burst**: a multi-file save yields exactly one settled re-render, not one per file event.
- **Watch on a transiently-unreadable tree**: an unreadable/partial mid-edit input surfaces `InputUnreadable` and
  is superseded by the next settled re-render — never a crash, never a fabricated report.
- **`fsgg watch`/`fsgg tui` host resolution**: the generic spelling resolves to the multi-subcommand dispatcher
  tool (`fsgg-governance watch`/`tui`) until a future single-tool unification; the `--watch`/`--plain` flags also
  attach to the packed standalone exes through the same shared `HumanRender` edge.
- **Empty / clean report**: a command with nothing to report renders a clear "nothing to report / clean" human
  view, and its JSON stays byte-identical to the clean-state golden.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each command host (`route`, `ship`, `verify`, `release`, `explain`, `evidence`) MUST replace its
  ad-hoc human (text) render branch with a **delegation** to the matching `HumanText.of*` over the **same** report
  object the host already resolved — the human view and the JSON contract MUST be derived from one report value
  (single source of truth), never separately computed. **Scope this row (plan.md D2):** the feasible subset whose
  held report object already matches `HumanText.of*` — `route`/`ship`/`verify`/evidence(`CacheEligibilityCommand`) —
  is delegated now; `release` human delegation (← the deferred F26 `ReleaseReport` assembly thread) and
  `explain` + legacy-`Cli` `evidence` (no matching F19 `RouteExplanation` / F41 `CacheEligibilityReport` surfaced
  yet) are **explicit, bounded deferrals**, scoped in plan.md and contracts/cli-surface.md, not pre-empted here.
- **FR-002**: Each host's persisted `*.json` contract MUST remain **byte-identical** to its pre-wiring golden for
  identical repository state — the wiring changes presentation only; it MUST NOT change any report object,
  governance verdict, rule evaluation, exit-code scheme, or JSON schema/contract (F18–F27 reused verbatim).
- **FR-003**: The host's own **operational output** (e.g. "wrote <path>" confirmations, changed-path counts) MUST
  be preserved as host output **distinct from** the `HumanText` report projection; it MUST NOT leak into the JSON
  contract and MUST NOT alter the report projection.
- **FR-004**: A capability-sensing effect at the CLI host edge MUST read `IsTty` / `NO_COLOR` / explicit `--plain` /
  terminal `Width` into a `RenderMode.ColorCapability`; the existing pure `RenderMode.selectMode` MUST remain
  unchanged and be the sole decider of `Json` / `Plain` / `Rich`. Sensing MUST occur only at the interpreter edge,
  not in any pure function.
- **FR-005**: Each host MUST route rendering by the selected `RenderMode`: `Json` → the existing `*Json` path
  (always overriding); `Plain` → the exact `HumanText.of*` string; `Rich` → `HumanRender.RichRender.emit`. Color,
  tables, and ANSI escapes MUST appear **only** in `Rich` (interactive) output and MUST NOT appear in `Json`,
  non-TTY/piped output, `NO_COLOR`, or `--plain` output.
- **FR-006**: The rich path MUST be **terminal-width resilient** — reflowing or truncating cleanly on narrow
  terminals and falling back to the safe default for an unknown width — without corrupting layout (reusing the F27
  `RichRender` behavior).
- **FR-007**: The multi-subcommand dispatcher MUST add a read-only **`watch` subcommand**, and the packed
  standalone exes MUST accept a **`--watch` flag**, both driving `HumanRender.Watch.run` over the route/evidence/
  check report (where "check" = the `verify` gate-check `Ship.ShipDecision`); the watch path MUST re-run the
  existing evaluation and re-render on working-tree change, debounced so a burst within the debounce window yields a
  single re-render.
- **FR-008**: The dispatcher MUST add an optional read-only **`tui` subcommand** driving `HumanRender.Tui.run` over
  the `ReportView` projected from the same report object the other surfaces use.
- **FR-009**: The `watch` and `tui` surfaces MUST be **read-only**: they MUST NOT change any governance verdict,
  evaluate any new rule, change any exit-code scheme, or emit any new automation contract — they only re-run the
  existing evaluation and re-project the existing report.
- **FR-010**: Every wired surface (one-shot plain/rich **and** watch) MUST distinguish a missing/malformed
  **input** (an unparseable config in a one-shot render, or a transiently-unreadable working tree during a watch
  re-render) from a **tool defect**, surfacing a clear input signal with no swallowed error, no crash, and **no
  fabricated report** — superseded by the next settled re-render where applicable (safe-failure, as F14–F27); a
  blocked verdict MUST be rendered as blocked, never softened.
- **FR-011**: The presentation dependency boundary MUST be preserved: the host exes MUST reach Spectre **only**
  through `FS.GG.Governance.HumanRender`; no command host, no pure core, no `*Json` library, and `HumanText` MUST
  add a direct Spectre reference. Each wired host MUST reference `HumanText` (for the plain projection) and, where
  it renders rich/watch/tui, `HumanRender`.
- **FR-012**: The new public CLI vocabulary (`--plain`, `--watch`, the `watch`/`tui` subcommands) MUST be reflected
  in the affected hosts' curated `.fsi` and surface baselines, and documented in the CLI docs/README (render modes,
  `--plain`/`NO_COLOR`/TTY behavior, `watch`/`tui`, host resolution), noting plain/rich are non-contractual and
  JSON is the only contract.
- **FR-013**: A full-suite build + test gate MUST pass with every pre-wiring JSON golden byte-identical (SC-002)
  and the F27 plain-text smoke snapshots stable, across the whole solution.

### Key Entities *(include if data involved)*

- **Report object** (reused, F18–F26): the immutable, presentation-free value each command resolves to — the
  single source of truth the host hands to both `*Json` and `HumanText`.
- **Command host** (extended): each `fsgg` command's `Loop`/`Interpreter`/`Program` MVU edge — its text render
  branch is replaced by a `HumanText` delegation and (where interactive) a render-mode dispatch; its operational
  output and persisted JSON path are preserved.
- **`HumanText.of*` projection** (reused, F27): the pure ANSI-free plain projection per report object the host now
  delegates to.
- **`ColorCapability` / `RenderMode` / `selectMode`** (reused, F27): the sensed terminal capability, the render
  mode, and the pure total selector (`Json` always wins) the host edge fills and consults.
- **`HumanRender.RichRender.emit` / `Watch.run` / `Tui.run`** (reused, F27): the Spectre rich renderer and the
  watch/tui interpreter edges the host drives; the sole owners of the Spectre dependency.
- **`watch` / `tui` subcommands and `--plain`/`--watch` flags** (new): the read-only CLI vocabulary added to the
  dispatcher and packed exes — new public surface, no new contract.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every command **wired this row** (`route`, `ship`, `verify`, evidence via
  `CacheEligibilityCommand`), the no-`--json` human output contains the matching `HumanText.of*` projection of the
  resolved report object and the `--json` contract conveys the same verdict/blockers/exit status — verified by
  per-command host parity tests proving both derive from one report value. (`release`/`explain`/legacy-`Cli`
  `evidence` are scoped deferrals per FR-001 — not measured here.)
- **SC-002**: Every pre-wiring `*.json` golden of a wired host (`route`/`gates`, `audit`(ship), `verify`,
  `cache-eligibility`) is byte-identical after the wiring for identical repository state — verified by
  golden-stability tests for every wired command host. The unwired hosts' goldens (incl. `release.json`) are
  untouched by construction and covered indirectly by the full-suite gate (SC-008).
- **SC-003**: Human (plain/rich) output contains **no** ANSI escapes in any non-interactive context (non-TTY,
  `NO_COLOR`, `--plain`, `--json`); color/tables appear only when an interactive capability is sensed — verified by
  ANSI-free and degrade-to-plain tests over a recording console across TTY/non-TTY/`NO_COLOR`/`--plain` fixtures.
- **SC-004**: `--json` selects `Json` and never reaches the rich renderer in any terminal/color state, and its
  output stays ANSI-free and byte-identical — verified by a JSON-always-wins dispatch test.
- **SC-005**: A burst of change events within the debounce window produces exactly **one** watch re-render after
  the window settles, and a real-`FileSystemWatcher` settle over a temp tree re-renders end to end — verified by
  the pure debounce fixture plus the end-to-end settle test (closing F27's `[PARTIAL]`).
- **SC-006**: The `watch` and `tui` surfaces change no governance verdict, evaluate no new rule, change no
  exit-code scheme, and emit no new automation contract — verified by read-only-projection tests.
- **SC-007**: No command host, pure core, `*Json` library, or `HumanText` references Spectre directly; only
  `FS.GG.Governance.HumanRender` does — verified by a project-reference/dependency-boundary check across the wired
  hosts.
- **SC-008**: The full-solution build + test sweep is green with all JSON goldens byte-identical and the F27 smoke
  snapshots stable — verified by the full-suite gate.

## Assumptions

- **Next-item resolution**: per the user's pointer to `docs/initial-implementation-plan.md` and the currency check,
  the detailed F-row plan ends at F27/M10 with no new feature queued; the remaining work is three deferred
  host-wiring passes, and the selected next item is the **F27 human-projection host wiring** (the user confirmed
  this over the F26 release and F25 cost-cache wiring threads). The new spec directory is
  `063-human-projection-host-wiring` (sequential).
- **Plan currency**: `docs/initial-implementation-plan.md` is current for the F25–F27 narrative and explicitly
  documents this wiring as F27's "Remaining (the host-wiring pass)". One stale spot — Phase 10's rows still read
  🔴 though specs `058`/`059` shipped that expansion — is **out of scope** here and flagged for a separate
  plan-maintenance fix.
- **F27 libraries are reused verbatim**: `HumanText` (`RenderMode`/`HumanText`/`ReportView`) and `HumanRender`
  (`RichRender`/`Watch`/`Tui`) are built, green, and surface-baselined. This row adds **no** new library and
  changes **no** F27 or F18–F26 module — it only consumes their public surfaces at the host edges.
- **JSON is the only contract; human views are non-contractual**: the persisted `*.json` outputs stay the
  deterministic, ANSI-free, byte-identical contract; plain/rich text is held only to the F27 smoke-snapshot
  stability (FR-002, FR-012, SC-002).
- **Console summary vs. persisted contract**: each host's existing `Loop.renderText`/`renderJson` is a host-context
  console **summary** (changed-path counts, "wrote …" lines) distinct from the persisted `*.json` written by the
  `*Json` libraries; the wiring delegates the **report** portion to `HumanText` while preserving the host's
  operational lines as host output (FR-003). This separation is why F27 deferred the pass.
- **Host resolution**: the packed `fsgg` is `FS.GG.Governance.RouteCommand` (route-only); the multi-subcommand
  dispatcher is `FS.GG.Governance.Cli`, packed as `fsgg-governance`. The new `watch`/`tui` **subcommands** are
  added to the dispatcher; the `--watch`/`--plain` **flags** also attach to the packed standalone exes through the
  shared `HumanRender` edge. "`fsgg watch`/`fsgg tui`" is the generic spelling, resolving to
  `fsgg-governance watch/tui` until a future single-tool unification.
- **Legacy `Cli` dispatch**: the dispatcher's `explain`/`evidence` paths are wired to `HumanText.ofRouteExplanation`
  / `ofCacheEligibilityReport`; where the legacy `Cli` is built on older Kernel/Host `Route`/`Explanation` types
  rather than the F19–F26 report objects, the wiring adapts at the dispatch site without changing the report
  objects or the JSON. The standalone `FS.GG.Governance.CacheEligibilityCommand` evidence host is wired alongside
  the dispatcher's `evidence` path.
- **Capability sensing is an effect**: TTY/`NO_COLOR`/`--plain`/width detection happens only at the host
  interpreter edge; `selectMode` stays pure and unchanged (FR-004).
- **Safe failure preserved**: a missing/malformed input in a one-shot render, or a transiently-unreadable tree
  during a watch re-render, surfaces a clear input signal distinct from a tool defect, with no crash and no
  fabricated report, superseded by the next settled re-render (FR-010), consistent with F14–F27.
- **Tier**: **Tier 1 (contracted change).** It adds new public CLI command/flag vocabulary (`--plain`, `--watch`,
  the `watch`/`tui` subcommands) and changes affected hosts' public `.fsi`/surface baselines — forcing the full
  chain: curated `.fsi` for changed host surfaces, re-blessed surface baselines, real test evidence, and docs. It
  adds **no** new dependency (Spectre is already pinned and owned by `HumanRender`), **no** new report object,
  verdict, exit-code, or JSON-schema/contract change — every existing JSON golden stays byte-identical. (Confirmed
  at `/speckit-plan`.)
