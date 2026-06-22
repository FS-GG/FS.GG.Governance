// Curated public signature contract for the typed vocabulary of the auditable review-record core (F038).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body exists
// (Principle I). The vocabulary captures ONE completed agent review as an immutable, auditable record — the
// six reproducible audit facts the design names plus any sensed metadata held structurally apart (the
// F032/F033 honesty boundary). It REUSES F037's `ReviewRequest` (the prompt-isolated review request), F035's
// `ModelId`/`ModelVersion`/`ReviewerPromptHash` (model + prompt identity), F029's `ArtifactHash` (one
// reviewed-artifact digest), and F034's `SensedMetadatum` (a sensed timestamp/duration) VERBATIM — brought in
// by the `open`s below rather than redefined (research D2) — and introduces only the three new newtypes below.

namespace FS.GG.Governance.ReviewRecord

open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.SensedMetadata.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The supplied hash standing in for the reviewer's response — *what came back* — carrying identity
    /// without response bytes. Opaque and comparable: the F029/F032 opaque-token discipline (no validation, no
    /// parsing; an EMPTY string is a literal value, distinct from a non-empty one). Carries NO response bytes
    /// (FR-001, US3, SC-004).
    type ResponseDigest = ResponseDigest of string

    /// The final verdict produced by the review, carried as an OPAQUE recorded fact — *what answer was
    /// recorded*. This core NEVER interprets, compares, thresholds, or promotes it (FR-007, research D3); an
    /// empty string is a literal value. Deliberately NOT a structured DU — interpreting the verdict belongs to
    /// the fifth row (advisory promotion) and the sixth (calibration), not here.
    type RecordedVerdict = RecordedVerdict of string

    /// The byte-stable canonical identity over the record's reproducible facts (FR-003). The wrapped string is
    /// the canonical rendering (contracts/review-record-identity-format.md); equality is exact byte equality
    /// and the value is portable across runs and machines. Mirrors F032 `CommandIdentity` and F033
    /// `ProvenanceIdentity`.
    type RecordIdentity = RecordIdentity of string

    /// The reproducible facts of a completed review — the addressable "reproducible part of the review" value
    /// and the SOLE input to `canonicalId` (the sensed metadata is deliberately absent, the F032
    /// `ReproducibleFacts` discipline). These are exactly the SIX audit facts the design names (FR-001):
    /// review request, response digest, model identity (`Model` + `ModelVersion`), prompt identity, artifact
    /// digests, and final verdict. `ReviewedArtifacts` is carried in supplied order/duplicates but compared as
    /// a SET in identity (research D4); a review over ZERO artifacts is the ordinary empty list, never
    /// malformed (Edge Cases).
    type ReproducibleFacts =
        { /// F037 — the prompt-isolated review request; its identity contribution is
          /// `PromptIsolation.render request` (research D5).
          Request: ReviewRequest
          /// F035 — the judge's id; half of model identity.
          Model: ModelId
          /// F035 — the judge's version; half of model identity.
          ModelVersion: ModelVersion
          /// F035 — the reviewer-prompt hash; prompt identity.
          PromptHash: ReviewerPromptHash
          /// F029 — the reviewed-artifact content hashes; carried verbatim, compared as a SET in identity.
          ReviewedArtifacts: ArtifactHash list
          /// New — the supplied hash of the reviewer's response; carries NO response bytes.
          ResponseDigest: ResponseDigest
          /// New — the final verdict, an opaque recorded fact, never interpreted here.
          Verdict: RecordedVerdict }

    /// The complete, immutable review record (FR-001): all six reproducible audit facts plus any sensed
    /// metadata, none dropped or optional-by-omission. The sensed metadata is a SEPARATE field of a distinct
    /// shape (the F032 `{ Reproducible; Duration }` split, with F034's richer carrier): reachable as
    /// `record.Sensed`, structurally apart from `record.Reproducible`, and structurally EXCLUDED from
    /// `canonicalId` (research D6). An empty `Sensed` list is the ordinary "no sensed metadata attached"
    /// value. The record carries NO raw response bytes and NO raw, unbounded artifact bytes — artifact content
    /// appears only inside the F037-bounded `Request` (whose excerpts are bounded by construction), and the
    /// response appears only as `Reproducible.ResponseDigest` (FR-001, US3, SC-004).
    type ReviewRecord =
        { Reproducible: ReproducibleFacts
          Sensed: SensedMetadatum list }
