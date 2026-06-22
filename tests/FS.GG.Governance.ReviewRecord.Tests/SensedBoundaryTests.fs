module FS.GG.Governance.ReviewRecord.Tests.SensedBoundaryTests

open Expecto
open FsCheck
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.ReviewRecord
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord.Tests.Support

// US2 (SC-003, L-I2): the honesty boundary — `canonicalId` never reads `record.Sensed`. Two records identical
// in every reproducible fact but differing only in `Sensed` share a byte-identical identity.

let private idOf (r: ReviewRecord) : string = ReviewRecord.identityValue (ReviewRecord.canonicalId r)

[<Tests>]
let tests =
    testList
        "SensedBoundary"
        [ test "records differing only in Sensed (empty vs non-empty) share a byte-identical identity (SC-003)" {
              let repro =
                  { Request = baseRequest
                    Model = modelId "gpt"
                    ModelVersion = modelVersion "2026-06"
                    PromptHash = promptHash "ph1"
                    ReviewedArtifacts = [ artifactHash "sha:a" ]
                    ResponseDigest = responseDigest "sha:resp"
                    Verdict = recordedVerdict "pass" }

              let withNone = { Reproducible = repro; Sensed = [] }
              let withSome = { Reproducible = repro; Sensed = [ sensedAt "at" "2026-06-22T10:00:00Z" ] }

              Expect.equal (idOf withSome) (idOf withNone) "sensed metadata must not affect the identity"
          }

          testPropertyWithConfig fscheckConfig "canonicalId never depends on record.Sensed (L-I2)"
          <| fun (repro: ReproducibleFacts) (s1: SensedMetadatum list) (s2: SensedMetadatum list) ->
              let a = { Reproducible = repro; Sensed = s1 }
              let b = { Reproducible = repro; Sensed = s2 }
              idOf a = idOf b ]
