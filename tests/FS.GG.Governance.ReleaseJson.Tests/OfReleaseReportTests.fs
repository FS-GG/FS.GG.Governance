module FS.GG.Governance.ReleaseJson.Tests.OfReleaseReportTests

open Expecto
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
open FS.GG.Governance.ReleaseReport
open FS.GG.Governance.ReleaseJson
open FS.GG.Governance.ReleaseJson.Tests.Support

// SC-007 / FR-015: ofReleaseReport emits fsgg.release/v2 — every v1 field unchanged, plus exactly three
// additive fields (packageEvidence < versionPolicy < attestation) in a fixed order.

let private packRun = { Kind = Pack; Record = CommandRecord.build (Executable "dotnet") [ Argument "pack" ] (WorkingDirectory "/w") { Added = []; Changed = []; Removed = [] } (TimeoutLimit 600) (ExitCode 0) (OutputDigest "o") (OutputDigest "e") NoCapturedOutput (SensedDuration 100L) }

let private packEvidence =
    Pack.evaluatePack
        (Map [ SurfaceId "A", "1.0.0" ])
        [ Packed({ Surface = SurfaceId "A"; ArtifactPath = "a.nupkg"; PackedVersion = "1.1.0"; Digest = ArtifactHash "dA" }, packRun) ]

let private attestation =
    let snapshot =
        Audit.auditSnapshot (Revision "c") (Revision "b") (Revision "h") (RuleHash "r") (GeneratorVersion "g") [ ArtifactHash "dA" ] [ packRun ] Local (BuilderIdentity "ci")
    Attestation.summarize snapshot packEvidence

let private report = Report.assemble decisionMixed sensedMixed packEvidence attestation

let private indexOf (h: string) (n: string) = h.IndexOf(n)

[<Tests>]
let tests =
    testList
        "ofReleaseReport"
        [ test "schemaVersion is fsgg.release/v2" {
              Expect.equal ReleaseJson.schemaVersion "fsgg.release/v2" ""
              let json = ReleaseJson.ofReleaseReport report
              Expect.stringContains json "\"schemaVersion\":\"fsgg.release/v2\"" ""
          }

          test "the three additive fields appear after evidence, in fixed order" {
              let json = ReleaseJson.ofReleaseReport report
              let ev = indexOf json "\"evidence\""
              let pe = indexOf json "\"packageEvidence\""
              let vp = indexOf json "\"versionPolicy\""
              let at = indexOf json "\"attestation\":{"
              Expect.isLessThan ev pe "packageEvidence after evidence"
              Expect.isLessThan pe vp "packageEvidence < versionPolicy"
              Expect.isLessThan vp at "versionPolicy < attestation"
          }

          test "v1 fields (verdict/exitCodeBasis/rules) are carried verbatim from the decision" {
              let json = ReleaseJson.ofReleaseReport report
              Expect.stringContains json "\"verdict\":\"fail\"" "mixed fixture is blocked"
              Expect.stringContains json "\"exitCodeBasis\":\"blocked\"" ""
              Expect.stringContains json "\"rules\":[" "the v1 rules array is present"
          }

          test "packageEvidence carries the per-project surface/outcome/version/digest" {
              let json = ReleaseJson.ofReleaseReport report
              Expect.stringContains json "\"surface\":\"A\"" ""
              Expect.stringContains json "\"outcome\":\"packed\"" ""
              Expect.stringContains json "\"packedVersion\":\"1.1.0\"" ""
              Expect.stringContains json "\"digest\":\"dA\"" ""
          }

          test "versionPolicy carries the per-project verdict" {
              let json = ReleaseJson.ofReleaseReport report
              Expect.stringContains json "\"verdict\":\"bumped\"" "A bumped 1.0.0 -> 1.1.0"
          }

          test "attestation is a self-contained reference (schemaVersion/identity/compliance/subjectCount)" {
              let json = ReleaseJson.ofReleaseReport report
              Expect.stringContains json "fsgg.attestation/v1" ""
              Expect.stringContains json "\"subjectCount\":1" ""
          }

          test "byte-identical for identical input" {
              Expect.equal (ReleaseJson.ofReleaseReport report) (ReleaseJson.ofReleaseReport report) ""
          }

          test "the retained ofRelease still emits a well-formed v2 document (empty package, null attestation)" {
              let json = ReleaseJson.ofRelease decisionMet sensedMet
              Expect.stringContains json "\"schemaVersion\":\"fsgg.release/v2\"" ""
              Expect.stringContains json "\"noPackableProjects\":true" "empty package evidence"
              Expect.stringContains json "\"attestation\":null" "no fabricated attestation"
          } ]
