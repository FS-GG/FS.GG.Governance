module FS.GG.Governance.EvidenceReuse.Tests.ReuseDecisionTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuse.Tests.Support

// US1 — reuse iff all freshness inputs match (SC-001). `decide` is *Reuse* exactly when some recorded entry
// matches on every category, else *Recompute*. The single rule the whole cost/cache phase turns on.

[<Tests>]
let tests =
    testList
        "ReuseDecision"
        [ test "full match ⇒ Reuse the entry's ref (US1 #1)" {
              let store = storeOf [ baseInputs, E1 ]

              match EvidenceReuse.decide baseInputs store with
              | Reuse r -> Expect.equal (EvidenceReuse.referenceValue r) "ev-1" "the matching entry's ref is reused"
              | other -> failtestf "expected Reuse, got %A" other
          }

          test "single-field change in ANY category ⇒ Recompute, no reuse (US1 #2, SC-001)" {
              // For each of the 10 categories, a one-entry store whose entry differs from the candidate in
              // exactly that category must NOT reuse.
              for (category, vary) in allCategories do
                  let candidate = vary baseInputs
                  let store = storeOf [ baseInputs, E1 ]

                  match EvidenceReuse.decide candidate store with
                  | Recompute _ -> ()
                  | Reuse _ -> failtestf "category %A changed but evidence was reused" category
          }

          test "several entries, exactly one fully matches ⇒ Reuse that one's ref, any order (US1 #3)" {
              // Two non-matching entries surround the one full match; the match must be found regardless of
              // position.
              let other1 = { baseInputs with RuleHash = RuleHash "r2" }
              let other2 = { baseInputs with Head = Revision "ddd" }

              let storeMiddle = storeOf [ other1, E2; baseInputs, E1; other2, E3 ]
              let storeLast = storeOf [ other1, E2; other2, E3; baseInputs, E1 ]

              for store in [ storeMiddle; storeLast ] do
                  match EvidenceReuse.decide baseInputs store with
                  | Reuse r -> Expect.equal (EvidenceReuse.referenceValue r) "ev-1" "the single full match is reused"
                  | other -> failtestf "expected Reuse ev-1, got %A" other
          }

          test "multiple full-match entries ⇒ Reuse the head (most-recent) ref (Edge: multiple matches, FR-005)" {
              // A hand-built store with two FULL matches; newest-first ⇒ head wins, deterministically.
              let store = storeOf [ baseInputs, E2; baseInputs, E1 ]

              match EvidenceReuse.decide baseInputs store with
              | Reuse r -> Expect.equal (EvidenceReuse.referenceValue r) "ev-2" "the head (most-recent) entry's ref wins"
              | other -> failtestf "expected Reuse ev-2, got %A" other
          } ]
