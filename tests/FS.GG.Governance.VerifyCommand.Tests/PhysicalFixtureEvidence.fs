/// GOV-423 prerequisite (b) physical fixture, part 2: the evidence record and the pure judges.
/// The judges decide whether a run counts as an authentic blocked regular-file read, an uncertain
/// physical close, or a qualified teardown. They read only recorded facts, so they run inert in
/// default CI against synthetic and mutated evidence; the physical cases feed them kernel-produced
/// evidence recorded by independent processes. A judge never trusts the production result alone:
/// every acceptance joins it to the daemon log or device-mapper events and to the observer samples.
module FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureEvidence

open System
open System.Text.Json

[<CLIMutable>]
type DaemonEvent =
    { DaemonAtNs: int64
      DaemonWall: string
      Event: string
      Op: string
      Unique: uint64
      DaemonNode: uint64
      /// Requesting process id as the kernel reports it in the request header.
      RequesterPid: int
      /// Positive errno carried in a reply; 0 for success or for events that carry none.
      ReplyErrno: int
      DaemonDetail: string }

[<CLIMutable>]
type Sample =
    { SampleAtNs: int64
      Tid: int
      State: string
      Wchan: string
      Stack: string }

[<CLIMutable>]
type DmEvent =
    { DmAtNs: int64
      DmAction: string
      DmExit: int
      DmDetail: string }

[<CLIMutable>]
type ReaderResult =
    { ReaderPid: int
      WorkEndNs: int64
      CleanupEndNs: int64
      CaptureStartNs: int64
      CaptureReturnedNs: int64
      Phase: string
      FirstFailure: string
      SecondaryFailures: string[]
      Settlement: string
      DocumentCount: int
      PendingActivity: bool
      RetainedDescriptors: int
      CloseUncertain: bool
      /// Result of the public `release` called right after capture returned.
      ReleaseAfterReturn: string
      /// State after the driver released the injected cause and the reader settled.
      FinalRelease: string
      FinalPendingActivity: bool
      /// The production model as the interpreter printed it, kept for the human reader of the evidence.
      ModelDetail: string }

[<CLIMutable>]
type Step =
    { StepAtNs: int64
      StepName: string
      StepOk: bool
      StepDetail: string }

[<CLIMutable>]
type Census =
    { DStateTasks: string[]
      FixtureProcesses: string[]
      FixtureMounts: string[]
      DmDevices: string[]
      LoopDevices: string[]
      /// FUSE connection ids present now and before the fixture started.
      FuseConnections: string[]
      FuseConnectionsBaseline: string[]
      /// Fixture dm devices with I/O in flight (from /sys/block/dm-*/inflight); a parked bio is still a survivor.
      InflightIo: string[] }

[<CLIMutable>]
type Evidence =
    { Schema: string
      Scenario: string
      /// Commit and tree of the checkout under test, and whether it had uncommitted changes.
      Head: string
      Tree: string
      Dirty: bool
      /// "fuse", "block" or "plain".
      Backend: string
      InjectedCause: string
      /// Always false: this is a contained kernel-level fixture, never a field incident.
      FieldIncident: bool
      Kernel: string
      /// File type reported by statx (via coreutils stat) for the selected provenance file.
      FileType: string
      /// Whether the fixture applied its block (false for the controls and the normal file).
      BlockInjected: bool
      Daemon: DaemonEvent[]
      Observer: Sample[]
      ObserverEndNs: int64
      Dm: DmEvent[]
      Reader: ReaderResult
      Teardown: Step[]
      CensusBefore: Census
      CensusInside: Census }

let schema = "fsgg.gov423b.physical-fixture/v1"

/// The fixed, scenario-specific statement of the injected cause. A judge accepts only these exact texts,
/// so a text such as "not injected" cannot pass and a run cannot rename its own cause.
let causeFor (scenario: string) : string option =
    match scenario with
    | "normal-file" -> Some "no fault injected: ordinary regular file in a namespace-private scratch directory (control for the production path)"
    | "fuse-blocked-read" ->
        Some "injected: FUSE daemon receives the READ for a regular file and withholds the reply until the driver releases it; synthetic kernel-level fixture, not a field incident"
    | "fuse-uncertain-close" ->
        Some "injected: FUSE daemon withholds the READ reply past the work deadline, releases it inside the cleanup window, then answers FLUSH with EIO so the kernel close() fails; synthetic kernel-level fixture, not a field incident"
    | "fuse-control-no-block" ->
        Some "no fault injected: the same FUSE daemon answers READ immediately and FLUSH with success (control: the fixture must not pass without the block)"
    | "block-blocked-read" | "block-abnormal-exit" ->
        Some "injected: the device-mapper device under a read-only ext4 mount is suspended before the first uncached data read, so the read sleeps in the block layer (killable D state) until the table is swapped to error; synthetic kernel-level fixture, not a field incident"
    | "block-control-no-block" ->
        Some "no fault injected: the same loop + dm-delay + ext4 stack is left running (control: the fixture must not pass without the block)"
    | _ -> None

