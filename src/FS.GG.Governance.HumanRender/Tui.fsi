// Curated public signature contract for the optional read-only TUI MVU (F27, §6).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Tui.fs carries NO access modifiers on top-level bindings.
//
// The pure `update` navigates a `ReportView` (the SAME projection the plain/JSON views use) — it
// changes ONLY the selection `Path` and the `Expanded` set; it never re-evaluates a rule, changes a
// verdict, or emits a contract (FR-009, SC-006). Key input and redraw are effects executed at the
// Spectre interpreter edge.

namespace FS.GG.Governance.HumanRender

open FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tui =

    /// Durable navigation state. `View` is the immutable projection being navigated (never mutated);
    /// `Path` is the selection cursor into `Sections`; `Expanded` is which group cursors are open.
    type TuiModel =
        { View: ReportView.ReportView
          Path: int list
          Expanded: Set<int list> }

    /// User navigation actions.
    type TuiMsg =
        | MoveUp
        | MoveDown
        | Expand
        | Collapse
        | Quit

    /// The I/O the pure `update` requests but never performs.
    type TuiEffect =
        | ReadKey
        | Draw of TuiModel
        | Exit

    /// Initial state (cursor at the first section) + the first draw/read effects. Pure.
    val init: view: ReportView.ReportView -> TuiModel * TuiEffect list

    /// The pure navigation transition (TOTAL, no I/O). Non-`Quit` ⇒ `Draw` then `ReadKey`;
    /// `Quit` ⇒ `Exit`. Changes only `Path`/`Expanded`.
    val update: msg: TuiMsg -> model: TuiModel -> TuiModel * TuiEffect list

    /// The interpreter edge: draw the current selection and read keys until `Quit`. Read-only.
    val run: view: ReportView.ReportView -> readKey: (unit -> TuiMsg) -> draw: (TuiModel -> unit) -> unit
