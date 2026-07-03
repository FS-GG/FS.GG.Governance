module FS.GG.Governance.ReviewRecord.Tests.CaptureTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord.Tests.Support

// US1 (SC-001): `build` assembles one immutable record carrying all six audit facts exactly as supplied, plus
// the sensed list, none dropped/altered/invented; total over degenerate inputs.

[<Tests>]
let tests =
    testList
        "Capture"
        [ test "faithful carriage — all six facts + sensed read back exactly as supplied (example)" {
              let req = baseRequest
              let m = modelId "gpt"
              let v = modelVersion "2026-06"
              let p = promptHash "ph1"
              let arts = [ artifactHash "sha:a"; artifactHash "sha:b"; artifactHash "sha:a" ]
              let resp = responseDigest "sha:resp"
              let vdt = recordedVerdict "pass"
              let sensed = [ sensedAt "at" "2026-06-22T10:00:00Z" ]

              let r = buildOf req m v p arts resp vdt sensed

              Expect.equal
                  r.Reproducible
                  { Request = req
                    Model = m
                    ModelVersion = v
                    PromptHash = p
                    ReviewedArtifacts = arts
                    ResponseDigest = resp
                    Verdict = vdt }
                  "reproducible facts must read back exactly as supplied"

              Expect.equal r.Sensed sensed "sensed list must read back whole and in order"
              // The artifact list keeps supplied order AND duplicates (no dedup at build time).
              Expect.equal r.Reproducible.ReviewedArtifacts arts "artifacts keep supplied order + duplicates"
          }

          testPropertyWithConfig fscheckConfig "faithful carriage holds over arbitrary supplied facts (L-B2)"
          <| fun
                 (request: ReviewRequest)
                 (m: ModelId)
                 (v: ModelVersion)
                 (p: ReviewerPromptHash)
                 (arts: ArtifactHash list)
                 (resp: ResponseDigest)
                 (vdt: RecordedVerdict)
                 (sensed: SensedMetadatum list) ->
              let r = buildOf request m v p arts resp vdt sensed

              r.Reproducible.Request = request
              && r.Reproducible.Model = m
              && r.Reproducible.ModelVersion = v
              && r.Reproducible.PromptHash = p
              && r.Reproducible.ReviewedArtifacts = arts
              && r.Reproducible.ResponseDigest = resp
              && r.Reproducible.Verdict = vdt
              && r.Sensed = sensed

          testPropertyWithConfig fscheckConfig "build determinism — same args twice ⇒ structurally equal (L-B5)"
          <| fun
                 (request: ReviewRequest)
                 (m: ModelId)
                 (v: ModelVersion)
                 (p: ReviewerPromptHash)
                 (arts: ArtifactHash list)
                 (resp: ResponseDigest)
                 (vdt: RecordedVerdict)
                 (sensed: SensedMetadatum list) ->
              buildOf request m v p arts resp vdt sensed = buildOf request m v p arts resp vdt sensed

          test "totality — zero-artifact, empty digest, empty verdict, empty sensed all build ordinary records" {
              let r =
                  buildOf
                      (requestOf "q" [])
                      (modelId "")
                      (modelVersion "")
                      (promptHash "")
                      []
                      (responseDigest "")
                      (recordedVerdict "")
                      []

              Expect.equal r.Reproducible.ReviewedArtifacts [] "zero-artifact list is the ordinary empty list"
              Expect.equal r.Reproducible.ResponseDigest (responseDigest "") "empty response digest is a literal value"
              Expect.equal r.Reproducible.Verdict (recordedVerdict "") "empty verdict is a literal value"
              Expect.equal r.Sensed [] "empty sensed list is the ordinary value"
          } ]
