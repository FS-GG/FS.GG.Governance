module FS.GG.Governance.CommandHost.Tests.CommandHostTests

open System
open System.IO
open Expecto
open FS.GG.Governance.CommandHost
open FS.GG.Governance.Snapshot.Model          // CommitId, DiffRange
open FS.GG.Governance.FreshnessKey.Model      // Revision
open FS.GG.Governance.EvidenceReuse           // empty
open FS.GG.Governance.CostBudget.Model        // CacheDecisionReport
open FS.GG.Governance.Adapters.SddHandoff

// Semantic tests over the 075 CommandHost leaf's PUBLIC surface, using REAL, literally-constructed domain
// values (Principle V — the helpers are pure; no mocks). These pin the behaviour the per-host copies relied
// on; behaviour preservation across the hosts is proven separately by the byte-identical command goldens.

let private withTempDir body =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-command-host-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        body dir
    finally
        Directory.Delete(dir, true)

let private validHandoff =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [], "dependencies": [] } }"""

let private writeHandoff (repo: string) (id: string) (json: string) =
    let dir = Path.Combine(repo, "readiness", id)
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "governance-handoff.json")
    File.WriteAllText(path, json)
    path

[<Tests>]
let tests =
    testList
        "CommandHost"
        [ test "under joins repo-relative paths, leaving `.`/empty clean" {
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
          }

          test "discoverHandoffs distinguishes absent and empty readiness state" {
              withTempDir (fun repo ->
                  Expect.equal (CommandHost.discoverHandoffs repo) CommandHost.HandoffsAbsent "no directory is absent"
                  Directory.CreateDirectory(Path.Combine(repo, "readiness")) |> ignore
                  Expect.equal (CommandHost.discoverHandoffs repo) CommandHost.HandoffsEmpty "empty directory is empty")
          }

          test "an unenumerable readiness path is explicitly unreadable" {
              withTempDir (fun repo ->
                  File.WriteAllText(Path.Combine(repo, "readiness"), "not a directory")

                  match CommandHost.discoverHandoffs repo with
                  | CommandHost.HandoffsUnreadable(source, message) ->
                      Expect.equal source "readiness" "enumeration faults identify the readiness root"
                      Expect.isNonEmpty message "the enumeration failure is retained"
                  | outcome -> failtestf "expected HandoffsUnreadable, got %A" outcome)
          }

          test "discoverHandoffs distinguishes loaded and malformed documents in ordinal order" {
              withTempDir (fun repo ->
                  writeHandoff repo "z-last" validHandoff |> ignore
                  writeHandoff repo "a-first" "{}" |> ignore

                  match CommandHost.discoverHandoffs repo with
                  | CommandHost.HandoffsMalformed(reads, diagnostics) ->
                      Expect.equal
                          (reads |> List.map (fun read -> read.Source))
                          [ "readiness/a-first/governance-handoff.json"
                            "readiness/z-last/governance-handoff.json" ]
                          "reads retain ordinal work-item order"

                      Expect.hasLength diagnostics 1 "only the malformed document produces a diagnostic"
                      Expect.equal diagnostics.Head.Cause Model.Malformed "malformed remains typed"
                  | outcome -> failtestf "expected HandoffsMalformed, got %A" outcome

                  File.Delete(Path.Combine(repo, "readiness", "a-first", "governance-handoff.json"))

                  match CommandHost.discoverHandoffs repo with
                  | CommandHost.HandoffsLoaded [ read ] ->
                      Expect.equal read.Source "readiness/z-last/governance-handoff.json" "valid read is loaded"
                  | outcome -> failtestf "expected one loaded handoff, got %A" outcome)
          }

          test "a locked handoff is unreadable and the compatibility port yields a diagnostic read" {
              withTempDir (fun repo ->
                  let path = writeHandoff repo "locked" validHandoff
                  use _lock = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)

                  match CommandHost.discoverHandoffs repo with
                  | CommandHost.HandoffsUnreadable(source, message) ->
                      Expect.equal source "readiness/locked/governance-handoff.json" "source stays repo-relative"
                      Expect.isNonEmpty message "the I/O failure is retained"
                  | outcome -> failtestf "expected HandoffsUnreadable, got %A" outcome

                  match CommandHost.realHandoffs repo with
                  | [ read ] ->
                      match Reader.parse read with
                      | Error diagnostic ->
                          Expect.equal diagnostic.Cause Model.Malformed "unreadable state becomes a malformed diagnostic"
                          Expect.stringContains diagnostic.Message "unreadable handoff state" "diagnostic names the fault"
                      | Ok _ -> failtest "unreadable state must never parse successfully"
                  | reads -> failtestf "expected one fail-closed diagnostic read, got %A" reads)
          } ]
