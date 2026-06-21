module FS.GG.Governance.CommandRecord.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model

// Shared REAL-input builders + FsCheck generators for the F032 tests (Principle V — every value below is a
// real, literally-constructible typed run fact, never a mock; no process is spawned). The operations are
// pure, so no upstream chain is needed: the ten facts a host would sense are handed in as literals — the
// core's contract. No I/O beyond repo-root resolution.

// ── A real, complete base record every test varies from ──

/// A complete, literal env delta — one added, one changed, one removed variable, all names/values distinct
/// so a single-field change is unambiguous (and a changed var is never confusable with an add+remove pair).
let baseEnvironment: EnvironmentDelta =
    { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]
      Changed = [ { Name = EnvVarName "PATH"; Old = EnvVarValue "/a"; New = EnvVarValue "/a:/b" } ]
      Removed = [ { Name = EnvVarName "TMP"; Old = EnvVarValue "/tmp" } ] }

/// The ten literal facts of a representative successful run. Every fact is present and distinct so a
/// single-field perturbation is unambiguous.
let baseExecutable = Executable "gcc"
let baseArguments = [ Argument "-c"; Argument "main.c" ]
let baseWorkingDirectory = WorkingDirectory "/work"
let baseTimeout = TimeoutLimit 30
let baseExitCode = ExitCode 0
let baseStdoutDigest = OutputDigest "sha-out"
let baseStderrDigest = OutputDigest "sha-err"
let baseCapturedOutput = NoCapturedOutput
let baseDuration = SensedDuration 123_456L

/// The complete base `CommandRecord`, assembled through the PUBLIC `build` (never a record literal — the
/// tests exercise the real assembly point).
let baseRecord: CommandRecord =
    CommandRecord.build
        baseExecutable
        baseArguments
        baseWorkingDirectory
        baseEnvironment
        baseTimeout
        baseExitCode
        baseStdoutDigest
        baseStderrDigest
        baseCapturedOutput
        baseDuration

/// Rebuild a record from a (possibly perturbed) `ReproducibleFacts` + duration, via the public `build`.
let rebuild (facts: ReproducibleFacts) (duration: SensedDuration) : CommandRecord =
    CommandRecord.build
        facts.Executable
        facts.Arguments
        facts.WorkingDirectory
        facts.Environment
        facts.Timeout
        facts.ExitCode
        facts.StdoutDigest
        facts.StderrDigest
        facts.CapturedOutput
        duration

// ── One representative single-field variant per reproducible fact ──
// Each takes a `ReproducibleFacts` and changes EXACTLY the named fact to a distinct value, for
// table-driven per-field-sensitivity tests (SC-004).

let variantExecutable (f: ReproducibleFacts) = { f with Executable = Executable "clang" }
let variantArgumentValue (f: ReproducibleFacts) = { f with Arguments = [ Argument "-c"; Argument "other.c" ] }
let variantArgumentOrder (f: ReproducibleFacts) = { f with Arguments = [ Argument "main.c"; Argument "-c" ] }
let variantWorkingDirectory (f: ReproducibleFacts) = { f with WorkingDirectory = WorkingDirectory "/other" }
let variantEnvironmentAdded (f: ReproducibleFacts) =
    { f with Environment = { f.Environment with Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "2" } ] } }
let variantEnvironmentChanged (f: ReproducibleFacts) =
    { f with Environment = { f.Environment with Changed = [ { Name = EnvVarName "PATH"; Old = EnvVarValue "/a"; New = EnvVarValue "/z" } ] } }
let variantEnvironmentRemoved (f: ReproducibleFacts) =
    { f with Environment = { f.Environment with Removed = [ { Name = EnvVarName "TMP"; Old = EnvVarValue "/var/tmp" } ] } }
let variantTimeout (f: ReproducibleFacts) = { f with Timeout = TimeoutLimit 60 }
let variantExitCode (f: ReproducibleFacts) = { f with ExitCode = ExitCode 1 }
let variantStdoutDigest (f: ReproducibleFacts) = { f with StdoutDigest = OutputDigest "sha-out-2" }
let variantStderrDigest (f: ReproducibleFacts) = { f with StderrDigest = OutputDigest "sha-err-2" }
let variantCapturedOutput (f: ReproducibleFacts) = { f with CapturedOutput = CapturedAt(CapturedOutputPath "/cap/out.log") }

