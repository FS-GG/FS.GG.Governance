// Curated public signature contract for the gate-run pure helpers (F052).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Plan.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings; the argv lexer's inner
// character scanner lives unexported, kept off-surface by absence here.
//
// Design-first artifact: drafted and committed BEFORE any Plan.fs body exists (Principle I) and exercised in
// FSI. These are the three genuinely-new pure pieces the host wiring needs: derive a GateCommand from a
// declared command spec, recover a reusable gate's prior exit code from its stored reference, and map an exit
// code to pass/fail. Everything is pure — no process, no clock, no I/O (the run itself is the injected F051
// port, at the command's interpreter edge).

namespace FS.GG.Governance.GateRun

open FS.GG.Governance.CommandRecord.Model      // Executable, Argument, ExitCode
open FS.GG.Governance.EvidenceReuse.Model       // EvidenceRef
open FS.GG.Governance.Config.Model              // ToolingFacts
open FS.GG.Governance.Gates.Model               // Gate
open FS.GG.Governance.GateExecution.Model        // GateCommand

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Plan =

    /// POSIX-style argv split of a declared command line into its executable + ordered arguments. Whitespace
    /// separates tokens; single quotes, double quotes, and backslash escapes group/quote a token; the first
    /// token is the `Executable`, the rest the ordered `Argument list` (order is identity-significant — F032
    /// D6). NO shell features (no globbing, variable expansion, pipes, or redirection) — a literal argv split
    /// only. `None` for an empty / all-whitespace line (a degenerate declared command — D1).
    val lexCommandLine: commandLine: string -> (Executable * Argument list) option

    /// Why a gate resolved to NO command-to-run — a typed reason instead of a bare `None`, so a caller can tell
    /// a genuine "the gate declares no command" from a misconfiguration (Principle VI: distinguish a defect
    /// from missing input). Each case is exactly one of the three former `None` outcomes of `commandFor`.
    ///   • `NoPrerequisite`         — the gate has no `RequiresCommand` prerequisite (it declares no command;
    ///                                the gate is `NotExecuted` — FR-005).
    ///   • `UnresolvedCommand id`   — the `RequiresCommand` `CommandId` resolves to no loaded `CommandSpec`.
    ///   • `EmptyCommandLine`       — the declared command line lexes to nothing (a degenerate command).
    type NoCommand =
        | NoPrerequisite
        | UnresolvedCommand of CommandId
        | EmptyCommandLine

    /// Derive the command-to-run for a gate from its DECLARED command spec (FR-002), or `Error` with the typed
    /// `NoCommand` reason (see above) — in every `Error` case the gate is `NotExecuted` (FR-005). The working
    /// directory is the governed `repoRoot`; the environment delta is EMPTY (the declared `EnvironmentClass` is
    /// a where-it-runs declaration, not an env mutation — no ambient-env leak, FR-002); the timeout is the
    /// declared `CommandSpec.Timeout` verbatim; the captured-output target is `NoCapturedOutput`.
    val commandFor: repoRoot: string -> tooling: ToolingFacts -> gate: Gate -> Result<GateCommand, NoCommand>

    /// Recover a reusable gate's prior `ExitCode` from its stored `EvidenceRef` — the F032 canonical-identity
    /// string (F049 `referenceOf`) embeds the exit code as the documented `exit=1<len>:<value>` segment (see
    /// specs/032-command-records/contracts/command-record-identity-format.md). `None` when the reference is
    /// not in canonical form, in which case the gate is conservatively RECOMPUTED rather than reused (FR-004,
    /// D2). This is the only place the otherwise-opaque reference is read (FR-015).
    val priorExitOf: reference: EvidenceRef -> ExitCode option

    /// Map an exit code to pass/fail: success (`ExitCode 0`) is a pass; any non-zero — including the F051
    /// `startFailureExitCode` / `timeoutExitCode` sentinels — is a fail (FR-006).
    val passed: exitCode: ExitCode -> bool
