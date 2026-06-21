module FS.GG.Governance.AgentReviewKey.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.AgentReviewKey.Tests.Support

// US3: byte-stable determinism — key/matches/diff are byte-identical on repeat (SC-004).

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "value (compute x) is byte-identical on repeat" {
              let once = AgentReviewKey.value (AgentReviewKey.compute baseInputs)
              let twice = AgentReviewKey.value (AgentReviewKey.compute baseInputs)
              Expect.equal once twice "repeated compute is byte-identical"
          }

          test "matches and diff over the same inputs are identical on repeat" {
              let variant = { baseInputs with Question = QuestionText "changed?" }
              Expect.equal (AgentReviewKey.matches baseInputs variant) (AgentReviewKey.matches baseInputs variant) "matches stable"
              Expect.equal (AgentReviewKey.diff baseInputs variant) (AgentReviewKey.diff baseInputs variant) "diff stable"
          }

          testPropertyWithConfig fscheckConfig "compute is deterministic over generated inputs"
          <| fun (inputs: AgentReviewInputs) ->
              AgentReviewKey.compute inputs = AgentReviewKey.compute inputs ]
