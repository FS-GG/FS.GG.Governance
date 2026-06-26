module FS.GG.Governance.EvidenceCapture.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model

// Shared REAL-input builders + FsCheck generators for the F049 tests (Principle V — every value below is a
// real, literally-constructible typed value, never a mock; no process is spawned, no I/O is touched). The
// derived-not-synthetic discipline is the whole point of this row: the captured reference under test is always
// `EvidenceCapture.referenceOf record`, derived from a REAL F032 `CommandRecord` assembled via the PUBLIC
// `CommandRecord.build` (never a record literal). The only `Synthetic` evidence refs are PRIOR store entries
// the recompute-safety tests fold in, which this row does not derive (a real `EvidenceRef` for those would
// itself need gate execution — the deferred row). No filesystem/clock/process/network anywhere except the
// repo-root walk used by the surface baseline path.

// ── A real, complete command record every test varies from (the F032 worked example) ──

/// The base environment delta — one added var, the other two classes empty.
let baseEnv: EnvironmentDelta =
    { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]
      Changed = []
      Removed = [] }

/// Build a real F032 `CommandRecord` through the PUBLIC `CommandRecord.build` (never a record literal), with
/// sensible defaults and a per-field override for EVERY reproducible fact AND the one sensed duration — so a
/// test can perturb exactly one fact (executable, an argument, argument order, working dir, env delta, timeout,
/// exit code, stdout digest, stderr digest, captured-output outcome) or ONLY the `SensedDuration`.
type Build =
    static member record
        (
            ?executable: string,
            ?arguments: Argument list,
            ?workingDirectory: string,
            ?environment: EnvironmentDelta,
            ?timeout: int,
            ?exitCode: int,
            ?stdoutDigest: string,
            ?stderrDigest: string,
            ?capturedOutput: CapturedOutput,
            ?duration: int64
        ) : CommandRecord =
        CommandRecord.build
            (Executable(defaultArg executable "gcc"))
            (defaultArg arguments [ Argument "-c"; Argument "main.c" ])
            (WorkingDirectory(defaultArg workingDirectory "/work"))
            (defaultArg environment baseEnv)
            (TimeoutLimit(defaultArg timeout 30))
            (ExitCode(defaultArg exitCode 0))
            (OutputDigest(defaultArg stdoutDigest "sha-out"))
            (OutputDigest(defaultArg stderrDigest "sha-err"))
            (defaultArg capturedOutput NoCapturedOutput)
            (SensedDuration(defaultArg duration 123_456L))

/// The base record — every reproducible fact present and distinct so a single-field perturbation is unambiguous.
let baseRecord: CommandRecord = Build.record ()

/// The SAME gate, identical in every reproducible fact, only with a different sensed duration. The reference
/// MUST be byte-identical to `baseRecord`'s (duration-invariance, FR-002 / SC-002).
let slowerRecord: CommandRecord = Build.record (duration = 999_999L)

/// One representative single-field perturbation per reproducible fact, each labelled and built through the
/// public `build` so it differs from `baseRecord` in EXACTLY the named reproducible fact. Injectivity (FR-003 /
/// SC-003): every one must yield a reference DIFFERENT from `baseRecord` and pairwise-distinct from the others.
/// The env-delta variant adds a var (the delta is compared as a SET); argument order is reversed (order is
/// significant); the captured-output variant flips `NoCapturedOutput` to a present path.
let reproducibleVariants: (string * CommandRecord) list =
    [ "executable", Build.record (executable = "clang")
      "argument value", Build.record (arguments = [ Argument "-c"; Argument "other.c" ])
      "argument order", Build.record (arguments = [ Argument "main.c"; Argument "-c" ])
      "working directory", Build.record (workingDirectory = "/elsewhere")
      "env delta set",
      Build.record (
          environment =
              { baseEnv with
                  Added = baseEnv.Added @ [ { Name = EnvVarName "X"; Value = EnvVarValue "2" } ] }
      )
      "timeout", Build.record (timeout = 60)
      "exit code", Build.record (exitCode = 1)
      "stdout digest", Build.record (stdoutDigest = "sha-out-2")
      "stderr digest", Build.record (stderrDigest = "sha-err-2")
      "captured output", Build.record (capturedOutput = CapturedAt(CapturedOutputPath "x")) ]

