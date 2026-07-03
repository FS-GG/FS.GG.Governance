module FS.GG.Governance.PromptIsolation.Tests.EdgeCaseTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.PromptIsolation.Tests.Support

// Cross-cutting (Edge Cases, FR-004/FR-006): the spec's enumerated degenerate inputs are ordinary complete
// values — empty excerpt, zero artifacts, zero bound, boundary content, repeated artifacts, empty/unusual
// digest — and the operations are total over every supplied value.

[<Tests>]
let tests =
    testList
        "EdgeCase"
        [ test "empty excerpt is Whole, renders exc=w,0:, distinct from a digest and from an absent artifact" {
              let e = excerpt (SizeBound 100) ""
              Expect.equal (excerptContent e) "" "empty excerpt content"
              Expect.equal (excerptTruncation e) Whole "empty content under the bound ⇒ Whole"

              let emptyExcerpt = PromptIsolation.render (requestOf baseInstructions [ Excerpt e ]) |> PromptIsolation.renderedValue
              let digest = PromptIsolation.render (requestOf baseInstructions [ digestPayload "" ]) |> PromptIsolation.renderedValue
              let absent = PromptIsolation.render (requestOf baseInstructions []) |> PromptIsolation.renderedValue

              Expect.stringContains emptyExcerpt "exc=w,0:" "empty excerpt renders exc=w,0:"
              Expect.notEqual emptyExcerpt digest "empty excerpt ≠ digest-only"
              Expect.notEqual emptyExcerpt absent "empty excerpt ≠ absent artifact"
          }

          test "zero governed artifacts renders instr=…\\nart=0; and is never malformed" {
              let rendered = PromptIsolation.render (requestOf baseInstructions []) |> PromptIsolation.renderedValue
              Expect.equal rendered "instr=37:Does this doc explain the public API?\nart=0;" "empty data channel is art=0;"
          }

          test "zero bound: non-empty content ⇒ empty Truncated; empty content ⇒ empty Whole" {
              let over = excerpt (SizeBound 0) "abc"
              Expect.equal (excerptContent over) "" "zero bound drops all content"
              Expect.equal (excerptTruncation over) Truncated "zero bound + non-empty ⇒ Truncated"

              let exact = excerpt (SizeBound 0) ""
              Expect.equal (excerptContent exact) "" "zero bound + empty content ⇒ empty"
              Expect.equal (excerptTruncation exact) Whole "zero bound + empty content ⇒ Whole"
          }

          test "digest-only with empty / unusual supplied token is carried verbatim, never parsed" {
              let emptyDig = PromptIsolation.render (requestOf baseInstructions [ digestPayload "" ]) |> PromptIsolation.renderedValue
              Expect.stringContains emptyDig "dig=0:" "empty digest renders dig=0:"

              let unusual = "not a hash :: =,;"
              let unusualDig = PromptIsolation.render (requestOf baseInstructions [ digestPayload unusual ]) |> PromptIsolation.renderedValue
              Expect.stringContains unusualDig (sprintf "dig=%d:%s" (System.Text.Encoding.UTF8.GetByteCount unusual) unusual) "unusual token carried verbatim by length"
          }

          test "repeated/identical-content artifacts are each carried and rendered in order" {
              let request = requestOf baseInstructions [ excerptPayload 3 "abc"; excerptPayload 3 "abc" ]
              let rendered = PromptIsolation.render request |> PromptIsolation.renderedValue
              Expect.stringContains rendered "art=2;exc=w,3:abc;exc=w,3:abc" "two identical excerpts rendered in order"
          }

          testPropertyWithConfig fscheckConfig "totality: excerpt/assemble/render never throw" (fun
                                                                                                     (i: QuestionText)
                                                                                                     (b: SizeBound)
                                                                                                     (content: string)
                                                                                                     (arts: ArtifactPayload list) ->
              let _ = excerpt b content
              let request = PromptIsolation.assemble i arts
              let _ = PromptIsolation.render request |> PromptIsolation.renderedValue
              true) ]
