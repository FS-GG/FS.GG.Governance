# Contract: `FS.GG.Governance.HumanText.RenderMode` (pure)

The render-mode decision: JSON (contract) vs Plain (default human / fallback) vs Rich (interactive). JSON always
overrides terminal state (FR-004, edge case "--json always wins"). Pure and total — sensing is a separate edge
effect.

## `RenderMode.fsi` (draft)

```fsharp
namespace FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderMode =

    type RenderMode =
        | Json
        | Plain
        | Rich

    type ColorCapability =
        { IsTty: bool
          NoColorEnv: bool
          ExplicitPlain: bool
          Width: int option }

    /// JSON always wins; else Rich iff an interactive, color-enabled, non-plain terminal; else Plain.
    val selectMode: explicitJson: bool -> capability: ColorCapability -> RenderMode
```

## Behavior (truth table — SC-004)

| explicitJson | IsTty | NoColorEnv | ExplicitPlain | ⇒ mode |
|---|---|---|---|---|
| true | * | * | * | `Json` |
| false | true | false | false | `Rich` |
| false | true | true | false | `Plain` |
| false | true | false | true | `Plain` |
| false | false | * | * | `Plain` |

`Width = None` is not part of the mode decision; it is consumed at render time as a safe default (edge case
"unknown width"). Total over the boolean product; no I/O.
