// The host-edge capability-sensing effect (F27 wiring, 063 §2). Visibility lives in Capability.fsi
// (Principle II) — this file carries NO access modifiers. This is the ONLY place TTY/NO_COLOR/width are
// sensed (FR-004); the pure decision is `RenderMode.selectMode`. Spectre and sensing stay confined to
// HumanRender (FR-011, SC-007).

namespace FS.GG.Governance.HumanRender

open System
open FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Capability =

    // stdout attached to a console (interactive) iff it is NOT redirected/piped to a file or pipe.
    let private senseTty () : bool =
        try not Console.IsOutputRedirected with _ -> false

    // NO_COLOR is honored when SET TO ANY non-empty value (the de-facto convention).
    let private senseNoColor () : bool =
        match Environment.GetEnvironmentVariable "NO_COLOR" with
        | null -> false
        | "" -> false
        | _ -> true

    // The terminal column count when a console is attached; `None` when unknown (redirected/no console,
    // or a zero/negative width) so a safe default is chosen at render time — width is NOT part of the
    // mode decision.
    let private senseWidth () : int option =
        try
            let w = Console.WindowWidth
            if w > 0 then Some w else None
        with _ ->
            None

    let senseCapability (explicitPlain: bool) : RenderMode.ColorCapability =
        { IsTty = senseTty ()
          NoColorEnv = senseNoColor ()
          ExplicitPlain = explicitPlain
          Width = senseWidth () }
