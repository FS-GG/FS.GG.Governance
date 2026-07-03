module FS.GG.Governance.ExecutionRecord.Tests.Support

open System
open System.IO
open System.Text
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.ExecutionRecord

// Shared REAL-input builders + FsCheck generators for the F050 tests (Principle V — every value below is a
// real, literally-constructible typed value, never a mock; no process is spawned, no I/O is touched). The
// DERIVED-not-synthetic discipline is the whole point of this row: every `OutputDigest` under test is
// `ExecutionRecord.digestOf realBytes` over a REAL `byte[]` buffer (never a synthetic `OutputDigest` literal).
// No filesystem/clock/process/network anywhere except the repo-root walk used by the surface baseline path.

// ── Real byte buffers (FR-002 / FR-003 / Edge cases) ──

/// A textual buffer and an INDEPENDENTLY-constructed equal-content twin (content agreement, SC-002/FR-002).
let bytesA: byte[] = Encoding.UTF8.GetBytes "build succeeded\n"
let bytesEqual: byte[] = Encoding.UTF8.GetBytes "build succeeded\n"

/// One byte CHANGED relative to `bytesA` (the trailing `\n` becomes `!\n` — same length region differs).
let bytesChanged: byte[] = Encoding.UTF8.GetBytes "build succeeded!\n"

/// `bytesA` with a single byte APPENDED, and with a single trailing byte REMOVED.
let bytesAdded: byte[] = Array.append bytesA [| 33uy |]
let bytesRemoved: byte[] = bytesA[.. bytesA.Length - 2]

/// A reordered pair — same multiset of bytes, different order (`[1;2;3]` vs `[3;2;1]`).
let bytesOrdered: byte[] = [| 1uy; 2uy; 3uy |]
let bytesReordered: byte[] = [| 3uy; 2uy; 1uy |]

/// The empty buffer (FR-003 totality + distinctness).
let bytesEmpty: byte[] = [||]

/// A binary / non-textual buffer — bytes that are not valid UTF-8 (Edge "Binary").
let bytesBinary: byte[] = [| 0uy; 255uy; 254uy; 1uy; 0uy; 128uy; 0uy |]

/// A large buffer (~1 MB) — totality over arbitrarily large input, no truncation (Edge "Large").
let bytesLarge: byte[] = Array.create 1_000_000 0uy

/// The fixed SHA-256-of-empty digest, lowercase hex — what `digestOf [||]` must equal (FR-003).
let emptySha256Hex: string =
    "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"

// ── A captured execution outcome every test varies from (the F032 worked example, RAW bytes) ──

/// The base environment delta — one added var, the other two classes empty.
let baseEnv: EnvironmentDelta =
    { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]
      Changed = []
      Removed = [] }

/// Assemble a real F032 `CommandRecord` through the PUBLIC `ExecutionRecord.recordOf` over RAW output bytes,
/// with sensible defaults and a per-field override for EVERY reproducible fact AND the one sensed duration — so
/// a test can perturb exactly one fact (executable, an argument, argument order, working dir, env delta,
/// timeout, exit code, a byte of stdout, a byte of stderr, captured-output outcome) or ONLY the
/// `SensedDuration`. Output positions are RAW `byte[]`; the digesting is `recordOf`'s job (the point of F050).
type Build =
    static member outcome
        (
            ?executable: string,
            ?arguments: Argument list,
            ?workingDirectory: string,
            ?environment: EnvironmentDelta,
            ?timeout: int,
            ?exitCode: int,
            ?stdout: byte[],
            ?stderr: byte[],
            ?capturedOutput: CapturedOutput,
            ?duration: int64
        ) : CommandRecord =
        ExecutionRecord.recordOf
            (Executable(defaultArg executable "gcc"))
            (defaultArg arguments [ Argument "-c"; Argument "main.c" ])
            (WorkingDirectory(defaultArg workingDirectory "/work"))
            (defaultArg environment baseEnv)
            (TimeoutLimit(defaultArg timeout 30))
            (ExitCode(defaultArg exitCode 0))
            (defaultArg stdout (Encoding.UTF8.GetBytes "out-bytes"))
            (defaultArg stderr (Encoding.UTF8.GetBytes "err-bytes"))
            (defaultArg capturedOutput NoCapturedOutput)
            (SensedDuration(defaultArg duration 123_456L))

/// The base outcome — every reproducible fact present and distinct so a single-field perturbation is unambiguous.
let baseOutcome: CommandRecord = Build.outcome ()

/// The SAME gate, identical in every reproducible fact (incl. output bytes), only with a different sensed
/// duration. Identity and reference MUST be byte-identical to `baseOutcome`'s (duration-invariance, FR-006).
let slowerOutcome: CommandRecord = Build.outcome (duration = 999_999L)

