module FS.GG.Governance.AttestationJson.Tests.Support

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
open FS.GG.Governance.Attestation
open FS.GG.Governance.Attestation.Model

// Shared REAL builders for the F26 attestation.json projection tests. The summary is built through the real
// Attestation.summarize over a real Audit.auditSnapshot (Principle V; no mock).

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

let packRun = { Kind = Pack; Record = makeRecord 0 200L }
let failedPackRun = { Kind = Pack; Record = makeRecord 137 200L }

let private surface s = SurfaceId s

let packedOutcome (s: string) (path: string) (version: string) (digest: string) : PackOutcome =
    Packed(
        { Surface = surface s
          ArtifactPath = path
          PackedVersion = version
          Digest = ArtifactHash digest },
        packRun
    )

// deliberately UNSORTED digests so the projection's set-sort is observable
let private digests = [ ArtifactHash "z9"; ArtifactHash "a1" ]

let snapshotOf (runs: KindedCommandRun list) : AuditSnapshot =
    Audit.auditSnapshot
        (Revision "c0ffee")
        (Revision "base1")
        (Revision "head2")
        (RuleHash "rule-x")
        (GeneratorVersion "gen-1")
        digests
        runs
        Local
        (BuilderIdentity "ci-runner")

// deliberately UNSORTED projects so subject sorting is observable
let twoPacked: PackEvidenceSet =
    Pack.evaluatePack
        Map.empty
        [ packedOutcome "B" "out/B.nupkg" "1.1.0" "dB"
          packedOutcome "A" "out/A.nupkg" "1.1.0" "dA" ]

let summaryWith (runs: KindedCommandRun list) (pack: PackEvidenceSet) : AttestationSummary =
    Attestation.summarize (snapshotOf runs) pack

let baseSummary: AttestationSummary = summaryWith [ packRun ] twoPacked

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
