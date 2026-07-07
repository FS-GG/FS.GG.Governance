module FS.GG.Governance.ShipCommand.Tests.DryRunTests

open Expecto
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Adapters.SddHandoff.Model
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// 112 (`fsgg ship --dry-run`): the SIMULATED gate. `--dry-run` runs the ship pipeline with NO gate command
// executed (every gate `NotExecuted`), NOTHING written to `readiness/`, the store never persisted, and a
// clearly-marked simulated projection (schema `fsgg.audit.dryrun/v1`) printed with a handoff-sufficiency
// breakdown. A preview: it exits 0 regardless of the simulated verdict. Driven through the PUBLIC
// `Loop.parse` / `Interpreter.run` and the pure `Simulate` core (Principle V — faked edges, real cores).

let private parse argv = Loop.parse argv

let private handoffRead json : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead =
    { Source = "readiness/wi-1/governance-handoff.json"; Json = json }

// A handoff whose only real, non-stale signal is `build:lib`; `test:unit` is failed (a required-absent gap).
let private mixedJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "real" }, { "id": "test:unit", "state": "failed" } ], "dependencies": [] } }"""

// A handoff carrying NOTHING real — the all-not-evaluated (FS.GG.Audio) failure mode.
let private allAbsentJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "pending" }, { "id": "test:unit", "state": "failed" } ], "dependencies": [] } }"""

// A dry-run request over a src change (selects block-on-ship command gates that would normally execute).
let private dryReq format = { requestFor Loop.DefaultRange format with DryRun = true }

let private runDry format execPort cap handoffs =
    let req = dryReq format
    let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

    let ports =
        { fakePortsExec validCatalog git fakeSensor absentStoreReader execPort cap req with
            Handoffs = fun _ -> handoffs }

    Interpreter.run ports req

