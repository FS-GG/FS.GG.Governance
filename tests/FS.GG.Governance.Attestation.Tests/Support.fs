module FS.GG.Governance.Attestation.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model

// Shared REAL builders for the F26 Attestation tests. Records are built through the public CommandRecord.build;
// the AuditSnapshot is rolled through the real Audit.auditSnapshot (Principle V; no mock).

let makeRecord (exit: int) (duration: int64) : CommandRecord =
    CommandRecord.build
        (Executable "dotnet")
        [ Argument "pack" ]
        (WorkingDirectory "/work")
        { Added = []; Changed = []; Removed = [] }
        (TimeoutLimit 600)
        (ExitCode exit)
        (OutputDigest "sha-out")
        (OutputDigest "sha-err")
        NoCapturedOutput
        (SensedDuration duration)

let buildRun = { Kind = Build; Record = makeRecord 0 100L }
let packRun = { Kind = Pack; Record = makeRecord 0 200L }
let failedPackRun = { Kind = Pack; Record = makeRecord 137 200L }

let srcCommit = Revision "c0ffee"
let baseRev = Revision "base1"
let headRev = Revision "head2"
let ruleHash = RuleHash "rule-x"
let genVer = GeneratorVersion "gen-1"
let digests = [ ArtifactHash "a2"; ArtifactHash "a1" ]
let env = Local
let builder = BuilderIdentity "ci-runner"

let snapshotOf (runs: KindedCommandRun list) : AuditSnapshot =
    Audit.auditSnapshot srcCommit baseRev headRev ruleHash genVer digests runs env builder

let snapshotWith (artifactDigests: ArtifactHash list) (runs: KindedCommandRun list) : AuditSnapshot =
    Audit.auditSnapshot srcCommit baseRev headRev ruleHash genVer artifactDigests runs env builder

let private surface s = SurfaceId s

let packedOutcome (s: string) (path: string) (version: string) (digest: string) : PackOutcome =
    Packed(
        { Surface = surface s
          ArtifactPath = path
          PackedVersion = version
          Digest = ArtifactHash digest },
        packRun
    )

let failedOutcome (s: string) (sentinel: int) : PackOutcome =
    PackFailed(surface s, sentinel, failedPackRun)

let packOf (outcomes: PackOutcome list) : PackEvidenceSet =
    Pack.evaluatePack Map.empty outcomes

/// A two-project packed evidence set (deliberately unsorted to observe subject sorting).
let twoPacked: PackEvidenceSet =
    packOf
        [ packedOutcome "B" "out/B.nupkg" "1.1.0" "dB"
          packedOutcome "A" "out/A.nupkg" "1.1.0" "dA" ]

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
