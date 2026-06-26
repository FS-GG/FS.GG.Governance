module FS.GG.Governance.VerifyCommand.Tests.SurfaceRollupTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// 067 (F24 verify-host wiring) US1 — the verdict fold, driven through the PUBLIC `Interpreter.run`/`update`
// path (never a Model literal). A `SurfacesSensed` carrying a blocking surface finding makes the verify
// verdict block at `RunMode.Verify`; an advisory finding leaves the exit code untouched. The verdict rides the
// EXISTING `deriveEffectiveSeverity` (via `enforcementInputOf`) — no new constant, no truth-table change: the
// SAME blocking finding that fails under Strict does NOT escalate under Standard, exactly as a block-on-pr
// gate would. The findings here are hand-built (SYNTHETIC, disclosed in Support) so the fold can be exercised
// without a drifted tree; the real-disk sense is proven in SurfaceChecksE2ETests.

let private srcScope = Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]

// Run the real verify pipeline over the in-memory catalog with a synthetic surface-sense port returning the
// given findings, and a PASSING gate exec (so any block comes from the surface finding, not a gate).
let private runWithFindings (profile: Profile) (findings: _ list) =
    let cap = newCapture ()
    let ports =
        { fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortPass cap with
            SenseSurfaces = syntheticSurfaceSense findings }
    Interpreter.run ports (requestForProfile srcScope Loop.Text profile), cap

[<Tests>]
let tests =
    testList
        "SurfaceRollup (US1)"
        [ test "a blocking surface finding fails the run at Verify under Strict (folded via deriveEffectiveSeverity)" {
              let model, _ = runWithFindings Strict [ blockingSurfaceFinding ]
              Expect.equal model.Exit Loop.Blocked "blocking surface finding ⇒ Blocked"
              Expect.equal (Loop.exitCode model.Exit) 1 "exit 1"
          }

          test "an advisory surface finding never escalates — exit unchanged from a clean run" {
              let clean, _ = runWithFindings Standard []
              let advised, _ = runWithFindings Standard [ advisorySurfaceFinding ]
              Expect.equal clean.Exit Loop.Success "clean ⇒ Success"
              Expect.equal advised.Exit Loop.Success "advisory-only ⇒ Success (no escalation)"
              Expect.equal advised.Exit clean.Exit "advisory exit equals a clean run's exit"
          }

          test "the truth table is NOT re-opened: the SAME blocking finding relaxes to advisory under Standard" {
              // block-on-pr base-Blocking ⇒ effective-Blocking only once the verify floor is reached (Strict);
              // under Standard it relaxes to advisory exactly as a gate finding does — proving the fold reuses
              // the existing severity derivation rather than a new surface-specific rule.
              let strict, _ = runWithFindings Strict [ blockingSurfaceFinding ]
              let standard, _ = runWithFindings Standard [ blockingSurfaceFinding ]
              Expect.equal strict.Exit Loop.Blocked "Strict ⇒ Blocked"
              Expect.equal standard.Exit Loop.Success "Standard ⇒ relaxed to advisory ⇒ Success" } ]
