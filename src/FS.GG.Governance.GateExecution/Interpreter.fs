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
open FS.GG.Governance.Config.Model // TimeoutLimit
open FS.GG.Governance.CommandRecord.Model // ExitCode, CommandRecord, the env-delta newtypes
open FS.GG.Governance.ExecutionRecord // ExecutionRecord.recordOf (F050)
open FS.GG.Governance.GateExecution.Model // GateCommand, ExecutionOutcome, ExecutionPort

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

    // Read a drained buffer ONLY once its copy task has completed. Calling `MemoryStream.ToArray()` while
    // its `CopyToAsync` is still writing is a data race (M-CORE-2); if the bounded drain wait timed out the
    // task is still in flight, so we yield the empty partial rather than race. On any normal exit the copy
    // has finished and we get the full bytes.
    let drainedBytes (copyTask: Task) (buf: MemoryStream) : byte[] =
        if copyTask.IsCompleted then buf.ToArray() else [||]

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
                    {
                        Stdout = [||]
                        Stderr = Encoding.UTF8.GetBytes "gate process failed to start"
                        ExitCode = startFailureExitCode
                        Duration = nanos ()
                    }
                | proc ->
                    use proc = proc
                    // Drain BOTH redirected base byte streams CONCURRENTLY into in-memory buffers — raw bytes
                    // only, never ReadToEnd() text (FR-002), and both at once to avoid pipe-buffer deadlock.
                    let stdoutBuf = new MemoryStream()
                    let stderrBuf = new MemoryStream()
                    let outTask = proc.StandardOutput.BaseStream.CopyToAsync stdoutBuf
                    let errTask = proc.StandardError.BaseStream.CopyToAsync stderrBuf

                    // Wait for exit BOUNDED by the timeout (seconds). A non-positive limit waits zero — an
                    // applied timeout that terminates immediately. A very large limit (seconds > ~2.1M) would
                    // overflow `seconds * 1000` in int32 to a NEGATIVE wait, which WaitForExit rejects — the
                    // throw was then reified as a bogus start failure while the ALREADY-STARTED process leaked
                    // (#56/B3). Compute in int64 and clamp to Int32.MaxValue (~24.8 days) so the wait is never
                    // negative and the overrun branch (which kills the tree) stays reachable.
                    let waitMs =
                        if seconds <= 0 then
                            0
                        else
                            let ms = int64 seconds * 1000L

                            if ms > int64 System.Int32.MaxValue then
                                System.Int32.MaxValue
                            else
                                int ms

                    if proc.WaitForExit waitMs then
                        // Clean / within-limit exit: drain BOUNDED too (M-CORE-2) — a gate that spawned a
                        // pipe-inheriting background child holding the streams open must not hang the port
                        // forever. Then capture the real integer exit code and the elapsed duration.
                        (try
                            Task.WaitAll([| outTask; errTask |], 5000) |> ignore
                         with _ ->
                             ())

                        proc.WaitForExit() // ensure ExitCode is available

                        {
                            Stdout = drainedBytes outTask stdoutBuf
                            Stderr = drainedBytes errTask stderrBuf
                            ExitCode = ExitCode proc.ExitCode
                            Duration = nanos ()
                        }
                    else
                        // Overrun (FR-006): terminate the whole tree, drain whatever was captured (bounded so
                        // we never hang), and record timeoutExitCode + partial output + elapsed duration.
                        (try
                            proc.Kill true
                         with _ ->
                             ())

                        (try
                            Task.WaitAll([| outTask; errTask |], 5000) |> ignore
                         with _ ->
                             ())

                        {
                            Stdout = drainedBytes outTask stdoutBuf
                            Stderr = drainedBytes errTask stderrBuf
                            ExitCode = timeoutExitCode
                            Duration = nanos ()
                        }
            with ex ->
                // A start failure (e.g. a missing executable) is CAUGHT and reified as startFailureExitCode +
                // the exception message captured in the stderr bytes (the diagnostic), never thrown (FR-007).
                {
                    Stdout = [||]
                    Stderr = Encoding.UTF8.GetBytes ex.Message
                    ExitCode = startFailureExitCode
                    Duration = nanos ()
                }

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


    // One process-local clock domain; never serialized or interpreted after a restart.
    let boundedClockDomain = System.Guid.NewGuid()

    let currentInstant () : MonotonicInstant =
        { Domain = boundedClockDomain; Ticks = Stopwatch.GetTimestamp() }

    // Local mutation is confined to this retained I/O owner. No lock spans a BCL blocking operation.
    // Every Task is assigned to its owner before Start; no finalizer/disposal abandons retained work.
    [<Sealed>]
    type ExecutionSession(request: BoundedRequest, startInfo: ProcessStartInfo, beforeStart: unit -> Result<unit, BoundedFailure>) =
        let gate = obj ()
        let ownedChild = new Process(StartInfo = startInfo)
        let stdoutPrefix = Array.zeroCreate<byte> (int (min request.Limits.StdoutBytes request.Limits.AggregateBytes))
        let stderrPrefix = Array.zeroCreate<byte> (int (min request.Limits.StderrBytes request.Limits.AggregateBytes))
        let mutable stdoutRetained = 0
        let mutable stderrRetained = 0
        let mutable stdoutObserved = 0L
        let mutable stderrObserved = 0L
        let mutable aggregateObserved = 0L
        let mutable aggregateRetained = 0L
        let mutable stdoutState = Pending
        let mutable stderrState = Pending
        let mutable launch = NotStarted
        let mutable exit = None
        let mutable workflow = initDirect ()
        let mutable cancelled = false
        let mutable expired = false
        let mutable overflow = false
        let mutable closeRequested = false
        let mutable launchTask: Task option = None
        let mutable stdoutTask: Task option = None
        let mutable stderrTask: Task option = None
        let mutable exitTask: Task option = None
        let mutable closeTask: Task option = None
        let mutable releaseTask: Task option = None
        let mutable released = false
        let mutable settling = false
        let mutable stopIdentityReported = false
        let mutable workEnd = request.Budget.WorkEnd.Ticks

        let now () = Stopwatch.GetTimestamp()
        let completed = function None -> true | Some (t: Task) -> t.IsCompleted
        let saturatingAdd a b = if b > System.Int64.MaxValue - a then System.Int64.MaxValue else a + b

        let fail cause =
            // Failure tags are fixed metadata; payload text competes with captured bytes.
            // No exception message, environment value or unlimited diagnostic is retained.
            let payload rebuild (text: string) =
                let available = max 0L (request.Limits.AggregateBytes - aggregateRetained)
                let bytes = Encoding.UTF8.GetBytes text
                let keep = int (min available (int64 bytes.Length))
                aggregateRetained <- aggregateRetained + int64 keep
                rebuild (Encoding.UTF8.GetString(bytes, 0, keep))
            if workflow.FirstFailure <> Some cause && not (List.contains cause workflow.SecondaryFailures)
               && (workflow.FirstFailure.IsNone || workflow.SecondaryFailures.Length < 8) then
                let bounded =
                    match cause with
                    | InvalidRequest text -> payload InvalidRequest text
                    | LaunchFailed text -> payload LaunchFailed text
                    | ReadFailed text -> payload ReadFailed text
                    | CleanupFailed text -> payload CleanupFailed text
                    | other -> other
                workflow <- updateDirect (FailureObserved bounded) workflow |> fst

        let startOwned (setOwner: Task -> unit) (action: unit -> unit) =
            let task = new Task(System.Action action)
            setOwner task
            task.Start TaskScheduler.Default

        let readStream isStdout (stream: Stream) =
            // Fixed read-buffer overhead: two4096-byte buffers, independent of retained prefixes.
            let buffer = Array.zeroCreate<byte> 4096
            try
                let mutable reading = true
                while reading && now () < request.Budget.CleanupEnd.Ticks do
                    let count = stream.Read(buffer, 0, buffer.Length)
                    if count = 0 then
                        lock gate (fun () ->
                            let state = if closeRequested then ClosedBeforeEndOfFile else EndOfFile
                            if isStdout then stdoutState <- state else stderrState <- state)
                        reading <- false
                    else
                        lock gate (fun () ->
                            let n = int64 count
                            aggregateObserved <- saturatingAdd aggregateObserved n
                            if isStdout then stdoutObserved <- saturatingAdd stdoutObserved n
                            else stderrObserved <- saturatingAdd stderrObserved n
                            let retained = if isStdout then stdoutRetained else stderrRetained
                            let target = if isStdout then stdoutPrefix else stderrPrefix
                            let available = min (int64 (target.Length - retained)) (request.Limits.AggregateBytes - aggregateRetained)
                            let useful = workflow.Phase <> Retiring && now () < workEnd
                            let keep = if overflow || not useful then 0 else int (min n (max 0L available))
                            if keep > 0 then
                                System.Array.Copy(buffer, 0, target, retained, keep)
                                if isStdout then stdoutRetained <- retained + keep else stderrRetained <- retained + keep
                                aggregateRetained <- aggregateRetained + int64 keep
                            let streamObserved = if isStdout then stdoutObserved else stderrObserved
                            let streamLimit = if isStdout then request.Limits.StdoutBytes else request.Limits.StderrBytes
                            if (streamObserved > streamLimit || aggregateObserved > request.Limits.AggregateBytes) && not overflow then
                                overflow <- true
                                fail OutputLimitReached
                            if now () >= workEnd && not expired then
                                expired <- true
                                fail WorkDeadlineReached)
            with _ ->
                lock gate (fun () ->
                    if closeRequested then
                        if isStdout then stdoutState <- ClosedBeforeEndOfFile else stderrState <- ClosedBeforeEndOfFile
                    else
                        if isStdout then stdoutState <- StreamReadFailed else stderrState <- StreamReadFailed
                        fail (ReadFailed (if isStdout then "stdout-read" else "stderr-read")))

        let requestClose () =
            lock gate (fun () ->
                if not settling && closeTask.IsNone && launch = Started && now () < request.Budget.CleanupEnd.Ticks then
                    closeRequested <- true
                    // This task remains owned if close blocks or fails. Closing a pipe never establishes EOF.
                    startOwned (fun t -> closeTask <- Some t) (fun () ->
                        try
                            ownedChild.StandardOutput.BaseStream.Dispose()
                            ownedChild.StandardError.BaseStream.Dispose()
                        with _ -> lock gate (fun () -> fail (CleanupFailed "pipe-close"))))

        let canRelease () =
            completed launchTask
            && completed stdoutTask && completed stderrTask && completed exitTask && completed closeTask
            && (exit.IsSome || launch = NotStarted)

        let requestRelease () =
            lock gate (fun () ->
                if not released && releaseTask.IsNone && now () < request.Budget.CleanupEnd.Ticks then
                    if workflow.Phase = Prepared then
                        workflow <- updateDirect DirectResourcesSettled workflow |> fst
                    // This retirement continuation is owned/started within the ORIGINAL window.
                    // It may finish late; that later fact never turns the bounded return into success.
                    startOwned (fun t -> releaseTask <- Some t) (fun () ->
                        try
                            let launching = lock gate (fun () -> launchTask)
                            match launching with Some t -> t.Wait() | None -> ()
                            let activities = lock gate (fun () -> [stdoutTask; stderrTask; exitTask] |> List.choose id |> List.toArray)
                            if activities.Length > 0 then Task.WaitAll activities
                            let closing =
                                lock gate (fun () ->
                                    // No new close activity can be acquired after settlement begins.
                                    settling <- true
                                    closeTask)
                            match closing with Some t -> t.Wait() | None -> ()
                            let safe = lock gate (fun () -> canRelease ())
                            if safe then
                                ownedChild.Dispose()
                                lock gate (fun () ->
                                    if stdoutState = Pending then stdoutState <- ClosedBeforeEndOfFile
                                    if stderrState = Pending then stderrState <- ClosedBeforeEndOfFile
                                    released <- true
                                    workflow <- updateDirect DirectResourcesSettled workflow |> fst)
                        with _ -> lock gate (fun () -> fail (CleanupFailed "direct-release"))))

        let snapshot boundedReturn =
            lock gate (fun () ->
                let immutablePrefix (bytes: byte[]) count =
                    System.Collections.Immutable.ImmutableArray.CreateRange<byte>(bytes |> Array.take count)
                let frozenStdout = immutablePrefix stdoutPrefix stdoutRetained
                let frozenStderr = immutablePrefix stderrPrefix stderrRetained
                // Check publication after byte copying, independently of resource settlement.
                // Later observe alone never rewrites an originally timely bounded outcome.
                if boundedReturn && now () >= request.Budget.CleanupEnd.Ticks then
                    fail CleanupDeadlineReached
                { Identity = request.Identity
                  Launch = launch
                  DirectExit = exit
                  Stdout = { State = stdoutState; ObservedBytes = stdoutObserved; Prefix = frozenStdout }
                  Stderr = { State = stderrState; ObservedBytes = stderrObserved; Prefix = frozenStderr }
                  FirstFailure = workflow.FirstFailure
                  SecondaryFailures = workflow.SecondaryFailures
                  CancellationRequested = cancelled
                  WorkDeadlineReached = expired
                  OutputLimitReached = overflow
                  Settlement = if released && completed releaseTask then Released else Retained
                  Descendants = UncontainedUnobserved })

        member _.Identity = request.Identity
        member _.Observe() = snapshot false

        member _.Run() =
            let selected =
                lock gate (fun () ->
                    let model, effects = updateDirect RunRequested workflow
                    workflow <- model
                    if effects = [LaunchDirectChild] then
                        if request.Cancellation.IsCancellationRequested then
                            cancelled <- true
                            fail CancellationRequested
                            false
                        elif now () >= workEnd then
                            expired <- true
                            fail WorkDeadlineReached
                            false
                        else
                            launch <- Starting
                            let (TimeoutLimit seconds) = request.Command.Timeout
                            let frequency = Stopwatch.Frequency
                            let duration = if int64 seconds > System.Int64.MaxValue / frequency then System.Int64.MaxValue else int64 seconds * frequency
                            workEnd <- min workEnd (saturatingAdd (now ()) duration)
                            startOwned (fun t -> launchTask <- Some t) (fun () ->
                                try
                                    match beforeStart () with
                                    | Error cause -> lock gate (fun () -> launch <- NotStarted; fail cause)
                                    | Ok () ->
                                        // Revalidate cancellation/deadline after mutable path checks, before Start.
                                        let permitted = lock gate (fun () -> not request.Cancellation.IsCancellationRequested && now () < workEnd)
                                        if not permitted then
                                            lock gate (fun () ->
                                                launch <- NotStarted
                                                if request.Cancellation.IsCancellationRequested then cancelled <- true; fail CancellationRequested
                                                else expired <- true; fail WorkDeadlineReached)
                                        elif ownedChild.Start() then
                                            lock gate (fun () ->
                                                launch <- Started
                                                workflow <- updateDirect LaunchObserved workflow |> fst
                                                if request.Cancellation.IsCancellationRequested && not cancelled then
                                                    cancelled <- true
                                                    fail CancellationRequested
                                                if now () >= workEnd && not expired then
                                                    expired <- true
                                                    fail WorkDeadlineReached
                                                if workflow.FirstFailure.IsSome && not stopIdentityReported then
                                                    stopIdentityReported <- true
                                                    fail StopIdentityUnknown
                                                startOwned (fun t -> stdoutTask <- Some t) (fun () -> readStream true ownedChild.StandardOutput.BaseStream)
                                                startOwned (fun t -> stderrTask <- Some t) (fun () -> readStream false ownedChild.StandardError.BaseStream)
                                                startOwned (fun t -> exitTask <- Some t) (fun () ->
                                                    try
                                                        ownedChild.WaitForExit()
                                                        let observed = ExitCode ownedChild.ExitCode
                                                        lock gate (fun () ->
                                                            exit <- Some observed
                                                            if now () >= workEnd && not expired then
                                                                expired <- true
                                                                fail WorkDeadlineReached)
                                                    with _ -> lock gate (fun () -> fail (CleanupFailed "direct-exit-observation"))))
                                        else lock gate (fun () -> launch <- NotStarted; fail (LaunchFailed "start-returned-false"))
                                with _ ->
                                    lock gate (fun () ->
                                        if launch = Started then
                                            // A later acquisition fault cannot erase a positively observed Start.
                                            fail (CleanupFailed "post-start-acquisition")
                                        else
                                            launch <- LaunchOutcomeUnknown
                                            fail (LaunchFailed "start-outcome-unknown")))
                            true
                    else false)
            requestRelease ()
            let mutable observing = selected
            while observing && now () < request.Budget.CleanupEnd.Ticks do
                let retiring =
                    lock gate (fun () ->
                        if request.Cancellation.IsCancellationRequested && not cancelled then cancelled <- true; fail CancellationRequested
                        let usefulPending = exit.IsNone || not (completed stdoutTask && completed stderrTask)
                        if usefulPending && now () >= workEnd && not expired then expired <- true; fail WorkDeadlineReached
                        workflow.FirstFailure.IsSome)
                if retiring then
                    lock gate (fun () ->
                        // Runtime generation-safe signaling is unqualified: no Kill/PID signal is attempted.
                        if launch = Started && exit.IsNone && not stopIdentityReported then
                            stopIdentityReported <- true
                            fail StopIdentityUnknown)
                    requestClose ()
                requestRelease ()
                observing <- lock gate (fun () -> not (released && completed releaseTask))
                if observing then System.Threading.Thread.Sleep 1
            snapshot true

        member _.Release() =
            requestRelease ()
            lock gate (fun () ->
                if released && completed releaseTask then Ok ()
                elif now () >= request.Budget.CleanupEnd.Ticks then Error CleanupDeadlineReached
                else Error (CleanupFailed "direct-resources-pending"))

    let run (session: ExecutionSession) = session.Run()
    let observe (session: ExecutionSession) = session.Observe()
    let release (session: ExecutionSession) = session.Release()


    // Refreshed pathname/metadata observations only: no inode identity or atomic start binding.
    type ObservedDirectory =
        { Path: string
          Attributes: FileAttributes
          CreationTicks: int64
          WriteTicks: int64
          LinkTarget: string option }

    let validText maximum (value: string) =
        not (System.Object.ReferenceEquals(value, null))
        && value.Length <= maximum
        && not (value.Contains '\000')

    let selectedPaths (root: string) (cwd: string) =
        try
            if not (validText 4096 root && validText 4096 cwd)
               || not (Path.IsPathFullyQualified root && Path.IsPathFullyQualified cwd) then
                Error (InvalidRequest "absolute-root-cwd-required")
            else
                let normalizedRoot = Path.GetFullPath root |> Path.TrimEndingDirectorySeparator
                let normalizedCwd = Path.GetFullPath cwd |> Path.TrimEndingDirectorySeparator
                let boundary =
                    if normalizedRoot.EndsWith(string Path.DirectorySeparatorChar, System.StringComparison.Ordinal) then normalizedRoot
                    else normalizedRoot + string Path.DirectorySeparatorChar
                if normalizedCwd <> normalizedRoot && not (normalizedCwd.StartsWith(boundary, System.StringComparison.Ordinal)) then
                    Error (InvalidRequest "cwd-outside-selected-root")
                else Ok (normalizedRoot, normalizedCwd)
        with _ -> Error (InvalidRequest "path-normalization-failed")

    let directoryComponents (path: string) =
        match Path.GetPathRoot path |> Option.ofObj with
        | None -> Error (InvalidRequest "path-root-unavailable")
        | Some prefix ->
            let parts =
                path.Substring(prefix.Length).Split([| Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar |], System.StringSplitOptions.RemoveEmptyEntries)
            if parts.Length > 128 then Error (InvalidRequest "path-observation-depth")
            else
                let _, paths =
                    parts |> Array.fold (fun (parent, paths) part ->
                        let next = Path.Combine(parent, part)
                        next, next :: paths) (prefix, [prefix])
                Ok (List.rev paths)

    let observeDirectories root cwd =
        match directoryComponents root, directoryComponents cwd with
        | Error cause, _ | _, Error cause -> Error cause
        | Ok rootParts, Ok cwdParts ->
            try
                let mutable failure = None
                let boundary =
                    if root.EndsWith(string Path.DirectorySeparatorChar, System.StringComparison.Ordinal) then root
                    else root + string Path.DirectorySeparatorChar
                // Observe the selected root, cwd and intervening components. Unrelated ancestor
                // directory writes are not changes to the caller's selected path observations.
                let observations =
                    rootParts @ cwdParts
                    |> List.distinct
                    |> List.filter (fun path -> path = root || path.StartsWith(boundary, System.StringComparison.Ordinal))
                    |> List.choose (fun path ->
                        let directory = DirectoryInfo path
                        directory.Refresh()
                        if not directory.Exists then
                            failure <- Some (InvalidRequest "directory-observation-missing")
                            None
                        else
                            let attributes = directory.Attributes
                            let target = directory.LinkTarget |> Option.ofObj
                            if (attributes &&& FileAttributes.Directory) = enum<FileAttributes> 0
                               || (attributes &&& FileAttributes.ReparsePoint) <> enum<FileAttributes> 0
                               || target.IsSome then
                                failure <- Some (InvalidRequest "directory-link-or-type-unsupported")
                                None
                            else
                                Some { Path = path; Attributes = attributes
                                       CreationTicks = directory.CreationTimeUtc.Ticks
                                       WriteTicks = directory.LastWriteTimeUtc.Ticks
                                       LinkTarget = target })
                match failure with Some cause -> Error cause | None -> Ok observations
            with _ -> Error (InvalidRequest "directory-observation-failed")

    let capturedEnvironment (snapshot: Map<string, string>) (delta: EnvironmentDelta) =
        try
            let nameValid name = validText 256 name && not (System.String.IsNullOrWhiteSpace name) && not (name.Contains '=')
            let valueValid value = validText 32768 value
            if System.Object.ReferenceEquals(snapshot, null) then Error (InvalidRequest "environment-snapshot-null")
            elif snapshot |> Map.exists (fun name value -> not (nameValid name && valueValid value)) then
                Error (InvalidRequest "environment-snapshot-invalid")
            else
                let comparer = if System.OperatingSystem.IsWindows() then System.StringComparer.OrdinalIgnoreCase else System.StringComparer.Ordinal
                let keys = System.Collections.Generic.HashSet<string>(comparer)
                let mutable failure = None
                for name, _ in Map.toList snapshot do
                    if not (keys.Add name) then failure <- Some (InvalidRequest "environment-key-alias")
                let mutable result = snapshot
                let changedNames = System.Collections.Generic.HashSet<string>(comparer)
                let claim name =
                    if not (nameValid name) || not (changedNames.Add name) then
                        failure <- Some (InvalidRequest "environment-delta-name")
                for item in delta.Added do
                    let (EnvVarName name) = item.Name
                    let (EnvVarValue value) = item.Value
                    claim name
                    if not (valueValid value) || keys.Contains name then failure <- Some (InvalidRequest "environment-added-baseline")
                    else result <- Map.add name value result
                for item in delta.Changed do
                    let (EnvVarName name) = item.Name
                    let (EnvVarValue oldValue) = item.Old
                    let (EnvVarValue newValue) = item.New
                    claim name
                    if not (valueValid oldValue && valueValid newValue) || Map.tryFind name snapshot <> Some oldValue || oldValue = newValue then
                        failure <- Some (InvalidRequest "environment-changed-baseline")
                    else result <- Map.add name newValue result
                for item in delta.Removed do
                    let (EnvVarName name) = item.Name
                    let (EnvVarValue oldValue) = item.Old
                    claim name
                    if not (valueValid oldValue) || Map.tryFind name snapshot <> Some oldValue then failure <- Some (InvalidRequest "environment-removed-baseline")
                    else result <- Map.remove name result
                match failure with Some cause -> Error cause | None -> Ok result
        with _ -> Error (InvalidRequest "environment-delta-invalid")

    let prepare (request: BoundedRequest) =
        try
            let now = currentInstant ()
            let (Executable executable) = request.Command.Executable
            let (WorkingDirectory cwd) = request.Command.WorkingDirectory
            let (TimeoutLimit seconds) = request.Command.Timeout
            let limits = request.Limits
            let failures =
                [
                    match validateCurrentHostPolicy request.Policy with Some cause -> yield cause | None -> ()
                    match validateBudget now request.Budget with Some cause -> yield cause | None -> ()
                    if not (validText 128 request.Identity.Operation && validText 128 request.Identity.Launch)
                       || System.String.IsNullOrWhiteSpace request.Identity.Operation || System.String.IsNullOrWhiteSpace request.Identity.Launch then
                        yield InvalidRequest "original-launch-identity-invalid"
                    if not (validText 4096 executable) || not (Path.IsPathFullyQualified executable) then
                        yield InvalidRequest "absolute-executable-required"
                    if request.Command.Arguments |> List.exists (fun (Argument value) -> not (validText 32768 value)) then
                        yield InvalidRequest "literal-argument-invalid"
                    if seconds <= 0 then yield InvalidRequest "command-timeout-invalid"
                    if limits.StdoutBytes < 0L || limits.StderrBytes < 0L || limits.AggregateBytes < 0L
                       || limits.StdoutBytes > int64 System.Int32.MaxValue || limits.StderrBytes > int64 System.Int32.MaxValue
                       || limits.AggregateBytes > int64 System.Int32.MaxValue then yield InvalidRequest "capture-limits-invalid"
                ]
            if not failures.IsEmpty then Error (failures |> List.distinct |> List.truncate 8)
            else
                match selectedPaths request.Policy.Root cwd, capturedEnvironment request.Policy.CapturedEnvironment request.Command.Environment with
                | Error cause, _ | _, Error cause -> Error [cause]
                | Ok (root, normalizedCwd), Ok environment ->
                    match observeDirectories root normalizedCwd with
                    | Error cause -> Error [cause]
                    | Ok originalObservation ->
                        let startInfo = buildStartInfo request.Command
                        startInfo.WorkingDirectory <- normalizedCwd
                        startInfo.Environment.Clear()
                        for name, value in Map.toList environment do startInfo.Environment.[name] <- value
                        let beforeStart () =
                            match validateBudget (currentInstant ()) request.Budget with
                            | Some cause -> Error cause
                            | None ->
                                match observeDirectories root normalizedCwd with
                                | Error cause -> Error cause
                                | Ok current when current <> originalObservation -> Error (InvalidRequest "directory-observation-changed")
                                | Ok _ -> Ok ()
                        Ok (ExecutionSession(request, startInfo, beforeStart))
        with _ -> Error [InvalidRequest "request-preparation-failed"]
