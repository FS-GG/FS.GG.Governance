namespace FS.GG.Governance.HumanRender

open Spectre.Console
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.ReportView

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RichRender =

    let defaultWidth = 80

    // Flatten one section subtree to indented (label, detail) rows for a width-resilient table.
    let private flatten (node: ReportNode) : (string * string) list =
        let rec go depth node =
            let indent = String.replicate depth "  "

            match node with
            | Leaf(label, detail) -> [ indent + label, defaultArg detail "" ]
            | Group(title, children) -> (indent + title, "") :: (children |> List.collect (go (depth + 1)))

        go 0 node

    // A verdict color keyed off the exit-status line (non-contractual styling only).
    let private bannerColor (exitStatus: string) =
        if exitStatus.Contains "blocked" then "red"
        elif exitStatus.Contains "clean" || exitStatus.Contains "success" then "green"
        else "yellow"

    let private emitRich (view: ReportView) (console: IAnsiConsole) =
        let color = bannerColor view.ExitStatus
        console.Write(Markup(sprintf "[bold %s]%s[/]\n" color (Markup.Escape view.Title)))

        let table = Table()
        table.Border <- TableBorder.Rounded
        table.AddColumn(Markup.Escape "Item") |> ignore
        table.AddColumn(Markup.Escape "Detail") |> ignore

        for section in view.Sections do
            for (label, detail) in flatten section do
                table.AddRow([| Markup.Escape label; Markup.Escape detail |]) |> ignore

        console.Write(table)
        console.Write(Markup(sprintf "[dim]exit status: %s[/]\n" (Markup.Escape view.ExitStatus)))

    let emit
        (mode: RenderMode.RenderMode)
        (view: ReportView)
        (plain: string)
        (console: IAnsiConsole)
        : unit =
        match mode with
        | RenderMode.Json -> () // host writes the byte-identical *Json string directly
        | RenderMode.Plain ->
            // Degrade to the precomputed plain projection, written VERBATIM (no Spectre layout, no
            // ANSI) so it stays byte-equal to the HumanText.of* string (FR-004, SC-004).
            console.Profile.Out.Writer.Write plain
        | RenderMode.Rich -> emitRich view console

    // F27 wiring (063): the stdout-bound emit hosts inject as their `RenderReport` edge port — it renders
    // to the real terminal via the default Spectre console so NO host references Spectre directly (FR-011,
    // SC-007); the dependency boundary stays confined to HumanRender.
    let emitStdout (mode: RenderMode.RenderMode) (view: ReportView) (plain: string) : unit =
        emit mode view plain AnsiConsole.Console
