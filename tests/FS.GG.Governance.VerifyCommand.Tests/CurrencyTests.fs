module FS.GG.Governance.VerifyCommand.Tests.CurrencyTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T020/T023 (US2) — currency findings are a first-class projection (data-model §6), computed purely from the
// model's Sensed/Store/SelectedGates and surfaced in the text render: per selected check fresh/reused vs
// stale/recomputed (with its cause) vs recompute-by-default (with the missing freshness tokens), EACH
// carrying its owning gate's enforcement-assigned effective severity. The verdict stays driven ONLY by the
// reused `Ship.rollup`/`applyExecution` (no new severity path): a blocking currency finding rides the EXISTING
// rollup to Blocked, an advisory one is a warning only.

// A `--since` scope so the fake git port supplies a base/head Range ⇒ gates resolve their freshness facts
// (and so can be cached/reused or reported stale), rather than landing unresolved with no base/head.
let private srcScope = Loop.Since "HEAD~1"

let private runWith profile sensor exec =
    let cap = newCapture ()
    let req = requestForProfile srcScope Loop.Text profile
    let ports = fakePortsExec validCatalog gitSrcChange sensor absentStoreReader exec cap
    Interpreter.run ports req |> ignore
    String.concat "\n" cap.Emits

[<Tests>]
let tests =
    testList
        "Currency (US2)"
        [ test "an empty store ⇒ every selected check is stale/recomputed (noPriorEvidence) in the currency section" {
              let text = runWith Standard fakeSensor fakeExecPortPass
              Expect.stringContains text "currency:" "currency header present"
              Expect.stringContains text "stale/recomputed:" "stale/recomputed block"
              Expect.stringContains text "noPriorEvidence" "noPriorEvidence cause"
          }

          test "a freshness sensing failure ⇒ recompute-by-default with the missing freshness tokens" {
              let text = runWith Standard throwingSensor fakeExecPortPass
              Expect.stringContains text "recompute by default" "recompute-by-default block"
              // a non-fatal degrade note is appended (never changes the verdict).
              Expect.stringContains text "currency note:" "non-fatal degrade note"
          }

          test "currency findings carry the owning gate's effective severity — advisory under Standard" {
              let text = runWith Standard fakeSensor fakeExecPortPass
              Expect.stringContains text "[advisory]" "advisory severity tag at Verify/Standard"
          }

          test "a blocking-severity currency finding rides the EXISTING rollup to Blocked (no second route)" {
              // At Verify/Strict the block-on-ship checks are effective-Blocking; a failing run leaves them
              // blockers (the existing rollup) and the currency finding carries [blocking].
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Text Strict
              let ports = fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortFail cap
              let model = Interpreter.run ports req
              let text = String.concat "\n" cap.Emits
              Expect.equal model.Exit Loop.Blocked "blocking check ⇒ Blocked via the existing rollup"
              Expect.stringContains text "[blocking]" "blocking severity tag at Verify/Strict"
              Expect.stringContains text "verdict blocked" "verdict blocked" } ]
