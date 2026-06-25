# Phase 1 Data Model — Human Projections (F27)

All types are **presentation-only** vocabulary over the **existing**, unchanged report objects (F18–F26). No
report object, verdict, rule, exit-code, or JSON schema is added or modified. Field/case order below is the
declaration order each `.fsi` must use.

Existing report objects rendered (reused verbatim, never redefined):

| Command | Report object | Source module | JSON projection mirrored |
|---|---|---|---|
| `route` | `RouteResult` | `FS.GG.Governance.Route.Model` | `RouteJson.ofRouteResult` |
| `explain` | `RouteExplanation` | `FS.GG.Governance.RouteExplain.Model` | (none — explain has no JSON contract) |
| `ship` | `ShipDecision` | `FS.GG.Governance.Ship.Model` | (ship summary) |
| `verify` | `ShipDecision` | `FS.GG.Governance.Ship.Model` | `VerifyJson.ofVerifyDecision` |
| `release` | `ReleaseReport` | `FS.GG.Governance.ReleaseReport.Model` | `ReleaseJson.ofReleaseReport` |
| `evidence` | `CacheEligibilityReport` | `FS.GG.Governance.CacheEligibility.Model` | `CacheEligibilityJson` |

The auxiliary inputs `route`/`verify` carry (a `CacheEligibilityReport option` and a `(GateId * GateOutcome) list`)
are passed through verbatim from the existing `*Json` call sites so the human projection sees the exact same report
value.

---

## §1 RenderMode (`FS.GG.Governance.HumanText`, pure)

```fsharp
/// How a command's report is rendered. JSON is the contract; Plain/Rich are non-contractual human views.
type RenderMode =
    | Json
    | Plain
    | Rich

/// The sensed terminal capability (filled by the edge SenseCapability effect; never sensed in a pure function).
type ColorCapability =
    { IsTty: bool
      NoColorEnv: bool          // NO_COLOR set (any value) per the de-facto standard
      ExplicitPlain: bool       // user passed --plain / --no-color
      Width: int option }       // None = unknown ⇒ safe default at render time
```

**Selection rule** (pure, total): `selectMode (explicitJson: bool) (cap: ColorCapability) : RenderMode`

- `explicitJson = true` ⇒ `Json` (**always wins**, regardless of `cap`).
- else `Rich` iff `cap.IsTty && not cap.NoColorEnv && not cap.ExplicitPlain`.
- else `Plain`.

State transitions: none (a one-shot decision). Exhaustive over the boolean product (SC-004 truth table).

---

## §2 HumanText projections (`FS.GG.Governance.HumanText`, pure)

Each is a pure, total, **ANSI-free**, deterministic `… -> string`. Signatures mirror the matching `*Json.of*`
input tuple (report-object parity, FR-001):

```fsharp
ofRouteResult        : RouteResult -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string
ofRouteExplanation   : RouteExplanation -> string
ofShipDecision       : ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string
ofVerifyDecision     : ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string
ofReleaseReport      : ReleaseReport -> string
ofCacheEligibilityReport : CacheEligibilityReport -> string
```

**Rendered content** (per report, in a stable order): a verdict/header line; the selected gates / unmet
preconditions; the blockers; the warnings; and the exit-status line — all read from the report object, never
re-derived. `ofVerifyDecision` may share `ofShipDecision`'s body (both render a `ShipDecision`); it stays a named
entry for command parity.

**Invariants** (FR-002, FR-011, SC-002, SC-003):
- No ANSI/CSI escape sequence ever appears (assert no `ESC[`).
- Byte-identical on repeated calls over identical input.
- No absolute path / wall-clock / username / environment in the text (normalized paths, stable ordering).
- A blocked verdict renders as blocked with explicit reason(s) + exit status — never softened.
- A missing/malformed **input** carried in the report renders a clear input signal distinct from a tool defect —
  no fabricated report (FR-012).

---

## §3 ReportView navigable view-model (`FS.GG.Governance.HumanText`, pure)

The shared, presentation-free model the **rich tables** and the **TUI** both render (so both stay parity-true to
the report object).

```fsharp
/// A node in the navigable projection of a report. Leaf carries a label + optional detail; Group nests children.
type ReportNode =
    | Leaf of label: string * detail: string option
    | Group of title: string * children: ReportNode list

/// The whole report as one navigable tree: a titled root (verdict) over grouped sections
/// (selected gates, blockers, warnings, preconditions, evidence/provenance references).
type ReportView =
    { Title: string            // verdict/header
      ExitStatus: string       // the exit-status line, verbatim from the report
      Sections: ReportNode list }
```

**Projections** (pure, one per report object, parity with §2):
`viewOfRouteResult`, `viewOfRouteExplanation`, `viewOfShipDecision`, `viewOfVerifyDecision`, `viewOfReleaseReport`,
`viewOfCacheEligibilityReport` — same inputs as the §2 functions. `HumanText.of*` and the rich tables can both be
defined as renders of `ReportView`, guaranteeing one structure behind every human surface.