/// Edge records exercising totality (FR-007): empty stdout/stderr digests, a non-zero exit code, and all three
/// captured-output outcomes — `referenceOf` is defined and never throws on every one.
let edgeRecords: (string * CommandRecord) list =
    [ "empty stdout digest", Build.record (stdoutDigest = "")
      "empty stderr digest", Build.record (stderrDigest = "")
      "both digests empty", Build.record (stdoutDigest = "", stderrDigest = "")
      "non-zero exit", Build.record (exitCode = 1)
      "no captured output", Build.record (capturedOutput = NoCapturedOutput)
      "captured at empty path", Build.record (capturedOutput = CapturedAt(CapturedOutputPath ""))
      "captured at path x", Build.record (capturedOutput = CapturedAt(CapturedOutputPath "x")) ]

/// The three captured-output outcomes, each a record differing ONLY in its captured-output fact — their
/// references must be pairwise distinct (F032 FR-011: absence never collides with an empty present path).
let capturedOutputRecords: CommandRecord list =
    [ Build.record (capturedOutput = NoCapturedOutput)
      Build.record (capturedOutput = CapturedAt(CapturedOutputPath ""))
      Build.record (capturedOutput = CapturedAt(CapturedOutputPath "x")) ]

// ── Real freshness-world builders (the F029/F030 worked example) ──

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

/// A spread of freshness worlds covering every `EnvironmentClass` case and the `Command`/`CommandVersion`
/// `Some`/`None` variants, so capture's recompute-safety is exercised across the category space.
let inputsVariants: FreshnessInputs list =
    [ { inputs "env-local" with Environment = Local }
      { inputs "env-ci" with Environment = Ci }
      { inputs "env-localorci" with Environment = LocalOrCi }
      { inputs "env-release" with Environment = Release }
      { inputs "cmd-none" with Command = None; CommandVersion = None }
      { inputs "cmd-some-ver-none" with Command = Some(CommandId "x"); CommandVersion = None } ]

// ── Real store builder (prior entries carry disclosed Synthetic refs) ──

/// An opaque, DISCLOSED-SYNTHETIC evidence reference for a PRIOR store entry this row does not derive.
/// SYNTHETIC: a real `EvidenceRef` for a prior entry is the output of gate execution (the deferred row); the
/// reference UNDER TEST is always `EvidenceCapture.referenceOf record`, derived — never this.
let syntheticRef (label: string) : EvidenceRef = EvidenceRef("synthetic://" + label) // SYNTHETIC: prior entries; real refs need gate execution

/// Build a non-empty `ReuseStore` by folding the REAL `EvidenceReuse.record` over `EvidenceReuse.empty` (so the
/// store is exactly the value production would hold; de-dup / most-recent-wins is F030's, not ours). Oldest-first
/// input ⇒ newest-first store.
let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries
    |> List.fold (fun store (i, evidence) -> EvidenceReuse.record i evidence store) EvidenceReuse.empty

// ── FsCheck generators (real values, no mocks) ──

let private shortStringGen: Gen<string> =
    Gen.elements [ ""; "a"; "gcc"; "clang"; "main.c"; "-c"; "/work"; "sha-out"; "héllo"; "x:y=z" ]

let private genEnvironmentClass: Gen<EnvironmentClass> =
    Gen.elements [ Local; Ci; LocalOrCi; Release ]

let private genEnvVar: Gen<EnvVarName * EnvVarValue> =
    gen {
        let! n = shortStringGen
        let! v = shortStringGen
        return (EnvVarName n, EnvVarValue v)
    }

let private genEnvironmentDelta: Gen<EnvironmentDelta> =
    gen {
        let! added = Gen.listOf genEnvVar
        let! changed = Gen.listOf genEnvVar
        let! removed = Gen.listOf genEnvVar

        return
            { Added = added |> List.map (fun (n, v) -> { Name = n; Value = v })
              Changed = changed |> List.map (fun (n, v) -> { Name = n; Old = v; New = v })
              Removed = removed |> List.map (fun (n, v) -> { Name = n; Old = v }) }
    }

