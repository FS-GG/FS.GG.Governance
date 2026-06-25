module FS.GG.Governance.ReleaseReport.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation
open FS.GG.Governance.Attestation.Model

// Shared REAL builders for the F26 ReleaseReport tests: real ReleaseRules.evaluateRelease decisions over real
// ReleaseFactsSensing snapshots, a real PackEvidenceSet, and a real AttestationSummary (Principle V; no mock).

let allFamilies =
    [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]

let private blockingRule kind : ReleaseRule =
    { Kind = kind
      Surface = SurfaceId "release"
      BaseSeverity = Blocking
      Maturity = BlockOnRelease }

/// A SensedRelease from per-family fact states + optional diagnostics (the F54 shape, surface "release").
let sensedFrom (states: (ReleaseRuleKind * FactState) list) (diagnostics: (ReleaseRuleKind * string) list) : SensedRelease =
    { Facts = { States = Map.ofList states }
      Snapshot =
        { Surface = SurfaceId "release"
          Version = None
          Metadata = None
          Pins = None
          PublishPlan = None
          TrustedPublishing = None
          Provenance = None
          Diagnostics = diagnostics |> List.map (fun (f, r) -> { Family = f; Reason = r }) } }

/// Every family Met — a fully-releasable sensing.
let allMet = allFamilies |> List.map (fun k -> k, Met)

let decisionFor (sensed: SensedRelease) : ReleaseDecision =
    let rules = sensed.Facts.States |> Map.toList |> List.map (fst >> blockingRule)
    Release.evaluateRelease rules sensed.Facts

// ── a real AttestationSummary (built through the real pipeline) ──

let private record =
    CommandRecord.build
        (Executable "dotnet")
        [ Argument "pack" ]
        (WorkingDirectory "/work")
        { Added = []; Changed = []; Removed = [] }
        (TimeoutLimit 600)
        (ExitCode 0)
        (OutputDigest "o")
        (OutputDigest "e")
        NoCapturedOutput
        (SensedDuration 100L)

let private packRun = { Kind = Pack; Record = record }

let packEvidence: PackEvidenceSet =
    Pack.evaluatePack Map.empty [ Packed({ Surface = SurfaceId "A"; ArtifactPath = "a.nupkg"; PackedVersion = "1.1.0"; Digest = ArtifactHash "dA" }, packRun) ]

let attestation: AttestationSummary =
    let snapshot =
        Audit.auditSnapshot
            (Revision "c") (Revision "b") (Revision "h") (RuleHash "r") (GeneratorVersion "g")
            [ ArtifactHash "dA" ] [ packRun ] Local (BuilderIdentity "ci")

    Attestation.summarize snapshot packEvidence

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
