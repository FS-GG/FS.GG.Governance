module FS.GG.Governance.AgentReviewKey.Tests.DiffTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.AgentReviewKey.Tests.Support

// US2: `matches` decides a cache hit; `diff` (+ `inputToken`) makes a miss explainable by naming exactly
// the inputs that changed, in fixed encoding order (SC-003, FR-004/FR-005).

/// The committed readable token table (contracts/agent-review-key-api.md) — DISTINCT from the terse
/// encoding tags mid/chk/art.
let private expectedToken =
    [ ModelIdInput, "modelId"
      ModelVersionInput, "modelVersion"
      PromptHashInput, "promptHash"
      ModelConfigInput, "modelConfig"
      CheckHashInput, "checkHash"
      ReviewedArtifactsInput, "reviewedArtifacts"
      QuestionTextInput, "questionText" ]

[<Tests>]
let tests =
    testList
        "Diff"
        [ test "reflexive: matches x x and diff x x = []" {
              Expect.isTrue (AgentReviewKey.matches baseInputs baseInputs) "matches x x"
              Expect.isEmpty (AgentReviewKey.diff baseInputs baseInputs) "diff x x = []"
          }

          test "single-input change: matches=false and diff names exactly that input (all seven)" {
              for (input, vary) in allInputs do
                  let variant = vary baseInputs

                  Expect.isFalse
                      (AgentReviewKey.matches baseInputs variant)
                      (sprintf "a %s change is not a match" (Model.inputToken input))

                  Expect.equal
                      (AgentReviewKey.diff baseInputs variant)
                      [ input ]
                      (sprintf "diff names exactly [%s]" (Model.inputToken input))
          }

          test "multi-input change returns exactly the changed set in fixed encoding order" {
              // Flip model version, check hash, and question — diff must list them in encoding order.
              let variant =
                  { baseInputs with
                      ModelVersion = ModelVersion "99999999"
                      Check = RuleHash "cZ"
                      Question = QuestionText "other?" }

              Expect.equal
                  (AgentReviewKey.diff baseInputs variant)
                  [ ModelVersionInput; CheckHashInput; QuestionTextInput ]
                  "diff lists the changed inputs in fixed encoding order, none hidden, no equal input reported"
          }

          test "an artifact-only change names ReviewedArtifactsInput" {
              let variant = { baseInputs with ReviewedArtifacts = [ ArtifactHash "hZ" ] }
              Expect.equal (AgentReviewKey.diff baseInputs variant) [ ReviewedArtifactsInput ] "artifact-only diff"
          }

          test "inputToken is total and injective over all seven cases, matching the committed table" {
              for (input, token) in expectedToken do
                  Expect.equal (Model.inputToken input) token (sprintf "inputToken %A" input)

              let tokens = allInputCases |> List.map Model.inputToken
              Expect.equal (List.length (List.distinct tokens)) 7 "inputToken is injective over the seven cases"
          }

          testPropertyWithConfig fscheckConfig "matches a b = (compute a = compute b)"
          <| fun (a: AgentReviewInputs) (b: AgentReviewInputs) ->
              AgentReviewKey.matches a b = (AgentReviewKey.compute a = AgentReviewKey.compute b)

          testPropertyWithConfig fscheckConfig "diff a b = [] iff matches a b"
          <| fun (a: AgentReviewInputs) (b: AgentReviewInputs) ->
              List.isEmpty (AgentReviewKey.diff a b) = AgentReviewKey.matches a b ]
