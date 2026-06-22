// Typed vocabulary for the reviewer-prompt isolation core (F037). The public surface is fixed by Model.fsi
// (Principle II); no top-level binding here carries an access modifier. The reviewer-instruction channel
// reuses F035's `QuestionText` and the digest-only form reuses F029's `ArtifactHash` verbatim (research D2,
// FR-007) rather than redefining them. `BoundedExcerpt` is declared abstract in Model.fsi, so its record
// representation is hidden — the `excerpt` smart constructor is the only way to build one, which is what
// makes "no excerpt exceeds its bound" and "no form carries raw, unbounded content" hold by construction
// (FR-002, FR-003, research D3).

namespace FS.GG.Governance.PromptIsolation

open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type SizeBound = SizeBound of int

    type Truncation =
        | Whole
        | Truncated

    // Representation hidden by Model.fsi's `[<Sealed>] type BoundedExcerpt`. Built only by `excerpt`.
    type BoundedExcerpt =
        { Content: string
          Bound: SizeBound
          Truncation: Truncation }

    let excerpt (SizeBound n) (content: string) : BoundedExcerpt =
        // Clamp a negative bound to 0 so capture stays total (research D4).
        let n = max 0 n

        if content.Length <= n then
            { Content = content
              Bound = SizeBound n
              Truncation = Whole }
        else
            { Content = content.Substring(0, n)
              Bound = SizeBound n
              Truncation = Truncated }

    let excerptContent (excerpt: BoundedExcerpt) : string = excerpt.Content

    let excerptBound (excerpt: BoundedExcerpt) : SizeBound = excerpt.Bound

    let excerptTruncation (excerpt: BoundedExcerpt) : Truncation = excerpt.Truncation

    type ArtifactPayload =
        | Excerpt of BoundedExcerpt
        | DigestOnly of ArtifactHash

    type ReviewRequest =
        { Instructions: QuestionText
          Artifacts: ArtifactPayload list }

    type RenderedPrompt = RenderedPrompt of string
