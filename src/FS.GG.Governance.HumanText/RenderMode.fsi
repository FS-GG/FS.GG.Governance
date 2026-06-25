// Curated public signature contract for the render-mode decision (F27, §1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching RenderMode.fs carries NO `private`/`internal`/`public` modifiers on top-level
// bindings — visibility is presence/absence here.
//
// `selectMode` is PURE and TOTAL: it decides how a command's report is rendered from a sensed
// terminal capability + the explicit `--json` flag. JSON is the only contract and ALWAYS wins;
// Plain/Rich are the non-contractual human views. The actual TTY/NO_COLOR/width sensing is an
// Effect executed at the HumanRender interpreter edge — never in this pure function (FR-004, D6).

namespace FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderMode =

    /// How a command's report is rendered. `Json` is the byte-identical automation contract;
    /// `Plain` and `Rich` are non-contractual human projections of the SAME report object.
    type RenderMode =
        | Json
        | Plain
        | Rich

    /// The sensed terminal capability — filled by the edge capability-sensing effect, NEVER sensed
    /// in a pure function. `Width = None` means unknown ⇒ a safe default is chosen at render time
    /// (it is NOT part of the mode decision).
    type ColorCapability =
        { IsTty: bool
          NoColorEnv: bool
          ExplicitPlain: bool
          Width: int option }

    /// Decide the render mode (pure, total). `explicitJson = true` ⇒ `Json` (always wins, whatever
    /// the capability); else `Rich` iff `IsTty && not NoColorEnv && not ExplicitPlain`; else `Plain`.
    val selectMode: explicitJson: bool -> cap: ColorCapability -> RenderMode
