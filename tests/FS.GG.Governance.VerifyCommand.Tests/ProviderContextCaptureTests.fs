module FS.GG.Governance.VerifyCommand.Tests.ProviderContextCaptureTests
open Expecto
open System
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.VerifyCommand.ProviderContextCapture
// SYNTHETIC: public workflow/input semantics only; physical Linux capture has a separate native gate.
let now : MonotonicInstant = {Domain=Guid.Parse("791c51b2-31d7-451d-aede-81cc62b4f37e");Ticks=10L}
let request : Request =
    { Operation="Synthetic-capture"
      RepositoryRoot="/repo"
      Selection={ProvenancePath=".fsgg/provenance.json";ProvenanceSha256=String('a',64);PolicyPath="/host/policy.json";PolicySha256=String('B',64);Platform="linux-x64"}
      Namespace=CooperativelyStableNamespace
      Limits={PerFileBytes=4096;AggregateBytes=8192}
      Budget={WorkEnd={now with Ticks=100L};CleanupEnd={now with Ticks=200L}}
      Cancellation=Threading.CancellationToken.None }
let valid r = Expect.isEmpty (validateRequest now r) "valid explicit request"
let refused r = Expect.isNonEmpty (validateRequest now r) "invalid explicit request refuses"
let acquiring () = update CaptureRequested (init()) |> fst
let ready () = update CaptureComparisonSucceeded (acquiring()) |> fst
let cases : (string * (unit -> unit)) list =
    [
      ("Synthetic capture valid explicit selection", fun () ->
        valid request)
      ("Synthetic capture independent policy inside workspace remains explicit", fun () ->
        valid {request with Selection={request.Selection with PolicyPath="/repo/host-policy.json"}})
      ("Synthetic capture absolute contained provenance", fun () ->
        valid {request with Selection={request.Selection with ProvenancePath="/repo/.fsgg/provenance.json"}})
      ("Synthetic capture strong namespace refused", fun () ->
        refused {request with Namespace=RequireAdversarialAtomicSnapshot})
      ("Synthetic capture relative repository refused", fun () ->
        refused {request with RepositoryRoot="repo"})
      ("Synthetic capture provenance absolute escape refused", fun () ->
        refused {request with Selection={request.Selection with ProvenancePath="/repo-other/provenance.json"}})
      ("Synthetic capture provenance dotdot refused", fun () ->
        refused {request with Selection={request.Selection with ProvenancePath="src/../provenance.json"}})
      ("Synthetic capture relative policy refused", fun () ->
        refused {request with Selection={request.Selection with PolicyPath="policy.json"}})
      ("Synthetic capture policy dotdot refused", fun () ->
        refused {request with Selection={request.Selection with PolicyPath="/host/../policy.json"}})
      ("Synthetic capture duplicate selected files refused", fun () ->
        refused {request with Selection={request.Selection with PolicyPath="/repo/.fsgg/provenance.json"}})
      ("Synthetic capture case alias ambiguity refused", fun () ->
        refused {request with Selection={request.Selection with PolicyPath="/REPO/.FSGG/PROVENANCE.JSON"}})
      ("Synthetic capture duplicate separator refused", fun () ->
        refused {request with Selection={request.Selection with ProvenancePath="src//provenance.json"}})
      ("Synthetic capture file trailing separator refused", fun () ->
        refused {request with Selection={request.Selection with PolicyPath="/host/policy.json/"}})
      ("Synthetic capture malformed raw digest refused", fun () ->
        refused {request with Selection={request.Selection with PolicySha256="not-sha256"}})
      ("Synthetic capture per-file cap refused", fun () ->
        refused {request with Limits={request.Limits with PerFileBytes=0}})
      ("Synthetic capture aggregate cap refused", fun () ->
        refused {request with Limits={request.Limits with AggregateBytes=0}})
      ("Synthetic capture original domain refused", fun () ->
        refused {request with Budget={request.Budget with WorkEnd={now with Domain=Guid.Empty;Ticks=100L}}})
      ("Synthetic capture cleanup ordering refused", fun () ->
        refused {request with Budget={request.Budget with CleanupEnd={now with Ticks=99L}}})
      ("Synthetic capture work expiry refused", fun () ->
        refused {request with Budget={request.Budget with WorkEnd=now}})
      ("Synthetic capture no acquisition before request", fun () ->
        Expect.equal (init()).Phase Prepared "pure prepare stage")
      ("Synthetic capture one acquisition effect", fun () ->
        let m,e=update CaptureRequested (init())
        Expect.equal (m.CaptureConsumed,e) (true,[AcquireAndCompareSelected]) "single acquisition")
      ("Synthetic capture capture cannot retry", fun () ->
        let m,e=update CaptureRequested (acquiring())
        Expect.equal (m.FirstFailure,e) (Some CaptureAlreadyConsumed,[RetireOwnedObjects]) "no retry")
      ("Synthetic capture comparison required before ready", fun () ->
        Expect.equal (acquiring()).Phase Acquiring "no fabricated complete set")
      ("Synthetic capture successful complete comparison ready", fun () ->
        Expect.equal (ready()).Phase Ready "comparison ready")
      ("Synthetic capture revalidation same owner effect", fun () ->
        let m,e=update RevalidationRequested (ready())
        Expect.equal (m.Phase,e) (Revalidating,[RecompareSameObjects]) "same objects")
      ("Synthetic capture revalidation before capture refuses", fun () ->
        let m,_=update RevalidationRequested (init())
        Expect.equal m.FirstFailure (Some CaptureNotReady) "no replacement acquisition")
      ("Synthetic capture late comparison never erases failure", fun () ->
        let failed,_=update (FailureObserved WorkDeadlineReached) (acquiring())
        let m,_=update CaptureComparisonSucceeded failed
        Expect.equal (m.Phase,m.FirstFailure) (Retiring,Some WorkDeadlineReached) "sticky bounded cause")
      ("Synthetic capture distinct causes retained", fun () ->
        let failed,_=update (FailureObserved WorkDeadlineReached) (acquiring())
        let duplicate,_=update (FailureObserved WorkDeadlineReached) failed
        let cleanup,_=update (FailureObserved CleanupDeadlineReached) duplicate
        Expect.equal cleanup.SecondaryFailures [CleanupDeadlineReached] "distinct cleanup diagnosis")
      ("Synthetic capture release request does not imply settlement", fun () ->
        let m,e=update ReleaseRequested (ready())
        Expect.equal (m.Phase,e) (Retiring,[RetireOwnedObjects]) "actual retirement required")
      ("Synthetic capture repeated release emits no new activity", fun () ->
        let m,_=update ReleaseRequested (ready())
        let _,e=update ReleaseRequested m
        Expect.isEmpty e "no replacement retire task")
      ("Synthetic capture late settlement retains original cause", fun () ->
        let failed,_=update (FailureObserved CleanupDeadlineReached) (acquiring())
        let m,_=update OwnedResourcesSettled failed
        Expect.equal (m.Phase,m.FirstFailure) (Released,Some CleanupDeadlineReached) "late release not bounded success")
      ("Synthetic capture unowned settlement cannot release", fun () ->
        let m,_=update OwnedResourcesSettled (ready())
        Expect.equal m.Phase Ready "edge must request retirement")
      ("Synthetic capture native function signatures compile only", fun () ->
        let captureShape : CaptureSession -> Observation = capture
        let validationShape : CaptureSession -> Observation = revalidate
        let releaseShape : CaptureSession -> Result<unit,Failure> = release
        ignore (captureShape,validationShape,releaseShape))
      ("Synthetic capture null repository root refuses without dependent normalization", fun () ->
        refused {request with RepositoryRoot=Unchecked.defaultof<string>})
      ("Synthetic capture null provenance locator refuses", fun () ->
        refused {request with Selection={request.Selection with ProvenancePath=Unchecked.defaultof<string>}})
      ("Synthetic capture null policy locator refuses", fun () ->
        refused {request with Selection={request.Selection with PolicyPath=Unchecked.defaultof<string>}})
    ]
[<Tests>]
let tests = testList "ProviderContextCapture Synthetic" (cases |> List.map(fun(name,run)->testCase name run))

// Scheduling only: these effects require a separately selected outer-owned local fixture recipe.
// Ordinary full test discovery keeps every case pending and acquires no capture objects.
let nativeEnabled = Environment.GetEnvironmentVariable("FSGG_GOV_CAPTURE_NATIVE_TESTS")="1"
let sha (bytes:byte[]) = Security.Cryptography.SHA256.HashData bytes |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
let selectedOuter = lazy (
    if Diagnostics.Stopwatch.Frequency<>1000000000L then failtest "Selected Linux monotonic clock frequency unavailable"
    let selected name =
        match Environment.GetEnvironmentVariable name with
        | null -> failtest ("Original fixture deadline missing: "+name)
        | value -> value
    let ticks name = Int64.Parse(selected name,Globalization.CultureInfo.InvariantCulture)
    let epoch name =
        let value=Double.Parse(selected name,Globalization.CultureInfo.InvariantCulture)
        if not (Double.IsFinite value) then failtest "Original wall bound must be finite"
        value
    let now=FS.GG.Governance.GateExecution.Interpreter.currentInstant()
    let budget:ExecutionBudget =
        {WorkEnd={now with Ticks=ticks "FSGG_CAPTURE_OUTER_WORK_TICKS"}
         CleanupEnd={now with Ticks=ticks "FSGG_CAPTURE_OUTER_CLEANUP_TICKS"}}
    let observed=ticks "FSGG_CAPTURE_OUTER_OBSERVED_TICKS"
    let workEpoch=epoch "FSGG_CAPTURE_OUTER_WORK_EPOCH"
    let cleanupEpoch=epoch "FSGG_CAPTURE_OUTER_CLEANUP_EPOCH"
    if budget.WorkEnd.Ticks<=now.Ticks || budget.CleanupEnd.Ticks<budget.WorkEnd.Ticks || now.Ticks<observed || cleanupEpoch<workEpoch then
        failtest "Original fixture monotonic deadline/domain observation unavailable"
    budget,workEpoch,cleanupEpoch)
let actualBudget () : ExecutionBudget =
    let outer,_,_=selectedOuter.Value
    let instant=FS.GG.Governance.GateExecution.Interpreter.currentInstant()
    let seconds=int64 Diagnostics.Stopwatch.Frequency
    {WorkEnd={instant with Ticks=min outer.WorkEnd.Ticks (instant.Ticks+5L*seconds)}
     CleanupEnd={instant with Ticks=min outer.CleanupEnd.Ticks (instant.Ticks+10L*seconds)}}
let fixturePermitted (budget:ExecutionBudget) cleanup =
    let outer,workEpoch,cleanupEpoch=selectedOuter.Value
    let instant=FS.GG.Governance.GateExecution.Interpreter.currentInstant()
    let endTicks=if cleanup then min budget.CleanupEnd.Ticks outer.CleanupEnd.Ticks else min budget.WorkEnd.Ticks outer.WorkEnd.Ticks
    let epoch=if cleanup then cleanupEpoch else workEpoch
    instant.Domain=outer.WorkEnd.Domain && instant.Ticks<endTicks && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()<int64 (epoch*1000.0)
let fixtureEffect budget cleanup action =
    if not (fixturePermitted budget cleanup) then failtest "Original fixture deadline: no new filesystem effect"
    let result=action()
    if not (fixturePermitted budget cleanup) then failtest "Fixture filesystem effect returned after original deadline"
    result
