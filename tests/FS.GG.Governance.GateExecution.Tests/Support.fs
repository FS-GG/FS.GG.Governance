module FS.GG.Governance.GateExecution.Tests.Support

open System
open System.IO
open System.Text
open Expecto
open FsCheck
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
        { Stdout = stdout
          Stderr = stderr
          ExitCode = exitCode
          Duration = duration }

// ── (2) The GateCommand builder (per-field overrides for single-fact perturbation) ──

/// A non-trivial environment delta with all THREE classes populated (so carriage of every class is
/// observable, and a `Changed` entry is never split into `Added` + `Removed`).
let baseEnv: EnvironmentDelta =
    { Added = [ { Name = EnvVarName "FSGG_ADDED"; Value = EnvVarValue "1" } ]
      Changed = [ { Name = EnvVarName "FSGG_CHANGED"; Old = EnvVarValue "old"; New = EnvVarValue "new" } ]
      Removed = [ { Name = EnvVarName "FSGG_REMOVED"; Old = EnvVarValue "gone" } ] }

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
        { Executable = Executable(defaultArg executable "/bin/echo")
          Arguments = defaultArg arguments [ Argument "alpha"; Argument "beta" ]
          WorkingDirectory = WorkingDirectory(defaultArg workingDirectory "/tmp")
          Environment = defaultArg environment baseEnv
          Timeout = TimeoutLimit(defaultArg timeout 30)
          CapturedOutput = defaultArg capturedOutput NoCapturedOutput }

/// The base command — every reproducible fact present and distinct so a single-field perturbation is
/// unambiguous (used by the duration-invariance / sensitivity tests via the fake port).
let baseCommand: GateCommand = Build.command ()

// ── (3) Real `/bin/sh` temp-script fixtures (the EDGE side) ──

/// A real-edge fixture: the command to run plus the bytes/exit the fixture is built to produce, so the
/// edge tests can assert `record.Reproducible.StdoutDigest = ExecutionRecord.digestOf ExpectedStdout`.
type ScriptFixture =
    { Command: GateCommand
      ExpectedStdout: byte[]
      ExpectedStderr: byte[]
      ExpectedExit: int }

