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
let actualCases : (string * (unit -> unit)) list =
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
[<Tests>]
let nativeTests =
    testList "ProviderContextCapture Actual" (actualCases |> List.map(fun (name,run) ->
        if nativeEnabled then testCase name run else ptestCase name run))
