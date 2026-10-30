/// GOV-423 prerequisite (b) physical fixture, part 3: the privileged runtime.
///
/// One executable, four roles, all started from the test assembly itself with
/// `--physical-fixture <role>` (see Main.fs):
///   scenario  pid 1 of a private mount + pid namespace; owns setup, injection, teardown and the census
///   daemon    raw-protocol FUSE daemon; logs every request with a monotonic timestamp
///   reader    the production ProviderContextCapture path, unmodified, against the fixture file
///   observer  separate process sampling /proc/<reader>/task/*/{stat,wchan,stack}
/// The orchestrator (the xunit-side tests) starts `scenario` through `sudo unshare`, so the kernel
/// physically produces the block and the close error and the whole tree dies with the namespace.
module FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureRuntime

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.ProviderContextCapture
open FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureFuse
open FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureEvidence

module Sys =
    [<DllImport("libc", EntryPoint = "open", SetLastError = true)>]
    extern int openFd(string path, int flags)
    [<DllImport("libc", EntryPoint = "read", SetLastError = true)>]
    extern int64 readFd(int fd, byte[] buffer, uint64 count)
    [<DllImport("libc", EntryPoint = "write", SetLastError = true)>]
    extern int64 writeFd(int fd, byte[] buffer, uint64 count)
    [<DllImport("libc", EntryPoint = "_exit")>]
    extern void exitNow(int code)
    [<DllImport("libc", SetLastError = true)>]
    extern int mount(string source, string target, string fstype, uint64 flags, string data)

let private compact = JsonSerializerOptions(WriteIndented = false)
let processPath =
    match Environment.ProcessPath with
    | null -> failwith "the runtime host path is unavailable"
    | path -> path
let monoNs () : int64 =
    if Stopwatch.Frequency <> 1_000_000_000L then failwith "monotonic clock is not nanosecond resolution"
    Stopwatch.GetTimestamp()

let provenanceContent = "provenance-original-gov423b\n"
let policyContent = "policy-original-gov423b\n"
let sha256 (text: string) =
    Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes text) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

/// Bounded external tool. Never waits past `timeoutMs`; a tool that ignores the kill is abandoned.
let runTool (exe: string) (args: string list) (timeoutMs: int) : int * string * string =
    use proc = new Process()
    proc.StartInfo <- ProcessStartInfo(exe, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true)
    for arg in args do proc.StartInfo.ArgumentList.Add arg
    try
        proc.Start() |> ignore
        let out = proc.StandardOutput.ReadToEndAsync()
        let err = proc.StandardError.ReadToEndAsync()
        if proc.WaitForExit timeoutMs then
            proc.WaitForExit()
            proc.ExitCode, out.Result, err.Result
        else
            (try proc.Kill true with _ -> ())
            -1, "", "timeout after " + string timeoutMs + "ms"
    with ex -> -1, "", ex.GetType().Name + ": " + ex.Message

/// Same, through sudo when not already root.
let isRoot () = Environment.UserName = "root"
let runPrivileged (exe: string) (args: string list) (timeoutMs: int) =
    if isRoot () then runTool exe args timeoutMs else runTool "sudo" ("-n" :: exe :: args) timeoutMs

let private writeText (path: string) (text: string) = File.WriteAllText(path, text)
let private readOrEmpty (path: string) = try (File.ReadAllText path).Trim() with _ -> ""

let waitUntil (timeoutMs: int) (condition: unit -> bool) : bool =
    let deadline = Stopwatch.StartNew()
    let mutable ok = condition ()
    while not ok && deadline.ElapsedMilliseconds < int64 timeoutMs do
        Thread.Sleep 10
        ok <- condition ()
    ok

let private line (value: 'a) = JsonSerializer.Serialize(value, compact)

// ---------------------------------------------------------------- daemon

/// argv: mountpoint ctlDir logPath hold(0|1) flushErrno
let runDaemon (argv: string[]) : int =
    let mountpoint, ctl, logPath = argv[0], argv[1], argv[2]
    let config: Config =
        { HoldRead = argv[3] = "1"
          FlushErrno = int argv[4]
          FileName = "provenance.json"
          Content = Encoding.UTF8.GetBytes provenanceContent }
    let writer = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), UTF8Encoding(false), AutoFlush = true)
    let log kind op (unique: uint64) node pid errno detail =
        let event: DaemonEvent =
            { DaemonAtNs = monoNs ()
              DaemonWall = DateTime.UtcNow.ToString "o"
              Event = kind
              Op = op
              Unique = unique
              DaemonNode = node
              RequesterPid = pid
              ReplyErrno = errno
              DaemonDetail = detail }
        lock writer (fun () -> writer.WriteLine(line event))
    let fd = Sys.openFd("/dev/fuse", 2 ||| 0x80000)
    if fd < 0 then
        log "fatal" "" 0UL 0UL 0 0 ("open /dev/fuse errno=" + string (Marshal.GetLastPInvokeError()))
        2
    else
        let data = sprintf "fd=%d,rootmode=40000,user_id=0,group_id=0,allow_other" fd
        let mounted = Sys.mount("fsgg423b-fixture", mountpoint, "fuse.fsgg423b", 6UL, data)
        log "mount" "" 0UL 0UL 0 0 (sprintf "rc=%d errno=%d hold=%b flush=%d" mounted (Marshal.GetLastPInvokeError()) config.HoldRead config.FlushErrno)
        if mounted <> 0 then 3
        else
            let interrupted = ConcurrentDictionary<uint64, bool>()
            let mutable running = true
            let send (unique: uint64) (errno: int) (payload: byte[]) =
                let bytes = encodeReply unique errno payload
                let written = Sys.writeFd(fd, bytes, uint64 bytes.Length)
                let failure = if written < 0L then "write errno=" + string (Marshal.GetLastPInvokeError()) else ""
                log "reply" "" unique 0UL 0 errno failure
            let release = Path.Combine(ctl, "release-read")
            let buffer = Array.zeroCreate<byte> (1536 * 1024)
            while running do
                let count = Sys.readFd(fd, buffer, uint64 buffer.Length)
                if count < 0L then
                    let errno = Marshal.GetLastPInvokeError()
                    match errno with
                    | 4 | 11 | 2 -> ()
                    | _ ->
                        log "connection-closed" "" 0UL 0UL 0 errno (if errno = 19 then "ENODEV: aborted or unmounted" else "read failed")
                        running <- false
                else
                    match parseRequest buffer (int count) with
                    | None -> log "malformed" "" 0UL 0UL 0 0 (string count)
                    | Some request ->
                        log "request" (opName request.Opcode) request.Unique request.NodeId (int request.Pid) 0 ""
                        match decide config request with
                        | Reply(errno, payload) ->
                            send request.Unique errno payload
                            if request.Opcode = OpInit then writeText (Path.Combine(ctl, "ready")) "ready"
                        | Hold payload ->
                            interrupted[request.Unique] <- false
                            Task.Run(fun () ->
                                let mutable answer = None
                                while answer.IsNone && running do
                                    if File.Exists release then answer <- Some(0, payload)
                                    elif interrupted[request.Unique] then answer <- Some(EINTR, [||])
                                    else Thread.Sleep 10
                                match answer with
                                | Some(errno, bytes) -> send request.Unique errno bytes
                                | None -> log "hold-abandoned" "READ" request.Unique 0UL 0 0 "connection ended while held")
                            |> ignore
                        | NoReply ->
                            match interruptTarget request with
                            | Some target -> interrupted[target] <- true
                            | None -> ()
            log "daemon-exit" "" 0UL 0UL 0 0 ""
            0

