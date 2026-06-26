module FS.GG.Governance.ReleaseCommand.Tests.EndToEndTests

open System.IO
open Expecto
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// End-to-end through `Interpreter.run` over a REAL temp-repo fixture and the REAL F053/F054/ReleaseJson
// cores (Principle V): the declaration is read from disk, the six families are sensed from real source
// files, evaluated verbatim, and the verdict/exit are asserted. Only the `Write`/`Out` edges are captured
// (no real console / atomic file write needed for the verdict).

let private run repo format =
    let written = ResizeArray<string * string>()
    let outs = ResizeArray<string>()
    let writer path content = written.Add(path, content); Ok()
    let ports = portsWith repo writer (fun s -> outs.Add s)

    let request =
        { Loop.Repo = repo
          Loop.Format = format
          Loop.ReleaseOut = Path.Combine(repo, "release.json")
          Loop.AttestationOut = Path.Combine(repo, "attestation.json") }

    Interpreter.run ports request, written, outs

let private decisionOf (m: Loop.Model) = m.Decision |> Option.defaultWith (fun () -> failtest "no decision")

[<Tests>]
let tests =
    testList
        "EndToEnd"
        [ test "compliant fixture ⇒ pass, six satisfied rules, Exit=Success (SC-001)" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let model, _, outs = run repo Loop.Text
                  let d = decisionOf model
                  Expect.equal model.Exit Loop.Success "exit success"
                  Expect.equal d.Verdict Pass "verdict pass"
                  Expect.equal (List.length d.Passing) 6 "all six satisfied/passing"
                  Expect.isEmpty d.Blockers "no blockers"
                  Expect.isEmpty d.Warnings "no warnings"
                  Expect.isNonEmpty outs "a summary was emitted")
          }

          test "un-bumped fixture ⇒ Blocked, version-bump blocker, five passing (SC-002/SC-006)" {
              withTempRepo releaseYmlAllBlocking writeUnbumpedSources (fun repo ->
                  let model, _, _ = run repo Loop.Text
                  let d = decisionOf model
                  Expect.equal model.Exit Loop.Blocked "exit blocked (distinct from 2/3/4)"
                  Expect.equal d.Verdict Fail "verdict fail"

                  let blockerKinds = d.Blockers |> List.map (fun e -> e.Finding.Kind)
                  Expect.equal blockerKinds [ VersionBump ] "version-bump is the sole blocker"
                  Expect.equal (List.length d.Passing) 5 "the other five pass"

                  // Six-family completeness (FR-013/SC-006).
                  let total = List.length d.Blockers + List.length d.Warnings + List.length d.Passing
                  Expect.equal total 6 "exactly six per-rule outcomes")
          }

          test "advisory-only-unmet ⇒ warning, not a blocker; verdict stays passing, Exit=Success (US1.4)" {
              withTempRepo releaseYmlProvenanceAdvisory writeMissingProvenanceSources (fun repo ->
                  let model, _, _ = run repo Loop.Text
                  let d = decisionOf model
                  Expect.equal model.Exit Loop.Success "advisory unmet does not block (exit 0)"
                  Expect.equal d.Verdict Pass "verdict still pass"
                  Expect.isEmpty d.Blockers "no blockers"

                  let warningKinds = d.Warnings |> List.map (fun e -> e.Finding.Kind)
                  Expect.equal warningKinds [ Provenance ] "the unmet advisory provenance rule is a warning"
                  Expect.equal (List.length d.Passing) 5 "the other five pass")
          }

          test "release writes both the release.json v2 projection and the attestation sidecar, and emits a summary" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let model, written, outs = run repo Loop.TextAndJson
                  Expect.equal model.Exit Loop.Success "exit success"
                  // 065: the publication boundary always writes BOTH artifacts (FR-004).
                  Expect.equal (written.Count) 2 "release.json v2 + attestation.json written"

                  let byPath = written |> Seq.map (fun (p, c) -> Path.GetFileName p, c) |> Map.ofSeq
                  let releaseDoc = byPath.["release.json"]
                  let attestationDoc = byPath.["attestation.json"]

                  Expect.isTrue (releaseDoc.Contains "\"verdict\":\"pass\"") "release.json carries the verdict"
                  Expect.isTrue (releaseDoc.Contains "fsgg.release/v2") "release.json bumped additively to v2"
                  Expect.isTrue (attestationDoc.Contains "fsgg.attestation/v1") "attestation.json is the v1 sidecar"
                  Expect.isNonEmpty outs "text summary on stdout for 'both'")
          } ]
