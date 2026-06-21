// Agent-review input vocabulary for the agent-review verdict cache-key core (F035). The public surface is
// fixed by Model.fsi (Principle II); no top-level binding here carries an access modifier. These are
// product-neutral, comparable values that `AgentReviewKey.compute` fingerprints; the CHECK hash reuses
// F029's `RuleHash` and the reviewed-artifact hashes reuse F029's `ArtifactHash` verbatim (research D2,
// FR-008) rather than redefining them.

namespace FS.GG.Governance.AgentReviewKey

open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ModelId = ModelId of string

    type ModelVersion = ModelVersion of string

    type ReviewerPromptHash = ReviewerPromptHash of string

    type ModelConfig = ModelConfig of string

    type QuestionText = QuestionText of string

    type AgentReviewInputs =
        { Model: ModelId
          ModelVersion: ModelVersion
          Config: ModelConfig
          PromptHash: ReviewerPromptHash
          Question: QuestionText
          Check: RuleHash
          ReviewedArtifacts: ArtifactHash list }

    type CacheKey = CacheKey of string

    type ReviewInput =
        | ModelIdInput
        | ModelVersionInput
        | PromptHashInput
        | ModelConfigInput
        | CheckHashInput
        | ReviewedArtifactsInput
        | QuestionTextInput

    let inputToken (input: ReviewInput) : string =
        match input with
        | ModelIdInput -> "modelId"
        | ModelVersionInput -> "modelVersion"
        | PromptHashInput -> "promptHash"
        | ModelConfigInput -> "modelConfig"
        | CheckHashInput -> "checkHash"
        | ReviewedArtifactsInput -> "reviewedArtifacts"
        | QuestionTextInput -> "questionText"
