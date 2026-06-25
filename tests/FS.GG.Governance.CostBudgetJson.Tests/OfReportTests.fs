module FS.GG.Governance.CostBudgetJson.Tests.OfReportTests

open System.Text.Json
open Expecto
open FS.GG.Governance.CostBudgetJson
open FS.GG.Governance.CostBudgetJson.Tests.Support

// US2 (T024): `ofReport` over a mixed report emits schemaVersion, a `decisions` array in GateId-ordinal
// order with the tagged-union decision shape, and a `findings` array; fixed field order verified by raw-text
// IndexOf; an agentReviewed review is labelled but its CacheKey is NOT emitted (contracts/cost-budget-json.md).

let private json = CostBudgetJson.ofReport mixedReport []
let private doc = JsonDocument.Parse json
let private root = doc.RootElement

let private decisions = root.GetProperty("decisions")
let private decisionAt i = decisions.[i]
let private prop (e: JsonElement) (name: string) = e.GetProperty(name).GetString()

[<Tests>]
let tests =
    testList
        "OfReport"
        [ test "schemaVersion is fsgg.cost-budget/v1" {
              Expect.equal (root.GetProperty("schemaVersion").GetString()) "fsgg.cost-budget/v1" "schema"
              Expect.equal CostBudgetJson.schemaVersion "fsgg.cost-budget/v1" "constant"
          }

          test "decisions are present in GateId-ordinal order, one per entry" {
              Expect.equal (decisions.GetArrayLength()) 5 "five decisions"
              let gates = [ for i in 0..4 -> prop (decisionAt i) "gate" ]
              Expect.equal gates [ "a:reuse"; "b:recompute"; "c:noev"; "d:skip"; "e:defer" ] "ordinal order preserved"
          }

          test "each entry carries gate, cost, review, decision in fixed order" {
              let e = decisionAt 0
              Expect.equal (prop e "cost") "cheap" "cost token"
              Expect.equal (prop e "review") "deterministic" "review token"
              let raw = e.GetRawText()
              Expect.isLessThan (raw.IndexOf "\"gate\"") (raw.IndexOf "\"cost\"") "gate < cost"
              Expect.isLessThan (raw.IndexOf "\"cost\"") (raw.IndexOf "\"review\"") "cost < review"
              Expect.isLessThan (raw.IndexOf "\"review\"") (raw.IndexOf "\"decision\"") "review < decision"
          }

          test "reuse decision carries the opaque evidence verbatim" {
              let d = (decisionAt 0).GetProperty("decision")
              Expect.equal (d.GetProperty("kind").GetString()) "reuse" "kind"
              Expect.equal (d.GetProperty("evidence").GetString()) "ev-1" "evidence verbatim"
          }

          test "recompute decision reuses the F042 cause shape (inputsChanged + categories)" {
              let d = (decisionAt 1).GetProperty("decision")
              Expect.equal (d.GetProperty("kind").GetString()) "recompute" "kind"
              let cause = d.GetProperty("cause")
              Expect.equal (cause.GetProperty("kind").GetString()) "inputsChanged" "cause kind"
              let cats = [ for c in cause.GetProperty("categories").EnumerateArray() -> c.GetString() ]
              Expect.equal cats [ "ruleHash"; "baseRevision" ] "categories named via categoryToken in order"
          }

          test "noPriorEvidence cause has no categories field" {
              let cause = (decisionAt 2).GetProperty("decision").GetProperty("cause")
              Expect.equal (cause.GetProperty("kind").GetString()) "noPriorEvidence" "cause kind"
              Expect.isFalse (fst (cause.TryGetProperty "categories")) "no categories field for noPriorEvidence"
          }

          test "overBudget decision carries class, ceiling, and a reason string" {
              let skip = (decisionAt 3).GetProperty("decision")
              Expect.equal (skip.GetProperty("kind").GetString()) "overBudget" "kind"
              Expect.equal (skip.GetProperty("class").GetString()) "skipped" "class"
              Expect.equal (skip.GetProperty("ceiling").GetString()) "cheap" "ceiling"
              Expect.equal (skip.GetProperty("reason").GetString()) "d:skip (exhaustive) exceeds the cheap budget" "reason"

              let defer = (decisionAt 4).GetProperty("decision")
              Expect.equal (defer.GetProperty("class").GetString()) "deferred" "deferred class"
              Expect.equal (defer.GetProperty("reason").GetString()) "e:defer (high) exceeds the medium budget" "reason"
          }

          test "an agentReviewed review is labelled but its CacheKey is NEVER emitted" {
              Expect.equal (prop (decisionAt 1) "review") "agentReviewed" "labelled agentReviewed"
              Expect.isFalse (json.Contains "SECRET-CACHE-KEY-DO-NOT-EMIT") "the CacheKey is not a blocking signal in the document"
          }

          test "findings is always present (empty array when none)" {
              Expect.equal (root.GetProperty("findings").GetArrayLength()) 0 "empty findings array present"
          }

          test "top-level field order is schemaVersion < decisions < findings" {
              Expect.isLessThan (json.IndexOf "\"schemaVersion\"") (json.IndexOf "\"decisions\"") "schemaVersion < decisions"
              Expect.isLessThan (json.IndexOf "\"decisions\"") (json.IndexOf "\"findings\"") "decisions < findings"
          }

          test "findings render kind/baseSeverity, with categories ONLY for stale" {
              let withFindings = CostBudgetJson.ofReport mixedReport mixedFindings
              let fdoc = JsonDocument.Parse withFindings
              let findings = fdoc.RootElement.GetProperty("findings")
              Expect.equal (findings.GetArrayLength()) 3 "three findings"
              // synthetic taint — no categories
              let synth = findings.[0]
              Expect.equal (synth.GetProperty("kind").GetString()) "syntheticTaint" "synthetic kind"
              Expect.equal (synth.GetProperty("baseSeverity").GetString()) "advisory" "advisory"
              Expect.isFalse (fst (synth.TryGetProperty "categories")) "no categories for syntheticTaint"
              // stale — categories present
              let stale = findings.[1]
              Expect.equal (stale.GetProperty("kind").GetString()) "stale" "stale kind"
              let cats = [ for c in stale.GetProperty("categories").EnumerateArray() -> c.GetString() ]
              Expect.equal cats [ "ruleHash"; "baseRevision" ] "stale categories"
          } ]
