module FS.GG.Governance.CacheEligibilityJson.Tests.NoHideTests

open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.CacheEligibilityJson.Tests.Support

// US3 (SC-005, L-R4/L-R5): every `mustRecompute` entry carries its named cause — `noPriorEvidence`, or the
// exact changed freshness-input categories in the report's order — so a recompute is always self-explaining,
// and `noPriorEvidence` is never confused with `inputsChanged []`.

[<Tests>]
let tests =
    testList
        "NoHide"
        [ test "inputsChanged names exactly the categories, in order, never truncated (L-R4)" {
              // Change SEVERAL non-identity categories at once against a recorded exact-match base; the cause
              // must name all of them in the report's order — never truncated to the first difference.
              let changed = nonIdentityCategories |> List.take 4
              let moved = changed |> List.fold (fun acc (_, mut) -> mut acc) baseInputs
              let r = report [ candidate (gid "build" "tests") moved ] (storeOf [ baseInputs, refA ])
              use doc = parse (CacheEligibilityJson.ofReport r)
              let v = entryVerdict (List.exactlyOne (entriesOf doc))
              Expect.equal (verdictKind v) "mustRecompute" "kind mustRecompute"

              let expected =
                  match (CacheEligibility.entries r |> List.exactlyOne).Verdict with
                  | MustRecompute(InputsChanged cs) -> cs |> List.map categoryToken
                  | other -> failtestf "expected InputsChanged, got %A" other; []

              Expect.equal (causeCategories (verdictCause v)) expected "categories named exactly, in report order, none truncated"
              Expect.isGreaterThan expected.Length 1 "the fixture genuinely changed more than one category"
          }

          test "noPriorEvidence ≠ inputsChanged [] — structurally distinct, never collapsed (L-R5)" {
              // Two hand-formed reports (real typed values) isolating the distinction the contract fixes.
              let noPrior = CacheEligibilityReport [ { Gate = gid "a" "a"; Verdict = MustRecompute NoPriorEvidence } ]
              let emptyChanged = CacheEligibilityReport [ { Gate = gid "a" "a"; Verdict = MustRecompute(InputsChanged []) } ]

              use d1 = parse (CacheEligibilityJson.ofReport noPrior)
              use d2 = parse (CacheEligibilityJson.ofReport emptyChanged)
              let c1 = verdictCause (entryVerdict (List.exactlyOne (entriesOf d1)))
              let c2 = verdictCause (entryVerdict (List.exactlyOne (entriesOf d2)))

              Expect.equal (causeKind c1) "noPriorEvidence" "noPriorEvidence kind"
              Expect.isFalse (hasField c1 "categories") "noPriorEvidence has NO categories field"
              Expect.equal (causeKind c2) "inputsChanged" "inputsChanged kind"
              Expect.isTrue (hasField c2 "categories") "inputsChanged [] HAS a present categories field"
              Expect.equal (causeCategories c2) [] "inputsChanged [] categories is the empty array"
              Expect.notEqual (CacheEligibilityJson.ofReport noPrior) (CacheEligibilityJson.ofReport emptyChanged) "the two never render identically"
          }

          testPropertyWithConfig fscheckConfig "every mustRecompute entry carries exactly one cause from the closed vocabulary (L-R4)" (fun (r: CacheEligibilityReport) ->
              use doc = parse (CacheEligibilityJson.ofReport r)
              entriesOf doc
              |> List.forall (fun e ->
                  let v = entryVerdict e
                  match verdictKind v with
                  | "mustRecompute" ->
                      hasField v "cause"
                      && (let k = causeKind (verdictCause v) in k = "noPriorEvidence" || k = "inputsChanged")
                  | _ -> true)) ]
