// Curated public signature contract for the host-edge capability-sensing effect (F27 wiring, 063 §2).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Capability.fs carries NO access modifiers on top-level bindings.
//
// `senseCapability` is the EDGE EFFECT that fills `RenderMode.ColorCapability` from the real terminal
// (TTY/NO_COLOR/width) plus the host-parsed `--plain` flag. It is the only sensing point (FR-004): the
// pure `RenderMode.selectMode` decides Json/Plain/Rich from the filled record and never senses. Sensing
// (and Spectre) stays confined to HumanRender (FR-011, SC-007). This effect carries no standalone
// FSI/semantic test (Constitution I.3) — it is exercised indirectly at the dispatch edge by the
// render-mode dispatch tests over a Spectre TestConsole.

namespace FS.GG.Governance.HumanRender

open FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Capability =

    /// Sense the real terminal into a `RenderMode.ColorCapability`. `IsTty` is true when stdout is an
    /// attached console (not redirected/piped); `NoColorEnv` is true when `NO_COLOR` is set non-empty;
    /// `ExplicitPlain` carries the host-parsed `--plain`; `Width = None` when the width is unknown
    /// (redirected/no console) so a safe default is chosen at render time. No mode decision here.
    val senseCapability: explicitPlain: bool -> RenderMode.ColorCapability
