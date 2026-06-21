module FS.GG.Governance.Ship.Tests.TotalityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.Ship.Tests.Support

// US3: `rollup` is TOTAL (defined for every routed change incl. the empty one), never throws, and
// accounts for every item exactly once in a sorted, disjoint, exhaustive partition (SC-005/SC-006).

let private itemCount (route: RouteResult) =
    route.SelectedGates.Length + route.Findings.Findings.Length

// The documented composite sort key, reconstructed in the test (the real `itemSortKey` is a hidden
// helper, absent from Ship.fsi — we assert the contract, not the private binding).
let private expectedKey (item: EnforcedItem) =
    match item.Id with
    | GateItem id -> "gate:" + gateIdValue id
    | FindingItem(id, GovernedPath path) -> "finding:" + path + ":" + findingIdToken id

[<Tests>]
let tests =
    testList
        "Totality"
        [ test "empty route ⇒ pass / empty / clean at every mode and profile (edge: empty change)" {
              for mode in allModes do
                  for profile in allProfiles do
                      let d = rollup emptyRoute mode profile
                      Expect.equal d.Verdict Pass "pass"
                      Expect.isEmpty d.Blockers "no blockers"
                      Expect.isEmpty d.Warnings "no warnings"
                      Expect.isEmpty d.Passing "no passing"
                      Expect.equal d.ExitCodeBasis Clean "clean"
          }

          testPropertyWithConfig fsCheckConfig "rollup never throws — returns a decision for every input (SC-005)" (fun (route, mode, profile) ->
              let d = rollup route mode profile
              // Forcing the lists proves no deferred exception lurks.
              d.Blockers.Length + d.Warnings.Length + d.Passing.Length >= 0)

          testPropertyWithConfig fsCheckConfig "partition law: |B|+|W|+|P| = N+M, disjoint, exhaustive (SC-006)" (fun (route, mode, profile) ->
              let d = rollup route mode profile
              let all = d.Blockers @ d.Warnings @ d.Passing
              let ids = all |> List.map (fun i -> i.Id)
              let distinctIds = ids |> List.distinct

              // sizes sum to N+M, and no item identity appears twice (disjoint + 1:1 over inputs).
              all.Length = itemCount route && distinctIds.Length = all.Length)

          testPropertyWithConfig fsCheckConfig "each output list is sorted ascending by the composite key (FR-009)" (fun (route, mode, profile) ->
              let d = rollup route mode profile
              let keys (items: EnforcedItem list) = items |> List.map expectedKey
              let sortedAsc (xs: string list) = xs = List.sort xs
              sortedAsc (keys d.Blockers) && sortedAsc (keys d.Warnings) && sortedAsc (keys d.Passing)) ]
