module FS.GG.Governance.CommandHost.Tests.CommandHostTests

open Expecto
open FS.GG.Governance.CommandHost
open FS.GG.Governance.Snapshot.Model          // CommitId, DiffRange
open FS.GG.Governance.FreshnessKey.Model      // Revision
open FS.GG.Governance.EvidenceReuse           // empty
open FS.GG.Governance.CostBudget.Model        // CacheDecisionReport

// Semantic tests over the 075 CommandHost leaf's PUBLIC surface, using REAL, literally-constructed domain
// values (Principle V — the helpers are pure; no mocks). These pin the behaviour the per-host copies relied
// on; behaviour preservation across the hosts is proven separately by the byte-identical command goldens.

[<Tests>]
let tests =
    testList
        "CommandHost"
        [ test "exitCode maps every case to its canonical code" {
              Expect.equal (CommandHost.exitCode CommandHost.Success) 0 "Success -> 0"
              Expect.equal (CommandHost.exitCode CommandHost.Blocked) 1 "Blocked -> 1"
              Expect.equal (CommandHost.exitCode CommandHost.UsageError') 2 "UsageError' -> 2"
              Expect.equal (CommandHost.exitCode CommandHost.InputUnavailable) 3 "InputUnavailable -> 3"
              Expect.equal (CommandHost.exitCode CommandHost.ToolError) 4 "ToolError -> 4"
          }

          test "under joins repo-relative paths, leaving `.`/empty clean" {
              Expect.equal (CommandHost.under "." ".fsgg/gates.json") ".fsgg/gates.json" "dot repo is clean"
              Expect.equal (CommandHost.under "" "readiness/route.json") "readiness/route.json" "empty repo is clean"
              Expect.equal (CommandHost.under "/r" "a.json") "/r/a.json" "real repo is prefixed"
              Expect.equal (CommandHost.under "/r/" "a.json") "/r/a.json" "trailing slash is trimmed"
          }

          test "revOfCommit lifts a CommitId into a Revision verbatim" {
              Expect.equal (CommandHost.revOfCommit (CommitId "abc123")) (Revision "abc123") "verbatim lift"
          }

          test "baseHeadOf reads the diff-range, or (None, None) when absent" {
              Expect.equal (CommandHost.baseHeadOf None) (None, None) "no range -> none"

              let range: DiffRange =
                  { Base = CommitId "b"
                    Head = CommitId "h"
                    MergeBase = CommitId "m" }

              Expect.equal
                  (CommandHost.baseHeadOf (Some range))
                  (Some(Revision "b"), Some(Revision "h"))
                  "range -> base/head revisions"
          }

          test "emptySensedFacts is all-empty (never fabricated)" {
              Expect.isNone CommandHost.emptySensedFacts.RuleHash "no rule hash"
              Expect.isNone CommandHost.emptySensedFacts.Base "no base"
              Expect.isNone CommandHost.emptySensedFacts.Head "no head"
              Expect.isEmpty (Map.toList CommandHost.emptySensedFacts.CoveredArtifacts) "no covered artifacts"
          }

          test "describeInvalid summarises an empty diagnostic list" {
              // The non-empty path (message + id token per diagnostic) is exercised end-to-end through the
              // command goldens, which construct real Config diagnostics; here we pin the empty-list form.
              Expect.equal (CommandHost.describeInvalid []) "catalog invalid" "empty -> bare message"
          }

          test "executionPlan with no sensed/store yields the empty plan (Route's no-input branch)" {
              let plan, inputs, _ =
                  CommandHost.executionPlan { CommandHost.BudgetFold = None } None None [] None "."

              Expect.isEmpty plan "no gates classified"
              Expect.isEmpty (Map.toList inputs) "no freshness inputs"
          }

          test "executionPlan with a budget fold runs it (Ship/Verify branch) over empty gates" {
              let foldReport = CacheDecisionReport []
              let parms = { CommandHost.BudgetFold = Some(fun _ -> Map.empty, foldReport) }

              let plan, inputs, report =
                  CommandHost.executionPlan parms (Some CommandHost.emptySensedFacts) (Some EvidenceReuse.empty) [] None "."

              Expect.isEmpty plan "no gates -> no classifications"
              Expect.isEmpty (Map.toList inputs) "no freshness inputs"
              Expect.equal report foldReport "the budget fold's report is threaded through"
          } ]
