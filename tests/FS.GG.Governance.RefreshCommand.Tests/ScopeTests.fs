module FS.GG.Governance.RefreshCommand.Tests.ScopeTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// FR-015 / research D6 — a selector narrows the evaluated set; out-of-scope views are NotEvaluated (never
// assumed current), counted, and projected `not-evaluated`. The default scope evaluates all views.

let private senseCount effects =
    effects
    |> List.filter (function Loop.SenseSource _ -> true | _ -> false)
    |> List.length

[<Tests>]
let tests =
    testList
        "Scope"
        [ test "--view <id> senses only the selected view; the other is NotEvaluated" {
              let req = { requestFor "." with DryRun = true; Scope = Loop.ByView "doc" }
              let m0, _ = Loop.init req
              let m1, effects = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlTwoViews))) m0
              Expect.equal (senseCount effects) 1 "only the in-scope view is sensed"

              // Complete sensing for the in-scope view, then inspect the decision.
              let m2, _ = Loop.update (Loop.Sensed("doc", Ok(digestsOf [ "d" ], GeneratorVersion "g1"))) m1
              let m3, _ = Loop.update (Loop.RecordedRead("doc", Some(digestsOf [ "d" ], GeneratorVersion "g1"))) m2

              match m3.Decision with
              | Some d ->
                  Expect.equal d.NotEvaluatedCount 1 "the out-of-scope view is counted not-evaluated"

                  match d.Views |> List.find (fun v -> v.Entry.ViewId = "cat") |> (fun v -> v.Status) with
                  | NotEvaluated -> ()
                  | other -> failtestf "expected NotEvaluated for the out-of-scope view, got %A" other
              | None -> failtest "expected a decision"
          }

          test "--view-kind narrows by kind" {
              let req = { requestFor "." with DryRun = true; Scope = Loop.ByKind(RuleCatalog) }
              let m0, _ = Loop.init req
              let _, effects = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlTwoViews))) m0
              // refreshYmlTwoViews declares one baseline ("doc") and one rule-catalog ("cat").
              Expect.equal (senseCount effects) 1 "only the rule-catalog view is in scope"
          }

          test "the default scope evaluates all declared views" {
              let m0, _ = Loop.init (requestFor ".")
              let _, effects = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlTwoViews))) m0
              Expect.equal (senseCount effects) 2 "no selector ⇒ all views"
          }

          test "a not-evaluated view projects status `not-evaluated` in refresh.json" {
              let entry = (parseYml refreshYmlOneView).Entries.Head

              let decision =
                  { Outcome = NothingToRefresh
                    DryRun = false
                    Views = [ { Entry = entry; Status = NotEvaluated; Drifted = [] } ]
                    RegeneratedCount = 0
                    CurrentCount = 0
                    UnresolvedCount = 0
                    NotEvaluatedCount = 1 }

              Expect.stringContains (RefreshJson.ofRefreshDecision decision) "\"status\":\"not-evaluated\"" "projected token"
          } ]
