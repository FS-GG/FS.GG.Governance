# Feature Specification: Human Projections — Plain Text, Spectre.Console, Watch, and Optional TUI Over the Immutable Report Objects

**Feature Branch**: `062-human-projections-watch-tui`

**Created**: 2026-06-25

**Status**: Planned

**Input**: User description: "next item in plan" — roadmap **F27 ·
`027-human-projections-watch-tui`** (`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`), the
next unimplemented row after **F26 (`061-verify-release-provenance`)** merged on 2026-06-25. F26 completed the
publication boundary, leaving every `fsgg` command resolving to an **immutable, presentation-free report object**
(route, ship, verify, release, and the evidence/audit/provenance snapshots) that today is rendered for automation
as deterministic JSON. This row gives **humans** first-class views of those same report objects — plain text by
default, optional Spectre.Console color/tables, a `watch` projection, and an optional TUI — **without creating a
second source of truth**. Per the roadmap: "operator UX improves without creating a second source of truth."

Three scope decisions are confirmed for this feature (the requester advanced from F26 to the next row, F27, after
F26 merged — confirming the human-projection scope over further publication/provenance work):

1. **This row adds human *projections* of the existing report objects — it does not change the reports, the
   verdicts, the exit codes, or the JSON contracts.** Every `fsgg` command (`route`, `explain`, `evidence`,
   `verify`, `ship`, `release`) already resolves to an immutable, presentation-free report value that today is
   projected to deterministic, `schemaVersion`-headed JSON (the automation contract). F27 reuses **all of that
   unchanged** and adds presentation-only renderers over the **same** report objects. No verdict is re-derived,
   no exit-code basis changes, and the JSON projections stay byte-identical.

2. **Plain text and rich (Spectre.Console) views are human-readable but *non-contractual*; JSON remains the only
   contract.** The plain-text and colorized renderings are for operators reading a terminal; they may evolve in
   wording and layout (held only to *smoke-snapshot stability*, not byte-for-byte contract), whereas `--json`
   output stays deterministic, presentation-free, and ANSI-free. Color, tables, spinners, and ANSI escapes
   appear **only** in interactive terminal output and **never** leak into JSON or non-TTY/piped output.

3. **`watch` and the optional TUI are added projections, never new sources of truth or new gates.** A `watch`
   mode re-renders the route/evidence/check report for a working tree as it changes (debounced); an optional TUI
   navigates the same report objects. Neither changes any governance verdict, evaluates any new rule, or emits a
   new automation contract — they are read-only views that re-run the existing pure evaluation and re-project its
   report.

## Overview

After F26, every `fsgg` command produces an **immutable, presentation-free report object** — the route result,
the ship/verify/release reports, and the evidence/audit/provenance/attestation snapshots — and renders it to
deterministic JSON for automation. What is missing is a **first-class human experience** over those same objects:

- Today the human-facing terminal output for each command is ad-hoc plain text emitted alongside the JSON path,
  rather than a **disciplined projection** of the immutable report object shared with JSON. There is no single
  rendering surface guaranteeing that what a human reads and what automation consumes come from the **same**
  report value.
- There is **no rich (Spectre.Console) projection** — color, tables, grouped findings, exit-status banners —
  that makes a blocked gate or a failing release precondition scannable at a glance in an interactive terminal,
  while staying strictly out of the JSON path and out of non-TTY output.
- There is **no `watch` projection**: an operator iterating locally must re-run `fsgg route`/`evidence`
  repeatedly by hand; there is no debounced, re-rendering view that reflects working-tree changes as they happen.
- There is **no optional TUI** for navigating a report (selected gates, proof/explanation trees, blockers,
  evidence references) interactively.

This feature closes that gap, in priority order:

- **Plain-text projections from the immutable report objects.** Every command's human output is a **disciplined
  plain-text projection of the same report object** that produces its JSON — route, evidence, verify, ship, and
  release. The plain-text view is human-readable and **non-contractual** (it may change), held to smoke-snapshot
  stability; the JSON stays the deterministic, presentation-free contract. This is the central new value and the
  P1 slice: **one source of truth, two projections.**
