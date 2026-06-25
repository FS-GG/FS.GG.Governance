module FS.GG.Governance.CommandKind.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateExecution
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind.Model

// Shared REAL builders for the F25 CommandKind tests (Principle V). The command-run fixtures are produced by
// the REAL F051 `Interpreter.realPort` driving a real process (`/bin/echo`, a deliberately-missing
// executable for the sentinel case) through `senseExecution` — never a mock. The provenance inputs are real,
// literally-constructible typed facts. No network, no governed repository.

// ── Real command-run fixtures via the real ExecutionPort ──

let private echoCommand (args: string list) : GateCommand =
    { Executable = Executable "/bin/echo"
      Arguments = args |> List.map Argument
      WorkingDirectory = WorkingDirectory "/tmp"
      Environment = { Added = []; Changed = []; Removed = [] }
      Timeout = TimeoutLimit 30
      CapturedOutput = NoCapturedOutput }

/// Run a real `/bin/echo` through the real port and wrap the resulting F032 record with the given kind.
let realRun (kind: CommandKind) (args: string list) : KindedCommandRun =
    let record = Interpreter.senseExecution Interpreter.realPort (echoCommand args)
    { Kind = kind; Record = record }

/// A run whose executable does not exist — the real port reifies it as an ordinary outcome carrying the
/// F051 `startFailureExitCode` sentinel (never an exception, never dropped).
let sentinelRun (kind: CommandKind) : KindedCommandRun =
    let command =
        { Executable = Executable "/no/such/executable-fsgg-test"
          Arguments = [ Argument "x" ]
          WorkingDirectory = WorkingDirectory "/tmp"
          Environment = { Added = []; Changed = []; Removed = [] }
          Timeout = TimeoutLimit 30
          CapturedOutput = NoCapturedOutput }

    { Kind = kind; Record = Interpreter.senseExecution Interpreter.realPort command }

/// One run of EACH of the seven kinds (real `/bin/echo`, the kind as its argument so each command is
/// distinct).
let everyKindRun: (CommandKind * KindedCommandRun) list =
    [ Build, realRun Build [ "build" ]
      Test, realRun Test [ "test" ]
      Pack, realRun Pack [ "pack" ]
      TemplateInstantiation, realRun TemplateInstantiation [ "template" ]
      GitDiff, realRun GitDiff [ "diff" ]
      PackageInspection, realRun PackageInspection [ "inspect" ]
      VisualCapture, realRun VisualCapture [ "capture" ] ]

// ── A literal F032 record (for duration-invariance) built through the public `CommandRecord.build` ──

let makeRecord (duration: int64) : CommandRecord =
    CommandRecord.build
        (Executable "gcc")
        [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work")
        { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]; Changed = []; Removed = [] }
        (TimeoutLimit 30)
        (ExitCode 0)
        (OutputDigest "sha-out")
        (OutputDigest "sha-err")
        NoCapturedOutput
        (SensedDuration duration)

// ── Real provenance inputs (literal typed facts) ──

let srcCommit = Revision "c0ffee"
let baseRev = Revision "base1"
let headRev = Revision "head2"
let ruleHash = RuleHash "rule-x"
let genVer = GeneratorVersion "gen-1"
let digests = [ ArtifactHash "a1"; ArtifactHash "a2" ]
let env = Local
let builder = BuilderIdentity "ci-runner"

// ── Repo root (for the surface baseline path) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
