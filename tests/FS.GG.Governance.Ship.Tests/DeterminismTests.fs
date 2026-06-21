module FS.GG.Governance.Ship.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.Ship.Tests.Support

// US3: identical inputs yield a byte-identical decision; ordering comes from `itemSortKey`, not
// input-arrival order (research D6) — so shuffling the input lists does not change the result.

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig fsCheckConfig
              "rollup is deterministic — run twice ⇒ structurally identical decision (SC-004)"
              (fun (route, mode, profile) ->
                  let a = rollup route mode profile
                  let b = rollup route mode profile
                  a = b)

          testPropertyWithConfig fsCheckConfig
              "shuffling the input SelectedGates / Findings does not change the result (FR-009)"
              (fun (route, mode, profile) ->
                  let reversed =
                      { route with
                          SelectedGates = List.rev route.SelectedGates
                          Findings = { route.Findings with Findings = List.rev route.Findings.Findings } }

                  rollup route mode profile = rollup reversed mode profile) ]