- **Spectre.Console rich rendering in the CLI only.** A rich projection adds color, tables, grouped
  findings/blockers, and an exit-status banner for interactive terminals — **terminal-width resilient** and
  degrading cleanly when the terminal is narrow, not a TTY, or color is disabled. ANSI/color **never** appears in
  JSON or piped/non-interactive output.
- **`watch` projection over route/evidence/check reports.** A `watch` mode re-runs the existing evaluation and
  re-renders the report for a working tree as it changes, **debounced** so a burst of edits yields a single
  re-render. It is a read-only view: no verdict, gate, or contract changes.
- **Optional TUI over the same reports.** An optional interactive TUI lets an operator navigate the report
  objects (selected gates, proof/explanation trees, blockers, evidence references). It is strictly a view over
  the existing reports.

This feature does **not** change any report object, governance verdict, rule evaluation, exit-code scheme, or
JSON schema/contract (F18–F26, reused), does **not** make plain text or rich output a contract, and adds a
presentation dependency (a terminal-rendering library) **only** in the CLI host — never in any pure core or the
JSON projections. It supplies disciplined human projections of the existing immutable report objects so operator
UX improves without creating a second source of truth.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Human plain-text view rendered from the same report object as JSON (Priority: P1)

An operator runs an `fsgg` command (`route`, `evidence`, `verify`, `ship`, or `release`) in a terminal and reads
a clear, human-readable **plain-text** summary of the outcome — the verdict, the selected gates / unmet
preconditions, the blockers and warnings, and the exit status — and the **same** command with `--json` produces
the deterministic automation contract. Both views are projections of the **same immutable report object**: the
human reads exactly what automation acts on, never a separately-computed summary. The plain-text wording/layout
is human-readable and may evolve (it is non-contractual), but it is held stable enough that a smoke snapshot
catches accidental regressions.

**Why this priority**: "Render route, evidence, verify, ship, and release reports from immutable report objects"
and "Preserve plain text as human-readable but non-contractual output" are the row's defining acts — they
guarantee the single-source-of-truth property the roadmap exit criterion demands ("without creating a second
source of truth"). Without this, the human and automation views can silently diverge. It delivers value alone:
even before color, watch, or a TUI, an operator gets a faithful plain-text view of every report.

**Independent Test**: For each command, run it against a fixture working tree both with and without `--json`.
Confirm the plain-text view reports the same verdict, blockers, and exit status as the JSON, that both are
derived from one report object (report-object parity), that the plain text contains **no** ANSI/color escapes,
and that the plain-text output matches a stable smoke snapshot for identical repository state.

**Acceptance Scenarios**:

1. **Given** a fixture working tree, **When** a command runs without `--json`, **Then** it prints a
   human-readable plain-text projection of the command's report object (verdict, selected gates / unmet
   preconditions, blockers, warnings, exit status), containing no ANSI/color escape sequences.
2. **Given** the same working tree, **When** the command runs with `--json`, **Then** the JSON is the existing
   deterministic, presentation-free contract — byte-identical to the pre-F27 golden — and conveys the same
   verdict and blockers as the plain text (report-object parity).
3. **Given** identical repository state, **When** a command's plain-text view is rendered twice, **Then** the
   output is identical and matches its committed smoke snapshot.
4. **Given** a blocked outcome, **When** the plain-text view is read, **Then** the blocking reason(s) and the
   process exit status are explicit and match the report object's verdict and exit-code basis — never a softened
   or separately-derived summary.

---

### User Story 2 - Rich Spectre.Console rendering for interactive terminals (Priority: P2)