/// Every reproducible fact paired with a single-field variation, each labelled. Table-driven sensitivity
/// tests iterate this so EVERY reproducible fact (including argument value AND order) is covered (SC-004).
let allReproducibleVariants: (string * (ReproducibleFacts -> ReproducibleFacts)) list =
    [ "executable", variantExecutable
      "argument value", variantArgumentValue
      "argument order", variantArgumentOrder
      "working directory", variantWorkingDirectory
      "env added", variantEnvironmentAdded
      "env changed", variantEnvironmentChanged
      "env removed", variantEnvironmentRemoved
      "timeout", variantTimeout
      "exit code", variantExitCode
      "stdout digest", variantStdoutDigest
      "stderr digest", variantStderrDigest
      "captured output", variantCapturedOutput ]

// ── FsCheck generators (real values, no mocks) ──

let private shortStringGen: Gen<string> =
    Gen.elements [ ""; "a"; "b"; "gcc"; "clang"; "-c"; "main.c"; "/work"; "CI"; "PATH"; "1"; "héllo"; "x:y=z;|" ]

let private genAddedVar: Gen<AddedVar> =
    gen {
        let! n = shortStringGen
        let! v = shortStringGen
        return { Name = EnvVarName n; Value = EnvVarValue v }
    }

let private genChangedVar: Gen<ChangedVar> =
    gen {
        let! n = shortStringGen
        let! o = shortStringGen
        let! w = shortStringGen
        return { Name = EnvVarName n; Old = EnvVarValue o; New = EnvVarValue w }
    }

let private genRemovedVar: Gen<RemovedVar> =
    gen {
        let! n = shortStringGen
        let! o = shortStringGen
        return { Name = EnvVarName n; Old = EnvVarValue o }
    }

let private genEnvironmentDelta: Gen<EnvironmentDelta> =
    gen {
        let! added = Gen.listOf genAddedVar
        let! changed = Gen.listOf genChangedVar
        let! removed = Gen.listOf genRemovedVar
        return { Added = added; Changed = changed; Removed = removed }
    }

let private genCapturedOutput: Gen<CapturedOutput> =
    Gen.oneof
        [ Gen.constant NoCapturedOutput
          shortStringGen |> Gen.map (fun s -> CapturedAt(CapturedOutputPath s)) ]

let private genReproducibleFacts: Gen<ReproducibleFacts> =
    gen {
        let! exe = shortStringGen
        let! args = Gen.listOf shortStringGen
        let! cwd = shortStringGen
        let! env = genEnvironmentDelta
        let! timeout = Gen.choose (0, 600)
        let! exit = Gen.choose (-2, 130)
        let! out = shortStringGen
        let! err = shortStringGen
        let! cap = genCapturedOutput

        return
            { Executable = Executable exe
              Arguments = args |> List.map Argument
              WorkingDirectory = WorkingDirectory cwd
              Environment = env
              Timeout = TimeoutLimit timeout
              ExitCode = ExitCode exit
              StdoutDigest = OutputDigest out
              StderrDigest = OutputDigest err
              CapturedOutput = cap }
    }

let private genSensedDuration: Gen<SensedDuration> =
    Gen.choose (0, 1_000_000_000) |> Gen.map (fun n -> SensedDuration(int64 n))

let private genCommandRecord: Gen<CommandRecord> =
    gen {
        let! facts = genReproducibleFacts
        let! duration = genSensedDuration
        return rebuild facts duration
    }

type Generators =
    static member ReproducibleFacts() : Arbitrary<ReproducibleFacts> = Arb.fromGen genReproducibleFacts
    static member SensedDuration() : Arbitrary<SensedDuration> = Arb.fromGen genSensedDuration
    static member CommandRecord() : Arbitrary<CommandRecord> = Arb.fromGen genCommandRecord

/// FsCheck config registering the real F032 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

/// A same-SET permutation+duplication of an env-delta: each class's entries are reversed and one entry of
/// each non-empty class is duplicated, so the underlying set is preserved while order and multiplicity
/// change (for the order/dup-invariance properties).
let permuteAndDuplicateEnv (env: EnvironmentDelta) : EnvironmentDelta =
    let perturb (xs: 'a list) =
        match List.rev xs with
        | [] -> []
        | head :: _ as reversed -> reversed @ [ head ]

    { Added = perturb env.Added
      Changed = perturb env.Changed
      Removed = perturb env.Removed }

// ── Repo root (for the surface baseline path) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then d.FullName
        else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
