module FS.GG.Governance.Attestation.Tests.StabilityTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.Attestation
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.Attestation.Tests.Support

// SC-005: summarize is byte-identical for identical inputs; changing ONLY a duration leaves Identity
// unchanged; changing a reproducible input changes Identity.

[<Tests>]
let tests =
    testList
        "summarize-stability"
        [ test "byte-identical (structurally equal) for identical inputs" {
              let a = Attestation.summarize (snapshotOf [ packRun ]) twoPacked
              let b = Attestation.summarize (snapshotOf [ packRun ]) twoPacked
              Expect.equal a b ""
          }

          test "changing ONLY a SensedDuration leaves Identity unchanged" {
              let fastPack = { Kind = Pack; Record = makeRecord 0 1L }
              let slowPack = { Kind = Pack; Record = makeRecord 0 999999L }
              let idFast = (Attestation.summarize (snapshotOf [ fastPack ]) twoPacked).Identity
              let idSlow = (Attestation.summarize (snapshotOf [ slowPack ]) twoPacked).Identity
              Expect.equal idFast idSlow "duration is sensed metadata, excluded from identity"
          }

          test "changing a reproducible input (an artifact digest) changes Identity" {
              let id1 = (Attestation.summarize (snapshotWith [ ArtifactHash "x" ] [ packRun ]) twoPacked).Identity
              let id2 = (Attestation.summarize (snapshotWith [ ArtifactHash "y" ] [ packRun ]) twoPacked).Identity
              Expect.notEqual id1 id2 "a reproducible-input change changes identity"
          } ]
