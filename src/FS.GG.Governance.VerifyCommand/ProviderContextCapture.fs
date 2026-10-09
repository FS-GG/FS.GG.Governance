namespace FS.GG.Governance.VerifyCommand
// Caller-owned Linux capture. Public visibility is defined exclusively by the matching signature.
module ProviderContextCapture =
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
    let init () : Model = { Phase=Prepared; CaptureConsumed=false; FirstFailure=None; SecondaryFailures=[] }
    let failure (cause:Failure) (model:Model) =
        let secondary = if model.FirstFailure=Some cause || List.contains cause model.SecondaryFailures then model.SecondaryFailures else (model.SecondaryFailures @ [cause]) |> List.truncate 8
        let phase = if model.Phase=Released then Released else Retiring
        let effects = if model.Phase=Released || model.Phase=Retiring then [] else [RetireOwnedObjects]
        match model.FirstFailure with
        | None -> {model with Phase=phase; FirstFailure=Some cause}, effects
        | Some _ -> {model with Phase=phase; SecondaryFailures=secondary}, []
    let update (msg:Msg) (model:Model) : Model * Effect list =
        match msg with
        | CaptureRequested when model.CaptureConsumed -> failure CaptureAlreadyConsumed model
        | CaptureRequested when model.Phase=Prepared && Option.isNone model.FirstFailure -> {model with Phase=Acquiring;CaptureConsumed=true},[AcquireAndCompareSelected]
        | CaptureRequested -> failure OperationInProgress model
        | CaptureComparisonSucceeded when model.Phase=Acquiring && Option.isNone model.FirstFailure -> {model with Phase=Ready},[]
        | RevalidationRequested when model.Phase=Ready && Option.isNone model.FirstFailure -> {model with Phase=Revalidating},[RecompareSameObjects]
        | RevalidationRequested -> failure CaptureNotReady model
        | RevalidationSucceeded when model.Phase=Revalidating && Option.isNone model.FirstFailure -> {model with Phase=Ready},[]
        | FailureObserved cause -> failure cause model
        | ReleaseRequested when model.Phase=Released || model.Phase=Retiring -> model,[]
        | ReleaseRequested -> {model with Phase=Retiring},[RetireOwnedObjects]
        | OwnedResourcesSettled when model.Phase=Retiring -> {model with Phase=Released},[]
        | _ -> model,[]
    let validateRequest (now:FS.GG.Governance.GateExecution.Model.MonotonicInstant) (request:Request) : Failure list =
        let absolute (s:string) = not (System.String.IsNullOrWhiteSpace s) && s.StartsWith("/",System.StringComparison.Ordinal)
        let invalidSegments (s:string) = s.Split('/') |> Array.exists(fun segment -> segment="." || segment="..")
        let ambiguous (s:string) = s.Contains("//",System.StringComparison.Ordinal) || s.Contains('\\') || s.Contains('\u0000')
        let digest (s:string) = not (System.String.IsNullOrWhiteSpace s) && s.Length=64 && Seq.forall(fun c -> (c>='0' && c<='9') || (c>='a' && c<='f') || (c>='A' && c<='F')) s
        let selected=request.Selection
        let validRoot = absolute request.RepositoryRoot && not (invalidSegments request.RepositoryRoot) && not (ambiguous request.RepositoryRoot)
        [ if System.String.IsNullOrWhiteSpace request.Operation then yield InvalidRequest "operation is empty"
          if request.Namespace<>CooperativelyStableNamespace then yield UnsupportedNamespace
          if not validRoot then yield InvalidRequest "repository root must be absolute without dot segments"
          if System.String.IsNullOrWhiteSpace selected.ProvenancePath || invalidSegments selected.ProvenancePath || ambiguous selected.ProvenancePath || selected.ProvenancePath.EndsWith("/",System.StringComparison.Ordinal) then yield InvalidRequest "provenance path is empty or has dot segments"
          elif validRoot && absolute selected.ProvenancePath && not (selected.ProvenancePath.StartsWith(request.RepositoryRoot.TrimEnd('/')+"/",System.StringComparison.Ordinal)) then yield InvalidRequest "provenance escapes selected repository root"
          if not (absolute selected.PolicyPath) || invalidSegments selected.PolicyPath || ambiguous selected.PolicyPath || selected.PolicyPath.EndsWith("/",System.StringComparison.Ordinal) then yield InvalidRequest "independent policy path must be absolute without dot segments"
          if validRoot && not (System.String.IsNullOrWhiteSpace selected.ProvenancePath) && not (System.String.IsNullOrWhiteSpace selected.PolicyPath) then
              let provenance = if absolute selected.ProvenancePath then selected.ProvenancePath else request.RepositoryRoot.TrimEnd('/')+"/"+selected.ProvenancePath
              if System.String.Equals(provenance,selected.PolicyPath,System.StringComparison.OrdinalIgnoreCase) then yield InvalidRequest "duplicate or case-alias selected documents"
          if System.String.IsNullOrWhiteSpace selected.Platform then yield InvalidRequest "selected platform is empty"
          if not (digest selected.ProvenanceSha256) || not (digest selected.PolicySha256) then yield InvalidRequest "raw digests must be SHA256 hexadecimal"
          if request.Limits.PerFileBytes<=0 || request.Limits.AggregateBytes<=0 then yield InvalidRequest "byte limits must be positive"
          if request.Budget.WorkEnd.Domain<>now.Domain || request.Budget.CleanupEnd.Domain<>now.Domain then yield InvalidRequest "original deadline domain differs"
          if request.Budget.CleanupEnd.Ticks<request.Budget.WorkEnd.Ticks then yield InvalidRequest "cleanup precedes work end"
          if request.Budget.WorkEnd.Ticks<=now.Ticks then yield WorkDeadlineReached ]

    // Exact x86-64 Linux ABI qualified by the separate finite primitive. No path fallback.
    module Native =
        [<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)>]
        type OpenHow =
            struct
                val mutable Flags: uint64
                val mutable Mode: uint64
                val mutable Resolve: uint64
                new(flags, resolve) = { Flags=flags; Mode=0UL; Resolve=resolve }
            end
        [<System.Runtime.InteropServices.DllImport("libc", EntryPoint="syscall", SetLastError=true)>]
        extern int64 openat2(int64 number, int directory, string path, OpenHow& how, uint64 size)
        [<System.Runtime.InteropServices.DllImport("libc", SetLastError=true)>]
        extern int statx(int fd, string path, int flags, uint32 mask, [<System.Runtime.InteropServices.Out>] byte[] data)
        [<System.Runtime.InteropServices.DllImport("libc", EntryPoint="open", SetLastError=true)>]
        extern int openReadOnly(string path, int flags)
        [<System.Runtime.InteropServices.DllImport("libc", SetLastError=true)>]
        extern int64 read(int fd, [<System.Runtime.InteropServices.Out>] byte[] data, uint64 count)
        [<System.Runtime.InteropServices.DllImport("libc", SetLastError=true)>]
        extern int64 lseek(int fd, int64 offset, int whence)
        [<System.Runtime.InteropServices.DllImport("libc", SetLastError=true)>]
        extern int close(int fd)

    type Metadata =
        { Identity: ObjectIdentity; Mode: uint16; Size: uint64
          ModifiedSeconds: int64; ModifiedNanoseconds: uint32
          ChangedSeconds: int64; ChangedNanoseconds: uint32 }
    type HeldRoot = { Path: string; Fd: int; Metadata: Metadata }
    type HeldDocument =
        { Role: string; SelectedPath: string; RelativePath: string; Root: HeldRoot
          PathFd: int; DataFd: int; Metadata: Metadata; Observation: DocumentObservation }
    type OwnerState =
        { Request: Request
          Sync: obj
          mutable Model: Model
          Fds: System.Collections.Generic.HashSet<int>
          mutable Roots: HeldRoot list
          mutable RootObservations: RootObservation list
          mutable Documents: HeldDocument list
          mutable Activity: System.Threading.Tasks.Task<unit> option
          mutable CloseUncertain: bool
          RootLeases: System.Collections.Generic.List<FS.GG.Governance.GateExecution.Interpreter.DirectoryLease> }
    exception CaptureFault of Failure
    [<Sealed>]
    type CaptureSession(state: OwnerState) =
        member _.Operation = state.Request.Operation
        member _.State = state

    let current () = FS.GG.Governance.GateExecution.Interpreter.currentInstant()
    let transition (state:OwnerState) msg = lock state.Sync (fun () -> state.Model <- update msg state.Model |> fst)
    let report (state:OwnerState) cause = transition state (FailureObserved cause)
    let errno () = System.Runtime.InteropServices.Marshal.GetLastPInvokeError() |> string
    let fault role detail = raise (CaptureFault(AcquisitionFailed(role,detail)))
    let checkWork (state:OwnerState) =
        let now=current()
        if state.Request.Cancellation.IsCancellationRequested then raise (CaptureFault CancellationRequested)
        if now.Ticks>=state.Request.Budget.WorkEnd.Ticks then raise (CaptureFault WorkDeadlineReached)
        if lock state.Sync (fun () -> Option.isSome state.Model.FirstFailure) then raise (CaptureFault OperationInProgress)
    let own (state:OwnerState) role fd =
        if fd<0 then fault role ("native acquisition errno="+errno())
        lock state.Sync (fun () -> state.Fds.Add fd |> ignore)
        fd
    let pathOpen (state:OwnerState) role directory path flags resolve =
        checkWork state
        let mutable how=Native.OpenHow(flags,resolve)
        let fd=Native.openat2(437L,directory,path,&how,24UL)
        if fd<0L then
            if System.Runtime.InteropServices.Marshal.GetLastPInvokeError()=38 then raise (CaptureFault(NativePrerequisiteUnavailable "openat2 unavailable"))
            fault role ("openat2 errno="+errno())
        own state role (int fd)
    let metadata (state:OwnerState) role fd =
        checkWork state
        let bytes=Array.zeroCreate<byte> 256
        if Native.statx(fd,"",4352,0x17ffu,bytes)<>0 then fault role ("statx errno="+errno())
        let u32 offset=System.BitConverter.ToUInt32(bytes,offset)
        let u64 offset=System.BitConverter.ToUInt64(bytes,offset)
        let mask=u32 0
        // TYPE, MODE, INO, SIZE, MTIME, CTIME and MNT_ID must actually be returned.
        if mask &&& 0x13c3u <> 0x13c3u then fault role "required statx fields unavailable"
        { Identity={DeviceMajor=u32 136;DeviceMinor=u32 140;Inode=u64 32;MountId=u64 144;ReturnedMask=mask}
          Mode=System.BitConverter.ToUInt16(bytes,28);Size=u64 40
          ModifiedSeconds=System.BitConverter.ToInt64(bytes,112);ModifiedNanoseconds=u32 120
          ChangedSeconds=System.BitConverter.ToInt64(bytes,96);ChangedNanoseconds=u32 104 }
    let sameObject (left:Metadata) (right:Metadata) =
        left.Identity.DeviceMajor=right.Identity.DeviceMajor && left.Identity.DeviceMinor=right.Identity.DeviceMinor
        && left.Identity.Inode=right.Identity.Inode && left.Identity.MountId=right.Identity.MountId
    let sameContentMetadata (left:Metadata) (right:Metadata) =
        sameObject left right && left.Mode=right.Mode && left.Size=right.Size
        && left.ModifiedSeconds=right.ModifiedSeconds && left.ModifiedNanoseconds=right.ModifiedNanoseconds
        && left.ChangedSeconds=right.ChangedSeconds && left.ChangedNanoseconds=right.ChangedNanoseconds
    let openRoot (state:OwnerState) path =
        let fd=pathOpen state "root" -100 path 0x290000UL 6UL
        let facts=metadata state "root" fd
        if facts.Mode &&& 0xf000us <> 0x4000us then fault "root" "selected root is not a directory"
        let root={Path=path;Fd=fd;Metadata=facts}
        lock state.Sync (fun () ->
            state.Roots <- state.Roots @ [root]
            state.RootObservations <- state.RootObservations @ [{RequestedPath=path;HeldIdentity=facts.Identity;CurrentBindingMatches=None}])
        root
    let closeTracked (state:OwnerState) fd =
        // Linux close failure is not retried: numeric fd reuse cannot establish continued ownership.
        if (current()).Ticks>=state.Request.Budget.CleanupEnd.Ticks then raise (CaptureFault CleanupDeadlineReached)
        if Native.close fd=0 then lock state.Sync (fun () -> state.Fds.Remove fd |> ignore)
        else
            lock state.Sync (fun () -> state.CloseUncertain <- true)
            raise (CaptureFault OwnedActivitiesPending)
    let reopenRegular (state:OwnerState) role fd original =
        let observed=metadata state role fd
        if observed.Mode &&& 0xf000us <> 0x8000us || not (sameObject original observed) then fault role "held object is not the selected regular file"
        checkWork state
        let data=own state role (Native.openReadOnly("/proc/self/fd/"+string fd,0x80000))
        let reopened=metadata state role data
        if not (sameObject original reopened) then fault role "procfd reopen changed object"
        data
    let reset (state:OwnerState) role fd =
        checkWork state
        if Native.lseek(fd,0L,0)<>0L then fault role ("held regular-file reset errno="+errno())
    let rawDigest (bytes: byte[]) count =
        System.Security.Cryptography.SHA256.HashData(System.ReadOnlySpan<byte>(bytes,0,count))
        |> System.Convert.ToHexString |> fun digest -> digest.ToLowerInvariant()
    let readOriginal (state:OwnerState) role fd remaining =
        // Fixed capacity is chosen before any bytes are retained. A separate scratch read detects excess.
        let capacity=min state.Request.Limits.PerFileBytes remaining
        let bytes=Array.zeroCreate<byte> capacity
        let scratch=Array.zeroCreate<byte> 512
        let mutable count=0
        let mutable reading=true
        reset state role fd
        while reading do
            checkWork state
            let amount=Native.read(fd,scratch,uint64 scratch.Length)
            if amount<0L then fault role ("read errno="+errno())
            elif amount=0L then reading <- false
            elif amount>int64 (capacity-count) then raise (CaptureFault(ByteLimitExceeded role))
            else
                System.Array.Copy(scratch,0,bytes,count,int amount)
                count <- count+int amount
        let immutable=System.Collections.Immutable.ImmutableArray.CreateRange<byte>(System.ArraySegment<byte>(bytes,0,count))
        immutable, rawDigest bytes count
    let compareBytes (state:OwnerState) (document:HeldDocument) =
        let scratch=Array.zeroCreate<byte> 512
        let mutable count=0
        let mutable reading=true
        reset state document.Role document.DataFd
        while reading do
            checkWork state
            let amount=Native.read(document.DataFd,scratch,uint64 scratch.Length)
            if amount<0L then fault document.Role ("comparison read errno="+errno())
            elif amount=0L then reading <- false
            elif amount>int64 (document.Observation.Bytes.Length-count) then raise (CaptureFault(InputChanged document.Role))
            else
                for index=0 to int amount-1 do
                    if scratch[index]<>document.Observation.Bytes[count+index] then raise (CaptureFault(InputChanged document.Role))
                count <- count+int amount
        if count<>document.Observation.Bytes.Length then raise (CaptureFault(InputChanged document.Role))
    let acquireDocument (state:OwnerState) role (root:HeldRoot) relative selected expected remaining =
        let pathFd=pathOpen state role root.Fd relative 0x280000UL 15UL
        let facts=metadata state role pathFd
        if facts.Mode &&& 0xf000us <> 0x8000us then fault role "selected document is not a regular file"
        let dataFd=reopenRegular state role pathFd facts
        let bytes,digest=readOriginal state role dataFd remaining
        if not (System.String.Equals(digest,expected,System.StringComparison.OrdinalIgnoreCase)) then raise (CaptureFault(RawDigestMismatch role))
        let after=metadata state role pathFd
        if not (sameContentMetadata facts after) then raise (CaptureFault(InputChanged role))
        let document=
            {Role=role;SelectedPath=selected;RelativePath=relative;Root=root;PathFd=pathFd;DataFd=dataFd;Metadata=facts
             Observation={Role=role;SelectedPath=selected;HeldIdentity=facts.Identity;Bytes=bytes;RawSha256=digest;ComparisonPasses=0}}
        lock state.Sync (fun () -> state.Documents <- state.Documents @ [document])
        document
    let compareRoot (state:OwnerState) (root:HeldRoot) =
        let currentFd=pathOpen state "root" -100 root.Path 0x290000UL 6UL
        let facts=metadata state "root" currentFd
        let held=metadata state "root" root.Fd
        let matches=sameObject root.Metadata facts && sameObject root.Metadata held
        lock state.Sync (fun () ->
            state.RootObservations <- state.RootObservations |> List.map(fun observation ->
                if observation.RequestedPath=root.Path then {observation with CurrentBindingMatches=Some matches} else observation))
        closeTracked state currentFd
        if not matches then raise (CaptureFault(InputChanged "root"))
    let compareDocument (state:OwnerState) (document:HeldDocument) =
        let before=metadata state document.Role document.PathFd
        let currentFd=pathOpen state document.Role document.Root.Fd document.RelativePath 0x280000UL 15UL
        let binding=metadata state document.Role currentFd
        closeTracked state currentFd
        if not (sameContentMetadata document.Metadata before && sameContentMetadata document.Metadata binding) then raise (CaptureFault(InputChanged document.Role))
        compareBytes state document
        let after=metadata state document.Role document.PathFd
        if not (sameContentMetadata document.Metadata after) then raise (CaptureFault(InputChanged document.Role))
        lock state.Sync (fun () -> state.Documents <- state.Documents |> List.map(fun held ->
            if held.Role=document.Role then {held with Observation={held.Observation with ComparisonPasses=held.Observation.ComparisonPasses+1}} else held))
    let compareSet (state:OwnerState) =
        let roots,documents=lock state.Sync (fun () -> state.Roots,state.Documents)
        for root in roots do compareRoot state root
        for document in documents do compareDocument state document
        // Refresh root bindings again after the sequential document pass; this is not atomic coherence.
        for root in roots do compareRoot state root
        checkWork state
    let retire (state:OwnerState) =
        let descriptors,uncertain,borrowed=lock state.Sync (fun () -> state.Fds |> Seq.toList,state.CloseUncertain,state.RootLeases.Count>0)
        if not uncertain && not borrowed then
            for fd in descriptors do closeTracked state fd
            if (current()).Ticks>=state.Request.Budget.CleanupEnd.Ticks then report state CleanupDeadlineReached
            transition state OwnedResourcesSettled
    let runOwned (state:OwnerState) action =
        try action()
        with
        | CaptureFault cause -> report state cause
        | :? System.EntryPointNotFoundException -> report state (NativePrerequisiteUnavailable "required libc entry point unavailable")
        | :? System.DllNotFoundException -> report state (NativePrerequisiteUnavailable "libc unavailable")
        | ex -> report state (AcquisitionFailed("capture",ex.GetType().Name+": "+ex.Message))
        if lock state.Sync (fun () -> state.Model.Phase=Retiring) then
            try retire state
            with
            | CaptureFault cause -> report state cause
            | ex -> report state (AcquisitionFailed("retirement",ex.GetType().Name+": "+ex.Message))
    let startActivity (state:OwnerState) action =
        // Called while holding Sync; actual task ownership is retained before it can acquire objects.
        let activity=new System.Threading.Tasks.Task<unit>(System.Func<unit>(fun () -> runOwned state action))
        state.Activity <- Some activity
        // Ownership precedes scheduling. A scheduling exception leaves this actual task retained.
        try activity.Start(System.Threading.Tasks.TaskScheduler.Default)
        with ex -> report state (AcquisitionFailed("activity",ex.GetType().Name+": "+ex.Message))
    let awaitOriginal (state:OwnerState) =
        let mutable waiting=true
        while waiting do
            let completed=lock state.Sync (fun () -> state.Activity |> Option.forall(fun task -> task.IsCompleted))
            let now=current()
            if now.Ticks>=state.Request.Budget.CleanupEnd.Ticks then
                report state CleanupDeadlineReached
                waiting <- false
            elif completed then
                if now.Ticks>=state.Request.Budget.WorkEnd.Ticks then report state WorkDeadlineReached
                if state.Request.Cancellation.IsCancellationRequested then report state CancellationRequested
                waiting <- false
            else
                if now.Ticks>=state.Request.Budget.WorkEnd.Ticks then report state WorkDeadlineReached
                if state.Request.Cancellation.IsCancellationRequested then report state CancellationRequested
                System.Threading.Thread.Sleep 1
    let snapshot (state:OwnerState) =
        let now=current()
        lock state.Sync (fun () ->
            {Operation=state.Request.Operation;Model=state.Model;Roots=state.RootObservations
             Documents=state.Documents |> List.map(fun held -> held.Observation)
             Settlement=if state.Model.Phase=Released && state.Fds.Count=0 && state.RootLeases.Count=0 && not state.CloseUncertain && (state.Activity |> Option.forall(fun task -> task.IsCompleted)) then ResourcesReleased else Retained
             WorkEndReached=now.Ticks>=state.Request.Budget.WorkEnd.Ticks
             CleanupEndReached=now.Ticks>=state.Request.Budget.CleanupEnd.Ticks
             CancellationObserved=state.Request.Cancellation.IsCancellationRequested})
    let prepare (request:Request) =
        let errors=validateRequest (current()) request
        let platform=System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                     && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture=System.Runtime.InteropServices.Architecture.X64
                     && request.Selection.Platform="linux-x64"
        let errors=if platform then errors else errors @ [UnsupportedPlatform]
        if not (List.isEmpty errors) then Error errors
        else
            Ok(CaptureSession({Request=request;Sync=obj();Model=init();Fds=System.Collections.Generic.HashSet<int>()
                               Roots=[];RootObservations=[];Documents=[];Activity=None;CloseUncertain=false;RootLeases=System.Collections.Generic.List<_>()}))
    let inspect (session:CaptureSession) = snapshot session.State
    let capture (session:CaptureSession) =
        let state=session.State
        lock state.Sync (fun () ->
            let model,effects=update CaptureRequested state.Model
            state.Model <- model
            if List.contains AcquireAndCompareSelected effects then
                startActivity state (fun () ->
                    checkWork state
                    let root=openRoot state state.Request.RepositoryRoot
                    let selected=state.Request.Selection
                    let relative=if selected.ProvenancePath.StartsWith("/",System.StringComparison.Ordinal) then selected.ProvenancePath.Substring(state.Request.RepositoryRoot.TrimEnd('/').Length+1) else selected.ProvenancePath
                    let provenance=acquireDocument state "provenance" root relative selected.ProvenancePath selected.ProvenanceSha256 state.Request.Limits.AggregateBytes
                    let parent=
                        match System.IO.Path.GetDirectoryName selected.PolicyPath with
                        | null -> fault "policy" "absolute policy parent unavailable"
                        | path -> path
                    let policyRoot=openRoot state parent
                    let filename=
                        match System.IO.Path.GetFileName selected.PolicyPath with
                        | null -> fault "policy" "absolute policy filename unavailable"
                        | value -> value
                    acquireDocument state "policy" policyRoot filename selected.PolicyPath selected.PolicySha256 (state.Request.Limits.AggregateBytes-provenance.Observation.Bytes.Length) |> ignore
                    compareSet state
                    transition state CaptureComparisonSucceeded))
        awaitOriginal state
        snapshot state
    let revalidate (session:CaptureSession) =
        let state=session.State
        lock state.Sync (fun () ->
            let active=state.Activity |> Option.exists(fun task -> not task.IsCompleted)
            if active then report state OperationInProgress
            else
                let model,effects=update RevalidationRequested state.Model
                state.Model <- model
                if List.contains RecompareSameObjects effects then
                    state.RootObservations <- state.RootObservations |> List.map(fun observation -> {observation with CurrentBindingMatches=None})
                    startActivity state (fun () -> compareSet state;transition state RevalidationSucceeded))
        awaitOriginal state
        snapshot state
    let release (session:CaptureSession) =
        let state=session.State
        lock state.Sync (fun () ->
            let active=state.Activity |> Option.exists(fun task -> not task.IsCompleted)
            if active || state.CloseUncertain || state.RootLeases.Count>0 then
                if (current()).Ticks>=state.Request.Budget.CleanupEnd.Ticks then report state CleanupDeadlineReached
                Error OwnedActivitiesPending
            elif state.Model.Phase=Released && state.Fds.Count=0 then Ok()
            elif state.Fds.Count=0 then
                if (current()).Ticks>=state.Request.Budget.CleanupEnd.Ticks then report state CleanupDeadlineReached
                transition state ReleaseRequested
                transition state OwnedResourcesSettled
                Ok()
            elif (current()).Ticks>=state.Request.Budget.CleanupEnd.Ticks then
                report state CleanupDeadlineReached
                Error CleanupDeadlineReached
            else
                transition state ReleaseRequested
                startActivity state ignore
                Error OwnedActivitiesPending)

    let borrowRepositoryRoot (session:CaptureSession) =
        let original = revalidate session
        let state = session.State
        lock state.Sync (fun () ->
            if original.Model.Phase<>Ready || original.Model.FirstFailure.IsSome || state.Model.Phase<>Ready then Error CaptureNotReady
            else
                match state.Roots |> List.tryFind(fun root -> root.Path=state.Request.RepositoryRoot) with
                | None -> Error CaptureNotReady
                | Some root ->
                    use handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(nativeint root.Fd, false)
                    let mutable held = None
                    let callback () = lock state.Sync (fun () -> held |> Option.iter(fun lease -> state.RootLeases.Remove lease |> ignore))
                    match FS.GG.Governance.GateExecution.Interpreter.duplicateDirectoryLease handle root.Path state.Request.Budget state.Request.Cancellation callback with
                    | Error _ -> Error (AcquisitionFailed("repository-root","original directory lease failed"))
                    | Ok lease ->
                        held <- Some lease
                        state.RootLeases.Add lease
                        Ok lease)
