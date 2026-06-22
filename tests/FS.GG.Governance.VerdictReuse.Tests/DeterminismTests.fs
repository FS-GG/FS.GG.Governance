module FS.GG.Governance.VerdictReuse.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model
open FS.GG.Governance.VerdictReuse.Tests.Support

// Cross-cutting (SC-004): `lookup` is byte-stable/deterministic; reviewed-artifact order/duplication never
// changes a decision (set semantics inherited from F035 `matches`/`diff`); multiple full-match entries
// resolve head-most; an empty-artifact-set transition is a real `ReviewedArtifactsInput` diff.

// A set-preserving permutation: reverse, then append a duplicate of the head (no-op for the empty list).
let private permuteSet (arts: ArtifactHash list) =
    match arts with
    | [] -> []
    | head :: _ -> (List.rev arts) @ [ head ]

[<Tests>]
let tests =
    testList
        "Determinism & set semantics"
        [ test "lookup asked twice yields identical results" {
              let store = handStore [ variantPromptHash baseInputs, refV2; baseInputs, refV1 ]
              Expect.equal (VerdictReuse.lookup baseInputs store) (VerdictReuse.lookup baseInputs store) "repeat lookup is identical"
          }

          test "reordering/duplicating ReviewedArtifacts in the REQUEST never changes the decision" {
              let store = handStore [ baseInputs, refV1 ]
              let request = { baseInputs with ReviewedArtifacts = permuteSet baseInputs.ReviewedArtifacts }
              Expect.equal (VerdictReuse.lookup request store) (Valid refV1) "request artifact reorder/dup still matches"
          }

          test "reordering/duplicating ReviewedArtifacts in the STORED entry never changes the decision" {
              let entry = { baseInputs with ReviewedArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2" ] }
              let store = handStore [ entry, refV1 ]
              Expect.equal (VerdictReuse.lookup baseInputs store) (Valid refV1) "stored artifact reorder/dup still matches"
          }

          test "multiple full-match entries ⇒ the head-most (most-recent) reference, deterministically" {
              let store = handStore [ baseInputs, refV2; baseInputs, refV1 ]
              Expect.equal (VerdictReuse.lookup baseInputs store) (Valid refV2) "head-most full match wins"
          }

          test "empty-artifact-set transition (entry [] vs request [h]) ⇒ InputsChanged [ReviewedArtifactsInput]" {
              let emptyEntry = { baseInputs with ReviewedArtifacts = [] }
              let store = handStore [ emptyEntry, refV1 ]
              Expect.equal
                  (VerdictReuse.lookup baseInputs store)
                  (Invalidated(InputsChanged [ ReviewedArtifactsInput ]))
                  "to-empty/from-empty is a real artifact diff"
          }

          test "empty-artifact-set transition (entry [h] vs request []) ⇒ InputsChanged [ReviewedArtifactsInput]" {
              let store = handStore [ baseInputs, refV1 ]
              let request = { baseInputs with ReviewedArtifacts = [] }
              Expect.equal
                  (VerdictReuse.lookup request store)
                  (Invalidated(InputsChanged [ ReviewedArtifactsInput ]))
                  "the reverse transition is also a real diff"
          }

          testPropertyWithConfig fscheckConfig "lookup is deterministic — repeated evaluation is identical"
          <| fun (request: AgentReviewInputs) (store: VerdictStore) ->
              VerdictReuse.lookup request store = VerdictReuse.lookup request store

          testPropertyWithConfig fscheckConfig "a set-preserving permutation of the request's artifacts never changes the decision"
          <| fun (request: AgentReviewInputs) (store: VerdictStore) ->
              let permuted = { request with ReviewedArtifacts = permuteSet request.ReviewedArtifacts }
              VerdictReuse.lookup permuted store = VerdictReuse.lookup request store ]
