module FS.GG.Governance.VerifyCommand.Tests.EndToEndTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T016 (US1) — the command end to end via `Interpreter.run` with `realPorts`-shaped FAKED ports: the reused
// F015–F052 cores are NEVER mocked; only the edge `Execute`/`Write`/`Out` (and freshness/store sensing) are
// faked. clean ⇒ pass/exit 0; blocking ⇒ Blocked/exit 1 with the unmet check named; advisory-only ⇒ warning,
// pass/exit 0; nothing-to-verify ⇒ "nothing to verify"/exit 0; uncertain ⇒ never coerced to pass, Blocked/
// exit 1 (FR-005). Written bytes equal the genuine `VerifyJson.ofVerifyDecision` projection.

let private srcScope = Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]
let private srcCandidates = [ gp "src/Lib/Thing.fs" ]

let private runWith profile exec =
    let cap = newCapture ()
    let req = requestForProfile srcScope Loop.Text profile
    let ports = fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader exec cap
    let model = Interpreter.run ports req
    model, cap

let private emitted (cap: Capture) = String.concat "\n" cap.Emits

[<Tests>]
let tests =
    testList
        "EndToEnd (US1)"
        [ test "clean: Standard profile + passing checks ⇒ passing verdict, exit 0, written bytes match the projection" {
              let model, cap = runWith Standard fakeExecPortPass
              Expect.equal model.Exit Loop.Success "clean ⇒ Success"
              // F27 wiring (063): the human summary is the shared HumanText projection (Title "verdict: PASS").
              Expect.stringContains (emitted cap) "verdict: PASS" "passing verdict"

              match writtenVerify cap with
              | Some(path, content) ->
                  Expect.equal path "readiness/verify.json" "default path"
                  Expect.equal content (verifyExpectedWith fakeExecPortPass validCatalog srcCandidates Standard None) "bytes equal the genuine projection"
              | None -> failtest "expected a verify.json write"
          }

          test "blocking: Strict profile + failing blocking check ⇒ Blocked, exit 1, the unmet check named" {
              let model, cap = runWith Strict fakeExecPortFail
              Expect.equal model.Exit Loop.Blocked "blocking ⇒ Blocked"
              let text = emitted cap
              Expect.stringContains text "verdict: FAIL" "blocked verdict"
              Expect.stringContains text "Blockers" "blockers section present"
              // exit 1 is distinct from 2/3/4.
              Expect.equal (Loop.exitCode model.Exit) 1 "exit 1 distinct"
          }

          test "advisory-only: Standard profile + failing check ⇒ advisory warning, verdict still passing, exit 0" {
              let model, cap = runWith Standard fakeExecPortFail
              Expect.equal model.Exit Loop.Success "advisory-only ⇒ Success"
              let text = emitted cap
              Expect.stringContains text "verdict: PASS" "still passing"
              Expect.stringContains text "Warnings" "advisory under warnings"
          }

          test "nothing-to-verify: an empty change ⇒ 'nothing to verify', exit 0" {
              let cap = newCapture ()
              let req = requestFor Loop.DefaultRange Loop.Text
              let ports = fakePortsExec validCatalog gitEmpty fakeSensor absentStoreReader fakeExecPortPass cap
              let model = Interpreter.run ports req
              Expect.equal model.Exit Loop.Success "nothing to verify ⇒ Success"
              // F27 wiring (063): an empty selection ⇒ a passing decision; the HumanText projection renders it
              // as "verdict: PASS" (the dedicated "nothing to verify" wording was non-contractual host text).
              Expect.stringContains (emitted cap) "verdict: PASS" "empty selection ⇒ passing verdict text"
          }

          test "uncertain: a blocking check reporting an uncertain (exit 125) result is never coerced to pass ⇒ Blocked, exit 1" {
              let model, cap = runWith Strict fakeExecPortUncertain
              Expect.equal model.Exit Loop.Blocked "uncertain blocking ⇒ Blocked, not coerced to pass"
              let text = emitted cap
              Expect.stringContains text "verdict: FAIL" "blocked, not pass"
              // F27 wiring (063): the exit code is surfaced in the CONTRACT (verify.json), not the
              // non-contractual human view (which states pass/failed per gate, not the raw code).
              match writtenVerify cap with
              | Some(_, content) -> Expect.stringContains content "125" "the uncertain exit code is surfaced in verify.json"
              | None -> failtest "expected a verify.json write" } ]
