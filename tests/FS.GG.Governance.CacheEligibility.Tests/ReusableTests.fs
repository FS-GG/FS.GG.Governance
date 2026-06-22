module FS.GG.Governance.CacheEligibility.Tests.ReusableTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility.Tests.Support

// User Story 2 — reusable when prior evidence matches, naming the evidence (SC-002, L-G5). When a candidate's
// resolved freshness inputs exactly match a recorded entry, `evaluateGate` returns `Reusable` carrying that
// entry's F030 `EvidenceRef` (not a bare boolean), with the same most-recent-wins choice F030 makes — no new
// reuse, recency, or matching policy introduced here. Validates the `Reusable` branch of the total
// `evaluateGate`.

[<Tests>]
let tests =
    testList
        "Reusable"
        [ test "exact match ⇒ Reusable carrying the recorded evidence reference, not a bare flag (US2 #1, SC-002, L-G5)" {
              let store = storeOf [ baseInputs, refA ]
              let c = candidate (gid "build" "tests") baseInputs

              Expect.equal
                  (CacheEligibility.evaluateGate c store)
                  (Reusable(EvidenceRef "ev-A"))
                  "an exact-match candidate reuses, carrying the exact reference"

              Expect.equal
                  (CacheEligibility.reusableEvidence (CacheEligibility.evaluateGate c store))
                  (Some(EvidenceRef "ev-A"))
                  "the verdict carries the exact evidence reference"
          }

          test "order/dup-invariant inputs still match ⇒ Reusable (SC-002, L-G5)" {
              // CoveredArtifacts compared as a set — a permuted/duplicated artifact list is still an exact
              // match (F029 invariance, inherited verbatim).
              let store = storeOf [ baseInputs, refA ]
              let shuffled = { baseInputs with CoveredArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2" ] }
              let c = candidate (gid "build" "tests") shuffled

              Expect.equal
                  (CacheEligibility.evaluateGate c store)
                  (Reusable(EvidenceRef "ev-A"))
                  "set-equal covered artifacts still reuse"
          }

          test "multiple matching entries ⇒ Reusable carrying F030's most-recent-wins reference (US2 #2, L-G5)" {
              // Two entries for the SAME inputs recorded under different references; `record` keeps the
              // most-recent (refB recorded last), so the verdict carries exactly what F030 chooses — no new
              // recency policy here.
              let store = storeOf [ baseInputs, refA; baseInputs, refB ]
              let c = candidate (gid "build" "tests") baseInputs

              // Assert equality against the F030 oracle projected through the relabel.
              let expected =
                  match EvidenceReuse.decide baseInputs store with
                  | Reuse r -> Reusable r
                  | Recompute cause -> MustRecompute cause

              Expect.equal (CacheEligibility.evaluateGate c store) expected "the verdict carries exactly F030's most-recent-wins reference"
              Expect.equal (CacheEligibility.evaluateGate c store) (Reusable refB) "most-recent recorded reference wins"
          }

          testPropertyWithConfig fscheckConfig "isReusable ⟺ F030 Reuse, and the carried reference is exactly F030's (US2, FR-004, L-G1)"
          <| fun (c: CandidateGate) (s: ReuseStore) ->
              match EvidenceReuse.decide c.Inputs s with
              | Reuse ref ->
                  CacheEligibility.isReusable (CacheEligibility.evaluateGate c s)
                  && CacheEligibility.reusableEvidence (CacheEligibility.evaluateGate c s) = Some ref
              | Recompute _ ->
                  not (CacheEligibility.isReusable (CacheEligibility.evaluateGate c s))
                  && CacheEligibility.reusableEvidence (CacheEligibility.evaluateGate c s) = None ]
