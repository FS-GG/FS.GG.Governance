# Contract — `tui` Host Wiring (read-only, optional, P3)

**Scope**: the optional `tui` subcommand on the dispatcher (`fsgg-governance tui`). Drives the F27
`HumanRender.Tui.run` interpreter edge over a `ReportView` projected from the same report object the other surfaces
use. Read-only — no verdict/gate/contract change (FR-008, FR-009, SC-006).

## Driving `Tui.run`

```
Tui.run
    view       // ReportView.viewOf<report> report …  — same report object as plain/JSON (parity)
    readKey    // unit -> TuiMsg : blocking key read mapped to MoveUp|MoveDown|Expand|Collapse|Quit
    draw       // TuiModel -> unit : render selection/expansion over `view` to the terminal via HumanRender
```

## Behavior (F27 pure `update`, reused)

- Non-`Quit` messages emit `Draw model` then `ReadKey`; `Quit` emits `Exit`.
- `update` changes only `TuiModel.Path` (selection cursor) and `TuiModel.Expanded` (open groups); `View` is never
  mutated.
- No evaluation, no I/O inside `update`; `readKey`/`draw` are the only effects executed at the edge.

## Guarantees

- **T1 (parity)**: `Tui.init(view).View` is exactly the `ReportView` projected from the report object the
  plain/JSON views use — never separately derived (FR-008, SC-006).
- **T2 (read-only)**: navigation changes only `Path`/`Expanded`; no verdict change, no new gate run, no contract
  emitted; only `ReadKey`/`Draw`/`Exit` effects (FR-009, SC-006).
- **T3 (host binding)**: `fsgg-governance tui`; "`fsgg tui`" is the generic spelling pending single-tool
  unification.
- **T4 (optional scope)**: a minimal read-only navigator (the F27 `Tui` MVU); a richer free-form TUI remains a
  bounded follow-up.
