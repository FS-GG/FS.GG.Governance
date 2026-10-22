namespace FS.GG.Governance.VerifyCommand

open System
open System.Threading
open FS.GG.Governance.GateExecution.Model

module internal ProviderHost =
    module Capture = ProviderContextCapture

    type Admission = { Budget: ExecutionBudget; Cancellation: CancellationToken }

    type Effects =
        { Environment: string -> string option
          Now: unit -> MonotonicInstant
          TicksPerSecond: int64
          Cancellation: unit -> CancellationToken
          Prepare: Capture.Request -> Result<Capture.CaptureSession, Capture.Failure list>
          Pause: unit -> unit }

    let workSecondsVariable = "FSGG_VERIFY_WORK_SECONDS"
    let cleanupSecondsVariable = "FSGG_VERIFY_CLEANUP_SECONDS"
    let private maximumSeconds = 1_000_000

    let private seconds (effects: Effects) name =
        match effects.Environment name with
        | None -> Error(sprintf "%s is not set" name)
        | Some text ->
            match Int32.TryParse(text, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
            | true, value when value > 0 && value <= maximumSeconds -> Ok value
            | _ -> Error(sprintf "%s must be a whole number of seconds from 1 to %d" name maximumSeconds)

    let admit (effects: Effects) (start: MonotonicInstant) : Result<Admission, string> =
        match seconds effects workSecondsVariable, seconds effects cleanupSecondsVariable with
        | Error message, _
        | _, Error message ->
            Error(
                "provider capture requires the original work and cleanup ends admitted by the outer runtime: " + message
            )
        | Ok work, Ok cleanup when cleanup < work ->
            Error(
                sprintf "provider capture cleanup end (%s=%d) precedes its work end (%s=%d)" cleanupSecondsVariable cleanup workSecondsVariable work
            )
        | Ok work, Ok cleanup ->
            Ok
                { Budget =
                    { WorkEnd = { start with Ticks = start.Ticks + int64 work * effects.TicksPerSecond }
                      CleanupEnd = { start with Ticks = start.Ticks + int64 cleanup * effects.TicksPerSecond } }
                  Cancellation = effects.Cancellation() }

    let commandContext (effects: Effects) (repoRoot: string) (admission: Admission) (now: MonotonicInstant) : FS.GG.Governance.GateRun.Plan.ProviderCommandContext =
        let remaining = (admission.Budget.WorkEnd.Ticks - now.Ticks) / effects.TicksPerSecond
        { RepoRoot = repoRoot
          Environment =
            (match effects.Environment "CI" with
             | Some value when value <> "" -> FS.GG.Governance.Config.Model.Ci
             | _ -> FS.GG.Governance.Config.Model.Local)
          EnvironmentDelta = { Added = []; Changed = []; Removed = [] }
          RemainingTimeout = FS.GG.Governance.Config.Model.TimeoutLimit(int (max 0L (min remaining (int64 maximumSeconds)))) }

    type Settlement =
        | Released
        | ReleasedAfterUnsettledReport
        | RetainedWithoutPendingRead

    let exitCode (inner: int) (settlement: Settlement) (sessionFailed: bool) : int =
        match settlement with
        | Released when inner = 0 && sessionFailed -> Loop.exitCode Loop.ToolError
        | Released -> inner
        | ReleasedAfterUnsettledReport
        | RetainedWithoutPendingRead -> Loop.exitCode Loop.ToolError

    type Supply =
        Loop.RunRequest -> ProviderContext.Selection ->
            Result<Capture.CaptureSession * FS.GG.Governance.GateRun.Plan.ProviderCommandContext, string>

    let private captureLimits : Capture.Limits = { PerFileBytes = 1 <<< 20; AggregateBytes = 1 <<< 21 }

    let private toolError message = "fsgg verify [tool-error]: " + message

    // The sole release site. Release is attempted until it is Ok; an Error is never answered with disposal,
    // replacement, renewal or a signal. Past the original cleanup end a session in which a read may be
    // pending keeps this host resident (reported once); only the outer admitted runtime ends that.
    let private settle (effects: Effects) (report: string -> unit) (budget: ExecutionBudget) (session: Capture.CaptureSession) =
        let rec attempt reported =
            match Capture.release session with
            | Ok() -> if reported then ReleasedAfterUnsettledReport else Released
            | Error cause ->
                let pastCleanup = (effects.Now()).Ticks >= budget.CleanupEnd.Ticks
                let owned = Capture.ownership session
                let readMayBePending = owned.PendingActivity || owned.CloseUncertain || owned.BorrowedRoots > 0
                if pastCleanup && not readMayBePending then
                    report (toolError (sprintf "provider capture session is retained after the original cleanup end (%A) with no read pending; exiting" cause))
                    RetainedWithoutPendingRead
                else
                    if pastCleanup && not reported then
                        report (
                            toolError (
                                sprintf "provider capture session is not released by the original cleanup end (%A); the host retains it and stays resident while a read may be pending; teardown belongs to the outer admitted runtime" cause
                            )
                        )
                    effects.Pause()
                    attempt (reported || pastCleanup)
        attempt false

    let run (effects: Effects) (report: string -> unit) (drive: Supply -> int) : int =
        let start = effects.Now()
        let held: (Capture.CaptureSession * ExecutionBudget) option ref = ref None
        let supply: Supply =
            fun request selection ->
                match admit effects start with
                | Error message -> Error message
                | Ok admission ->
                    let root = IO.Path.GetFullPath request.Repo
                    let captureRequest: Capture.Request =
                        { Operation = "fsgg-verify-provider"
                          RepositoryRoot = root
                          Selection = selection
                          Namespace = Capture.CooperativelyStableNamespace
                          Limits = captureLimits
                          Budget = admission.Budget
                          Cancellation = admission.Cancellation }
                    match effects.Prepare captureRequest with
                    | Error failures -> Error(sprintf "provider capture request refused: %A" failures)
                    | Ok session ->
                        // The owner is retained before any acquisition so no return path can lose it.
                        held.Value <- Some(session, admission.Budget)
                        let observation = Capture.capture session
                        if observation.Model.Phase = Capture.Ready && observation.Model.FirstFailure.IsNone then
                            Ok(session, commandContext effects root admission (effects.Now()))
                        else
                            Error(
                                sprintf "provider capture did not reach a ready state: %A (secondary %A)" observation.Model.FirstFailure observation.Model.SecondaryFailures
                            )
        let inner =
            try
                drive supply
            with ex ->
                report (toolError (ex.GetType().Name + ": " + ex.Message))
                Loop.exitCode Loop.ToolError
        match held.Value with
        | None -> inner
        | Some(session, budget) ->
            let settlement = settle effects report budget session
            let model = (Capture.inspect session).Model
            let failed = model.FirstFailure.IsSome || not (List.isEmpty model.SecondaryFailures)
            let code = exitCode inner settlement failed
            if code <> inner then
                report (toolError (sprintf "provider invocation exit %d is replaced by %d: the capture session did not settle cleanly" inner code))
            code

    let systemEffects () : Effects =
        let cancellation =
            lazy
                (let source = new CancellationTokenSource()
                 // Cancellation is a request the capture observes. The handlers keep the host alive so it can
                 // settle its session; if that cannot happen, the outer runtime terminates the whole worker.
                 Console.CancelKeyPress.Add(fun args ->
                     args.Cancel <- true
                     source.Cancel())
                 let register signal =
                     Runtime.InteropServices.PosixSignalRegistration.Create(
                         signal,
                         fun context ->
                             context.Cancel <- true
                             source.Cancel()
                     )
                     |> ignore
                 register Runtime.InteropServices.PosixSignal.SIGTERM
                 source.Token)
        { Environment =
            fun name ->
                match Environment.GetEnvironmentVariable name with
                | null -> None
                | value -> Some value
          Now = FS.GG.Governance.GateExecution.Interpreter.currentInstant
          TicksPerSecond = Diagnostics.Stopwatch.Frequency
          Cancellation = fun () -> cancellation.Force()
          Prepare = Capture.prepare
          Pause = fun () -> Thread.Sleep 10 }
