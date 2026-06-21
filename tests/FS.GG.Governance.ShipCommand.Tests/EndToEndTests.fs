module FS.GG.Governance.ShipCommand.Tests.EndToEndTests

open System.IO
open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// US1 real-evidence backstop (Principle V): the ONE end-to-end proof through the REAL git +
// filesystem boundary — a real temp repo, a real `.fsgg` catalog, real `Interpreter.realPorts`. No
// fakes. Proves the full composition writes the F025 bytes (of the F024 rollup), carries the verdict,
// maps the exit code, and is byte-identical on re-run (SC-001, SC-002, SC-007). No `Synthetic` token —
// this is genuine evidence.

[<Tests>]
let tests =
    testList
        "EndToEnd"
        [ test "real git + real catalog: --since HEAD~1 under gate/standard ⇒ fail/blocked, exit 1, bytes = F025 rollup, re-run identical" {
              withTempRepo (fun dir ->
                  let req =
                      match Loop.parse [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--mode"; "gate"; "--profile"; "standard" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let ports = Interpreter.realPorts req.Repo
                  let model = Interpreter.run ports req

                  // The committed src/ edit selects block-on-ship gates ⇒ under gate/standard, blocked.
                  Expect.equal (Option.get model.Decision).Verdict Fail "real src change under gate/standard fails"
                  Expect.equal model.Exit Loop.Blocked "real run exits Blocked"
                  Expect.equal (Loop.exitCode model.Exit) 1 "exit code 1"

                  // Recompute the expected bytes from the SAME real git sensing + real catalog + levers.
                  let candidates = candidatesOfRepo dir (sinceOpts "HEAD~1")
                  Expect.isNonEmpty candidates "the committed src edit is sensed as a changed path"
                  let expected = auditExpected validCatalog candidates Gate Standard

                  Expect.isTrue (File.Exists req.AuditOut) "audit.json exists on disk"
                  Expect.equal (File.ReadAllText req.AuditOut) expected "audit.json bytes = AuditJson.ofShipDecision (Ship.rollup …) (SC-001)"

                  // SC-002 on disk: a second real run produces a byte-identical file.
                  let a1 = File.ReadAllText req.AuditOut
                  Interpreter.run ports req |> ignore
                  Expect.equal (File.ReadAllText req.AuditOut) a1 "audit.json byte-identical on re-run")
          }

          test "real git: a relaxing run mode lands the same change clean (exit 0), no-hide warnings" {
              withTempRepo (fun dir ->
                  let req =
                      match Loop.parse [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--mode"; "inner"; "--profile"; "standard" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let model = Interpreter.run (Interpreter.realPorts req.Repo) req
                  Expect.equal model.Exit Loop.Success "relaxed run mode ⇒ clean pass (exit 0)"
                  Expect.isNonEmpty (Option.get model.Decision).Warnings "the base-blocking gates relax to warnings (no-hide)"
                  Expect.isTrue (File.Exists req.AuditOut) "audit.json still written")
          } ]
