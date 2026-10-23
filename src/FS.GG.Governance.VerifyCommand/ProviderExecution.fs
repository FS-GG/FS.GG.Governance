namespace FS.GG.Governance.VerifyCommand

open System
open System.IO
open System.Security.Cryptography
open System.Text
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateExecution.Model

module internal ProviderExecution =
    module CB = FS.GG.Governance.Config.CapabilityBindings
    module HM = FS.GG.Governance.Adapters.SddHandoff.Model
    module Reader = FS.GG.Governance.Adapters.SddHandoff.Reader
    module Capture = ProviderContextCapture
    module Gate = FS.GG.Governance.GateExecution.Interpreter

    let evidenceByteLimit = 1 <<< 20
    let sourceByteLimit = 16 <<< 20

    let captureLimits: CaptureLimits =
        { StdoutBytes = 1L <<< 20
          StderrBytes = 1L <<< 20
          AggregateBytes = 2L <<< 20 }

    // Governed relative path below the selected root, with every existing component checked to be a real
    // (non-link) entry, so an output or cited source cannot be read through a link out of the workspace.
    let resolve (root: string) (GovernedPath relative) : Result<string, string> =
        let segments = relative.Split('/')

        if String.IsNullOrWhiteSpace relative || Path.IsPathFullyQualified relative || relative.Contains '\\'
           || segments |> Array.exists (fun s -> s = "" || s = "." || s = "..") then
            Error "not a governed relative path"
        else
            let rec walk (current: string) (rest: string list) =
                match rest with
                | [] -> Ok current
                | segment :: more ->
                    let next = Path.Combine(current, segment)
                    let info = FileInfo next

                    if not (isNull info.LinkTarget) then
                        Error "a path component is a link"
                    else
                        walk next more

            walk root (List.ofArray segments)

    let present (path: string) =
        File.Exists path || Directory.Exists path || not (isNull (FileInfo path).LinkTarget)

    let beforeLaunch (root: string) (evidence: CB.EvidenceBinding) : Loop.ProviderEvidenceState option =
        try
            match resolve root evidence.OutputPath with
            | Error reason -> Some(Loop.Unreadable reason)
            | Ok path -> if present path then Some Loop.PresentBeforeLaunch else None
        with ex ->
            Some(Loop.Unreadable("output state could not be observed before launch: " + ex.GetType().Name))

    let sha256 (bytes: byte array) =
        "sha256:" + Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()

    // Bounded read of one regular file; None when absent.
    let readRegular (path: string) (limit: int) : Result<byte array option, string> =
        if Directory.Exists path then
            Error "not a regular file"
        elif not (File.Exists path) then
            Ok None
        else
            use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)

            if stream.Length > int64 limit then
                Error(sprintf "larger than %d bytes" limit)
            else
                let buffer = Array.zeroCreate<byte> (int stream.Length)
                let mutable read = 0
                let mutable finished = false

                while not finished && read < buffer.Length do
                    let n = stream.Read(buffer, read, buffer.Length - read)
                    if n = 0 then finished <- true else read <- read + n

                if read <> buffer.Length || stream.ReadByte() <> -1 then
                    Error "changed while it was read"
                else
                    Ok(Some buffer)

    // Governance's own digest of every source the produced document cites. A cited path that cannot be read
    // within bounds is left out, so the pure acceptance refuses it as uncaptured.
    let capturedSources (root: string) (source: string) (text: string) : HM.HandoffSource list =
        match Reader.parseWithSources { Source = source; Json = text } with
        | Error _ -> []
        | Ok sourced ->
            sourced.Sources
            |> List.choose (fun cited ->
                try
                    match resolve root cited.Path with
                    | Error _ -> None
                    | Ok path ->
                        match readRegular path sourceByteLimit with
                        | Ok(Some bytes) -> Some { Path = cited.Path; Digest = sha256 bytes }
                        | Ok None
                        | Error _ -> None
                with _ ->
                    None)

    let afterRun (root: string) (evidence: CB.EvidenceBinding) : Loop.ProviderEvidenceState =
        try
            match resolve root evidence.OutputPath with
            | Error reason -> Loop.Unreadable reason
            | Ok path ->
                match readRegular path evidenceByteLimit with
                | Error reason -> Loop.Unreadable reason
                | Ok None -> Loop.AbsentAfterRun
                | Ok(Some bytes) ->
                    match (try Ok(UTF8Encoding(false, true).GetString bytes) with :? DecoderFallbackException -> Error()) with
                    | Error() -> Loop.Unreadable "not strict UTF-8"
                    | Ok text ->
                        let (GovernedPath source) = evidence.OutputPath
                        Loop.Produced(text, capturedSources root source text)
        with ex ->
            Loop.Unreadable("output could not be read: " + ex.GetType().Name)

    let executeWith
        (root: string)
        (launch: Loop.ProviderGateRun -> Loop.ProviderLaunch)
        (runs: Loop.ProviderGateRun list)
        : Loop.ProviderGateObservation list =
        runs
        |> List.map (fun run ->
            let preconditions = run.Evidence |> List.map (fun evidence -> evidence, beforeLaunch root evidence)

            if preconditions |> List.exists (fun (_, state) -> state.IsSome) then
                // Stale or unobservable outputs are never overwritten, deleted or accepted: nothing starts.
                { Gate = run.Gate
                  Launch = Loop.NotLaunched "a declared evidence output was not absent before launch"
                  Evidence =
                    preconditions
                    |> List.map (fun (evidence, state) ->
                        ({ Evidence = evidence
                           State = state |> Option.defaultValue Loop.AbsentAfterRun }: Loop.ProviderEvidenceObservation)) }
            else
                let launched =
                    try
                        launch run
                    with ex ->
                        Loop.NotLaunched("launcher fault: " + ex.GetType().Name + ": " + ex.Message)

                { Gate = run.Gate
                  Launch = launched
                  Evidence =
                    run.Evidence
                    |> List.map (fun evidence ->
                        ({ Evidence = evidence; State = afterRun root evidence }: Loop.ProviderEvidenceObservation)) })

    type Host =
        { Session: Capture.CaptureSession
          Environment: Map<string, string>
          Cancellation: Threading.CancellationToken
          Now: unit -> MonotonicInstant
          TicksPerSecond: int64
          Pause: unit -> unit
          Report: string -> unit }

    let toolError message = "fsgg verify [tool-error]: " + message

    // Retry `release` until it is Ok. Past the original cleanup end the refusal is reported once and the host
    // keeps the owner; it stays resident while a release can still succeed. A refusal that can never succeed
    // (`permanent`, e.g. a lease past its cleanup end) is reported and left retained: the capture owner then stays
    // fenced and the Verify host applies its recorded settlement (#465). Nothing is abandoned or signaled.
    let settle
        (host: Host)
        (cleanupEnd: MonotonicInstant)
        (what: string)
        (permanent: BoundedFailure -> bool)
        (release: unit -> Result<unit, BoundedFailure>)
        =
        let rec attempt reported =
            match release () with
            | Ok() -> ()
            | Error cause ->
                let past = (host.Now()).Ticks >= cleanupEnd.Ticks

                if past && not reported then
                    host.Report(
                        toolError (
                            sprintf "%s is not released by the original cleanup end (%A); the host keeps it; teardown belongs to the outer admitted runtime" what cause
                        )
                    )

                if not (past && permanent cause) then
                    host.Pause()
                    attempt (reported || past)

        attempt false

    let launchHeld (host: Host) (run: Loop.ProviderGateRun) : Loop.ProviderLaunch =
        match Capture.borrowRepositoryRoot host.Session with
        | Error cause -> Loop.NotLaunched(sprintf "original repository root was not lent (capture revalidation or lease): %A" cause)
        | Ok lease ->
            let original = lease.Budget

            let launched =
                try
                    let now = host.Now()
                    let (TimeoutLimit seconds) = run.Command.Timeout

                    let commandEnd =
                        if seconds <= 0 then now.Ticks
                        elif int64 seconds > (Int64.MaxValue - now.Ticks) / host.TicksPerSecond then Int64.MaxValue
                        else now.Ticks + int64 seconds * host.TicksPerSecond

                    let request: BoundedRequest =
                        { Command = run.Command
                          Identity =
                            { Operation = "fsgg-verify-provider"
                              Launch = gateIdValue run.Gate }
                          Policy =
                            { Root = lease.RequestedPath
                              CapturedEnvironment = host.Environment
                              Descendants = AcceptUncontainedUnobservedDescendants
                              Paths = RequireStableAtomicPathBinding }
                          Budget =
                            { WorkEnd = { original.WorkEnd with Ticks = min original.WorkEnd.Ticks commandEnd }
                              CleanupEnd = original.CleanupEnd }
                          Limits = captureLimits
                          Cancellation = host.Cancellation }

                    match Gate.prepareHeldCwd lease run.WorkingDirectory request with
                    | Error failures -> Loop.NotLaunched(sprintf "GateExecution refused the held-cwd request: %A" failures)
                    | Ok session ->
                        // Once a session exists it is always settled by this owner, even if `run` faults; a fault is
                        // retained as a bounded failure so the observation can never read as a clean run.
                        let fault =
                            try
                                Gate.run session |> ignore
                                None
                            with ex ->
                                Some(LaunchFailed("run fault: " + ex.GetType().Name))

                        settle
                            host
                            original.CleanupEnd
                            ("provider execution session " + gateIdValue run.Gate)
                            (fun _ -> false)
                            (fun () -> Gate.release session)

                        let observed = Gate.observe session

                        match fault with
                        | None -> Loop.Launched observed
                        | Some failure ->
                            Loop.Launched { observed with SecondaryFailures = observed.SecondaryFailures @ [ failure ] }
                with ex ->
                    Loop.NotLaunched("held-cwd launch fault: " + ex.GetType().Name + ": " + ex.Message)

            settle
                host
                original.CleanupEnd
                "borrowed repository root lease"
                (fun cause -> cause = CleanupDeadlineReached)
                (fun () -> Gate.releaseDirectoryLease lease)
            launched
