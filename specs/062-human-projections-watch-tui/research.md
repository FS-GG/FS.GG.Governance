# Phase 0 Research — Human Projections (F27)

All NEEDS CLARIFICATION from the Technical Context are resolved below. Each decision records what was chosen, why,
and the alternatives rejected. These are the planning decisions the spec deferred to `/speckit-plan` (the rich
library binding, the watch file-sensing mechanism, the TUI library/scope, and the Tier determination).

---

## D1 — One source of truth, two projections: a pure `HumanText` library mirroring `*Json`

**Decision.** Add a pure library `FS.GG.Governance.HumanText` carrying one ANSI-free, deterministic projection per
report object — `ofRouteResult`, `ofRouteExplanation`, `ofShipDecision`, `ofVerifyDecision`, `ofReleaseReport`,
`ofCacheEligibilityReport` — each taking the **same input tuple** as the matching `*Json.of*` (route/verify also
carry `CacheEligibilityReport option` + `(GateId*GateOutcome) list`). Each command host's existing plain-text
`render` branch is replaced by a delegation to `HumanText`; the JSON branch is untouched.

**Rationale.** The roadmap exit criterion is "operator UX improves **without creating a second source of truth**."
The `*Json` libraries already establish the exact pattern: a pure, total projection of an immutable report object
to a string. Making the human text a sibling projection — same input, different output format — guarantees by
construction (report-object parity, FR-001/SC-001) that the human reads what automation acts on. Delegation keeps
the JSON path byte-identical (FR-010/SC-002): the host's JSON branch is literally unchanged.

**Alternatives rejected.**
- *Keep ad-hoc per-host `render` plain text.* Status quo; no single surface guaranteeing parity — the human and
  JSON views can silently diverge. Rejected by FR-001.
- *Render plain text **from** the JSON string.* Couples the human view to the JSON wire shape and would make a
  plain-text tweak risk a JSON change. Rejected — both must project the **report object**, independently.
- *One generic `render : Report -> string` over a unified report sum type.* The six report objects are distinct
  existing types (F18–F26); unifying them is a refactor outside this presentation-only row. Rejected.

---

## D2 — Presentation lives in one new CLI-host library, `FS.GG.Governance.HumanRender`

**Decision.** Put the Spectre rich renderer, the watch MVU, and the TUI MVU in a single new library
`FS.GG.Governance.HumanRender`, the **sole owner** of the rich dependency. The host edges (the per-command exes
and the legacy `Cli`) call it; the pure `HumanText` (incl. the navigable `ReportView`) stays presentation-free.

**Rationale.** The repo has **no single CLI host today**: `RouteCommand` is the packed `fsgg`
(`ToolCommandName=fsgg`, `IsPackable=true`); `ShipCommand`/`VerifyCommand`/`ReleaseCommand`/
`CacheEligibilityCommand`/`RefreshCommand` are `OutputType=Exe` but `IsPackable=false`, standalone entrypoints
awaiting a future "single-packed-`fsgg`-tool unification" that is explicitly **not** in scope; the legacy
`FS.GG.Governance.Cli` is a separate tool (`fsgg-governance`). So "confine presentation to the CLI host"
(FR-005/FR-013/SC-007) must name a concrete home. A single shared presentation **library** the host edges
reference is that home: the dependency lives in exactly one project, and SC-007 becomes a one-line
project-reference assertion (no pure core / `*Json` / `HumanText` references Spectre; only `HumanRender` does).

**Alternatives rejected.**
- *Add Spectre directly to each `*Command` exe.* Duplicates the dependency across many projects, violates
  dependency minimalism, and makes SC-007 unenforceable. Rejected.
- *Put it only in the legacy `Cli`.* `Cli` does not dispatch route/ship/verify/release (it runs the older `Host`
  MVU for route/explain/contract/evidence), so it cannot host their rich rendering. Rejected.
- *Wait for the single-`fsgg`-tool unification.* That unification is out of scope and undated; F27 is the row that
  must deliver these surfaces now. Rejected.

---

## D3 — Spectre.Console as the rich/TUI library: a justified, isolated new dependency

**Decision.** Use **Spectre.Console** for color, tables, width-resilient layout, and the minimal read-only TUI.
Add it to `Directory.Packages.props` with a NEED/SCOPE/OWNER comment (the YamlDotNet precedent) and reference it
**only** from `FS.GG.Governance.HumanRender`.

**Rationale.** The roadmap row names Spectre.Console. Color-coded banners, grouped tables, terminal-width
reflow/truncation (FR-006), and a `TestConsole` for deterministic tests are exactly its remit; hand-rolling ANSI
tables and width logic would be out of proportion (Principle III's "out of proportion" test, as YamlDotNet was for
YAML). Central pinning + single-project scope satisfies the Engineering Constraints' new-dependency rule (need,
version-pin, owner) and keeps the pure cores and the first-useful-product library BCL-only.

**Alternatives rejected.**
- *Hand-rolled ANSI.* Re-implements width math, color downgrade, and table layout; high cost, low reuse, brittle.
  Rejected as out of proportion.
- *Terminal.Gui for everything.* A heavier, more stateful full-TUI framework; unjustified for the rich one-shot
  render and the minimal navigator. Considered for the TUI only (see D5) and rejected for in-row scope.
- *No dependency; plain text only.* Fails FR-005/FR-006/FR-009. Rejected.

