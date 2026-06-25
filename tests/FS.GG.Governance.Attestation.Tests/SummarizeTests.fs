module FS.GG.Governance.Attestation.Tests.SummarizeTests

open Expecto
open FS.GG.Governance.CommandKind
open FS.GG.Governance.Attestation
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.Attestation.Tests.Support

// SC-005 / FR-007 / FR-008: summarize projects the snapshot (+ pack subjects) in an in-toto-compatible shape,
// attesting ONLY produced subjects and never overclaiming.

[<Tests>]
let tests =
    testList
        "summarize"
        [ test "Subjects come from Packed outcomes only, sorted by Name" {
              let snapshot = snapshotOf [ buildRun; packRun ]
              let summary = Attestation.summarize snapshot twoPacked
              let names = summary.Subjects |> List.map (fun s -> s.Name)
              Expect.equal names [ "out/A.nupkg"; "out/B.nupkg" ] "subjects sorted by name"
          }

          test "Materials project the snapshot's Provenance verbatim" {
              let snapshot = snapshotOf [ packRun ]
              let summary = Attestation.summarize snapshot twoPacked
              Expect.equal summary.Materials.RuleHash ruleHash ""
              Expect.equal summary.Materials.GeneratorVersion genVer ""
              Expect.equal summary.Materials.BaseRevision baseRev ""
              Expect.equal summary.Materials.HeadRevision headRev ""
              Expect.equal summary.Materials.SourceCommit srcCommit ""
              Expect.equal summary.Materials.Environment env ""
          }

          test "Invocation.Runs = the snapshot's runs in carried order" {
              let snapshot = snapshotOf [ buildRun; packRun ]
              let summary = Attestation.summarize snapshot twoPacked
              Expect.equal summary.Invocation.Runs [ buildRun; packRun ] ""
          }

          test "Identity = Provenance.canonicalId (via Audit.snapshotIdentity) verbatim" {
              let snapshot = snapshotOf [ packRun ]
              let summary = Attestation.summarize snapshot twoPacked
              Expect.equal summary.Identity (Audit.snapshotIdentity snapshot) ""
          }

          test "Compliance is ALWAYS CompatibleShapeNotFormalCompliance — never overclaims" {
              let summary = Attestation.summarize (snapshotOf [ packRun ]) twoPacked
              Expect.equal summary.Compliance CompatibleShapeNotFormalCompliance ""
          }

          test "a failed-build snapshot ⇒ Subjects = [] while the failed run stays under Invocation.Runs" {
              let snapshot = snapshotOf [ failedPackRun ]
              let pack = packOf [ failedOutcome "A" 137 ]
              let summary = Attestation.summarize snapshot pack
              Expect.isEmpty summary.Subjects "no attested subject for a failed build (FR-008)"
              Expect.contains summary.Invocation.Runs failedPackRun "the failed run is still recorded"
          } ]
