// Curated public signature contract for the rich Spectre.Console renderer (F27, §4).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching RichRender.fs carries NO access modifiers on top-level bindings.
//
// `emit` renders a `ReportView` to a Spectre `IAnsiConsole` at the chosen mode. `Rich` draws a
// color-coded verdict banner + grouped, width-resilient tables (reflow/truncate to the console
// width, safe default when unknown). `Plain` writes the precomputed `HumanText` plain string
// verbatim (the degrade path — byte-equal, no ANSI). `Json` is a no-op here (the host writes the
// byte-identical `*Json` string directly); it is present in the match for totality. Color/ANSI
// appear ONLY for `Rich`, never for `Plain`/`Json` (FR-004, FR-006, SC-004).

namespace FS.GG.Governance.HumanRender

open Spectre.Console
open FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RichRender =

    /// The safe default terminal width used when the sensed width is unknown.
    val defaultWidth: int

    /// Render a report view to a Spectre console at the chosen mode. `plain` is the precomputed
    /// `HumanText` projection used verbatim for the `Plain` degrade path.
    val emit:
        mode: RenderMode.RenderMode ->
        view: ReportView.ReportView ->
        plain: string ->
        console: IAnsiConsole ->
            unit
