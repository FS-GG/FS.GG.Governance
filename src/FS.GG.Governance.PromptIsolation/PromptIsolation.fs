// Reviewer-prompt isolation operations for the prompt-isolation core (F037). The public surface is fixed by
// PromptIsolation.fsi (Principle II); no top-level binding here carries an access modifier. All three
// operations are pure, total, and deterministic (FR-004, FR-005, FR-006): no clock, filesystem, git,
// environment, or network; no model invoked, no bytes hashed; identical requests always render the identical
// prompt. BCL string building only (FR-010). The canonical injective render is fixed by
// contracts/render-format.md, reusing the F035 `byteLen` (UTF-8 length-prefix) discipline applied to the
// prompt's two channels and its payload forms.

namespace FS.GG.Governance.PromptIsolation

open System.Text
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.PromptIsolation.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PromptIsolation =

    // ── Segment encoders (internal; hidden by PromptIsolation.fsi) ──

    // UTF-8 byte length of a value — the length prefix that makes the encoding injective: a reader consumes
    // exactly that many bytes for the value, so no value can masquerade as another field or as a structural
    // marker (FR-005, the F035 `byteLen` helper verbatim).
    let byteLen (s: string) : int = Encoding.UTF8.GetByteCount s

    // One artifact payload: "exc=<flag>,<byteLen>:<content>" or "dig=<byteLen>:<hash>". The content/hash is
    // read by length, so any structural character or marker inside it is consumed as data
    // (contracts/render-format.md).
    let payloadSegment (payload: ArtifactPayload) : string =
        match payload with
        | Excerpt e ->
            let content = excerptContent e

            let flag =
                match excerptTruncation e with
                | Whole -> "w"
                | Truncated -> "t"

            let sb = StringBuilder()

            sb
                .Append("exc=")
                .Append(flag)
                .Append(',')
                .Append(byteLen content)
                .Append(':')
                .Append(content)
                .ToString()
        | DigestOnly(ArtifactHash h) ->
            let sb = StringBuilder()
            sb.Append("dig=").Append(byteLen h).Append(':').Append(h).ToString()

    // The data segment: "art=<count>;<payload-1>;<payload-2>;…" in SUPPLIED ORDER, NO dedup/sort (research
    // D6, the deliberate contrast with F035's artifact set). Empty channel ⇒ "art=0;".
    let dataSegment (artifacts: ArtifactPayload list) : string =
        let body = artifacts |> List.map payloadSegment |> String.concat ";"

        let sb = StringBuilder()
        sb.Append("art=").Append(List.length artifacts).Append(';').Append(body).ToString()

    let assemble (instructions: QuestionText) (artifacts: ArtifactPayload list) : ReviewRequest =
        // Pair the two already-formed channels verbatim — no reorder, de-duplication, capture, hashing, or
        // I/O (FR-004, research D6).
        { Instructions = instructions
          Artifacts = artifacts }

    let render (request: ReviewRequest) : RenderedPrompt =
        let (QuestionText instructions) = request.Instructions

        // Instruction segment "instr=<byteLen>:<instructions>" then the data segment, joined by a single
        // '\n' with no trailing newline (contracts/render-format.md). The length-prefixed instruction value
        // fixes the instruction/data boundary, so instructions containing "\nart=…" cannot shorten or extend
        // the trusted channel.
        let instructionSegment =
            let sb = StringBuilder()
            sb.Append("instr=").Append(byteLen instructions).Append(':').Append(instructions).ToString()

        [ instructionSegment; dataSegment request.Artifacts ]
        |> String.concat "\n"
        |> RenderedPrompt

    let renderedValue (prompt: RenderedPrompt) : string =
        let (RenderedPrompt s) = prompt
        s
