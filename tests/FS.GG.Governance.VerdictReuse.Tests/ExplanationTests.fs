module FS.GG.Governance.VerdictReuse.Tests.ExplanationTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model
open FS.GG.Governance.VerdictReuse.Tests.Support

// US2 (SC-002, SC-003): every `Invalidated` carries a located, non-hidden cause — `NoCachedVerdict` when no
// entry shares the request's check, else `InputsChanged (diff request prior)` — and `inputGroup` attributes
// each changed input to JudgeIdentity / PromptIdentity / CheckArtifactIdentity, so a judge change and a
// prompt change are each visible AS SUCH.

let private causeInputs decision =
    match decision with
    | Invalidated (InputsChanged inputs) -> inputs
    | other -> failtestf "expected Invalidated (InputsChanged _), got %A" other

[<Tests>]
let tests =
    testList
        "Explanation (US2 — located, attributed invalidation cause)"
        [ test "same-check entry differing in exactly one non-check input ⇒ InputsChanged [thatInput], no equal input named" {
              let store = handStore [ baseInputs, refV1 ]

              for (ri, variant) in nonCheckInputs do
                  let request = variant baseInputs

                  Expect.equal
                      (VerdictReuse.lookup request store)
                      (Invalidated(InputsChanged [ ri ]))
                      (sprintf "single %A change ⇒ exactly that input named" ri)
          }

          test "multi-input change ⇒ InputsChanged carries exactly the changed set in F035's fixed diff order, never CheckHashInput" {
              let store = handStore [ baseInputs, refV1 ]
              // change ModelVersion (2nd in encoding order) and Config (4th) ⇒ [ModelVersionInput; ModelConfigInput]
              let request =
                  { baseInputs with
                      ModelVersion = ModelVersion "20260202"
                      Config = ModelConfig "temp=1" }

              Expect.equal
                  (VerdictReuse.lookup request store)
                  (Invalidated(InputsChanged [ ModelVersionInput; ModelConfigInput ]))
                  "fixed diff order, CheckHashInput absent"
          }

          test "judge-only change ⇒ every changed input attributes to JudgeIdentity" {
              let store = handStore [ baseInputs, refV1 ]

              let request =
                  { baseInputs with
                      Model = ModelId "claude-sonnet-4"
                      ModelVersion = ModelVersion "20260202"
                      Config = ModelConfig "temp=1" }

              let groups = causeInputs (VerdictReuse.lookup request store) |> List.map Model.inputGroup
              Expect.allEqual groups JudgeIdentity "all judge inputs ⇒ JudgeIdentity"
          }

          test "prompt-only change ⇒ every changed input attributes to PromptIdentity" {
              let store = handStore [ baseInputs, refV1 ]

              let request =
                  { baseInputs with
                      PromptHash = ReviewerPromptHash "p2"
                      Question = QuestionText "different?" }

              let groups = causeInputs (VerdictReuse.lookup request store) |> List.map Model.inputGroup
              Expect.allEqual groups PromptIdentity "all prompt inputs ⇒ PromptIdentity"
          }

          test "reviewed-artifact change ⇒ attributes to CheckArtifactIdentity" {
              let store = handStore [ baseInputs, refV1 ]
              let request = variantArtifacts baseInputs
              let groups = causeInputs (VerdictReuse.lookup request store) |> List.map Model.inputGroup
              Expect.allEqual groups CheckArtifactIdentity "artifact change ⇒ CheckArtifactIdentity"
          }

          test "no entry sharing the request's check ⇒ NoCachedVerdict (distinct from InputsChanged)" {
              // store holds a verdict for a DIFFERENT rule's work only
              let otherWork = variantCheck baseInputs
              let store = handStore [ otherWork, refV1 ]
              Expect.equal (VerdictReuse.lookup baseInputs store) (Invalidated NoCachedVerdict) "different work ⇒ NoCachedVerdict"
          }

          test "question-only change ⇒ InputsChanged [QuestionTextInput], NOT NoCachedVerdict (research D5 edge)" {
              let store = handStore [ baseInputs, refV1 ]
              let request = variantQuestion baseInputs

              Expect.equal
                  (VerdictReuse.lookup request store)
                  (Invalidated(InputsChanged [ QuestionTextInput ]))
                  "same work, prompt identity moved — not a missing-cache"
          }

          test "inputGroup is total over all seven ReviewInput cases, equal to the data-model table" {
              for (ri, expected) in inputGroupTable do
                  Expect.equal (Model.inputGroup ri) expected (sprintf "%A ⇒ %A" ri expected)
          }

          testPropertyWithConfig fscheckConfig "every Invalidated carries NoCachedVerdict or a non-empty InputsChanged without CheckHashInput"
          <| fun (request: AgentReviewInputs) (store: VerdictStore) ->
              match VerdictReuse.lookup request store with
              | Valid _ -> true
              | Invalidated NoCachedVerdict -> true
              | Invalidated (InputsChanged inputs) ->
                  not (List.isEmpty inputs) && not (List.contains CheckHashInput inputs) ]
