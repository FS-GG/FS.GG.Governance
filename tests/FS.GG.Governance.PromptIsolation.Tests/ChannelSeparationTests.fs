module FS.GG.Governance.PromptIsolation.Tests.ChannelSeparationTests

open Expecto
open FsCheck
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.PromptIsolation.Tests.Support

// US1 (SC-001): `assemble` keeps the trusted instruction channel and the untrusted data channel
// structurally separate — `r.Instructions` is exactly the supplied instructions and `r.Artifacts` is
// exactly the supplied payloads — and no payload, however its content reads, can enter `Instructions`.

[<Tests>]
let tests =
    testList
        "ChannelSeparation"
        [ test "assemble pairs the two channels verbatim (example)" {
              let arts =
                  [ digestPayload "sha256:abc"
                    excerptPayload 100 instructionImitatingText ]

              let r = PromptIsolation.assemble baseInstructions arts
              Expect.equal r.Instructions baseInstructions "instruction channel is exactly the supplied instructions"
              Expect.equal r.Artifacts arts "data channel is exactly the supplied payloads, in order"
          }

          testPropertyWithConfig fscheckConfig "channel separation: r.Instructions = i and r.Artifacts = arts" (fun
                                                                                                                    (i: QuestionText)
                                                                                                                    (arts: ArtifactPayload list) ->
              let r = PromptIsolation.assemble i arts
              r.Instructions = i && r.Artifacts = arts)

          test "instruction-imitating content stays in the data channel (digest + excerpt)" {
              let arts =
                  [ digestPayload instructionImitatingText
                    excerptPayload 100 instructionImitatingText ]

              let r = PromptIsolation.assemble baseInstructions arts
              // The trusted channel is unchanged by the hostile content...
              Expect.equal r.Instructions baseInstructions "instructions unchanged by instruction-imitating payloads"
              // ...and the hostile content appears ONLY in the data channel.
              Expect.equal r.Artifacts arts "instruction-imitating payloads appear only in r.Artifacts"
          }

          testPropertyWithConfig
              fscheckConfig
              "varying the artifacts never changes the instruction channel"
              (fun (i: QuestionText) (arts1: ArtifactPayload list) (arts2: ArtifactPayload list) ->
                  let r1 = PromptIsolation.assemble i arts1
                  let r2 = PromptIsolation.assemble i arts2
                  // The instruction channel is a function of `i` alone — no payload content leaks into it.
                  r1.Instructions = i && r2.Instructions = i && r1.Instructions = r2.Instructions)

          test "the two channels have distinct types (no cross constructor)" {
              // A documented compile-time guarantee: `Instructions` is a `QuestionText` and the data channel
              // is an `ArtifactPayload list`. There is no public constructor by which an `ArtifactPayload`
              // occupies `Instructions`; the value-level check above (varying arts never moves Instructions)
              // is its runtime witness. This test pins the field shapes so a future widening trips it.
              let r = PromptIsolation.assemble baseInstructions [ digestPayload "h1" ]
              let _: QuestionText = r.Instructions
              let _: ArtifactPayload list = r.Artifacts
              Expect.equal (List.length r.Artifacts) 1 "data channel carries the supplied payloads"
          } ]
