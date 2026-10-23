namespace FS.GG.Governance.VerifyCommand

/// GOV-423-C2 provider execution edge. Runs each admitted provider command through the existing GateExecution
/// held-cwd direct-child host, under the same capture owner and original ends as the Verify invocation, and
/// observes the declared evidence outputs itself. Acceptance stays pure in `Loop.acceptProviderGate`.
///
/// Profile: cooperative, Linux-x64 only. The command starts in the held original repository root (or a governed
/// subdirectory of it) with the environment captured once at admission plus the admitted (empty) delta.
/// Descendants are neither contained nor observed. Nothing here is a sandbox or hostile-writer isolation.
module internal ProviderExecution =
    /// Upper bound of one evidence document Governance reads.
    val evidenceByteLimit: int

    /// Upper bound of one cited source file Governance digests.
    val sourceByteLimit: int

    /// Per-stream and aggregate stdout/stderr retention for one provider command.
    val captureLimits: FS.GG.Governance.GateExecution.Model.CaptureLimits

    /// Governance's own check that a declared output does not exist before launch. `None` means absent (the run
    /// may start); otherwise the observed reason not to launch (present, link, or unobservable).
    val beforeLaunch:
        root: string -> evidence: FS.GG.Governance.Config.CapabilityBindings.EvidenceBinding -> Loop.ProviderEvidenceState option

    /// Read a declared output after the run, within bounds and containment, and digest every source it cites.
    val afterRun: root: string -> evidence: FS.GG.Governance.Config.CapabilityBindings.EvidenceBinding -> Loop.ProviderEvidenceState

    /// Run the admitted gates in order. A gate whose declared output already exists is not launched; every other
    /// gate is handed to `launch`, then each declared output is observed. The launcher's result is retained as is.
    val executeWith:
        root: string ->
        launch: (Loop.ProviderGateRun -> Loop.ProviderLaunch) ->
        runs: Loop.ProviderGateRun list ->
            Loop.ProviderGateObservation list

    /// Inputs of the real launcher, all owned by the Verify host for the whole invocation.
    type Host =
        { Session: ProviderContextCapture.CaptureSession
          /// The environment captured once at admission; the admitted delta is applied by GateExecution.
          Environment: Map<string, string>
          Cancellation: System.Threading.CancellationToken
          Now: unit -> FS.GG.Governance.GateExecution.Model.MonotonicInstant
          TicksPerSecond: int64
          /// One bounded wait between release attempts; never a release, disposal or signal.
          Pause: unit -> unit
          Report: string -> unit
          /// Told when a borrowed root lease is left retained only because the original cleanup end passed: it
          /// has no borrower and no uncertain close, so no read can be pending in it. The Verify host counts it so
          /// its settlement can report "retained with nothing pending" and exit instead of staying resident.
          IdleLeaseRetained: unit -> unit }

    /// The real launcher. Revalidates the original captured documents and borrows the held repository root
    /// (`ProviderContextCapture.borrowRepositoryRoot`), prepares one GateExecution held-cwd session whose work end
    /// is the earlier of the original work end and the command timeout and whose cleanup end is the original
    /// cleanup end, runs it once, then releases the session and the lease. A release refused past the original
    /// cleanup end is reported once and retried while it can still succeed; a lease refused only by the passed
    /// cleanup end (idle, never releasable) is reported and handed to `IdleLeaseRetained`. The host never abandons,
    /// signals around or replaces an owner.
    val launchHeld: host: Host -> run: Loop.ProviderGateRun -> Loop.ProviderLaunch
