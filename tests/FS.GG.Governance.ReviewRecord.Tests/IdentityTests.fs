module FS.GG.Governance.ReviewRecord.Tests.IdentityTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.ReviewRecord
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord.Tests.Support

// US2 (SC-002): `canonicalId` is deterministic + INJECTIVE over the reproducible facts; `identityValue`
// unwraps. Identical reproducible facts ⇒ byte-equal identity; any single differing reproducible fact ⇒ a
// different identity.

let private idOf (r: ReviewRecord) : string = ReviewRecord.identityValue (ReviewRecord.canonicalId r)

/// A baseline record every injectivity case varies exactly one reproducible fact from.
let private baseRecord: ReviewRecord =
    buildOf
        baseRequest
        (modelId "gpt")
        (modelVersion "2026-06")
        (promptHash "ph1")
        [ artifactHash "sha:a" ]
        (responseDigest "sha:resp")
        (recordedVerdict "pass")
        [ sensedAt "at" "2026-06-22T10:00:00Z" ]

[<Tests>]
let tests =
    testList
        "Identity"
        [ testPropertyWithConfig fscheckConfig "stability — identical reproducible facts ⇒ byte-equal id (L-I3)"
          <| fun (repro: ReproducibleFacts) (s1: int) (s2: int) ->
              // Two records sharing reproducible facts but differing sensed lengths still match.
              let a = { Reproducible = repro; Sensed = [ sensedAt "a" (string s1) ] }
              let b = { Reproducible = repro; Sensed = [ sensedAt "b" (string s2); sensedAt "c" "z" ] }
              idOf a = idOf b

          test "injectivity — changing exactly one reproducible fact changes the identity (L-I4)" {
              let baseId = idOf baseRecord

              let variants =
                  [ "request-instructions", { baseRecord with Reproducible = { baseRecord.Reproducible with Request = requestOf "different question?" [] } }
                    "request-payload", { baseRecord with Reproducible = { baseRecord.Reproducible with Request = requestOf "Does this doc explain the public API?" [ digestPayload "sha:a" ] } }
                    "model", { baseRecord with Reproducible = { baseRecord.Reproducible with Model = modelId "claude" } }
                    "modelVersion", { baseRecord with Reproducible = { baseRecord.Reproducible with ModelVersion = modelVersion "2026-07" } }
                    "promptHash", { baseRecord with Reproducible = { baseRecord.Reproducible with PromptHash = promptHash "ph2" } }
                    "artifact", { baseRecord with Reproducible = { baseRecord.Reproducible with ReviewedArtifacts = [ artifactHash "sha:z" ] } }
                    "responseDigest", { baseRecord with Reproducible = { baseRecord.Reproducible with ResponseDigest = responseDigest "sha:other" } }
                    "verdict", { baseRecord with Reproducible = { baseRecord.Reproducible with Verdict = recordedVerdict "fail" } } ]

              for name, v in variants do
                  Expect.notEqual (idOf v) baseId (sprintf "changing %s must change the identity" name)
          }

          test "independence — identically-rendering requests but differing model/prompt ⇒ different ids (L-I7)" {
              let req = requestOf "same" []
              let mk m v p = buildOf req (modelId m) (modelVersion v) (promptHash p) [] (responseDigest "r") (recordedVerdict "x") []

              Expect.notEqual (idOf (mk "a" "1" "p")) (idOf (mk "b" "1" "p")) "differing model id ⇒ different id"
              Expect.notEqual (idOf (mk "a" "1" "p")) (idOf (mk "a" "2" "p")) "differing model version ⇒ different id"
              Expect.notEqual (idOf (mk "a" "1" "p")) (idOf (mk "a" "1" "q")) "differing prompt hash ⇒ different id"
          }

          test "cross-field injectivity — same string in two different fields ⇒ different ids (L-I6)" {
              let shared = "collision"
              // shared as ResponseDigest vs as Verdict.
              let asResp = buildOf baseRequest (modelId "m") (modelVersion "v") (promptHash "p") [] (responseDigest shared) (recordedVerdict "x") []
              let asVdt = buildOf baseRequest (modelId "m") (modelVersion "v") (promptHash "p") [] (responseDigest "x") (recordedVerdict shared) []
              Expect.notEqual (idOf asResp) (idOf asVdt) "same string as ResponseDigest vs Verdict ⇒ different ids"

              // shared as ModelVersion vs PromptHash.
              let asMver = buildOf baseRequest (modelId "m") (modelVersion shared) (promptHash "x") [] (responseDigest "r") (recordedVerdict "x") []
              let asPph = buildOf baseRequest (modelId "m") (modelVersion "x") (promptHash shared) [] (responseDigest "r") (recordedVerdict "x") []
              Expect.notEqual (idOf asMver) (idOf asPph) "same string as ModelVersion vs PromptHash ⇒ different ids"
          }

          test "fence-hostile content is read as data, forges no boundary (L-I6)" {
              let a = buildOf baseRequest (modelId fenceHostileText) (modelVersion "v") (promptHash "p") [] (responseDigest "r") (recordedVerdict "x") []
              let b = buildOf baseRequest (modelId "v") (modelVersion fenceHostileText) (promptHash "p") [] (responseDigest "r") (recordedVerdict "x") []
              // The hostile string placed in different fields still yields different identities.
              Expect.notEqual (idOf a) (idOf b) "tag/separator/fence chars cannot forge a field boundary"
          }

          testPropertyWithConfig fscheckConfig "identityValue round-trips canonicalId (L-V1)"
          <| fun (r: ReviewRecord) ->
              let (RecordIdentity s) = ReviewRecord.canonicalId r
              ReviewRecord.identityValue (ReviewRecord.canonicalId r) = s ]