let business budget action = fixtureEffect budget false action
let cleanupEffect budget action = fixtureEffect budget true action
let holdOwner (session:CaptureSession) (retirementError:Exception option) =
    while true do
        try Threading.Thread.Sleep 1000 with _ -> ()
        GC.KeepAlive session
        GC.KeepAlive retirementError
let retireOrRetain (session:CaptureSession) (budget:ExecutionBudget) =
    let mutable released=false
    let mutable retirementError:Exception option=None
    try
        while not released && fixturePermitted budget true do
            match release session with
            | Ok () -> released <- true
            | Error _ -> Threading.Thread.Sleep 1
    with ex -> retirementError <- Some ex
    if not released then
        // Projection failure cannot unwind the actual owner; this no-signal profile observes passively.
        try
            Console.Error.WriteLine("Capture fixture owner retained: "+session.Operation)
            retirementError |> Option.iter(fun error -> Console.Error.WriteLine("Capture retirement first failure: "+error.GetType().Name+": "+error.Message))
            Console.Error.Flush()
        with _ -> ()
        holdOwner session retirementError
let withFixture run =
    let budget=actualBudget()
    let basePath=
        match Environment.GetEnvironmentVariable("FSGG_CAPTURE_FIXTURE_ROOT") with
        | null -> failtest "Actual capture fixture root is not selected"
        | path -> path
    if String.IsNullOrWhiteSpace basePath || not (IO.Path.IsPathFullyQualified basePath) || not (business budget (fun () -> IO.Directory.Exists basePath)) then
        failtest "Actual capture fixture requires the selected existing absolute local root"
    let outer=IO.Path.Combine(basePath,Guid.NewGuid().ToString("N"))
    let repo=IO.Path.Combine(outer,"repo")
    let host=IO.Path.Combine(outer,"host")
    business budget (fun () -> IO.Directory.CreateDirectory repo) |> ignore
    business budget (fun () -> IO.Directory.CreateDirectory host) |> ignore
    let provenance=IO.Path.Combine(repo,"provenance.json")
    let policy=IO.Path.Combine(host,"policy.json")
    let original=Text.Encoding.UTF8.GetBytes "provenance-original"
    let policyBytes=Text.Encoding.UTF8.GetBytes "policy-original"
    business budget (fun () -> IO.File.WriteAllBytes(provenance,original))
    business budget (fun () -> IO.File.WriteAllBytes(policy,policyBytes))
    let request:Request =
        {Operation="Actual-capture-"+Guid.NewGuid().ToString("N");RepositoryRoot=repo
         Selection={ProvenancePath="provenance.json";ProvenanceSha256=sha original;PolicyPath=policy;PolicySha256=sha policyBytes;Platform="linux-x64"}
         Namespace=CooperativelyStableNamespace;Limits={PerFileBytes=4096;AggregateBytes=8192}
         Budget=budget;Cancellation=Threading.CancellationToken.None}
    let mutable held:CaptureSession option=None
    let prepareOwned selected =
        match prepare selected with
        | Error causes -> failtestf "Actual fixture prepare refused: %A" causes
        | Ok session -> held <- Some session; session
    let mutable firstFailure:System.Runtime.ExceptionServices.ExceptionDispatchInfo option=None
    try run request prepareOwned provenance policy original
    with ex -> firstFailure <- Some(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture ex)
    try
        match held with
        | Some session -> retireOrRetain session budget
        | None -> ()
        cleanupEffect budget (fun () -> IO.Directory.Delete(outer,true))
    with ex ->
        if Option.isNone firstFailure then firstFailure <- Some(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture ex)
        else
            try Console.Error.WriteLine("Capture fixture secondary cleanup failure: "+ex.GetType().Name)
            with _ -> ()
    match firstFailure with
    | Some failure -> failure.Throw()
    | None -> ()
let expectReady (observation:Observation) =
    Expect.equal observation.Model.FirstFailure None "actual capture has no failure"
    Expect.equal observation.Model.Phase Ready "complete set compared"
    Expect.equal observation.Documents.Length 2 "both explicitly selected files captured"
    Expect.all observation.Documents (fun document -> document.ComparisonPasses>=1) "held files compared"
    Expect.all observation.Roots (fun root -> root.CurrentBindingMatches=Some true) "both root bindings observed"
let changed (observation:Observation) =
    Expect.isSome observation.Model.FirstFailure "changed input cannot pass"
    Expect.notEqual observation.Model.Phase Ready "failed comparison cannot stay ready"
let baseActualCases : (string * (unit -> unit)) list =
    [ "Actual capture explicit two roots immutable bytes and revalidation", fun () ->
          withFixture(fun r own _ _ original ->
              let session=own r
              let observed=capture session
              expectReady observed
              Expect.sequenceEqual observed.Documents[0].Bytes original "exact original bytes"
              let revalidated=revalidate session
              expectReady revalidated
              Expect.all revalidated.Documents (fun document -> document.ComparisonPasses=2) "second same-owner comparison"
              Expect.equal (inspect session).Documents revalidated.Documents "nonblocking immutable snapshot")
      "Actual capture per-file cap refuses before excess retention", fun () ->
          withFixture(fun r own _ _ _ ->
              let result=capture (own {r with Limits={r.Limits with PerFileBytes=4}})
              Expect.equal result.Model.FirstFailure (Some(ByteLimitExceeded "provenance")) "bounded first cause")
      "Actual capture aggregate remaining across two files", fun () ->
          withFixture(fun r own _ _ original ->
              let result=capture (own {r with Limits={r.Limits with AggregateBytes=original.Length+2}})
              Expect.equal result.Model.FirstFailure (Some(ByteLimitExceeded "policy")) "aggregate charge spans selection")
      "Actual capture raw digest mismatch is not policy authority", fun () ->
          withFixture(fun r own _ _ _ ->
              let result=capture (own {r with Selection={r.Selection with PolicySha256=String('0',64)}})
              Expect.equal result.Model.FirstFailure (Some(RawDigestMismatch "policy")) "exact bytes required")
      "Actual capture provenance symlink refused", fun () ->
          withFixture(fun r own provenance policy _ ->
              business r.Budget (fun () -> IO.File.Delete provenance)
              business r.Budget (fun () -> IO.File.CreateSymbolicLink(provenance,policy)) |> ignore
              changed (capture (own r)))
      "Actual capture independent policy symlink refused", fun () ->
          withFixture(fun r own provenance policy _ ->
              business r.Budget (fun () -> IO.File.Delete policy)
              business r.Budget (fun () -> IO.File.CreateSymbolicLink(policy,provenance)) |> ignore
              changed (capture (own r)))
      "Actual capture selected directory refused before data open", fun () ->
          withFixture(fun r own provenance _ _ ->
              business r.Budget (fun () -> IO.File.Delete provenance)
              business r.Budget (fun () -> IO.Directory.CreateDirectory provenance) |> ignore
              changed (capture (own r)))
      "Actual capture controlled in-place growth refuses revalidation", fun () ->
          withFixture(fun r own provenance _ _ ->
              let session=own r
              expectReady (capture session)
              business r.Budget (fun () -> IO.File.AppendAllText(provenance,"-growth"))
              changed (revalidate session))
      "Actual capture controlled same-length bytes drift refuses", fun () ->
          withFixture(fun r own provenance _ original ->
              let session=own r
              expectReady (capture session)
              business r.Budget (fun () -> IO.File.WriteAllBytes(provenance,Array.create original.Length 120uy))
              changed (revalidate session))
      "Actual capture entry replacement refuses same held-object admission", fun () ->
          withFixture(fun r own provenance _ original ->
              let session=own r
              expectReady (capture session)
              business r.Budget (fun () -> IO.File.Move(provenance,provenance+".old"))
              business r.Budget (fun () -> IO.File.WriteAllBytes(provenance,original))
              changed (revalidate session))
      "Actual capture second selected file drift refuses", fun () ->
          withFixture(fun r own _ policy _ ->
              let session=own r
              expectReady (capture session)
              business r.Budget (fun () -> IO.File.AppendAllText(policy,"-drift"))
              changed (revalidate session))
      "Actual capture root relocation is not current pathname freshness", fun () ->
          withFixture(fun r own provenance _ _ ->
              let session=own r
              expectReady (capture session)
              let root=
                  match IO.Path.GetDirectoryName provenance with
                  | null -> failtest "Actual provenance parent is unavailable"
                  | path -> path
              business r.Budget (fun () -> IO.Directory.Move(root,root+".old"))
              business r.Budget (fun () -> IO.Directory.CreateDirectory root) |> ignore
              changed (revalidate session))
      "Actual capture cancellation before acquisition has settled owner", fun () ->
          withFixture(fun r own _ _ _ ->
              use cancelled=new Threading.CancellationTokenSource()
              cancelled.Cancel()
              let session=own {r with Cancellation=cancelled.Token}
              let observed=capture session
              Expect.equal observed.Model.FirstFailure (Some CancellationRequested) "no capture after cancellation")
      "Actual capture original work expiry is not renewed by capture", fun () ->
          withFixture(fun r own _ _ _ ->
              let now=FS.GG.Governance.GateExecution.Interpreter.currentInstant()
              let session=own {r with Budget={r.Budget with WorkEnd={now with Ticks=min r.Budget.WorkEnd.Ticks (now.Ticks+int64 Diagnostics.Stopwatch.Frequency/20L)}}}
              Threading.Thread.Sleep 75
              let observed=capture session
              Expect.equal observed.Model.FirstFailure (Some WorkDeadlineReached) "original end retained")
      "Actual capture one-use owner and late retirement preserve failure", fun () ->
          withFixture(fun r own _ _ _ ->
              let session=own r
              expectReady (capture session)
              let duplicate=capture session
              Expect.equal duplicate.Model.FirstFailure (Some CaptureAlreadyConsumed) "no acquisition retry"
              retireOrRetain session r.Budget
              let settled=inspect session
              Expect.equal settled.Settlement ResourcesReleased "actual close completion"
              Expect.equal settled.Model.FirstFailure duplicate.Model.FirstFailure "retirement preserves original cause") ]
