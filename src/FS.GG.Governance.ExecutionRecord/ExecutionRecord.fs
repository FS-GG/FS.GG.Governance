// The pure execution-record bridge (F050). Visibility lives in ExecutionRecord.fsi (Principle II) — NO
// `private`/`internal`/`public` modifiers here. `digestOf` is the first and only place in the codebase that
// hashes output bytes (the gap F032 left open at D3): a four-step BCL pipeline — SHA-256 over the raw bytes,
// lowercase hex, wrapped in the F032 `OutputDigest` newtype reused verbatim. `recordOf` is `CommandRecord.build`
// composed with `digestOf` on the two output positions, nothing more — every other fact and the sensed duration
// are carried by `build` verbatim. Both bodies are PURE and TOTAL (FR-008): no I/O, no clock, no process;
// hashing in-memory bytes is pure computation, not I/O.

namespace FS.GG.Governance.ExecutionRecord

open System
open System.Security.Cryptography
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ExecutionRecord =

    let digestOf (bytes: byte[]) : OutputDigest =
        OutputDigest((Convert.ToHexString(SHA256.HashData bytes)).ToLowerInvariant())

    let recordOf
        (executable: Executable)
        (arguments: Argument list)
        (workingDirectory: WorkingDirectory)
        (environment: EnvironmentDelta)
        (timeout: TimeoutLimit)
        (exitCode: ExitCode)
        (stdout: byte[])
        (stderr: byte[])
        (capturedOutput: CapturedOutput)
        (duration: SensedDuration)
        : CommandRecord =
        CommandRecord.build
            executable
            arguments
            workingDirectory
            environment
            timeout
            exitCode
            (digestOf stdout)
            (digestOf stderr)
            capturedOutput
            duration
