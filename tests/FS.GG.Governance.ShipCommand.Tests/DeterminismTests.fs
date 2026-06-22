module FS.GG.Governance.ShipCommand.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// US4 (deterministic, byte-stable artifacts) for `fsgg ship` (FR-012, SC-007; L5): the same repository state
// yields byte-identical audit.json — INCLUDING the cache section — across runs; cache verdicts follow each
// gate item's existing position (the F045 embed owns ordering); no wall-clock / cwd / absolute-path text
// leaks into the cache section.

let private auditDocOf git =
    let req = requestFor Loop.DefaultRange Loop.Json
    let cap = newCapture ()
    Interpreter.run (fakePorts validCatalog git cap req) req |> ignore
    writtenAudit cap |> Option.map snd |> Option.defaultValue ""

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "same repo state ⇒ byte-identical audit.json incl the cache section over two runs (SC-007, L5)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let d1 = auditDocOf git
              let d2 = auditDocOf git
              Expect.equal d1 d2 "audit.json byte-identical across runs (cache section included)"
              Expect.stringContains d1 "\"cacheEligibilityEvaluated\":true" "the compared documents carry an evaluated cache section"
          }

          test "no wall-clock / cwd / absolute-path text leaks into audit.json (incl the cache section)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let doc = (auditDocOf git).ToLowerInvariant()
              for token in [ "/tmp/"; "/home/"; "c:\\"; "timestamp"; "datetime"; "utc" ] do
                  Expect.isFalse (doc.Contains token) (sprintf "excluded token %s must not appear in audit.json" token)
          } ]
