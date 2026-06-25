module FS.GG.Governance.VerifyJson.Tests.ReleaseReadinessPreviewTests

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
open FS.GG.Governance.ReleaseReport.Model
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.VerifyJson.Tests.Support

// SC-003 / FR-005 / FR-015: the additive releaseReadiness block is advisory:true, uses the same evidence the
// release boundary would, and is BYTE-IDENTICAL-when-absent (no schemaVersion bump).

let private packRun =
    { Kind = Pack
      Record = CommandRecord.build (Executable "dotnet") [ Argument "pack" ] (WorkingDirectory "/w") { Added = []; Changed = []; Removed = [] } (TimeoutLimit 600) (ExitCode 0) (OutputDigest "o") (OutputDigest "e") NoCapturedOutput (SensedDuration 100L) }

let private packEvidence =
    Pack.evaluatePack Map.empty [ Packed({ Surface = SurfaceId "A"; ArtifactPath = "a.nupkg"; PackedVersion = "1.1.0"; Digest = ArtifactHash "dA" }, packRun) ]

let private attestation =
    let snapshot =
        Audit.auditSnapshot (Revision "c") (Revision "b") (Revision "h") (RuleHash "r") (GeneratorVersion "g") [ ArtifactHash "dA" ] [ packRun ] Local (BuilderIdentity "ci")
    Attestation.summarize snapshot packEvidence

let private preview: VerifyReleasePreview =
    { Verdict = FS.GG.Governance.Ship.Model.Pass
      Package = packEvidence
      Preconditions = []
      Attestation = attestation
      Advisory = true }

[<Tests>]
let tests =
    testList
        "releaseReadiness-preview"
        [ test "no preview ⇒ byte-identical to ofVerifyDecisionWithSurfaceChecks (no schema bump)" {
              let withNone = VerifyJson.ofVerifyDecisionWithPreview richDecision None [] [] None
              let baseline = VerifyJson.ofVerifyDecisionWithSurfaceChecks richDecision None [] []
              Expect.equal withNone baseline "absent preview ⇒ byte-identical"
              Expect.stringContains withNone "\"schemaVersion\":\"fsgg.verify/v1\"" "no schema bump"
              Expect.isFalse (withNone.Contains "releaseReadiness") "no block when absent"
          }

          test "present preview ⇒ an advisory releaseReadiness block with the same evidence" {
              let json = VerifyJson.ofVerifyDecisionWithPreview richDecision None [] [] (Some preview)
              Expect.stringContains json "\"releaseReadiness\":{" "the block is present"
              Expect.stringContains json "\"advisory\":true" "always advisory"
              Expect.stringContains json "\"packageEvidence\":{" "same evidence as release.json v2"
              Expect.stringContains json "\"versionPolicy\":{" ""
              Expect.stringContains json "fsgg.attestation/v1" "attestation reference"
              Expect.stringContains json "\"schemaVersion\":\"fsgg.verify/v1\"" "still v1 — the preview never bumps it"
          }

          test "the existing verify fields are unchanged when the preview is appended" {
              let json = VerifyJson.ofVerifyDecisionWithPreview richDecision None [] [] (Some preview)
              // the block is the LAST top-level field — the verify body precedes it byte-for-byte
              let baseline = VerifyJson.ofVerifyDecisionWithSurfaceChecks richDecision None [] []
              let prefix = baseline.Substring(0, baseline.Length - 1) // drop the closing brace
              Expect.stringContains json prefix "the verify body is unchanged; the block is appended"
          } ]
