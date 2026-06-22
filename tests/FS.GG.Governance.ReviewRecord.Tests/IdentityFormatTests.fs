module FS.GG.Governance.ReviewRecord.Tests.IdentityFormatTests

open Expecto
open FsCheck
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.ReviewRecord
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord.Tests.Support

// US2: example tests pinned to contracts/review-record-identity-format.md — the canonical string is
// byte-for-byte the documented block, and the sensed metadata never appears in it.

let private idOf (r: ReviewRecord) : string = ReviewRecord.identityValue (ReviewRecord.canonicalId r)

[<Tests>]
let tests =
    testList
        "IdentityFormat"
        [ test "worked example renders byte-for-byte equal to the documented block" {
              // request = assemble (QuestionText "Explain the API?") [ DigestOnly (ArtifactHash "sha:a") ]
              let request = PromptIsolation.assemble (QuestionText "Explain the API?") [ DigestOnly(ArtifactHash "sha:a") ]

              let record =
                  buildOf
                      request
                      (modelId "gpt")
                      (modelVersion "2026-06")
                      (promptHash "ph1")
                      [ artifactHash "sha:a" ]
                      (responseDigest "sha:resp")
                      (recordedVerdict "pass")
                      [ sensedAt "at" "2026-06-22T10:00:00Z" ]

              // R = the F037 rendering of the request (its own two segments joined by '\n').
              let r = "instr=16:Explain the API?\nart=1;dig=5:sha:a"
              let reqByteLen = System.Text.Encoding.UTF8.GetByteCount r

              let expected =
                  sprintf "req=%d:%s\nmid=3:gpt\nmver=7:2026-06\npph=3:ph1\nart=1;5:sha:a\nresp=8:sha:resp\nvdt=4:pass" reqByteLen r

              Expect.equal (idOf record) expected "identity must equal the documented worked-example block"
              Expect.isFalse ((idOf record).Contains "2026-06-22T10:00:00Z") "the sensed timestamp must not appear in the identity"
          }

          test "empty-set and empty-scalar forms render as documented (art=0;, resp=0:, vdt=0:)" {
              let request = PromptIsolation.assemble (QuestionText "") []
              let record = buildOf request (modelId "") (modelVersion "") (promptHash "") [] (responseDigest "") (recordedVerdict "") []

              let r = PromptIsolation.renderedValue (PromptIsolation.render request)
              let reqByteLen = System.Text.Encoding.UTF8.GetByteCount r
              let expected = sprintf "req=%d:%s\nmid=0:\nmver=0:\npph=0:\nart=0;\nresp=0:\nvdt=0:" reqByteLen r

              Expect.equal (idOf record) expected "empty/degenerate forms render as documented"
          }

          testPropertyWithConfig fscheckConfig "the independent oracle agrees with canonicalId over arbitrary records"
          <| fun (r: ReviewRecord) -> idOf r = expectedIdentity r ]
