module FS.GG.Governance.ShipCommand.Tests.InterpreterTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// US1/US2/US3 (the edge): `Interpreter.run` over FAKED ports (in-memory FileReader, in-memory GitPort
// over canned read-only git output, capturing ArtifactWriter/OutputSink) — no real git, no real
// filesystem (FR-013, SC-007). The written bytes are compared to the genuine F025 projection of the
// F024 rollup of the same typed inputs.

let private runWith files git scope format mode profile =
    let req = requestForLevers scope format mode profile
    let cap = newCapture ()
    let model = Interpreter.run (fakePorts files git cap req) req
    req, cap, model

[<Tests>]
let tests =
    testList
        "Interpreter"
        [ // ── US1: roll up + persist; bytes = F025(F024 rollup); blocked/clean exit ──

          test "base-blocking change ⇒ audit bytes = F025(F024 rollup), verdict fail, exit 1 (US1 AS1, SC-001, SC-007)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text Gate Standard
              let candidates = candidatesOf git defaultOpts
              let expected = auditExpected validCatalog candidates Gate Standard

              Expect.equal (writtenAudit cap) (Some(req.AuditOut, expected)) "audit.json = AuditJson.ofShipDecision (Ship.rollup …)"
              Expect.equal model.Exit Loop.Blocked "exit decision Blocked"
              Expect.equal (Loop.exitCode model.Exit) 1 "exit code 1"
              Expect.equal (Option.get model.Decision).Verdict Fail "verdict fail"
          }

          test "passing-only change ⇒ verdict pass, exit 0, audit still written (US1 AS2, SC-001)" {
              let git = gitWithChanges [ 'M', "notes.txt" ]
              let req, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text Gate Standard
              let candidates = candidatesOf git defaultOpts
              let expected = auditExpected validCatalog candidates Gate Standard

              Expect.equal (Option.get model.Decision).Verdict Pass "verdict pass"
              Expect.equal model.Exit Loop.Success "exit Success"
              Expect.equal (Loop.exitCode model.Exit) 0 "exit code 0"
              Expect.equal (writtenAudit cap) (Some(req.AuditOut, expected)) "audit.json written for a pass too"
          }

          test "the summary states the verdict, the partition, and the written path (US1 AS3)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req, cap, _ = runWith validCatalog git Loop.DefaultRange Loop.Text Gate Standard
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a summary was emitted"
              Expect.stringContains summary "verdict fail" "summary states the verdict"
              Expect.stringContains summary "blockers:" "summary lists the blockers section"
              Expect.stringContains summary req.AuditOut "summary names the written path"
          }

          // ── US2 / SC-003: two lever sets, the no-hide rule ──

          test "same change, two lever sets ⇒ two verdicts/exit codes; relaxed lands in warnings (US2 AS1/AS2, SC-003, FR-011)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

              // Strict lever set: gate/standard ⇒ block-on-ship gates effective-Blocking ⇒ fail/blocked.
              let _, _, strict = runWith validCatalog git Loop.DefaultRange Loop.Text Gate Standard
              Expect.equal strict.Exit Loop.Blocked "strict set ⇒ Blocked (exit 1)"
              Expect.isNonEmpty (Option.get strict.Decision).Blockers "the base-blocking gates are blockers"

              // Relaxed lever set: inner/standard ⇒ run mode below the block-on-ship floor ⇒ effective-Advisory.
              let _, _, relaxed = runWith validCatalog git Loop.DefaultRange Loop.Text Inner Standard
              Expect.equal relaxed.Exit Loop.Success "relaxed set ⇒ Success (exit 0)"
              let warnings = (Option.get relaxed.Decision).Warnings
              Expect.isNonEmpty warnings "the relaxed base-blocking gates land in warnings (no-hide)"
              // Every warning carries base Blocking + effective Advisory — the no-hide rule.
              for w in warnings do
                  Expect.equal w.Decision.BaseSeverity Blocking "warning keeps base severity Blocking"
                  Expect.equal w.Decision.EffectiveSeverity Advisory "warning shows effective severity Advisory"
          }

          test "the applied mode/profile flow through F024→F025 and are recorded per item (US2 AS1, T023)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

              // The persisted audit equals the F025 projection of the rollup under each distinct lever
              // set — proving Loop.fs reads request.Mode/Profile (not a hardcoded default) end to end.
              let candidates = candidatesOf git defaultOpts

              let _, capA, _ = runWith validCatalog git Loop.DefaultRange Loop.Json Gate Standard
              let docA = writtenAudit capA |> Option.map snd |> Option.defaultValue ""
              Expect.equal docA (auditExpected validCatalog candidates Gate Standard) "gate/standard bytes match the rollup"
              Expect.stringContains docA "\"mode\":\"gate\"" "audit records mode gate per item"
              Expect.stringContains docA "\"profile\":\"standard\"" "audit records profile standard per item"

              let _, capB, _ = runWith validCatalog git Loop.DefaultRange Loop.Json Inner Strict
              let docB = writtenAudit capB |> Option.map snd |> Option.defaultValue ""
              Expect.equal docB (auditExpected validCatalog candidates Inner Strict) "inner/strict bytes match the rollup"
              Expect.stringContains docB "\"mode\":\"inner\"" "audit records mode inner per item"
              Expect.stringContains docB "\"profile\":\"strict\"" "audit records profile strict per item"

              Expect.notEqual docA docB "different lever sets ⇒ different audit documents"
          }

          // ── US3 / SC-002/SC-005: determinism, JSON contract, exclusion ──

          test "twice-run over fixed inputs+levers ⇒ byte-identical audit, --json stdout, and exit (US3 AS1, SC-002)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, cap1, m1 = runWith validCatalog git Loop.DefaultRange Loop.Json Gate Standard
              let _, cap2, m2 = runWith validCatalog git Loop.DefaultRange Loop.Json Gate Standard

              Expect.equal (writtenAudit cap1) (writtenAudit cap2) "audit.json byte-identical across runs"
              Expect.equal cap1.Emits cap2.Emits "--json stdout byte-identical across runs"
              Expect.equal m1.Exit m2.Exit "exit decision identical across runs"
          }

          test "--json stdout equals the persisted audit content exactly; text suppressed (US3 AS2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, capJson, _ = runWith validCatalog git Loop.DefaultRange Loop.Json Gate Standard
              let _, capText, _ = runWith validCatalog git Loop.DefaultRange Loop.Text Gate Standard

              let j = Expect.wantSome (List.tryHead capJson.Emits) "json summary emitted"
              let persisted = writtenAudit capJson |> Option.map snd |> Option.defaultValue ""
              Expect.equal j persisted "--json stdout = the persisted audit.json byte-for-byte"
              use _doc = JsonDocument.Parse j // throws if not valid JSON
              Expect.isFalse (j.Contains "verdict fail") "json form is not the human text"

              let t = Expect.wantSome (List.tryHead capText.Emits) "text summary emitted"
              Expect.stringContains t "verdict" "text form is the human summary"
              Expect.isFalse (t.TrimStart().StartsWith "{") "text form is not JSON"
          }

          test "the persisted audit carries schemaVersion and no clock/abs-path/env (US3 AS3, SC-005)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, cap, _ = runWith validCatalog git Loop.DefaultRange Loop.Json Gate Standard
              let doc = writtenAudit cap |> Option.map snd |> Option.defaultValue ""

              Expect.stringContains doc AuditJson.schemaVersion "the declared schemaVersion is present"
              Expect.stringContains doc "\"schemaVersion\"" "schemaVersion field present"
              // No wall-clock / absolute-path / environment token leaks (inherited from F025).
              let lower = doc.ToLowerInvariant()
              for token in [ "/tmp/"; "/home/"; "c:\\"; "timestamp"; "datetime"; "utc" ] do
                  Expect.isFalse (lower.Contains token) (sprintf "excluded token %s must not appear" token)
          }

          // ── SC-006: empty scope / empty catalog are CLEAN passes ──

          test "an empty changed-path set ⇒ valid empty-partition audit, verdict pass, exit 0 (SC-006)" {
              let _, cap, model = runWith validCatalog gitEmpty Loop.DefaultRange Loop.Json Gate Standard
              Expect.equal (Option.get model.Decision).Blockers [] "no changed paths ⇒ no blockers"
              Expect.equal model.Exit Loop.Success "empty scope is a clean pass"
              Expect.isSome (writtenAudit cap) "a valid empty-partition audit.json is still written"
          }

          test "a valid empty catalog ⇒ empty partition, verdict pass, exit 0 (SC-006)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let candidates = candidatesOf git defaultOpts
              let expected = auditExpected emptyCatalog candidates Gate Standard
              let req, cap, model = runWith emptyCatalog git Loop.DefaultRange Loop.Json Gate Standard

              Expect.equal (writtenAudit cap) (Some(req.AuditOut, expected)) "audit.json = projection of the empty registry rollup"
              Expect.equal (Option.get model.Decision).Blockers [] "empty registry ⇒ no blockers"
              Expect.equal model.Exit Loop.Success "empty catalog is a clean pass"
          }

          // ── SC-006 / FR-012: routine-only paths are information, never a default-deny ──

          test "a routine-only change is a clean pass; blocking is decided by the cores, not the host (FR-012, T030a)" {
              // notes.txt is an unclassified in-root path ⇒ a GovernedRootUnknown advisory finding;
              // it selects no gate, so the verdict is decided entirely by Ship.rollup.
              let git = gitWithChanges [ 'M', "notes.txt" ]
              let candidates = candidatesOf git defaultOpts
              let _, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Json Gate Standard

              Expect.equal (Option.get model.Decision).Verdict Pass "routine path ⇒ pass (no default-deny)"
              Expect.equal model.Exit Loop.Success "routine path ⇒ exit 0"
              // The host edge re-decides nothing: the persisted bytes equal the cores' own projection.
              let expected = auditExpected validCatalog candidates Gate Standard
              Expect.equal (writtenAudit cap |> Option.map snd) (Some expected) "host edge never decides blocking — bytes = cores' output"
          } ]