let private isHex40 (value: string) = not (String.IsNullOrEmpty value) && value.Length = 40 && value |> Seq.forall Uri.IsHexDigit

let private options = JsonSerializerOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)
let toJson (value: 'a) = JsonSerializer.Serialize(value, options)
let ofJson<'a when 'a: not null> (text: string) : 'a =
    match JsonSerializer.Deserialize<'a>(text, options) with
    | null -> failwith "JSON document deserialized to null"
    | value -> value

type Verdict =
    | Accepted
    | Rejected of reasons: string list

let isAccepted verdict = match verdict with Accepted -> true | Rejected _ -> false

let private conclude (checks: (bool * string) list) =
    match [ for ok, reason in checks do if not ok then yield reason ] with
    | [] -> Accepted
    | reasons -> Rejected reasons

let private nanos seconds = int64 (seconds * 1_000_000_000.0)

/// Whether a sample shows a task parked on the injected cause for the given backend.
let parkedOnCause (backend: string) (sample: Sample) =
    let text = (sample.Wchan + " " + sample.Stack).ToLowerInvariant()
    match backend with
    | "fuse" ->
        (sample.State = "S" || sample.State = "D")
        && (text.Contains "request_wait_answer" || text.Contains "fuse")
    | "block" ->
        // D state whose kernel stack shows the sleep is inside read(2). A park in another syscall (for
        // example an openat2 lookup after cache eviction) or an unreadable stack proves nothing.
        sample.State = "D" && (text.Contains "ksys_read" || text.Contains "vfs_read")
    | _ -> false

/// Maximal [start, end) spans, per thread, in which the sample predicate holds.
/// A span still open at the last observation ends at `observerEnd`.
let parkedSpans (backend: string) (observerEnd: int64) (samples: Sample[]) : (int * int64 * int64) list =
    samples
    |> Array.groupBy (fun sample -> sample.Tid)
    |> Array.toList
    |> List.collect (fun (tid, group) ->
        let ordered = group |> Array.sortBy (fun sample -> sample.SampleAtNs)
        let mutable openAt: int64 option = None
        let spans = ResizeArray<int * int64 * int64>()
        for sample in ordered do
            match openAt, parkedOnCause backend sample with
            | None, true -> openAt <- Some sample.SampleAtNs
            | Some start, false ->
                spans.Add((tid, start, sample.SampleAtNs))
                openAt <- None
            | _ -> ()
        match openAt with
        | Some start -> spans.Add((tid, start, observerEnd))
        | None -> ()
        List.ofSeq spans)

/// The observer emits a heartbeat (state "HB") every 250 ms. A park only counts when heartbeats cover the
/// whole span with no gap above one second, so a stalled observer cannot look like a sustained park.
let heartbeatCovers (samples: Sample[]) (start: int64) (finish: int64) : bool =
    let beats =
        samples
        |> Array.filter (fun sample -> sample.State = "HB" && sample.SampleAtNs >= start && sample.SampleAtNs <= finish)
        |> Array.map (fun sample -> sample.SampleAtNs)
    Array.concat [ [| start |]; beats; [| finish |] ]
    |> Array.sort
    |> Array.pairwise
    |> Array.forall (fun (a, b) -> b - a <= 1_000_000_000L)

/// The longest uninterrupted park any observed thread had on the cause. Ordinary FUSE round trips
/// (FLUSH, RELEASE) park for microseconds; an injected block parks for the whole work window.
let longestParkNs (backend: string) (observerEnd: int64) (samples: Sample[]) : int64 =
    parkedSpans backend observerEnd samples |> List.fold (fun longest (_, start, finish) -> max longest (finish - start)) 0L

let private startsWith (prefix: string) (value: string) = not (String.IsNullOrEmpty value) && value.StartsWith(prefix, StringComparison.Ordinal)
let private failureNamed name (reader: ReaderResult) =
    startsWith name reader.FirstFailure || (reader.SecondaryFailures |> Array.exists (startsWith name))

/// The kernel stamps each FUSE request with the requesting task's id (a thread id), so the join to the
/// production reader is: the requester is a thread the separate observer saw in the reader's task directory.
let private fromReader (evidence: Evidence) (event: DaemonEvent) =
    event.RequesterPid = evidence.Reader.ReaderPid
    || evidence.Observer |> Array.exists (fun sample -> sample.Tid <> 0 && sample.Tid = event.RequesterPid)

let private daemonRequest (op: string) (evidence: Evidence) =
    evidence.Daemon |> Array.tryFind (fun event -> event.Event = "request" && event.Op = op && fromReader evidence event)

let private daemonRead (evidence: Evidence) = daemonRequest "READ" evidence

let private daemonReplyTo (unique: uint64) (evidence: Evidence) =
    evidence.Daemon |> Array.tryFind (fun event -> event.Event = "reply" && event.Unique = unique)

/// Only the fixed cause of an injecting scenario counts; controls state "no fault injected".
let private causeStated (evidence: Evidence) =
    not evidence.FieldIncident
    && causeFor evidence.Scenario = Some evidence.InjectedCause
    && evidence.InjectedCause.StartsWith("injected:", StringComparison.Ordinal)

let private identified (evidence: Evidence) = isHex40 evidence.Head && isHex40 evidence.Tree && not evidence.Dirty

/// Authentic blocked regular-file read: the production path reached `read` on a file that statx reports
/// as a regular file, the cause was injected and is stated, the kernel parked the reading task on it for
/// the whole work window, and the production result is the classified WorkDeadlineReached outcome.
let judgeBlockedRead (evidence: Evidence) : Verdict =
    let reader = evidence.Reader
    let spans = parkedSpans evidence.Backend evidence.ObserverEndNs evidence.Observer
    let oneSecond = nanos 1.0
    let readThread = daemonRead evidence |> Option.map (fun request -> request.RequesterPid)
    // For FUSE the parked thread must be the very thread whose READ the daemon holds.
    let onRequestingThread tid = evidence.Backend <> "fuse" || readThread = Some tid
    let coversWorkEnd =
        spans
        |> List.exists (fun (tid, start, finish) ->
            onRequestingThread tid
            && start <= reader.WorkEndNs - oneSecond
            && finish >= reader.WorkEndNs
            && heartbeatCovers evidence.Observer start finish)
    let read = daemonRead evidence
    let opened = (daemonRequest "OPEN" evidence).IsSome
    let causeJoin =
        match evidence.Backend with
        | "fuse" ->
            match read with
            | None -> false, "daemon log has no READ from the production reader"
            | Some request ->
                let unanswered =
                    match daemonReplyTo request.Unique evidence with
                    | None -> true
                    | Some reply -> reply.DaemonAtNs > reader.WorkEndNs
                request.DaemonAtNs < reader.WorkEndNs && unanswered && opened,
                "daemon did not receive the READ before work end and leave it unanswered, or the OPEN is missing"
        | "block" ->
            let suspended = evidence.Dm |> Array.tryFind (fun event -> event.DmAction = "suspend" && event.DmExit = 0)
            let released = evidence.Dm |> Array.tryFind (fun event -> event.DmAction = "release" )
            (match suspended with
             | Some s -> s.DmAtNs < reader.CaptureStartNs && (match released with Some r -> r.DmAtNs > reader.WorkEndNs | None -> true)
             | None -> false),
            "device was not suspended before capture and held past work end"
        | _ -> false, "backend is not a blocking backend"
    conclude
        [ not evidence.FieldIncident, "evidence presents the fixture as a field incident"
          causeStated evidence, "injected cause is not stated explicitly"
          identified evidence, "evidence is not bound to a clean 40-hex commit and tree"
          evidence.BlockInjected, "no block was injected"
          evidence.FileType = "regular file", "statx did not report a regular file for the selected document"
          reader.FirstFailure = "WorkDeadlineReached",
          "production outcome is not the classified blocked-read result (WorkDeadlineReached)"
          reader.DocumentCount = 0, "production accepted a document despite the block"
          reader.Settlement = "Retained", "production claimed resources released while the read was blocked"
          coversWorkEnd, "observer did not see a task parked on the cause (inside read(2) for the block layer) across the work window with heartbeat coverage"
          fst causeJoin, snd causeJoin ]

/// Uncertain physical close: after the block was released the production close hit the injected FLUSH
/// error, production classified it as uncertain, and the FLUSH is independently logged as EIO.
let judgeUncertainClose (evidence: Evidence) : Verdict =
    let reader = evidence.Reader
    let flush = daemonRequest "FLUSH" evidence
    let flushedEio =
        match flush with
        | None -> false
        | Some request ->
            match daemonReplyTo request.Unique evidence with
            | Some reply -> reply.ReplyErrno = 5
            | None -> false
    let readReplyAt =
        match daemonRead evidence with
        | Some request -> daemonReplyTo request.Unique evidence |> Option.map (fun reply -> reply.DaemonAtNs)
        | None -> None
    let flushAfterRead =
        match flush, readReplyAt with
        | Some request, Some replied -> request.DaemonAtNs >= replied
        | _ -> false
    conclude
        [ evidence.Backend = "fuse", "uncertain close is produced by the FUSE FLUSH injection"
          causeStated evidence, "injected cause is not stated explicitly"
          evidence.BlockInjected, "no block was injected before the close"
          isAccepted (judgeBlockedRead evidence), "held READ not proven: the blocked-read judge rejects this run"
          evidence.FileType = "regular file", "statx did not report a regular file"
          flushedEio, "daemon log shows no FLUSH from the production reader answered with EIO"
          flushAfterRead, "FLUSH is not ordered after the released READ"
          failureNamed "OwnedActivitiesPending" reader, "production did not classify the close as OwnedActivitiesPending"
          reader.CloseUncertain, "production ownership does not record an uncertain close"
          reader.Settlement = "Retained", "production claimed settlement after an uncertain close"
          startsWith "Error" reader.ReleaseAfterReturn, "public release did not refuse after an uncertain close"
          not reader.PendingActivity, "capture returned with the activity still pending (close not reached)" ]

/// Qualified teardown: every ordered step succeeded and no task, mount, device or connection survives.
let judgeCensus (label: string) (before: Census) (after: Census) : Verdict =
    conclude
        [ Array.isEmpty after.DStateTasks, label + ": a D-state task survives"
          Array.isEmpty after.FixtureProcesses, label + ": a fixture process survives"
          Array.isEmpty after.FixtureMounts, label + ": a fixture mount survives"
          Array.isEmpty after.DmDevices, label + ": a dm device survives"
          Array.isEmpty after.LoopDevices, label + ": a loop device survives"
          (Array.sort after.FuseConnections = Array.sort before.FuseConnectionsBaseline), label + ": a FUSE connection survives"
          Array.isEmpty after.InflightIo, label + ": a fixture dm device has I/O in flight" ]

let requiredTeardownSteps (backend: string) =
    match backend with
    | "fuse" -> [ "release-cause"; "reader-settled"; "unmount"; "kill-processes" ]
    | "block" -> [ "release-cause"; "reader-settled"; "unmount"; "dm-remove"; "loop-detach"; "kill-processes" ]
    | _ -> [ "reader-settled"; "kill-processes" ]

/// `outside` is the census taken by the unprivileged orchestrator after the namespace exited.
let judgeTeardown (evidence: Evidence) (outside: Census) : Verdict =
    let ordered =
        let names = evidence.Teardown |> Array.sortBy (fun step -> step.StepAtNs) |> Array.map (fun step -> step.StepName)
        let required = requiredTeardownSteps evidence.Backend
        let positions = required |> List.map (fun name -> Array.IndexOf(names, name))
        positions |> List.forall (fun index -> index >= 0) && positions = List.sort positions
    let failed = evidence.Teardown |> Array.filter (fun step -> not step.StepOk) |> Array.map (fun step -> step.StepName)
    let census =
        [ judgeCensus "inside namespace" evidence.CensusBefore evidence.CensusInside
          judgeCensus "outside namespace" evidence.CensusBefore outside ]
    let censusReasons = census |> List.collect (fun verdict -> match verdict with Rejected reasons -> reasons | Accepted -> [])
    conclude
        [ ordered, "teardown steps are missing or out of order"
          Array.isEmpty failed, "teardown steps failed: " + String.Join(",", failed)
          not evidence.Reader.FinalPendingActivity, "reader activity still pending after teardown"
          List.isEmpty censusReasons, String.Join("; ", censusReasons) ]

/// A run that applied no block must read normally: the production outcome is Ready.
let readNormally (evidence: Evidence) =
    evidence.Reader.Phase = "Ready" && evidence.Reader.FirstFailure = "None" && evidence.Reader.DocumentCount = 2
