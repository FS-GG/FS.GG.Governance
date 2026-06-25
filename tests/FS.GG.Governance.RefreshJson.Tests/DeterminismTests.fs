module FS.GG.Governance.RefreshJson.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.RefreshJson
open FS.GG.Governance.RefreshJson.Tests.Support

// `ofRefreshDecision` is byte-deterministic and carries no clock/abs-path/env/username content (FR-007/SC-004).

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "two calls on identical input are byte-identical" {
              Expect.equal (RefreshJson.ofRefreshDecision decisionMixed) (RefreshJson.ofRefreshDecision decisionMixed) "byte-identical"
          }

          test "the document carries no clock / absolute-path / username / machine content" {
              let doc = RefreshJson.ofRefreshDecision decisionMixed
              // No drive-absolute path, no obvious timestamp marker.
              Expect.isFalse (doc.Contains "/home/") "no absolute home path"
              Expect.isFalse (doc.Contains ":\\") "no absolute Windows path"
              Expect.isFalse (doc.Contains "T00:") "no ISO timestamp"
          }

          test "the clean decision projects a valid document" {
              let doc = RefreshJson.ofRefreshDecision decisionClean
              Expect.stringContains doc "\"outcome\":\"nothing-to-refresh\"" "clean outcome token"
          } ]
