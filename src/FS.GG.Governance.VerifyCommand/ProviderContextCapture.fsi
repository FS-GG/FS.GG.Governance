namespace FS.GG.Governance.VerifyCommand

/// Caller-owned Linux capture of two explicitly selected documents; no policy or execution authority.
module ProviderContextCapture =
    /// Explicit assumption over capture and admitted use; comparisons cannot exclude hostile ABA.
    type NamespaceAcceptance = CooperativelyStableNamespace | RequireAdversarialAtomicSnapshot
    type Limits = { PerFileBytes: int; AggregateBytes: int }
    /// Original ends in the existing process monotonic clock domain; neither end can renew.
    type Request =
        { Operation: string
          RepositoryRoot: string
          Selection: ProviderContext.Selection
          Namespace: NamespaceAcceptance
          Limits: Limits
          Budget: FS.GG.Governance.GateExecution.Model.ExecutionBudget
          Cancellation: System.Threading.CancellationToken }
    type Failure =
        | InvalidRequest of detail: string
        | UnsupportedNamespace
        | UnsupportedPlatform
        | NativePrerequisiteUnavailable of detail: string
        | CaptureAlreadyConsumed
        | OperationInProgress
        | CaptureNotReady
        | WorkDeadlineReached
        | CleanupDeadlineReached
        | CancellationRequested
        | ByteLimitExceeded of document: string
        | AcquisitionFailed of document: string * detail: string
        | InputChanged of document: string
        | RawDigestMismatch of document: string
        | OwnedActivitiesPending
    type Phase = Prepared | Acquiring | Ready | Revalidating | Retiring | Released
    type Model =
        { Phase: Phase
          CaptureConsumed: bool
          FirstFailure: Failure option
          SecondaryFailures: Failure list }
    type Msg =
        | CaptureRequested
        | CaptureComparisonSucceeded
        | RevalidationRequested
        | RevalidationSucceeded
        | FailureObserved of Failure
        | ReleaseRequested
        | OwnedResourcesSettled
    type Effect = AcquireAndCompareSelected | RecompareSameObjects | RetireOwnedObjects
    /// Pure transitions: one-use acquisition, sticky distinct causes, no native effects inside update.
    val init: unit -> Model
    val update: Msg -> Model -> Model * Effect list
    /// Pure request/path/cap/original-domain validation; no filesystem observation or fallback.
    val validateRequest: now: FS.GG.Governance.GateExecution.Model.MonotonicInstant -> Request -> Failure list
    type ObjectIdentity =
        { DeviceMajor: uint32; DeviceMinor: uint32; Inode: uint64; MountId: uint64; ReturnedMask: uint32 }
    type RootObservation =
        { RequestedPath: string; HeldIdentity: ObjectIdentity; CurrentBindingMatches: bool option }
    /// Immutable bytes and raw digest from original held object, not a simultaneous multi-file snapshot.
    type DocumentObservation =
        { Role: string
          SelectedPath: string
          HeldIdentity: ObjectIdentity
          Bytes: System.Collections.Immutable.ImmutableArray<byte>
          RawSha256: string
          ComparisonPasses: int }
    type Settlement = Retained | ResourcesReleased
    type Observation =
        { Operation: string
          Model: Model
          Roots: RootObservation list
          Documents: DocumentObservation list
          Settlement: Settlement
          WorkEndReached: bool
          CleanupEndReached: bool
          CancellationObserved: bool }
    /// Caller holds this actual owner before acquisition and after any incomplete return.
    /// No disposal/finalizer/replacement owner may abandon descriptors or pending activities.
    [<Sealed>]
    type CaptureSession =
        member Operation: string
    /// No filesystem/syscall acquisition. Invalid request/stronger namespace requirements refuse.
    val prepare: Request -> Result<CaptureSession, Failure list>
    /// Consume capture once. Linux openat2/statx/procfs must be qualified; no BCL path fallback.
    /// Cap bytes before retention; compare the complete original selected set before Ready.
    val capture: CaptureSession -> Observation
    /// Compare the same held objects and current bindings within original ends; never parse replacements.
    val revalidate: CaptureSession -> Observation
    /// Nonblocking immutable facts; grants no custody or additional budget.
    val inspect: CaptureSession -> Observation
    /// Only settled activities/descriptors can release. Late settlement cannot erase original failure.
    val release: CaptureSession -> Result<unit, Failure>

    /// Revalidate the same owner, duplicate its original held repository root, and retain a managed lease.
    /// Capture release refuses while the borrowed lease or its direct execution borrowers remain owned.
    val borrowRepositoryRoot:
        CaptureSession -> Result<FS.GG.Governance.GateExecution.Interpreter.DirectoryLease, Failure>
