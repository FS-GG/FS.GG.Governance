module FS.GG.Governance.VerifyJson.Tests.VerifyJsonTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.VerifyJson.Tests.Support

// T025 (US3) — the verify.json document shape (contracts/verify.schema.md). Every report/decision/outcome is
// a REAL upstream value (Principle V); the emitted bytes are inspected by a read-only JsonDocument parse.

let private allItems (doc: JsonDocument) : JsonElement list =
    List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ]

let private gateItemById (doc: JsonDocument) (gid: string) : JsonElement =
    allItems doc
    |> List.find (fun it ->
        let id = it.GetProperty "id"
        strField id "kind" = "gate" && strField id "gate" = gid)

[<Tests>]
let tests =
    testList
        "VerifyJson shape (US3)"
        [ test "schemaVersion is fsgg.verify/v1" {
              Expect.equal VerifyJson.schemaVersion "fsgg.verify/v1" "constant is v1"
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              Expect.equal (strField doc.RootElement "schemaVersion") "fsgg.verify/v1" "document schemaVersion"
          }

          test "top-level field order is fixed" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)

              Expect.equal
                  (topLevelFieldOrder doc)
                  [ "schemaVersion"; "verdict"; "exitCodeBasis"; "blockers"; "warnings"; "passing"; "currency" ]
                  "fixed top-level field order"
          }

          test "verdict is pass|blocked; a blocked decision renders blocked" {
              use docBlocked = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              Expect.equal (strField docBlocked.RootElement "verdict") "blocked" "rich decision blocks"
              Expect.equal (strField docBlocked.RootElement "exitCodeBasis") "blocked" "blocked basis"

              use docClean = parse (VerifyJson.ofVerifyDecision emptyCleanDecision None [])
              Expect.equal (strField docClean.RootElement "verdict") "pass" "empty decision passes"
              Expect.equal (strField docClean.RootElement "exitCodeBasis") "clean" "clean basis"
          }

          test "a gate item id is a tagged { kind:gate, gate } object" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              let item = gateItemById doc "build:ship"
              let id = item.GetProperty "id"
              Expect.equal (fieldOrder id) [ "kind"; "gate" ] "gate id field order"
              Expect.equal (strField id "kind") "gate" "kind gate"
              Expect.equal (strField id "gate") "build:ship" "gate id verbatim"
          }

          test "a finding item id is a tagged { kind:finding, finding, path } object" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)

              let findingItem =
                  allItems doc |> List.find (fun it -> strField (it.GetProperty "id") "kind" = "finding")

              let id = findingItem.GetProperty "id"
              Expect.equal (fieldOrder id) [ "kind"; "finding"; "path" ] "finding id field order"
              Expect.equal (strField id "kind") "finding" "kind finding"
          }

          test "each item carries id/enforcement/cache/execution in order; enforcement mode is verify" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              let item = gateItemById doc "build:ship"
              Expect.equal (fieldOrder item) [ "id"; "enforcement"; "cache"; "execution" ] "item field order"

              Expect.equal
                  (fieldOrder (item.GetProperty "enforcement"))
                  [ "baseSeverity"; "maturity"; "mode"; "profile"; "effectiveSeverity"; "reason" ]
                  "enforcement field order"

              Expect.equal (strField (item.GetProperty "enforcement") "mode") "verify" "mode is always verify"
          }

          test "a reusable gate carries { kind:reusable, evidence } cache verbatim" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              let cache = (gateItemById doc "build:ship").GetProperty "cache"
              Expect.equal (strField cache "kind") "reusable" "reusable"
              Expect.equal (strField cache "evidence") "ev-A" "evidence verbatim"
          }

          test "an inputsChanged gate carries { kind:mustRecompute, cause:{ kind:inputsChanged, categories } }" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              let cache = (gateItemById doc "build:rel").GetProperty "cache"
              Expect.equal (strField cache "kind") "mustRecompute" "mustRecompute"
              let cause = cache.GetProperty "cause"
              Expect.equal (strField cause "kind") "inputsChanged" "inputsChanged"
              Expect.equal cause.ValueKind JsonValueKind.Object "inputsChanged cause is an object"
          }

          test "a noPriorEvidence cause is the bare string \"noPriorEvidence\" (not an object)" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some noPriorReport) mixedOutcomes)
              let cause = ((gateItemById doc "build:ship").GetProperty "cache").GetProperty "cause"
              Expect.equal cause.ValueKind JsonValueKind.String "noPriorEvidence is a bare string"
              Expect.equal (cause.GetString()) "noPriorEvidence" "noPriorEvidence token"
          }

          test "an unevaluated gate and every finding carry cache:null" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              Expect.equal ((gateItemById doc "docs:lint").GetProperty("cache")).ValueKind JsonValueKind.Null "docs:lint cache null"

              let findingItem =
                  allItems doc |> List.find (fun it -> strField (it.GetProperty "id") "kind" = "finding")

              Expect.equal (findingItem.GetProperty("cache")).ValueKind JsonValueKind.Null "finding cache null"
          }

          test "execution carries disposition/exitCode/passed; a not-executed gate uses null exitCode/passed" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              let exec = (gateItemById doc "build:ship").GetProperty "execution"
              Expect.equal (fieldOrder exec) [ "disposition"; "exitCode"; "passed" ] "execution field order"
              Expect.equal (strField exec "disposition") "reused" "reused disposition"
              Expect.equal (exec.GetProperty("exitCode").GetInt32()) 0 "exit 0"
              Expect.isTrue (exec.GetProperty("passed").GetBoolean()) "passed true"

              let execLint = (gateItemById doc "docs:lint").GetProperty "execution"
              Expect.equal (strField execLint "disposition") "not-executed" "not-executed"
              Expect.equal (execLint.GetProperty "exitCode").ValueKind JsonValueKind.Null "null exitCode"
              Expect.equal (execLint.GetProperty "passed").ValueKind JsonValueKind.Null "null passed"
          }

          test "currency has fresh/recomputed/unresolved arrays in order" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              let cur = doc.RootElement.GetProperty "currency"
              Expect.equal (fieldOrder cur) [ "fresh"; "recomputed"; "unresolved" ] "currency field order"

              let fresh = currency doc "fresh"
              Expect.equal (List.length fresh) 1 "one fresh entry (build:ship)"
              Expect.equal (strField fresh.Head "gate") "build:ship" "fresh gate"
              Expect.equal (strField fresh.Head "evidence") "ev-A" "fresh evidence"

              let recomputed = currency doc "recomputed"
              Expect.equal (List.length recomputed) 1 "one recomputed entry (build:rel)"
              Expect.equal (strField recomputed.Head "gate") "build:rel" "recomputed gate"
              Expect.equal ((recomputed.Head.GetProperty("cause").GetProperty "kind").GetString()) "inputsChanged" "recomputed inputsChanged"

              // docs:lint is a selected gate item absent from the cache report ⇒ unresolved with missing:[].
              let unresolved = currency doc "unresolved"
              Expect.contains (unresolved |> List.map (fun e -> strField e "gate")) "docs:lint" "docs:lint unresolved"
              let lint = unresolved |> List.find (fun e -> strField e "gate" = "docs:lint")
              Expect.equal (fieldOrder lint) [ "gate"; "missing" ] "unresolved entry shape"
              Expect.equal (lint.GetProperty("missing").ValueKind) JsonValueKind.Array "missing is a present array"
          }

          test "recomputed.cause for noPriorEvidence is the bare string" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some noPriorReport) mixedOutcomes)
              let recomputed = currency doc "recomputed"
              let ship = recomputed |> List.find (fun e -> strField e "gate" = "build:ship")
              Expect.equal (ship.GetProperty("cause").ValueKind) JsonValueKind.String "bare string cause"
              Expect.equal (ship.GetProperty("cause").GetString()) "noPriorEvidence" "noPriorEvidence" } ]
