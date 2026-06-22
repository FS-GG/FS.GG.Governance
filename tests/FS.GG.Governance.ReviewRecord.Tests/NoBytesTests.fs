module FS.GG.Governance.ReviewRecord.Tests.NoBytesTests

open Expecto
open FsCheck
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord.Tests.Support

// US3 (SC-004): the record carries NO raw response bytes and NO raw, unbounded artifact bytes. The response is
// only its `ResponseDigest`; the reviewed artifacts are only their `ArtifactHash` digests; the only content is
// inside the F037-bounded `Request` (its `Excerpt` payloads are `BoundedExcerpt`, bounded by construction).
// This holds BY CONSTRUCTION of the types; the test pins the positive shape of what the record does carry.

[<Tests>]
let tests =
    testList
        "NoBytes"
        [ test "response is exposed solely as a ResponseDigest newtype (SC-004)" {
              let r = buildOf baseRequest (modelId "m") (modelVersion "v") (promptHash "p") [] (responseDigest "sha:resp") (recordedVerdict "x") []
              // The only response-bearing field is `Reproducible.ResponseDigest`; it carries no bytes, only a digest.
              let (ResponseDigest d) = r.Reproducible.ResponseDigest
              Expect.equal d "sha:resp" "the response is carried only as its supplied digest token"
          }

          test "reviewed artifacts are an ArtifactHash list — digests only, no raw bytes" {
              let arts = [ artifactHash "sha:a"; artifactHash "sha:b" ]
              let r = buildOf baseRequest (modelId "m") (modelVersion "v") (promptHash "p") arts (responseDigest "r") (recordedVerdict "x") []
              Expect.equal r.Reproducible.ReviewedArtifacts arts "reviewed artifacts are digests only"
          }

          test "the only content-bearing field is the F037-bounded request; its excerpts are bounded by construction" {
              // Supply oversized content; the excerpt must be bounded (Truncated/within bound) — there is no
              // record/facts field by which raw, unbounded content attaches outside the bounded request.
              let big = String.replicate 1000 "x"
              let request = requestOf "q" [ excerptPayload 8 big ]
              let r = buildOf request (modelId "m") (modelVersion "v") (promptHash "p") [] (responseDigest "r") (recordedVerdict "x") []

              match r.Reproducible.Request.Artifacts with
              | [ Excerpt e ] ->
                  Expect.isLessThanOrEqual (excerptContent e).Length 8 "the excerpt is bounded by construction"
                  Expect.equal (excerptTruncation e) Truncated "oversized content is deterministically truncated"
              | other -> failtestf "expected a single bounded excerpt payload, got %A" other
          } ]
