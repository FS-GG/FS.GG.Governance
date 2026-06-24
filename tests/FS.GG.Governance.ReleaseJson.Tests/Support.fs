module FS.GG.Governance.ReleaseJson.Tests.Support

open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model

// Shared REAL-input builders for the F055 ReleaseJson tests (Principle V). The decision + snapshot are
// produced by the REAL F053/F054 cores (`Release.evaluateRelease`, `Sensing.deriveFacts`) over hand-built
// recovered evidence — never mocks. `ofRelease` is then exercised against the genuine values.

let surfaceId = SurfaceId "pkg"

let expectations: ReleaseExpectations =
    { Surface = surfaceId
      VersionBaseline = Some "1.2.0"
      RequiredMetadataFields = Some [ "authors"; "license" ]
      ExpectedPins = Some(Map [ "base", "9.0.0" ])
      RequiredPublishPosture = Some [ "plan-present" ]
      RequiredTrustedPublishing = Some [ "oidc" ]
      RequiredProvenance = Some [ "attestation" ] }

/// One blocking-at-release rule per family, in declaration order.
let rules: ReleaseRule list =
    [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]
    |> List.map (fun k ->
        { Kind = k
          Surface = surfaceId
          BaseSeverity = Blocking
          Maturity = BlockOnRelease })

let recoveredMet: RecoveredEvidence =
    { Version = Ok { Declared = "1.3.0" }
      Metadata = Ok { PresentFields = [ "authors"; "license" ] }
      Pins = Ok { Resolved = Map [ "base", "9.0.0" ] }
      PublishPlan = Ok { Observed = [ "plan-present" ] }
      TrustedPublishing = Ok { Observed = [ "oidc" ] }
      Provenance = Ok { Observed = [ "attestation" ] } }

/// A mixed bundle exercising met / unmet / unrecoverable (pins is unrecoverable ⇒ a `null` evidence object).
let recoveredMixed: RecoveredEvidence =
    { Version = Ok { Declared = "1.3.0" } // met
      Metadata = Ok { PresentFields = [ "authors" ] } // unmet (missing license)
      Pins = Error "pins source not found" // unrecoverable
      PublishPlan = Ok { Observed = [ "plan-present" ] } // met
      TrustedPublishing = Ok { Observed = [] } // unmet (missing oidc)
      Provenance = Ok { Observed = [ "attestation" ] } } // met

let sensedMet = Sensing.deriveFacts expectations recoveredMet
let sensedMixed = Sensing.deriveFacts expectations recoveredMixed

let decisionMet = Release.evaluateRelease rules sensedMet.Facts
let decisionMixed = Release.evaluateRelease rules sensedMixed.Facts

// ── Repo root + golden baseline path ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(System.AppContext.BaseDirectory))

let goldenPath =
    Path.Combine(repoRoot, "specs", "055-release-command", "contracts", "release.golden.json")