In an interactive terminal, the operator gets a **rich** rendering of the same report — color-coded verdict
banner, grouped tables of selected gates / findings / blockers, and clearly highlighted failures — so a blocked
gate or failing release precondition is scannable at a glance. The rich rendering is **terminal-width resilient**
(it reflows or truncates cleanly on a narrow terminal) and degrades to plain text when output is not a TTY, when
color is disabled (e.g. `NO_COLOR`), or when explicitly requested as plain. Color and ANSI escapes appear
**only** in interactive terminal output and **never** in JSON or piped/redirected output.

**Why this priority**: "Add Spectre.Console projections in CLI only" makes governance outcomes legible fast, but
it builds on the plain-text projection (Story 1) being the faithful baseline and the safe fallback. Keeping ANSI
strictly out of JSON/non-TTY output is the safety boundary that protects the automation contract. It is
independently testable against width and TTY/no-color fixtures.

**Acceptance Scenarios**:

1. **Given** an interactive terminal, **When** a command renders its report richly, **Then** it shows a
   color-coded verdict banner and grouped tables of gates/findings/blockers conveying the same information as the
   plain-text and JSON projections.
2. **Given** output redirected to a non-TTY, color disabled (`NO_COLOR`), or plain output explicitly requested,
   **When** the command runs, **Then** it emits the plain-text projection with **no** ANSI/color escapes.
3. **Given** a narrow terminal width, **When** the rich view renders, **Then** it reflows or truncates cleanly
   without corrupting the layout (terminal-width resilience).
4. **Given** any rendering mode, **When** `--json` is requested, **Then** the JSON output contains **no**
   ANSI/color escapes and is byte-identical to the pre-F27 golden.

---

### User Story 3 - `watch` projection re-renders route/evidence/check reports on change (Priority: P2)

An operator iterating locally runs `fsgg ... --watch` (or an `fsgg watch` projection) over a working tree. The
view re-runs the existing route/evidence/check evaluation and re-renders the report whenever the working tree
changes, **debounced** so a rapid burst of edits (e.g. a multi-file save) produces a **single** re-render rather
than one per file event. The watch view is a read-only projection: it changes no verdict, evaluates no new rule,
and emits no new automation contract.

**Why this priority**: "Add `watch` projection over route/evidence/check reports" sharpens the inner-loop UX, but
it depends on the report projections (Stories 1–2) already existing as the thing being re-rendered. Debounce is
the property that makes it usable rather than a flicker storm. It is independently testable with a debounce
fixture that feeds a burst of change events.

**Acceptance Scenarios**:

1. **Given** a watch session over a working tree, **When** a tracked file changes, **Then** the route/evidence/
   check report is re-evaluated and re-rendered reflecting the new state.
2. **Given** a rapid burst of change events within the debounce window, **When** they arrive, **Then** the view
   re-renders **once** after the burst settles — not once per event (debounce).
3. **Given** a watch session, **When** it is running, **Then** it changes no governance verdict, evaluates no new
   rule, and emits no new automation contract — it only re-projects the existing report.

---

### User Story 4 - Optional TUI for navigating the report objects (Priority: P3)

An operator who wants to explore a report interactively launches an optional TUI that navigates the same
immutable report objects — selected gates, proof/explanation trees, blockers and warnings, and evidence/
provenance references — letting them drill into why a gate was selected or a precondition failed. The TUI is
strictly a view over the existing reports; it never changes a verdict, runs a new gate, or emits a contract.

**Why this priority**: An interactive TUI is the richest operator surface but the least essential — it depends on
all the projections beneath it and is explicitly "optional" in the roadmap. It is P3, and independently testable
in that its rendered content is a projection of the same report object the other views use (report-object
parity), with no new verdict or contract.

**Acceptance Scenarios**:

1. **Given** a command's report object, **When** the optional TUI renders it, **Then** the navigable content
   (gates, proof/explanation trees, blockers, evidence references) is a projection of the same report object the
   plain-text/JSON views use — never separately derived.
2. **Given** a TUI session, **When** the operator navigates, **Then** no governance verdict changes, no new gate
   runs, and no automation contract is emitted.

---

### Edge Cases

