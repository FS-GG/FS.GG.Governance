/// GOV-423 prerequisite (b): contained kernel-level fixture for an authentic regular-file blocked read
/// accepted by production acquisition and an independently observed uncertain physical close.
///
/// Two tiers (docs/roadmaps/gov423-physical-fixture.md):
///   inert      always run in default CI: the FUSE wire decisions, the evidence judges and their mutations
///   physical   need FSGG_GOV_PHYSICAL_FIXTURE=1, passwordless sudo, /dev/fuse and dm_delay; each case runs
///              the unmodified production path inside a private mount + pid namespace, once, bounded
module FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureTests

open System
open System.Collections.Concurrent
open Expecto
open FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureFuse
open FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureEvidence
open FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureRuntime

// ------------------------------------------------------------------ synthetic evidence

let private ms (value: int) = int64 value * 1_000_000L
let private config: Config = { HoldRead = true; FlushErrno = EIO; FileName = "provenance.json"; Content = Text.Encoding.UTF8.GetBytes provenanceContent }

let private emptyCensus: Census =
    { DStateTasks = [||]; FixtureProcesses = [||]; FixtureMounts = [||]; DmDevices = [||]; LoopDevices = [||]
      FuseConnections = [||]; FuseConnectionsBaseline = [||]; InflightIo = [||] }

let private readerPid = 4242
let private readerThread = 77
let private event kind op unique atMs pid errno : DaemonEvent =
    { DaemonAtNs = ms atMs; DaemonWall = "2026-10-30T00:00:00Z"; Event = kind; Op = op; Unique = unique; DaemonNode = 2UL
      RequesterPid = pid; ReplyErrno = errno; DaemonDetail = "" }
let private sample atMs tid state wchan stack : Sample = { SampleAtNs = ms atMs; Tid = tid; State = state; Wchan = wchan; Stack = stack }

let private reader: ReaderResult =
    { ReaderPid = readerPid; WorkEndNs = ms 3000; CleanupEndNs = ms 6000; CaptureStartNs = ms 0; CaptureReturnedNs = ms 6010
      Phase = "Retiring"; FirstFailure = "WorkDeadlineReached"; SecondaryFailures = [| "CleanupDeadlineReached" |]
      Settlement = "Retained"; DocumentCount = 0; PendingActivity = true; RetainedDescriptors = 4; CloseUncertain = false
      ReleaseAfterReturn = "Error OwnedActivitiesPending"; FinalRelease = "Error CleanupDeadlineReached"; FinalPendingActivity = false; ReaderUid = 1000
      ModelDetail = "" }

let private causeOf scenario = (causeFor scenario).Value
let private cause = causeOf "fuse-blocked-read"
let private commit = String('a', 40)
let private treeHash = String('b', 40)
/// Observer heartbeats every 250 ms across the whole observation, as the real observer writes them.
let private beats (fromMs: int) (toMs: int) = [| for t in fromMs .. 250 .. toMs -> sample t 0 "HB" "" "" |]
let private withBeats samples = Array.append (beats 0 7000) samples

let private fuseBlocked: Evidence =
    { Schema = schema; Scenario = "fuse-blocked-read"; Head = commit; Tree = treeHash; Dirty = false; Backend = "fuse"; InjectedCause = cause; FieldIncident = false
      Kernel = "7.2.9"; FileType = "regular file"; BlockInjected = true
      Daemon =
        [| event "request" "OPEN" 11UL 100 readerThread 0
           event "request" "READ" 12UL 300 readerThread 0
           event "reply" "" 12UL 6400 0 -0 |]
      Observer = withBeats [| sample 100 77 "S" "futex_wait_queue" ""; sample 300 77 "S" "request_wait_answer" ""; sample 6400 77 "R" "0" "" |]
      ObserverEndNs = ms 7000; Dm = [||]; Reader = reader
      Teardown =
        [| { StepAtNs = ms 6401; StepName = "release-cause"; StepOk = true; StepDetail = "" }
           { StepAtNs = ms 6500; StepName = "reader-settled"; StepOk = true; StepDetail = "" }
           { StepAtNs = ms 6600; StepName = "unmount"; StepOk = true; StepDetail = "" }
           { StepAtNs = ms 6700; StepName = "kill-processes"; StepOk = true; StepDetail = "" } |]
      CensusBefore = emptyCensus; CensusInside = emptyCensus }

/// Kernel stack of the thread parked inside read(2), and of one parked in an openat2 lookup.
let private readStack = "folio_wait_bit_common;filemap_read_folio;filemap_get_pages;filemap_read;vfs_read;ksys_read;do_syscall_64"
let private lookupStack = "folio_wait_bit_common;ext4_lookup;__lookup_slow;path_openat;do_filp_open;do_sys_openat2;__x64_sys_openat2;do_syscall_64"

