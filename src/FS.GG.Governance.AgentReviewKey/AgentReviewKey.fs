// Agent-review verdict cache-key operations for the agent-review cache-key core (F035). The public surface
// is fixed by AgentReviewKey.fsi (Principle II); no top-level binding here carries an access modifier. All
// four operations are pure, total, and deterministic (FR-002, FR-007): no clock, filesystem, git,
// environment, or network; no model invoked, no bytes hashed; identical inputs (reviewed artifacts compared
// as a set) always render the identical key. BCL string building only (FR-011). The canonical encoding is
// fixed by contracts/agent-review-key-format.md.

namespace FS.GG.Governance.AgentReviewKey

open System.Text
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AgentReviewKey =

    // ── Segment encoders (internal; hidden by AgentReviewKey.fsi) ──

    // UTF-8 byte length of a value — the length prefix that makes the encoding injective: a reader knows
    // exactly how many bytes a value occupies, so no value can masquerade as another field (FR-003).
    let byteLen (s: string) : int = Encoding.UTF8.GetByteCount s

    // A required string value: "<tag>=<byteLen>:<value>". Unlike F029, every input here is REQUIRED, so
    // there is NO presence digit (contracts/agent-review-key-format.md).
    let seg (tag: string) (s: string) : string =
        let sb = StringBuilder()
        sb.Append(tag).Append('=').Append(byteLen s).Append(':').Append(s) |> ignore
        sb.ToString()

    // The canonical SET of reviewed-artifact strings: unwrap, deduplicate, ordinal-sort.
    let artifactSet (arts: ArtifactHash list) : string list =
        arts
        |> List.map (fun (ArtifactHash h) -> h)
        |> List.distinct
        |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))

    // The reviewed-artifact segment: "art=<count>;<len1>:<v1>;<len2>:<v2>;…" (empty set ⇒ "art=0;"). The
    // count first removes the empty-vs-one-empty-element ambiguity; each element is still length-prefixed
    // for injectivity.
    let artSegment (arts: ArtifactHash list) : string =
        let elems = artifactSet arts
        // "art=<count>;" then the elements joined by ';' with NO trailing ';' (empty set ⇒ "art=0;",
        // a two-element set ⇒ "art=2;2:h1;2:h2").
        let body =
            elems
            |> List.map (fun v ->
                let e = StringBuilder()
                e.Append(byteLen v).Append(':').Append(v).ToString())
            |> String.concat ";"

        let sb = StringBuilder()
        sb.Append("art=").Append(List.length elems).Append(';').Append(body).ToString()

    let compute (inputs: AgentReviewInputs) : CacheKey =
        let (ModelId modelId) = inputs.Model
        let (ModelVersion modelVersion) = inputs.ModelVersion
        let (ReviewerPromptHash promptHash) = inputs.PromptHash
        let (ModelConfig config) = inputs.Config
        let (RuleHash check) = inputs.Check
        let (QuestionText question) = inputs.Question

        // Fixed field order, joined by '\n', no trailing newline (contracts/agent-review-key-format.md).
        [ seg "mid" modelId
          seg "mver" modelVersion
          seg "prompt" promptHash
          seg "cfg" config
          seg "chk" check
          artSegment inputs.ReviewedArtifacts
          seg "q" question ]
        |> String.concat "\n"
        |> CacheKey

    let matches (a: AgentReviewInputs) (b: AgentReviewInputs) : bool = compute a = compute b

    let diff (a: AgentReviewInputs) (b: AgentReviewInputs) : ReviewInput list =
        // Compare input-by-input in the fixed encoding order; reviewed artifacts compared as a SET so a
        // reorder/duplicate is never reported. Returns exactly the differing inputs (FR-005).
        [ if a.Model <> b.Model then ModelIdInput
          if a.ModelVersion <> b.ModelVersion then ModelVersionInput
          if a.PromptHash <> b.PromptHash then PromptHashInput
          if a.Config <> b.Config then ModelConfigInput
          if a.Check <> b.Check then CheckHashInput
          if artifactSet a.ReviewedArtifacts <> artifactSet b.ReviewedArtifacts then ReviewedArtifactsInput
          if a.Question <> b.Question then QuestionTextInput ]

    let value (key: CacheKey) : string =
        let (CacheKey s) = key
        s