---

## §4 Rich render (`FS.GG.Governance.HumanRender`, edge — depends on Spectre)

```fsharp
/// Render a report to a Spectre console at the chosen mode. Plain/Json delegate out (never reached for Json here).
emit : RenderMode -> ReportView -> Spectre.Console.IAnsiConsole -> unit
```

- `Rich` ⇒ a color-coded verdict banner (`ReportView.Title`) + grouped tables from `Sections`, width-resilient:
  reflow/truncate to `ColorCapability.Width`, falling back to a safe default (e.g. 80) when `Width = None`.
- `Plain` ⇒ writes `HumanText.of*` (the exact plain projection) — degrade path, no ANSI.
- `Json` ⇒ not handled here (the host writes the `*Json` string directly); included in the match for totality.

Determinism: rich **content** mirrors the plain content (same `ReportView`); only styling differs (SC-004).

---

## §5 Watch MVU (`FS.GG.Governance.HumanRender`, read-only)

Pure `Model`/`Msg`/`update` + `Effect`; interpreter at the edge. Debounce lives in `update` (SC-005).

```fsharp
type WatchModel =
    { Root: string                       // working-tree root being watched
      Mode: RenderMode                   // Plain or Rich (never Json — watch is interactive)
      PendingSince: int64 option         // logical timestamp of the first un-settled change (None = idle)
      LastSignal: WatchSignal }          // last render outcome (for the status line)

and WatchSignal =
    | Idle
    | Rendered                            // a settled re-render succeeded
    | InputUnreadable of reason: string   // transiently unreadable/partial tree (FR-012) — superseded next settle

type WatchMsg =
    | ChangeDetected of at: int64         // a file-change event (logical time supplied by the edge)
    | WindowSettled of at: int64          // the debounce window elapsed with no newer change
    | Rerendered of WatchSignal           // result of a ReRender effect

type WatchEffect =
    | SenseChanges of root: string        // start/continue FileSystemWatcher (or poll fallback)
    | ScheduleDebounce of dueAt: int64    // ask the edge to fire WindowSettled after the window
    | ReRender of root: string * mode: RenderMode   // re-run the EXISTING evaluation + project (no contract write)
```

**Transition rules** (pure, total):
- `ChangeDetected at` ⇒ set/refresh `PendingSince`; emit `ScheduleDebounce (at + window)`. A burst keeps refreshing
  the due time, so only the final `WindowSettled` fires a render (**coalesce → one re-render**, SC-005).
- `WindowSettled at` when `PendingSince = Some s` and no change arrived after `s` ⇒ clear `PendingSince`; emit
  `ReRender(Root, Mode)`. If a newer change arrived, ignore (a later `WindowSettled` will fire).
- `Rerendered signal` ⇒ `LastSignal = signal`. `InputUnreadable` is **not** a crash and **not** a fabricated
  report; it is superseded by the next settled re-render (FR-012).

**Read-only guarantee** (FR-008, SC-006): the only effects are sense/schedule/re-render; `ReRender` re-runs the
**existing** route/evidence/check evaluation and writes **no** new contract artifact. No `Msg` changes a verdict or
emits JSON.

---

## §6 Optional TUI MVU (`FS.GG.Governance.HumanRender`, read-only)

Pure navigation over a `ReportView`; interpreter does key input + redraw. Holds only view/selection state.

```fsharp
type TuiModel =
    { View: ReportView                    // the immutable projection being navigated (never mutated)
      Path: int list                      // selection path into Sections/children (expansion/cursor)
      Expanded: Set<int list> }           // which Group nodes are expanded

type TuiMsg =
    | MoveUp | MoveDown | Expand | Collapse | Quit

type TuiEffect =
    | ReadKey                             // await the next keypress
    | Draw of TuiModel                    // redraw the current view/selection
    | Exit                                // leave the TUI (read-only; no contract)
```

**Read-only guarantee** (FR-009, SC-006): `update` changes only `Path`/`Expanded`; it never re-evaluates a rule,
changes a verdict, or emits a contract. The navigable content is the `ReportView` projected from the same report
object the plain/JSON views use (report-object parity).

---

## Determinism & boundary summary

- **Pure / no dependency**: `RenderMode`, `HumanText.of*`, `ReportView`/`viewOf*` (library `HumanText`) — no
  Spectre, no I/O.
- **Edge / Spectre**: `emit` (rich), the watch interpreter (`FileSystemWatcher`/poll, timer), the TUI interpreter
  (key/redraw) — library `HumanRender` only (FR-013, SC-007).
- **Unchanged**: every report object, verdict, exit-code scheme, and JSON projection (FR-010); `--json` stays the
  byte-identical contract (SC-002).
