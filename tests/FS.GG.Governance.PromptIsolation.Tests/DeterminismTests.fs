module FS.GG.Governance.PromptIsolation.Tests.DeterminismTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.PromptIsolation.Tests.Support

// US3 (SC-004): `render` is a pure function of the request value — byte-identical every time — and the data
// channel is ORDER- and DUPLICATE-significant (the deliberate contrast with F035's artifact set, research
// D6).

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "render of the same request is byte-identical when repeated" {
              let request = requestOf baseInstructions [ excerptPayload 12 instructionImitatingText; digestPayload "h1" ]
              let a = PromptIsolation.render request |> PromptIsolation.renderedValue
              let b = PromptIsolation.render request |> PromptIsolation.renderedValue
              Expect.equal a b "two renders of one request are byte-identical"
          }

          test "assemble+render of identical inputs are byte-identical" {
              let payloads = [ digestPayload "h1"; excerptPayload 3 "abcdef" ]
              let r1 = PromptIsolation.render (PromptIsolation.assemble baseInstructions payloads) |> PromptIsolation.renderedValue
              let r2 = PromptIsolation.render (PromptIsolation.assemble baseInstructions payloads) |> PromptIsolation.renderedValue
              Expect.equal r1 r2 "identical inputs ⇒ identical rendering"
          }

          test "order is significant: reordering the data channel changes the rendering" {
              let a = requestOf baseInstructions [ digestPayload "h1"; digestPayload "h2" ]
              let b = requestOf baseInstructions [ digestPayload "h2"; digestPayload "h1" ]
              Expect.notEqual
                  (PromptIsolation.render a |> PromptIsolation.renderedValue)
                  (PromptIsolation.render b |> PromptIsolation.renderedValue)
                  "a reordered data channel renders differently (no sort)"
          }

          test "duplicates are preserved: the same payload twice renders twice" {
              let request = requestOf baseInstructions [ digestPayload "h1"; digestPayload "h1" ]
              let rendered = PromptIsolation.render request |> PromptIsolation.renderedValue
              Expect.stringContains rendered "art=2;" "two payloads declared (not de-duplicated)"
              Expect.stringContains rendered "dig=2:h1;dig=2:h1" "the duplicate is rendered twice in order"
          }

          testPropertyWithConfig fscheckConfig "render is a pure function of the request value" (fun (r: ReviewRequest) ->
              PromptIsolation.render r = PromptIsolation.render r) ]
