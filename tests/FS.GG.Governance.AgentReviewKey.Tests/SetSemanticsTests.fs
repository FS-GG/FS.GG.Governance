module FS.GG.Governance.AgentReviewKey.Tests.SetSemanticsTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.AgentReviewKey.Tests.Support

// US3: the reviewed-artifact hashes are keyed as a SET — reordering or duplicating them never changes the
// key, match, or diff; the empty set keys to a distinct, unambiguous value (SC-002, FR-006).

[<Tests>]
let tests =
    testList
        "SetSemantics"
        [ test "reordering reviewed artifacts leaves key, matches, and diff unchanged" {
              let reordered =
                  { baseInputs with ReviewedArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2" ] }

              Expect.equal
                  (AgentReviewKey.compute reordered)
                  (AgentReviewKey.compute baseInputs)
                  "reorder ⇒ identical key"
              Expect.isTrue (AgentReviewKey.matches baseInputs reordered) "reorder ⇒ matches"
              Expect.isEmpty (AgentReviewKey.diff baseInputs reordered) "reorder ⇒ empty diff"
          }

          test "duplicating an artifact hash keys identically to the deduped set" {
              let duped =
                  { baseInputs with
                      ReviewedArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2"; ArtifactHash "h1" ] }

              Expect.equal
                  (AgentReviewKey.compute duped)
                  (AgentReviewKey.compute baseInputs)
                  "duplication ⇒ identical key to the deduped set"
          }

          test "the empty artifact set is distinct from every one-artifact set" {
              let empty = { baseInputs with ReviewedArtifacts = [] }
              let one = { baseInputs with ReviewedArtifacts = [ ArtifactHash "h1" ] }

              Expect.notEqual
                  (AgentReviewKey.compute empty)
                  (AgentReviewKey.compute one)
                  "the empty set is never treated as a one-artifact set"
          }

          testPropertyWithConfig fscheckConfig "shuffled/duplicated artifacts preserve the key (set semantics)"
          <| fun (inputs: AgentReviewInputs) ->
              let permGen = samePermutationOf inputs.ReviewedArtifacts
              let permuted = FsCheck.FSharp.Gen.sampleWithSize 0 1 permGen |> Seq.head
              let other = { inputs with ReviewedArtifacts = permuted }
              AgentReviewKey.compute inputs = AgentReviewKey.compute other ]
