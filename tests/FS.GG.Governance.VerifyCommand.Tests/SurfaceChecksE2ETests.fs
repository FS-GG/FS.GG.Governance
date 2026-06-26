module FS.GG.Governance.VerifyCommand.Tests.SurfaceChecksE2ETests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// 067 (F24 verify-host wiring) — the end-to-end proofs that `fsgg verify` now classifies → senses → runs the
// product-surface checks, folds a blocking finding into the verdict, and projects the additive `surfaceChecks`
// section. The surface SENSORS run for real over a real temp tree (the feature under test — real package/docs/
// skill/design file reads through the READ-ONLY package port); git is faked with FIXED revisions and the gate
// EXEC is faked (the established pattern in this suite) so the rest of `verify.json` is byte-deterministic —
// commit SHAs from a real `git` would otherwise vary run-to-run. Synthetic inputs (the advisory finding the
// real sensors cannot yet emit from disk) are disclosed at their use site (Constitution V).

let private srcCandidate = "src/Api.fsi"

// Deterministic ports: the in-memory `catalog` drives classification (so `update` sees the declared product
// surface); FIXED-SHA faked git reports `changed` as the routed path; the REAL surface sense reads `dir`'s
// files; the gate exec is `exec`; writes/stdout are captured. Real sensors, faked git/exec — disclosed.
let private detPorts (catalog) (dir: string) (changed: string) (exec) (cap: Capture) : Interpreter.Ports =
    { fakePortsExec catalog (gitWithChanges [ 'M', changed ]) fakeSensor absentStoreReader exec cap with
        SenseSurfaces = realSurfaceSense dir }

let private goldenDir =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.VerifyCommand.Tests", "goldens")

// Compare against a frozen golden; `BLESS_GOLDEN=1` (re)writes it. A missing golden fails loudly (never a
// silent self-fulfilling pass) unless blessing.
let private goldenAssert (name: string) (actual: string) =
    let path = Path.Combine(goldenDir, name)
    if Environment.GetEnvironmentVariable "BLESS_GOLDEN" = "1" then
        File.WriteAllText(path, actual)
    Expect.isTrue (File.Exists path) (sprintf "golden %s exists (run BLESS_GOLDEN=1 dotnet test to mint it)" name)
    Expect.equal actual (File.ReadAllText path) (sprintf "byte-identical to golden %s (BLESS_GOLDEN=1 to refresh)" name)

let private contentOf (cap: Capture) : string =
    match writtenVerify cap with
    | Some(_, c) -> c
    | None -> failtest "expected a verify.json write"