let private blockBlocked: Evidence =
    { fuseBlocked with
        Scenario = "block-blocked-read"; Backend = "block"; InjectedCause = causeOf "block-blocked-read"
        Daemon = [||]
        Observer = withBeats [| sample 100 77 "S" "futex_wait_queue" ""; sample 300 77 "D" "folio_wait_bit_common" readStack; sample 6400 77 "R" "0" "" |]
        Dm =
          [| { DmAtNs = ms -50; DmAction = "suspend"; DmExit = 0; DmDetail = "" }
             { DmAtNs = ms 6400; DmAction = "release"; DmExit = 0; DmDetail = "" } |]
        Teardown =
          [| { StepAtNs = ms 6401; StepName = "release-cause"; StepOk = true; StepDetail = "" }
             { StepAtNs = ms 6500; StepName = "reader-settled"; StepOk = true; StepDetail = "" }
             { StepAtNs = ms 6600; StepName = "unmount"; StepOk = true; StepDetail = "" }
             { StepAtNs = ms 6650; StepName = "dm-remove"; StepOk = true; StepDetail = "" }
             { StepAtNs = ms 6660; StepName = "loop-detach"; StepOk = true; StepDetail = "" }
             { StepAtNs = ms 6700; StepName = "kill-processes"; StepOk = true; StepDetail = "" } |] }

let private uncertainClose: Evidence =
    { fuseBlocked with
        Scenario = "fuse-uncertain-close"; InjectedCause = causeOf "fuse-uncertain-close"
        Daemon =
          [| event "request" "OPEN" 11UL 100 readerThread 0
             event "request" "READ" 12UL 300 readerThread 0
             event "reply" "" 12UL 5500 0 0
             event "request" "FLUSH" 13UL 5520 readerThread 0
             event "reply" "" 13UL 5521 0 5 |]
        Observer = withBeats [| sample 300 77 "S" "request_wait_answer" ""; sample 5500 77 "R" "0" "" |]
        Reader =
          { reader with
              CaptureReturnedNs = ms 5600; SecondaryFailures = [| "OwnedActivitiesPending" |]; PendingActivity = false
              CloseUncertain = true; ReleaseAfterReturn = "Error OwnedActivitiesPending" } }

let private noBlock: Evidence =
    { fuseBlocked with
        Scenario = "fuse-control-no-block"; BlockInjected = false
        InjectedCause = causeOf "fuse-control-no-block"
        Daemon = [| event "request" "READ" 12UL 300 readerThread 0; event "reply" "" 12UL 301 0 0 |]
        Observer = withBeats [| sample 100 77 "S" "futex_wait_queue" "" |]
        Reader = { reader with Phase = "Ready"; FirstFailure = "None"; SecondaryFailures = [||]; Settlement = "ResourcesReleased"; DocumentCount = 2; PendingActivity = false } }

let private rejected verdict = match verdict with Rejected _ -> true | Accepted -> false
let private reasonsOf verdict = match verdict with Rejected reasons -> String.Join(" | ", reasons) | Accepted -> ""
let private expectRejected (label: string) (needle: string) (verdict: Verdict) =
    Expect.isTrue (rejected verdict) (label + " must be rejected")
    Expect.isTrue ((reasonsOf verdict).Contains needle) (label + " must be rejected for the stated reason; got: " + reasonsOf verdict)
let private expectAccepted label verdict = Expect.equal verdict Accepted (label + " expected Accepted; got: " + reasonsOf verdict)

// ------------------------------------------------------------------ inert controls (default CI)

let private request opcode unique node body : FuseRequest =
    { Length = InHeaderSize + Array.length body; Opcode = opcode; Unique = unique; NodeId = node; Uid = 0u; Gid = 0u; Pid = 99u; Body = body }
let private readBody offset size =
    let body = Array.zeroCreate<byte> 40
    BitConverter.TryWriteBytes(Span<byte>(body, 8, 8), uint64 offset) |> ignore
    BitConverter.TryWriteBytes(Span<byte>(body, 16, 4), uint32 size) |> ignore
    body