let actualCases = baseActualCases @ [
    "Actual capture cancelled revalidation retains capture and outstanding original lease", fun () ->
        withFixture(fun r own _ _ _ ->
            use cancelled=new Threading.CancellationTokenSource()
            let session=own {r with Cancellation=cancelled.Token}
            expectReady (capture session)
            let lease=match borrowRepositoryRoot session with Ok held -> held | Error cause -> failtestf "borrow refused: %A" cause
            try
                cancelled.Cancel()
                let failed=revalidate session
                Expect.equal failed.Model.FirstFailure (Some CancellationRequested) "original cancellation is sticky"
                Expect.equal failed.Model.Phase Retiring "capture must keep its originals while borrowed"
                Expect.equal failed.Settlement Retained "duplicate owner prevents a resource-release claim"
                Expect.equal (release session) (Error OwnedActivitiesPending) "released shortcut cannot bypass original lease"
            finally
                let mutable settled=false
                while not settled do
                    match FS.GG.Governance.GateExecution.Interpreter.releaseDirectoryLease lease with
                    | Ok () -> settled <- true
                    | Error _ -> Threading.Thread.Sleep 5
            retireOrRetain session r.Budget
            Expect.equal (inspect session).Model.FirstFailure (Some CancellationRequested) "late settlement cannot erase first failure"
            Expect.equal (inspect session).Settlement ResourcesReleased "both original owners actually settled")
    "Actual capture changed revalidation retains originals until outstanding lease settles", fun () ->
        withFixture(fun r own provenance _ _ ->
            let session=own r
            expectReady (capture session)
            let lease=match borrowRepositoryRoot session with Ok held -> held | Error cause -> failtestf "borrow refused: %A" cause
            let mutable first=None
            try
                business r.Budget (fun () -> IO.File.AppendAllText(provenance,"-drift"))
                let failed=revalidate session
                first <- failed.Model.FirstFailure
                Expect.isSome first "actual drift refused"
                Expect.equal failed.Model.Phase Retiring "original capture retained during borrower use"
                Expect.equal failed.Settlement Retained "snapshot cannot claim duplicate was released"
                Expect.equal (release session) (Error OwnedActivitiesPending) "capture release fences same outstanding lease"
            finally
                let mutable settled=false
                while not settled do
                    match FS.GG.Governance.GateExecution.Interpreter.releaseDirectoryLease lease with
                    | Ok () -> settled <- true
                    | Error _ -> Threading.Thread.Sleep 5
            retireOrRetain session r.Budget
            Expect.equal (inspect session).Model.FirstFailure first "first causal drift survives settlement"
            Expect.equal (inspect session).Settlement ResourcesReleased "both owners settled")

    "Actual capture original root lease binds identity and fences capture release", fun () ->
        withFixture(fun r own _ _ _ ->
            let session=own r
            expectReady (capture session)
            let lease=
                match borrowRepositoryRoot session with
                | Ok held -> held
                | Error cause -> failtestf "held original root borrow refused: %A" cause
            try
                let observed=inspect session
                let root=observed.Roots |> List.find(fun root -> root.RequestedPath=r.RepositoryRoot)
                Expect.equal lease.RequestedPath r.RepositoryRoot "same caller-selected original root"
                Expect.equal lease.Identity.Inode root.HeldIdentity.Inode "same held inode"
                Expect.equal lease.Identity.MountId root.HeldIdentity.MountId "same held mount"
                Expect.equal lease.Budget r.Budget "original ends are not renewed"
                Expect.equal (release session) (Error OwnedActivitiesPending) "capture cannot retire while actual root lease remains"
                Expect.equal (inspect session).Model.Phase Ready "borrow fence does not replace original session"
            finally
                let mutable released=false
                while not released do
                    match FS.GG.Governance.GateExecution.Interpreter.releaseDirectoryLease lease with
                    | Ok () -> released <- true
                    | Error _ -> Threading.Thread.Sleep 5)
    "Actual capture root lease refuses original file drift", fun () ->
        withFixture(fun r own provenance _ _ ->
            let session=own r
            expectReady (capture session)
            business r.Budget (fun () -> IO.File.AppendAllText(provenance,"-changed"))
            Expect.isError (borrowRepositoryRoot session) "must revalidate original captured bytes before root transfer")
    "Actual capture root lease refuses before actual readiness", fun () ->
        withFixture(fun r own _ _ _ ->
            let session=own r
            Expect.isError (borrowRepositoryRoot session) "prepared receipt is not ready-root authority") ]
[<Tests>]
let nativeTests =
    testList "ProviderContextCapture Actual" (actualCases |> List.map(fun (name,run) ->
        if nativeEnabled then testCase name run else ptestCase name run))

// Controlled real interpreter: synthetic descriptors and finite gates, no kernel acquisition.
// Every paused activity is unblocked and joined in finally, even after an assertion fails.
type private ControlledFixture(blockRead: int, failClose: bool, provenancePayload: byte[], policyPayload: byte[]) =
    let entered = new Threading.ManualResetEventSlim(false)
    let resume = new Threading.ManualResetEventSlim(false)
    // Optional finite gate on one later metadata read, armed only after a ready capture.
    let statEntered = new Threading.ManualResetEventSlim(false)
    let statResume = new Threading.ManualResetEventSlim(false)
    let mutable statGate : (string -> bool) option = None
    // Optional finite gate on the next descriptor-acquiring call; when released it fails without a descriptor.
    let openEntered = new Threading.ManualResetEventSlim(false)
    let openResume = new Threading.ManualResetEventSlim(false)
    let mutable openGate = false
    let descriptors = Collections.Generic.Dictionary<int, string>()
    let positions = Collections.Generic.Dictionary<int, int>()
    let closes = Collections.Generic.List<int>()
    let mutable next = 1000
    let mutable reads = 0
    let mutable ticks = 10L
    let mutable changed = false
    let digestOf (value: byte[]) = Security.Cryptography.SHA256.HashData(value) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
    let payloadAt (path: string) = if path.Contains "provenance" then provenancePayload else policyPayload
    let allocate path =
        next <- next + 1
        descriptors.Add(next,path)
        positions.Add(next,0)
        next
    let write (bytes:byte[]) offset (value:byte[]) = Array.Copy(value,0,bytes,offset,value.Length)
    let operations : Operations =
        { Current = fun () -> {now with Ticks=Threading.Interlocked.Read(&ticks)}
          OpenAt = fun directory path _ _ ->
              if openGate then
                  openGate <- false
                  openEntered.Set()
                  if not (openResume.Wait(TimeSpan.FromSeconds 5.)) then failwith "finite controlled open gate expired"
                  -1L,5
              else
                  let absolute = if directory = -100 then path else descriptors[directory]+"/"+path
                  int64 (allocate absolute),0
          Stat = fun fd bytes ->
              let path=descriptors[fd]
              let directory=path="/repo" || path="/host"
              match statGate with
              | Some selected when selected path ->
                  statGate <- None
                  statEntered.Set()
                  if not (statResume.Wait(TimeSpan.FromSeconds 5.)) then failwith "finite controlled metadata gate expired"
              | _ -> ()
              write bytes 0 (BitConverter.GetBytes 0x17ffu)
              write bytes 28 (BitConverter.GetBytes (if directory then 0x4000us else 0x8000us))
              let identity = if path="/repo" then 1UL elif path="/host" then 2UL elif path.Contains("provenance") then 3UL else 4UL
              write bytes 32 (BitConverter.GetBytes (if changed && not directory then identity+10UL else identity))
              write bytes 40 (BitConverter.GetBytes (if directory then 0UL else uint64 (payloadAt path).Length))
              write bytes 144 (BitConverter.GetBytes 1UL)
              0,0
          OpenRead = fun path ->
              let original=Int32.Parse(path.Substring(path.LastIndexOf('/')+1))
              allocate descriptors[original],0
          Read = fun fd bytes ->
              reads <- reads + 1
              if reads=blockRead then
                  entered.Set()
                  if not (resume.Wait(TimeSpan.FromSeconds 5.)) then failwith "finite controlled read gate expired"
              let position=positions[fd]
              let payload=payloadAt descriptors[fd]
              let count=min bytes.Length (payload.Length-position)
              Array.Copy(payload,position,bytes,0,count)
              positions[fd] <- position+count
              int64 count,0
          Seek = fun fd -> positions[fd] <- 0; 0L,0
          Close = fun fd -> closes.Add fd; (if failClose then -1,5 else 0,0) }
    new(blockRead: int, failClose: bool) =
        let payload = Text.Encoding.UTF8.GetBytes "bounded-document"
        new ControlledFixture(blockRead, failClose, payload, payload)
    member _.Operations = operations
    member _.Request = {request with Selection={request.Selection with ProvenanceSha256=digestOf provenancePayload;PolicySha256=digestOf policyPayload}}
    member _.Advance value = Threading.Interlocked.Exchange(&ticks,value) |> ignore
    member _.ChangeBinding() = changed <- true
    member _.CloseAttempts = closes |> Seq.toList
    member _.ReuseUncertainIdentity() =
        // Simulate another owner's reuse of the failed close's numeric descriptor.
        descriptors[closes[0]] <- "/unrelated-owner"
        positions[closes[0]] <- 0
    member _.WaitBlocked() = Expect.isTrue (entered.Wait(TimeSpan.FromSeconds 3.)) "controlled read entered within finite bound"
    member _.Resume() = resume.Set()
    /// Blocks the next metadata read whose descriptor path satisfies the predicate.
    member _.ArmMetadataGate(selected: string -> bool) = statGate <- Some selected
    member _.WaitMetadataBlocked() = Expect.isTrue (statEntered.Wait(TimeSpan.FromSeconds 3.)) "controlled metadata read entered within finite bound"
    member _.ResumeMetadata() = statResume.Set()
    /// Blocks the first descriptor acquisition (holding no descriptor); on resume it fails with an errno.
    member _.ArmOpenFailureGate() = openGate <- true
    member _.WaitOpenBlocked() = Expect.isTrue (openEntered.Wait(TimeSpan.FromSeconds 3.)) "controlled open entered within finite bound"
    member _.ResumeOpen() = openResume.Set()
    interface IDisposable with
        member _.Dispose() = resume.Dispose(); entered.Dispose(); statResume.Dispose(); statEntered.Dispose(); openResume.Dispose(); openEntered.Dispose()

let private controlledSession (fixture:ControlledFixture) request =
    match prepareControlled fixture.Operations request with
    | Ok session -> session
    | Error errors -> failtestf "controlled prepare refused: %A" errors
