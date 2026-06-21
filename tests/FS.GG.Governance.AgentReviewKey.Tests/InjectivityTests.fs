module FS.GG.Governance.AgentReviewKey.Tests.InjectivityTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.AgentReviewKey.Tests.Support

// US1/US2: the length-prefixed tagged encoding is INJECTIVE — the same opaque string placed in two
// different inputs yields different keys, and no separator-bearing or empty token can spoof a field
// boundary (SC-005, FR-003).

[<Tests>]
let tests =
    testList
        "Injectivity"
        [ test "moving the same string between two different inputs changes the key" {
              // Put "shared" in the model id vs in the question text — must not collide.
              let inModel =
                  { baseInputs with Model = ModelId "shared"; Question = QuestionText "q" }

              let inQuestion =
                  { baseInputs with Model = ModelId "q"; Question = QuestionText "shared" }

              Expect.notEqual
                  (AgentReviewKey.compute inModel)
                  (AgentReviewKey.compute inQuestion)
                  "a string in model id must not key the same as the same string in question text"
          }

          test "tokens containing :/=/;/\\n cannot spoof a field boundary" {
              // A model id whose text mimics the start of the next segment must stay inside its own segment.
              let spoof =
                  { baseInputs with Model = ModelId "x\nmver=8:99999999"; ModelVersion = ModelVersion "20260101" }

              let honest =
                  { baseInputs with Model = ModelId "x"; ModelVersion = ModelVersion "99999999" }

              Expect.notEqual
                  (AgentReviewKey.compute spoof)
                  (AgentReviewKey.compute honest)
                  "a newline/tag-bearing token is length-counted, never read as a boundary"
          }

          test "an empty token encodes a distinct segment that never collides with another field" {
              let emptyModel = { baseInputs with Model = ModelId "" }
              let key = AgentReviewKey.value (AgentReviewKey.compute emptyModel)
              Expect.stringContains key "mid=0:" "an empty token renders <tag>=0: , a present zero-length value"
          }

          test "an artifact token containing ';' stays within the art segment" {
              let tricky = { baseInputs with ReviewedArtifacts = [ ArtifactHash "h;1" ] }
              let plain = { baseInputs with ReviewedArtifacts = [ ArtifactHash "h"; ArtifactHash "1" ] }

              Expect.notEqual
                  (AgentReviewKey.compute tricky)
                  (AgentReviewKey.compute plain)
                  "the ';' inside an artifact value is length-counted, not a set separator"
          }

          testPropertyWithConfig fscheckConfig "distinct input sets (modulo artifact-set) yield distinct keys"
          <| fun (a: AgentReviewInputs) (b: AgentReviewInputs) ->
              // Compare a and b on the SET-normalized identity used by the key. If they agree on every
              // input as the key sees them, the keys must be equal; otherwise distinct.
              let sameAsKeySees = AgentReviewKey.diff a b |> List.isEmpty
              (AgentReviewKey.compute a = AgentReviewKey.compute b) = sameAsKeySees ]