[<Tests>]
let tests =
    testList
        "DryRun"
        [ // ── US1: parse ──
          test "--dry-run ⇒ DryRun = true; composes with --since/--mode/--profile/--json (US1)" {
              match parse [ "ship"; "--dry-run" ] with
              | Ok req -> Expect.isTrue req.DryRun "--dry-run sets DryRun"
              | Error e -> failtestf "expected Ok, got %A" e

              match parse [ "ship"; "--since"; "HEAD~1"; "--dry-run"; "--profile"; "strict"; "--json" ] with
              | Ok req ->
                  Expect.isTrue req.DryRun "composes with other flags"
                  Expect.equal req.Scope (Loop.Since "HEAD~1") "scope preserved"
                  Expect.equal req.Format Loop.Json "json preserved"
              | Error e -> failtestf "expected Ok, got %A" e

              match parse [ "ship" ] with
              | Ok req -> Expect.isFalse req.DryRun "default is not dry-run"
              | Error e -> failtestf "expected Ok, got %A" e
          }

          test "--dry-runn (typo) ⇒ UnknownFlag, writes nothing" {
              Expect.equal (parse [ "ship"; "--dry-runn" ]) (Error(Loop.UnknownFlag "--dry-runn")) "typo is a usage error"
          }

          // ── US1: no execution, no writes, exit 0, determinism ──
          test "dry-run executes NO gate command and writes NO artifact (SC-003)" {
              let counter = { Calls = 0 }
              let cap = newCapture ()
              let model = runDry Loop.Text (countingExecPort counter 0) cap []

              Expect.equal counter.Calls 0 "no gate command is spawned in a dry run"
              Expect.isEmpty cap.Writes "no readiness/ artifact is written"
              Expect.isNonEmpty cap.Emits "the simulated summary is printed"
              Expect.stringContains (List.head cap.Emits) "SIMULATED (dry-run)" "output is marked simulated"
              Expect.equal model.Phase Loop.Done "the run reaches Done"
          }

          test "a real run over the SAME ports WOULD execute — proving dry-run's suppression is real" {
              // Contrast: without --dry-run the same selected gates spawn the exec port (counter > 0).
              let counter = { Calls = 0 }
              let cap = newCapture ()
              let req = requestFor Loop.DefaultRange Loop.Text // NOT dry-run
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              Interpreter.run (fakePortsExec validCatalog git fakeSensor absentStoreReader (countingExecPort counter 0) cap req) req
              |> ignore
              Expect.isGreaterThan counter.Calls 0 "a normal ship DOES execute the selected command gates"
          }

          test "dry-run is a PREVIEW — it exits 0 even when the simulated verdict is Fail (contract exit status)" {
              let cap = newCapture ()
              // A failing handoff node makes the simulated verdict Fail; the process still exits 0.
              let model = runDry Loop.Text fakeExecPort cap [ handoffRead mixedJson ]
              Expect.equal (Option.get model.Decision).Verdict Fail "the simulated verdict is Fail"
              Expect.equal model.Exit Loop.Success "a dry run still exits 0 (preview, not a gate)"
          }

          test "dry-run output is deterministic — identical inputs ⇒ byte-identical stdout (SC-005)" {
              let cap1 = newCapture ()
              let cap2 = newCapture ()
              runDry Loop.Json fakeExecPort cap1 [ handoffRead mixedJson ] |> ignore
              runDry Loop.Json fakeExecPort cap2 [ handoffRead mixedJson ] |> ignore
              Expect.equal cap1.Emits cap2.Emits "two identical dry runs emit identical bytes"
          }

          // ── US2: sufficiency classification (pure) ──
          test "classify: real⇒satisfied, pending/failed/skipped⇒absent, stale⇒absent, deferred/accepted/synthetic⇒not-required" {
              Expect.equal (Simulate.classify Real false) Simulate.RequiredSatisfied "real, fresh ⇒ satisfied"
              Expect.equal (Simulate.classify Real true) Simulate.RequiredAbsent "real but STALE ⇒ absent"
              Expect.equal (Simulate.classify Pending false) Simulate.RequiredAbsent "pending ⇒ absent"
              Expect.equal (Simulate.classify Failed false) Simulate.RequiredAbsent "failed ⇒ absent"
              Expect.equal (Simulate.classify Skipped false) Simulate.RequiredAbsent "skipped ⇒ absent"
              Expect.equal (Simulate.classify Deferred false) Simulate.NotRequired "deferred ⇒ not-required"
              Expect.equal (Simulate.classify AcceptedDeferral false) Simulate.NotRequired "accepted ⇒ not-required"
              Expect.equal (Simulate.classify Synthetic false) Simulate.NotRequired "synthetic ⇒ not-required"
          }

          // ── US2: absence surfaced, never a bare Pass (FR-011) ──
          test "a handoff missing a required signal names the required-absent gap in the output (SC-002/FR-011)" {
              let cap = newCapture ()
              runDry Loop.Json fakeExecPort cap [ handoffRead mixedJson ] |> ignore
              let json = List.head cap.Emits
              Expect.stringContains json "\"requiredAbsent\"" "the failed node is classified required-absent"
              Expect.stringContains json "test:unit" "the specific absent signal is named"
              Expect.stringContains json "\"requiredSatisfied\"" "the real node is classified satisfied"
          }

          test "an all-absent handoff surfaces allNotEvaluated=true (the notEvaluated failure mode)" {
              let cap = newCapture ()
              runDry Loop.Json fakeExecPort cap [ handoffRead allAbsentJson ] |> ignore
              let json = List.head cap.Emits
              Expect.stringContains json "\"allNotEvaluated\": true" "nothing real was carried ⇒ allNotEvaluated true"

              let capT = newCapture ()
              runDry Loop.Text fakeExecPort capT [ handoffRead allAbsentJson ] |> ignore
              Expect.stringContains (List.head capT.Emits) "all-not-evaluated" "the text form surfaces the absence explicitly"
          }

          // ── US2: safe failure on a bad handoff (FR-008) ──
          test "a malformed handoff surfaces a diagnostic and does NOT read as a bare Pass (FR-008)" {
              let cap = newCapture ()
              runDry Loop.Json fakeExecPort cap [ handoffRead "{ this is not valid json" ] |> ignore
              let json = List.head cap.Emits
              Expect.stringContains json "\"handoffDiagnostics\"" "the document carries a diagnostics block"
              Expect.stringContains json "malformed" "the malformed cause is surfaced"
              // A malformed handoff yields a blocking integrity gate ⇒ the simulated verdict is not a clean Pass.
              Expect.stringContains json "\"verdict\": \"fail\"" "a defect-in-input is not emitted as Pass"
          }

          // ── US3: machine-readable simulated document ──
          test "dry-run --json emits the DISTINCT dryrun schema + simulated marker; never the real audit schema (G1/G2/G5)" {
              let cap = newCapture ()
              runDry Loop.Json fakeExecPort cap [ handoffRead mixedJson ] |> ignore
              let json = List.head cap.Emits
              Expect.stringContains json "fsgg.audit.dryrun/v1" "distinct dry-run schema id"
              Expect.stringContains json "\"simulated\": true" "explicit simulated marker"
              Expect.stringContains json "\"sufficiency\"" "carries the sufficiency block"
              Expect.isFalse (json.Contains "fsgg.audit/v2") "the real audit schema id never appears in simulated output"
              Expect.equal SimulateProjection.schemaVersion "fsgg.audit.dryrun/v1" "the projection's schema id is the distinct one"
          } ]
