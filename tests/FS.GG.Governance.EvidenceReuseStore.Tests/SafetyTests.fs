module FS.GG.Governance.EvidenceReuseStore.Tests.SafetyTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.EvidenceReuseStore.Tests.Support

// Cross-cutting recompute-safety invariant (SC-006, FR-008, data-model §"recompute-safety invariant"): for
// each operation op ∈ { serialise∘readBack, retain n, prune }, it is NEVER the case that `decide c s` is a
// `Recompute` while `decide c (op s)` is a `Reuse`. No operation turns a `mustRecompute` candidate into a
// `reusable` one. This is the row's load-bearing safety proof.

/// True when `op` did NOT manufacture a reuse for candidate `c` against store `s`.
let private noSpuriousReuse (decideBefore: ReuseDecision) (decideAfter: ReuseDecision) : bool =
    match decideBefore, decideAfter with
    | Recompute _, Reuse _ -> false
    | _ -> true

[<Tests>]
let tests =
    testList
        "Safety"
        [ testPropertyWithConfig fscheckConfig "serialise∘readBack never turns Recompute into Reuse (in fact verdict-identical)" (fun (candidate: FreshnessInputs) (store: ReuseStore) ->
              match readBack (EvidenceReuseStore.serialise store) with
              | Some loaded ->
                  let before = EvidenceReuse.decide candidate store
                  let after = EvidenceReuse.decide candidate loaded
                  // round-trip is an equal store ⇒ verdicts identical (the strongest form of safe)
                  before = after && noSpuriousReuse before after
              | None ->
                  // only the empty store maps to a None-via-absent; serialise always yields a present recorded
                  // array, so readBack is always Some — a None here is a genuine failure.
                  false)

          testPropertyWithConfig fscheckConfig "retain n never turns Recompute into Reuse" (fun (candidate: FreshnessInputs) (store: ReuseStore) (n: int) ->
              let before = EvidenceReuse.decide candidate store
              let after = EvidenceReuse.decide candidate (EvidenceReuseStore.retain n store)
              noSpuriousReuse before after)

          testPropertyWithConfig fscheckConfig "prune never turns Recompute into Reuse (in fact verdict-identical)" (fun (candidate: FreshnessInputs) (store: ReuseStore) ->
              let before = EvidenceReuse.decide candidate store
              let after = EvidenceReuse.decide candidate (EvidenceReuseStore.prune store)
              before = after && noSpuriousReuse before after) ]
