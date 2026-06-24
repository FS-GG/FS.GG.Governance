// Curated public signature contract for the gate-execution domain vocabulary (F051).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and committed BEFORE any Model.fs body exists (Principle I) and exercised in
// FSI (scripts/prelude.fsx). These are the reproducible inputs and the sensed outcome for ONE gate execution,
// plus the injected execution port type. Every field REUSES the F032/F014 vocabulary VERBATIM — opened from
// `FS.GG.Governance.CommandRecord.Model` and `FS.GG.Governance.Config.Model`, never redefined (FR-004,
// FR-011). Nothing here starts a process or reads a clock; the I/O lives only in Interpreter.realPort.

namespace FS.GG.Governance.GateExecution

open FS.GG.Governance.Config.Model           // TimeoutLimit
open FS.GG.Governance.CommandRecord.Model     // Executable, Argument, WorkingDirectory, EnvironmentDelta,
                                              // ExitCode, CapturedOutput, SensedDuration

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The REPRODUCIBLE inputs for one gate execution — the command-to-run (Key entity: "Gate
    /// command-to-run"). All F032/F014 vocabulary reused verbatim: the program, its ORDERED arguments
    /// (argument order is significant in the identity), the working directory, the environment DELTA (a
    /// three-class partition, not a full snapshot), the timeout to enforce, and the captured-output target
    /// (`NoCapturedOutput` in the common case). Carries NO bytes, NO clock reading, NO product vocabulary.
    type GateCommand =
        { Executable: Executable
          Arguments: Argument list
          WorkingDirectory: WorkingDirectory
          Environment: EnvironmentDelta
          Timeout: TimeoutLimit
          CapturedOutput: CapturedOutput }

    /// The SENSED result of one run (Key entity: "Captured execution outcome") — the raw stdout/stderr BYTES
    /// captured verbatim (no decoding, locale, normalization, or truncation), the integer exit code (or a
    /// sentinel for start failure / timeout — see Interpreter), and the measured wall-clock duration. This is
    /// the value the injected port YIELDS and that F050 `ExecutionRecord.recordOf` CONSUMES; the duration is
    /// the sole non-deterministic fact and is held apart (excluded from the canonical identity, F050 FR-006).
    type ExecutionOutcome =
        { Stdout: byte[]
          Stderr: byte[]
          ExitCode: ExitCode
          Duration: SensedDuration }

    /// The injected execution port (Key entity: "Execution port (injected)") — a function value that runs ONE
    /// gate command and yields its captured outcome. TOTAL by contract: a start failure or timeout is reified
    /// into an ordinary outcome carrying a sentinel exit code, NEVER an exception (FR-007, FR-008). This is
    /// the sole seam through which the feature touches a process (FR-010); `Interpreter.realPort` is the real
    /// implementation, and tests supply a deterministic fake of this exact shape.
    type ExecutionPort = GateCommand -> ExecutionOutcome
