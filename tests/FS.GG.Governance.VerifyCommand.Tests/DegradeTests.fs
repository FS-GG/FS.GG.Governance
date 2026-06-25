module FS.GG.Governance.VerifyCommand.Tests.DegradeTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T022 (US2) — a freshness or store SENSING `Error` degrades to a safe default + a non-fatal currency note and
// NEVER changes the verdict or the exit (FR-010/FR-013). It is not an exit code.

let private srcScope = Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]

[<Tests>]
let tests =
    testList
        "Degrade (US2)"
        [ test "a freshness sensing failure degrades with a note and leaves the verdict/exit unchanged" {
              let cap = newCapture ()
              let ports = fakePortsExec validCatalog gitSrcChange throwingSensor absentStoreReader fakeExecPortPass cap
              let model = Interpreter.run ports (requestForProfile srcScope Loop.Text Standard)
              Expect.equal model.Exit Loop.Success "freshness degrade ⇒ exit unchanged (Success)"
              // F27 wiring (063): the non-fatal note is a model fact (the non-contractual human summary no
              // longer echoes it).
              Expect.stringContains (String.concat " " model.CurrencyNotes) "currency note:" "non-fatal currency note recorded"
          }

          test "a malformed store degrades (StoreDegraded), suppresses persistence, leaves the verdict/exit unchanged" {
              let cap = newCapture ()
              let ports = fakePortsExec validCatalog gitSrcChange fakeSensor malformedStoreReader fakeExecPortPass cap
              let model = Interpreter.run ports (requestForProfile srcScope Loop.Text Standard)
              Expect.equal model.Exit Loop.Success "store degrade ⇒ exit unchanged (Success)"
              Expect.isTrue model.StoreDegraded "store marked degraded"
              // F27 wiring (063): the non-fatal note is a model fact (the non-contractual human summary no
              // longer echoes it).
              Expect.stringContains (String.concat " " model.CurrencyNotes) "currency note:" "non-fatal currency note recorded"
          }

          test "the degraded verdict equals the non-degraded verdict (a note never perturbs the rollup)" {
              let capOk = newCapture ()
              let mOk = Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortPass capOk) (requestForProfile srcScope Loop.Text Standard)

              let capDeg = newCapture ()
              let mDeg = Interpreter.run (fakePortsExec validCatalog gitSrcChange throwingSensor malformedStoreReader fakeExecPortPass capDeg) (requestForProfile srcScope Loop.Text Standard)

              Expect.equal mDeg.Exit mOk.Exit "degraded exit equals clean exit" } ]