let private joined (task:Threading.Tasks.Task<'a>) =
    Expect.isTrue (task.Wait(TimeSpan.FromSeconds 8.)) "actual controlled activity returned within finite bound"
    task.GetAwaiter().GetResult()
let private settleControlled session =
    Expect.isTrue
        (Threading.SpinWait.SpinUntil((fun () -> not (ownership session).PendingActivity),TimeSpan.FromSeconds 3.))
        "actual owner task finished"
let private releaseControlled session =
    release session |> ignore
    settleControlled session
    Expect.equal (release session) (Ok ()) "settled finite owner releases"
    Expect.equal (inspect session).Settlement ResourcesReleased "all release predicates hold"
let private pausedControl action =
    use fixture = new ControlledFixture(3,false)
    let session=controlledSession fixture fixture.Request
    let caller=Threading.Tasks.Task.Run(fun () -> capture session)
    try
        fixture.WaitBlocked()
        action fixture session caller
    finally
        fixture.Resume()
        joined caller |> ignore
        settleControlled session

[<Tests>]
let controlledTests =
    testList "ProviderContextCapture Controlled interpreter" [
        testCase "finite success and original-object revalidation" (fun () ->
            use fixture = new ControlledFixture(0,false)
            let session=controlledSession fixture fixture.Request
            Expect.equal (capture session).Model.Phase Ready "real comparison flow ready"
            Expect.equal (revalidate session).Model.Phase Ready "same objects revalidated"
            Expect.equal (inspect session).Documents.Length 2 "complete selected set"
            releaseControlled session)
        testCase "pending read retains original task descriptors and bounded evidence" (fun () ->
            pausedControl (fun fixture session caller ->
                let before=inspect session
                Expect.equal before.Documents.Length 1 "first document retained before policy block"
                Expect.isTrue (before.Documents[0].Bytes.Length<=fixture.Request.Limits.PerFileBytes) "bounded original evidence"
                Expect.isTrue (ownership session).PendingActivity "registered actual activity"
                fixture.Advance 100L
                Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> (inspect session).Model.FirstFailure=Some WorkDeadlineReached),TimeSpan.FromSeconds 2.)) "original work end observed"
                fixture.Advance 200L
                let retained=joined caller
                Expect.equal retained.Settlement Retained "cleanup expiry retains custody"
                Expect.equal retained.Model.FirstFailure (Some WorkDeadlineReached) "first cause retained"
                Expect.contains retained.Model.SecondaryFailures CleanupDeadlineReached "later cleanup cause retained"
                Expect.isGreaterThan (ownership session).RetainedDescriptors 0 "descriptors retained"
                Expect.equal (release session) (Error OwnedActivitiesPending) "release refuses pending read"
                fixture.Resume()
                settleControlled session
                let late=inspect session
                Expect.equal late.Documents before.Documents "no replacement output after failed read"
                Expect.equal late.Model.FirstFailure retained.Model.FirstFailure "late read cannot erase first cause"
                Expect.equal late.Settlement Retained "expired cleanup cannot release descriptors"
                Expect.equal (release session) (Error CleanupDeadlineReached) "original cleanup end never renews"))
        testCase "cancellation before cleanup settles actual activity" (fun () ->
            use fixture = new ControlledFixture(1,false)
            use cancelled = new Threading.CancellationTokenSource()
            let session=controlledSession fixture {fixture.Request with Cancellation=cancelled.Token}
            let caller=Threading.Tasks.Task.Run(fun () -> capture session)
            try
                fixture.WaitBlocked()
                cancelled.Cancel()
                Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> (inspect session).Model.FirstFailure=Some CancellationRequested),TimeSpan.FromSeconds 2.)) "cancellation observed"
            finally fixture.Resume(); joined caller |> ignore; settleControlled session
            Expect.equal (inspect session).Model.FirstFailure (Some CancellationRequested) "first cause survives retirement"
            releaseControlled session)
        testCase "pending revalidation cannot replace task or create readiness" (fun () ->
            pausedControl (fun fixture session caller ->
                let retry=Threading.Tasks.Task.Run(fun () -> revalidate session)
                try
                    Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> (inspect session).Model.FirstFailure=Some OperationInProgress),TimeSpan.FromSeconds 2.)) "pending revalidation recorded before expiry"
                finally fixture.Advance 200L
                let rejected=joined retry
                Expect.equal rejected.Model.Phase Retiring "pending revalidation refuses"
                Expect.equal rejected.Model.FirstFailure (Some OperationInProgress) "pending task is original cause"
                Expect.equal rejected.Settlement Retained "same owner retained"
                joined caller |> ignore))
        testCase "pending borrow refuses and keeps same owner" (fun () ->
            pausedControl (fun fixture session caller ->
                fixture.Advance 200L
                Expect.isError (borrowRepositoryRoot session) "pending owner gives no lease"
                Expect.equal (ownership session).BorrowedRoots 0 "no native borrowing attempted"
                Expect.isTrue (ownership session).PendingActivity "original task remains"
                joined caller |> ignore))
        testCase "uncertain close stays sticky and synthetic identity is never retried" (fun () ->
            use fixture = new ControlledFixture(0,true)
            let session=controlledSession fixture fixture.Request
            let observation=capture session
            Expect.equal observation.Model.FirstFailure (Some OwnedActivitiesPending) "failed native-shaped close retained"
            let attempts=fixture.CloseAttempts
            Expect.equal attempts.Length 1 "one close attempt"
            fixture.ReuseUncertainIdentity()
            for _ in 1..3 do
                Expect.equal (release session) (Error OwnedActivitiesPending) "uncertain numeric identity cannot retire"
                Expect.isError (borrowRepositoryRoot session) "uncertainty cannot create lease"
            Expect.equal fixture.CloseAttempts attempts "no numeric descriptor retry"
            Expect.isTrue (ownership session).CloseUncertain "sticky uncertainty"
            Expect.equal (inspect session).Settlement Retained "no false settlement")
        testCase "byte limit first cause survives completed retirement" (fun () ->
            use fixture = new ControlledFixture(0,false)
            let session=controlledSession fixture {fixture.Request with Limits={PerFileBytes=1;AggregateBytes=2}}
            Expect.equal (capture session).Model.FirstFailure (Some(ByteLimitExceeded "provenance")) "actual read enforces cap"
            Expect.isEmpty (inspect session).Documents "over-limit bytes never become observations"
            releaseControlled session)
        testCase "digest failure precedes close uncertainty" (fun () ->
            use fixture = new ControlledFixture(0,true)
            let session=controlledSession fixture {fixture.Request with Selection={fixture.Request.Selection with ProvenanceSha256=String('0',64)}}
            let observation=capture session
            Expect.equal observation.Model.FirstFailure (Some(RawDigestMismatch "provenance")) "original digest failure preserved"
            Expect.contains observation.Model.SecondaryFailures OwnedActivitiesPending "independent close failure preserved"
            Expect.equal (release session) (Error OwnedActivitiesPending) "uncertainty retains owner")
        testCase "changed original binding refuses revalidation" (fun () ->
            use fixture = new ControlledFixture(0,false)
            let session=controlledSession fixture fixture.Request
            Expect.equal (capture session).Model.Phase Ready "ready before drift"
            fixture.ChangeBinding()
            Expect.equal (revalidate session).Model.FirstFailure (Some(InputChanged "provenance")) "original-object comparison rejects drift"
            releaseControlled session)
        testCase "one-use acquisition cannot replace retained original evidence" (fun () ->
            use fixture = new ControlledFixture(0,false)
            let session=controlledSession fixture fixture.Request
            let first=capture session
            let second=capture session
            Expect.equal second.Model.FirstFailure (Some CaptureAlreadyConsumed) "no second acquisition"
            Expect.equal second.Documents first.Documents "same bounded original bytes"
            releaseControlled session)
    ] |> testSequenced

// Direct ProviderContextLoader.load and Program.runWithCapturedProvider controls. The ready capture is the
// real interpreter over the private controlled fixture above (synthetic descriptors, no kernel acquisition),
// so the fixture stays private to the capture tests. The caller owns every session throughout: nothing below
// lets the loader or the adapter acquire, renew, revalidate or release it.
let private provenanceDocument () = ProviderContextLoaderTests.provenanceBytes (ProviderContextLoaderTests.coherentRecord ())
let private newLoaderFixture () = new ControlledFixture(0,false,provenanceDocument (),ProviderContextLoaderTests.coherentPolicyBytes)
// The coherent provenance exceeds the default synthetic per-file cap; the cap is part of the request, not the loader.
let private loaderRequest (fixture:ControlledFixture) =
    {fixture.Request with Limits={PerFileBytes=1 <<< 20;AggregateBytes=1 <<< 21}}
let private loaderSelection (request:Request) : ProviderContext.Selection = {request.Selection with Platform="fixture-platform"}
let private readySession (fixture:ControlledFixture) request =
    let session=controlledSession fixture request
    let observed=capture session
    Expect.equal observed.Model.Phase Ready (sprintf "ready capture fixture: %A" observed.Model)
    Expect.equal observed.Documents.Length 2 "both selected documents retained"
    session
let private load selection session = ProviderContextLoader.load selection session
let private refusedOnly path (result:Result<_,Fsgg.ProviderCatalog.Diagnostic list>) =
    match result with
    | Ok _ -> failtest "loader admitted a capture or declaration it must refuse"
    | Error errors ->
        Expect.equal (errors |> List.map(fun d -> d.Path)) [path] (sprintf "single located refusal: %A" errors)
        Expect.equal (errors |> List.map(fun d -> d.Code)) ["verify.providerContextInvalid"] "stable refusal code"
let private admitted result =
    match result with
    | Ok request -> request
    | Error errors -> failtestf "ready capture was not admitted: %A" errors

[<Tests>]
let loaderOverReadyCaptureTests =
    testList "ProviderContextLoader over a ready controlled capture" [
        testCase "ready retained capture yields the request decoded from its captured bytes and stays caller-owned" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let selection=loaderSelection request
            let session=readySession fixture request
            let before=inspect session
            let ownedBefore=ownership session
            let closesBefore=fixture.CloseAttempts
            let expected=ProviderContextLoader.decode selection (provenanceDocument ()) ProviderContextLoaderTests.coherentPolicyBytes |> admitted
            let first=load selection session |> admitted
            Expect.equal first expected "load decodes exactly the captured provenance and policy bytes"
            Expect.equal first.RequiredCapabilityIds ["build:build"] "independently selected floor survives"
            Expect.equal (load selection session |> admitted) first "load is repeatable: it consumes nothing"
            let after=inspect session
            Expect.equal after.Model before.Model "model unchanged"
            Expect.equal after.Documents before.Documents "retained bytes unchanged"
            Expect.equal after.Roots before.Roots "root observations unchanged"
            Expect.equal after.Settlement Retained "load never settles the caller's session"
            Expect.equal (ownership session) ownedBefore "descriptors and activity unchanged"
            Expect.equal fixture.CloseAttempts closesBefore "load closes nothing"
            releaseControlled session)
        testCase "selected digests compare case-insensitively against captured raw digests" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            let selection=loaderSelection request
            let upper={selection with ProvenanceSha256=selection.ProvenanceSha256.ToUpperInvariant();PolicySha256=selection.PolicySha256.ToUpperInvariant()}
            Expect.notEqual upper selection "selection really differs in case"
            Expect.equal (load upper session |> admitted) (load selection session |> admitted) "same request for the same captured objects"
            releaseControlled session)
        testCase "selected platform is applied to the captured declaration, not taken from the capture request" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            load {loaderSelection request with Platform="other-platform"} session |> refusedOnly "$.platform"
            releaseControlled session)
        testCase "captured provenance that the actual package cannot decode is refused through load" (fun () ->
            use fixture = new ControlledFixture(0,false,Text.Encoding.UTF8.GetBytes "{\"schemaVersion\":99}",ProviderContextLoaderTests.coherentPolicyBytes)
            let request=loaderRequest fixture
            let session=readySession fixture request
            match load (loaderSelection request) session with
            | Ok _ -> failtest "unsupported captured schema must not become a request"
            | Error errors -> Expect.isNonEmpty errors "actual package refusal of the captured bytes"
            releaseControlled session)
        testCase "session that was never captured is not ready and is not acquired by load" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=controlledSession fixture request
            load (loaderSelection request) session |> refusedOnly "$.capture"
            Expect.isFalse (inspect session).Model.CaptureConsumed "load never starts the one-use capture"
            Expect.isEmpty (inspect session).Documents "no document was read for the loader"
            Expect.equal (ownership session).RetainedDescriptors 0 "no descriptor was acquired"
            Expect.equal (capture session).Model.Phase Ready "caller can still capture the same owner afterwards"
            releaseControlled session)
        testCase "capture that failed keeps its first cause and is not admitted" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=controlledSession fixture {request with Limits={PerFileBytes=8;AggregateBytes=16}}
            let failed=capture session
            Expect.equal failed.Model.FirstFailure (Some(ByteLimitExceeded "provenance")) "actual cap failure"
            load (loaderSelection request) session |> refusedOnly "$.capture"
            Expect.equal (inspect session).Model.FirstFailure failed.Model.FirstFailure "load cannot erase or replace the first cause"
            releaseControlled session)
        testCase "capture whose original binding drifted is not admitted after failed revalidation" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            load (loaderSelection request) session |> admitted |> ignore
            fixture.ChangeBinding()
            Expect.equal (revalidate session).Model.FirstFailure (Some(InputChanged "provenance")) "original-object comparison rejects drift"
            load (loaderSelection request) session |> refusedOnly "$.capture"
            releaseControlled session)
        testCase "released session is no longer admissible" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            releaseControlled session
            Expect.equal (inspect session).Settlement ResourcesReleased "caller released its own owner"
            load (loaderSelection request) session |> refusedOnly "$.capture")
        testCase "revalidation in flight is not ready even when every root binding was just refreshed" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            fixture.ArmMetadataGate(fun path -> path.Contains "provenance")
            let revalidating=Threading.Tasks.Task.Run(fun () -> revalidate session)
            try
                fixture.WaitMetadataBlocked()
                let observed=inspect session
                Expect.equal observed.Model.Phase Revalidating "actual revalidation window"
                Expect.isTrue (observed.Roots |> List.forall(fun root -> root.CurrentBindingMatches=Some true)) "only the phase can refuse here"
                Expect.isTrue (observed.Model.CaptureConsumed && observed.Model.FirstFailure.IsNone && observed.Settlement=Retained) "no other readiness guard applies"
                load (loaderSelection request) session |> refusedOnly "$.capture"
            finally
                fixture.ResumeMetadata()
                joined revalidating |> ignore
            Expect.equal (inspect session).Model.Phase Ready "revalidation completed on the same owner"
            load (loaderSelection request) session |> admitted |> ignore
            releaseControlled session)
        testCase "original work end reached after ready is refused without cancelling or renewing" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            load (loaderSelection request) session |> admitted |> ignore
            fixture.Advance request.Budget.WorkEnd.Ticks
            let observed=inspect session
            Expect.isTrue (observed.Model.Phase=Ready && observed.WorkEndReached && not observed.CleanupEndReached && not observed.CancellationObserved) "only the work end differs"
            load (loaderSelection request) session |> refusedOnly "$.capture"
            Expect.equal (inspect session).Model observed.Model "load does not mutate the model"
            fixture.Advance 10L
            releaseControlled session)
        testCase "original cleanup end reached after ready is refused" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            fixture.Advance request.Budget.CleanupEnd.Ticks
            let observed=inspect session
            Expect.isTrue (observed.Model.Phase=Ready && observed.CleanupEndReached) "cleanup end observed"
            load (loaderSelection request) session |> refusedOnly "$.capture"
            Expect.equal (inspect session).Settlement Retained "load never releases past the original cleanup end")
        testCase "cancellation after ready is refused" (fun () ->
            use fixture = newLoaderFixture ()
            use cancelled = new Threading.CancellationTokenSource()
            let request={loaderRequest fixture with Cancellation=cancelled.Token}
            let session=readySession fixture request
            load (loaderSelection request) session |> admitted |> ignore
            cancelled.Cancel()
            let observed=inspect session
            Expect.isTrue (observed.Model.Phase=Ready && observed.CancellationObserved && not observed.WorkEndReached) "only cancellation differs"
            load (loaderSelection request) session |> refusedOnly "$.capture"
            releaseControlled session)
        testCase "selected provenance path must be the compared captured provenance path" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            load {loaderSelection request with ProvenancePath=".fsgg/other-provenance.json"} session |> refusedOnly "$.capture.documents"
            releaseControlled session)
        testCase "selected policy path must be the compared captured policy path" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            load {loaderSelection request with PolicyPath="/host/other-policy.json"} session |> refusedOnly "$.capture.documents"
            releaseControlled session)
        testCase "captured documents cannot be presented under each other's role" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            let selection=loaderSelection request
            let swapped=
                {selection with
                    ProvenancePath=selection.PolicyPath; ProvenanceSha256=selection.PolicySha256
                    PolicyPath=selection.ProvenancePath; PolicySha256=selection.ProvenanceSha256}
            load swapped session |> refusedOnly "$.capture.documents"
            releaseControlled session)
        testCase "selected raw digests must equal the captured document digests before any decoding" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let session=readySession fixture request
            let selection=loaderSelection request
            load {selection with ProvenanceSha256=String('0',64)} session |> refusedOnly "$.capture.documents"
            load {selection with PolicySha256=String('0',64)} session |> refusedOnly "$.capture.documents"
            releaseControlled session)
    ] |> testSequenced