- **Not a TTY / piped output**: when stdout is redirected or piped, the rich/color projection degrades to
  plain text with no ANSI escapes, so captured output stays clean.
- **Color disabled**: when color is disabled (`NO_COLOR` or an explicit plain flag), no ANSI/color escapes are
  emitted in any human view.
- **Very narrow or unknown terminal width**: the rich view reflows or truncates cleanly and never produces a
  corrupted/overflowing layout; an unknown width falls back to a safe default rather than failing.
- **`--json` always wins for automation**: requesting JSON always yields the deterministic, ANSI-free contract
  regardless of terminal/color state — the human-projection mode never alters the JSON.
- **Watch debounce burst**: a multi-file save (many change events in a short window) yields exactly one
  re-render after the window settles, not one per file.
- **Watch on a transiently-unreadable tree**: a change event arriving mid-edit (an unreadable/partial input)
  surfaces a clear input signal in the view and is superseded by the next settled re-render — never a crash and
  never a fabricated report.
- **Empty / clean report**: a command with nothing to report (no selected gates, no blockers) renders a clear
  "nothing to report / clean" human view, and its JSON stays byte-identical to the clean-state golden.
- **Plain-text wording change**: because plain text is non-contractual, a deliberate wording/layout change
  updates the smoke snapshot only — it never requires a JSON schema bump and never changes a verdict or exit code.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each `fsgg` command (`route`, `explain`, `evidence`, `verify`, `ship`, `release`) MUST render its
  human plain-text view as a **projection of the same immutable, presentation-free report object** that produces
  its JSON — the human view and the JSON MUST NOT be separately computed (single source of truth).
- **FR-002**: The plain-text human view MUST convey the report's verdict, selected gates / unmet preconditions,
  blockers, warnings, and process exit status, consistent with the report object's verdict and exit-code basis;
  a blocked outcome MUST be reported as blocked, never softened.
- **FR-003**: The plain-text and rich human views MUST be **non-contractual** (human-readable, free to evolve in
  wording/layout), while the `--json` output MUST remain the **only contract**: deterministic, presentation-free,
  and byte-identical to the pre-F27 golden for identical repository state.
- **FR-004**: Color, tables, ANSI escapes, and any rich-rendering artifacts MUST appear **only** in interactive
  terminal output; they MUST NOT appear in `--json` output, in non-TTY/piped output, or when color is disabled
  (`NO_COLOR` or an explicit plain mode).
- **FR-005**: The CLI MUST add a rich (Spectre.Console) projection of the report objects (color-coded verdict
  banner, grouped gate/finding/blocker tables) **in the CLI host only**; no pure core and no JSON projection may
  depend on the rich-rendering library.
- **FR-006**: The rich projection MUST be **terminal-width resilient** — reflowing or truncating cleanly on
  narrow terminals and falling back to a safe default for an unknown width — without corrupting layout.
- **FR-007**: The CLI MUST provide a **`watch` projection** over the route/evidence/check report — where
  **"check" denotes the `verify` gate-check report** (the `Ship.ShipDecision` that `verify` projects), so the
  watched triad is `route` (`RouteResult`), `evidence` (`CacheEligibilityReport`), and `check` = `verify`
  (`ShipDecision`) — that re-runs the existing evaluation and re-renders the report when the working tree changes,
  **debounced** so a burst of change events within the debounce window yields a single re-render.
- **FR-008**: The `watch` projection and the optional TUI MUST be **read-only views**: they MUST NOT change any
  governance verdict, evaluate any new rule, change any exit-code scheme, or emit any new automation contract.
- **FR-009**: The CLI MAY provide an **optional TUI** that navigates the same report objects (selected gates,
  proof/explanation trees, blockers, evidence/provenance references); its content MUST be a projection of the
  same report object the plain-text/JSON views use.
- **FR-010**: The human projections MUST NOT change any report object, governance verdict, rule evaluation,
  exit-code scheme, or JSON schema/contract established in F18–F26 — they are additive, presentation-only views.
