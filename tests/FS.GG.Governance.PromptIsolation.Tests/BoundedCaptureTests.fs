module FS.GG.Governance.PromptIsolation.Tests.BoundedCaptureTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.PromptIsolation.Tests.Support

// US2 (SC-002): `excerpt` captures supplied content into exactly one bounded form — at/under the bound whole
// + `Whole`, over the bound truncated to the bound + `Truncated` — with no excerpt ever exceeding its bound
// and no form carrying raw, unbounded content (`BoundedExcerpt` abstract; `DigestOnly` carries no bytes).

let private boundInt (SizeBound n) = n

[<Tests>]
let tests =
    testList
        "BoundedCapture"
        [ test "content at/under the bound is carried whole and marked Whole" {
              let e = excerpt (SizeBound 10) "abcdef"
              Expect.equal (excerptContent e) "abcdef" "content carried whole"
              Expect.equal (excerptTruncation e) Whole "marked Whole"
          }

          test "content over the bound is truncated to the bound prefix and marked Truncated" {
              let e = excerpt (SizeBound 3) "abcdef"
              Expect.equal (excerptContent e) "abc" "content is the bound-char prefix"
              Expect.equal (excerptTruncation e) Truncated "marked Truncated"
          }

          test "boundary exactness at bound-1, bound, bound+1" {
              let content = "abcde" // length 5
              Expect.equal (excerptTruncation (excerpt (SizeBound 6) content)) Whole "bound-1 (under) ⇒ Whole"
              Expect.equal (excerptTruncation (excerpt (SizeBound 5) content)) Whole "exactly bound ⇒ Whole"
              Expect.equal (excerptContent (excerpt (SizeBound 5) content)) content "exactly bound ⇒ whole content"

              let e4 = excerpt (SizeBound 4) content
              Expect.equal (excerptTruncation e4) Truncated "bound+1 (over) ⇒ Truncated"
              Expect.equal (excerptContent e4) "abcd" "bound+1 ⇒ prefix of length bound"
          }

          testPropertyWithConfig fscheckConfig "never over-bound: captured length <= max 0 bound" (fun
                                                                                                        (b: SizeBound)
                                                                                                        (content: string) ->
              let e = excerpt b content
              (excerptContent e).Length <= max 0 (boundInt b))

          testPropertyWithConfig
              fscheckConfig
              "truncated ⇔ content longer than max 0 bound; whole content is the prefix"
              (fun (b: SizeBound) (content: string) ->
                  let n = max 0 (boundInt b)
                  let e = excerpt b content

                  match excerptTruncation e with
                  | Whole -> content.Length <= n && excerptContent e = content
                  | Truncated -> content.Length > n && excerptContent e = content.Substring(0, n))

          test "negative bound clamps to 0 ⇒ non-empty content gives an empty Truncated excerpt" {
              let e = excerpt (SizeBound -5) "anything"
              Expect.equal (excerptContent e) "" "negative bound clamps to 0 ⇒ empty content"
              Expect.equal (excerptTruncation e) Truncated "non-empty content over a 0 bound ⇒ Truncated"
              Expect.equal (excerptBound e) (SizeBound 0) "bound is clamped to 0 in the value"
          }

          test "the bound accessor round-trips (clamped)" {
              Expect.equal (excerptBound (excerpt (SizeBound 12) "x")) (SizeBound 12) "non-negative bound preserved"
              Expect.equal (excerptBound (excerpt (SizeBound -1) "x")) (SizeBound 0) "negative bound clamped to 0"
          }

          test "a digest-only payload carries no content excerpt (no bytes)" {
              let payload = digestPayload "sha256:abc"

              match payload with
              | DigestOnly(ArtifactHash h) -> Expect.equal h "sha256:abc" "digest exposes only its supplied token"
              | Excerpt _ -> failtest "a digest-only payload must not carry a BoundedExcerpt"
          } ]
