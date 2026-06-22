module FS.GG.Governance.RouteCommand.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// US4 (deterministic, byte-stable artifacts) for `fsgg route` (FR-012, SC-007; L5): the same repository
// state yields byte-identical route.json — INCLUDING the cache section — across runs; cache verdicts follow
// the document's existing selected-gate order (the F045 embed owns ordering); no wall-clock / cwd /
// absolute-path text leaks into the cache section.

let private routeDocOf git =
    let req = requestFor Loop.DefaultRange Loop.Json
    let cap = newCapture ()
    Interpreter.run (fakePorts validCatalog git cap req) req |> ignore
    writtenOf cap Loop.RouteArtifact |> Option.map snd |> Option.defaultValue ""

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "same repo state ⇒ byte-identical route.json incl the cache section over two runs (SC-007, L5)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let d1 = routeDocOf git
              let d2 = routeDocOf git
              Expect.equal d1 d2 "route.json byte-identical across runs (cache section included)"
              Expect.stringContains d1 "\"cacheEligibilityEvaluated\":true" "the compared documents carry an evaluated cache section"
          }

          test "the same diff sensed via Since vs DefaultRange yields the same route.json cache section" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

              let docOf scope =
                  let req = { requestFor scope Loop.Json with Repo = "." }
                  let cap = newCapture ()
                  Interpreter.run (fakePorts validCatalog git cap req) req |> ignore
                  writtenOf cap Loop.RouteArtifact |> Option.map snd |> Option.defaultValue ""

              Expect.equal (docOf (Loop.Since "HEAD~2")) (docOf Loop.DefaultRange) "identical faked diff ⇒ identical route.json across scopes"
          }

          test "no wall-clock / cwd / absolute-path text leaks into route.json (incl the cache section)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let doc = (routeDocOf git).ToLowerInvariant()
              for token in [ "/tmp/"; "/home/"; "c:\\"; "timestamp"; "datetime"; "utc" ] do
                  Expect.isFalse (doc.Contains token) (sprintf "excluded token %s must not appear in route.json" token)
          } ]
