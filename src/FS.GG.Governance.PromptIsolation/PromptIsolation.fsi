// Curated public signature contract for the operations of the reviewer-prompt isolation core (F037).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// PromptIsolation.fs carries NO access modifiers. The three operations are pure, total, and deterministic
// (FR-004, FR-005, FR-006): no clock, filesystem, git, environment, or network; no model/agent invoked; no
// bytes hashed (digests are supplied tokens). The canonical injective render is fixed by
// contracts/render-format.md.

namespace FS.GG.Governance.PromptIsolation

open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.PromptIsolation.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PromptIsolation =

    /// Pair the trusted instruction channel with the ordered data channel. TOTAL over all supplied values,
    /// including an empty sequence and empty/boundary-length content (FR-004). Performs NO reorder,
    /// de-duplication, capture, hashing, or I/O — it pairs the two already-formed channels (research D6).
    val assemble: instructions: QuestionText -> artifacts: ArtifactPayload list -> ReviewRequest

    /// Render a review request to its canonical `RenderedPrompt`. PURE, TOTAL, DETERMINISTIC, INJECTIVE
    /// (FR-005, FR-006): reads no clock/filesystem/git/environment/network, invokes no model, hashes no
    /// bytes; identical requests render byte-identically; no artifact content can break the fence
    /// (contracts/render-format.md).
    val render: request: ReviewRequest -> RenderedPrompt

    /// Unwrap a `RenderedPrompt` to its canonical string (for handoff, messages, tests). TOTAL.
    val renderedValue: prompt: RenderedPrompt -> string
