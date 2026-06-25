module FS.GG.Governance.ProvenanceJson.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.CommandKind

// Shared REAL builders for the F25 provenance.json projection tests. The command records are built through
// the public `CommandRecord.build`; the provenance is rolled up through the real `Audit.auditSnapshot`; the
// projection is pure (Principle V; no mock).

let makeRecord (exit: int) (duration: int64) : CommandRecord =
    CommandRecord.build
        (Executable "gcc")
        [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work")
        { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]; Changed = []; Removed = [] }
        (TimeoutLimit 30)
        (ExitCode exit)
        (OutputDigest "sha-out")
        (OutputDigest "sha-err")
        NoCapturedOutput
        (SensedDuration duration)

let runBuild = { Kind = Build; Record = makeRecord 0 111L }
let runTest = { Kind = Test; Record = makeRecord 0 222L }
let runFailed = { Kind = Pack; Record = makeRecord 137 333L } // a non-zero (sentinel-style) exit

let srcCommit = Revision "c0ffee"
let baseRev = Revision "base1"
let headRev = Revision "head2"
let ruleHash = RuleHash "rule-x"
let genVer = GeneratorVersion "gen-1"
// deliberately UNSORTED + duplicate so the projection's sort/set rendering is observable
let digests = [ ArtifactHash "a2"; ArtifactHash "a1"; ArtifactHash "a2" ]
let env = Local
let builder = BuilderIdentity "ci-runner"

let snapshotOf (runs: KindedCommandRun list) : AuditSnapshot =
    Audit.auditSnapshot srcCommit baseRev headRev ruleHash genVer digests runs env builder

let baseSnapshot = snapshotOf [ runBuild; runTest; runFailed ]

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
