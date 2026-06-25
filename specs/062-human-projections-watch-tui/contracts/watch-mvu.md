# Contract: `FS.GG.Governance.HumanRender.Watch` (edge, P2 — read-only MVU)

A debounced, read-only watch projection over the route/evidence/check report: re-runs the **existing** evaluation
and re-renders on working-tree change, coalescing a burst into a **single** re-render (FR-007, SC-005). Changes no
verdict, rule, exit-code, or contract (FR-008, SC-006). The debounce is **pure** (in `update`); sensing/timer/
re-render are edge effects.

> **"check" binding.** "route/evidence/check" names three **existing** report objects — `route`
> (`Route.RouteResult`), `evidence` (`CacheEligibility.CacheEligibilityReport`), and `check` = the `verify`
> gate-check report (`Ship.ShipDecision`). The `ReRender` effect re-projects whichever of these three the watched
> command resolves to; **no new "check" report object is introduced** (FR-007).

## `Watch.fsi` (draft)

```fsharp
namespace FS.GG.Governance.HumanRender

open FS.GG.Governance.HumanText.RenderMode        // RenderMode

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Watch =

    type WatchSignal =
        | Idle
        | Rendered
        | InputUnreadable of reason: string

    type WatchModel =
        { Root: string
          Mode: RenderMode
          PendingSince: int64 option
          LastSignal: WatchSignal }

    type WatchMsg =
        | ChangeDetected of at: int64
        | WindowSettled of at: int64
        | Rerendered of WatchSignal

    type WatchEffect =
        | SenseChanges of root: string
        | ScheduleDebounce of dueAt: int64
        | ReRender of root: string * mode: RenderMode

    /// Debounce window in the edge's logical time unit (e.g. ms).
    val debounceWindow: int64

    val init: root: string -> mode: RenderMode -> WatchModel * WatchEffect list
    val update: WatchMsg -> WatchModel -> WatchModel * WatchEffect list
    // interpreter (FileSystemWatcher / poll fallback, timer, re-run-existing-evaluation) lives in Watch.fs edge.
```

## Transition rules (pure, total)

- **`ChangeDetected at`** ⇒ `PendingSince := Some at`; emit `[ScheduleDebounce (at + debounceWindow)]`. A burst
  keeps pushing the due time forward, so only the final settle renders (**coalesce → one re-render**, SC-005).
- **`WindowSettled at`** ⇒ if `PendingSince = Some s` and no later change arrived, clear `PendingSince` and emit
  `[ReRender(Root, Mode)]`; otherwise emit `[]` (a later settle will fire).
- **`Rerendered signal`** ⇒ `LastSignal := signal`. `InputUnreadable` is never a crash and never a fabricated
  report; it is superseded by the next settled re-render (FR-012).

## Invariants

- **Single re-render per burst** (SC-005) — provable by a pure-`update` test feeding N `ChangeDetected` then one
  `WindowSettled`; exactly one `ReRender` is emitted.
- **Read-only** (FR-008, SC-006) — the only effects are `SenseChanges`/`ScheduleDebounce`/`ReRender`; `ReRender`
  re-runs the existing evaluation and writes **no** contract artifact; no `Msg` changes a verdict or emits JSON.
- **Safe failure** (FR-012) — a transiently unreadable tree yields `InputUnreadable`, superseded next settle; no
  swallowed error, no crash.
- **Interpreter test** — a real `FileSystemWatcher` over a temp tree drives at least one end-to-end settle where
  safe (Constitution IV/V); the debounce proof itself needs no real timer.
