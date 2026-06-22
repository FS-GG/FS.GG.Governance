module FS.GG.Governance.EvidenceReuseStore.Tests.RetentionTests

open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.EvidenceReuseStore.Tests.Support

// US2 (SC-004, FR-006, research D8): `retain` bounds the store to the newest `maxEntries`, removing only whole
// entries, idempotent at/under the bound, total over every `n` and store.

[<Tests>]
let tests =
    testList
        "Retention"
        [ test "bounded + newest-retained: retain n keeps exactly the first n of the newest-first store" {
              let store =
                  storeOf
                      [ inputs "a", syntheticRef "a"
                        inputs "b", syntheticRef "b"
                        inputs "c", syntheticRef "c"
                        inputs "d", syntheticRef "d" ] // newest-first: d, c, b, a

              let bounded = EvidenceReuseStore.retain 2 store
              Expect.equal (EvidenceReuse.entries bounded) (EvidenceReuse.entries store |> List.truncate 2) "first 2 of newest-first, in order"
              Expect.equal (EvidenceReuse.entries bounded |> List.length) 2 "length within bound"
          }

          testPropertyWithConfig fscheckConfig "retain n is the prefix of length (min n len), in order" (fun (store: ReuseStore) (n: int) ->
              let all = EvidenceReuse.entries store
              let expected = all |> List.truncate (max 0 n)
              EvidenceReuse.entries (EvidenceReuseStore.retain n store) = expected)

          testPropertyWithConfig fscheckConfig "idempotent at/under bound: length ≤ n ⇒ retain n store = store" (fun (store: ReuseStore) ->
              let len = EvidenceReuse.entries store |> List.length
              // any n ≥ len returns the store UNCHANGED (no reorder/rewrite)
              EvidenceReuseStore.retain len store = store && EvidenceReuseStore.retain (len + 5) store = store)

          testPropertyWithConfig fscheckConfig "removal-only: every survivor is byte-for-byte an input entry (a prefix subset)" (fun (store: ReuseStore) (n: int) ->
              let all = EvidenceReuse.entries store
              let survivors = EvidenceReuse.entries (EvidenceReuseStore.retain n store)
              // survivors are a prefix of the input ⇒ each is one of the inputs, nothing mutated/fabricated
              survivors = (all |> List.truncate (max 0 n)) && survivors |> List.forall (fun e -> List.contains e all))

          test "totality / boundary: retain 0 and retain negative ⇒ empty store" {
              let store = storeOf [ inputs "a", syntheticRef "a"; inputs "b", syntheticRef "b" ]
              Expect.equal (EvidenceReuseStore.retain 0 store) EvidenceReuse.empty "retain 0 ⇒ empty"
              Expect.equal (EvidenceReuseStore.retain -5 store) EvidenceReuse.empty "retain negative ⇒ empty"
          }

          test "retain n of the empty store ⇒ empty" {
              Expect.equal (EvidenceReuseStore.retain 10 EvidenceReuse.empty) EvidenceReuse.empty "empty stays empty"
          }

          test "defaultRetentionBound is a positive constant usable as the bound; pins current value 256 (D8)" {
              Expect.isGreaterThan EvidenceReuseStore.defaultRetentionBound 0 "default bound is positive"
              // Current documented mechanism value (research D8); the exact number is not a contract.
              Expect.equal EvidenceReuseStore.defaultRetentionBound 256 "current documented default bound"

              let store = storeOf [ inputs "a", syntheticRef "a"; inputs "b", syntheticRef "b" ]
              let bounded = EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound store
              Expect.isLessThanOrEqual (EvidenceReuse.entries bounded |> List.length) EvidenceReuseStore.defaultRetentionBound "obeys the bound"
              // store is well under the bound ⇒ unchanged
              Expect.equal bounded store "store under the default bound is unchanged"
          } ]
