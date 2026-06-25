# Contract: `FS.GG.Governance.HumanRender.Tui` (edge, P3 — optional, read-only MVU)

A minimal, read-only interactive navigator over a `ReportView` (selected gates, proof/explanation trees, blockers,
evidence references). Strictly a view — no verdict, gate, or contract (FR-009, SC-006). Navigation is **pure**; key
input + redraw are edge effects. Built on the already-present Spectre dependency; a richer free-form TUI is a
possible bounded follow-up (research D5).

## `Tui.fsi` (draft)

```fsharp
namespace FS.GG.Governance.HumanRender

open FS.GG.Governance.HumanText.ReportView         // ReportView

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tui =

    type TuiModel =
        { View: ReportView
          Path: int list
          Expanded: Set<int list> }

    type TuiMsg =
        | MoveUp
        | MoveDown
        | Expand
        | Collapse
        | Quit

    type TuiEffect =
        | ReadKey
        | Draw of TuiModel
        | Exit

    val init: view: ReportView -> TuiModel * TuiEffect list
    val update: TuiMsg -> TuiModel -> TuiModel * TuiEffect list
    // interpreter (Spectre key read + redraw loop) lives in Tui.fs edge.
```

## Transition rules (pure, total)

- `MoveUp`/`MoveDown` ⇒ move the cursor `Path` within the current level (clamped).
- `Expand`/`Collapse` ⇒ toggle the selected `Group` in `Expanded`.
- `Quit` ⇒ emit `[Exit]`.
- Every non-`Quit` ⇒ emit `[Draw model']` then `[ReadKey]`.

## Invariants

- **Read-only** (FR-009, SC-006) — `update` changes only `Path`/`Expanded`; it never re-evaluates a rule, changes a
  verdict, or emits a contract. `init`/`update` are pure; only `ReadKey`/`Draw`/`Exit` are effects.
- **Report-object parity** — `View` is the `ReportView` projected from the same report object the plain/JSON views
  use (never separately derived).
- **Optional** — if a build/CI cannot host an interactive TUI, the surface degrades to plain/rich; the navigator is
  never on a non-interactive path.