type private AdapterRun =
    { Code: int
      Messages: string list
      PortsBuilt: int
      Supplied: (Loop.RunRequest * ProviderContext.Selection) list
      Executed: bool
      Reused: bool
      Writes: int }

let private adapterArgv repo (selection:ProviderContext.Selection) =
    [| "--repo"; repo
       "--provider-provenance"; selection.ProvenancePath; "--provider-provenance-sha256"; selection.ProvenanceSha256
       "--provider-policy"; selection.PolicyPath; "--provider-policy-sha256"; selection.PolicySha256
       "--provider-platform"; selection.Platform; "--paths"; "src/a.fs" |]

let private boundContext repo : FS.GG.Governance.GateRun.Plan.ProviderCommandContext = {ProviderPlanningTests.context with RepoRoot=repo}

/// Drives the real adapter; the supplier hands back the caller-owned session and never loses it.
let private runAdapter supply argv =
    let supplied=ResizeArray<Loop.RunRequest * ProviderContext.Selection>()
    let messages=ResizeArray<string>()
    let built=ref 0
    let executed=ref false
    let reused=ref false
    let cap=Support.newCapture ()
    let ports=Support.fakePorts Support.validCatalog Support.gitSrcChange cap
    let ports={ports with Execute=(fun _ -> executed.Value <- true; failwith "provider planning cannot execute")
                          Store=(fun _ -> reused.Value <- true; failwith "provider planning cannot reuse")}
    let supplier request selection =
        supplied.Add((request,selection))
        supply request selection
    let code=Program.runWithCapturedProvider supplier (fun _ -> built.Value <- built.Value+1; ports) messages.Add argv
    { Code=code; Messages=List.ofSeq messages; PortsBuilt=built.Value; Supplied=List.ofSeq supplied
      Executed=executed.Value; Reused=reused.Value; Writes=cap.Writes.Length }

let private refusedBeforePorts (run:AdapterRun) fragment =
    Expect.equal run.Code (Loop.exitCode Loop.InputUnavailable) "input unavailable"
    Expect.equal run.PortsBuilt 0 "real ports stay unbuilt after an adapter refusal"
    Expect.isFalse (run.Executed || run.Reused) "no execution or cache reuse"
    Expect.equal run.Writes 0 "no artifact"
    Expect.isTrue (run.Messages |> List.exists(fun m -> m.Contains "input-unavailable" && m.Contains(fragment:string))) (sprintf "reported cause '%s' in %A" fragment run.Messages)

[<Tests>]
let capturedProviderAdapterTests =
    testList "Program.runWithCapturedProvider over a ready controlled capture" [
        testCase "bound ready session reaches nonpassing provider planning and is left to its caller" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let selection=loaderSelection request
            let session=readySession fixture request
            let before=inspect session
            let closesBefore=fixture.CloseAttempts
            let run=runAdapter (fun _ _ -> Ok(session,boundContext "/repo")) (adapterArgv "/repo" selection)
            Expect.equal run.PortsBuilt 1 "ports are built only after the declaration was admitted"
            Expect.isFalse (run.Messages |> List.exists(fun m -> m.Contains "provider command context" || m.Contains "provider declaration admission")) (sprintf "no adapter refusal: %A" run.Messages)
            Expect.isTrue (run.Code=1 || run.Code=3) "planning stays nonpassing"
            Expect.isFalse (run.Executed || run.Reused) "no execution or legacy reuse"
            Expect.equal run.Writes 0 "no passing artifact"
            let (suppliedRequest,suppliedSelection)=List.exactlyOne run.Supplied
            Expect.equal suppliedSelection selection "every selector reaches the supplier unchanged"
            Expect.equal suppliedRequest.Repo "/repo" "supplier sees the selected repository"
            let after=inspect session
            Expect.equal after.Model before.Model "adapter neither revalidated nor retired the session"
            Expect.equal after.Settlement Retained "adapter never releases the caller's session"
            Expect.equal fixture.CloseAttempts closesBefore "adapter closes nothing"
            releaseControlled session)
        testCase "supplier refusal is reported verbatim and nothing else starts" (fun () ->
            let selection=loaderSelection request
            let run=runAdapter (fun _ _ -> Error "no admitted outer owner") (adapterArgv "/repo" selection)
            refusedBeforePorts run "no admitted outer owner")
        testCase "command context for another repository cannot be paired with a captured root" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let selection=loaderSelection request
            let session=readySession fixture request
            let run=runAdapter (fun _ _ -> Ok(session,boundContext "/synthetic")) (adapterArgv "/repo" selection)
            refusedBeforePorts run "provider command context does not bind the original captured repository root"
            releaseControlled session)
        testCase "selected repository that is not an original captured root is refused even with a matching context" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let selection=loaderSelection request
            let session=readySession fixture request
            let run=runAdapter (fun _ _ -> Ok(session,boundContext "/other")) (adapterArgv "/other" selection)
            refusedBeforePorts run "provider command context does not bind the original captured repository root"
            releaseControlled session)
        testCase "root whose current binding is not freshly confirmed is refused by the adapter itself" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let selection=loaderSelection request
            let session=readySession fixture request
            fixture.ArmMetadataGate(fun path -> path = "/repo")
            let revalidating=Threading.Tasks.Task.Run(fun () -> revalidate session)
            try
                fixture.WaitMetadataBlocked()
                Expect.isTrue ((inspect session).Roots |> List.forall(fun root -> root.CurrentBindingMatches=None)) "bindings pending in the revalidation window"
                let run=runAdapter (fun _ _ -> Ok(session,boundContext "/repo")) (adapterArgv "/repo" selection)
                refusedBeforePorts run "provider command context does not bind the original captured repository root"
            finally
                fixture.ResumeMetadata()
                joined revalidating |> ignore
            releaseControlled session)
        testCase "loader refusal of the supplied session is reported and nothing else starts" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let selection=loaderSelection request
            let session=readySession fixture request
            fixture.ChangeBinding()
            Expect.equal (revalidate session).Model.FirstFailure (Some(InputChanged "provenance")) "supplied session lost readiness"
            let run=runAdapter (fun _ _ -> Ok(session,boundContext "/repo")) (adapterArgv "/repo" selection)
            refusedBeforePorts run "provider declaration admission rejected"
            Expect.isTrue (run.Messages |> List.exists(fun m -> m.Contains "$.capture")) "located loader diagnostic retained"
            releaseControlled session)
        testCase "selected policy that differs from the captured policy is refused by the loader through the adapter" (fun () ->
            use fixture = newLoaderFixture ()
            let request=loaderRequest fixture
            let selection=loaderSelection request
            let session=readySession fixture request
            let run=runAdapter (fun _ _ -> Ok(session,boundContext "/repo")) (adapterArgv "/repo" {selection with PolicySha256=String('0',64)})
            refusedBeforePorts run "provider declaration admission rejected"
            Expect.isTrue (run.Messages |> List.exists(fun m -> m.Contains "$.capture.documents")) "captured-document selection refusal retained"
            releaseControlled session)
    ] |> testSequenced

// Verify host composition root (#468). The host is the outer capture-lifetime owner: it prepares and captures the
// single session, keeps it through planning, is the only caller of `release`, and never disposes or replaces an
// unsettled session. These controls drive the real host over the real capture interpreter with finite synthetic
// descriptors and gates (no kernel acquisition). The 20 physical cases stay pending: nothing here is a real
// blocked regular-file read, an observed uncertain close or demonstrated whole-worker teardown.
let private admittedEnv = [ ProviderHost.workSecondsVariable, "9"; ProviderHost.cleanupSecondsVariable, "19" ]