// ---------------------------------------------------------------- observer

let private threadSample (pid: int) (tid: string) : Sample option =
    let root = sprintf "/proc/%d/task/%s" pid tid
    try
        let stat = File.ReadAllText(root + "/stat")
        let state = stat.Substring(stat.LastIndexOf ')' + 2, 1)
        let wchan = readOrEmpty (root + "/wchan")
        let stack =
            if state = "D" || wchan.Contains "fuse" || wchan.Contains "request_wait" then
                try (File.ReadAllText(root + "/stack")).Replace('\n', ';').Replace("\t", " ") |> fun s -> s.Substring(0, min 700 s.Length)
                with _ -> ""
            else ""
        Some { SampleAtNs = monoNs (); Tid = int tid; State = state; Wchan = wchan; Stack = stack }
    with _ -> None

/// argv: pid outPath stopFile
let runObserver (argv: string[]) : int =
    let pid, outPath, stopFile = int argv[0], argv[1], argv[2]
    use writer = new StreamWriter(new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), UTF8Encoding(false), AutoFlush = true)
    let last = Collections.Generic.Dictionary<int, string>()
    let emit (sample: Sample) = writer.WriteLine(line sample)
    let mutable lastBeat = 0L
    while not (File.Exists stopFile) do
        // Heartbeat: proves this observer was running, so a stall cannot masquerade as a sustained park.
        if monoNs () - lastBeat >= 250_000_000L then
            lastBeat <- monoNs ()
            emit { SampleAtNs = lastBeat; Tid = 0; State = "HB"; Wchan = ""; Stack = "" }
        let tids = try Directory.GetDirectories(sprintf "/proc/%d/task" pid) |> Array.choose (fun path -> Path.GetFileName path |> Option.ofObj) with _ -> [||]
        let present = Collections.Generic.HashSet<int>()
        for tid in tids do
            match threadSample pid tid with
            | Some sample ->
                present.Add sample.Tid |> ignore
                let key = sample.State + "|" + sample.Wchan
                match last.TryGetValue sample.Tid with
                | true, previous when previous = key -> ()
                | _ ->
                    last[sample.Tid] <- key
                    emit sample
            | None -> ()
        for tid in List.ofSeq last.Keys do
            if not (present.Contains tid) && last[tid] <> "gone|" then
                last[tid] <- "gone|"
                emit { SampleAtNs = monoNs (); Tid = tid; State = "gone"; Wchan = ""; Stack = "" }
        Thread.Sleep 10
    emit { SampleAtNs = monoNs (); Tid = 0; State = "END"; Wchan = ""; Stack = "" }
    0

// ---------------------------------------------------------------- reader (production path)

