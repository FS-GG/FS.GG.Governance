module FS.GG.Governance.VerdictReuse.Tests.EmptyStoreTests

open Expecto
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model
open FS.GG.Governance.VerdictReuse.Tests.Support

// Edge / totality (SC-001, SC-003, FR-012): the empty store always invalidates as NoCachedVerdict; an
// empty/unusual VerdictRef is carried verbatim and never parsed or rejected; no value throws.

[<Tests>]
let tests =
    testList
        "EmptyStore & totality"
        [ test "lookup against the empty store ⇒ Invalidated NoCachedVerdict" {
              Expect.equal (VerdictReuse.lookup baseInputs VerdictReuse.empty) (Invalidated NoCachedVerdict) "empty ⇒ NoCachedVerdict"
          }

          test "empty store holds no entries" {
              Expect.isEmpty (VerdictReuse.entries VerdictReuse.empty) "empty store has no entries"
          }

          test "an empty-string VerdictRef recorded then looked up ⇒ Valid carrying it verbatim (never parsed/rejected)" {
              let store = VerdictReuse.record baseInputs refEmpty VerdictReuse.empty

              match VerdictReuse.lookup baseInputs store with
              | Valid ref ->
                  Expect.equal ref refEmpty "the empty-string reference is carried back unchanged"
                  Expect.equal (VerdictReuse.referenceValue ref) "" "referenceValue round-trips the empty string"
              | other -> failtestf "expected Valid (VerdictRef \"\"), got %A" other
          }

          testPropertyWithConfig fscheckConfig "lookup r empty = Invalidated NoCachedVerdict for every request"
          <| fun (request: AgentReviewInputs) ->
              VerdictReuse.lookup request VerdictReuse.empty = Invalidated NoCachedVerdict

          testPropertyWithConfig fscheckConfig "no AgentReviewInputs/VerdictRef/VerdictStore value makes lookup or record throw (totality)"
          <| fun (request: AgentReviewInputs) (verdict: VerdictRef) (store: VerdictStore) ->
              VerdictReuse.lookup request store |> ignore
              VerdictReuse.record request verdict store |> ignore
              VerdictReuse.referenceValue verdict |> ignore
              true ]
