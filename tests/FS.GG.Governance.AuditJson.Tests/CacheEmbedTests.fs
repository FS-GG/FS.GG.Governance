module FS.GG.Governance.AuditJson.Tests.CacheEmbedTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

// F045 (US2/US3/US4) — the embedded cache-eligibility verdict on audit.json. Every report is a REAL
// `CacheEligibility.evaluate` roll-up over real `FreshnessInputs` and a real `ReuseStore`
// (`EvidenceReuse.record`); the `ShipDecision` is the genuine F024 `Ship.rollup` (Support.richDecision /
// decisionOf). No mocks of the cores (Principle V). The emitted bytes are inspected by a read-only
// JsonDocument parse. These tests FAIL before `AuditJson.fs` carries the verdict and pass after.

// richDecision's items: gates build:ship (blocker), build:rel (warning), docs:lint (passing); plus two
// finding items. We attribute verdicts to those gate ids.

let private baseInputs: FreshnessInputs =
    { Check = CheckId "ship"
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

let private shipInputs = baseInputs
let private relInputs = { baseInputs with Check = CheckId "rel"; Domain = DomainId "build" }
let private lintInputs = { baseInputs with Check = CheckId "lint"; Domain = DomainId "docs" }

let private refA = EvidenceRef "ev-A"
let private refR = EvidenceRef "ev-R"
let private refL = EvidenceRef "ev-L"

let private candidate (gate: string) (inputs: FreshnessInputs) : CandidateGate = { Gate = GateId gate; Inputs = inputs }

let private storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries |> List.fold (fun s (i, e) -> EvidenceReuse.record i e s) EvidenceReuse.empty

let private recordedStore = storeOf [ shipInputs, refA; relInputs, refR; lintInputs, refL ]

let private reportOf (cands: CandidateGate list) (store: ReuseStore) : CacheEligibilityReport =
    CacheEligibility.evaluate cands store

/// build:ship exact ⇒ Reusable ev-A; build:rel RuleHash moved ⇒ InputsChanged [ruleHash]; docs:lint is
/// ABSENT from the report ⇒ notEvaluated.
let private mixedReport =
    reportOf
        [ candidate "build:ship" shipInputs
          candidate "build:rel" { relInputs with RuleHash = RuleHash "r2" } ]
        recordedStore

let private noPriorReport = reportOf [ candidate "build:ship" shipInputs ] EvidenceReuse.empty

// ── read helpers over the emitted cache verdict ──

let private allItems (doc: JsonDocument) : JsonElement list =
    List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ]

let private gateItemById (doc: JsonDocument) (gid: string) : JsonElement =
    allItems doc |> List.find (fun it -> itemKind it = "gate" && itemId it = gid)

let private cacheOf (item: JsonElement) : JsonElement = item.GetProperty "cacheEligibility"
let private kindOf (verdict: JsonElement) : string = strField verdict "kind"
let private causeKind (verdict: JsonElement) : string = strField (verdict.GetProperty "cause") "kind"

let private causeCategories (verdict: JsonElement) : string list =
    [ for c in (verdict.GetProperty("cause").GetProperty "categories").EnumerateArray() ->
          match c.GetString() with
          | null -> failwith "null category"
          | s -> s ]

let private verdictFor (report: CacheEligibilityReport) (gid: string) : CacheEligibilityVerdict option =
    CacheEligibility.entries report
    |> List.tryPick (fun e -> if gateIdValue e.Gate = gid then Some e.Verdict else None)

let private sectionIds (doc: JsonDocument) (name: string) : string list =
    section doc name |> List.map itemId

