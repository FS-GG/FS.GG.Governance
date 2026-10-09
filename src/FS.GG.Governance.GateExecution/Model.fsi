// Curated public signature contract for the gate-execution domain vocabulary (F051).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and committed BEFORE any Model.fs body exists (Principle I) and exercised in
// FSI (scripts/prelude.fsx). These are the reproducible inputs and the sensed outcome for ONE gate execution,
// plus the injected execution port type. Every field REUSES the F032/F014 vocabulary VERBATIM — opened from
// `FS.GG.Governance.CommandRecord.Model` and `FS.GG.Governance.Config.Model`, never redefined (FR-004,
// FR-011). Nothing here starts a process or reads a clock; the I/O lives only in Interpreter.realPort.

namespace FS.GG.Governance.GateExecution

open FS.GG.Governance.Config.Model // TimeoutLimit
open FS.GG.Governance.CommandRecord.Model // Executable, Argument, WorkingDirectory, EnvironmentDelta,
// ExitCode, CapturedOutput, SensedDuration

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The REPRODUCIBLE inputs for one gate execution — the command-to-run (Key entity: "Gate
    /// command-to-run"). All F032/F014 vocabulary reused verbatim: the program, its ORDERED arguments
    /// (argument order is significant in the identity), the working directory, the environment DELTA (a
    /// three-class partition, not a full snapshot), the timeout to enforce, and the captured-output target
    /// (`NoCapturedOutput` in the common case). Carries NO bytes, NO clock reading, NO product vocabulary.
    type GateCommand =
        {
            Executable: Executable
            Arguments: Argument list
            WorkingDirectory: WorkingDirectory
            Environment: EnvironmentDelta
            Timeout: TimeoutLimit
            CapturedOutput: CapturedOutput
        }

    /// The SENSED result of one run (Key entity: "Captured execution outcome") — the raw stdout/stderr BYTES
    /// captured verbatim (no decoding, locale, normalization, or truncation), the integer exit code (or a
    /// sentinel for start failure / timeout — see Interpreter), and the measured wall-clock duration. This is
    /// the value the injected port YIELDS and that F050 `ExecutionRecord.recordOf` CONSUMES; the duration is
    /// the sole non-deterministic fact and is held apart (excluded from the canonical identity, F050 FR-006).
    type ExecutionOutcome =
        {
            Stdout: byte[]
            Stderr: byte[]
            ExitCode: ExitCode
            Duration: SensedDuration
        }

    /// The injected execution port (Key entity: "Execution port (injected)") — a function value that runs ONE
    /// gate command and yields its captured outcome. TOTAL by contract: a start failure or timeout is reified
    /// into an ordinary outcome carrying a sentinel exit code, NEVER an exception (FR-007, FR-008). This is
    /// the sole seam through which the feature touches a process (FR-010); `Interpreter.realPort` is the real
    /// implementation, and tests supply a deterministic fake of this exact shape.
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

    /// Physical directory identity; observations grant no handle or execution authority.
    type DirectoryIdentity =
        { DeviceMajor: uint32; DeviceMinor: uint32; Inode: uint64; MountId: uint64; ReturnedMask: uint32 }

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

    /// Pure initial state; acquiring it starts no process.
    val initDirect: unit -> DirectModel

    /// One-use launch and sticky first cause; interpretation stays at the edge.
    val updateDirect: DirectMsg -> DirectModel -> DirectModel * DirectEffect list

    /// Pure original-end validation, including domain and ordering; no clock read or renewal.
    val validateBudget: now: MonotonicInstant -> ExecutionBudget -> BoundedFailure option

    /// Pure rejection of stronger descendant/path guarantees; no fallback or launch.
    val validateCurrentHostPolicy: CurrentHostPolicy -> BoundedFailure option
