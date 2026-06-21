// Command-record run-fact types for the command-record core (F032). The public surface is fixed by
// Model.fsi (Principle II); no top-level binding here carries an access modifier. These are product-neutral,
// YAML-free values that `CommandRecord.build` constructs and `canonicalId` projects over; they reuse the
// F014 `TimeoutLimit` verbatim rather than redefining it (FR-009). The only new shapes are the run facts.

namespace FS.GG.Governance.CommandRecord

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type Executable = Executable of string

    type Argument = Argument of string

    type WorkingDirectory = WorkingDirectory of string

    type ExitCode = ExitCode of int

    type OutputDigest = OutputDigest of string

    type EnvVarName = EnvVarName of string

    type EnvVarValue = EnvVarValue of string

    type CapturedOutputPath = CapturedOutputPath of string

    type SensedDuration = SensedDuration of nanoseconds: int64

    type CommandIdentity = CommandIdentity of string

    type AddedVar = { Name: EnvVarName; Value: EnvVarValue }

    type ChangedVar = { Name: EnvVarName; Old: EnvVarValue; New: EnvVarValue }

    type RemovedVar = { Name: EnvVarName; Old: EnvVarValue }

    type EnvironmentDelta =
        { Added: AddedVar list
          Changed: ChangedVar list
          Removed: RemovedVar list }

    type CapturedOutput =
        | CapturedAt of CapturedOutputPath
        | NoCapturedOutput

    type ReproducibleFacts =
        { Executable: Executable
          Arguments: Argument list
          WorkingDirectory: WorkingDirectory
          Environment: EnvironmentDelta
          Timeout: TimeoutLimit
          ExitCode: ExitCode
          StdoutDigest: OutputDigest
          StderrDigest: OutputDigest
          CapturedOutput: CapturedOutput }

    type CommandRecord =
        { Reproducible: ReproducibleFacts
          Duration: SensedDuration }