[<Tests>]
let tests =
    testList
        "CacheEmbed (US2/US3/US4)"
        [
          // ── US2: each gate item carries its verdict matched by GateId, across sections ──

          test "a reusable gate item carries { kind:reusable, evidence:<ref> } verbatim (US2.1, SC-001)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              let v = cacheOf (gateItemById doc "build:ship")
              Expect.equal (kindOf v) "reusable" "build:ship reusable"
              Expect.equal (strField v "evidence") "ev-A" "evidence verbatim"
          }

          test "an inputsChanged gate item names its changed categories in report order (US2.1, SC-005)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              let v = cacheOf (gateItemById doc "build:rel")
              Expect.equal (kindOf v) "mustRecompute" "build:rel must recompute"
              Expect.equal (causeKind v) "inputsChanged" "cause inputsChanged"

              match verdictFor mixedReport "build:rel" with
              | Some(MustRecompute(InputsChanged cats)) ->
                  Expect.equal (causeCategories v) (cats |> List.map categoryToken) "categories verbatim, in report order"
              | other -> failtestf "expected build:rel InputsChanged in the report, got %A" other
          }

          test "a must-recompute (noPriorEvidence) gate item carries its cause, no evidence (US2.1)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some noPriorReport) [])
              let v = cacheOf (gateItemById doc "build:ship")
              Expect.equal (kindOf v) "mustRecompute" "build:ship must recompute"
              Expect.equal (causeKind v) "noPriorEvidence" "noPriorEvidence"
              Expect.isFalse (hasField v "evidence") "no evidence on mustRecompute (no-hide)"
          }

          test "a gate item absent from the report renders notEvaluated, never reusable (US2.3, L2)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              let v = cacheOf (gateItemById doc "docs:lint")
              Expect.equal (kindOf v) "notEvaluated" "docs:lint (absent) is notEvaluated"
              Expect.equal (fieldOrder v) [ "kind" ] "notEvaluated carries only kind"
          }

          test "every finding item carries NO cacheEligibility field (gate-scoped, US2.2, SC-002, L4)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              let findingItems = allItems doc |> List.filter (fun it -> itemKind it = "finding")
              Expect.isNonEmpty findingItems "richDecision has finding items"

              for f in findingItems do
                  Expect.isFalse (hasField f "cacheEligibility") "a finding item carries no cacheEligibility"
          }

          // ── US2.4 / L7: the cache verdict alters no verdict, severity, section, or ship outcome ──

          test "a reusable verdict on a base-blocking gate leaves it a blocker with full six-field enforcement (US2.4, L7, SC-004)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])

              // build:ship (BlockOnShip ⇒ blocking) is reusable, yet stays in blockers with full detail.
              Expect.contains (sectionIds doc "blockers") "build:ship" "reusable blocker stays in blockers"
              let item = gateItemById doc "build:ship"
              Expect.equal (kindOf (cacheOf item)) "reusable" "the verdict is reusable"
              Expect.equal
                  (fieldOrder (item.GetProperty "enforcement"))
                  [ "baseSeverity"; "maturity"; "mode"; "profile"; "effectiveSeverity"; "reason" ]
                  "all six enforcement fields intact"
              Expect.equal (enforcement item "effectiveSeverity") "blocking" "effective severity unchanged by the cache verdict"
          }

          test "additive: verdict / exitCodeBasis / sections / enforcement are byte-identical to the None projection (L7, FR-008)" {
              use docSome = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              use docNone = parse (AuditJson.ofShipDecision richDecision None [])

              Expect.equal (strField docSome.RootElement "verdict") (strField docNone.RootElement "verdict") "verdict unchanged"
              Expect.equal (strField docSome.RootElement "exitCodeBasis") (strField docNone.RootElement "exitCodeBasis") "exitCodeBasis unchanged"
              Expect.equal (strField docSome.RootElement "schemaVersion") (strField docNone.RootElement "schemaVersion") "schemaVersion unchanged"

              for name in [ "blockers"; "warnings"; "passing" ] do
                  for (its, itn) in List.zip (section docSome name) (section docNone name) do
                      Expect.equal (itemId its) (itemId itn) (sprintf "%s item identity unchanged" name)
                      // enforcement byte-identical; only cacheEligibility differs on gate items.
                      Expect.equal
                          ((its.GetProperty "enforcement").GetRawText())
                          ((itn.GetProperty "enforcement").GetRawText())
                          (sprintf "%s item enforcement byte-identical" name)
          }

          test "an orphan report entry (GateId matching no item) adds nothing (L5, FR-006)" {
              let orphan = reportOf [ candidate "ghost:gate" shipInputs ] recordedStore
              use docOrphan = parse (AuditJson.ofShipDecision richDecision (Some orphan) [])
              use docNone = parse (AuditJson.ofShipDecision richDecision None [])

              for name in [ "blockers"; "warnings"; "passing" ] do
                  Expect.equal (sectionIds docOrphan name) (sectionIds docNone name) (sprintf "%s unchanged by orphan" name)

              Expect.isFalse (allItems docOrphan |> List.exists (fun it -> hasField it "id" && itemId it = "ghost:gate")) "orphan id absent"
          }

          test "no raw freshness input value / extra cache-derived field leaks; evidence verbatim (L8, SC-007)" {
              let json = AuditJson.ofShipDecision richDecision (Some mixedReport) []
              let lower = json.ToLowerInvariant()
              // Category tokens (ruleHash) are the no-hide vocabulary, not raw inputs. The raw input
              // VALUES (RuleHash "r1"/"r2", ArtifactHash "h1", Revision "aaa"/"bbb", GeneratorVersion
              // "g1") must never appear — the projection computes no key/hash, dereferences nothing.
              for token in [ "\"r1\""; "\"r2\""; "\"h1\""; "\"aaa\""; "\"bbb\""; "\"g1\""; "freshnesskey" ] do
                  Expect.isFalse (lower.Contains token) (sprintf "no raw input value / excluded token %s leaks" token)

              use doc = parse json
              Expect.equal (strField (cacheOf (gateItemById doc "build:ship")) "evidence") "ev-A" "evidence verbatim, never dereferenced"
          }

          // ── US4: deterministic, versioned, ordered contract ──

          test "schemaVersion is fsgg.audit/v2 (US4.3, FR-013)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              Expect.equal (strField doc.RootElement "schemaVersion") "fsgg.audit/v2" "v2 contract"
              Expect.equal AuditJson.schemaVersion "fsgg.audit/v2" "constant is v2"
          }

          test "byte-identical for identical inputs (L10, SC-003)" {
              Expect.equal
                  (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
                  (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
                  "repeated projection byte-identical"
          }

          test "cache entries follow the document's existing composite item order (L10)" {
              use docSome = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              use docNone = parse (AuditJson.ofShipDecision richDecision None [])
              // the item order (and section placement) is the ShipDecision's, untouched by the embed.
              for name in [ "blockers"; "warnings"; "passing" ] do
                  Expect.equal (sectionIds docSome name) (sectionIds docNone name) (sprintf "%s order unchanged" name)
          }

          test "a duplicate GateId in the report resolves to the first entry by report order, deterministically (L6, FR-007)" {
              let dup =
                  CacheEligibility.evaluate
                      [ candidate "build:ship" shipInputs
                        candidate "build:ship" { shipInputs with RuleHash = RuleHash "moved" } ]
                      recordedStore

              let firstVerdict =
                  CacheEligibility.entries dup
                  |> List.find (fun e -> gateIdValue e.Gate = "build:ship")
                  |> fun e -> e.Verdict

              let expectedKind =
                  match firstVerdict with
                  | Reusable _ -> "reusable"
                  | MustRecompute _ -> "mustRecompute"

              use doc = parse (AuditJson.ofShipDecision richDecision (Some dup) [])
              Expect.equal (kindOf (cacheOf (gateItemById doc "build:ship"))) expectedKind "first-by-report-order verdict wins"
              Expect.equal (AuditJson.ofShipDecision richDecision (Some dup) []) (AuditJson.ofShipDecision richDecision (Some dup) []) "deterministic"
          }

          test "the None case is evaluated:false with every gate item notEvaluated; Some _ is evaluated:true (L2, L9)" {
              use docNone = parse (AuditJson.ofShipDecision richDecision None [])
              Expect.isFalse (docNone.RootElement.GetProperty("cacheEligibilityEvaluated").GetBoolean()) "evaluated false under None"

              for it in allItems docNone do
                  if itemKind it = "gate" then
                      Expect.equal (kindOf (cacheOf it)) "notEvaluated" "gate notEvaluated under None"

              use docSome = parse (AuditJson.ofShipDecision richDecision (Some mixedReport) [])
              Expect.isTrue (docSome.RootElement.GetProperty("cacheEligibilityEvaluated").GetBoolean()) "evaluated true under Some"
          }

          test "totality: None / empty report / clean empty decision each yield a document with the section (L11, SC-006)" {
              let cases =
                  [ "None", AuditJson.ofShipDecision richDecision None []
                    "empty report", AuditJson.ofShipDecision richDecision (Some(CacheEligibilityReport [])) []
                    "clean empty decision", AuditJson.ofShipDecision emptyCleanDecision (Some mixedReport) []
                    "empty report + clean decision", AuditJson.ofShipDecision emptyCleanDecision None [] ]

              for (label, json) in cases do
                  use doc = parse json
                  Expect.equal doc.RootElement.ValueKind JsonValueKind.Object (sprintf "%s yields a JSON object" label)
                  Expect.isTrue (hasField doc.RootElement "cacheEligibilityEvaluated") (sprintf "%s carries the cache section" label)
          }

          test "an evaluated-but-empty report is evaluated:true with every gate item notEvaluated (L9)" {
              use doc = parse (AuditJson.ofShipDecision richDecision (Some(CacheEligibilityReport [])) [])
              Expect.isTrue (doc.RootElement.GetProperty("cacheEligibilityEvaluated").GetBoolean()) "empty report is evaluated:true"

              for it in allItems doc do
                  if itemKind it = "gate" then
                      Expect.equal (kindOf (cacheOf it)) "notEvaluated" "gate notEvaluated under empty report"
          } ]
