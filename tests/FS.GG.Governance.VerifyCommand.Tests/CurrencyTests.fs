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
    let model = Interpreter.run ports req
    model, String.concat "\n" cap.Emits

[<Tests>]
let tests =
    testList
        "Currency (US2)"
        [ test "an empty store ⇒ every selected check is recompute (no prior evidence) in the cache-eligibility section" {
              let _, text = runWith Standard fakeSensor fakeExecPortPass
              // F27 wiring (063): currency is now the HumanText "Cache eligibility" section; an absent store ⇒
              // every selected check is `recompute: no prior evidence`.
              Expect.stringContains text "Cache eligibility" "cache-eligibility section present"
              Expect.stringContains text "recompute:" "recompute outcome listed"
              Expect.stringContains text "no prior evidence" "no-prior-evidence cause"
          }

          test "a freshness sensing failure ⇒ a recorded non-fatal currency note, verdict unchanged" {
              // F27 wiring (063): the missing-freshness degrade is a model/contract fact — a non-fatal currency
              // note recorded on the model (no longer echoed in the non-contractual human summary) — and the
              // verdict/exit stay unchanged.
              let model, _ = runWith Standard throwingSensor fakeExecPortPass
              Expect.equal model.Exit Loop.Success "a sensing failure never changes the exit"
              Expect.isNonEmpty model.CurrencyNotes "a non-fatal currency note is recorded"
              Expect.stringContains (String.concat " " model.CurrencyNotes) "could not be sensed" "the note names the unsensed input"
          }

          test "currency findings carry the owning gate's effective severity — advisory under Standard" {
              // F27 wiring (063): at Verify/Standard the block-on-ship checks are effective-Advisory; a failing
              // run leaves them in the HumanText "Warnings" section, each item detail prefixed with its
              // effective severity (`advisory — …`). The verdict stays a pass.
              let model, text = runWith Standard fakeSensor fakeExecPortFail
              Expect.equal model.Exit Loop.Success "advisory-only ⇒ still a pass"
              Expect.stringContains text "advisory" "advisory effective severity surfaced under Standard"
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
              Expect.stringContains text "blocking" "blocking effective severity at Verify/Strict"
              Expect.stringContains text "verdict: FAIL" "verdict blocked" } ]
