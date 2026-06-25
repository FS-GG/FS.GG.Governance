# Contract — `watch` Host Wiring (read-only)

**Scope**: the `watch` subcommand on the dispatcher (`fsgg-governance watch`) and the `--watch` flag on the packed
`fsgg` (`fsgg route --watch`). Both drive the F27 `HumanRender.Watch.run` interpreter edge. Read-only — no contract
written, no verdict changed (FR-007, FR-009, SC-006).

## Driving `Watch.run`

```
Watch.run
    root          // working-tree root to watch
    mode          // RenderMode from selectMode (Plain or Rich; Json is meaningless for watch)
    clock         // unit -> int64 : monotonic-ish ms at the edge (e.g. DateTime.UtcNow.Ticks / 10000L)
    reRender      // string -> RenderMode -> WatchSignal : RE-RUN existing eval, re-project, return signal
    shouldStop    // unit -> bool : true on Ctrl+C / cancellation
```

## The `reRender` callback (host-supplied, read-only)

For a `root` and `mode`, `reRender`:

1. Re-runs the **existing** evaluation for the watched member of the route/evidence/check triad:
   - `route` → the route evaluation → `RouteResult`
   - `evidence` → `CacheEligibility.evaluate …` → `CacheEligibilityReport`
   - `check` → the `verify` gate-check evaluation → `ShipDecision`
2. Projects via `render-dispatch.md` (`viewOf*` + `of*`) and dispatches by `mode` (`Plain`/`Rich`).
3. Returns a `WatchSignal`:
   - `Rendered` — a clean settled re-render.
   - `InputUnreadable reason` — a transiently-unreadable/partial tree (FR-010): surfaced as a clear input signal,
     **no** crash, **no** fabricated report; superseded by the next settled re-render.
   - `Idle` — nothing to do.
4. Performs **no** `WriteArtifact` and changes **no** verdict/rule/exit-code (FR-009, SC-006).

## Debounce (F27 pure `update`, reused)

- `ChangeDetected at` refreshes `PendingSince` and schedules `ScheduleDebounce (at + debounceWindow)` (200 ms).
- `WindowSettled at` with no later change emits exactly one `ReRender`; a stale settle (superseded by a newer
  change) is ignored.
- A burst of N change events within the window ⇒ **one** `ReRender` (SC-005).

## Guarantees

- **W1 (debounce)**: burst-within-window ⇒ one settled re-render; well-separated changes ⇒ one each (SC-005).
- **W2 (end-to-end settle)**: a real `FileSystemWatcher` over a temp tree, on a tracked-file change, invokes
  `reRender` once after the window settles reflecting the new state — closes F27's `[PARTIAL]` (SC-005, research D8).
- **W3 (read-only)**: only `SenseChanges`/`ScheduleDebounce`/`ReRender` effects; no contract write; no verdict change
  (FR-009, SC-006).
- **W4 (safe failure)**: an unreadable mid-edit tree ⇒ `InputUnreadable`, no crash, superseded by next settle
  (FR-010).
- **W5 (host binding)**: `fsgg-governance watch` (dispatcher subcommand) and `fsgg route --watch` (packed exe flag)
  share the same `HumanRender` edge; "`fsgg watch`" is the generic spelling.
