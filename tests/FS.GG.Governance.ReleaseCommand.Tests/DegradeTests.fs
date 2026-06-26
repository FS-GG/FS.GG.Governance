module FS.GG.Governance.ReleaseCommand.Tests.DegradeTests

open System.IO
open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// A removed governing source makes that family `Unrecoverable` ⇒ its rule unmet (NEVER satisfied), while
// the run still completes with a full six-family verdict and no crash (US3.2/SC-004/SC-006). Zero
// fabricated passes.

let private runText repo =
    let ports = portsWith repo (fun _ _ -> Ok()) ignore

    let request =
        { Loop.Repo = repo
          Loop.Format = Loop.Text
          Loop.ReleaseOut = Path.Combine(repo, "release.json")
          Loop.AttestationOut = Path.Combine(repo, "attestation.json") }

    Interpreter.run ports request

[<Tests>]
let tests =
    testList
        "Degrade"
        [ test "a missing source ⇒ that family unrecoverable/unmet, six-family verdict, no fabricated pass" {
              withTempRepo releaseYmlAllBlocking writeMissingProvenanceSources (fun repo ->
                  let model = runText repo
                  let d = model.Decision |> Option.defaultWith (fun () -> failtest "no decision")

                  // All six families are present in the verdict (FR-013/SC-006).
                  let total = List.length d.Blockers + List.length d.Warnings + List.length d.Passing
                  Expect.equal total 6 "exactly six per-rule outcomes"

                  // Provenance is a blocker (blocking + block-on-release, Violated via Unrecoverable) — and is
                  // NEVER in the passing set (no fabricated pass, SC-004).
                  let blockerKinds = d.Blockers |> List.map (fun e -> e.Finding.Kind)
                  let passingKinds = d.Passing |> List.map (fun e -> e.Finding.Kind)
                  Expect.contains blockerKinds Provenance "provenance blocks"
                  Expect.isFalse (List.contains Provenance passingKinds) "provenance never fabricated as a pass"

                  // The sensed fact state for provenance is Unrecoverable (never Met).
                  let sensed = model.Sensed |> Option.defaultWith (fun () -> failtest "no sensed")
                  Expect.equal (Release.factFor sensed.Facts Provenance) Unrecoverable "provenance unrecoverable"

                  // A sensing diagnostic names the affected family.
                  let diagFamilies = sensed.Snapshot.Diagnostics |> List.map (fun x -> x.Family)
                  Expect.contains diagFamilies Provenance "a diagnostic names provenance")
          } ]
