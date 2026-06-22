// Typed vocabulary for the auditable review-record core (F038). The public surface is fixed by Model.fsi
// (Principle II); no top-level binding here carries an access modifier. The review request reuses F037's
// `ReviewRequest`, the model/prompt identity reuses F035's `ModelId`/`ModelVersion`/`ReviewerPromptHash`, the
// reviewed-artifact digest reuses F029's `ArtifactHash`, and the sensed metadatum reuses F034's
// `SensedMetadatum` verbatim (research D2) rather than redefining them. The three new newtypes + the two
// records below are plain immutable data — no smart constructor, no hidden representation.

namespace FS.GG.Governance.ReviewRecord

open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.SensedMetadata.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ResponseDigest = ResponseDigest of string

    type RecordedVerdict = RecordedVerdict of string

    type RecordIdentity = RecordIdentity of string

    type ReproducibleFacts =
        { Request: ReviewRequest
          Model: ModelId
          ModelVersion: ModelVersion
          PromptHash: ReviewerPromptHash
          ReviewedArtifacts: ArtifactHash list
          ResponseDigest: ResponseDigest
          Verdict: RecordedVerdict }

    type ReviewRecord =
        { Reproducible: ReproducibleFacts
          Sensed: SensedMetadatum list }
