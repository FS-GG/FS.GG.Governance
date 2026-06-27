// Curated public signature for the pure CLI rendering module (Phase E, 077).
//
// CliRender is a pure projection from CommandResult to a string (text or JSON), with NO
// filesystem or process access. It compiles AFTER Cli (research D2), so it reuses Cli's
// public pure vocabulary (exitCode, stableStrings, …). The four entry points below are
// RELOCATED from Cli.fsi (research D3); all *Json/*Text sub-writers and the render-only
// formatting helpers (commandName/modeName/exitCategory/quote/jsonArray/failureText/
// budgetLine/…) stay HIDDEN (absent from this .fsi → private).
//
// Byte-identity contract (FR-002, SC-001): every emitted text line and the
// `fsgg-governance.cli.v1` JSON envelope are unchanged from the pre-extraction module Cli
// output.

namespace FS.GG.Governance.Cli

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CliRender =

    /// Render a parse error for human text output. (Relocated from Cli.renderParseError.)
    val renderParseError: error: ParseError -> string

    /// Render a command result as deterministic terminal text. (Relocated from Cli.renderText.)
    val renderText: result: CommandResult -> string

    /// Render a command result as deterministic JSON with the stable `fsgg-governance.cli.v1`
    /// envelope. (Relocated from Cli.renderJson.)
    val renderJson: result: CommandResult -> string

    /// Select the renderer from the request format. Usage errors with no request render as
    /// text (never JSON). (Relocated from Cli.render.)
    val render: result: CommandResult -> string
