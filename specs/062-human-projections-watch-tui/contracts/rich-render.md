# Contract: `FS.GG.Governance.HumanRender.RichRender` (edge, P2 — Spectre)

The rich projection for interactive terminals: a color-coded verdict banner + grouped gate/finding/blocker tables,
**terminal-width resilient**, **degrading to `HumanText` plain text** on non-TTY / `NO_COLOR` / explicit-plain
(FR-004, FR-006). The **sole** owner of the Spectre.Console dependency (FR-005, FR-013, SC-007).

## `RichRender.fsi` (draft)

```fsharp
namespace FS.GG.Governance.HumanRender

open FS.GG.Governance.HumanText.RenderMode        // RenderMode
open FS.GG.Governance.HumanText.ReportView          // ReportView
open Spectre.Console                                  // IAnsiConsole — confined to THIS library

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RichRender =

    /// Render a report's view at the chosen mode to the given console.
    /// Rich ⇒ banner + grouped tables (width-resilient). Plain ⇒ HumanText plain text. Json ⇒ not handled here.
    val emit: mode: RenderMode -> view: ReportView -> plain: string -> console: IAnsiConsole -> unit
```

(`plain` is the precomputed `HumanText.of*` string, passed in so the degrade path writes the exact plain
projection and `RichRender` need not re-import every report type.)

## Behavior

- **Rich** — banner colored by verdict (clean/blocked/warn); each `ReportView.Section` becomes a grouped table;
  layout reflows/truncates to `ColorCapability.Width`, safe default (80) when unknown — never a corrupted overflow
  (FR-006, SC-004). Content mirrors the plain text (same `ReportView`).
- **Plain** — writes `plain` verbatim, **no ANSI** (the degrade path; SC-004).
- **Json** — not reached (the host writes the `*Json` string directly); present in the match for totality.

## Invariants

- **No ANSI off-TTY** — `Plain` output is byte-equal to `HumanText.of*` (SC-002, SC-004).
- **`--json` never reaches here** — JSON is written by the existing `*Json` path, byte-identical (SC-002).
- **Dependency confined** — only `FS.GG.Governance.HumanRender` references Spectre; verified by the
  dependency-boundary check (SC-007).
- **Tested via `TestConsole`** — Spectre's recording console at fixed widths drives deterministic rich/width tests
  with no real terminal.
