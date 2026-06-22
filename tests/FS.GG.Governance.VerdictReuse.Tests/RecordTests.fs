module FS.GG.Governance.VerdictReuse.Tests.RecordTests

open Expecto
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model
open FS.GG.Governance.VerdictReuse.Tests.Support

// US3 (SC-005): `record` returns a NEW store (no mutation) in which a just-recorded verdict is immediately
// reusable, refreshes a matching entry most-recent-wins (no duplicates), and leaves non-matching prior
// entries independently reusable.

let private matchCount inputs store =
    VerdictReuse.entries store
    |> List.filter (fun e -> AgentReviewKey.matches inputs e.Inputs)
    |> List.length

[<Tests>]
let tests =
    testList
        "Record (US3 — pure, deterministic, de-duplicating)"
        [ test "reflexive validity: lookup i (record i v empty) = Valid v" {
              let store = VerdictReuse.record baseInputs refV1 VerdictReuse.empty
              Expect.equal (VerdictReuse.lookup baseInputs store) (Valid refV1) "just-recorded verdict is reusable"
          }

          test "refresh / de-dup most-recent-wins: record i v2 (record i v1 s) ⇒ Valid v2 and no duplicate for i" {
              let store = VerdictReuse.record baseInputs refV2 (VerdictReuse.record baseInputs refV1 VerdictReuse.empty)
              Expect.equal (VerdictReuse.lookup baseInputs store) (Valid refV2) "the refreshed reference wins"
              Expect.equal (matchCount baseInputs store) 1 "no duplicate entry for the same matching-input class"
          }

          test "independence: recording a non-matching entry leaves every prior entry reusable" {
              let start = VerdictReuse.record baseInputs refV1 VerdictReuse.empty
              let other = variantModelVersion baseInputs // same work, different judge identity ⇒ matches nothing prior
              let store = VerdictReuse.record other refV2 start
              Expect.equal (VerdictReuse.lookup baseInputs store) (Valid refV1) "the original entry is still reusable"
              Expect.equal (VerdictReuse.lookup other store) (Valid refV2) "the new entry is reusable"
          }

          test "no mutation: recording into a captured store value leaves that value unchanged" {
              let original = handStore [ baseInputs, refV1 ]
              let snapshot = VerdictReuse.entries original
              VerdictReuse.record (variantModelVersion baseInputs) refV2 original |> ignore
              Expect.equal (VerdictReuse.entries original) snapshot "the input store value is structurally unchanged"
          }

          test "replay determinism: same start store + same recording sequence ⇒ identical lookup decisions" {
              let seq = [ baseInputs, refV1; variantPromptHash baseInputs, refV2; baseInputs, refV3 ]
              let s1 = storeOf seq
              let s2 = storeOf seq
              let requests = [ baseInputs; variantPromptHash baseInputs; variantCheck baseInputs ]

              for r in requests do
                  Expect.equal (VerdictReuse.lookup r s1) (VerdictReuse.lookup r s2) "replayed store decides identically"
              // and the refresh held: baseInputs resolves to the last-recorded V3
              Expect.equal (VerdictReuse.lookup baseInputs s1) (Valid refV3) "most-recent record of baseInputs wins"
          }

          testPropertyWithConfig fscheckConfig "record i v2 (record i v1 s) ⇒ lookup i = Valid v2 with exactly one matching entry"
          <| fun (inputs: AgentReviewInputs) (v1: VerdictRef) (v2: VerdictRef) (store: VerdictStore) ->
              let store' = VerdictReuse.record inputs v2 (VerdictReuse.record inputs v1 store)
              VerdictReuse.lookup inputs store' = Valid v2 && matchCount inputs store' = 1

          testPropertyWithConfig fscheckConfig "recording preserves every prior Valid reuse for a request that does not match the recorded inputs"
          <| fun (inputs: AgentReviewInputs) (v: VerdictRef) (request: AgentReviewInputs) (store: VerdictStore) ->
              // Independence (FR-005): for a request that does NOT match the just-recorded inputs, recording
              // cannot remove or alter a Valid reuse, nor manufacture a new one. (The exact InputsChanged
              // cause may differ — a same-check record becomes the new most-recent prior — so this asserts
              // Valid-preservation, not cause-equality.)
              if AgentReviewKey.matches request inputs then
                  true // the matching case is the refresh property above
              else
                  match VerdictReuse.lookup request store, VerdictReuse.lookup request (VerdictReuse.record inputs v store) with
                  | Valid a, Valid b -> a = b
                  | Invalidated _, Invalidated _ -> true
                  | _ -> false ]
