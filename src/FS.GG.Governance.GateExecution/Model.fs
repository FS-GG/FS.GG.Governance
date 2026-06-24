// The gate-execution domain vocabulary (F051). Visibility lives in Model.fsi (Principle II): this file
// carries NO `private`/`internal`/`public` modifiers on top-level bindings. These are the reproducible
// inputs and the sensed outcome for ONE gate execution, plus the injected execution-port type — all data,
// no behavior, so no stub. Every field REUSES the F032/F014 vocabulary VERBATIM, never redefined (FR-004,
// FR-011); nothing here starts a process or reads a clock (the I/O lives only in Interpreter.realPort).

namespace FS.GG.Governance.GateExecution

open FS.GG.Governance.Config.Model           // TimeoutLimit
open FS.GG.Governance.CommandRecord.Model     // Executable, Argument, WorkingDirectory, EnvironmentDelta,
                                              // ExitCode, CapturedOutput, SensedDuration

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type GateCommand =
        { Executable: Executable
          Arguments: Argument list
          WorkingDirectory: WorkingDirectory
          Environment: EnvironmentDelta
          Timeout: TimeoutLimit
          CapturedOutput: CapturedOutput }

    type ExecutionOutcome =
        { Stdout: byte[]
          Stderr: byte[]
          ExitCode: ExitCode
          Duration: SensedDuration }

    type ExecutionPort = GateCommand -> ExecutionOutcome
