module FS.GG.Governance.CacheEligibilityJson.Tests.DeterminismTests

open System
open System.IO
open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.CacheEligibilityJson.Tests.Support

// US2 (SC-002, SC-003, L-R7/L-T2/L-T3): the document is a CONTRACT — byte-identical for identical (and
// value-equal) reports, version-stamped, and stably ordered. The field/collection order is part of the
// contract and is asserted on the RAW text key positions, not just the parsed tree.

/// The index of a key's first occurrence in the raw document text (its quoted form), or -1.
let private keyPos (doc: string) (key: string) : int = doc.IndexOf("\"" + key + "\"", StringComparison.Ordinal)

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig fscheckConfig "byte-for-byte determinism: ofReport r = ofReport r (L-T2)" (fun (r: CacheEligibilityReport) ->
              CacheEligibilityJson.ofReport r = CacheEligibilityJson.ofReport r)

          test "purity: identical text under a changed working directory + filesystem state (L-T2)" {
              // No I/O is performed, so changing the cwd / touching unrelated files between calls cannot move
              // the output (the F041 purity-check precedent).
              let r = inputsChangedReport
              let before = CacheEligibilityJson.ofReport r
              let original = Directory.GetCurrentDirectory()
              let tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
              Directory.CreateDirectory tmp |> ignore

              try
                  Directory.SetCurrentDirectory tmp
                  File.WriteAllText(Path.Combine(tmp, "noise.txt"), "unrelated")
                  let after = CacheEligibilityJson.ofReport r
                  Expect.equal after before "output is independent of cwd / filesystem state"
              finally
                  Directory.SetCurrentDirectory original
                  try Directory.Delete(tmp, true) with _ -> ()
          }

          test "order-independence at the source: any permutation projects byte-identically (L-T3)" {
              // candidates z:a, a:b, a:a ⇒ entries ordered a:a, a:b, z:a, regardless of supply order.
              let canonical = CacheEligibilityJson.ofReport (orderingReport [ "z", "a"; "a", "b"; "a", "a" ])
              let perms =
                  [ [ "a", "a"; "a", "b"; "z", "a" ]
                    [ "a", "b"; "a", "a"; "z", "a" ]
                    [ "z", "a"; "a", "a"; "a", "b" ] ]

              for p in perms do
                  Expect.equal (CacheEligibilityJson.ofReport (orderingReport p)) canonical "byte-identical for any permutation"

              use doc = parse canonical
              Expect.equal (entriesOf doc |> List.map entryGate) [ "a:a"; "a:b"; "z:a" ] "entries in GateId-ordinal order"
          }

          testPropertyWithConfig fscheckConfig "versioned schema present + equals the constant (L-V1)" (fun (r: CacheEligibilityReport) ->
              use doc = parse (CacheEligibilityJson.ofReport r)
              docSchemaVersion doc = "fsgg.cache-eligibility/v1"
              && docSchemaVersion doc = CacheEligibilityJson.schemaVersion)

          test "schemaVersion constant is the fixed literal (L-V1)" {
              Expect.equal CacheEligibilityJson.schemaVersion "fsgg.cache-eligibility/v1" "fixed contract-version constant"
          }

          test "stable field order on the raw text: top-level, entry, verdict, cause (FR-007)" {
              // A mixed report exercising every nested shape (reusable + both cause kinds).
              let moved = baseInputs |> (fun i -> { i with RuleHash = FS.GG.Governance.FreshnessKey.Model.RuleHash "r2" })
              let r =
                  report
                      [ candidate (gid "docs" "lint") baseInputs // reusable
                        candidate (gid "security" "scan") baseInputs // noPriorEvidence
                        candidate (gid "build" "tests") moved ] // inputsChanged
                      (storeOf [ baseInputs, refA ])
              let text = CacheEligibilityJson.ofReport r
              use doc = parse text

              // top-level: schemaVersion before entries
              Expect.equal (topLevelFieldOrder doc) [ "schemaVersion"; "entries" ] "top-level field order"
              Expect.isLessThan (keyPos text "schemaVersion") (keyPos text "entries") "schemaVersion precedes entries in raw text"

              // per entry: gate before verdict; per verdict: kind before payload; per cause: kind before categories
              for e in entriesOf doc do
                  Expect.equal (fieldOrder e) [ "gate"; "verdict" ] "entry field order gate, verdict"
                  let v = entryVerdict e
                  match verdictKind v with
                  | "reusable" -> Expect.equal (fieldOrder v) [ "kind"; "evidence" ] "reusable verdict order"
                  | "mustRecompute" ->
                      Expect.equal (fieldOrder v) [ "kind"; "cause" ] "mustRecompute verdict order"
                      let c = verdictCause v
                      match causeKind c with
                      | "inputsChanged" -> Expect.equal (fieldOrder c) [ "kind"; "categories" ] "inputsChanged cause order"
                      | _ -> Expect.equal (fieldOrder c) [ "kind" ] "noPriorEvidence cause order"
                  | other -> failtestf "unknown verdict kind %s" other
          } ]