let inertProtocol: (string * (unit -> unit)) list =
    [ "FUSE INIT answers with the 64-byte init_out at protocol 7.31", fun () ->
          match decide config (request OpInit 1UL 0UL (Array.zeroCreate 16)) with
          | Reply(0, payload) ->
              Expect.equal payload.Length 64 "init_out is 64 bytes"
              Expect.equal (BitConverter.ToUInt32(payload, 0), BitConverter.ToUInt32(payload, 4)) (7u, 31u) "protocol 7.31"
          | other -> failtestf "unexpected INIT decision %A" other
      "FUSE LOOKUP of the selected name yields a 128-byte entry for a regular file", fun () ->
          match decide config (request OpLookup 2UL RootNode (Array.append (Text.Encoding.UTF8.GetBytes "provenance.json") [| 0uy |])) with
          | Reply(0, payload) ->
              Expect.equal payload.Length 128 "entry_out is 128 bytes"
              Expect.equal (BitConverter.ToUInt64(payload, 0)) FileNode "file node id"
              Expect.equal (BitConverter.ToUInt32(payload, 40 + 60) &&& 0o170000u) 0o100000u "S_IFREG, the type statx must report"
              Expect.equal (BitConverter.ToUInt64(payload, 40 + 8)) (uint64 config.Content.Length) "size matches content"
          | other -> failtestf "unexpected LOOKUP decision %A" other
      "FUSE LOOKUP of any other name is ENOENT", fun () ->
          Expect.equal (decide config (request OpLookup 3UL RootNode (Array.append (Text.Encoding.UTF8.GetBytes "other") [| 0uy |]))) (Reply(ENOENT, [||])) "unknown name"
      "FUSE GETATTR reports a directory for the root and a regular file for the file", fun () ->
          let mode node =
              match decide config (request OpGetattr 4UL node (Array.zeroCreate 16)) with
              | Reply(0, payload) ->
                  Expect.equal payload.Length 104 "attr_out is 104 bytes"
                  BitConverter.ToUInt32(payload, 16 + 60) &&& 0o170000u
              | other -> failtestf "unexpected GETATTR decision %A" other
          Expect.equal (mode RootNode, mode FileNode) (0o040000u, 0o100000u) "dir then regular"
      "FUSE READ is held when the injected block is on and answered at once when it is off", fun () ->
          let read = request OpRead 5UL FileNode (readBody 0 4096)
          Expect.equal (decide config read) (Hold config.Content) "held with the payload a release returns"
          Expect.equal (decide { config with HoldRead = false } read) (Reply(0, config.Content)) "control answers immediately"
      "FUSE READ beyond end of file returns no bytes", fun () ->
          Expect.equal (decide { config with HoldRead = false } (request OpRead 6UL FileNode (readBody 4096 512))) (Reply(0, [||])) "EOF"
      "FUSE FLUSH carries the injected errno and the control carries success", fun () ->
          let flush = request OpFlush 7UL FileNode (Array.zeroCreate 24)
          Expect.equal (decide config flush) (Reply(EIO, [||])) "EIO injected"
          Expect.equal (decide { config with FlushErrno = 0 } flush) (Reply(0, [||])) "control succeeds"
      "FUSE reply encoding negates errno and sets length and unique", fun () ->
          let bytes = encodeReply 77UL EIO [| 1uy; 2uy |]
          Expect.equal (BitConverter.ToUInt32(bytes, 0), BitConverter.ToInt32(bytes, 4), BitConverter.ToUInt64(bytes, 8)) (16u, -5, 77UL) "error replies carry no payload"
          Expect.equal (encodeReply 78UL 0 [| 1uy; 2uy |]).Length 18 "success carries payload"
      "FUSE request parsing rejects short and truncated frames", fun () ->
          Expect.isNone (parseRequest (Array.zeroCreate 8) 8) "short"
          let frame = Array.zeroCreate<byte> 48
          BitConverter.TryWriteBytes(Span<byte>(frame, 0, 4), 64u) |> ignore
          Expect.isNone (parseRequest frame 48) "declared length exceeds frame"
      "FUSE INTERRUPT names its target and unknown opcodes are ENOSYS", fun () ->
          let body = BitConverter.GetBytes 12UL
          Expect.equal (interruptTarget (request OpInterrupt 8UL 0UL body)) (Some 12UL) "target unique"
          Expect.equal (decide config (request 9999u 9UL 0UL [||])) (Reply(ENOSYS, [||])) "unknown op" ]