**Boundary guarantee.** SC-007 is enforced by a test/build check asserting no `.fsproj` other than
`HumanRender` references Spectre, and that the requirement text stays library-agnostic ("rich-rendering
dependency").

---

## D4 — Watch sensing: `System.IO.FileSystemWatcher` + a pure debounce MVU

**Decision.** Sense working-tree changes with BCL `System.IO.FileSystemWatcher` (no new package), feeding events
into a **pure** debounce/coalesce `update`; a settled-window timer emits a single `ReRender` effect. Provide a
polling fallback for filesystems where the watcher is unreliable (network shares, some containers). The interpreter
re-runs the **existing** route/evidence/check evaluation and re-projects via `HumanRender`. Read-only — no contract
write.

**Rationale.** `FileSystemWatcher` is the standard BCL mechanism and avoids a dependency. The debounce belongs in
the **pure** `update` (a burst of `ChangeDetected` within the window collapses to one `WindowSettled` →
`ReRender`), making SC-005 (one re-render per burst) a pure transition test with no real timer. Keeping the
watcher, the timer, and the re-render at the interpreter edge preserves the MVU boundary (Principle IV).

**Alternatives rejected.**
- *Pure polling only.* Simpler but either laggy or busy; `FileSystemWatcher` is event-driven and free. Kept as a
  **fallback**, not the default.
- *A third-party file-watch library.* Unjustified second dependency for what BCL provides. Rejected.
- *Debounce in the interpreter.* Would make the coalescing untestable as a pure transition and bury the SC-005
  property in timing. Rejected.

---

## D5 — TUI: a minimal, read-only, in-row navigator on Spectre over a pure `ReportView`

**Decision.** Ship the optional TUI **in this row** as a minimal, read-only navigator built on the
already-present Spectre dependency (its `Tree`/selection/live-display primitives). The navigable model is a
**pure** `HumanText.ReportView` (a node tree projected from the report object). A richer free-form TUI
(e.g. Terminal.Gui) is explicitly scoped as a possible **bounded follow-up**, not this row.

**Rationale.** The spec frames the TUI as optional/P3 and allows in-row or follow-up. The expensive, risky part
(a full interactive framework) is avoided; the cheap, valuable part (a pure report→view-model projection plus a
minimal Spectre navigator) is delivered, satisfying FR-009/SC-006 without a second UI dependency. The pure
`ReportView` is also reused by the rich renderer's grouped tables, so it pays for itself twice.

**Alternatives rejected.**
- *Defer the TUI entirely.* Leaves the roadmap's named optional surface unbuilt when a low-cost version is
  available. Rejected, but the richer variant is deferred.
- *Terminal.Gui in-row.* A second, heavier UI dependency for a P3 surface; disproportionate. Rejected for in-row.
- *Separately-derived TUI content.* Would break report-object parity (FR-009). Rejected — the TUI renders the
  pure `ReportView` only.

---

## D6 — Render-mode selection is pure; sensing is an effect; JSON always wins

**Decision.** `RenderMode = Json | Plain | Rich`. A pure total `selectMode (explicitJson: bool)
(capability: ColorCapability) : RenderMode` returns `Json` whenever JSON is requested (it always wins), else `Rich`
iff `IsTty && not NoColorEnv && not ExplicitPlain`, else `Plain`. TTY/`NO_COLOR`/terminal-width detection is an
edge `Effect` (`SenseCapability`).

**Rationale.** The decision is a small total function over four booleans — pure and exhaustively testable
(SC-004's TTY/no-color matrix becomes a truth-table test). Isolating the actual sensing as an effect keeps
`selectMode` deterministic and keeps the dependency on the terminal at the edge.

**Alternatives rejected.**
- *Detect inside the renderer.* Hides the decision in I/O, untestable. Rejected.
- *Make `--json` just another mode peer.* JSON is the contract and must override terminal state unconditionally
  (edge case "--json always wins"); modelling it as "always wins" rather than a peer encodes that. Kept.

---

## D7 — Determinism of rendered content; JSON is the only contract

**Decision.** The rendered **content** (facts shown) is deterministic for identical repository state — stable
ordering, normalized paths, no wall-clock/username/environment in the text — so a committed smoke snapshot is
stable even though the layout is non-contractual. `--json` stays the only contract, byte-identical to the pre-F27
golden. A deliberate wording/layout change re-blesses the smoke snapshot only (SC-008), with every JSON golden
left byte-identical and no verdict/exit-code change.

**Rationale.** Non-contractual ≠ non-deterministic: operators and CI smoke tests need stable output, but the team
needs freedom to reword. Separating "content is deterministic" (FR-011) from "layout is non-contractual" (FR-003)
gives both. The boundary is proved by the non-contractual-text guard test (SC-008).

**Alternatives rejected.**
- *Hold plain text to a byte-for-byte contract like JSON.* Freezes wording; contradicts FR-003. Rejected.
- *No snapshot at all.* Loses regression protection (SC-003). Rejected.

---

## Tier determination (resolves spec §Assumptions "Tier", checklist note)

**Tier 1 (contracted change).** F27 (a) adds new public API surface — the `HumanText` projection library and
`HumanRender`'s public MVU surface, each with a curated `.fsi` + committed surface baseline; (b) **introduces one
new dependency** (Spectre.Console); and (c) adds new public CLI command/flag vocabulary (`--watch`/`fsgg watch`,
`fsgg tui`, an explicit-plain flag). Any one is Tier 1; together they require the full chain (spec, plan, `.fsi`,
baselines, dependency justification, tests, docs). The plain-text/rich **renderings themselves** stay
non-contractual (smoke-snapshot stability only) — only the library API and the command vocabulary are surface, and
**no** JSON contract changes (every golden byte-identical).
