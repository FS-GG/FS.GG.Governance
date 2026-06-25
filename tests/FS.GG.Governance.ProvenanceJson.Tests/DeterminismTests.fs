module FS.GG.Governance.ProvenanceJson.Tests.DeterminismTests

open System.Text.Json
open Expecto
open FS.GG.Governance.ProvenanceJson
open FS.GG.Governance.ProvenanceJson.Tests.Support

// US4 (T035): `ofSnapshot` is byte-identical for identical input; a duration-only change leaves the text's
// identity fields unchanged (only durationNanos differs); no wall-clock/abs-path/env beyond the opaque
// EnvironmentClass/BuilderIdentity tokens (FR-011, SC-006).

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "ofSnapshot is byte-identical for identical input" {
              Expect.equal (ProvenanceJson.ofSnapshot baseSnapshot) (ProvenanceJson.ofSnapshot baseSnapshot) "identical"
          }

          test "a duration-only change leaves the identity fields unchanged (only durationNanos differs)" {
              let fast = ProvenanceJson.ofSnapshot baseSnapshot

              let slow =
                  ProvenanceJson.ofSnapshot (snapshotOf [ { runBuild with Record = makeRecord 0 7_000_000L }; runTest; runFailed ])

              let idOf (s: string) = (JsonDocument.Parse s).RootElement.GetProperty("identity").GetString()
              Expect.equal (idOf fast) (idOf slow) "identity is duration-invariant"
              Expect.notEqual fast slow "the rendered durationNanos does differ"
          } ]