let inertJudges: (string * (unit -> unit)) list =
    [ "Judge accepts well-formed FUSE blocked-read evidence", fun () -> expectAccepted "fuse blocked" (judgeBlockedRead fuseBlocked)
      "Judge accepts well-formed block-layer blocked-read evidence", fun () -> expectAccepted "block blocked" (judgeBlockedRead blockBlocked)
      "Judge rejects a run presented as a field incident", fun () ->
          expectRejected "field incident" "field incident" (judgeBlockedRead { fuseBlocked with FieldIncident = true })
      "Judge rejects evidence without an explicit injected cause", fun () ->
          expectRejected "blank cause" "injected cause" (judgeBlockedRead { fuseBlocked with InjectedCause = "" })
          expectRejected "cause not naming an injection" "injected cause" (judgeBlockedRead { fuseBlocked with InjectedCause = "the disk was slow today, as seen in production" })
          expectRejected "negated cause" "injected cause" (judgeBlockedRead { fuseBlocked with InjectedCause = "not injected: " + cause })
          expectRejected "cause of another scenario" "injected cause" (judgeBlockedRead { fuseBlocked with InjectedCause = causeOf "block-blocked-read" })
          expectRejected "unknown scenario" "injected cause" (judgeBlockedRead { fuseBlocked with Scenario = "invented" })
      "Judge rejects a file that statx does not report as regular", fun () ->
          expectRejected "directory" "regular file" (judgeBlockedRead { fuseBlocked with FileType = "directory" })
      "Judge rejects blocked-read evidence when the daemon never logged the READ", fun () ->
          expectRejected "no READ" "no READ" (judgeBlockedRead { fuseBlocked with Daemon = [| event "request" "OPEN" 11UL 100 readerThread 0 |] })
      "Judge rejects blocked-read evidence when the READ did not come from the reader or its threads", fun () ->
          expectRejected "foreign READ" "no READ" (judgeBlockedRead { fuseBlocked with Daemon = [| event "request" "READ" 12UL 300 1 0 |] })
          expectRejected "parked on another thread" "observer"
              (judgeBlockedRead { fuseBlocked with Observer = withBeats [| sample 300 99 "S" "request_wait_answer" ""; sample 6400 99 "R" "0" ""; sample 100 77 "S" "futex_wait_queue" "" |] })
      "Judge rejects a READ the daemon answered before the work deadline", fun () ->
          let answered = Array.append fuseBlocked.Daemon.[0..1] [| event "reply" "" 12UL 1200 0 0 |]
          expectRejected "early reply" "unanswered" (judgeBlockedRead { fuseBlocked with Daemon = answered })
      "Judge rejects when the observer never saw a task parked on the cause", fun () ->
          expectRejected "no parked" "observer" (judgeBlockedRead { fuseBlocked with Observer = withBeats [| sample 100 77 "S" "futex_wait_queue" "" |] })
      "Judge rejects when the observed park is shorter than the work window", fun () ->
          expectRejected "short park" "observer"
              (judgeBlockedRead { fuseBlocked with Observer = withBeats [| sample 2800 77 "S" "request_wait_answer" ""; sample 2900 77 "R" "0" "" |] })
      "Judge rejects a block-layer claim without uninterruptible sleep", fun () ->
          expectRejected "S state" "observer"
              (judgeBlockedRead { blockBlocked with Observer = withBeats [| sample 300 77 "S" "folio_wait_bit_common" readStack; sample 6400 77 "R" "0" "" |] })
      "Judge rejects a D-state park inside an openat2 lookup as a blocked read (M1)", fun () ->
          expectRejected "lookup park" "observer"
              (judgeBlockedRead { blockBlocked with Observer = withBeats [| sample 300 77 "D" "folio_wait_bit_common" lookupStack; sample 6400 77 "R" "0" "" |] })
      "Judge rejects a block-layer park with an unreadable kernel stack (M2)", fun () ->
          expectRejected "empty stack" "observer"
              (judgeBlockedRead { blockBlocked with Observer = withBeats [| sample 300 77 "D" "folio_wait_bit_common" ""; sample 6400 77 "R" "0" "" |] })
      "Judge rejects a park the observer could not have watched: no heartbeat across it (M4)", fun () ->
          let stalled = blockBlocked.Observer |> Array.filter (fun s -> s.State <> "HB" || s.SampleAtNs <= ms 400 || s.SampleAtNs >= ms 6300)
          expectRejected "stalled observer" "heartbeat" (judgeBlockedRead { blockBlocked with Observer = stalled })
          let fuseStalled = fuseBlocked.Observer |> Array.filter (fun s -> s.State <> "HB" || s.SampleAtNs <= ms 1000)
          expectRejected "stalled observer, FUSE" "heartbeat" (judgeBlockedRead { fuseBlocked with Observer = fuseStalled })
      "Judge refuses non-observer samples bound to state HB as heartbeats (M8)", fun () ->
          // The real heartbeats are the observer's own records (Tid 0). Stall the observer, then fill the gap with
          // samples of a reader thread that merely carry the state "HB".
          let stalled = blockBlocked.Observer |> Array.filter (fun s -> s.State <> "HB" || s.SampleAtNs <= ms 400 || s.SampleAtNs >= ms 6300)
          let spoofed = [| for t in 500 .. 250 .. 6200 -> sample t 77 "HB" "" "" |]
          expectRejected "spoofed heartbeats" "heartbeat" (judgeBlockedRead { blockBlocked with Observer = Array.append stalled spoofed })
          Expect.isFalse (heartbeatCovers spoofed (ms 500) (ms 6200)) "Tid-bound heartbeats only"
      "Judge accepts a long park when heartbeats cover it", fun () ->
          expectAccepted "covered" (judgeBlockedRead blockBlocked)
      "Judge rejects evidence not bound to a clean commit and tree", fun () ->
          expectRejected "no head" "commit" (judgeBlockedRead { fuseBlocked with Head = "unknown" })
          expectRejected "no tree" "commit" (judgeBlockedRead { fuseBlocked with Tree = "" })
          expectRejected "dirty" "commit" (judgeBlockedRead { fuseBlocked with Dirty = true })
      "Judge rejects a block-layer claim when the device was never suspended", fun () ->
          expectRejected "no suspend" "suspended" (judgeBlockedRead { blockBlocked with Dm = [||] })
      "Judge rejects a block-layer claim when the device was released before work end", fun () ->
          let early = [| { DmAtNs = ms -50; DmAction = "suspend"; DmExit = 0; DmDetail = "" }; { DmAtNs = ms 1000; DmAction = "release"; DmExit = 0; DmDetail = "" } |]
          expectRejected "early release" "suspended" (judgeBlockedRead { blockBlocked with Dm = early })
      "Judge rejects production outcomes that are not the classified blocked read", fun () ->
          expectRejected "ready" "WorkDeadlineReached" (judgeBlockedRead { fuseBlocked with Reader = { reader with FirstFailure = "None"; Phase = "Ready" } })
          expectRejected "document accepted" "accepted a document" (judgeBlockedRead { fuseBlocked with Reader = { reader with DocumentCount = 1 } })
          expectRejected "claimed released" "released" (judgeBlockedRead { fuseBlocked with Reader = { reader with Settlement = "ResourcesReleased" } })
      "Control: a run without the block is rejected as blocked-read evidence and reads normally", fun () ->
          expectRejected "no block" "no block was injected" (judgeBlockedRead noBlock)
          Expect.isTrue (readNormally noBlock) "production reads the unblocked file normally"
          Expect.equal (longestParkNs "fuse" noBlock.ObserverEndNs noBlock.Observer) 0L "observer sees nothing parked"
      "Control: relabelling an unblocked run as injected still fails on observation, daemon and outcome", fun () ->
          let relabelled = { noBlock with BlockInjected = true; InjectedCause = cause }
          let verdict = judgeBlockedRead relabelled
          Expect.isTrue (rejected verdict) "cannot pass by assertion"
          Expect.isTrue ((reasonsOf verdict).Contains "WorkDeadlineReached" && (reasonsOf verdict).Contains "observer" && (reasonsOf verdict).Contains "unanswered") ("independent facts disagree; got: " + reasonsOf verdict) ]