type private HostFixtureRun =
    { Code: int option
      Messages: string list
      Sessions: CaptureSession list
      Prepares: int
      CancellationReads: int
      PortsBuilt: int
      /// Phase and settlement of every prepared session at the instant real ports were built (planning start).
      AtPlanning: (Phase * Settlement) list
      Executed: bool
      Pauses: int }

exception private ResidentObserved

type private HostFault =
    | NoFault
    /// Planning port construction throws (inside the interpreter's containment).
    | PortsFault
    /// The host's own planning-context lookup throws after capture, outside any interpreter containment.
    | SupplyFault
    /// The clock throws once on the capturing thread while the capture activity is already running.
    | CaptureCallerFault

/// `onSettleNow` runs on the second host clock read of an invocation whose capture failed: the read `settle`
/// makes right after a refused release, so it can finish the pending activity inside that window.
let private hostEffects (fixture: ControlledFixture) env (fault: HostFault) (onSettleNow: unit -> unit) (cancellation: Threading.CancellationToken) (prepares: int ref) (cancellationReads: int ref) (sessions: ResizeArray<CaptureSession>) (pause: int -> unit) : ProviderHost.Effects =
    let pauses = ref 0
    let nowReads = ref 0
    let callerThread = Threading.Thread.CurrentThread.ManagedThreadId
    let armed = ref false
    let operations =
        { fixture.Operations with
            Current = fun () ->
                if armed.Value && Threading.Thread.CurrentThread.ManagedThreadId = callerThread then
                    armed.Value <- false
                    failwith "synthetic clock fault on the capturing thread"
                else fixture.Operations.Current() }
    { Environment = fun name ->
          if fault = SupplyFault && name = "CI" then failwith "synthetic environment fault after capture"
          env |> List.tryFind (fun (key, _) -> key = name) |> Option.map snd
      Now = fun () ->
          nowReads.Value <- nowReads.Value + 1
          if nowReads.Value = 2 then onSettleNow ()
          fixture.Operations.Current()
      TicksPerSecond = 10L
      Cancellation = fun () -> cancellationReads.Value <- cancellationReads.Value + 1; cancellation
      Prepare = fun r ->
          prepares.Value <- prepares.Value + 1
          match prepareControlled operations r with
          | Ok session ->
              lock sessions (fun () -> sessions.Add session)
              armed.Value <- (fault = CaptureCallerFault)
              Ok session
          | Error failures -> Error failures
      Pause = fun () -> pauses.Value <- pauses.Value + 1; pause pauses.Value }

/// `swallowResident`: a pause hook may raise ResidentObserved to end a host that would otherwise stay resident.
let private hostRunWith (onSettleNow: unit -> unit) (sessions: ResizeArray<CaptureSession>) (fixture: ControlledFixture) env cancellation (pause: int -> unit) (fault: HostFault) argv =
    let messages = ResizeArray<string>()
    let atPlanning = ResizeArray<Phase * Settlement>()
    let prepares = ref 0
    let cancellationReads = ref 0
    let built = ref 0
    let executed = ref false
    let pauseCount = ref 0
    let cap = Support.newCapture ()
    let ports = Support.fakePorts Support.validCatalog Support.gitSrcChange cap
    // Provider planning must execute and reuse nothing; the legacy control keeps the ordinary fake ports.
    let providerArgv = argv |> Array.contains "--provider-provenance"
    let ports =
        if providerArgv then
            { ports with Execute = (fun _ -> executed.Value <- true; failwith "provider planning cannot execute")
                         Store = (fun _ -> executed.Value <- true; failwith "provider planning cannot reuse") }
        else ports
    let makePorts _ =
        built.Value <- built.Value + 1
        for session in sessions do
            let observed = inspect session
            atPlanning.Add((observed.Model.Phase, observed.Settlement))
        if fault = PortsFault then failwith "synthetic ports construction fault"
        ports
    let effects = hostEffects fixture env fault onSettleNow cancellation prepares cancellationReads sessions (fun k -> pauseCount.Value <- k; pause k)
    let code = try Some(Program.runHosted effects makePorts messages.Add argv) with ResidentObserved -> None
    { Code = code; Messages = List.ofSeq messages; Sessions = lock sessions (fun () -> List.ofSeq sessions); Prepares = prepares.Value
      CancellationReads = cancellationReads.Value; PortsBuilt = built.Value; AtPlanning = List.ofSeq atPlanning
      Executed = executed.Value; Pauses = pauseCount.Value }

let private hostRunIn sessions fixture env cancellation pause fault argv =
    hostRunWith ignore sessions fixture env cancellation pause fault argv

let private hostRun fixture env cancellation pause fault argv =
    hostRunIn (ResizeArray<CaptureSession>()) fixture env cancellation pause fault argv

let private unsettledReports (run: HostFixtureRun) =
    run.Messages |> List.filter (fun m -> m.Contains "is not released by the original cleanup end")

let private noPause (_: int) = ()

/// The hosted-argv for a fixture selection; paths are the fixture's synthetic ones.
let private hostArgv (selection: ProviderContext.Selection) = adapterArgv "/repo" selection

let private distinctClosed (fixture: ControlledFixture) =
    let closed = fixture.CloseAttempts
    Expect.isNonEmpty closed "the host released and thereby closed the retained descriptors"
    Expect.equal (List.distinct closed) closed "no descriptor was closed twice"

[<Tests>]
let hostAdmissionTests =
    let effects env (reads: int ref) : ProviderHost.Effects =
        { Environment = fun name -> env |> List.tryFind (fun (key, _) -> key = name) |> Option.map snd
          Now = fun () -> failwith "admission must not read the clock"
          TicksPerSecond = 10L
          Cancellation = fun () -> reads.Value <- reads.Value + 1; Threading.CancellationToken.None
          Prepare = fun _ -> failwith "admission must not prepare"
          Pause = fun () -> failwith "admission must not pause" }
    let admit env = let reads = ref 0 in ProviderHost.admit (effects env reads) now, reads.Value
    let refused env fragment =
        match admit env with
        | Error message, reads ->
            Expect.stringContains message fragment "refusal names the missing or inconsistent end"
            Expect.equal reads 0 "cancellation wiring is not evaluated when nothing is admitted"
        | Ok admission, _ -> failtestf "must not admit: %A" admission.Budget
    testList "ProviderHost admission of the outer work and cleanup ends" [
        testCase "valid ends are measured from host start and keep their order" (fun () ->
            use source = new Threading.CancellationTokenSource()
            let reads = ref 0
            let effects = { effects admittedEnv reads with Cancellation = fun () -> reads.Value <- reads.Value + 1; source.Token }
            match ProviderHost.admit effects now with
            | Ok admission ->
                Expect.equal admission.Budget.WorkEnd { now with Ticks = 100L } "work end is start plus admitted seconds"
                Expect.equal admission.Budget.CleanupEnd { now with Ticks = 200L } "cleanup end is start plus admitted seconds"
                Expect.equal admission.Cancellation source.Token "the invocation observes exactly the supplied cancellation"
                Expect.equal reads.Value 1 "cancellation is wired once"
            | Error message -> failtest message)
        testCase "cleanup equal to work is admitted (never reordered)" (fun () ->
            match admit [ ProviderHost.workSecondsVariable, "5"; ProviderHost.cleanupSecondsVariable, "5" ] with
            | Ok admission, _ -> Expect.equal admission.Budget.CleanupEnd admission.Budget.WorkEnd "equal ends"
            | Error message, _ -> failtest message)
        testCase "missing work end admits nothing" (fun () -> refused [ ProviderHost.cleanupSecondsVariable, "19" ] ProviderHost.workSecondsVariable)
        testCase "missing cleanup end admits nothing" (fun () -> refused [ ProviderHost.workSecondsVariable, "9" ] ProviderHost.cleanupSecondsVariable)
        testCase "non-positive, fractional, signed, padded, textual and oversized ends admit nothing" (fun () ->
            for bad in [ "0"; "-3"; "1.5"; "+4"; " 4"; "4 "; "abc"; ""; "1000001" ] do
                refused [ ProviderHost.workSecondsVariable, bad; ProviderHost.cleanupSecondsVariable, "19" ] ProviderHost.workSecondsVariable
                refused [ ProviderHost.workSecondsVariable, "9"; ProviderHost.cleanupSecondsVariable, bad ] ProviderHost.cleanupSecondsVariable)
        testCase "cleanup end before work end admits nothing" (fun () ->
            refused [ ProviderHost.workSecondsVariable, "9"; ProviderHost.cleanupSecondsVariable, "8" ] "precedes")
    ] |> testSequenced

[<Tests>]
let hostContextAndExitTests =
    let effects env : ProviderHost.Effects =
        { Environment = fun name -> env |> List.tryFind (fun (key, _) -> key = name) |> Option.map snd
          Now = fun () -> failwith "unused"
          TicksPerSecond = 10L
          Cancellation = fun () -> Threading.CancellationToken.None
          Prepare = fun _ -> failwith "unused"
          Pause = fun () -> failwith "unused" }
    let admission = { ProviderHost.Admission.Budget = { WorkEnd = { now with Ticks = 100L }; CleanupEnd = { now with Ticks = 200L } }
                      ProviderHost.Admission.Cancellation = Threading.CancellationToken.None }
    let timeout (context: FS.GG.Governance.GateRun.Plan.ProviderCommandContext) = let (FS.GG.Governance.Config.Model.TimeoutLimit seconds) = context.RemainingTimeout in seconds
    testList "ProviderHost planning context and defined exit mapping" [
        testCase "planning context carries the observed environment, an empty delta and whole seconds left in the work window" (fun () ->
            let ci = ProviderHost.commandContext (effects [ "CI", "true" ]) "/repo" admission { now with Ticks = 15L }
            Expect.equal ci.Environment FS.GG.Governance.Config.Model.Ci "CI set means a CI environment"
            Expect.equal ci.RepoRoot "/repo" "governed root"
            Expect.equal ci.EnvironmentDelta { Added = []; Changed = []; Removed = [] } "no ambient environment leaks into the plan"
            Expect.equal (timeout ci) 8 "85 ticks at 10 per second leave 8 whole seconds"
            Expect.equal (ProviderHost.commandContext (effects []) "/repo" admission now).Environment FS.GG.Governance.Config.Model.Local "unset CI is local"
            Expect.equal (ProviderHost.commandContext (effects [ "CI", "" ]) "/repo" admission now).Environment FS.GG.Governance.Config.Model.Local "empty CI is local")
        testCase "an exhausted work window leaves no timeout rather than a negative one" (fun () ->
            Expect.equal (timeout (ProviderHost.commandContext (effects []) "/repo" admission { now with Ticks = 100L })) 0 "at the work end"
            Expect.equal (timeout (ProviderHost.commandContext (effects []) "/repo" admission { now with Ticks = 500L })) 0 "past the work end")
        testCase "a released session keeps the invocation's own exit" (fun () ->
            for inner in [ 0; 1; 2; 3; 4 ] do
                Expect.equal (ProviderHost.exitCode inner ProviderHost.Released false) inner (sprintf "inner %d" inner))
        testCase "a passing verdict cannot stand on a session with a recorded failure" (fun () ->
            Expect.equal (ProviderHost.exitCode 0 ProviderHost.Released true) (Loop.exitCode Loop.ToolError) "pass becomes a tool error"
            for inner in [ 1; 3 ] do
                Expect.equal (ProviderHost.exitCode inner ProviderHost.Released true) inner "an already non-passing exit is not rewritten")
        testCase "every unsettled outcome is a tool error whatever the invocation decided" (fun () ->
            for settlement in [ ProviderHost.ReleasedAfterUnsettledReport; ProviderHost.RetainedWithoutPendingRead ] do
                for inner in [ 0; 1; 3 ] do
                    for failed in [ false; true ] do
                        Expect.equal (ProviderHost.exitCode inner settlement failed) 4 (sprintf "%A inner %d" settlement inner))
    ] |> testSequenced