- **FR-011**: The plain-text human views MUST be **deterministic for identical repository state** (stable
  ordering, normalized paths, no wall-clock/username/environment dependence in the rendered content) so a
  committed **smoke snapshot** is stable and catches accidental regressions; the rich view MUST convey the same
  information as the plain-text view.
- **FR-012**: Every human projection (plain-text, rich, **and** watch) MUST distinguish a missing/malformed
  **input** (e.g. an unparseable config surfaced in a one-shot `route`/`evidence` render, or a transiently
  unreadable working tree during a watch re-render) from a **tool defect**, surfacing a clear input signal in the
  view with no swallowed errors, no crash, and **no fabricated report** — superseded by the next settled
  re-render where applicable (safe-failure, as F14–F26).
- **FR-013**: The presentation/rendering dependency MUST be confined to the **CLI host project**; the pure cores
  and the JSON projection libraries MUST add **no** presentation dependency, preserving the existing layering.

### Key Entities *(include if data involved)*

- **Report object** (reused, F18–F26): the immutable, presentation-free value each command resolves to (route
  result, ship/verify/release report, evidence/audit/provenance/attestation snapshot) — the single source of
  truth that both JSON and the human projections render.
- **Plain-text projection**: a deterministic, ANSI-free, human-readable rendering of a report object — non-
  contractual but smoke-snapshot-stable; the safe fallback for every non-interactive context.
- **Rich (Spectre.Console) projection**: a color/table rendering of a report object for interactive terminals —
  terminal-width resilient, confined to the CLI host, never present in JSON or non-TTY output.
- **Watch projection**: a debounced, read-only re-render of the route/evidence/check report as a working tree
  changes — re-runs the existing evaluation, emits no new contract. Here **"check" = the `verify` gate-check
  report** (`Ship.ShipDecision`); the watched triad is `route` / `evidence` / `verify`-check, all existing report
  objects (no new "check" report object is introduced).
- **Optional TUI**: an interactive navigable view over the same report objects (gates, proof trees, blockers,
  evidence references) — read-only, no new verdict or contract.
- **Render mode**: the selection among JSON (contract), plain text (default human / non-TTY fallback), and rich
  (interactive terminal), with color governed by TTY/`NO_COLOR`/explicit-plain — JSON always overrides.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every command (`route`, `explain`, `evidence`, `verify`, `ship`, `release`), the plain-text
  human view and the `--json` output convey the same verdict, blockers, and exit status and are derived from one
  report object — verified by report-object parity tests across all commands. (`explain` projects the
  `RouteExplanation` it already derives from the route result; its parity is measured like the others.)
- **SC-002**: `--json` output contains **no** ANSI/color escapes and is byte-identical to the pre-F27 golden for
  identical repository state — verified by ANSI-free-JSON and golden-stability tests for every command.
- **SC-003**: Each command's plain-text view is deterministic for identical repository state and matches its
  committed **smoke snapshot** — verified by stable plain-text smoke-snapshot tests.
- **SC-004**: The rich projection renders cleanly across a range of terminal widths (including very narrow) and
  degrades to ANSI-free plain text on a non-TTY, with `NO_COLOR`, or in explicit plain mode — verified by
  terminal-width-resilience and TTY/no-color fixtures.
- **SC-005**: A burst of change events within the debounce window produces exactly **one** watch re-render after
  the window settles, not one per event — verified by a watch debounce fixture.
- **SC-006**: The watch projection and the optional TUI change no governance verdict, evaluate no new rule,
  change no exit-code scheme, and emit no new automation contract — verified by read-only-projection tests.
- **SC-007**: The presentation/rendering dependency is present **only** in the CLI host project; no pure core or
  JSON projection library references it — verified by a project-reference/dependency-boundary check.
- **SC-008**: A deliberate plain-text wording/layout change updates only the smoke snapshot and requires **no**
  JSON schema bump and changes **no** verdict or exit code — verified by demonstrating a plain-text change with
  every JSON golden left byte-identical.

