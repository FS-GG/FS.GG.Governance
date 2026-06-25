# Phase 1 Data Model — Human-Projection Host Wiring (F27 wiring)

This row adds **no new report object** and **no new pure core**. It consumes the F27 types verbatim and threads them
through each host's existing MVU edge. The "data model" here is therefore (a) the F27 types being consumed,
(b) the small host-edge glue this row introduces, and (c) the per-host mapping from the resolved report object to a
rendered surface. Nothing below changes a verdict, exit-code basis, or JSON schema.

## 1. Consumed F27 types (verbatim — not modified)

From `FS.GG.Governance.HumanText`:

- `RenderMode = Json | Plain | Rich` — the render mode (closed DU).
- `ColorCapability = { IsTty: bool; NoColorEnv: bool; ExplicitPlain: bool; Width: int option }` — the sensed
  terminal capability (record).
- `selectMode : explicitJson:bool -> ColorCapability -> RenderMode` — pure, total. `Json` always wins; else `Rich`
  iff `IsTty && not NoColorEnv && not ExplicitPlain`; else `Plain`. **Unchanged by this row.**
- `HumanText.of* : <report …> -> string` — ANSI-free plain projection per report object (`ofRouteResult`,
  `ofShipDecision`, `ofVerifyDecision`, `ofCacheEligibilityReport`; `ofReleaseReport`/`ofRouteExplanation` exist but
  are **not consumed** this row — see research.md D2).
- `ReportView = { Title; ExitStatus; Sections: ReportNode list }`, `ReportNode = Leaf … | Group …`, and
  `viewOf* : <report …> -> ReportView` — the navigable view-model the rich/tui surfaces render.

From `FS.GG.Governance.HumanRender`:

- `RichRender.emit : RenderMode -> ReportView -> plain:string -> IAnsiConsole -> unit` — `Rich` ⇒ banner + tables;
  `Plain` ⇒ writes `plain` verbatim; `Json` ⇒ no-op.
- `Watch.run : root:string -> RenderMode -> clock:(unit->int64) -> reRender:(string -> RenderMode -> WatchSignal) ->
  shouldStop:(unit->bool) -> unit` plus the pure `WatchModel/WatchMsg/WatchEffect/WatchSignal` + `update`/`init`.
- `Tui.run : ReportView -> readKey:(unit->TuiMsg) -> draw:(TuiModel->unit) -> unit` plus the pure
  `TuiModel/TuiMsg/TuiEffect` + `init`/`update`.

## 2. New host-edge glue (this row)

### 2.1 `HumanRender.Capability.senseCapability` (new, edge effect)

```
senseCapability : explicitPlain:bool -> ColorCapability
```

- Reads `IsTty` (is stdout a terminal), `NoColorEnv` (`NO_COLOR` present), `Width` (terminal width if known,
  else `None`), and carries the host-parsed `explicitPlain` (`--plain`/`--no-color`) into `ColorCapability`.
- Lives in `HumanRender` so no host touches the console/Spectre directly (research.md D4/D7). Executed **only** at
  the interpreter edge (it is an effect, not pure). The pure decision stays `HumanText.selectMode`.

### 2.2 Per-host render dispatch (no new type — a branch in each host's interpreter)

Each wired host's interpreter, when emitting the human/JSON surface, computes:

```
mode = HumanText.selectMode explicitJson (senseCapability explicitPlain)
match mode with
| Json -> <existing JSON path, byte-for-byte unchanged>                       // FR-002, SC-002, SC-004
| Plain -> write (HumanText.of<report> …) ++ <host operational lines>         // FR-001, FR-003, SC-003
| Rich -> RichRender.emit Rich (viewOf<report> …) (HumanText.of<report> …) console
          ++ <host operational lines>                                          // FR-005, FR-006
```

- `explicitJson` is the host's existing JSON selector (`--json` bool, or `--format json`).
- `<host operational lines>` = the host's own `wrote <path>` / changed-path-count narration (D1) — emitted around
  the report projection, never inside the JSON contract.