let private caseName (value: 'a) =
    let case, _ = Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(value, typeof<'a>)
    case.Name

let private describeRelease (result: Result<unit, Failure>) =
    match result with
    | Ok() -> "Ok"
    | Error failure -> "Error " + caseName failure

/// argv: root provenanceRelative policyPath ctlDir workSeconds cleanupSeconds
/// Runs the unmodified production prepare/capture/release; records only what production reports.
let runReader (argv: string[]) : int =
    let root, relative, policyPath, ctl = argv[0], argv[1], argv[2], argv[3]
    let work, cleanup = Double.Parse(argv[4], Globalization.CultureInfo.InvariantCulture), Double.Parse(argv[5], Globalization.CultureInfo.InvariantCulture)
    let pid = Process.GetCurrentProcess().Id
    writeText (Path.Combine(ctl, "reader.pid")) (string pid)
    if not (waitUntil 60000 (fun () -> File.Exists(Path.Combine(ctl, "go")))) then 4
    else
        let instant = FS.GG.Governance.GateExecution.Interpreter.currentInstant ()
        let freq = Stopwatch.Frequency
        let budget: FS.GG.Governance.GateExecution.Model.ExecutionBudget =
            { WorkEnd = { instant with Ticks = instant.Ticks + int64 (work * float freq) }
              CleanupEnd = { instant with Ticks = instant.Ticks + int64 ((work + cleanup) * float freq) } }
        let request: Request =
            { Operation = "Physical-fixture-" + string pid
              RepositoryRoot = root
              Selection =
                { ProvenancePath = relative
                  ProvenanceSha256 = sha256 provenanceContent
                  PolicyPath = policyPath
                  PolicySha256 = sha256 policyContent
                  Platform = "linux-x64" }
              Namespace = CooperativelyStableNamespace
              Limits = { PerFileBytes = 4096; AggregateBytes = 8192 }
              Budget = budget
              Cancellation = CancellationToken.None }
        match prepare request with
        | Error causes ->
            writeText (Path.Combine(ctl, "reader-error")) (sprintf "%A" causes)
            5
        | Ok session ->
            let capturedAt = monoNs ()
            writeText (Path.Combine(ctl, "reader-budget")) (sprintf "%d %d %d" capturedAt budget.WorkEnd.Ticks budget.CleanupEnd.Ticks)
            let observation = capture session
            let returnedAt = monoNs ()
            let ownedAtReturn = ownership session
            let releasedAtReturn = release session
            let model = observation.Model
            let result (finalRelease: string) (finalPending: bool) : ReaderResult =
                { ReaderPid = pid
                  WorkEndNs = budget.WorkEnd.Ticks
                  CleanupEndNs = budget.CleanupEnd.Ticks
                  CaptureStartNs = capturedAt
                  CaptureReturnedNs = returnedAt
                  Phase = string model.Phase
                  FirstFailure = (match model.FirstFailure with Some failure -> caseName failure | None -> "None")
                  SecondaryFailures = model.SecondaryFailures |> List.map caseName |> Array.ofList
                  Settlement = string observation.Settlement
                  DocumentCount = observation.Documents.Length
                  PendingActivity = ownedAtReturn.PendingActivity
                  RetainedDescriptors = ownedAtReturn.RetainedDescriptors
                  CloseUncertain = ownedAtReturn.CloseUncertain
                  ReleaseAfterReturn = describeRelease releasedAtReturn
                  FinalRelease = finalRelease
                  FinalPendingActivity = finalPending
                  ModelDetail = (sprintf "%A" model).Replace("\n", " ") }
            writeText (Path.Combine(ctl, "reader-returned.json")) (toJson (result "" ownedAtReturn.PendingActivity))
            // Stay alive as the owner until the driver has released the injected cause, polling the public
            // release the way a caller must. The process exit is what finally frees any leaked descriptor.
            let exitFile = Path.Combine(ctl, "reader-exit")
            let wait = Stopwatch.StartNew()
            while not (File.Exists exitFile) && wait.ElapsedMilliseconds < 120000L do
                (release session |> ignore)
                Thread.Sleep 50
            let finalRelease = describeRelease (release session)
            let finalPending = (ownership session).PendingActivity
            writeText (Path.Combine(ctl, "reader-final.json")) (toJson (result finalRelease finalPending))
            0

// ---------------------------------------------------------------- census

let private fixtureToken (outDir: string) = outDir

let private listDir (path: string) = try Directory.GetDirectories path |> Array.choose (fun entry -> Path.GetFileName entry |> Option.ofObj) |> Array.sort with _ -> [||]

/// Look at the kernel's own tables. `outDir` is the unique per-run token carried on every fixture
/// process command line and in every fixture mount, dm and loop name.
let takeCensus (outDir: string) (baseline: string[] option) : Census =
    let token = fixtureToken outDir
    let self = Process.GetCurrentProcess().Id
    // As pid 1 of the private pid namespace every visible task is the fixture's: no command-line filter.
    let everything = self = 1
    let dStates = ResizeArray<string>()
    let processes = ResizeArray<string>()
    for procDir in (try Directory.GetDirectories "/proc" with _ -> [||]) do
        let name = Path.GetFileName procDir
        match Int32.TryParse name with
        | true, pid ->
            let cmdline = try (File.ReadAllText(procDir + "/cmdline")).Replace('\000', ' ').Trim() with _ -> ""
            let ours = everything || cmdline.Contains token
            if (everything || cmdline.Contains token) && pid <> self then processes.Add(sprintf "%d %s" pid cmdline)
            for taskDir in (try Directory.GetDirectories(procDir + "/task") with _ -> [||]) do
                try
                    let stat = File.ReadAllText(taskDir + "/stat")
                    if stat.Substring(stat.LastIndexOf ')' + 2, 1) = "D" && ours then
                        dStates.Add(sprintf "%d/%s %s" pid (Path.GetFileName taskDir) cmdline)
                with _ -> ()
        | _ -> ()
    let mounts =
        (try File.ReadAllLines "/proc/self/mountinfo" with _ -> [||])
        |> Array.filter (fun entry -> entry.Contains token || entry.Contains "fsgg423b")
    let _, dm, _ = runPrivileged "dmsetup" [ "ls" ] 5000
    let dmDevices = dm.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.filter (fun entry -> entry.StartsWith "fsgg423b-")
    let _, loops, _ = runTool "losetup" [ "-a" ] 5000
    let loopDevices = loops.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.filter (fun entry -> entry.Contains token)
    let connections = listDir "/sys/fs/fuse/connections"
    let inflight =
        [| for block in (try Directory.GetDirectories "/sys/block" with _ -> [||]) do
               if (Path.GetFileName block |> Option.ofObj |> Option.exists (fun n -> n.StartsWith "dm-")) then
                   let name = readOrEmpty (block + "/dm/name")
                   let busy = (readOrEmpty (block + "/inflight")).Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                   if name.StartsWith "fsgg423b-" && busy |> Array.exists (fun count -> count <> "0") then
                       yield sprintf "%s %s inflight=%s" (Path.GetFileName block |> Option.ofObj |> Option.defaultValue "") name (String.Join(" ", busy)) |]
    { DStateTasks = dStates.ToArray()
      FixtureProcesses = processes.ToArray()
      FixtureMounts = mounts
      DmDevices = dmDevices
      LoopDevices = loopDevices
      FuseConnections = connections
      FuseConnectionsBaseline = defaultArg baseline connections
      InflightIo = inflight }

// ---------------------------------------------------------------- scenarios

type Scenario =
    { Name: string
      Backend: string
      Hold: bool
      FlushErrno: int
      WorkSeconds: float
      CleanupSeconds: float
      /// None: release after capture returned. Some ms: release this long after work end, inside the cleanup window.
      ReleaseInsideCleanupAfterMs: int option
      /// Janitor control only: the driver (pid 1) exits abruptly once the read is parked, skipping all teardown.
      CrashWhenParked: bool }

let private scenario name backend hold flush work cleanup release crash =
    { Name = name; Backend = backend; Hold = hold; FlushErrno = flush; WorkSeconds = work; CleanupSeconds = cleanup
      ReleaseInsideCleanupAfterMs = release; CrashWhenParked = crash }
let scenarios: Scenario list =
    [ scenario "normal-file" "plain" false 0 20.0 20.0 None false
      scenario "fuse-blocked-read" "fuse" true 0 3.0 3.0 None false
      scenario "fuse-uncertain-close" "fuse" true EIO 4.0 30.0 (Some 1500) false
      scenario "fuse-control-no-block" "fuse" false 0 20.0 20.0 None false
      scenario "block-blocked-read" "block" true 0 3.0 3.0 None false
      scenario "block-control-no-block" "block" false 0 20.0 20.0 None false
      scenario "block-abnormal-exit" "block" true 0 3.0 3.0 None true ]

// ---------------------------------------------------------------- driver (pid 1 of the private namespace)

let private spawn (role: string) (args: string list) : Process =
    let psi = ProcessStartInfo(processPath, UseShellExecute = false)
    for arg in [ Reflection.Assembly.GetExecutingAssembly().Location; "--physical-fixture"; role ] @ args do psi.ArgumentList.Add arg
    psi.Environment["DOTNET_EnableDiagnostics"] <- "0"
    match Process.Start psi with
    | null -> failwith ("could not start fixture role " + role)
    | proc -> proc

let private readJsonLines<'a when 'a: not null> (path: string) : 'a[] =
    let result = ResizeArray<'a>()
    try
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        use reader = new StreamReader(stream)
        let mutable reading = true
        while reading do
            match reader.ReadLine() with
            | null -> reading <- false
            | current ->
                if current.Trim().StartsWith "{" then
                    (try
                        match JsonSerializer.Deserialize<'a>(current) with
                        | null -> ()
                        | value -> result.Add value
                     with _ -> ())
    with _ -> ()
    result.ToArray()

let private emptyReader: ReaderResult =
    { ReaderPid = 0; WorkEndNs = 0L; CleanupEndNs = 0L; CaptureStartNs = 0L; CaptureReturnedNs = 0L; Phase = "NoReader"
      FirstFailure = "NoReader"; SecondaryFailures = [||]; Settlement = "NoReader"; DocumentCount = 0; PendingActivity = false
      RetainedDescriptors = 0; CloseUncertain = false; ReleaseAfterReturn = ""; FinalRelease = ""; FinalPendingActivity = false; ModelDetail = "" }

let private diskBytes = 32L * 1024L * 1024L
let private sectors = diskBytes / 512L

/// argv: scenarioName outDir head tree dirty
let runScenario (argv: string[]) : int =
    let sc = scenarios |> List.find (fun s -> s.Name = argv[0])
    let outDir = argv[1]
    let head, tree, dirty = argv[2], argv[3], argv[4] = "1"
    // Internal deadlines are budgeted below the orchestrator's 180 s hard kill: the working phase may use
    // 75 s, teardown waits stop at 150 s and any tool is cut off by 155 s; the census adds two 5 s tools.
    let clock = Stopwatch.StartNew()
    let leftMs (limitSeconds: int) = max 1000 (limitSeconds * 1000 - int clock.ElapsedMilliseconds)
    let waitB (ms: int) condition = waitUntil (min ms (leftMs 75)) condition
    let waitT (ms: int) condition = waitUntil (min ms (leftMs 150)) condition
    let ctl = Path.Combine(outDir, "ctl")
    Directory.CreateDirectory ctl |> ignore
    let steps = ResizeArray<Step>()
    let dmEvents = ResizeArray<DmEvent>()
    let step name ok detail =
        steps.Add { StepAtNs = monoNs (); StepName = name; StepOk = ok; StepDetail = detail }
    let dmEvent action (code, out: string, err: string) =
        dmEvents.Add { DmAtNs = monoNs (); DmAction = action; DmExit = code; DmDetail = (out + " " + err).Trim() }
        code = 0
    let tool exe args = runPrivileged exe args (min 20000 (leftMs 155))
    let spawned = ResizeArray<Process>()
    let tag = Guid.NewGuid().ToString("N").Substring(0, 8)
    let dmName = "fsgg423b-" + tag
    let mnt = Path.Combine(outDir, "mnt")
    let plain = Path.Combine(outDir, "plain")
    let host = Path.Combine(outDir, "host")
    let daemonLog = Path.Combine(outDir, "daemon.jsonl")
    let observerLog = Path.Combine(outDir, "observer.jsonl")
    let policyPath = Path.Combine(host, "policy.json")
    let mutable loopDevice = ""
    let mutable minor = ""
    let mutable fileType = ""
    let mutable exited = false
    let mutable unmounted = false
    let mutable dmRemoved = false
    let mutable loopDetached = false
    let mutable causeReleased = false

    let abortConnection () =
        if minor <> "" then
            try
                File.WriteAllText(sprintf "/sys/fs/fuse/connections/%s/abort" minor, "1")
                true, "abort written for connection " + minor
            with ex -> false, ex.GetType().Name + ": " + ex.Message
        else false, "no connection"
    let releaseDm () =
        let table = sprintf "0 %d error" sectors
        let loaded = tool "dmsetup" [ "load"; "--noudevsync"; dmName; "--table"; table ] |> dmEvent "load-error-table"
        let resumed = tool "dmsetup" [ "resume"; "--noudevsync"; dmName ] |> dmEvent "release"
        loaded && resumed
    let releaseCause () =
        if causeReleased then true, "already released"
        else
            let ok, detail =
                match sc.Backend with
                | "fuse" when sc.Hold ->
                    (match sc.ReleaseInsideCleanupAfterMs with
                     | Some _ -> true, "READ was released by the daemon release file inside the cleanup window"
                     | None -> abortConnection ())
                | "block" when sc.Hold -> releaseDm (), "table swapped to error and resumed"
                | _ -> true, "no block was injected"
            // Only a release that succeeded counts: the failsafe retries an unreleased cause.
            if ok then causeReleased <- true
            ok, detail
    let tearDownMountAndDevices () =
        if not unmounted && (sc.Backend = "fuse" || sc.Backend = "block") then
            let code, _, err = runPrivileged "umount" [ mnt ] (min 20000 (leftMs 155))
            if code <> 0 then
                let lazyCode, _, lazyErr = runPrivileged "umount" [ "-l"; mnt ] (min 20000 (leftMs 155))
                step "unmount" false (sprintf "plain umount failed (%s); lazy umount rc=%d %s" err lazyCode lazyErr)
            else step "unmount" true "umount ok"
            unmounted <- true
        if sc.Backend = "block" then
            if not dmRemoved then
                let ok = tool "dmsetup" [ "remove"; "--retry"; "--noudevsync"; dmName ] |> dmEvent "remove"
                step "dm-remove" ok (dmName)
                dmRemoved <- true
            if not loopDetached && loopDevice <> "" then
                let code, _, err = tool "losetup" [ "-d"; loopDevice ]
                step "loop-detach" (code = 0) (loopDevice + " " + err)
                loopDetached <- true
    let killAll () =
        if not exited then
            writeText (Path.Combine(ctl, "observer-stop")) "stop"
            for proc in spawned do
                if not proc.HasExited then
                    (try proc.WaitForExit 1000 |> ignore with _ -> ())
            for proc in spawned do
                if not proc.HasExited then (try proc.Kill true with _ -> ())
            let allGone = spawned |> Seq.forall (fun proc -> (try proc.WaitForExit 4000 with _ -> false))
            step "kill-processes" allGone (sprintf "%d processes" spawned.Count)
            exited <- true

    // --- namespace and baseline
    runTool "mount" [ "--make-rprivate"; "/" ] 10000 |> ignore
    if sc.Backend = "fuse" then runTool "mount" [ "-t"; "fusectl"; "fusectl"; "/sys/fs/fuse/connections" ] 10000 |> ignore
    let before = takeCensus outDir None
    Directory.CreateDirectory host |> ignore
    writeText policyPath policyContent

    try
        // --- backend
        match sc.Backend with
        | "plain" ->
            Directory.CreateDirectory plain |> ignore
            writeText (Path.Combine(plain, "provenance.json")) provenanceContent
            fileType <- (let _, out, _ = runTool "stat" [ "-c"; "%F"; "--"; Path.Combine(plain, "provenance.json") ] 5000 in out.Trim())
        | "fuse" ->
            Directory.CreateDirectory mnt |> ignore
            let daemon = spawn "daemon" [ mnt; ctl; daemonLog; (if sc.Hold then "1" else "0"); string sc.FlushErrno ]
            spawned.Add daemon
            step "daemon-ready" (waitB 15000 (fun () -> File.Exists(Path.Combine(ctl, "ready")))) "FUSE INIT answered"
            for entry in File.ReadAllLines "/proc/self/mountinfo" do
                let fields = entry.Split ' '
                if fields.Length > 4 && fields[4] = mnt then minor <- fields[2].Split(':')[1]
            fileType <- (let _, out, _ = runTool "stat" [ "-c"; "%F"; "--"; Path.Combine(mnt, "provenance.json") ] 10000 in out.Trim())
        | _ ->
            Directory.CreateDirectory mnt |> ignore
            let stage = Path.Combine(outDir, "stage")
            Directory.CreateDirectory stage |> ignore
            let backing = Path.Combine(outDir, "backing.img")
            runTool "truncate" [ "-s"; string diskBytes; backing ] 10000 |> ignore
            let _, loop, _ = tool "losetup" [ "--find"; "--show"; backing ]
            loopDevice <- loop.Trim()
            step "loop-attach" (loopDevice <> "") loopDevice
            tool "mkfs.ext4" [ "-F"; "-q"; "-b"; "4096"; "-O"; "^has_journal"; loopDevice ] |> ignore
            tool "mount" [ loopDevice; stage ] |> ignore
            writeText (Path.Combine(stage, "provenance.json")) provenanceContent
            tool "sync" [] |> ignore
            tool "umount" [ stage ] |> ignore
            tool "dmsetup" [ "create"; "--noudevsync"; "--noudevrules"; dmName; "--table"; sprintf "0 %d delay %s 0 0" sectors loopDevice ]
            |> dmEvent "create" |> ignore
            tool "dmsetup" [ "mknodes"; dmName ] |> ignore
            tool "mount" [ "-o"; "ro,noatime,nodev,nosuid"; "/dev/mapper/" + dmName; mnt ] |> ignore
            // Cache the directory and inode so the first uncached access is the data read, not the lookup.
            fileType <- (let _, out, _ = runTool "stat" [ "-c"; "%F"; "--"; Path.Combine(mnt, "provenance.json") ] 10000 in out.Trim())
            runTool "ls" [ mnt ] 10000 |> ignore
            if sc.Hold then
                let ok = tool "dmsetup" [ "suspend"; "--noudevsync"; "--nolockfs"; "--noflush"; dmName ] |> dmEvent "suspend"
                let _, info, _ = tool "dmsetup" [ "info"; dmName ]
                dmEvents.Add { DmAtNs = monoNs (); DmAction = "info-after-suspend"; DmExit = 0; DmDetail = info.Replace("\n", " ") }
                step "inject-block" ok "dm device suspended"

        // --- reader + independent observer
        let root = if sc.Backend = "plain" then plain else mnt
        let reader =
            spawn "reader" [ root; "provenance.json"; policyPath; ctl; string sc.WorkSeconds; string sc.CleanupSeconds ]
        spawned.Add reader
        let pidFile = Path.Combine(ctl, "reader.pid")
        step "reader-started" (waitB 30000 (fun () -> File.Exists pidFile && readOrEmpty pidFile <> "")) "reader pid published"
        let readerPid = readOrEmpty pidFile
        if readerPid <> "" then
            let observer = spawn "observer" [ readerPid; observerLog; Path.Combine(ctl, "observer-stop") ]
            spawned.Add observer
            step "observer-started" (waitB 15000 (fun () -> File.Exists observerLog && FileInfo(observerLog).Length > 0L)) "first sample written"
        writeText (Path.Combine(ctl, "go")) "go"

        if sc.CrashWhenParked then
            // Janitor control: wait until the observer has seen the reader parked inside read(2) in D state,
            // then die without any teardown, as an OOM kill or a scope stop would.
            let parkedInRead () =
                readJsonLines<Sample> observerLog
                |> Array.exists (fun sample -> sample.State = "D" && (sample.Stack.Contains "ksys_read" || sample.Stack.Contains "vfs_read"))
            waitB 20000 parkedInRead |> ignore
            Sys.exitNow 70

        // --- wait for the read to reach the cause, optionally release inside the cleanup window
        if sc.Backend = "fuse" && sc.Hold then
            let sawRead () =
                readJsonLines<DaemonEvent> daemonLog
                |> Array.exists (fun event -> event.Event = "request" && event.Op = "READ" && Directory.Exists(sprintf "/proc/%s/task/%d" readerPid event.RequesterPid))
            step "read-reached-daemon" (waitB 20000 sawRead) "READ request from the production reader logged"
        match sc.ReleaseInsideCleanupAfterMs with
        | Some delayMs ->
            waitB 20000 (fun () -> File.Exists(Path.Combine(ctl, "reader-budget"))) |> ignore
            let fields = (readOrEmpty (Path.Combine(ctl, "reader-budget"))).Split ' '
            let workEnd = if fields.Length = 3 then int64 fields[1] else 0L
            waitB 20000 (fun () -> monoNs () >= workEnd + int64 delayMs * 1_000_000L) |> ignore
            writeText (Path.Combine(ctl, "release-read")) "release"
            causeReleased <- true
            step "release-cause" true (sprintf "READ released %dms after work end, inside the cleanup window" delayMs)
        | None -> ()
        let returned = Path.Combine(ctl, "reader-returned.json")
        let returnedInTime = waitB (int ((sc.WorkSeconds + sc.CleanupSeconds + 30.0) * 1000.0)) (fun () -> File.Exists returned)
        step "capture-returned" returnedInTime "production capture returned to its caller"

        // --- teardown
        if sc.ReleaseInsideCleanupAfterMs.IsNone then
            let ok, detail = releaseCause ()
            step "release-cause" ok detail
        writeText (Path.Combine(ctl, "reader-exit")) "exit"
        let finalPath = Path.Combine(ctl, "reader-final.json")
        let settled = waitT 15000 (fun () -> File.Exists finalPath) && reader.WaitForExit(min 15000 (leftMs 150))
        step "reader-settled" settled "reader published its final state and exited"
        if not settled then
            let ok, detail = abortConnection ()
            step "abort-retry" ok detail
        tearDownMountAndDevices ()
        killAll ()
    with ex -> step "driver-exception" false (ex.GetType().Name + ": " + ex.Message)

    // --- failsafe: never leave a mount, device or process, whatever happened above
    if not exited || not unmounted && sc.Backend <> "plain" then
        if sc.Backend = "fuse" then abortConnection () |> ignore
        if sc.Backend = "block" && sc.Hold && not causeReleased then releaseDm () |> ignore
        killAll ()
        tearDownMountAndDevices ()
    let inside = takeCensus outDir (Some before.FuseConnectionsBaseline)
    let observer = readJsonLines<Sample> observerLog
    let observerEnd = observer |> Array.tryFind (fun sample -> sample.State = "END") |> Option.map (fun sample -> sample.SampleAtNs) |> Option.defaultValue 0L
    let reader =
        let final = Path.Combine(ctl, "reader-final.json")
        let returned = Path.Combine(ctl, "reader-returned.json")
        if File.Exists final then ofJson<ReaderResult> (File.ReadAllText final)
        elif File.Exists returned then ofJson<ReaderResult> (File.ReadAllText returned)
        else emptyReader
    let _, kernel, _ = runTool "uname" [ "-r" ] 5000
    let evidence: Evidence =
        { Schema = schema
          Scenario = sc.Name
          Head = head
          Tree = tree
          Dirty = dirty
          Backend = sc.Backend
          InjectedCause = (causeFor sc.Name |> Option.defaultValue "")
          FieldIncident = false
          Kernel = kernel.Trim()
          FileType = fileType
          BlockInjected = sc.Hold
          Daemon = readJsonLines<DaemonEvent> daemonLog
          Observer = observer |> Array.filter (fun sample -> sample.State <> "END")
          ObserverEndNs = observerEnd
          Dm = dmEvents.ToArray()
          Reader = reader
          Teardown = steps.ToArray()
          CensusBefore = before
          CensusInside = inside }
    writeText (Path.Combine(outDir, "evidence.json")) (toJson evidence)
    match Environment.GetEnvironmentVariable "SUDO_UID", Environment.GetEnvironmentVariable "SUDO_GID" with
    | null, _ | _, null -> ()
    | uid, gid -> runTool "chown" [ "-R"; uid + ":" + gid; outDir ] 5000 |> ignore
    0

// ---------------------------------------------------------------- dispatch (called from Main)

let dispatch (argv: string[]) : int option =
    if argv.Length >= 2 && argv[0] = "--physical-fixture" then
        let rest = argv[2..]
        Some(
            match argv[1] with
            | "daemon" -> runDaemon rest
            | "observer" -> runObserver rest
            | "reader" -> runReader rest
            | "scenario" -> runScenario rest
            | _ -> 64
        )
    else None

// ---------------------------------------------------------------- orchestrator (unprivileged side)

/// Explicit opt-in. Scheduling only: the physical cases need sudo, /dev/fuse, dm_delay, loop and ext4.
let optedIn () = Environment.GetEnvironmentVariable "FSGG_GOV_PHYSICAL_FIXTURE" = "1"

let missingPrerequisites () : string list =
    [ if not (File.Exists "/dev/fuse") then yield "/dev/fuse is absent"
      let sudo, _, _ = runTool "sudo" [ "-n"; "true" ] 10000
      if sudo <> 0 then yield "passwordless sudo is unavailable"
      for tool in [ "unshare"; "timeout"; "dmsetup"; "losetup"; "mkfs.ext4"; "mount"; "umount"; "stat"; "truncate"; "chown" ] do
          let code, _, _ = runTool "which" [ tool ] 5000
          if code <> 0 then yield tool + " is not installed"
      let delay, _, _ = runTool "sudo" [ "-n"; "modprobe"; "dm_delay" ] 20000
      if delay <> 0 then yield "dm_delay module cannot be loaded" ]

type RunResult =
    { Evidence: Evidence
      OutsideBefore: Census
      Outside: Census
      ExitCode: int
      Stdout: string
      Stderr: string
      /// Janitor actions taken before and after this run (empty when nothing was left).
      JanitorBefore: string[]
      JanitorAfter: string[] }

let private hardTimeoutSeconds = 180

/// Restore a clean kernel state after an abnormal exit (OOM kill, scope stop, hang past the hard kill).
/// dm devices and loop devices are global kernel objects: nothing in a dead namespace removes them. For each
/// `fsgg423b-` dm device load the error table, resume (so parked I/O fails instead of waiting), remove it;
/// then detach every loop device backed under a `/fsgg423b-` scratch directory. Idempotent and bounded.
let janitor () : string[] =
    let actions = ResizeArray<string>()
    let say (text: string) = actions.Add text
    // Stray fixture processes first. The bracket keeps pkill from matching its own command line.
    let killed, _, _ = runPrivileged "pkill" [ "-KILL"; "-f"; "[-]-physical-fixture" ] 10000
    if killed = 0 then say "killed stray fixture processes"
    let _, listing, _ = runPrivileged "dmsetup" [ "ls" ] 10000
    let names =
        listing.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        |> Array.choose (fun entry ->
            let name = entry.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            if name.Length > 0 && name[0].StartsWith "fsgg423b-" then Some name[0] else None)
    for name in names do
        let table = sprintf "0 %d error" (32L * 1024L * 1024L / 512L)
        let loaded, _, _ = runPrivileged "dmsetup" [ "load"; "--noudevsync"; name; "--table"; table ] 20000
        let resumed, _, _ = runPrivileged "dmsetup" [ "resume"; "--noudevsync"; name ] 20000
        let removed = waitUntil 20000 (fun () -> let code, _, _ = runPrivileged "dmsetup" [ "remove"; "--retry"; "--noudevsync"; name ] 20000 in code = 0)
        say (sprintf "dm %s: load-error=%d resume=%d removed=%b" name loaded resumed removed)
    let _, loops, _ = runTool "losetup" [ "-a" ] 10000
    for entry in loops.Split('\n', StringSplitOptions.RemoveEmptyEntries) do
        if entry.Contains "/fsgg423b-" then
            let device = entry.Substring(0, entry.IndexOf ':')
            let detached = waitUntil 20000 (fun () -> let code, _, _ = runPrivileged "losetup" [ "-d"; device ] 10000 in code = 0)
            say (sprintf "loop %s detached=%b" device detached)
    actions.ToArray()

/// The checkout under test: commit, tree and whether it has uncommitted changes. Bound into the evidence.
let private checkoutIdentity () : string * string * bool =
    let rec findRoot (dir: string | null) =
        match dir with
        | null -> None
        | d when Directory.Exists(Path.Combine(d, ".git")) || File.Exists(Path.Combine(d, ".git")) -> Some d
        | d -> findRoot (Path.GetDirectoryName d)
    match findRoot (Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)) with
    | None -> "unknown", "unknown", true
    | Some root ->
        let git args = let code, out, _ = runTool "git" ([ "-C"; root ] @ args) 20000 in (if code = 0 then out.Trim() else "unknown")
        let status = git [ "status"; "--porcelain" ]
        git [ "rev-parse"; "HEAD" ], git [ "rev-parse"; "HEAD^{tree}" ], (status <> "")

let private scenarioCommand (name: string) (outDir: string) (killAfterSeconds: int) =
    let dll = Reflection.Assembly.GetExecutingAssembly().Location
    let head, tree, dirty = checkoutIdentity ()
    [ "-n"; "timeout"; "-s"; "KILL"; string killAfterSeconds
      "unshare"; "--mount"; "--pid"; "--fork"; "--kill-child"; "--mount-proc"; "--"
      "env"; "DOTNET_EnableDiagnostics=0"; "DOTNET_NOLOGO=1"
      processPath; dll; "--physical-fixture"; "scenario"; name; outDir; head; tree; (if dirty then "1" else "0") ]

let private removeTree (outDir: string) =
    let removed, _, _ = runTool "rm" [ "-rf"; "--"; outDir ] 30000
    if removed <> 0 then runTool "sudo" [ "-n"; "rm"; "-rf"; "--"; outDir ] 30000 |> ignore

/// Run one scenario inside `sudo unshare --mount --pid --fork --kill-child`. Exactly one bounded run, one at
/// a time (dm and loop devices are global). The janitor runs before the run and in a finally after it, and the
/// outside census is taken on every exit path, including a run that wrote no evidence.
let runPhysically (name: string) : RunResult =
    let janitorBefore = janitor ()
    let outDir = Directory.CreateTempSubdirectory("fsgg423b-").FullName
    let before = takeCensus outDir None
    let attempt =
        try
            let code, out, err = runTool "sudo" (scenarioCommand name outDir hardTimeoutSeconds) ((hardTimeoutSeconds + 30) * 1000)
            let evidencePath = Path.Combine(outDir, "evidence.json")
            if File.Exists evidencePath then Choice1Of2(ofJson<Evidence> (File.ReadAllText evidencePath), code, out, err)
            else Choice2Of2(sprintf "scenario %s produced no evidence (exit %d)\nstdout: %s\nstderr: %s" name code out err)
        with ex -> Choice2Of2(ex.GetType().Name + ": " + ex.Message)
    // Allow the kernel a moment to finish tearing the namespace down, then clean up whatever an abnormal exit left.
    waitUntil 15000 (fun () -> (takeCensus outDir (Some before.FuseConnectionsBaseline)).FixtureProcesses.Length = 0) |> ignore
    let leaked = takeCensus outDir (Some before.FuseConnectionsBaseline)
    let janitorAfter = janitor ()
    let outside = takeCensus outDir (Some before.FuseConnectionsBaseline)
    match attempt with
    | Choice2Of2 failure ->
        removeTree outDir
        failwithf "%s\njanitor after: %A\ncensus before janitor: %s\ncensus after janitor: %s" failure janitorAfter (toJson leaked) (toJson outside)
    | Choice1Of2(evidence, code, out, err) ->
        match Environment.GetEnvironmentVariable "FSGG_GOV_PHYSICAL_EVIDENCE_DIR" with
        | null | "" -> ()
        | keep ->
            Directory.CreateDirectory keep |> ignore
            File.Copy(Path.Combine(outDir, "evidence.json"), Path.Combine(keep, name + ".evidence.json"), true)
            File.WriteAllText(Path.Combine(keep, name + ".outside-census.json"), toJson outside)
        removeTree outDir
        { Evidence = evidence; OutsideBefore = before; Outside = outside; ExitCode = code; Stdout = out; Stderr = err
          JanitorBefore = janitorBefore; JanitorAfter = janitorAfter }

type AbnormalExitResult =
    { ExitCode: int
      /// True if the driver reached its evidence write, which an abrupt death must never do.
      EvidenceWritten: bool
      /// Census taken after the driver died, before any cleanup: what the abnormal exit left behind.
      Leaked: Census
      JanitorActions: string[]
      /// Census after the janitor ran.
      Restored: Census }

/// Janitor control: block mode, driver exits abruptly once the read is parked in D state. The test asserts
/// that something really leaked, then that the janitor restores a clean census.
let runAbnormalExit () : AbnormalExitResult =
    janitor () |> ignore
    let outDir = Directory.CreateTempSubdirectory("fsgg423b-").FullName
    let before = takeCensus outDir None
    // The dead pid 1 cannot finish exiting while its reader is parked on the suspended device, so the
    // command hangs until the kill; a short kill keeps the control quick. The janitor is what frees it.
    let code, _, _ = runTool "sudo" (scenarioCommand "block-abnormal-exit" outDir 40) 70000
    let evidenceWritten = File.Exists(Path.Combine(outDir, "evidence.json"))
    Thread.Sleep 1000
    let leaked = takeCensus outDir (Some before.FuseConnectionsBaseline)
    let actions = janitor ()
    waitUntil 15000 (fun () -> (takeCensus outDir (Some before.FuseConnectionsBaseline)).FixtureProcesses.Length = 0) |> ignore
    let restored = takeCensus outDir (Some before.FuseConnectionsBaseline)
    removeTree outDir
    { ExitCode = code; EvidenceWritten = evidenceWritten; Leaked = leaked; JanitorActions = actions; Restored = restored }