/// Create a disposable temp dir, run `body` against its path, then delete it. No network, no governed repo.
let withTempDir (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-gateexec-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try
        body dir
    finally
        try Directory.Delete(dir, true) with _ -> ()

/// Write a `/bin/sh` script into `dir` and return a `GateCommand` running `/bin/sh <script>` with the given
/// timeout. The script need not be executable — `sh` reads it as a file. WorkingDirectory is the temp dir.
let private scriptCommand (dir: string) (scriptBody: string) (timeoutSeconds: int) : GateCommand =
    let scriptPath = Path.Combine(dir, "gate.sh")
    File.WriteAllText(scriptPath, scriptBody)
    { Executable = Executable "/bin/sh"
      Arguments = [ Argument scriptPath ]
      WorkingDirectory = WorkingDirectory dir
      Environment = { Added = []; Changed = []; Removed = [] }
      Timeout = TimeoutLimit timeoutSeconds
      CapturedOutput = NoCapturedOutput }

/// A clean gate: prints KNOWN, DISTINCT bytes to stdout and stderr, exits 0.
let cleanFixture (dir: string) : ScriptFixture =
    let body = "printf '%s' 'stdout-content'\nprintf '%s' 'stderr-detail' 1>&2\nexit 0\n"
    { Command = scriptCommand dir body 30
      ExpectedStdout = Encoding.UTF8.GetBytes "stdout-content"
      ExpectedStderr = Encoding.UTF8.GetBytes "stderr-detail"
      ExpectedExit = 0 }

/// The SAME clean gate but with stdout/stderr SWAPPED — used to prove the two digest positions are not
/// interchangeable (a swapped fixture assembles to a different record).
let swappedFixture (dir: string) : ScriptFixture =
    let body = "printf '%s' 'stderr-detail'\nprintf '%s' 'stdout-content' 1>&2\nexit 0\n"
    { Command = scriptCommand dir body 30
      ExpectedStdout = Encoding.UTF8.GetBytes "stderr-detail"
      ExpectedStderr = Encoding.UTF8.GetBytes "stdout-content"
      ExpectedExit = 0 }

/// A failing gate: writes output, then exits 7 (a non-zero exit is RECORDED, not rejected).
let exit7Fixture (dir: string) : ScriptFixture =
    let body = "printf '%s' 'failing-stdout'\nprintf '%s' 'failure-detail' 1>&2\nexit 7\n"
    { Command = scriptCommand dir body 30
      ExpectedStdout = Encoding.UTF8.GetBytes "failing-stdout"
      ExpectedStderr = Encoding.UTF8.GetBytes "failure-detail"
      ExpectedExit = 7 }

/// An overrunning gate: writes a little, then sleeps FAR past a short (1s) `TimeoutLimit` — terminated and
/// recorded as `timeoutExitCode` within a bounded time. (The partial-output capture is racy, so the timeout
/// test asserts the exit/bound, not exact bytes.)
let timeoutFixture (dir: string) : ScriptFixture =
    let body = "printf '%s' 'before-sleep'\nsleep 30\nprintf '%s' 'after-sleep'\nexit 0\n"
    { Command = scriptCommand dir body 1
      ExpectedStdout = Encoding.UTF8.GetBytes "before-sleep"
      ExpectedStderr = [||]
      ExpectedExit = 124 }

/// An empty gate: no output at all, exits 0 (the empty-bytes digest is an ordinary value, SC-008).
let emptyFixture (dir: string) : ScriptFixture =
    let body = "exit 0\n"
    { Command = scriptCommand dir body 30
      ExpectedStdout = [||]
      ExpectedStderr = [||]
      ExpectedExit = 0 }

/// A binary gate: emits raw, NON-UTF-8 bytes by `cat`-ing a file written with known bytes (robust against
/// shell `printf` escape differences). Captured verbatim — no decoding/normalization (FR-002, SC-008).
let binaryFixture (dir: string) : ScriptFixture =
    let bytes: byte[] = [| 0uy; 255uy; 254uy; 1uy; 0uy; 128uy; 0uy; 13uy; 10uy |]
    let binPath = Path.Combine(dir, "payload.bin")
    File.WriteAllBytes(binPath, bytes)
    let body = sprintf "cat '%s'\nexit 0\n" binPath
    { Command = scriptCommand dir body 30
      ExpectedStdout = bytes
      ExpectedStderr = [||]
      ExpectedExit = 0 }

/// A large gate (~1 MB): `cat`-s a 1 MB file — captured and digested IN FULL, no truncation (SC-008).
let largeFixture (dir: string) : ScriptFixture =
    let bytes: byte[] = Array.init 1_000_000 (fun i -> byte (i % 251))
    let binPath = Path.Combine(dir, "large.bin")
    File.WriteAllBytes(binPath, bytes)
    let body = sprintf "cat '%s'\nexit 0\n" binPath
    { Command = scriptCommand dir body 30
      ExpectedStdout = bytes
      ExpectedStderr = [||]
      ExpectedExit = 0 }

/// A GateCommand naming a GUARANTEED-MISSING executable — the start-failure case (no script, no dir state).
let missingExecutableCommand () : GateCommand =
    { Executable = Executable "/nonexistent/fsgg-definitely-not-a-real-binary-xyz"
      Arguments = [ Argument "irrelevant" ]
      WorkingDirectory = WorkingDirectory(Path.GetTempPath())
      Environment = { Added = []; Changed = []; Removed = [] }
      Timeout = TimeoutLimit 30
      CapturedOutput = NoCapturedOutput }

// ── (4) Real freshness-world builders (the F029/F030 worked example, for close-the-loop only) ──

/// A complete, literal `FreshnessInputs` for `check` — every category present and distinct so a mismatch is
/// observable, with a multi-element verbatim `CoveredArtifacts` list.
let inputs (check: string) : FreshnessInputs =
    { Check = CheckId check
      Domain = DomainId "build"
      Command = Some(CommandId "gate")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "1.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

/// A DIFFERENT freshness world (the head revision moved) — for the recompute-safety / no-spurious-match check.
let differentInputs: FreshnessInputs = { inputs "build:tests" with Head = Revision "ccc" }

// ── (5) FsCheck generators (real values, no mocks) ──

let private genBytes: Gen<byte[]> =
    Gen.sized (fun n -> Gen.listOfLength (max 0 (n % 64)) Arb.generate<byte> |> Gen.map List.toArray)

let private shortStringGen: Gen<string> =
    Gen.elements [ ""; "a"; "/bin/echo"; "/bin/sh"; "alpha"; "beta"; "/tmp"; "héllo"; "x:y=z" ]

let private genEnvironmentDelta: Gen<EnvironmentDelta> =
    gen {
        let! added = Gen.listOf (Gen.zip shortStringGen shortStringGen)
        let! changed = Gen.listOf (Gen.zip shortStringGen shortStringGen)
        let! removed = Gen.listOf (Gen.zip shortStringGen shortStringGen)

        return
            { Added = added |> List.map (fun (n, v) -> { Name = EnvVarName n; Value = EnvVarValue v })
              Changed =
                changed
                |> List.map (fun (n, v) -> { Name = EnvVarName n; Old = EnvVarValue v; New = EnvVarValue(v + "!") })
              Removed = removed |> List.map (fun (n, v) -> { Name = EnvVarName n; Old = EnvVarValue v }) }
    }

let private genCapturedOutput: Gen<CapturedOutput> =
    Gen.oneof
        [ Gen.constant NoCapturedOutput
          shortStringGen |> Gen.map (fun p -> CapturedAt(CapturedOutputPath p)) ]

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
            { Stdout = out
              Stderr = err
              ExitCode = ExitCode exit
              Duration = SensedDuration(int64 d) }
    }

type Generators =
    static member Bytes() : Arbitrary<byte[]> = Arb.fromGen genBytes
    static member Command() : Arbitrary<GateCommand> = Arb.fromGen genCommand
    static member Outcome() : Arbitrary<ExecutionOutcome> = Arb.fromGen genOutcome

/// FsCheck config registering the real F051 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

// ── (6) Repo root (for the surface baseline path) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
