module FS.GG.Governance.RefreshJson.Tests.RefreshJsonTests

open System.Text.Json
open Expecto
open FS.GG.Governance.RefreshJson
open FS.GG.Governance.RefreshJson.Tests.Support

// `ofRefreshDecision` shape (refresh.schema.md): fixed top-level key order, the summary counts, views in
// declared order with the exhaustive status tokens, `drifted` omitted/empty when current, `reason` ONLY for
// stale-unresolved.

let private doc = RefreshJson.ofRefreshDecision decisionMixed
let private root = JsonDocument.Parse(doc).RootElement
let private views = root.GetProperty "views"
let private view i = views[i]

[<Tests>]
let tests =
    testList
        "RefreshJson"
        [ test "schemaVersion is the fixed constant" {
              Expect.equal RefreshJson.schemaVersion "fsgg.refresh/v1" "constant"
              Expect.equal (root.GetProperty("schemaVersion").GetString()) "fsgg.refresh/v1" "stamped"
          }

          test "top-level keys are in the fixed order" {
              let keys = [ for p in root.EnumerateObject() -> p.Name ]
              Expect.equal keys [ "schemaVersion"; "outcome"; "dryRun"; "summary"; "views" ] "fixed key order"
          }

          test "summary carries the four counts" {
              let s = root.GetProperty "summary"
              Expect.equal (s.GetProperty("regenerated").GetInt32()) 1 "regenerated"
              Expect.equal (s.GetProperty("current").GetInt32()) 1 "current"
              Expect.equal (s.GetProperty("unresolved").GetInt32()) 1 "unresolved"
              Expect.equal (s.GetProperty("notEvaluated").GetInt32()) 1 "notEvaluated"
          }

          test "views are in declared order with the exhaustive status tokens" {
              let statuses = [ for v in views.EnumerateArray() -> v.GetProperty("status").GetString() ]
              Expect.equal statuses [ "regenerated"; "current"; "stale-unresolved"; "not-evaluated" ] "declared order + tokens"
          }

          test "drifted is present for a regenerated view, empty for a current view" {
              let drifted (v: JsonElement) = [ for d in v.GetProperty("drifted").EnumerateArray() -> d.GetString() ]
              Expect.equal (drifted (view 0)) [ "coveredArtifacts" ] "regenerated carries the drifted category token"
              Expect.equal (drifted (view 1)) [] "a current view has no drift"
          }

          test "reason is present ONLY for stale-unresolved (FR-010/FR-016)" {
              let hasReason (v: JsonElement) = fst (v.TryGetProperty "reason")
              Expect.isFalse (hasReason (view 0)) "regenerated ⇒ no reason"
              Expect.isFalse (hasReason (view 1)) "current ⇒ no reason"
              Expect.isTrue (hasReason (view 2)) "stale-unresolved ⇒ reason"
              Expect.stringContains (view(2).GetProperty("reason").GetString()) "source not found" "names why"
          }

          test "each view carries id/kind/output verbatim" {
              Expect.equal (view(0).GetProperty("id").GetString()) "gate-metadata" "id"
              Expect.equal (view(0).GetProperty("kind").GetString()) "gate-metadata" "kind token"
              Expect.equal (view(0).GetProperty("output").GetString()) "docs/gates.json" "output"
              Expect.equal (view(3).GetProperty("kind").GetString()) "custom-kind" "Other kind renders verbatim"
          } ]
