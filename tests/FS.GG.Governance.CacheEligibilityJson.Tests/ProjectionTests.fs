module FS.GG.Governance.CacheEligibilityJson.Tests.ProjectionTests

open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.CacheEligibilityJson.Tests.Support

// US1 (SC-001, SC-004, L-R1/L-R2/L-R3/L-R6): a real `CacheEligibilityReport` projects to one faithful entry
// per report entry — its declared gate id and its verdict (reusable + opaque evidence, or mustRecompute +
// named cause) — every value tracing back to the report. Output is parsed back with a read-only
// `JsonDocument`; nothing is recomputed, dropped, merged, deduplicated, reordered, or invented.

/// Render the verdict a verdict-element carries back to a comparable F041 value (so we assert against the
/// report value, not raw substrings).
let private verdictShape (v: System.Text.Json.JsonElement) =
    match verdictKind v with
    | "reusable" -> Reusable(EvidenceRef(verdictEvidence v))
    | "mustRecompute" ->
        let c = verdictCause v
        match causeKind c with
        | "noPriorEvidence" -> MustRecompute NoPriorEvidence
        | "inputsChanged" ->
            // round-trip the category tokens back through the F029 vocabulary
            let cats =
                causeCategories c
                |> List.map (fun tok ->
                    allCategories
                    |> List.tryFind (fun (cat, _) -> categoryToken cat = tok)
                    |> Option.map fst
                    |> Option.defaultWith (fun () -> failwithf "unknown category token %s" tok))
            MustRecompute(InputsChanged cats)
        | other -> failwithf "unknown cause kind %s" other
    | other -> failwithf "unknown verdict kind %s" other

[<Tests>]
let tests =
    testList
        "Projection"
        [ test "one entry per report entry, gate + verdict tracing back, in order (L-R1)" {
              let r = report [ candidate (gid "docs" "lint") baseInputs ] exactStore
              use doc = parse (CacheEligibilityJson.ofReport r)
              let es = CacheEligibility.entries r
              let rendered = entriesOf doc

              Expect.equal rendered.Length (List.length es) "exactly one rendered entry per report entry"

              List.iteri
                  (fun i (e: CacheEligibilityEntry) ->
                      let rE = rendered.[i]
                      Expect.equal (entryGate rE) (gateIdValue e.Gate) (sprintf "entry %d gate verbatim" i)
                      Expect.equal (verdictShape (entryVerdict rE)) e.Verdict (sprintf "entry %d verdict tracing back" i))
                  es
          }

          testPropertyWithConfig fscheckConfig "entries length + gate sequence equal the report's (L-R1, FsCheck)" (fun (r: CacheEligibilityReport) ->
              use doc = parse (CacheEligibilityJson.ofReport r)
              let es = CacheEligibility.entries r
              let rendered = entriesOf doc
              rendered.Length = List.length es
              && (rendered |> List.map entryGate) = (es |> List.map (fun e -> gateIdValue e.Gate)))

          test "reusable carries its evidence verbatim, no cause field (L-R3)" {
              let r = report [ candidate (gid "docs" "lint") baseInputs ] (storeOf [ baseInputs, refA ])
              use doc = parse (CacheEligibilityJson.ofReport r)
              let v = entryVerdict (List.exactlyOne (entriesOf doc))
              Expect.equal (verdictKind v) "reusable" "kind reusable"
              Expect.equal (verdictEvidence v) (EvidenceReuse.referenceValue refA) "evidence is referenceValue ref verbatim"
              Expect.isFalse (hasField v "cause") "reusable verdict has no cause field"
          }

          test "mustRecompute / noPriorEvidence, no evidence field (L-R4)" {
              let r = report [ candidate (gid "security" "scan") baseInputs ] EvidenceReuse.empty
              use doc = parse (CacheEligibilityJson.ofReport r)
              let v = entryVerdict (List.exactlyOne (entriesOf doc))
              Expect.equal (verdictKind v) "mustRecompute" "kind mustRecompute"
              Expect.isFalse (hasField v "evidence") "mustRecompute verdict has no evidence field"
              Expect.equal (causeKind (verdictCause v)) "noPriorEvidence" "cause noPriorEvidence"
          }

          test "mustRecompute / inputsChanged names exactly the categories in order (L-R4)" {
              // RuleHash + Head moved against a recorded exact-match base ⇒ InputsChanged [ruleHash; headRevision].
              let moved = baseInputs |> (fun i -> { i with RuleHash = RuleHash "r2" }) |> (fun i -> { i with Head = Revision "ddd" })
              let r = report [ candidate (gid "build" "tests") moved ] (storeOf [ baseInputs, refA ])
              use doc = parse (CacheEligibilityJson.ofReport r)
              let v = entryVerdict (List.exactlyOne (entriesOf doc))
              Expect.equal (verdictKind v) "mustRecompute" "kind mustRecompute"
              let cats = causeCategories (verdictCause v)
              // assert against the report's own carried order via categoryToken
              let expected =
                  match (CacheEligibility.entries r |> List.exactlyOne).Verdict with
                  | MustRecompute(InputsChanged cs) -> cs |> List.map categoryToken
                  | other -> failwithf "expected InputsChanged, got %A" other
              Expect.equal cats expected "categories are categoryToken of the report's cats, in order"
              Expect.equal cats [ "ruleHash"; "headRevision" ] "the worked-example tokens, in report order"
          }

          test "gate id rendered verbatim across a ':' separator (L-R6)" {
              let r = report [ candidate (gid "build" "tests") baseInputs ] EvidenceReuse.empty
              use doc = parse (CacheEligibilityJson.ofReport r)
              Expect.equal (entryGate (List.exactlyOne (entriesOf doc))) "build:tests" "gate id verbatim, never re-parsed"
          } ]
