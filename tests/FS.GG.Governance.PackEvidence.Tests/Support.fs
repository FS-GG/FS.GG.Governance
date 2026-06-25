module FS.GG.Governance.PackEvidence.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.PackEvidence.Model

// Shared REAL builders for the F26 PackEvidence tests. The command records are built through the public
// `CommandRecord.build`; the pack outcomes carry those real records (Principle V; no mock).

let makePackRecord (exit: int) (duration: int64) : CommandRecord =
    CommandRecord.build
        (Executable "dotnet")
        [ Argument "pack"; Argument "-c"; Argument "Release" ]
        (WorkingDirectory "/work")
        { Added = []; Changed = []; Removed = [] }
        (TimeoutLimit 600)
        (ExitCode exit)
        (OutputDigest "sha-out")
        (OutputDigest "sha-err")
        NoCapturedOutput
        (SensedDuration duration)

let packRun (exit: int) (duration: int64) : KindedCommandRun =
    { Kind = Pack; Record = makePackRecord exit duration }

let surface (s: string) = SurfaceId s

let artifact (s: string) (path: string) (version: string) (digest: string) : PackArtifact =
    { Surface = surface s
      ArtifactPath = path
      PackedVersion = version
      Digest = ArtifactHash digest }

/// A successful pack of a project at the given version (real Pack run, exit 0).
let packed (s: string) (path: string) (version: string) (digest: string) : PackOutcome =
    Packed(artifact s path version digest, packRun 0 111L)

/// A zero-exit pack that produced no usable artifact.
let packedNoArtifact (s: string) (reason: NoArtifactReason) : PackOutcome =
    PackedNoArtifact(surface s, reason, packRun 0 111L)

/// A failed pack carrying its sentinel exit code (a real non-zero Pack run).
let packFailed (s: string) (sentinel: int) : PackOutcome =
    PackFailed(surface s, sentinel, packRun sentinel 222L)

let baselines (pairs: (string * string) list) : Map<SurfaceId, string> =
    pairs |> List.map (fun (s, v) -> surface s, v) |> Map.ofList

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
