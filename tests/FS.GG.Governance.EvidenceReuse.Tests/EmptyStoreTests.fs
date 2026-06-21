module FS.GG.Governance.EvidenceReuse.Tests.EmptyStoreTests

open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuse.Tests.Support

// US1 #4 / SC-004 — an empty store always recomputes with NoPriorEvidence, and the operations are total on
// degenerate inputs (empty store, empty reference) — no exception (FR-012).

[<Tests>]
let tests =
    testList
        "EmptyStore"
        [ test "empty store ⇒ Recompute NoPriorEvidence (representative, SC-004)" {
              Expect.equal
                  (EvidenceReuse.decide baseInputs EvidenceReuse.empty)
                  (Recompute NoPriorEvidence)
                  "no prior evidence at all ⇒ NoPriorEvidence"
          }

          testPropertyWithConfig fscheckConfig "empty store ⇒ Recompute NoPriorEvidence for ANY candidate (SC-004)"
          <| fun (c: FS.GG.Governance.FreshnessKey.Model.FreshnessInputs) ->
              EvidenceReuse.decide c EvidenceReuse.empty = Recompute NoPriorEvidence

          test "empty store has no entries" {
              Expect.isEmpty (EvidenceReuse.entries EvidenceReuse.empty) "empty store has no entries"
          }

          test "an empty EvidenceRef is carried verbatim on reuse and never rejected (FR-012)" {
              let store = storeOf [ baseInputs, emptyRef ]

              match EvidenceReuse.decide baseInputs store with
              | Reuse r -> Expect.equal (EvidenceReuse.referenceValue r) "" "the empty reference is carried back verbatim"
              | other -> failtestf "expected Reuse of the empty ref, got %A" other
          }

          test "decide/entries/referenceValue throw no exception on degenerate inputs (FR-012)" {
              // Exercise the total contract: empty store, empty ref, and a store of one empty-ref entry.
              EvidenceReuse.decide baseInputs EvidenceReuse.empty |> ignore
              EvidenceReuse.entries EvidenceReuse.empty |> ignore
              EvidenceReuse.referenceValue emptyRef |> ignore
              EvidenceReuse.decide baseInputs (storeOf [ baseInputs, emptyRef ]) |> ignore
          } ]
