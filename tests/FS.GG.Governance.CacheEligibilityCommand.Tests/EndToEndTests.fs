module FS.GG.Governance.CacheEligibilityCommand.Tests.EndToEndTests

open System.IO
open System.Text.Json
open Expecto
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T028 (Principle V, SC-001/SC-002/SC-004, L13) — the ONE real proof: a real temp git repo + real `.fsgg`
// catalog + a real on-disk `fsgg.evidence-reuse-store/v1` whose newest matching entry makes a selected gate
// reusable + `Interpreter.realPorts` (real git, real BCL-crypto FreshnessSensor, real store reader, atomic
// write). Both artifacts validate against their schemas; the prepared gate is reusable over the REAL
// StoreReader+evaluate path (not faked); the artifacts are byte-identical on a re-run.

let private schemaOf (text: string) =
    use doc = JsonDocument.Parse text
    jsonProp doc.RootElement "schemaVersion"

[<Tests>]
let tests =
    testList
        "EndToEnd"
        [ test "real repo + real store ⇒ schema-valid artifacts, a reusable gate, byte-identical re-run" {
              withTempRepo (fun dir ->
                  let ports = Interpreter.realPorts dir
                  // DefaultRange over a clean working tree senses nothing; `--since HEAD~1` senses the
                  // committed src edit (the RouteCommand e2e precedent).
                  let snap = FS.GG.Governance.Snapshot.Interpreter.senseSnapshot ports.Git (sinceOpts "HEAD~1")
                  let baseHead = baseHeadOfSnapshot snap
                  let candidates = snap.Changed |> List.map (fun c -> c.Path)
                  let gates = selectedGatesOf validCatalog candidates

                  Expect.isNonEmpty gates "the committed src edit selects gates"

                  // Assemble the SensedFacts the real sensor produces, resolve, and prepare a store entry from
                  // a genuinely-resolved gate's inputs (so the match is real, never fabricated).
                  let sensed = assembleSensed ports.Freshness gates baseHead
                  let resolved = FreshnessResolution.resolve gates sensed |> FreshnessResolution.entries |> List.choose FreshnessResolution.candidate

                  Expect.isNonEmpty resolved "the real sensor fully senses at least one gate ⇒ it resolves"

                  let store =
                      resolved
                      |> List.truncate 1
                      |> List.map (fun c -> { Inputs = c.Inputs; Evidence = EvidenceRef "ev-e2e" })
                      |> ReuseStore

                  writeFile dir "readiness/evidence-reuse.json" (serializeStore store)

                  let req =
                      { requestFor (Loop.Since "HEAD~1") Loop.Human with
                          Repo = dir
                          StorePath = Path.Combine(dir, "readiness/evidence-reuse.json")
                          CacheOut = Path.Combine(dir, "readiness/cache-eligibility.json")
                          UnresolvedOut = Path.Combine(dir, "readiness/cache-eligibility.unresolved.json") }

                  let model = Interpreter.run ports req
                  Expect.equal model.Exit Loop.Success "exit 0"

                  let cacheText = File.ReadAllText req.CacheOut
                  let sideText = File.ReadAllText req.UnresolvedOut

                  Expect.equal (schemaOf cacheText) "fsgg.cache-eligibility/v1" "cache-eligibility.json schema id"
                  Expect.equal (schemaOf sideText) Loop.unresolvedSchemaVersion "sidecar schema id"
                  Expect.stringContains cacheText "reusable" "the prepared gate is reusable over the real path (L13)"
                  Expect.stringContains cacheText "ev-e2e" "carries the real evidence reference"

                  // Re-run over the same state ⇒ byte-identical artifacts (determinism, SC-004).
                  let model2 = Interpreter.run ports req
                  Expect.equal model2.Exit Loop.Success "re-run exit 0"
                  Expect.equal (File.ReadAllText req.CacheOut) cacheText "cache-eligibility.json byte-identical on re-run"
                  Expect.equal (File.ReadAllText req.UnresolvedOut) sideText "sidecar byte-identical on re-run")
          } ]
