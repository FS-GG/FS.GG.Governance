module FS.GG.Governance.GateExecution.Tests.Support

open System
open System.IO
open System.Text
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.GateExecution.Model

// Shared builders + the TWO test surfaces (Principle IV) for the F051 tests. Every value below is a real,
// literally-constructible typed value — never a mock (Principle V). The PURE-GIVEN-THE-PORT side drives
// `senseExecution` through a deterministic FAKE port (no process at all); the EDGE side drives `realPort`
// against REAL `/bin/sh` temp-script fixtures (mirroring the `Snapshot` tests' real `git`). Output digests
// in assertions are DERIVED from real captured bytes (`ExecutionRecord.digestOf`), never `OutputDigest`
// literals. No network, no governed repository anywhere (SC-007).

// ── (1) The fake port (the PURE-GIVEN-THE-PORT side) ──

/// A deterministic fake `ExecutionPort` that yields a literal `ExecutionOutcome` REGARDLESS of the command —
/// so `senseExecution` can be driven with NO process at all. The bytes/exit/duration are whatever the test
/// supplies, never sensed.
let fakePort (stdout: byte[]) (stderr: byte[]) (exitCode: ExitCode) (duration: SensedDuration) : ExecutionPort =
    fun _command ->
        {
            Stdout = stdout
            Stderr = stderr
            ExitCode = exitCode
            Duration = duration
        }

// ── (2) The GateCommand builder (per-field overrides for single-fact perturbation) ──

/// A non-trivial environment delta with all THREE classes populated (so carriage of every class is
/// observable, and a `Changed` entry is never split into `Added` + `Removed`).
let baseEnv: EnvironmentDelta =
    {
        Added =
            [
                {
                    Name = EnvVarName "FSGG_ADDED"
                    Value = EnvVarValue "1"
                }
            ]
        Changed =
            [
                {
                    Name = EnvVarName "FSGG_CHANGED"
                    Old = EnvVarValue "old"
                    New = EnvVarValue "new"
                }
            ]
        Removed =
            [
                {
                    Name = EnvVarName "FSGG_REMOVED"
                    Old = EnvVarValue "gone"
                }
            ]
    }

/// Build a `GateCommand` with sensible defaults and a per-field override for EVERY reproducible fact, so a
/// test can perturb exactly one of them (executable, an argument, argument ORDER, working dir, a single
/// env-delta entry per class, timeout, captured-output target).
type Build =
    static member command
        (
            ?executable: string,
            ?arguments: Argument list,
            ?workingDirectory: string,
            ?environment: EnvironmentDelta,
            ?timeout: int,
            ?capturedOutput: CapturedOutput
        ) : GateCommand =
        {
            Executable = Executable(defaultArg executable "/bin/echo")
            Arguments = defaultArg arguments [ Argument "alpha"; Argument "beta" ]
            WorkingDirectory = WorkingDirectory(defaultArg workingDirectory "/tmp")
            Environment = defaultArg environment baseEnv
            Timeout = TimeoutLimit(defaultArg timeout 30)
            CapturedOutput = defaultArg capturedOutput NoCapturedOutput
        }

/// The base command — every reproducible fact present and distinct so a single-field perturbation is
/// unambiguous (used by the duration-invariance / sensitivity tests via the fake port).
let baseCommand: GateCommand = Build.command ()

// ── (3) Real `/bin/sh` temp-script fixtures (the EDGE side) ──

/// A real-edge fixture: the command to run plus the bytes/exit the fixture is built to produce, so the
/// edge tests can assert `record.Reproducible.StdoutDigest = ExecutionRecord.digestOf ExpectedStdout`.
type ScriptFixture =
    {
        Command: GateCommand
        ExpectedStdout: byte[]
        ExpectedStderr: byte[]
        ExpectedExit: int
    }

