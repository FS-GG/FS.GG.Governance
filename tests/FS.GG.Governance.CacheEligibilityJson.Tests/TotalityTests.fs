module FS.GG.Governance.CacheEligibilityJson.Tests.TotalityTests

open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.CacheEligibilityJson.Tests.Support

// US4 (SC-006, L-T1/L-R8/L-R9): `ofReport` succeeds for every well-typed report — empty, all-reusable,
// all-must-recompute, mixed, duplicate-`GateId` — returning a valid, parseable document and never throwing.

[<Tests>]
let tests =
    testList
        "Totality"
        [ testPropertyWithConfig fscheckConfig "totality: every well-typed report renders to a parseable document, never throws (L-T1)" (fun (r: CacheEligibilityReport) ->
              let text = CacheEligibilityJson.ofReport r // must not throw
              use doc = parse text // must parse
              docSchemaVersion doc = CacheEligibilityJson.schemaVersion
              && (entriesOf doc |> List.length) = (CacheEligibility.entries r |> List.length))

          test "empty report ⇒ present, empty entries array — a success, not a placeholder (L-R9)" {
              use doc = parse (CacheEligibilityJson.ofReport (report [] EvidenceReuse.empty))
              Expect.equal (docSchemaVersion doc) "fsgg.cache-eligibility/v1" "schemaVersion present"
              Expect.equal (topLevelFieldOrder doc) [ "schemaVersion"; "entries" ] "entries present even when empty"
              Expect.isEmpty (entriesOf doc) "entries is the empty array — no by-default placeholder entry"
          }

          test "all-reusable report renders every entry with its verdict" {
              let store = storeOf [ baseInputs, refA ]
              let r = report [ candidate (gid "a" "x") baseInputs; candidate (gid "b" "y") baseInputs ] store
              use doc = parse (CacheEligibilityJson.ofReport r)
              let vs = entriesOf doc |> List.map (entryVerdict >> verdictKind)
              Expect.equal vs [ "reusable"; "reusable" ] "both entries reusable, valid document"
          }

          test "all-must-recompute report renders every entry with its verdict" {
              let r = report [ candidate (gid "a" "x") baseInputs; candidate (gid "b" "y") baseInputs ] EvidenceReuse.empty
              use doc = parse (CacheEligibilityJson.ofReport r)
              let vs = entriesOf doc |> List.map (entryVerdict >> verdictKind)
              Expect.equal vs [ "mustRecompute"; "mustRecompute" ] "both entries mustRecompute, valid document"
          }

          test "duplicate GateId kept — two entries under build:tests, neither merged nor deduplicated (L-R8)" {
              use doc = parse (CacheEligibilityJson.ofReport duplicateReport)
              let entries = entriesOf doc
              Expect.equal entries.Length 2 "two distinct entries"
              Expect.equal (entries |> List.map entryGate) [ "build:tests"; "build:tests" ] "both under the same gate id, in report order"
          } ]