/// One representative single-fact perturbation per reproducible fact, each labelled and built through the public
/// `recordOf` so it differs from `baseOutcome` in EXACTLY the named reproducible fact (incl. one byte of either
/// output stream). Every one must yield an identity DIFFERENT from `baseOutcome` (FR-007 / SC-003). The
/// env-delta variant adds a var (compared as a SET); argument order is reversed (order is significant); the
/// captured-output variant flips `NoCapturedOutput` to a present path.
let reproducibleVariants: (string * CommandRecord) list =
    [ "executable", Build.outcome (executable = "clang")
      "argument value", Build.outcome (arguments = [ Argument "-c"; Argument "other.c" ])
      "argument order", Build.outcome (arguments = [ Argument "main.c"; Argument "-c" ])
      "working directory", Build.outcome (workingDirectory = "/elsewhere")
      "env delta set",
      Build.outcome (
          environment =
              { baseEnv with
                  Added = baseEnv.Added @ [ { Name = EnvVarName "X"; Value = EnvVarValue "2" } ] }
      )
      "timeout", Build.outcome (timeout = 60)
      "exit code", Build.outcome (exitCode = 1)
      "stdout byte", Build.outcome (stdout = Encoding.UTF8.GetBytes "out-bytez")
      "stderr byte", Build.outcome (stderr = Encoding.UTF8.GetBytes "err-bytez")
      "captured output", Build.outcome (capturedOutput = CapturedAt(CapturedOutputPath "x")) ]

/// Edge outcomes exercising totality (FR-008): empty stdout/stderr bytes, a non-zero exit code, an applied
/// timeout, and all three captured-output outcomes — `recordOf` is defined and never throws on every one.
let edgeOutcomes: (string * CommandRecord) list =
    [ "empty stdout", Build.outcome (stdout = [||])
      "empty stderr", Build.outcome (stderr = [||])
      "both empty", Build.outcome (stdout = [||], stderr = [||])
      "non-zero exit", Build.outcome (exitCode = 1)
      "applied timeout", Build.outcome (timeout = 0)
      "no captured output", Build.outcome (capturedOutput = NoCapturedOutput)
      "captured at empty path", Build.outcome (capturedOutput = CapturedAt(CapturedOutputPath ""))
      "captured at path x", Build.outcome (capturedOutput = CapturedAt(CapturedOutputPath "x")) ]

// ── Real freshness-world builders (the F029/F030 worked example, for close-the-loop only) ──

/// A complete, literal `FreshnessInputs` for `check` — every category present and distinct so a mismatch is
/// observable, with a multi-element verbatim `CoveredArtifacts` list.
let inputs (check: string) : FreshnessInputs =
    { Check = CheckId check
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

/// A DIFFERENT freshness world (the head revision moved) — for the recompute-safety / no-spurious-match tests.
let differentInputs: FreshnessInputs = { inputs "build:tests" with Head = Revision "ccc" }

// ── FsCheck generators (real values, no mocks) ──

/// Arbitrary `byte[]` (incl. empty), so the agreement/sensitivity/determinism properties range over real bytes.
let private genBytes: Gen<byte[]> =
    Gen.sized (fun n -> Gen.listOfLength (max 0 (n % 64)) (ArbMap.defaults |> ArbMap.generate<byte>) |> Gen.map List.toArray)

let private shortStringGen: Gen<string> =
    Gen.elements [ ""; "a"; "gcc"; "clang"; "main.c"; "-c"; "/work"; "héllo"; "x:y=z" ]

let private genEnvironmentDelta: Gen<EnvironmentDelta> =
    gen {
        let! added = Gen.listOf (Gen.zip shortStringGen shortStringGen)
        let! changed = Gen.listOf (Gen.zip shortStringGen shortStringGen)
        let! removed = Gen.listOf (Gen.zip shortStringGen shortStringGen)

        return
            { Added = added |> List.map (fun (n, v) -> { Name = EnvVarName n; Value = EnvVarValue v })
              Changed = changed |> List.map (fun (n, v) -> { Name = EnvVarName n; Old = EnvVarValue v; New = EnvVarValue v })
              Removed = removed |> List.map (fun (n, v) -> { Name = EnvVarName n; Old = EnvVarValue v }) }
    }

let private genCapturedOutput: Gen<CapturedOutput> =
    Gen.oneof
        [ Gen.constant NoCapturedOutput
          shortStringGen |> Gen.map (fun p -> CapturedAt(CapturedOutputPath p)) ]

/// An arbitrary well-typed captured outcome — varying EVERY reproducible fact (executable, arguments incl. `[]`
/// and multi-element verbatim order, working dir, the three-class env delta, timeout, exit code, BOTH raw output
/// byte buffers) plus the sensed duration. Built through the PUBLIC `recordOf`. The two byte buffers ARE the
/// content `recordOf` digests — never synthetic digest literals.
let private genOutcome: Gen<CommandRecord> =
    gen {
        let! exe = shortStringGen
        let! args = Gen.listOf shortStringGen
        let! cwd = shortStringGen
        let! env = genEnvironmentDelta
        let! timeout = Gen.choose (0, 600)
        let! exit = Gen.choose (-1, 255)
        let! out = genBytes
        let! err = genBytes
        let! captured = genCapturedOutput
        let! d = Gen.choose (0, 1_000_000_000)

        return
            Build.outcome (
                executable = exe,
                arguments = (args |> List.map Argument),
                workingDirectory = cwd,
                environment = env,
                timeout = timeout,
                exitCode = exit,
                stdout = out,
                stderr = err,
                capturedOutput = captured,
                duration = int64 d
            )
    }

type Generators =
    static member Bytes() : Arbitrary<byte[]> = Arb.fromGen genBytes
    static member Outcome() : Arbitrary<CommandRecord> = Arb.fromGen genOutcome

/// FsCheck config registering the real F050 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
