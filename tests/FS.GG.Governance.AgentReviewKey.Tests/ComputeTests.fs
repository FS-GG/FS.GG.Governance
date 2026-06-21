module FS.GG.Governance.AgentReviewKey.Tests.ComputeTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.AgentReviewKey.Tests.Support

// US1: `compute` (+ `value`) turns the seven supplied inputs into one byte-stable, injective `CacheKey`.
// Identical inputs ⇒ byte-identical key; any single differing input ⇒ a different key (SC-001). Plus the
// byte-exact worked-example pin to contracts/agent-review-key-format.md.

[<Tests>]
let tests =
    testList
        "Compute"
        [ test "identical inputs yield a byte-identical key" {
              Expect.equal
                  (AgentReviewKey.value (AgentReviewKey.compute baseInputs))
                  (AgentReviewKey.value (AgentReviewKey.compute baseInputs))
                  "compute is a function: equal inputs ⇒ equal key"
          }

          test "changing exactly one input changes the key (all seven)" {
              let baseKey = AgentReviewKey.compute baseInputs

              for (input, vary) in allInputs do
                  let variedKey = AgentReviewKey.compute (vary baseInputs)

                  Expect.notEqual
                      variedKey
                      baseKey
                      (sprintf "changing %s must change the key" (Model.inputToken input))
          }

          test "the key carries all seven inputs in the fixed encoding order" {
              // Every tag appears, in order, exactly once (mid, mver, prompt, cfg, chk, art, q).
              let key = AgentReviewKey.value (AgentReviewKey.compute baseInputs)
              let lines = key.Split('\n')
              Expect.equal lines.Length 7 "seven segments, one per line"
              Expect.isTrue (lines.[0].StartsWith "mid=") "segment 1 is mid"
              Expect.isTrue (lines.[1].StartsWith "mver=") "segment 2 is mver"
              Expect.isTrue (lines.[2].StartsWith "prompt=") "segment 3 is prompt"
              Expect.isTrue (lines.[3].StartsWith "cfg=") "segment 4 is cfg"
              Expect.isTrue (lines.[4].StartsWith "chk=") "segment 5 is chk"
              Expect.isTrue (lines.[5].StartsWith "art=") "segment 6 is art"
              Expect.isTrue (lines.[6].StartsWith "q=") "segment 7 is q"
          }

          test "worked-example byte-pin equals the format contract" {
              Expect.equal
                  (AgentReviewKey.value (AgentReviewKey.compute baseInputs))
                  exampleKey
                  "the documented example must produce the byte-exact key in agent-review-key-format.md"
          }

          test "empty reviewed-artifact set renders art=0; and never collides with a one-artifact set" {
              let empty = { baseInputs with ReviewedArtifacts = [] }
              let key = AgentReviewKey.value (AgentReviewKey.compute empty)
              Expect.stringContains key "\nart=0;\n" "the empty set renders the distinct art=0; segment"

              let oneEmptyToken = { baseInputs with ReviewedArtifacts = [ ArtifactHash "" ] }

              Expect.notEqual
                  (AgentReviewKey.compute empty)
                  (AgentReviewKey.compute oneEmptyToken)
                  "the empty set must NOT collide with a set holding one empty-string artifact"
          }

          test "every AgentReviewInputs value yields a CacheKey without throwing (totality)" {
              // Empty tokens, separator-bearing tokens, and an empty artifact set are all ordinary values.
              let weird =
                  { Model = ModelId ""
                    ModelVersion = ModelVersion "x:y=z\n;"
                    Config = ModelConfig ""
                    PromptHash = ReviewerPromptHash "art=2;"
                    Question = QuestionText ""
                    Check = RuleHash ":"
                    ReviewedArtifacts = [] }

              let key = AgentReviewKey.value (AgentReviewKey.compute weird)
              Expect.isTrue (key.Length > 0) "a non-empty canonical key is produced for any value"
          } ]
