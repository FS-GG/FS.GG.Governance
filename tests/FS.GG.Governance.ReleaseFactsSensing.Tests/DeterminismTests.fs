module FS.GG.Governance.ReleaseFactsSensing.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// US3 (acc. 2, FR-008/FR-010, SC-003/SC-006): identical inputs ⇒ structurally identical `SensedRelease`
// (compare equal, every collection ordered); the output family set equals `releaseFamilies` across the
// satisfied / all-violating / all-unrecoverable fixtures (no family dropped or fabricated).

let private familyKeys (sensed: SensedRelease) =
    sensed.Facts.States |> Map.toList |> List.map fst |> List.sort

[<Tests>]
let tests =
    testList
        "DeterminismTests"
        [ test "two senseRelease over the identical real fixture are structurally equal" {
              withTempDir (fun dir ->
                  writeMetFixture dir
                  let port = Interpreter.realPort dir layout
                  let a = Interpreter.senseRelease port expectations
                  let b = Interpreter.senseRelease port expectations
                  Expect.equal a b "identical repository state ⇒ structurally identical SensedRelease (SC-003)")
          }

          test "two deriveFacts over identical hand-built evidence are equal" {
              let a = Sensing.deriveFacts expectations recoveredAllUnmet
              let b = Sensing.deriveFacts expectations recoveredAllUnmet
              Expect.equal a b "identical input ⇒ equal output"
          }

          test "the output family set equals releaseFamilies across satisfied/violating/unrecoverable" {
              let expectedSet = Sensing.releaseFamilies |> List.sort

              let satisfied = Sensing.deriveFacts expectations recoveredMet
              let violating = Sensing.deriveFacts expectations recoveredAllUnmet

              let unrecoverable =
                  Sensing.deriveFacts
                      expectations
                      { Version = Error "x"
                        Metadata = Error "x"
                        Pins = Error "x"
                        PublishPlan = Error "x"
                        TrustedPublishing = Error "x"
                        Provenance = Error "x" }

              Expect.equal (familyKeys satisfied) expectedSet "satisfied: family set = releaseFamilies (SC-006)"
              Expect.equal (familyKeys violating) expectedSet "violating: family set = releaseFamilies"
              Expect.equal (familyKeys unrecoverable) expectedSet "all-unrecoverable: family set = releaseFamilies"

              Expect.isTrue
                  (unrecoverable.Facts.States |> Map.forall (fun _ s -> s = Unrecoverable))
                  "all-error evidence ⇒ every family Unrecoverable"
          }

          test "extra unrelated repository artifacts are ignored (no fact for an unrecognized family)" {
              withTempDir (fun dir ->
                  writeMetFixture dir
                  // An unrelated file the layout never references must not add a family.
                  writeFile dir "UNRELATED.txt" "noise\n"
                  let sensed = Interpreter.senseRelease (Interpreter.realPort dir layout) expectations
                  Expect.equal (familyKeys sensed) (Sensing.releaseFamilies |> List.sort) "only the six known families")
          } ]
