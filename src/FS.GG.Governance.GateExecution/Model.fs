// The gate-execution domain vocabulary (F051). Visibility lives in Model.fsi (Principle II): this file
// carries NO `private`/`internal`/`public` modifiers on top-level bindings. These are the reproducible
// inputs and the sensed outcome for ONE gate execution, plus the injected execution-port type — all data,
// no behavior, so no stub. Every field REUSES the F032/F014 vocabulary VERBATIM, never redefined (FR-004,
// FR-011); nothing here starts a process or reads a clock (the I/O lives only in Interpreter.realPort).

namespace FS.GG.Governance.GateExecution

open FS.GG.Governance.Config.Model // TimeoutLimit
open FS.GG.Governance.CommandRecord.Model // Executable, Argument, WorkingDirectory, EnvironmentDelta,
// ExitCode, CapturedOutput, SensedDuration

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type GateCommand =
        {
            Executable: Executable
            Arguments: Argument list
            WorkingDirectory: WorkingDirectory
            Environment: EnvironmentDelta
            Timeout: TimeoutLimit
            CapturedOutput: CapturedOutput
        }

    type ExecutionOutcome =
        {
            Stdout: byte[]
            Stderr: byte[]
            ExitCode: ExitCode
            Duration: SensedDuration
        }

    type ExecutionPort = GateCommand -> ExecutionOutcome

    /// The selected weaker scope. Descendants are neither contained nor observed.
    type DescendantAcceptance =
        | AcceptUncontainedUnobservedDescendants
        | RequireContainedWorkload

    /// A current-process monotonic clock reading; never persisted or renewed by recovery.
    type MonotonicInstant = { Domain: System.Guid; Ticks: int64 }

    /// Original work and total cleanup ends in one owning process's clock domain.
    type ExecutionBudget = { WorkEnd: MonotonicInstant; CleanupEnd: MonotonicInstant }

    /// Independent byte limits, charged before retention; diagnostics share the aggregate limit.
    type CaptureLimits = { StdoutBytes: int64; StderrBytes: int64; AggregateBytes: int64 }

    /// Explicit acceptance of observations without stable inode or atomic cwd binding.
    type PathAcceptance =
        | AcceptObservedPathsWithoutAtomicBinding
        | RequireStableAtomicPathBinding

    /// Explicit current-host inputs. The environment is captured once by the caller.
    /// Root bounds only initial cwd selection; it supplies no filesystem/network sandbox.
    type CurrentHostPolicy =
        { Root: string; CapturedEnvironment: Map<string, string>; Descendants: DescendantAcceptance; Paths: PathAcceptance }

    /// Original operation/launch identity; not an execution grant or a numeric PID.
    type LaunchIdentity = { Operation: string; Launch: string }

    /// A one-use direct-child request; no provider or persisted context data.
    type BoundedRequest =
        { Command: GateCommand
          Identity: LaunchIdentity
          Policy: CurrentHostPolicy
          Budget: ExecutionBudget
          Limits: CaptureLimits
          Cancellation: System.Threading.CancellationToken }

    /// Separate causal failures; a real child exit 124 is never interpreted as timeout.
    type BoundedFailure =
        | InvalidRequest of string
        | UnsupportedGuarantee
        | WrongClockDomain
        | WorkDeadlineReached
        | CancellationRequested
        | OutputLimitReached
        | LaunchFailed of string
        | ReadFailed of string
        | StopIdentityUnknown
        | CleanupDeadlineReached
        | CleanupFailed of string
        | AlreadyRun

    type DirectLaunchState = NotStarted | Starting | Started | LaunchOutcomeUnknown

    /// EOF differs from a locally closed pipe and from an outstanding owned read.
    type StreamState = Pending | EndOfFile | ClosedBeforeEndOfFile | StreamReadFailed

    /// Immutable captured prefixes; no returned snapshot aliases a writer's buffer.
    type StreamObservation =
        { State: StreamState
          ObservedBytes: int64
          Prefix: System.Collections.Immutable.ImmutableArray<byte> }

    /// Released describes direct resources only, never descendants or reaping.
    type DirectSettlement = Released | Retained

    type DescendantObservation = UncontainedUnobserved

    /// Bounded facts; retained custody stays with the same caller-held session.
    type BoundedObservation =
        { Identity: LaunchIdentity
          Launch: DirectLaunchState
          DirectExit: ExitCode option
          Stdout: StreamObservation
          Stderr: StreamObservation
          FirstFailure: BoundedFailure option
          SecondaryFailures: BoundedFailure list
          CancellationRequested: bool
          WorkDeadlineReached: bool
          OutputLimitReached: bool
          Settlement: DirectSettlement
          Descendants: DescendantObservation }

    /// Pure workflow state: an I/O edge interprets only the emitted effects.
    type DirectPhase = Prepared | Launching | Running | Retiring | Settled

    type DirectModel =
        { Phase: DirectPhase
          LaunchConsumed: bool
          FirstFailure: BoundedFailure option
          SecondaryFailures: BoundedFailure list }

    type DirectMsg =
        | RunRequested
        | LaunchObserved
        | FailureObserved of BoundedFailure
        | DirectResourcesSettled

    type DirectEffect = LaunchDirectChild | RetireDirectResources


    let initDirect () : DirectModel =
        { Phase = Prepared; LaunchConsumed = false; FirstFailure = None; SecondaryFailures = [] }

    let fail (cause: BoundedFailure) (model: DirectModel) =
        match model.FirstFailure with
        | None -> { model with FirstFailure = Some cause }
        | Some first when first = cause || List.contains cause model.SecondaryFailures -> model
        | Some _ -> { model with SecondaryFailures = (model.SecondaryFailures @ [cause]) |> List.truncate 8 }

    let updateDirect (msg: DirectMsg) (model: DirectModel) : DirectModel * DirectEffect list =
        match msg, model.Phase with
        | RunRequested, Prepared when not model.LaunchConsumed && model.FirstFailure.IsNone ->
            { model with Phase = Launching; LaunchConsumed = true }, [LaunchDirectChild]
        | RunRequested, _ -> fail AlreadyRun model, []
        | LaunchObserved, Launching -> { model with Phase = Running }, []
        | FailureObserved cause, Prepared ->
            { (fail cause model) with Phase = Retiring; LaunchConsumed = true }, []
        | FailureObserved cause, (Launching | Running) ->
            { (fail cause model) with Phase = Retiring }, [RetireDirectResources]
        | FailureObserved cause, _ -> fail cause model, []
        | DirectResourcesSettled, (Prepared | Running | Retiring) -> { model with Phase = Settled; LaunchConsumed = true }, []
        | _ -> model, []

    let validateBudget (now: MonotonicInstant) (budget: ExecutionBudget) =
        if now.Domain = System.Guid.Empty || now.Domain <> budget.WorkEnd.Domain || now.Domain <> budget.CleanupEnd.Domain then
            Some WrongClockDomain
        elif budget.WorkEnd.Ticks < 0L || budget.CleanupEnd.Ticks < budget.WorkEnd.Ticks then
            Some(InvalidRequest "invalid original deadline order")
        elif now.Ticks >= budget.CleanupEnd.Ticks then Some CleanupDeadlineReached
        elif now.Ticks >= budget.WorkEnd.Ticks then Some WorkDeadlineReached
        else None

    let validateCurrentHostPolicy (policy: CurrentHostPolicy) =
        if System.Object.ReferenceEquals(policy, null) then Some(InvalidRequest "policy-null")
        elif policy.Descendants <> AcceptUncontainedUnobservedDescendants || policy.Paths <> AcceptObservedPathsWithoutAtomicBinding then
            Some UnsupportedGuarantee
        else None