let inertClose: (string * (unit -> unit)) list =
    [ "Judge accepts well-formed uncertain-close evidence", fun () -> expectAccepted "uncertain close" (judgeUncertainClose uncertainClose)
      "Judge rejects an uncertain close when the daemon answered FLUSH with success", fun () ->
          let ok = uncertainClose.Daemon |> Array.map (fun e -> if e.Unique = 13UL && e.Event = "reply" then { e with ReplyErrno = 0 } else e)
          expectRejected "flush ok" "EIO" (judgeUncertainClose { uncertainClose with Daemon = ok })
      "Judge rejects an uncertain close with no FLUSH from the production reader", fun () ->
          expectRejected "no flush" "EIO" (judgeUncertainClose { uncertainClose with Daemon = uncertainClose.Daemon |> Array.filter (fun e -> e.Unique <> 13UL) })
      "Judge rejects an uncertain close that production did not classify", fun () ->
          expectRejected "unclassified" "OwnedActivitiesPending"
              (judgeUncertainClose { uncertainClose with Reader = { uncertainClose.Reader with SecondaryFailures = [||] } })
          expectRejected "ownership clean" "uncertain close"
              (judgeUncertainClose { uncertainClose with Reader = { uncertainClose.Reader with CloseUncertain = false } })
      "Judge rejects an uncertain close when public release succeeds or settlement is claimed", fun () ->
          expectRejected "release ok" "public release" (judgeUncertainClose { uncertainClose with Reader = { uncertainClose.Reader with ReleaseAfterReturn = "Ok" } })
          expectRejected "released" "settlement" (judgeUncertainClose { uncertainClose with Reader = { uncertainClose.Reader with Settlement = "ResourcesReleased" } })
      "Judge rejects an uncertain close whose FLUSH precedes the released READ", fun () ->
          let early = uncertainClose.Daemon |> Array.map (fun e -> if e.Unique = 13UL && e.Event = "request" then { e with DaemonAtNs = ms 100 } else e)
          expectRejected "flush before read" "ordered" (judgeUncertainClose { uncertainClose with Daemon = early })
      "Judge rejects an uncertain close with no injected block before it (M3)", fun () ->
          expectRejected "no block injected" "no block was injected" (judgeUncertainClose { uncertainClose with BlockInjected = false })
          let early = uncertainClose.Daemon |> Array.map (fun e -> if e.Unique = 12UL && e.Event = "reply" then { e with DaemonAtNs = ms 301 } else e)
          expectRejected "READ answered at once" "held READ" (judgeUncertainClose { uncertainClose with Daemon = early })
          expectRejected "park not observed" "held READ" (judgeUncertainClose { uncertainClose with Observer = withBeats [| sample 100 77 "S" "futex_wait_queue" "" |] })
      "Judge rejects an uncertain close claimed for the block-layer backend", fun () ->
          expectRejected "block backend" "FUSE" (judgeUncertainClose { uncertainClose with Backend = "block" }) ]

