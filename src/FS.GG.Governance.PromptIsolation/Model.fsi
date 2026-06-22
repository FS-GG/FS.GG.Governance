// Curated public signature contract for the typed vocabulary of the reviewer-prompt isolation core (F037).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body exists
// (Principle I). The vocabulary keeps trusted reviewer INSTRUCTIONS and untrusted governed-artifact CONTENT
// in two structurally distinct channels and carries each artifact only as a BOUNDED excerpt or a DIGEST. The
// reviewer-instruction channel REUSES F035's `QuestionText` and the digest-only form REUSES F029's
// `ArtifactHash` VERBATIM — brought in by the `open`s below rather than redefined (research D2, FR-007).

namespace FS.GG.Governance.PromptIsolation

open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The declared maximum size of a bounded excerpt, in CHARACTERS (UTF-16 code units / .NET
    /// String.Length). A supplied value; a negative bound is clamped to 0 by `excerpt` so capture stays
    /// total (research D4). NOTE: this character bound is distinct from the render's UTF-8 byte-length
    /// prefix (contracts/render-format.md, D5).
    type SizeBound = SizeBound of int

    /// Whether a bounded excerpt was truncated to its declared bound. Never silent (Principle VI, FR-003).
    type Truncation =
        /// Content was at or under the bound — carried in full.
        | Whole
        /// Content exceeded the bound — deterministically truncated to it.
        | Truncated

    /// Artifact content captured within a declared `SizeBound`, with its truncation status. ABSTRACT: the
    /// representation is hidden, so the ONLY way to obtain one is the `excerpt` smart constructor — which is
    /// what makes "no excerpt exceeds its bound" and "no form carries raw, unbounded content" hold BY
    /// CONSTRUCTION (FR-002, FR-003, research D3). Read with the accessors below.
    [<Sealed>]
    type BoundedExcerpt

    /// Capture supplied content into the declared bound. TOTAL. If `content.Length <= bound`, carries the
    /// content whole and marks it `Whole`; otherwise carries `content.Substring(0, bound)` and marks it
    /// `Truncated`. A negative bound is clamped to 0. Reads no file, computes no hash (FR-003, FR-006,
    /// research D4).
    val excerpt: bound: SizeBound -> content: string -> BoundedExcerpt

    /// The captured content (already within the bound). TOTAL.
    val excerptContent: excerpt: BoundedExcerpt -> string

    /// The declared bound the content was captured into. TOTAL.
    val excerptBound: excerpt: BoundedExcerpt -> SizeBound

    /// Whether the content was truncated to the bound. TOTAL.
    val excerptTruncation: excerpt: BoundedExcerpt -> Truncation

    /// The closed two-form carrier of ONE governed artifact in the data channel. There is no third,
    /// unbounded form (FR-002, research D2).
    type ArtifactPayload =
        /// Content carried as data, within bound (BoundedExcerpt is abstract ⇒ always bounded).
        | Excerpt of BoundedExcerpt
        /// The supplied digest only — no content bytes (reused F029 `ArtifactHash`).
        | DigestOnly of ArtifactHash

    /// The assembled review request: the trusted instruction channel paired with the ORDERED data channel.
    /// The two channels are different SHAPES (`QuestionText` vs `ArtifactPayload list`); there is no
    /// constructor placing artifact content into `Instructions` — separation BY CONSTRUCTION (FR-001).
    /// Artifacts preserve supplied order and duplicates (research D6).
    type ReviewRequest =
        { /// The one channel an artifact may never enter (reused F035 `QuestionText`).
          Instructions: QuestionText
          /// The data channel — ordered, duplicate-preserving.
          Artifacts: ArtifactPayload list }

    /// The deterministic, byte-stable, INJECTIVE serialization of a `ReviewRequest`, with an explicit,
    /// unspoofable fence between the instruction channel and the data channel (the F029/F032/F035 tagged,
    /// length-prefixed discipline — contracts/render-format.md). The auditable face of prompt isolation
    /// (FR-005). Equality is exact byte equality; the value is portable across runs and machines.
    type RenderedPrompt = RenderedPrompt of string
