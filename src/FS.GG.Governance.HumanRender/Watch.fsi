// Curated public signature contract for the read-only watch MVU (F27, §5).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Watch.fs carries NO access modifiers on top-level bindings.
//
// The pure `update` owns the DEBOUNCE: a burst of `ChangeDetected` within the window coalesces into
// a SINGLE `ReRender` once the window settles (SC-005). It performs NO I/O — file-change sensing,
// the debounce timer, and the re-render print are effects executed at the interpreter edge. The
// session is strictly READ-ONLY: the only effects are sense/schedule/re-render; no `Msg` changes a
// verdict, evaluates a new rule, or emits a contract (FR-008, SC-006). A transiently-unreadable tree
// surfaces `InputUnreadable` (a clear input signal, superseded by the next settled render — FR-012).

namespace FS.GG.Governance.HumanRender

open FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Watch =

    /// The outcome of a settled re-render, shown on the status line.
    type WatchSignal =
        | Idle
        | Rendered
        | InputUnreadable of reason: string

    /// Durable watch state. `PendingSince` is the logical time of the latest un-settled change
    /// (`None` = idle); `Mode` is `Plain` or `Rich` (never `Json` — watch is interactive).
    type WatchModel =
        { Root: string
          Mode: RenderMode.RenderMode
          PendingSince: int64 option
          LastSignal: WatchSignal }

    /// Inputs to the pure transition (logical time supplied by the edge).
    type WatchMsg =
        | ChangeDetected of at: int64
        | WindowSettled of at: int64
        | Rerendered of WatchSignal

    /// The I/O the pure `update` REQUESTS but never performs. `ReRender` re-runs the EXISTING
    /// route/evidence/check evaluation and re-projects — it writes NO new contract artifact.
    type WatchEffect =
        | SenseChanges of root: string
        | ScheduleDebounce of dueAt: int64
        | ReRender of root: string * mode: RenderMode.RenderMode

    /// The debounce window (logical units) a burst must settle for before a single re-render fires.
    val debounceWindow: int64

    /// Initial state + the first effect (start sensing). Pure.
    val init: root: string -> mode: RenderMode.RenderMode -> WatchModel * WatchEffect list

    /// The pure debounce transition (TOTAL, no I/O).
    val update: msg: WatchMsg -> model: WatchModel -> WatchModel * WatchEffect list

    /// Headless-safe interactive stop-poll: `true` when the user pressed `q`, OR when the console is
    /// unreadable (stdin redirected / no console — `KeyAvailable`/`ReadKey` throw). It NEVER throws, so
    /// `--watch` in a pipe/CI stops cleanly instead of crashing. Shared by both watch hosts (H3 / #47).
    val safeKeyPoll: unit -> bool

    /// The interpreter edge: sense filesystem changes under `root` (FileSystemWatcher), drive the
    /// pure `update`, and run each `ReRender` through the injected, read-only re-render callback.
    /// Blocks until `shouldStop` returns true, then returns the last settled signal. A `root` whose
    /// watcher cannot be constructed (nonexistent/unreadable) returns `InputUnreadable` without ever
    /// entering the loop — the host maps it to its input-unavailable exit code (3 / 66). NO contract
    /// is written.
    val run:
        root: string ->
        mode: RenderMode.RenderMode ->
        clock: (unit -> int64) ->
        reRender: (string -> RenderMode.RenderMode -> WatchSignal) ->
        shouldStop: (unit -> bool) ->
            WatchSignal