let inertTeardown: (string * (unit -> unit)) list =
    let accepted evidence = judgeTeardown evidence emptyCensus
    let with' census = { fuseBlocked with CensusInside = census }
    [ "Judge accepts a qualified FUSE and block teardown", fun () ->
          expectAccepted "fuse" (accepted fuseBlocked)
          expectAccepted "block" (accepted blockBlocked)
      "Judge rejects teardown when a D-state task survives inside or outside the namespace", fun () ->
          expectRejected "inside" "D-state" (accepted (with' { emptyCensus with DStateTasks = [| "4242/77 reader" |] }))
          expectRejected "outside" "D-state" (judgeTeardown fuseBlocked { emptyCensus with DStateTasks = [| "4242/77 reader" |] })
      "Judge rejects teardown when a fixture mount, dm device, loop device or process survives", fun () ->
          expectRejected "mount" "mount" (accepted (with' { emptyCensus with FixtureMounts = [| "42 25 0:55 / /x" |] }))
          expectRejected "dm" "dm device" (accepted (with' { emptyCensus with DmDevices = [| "fsgg423b-1" |] }))
          expectRejected "loop" "loop device" (accepted (with' { emptyCensus with LoopDevices = [| "/dev/loop3" |] }))
          expectRejected "process" "process" (accepted (with' { emptyCensus with FixtureProcesses = [| "1 daemon" |] }))
      "Judge rejects teardown when a FUSE connection outlives the run", fun () ->
          expectRejected "connection" "FUSE connection" (accepted (with' { emptyCensus with FuseConnections = [| "55" |] }))
      "Judge rejects teardown with missing, failed or out-of-order steps", fun () ->
          expectRejected "missing" "missing" (accepted { fuseBlocked with Teardown = fuseBlocked.Teardown |> Array.filter (fun s -> s.StepName <> "unmount") })
          expectRejected "failed" "failed" (accepted { fuseBlocked with Teardown = fuseBlocked.Teardown |> Array.map (fun s -> if s.StepName = "unmount" then { s with StepOk = false } else s) })
          expectRejected "order" "out of order" (accepted { fuseBlocked with Teardown = fuseBlocked.Teardown |> Array.map (fun s -> if s.StepName = "release-cause" then { s with StepAtNs = ms 9000 } else s) })
      "Judge rejects teardown while a fixture dm device has I/O in flight", fun () ->
          expectRejected "inflight" "in flight" (accepted (with' { emptyCensus with InflightIo = [| "dm-3 fsgg423b-1 inflight=1 0" |] }))
      "Judge rejects teardown while the reader activity is still pending", fun () ->
          expectRejected "pending" "pending" (accepted { fuseBlocked with Reader = { reader with FinalPendingActivity = true } })
      "Evidence round-trips through its JSON record unchanged", fun () ->
          Expect.equal (ofJson<Evidence> (toJson uncertainClose)) uncertainClose "lossless" ]

let inertLock: (string * (unit -> unit)) list =
    [ "Host lock: a second acquisition refuses while the first is held and succeeds after release", fun () ->
          if not (OperatingSystem.IsLinux()) then skiptest "flock is Linux-only"
          let dir = IO.Directory.CreateTempSubdirectory("fsgg423b-locktest-").FullName
          try
              let path = IO.Path.Combine(dir, "lock")
              match tryAcquireHostLock path with
              | Error why -> failtestf "first acquisition must succeed: %s" why
              | Ok first ->
                  (match tryAcquireHostLock path with
                   | Ok second -> second.Dispose(); first.Dispose(); failtest "a second holder must be refused while the first is held"
                   | Error why -> Expect.stringContains why "busy" "refusal says why")
                  first.Dispose()
                  match tryAcquireHostLock path with
                  | Ok again -> again.Dispose()
                  | Error why -> failtestf "re-acquisition after release must succeed: %s" why
          finally
              IO.Directory.Delete(dir, true) ]

let private inertCases = inertProtocol @ inertJudges @ inertClose @ inertTeardown @ inertLock

[<Tests>]
let inertTests = testList "PhysicalFixture inert" (inertCases |> List.map (fun (name, run) -> testCase name run))

// ------------------------------------------------------------------ physical cases (opt-in)

let private runs = ConcurrentDictionary<string, Lazy<RunResult>>()
let private run (name: string) = runs.GetOrAdd(name, fun n -> lazy (runPhysically n)).Value

let private prerequisites =
    lazy (
        match missingPrerequisites () with
        | [] -> ()
        | missing -> failtestf "FSGG_GOV_PHYSICAL_FIXTURE=1 but the host cannot run the fixture: %s" (String.Join("; ", missing)))

let private physical (name: string) (body: unit -> unit) =
    let guarded () = prerequisites.Value; body ()
    if optedIn () then testCase name guarded else ptestCase name guarded

let private teardownVerdict (name: string) = let result = run name in judgeTeardown result.Evidence result.Outside

/// The uncertain-close case runs only after both blocking backends have been torn down and censused clean.
let private teardownQualified =
    lazy (
        for name in [ "fuse-blocked-read"; "block-blocked-read" ] do
            expectAccepted ("teardown qualification of " + name) (teardownVerdict name))

let private statedInjected (evidence: Evidence) =
    Expect.isFalse evidence.FieldIncident "never presented as a field incident"
    Expect.isTrue (evidence.InjectedCause.ToLowerInvariant().Contains "inject") "injected cause recorded explicitly"

let physicalCases: (string * (unit -> unit)) list =
    [ "Physical: production acquisition against a normal file succeeds", fun () ->
          let result = run "normal-file"
          Expect.isTrue (readNormally result.Evidence) (sprintf "Ready with two documents; got %s / %s" result.Evidence.Reader.Phase result.Evidence.Reader.FirstFailure)
          Expect.equal result.Evidence.FileType "regular file" "statx regular file"
          if not (isNull (Environment.GetEnvironmentVariable "SUDO_UID")) || Environment.UserName <> "root" then
              Expect.notEqual result.Evidence.Reader.ReaderUid 0 "the production reader runs as the invoking user, not root"
          Expect.equal result.Evidence.Reader.FinalRelease "Ok" "public release settles after the caller retires the owner"
          expectAccepted "normal-file teardown" (judgeTeardown result.Evidence result.Outside)
      "Physical: FUSE regular file read blocks and production classifies WorkDeadlineReached, observed independently", fun () ->
          let evidence = (run "fuse-blocked-read").Evidence
          statedInjected evidence
          expectAccepted "fuse blocked read" (judgeBlockedRead evidence)
          Expect.isTrue evidence.Reader.PendingActivity "production still owns the pending read at return"
          Expect.isTrue (evidence.Observer |> Array.exists (fun s -> s.Wchan.Contains "request_wait_answer")) "kernel wait channel from the separate observer"
      "Physical: block-layer regular file read blocks in D state and production classifies WorkDeadlineReached", fun () ->
          let evidence = (run "block-blocked-read").Evidence
          statedInjected evidence
          expectAccepted "block blocked read" (judgeBlockedRead evidence)
          Expect.isTrue (evidence.Observer |> Array.exists (fun s -> s.State = "D")) "observer saw uninterruptible sleep"
      "Physical: FUSE teardown (connection abort) leaves no D-state task, mount, connection or process", fun () ->
          expectAccepted "fuse teardown" (teardownVerdict "fuse-blocked-read")
      "Physical: block teardown (error table, dm remove, loop detach) leaves no D-state task, mount or device", fun () ->
          expectAccepted "block teardown" (teardownVerdict "block-blocked-read")
      "Physical: uncertain close after a qualified teardown is classified OwnedActivitiesPending", fun () ->
          teardownQualified.Value
          let evidence = (run "fuse-uncertain-close").Evidence
          statedInjected evidence
          expectAccepted "uncertain close" (judgeUncertainClose evidence)
          expectAccepted "blocked before close" (judgeBlockedRead evidence)
          expectAccepted "uncertain-close teardown" (teardownVerdict "fuse-uncertain-close")
      "Physical control: FUSE fixture without the block does not pass", fun () ->
          let evidence = (run "fuse-control-no-block").Evidence
          Expect.isTrue (rejected (judgeBlockedRead evidence)) "no block, no pass"
          Expect.isTrue (readNormally evidence) "production reads the FUSE regular file normally"
          Expect.isLessThan (longestParkNs "fuse" evidence.ObserverEndNs evidence.Observer) 1_000_000_000L "observer saw only microsecond FUSE round trips, no park"
          Expect.isTrue (rejected (judgeUncertainClose evidence)) "no uncertain close either"
          expectAccepted "control teardown" (teardownVerdict "fuse-control-no-block")
      "Physical control: abnormal exit of the namespace driver leaks devices and the janitor restores a clean census", fun () ->
          let result = runAbnormalExit ()
          Expect.isFalse result.EvidenceWritten "the driver died before any teardown or evidence write"
          Expect.contains [ 70; 137 ] result.ExitCode (sprintf "abrupt exit (70) or hung pid 1 killed by the timeout (137); got %d" result.ExitCode)
          let leakedSomething =
              result.Leaked.DmDevices.Length + result.Leaked.LoopDevices.Length + result.Leaked.DStateTasks.Length + result.Leaked.FixtureMounts.Length > 0
          Expect.isTrue leakedSomething (sprintf "the abnormal exit really left state behind (otherwise the control is vacuous); census: %s" (toJson result.Leaked))
          Expect.isNonEmpty result.Leaked.DmDevices "the suspended dm device leaked"
          Expect.isNonEmpty result.Leaked.LoopDevices "the loop device leaked (the census matches it by run directory name)"
          Expect.isNonEmpty result.JanitorActions "janitor acted"
          expectAccepted "census after janitor" (judgeCensus "after janitor" result.Restored result.Restored)
          Expect.isEmpty result.Restored.DmDevices "no dm device"
          Expect.isEmpty result.Restored.LoopDevices "no loop device"
      "Physical control: a second run while the host lock is held refuses and leaves a live run's devices alone", fun () ->
          let name, device, dir = createLiveDevices ()
          try
              let holder =
                  match tryAcquireHostLock (lockPath ()) with
                  | Ok held -> held
                  | Error why -> failtestf "could not take the host lock for the control: %s" why
              try
                  let refused (label: string) (attempt: unit -> unit) =
                      try
                          attempt ()
                          failtestf "%s ran although the host lock was held" label
                      with PhysicalRunBusy why -> Expect.stringContains why "busy" (label + " refuses with the reason")
                  refused "runPhysically" (fun () -> runPhysically "normal-file" |> ignore)
                  refused "runAbnormalExit" (fun () -> runAbnormalExit () |> ignore)
                  let _, devices, _ = runPrivileged "dmsetup" [ "ls" ] 10000 |> fun (c, o, e) -> c, o, e
                  let _, loops, _ = runTool "losetup" [ "-a" ] 10000
                  Expect.stringContains devices name "the live run's dm device was not janitored away"
                  Expect.stringContains loops device "the live run's loop device was not janitored away"
              finally
                  holder.Dispose()
          finally
              janitor () |> ignore
              IO.Directory.Delete(dir, true)
          let _, after, _ = runPrivileged "dmsetup" [ "ls" ] 10000
          Expect.isFalse (after.Contains name) "the test's own devices are cleaned up afterwards"
      "Physical control: block fixture without the suspension does not pass", fun () ->
          let evidence = (run "block-control-no-block").Evidence
          Expect.isTrue (rejected (judgeBlockedRead evidence)) "no block, no pass"
          Expect.isTrue (readNormally evidence) "production reads the ext4 regular file normally"
          Expect.isLessThan (longestParkNs "block" evidence.ObserverEndNs evidence.Observer) 1_000_000_000L "observer saw no sustained D-state park"
          expectAccepted "control teardown" (teardownVerdict "block-control-no-block") ]

[<Tests>]
let physicalTests =
    // Sequenced: the cases share the namespace scenarios and the kernel's global dm/loop tables.
    testSequenced (testList "PhysicalFixture physical (FSGG_GOV_PHYSICAL_FIXTURE=1)" (physicalCases |> List.map (fun (name, body) -> physical name body)))
