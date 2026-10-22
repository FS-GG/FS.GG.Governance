namespace FS.GG.Governance.VerifyCommand

/// Verify host composition root for explicit provider mode (Governance#468, decision of #465).
/// For one provider invocation this is the outer capture-lifetime owner: it forms the capture request from the
/// original work and cleanup ends the outer admitted runtime supplied, holds the single session for the whole
/// invocation, and is the only code that calls `ProviderContextCapture.release`. It never disposes, finalizes,
/// replaces, renews or signals around an unsettled session. Whole-worker termination and resource reclamation
/// stay with the outer admitted runtime.
module internal ProviderHost =
    /// Original ends in the process monotonic domain, plus the cancellation the invocation observes.
    type Admission =
        { Budget: FS.GG.Governance.GateExecution.Model.ExecutionBudget
          Cancellation: System.Threading.CancellationToken }

    /// Every ambient input and delay of the host, so the composition is testable without a kernel or a clock.
    type Effects =
        { /// Process environment lookup (admitted ends, the CI sensor).
          Environment: string -> string option
          /// Process monotonic clock in the same domain the capture checks its ends against.
          Now: unit -> FS.GG.Governance.GateExecution.Model.MonotonicInstant
          TicksPerSecond: int64
          /// Evaluated only when provider mode is actually admitted; legacy invocations never reach it.
          Cancellation: unit -> System.Threading.CancellationToken
          Prepare:
              ProviderContextCapture.Request ->
                  Result<ProviderContextCapture.CaptureSession, ProviderContextCapture.Failure list>
          /// One bounded wait between release attempts; never a release, disposal or signal.
          Pause: unit -> unit }

    /// Outer admitted runtime supplies seconds after host start; absent or inconsistent ends admit nothing.
    val workSecondsVariable: string
    val cleanupSecondsVariable: string

    /// Convert the admitted seconds into original ends measured from `start`; cleanup must not precede work.
    val admit:
        Effects -> start: FS.GG.Governance.GateExecution.Model.MonotonicInstant -> Result<Admission, string>

    /// Explicit planning inputs: observed environment (`CI` set means Ci), empty delta, whole seconds left in the work window.
    val commandContext:
        Effects -> repoRoot: string -> Admission -> now: FS.GG.Governance.GateExecution.Model.MonotonicInstant ->
            FS.GG.Governance.GateRun.Plan.ProviderCommandContext

    /// How the held session ended. `RetainedWithoutPendingRead` means release stayed refused after the original
    /// cleanup end with no activity, uncertain close or lease that a read could still be pending in.
    type Settlement =
        | Released
        | ReleasedAfterUnsettledReport
        | RetainedWithoutPendingRead

    /// Defined exit mapping: a released session keeps the invocation's own exit, except that a passing verdict
    /// cannot stand on a session with a recorded failure; every other settlement is a tool error (4).
    val exitCode: inner: int -> Settlement -> sessionFailed: bool -> int

    /// Supplier handed to the adapter: it prepares and captures the single session and retains it in the host.
    type Supply =
        Loop.RunRequest -> ProviderContext.Selection ->
            Result<ProviderContextCapture.CaptureSession * FS.GG.Governance.GateRun.Plan.ProviderCommandContext, string>

    /// Drive one invocation, then settle the session it created (if any). While release is refused and a read may
    /// be pending, this does not return: it keeps the session, reports once, and waits for the outer runtime.
    val run: Effects -> report: (string -> unit) -> drive: (Supply -> int) -> int

    /// Process-signal cancellation, registered only when provider mode asks for its token. Dispose unregisters.
    /// SIGINT and SIGTERM request cancellation of the capture; they do not terminate the host, which still
    /// settles its session (or stays resident). The owner (main) must keep this object alive.
    [<Sealed>]
    type SignalCancellation =
        new: unit -> SignalCancellation
        member Token: System.Threading.CancellationToken
        interface System.IDisposable

    /// Real clock, environment, capture preparation and signal-driven cancellation.
    val systemEffects: SignalCancellation -> Effects
