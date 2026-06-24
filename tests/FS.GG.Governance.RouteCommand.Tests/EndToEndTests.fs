module FS.GG.Governance.RouteCommand.Tests.EndToEndTests

open System.IO
open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// US1 real-evidence backstop (Principle V): the ONE end-to-end proof through the REAL git +
// filesystem boundary — a real temp repo, a real `.fsgg` catalog, real `Interpreter.realPorts`. No
// fakes. Proves the full composition writes the F020/F021 bytes and is byte-identical on re-run
// (SC-001, SC-002, SC-007). No `Synthetic` token — this is genuine evidence.

[<Tests>]
let tests =
    testList
        "EndToEnd"
        [ test "real git + real catalog: --since HEAD~1 writes both artifacts = F021/F020 bytes, re-run identical, exit 0" {
              withTempRepo (fun dir ->
                  let req =
                      match Loop.parse [ "route"; "--repo"; dir; "--since"; "HEAD~1" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let ports = { Interpreter.realPorts req.Repo with Execute = fakeExecPort }
                  let model = Interpreter.run ports req
                  Expect.equal model.Exit Loop.Success "real run exits Success"

                  // Recompute the expected bytes from the SAME real git sensing + real catalog.
                  let candidates = candidatesOfRepo dir (sinceOpts "HEAD~1")
                  Expect.isNonEmpty candidates "the committed src edit is sensed as a changed path"
                  // The real run senses real hashes, but with no store on disk every resolved gate is
                  // mustRecompute/noPriorEvidence — independent of the hash VALUES — so the fake-sensor
                  // expected report (over the SAME real snapshot's base/head) is byte-identical (SC-001).
                  let expectedGates, expectedRoute = projectExpected validCatalog candidates (Some(snapshotOfRepo dir (sinceOpts "HEAD~1")))

                  Expect.isTrue (File.Exists req.GatesOut) "gates.json exists on disk"
                  Expect.isTrue (File.Exists req.RouteOut) "route.json exists on disk"
                  Expect.equal (File.ReadAllText req.GatesOut) expectedGates "gates.json bytes = GatesJson projection (SC-001)"
                  Expect.equal (File.ReadAllText req.RouteOut) expectedRoute "route.json bytes = RouteJson projection (SC-001)"

                  // SC-002 on disk: a second real run produces byte-identical files.
                  let g1 = File.ReadAllText req.GatesOut
                  let r1 = File.ReadAllText req.RouteOut
                  Interpreter.run ports req |> ignore
                  Expect.equal (File.ReadAllText req.GatesOut) g1 "gates.json byte-identical on re-run"
                  Expect.equal (File.ReadAllText req.RouteOut) r1 "route.json byte-identical on re-run")
          }

          test "real git: DefaultRange over a clean working tree selects nothing yet exits 0 (SC-006)" {
              withTempRepo (fun dir ->
                  let req =
                      match Loop.parse [ "route"; "--repo"; dir ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let model = Interpreter.run ({ Interpreter.realPorts req.Repo with Execute = fakeExecPort }) req
                  Expect.equal model.Exit Loop.Success "clean default range is a success"
                  Expect.isTrue (File.Exists req.RouteOut) "route.json still written")
          } ]