[<Tests>]
let hostCompositionTests =
    testList "ProviderHost over a controlled capture" [
        testCase "settled: the host captures once, keeps the session through planning, releases it once and keeps the invocation's exit" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None noPause NoFault (hostArgv selection)
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Prepares 1 "exactly one owner is prepared; none replaces it"
            Expect.equal run.PortsBuilt 1 "planning started after the declaration was admitted"
            Expect.equal run.AtPlanning [ Ready, Retained ] "the session was ready and still held when planning started"
            Expect.isFalse (run.Messages |> List.exists (fun m -> m.Contains "tool-error" || m.Contains "provider capture" || m.Contains "provider declaration")) (sprintf "no host or capture refusal: %A" run.Messages)
            Expect.isFalse run.Executed "planning executes nothing"
            // The same declaration through the caller-owned adapter, released by its caller, decides the same exit.
            use baselineFixture = newLoaderFixture ()
            let baselineSession = readySession baselineFixture (loaderRequest baselineFixture)
            let baseline = runAdapter (fun _ _ -> Ok(baselineSession, boundContext "/repo")) (adapterArgv "/repo" selection)
            releaseControlled baselineSession
            Expect.isTrue (baseline.Code = 1 || baseline.Code = 3) "planning stays nonpassing"
            Expect.equal run.Code (Some baseline.Code) "the host does not alter the invocation's exit when the session settles"
            Expect.equal (inspect session).Settlement ResourcesReleased "the host released its own session"
            Expect.equal (ownership session).RetainedDescriptors 0 "no descriptor remains"
            distinctClosed fixture)
        testCase "legacy invocations never touch capture, admission or cancellation" (fun () ->
            use fixture = newLoaderFixture ()
            let run = hostRun fixture [] Threading.CancellationToken.None noPause NoFault [| "--repo"; "/repo"; "--paths"; "src/a.fs" |]
            Expect.equal run.Prepares 0 "no session prepared"
            Expect.equal run.CancellationReads 0 "no signal wiring for legacy mode"
            Expect.isEmpty run.Sessions "nothing to release"
            let baselinePorts = Support.fakePorts Support.validCatalog Support.gitSrcChange (Support.newCapture ())
            let baseline = Program.runWithProviderLoader (fun _ _ -> failtest "legacy mode must not load a provider") (fun _ -> baselinePorts) ignore [| "--repo"; "/repo"; "--paths"; "src/a.fs" |]
            Expect.equal run.Code (Some baseline) "the host leaves the legacy exit unchanged"
            Expect.isFalse (run.Messages |> List.exists (fun m -> m.Contains "tool-error")) (sprintf "no host-contained fault: %A" run.Messages))
        testCase "refused: unadmitted ends stop before any session exists" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let run = hostRun fixture [] Threading.CancellationToken.None noPause NoFault (hostArgv selection)
            Expect.equal run.Code (Some(Loop.exitCode Loop.InputUnavailable)) "input unavailable"
            Expect.equal run.Prepares 0 "nothing prepared without admitted ends"
            Expect.equal run.PortsBuilt 0 "no planning"
            Expect.isTrue (run.Messages |> List.exists (fun m -> m.Contains ProviderHost.workSecondsVariable)) (sprintf "missing admission named: %A" run.Messages))
        testCase "refused: an invalid capture request is reported and no session is held" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = { loaderSelection (loaderRequest fixture) with PolicyPath = "relative-policy.json" }
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None noPause NoFault (hostArgv selection)
            Expect.equal run.Code (Some(Loop.exitCode Loop.InputUnavailable)) "input unavailable"
            Expect.equal run.Prepares 1 "preparation was attempted once"
            Expect.isEmpty run.Sessions "a refused preparation owns nothing"
            Expect.equal run.PortsBuilt 0 "no planning"
            Expect.isTrue (run.Messages |> List.exists (fun m -> m.Contains "provider capture request refused")) (sprintf "cause reported: %A" run.Messages))
        testCase "refused: a capture that cannot reach ready keeps its cause, plans nothing and is still released by the host" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = { loaderSelection (loaderRequest fixture) with PolicySha256 = String('0', 64) }
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None noPause NoFault (hostArgv selection)
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some(Loop.exitCode Loop.InputUnavailable)) "input unavailable"
            Expect.equal run.PortsBuilt 0 "no planning over a failed capture"
            Expect.equal (inspect session).Model.FirstFailure (Some(RawDigestMismatch "policy")) "first cause retained"
            Expect.isTrue (run.Messages |> List.exists (fun m -> m.Contains "RawDigestMismatch")) (sprintf "cause reported: %A" run.Messages)
            Expect.equal (inspect session).Settlement ResourcesReleased "the failed owner was still released by the host"
            distinctClosed fixture)
        testCase "cancellation: a cancelled invocation is refused with its cause and released; the uncancelled control plans" (fun () ->
            use cancelled = new Threading.CancellationTokenSource()
            cancelled.Cancel()
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let run = hostRun fixture admittedEnv cancelled.Token noPause NoFault (hostArgv selection)
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some(Loop.exitCode Loop.InputUnavailable)) "cancelled capture is input unavailable"
            Expect.equal run.PortsBuilt 0 "no planning after cancellation"
            Expect.equal (inspect session).Model.FirstFailure (Some CancellationRequested) "the supplied cancellation reached the capture"
            Expect.equal (inspect session).Settlement ResourcesReleased "released after cancellation"
            use controlFixture = newLoaderFixture ()
            let control = hostRun controlFixture admittedEnv Threading.CancellationToken.None noPause NoFault (hostArgv selection)
            Expect.equal control.PortsBuilt 1 "same invocation without cancellation reaches planning")
        testCase "cancellation: with a read in flight the host waits for the read, then releases" (fun () ->
            use fixture = new ControlledFixture(1, false, provenanceDocument (), ProviderContextLoaderTests.coherentPolicyBytes)
            use cancelled = new Threading.CancellationTokenSource()
            let selection = loaderSelection (loaderRequest fixture)
            let host = Threading.Tasks.Task.Run(fun () -> hostRun fixture admittedEnv cancelled.Token noPause NoFault (hostArgv selection))
            try
                fixture.WaitBlocked()
                cancelled.Cancel()
                Threading.Thread.Sleep 50
                Expect.isFalse host.IsCompleted "capture cannot return while its read is still pending"
            finally fixture.Resume()
            let run = joined host
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some(Loop.exitCode Loop.InputUnavailable)) "cancelled"
            Expect.equal (inspect session).Model.FirstFailure (Some CancellationRequested) "cancellation, not the read result, decided"
            Expect.equal (inspect session).Settlement ResourcesReleased "released once the read returned"
            Expect.isEmpty (unsettledReports run) "an in-window release is not an unsettled outcome")
        testCase "deadline: a read that returns inside the window settles normally (control for the resident cases)" (fun () ->
            use fixture = new ControlledFixture(3, false)
            let host = Threading.Tasks.Task.Run(fun () -> hostRun fixture admittedEnv Threading.CancellationToken.None noPause NoFault (hostArgv fixture.Request.Selection))
            try
                fixture.WaitBlocked()
                Threading.Thread.Sleep 50
            finally fixture.Resume()
            let run = joined host
            let session = List.exactlyOne run.Sessions
            Expect.equal (inspect session).Model.FirstFailure None "no failure: the late read stayed inside the original ends"
            Expect.equal (inspect session).Settlement ResourcesReleased "released"
            Expect.isEmpty (unsettledReports run) "nothing unsettled"
            Expect.notEqual run.Code (Some 4) "not mapped as unsettled")
        testCase "deadline: past the original cleanup end a pending read keeps the session and the host resident, then exits non-zero" (fun () ->
            use fixture = new ControlledFixture(3, false)
            use paused = new Threading.ManualResetEventSlim(false)
            let pauseCount = ref 0
            let sessions = ResizeArray<CaptureSession>()
            let host =
                Threading.Tasks.Task.Run(fun () ->
                    hostRunIn sessions fixture admittedEnv Threading.CancellationToken.None (fun k -> pauseCount.Value <- k; if k >= 3 then paused.Set()) NoFault (hostArgv fixture.Request.Selection))
            try
                    fixture.WaitBlocked()
                    fixture.Advance 100L
                    Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> lock sessions (fun () -> Seq.toList sessions) |> List.exists(fun s -> (inspect s).Model.FirstFailure = Some WorkDeadlineReached)), TimeSpan.FromSeconds 2.)) "original work end observed by the capture"
                    fixture.Advance 200L
                    Expect.isTrue (paused.Wait(TimeSpan.FromSeconds 4.)) "host is in its bounded wait for release"
                    Expect.isFalse host.IsCompleted "the host does not exit while a read may be pending"
                    Threading.Thread.Sleep 50
                    Expect.isFalse host.IsCompleted "and stays resident past the cleanup end"
            finally fixture.Resume()
            let run = joined host
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some 4) "defined exit for an unsettled session"
            Expect.equal (unsettledReports run).Length 1 (sprintf "reported exactly once, however long it waited: %A" run.Messages)
            Expect.isGreaterThanOrEqual run.Pauses 3 "the host really waited"
            Expect.equal run.PortsBuilt 0 "no planning over a capture that missed its ends"
            Expect.equal (inspect session).Model.FirstFailure (Some WorkDeadlineReached) "first cause retained"
            Expect.equal (inspect session).Settlement Retained "never released after the original cleanup end"
            Expect.isGreaterThan (ownership session).RetainedDescriptors 0 "descriptors still owned by the same session"
            Expect.isEmpty fixture.CloseAttempts "the host never closed, disposed or replaced anything around the unsettled session"
            Expect.equal run.Prepares 1 "no replacement owner"
            Expect.isTrue (run.Messages |> List.exists (fun m -> m.Contains "no read pending")) (sprintf "exit reason reported: %A" run.Messages))
        testCase "uncertain close: the host stays resident past the cleanup end, reports once and never retries the close" (fun () ->
            use fixture = new ControlledFixture(0, true)
            let beforeEnd = ref []
            let pause k =
                if k = 2 then
                    beforeEnd.Value <- [ fixture.Operations.Current().Ticks ]
                    fixture.Advance 200L
                if k >= 6 then raise ResidentObserved
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None pause NoFault (hostArgv fixture.Request.Selection)
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code None "the host had not returned: it is still resident"
            Expect.equal beforeEnd.Value [ 10L ] "waited inside the window first"
            Expect.equal (unsettledReports run).Length 1 (sprintf "one report across %d waits: %A" run.Pauses run.Messages)
            Expect.equal fixture.CloseAttempts.Length 1 "the uncertain descriptor was closed once and never retried"
            Expect.isTrue (ownership session).CloseUncertain "uncertainty is sticky"
            Expect.equal (inspect session).Settlement Retained "no false settlement"
            Expect.equal run.Prepares 1 "no replacement owner")
        testCase "an unsettled wait inside the window is silent: no report before the original cleanup end" (fun () ->
            use fixture = new ControlledFixture(0, true)
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None (fun k -> if k >= 4 then raise ResidentObserved) NoFault (hostArgv fixture.Request.Selection)
            Expect.equal run.Code None "still waiting"
            Expect.isEmpty (unsettledReports run) "the cleanup end has not been reached"
            Expect.equal (inspect (List.exactlyOne run.Sessions)).Settlement Retained "still held")
        testCase "a fault in planning ports still ends in the host's release and a tool error" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None noPause PortsFault (hostArgv selection)
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some(Loop.exitCode Loop.ToolError)) "tool error"
            Expect.isTrue (run.Messages |> List.exists (fun m -> m.Contains "synthetic ports construction fault")) (sprintf "fault reported: %A" run.Messages)
            Expect.equal (inspect session).Settlement ResourcesReleased "the exception did not strand the owner"
            distinctClosed fixture)
        testCase "a fault in the host's own supply after capture is contained and the session is still released" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None noPause SupplyFault (hostArgv selection)
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some(Loop.exitCode Loop.ToolError)) "tool error"
            Expect.equal run.PortsBuilt 0 "no planning after the fault"
            Expect.isTrue (run.Messages |> List.exists (fun m -> m.Contains "synthetic environment fault after capture")) (sprintf "fault reported: %A" run.Messages)
            Expect.equal (inspect session).Settlement ResourcesReleased "a ready session raised past the host is still released"
            distinctClosed fixture)
        testCase "a fault on the capturing thread leaves the owner held by the host, which still settles it" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let run = hostRun fixture admittedEnv Threading.CancellationToken.None noPause CaptureCallerFault (hostArgv selection)
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some(Loop.exitCode Loop.ToolError)) "tool error"
            Expect.isTrue (run.Messages |> List.exists (fun m -> m.Contains "synthetic clock fault on the capturing thread")) (sprintf "fault reported: %A" run.Messages)
            Expect.equal run.Prepares 1 "no replacement owner"
            Expect.equal (inspect session).Settlement ResourcesReleased "the owner retained before acquisition was settled by the host"
            distinctClosed fixture)
        testCase "a passing verdict is refused on a session that recorded a failure; the same verdict stands on a clean one" (fun () ->
            let attempt (drift: bool) =
                use fixture = newLoaderFixture ()
                let selection = loaderSelection (loaderRequest fixture)
                let sessions = ResizeArray<CaptureSession>()
                let prepares = ref 0
                let reads = ref 0
                let messages = ResizeArray<string>()
                let effects = hostEffects fixture admittedEnv NoFault ignore Threading.CancellationToken.None prepares reads sessions noPause
                let invocationRequest =
                    match Loop.parseInvocation (List.ofArray (hostArgv selection)) with
                    | Ok invocation -> invocation.Request
                    | Error error -> failtestf "argv rejected: %A" error
                let drive supply =
                    match supply invocationRequest selection with
                    | Ok(session, _) ->
                        if drift then
                            fixture.ChangeBinding()
                            Expect.equal (revalidate session).Model.FirstFailure (Some(InputChanged "provenance")) "failure recorded on the held session"
                        0
                    | Error message -> failtestf "capture refused: %s" message
                let code = ProviderHost.run effects messages.Add drive
                Expect.equal (inspect (List.exactlyOne (List.ofSeq sessions))).Settlement ResourcesReleased "released either way"
                code
            Expect.equal (attempt false) 0 "clean session: the invocation's pass stands"
            Expect.equal (attempt true) (Loop.exitCode Loop.ToolError) "recorded failure: pass becomes a tool error")
        testCase "deadline: a held call that returns without a descriptor after the cleanup end lets the host release late, and the exit is still a tool error" (fun () ->
            use fixture = new ControlledFixture(0, false)
            fixture.ArmOpenFailureGate()
            use paused = new Threading.ManualResetEventSlim(false)
            use proceed = new Threading.ManualResetEventSlim(false)
            let sessions = ResizeArray<CaptureSession>()
            // From the third wait on, the host is held in its pause until the test has let the held call finish,
            // so the next release attempt deterministically sees a settled owner through the ordinary loop.
            let pause k = if k >= 3 then (paused.Set(); proceed.Wait(TimeSpan.FromSeconds 5.) |> ignore)
            let host =
                Threading.Tasks.Task.Run(fun () ->
                    hostRunIn sessions fixture admittedEnv Threading.CancellationToken.None pause NoFault (hostArgv fixture.Request.Selection))
            try
                fixture.WaitOpenBlocked()
                fixture.Advance 100L
                Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> lock sessions (fun () -> Seq.toList sessions) |> List.exists(fun s -> (inspect s).Model.FirstFailure = Some WorkDeadlineReached)), TimeSpan.FromSeconds 2.)) "original work end observed by the capture"
                fixture.Advance 200L
                Expect.isTrue (paused.Wait(TimeSpan.FromSeconds 4.)) "host is in its bounded wait for release"
                Expect.isFalse host.IsCompleted "the host does not exit while the held call may still be pending"
                let session = lock sessions (fun () -> List.head (Seq.toList sessions))
                Expect.isTrue (ownership session).PendingActivity "the held call is still pending"
                fixture.ResumeOpen()
                Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> not (ownership session).PendingActivity), TimeSpan.FromSeconds 3.)) "held call returned"
            finally
                fixture.ResumeOpen()
                proceed.Set()
            let run = joined host
            let session = List.exactlyOne run.Sessions
            Expect.equal run.Code (Some 4) "released only after it was reported unsettled: still a tool error, not the invocation's own exit"
            Expect.equal (unsettledReports run).Length 1 (sprintf "reported exactly once: %A" run.Messages)
            Expect.equal (inspect session).Settlement ResourcesReleased "the same owner was released once the held call returned"
            Expect.isEmpty fixture.CloseAttempts "the failed call left no descriptor to close"
            Expect.equal (inspect session).Model.FirstFailure (Some WorkDeadlineReached) "first cause retained"
            Expect.isFalse (run.Messages |> List.exists (fun m -> m.Contains "no read pending")) (sprintf "not reported as retained: %A" run.Messages))
        testCase "a release that races the last owned activity finishing is not reported as retained" (fun () ->
            use fixture = new ControlledFixture(0, false)
            fixture.ArmOpenFailureGate()
            let sessions = ResizeArray<CaptureSession>()
            // Runs between the refused release and the pending-read check: the activity finishes in exactly that window.
            let finishActivity () =
                fixture.ResumeOpen()
                let session = lock sessions (fun () -> List.head (Seq.toList sessions))
                Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> not (ownership session).PendingActivity), TimeSpan.FromSeconds 3.)) "owned activity finished inside the window"
            let host =
                Threading.Tasks.Task.Run(fun () ->
                    hostRunWith finishActivity sessions fixture admittedEnv Threading.CancellationToken.None noPause NoFault (hostArgv fixture.Request.Selection))
            try
                fixture.WaitOpenBlocked()
                fixture.Advance 100L
                Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> lock sessions (fun () -> Seq.toList sessions) |> List.exists(fun s -> (inspect s).Model.FirstFailure = Some WorkDeadlineReached)), TimeSpan.FromSeconds 2.)) "original work end observed by the capture"
                fixture.Advance 200L
                let run = joined host
                let session = List.exactlyOne run.Sessions
                Expect.equal (inspect session).Settlement ResourcesReleased "the session did settle"
                Expect.equal run.Code (Some(Loop.exitCode Loop.InputUnavailable)) "so the invocation's own exit stands"
                Expect.isEmpty (unsettledReports run) "nothing was ever unsettled"
                Expect.isFalse (run.Messages |> List.exists (fun m -> m.Contains "retained")) (sprintf "no false retained report: %A" run.Messages)
            finally fixture.ResumeOpen())
        testCase "a second supply call cannot replace the held owner" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let sessions = ResizeArray<CaptureSession>()
            let prepares = ref 0
            let reads = ref 0
            let messages = ResizeArray<string>()
            let effects = hostEffects fixture admittedEnv NoFault ignore Threading.CancellationToken.None prepares reads sessions noPause
            let invocationRequest =
                match Loop.parseInvocation (List.ofArray (hostArgv selection)) with
                | Ok invocation -> invocation.Request
                | Error error -> failtestf "argv rejected: %A" error
            let drive supply =
                match supply invocationRequest selection with
                | Ok _ ->
                    match supply invocationRequest selection with
                    | Error message ->
                        Expect.stringContains message "already held" "second owner refused with its cause"
                        3
                    | Ok _ -> failtest "a second owner was prepared"
                | Error message -> failtestf "first capture refused: %s" message
            Expect.equal (ProviderHost.run effects messages.Add drive) 3 "invocation exit unchanged"
            Expect.equal prepares.Value 1 "exactly one owner was ever prepared"
            Expect.equal (inspect (List.exactlyOne (List.ofSeq sessions))).Settlement ResourcesReleased "the one owner was released")
        testCase "only provider-mode faults are contained: a legacy fault propagates and a fault before any session is a tool error" (fun () ->
            use fixture = newLoaderFixture ()
            let selection = loaderSelection (loaderRequest fixture)
            let sessions = ResizeArray<CaptureSession>()
            let messages = ResizeArray<string>()
            let effects = hostEffects fixture admittedEnv NoFault ignore Threading.CancellationToken.None (ref 0) (ref 0) sessions noPause
            Expect.throws (fun () -> ProviderHost.run effects messages.Add (fun _ -> failwith "legacy fault") |> ignore) "a fault that never asked for a capture is not rewritten"
            Expect.isEmpty messages "and nothing is reported on its behalf"
            let invocationRequest =
                match Loop.parseInvocation (List.ofArray (hostArgv selection)) with
                | Ok invocation -> invocation.Request
                | Error error -> failtestf "argv rejected: %A" error
            let throwingPrepare = { effects with Prepare = fun _ -> failwith "synthetic prepare fault" }
            let code = ProviderHost.run throwingPrepare messages.Add (fun supply -> supply invocationRequest selection |> ignore; 0)
            Expect.equal code (Loop.exitCode Loop.ToolError) "provider-mode fault with no session is a tool error"
            Expect.isTrue (messages |> Seq.exists (fun m -> m.Contains "synthetic prepare fault")) (sprintf "reported: %A" (List.ofSeq messages))
            Expect.isEmpty sessions "no owner was ever created")
    ] |> testSequenced

module private SignalSupport =
    [<Runtime.InteropServices.DllImport("libc", SetLastError = true)>]
    extern int kill(int pid, int signal)

[<Tests>]
let hostSignalTests =
    testList "ProviderHost signal-driven cancellation" [
        testCase "SIGTERM requests cancellation even after a full collection, and the registration is released on dispose" (fun () ->
            if not (OperatingSystem.IsLinux()) then skiptest "Linux process signals only"
            let signals = new ProviderHost.SignalCancellation()
            let token = signals.Token
            Expect.isFalse token.IsCancellationRequested "nothing requested yet"
            // An unreferenced PosixSignalRegistration is finalized and silently unregisters its handler.
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            Expect.equal (SignalSupport.kill(Environment.ProcessId, 15)) 0 "signal delivered to this process"
            Expect.isTrue (Threading.SpinWait.SpinUntil((fun () -> token.IsCancellationRequested), TimeSpan.FromSeconds 5.)) "the handler survived collection and requested cancellation"
            GC.KeepAlive signals
            (signals :> IDisposable).Dispose())
    ] |> testSequenced