/// Create a disposable temp dir, run `body` against its path, then delete it. No network, no governed repo.
let withTempDir (body: string -> 'a) : 'a =
    let dir =
        Path.Combine(Path.GetTempPath(), "fsgg-gateexec-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory dir |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

/// Write a `/bin/sh` script into `dir` and return a `GateCommand` running `/bin/sh <script>` with the given
/// timeout. The script need not be executable — `sh` reads it as a file. WorkingDirectory is the temp dir.
let private scriptCommand (dir: string) (scriptBody: string) (timeoutSeconds: int) : GateCommand =
    let scriptPath = Path.Combine(dir, "gate.sh")
    File.WriteAllText(scriptPath, scriptBody)

    {
        Executable = Executable "/bin/sh"
        Arguments = [ Argument scriptPath ]
        WorkingDirectory = WorkingDirectory dir
        Environment =
            {
                Added = []
                Changed = []
                Removed = []
            }
        Timeout = TimeoutLimit timeoutSeconds
        CapturedOutput = NoCapturedOutput
    }

/// A clean gate: prints KNOWN, DISTINCT bytes to stdout and stderr, exits 0.
let cleanFixture (dir: string) : ScriptFixture =
    let body =
        "printf '%s' 'stdout-content'\nprintf '%s' 'stderr-detail' 1>&2\nexit 0\n"

    {
        Command = scriptCommand dir body 30
        ExpectedStdout = Encoding.UTF8.GetBytes "stdout-content"
        ExpectedStderr = Encoding.UTF8.GetBytes "stderr-detail"
        ExpectedExit = 0
    }

/// The SAME clean gate but with stdout/stderr SWAPPED — used to prove the two digest positions are not
/// interchangeable (a swapped fixture assembles to a different record).
let swappedFixture (dir: string) : ScriptFixture =
    let body =
        "printf '%s' 'stderr-detail'\nprintf '%s' 'stdout-content' 1>&2\nexit 0\n"

    {
        Command = scriptCommand dir body 30
        ExpectedStdout = Encoding.UTF8.GetBytes "stderr-detail"
        ExpectedStderr = Encoding.UTF8.GetBytes "stdout-content"
        ExpectedExit = 0
    }

/// A failing gate: writes output, then exits 7 (a non-zero exit is RECORDED, not rejected).
let exit7Fixture (dir: string) : ScriptFixture =
    let body =
        "printf '%s' 'failing-stdout'\nprintf '%s' 'failure-detail' 1>&2\nexit 7\n"

    {
        Command = scriptCommand dir body 30
        ExpectedStdout = Encoding.UTF8.GetBytes "failing-stdout"
        ExpectedStderr = Encoding.UTF8.GetBytes "failure-detail"
        ExpectedExit = 7
    }

/// An overrunning gate: writes a little, then sleeps FAR past a short (1s) `TimeoutLimit` — terminated and
/// recorded as `timeoutExitCode` within a bounded time. (The partial-output capture is racy, so the timeout
/// test asserts the exit/bound, not exact bytes.)
let timeoutFixture (dir: string) : ScriptFixture =
    let body =
        "printf '%s' 'before-sleep'\nsleep 30\nprintf '%s' 'after-sleep'\nexit 0\n"

    {
        Command = scriptCommand dir body 1
        ExpectedStdout = Encoding.UTF8.GetBytes "before-sleep"
        ExpectedStderr = [||]
        ExpectedExit = 124
    }

/// A gate that exits 0 PROMPTLY but leaves a backgrounded child (`sleep`) holding the redirected pipes open
/// (the child inherits fd 1/2, so the write ends never close until it ends). The main process's clean exit
/// must NOT make the port block on the never-EOF streams (M-CORE-2 / H2): the post-exit drain is BOUNDED, so
/// the port returns well before the child ends. Partial capture is best-effort here (like the timeout case),
/// so the matching test asserts the bound + clean exit, not the bytes.
let pipeHoldingChildFixture (dir: string) : ScriptFixture =
    let body =
        "printf '%s' 'stdout-content'\nprintf '%s' 'stderr-detail' 1>&2\nsleep 30 &\nexit 0\n"

    {
        Command = scriptCommand dir body 30
        ExpectedStdout = [||]
        ExpectedStderr = [||]
        ExpectedExit = 0
    }

/// An empty gate: no output at all, exits 0 (the empty-bytes digest is an ordinary value, SC-008).
let emptyFixture (dir: string) : ScriptFixture =
    let body = "exit 0\n"

    {
        Command = scriptCommand dir body 30
        ExpectedStdout = [||]
        ExpectedStderr = [||]
        ExpectedExit = 0
    }

/// A binary gate: emits raw, NON-UTF-8 bytes by `cat`-ing a file written with known bytes (robust against
/// shell `printf` escape differences). Captured verbatim — no decoding/normalization (FR-002, SC-008).
let binaryFixture (dir: string) : ScriptFixture =
    let bytes: byte[] = [| 0uy; 255uy; 254uy; 1uy; 0uy; 128uy; 0uy; 13uy; 10uy |]
    let binPath = Path.Combine(dir, "payload.bin")
    File.WriteAllBytes(binPath, bytes)
    let body = sprintf "cat '%s'\nexit 0\n" binPath

    {
        Command = scriptCommand dir body 30
        ExpectedStdout = bytes
        ExpectedStderr = [||]
        ExpectedExit = 0
    }

/// A large gate (~1 MB): `cat`-s a 1 MB file — captured and digested IN FULL, no truncation (SC-008).
let largeFixture (dir: string) : ScriptFixture =
    let bytes: byte[] = Array.init 1_000_000 (fun i -> byte (i % 251))
    let binPath = Path.Combine(dir, "large.bin")
    File.WriteAllBytes(binPath, bytes)
    let body = sprintf "cat '%s'\nexit 0\n" binPath

    {
        Command = scriptCommand dir body 30
        ExpectedStdout = bytes
        ExpectedStderr = [||]
        ExpectedExit = 0
    }

/// A fast command (`/bin/echo`) carrying a HUGE `TimeoutLimit` (`Int32.MaxValue` seconds). Under the old
/// wait math `seconds * 1000` overflowed int32 to a NEGATIVE wait, which `WaitForExit` rejected — the throw
/// was then misreported as a `startFailure` while the started process leaked (#56/B3). Used to prove the
/// clamp: the command still runs promptly and records its real exit code (0).
let hugeTimeoutFastCommand () : GateCommand =
    Build.command (executable = "/bin/echo", arguments = [ Argument "ok" ], timeout = System.Int32.MaxValue)

/// A GateCommand naming a GUARANTEED-MISSING executable — the start-failure case (no script, no dir state).
let missingExecutableCommand () : GateCommand =
    {
        Executable = Executable "/nonexistent/fsgg-definitely-not-a-real-binary-xyz"
        Arguments = [ Argument "irrelevant" ]
        WorkingDirectory = WorkingDirectory(Path.GetTempPath())
        Environment =
            {
                Added = []
                Changed = []
                Removed = []
            }
        Timeout = TimeoutLimit 30
        CapturedOutput = NoCapturedOutput
    }

// ── (4) Real freshness-world builders (the F029/F030 worked example, for close-the-loop only) ──

/// A complete, literal `FreshnessInputs` for `check` — every category present and distinct so a mismatch is
/// observable, with a multi-element verbatim `CoveredArtifacts` list.
let inputs (check: string) : FreshnessInputs =
    {
        Check = CheckId check
        Domain = DomainId "build"
        Command = Some(CommandId "gate")
        Environment = Local
        RuleHash = RuleHash "r1"
        CoveredArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1" ]
        CommandVersion = Some(CommandVersion "1.0")
        GeneratorVersion = GeneratorVersion "g1"
        Base = Revision "aaa"
        Head = Revision "bbb"
    }

/// A DIFFERENT freshness world (the head revision moved) — for the recompute-safety / no-spurious-match check.
let differentInputs: FreshnessInputs =
    { inputs "build:tests" with
        Head = Revision "ccc"
    }

// ── (5) FsCheck generators (real values, no mocks) ──

let private genBytes: Gen<byte[]> =
    Gen.sized (fun n ->
        Gen.listOfLength (max 0 (n % 64)) (ArbMap.defaults |> ArbMap.generate<byte>)
        |> Gen.map List.toArray)

let private shortStringGen: Gen<string> =
    Gen.elements [ ""; "a"; "/bin/echo"; "/bin/sh"; "alpha"; "beta"; "/tmp"; "héllo"; "x:y=z" ]

let private genEnvironmentDelta: Gen<EnvironmentDelta> =
    gen {
        let! added = Gen.listOf (Gen.zip shortStringGen shortStringGen)
        let! changed = Gen.listOf (Gen.zip shortStringGen shortStringGen)
        let! removed = Gen.listOf (Gen.zip shortStringGen shortStringGen)

        return
            {
                Added =
                    added
                    |> List.map (fun (n, v) ->
                        {
                            Name = EnvVarName n
                            Value = EnvVarValue v
                        })
                Changed =
                    changed
                    |> List.map (fun (n, v) ->
                        {
                            Name = EnvVarName n
                            Old = EnvVarValue v
                            New = EnvVarValue(v + "!")
                        })
                Removed =
                    removed
                    |> List.map (fun (n, v) ->
                        {
                            Name = EnvVarName n
                            Old = EnvVarValue v
                        })
            }
    }

let private genCapturedOutput: Gen<CapturedOutput> =
    Gen.oneof
        [
            Gen.constant NoCapturedOutput
            shortStringGen |> Gen.map (fun p -> CapturedAt(CapturedOutputPath p))
        ]

/// An arbitrary well-typed `GateCommand` — varying every reproducible fact.
let private genCommand: Gen<GateCommand> =
    gen {
        let! exe = shortStringGen
        let! args = Gen.listOf shortStringGen
        let! cwd = shortStringGen
        let! env = genEnvironmentDelta
        let! timeout = Gen.choose (0, 600)
        let! captured = genCapturedOutput

        return
            Build.command (
                executable = exe,
                arguments = (args |> List.map Argument),
                workingDirectory = cwd,
                environment = env,
                timeout = timeout,
                capturedOutput = captured
            )
    }

/// An arbitrary well-typed `ExecutionOutcome` — both raw byte buffers, exit code, sensed duration.
let private genOutcome: Gen<ExecutionOutcome> =
    gen {
        let! out = genBytes
        let! err = genBytes
        let! exit = Gen.choose (-1, 255)
        let! d = Gen.choose (0, 1_000_000_000)

        return
            {
                Stdout = out
                Stderr = err
                ExitCode = ExitCode exit
                Duration = SensedDuration(int64 d)
            }
    }

type Generators =
    static member Bytes() : Arbitrary<byte[]> = Arb.fromGen genBytes
    static member Command() : Arbitrary<GateCommand> = Arb.fromGen genCommand
    static member Outcome() : Arbitrary<ExecutionOutcome> = Arb.fromGen genOutcome

/// FsCheck config registering the real F051 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<Generators> ]
    }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot


// New bounded-path fixtures: an explicit test-only owner retains every session until direct
// settlement. Unsettled sessions remain strongly held through test-runner life, including failures.
// This is not production admission or a cleanup receipt. Native selection must additionally retain
// the actual independent outer runner/group owner; disposable commands below finish themselves.
let retainedBoundedFixtures = System.Collections.Concurrent.ConcurrentDictionary<Guid, FS.GG.Governance.GateExecution.Interpreter.ExecutionSession>()

let boundedRequest (dir: string) (python: string) (workMilliseconds: int) (cleanupMilliseconds: int) : BoundedRequest =
    let now = FS.GG.Governance.GateExecution.Interpreter.currentInstant ()
    let endAt milliseconds : MonotonicInstant =
        { now with Ticks = now.Ticks + int64 milliseconds * System.Diagnostics.Stopwatch.Frequency / 1000L }
    { Command = Build.command(executable="/usr/bin/python3", arguments=[Argument "-c"; Argument python],
                              workingDirectory=dir, environment={Added=[];Changed=[];Removed=[]}, timeout=10)
      Identity = {Operation="test-bounded-direct-child";Launch=Guid.NewGuid().ToString("N")}
      Policy = {Root=dir;CapturedEnvironment=Map.empty;Descendants=AcceptUncontainedUnobservedDescendants
                Paths=AcceptObservedPathsWithoutAtomicBinding}
      Budget = {WorkEnd=endAt workMilliseconds;CleanupEnd=endAt cleanupMilliseconds}
      Limits = {StdoutBytes=4096L;StderrBytes=4096L;AggregateBytes=8192L}
      Cancellation = System.Threading.CancellationToken.None }

let acquireBoundedFixture request =
    match FS.GG.Governance.GateExecution.Interpreter.prepare request with
    | Error causes -> failtestf "bounded fixture preparation refused: %A" causes
    | Ok session ->
        let owner = Guid.NewGuid()
        retainedBoundedFixtures.[owner] <- session
        owner, session

let awaitBoundedFixture (owner: Guid) (session: FS.GG.Governance.GateExecution.Interpreter.ExecutionSession) =
    // Observation of the original owner grants no execution or cleanup time. The self-finishing
    // fixture's independent outer recipe reserves this later observation, not a component retry.
    let wait = System.Diagnostics.Stopwatch.StartNew()
    let mutable observed = FS.GG.Governance.GateExecution.Interpreter.observe session
    while observed.Settlement <> Released && wait.Elapsed.TotalSeconds < 4.0 do
        System.Threading.Thread.Sleep 5
        observed <- FS.GG.Governance.GateExecution.Interpreter.observe session
    if observed.Settlement <> Released then
        // Unknown ownership must not be dropped merely because an assertion finished. Keep
        // the actual caller/runner alive for the separately selected outer supervisor to retire.
        // This backstop grants no component time and cannot produce a passing fixture result.
        while observed.Settlement <> Released do
            System.Threading.Thread.Sleep 20
            observed <- FS.GG.Governance.GateExecution.Interpreter.observe session
    let mutable removed = session
    retainedBoundedFixtures.TryRemove(owner, &removed) |> ignore
    observed

let withBoundedFixture request body =
    let owner, session = acquireBoundedFixture request
    try body session
    finally awaitBoundedFixture owner session |> ignore

// A control channel joins observations to an independently admitted private guardian. These
// environment values schedule/select a fixture; they are not custody or execution authority.
let boundedFixtureControl () =
    let required name =
        match System.Environment.GetEnvironmentVariable(name) |> Option.ofObj with
        | Some value when not (String.IsNullOrWhiteSpace value) -> value
        | _ -> failtest "actual bounded fixture control directory and operation are required"
    let directory = required "FSGG_BOUNDED_FIXTURE_CONTROL"
    let operation = required "FSGG_BOUNDED_FIXTURE_OPERATION"
    if not (System.IO.Path.IsPathFullyQualified directory) || not (System.IO.Directory.Exists directory) then
        failtest "actual absolute bounded fixture control directory is required"
    directory, operation

let publishBoundedFixtureControl (directory: string) (operation: string) (launch: string) (kind: string) (detail: string) =
    let observation =
        {| operation = operation; launch = launch; kind = kind; detail = detail
           ownerPid = System.Environment.ProcessId |}
    let text = System.Text.Json.JsonSerializer.Serialize observation
    let destination = System.IO.Path.Combine(directory, kind + ".json")
    let staging = destination + ".pending"
    do
        use output = new System.IO.FileStream(staging, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None)
        let bytes = System.Text.Encoding.UTF8.GetBytes text
        output.Write(bytes, 0, bytes.Length)
        output.Flush(true)
    System.IO.File.Move(staging, destination)

let publishBoundedRunSnapshot directory operation launch kind (result: BoundedObservation) =
    // Deliberately omit captured bytes, environment, paths and exception payloads. Model causes
    // are already bounded codes; this selected fixture snapshot preserves actual observed facts.
    let detail =
        sprintf "launch=%A; directExit=%A; stdout=%A/%d; stderr=%A/%d; first=%A; secondary=%A; settlement=%A; descendants=%A"
            result.Launch result.DirectExit result.Stdout.State result.Stdout.ObservedBytes
            result.Stderr.State result.Stderr.ObservedBytes result.FirstFailure result.SecondaryFailures
            result.Settlement result.Descendants
    publishBoundedFixtureControl directory operation launch kind detail
