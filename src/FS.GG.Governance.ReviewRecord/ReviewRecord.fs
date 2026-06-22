// Auditable review-record operations for the review-record core (F038). The public surface is fixed by
// ReviewRecord.fsi (Principle II); no top-level binding here carries an access modifier. All three operations
// are pure, total, and deterministic (FR-002, FR-005): no clock, filesystem, git, environment, or network; no
// model invoked, no bytes hashed; identical inputs (reviewed artifacts compared as a set) always yield the
// identical record and identical identity. BCL string building only (plus the pure `PromptIsolation.render`
// for the request segment). The canonical injective identity is fixed by
// contracts/review-record-identity-format.md, reusing the F035 `byteLen`/`seg`/`artSegment` discipline.

namespace FS.GG.Governance.ReviewRecord

open System.Text
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.ReviewRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReviewRecord =

    // ── Segment encoders (internal; hidden by ReviewRecord.fsi) ──

    // UTF-8 byte length of a value — the length prefix that makes the encoding injective: a reader consumes
    // exactly that many bytes for the value, so no value can masquerade as another field or as a structural
    // marker (FR-003, the F035 `byteLen` helper verbatim).
    let byteLen (s: string) : int = Encoding.UTF8.GetByteCount s

    // A required string value: "<tag>=<byteLen>:<value>". Every reproducible field is required, so there is
    // NO presence digit (contracts/review-record-identity-format.md, the F035 `seg` idiom verbatim).
    let seg (tag: string) (s: string) : string =
        let sb = StringBuilder()
        sb.Append(tag).Append('=').Append(byteLen s).Append(':').Append(s) |> ignore
        sb.ToString()

    // The canonical SET of reviewed-artifact strings: unwrap, deduplicate, ordinal-sort (research D4).
    let artifactSet (arts: ArtifactHash list) : string list =
        arts
        |> List.map (fun (ArtifactHash h) -> h)
        |> List.distinct
        |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))

    // The reviewed-artifact segment: "art=<count>;<len1>:<v1>;<len2>:<v2>;…" (empty set ⇒ "art=0;"). The
    // count first removes the empty-vs-one-empty-element ambiguity; each element is length-prefixed for
    // injectivity (the F035 `artSegment` idiom verbatim).
    let artSegment (arts: ArtifactHash list) : string =
        let elems = artifactSet arts

        let body =
            elems
            |> List.map (fun v ->
                let e = StringBuilder()
                e.Append(byteLen v).Append(':').Append(v).ToString())
            |> String.concat ";"

        let sb = StringBuilder()
        sb.Append("art=").Append(List.length elems).Append(';').Append(body).ToString()

    let build
        (request: ReviewRequest)
        (model: ModelId)
        (modelVersion: ModelVersion)
        (promptHash: ReviewerPromptHash)
        (reviewedArtifacts: ArtifactHash list)
        (responseDigest: ResponseDigest)
        (verdict: RecordedVerdict)
        (sensed: SensedMetadatum list)
        : ReviewRecord =
        // Assemble the supplied facts verbatim — NO reorder, dedup, normalization, capture, hashing, or I/O
        // (L-B4, FR-005). Plain record construction; sensed held structurally apart (L-B3).
        { Reproducible =
            { Request = request
              Model = model
              ModelVersion = modelVersion
              PromptHash = promptHash
              ReviewedArtifacts = reviewedArtifacts
              ResponseDigest = responseDigest
              Verdict = verdict }
          Sensed = sensed }

    let canonicalId (record: ReviewRecord) : RecordIdentity =
        // Render record.Reproducible (NEVER record.Sensed, L-I2) as length-prefixed tagged segments joined by
        // '\n', no trailing newline, in fixed order (contracts/review-record-identity-format.md). The `req`
        // segment carries F037's own injective render of the embedded request — its inner '\n' is consumed
        // inside `req` by length (the F033 embedded-record analogue).
        let r = record.Reproducible
        let rendered = PromptIsolation.renderedValue (PromptIsolation.render r.Request)
        let (ModelId modelId) = r.Model
        let (ModelVersion modelVersion) = r.ModelVersion
        let (ReviewerPromptHash promptHash) = r.PromptHash
        let (ResponseDigest responseDigest) = r.ResponseDigest
        let (RecordedVerdict verdict) = r.Verdict

        [ seg "req" rendered
          seg "mid" modelId
          seg "mver" modelVersion
          seg "pph" promptHash
          artSegment r.ReviewedArtifacts
          seg "resp" responseDigest
          seg "vdt" verdict ]
        |> String.concat "\n"
        |> RecordIdentity

    let identityValue (identity: RecordIdentity) : string =
        let (RecordIdentity s) = identity
        s
