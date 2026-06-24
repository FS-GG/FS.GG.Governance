module FS.GG.Governance.ReleaseJson.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseJson
open FS.GG.Governance.ReleaseJson.Tests.Support

// `ofRelease` is byte-deterministic (FR-008/SC-003): identical inputs ⇒ identical bytes; reordering the
// decision's partition lists never changes the output (the projection re-orders rules by the F053 composite
// key, value-keyed). No clock/path/env content.

let private rotate (k: int) (xs: 'a list) : 'a list =
    match xs with
    | [] -> []
    | _ ->
        let n = List.length xs
        let k = ((k % n) + n) % n
        (List.skip k xs) @ (List.take k xs)

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "ofRelease called twice on the same inputs is byte-identical" {
              Expect.equal (ReleaseJson.ofRelease decisionMixed sensedMixed) (ReleaseJson.ofRelease decisionMixed sensedMixed) "identical bytes"
          }

          test "reversing the partition lists does not change the bytes" {
              let reversed =
                  { decisionMixed with
                      Blockers = List.rev decisionMixed.Blockers
                      Warnings = List.rev decisionMixed.Warnings
                      Passing = List.rev decisionMixed.Passing }

              Expect.equal (ReleaseJson.ofRelease reversed sensedMixed) (ReleaseJson.ofRelease decisionMixed sensedMixed) "reorder-invariant"
          }

          testProperty "rotating the partition lists by any amount yields identical bytes" (fun (k: int) ->
              let shuffled =
                  { decisionMixed with
                      Blockers = rotate k decisionMixed.Blockers
                      Warnings = rotate k decisionMixed.Warnings
                      Passing = rotate k decisionMixed.Passing }

              ReleaseJson.ofRelease shuffled sensedMixed = ReleaseJson.ofRelease decisionMixed sensedMixed) ]
