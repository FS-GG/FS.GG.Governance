// The EDGE of gate execution (F051) — the codebase's FIRST and ONLY process-spawning capability.
// Visibility lives in Interpreter.fsi (Principle II): this file carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — the process-spawning helpers stay unexported by ABSENCE from the .fsi.
//
// `realPort` is the sole place a process starts; `senseExecution` is PURE GIVEN THE PORT (edge I/O + the
// pure F050 `recordOf`), so tests drive it with a deterministic fake and reach no process, no network, no
// governed repository. The port is TOTAL & SAFE (Principle VI): it records, never throws or hangs.
//
// Local mutation is DISCLOSED and CONFINED to `realPort` (Principle III): `MemoryStream`/`Process`/
// `Stopwatch` are inherently mutable BCL objects, and the two redirected streams are drained CONCURRENTLY
// (each on an async copy) to avoid the classic pipe-buffer deadlock. No shared mutable state escapes.

namespace FS.GG.Governance.GateExecution

open System.Text
open System.IO
open System.Diagnostics
open System.Threading.Tasks
open FS.GG.Governance.Config.Model            // TimeoutLimit
open FS.GG.Governance.CommandRecord.Model      // ExitCode, CommandRecord, the env-delta newtypes
open FS.GG.Governance.ExecutionRecord          // ExecutionRecord.recordOf (F050)
open FS.GG.Governance.GateExecution.Model        // GateCommand, ExecutionOutcome, ExecutionPort

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    // The two sentinel exit codes are values an ordinary successful gate would not return (data-model.md
    // §Sentinel exit codes). 127 is the POSIX shell convention for "command not found" and 124 the
    // GNU `timeout(1)` convention for "killed for exceeding the limit"; here they are recorded ALONGSIDE a
    // captured diagnostic / partial output so a consumer can distinguish a tool-level failure-to-start or
    // timeout from an ordinary gate exit by these named values (Principle VI).
    let startFailureExitCode: ExitCode = ExitCode 127

    let timeoutExitCode: ExitCode = ExitCode 124

    // Apply the environment DELTA's three classes to a start-info's environment (research D7): Added/Changed
    // SET the value, Removed DELETES the key. The delta is applied, not diffed back.
    let applyEnv (psi: ProcessStartInfo) (env: EnvironmentDelta) : unit =
        for a in env.Added do
            let (EnvVarName name) = a.Name
            let (EnvVarValue value) = a.Value
            psi.Environment.[name] <- value

        for c in env.Changed do
            let (EnvVarName name) = c.Name
            let (EnvVarValue value) = c.New
            psi.Environment.[name] <- value

        for r in env.Removed do
            let (EnvVarName name) = r.Name
            psi.Environment.Remove name |> ignore

    // Build the start-info from the command: the executable, the ORDERED arguments via ArgumentList (no
    // shell string-splitting), the working directory, redirected stdout/stderr, no shell execution.
    let buildStartInfo (command: GateCommand) : ProcessStartInfo =
        let (Executable exe) = command.Executable
        let psi = ProcessStartInfo exe

        for arg in command.Arguments do
            let (Argument a) = arg
            psi.ArgumentList.Add a

        let (WorkingDirectory wd) = command.WorkingDirectory
        psi.WorkingDirectory <- wd
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        applyEnv psi command.Environment
        psi

    let realPort: ExecutionPort =
        fun command ->
            let (TimeoutLimit seconds) = command.Timeout
            // Stopwatch ticks are 100-ns units; *100 yields the nanoseconds SensedDuration carries.
            let sw = Stopwatch.StartNew()
            let nanos () = SensedDuration(sw.Elapsed.Ticks * 100L)

            try
                let psi = buildStartInfo command

                match Process.Start psi with
                | null ->
                    // A null process is a start failure — reified, never thrown (FR-007).
                    { Stdout = [||]
                      Stderr = Encoding.UTF8.GetBytes "gate process failed to start"
                      ExitCode = startFailureExitCode
                      Duration = nanos () }
                | proc ->
                    use proc = proc
                    // Drain BOTH redirected base byte streams CONCURRENTLY into in-memory buffers — raw bytes
                    // only, never ReadToEnd() text (FR-002), and both at once to avoid pipe-buffer deadlock.
                    let stdoutBuf = new MemoryStream()
                    let stderrBuf = new MemoryStream()
                    let outTask = proc.StandardOutput.BaseStream.CopyToAsync stdoutBuf
                    let errTask = proc.StandardError.BaseStream.CopyToAsync stderrBuf

                    // Wait for exit BOUNDED by the timeout (seconds). A non-positive limit waits zero — an
                    // applied timeout that terminates immediately.
                    let waitMs = if seconds <= 0 then 0 else seconds * 1000

                    if proc.WaitForExit waitMs then
                        // Clean / within-limit exit: ensure the streams are fully drained, then capture the
                        // real integer exit code and the elapsed duration.
                        (try
                            Task.WaitAll [| outTask; errTask |]
                         with _ ->
                             ())

                        proc.WaitForExit() // ensure ExitCode is available

                        { Stdout = stdoutBuf.ToArray()
                          Stderr = stderrBuf.ToArray()
                          ExitCode = ExitCode proc.ExitCode
                          Duration = nanos () }
                    else
                        // Overrun (FR-006): terminate the whole tree, drain whatever was captured (bounded so
                        // we never hang), and record timeoutExitCode + partial output + elapsed duration.
                        (try proc.Kill true with _ -> ())

                        (try
                            Task.WaitAll([| outTask; errTask |], 5000) |> ignore
                         with _ ->
                             ())

                        { Stdout = stdoutBuf.ToArray()
                          Stderr = stderrBuf.ToArray()
                          ExitCode = timeoutExitCode
                          Duration = nanos () }
            with ex ->
                // A start failure (e.g. a missing executable) is CAUGHT and reified as startFailureExitCode +
                // the exception message captured in the stderr bytes (the diagnostic), never thrown (FR-007).
                { Stdout = [||]
                  Stderr = Encoding.UTF8.GetBytes ex.Message
                  ExitCode = startFailureExitCode
                  Duration = nanos () }

    let senseExecution (port: ExecutionPort) (command: GateCommand) : CommandRecord =
        // Edge I/O + the pure F050 `recordOf` (mirrors `Snapshot.senseSnapshot` = edge I/O + pure `assemble`).
        // PURE GIVEN THE PORT: this starts no process itself. The two captured buffers become StdoutDigest /
        // StderrDigest (never swapped); the exit code and duration come from the outcome; every other
        // reproducible fact is carried VERBATIM from the command. No success/exit-code/reuse policy (FR-005).
        let outcome = port command

        ExecutionRecord.recordOf
            command.Executable
            command.Arguments
            command.WorkingDirectory
            command.Environment
            command.Timeout
            outcome.ExitCode
            outcome.Stdout
            outcome.Stderr
            command.CapturedOutput
            outcome.Duration