## 3. Per-host mapping (the report object → surface table)

| Host | Resolved report value | Plain projection | Rich view | Watch triad member |
|---|---|---|---|---|
| `route` | `RouteResult` + `CacheEligibilityReport option` + `(GateId*GateOutcome) list` | `ofRouteResult` | `viewOfRouteResult` | `route` |
| `ship` | `ShipDecision` (+ same aux tuple) | `ofShipDecision` | `viewOfShipDecision` | — |
| `verify` | `ShipDecision` (+ same aux tuple) | `ofVerifyDecision` | `viewOfVerifyDecision` | `verify`-check |
| evidence (`CacheEligibilityCommand`) | `CacheEligibilityReport` (already computed: `CacheEligibility.evaluate candidates store`) | `ofCacheEligibilityReport` | `viewOfCacheEligibilityReport` | `evidence` |
| `release` | *(deferred — needs F26 `ReleaseReport`)* | — | — | — |
| `explain` (`Cli`) | *(deferred — F03 `Explanation list`, not F19)* | — | — | — |
| evidence (`Cli`) | *(deferred — `ProjectEvidenceReport`, not F41)* | — | — | — |

The aux tuple (`CacheEligibilityReport option` + `(GateId*GateOutcome) list`) is what `ofRouteResult`/`ofShipDecision`
/`ofVerifyDecision` already take (matching the `*Json.of*` signatures) — the host already has these values in its
`Model`.

## 4. Watch host glue (read-only)

- `reRender root mode` (host-supplied callback): re-runs the **existing** route/evidence/verify-check evaluation for
  `root`, projects to `ReportView` + plain via §3, dispatches by `mode` (§2.2), and returns a `WatchSignal`
  (`Rendered` | `InputUnreadable reason` | `Idle`). It performs **no** `WriteArtifact` and changes **no** verdict
  (FR-009, SC-006).
- `clock` = real monotonic-ish ms reading (e.g. `DateTime.UtcNow.Ticks / 10000L`) at the interpreter edge; a
  synthetic clock drives the pure `update` tests (F27, already covered).
- `shouldStop` = a volatile flag set on Ctrl+C / cancellation.
- Safe failure: a transiently-unreadable tree yields `reRender … = InputUnreadable reason`, recorded in
  `WatchModel.LastSignal`, superseded by the next settled `reRender` (FR-010).

## 5. TUI host glue (read-only)

- `view` = `viewOf<report>` from the same report object (§3) — report-object parity (FR-008).
- `readKey` = a blocking key read mapped to `TuiMsg` (`MoveUp`/`MoveDown`/`Expand`/`Collapse`/`Quit`).
- `draw model` = render `model` (selection/expansion over `view`) to the terminal via `HumanRender`.
- The pure `Tui.update` changes only `Path`/`Expanded`; `View` is never mutated; no verdict/gate/contract (F27,
  already covered) — this row adds only the interpreter-edge wiring + the `tui` subcommand.

## 6. Invariants (asserted by tests)

1. **Report-object identity**: the value passed to `HumanText.of<report>` is the **same** value the host's JSON path
   projects (SC-001).
2. **JSON byte-identity**: each host's persisted/`--json` contract is byte-identical to its pre-wiring golden
   (FR-002, SC-002).
3. **ANSI discipline**: `Plain`/`Json` output contains no ANSI escapes in any terminal state; ANSI appears only in
   `Rich` (FR-005, SC-003); `--json` never reaches `RichRender` (SC-004).
4. **Read-only watch/tui**: only `SenseChanges`/`ScheduleDebounce`/`ReRender` (watch) and `ReadKey`/`Draw`/`Exit`
   (tui) effects; no verdict/rule/exit-code/contract change (FR-009, SC-006).
5. **Dependency boundary**: no wired host references Spectre directly; only `HumanRender` does (FR-011, SC-007).
6. **Debounce**: a burst within the window ⇒ one settled re-render; real-`FileSystemWatcher` settle end-to-end
   (SC-005).
