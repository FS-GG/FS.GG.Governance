module FS.GG.Governance.VerdictReuse.Tests.LookupDecisionTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model
open FS.GG.Governance.VerdictReuse.Tests.Support

// US1 (SC-001): `lookup` is `Valid` (carrying the matching entry's `VerdictRef`) IFF some cached entry
// `AgentReviewKey.matches` the request on every one of the seven inputs; any single-input change ⇒
// `Invalidated`. The dual of "invalidate when judge or prompt identity changes."

[<Tests>]
let tests =
    testList
        "LookupDecision (US1 — Valid iff all seven inputs match)"
        [ test "request equal on all seven inputs ⇒ Valid carrying that entry's reference" {
              let store = handStore [ baseInputs, refV1 ]
              Expect.equal (VerdictReuse.lookup baseInputs store) (Valid refV1) "exact-match request reuses V1"
          }

          test "request equal up to reviewed-artifact SET (reordered/deduped) ⇒ still Valid (set semantics)" {
              let store = handStore [ baseInputs, refV1 ]
              // same set {h1,h2}, reordered and without the duplicate
              let request = { baseInputs with ReviewedArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2" ] }
              Expect.equal (VerdictReuse.lookup request store) (Valid refV1) "artifact reorder/dedup still matches"
          }

          test "single-field change ⇒ Invalidated — table-driven over EVERY one of the seven inputs" {
              let store = handStore [ baseInputs, refV1 ]

              for (ri, variant) in allInputs do
                  let request = variant baseInputs

                  match VerdictReuse.lookup request store with
                  | Invalidated _ -> ()
                  | Valid _ ->
                      failtestf "changing %s alone must invalidate, got Valid" (Model.inputToken ri)
          }

          test "selection among several entries ⇒ Valid with the one that fully matches" {
              // newest-first: a judge-changed entry, then the exact match, then a prompt-changed entry.
              let store =
                  handStore
                      [ variantModelVersion baseInputs, refV2
                        baseInputs, refV1
                        variantPromptHash baseInputs, refV3 ]

              Expect.equal
                  (VerdictReuse.lookup baseInputs store)
                  (Valid refV1)
                  "the single fully-matching entry's reference is reused regardless of the others"
          }

          testPropertyWithConfig fscheckConfig "Valid iff some entry matches (and carries the head-most match's verdict)"
          <| fun (request: AgentReviewInputs) (store: VerdictStore) ->
              let firstMatch =
                  VerdictReuse.entries store
                  |> List.tryFind (fun e -> AgentReviewKey.matches request e.Inputs)

              match VerdictReuse.lookup request store, firstMatch with
              | Valid ref, Some e -> ref = e.Verdict
              | Valid _, None -> false
              | Invalidated _, None -> true
              | Invalidated _, Some _ -> false ]
