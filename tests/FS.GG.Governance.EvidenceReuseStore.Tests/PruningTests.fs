module FS.GG.Governance.EvidenceReuseStore.Tests.PruningTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.EvidenceReuseStore.Tests.Support

// US3 (SC-005, FR-007/FR-010, research D9): `prune` removes every entry a strictly-newer entry already
// `FreshnessKey.matches`, keeping the newest entry per world-class in newest-first order; reuses F029 `matches`
// VERBATIM; verdict-preserving hence recompute-safe.

[<Tests>]
let tests =
    testList
        "Pruning"
        [ test "superseded removed, subset, newest-first order" {
              // supersededStore: [ world@newest ; otherWorld@distinct ; world@superseded ] — the 3rd entry's
              // world is full-matched by the 1st (strictly newer) ⇒ dropped.
              let pruned = EvidenceReuseStore.prune supersededStore
              let survivors = EvidenceReuse.entries pruned
              let original = EvidenceReuse.entries supersededStore

              Expect.equal survivors [ original.[0]; original.[1] ] "the superseded older entry is removed; survivors newest-first subset"
              Expect.isTrue (survivors |> List.forall (fun e -> List.contains e original)) "survivors are a subset of the input"
          }

          testPropertyWithConfig fscheckConfig "survivors are a newest-first subset of the input" (fun (store: ReuseStore) ->
              let original = EvidenceReuse.entries store
              let survivors = EvidenceReuse.entries (EvidenceReuseStore.prune store)
              // subsequence-preserving subset: survivors appear in the input in the same order
              let rec isSubseq sub super =
                  match sub, super with
                  | [], _ -> true
                  | _, [] -> false
                  | s :: srest, h :: hrest -> if s = h then isSubseq srest hrest else isSubseq sub hrest

              isSubseq survivors original)

          test "no-op on a record-built (already full-match-deduped) store" {
              let store = storeOf [ inputs "fmt", syntheticRef "fmt"; inputs "lint", syntheticRef "lint" ]
              Expect.equal (EvidenceReuseStore.prune store) store "record-built store has no dead entry ⇒ unchanged"
          }

          test "no-op on an all-distinct-worlds store" {
              let store =
                  ReuseStore
                      [ { Inputs = inputs "a"; Evidence = syntheticRef "a" }
                        { Inputs = { inputs "a" with Domain = DomainId "d2" }; Evidence = syntheticRef "b" }
                        { Inputs = { inputs "a" with Environment = Ci }; Evidence = syntheticRef "c" } ]

              Expect.equal (EvidenceReuseStore.prune store) store "all worlds distinct ⇒ unchanged"
          }

          testPropertyWithConfig fscheckConfig "verdict-preserving: decide c (prune store) = decide c store for every candidate" (fun (candidate: FreshnessInputs) (store: ReuseStore) ->
              EvidenceReuse.decide candidate (EvidenceReuseStore.prune store) = EvidenceReuse.decide candidate store)

          test "dead-entry criterion is exactly F029 matches — duplicate worlds collapse to the newest" {
              let world = inputs "build:tests"
              // three entries of the SAME world (matches), oldest-first input ⇒ newest-first store
              let store =
                  ReuseStore
                      [ { Inputs = world; Evidence = syntheticRef "v3" }
                        { Inputs = world; Evidence = syntheticRef "v2" }
                        { Inputs = world; Evidence = syntheticRef "v1" } ]

              let pruned = EvidenceReuseStore.prune store

              match EvidenceReuse.entries pruned with
              | [ e ] ->
                  Expect.isTrue (FreshnessKey.matches e.Inputs world) "kept entry is the world"
                  Expect.equal e.Evidence (syntheticRef "v3") "the NEWEST of the duplicate world survives"
              | other -> failtestf "expected a single survivor, got %A" other
          }

          test "totality: empty / singleton / all-superseded never throw" {
              Expect.equal (EvidenceReuseStore.prune EvidenceReuse.empty) EvidenceReuse.empty "empty ⇒ empty"

              let singleton = ReuseStore [ { Inputs = inputs "a"; Evidence = syntheticRef "a" } ]
              Expect.equal (EvidenceReuseStore.prune singleton) singleton "singleton unchanged"

              let world = inputs "x"

              let allSuperseded =
                  ReuseStore
                      [ { Inputs = world; Evidence = syntheticRef "n" }
                        { Inputs = world; Evidence = syntheticRef "o1" }
                        { Inputs = world; Evidence = syntheticRef "o2" } ]

              Expect.equal (EvidenceReuse.entries (EvidenceReuseStore.prune allSuperseded) |> List.length) 1 "all-superseded collapses to one"
          } ]