## Assumptions

- **Next-item resolution**: "next item in plan" is roadmap **F27 · `027-human-projections-watch-tui`**, the next
  unimplemented row after F26 (`061-verify-release-provenance`) merged on 2026-06-25. F27's roadmap dependencies
  are F21 (readiness-report-suite) and F26; F26 is implemented, and the immutable report objects it depends on
  (route result, ship/verify/release reports, evidence/audit/provenance/attestation snapshots) exist and are
  rendered to deterministic JSON today. The new spec directory is `062-human-projections-watch-tui` (sequential),
  independent of the roadmap's `027-` row id.
- **Report objects and verdicts are reused unchanged**: every command already resolves to an immutable,
  presentation-free report object projected to deterministic JSON. This row adds **presentation-only** human
  projections over those objects — it does **not** add or change a report object, a verdict, a rule, an exit-code
  scheme, or a JSON schema/contract (F18–F26 reused).
- **JSON is the only contract; human views are non-contractual**: `--json` output stays the deterministic,
  presentation-free, ANSI-free automation contract held byte-identical against goldens; the plain-text and rich
  views are human-readable and free to evolve, held only to smoke-snapshot stability (FR-003, SC-002, SC-003,
  SC-008).
- **Rendering is a CLI-host concern**: the rich-rendering dependency (Spectre.Console or equivalent) lives **only**
  in the CLI host; pure cores and JSON projections add no presentation dependency, preserving the existing
  layering (FR-005, FR-013, SC-007). The MVU boundary stays at the CLI edge (roadmap F27 MVU note).
- **Color/TTY discipline**: color and ANSI appear only in interactive terminal output; non-TTY/piped output,
  `NO_COLOR`, and an explicit plain mode all yield ANSI-free plain text, and `--json` always overrides to the
  contract regardless of terminal state (FR-004, SC-002, SC-004).
- **Watch is debounced and read-only**: the `watch` projection re-runs the existing route/evidence/check
  evaluation and re-renders the report on working-tree change, coalescing bursts via a debounce window, and never
  changes a verdict or emits a new contract (FR-007, FR-008, SC-005, SC-006). The exact file-change sensing
  mechanism and the optional TUI's interaction model and library are planning decisions deferred to
  `/speckit-plan`.
- **TUI is optional**: the interactive TUI is an optional surface over the same report objects; whether it ships
  in this row or is scoped as a bounded follow-up, and its precise navigation model, are deferred to
  `/speckit-plan` (FR-009, SC-006).
- **Safe failure preserved**: a transiently-unreadable working tree during a watch re-render (or any
  missing/malformed input) surfaces a clear input signal distinct from a tool defect, with no crash and no
  fabricated report, superseded by the next settled re-render (FR-012), consistent with the F14–F26 safe-failure
  discipline.
- **Determinism of rendered content**: the human-readable rendered *content* (the facts shown) is deterministic
  for identical repository state — stable ordering, normalized paths, no wall-clock/username/environment
  dependence — so smoke snapshots are stable even though the layout itself is non-contractual (FR-011, SC-003).
- **Tier**: **Tier 1 (contracted change).** Although the roadmap framed F27 as "Tier 2 unless the public CLI/API
  surface changes require Tier 1," this row **does** make all three Tier-1 triggers: it adds new public projection
  APIs (`HumanText`/`HumanRender`), introduces a new dependency (Spectre.Console), and adds new public CLI
  command/flag vocabulary (`--plain`/`--watch`, the `watch`/`tui` commands). Any one forces Tier 1; together they
  require the full chain — curated `.fsi` for every new module, committed surface baselines for both new
  libraries, the new-dependency justification, real test evidence, and docs for the new modes/commands. It adds
  **no** report-object, verdict, exit-code, or JSON-schema/contract change, so every existing JSON golden stays
  byte-identical. (Confirmed at `/speckit-plan`; see plan.md "Change Classification.")
</content>
</invoke>