[<Tests>]
let tests =
    testList
        "SurfaceChecksE2E"
        [
          // ── US1 (T004 / SC-001): a drifted package surface blocks `fsgg verify` ──
          test "T004 drifted package surface ⇒ surfaceChecks package.baseline-drift, blocking exit, evidence tag, no leakage" {
              withDriftedPackageRepo (fun dir ->
                  let cap = newCapture ()
                  let model = Interpreter.run (detPorts surfaceCatalog dir srcCandidate fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  // The build gate passes (faked exit 0) ⇒ the ONLY blocker is the surface finding.
                  Expect.equal model.Exit Loop.Blocked "drifted surface ⇒ Blocked at Verify under Strict"
                  Expect.equal (Loop.exitCode model.Exit) 1 "exit 1 (distinct from tool errors)"

                  let content = contentOf cap
                  Expect.stringContains content "surfaceChecks" "the additive section is emitted"
                  Expect.stringContains content "package.baseline-drift" "the drift finding is reported"
                  Expect.stringContains content "api-contract" "the declared evidenceTag is carried"
                  // FR-006: no absolute path / temp-dir leakage in the emitted bytes.
                  Expect.isFalse (content.Contains dir) "no absolute repo path leaks into surfaceChecks"
                  Expect.isFalse (content.Contains(Path.GetTempPath())) "no temp path leaks") }

          // ── US2 (T005 / SC-002): no declared surface ⇒ byte-identical, section omitted ──
          test "T005 no declared product surface ⇒ verify.json byte-identical to the pre-wiring golden, no surfaceChecks" {
              // The no-surface catalog (its only surface is `protected`) ⇒ the real sense returns [] regardless
              // of the tree, so the default empty sense + FIXED-SHA faked git give a stable anchor.
              let cap = newCapture ()
              let candidates = [ gp "src/Lib/Thing.fs" ]
              let model = Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortPass cap) (requestFor (Loop.ExplicitPaths candidates) Loop.Text)
              let content = contentOf cap
              Expect.equal model.Exit Loop.Success "no surface, passing gates ⇒ Success"
              Expect.isFalse (content.Contains "surfaceChecks") "no surfaceChecks section when there are no findings"
              // Independent no-regression anchor: equals the genuine pre-wiring projection of the same inputs
              // (ExplicitPaths senses no snapshot ⇒ baseHead None, matching the actual run).
              Expect.equal content (verifyExpectedWith fakeExecPortPass validCatalog candidates Standard None) "byte-identical to the genuine VerifyJson.ofVerifyDecision projection"
              goldenAssert "verify-no-surfaces.json" content }

          // ── US3 (T006 / SC-003): an advisory-only finding surfaces without escalating ──
          test "T006 advisory-only surface finding ⇒ surfaceChecks advisory entry, exit equals a clean run (Synthetic)" {
              // SYNTHETIC: the advisory finding is injected through the surface-sense port — the real disk
              // sensors emit only Blocking findings today (the lone Advisory check, docs.example-freshness, the
              // real docs sensor does not yet populate). The fold + projection are exercised for real.
              let runWith findings =
                  let cap = newCapture ()
                  let ports =
                      { fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortPass cap with
                          SenseSurfaces = syntheticSurfaceSense findings }
                  Interpreter.run ports (requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text), cap

              let clean, _ = runWith []
              let advised, capA = runWith [ advisorySurfaceFinding ]
              let content = contentOf capA
              Expect.stringContains content "surfaceChecks" "the advisory finding is visible in surfaceChecks"
              Expect.stringContains content "advisory" "carried with advisory severity"
              Expect.equal advised.Exit clean.Exit "advisory never changes the exit code"
              Expect.equal advised.Exit Loop.Success "advisory-only ⇒ Success" }

          // ── US1 (T008 / SC-004): determinism + the absent-baseline read-only case ──
          test "T008 re-running over unchanged inputs ⇒ byte-identical verify.json (deterministic)" {
              withDriftedPackageRepo (fun dir ->
                  let run () =
                      let cap = newCapture ()
                      Interpreter.run (detPorts surfaceCatalog dir srcCandidate fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                      |> ignore
                      contentOf cap

                  Expect.equal (run ()) (run ()) "two runs over unchanged inputs are byte-identical") }

          test "T008b absent baseline ⇒ two runs byte-identical and the working tree is unchanged (read-only)" {
              withAbsentBaselineRepo (fun dir ->
                  let baselineFile = Path.Combine(dir, "src", "Api.fsi.baseline")
                  let run () =
                      let cap = newCapture ()
                      Interpreter.run (detPorts surfaceCatalogNoGates dir srcCandidate fakeExecPortFail cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                      |> ignore
                      contentOf cap

                  let first = run ()
                  Expect.isFalse (File.Exists baselineFile) "the read-only port never writes the absent baseline"
                  let second = run ()
                  Expect.equal first second "absent-baseline runs are byte-identical (no first-run-writes divergence)"
                  Expect.stringContains first "package.baseline-absent" "the absent baseline is reported (blocking)") }

          // ── US2 (T009b / FR-012): read-only, no working-tree write, no spawned process ──
          test "T009b read-only verify ⇒ no .baseline written and no process spawned by surface sensing" {
              withAbsentBaselineRepo (fun dir ->
                  let baselineFile = Path.Combine(dir, "src", "Api.fsi.baseline")
                  let counter = { Calls = 0 }
                  let cap = newCapture ()
                  // No gates in this catalog, so the ONLY thing that could spawn a process is a transcript run —
                  // which the read-only package port suppresses (ListTranscripts ⇒ Ok []).
                  let model = Interpreter.run (detPorts surfaceCatalogNoGates dir srcCandidate (countingExecPort counter 1) cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  Expect.equal counter.Calls 0 "the read-only package port spawns no process (no transcript executed)"
                  Expect.isFalse (File.Exists baselineFile) "no .baseline is written to the working tree"
                  Expect.equal model.Exit Loop.Blocked "the package.baseline-absent finding still blocks under Strict"
                  Expect.stringContains (contentOf cap) "package.baseline-absent" "the absent baseline is reported, written nowhere") }

          // ── US3 (T009 / FR-010): safe failure on an unreadable / missing surface source ──
          test "T009 a routed-but-missing surface source ⇒ a disclosed sensor outcome, not a crash or silent pass" {
              withDriftedPackageRepo (fun dir ->
                  let cap = newCapture ()
                  // Faked git reports a GHOST `.fsi` that is not on disk — the real package sensor surfaces a
                  // disclosed `package.baseline-unreadable` input-state finding (FR-010), never a crash/silent pass.
                  let model = Interpreter.run (detPorts surfaceCatalog dir "src/Ghost.fsi" fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  let content = contentOf cap
                  Expect.stringContains content "package.baseline-unreadable" "a missing source is a disclosed sensor outcome"
                  Expect.equal model.Exit Loop.Blocked "the disclosed input-state finding blocks under Strict (not a silent pass)") }

          // ── T020 / contract C2: the non-empty surfaceChecks projection is frozen byte-identically ──
          test "T020 non-empty surfaceChecks projection is deterministic and byte-identical to the golden" {
              withDriftedPackageRepo (fun dir ->
                  let cap = newCapture ()
                  Interpreter.run (detPorts surfaceCatalog dir srcCandidate fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  |> ignore
                  goldenAssert "verify-surfacechecks.json" (contentOf cap)) }
        ]
