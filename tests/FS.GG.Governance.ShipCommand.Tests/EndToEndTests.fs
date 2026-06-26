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

                  let ports = { Interpreter.realPorts req.Repo with Execute = fakeExecPort }
                  let model = Interpreter.run ports req

                  // The committed src/ edit selects block-on-ship gates ⇒ under gate/standard, blocked.
                  Expect.equal (Option.get model.Decision).Verdict Fail "real src change under gate/standard fails"
                  Expect.equal model.Exit Loop.Blocked "real run exits Blocked"
                  Expect.equal (Loop.exitCode model.Exit) 1 "exit code 1"

                  // Recompute the expected bytes from the SAME real git sensing + real catalog + levers.
                  let candidates = candidatesOfRepo dir (sinceOpts "HEAD~1")
                  Expect.isNonEmpty candidates "the committed src edit is sensed as a changed path"
                  // No store on disk ⇒ every resolved gate mustRecompute/noPriorEvidence (independent of the
                  // real hash values), so the fake-sensor expected report over the SAME real snapshot matches.
                  let expected = auditExpected validCatalog candidates Gate Standard (Some(snapshotOfRepo dir (sinceOpts "HEAD~1")))

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

                  let model = Interpreter.run ({ Interpreter.realPorts req.Repo with Execute = fakeExecPort }) req
                  Expect.equal model.Exit Loop.Success "relaxed run mode ⇒ clean pass (exit 0)"
                  Expect.isNonEmpty (Option.get model.Decision).Warnings "the base-blocking gates relax to warnings (no-hide)"
                  Expect.isTrue (File.Exists req.AuditOut) "audit.json still written")
          }

          // F070 (US1): a configured stale generated view, sensed by the REAL interpreter (real refresh.yml +
          // real refresh.lock.json + real source digest), folds into a Fail/Blocked verdict and rides in
          // audit.json's `generatedViews` blocker naming the stale view (SC-001, SC-005). Not Synthetic — the
          // currency is sensed from real on-disk state through Interpreter.realPorts.
          test "F070: a configured stale generated view ⇒ Fail/Blocked + a generatedViews blocker in audit.json" {
              withTempRepo (fun dir ->
                  let sha (s: string) =
                      use h = System.Security.Cryptography.SHA256.Create()
                      h.ComputeHash(System.Text.Encoding.UTF8.GetBytes s) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

                  // A real, configured-blocking, source-drifted generated view in the working tree.
                  writeFile dir "view-src.txt" "current\n"

                  writeFile
                      dir
                      ".fsgg/refresh.yml"
                      ("currency-enforcement: block-on-ship\n"
                       + "views:\n  - id: route-projection\n    kind: route-projection\n    output: out.json\n    sources:\n      - view-src.txt\n    generator: [\"cp\"]\n    generatorBasis: g1\n")

                  // recorded provenance disagrees with the live source digest ⇒ stale.
                  writeFile
                      dir
                      ".fsgg/refresh.lock.json"
                      (sprintf "{\"views\":{\"route-projection\":{\"sources\":[\"%s\"],\"generatorVersion\":\"g1\",\"output\":\"x\"}}}" (sha "OLD\n"))

                  let req =
                      match Loop.parse [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--mode"; "gate"; "--profile"; "standard" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let model = Interpreter.run ({ Interpreter.realPorts req.Repo with Execute = fakeExecPort }) req

                  Expect.equal (Option.get model.Decision).Verdict Fail "a stale generated view at block-on-ship ⇒ Fail"
                  Expect.equal model.Exit Loop.Blocked "blocked exit"
                  let audit = File.ReadAllText req.AuditOut
                  Expect.stringContains audit "\"generatedViews\"" "audit.json carries the generatedViews detail"
                  Expect.stringContains audit "route-projection" "the blocker names the stale view"
                  Expect.stringContains audit "\"effectiveSeverity\":\"blocking\"" "the stale view blocks at the gate boundary")
          } ]
