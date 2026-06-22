// Verdict-store + invalidation vocabulary for the agent-reviewed verdict store core (F036). The public
// surface is fixed by Model.fsi (Principle II); no top-level binding here carries an access modifier. These
// are product-neutral, comparable values; the F035 agent-review vocabulary (`AgentReviewInputs`,
// `ReviewInput`) is reused verbatim via the `open` below (research D2, FR-010) rather than redefined.

namespace FS.GG.Governance.VerdictReuse

open FS.GG.Governance.AgentReviewKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type VerdictRef = VerdictRef of string

    type CachedVerdict =
        { Inputs: AgentReviewInputs
          Verdict: VerdictRef }

    type VerdictStore = VerdictStore of CachedVerdict list

    type IdentityGroup =
        | JudgeIdentity
        | PromptIdentity
        | CheckArtifactIdentity

    let inputGroup (input: ReviewInput) : IdentityGroup =
        match input with
        | ModelIdInput -> JudgeIdentity
        | ModelVersionInput -> JudgeIdentity
        | ModelConfigInput -> JudgeIdentity
        | PromptHashInput -> PromptIdentity
        | QuestionTextInput -> PromptIdentity
        | CheckHashInput -> CheckArtifactIdentity
        | ReviewedArtifactsInput -> CheckArtifactIdentity

    type InvalidationCause =
        | NoCachedVerdict
        | InputsChanged of ReviewInput list

    type LookupDecision =
        | Valid of VerdictRef
        | Invalidated of InvalidationCause
