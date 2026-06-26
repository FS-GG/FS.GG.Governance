module FS.GG.Governance.VerifyCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T013/T014/T015 (US1) — the pure MVU transitions, the exit-code mapping, and the empty-selection
// "nothing to verify" short-circuit. No I/O: literal Model/Msg values, asserting both next state AND emitted
// effects (Principle IV). The verdict is the GENUINE `Ship.rollup` at `RunMode.Verify`.

let private hasEffect pred effs = effs |> List.exists pred

let private initFor scope = Loop.init (requestFor scope Loop.Text)

[<Tests>]
let tests =
    testList
        "Loop (US1)"
        [ test "init: DefaultRange emits SenseScope; ExplicitPaths senses the release preview before the catalog" {
              let _, effDefault = initFor Loop.DefaultRange
              Expect.isTrue (hasEffect (function Loop.SenseScope _ -> true | _ -> false) effDefault) "DefaultRange senses scope"

              let m, effExplicit = initFor (Loop.ExplicitPaths [ gp "src/a.fs" ])
              // F25 (064): provenance first. 065 (US3): the release preview is sensed BEFORE the catalog load
              // (SenseReleasePreview ⇒ ReleasePreviewSensed ⇒ LoadCatalog), so the preview is ready at projection.
              Expect.equal effExplicit [ Loop.SenseProvenance; Loop.SenseReleasePreview "." ] "ExplicitPaths senses the preview first"
              Expect.equal m.Candidates (Some [ gp "src/a.fs" ]) "candidates set from explicit paths"
          }

          test "Sensed(Ok) records candidates and senses the release preview (then the catalog)" {
              let m0, _ = initFor Loop.DefaultRange
              let snap = snapshotOf gitSrcChange defaultOpts
              let m1, eff = Loop.update (Loop.Sensed(Ok snap)) m0
              Expect.equal eff [ Loop.SenseReleasePreview "." ] "senses the release preview after git sensing"
              Expect.equal m1.Phase Loop.Sensed' "phase advanced"
              Expect.isSome m1.Candidates "candidates recorded"
              // 065 (US3): the catalog load follows the preview sense.
              let m2, eff2 = Loop.update (Loop.ReleasePreviewSensed None) m1
              Expect.equal eff2 [ Loop.LoadCatalog "." ] "ReleasePreviewSensed then loads the catalog"
              Expect.isNone m2.ReleasePreview "no declaration ⇒ no preview"
          }

          test "Loaded(Valid) runs the F015→F019 selection at RunMode.Verify and senses freshness + store" {
              let m0, _ = initFor Loop.DefaultRange
              let snap = snapshotOf gitSrcChange defaultOpts
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let candidates = m1.Candidates |> Option.defaultValue []
              let m2, eff = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1

              Expect.isTrue (hasEffect (function Loop.SenseFreshness _ -> true | _ -> false) eff) "senses freshness"
              Expect.isTrue (hasEffect (function Loop.LoadStore _ -> true | _ -> false) eff) "loads store"
              Expect.isFalse (hasEffect (function Loop.ExecuteGates _ -> true | _ -> false) eff) "no execute before sensing"
              Expect.isNonEmpty m2.SelectedGates "src change selects gates"

              // The decision threads Verify (NOT Gate): byte-equal to the genuine rollup at Verify/Standard.
              Expect.equal m2.Decision (Some(decisionOf validCatalog candidates Verify Standard)) "decision rolled at Verify"
          }

          test "exitCode maps the five categories" {
              Expect.equal (Loop.exitCode Loop.Success) 0 "success 0"
              Expect.equal (Loop.exitCode Loop.Blocked) 1 "blocked 1"
              Expect.equal (Loop.exitCode Loop.UsageError') 2 "usage 2"
              Expect.equal (Loop.exitCode Loop.InputUnavailable) 3 "input 3"
              Expect.equal (Loop.exitCode Loop.ToolError) 4 "tool 4"
          }

          test "empty selection short-circuits to a passing 'nothing to verify' verdict with no freshness/execute work" {
              // ExplicitPaths [] ⇒ no candidates ⇒ empty selection, no findings.
              let m0, _ = Loop.init (requestFor (Loop.ExplicitPaths []) Loop.Text)
              let m1, eff = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m0

              Expect.isEmpty m1.SelectedGates "no gates selected"
              Expect.isFalse (hasEffect (function Loop.SenseFreshness _ -> true | _ -> false) eff) "no freshness sense"
              Expect.isFalse (hasEffect (function Loop.ExecuteGates _ -> true | _ -> false) eff) "no execute"
              // 067: the empty-selection projection is deferred until the read-only surface checks land (so a
              // surface finding on a no-gate change can still be folded). `Loaded` senses surfaces; the write
              // is emitted on `SurfacesSensed`. validCatalog declares no product surface ⇒ `[]` ⇒ byte-identical.
              Expect.isTrue (hasEffect (function Loop.SenseSurfaces _ -> true | _ -> false) eff) "senses surfaces"
              Expect.isFalse (hasEffect (function Loop.WriteArtifact _ -> true | _ -> false) eff) "no write before surfaces land"

              let m1b, eff2 = Loop.update (Loop.SurfacesSensed []) m1
              Expect.isTrue (hasEffect (function Loop.WriteArtifact _ -> true | _ -> false) eff2) "writes verify.json once surfaces land"
              Expect.equal m1b.Phase Loop.Rolled "rolled"

              // F27 wiring (063): the render delegates to the shared HumanText projection; an empty selection
              // is a passing decision, rendered as "verdict: PASS" (the dedicated "nothing to verify" wording
              // was non-contractual host text).
              Expect.stringContains (Loop.render m1b Loop.Text) "verdict: PASS" "empty selection ⇒ passing verdict text"

              // On the write ack the summary is emitted and the terminal exit is Success.
              let m2, _ = Loop.update (Loop.Wrote(Loop.VerifyArtifact, Ok())) m1b
              let m3, _ = Loop.update Loop.Emitted m2
              Expect.equal m3.Exit Loop.Success "nothing to verify ⇒ exit Success" } ]