let private genCapturedOutput: Gen<CapturedOutput> =
    Gen.oneof
        [ Gen.constant NoCapturedOutput
          shortStringGen |> Gen.map (fun p -> CapturedAt(CapturedOutputPath p)) ]

/// Arbitrary well-typed `CommandRecord`s, varying EVERY reproducible fact (executable, an argument list incl.
/// `[]` and multi-element verbatim order, working dir, the three-class env delta, timeout, exit code, both
/// digests, and the captured-output outcome) plus the sensed duration. Built through the PUBLIC `build`.
let private genCommandRecord: Gen<CommandRecord> =
    gen {
        let! exe = shortStringGen
        let! args = Gen.listOf shortStringGen
        let! cwd = shortStringGen
        let! env = genEnvironmentDelta
        let! timeout = Gen.choose (0, 600)
        let! exit = Gen.choose (-1, 255)
        let! out = shortStringGen
        let! err = shortStringGen
        let! captured = genCapturedOutput
        let! d = Gen.choose (0, 1_000_000_000)

        return
            Build.record (
                executable = exe,
                arguments = (args |> List.map Argument),
                workingDirectory = cwd,
                environment = env,
                timeout = timeout,
                exitCode = exit,
                stdoutDigest = out,
                stderrDigest = err,
                capturedOutput = captured,
                duration = int64 d
            )
    }

let private genFreshnessInputs: Gen<FreshnessInputs> =
    gen {
        let! check = shortStringGen
        let! domain = shortStringGen
        let! hasCommand = Gen.elements [ true; false ]
        let! command = shortStringGen
        let! env = genEnvironmentClass
        let! ruleHash = shortStringGen
        let! arts = Gen.listOf shortStringGen
        let! hasCmdVersion = Gen.elements [ true; false ]
        let! cmdVersion = shortStringGen
        let! genVersion = shortStringGen
        let! baseRev = shortStringGen
        let! headRev = shortStringGen

        return
            { Check = CheckId check
              Domain = DomainId domain
              Command = (if hasCommand then Some(CommandId command) else None)
              Environment = env
              RuleHash = RuleHash ruleHash
              CoveredArtifacts = arts |> List.map ArtifactHash
              CommandVersion = (if hasCmdVersion then Some(CommandVersion cmdVersion) else None)
              GeneratorVersion = GeneratorVersion genVersion
              Base = Revision baseRev
              Head = Revision headRev }
    }

let private genEvidenceRef: Gen<EvidenceRef> =
    // Opaque, edge-supplied references incl. empty + escaping-significant strings (prior-entry fixtures).
    Gen.elements [ ""; "ev-A"; "ev-B"; "héllo"; "with\"quote"; "back\\slash" ]
    |> Gen.map EvidenceRef

let private genRecordedEvidence: Gen<RecordedEvidence> =
    gen {
        let! i = genFreshnessInputs
        let! e = genEvidenceRef
        return { Inputs = i; Evidence = e }
    }

/// Arbitrary well-typed `ReuseStore` values (empty / singleton / large), varying every category. Built as a raw
/// `ReuseStore` so the recompute-safety property sees arbitrary prior worlds the captured world must not disturb.
let private genReuseStore: Gen<ReuseStore> =
    Gen.listOf genRecordedEvidence |> Gen.map ReuseStore

type Generators =
    static member CommandRecord() : Arbitrary<CommandRecord> = Arb.fromGen genCommandRecord
    static member FreshnessInputs() : Arbitrary<FreshnessInputs> = Arb.fromGen genFreshnessInputs
    static member EvidenceRef() : Arbitrary<EvidenceRef> = Arb.fromGen genEvidenceRef
    static member RecordedEvidence() : Arbitrary<RecordedEvidence> = Arb.fromGen genRecordedEvidence
    static member ReuseStore() : Arbitrary<ReuseStore> = Arb.fromGen genReuseStore

/// FsCheck config registering the real F049 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
